using EquaMeridian.DTOs.Quotations;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class QuotationRepository : IQuotationRepository
{
    private readonly AppDbContext _db;
    private readonly IPricingEngine _pricingEngine;
    private readonly IDeliveryDistanceService _distanceService;

    public QuotationRepository(AppDbContext db, IPricingEngine pricingEngine, IDeliveryDistanceService distanceService)
    {
        _db = db;
        _pricingEngine = pricingEngine;
        _distanceService = distanceService;
    }

    public async Task<(IEnumerable<QuotationListItemDto>, int)> GetAllForSupplierAsync(
        int supplierId, int? listingId, string? status, int page, int pageSize)
    {
        var q = _db.Quotations
            .AsNoTracking()
            .Include(x => x.Listing)
            .Include(x => x.Contractor)
            .Where(x => x.SupplierID == supplierId)
            .AsQueryable();

        if (listingId.HasValue)
            q = q.Where(x => x.ListingID == listingId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync();
        var quotations = await q
            .OrderByDescending(x => x.RequestedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (quotations.Select(MapToListItemDto), total);
    }

    public async Task<QuotationReviewDto?> GetForReviewAsync(int quotationId, int supplierId)
    {
        var quotation = await _db.Quotations
            .AsNoTracking()
            .Include(x => x.Listing)
            .Include(x => x.Contractor)
            .FirstOrDefaultAsync(x => x.QuotationID == quotationId && x.SupplierID == supplierId);

        return quotation == null ? null : MapToReviewDto(quotation);
    }

    public async Task<QuotationSubmitResult> SubmitAsync(int quotationId, int supplierId, SubmitQuotationDto dto)
    {
        var quotation = await _db.Quotations
            .Include(x => x.Listing)
            .Include(x => x.Contractor)
            .FirstOrDefaultAsync(x => x.QuotationID == quotationId && x.SupplierID == supplierId);

        if (quotation == null)
            return new QuotationSubmitResult { Success = false, Error = "Quotation request not found." };

        if (quotation.Status != "Requested")
            return new QuotationSubmitResult
            {
                Success = false,
                Error = $"Quotation cannot be submitted because its current status is '{quotation.Status}'."
            };

        if (dto.QuoteValidUntil.Date < AppTime.Now.Date)
            return new QuotationSubmitResult { Success = false, Error = "Quote expiration date must be in the future." };

        var days = Math.Max(1, (quotation.RentalEndDate.Date - quotation.RentalStartDate.Date).Days);
        var distanceKm = await _distanceService.CalculateDistanceKmAsync(
            quotation.Listing.Location, quotation.DeliveryAddress);

        var pricing = await _pricingEngine.CalculateAsync(
            dto.DailyRateZAR, days, quotation.Quantity, distanceKm, quotation.Listing.CategoryID);

        quotation.DailyRateZAR = dto.DailyRateZAR;
        quotation.WeeklyRateZAR = dto.WeeklyRateZAR;
        quotation.DeliveryDistanceKm = distanceKm;
        quotation.DeliveryFee = pricing.DeliveryFee;
        quotation.RentalSubtotal = pricing.RentalSubtotal;
        quotation.DiscountPercent = pricing.DiscountPercent;
        quotation.DiscountAmount = pricing.DiscountAmount;
        quotation.PriceExclVat = pricing.PriceExclVat;
        quotation.VatRate = dto.VatRate ?? pricing.VatRate;
        quotation.PriceInclVat = dto.VatRate.HasValue
            ? pricing.PriceExclVat + Math.Round(pricing.PriceExclVat * (dto.VatRate.Value / 100m), 2)
            : pricing.PriceInclVat;
        quotation.EstimatedTotal = quotation.PriceInclVat.Value;
        quotation.SecurityDeposit = dto.SecurityDeposit;
        quotation.DamageWaiverFee = dto.DamageWaiverFee;
        quotation.QuoteValidUntil = dto.QuoteValidUntil;
        quotation.NotesToCustomer = dto.NotesToCustomer;
        quotation.Status = "Submitted";
        quotation.SubmittedDate = AppTime.Now;

        await _db.SaveChangesAsync();

        return new QuotationSubmitResult
        {
            Success = true,
            Quotation = MapToReviewDto(quotation),
            ContractorID = quotation.ContractorID,
            ContractorEmail = quotation.Contractor.Email,
            ContractorName = quotation.Contractor.FullName
        };
    }
    public async Task<CreateQuotationResult> CreateRequestAsync(int contractorId, CreateQuotationRequestDto dto)
    {
        if (dto.EndDate.Date <= dto.StartDate.Date)
            return new CreateQuotationResult { Success = false, Error = "End Date must be after Start Date." };

        var earliestStart = AppTime.Now.Date.AddDays(1);
        if (dto.StartDate.Date < earliestStart)
            return new CreateQuotationResult { Success = false, Error = "Start Date must be at least the next business day." };

        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.ListingID == dto.ListingID);
        if (listing == null)
            return new CreateQuotationResult { Success = false, Error = "Listing not found." };

        if (listing.AvailabilityStatus != "Active")
            return new CreateQuotationResult
            {
                Success = false,
                Error = "This listing is no longer available for quotation requests. Please browse other machinery listings."
            };

        if (listing.SupplierID == contractorId)
            return new CreateQuotationResult { Success = false, Error = "You cannot request a quotation for your own listing." };

        var contractor = await _db.Users.FirstOrDefaultAsync(u => u.UserID == contractorId);
        if (contractor == null || contractor.AccountStatus != "Active")
            return new CreateQuotationResult
            {
                Success = false,
                Error = "Your account is not active. Please contact support for assistance."
            };

        var overlaps = await _db.Quotations.AnyAsync(q =>
            q.ListingID == dto.ListingID && q.ContractorID == contractorId &&
            q.Status != "Rejected" && q.Status != "Cancelled" &&
            dto.StartDate < q.RentalEndDate && q.RentalStartDate < dto.EndDate);

        if (overlaps)
            return new CreateQuotationResult
            {
                Success = false,
                Error = "You have already requested a quotation for this listing for overlapping dates. Please check your existing requests."
            };

        var rentalDays = Math.Max(1, (dto.EndDate.Date - dto.StartDate.Date).Days);
        var distanceKm = await _distanceService.CalculateDistanceKmAsync(listing.Location, dto.DeliveryAddress);
        var pricing = await _pricingEngine.CalculateAsync(
            listing.DailyRateZAR, rentalDays, dto.Quantity, distanceKm, listing.CategoryID);

        var quotation = new Quotation
        {
            ListingID = dto.ListingID,
            SupplierID = listing.SupplierID,
            ContractorID = contractorId,
            RentalStartDate = dto.StartDate,
            RentalEndDate = dto.EndDate,
            Quantity = dto.Quantity,
            DeliveryAddress = dto.DeliveryAddress,
            SpecialRequirements = dto.SpecialRequirements,
            PreferredContact = dto.PreferredContact,
            DeliveryDistanceKm = distanceKm,
            RentalSubtotal = pricing.RentalSubtotal,
            DiscountPercent = pricing.DiscountPercent,
            DiscountAmount = pricing.DiscountAmount,
            DeliveryFee = pricing.DeliveryFee,
            PriceExclVat = pricing.PriceExclVat,
            VatRate = pricing.VatRate,
            PriceInclVat = pricing.PriceInclVat,
            EstimatedTotal = pricing.PriceInclVat,
            Status = "Requested",
            RequestedDate = AppTime.Now
        };

        _db.Quotations.Add(quotation);
        await _db.SaveChangesAsync();

        var supplier = await _db.Users.FirstAsync(u => u.UserID == listing.SupplierID);

        return new CreateQuotationResult
        {
            Success = true,
            Quotation = new QuotationListItemDto
            {
                QuotationID = quotation.QuotationID,
                ListingID = quotation.ListingID,
                ListingTitle = listing.ListingTitle,
                ContractorName = contractor.FullName,
                Status = quotation.Status,
                RequestedDate = quotation.RequestedDate,
                RentalStartDate = quotation.RentalStartDate,
                RentalEndDate = quotation.RentalEndDate,
                Quantity = quotation.Quantity,
                EstimatedTotal = quotation.EstimatedTotal
            },
            SupplierID = listing.SupplierID,
            SupplierEmail = supplier.Email,
            SupplierName = supplier.FullName,
            ListingTitle = listing.ListingTitle
        };
    }

    public async Task<(IEnumerable<QuotationListItemDto>, int)> GetAllForContractorAsync(
        int contractorId, int? listingId, string? status, string? search,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        var q = _db.Quotations
            .AsNoTracking()
            .Include(x => x.Listing)
            .Include(x => x.Supplier)
            .Where(x => x.ContractorID == contractorId)
            .AsQueryable();

        if (listingId.HasValue)
            q = q.Where(x => x.ListingID == listingId.Value);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.Status == status);

        if (from.HasValue)
            q = q.Where(x => x.RequestedDate.Date >= from.Value.Date);

        if (to.HasValue)
            q = q.Where(x => x.RequestedDate.Date <= to.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.Listing.ListingTitle.Contains(term) || x.Supplier.FullName.Contains(term));
        }

        var total = await q.CountAsync();
        var quotations = await q
            .OrderByDescending(x => x.RequestedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (quotations.Select(MapToContractorListItemDto), total);
    }

    public async Task<QuotationDetailDto?> GetDetailForContractorAsync(int quotationId, int contractorId)
    {
        var q = await _db.Quotations
            .AsNoTracking()
            .Include(x => x.Listing)
            .Include(x => x.Supplier)
            .Include(x => x.Contractor)
            .FirstOrDefaultAsync(x => x.QuotationID == quotationId && x.ContractorID == contractorId);

        if (q == null) return null;

        var invoiceId = await _db.Invoices
            .Where(i => i.QuotationID == quotationId)
            .Select(i => (int?)i.InvoiceID)
            .FirstOrDefaultAsync();

        var history = new List<QuotationStatusEventDto> { new() { Status = "Requested", Date = q.RequestedDate } };
        if (q.SubmittedDate.HasValue) history.Add(new() { Status = "Quoted", Date = q.SubmittedDate.Value });
        if (q.AcceptedDate.HasValue) history.Add(new() { Status = "Accepted", Date = q.AcceptedDate.Value });
        if (q.RejectedDate.HasValue) history.Add(new() { Status = "Rejected", Date = q.RejectedDate.Value });

        return new QuotationDetailDto
        {
            QuotationID = q.QuotationID,
            ListingID = q.ListingID,
            ListingTitle = q.Listing.ListingTitle,
            ContractorName = q.Contractor?.FullName ?? string.Empty,
            SupplierName = q.Supplier.FullName,
            Status = q.Status,
            RequestedDate = q.RequestedDate,
            QuoteValidUntil = q.QuoteValidUntil,
            DailyRateZAR = q.DailyRateZAR,
            WeeklyRateZAR = q.WeeklyRateZAR,
            DeliveryFee = q.DeliveryFee,
            NotesToCustomer = q.NotesToCustomer,
            DeliveryDistanceKm = q.DeliveryDistanceKm,
            RentalSubtotal = q.RentalSubtotal,
            DiscountPercent = q.DiscountPercent,
            DiscountAmount = q.DiscountAmount,
            PriceExclVat = q.PriceExclVat,
            VatRate = q.VatRate,
            PriceInclVat = q.PriceInclVat,
            SecurityDeposit = q.SecurityDeposit,
            DamageWaiverFee = q.DamageWaiverFee,
            RentalStartDate = q.RentalStartDate,
            RentalEndDate = q.RentalEndDate,
            Quantity = q.Quantity,
            DeliveryAddress = q.DeliveryAddress,
            SpecialRequirements = q.SpecialRequirements,
            PreferredContact = q.PreferredContact,
            EstimatedTotal = q.EstimatedTotal,
            StatusHistory = history,
            CanAccept = q.Status == "Submitted",
            CanReject = q.Status == "Submitted",
            InvoiceID = invoiceId
        };
    }

    public async Task<List<QuotationCompareDto>> CompareAsync(IEnumerable<int> quotationIds, int contractorId)
    {
        var ids = quotationIds.Distinct().ToList();

        var quotations = await _db.Quotations
            .AsNoTracking()
            .Include(x => x.Listing).ThenInclude(l => l.Supplier)
            .Where(x => ids.Contains(x.QuotationID) && x.ContractorID == contractorId)
            .ToListAsync();

        var categories = await _db.Categories.ToDictionaryAsync(c => c.CategoryID, c => c.Name);

        var result = new List<QuotationCompareDto>();
        foreach (var q in quotations)
        {
            var days = Math.Max(1, (q.RentalEndDate.Date - q.RentalStartDate.Date).Days);
            var effectiveDailyRate = q.DailyRateZAR ?? q.Listing.DailyRateZAR;

            decimal estimatedTotal;
            if (q.DailyRateZAR.HasValue && q.PriceInclVat.HasValue)
            {
                estimatedTotal = q.PriceInclVat.Value;
            }
            else
            {
                var pricing = await _pricingEngine.CalculateAsync(
                    effectiveDailyRate, days, q.Quantity, q.DeliveryDistanceKm ?? 0, q.Listing.CategoryID);
                estimatedTotal = pricing.PriceInclVat;
            }

            categories.TryGetValue(q.Listing.CategoryID, out var categoryName);

            result.Add(new QuotationCompareDto
            {
                QuotationID = q.QuotationID,
                ListingID = q.ListingID,
                ListingTitle = q.Listing.ListingTitle,
                Category = categoryName ?? string.Empty,
                SupplierCompany = q.Listing.Supplier.CompanyName ?? q.Listing.Supplier.FullName,
                RentalStartDate = q.RentalStartDate,
                RentalEndDate = q.RentalEndDate,
                RentalDurationDays = days,
                Quantity = q.Quantity,
                DailyRateZAR = effectiveDailyRate,
                EstimatedTotal = estimatedTotal,
                QuoteValidUntil = q.QuoteValidUntil,
                DepositAmount = q.SecurityDeposit,
                MakeBrand = q.Listing.MakeBrand,
                Model = q.Listing.Model,
                Year = q.Listing.Year,
                OperatingWeight = q.Listing.OperatingWeight,
                EnginePower = q.Listing.EnginePower,
                Location = q.Listing.Location,
                Status = q.Status,
                SpecialRequirements = q.SpecialRequirements,
                CanAccept = q.Status == "Submitted"
            });
        }

        return result;
    }

    public async Task<AcceptQuotationResult> AcceptAsync(int quotationId, int contractorId)
    {
        var q = await _db.Quotations
            .Include(x => x.Listing)
            .Include(x => x.Supplier)
            .Include(x => x.Contractor)
            .FirstOrDefaultAsync(x => x.QuotationID == quotationId && x.ContractorID == contractorId);

        if (q == null)
            return new AcceptQuotationResult { Success = false, Error = "Quotation not found." };

        if (q.Status is "Accepted" or "Rejected")
            return new AcceptQuotationResult { Success = false, Error = "This quotation has already been processed." };

        if (q.QuoteValidUntil.HasValue && q.QuoteValidUntil.Value.Date < AppTime.Now.Date)
            return new AcceptQuotationResult
            {
                Success = false,
                Error = "This quotation has expired. Please contact the Supplier or request a new quotation."
            };

        if (q.Status != "Submitted")
            return new AcceptQuotationResult
            {
                Success = false,
                Error = $"Quotation cannot be accepted because its current status is '{q.Status}'."
            };

        if (q.Listing.AvailabilityStatus != "Active")
            return new AcceptQuotationResult
            {
                Success = false,
                Error = "The listing for this quotation is no longer available. Please contact the Supplier for assistance."
            };

        if (q.Supplier.AccountStatus != "Active")
            return new AcceptQuotationResult
            {
                Success = false,
                Error = "The Supplier's account is not active. Please contact support for assistance."
            };
        if (q.Listing.UnitsAvailable < q.Quantity)
            return new AcceptQuotationResult
            {
                Success = false,
                Error = "This equipment is no longer available in the requested quantity."
            };

        q.Listing.UnitsAvailable -= q.Quantity;
        q.Listing.UnitsReserved += q.Quantity;
        var competingQuotes = await _db.Quotations
            .Include(x => x.Listing)
            .Where(x => x.ContractorID == contractorId
                        && x.QuotationID != q.QuotationID
                        && (x.Status == "Requested" || x.Status == "Submitted")
                        && x.Listing.CategoryID == q.Listing.CategoryID
                        && x.RentalStartDate < q.RentalEndDate
                        && q.RentalStartDate < x.RentalEndDate)
            .ToListAsync();

        foreach (var competing in competingQuotes)
        {
            competing.Status = "Superseded";
            competing.RejectedDate = AppTime.Now;
            competing.RejectionReason = $"Superseded: Contractor accepted quotation #{q.QuotationID} for the same job.";
        }

        var booking = new Booking
        {
            ListingID = q.ListingID,
            SupplierID = q.SupplierID,
            ContractorID = q.ContractorID,
            RentalStartDate = q.RentalStartDate,
            RentalEndDate = q.RentalEndDate,
            DeliveryAddress = q.DeliveryAddress,
            CreatedDate = AppTime.Now,
            Quantity = Math.Max(1, q.Quantity)
        };

        q.Status = "Accepted";
        q.AcceptedDate = AppTime.Now;
        q.Booking = booking;

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        return new AcceptQuotationResult
        {
            Success = true,
            Quotation = MapToContractorListItemDto(q),
            BookingID = booking.BookingID,
            SupplierID = q.SupplierID,
            SupplierEmail = q.Supplier.Email,
            SupplierName = q.Supplier.FullName,
            ListingTitle = q.Listing.ListingTitle
        };
    }

    public async Task<RejectQuotationResult> RejectAsync(int quotationId, int contractorId, string? reason)
    {
        var q = await _db.Quotations
            .Include(x => x.Listing)
            .Include(x => x.Supplier)
            .Include(x => x.Contractor)
            .FirstOrDefaultAsync(x => x.QuotationID == quotationId && x.ContractorID == contractorId);

        if (q == null)
            return new RejectQuotationResult { Success = false, Error = "Quotation not found." };

        if (q.Status is "Accepted" or "Rejected")
            return new RejectQuotationResult { Success = false, Error = "This quotation has already been processed." };

        if (q.Status != "Submitted")
            return new RejectQuotationResult
            {
                Success = false,
                Error = $"Quotation cannot be rejected because its current status is '{q.Status}'."
            };

        q.Status = "Rejected";
        q.RejectedDate = AppTime.Now;
        q.RejectionReason = reason;

        await _db.SaveChangesAsync();

        return new RejectQuotationResult
        {
            Success = true,
            Quotation = MapToContractorListItemDto(q),
            SupplierID = q.SupplierID,
            SupplierEmail = q.Supplier.Email,
            SupplierName = q.Supplier.FullName,
            ListingTitle = q.Listing.ListingTitle
        };
    }

    private static QuotationListItemDto MapToContractorListItemDto(Quotation q) => new()
    {
        QuotationID = q.QuotationID,
        ListingID = q.ListingID,
        ListingTitle = q.Listing.ListingTitle,
        ContractorName = q.Contractor?.FullName ?? string.Empty,
        SupplierName = q.Supplier?.FullName,
        Status = q.Status,
        RequestedDate = q.RequestedDate,
        QuoteValidUntil = q.QuoteValidUntil,
        RentalStartDate = q.RentalStartDate,
        RentalEndDate = q.RentalEndDate,
        Quantity = q.Quantity,
        EstimatedTotal = q.EstimatedTotal
    };

    private static QuotationListItemDto MapToListItemDto(Quotation q) => new()
    {
        QuotationID = q.QuotationID,
        ListingID = q.ListingID,
        ListingTitle = q.Listing.ListingTitle,
        ContractorName = q.Contractor.FullName,
        Status = q.Status,
        RequestedDate = q.RequestedDate,
        QuoteValidUntil = q.QuoteValidUntil,
        RentalStartDate = q.RentalStartDate,
        RentalEndDate = q.RentalEndDate,
        Quantity = q.Quantity,
        EstimatedTotal = q.EstimatedTotal
    };

    private static QuotationReviewDto MapToReviewDto(Quotation q) => new()
    {
        QuotationID = q.QuotationID,
        ListingID = q.ListingID,
        ListingTitle = q.Listing.ListingTitle,
        ContractorName = q.Contractor.FullName,
        Status = q.Status,
        RequestedDate = q.RequestedDate,
        QuoteValidUntil = q.QuoteValidUntil,
        RentalStartDate = q.RentalStartDate,
        RentalEndDate = q.RentalEndDate,
        Quantity = q.Quantity,
        DeliveryAddress = q.DeliveryAddress ?? string.Empty,
        SpecialRequirements = q.SpecialRequirements,
        FulfillmentMethod = InferFulfillmentMethod(q.DeliveryAddress),
        HireType = "Dry",
        DailyRateZAR = q.DailyRateZAR,
        WeeklyRateZAR = q.WeeklyRateZAR,
        DeliveryFee = q.DeliveryFee,
        NotesToCustomer = q.NotesToCustomer,
        DeliveryDistanceKm = q.DeliveryDistanceKm,
        RentalSubtotal = q.RentalSubtotal,
        DiscountPercent = q.DiscountPercent,
        DiscountAmount = q.DiscountAmount,
        PriceExclVat = q.PriceExclVat,
        VatRate = q.VatRate,
        PriceInclVat = q.PriceInclVat,
        SecurityDeposit = q.SecurityDeposit,
        DamageWaiverFee = q.DamageWaiverFee,
        EstimatedTotal = q.EstimatedTotal
    };

    private static string InferFulfillmentMethod(string? deliveryAddress)
    {
        var a = (deliveryAddress ?? string.Empty).Trim();
        if (a.Contains("pickup", StringComparison.OrdinalIgnoreCase)
            || a.Contains("collect", StringComparison.OrdinalIgnoreCase)
            || a.StartsWith("Pickup:", StringComparison.OrdinalIgnoreCase))
            return "Contractor Pickup";
        return "Supplier Delivery";
    }
}
