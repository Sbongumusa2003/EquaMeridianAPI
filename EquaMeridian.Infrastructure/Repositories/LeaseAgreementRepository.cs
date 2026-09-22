using EquaMeridian.DTOs.LeaseAgreements;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class LeaseAgreementRepository : ILeaseAgreementRepository
{
    private const string DefaultStandardTerms =
        "The Contractor shall use the machinery solely for the purposes described in the listing and " +
        "shall return it in the condition received, ordinary wear and tear excepted.";

    private const string DefaultCancellationPolicy =
        "Cancellations made less than 48 hours before the rental start date may be subject to a " +
        "cancellation fee as configured by the platform.";

    private const string DefaultLiabilityTerms =
        "The Contractor assumes liability for loss or damage to the machinery while in their possession, " +
        "except where caused by the Supplier's negligence. Insurance requirements are as agreed between the parties.";

    private const string DefaultMaintenanceProvisions =
        "Routine maintenance during the rental period is the Contractor's responsibility. Mechanical faults " +
        "not caused by misuse must be reported to the Supplier immediately.";

    private readonly AppDbContext _db;
    public LeaseAgreementRepository(AppDbContext db) => _db = db;

    public async Task<LeaseAgreement> CreateFromQuotationAsync(int quotationId, int bookingId)
    {
        var quotation = await _db.Quotations
            .Include(q => q.Listing)
            .FirstOrDefaultAsync(q => q.QuotationID == quotationId)
            ?? throw new InvalidOperationException("Quotation not found.");

        var agreementDate = AppTime.Now;
        var agreementNumber = await GenerateAgreementNumberAsync(agreementDate);

        var agreement = new LeaseAgreement
        {
            AgreementNumber = agreementNumber,
            QuotationID = quotation.QuotationID,
            BookingID = bookingId,
            ListingID = quotation.ListingID,
            SupplierID = quotation.SupplierID,
            ContractorID = quotation.ContractorID,
            RentalStartDate = quotation.RentalStartDate,
            RentalEndDate = quotation.RentalEndDate,
            Quantity = quotation.Quantity,
            TotalAmount = quotation.EstimatedTotal,
            PaymentDueDate = agreementDate.AddDays(30),
            PaymentMethod = quotation.EstimatedTotal > 50000m ? "EFT" : "PayFast",
            StandardTerms = DefaultStandardTerms,
            CancellationPolicy = DefaultCancellationPolicy,
            LiabilityAndInsuranceTerms = DefaultLiabilityTerms,
            DamageAndMaintenanceProvisions = DefaultMaintenanceProvisions,
            Status = "Pending_Supplier",
            CreatedDate = agreementDate
        };

        _db.LeaseAgreements.Add(agreement);
        await _db.SaveChangesAsync();

        return agreement;
    }

    public async Task<LeaseAgreementDetailDto?> GetByIdAsync(int leaseAgreementId, int userId, string role)
    {
        var agreement = await _db.LeaseAgreements
            .AsNoTracking()
            .Include(a => a.Listing).ThenInclude(l => l.Supplier)
            .Include(a => a.Supplier)
            .Include(a => a.Contractor)
            .Include(a => a.Quotation)
            .FirstOrDefaultAsync(a => a.LeaseAgreementID == leaseAgreementId);

        if (agreement == null) return null;

        var isAdmin = role.Equals("admin", StringComparison.OrdinalIgnoreCase);
        var isParty = agreement.ContractorID == userId || agreement.SupplierID == userId;
        if (!isAdmin && !isParty) return null;

        var category = await _db.Categories
            .Where(c => c.CategoryID == agreement.Listing.CategoryID)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();

        return MapToDetailDto(agreement, category, userId, role);
    }

    public async Task<(IEnumerable<LeaseAgreementListItemDto>, int)> GetAllForUserAsync(
        int userId, string role, int page, int pageSize)
    {
        var q = _db.LeaseAgreements.AsNoTracking().Include(a => a.Listing).AsQueryable();

        if (!role.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            q = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase)
                ? q.Where(a => a.ContractorID == userId)
                : q.Where(a => a.SupplierID == userId);
        }

        var total = await q.CountAsync();
        var agreements = await q
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (agreements.Select(a => new LeaseAgreementListItemDto
        {
            LeaseAgreementID = a.LeaseAgreementID,
            AgreementNumber = a.AgreementNumber,
            ListingTitle = a.Listing.ListingTitle,
            Status = a.Status,
            RentalStartDate = a.RentalStartDate,
            RentalEndDate = a.RentalEndDate,
            CanSign = CanCurrentUserSign(a, userId, role)
        }), total);
    }

    public async Task<SignLeaseAgreementResult> SignAsync(
        int leaseAgreementId, int userId, string role, SignLeaseAgreementDto dto)
    {
        var agreement = await _db.LeaseAgreements
            .Include(a => a.Listing)
            .Include(a => a.Supplier)
            .Include(a => a.Contractor)
            .Include(a => a.Quotation)
            .FirstOrDefaultAsync(a => a.LeaseAgreementID == leaseAgreementId);

        if (agreement == null)
            return new SignLeaseAgreementResult { Success = false, Error = "Agreement not found or you do not have permission to view this agreement." };

        var isSupplier = role.Equals("Supplier", StringComparison.OrdinalIgnoreCase) && agreement.SupplierID == userId;
        var isContractor = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase) && agreement.ContractorID == userId;
        if (!isSupplier && !isContractor)
            return new SignLeaseAgreementResult { Success = false, Error = "Agreement not found or you do not have permission to view this agreement." };

        if (!dto.AcknowledgeTermsRead || !dto.AcknowledgeLegallyBinding)
            return new SignLeaseAgreementResult { Success = false, Error = "Please acknowledge both statements before signing." };

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return new SignLeaseAgreementResult { Success = false, Error = "Please enter your full name." };

        if (!CanCurrentUserSign(agreement, userId, role))
            return new SignLeaseAgreementResult { Success = false, Error = "The agreement status has changed. Please refresh and try again." };

        var currentUser = isSupplier ? agreement.Supplier : agreement.Contractor;
        var otherUser = isSupplier ? agreement.Contractor : agreement.Supplier;

        if (currentUser.AccountStatus != "Active")
            return new SignLeaseAgreementResult { Success = false, Error = "Your account is not active. Please contact support for assistance." };

        if (otherUser.AccountStatus != "Active")
            return new SignLeaseAgreementResult { Success = false, Error = "The other party's account is not active. Please contact support for assistance." };

        if (agreement.Listing.AvailabilityStatus != "Active" && agreement.Status != "Active")
            return new SignLeaseAgreementResult { Success = false, Error = "The associated listing is no longer active." };

        var previousStatus = agreement.Status;

        if (isSupplier)
        {
            agreement.SupplierSigned = true;
            agreement.SupplierSignatureName = dto.FullName;
            agreement.SupplierDigitalSignature = dto.DigitalSignature;
            agreement.SupplierSignedDate = AppTime.Now;
        }
        else
        {
            agreement.ContractorSigned = true;
            agreement.ContractorSignatureName = dto.FullName;
            agreement.ContractorDigitalSignature = dto.DigitalSignature;
            agreement.ContractorSignedDate = AppTime.Now;
        }

        var fullyExecuted = agreement.SupplierSigned && agreement.ContractorSigned;
        agreement.Status = fullyExecuted
            ? "Active"
            : (agreement.SupplierSigned ? "Pending_Contractor" : "Pending_Supplier");

        agreement.UpdatedDate = AppTime.Now;
        agreement.UpdatedByUserID = userId;
        if (fullyExecuted)
        {
            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.BookingID == agreement.BookingID);
            if (booking != null && booking.Status == "AwaitingSignature")
            {
                booking.Status = "AwaitingPayment";
                _db.BookingStatusHistories.Add(new BookingStatusHistory
                {
                    BookingID = booking.BookingID,
                    Stage = booking.Status,
                    ChangedByUserID = userId,
                    ChangedByRole = "System",
                    Notes = $"Lease agreement {agreement.AgreementNumber} fully executed — awaiting payment.",
                    CreatedDate = AppTime.Now
                });
            }
        }

        await _db.SaveChangesAsync();

        var category = await _db.Categories
            .Where(c => c.CategoryID == agreement.Listing.CategoryID)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();

        return new SignLeaseAgreementResult
        {
            Success = true,
            Agreement = MapToDetailDto(agreement, category, userId, role),
            FullyExecuted = fullyExecuted,
            OtherPartyID = otherUser.UserID,
            OtherPartyEmail = otherUser.Email,
            OtherPartyName = otherUser.FullName,
            SignerID = currentUser.UserID,
            SignerEmail = currentUser.Email,
            SignerName = currentUser.FullName
        };
    }

    private static bool CanCurrentUserSign(LeaseAgreement a, int userId, string role)
    {
        var isSupplier = role.Equals("Supplier", StringComparison.OrdinalIgnoreCase) && a.SupplierID == userId;
        var isContractor = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase) && a.ContractorID == userId;

        if (isSupplier) return a.Status == "Pending_Supplier" && !a.SupplierSigned;
        if (isContractor) return a.Status == "Pending_Contractor" && !a.ContractorSigned;
        return false;
    }

    private async Task<string> GenerateAgreementNumberAsync(DateTime agreementDate)
    {
        var datePart = agreementDate.ToString("yyyyMMdd");
        var prefix = $"LA-{datePart}";

        var countToday = await _db.LeaseAgreements.CountAsync(a => a.AgreementNumber.StartsWith(prefix));
        var sequence = (countToday + 1).ToString("D4");

        return $"{prefix}{sequence}";
    }

    private static LeaseAgreementDetailDto MapToDetailDto(LeaseAgreement a, string? category, int userId, string role) => new()
    {
        LeaseAgreementID = a.LeaseAgreementID,
        AgreementNumber = a.AgreementNumber,
        Status = a.Status,
        SupplierName = a.Supplier.FullName,
        ContractorName = a.Contractor.FullName,
        ListingTitle = a.Listing.ListingTitle,
        Category = category ?? string.Empty,
        Make = a.Listing.MakeBrand,
        Model = a.Listing.Model,
        Year = a.Listing.Year,
        Location = a.Listing.Location,
        RentalStartDate = a.RentalStartDate,
        RentalEndDate = a.RentalEndDate,
        Quantity = a.Quantity,
        RentalSubtotal = a.Quotation.RentalSubtotal,
        DiscountAmount = a.Quotation.DiscountAmount,
        DeliveryFee = a.Quotation.DeliveryFee,
        PriceExclVat = a.Quotation.PriceExclVat,
        VatRate = a.Quotation.VatRate,
        VatAmount = a.Quotation.PriceInclVat.HasValue && a.Quotation.PriceExclVat.HasValue
            ? a.Quotation.PriceInclVat.Value - a.Quotation.PriceExclVat.Value
            : null,
        // Same source (Quotation.PriceInclVat) as the cart's grand total, so this always matches
        // what the contractor saw at checkout — see cart's GroupTotal / GrandTotal.
        TotalAmount = a.TotalAmount,
        DepositAmount = a.DepositAmount,
        PaymentDueDate = a.PaymentDueDate,
        PaymentMethod = a.PaymentMethod,
        StandardTerms = a.StandardTerms,
        SpecialConditions = a.SpecialConditions,
        CancellationPolicy = a.CancellationPolicy,
        LiabilityAndInsuranceTerms = a.LiabilityAndInsuranceTerms,
        DamageAndMaintenanceProvisions = a.DamageAndMaintenanceProvisions,
        SupplierSigned = a.SupplierSigned,
        SupplierSignatureName = a.SupplierSignatureName,
        SupplierSignedDate = a.SupplierSignedDate,
        ContractorSigned = a.ContractorSigned,
        ContractorSignatureName = a.ContractorSignatureName,
        ContractorSignedDate = a.ContractorSignedDate,
        CanSign = CanCurrentUserSign(a, userId, role)
    };

    public async Task<LeaseExecutionStatus?> GetExecutionStatusForQuotationAsync(int quotationId)
    {
        var agreement = await _db.LeaseAgreements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.QuotationID == quotationId);

        if (agreement == null) return null;

        return new LeaseExecutionStatus
        {
            LeaseAgreementID = agreement.LeaseAgreementID,
            SupplierSigned = agreement.SupplierSigned,
            ContractorSigned = agreement.ContractorSigned
        };
    }
}
