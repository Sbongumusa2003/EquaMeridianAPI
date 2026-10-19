using EquaMeridian.DTOs.Cart;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class CartRepository : ICartRepository
{
    private readonly AppDbContext _db;
    private readonly IPricingEngine _pricingEngine;
    private readonly IDeliveryDistanceService _distanceService;
    private readonly IInvoiceRepository _invoices;
    private readonly ILeaseAgreementRepository _leaseAgreements;
    private readonly ICampaignRepository _campaigns;

    public CartRepository(
        AppDbContext db, IPricingEngine pricingEngine, IDeliveryDistanceService distanceService,
        IInvoiceRepository invoices, ILeaseAgreementRepository leaseAgreements, ICampaignRepository campaigns)
    {
        _db = db;
        _pricingEngine = pricingEngine;
        _distanceService = distanceService;
        _invoices = invoices;
        _leaseAgreements = leaseAgreements;
        _campaigns = campaigns;
    }

    public async Task<CartDto> GetCartAsync(int contractorId, string? promoCode = null)
    {
        var items = await _db.CartItems
            .Include(c => c.Listing).ThenInclude(l => l.Supplier)
            .Where(c => c.ContractorID == contractorId)
            .OrderBy(c => c.AddedDate)
            .ToListAsync();
        PromoCodeResolution? codeResolution = null;
        if (!string.IsNullOrWhiteSpace(promoCode))
            codeResolution = await _campaigns.ResolveDiscountCodeAsync(promoCode);

        // Soft oversell awareness: subtract qty on bookings still awaiting dual lease signature.
        // Advisory only — cart add is not hard-blocked by this.
        var listingIds = items.Select(i => i.ListingID).Distinct().ToList();
        var pendingByListing = await _db.Bookings.AsNoTracking()
            .Where(b => listingIds.Contains(b.ListingID) && b.Status == "AwaitingSignature")
            .GroupBy(b => b.ListingID)
            .Select(g => new { ListingID = g.Key, Qty = g.Sum(b => b.Quantity) })
            .ToDictionaryAsync(x => x.ListingID, x => x.Qty);

        var itemDtos = new List<(CartItemDto Dto, int SupplierId, string SupplierName)>();
        foreach (var item in items)
        {
            var days = HireDuration.InclusiveDays(item.RentalStartDate, item.RentalEndDate);
            var isPickup = IsContractorPickup(item.DeliveryAddress, item.Listing.Location);
            var distanceKm = isPickup
                ? 0m
                : await _distanceService.CalculateDistanceKmAsync(item.Listing.Location, item.DeliveryAddress);
            var listingPromo = await _campaigns.GetActivePromoDiscountPercentAsync(item.ListingID);
            var codePromo = 0m;
            if (codeResolution is { Valid: true })
            {
                if (!codeResolution.FeaturedListingId.HasValue
                    || codeResolution.FeaturedListingId.Value == item.ListingID)
                    codePromo = codeResolution.DiscountPercent;
            }
            var promoPercent = Math.Max(listingPromo, codePromo);
            var chargeDelivery = !isPickup && item.Listing.DeliveryAvailable;
            var flatFee = chargeDelivery && item.Listing.DeliveryFeeZAR is > 0
                ? item.Listing.DeliveryFeeZAR
                : null;
            var pricing = await _pricingEngine.CalculateAsync(
                item.Listing.DailyRateZAR, days, item.Quantity, distanceKm, item.Listing.CategoryID, promoPercent,
                chargeDelivery: chargeDelivery,
                listingFlatDeliveryFee: flatFee);

            var image = await _db.ListingImages
                .Where(i => i.ListingID == item.ListingID)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.FilePath)
                .FirstOrDefaultAsync();

            var dto = new CartItemDto
            {
                CartItemID = item.CartItemID,
                ListingID = item.ListingID,
                ListingTitle = item.Listing.ListingTitle,
                ImageUrl = image,
                // Stock is not held in cart — committed only when both parties sign the lease.
                // EffectiveAvailable subtracts other bookings still awaiting dual signature (advisory).
                UnitsAvailable = ListingInventory.EffectiveAvailable(
                    item.Listing.UnitsAvailable,
                    pendingByListing.GetValueOrDefault(item.ListingID)),
                Quantity = item.Quantity,
                RentalStartDate = item.RentalStartDate,
                RentalEndDate = item.RentalEndDate,
                RentalDurationDays = days,
                DeliveryAddress = item.DeliveryAddress,
                FulfillmentMethod = isPickup ? "Contractor Pickup" : "Supplier Delivery",
                DailyRateZAR = item.Listing.DailyRateZAR,
                RentalSubtotal = pricing.RentalSubtotal,
                DiscountPercent = pricing.DiscountPercent,
                DiscountAmount = pricing.DiscountAmount,
                TierDiscountPercent = pricing.TierDiscountPercent,
                ItemPromoDiscountPercent = pricing.PromoDiscountPercent,
                DeliveryFee = pricing.DeliveryFee,
                DeliveryDistanceKm = distanceKm,
                PriceExclVat = pricing.PriceExclVat,
                VatRate = pricing.VatRate,
                PriceInclVat = pricing.PriceInclVat,
                IsAvailable = item.Listing.AvailabilityStatus == "Active"
                              && item.Listing.PricingMode == "Fixed"
                              && ListingInventory.EffectiveAvailable(
                                     item.Listing.UnitsAvailable,
                                     pendingByListing.GetValueOrDefault(item.ListingID)) >= item.Quantity
            };

            itemDtos.Add((dto, item.Listing.SupplierID, item.Listing.Supplier.CompanyName ?? item.Listing.Supplier.FullName));
        }

        // One delivery charge per unique (supplier, destination) — not per cart line.
        ConsolidateCartDeliveryFees(itemDtos);

        var groups = itemDtos
            .GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(g => new CartSupplierGroupDto
            {
                SupplierID = g.Key.SupplierId,
                SupplierCompany = g.Key.SupplierName,
                Items = g.Select(x => x.Dto).ToList(),
                GroupRentalSubtotal = g.Sum(x => x.Dto.RentalSubtotal),
                GroupDiscountAmount = g.Sum(x => x.Dto.DiscountAmount),
                GroupDeliveryFee = g.Sum(x => x.Dto.DeliveryFee),
                GroupPriceExclVat = g.Sum(x => x.Dto.PriceExclVat),
                GroupVatAmount = g.Sum(x => x.Dto.PriceInclVat - x.Dto.PriceExclVat),
                GroupTotal = g.Sum(x => x.Dto.PriceInclVat)
            })
            .ToList();

        var cart = new CartDto
        {
            SupplierGroups = groups,
            GrandTotal = groups.Sum(g => g.GroupTotal),
            ItemCount = itemDtos.Count,
            HasUnavailableItems = itemDtos.Any(x => !x.Dto.IsAvailable)
        };

        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            cart.AppliedPromoCode = promoCode.Trim();
            if (codeResolution == null)
            {
                cart.PromoValid = false;
                cart.PromoMessage = "Invalid or expired promo code.";
            }
            else if (!codeResolution.Valid)
            {
                cart.PromoValid = false;
                cart.PromoMessage = codeResolution.Message;
            }
            else
            {
                var appliedAny = itemDtos.Any(x => x.Dto.DiscountPercent > 0);
                cart.PromoValid = true;
                cart.PromoCampaignName = codeResolution.CampaignName;
                cart.PromoDiscountPercent = codeResolution.DiscountPercent;
                cart.PromoMessage = appliedAny
                    ? codeResolution.Message
                    : "Promo code is valid but does not apply to items in your cart.";
            }
        }

        return cart;
    }

    public async Task<(bool Success, string? Error)> AddItemAsync(int contractorId, AddCartItemDto dto)
    {
        if (dto.EndDate.Date <= dto.StartDate.Date)
            return (false, "End date must be after the start date.");
        if (dto.StartDate.Date < AppTime.Now.Date)
            return (false, "Start date cannot be in the past.");

        var listing = await _db.Listings.FindAsync(dto.ListingID);
        if (listing == null) return (false, "Listing not found.");
        if (listing.AvailabilityStatus != "Active")
            return (false, "This listing is not currently available for booking.");
        if (listing.PricingMode != "Fixed")
            return (false, "This item requires a quote and cannot be added to the Cart directly. Please use Request a Quote instead.");
        if (listing.UnitsAvailable < dto.Quantity)
            return (false, $"Only {listing.UnitsAvailable} unit(s) of this item are currently available.");

        var method = (dto.FulfillmentMethod ?? "").Trim();
        var isPickup = method.Equals("Contractor Pickup", StringComparison.OrdinalIgnoreCase)
            || method.Contains("pickup", StringComparison.OrdinalIgnoreCase);
        var address = (dto.DeliveryAddress ?? string.Empty).Trim();
        if (isPickup)
        {
            if (!listing.PickupAvailable)
                return (false, "This listing does not offer contractor pickup. Please choose Supplier Delivery.");
            if (string.IsNullOrWhiteSpace(address))
                address = string.IsNullOrWhiteSpace(listing.Location)
                    ? "Supplier yard (pickup)"
                    : listing.Location.Trim();
            if (!address.Contains("pickup", StringComparison.OrdinalIgnoreCase))
                address = "Pickup: " + address;
        }
        else
        {
            if (!listing.DeliveryAvailable)
                return (false, "This listing does not offer supplier delivery. Please choose Contractor Pickup.");
            if (string.IsNullOrWhiteSpace(address))
                return (false, "Please enter a delivery address, or choose Contractor Pickup.");
        }

        var existing = await _db.CartItems.FirstOrDefaultAsync(c =>
            c.ContractorID == contractorId && c.ListingID == dto.ListingID &&
            c.RentalStartDate == dto.StartDate.Date && c.RentalEndDate == dto.EndDate.Date);

        var ttl = TimeSpan.FromMinutes(30);
        // Cart does NOT reserve stock. Units stay available to other contractors until
        // both parties have signed the lease agreement.
        if (existing != null)
        {
            if (listing.UnitsAvailable < existing.Quantity + dto.Quantity)
                return (false, $"Only {listing.UnitsAvailable} unit(s) of this item are currently available.");
            existing.Quantity += dto.Quantity;
            existing.DeliveryAddress = address;
            existing.ReservedUntil = AppTime.Now.Add(ttl);
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                ContractorID = contractorId,
                ListingID = dto.ListingID,
                Quantity = dto.Quantity,
                RentalStartDate = dto.StartDate.Date,
                RentalEndDate = dto.EndDate.Date,
                DeliveryAddress = address,
                AddedDate = AppTime.Now,
                ReservedUntil = AppTime.Now.Add(ttl)
            });
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateItemAsync(int contractorId, int cartItemId, UpdateCartItemDto dto)
    {
        if (dto.EndDate.Date <= dto.StartDate.Date)
            return (false, "End date must be after the start date.");

        var item = await _db.CartItems.Include(c => c.Listing)
            .FirstOrDefaultAsync(c => c.CartItemID == cartItemId && c.ContractorID == contractorId);
        if (item == null) return (false, "Cart item not found.");

        var newQty = dto.Quantity;
        if (newQty < 1)
            return (false, "Quantity must be at least 1.");
        // No cart hold — availability is the open market stock.
        if (item.Listing.UnitsAvailable < newQty)
            return (false, $"Only {item.Listing.UnitsAvailable} unit(s) of this item are currently available.");

        var method = (dto.FulfillmentMethod ?? "").Trim();
        var isPickup = method.Equals("Contractor Pickup", StringComparison.OrdinalIgnoreCase)
            || method.Contains("pickup", StringComparison.OrdinalIgnoreCase);
        var address = (dto.DeliveryAddress ?? item.DeliveryAddress ?? string.Empty).Trim();
        if (isPickup)
        {
            if (!item.Listing.PickupAvailable)
                return (false, "This listing does not offer contractor pickup. Please choose Supplier Delivery.");
            if (string.IsNullOrWhiteSpace(address))
                address = string.IsNullOrWhiteSpace(item.Listing.Location)
                    ? "Supplier yard (pickup)"
                    : item.Listing.Location.Trim();
            if (!address.Contains("pickup", StringComparison.OrdinalIgnoreCase))
                address = "Pickup: " + address;
        }
        else
        {
            if (!item.Listing.DeliveryAvailable)
                return (false, "This listing does not offer supplier delivery. Please choose Contractor Pickup.");
            if (string.IsNullOrWhiteSpace(address))
                return (false, "Please enter a delivery address, or choose Contractor Pickup.");
        }

        item.RentalStartDate = dto.StartDate.Date;
        item.RentalEndDate = dto.EndDate.Date;
        item.Quantity = newQty;
        item.DeliveryAddress = address;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> RemoveItemAsync(int contractorId, int cartItemId)
    {
        var item = await _db.CartItems.Include(c => c.Listing)
            .FirstOrDefaultAsync(c => c.CartItemID == cartItemId && c.ContractorID == contractorId);
        if (item == null) return false;

        // No inventory change — cart never reserved stock.
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CheckoutResult> CheckoutAsync(int contractorId, string? promoCode = null)
    {
        var items = await _db.CartItems
            .Include(c => c.Listing)
            .Where(c => c.ContractorID == contractorId)
            .ToListAsync();

        if (items.Count == 0)
            return new CheckoutResult { Success = false, Error = "Your cart is empty." };

        foreach (var item in items)
        {
            if (item.Listing.AvailabilityStatus != "Active" || item.Listing.PricingMode != "Fixed")
                return new CheckoutResult { Success = false, Error = $"'{item.Listing.ListingTitle}' is no longer available and has been removed from your cart. Please review your cart and try again." };
            // Advisory check only — stock is committed at dual lease signature, not at checkout.
            if (item.Listing.UnitsAvailable < item.Quantity)
                return new CheckoutResult { Success = false, Error = $"Only {item.Listing.UnitsAvailable} unit(s) of '{item.Listing.ListingTitle}' are available. Please adjust the quantity in your cart." };
        }

        PromoCodeResolution? codeResolution = null;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            codeResolution = await _campaigns.ResolveDiscountCodeAsync(promoCode);
            if (!codeResolution.Valid)
                return new CheckoutResult { Success = false, Error = codeResolution.Message };
        }

        var result = new CheckoutResult { Success = true };
        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Pass 1: price every line (before consolidating delivery fees).
            var priced = new List<CheckoutPricedLine>();
            foreach (var item in items)
            {
                var days = HireDuration.InclusiveDays(item.RentalStartDate, item.RentalEndDate);
                var isPickup = IsContractorPickup(item.DeliveryAddress, item.Listing.Location);
                var distanceKm = isPickup
                    ? 0m
                    : await _distanceService.CalculateDistanceKmAsync(item.Listing.Location, item.DeliveryAddress);
                var listingPromo = await _campaigns.GetActivePromoDiscountPercentAsync(item.ListingID);
                var codePromo = 0m;
                if (codeResolution is { Valid: true }
                    && (!codeResolution.FeaturedListingId.HasValue
                        || codeResolution.FeaturedListingId.Value == item.ListingID))
                    codePromo = codeResolution.DiscountPercent;
                var promoPercent = Math.Max(listingPromo, codePromo);
                var chargeDelivery = !isPickup && item.Listing.DeliveryAvailable;
                var flatFee = chargeDelivery && item.Listing.DeliveryFeeZAR is > 0
                    ? item.Listing.DeliveryFeeZAR
                    : null;
                var pricing = await _pricingEngine.CalculateAsync(
                    item.Listing.DailyRateZAR, days, item.Quantity, distanceKm, item.Listing.CategoryID, promoPercent,
                    chargeDelivery: chargeDelivery,
                    listingFlatDeliveryFee: flatFee);

                priced.Add(new CheckoutPricedLine
                {
                    Item = item,
                    SupplierId = item.Listing.SupplierID,
                    DeliveryAddress = item.DeliveryAddress,
                    IsPickup = isPickup,
                    DistanceKm = distanceKm,
                    Pricing = pricing
                });
            }

            // One delivery charge per unique (supplier, destination).
            ConsolidateCheckoutDeliveryFees(priced);

            // Pass 2: create quotation / booking / invoice with consolidated fees.
            foreach (var line in priced)
            {
                var item = line.Item;
                var pricing = line.Pricing;
                var now = AppTime.Now;
                var quotation = new Quotation
                {
                    ListingID = item.ListingID,
                    SupplierID = item.Listing.SupplierID,
                    ContractorID = contractorId,
                    RentalStartDate = item.RentalStartDate,
                    RentalEndDate = item.RentalEndDate,
                    Quantity = item.Quantity,
                    DeliveryAddress = item.DeliveryAddress,
                    PreferredContact = "Email",
                    DailyRateZAR = item.Listing.DailyRateZAR,
                    DeliveryDistanceKm = line.DistanceKm,
                    RentalSubtotal = pricing.RentalSubtotal,
                    DiscountPercent = pricing.DiscountPercent,
                    DiscountAmount = pricing.DiscountAmount,
                    DeliveryFee = pricing.DeliveryFee,
                    PriceExclVat = pricing.PriceExclVat,
                    VatRate = pricing.VatRate,
                    PriceInclVat = pricing.PriceInclVat,
                    EstimatedTotal = pricing.PriceInclVat,
                    Status = "Accepted",
                    RequestedDate = now,
                    SubmittedDate = now,
                    AcceptedDate = now
                };
                _db.Quotations.Add(quotation);
                await _db.SaveChangesAsync();

                await _db.Entry(item.Listing).ReloadAsync();
                if (item.Listing.AvailabilityStatus != "Active")
                    throw new InvalidOperationException($"'{item.Listing.ListingTitle}' is no longer available.");

                // Do NOT claim units here. Inventory is committed only when both contractor and
                // supplier have signed the lease (see LeaseAgreementRepository.SignAsync).
                if (item.Listing.UnitsAvailable < item.Quantity)
                    throw new InvalidOperationException(
                        $"Only {item.Listing.UnitsAvailable} unit(s) of '{item.Listing.ListingTitle}' remain.");

                var booking = new Booking
                {
                    ListingID = item.ListingID,
                    SupplierID = item.Listing.SupplierID,
                    ContractorID = contractorId,
                    RentalStartDate = item.RentalStartDate,
                    RentalEndDate = item.RentalEndDate,
                    DeliveryAddress = item.DeliveryAddress,
                    CreatedDate = now,
                    Quantity = Math.Max(1, item.Quantity)
                };

                quotation.Booking = booking;

                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync();

                var invoiceResult = await _invoices.GenerateSystemAsync(quotation.QuotationID);
                var leaseAgreement = await _leaseAgreements.CreateFromQuotationAsync(quotation.QuotationID, booking.BookingID);

                _db.CartItems.Remove(item);

                result.Bookings.Add(new CheckoutResultBookingDto
                {
                    BookingID = booking.BookingID,
                    ListingID = item.ListingID,
                    ListingTitle = item.Listing.ListingTitle,
                    InvoiceID = invoiceResult.Success ? invoiceResult.Invoice!.InvoiceID : 0,
                    InvoiceNumber = invoiceResult.Success ? invoiceResult.Invoice!.InvoiceNumber : string.Empty,
                    LeaseAgreementID = leaseAgreement.LeaseAgreementID,
                    SupplierID = item.Listing.SupplierID,
                    TotalAmount = pricing.PriceInclVat
                });
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
        }
        catch (InvalidOperationException ex)
        {
            return new CheckoutResult { Success = false, Error = ex.Message };
        }
        catch (DbUpdateException)
        {
            return new CheckoutResult
            {
                Success = false,
                Error = "Checkout could not finish because of a data conflict. Open Bookings / Lease agreements — the order may already have been created. If the cart is empty, do not check out again."
            };
        }
        catch (Exception ex)
        {
            return new CheckoutResult { Success = false, Error = ex.Message };
        }

        return result;
    }

    private static bool IsContractorPickup(string? address, string? listingLocation)
    {
        var a = (address ?? string.Empty).Trim();
        if (a.Contains("pickup", StringComparison.OrdinalIgnoreCase))
            return true;
        var loc = (listingLocation ?? string.Empty).Trim();
        return loc.Length > 0 && a.Equals(loc, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalise an address for consolidation keys so minor spacing/case differences
    /// still count as the same destination.
    /// </summary>
    private static string NormalizeAddressKey(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;
        var parts = address.Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Keep the highest delivery fee on one line per (supplier, destination);
    /// zero the fee on sibling lines and subtract it from their totals.
    /// VAT is unchanged (it never included delivery).
    /// </summary>
    private static void ConsolidateCartDeliveryFees(
        List<(CartItemDto Dto, int SupplierId, string SupplierName)> lines)
    {
        var groups = lines
            .Where(x => x.Dto.DeliveryFee > 0
                        && !string.Equals(x.Dto.FulfillmentMethod, "Contractor Pickup", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => new
            {
                x.SupplierId,
                Address = NormalizeAddressKey(x.Dto.DeliveryAddress)
            });

        foreach (var g in groups)
        {
            var ordered = g
                .OrderByDescending(x => x.Dto.DeliveryFee)
                .ThenBy(x => x.Dto.CartItemID)
                .ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                var dto = ordered[i].Dto;
                var removed = dto.DeliveryFee;
                if (removed <= 0) continue;
                dto.DeliveryFee = 0;
                dto.PriceExclVat = Math.Round(Math.Max(0, dto.PriceExclVat - removed), 2);
                dto.PriceInclVat = Math.Round(Math.Max(0, dto.PriceInclVat - removed), 2);
            }
        }
    }

    private static void ConsolidateCheckoutDeliveryFees(List<CheckoutPricedLine> lines)
    {
        var groups = lines
            .Where(x => !x.IsPickup && x.Pricing.DeliveryFee > 0)
            .GroupBy(x => new
            {
                x.SupplierId,
                Address = NormalizeAddressKey(x.DeliveryAddress)
            });

        foreach (var g in groups)
        {
            var ordered = g
                .OrderByDescending(x => x.Pricing.DeliveryFee)
                .ThenBy(x => x.Item.CartItemID)
                .ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                var pricing = ordered[i].Pricing;
                var removed = pricing.DeliveryFee;
                if (removed <= 0) continue;
                pricing.DeliveryFee = 0;
                pricing.PriceExclVat = Math.Round(Math.Max(0, pricing.PriceExclVat - removed), 2);
                pricing.PriceInclVat = Math.Round(Math.Max(0, pricing.PriceInclVat - removed), 2);
            }
        }
    }

    private sealed class CheckoutPricedLine
    {
        public CartItem Item { get; set; } = null!;
        public int SupplierId { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public bool IsPickup { get; set; }
        public decimal DistanceKm { get; set; }
        public PricingResult Pricing { get; set; } = null!;
    }

}
