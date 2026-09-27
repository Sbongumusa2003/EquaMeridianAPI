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

        var itemDtos = new List<(CartItemDto Dto, int SupplierId, string SupplierName)>();
        foreach (var item in items)
        {
            var days = Math.Max(1, (item.RentalEndDate.Date - item.RentalStartDate.Date).Days);
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
            var pricing = await _pricingEngine.CalculateAsync(
                item.Listing.DailyRateZAR, days, item.Quantity, distanceKm, item.Listing.CategoryID, promoPercent,
                chargeDelivery: !isPickup);

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
                // Units already held by THIS cart line were subtracted from Listing.UnitsAvailable
                // when the item was added. For the contractor who holds them, they still count
                // toward what they can keep / increase to — so report free + own hold.
                UnitsAvailable = item.Listing.UnitsAvailable + item.Quantity,
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
                              && (item.Listing.UnitsAvailable + item.Quantity) >= item.Quantity
            };

            itemDtos.Add((dto, item.Listing.SupplierID, item.Listing.Supplier.CompanyName ?? item.Listing.Supplier.FullName));
        }

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
            if (string.IsNullOrWhiteSpace(address))
                address = string.IsNullOrWhiteSpace(listing.Location)
                    ? "Supplier yard (pickup)"
                    : listing.Location.Trim();
            if (!address.Contains("pickup", StringComparison.OrdinalIgnoreCase))
                address = "Pickup: " + address;
        }
        else if (string.IsNullOrWhiteSpace(address))
        {
            return (false, "Please enter a delivery address, or choose Contractor Pickup.");
        }

        var existing = await _db.CartItems.FirstOrDefaultAsync(c =>
            c.ContractorID == contractorId && c.ListingID == dto.ListingID &&
            c.RentalStartDate == dto.StartDate.Date && c.RentalEndDate == dto.EndDate.Date);

        var ttl = TimeSpan.FromMinutes(30);
        if (existing != null)
        {
            if (listing.UnitsAvailable < dto.Quantity)
                return (false, $"Only {listing.UnitsAvailable} unit(s) of this item are currently available.");
            existing.Quantity += dto.Quantity;
            existing.DeliveryAddress = address;
            existing.ReservedUntil = AppTime.Now.Add(ttl);
            listing.UnitsAvailable -= dto.Quantity;
            listing.UnitsReserved += dto.Quantity;
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
            listing.UnitsAvailable -= dto.Quantity;
            listing.UnitsReserved += dto.Quantity;
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

        // Units held by this line already reduced Listing.UnitsAvailable. Free stock for
        // this contractor = remaining market stock + what this line currently holds.
        var oldQty = item.Quantity;
        var newQty = dto.Quantity;
        if (newQty < 1)
            return (false, "Quantity must be at least 1.");
        var freeForContractor = item.Listing.UnitsAvailable + oldQty;
        if (freeForContractor < newQty)
            return (false, $"Only {freeForContractor} unit(s) of this item are currently available.");

        var method = (dto.FulfillmentMethod ?? "").Trim();
        var isPickup = method.Equals("Contractor Pickup", StringComparison.OrdinalIgnoreCase)
            || method.Contains("pickup", StringComparison.OrdinalIgnoreCase);
        var address = (dto.DeliveryAddress ?? item.DeliveryAddress ?? string.Empty).Trim();
        if (isPickup)
        {
            if (string.IsNullOrWhiteSpace(address))
                address = string.IsNullOrWhiteSpace(item.Listing.Location)
                    ? "Supplier yard (pickup)"
                    : item.Listing.Location.Trim();
            if (!address.Contains("pickup", StringComparison.OrdinalIgnoreCase))
                address = "Pickup: " + address;
        }
        else if (string.IsNullOrWhiteSpace(address))
        {
            return (false, "Please enter a delivery address, or choose Contractor Pickup.");
        }

        var delta = newQty - oldQty;
        if (delta > 0)
        {
            item.Listing.UnitsAvailable -= delta;
            item.Listing.UnitsReserved += delta;
        }
        else if (delta < 0)
        {
            item.Listing.UnitsAvailable += -delta;
            item.Listing.UnitsReserved = Math.Max(0, item.Listing.UnitsReserved + delta);
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

        if (item.Listing != null)
        {
            item.Listing.UnitsReserved = Math.Max(0, item.Listing.UnitsReserved - item.Quantity);
            item.Listing.UnitsAvailable += item.Quantity;
        }
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
            // Units for this cart line were already taken from UnitsAvailable when the item
            // was added. Treat the line's Quantity as held for this contractor regardless of
            // ReservedUntil TTL so checkout is not blocked by the soft-hold window.
            var freeUnits = item.Listing.UnitsAvailable + item.Quantity;
            if (freeUnits < item.Quantity)
                return new CheckoutResult { Success = false, Error = $"Only {freeUnits} unit(s) of '{item.Listing.ListingTitle}' are available. Please adjust the quantity in your cart." };
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

            foreach (var item in items)
            {
                var days = Math.Max(1, (item.RentalEndDate.Date - item.RentalStartDate.Date).Days);
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
                var pricing = await _pricingEngine.CalculateAsync(
                    item.Listing.DailyRateZAR, days, item.Quantity, distanceKm, item.Listing.CategoryID, promoPercent,
                    chargeDelivery: !isPickup);

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
                    DeliveryDistanceKm = distanceKm,
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

                // Re-read inside the transaction so two simultaneous checkouts cannot
                // oversell the last unit (the pre-check above used a snapshot).
                await _db.Entry(item.Listing).ReloadAsync();
                if (item.Listing.AvailabilityStatus != "Active")
                    throw new InvalidOperationException($"'{item.Listing.ListingTitle}' is no longer available.");

                // Cart-add already reduced UnitsAvailable and increased UnitsReserved for this
                // line's Quantity. Free stock for THIS contractor = market free + own hold.
                var freeForContractor = item.Listing.UnitsAvailable + item.Quantity;
                if (freeForContractor < item.Quantity)
                    throw new InvalidOperationException(
                        $"Only {freeForContractor} unit(s) of '{item.Listing.ListingTitle}' remain.");

                // Convert soft cart hold into a confirmed booking:
                // - UnitsAvailable stays reduced (units are now committed to the booking)
                // - UnitsReserved drops because the hold is no longer a cart reservation
                // If for any reason no hold was recorded, claim units now.
                if (item.Listing.UnitsReserved >= item.Quantity)
                {
                    item.Listing.UnitsReserved -= item.Quantity;
                }
                else
                {
                    // Fallback: claim from free stock (should be rare)
                    if (item.Listing.UnitsAvailable < item.Quantity)
                        throw new InvalidOperationException(
                            $"Only {item.Listing.UnitsAvailable} unit(s) of '{item.Listing.ListingTitle}' remain.");
                    item.Listing.UnitsAvailable -= item.Quantity;
                }

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

}
