using EquaMeridian.DTOs.Disputes;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class DisputeRepository : IDisputeRepository
{
    private readonly AppDbContext _db;
    public DisputeRepository(AppDbContext db) => _db = db;

    public async Task<(IEnumerable<DisputeListItemDto>, int)> GetAllAsync(
        string? search, string? status, int page, int pageSize)
    {
        var q = _db.Disputes
            .AsNoTracking()
            .Include(d => d.Contractor)
            .Include(d => d.Supplier)
            .Include(d => d.Listing)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(d =>
                d.DisputeID.ToString() == search ||
                d.Contractor.FullName.Contains(search) ||
                d.Supplier.FullName.Contains(search) ||
                (d.Listing != null && d.Listing.ListingTitle.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(d => d.Status == status);

        var total = await q.CountAsync();
        var disputes = await q
            .OrderByDescending(d => d.RaisedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (disputes.Select(MapToListItemDto), total);
    }

    public async Task<DisputeDetailDto?> GetForReviewAsync(int disputeId)
    {
        var dispute = await _db.Disputes
            .Include(d => d.Contractor)
            .Include(d => d.Supplier)
            .Include(d => d.Listing)
            .FirstOrDefaultAsync(d => d.DisputeID == disputeId);

        if (dispute == null) return null;

        if (dispute.Status == "Open")
        {
            dispute.Status = "Under Review";
            await _db.SaveChangesAsync();
        }

        return MapToDetailDto(dispute);
    }

    public async Task<(IEnumerable<DisputeListItemDto>, int)> GetForUserAsync(
        int userId, int page, int pageSize)
    {
        var q = _db.Disputes
            .AsNoTracking()
            .Include(d => d.Contractor)
            .Include(d => d.Supplier)
            .Include(d => d.Listing)
            .Where(d => d.ContractorID == userId || d.SupplierID == userId)
            .AsQueryable();

        var total = await q.CountAsync();
        var disputes = await q
            .OrderByDescending(d => d.RaisedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (disputes.Select(MapToListItemDto), total);
    }

    public async Task<DisputeDetailDto?> GetForPartyAsync(int disputeId, int userId)
    {
        var dispute = await _db.Disputes
            .AsNoTracking()
            .Include(d => d.Contractor)
            .Include(d => d.Supplier)
            .Include(d => d.Listing)
            .FirstOrDefaultAsync(d => d.DisputeID == disputeId
                && (d.ContractorID == userId || d.SupplierID == userId));
        return dispute == null ? null : MapToDetailDto(dispute);
    }

    public async Task<DisputeResolutionResult> ResolveAsync(int disputeId, ResolveDisputeDto dto, int adminId)
    {
        var dispute = await _db.Disputes
            .Include(d => d.Contractor)
            .Include(d => d.Supplier)
            .Include(d => d.Listing)
            .FirstOrDefaultAsync(d => d.DisputeID == disputeId);

        if (dispute == null)
            return new DisputeResolutionResult { Success = false, Error = "Dispute not found." };

        if (dispute.Status is not ("Open" or "Under Review"))
            return new DisputeResolutionResult
            {
                Success = false,
                Error = $"Dispute cannot be resolved because its current status is '{dispute.Status}'."
            };

        var requiresRefund = dto.ResolutionOutcome is "UpholdRefund" or "PartialRefund";
        if (requiresRefund)
        {
            if (dto.RefundAmount is not > 0)
                return new DisputeResolutionResult { Success = false, Error = "Refund amount must be a positive number." };

            if (dto.RefundAmount > dispute.BookingAmount)
                return new DisputeResolutionResult { Success = false, Error = "Refund amount cannot exceed the original booking amount." };
        }

        var previousStatus = dispute.Status;

        dispute.Status = dto.ResolutionOutcome == "Escalate" ? "Escalated" : "Resolved";
        dispute.ResolutionType = dto.ResolutionOutcome;
        dispute.ResolutionNotes = dto.ResolutionNotes;
        dispute.ResolvedByAdminID = adminId;
        dispute.ResolvedDate = AppTime.Now;

        int? createdRefundId = null;
        if (requiresRefund)
        {
            var refund = new Refund
            {
                DisputeID = dispute.DisputeID,
                Amount = dto.RefundAmount!.Value,
                RequestedByUserID = dispute.ContractorID,
                Status = "Pending",
                CreatedDate = AppTime.Now
            };
            _db.Refunds.Add(refund);
            await _db.SaveChangesAsync();
            createdRefundId = refund.RefundID;
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        return new DisputeResolutionResult
        {
            Success = true,
            Dispute = MapToDetailDto(dispute),
            PreviousStatus = previousStatus,
            CreatedRefundId = createdRefundId,
            ContractorID = dispute.ContractorID,
            ContractorEmail = dispute.Contractor.Email,
            ContractorName = dispute.Contractor.FullName,
            SupplierID = dispute.SupplierID,
            SupplierEmail = dispute.Supplier.Email,
            SupplierName = dispute.Supplier.FullName
        };
    }

    private static readonly HashSet<string> ValidCategories = new()
    {
        "Non-delivery/late delivery", "Payment or invoicing issue",
        "Damage assessment disagreement", "Other"
    };
    public async Task<RaiseDisputeResult> RaiseAsync(
        int bookingId, int complainantId, string complainantRole,
        RaiseDisputeDto dto, IReadOnlyList<string> evidencePaths)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Supplier)
            .Include(b => b.Contractor)
            .Include(b => b.Delivery)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId
                && (b.SupplierID == complainantId || b.ContractorID == complainantId));

        if (booking == null)
            return new RaiseDisputeResult { Success = false, Error = "Booking not found." };

        var reachedInProgress = booking.Status is "Return Requested" or "Completed"
            || (booking.Status == "Confirmed" && booking.Delivery?.Status == "Delivered");
        if (!reachedInProgress)
            return new RaiseDisputeResult
            {
                Success = false,
                Error = "A dispute may only be raised against a booking that has reached at least 'In Progress' status."
            };

        var hasOpenDispute = await _db.Disputes.AnyAsync(d =>
            d.BookingID == bookingId && (d.Status == "Open" || d.Status == "Under Review"));
        if (hasOpenDispute)
            return new RaiseDisputeResult
            {
                Success = false,
                Error = "A dispute is already open for this booking. Please check My Disputes for its status."
            };

        if (!ValidCategories.Contains(dto.DisputeCategory))
            return new RaiseDisputeResult { Success = false, Error = "Please select a valid dispute category." };

        if (string.IsNullOrWhiteSpace(dto.Description) || string.IsNullOrWhiteSpace(dto.DesiredResolution))
            return new RaiseDisputeResult { Success = false, Error = "Description and desired resolution are required." };

        var isComplainantContractor = booking.ContractorID == complainantId;
        var respondent = isComplainantContractor ? booking.Supplier : booking.Contractor;

        var leaseDays = Math.Max(1, (booking.RentalEndDate.Date - booking.RentalStartDate.Date).Days);
        var bookingAmount = leaseDays * booking.Listing.DailyRateZAR;

        var dispute = new Dispute
        {
            ContractorID = booking.ContractorID,
            Contractor = booking.Contractor,
            SupplierID = booking.SupplierID,
            Supplier = booking.Supplier,
            ListingID = booking.ListingID,
            Listing = booking.Listing,
            BookingID = booking.BookingID,
            BookingReference = $"BK-{booking.BookingID}",
            BookingAmount = bookingAmount,
            RaisedByUserID = complainantId,
            RespondentID = respondent.UserID,
            ReasonCategory = dto.DisputeCategory,
            ComplaintDescription = dto.Description,
            DesiredResolution = dto.DesiredResolution,
            EvidenceUrls = evidencePaths.Count > 0 ? string.Join(",", evidencePaths) : null,
            Status = "Open",
            RaisedDate = AppTime.Now
        };

        _db.Disputes.Add(dispute);
        await _db.SaveChangesAsync();

        var complainantName = isComplainantContractor ? booking.Contractor.FullName : booking.Supplier.FullName;

        return new RaiseDisputeResult
        {
            Success = true,
            Dispute = MapToDetailDto(dispute),
            RespondentID = respondent.UserID,
            RespondentEmail = respondent.Email,
            RespondentName = respondent.FullName,
            ComplainantName = complainantName
        };
    }

    private static DisputeListItemDto MapToListItemDto(Dispute d) => new()
    {
        DisputeID = d.DisputeID,
        ContractorName = d.Contractor.FullName,
        SupplierName = d.Supplier.FullName,
        ListingID = d.ListingID,
        ListingTitle = d.Listing?.ListingTitle,
        ReasonCategory = d.ReasonCategory,
        Status = d.Status,
        RaisedDate = d.RaisedDate
    };

    private static DisputeDetailDto MapToDetailDto(Dispute d) => new()
    {
        DisputeID = d.DisputeID,
        ContractorName = d.Contractor.FullName,
        SupplierName = d.Supplier.FullName,
        ListingID = d.ListingID,
        ListingTitle = d.Listing?.ListingTitle,
        ReasonCategory = d.ReasonCategory,
        Status = d.Status,
        RaisedDate = d.RaisedDate,
        ContractorContact = d.Contractor.Email,
        SupplierContact = d.Supplier.Email,
        BookingID = d.BookingID,
        BookingReference = d.BookingReference,
        BookingAmount = d.BookingAmount,
        ComplaintDescription = d.ComplaintDescription,
        DesiredResolution = d.DesiredResolution,
        EvidenceUrls = string.IsNullOrWhiteSpace(d.EvidenceUrls)
            ? new List<string>()
            : d.EvidenceUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        ResolutionType = d.ResolutionType,
        ResolutionNotes = d.ResolutionNotes,
        ResolvedDate = d.ResolvedDate
    };
}
