using EquaMeridian.DTOs.Refunds;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class RefundRepository : IRefundRepository
{
    private readonly AppDbContext _db;
    private readonly IPaymentSyncService _paymentSync;
    public RefundRepository(AppDbContext db, IPaymentSyncService paymentSync)
    {
        _db = db;
        _paymentSync = paymentSync;
    }

    public async Task<(IEnumerable<RefundDto>, int)> GetAllAsync(
        string? search, string? status, int page, int pageSize)
    {
        var q = _db.Refunds
            .AsNoTracking()
            .Include(r => r.Dispute)
            .Include(r => r.RequestedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(r =>
                r.RefundID.ToString() == search ||
                (r.DisputeID != null && r.DisputeID.ToString() == search) ||
                r.RequestedBy.FullName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status);

        var total = await q.CountAsync();
        var refunds = await q
            .OrderByDescending(r => r.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (refunds.Select(MapToDto), total);
    }

    public async Task<RefundDto?> GetByIdAsync(int refundId)
    {
        var refund = await _db.Refunds
            .AsNoTracking()
            .Include(r => r.Dispute)
            .Include(r => r.RequestedBy)
            .FirstOrDefaultAsync(r => r.RefundID == refundId);

        return refund == null ? null : MapToDto(refund);
    }

    public async Task<RefundRequestResult> CreateContractorRequestAsync(int contractorId, int invoiceId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return new RefundRequestResult { Success = false, ErrorCode = "ValidationError", Error = "This field is required." };

        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId && i.ContractorID == contractorId);

        if (invoice == null)
            return new RefundRequestResult { Success = false, ErrorCode = "NotFound", Error = "Payment record not found for this booking." };

        invoice.PaymentStatus = await _paymentSync.SyncIfPendingAsync(invoice.InvoiceID, invoice.PaymentStatus);

        if (invoice.PaymentStatus != "Paid")
            return new RefundRequestResult
            {
                Success = false,
                ErrorCode = "NotEligible",
                Error = "A refund can only be requested once payment has been confirmed as Paid."
            };

        var hasOpenRequest = await _db.Refunds.AnyAsync(r =>
            r.InvoiceID == invoiceId && r.Status == "Pending");

        if (hasOpenRequest)
            return new RefundRequestResult
            {
                Success = false,
                ErrorCode = "AlreadyExists",
                Error = "A refund request for this payment is already in progress."
            };

        var refund = new Refund
        {
            InvoiceID = invoiceId,
            Amount = invoice.TotalAmount,
            RequestedByUserID = contractorId,
            Reason = reason,
            Status = "Pending",
            CreatedDate = AppTime.Now
        };

        _db.Refunds.Add(refund);
        await _db.SaveChangesAsync();

        var saved = await _db.Refunds
            .Include(r => r.RequestedBy)
            .FirstAsync(r => r.RefundID == refund.RefundID);

        return new RefundRequestResult { Success = true, Refund = MapToDto(saved) };
    }

    public async Task<RefundRequestResult> ProcessAsync(int refundId, string newStatus, string? administratorNotes, string? failureReason)
    {
        var validStatuses = new[] { "Processed", "Rejected" };
        if (!validStatuses.Contains(newStatus))
            return new RefundRequestResult { Success = false, ErrorCode = "ValidationError", Error = "Status must be either 'Processed' or 'Rejected'." };

        var refund = await _db.Refunds
            .Include(r => r.Invoice)
            .Include(r => r.RequestedBy)
            .FirstOrDefaultAsync(r => r.RefundID == refundId);

        if (refund == null)
            return new RefundRequestResult { Success = false, ErrorCode = "NotFound", Error = "Refund not found." };

        if (refund.Status != "Pending")
            return new RefundRequestResult { Success = false, ErrorCode = "AlreadyProcessed", Error = "This refund has already been processed." };

        if (newStatus == "Rejected" && string.IsNullOrWhiteSpace(failureReason))
            return new RefundRequestResult { Success = false, ErrorCode = "ValidationError", Error = "A failure reason is required when declining a refund." };

        refund.Status = newStatus;
        refund.AdministratorNotes = administratorNotes;
        refund.FailureReason = newStatus == "Rejected" ? failureReason : null;
        refund.ProcessedDate = AppTime.Now;
        refund.DateUpdated = AppTime.Now;
        if (newStatus == "Processed" && refund.InvoiceID.HasValue)
        {
            var invoice = refund.Invoice ?? await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == refund.InvoiceID.Value);
            if (invoice != null)
            {
                // Per the lecturer's note: the refund itself already happened outside the platform
                // (manually, via PayFast's own dashboard/EFT) — we only reflect that here by updating
                // status. We never call the gateway to trigger a refund from this endpoint.
                invoice.PaymentStatus = "Refunded";
                invoice.LastSyncedDate = AppTime.Now;
            }
        }

        await _db.SaveChangesAsync();

        var saved = await _db.Refunds
            .Include(r => r.RequestedBy)
            .FirstAsync(r => r.RefundID == refund.RefundID);

        return new RefundRequestResult { Success = true, Refund = MapToDto(saved) };
    }

    public async Task<RefundDto?> GetByIdForContractorAsync(int refundId, int contractorId)
    {
        var refund = await _db.Refunds
            .AsNoTracking()
            .Include(r => r.Dispute)
            .Include(r => r.RequestedBy)
            .FirstOrDefaultAsync(r => r.RefundID == refundId && r.RequestedByUserID == contractorId);

        return refund == null ? null : MapToDto(refund);
    }

    public async Task<IEnumerable<RefundDto>> GetAllForContractorAsync(int contractorId)
    {
        var refunds = await _db.Refunds
            .AsNoTracking()
            .Include(r => r.Dispute)
            .Include(r => r.RequestedBy)
            .Where(r => r.RequestedByUserID == contractorId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();

        return refunds.Select(MapToDto);
    }
    private static RefundDto MapToDto(Refund r) => new()
    {
        RefundID = r.RefundID,
        DisputeID = r.DisputeID,
        InvoiceID = r.InvoiceID,
        RequestedByUserID = r.RequestedByUserID,
            PartyName = r.RequestedBy.FullName,
        Amount = r.Amount,
        Status = r.Status,
        Reason = r.Reason,
        AdministratorNotes = r.AdministratorNotes,
        PaymentGatewayReference = r.PaymentGatewayReference,
        FailureReason = r.FailureReason,
        CreatedDate = r.CreatedDate,
        DateUpdated = r.DateUpdated,
        ProcessedDate = r.ProcessedDate
    };
}
