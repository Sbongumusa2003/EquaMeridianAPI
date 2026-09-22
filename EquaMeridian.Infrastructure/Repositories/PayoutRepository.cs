using EquaMeridian.DTOs.Payouts;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class PayoutRepository : IPayoutRepository
{
    private readonly AppDbContext _db;
    private readonly IPaymentSyncService _paymentSync;
    public PayoutRepository(AppDbContext db, IPaymentSyncService paymentSync)
    {
        _db = db;
        _paymentSync = paymentSync;
    }
    private static readonly string[] OpenPayoutStatuses = { "Pending", "Approved" };

    public async Task<IEnumerable<EligibleInvoiceDto>> GetEligibleInvoicesAsync(int supplierId)
    {
        var claimedInvoiceIds = await _db.Payouts
            .Where(p => p.SupplierID == supplierId && OpenPayoutStatuses.Contains(p.Status))
            .Select(p => p.InvoiceID)
            .ToListAsync();
        var candidates = await _db.Invoices
            .Include(i => i.Listing)
            .Include(i => i.Quotation)
            .Where(i => i.SupplierID == supplierId && !claimedInvoiceIds.Contains(i.InvoiceID))
            .ToListAsync();

        foreach (var inv in candidates.Where(i => i.PaymentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)))
        {
            inv.PaymentStatus = await _paymentSync.SyncIfPendingAsync(inv.InvoiceID, inv.PaymentStatus);
        }

        var invoices = candidates
            .Where(i => i.PaymentStatus == "Paid")
            .OrderByDescending(i => i.InvoiceDate)
            .ToList();

        return invoices.Select(i => new EligibleInvoiceDto
        {
            InvoiceID = i.InvoiceID,
            InvoiceNumber = i.InvoiceNumber,
            BookingID = i.Quotation?.BookingID,
            ListingTitle = i.Listing.ListingTitle,
            InvoiceDate = i.InvoiceDate,
            GrossAmount = i.TotalAmount,
            CommissionAmount = i.PlatformFeeAmount,
            VATAmount = i.VATAmount,
            PayoutAmount = i.SupplierPayableAmount
        });
    }

    public async Task<PayoutRequestResult> CreateRequestAsync(int supplierId, int invoiceId, string? supplierNotes)
    {
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId && i.SupplierID == supplierId);

        if (invoice == null)
            return new PayoutRequestResult { Success = false, ErrorCode = "NotFound", Error = "Invoice not found." };

        invoice.PaymentStatus = await _paymentSync.SyncIfPendingAsync(invoice.InvoiceID, invoice.PaymentStatus);

        if (invoice.PaymentStatus != "Paid")
            return new PayoutRequestResult
            {
                Success = false,
                ErrorCode = "NotEligible",
                Error = "A payout can only be requested once the contractor's payment has been confirmed as Paid."
            };

        var hasOpenRequest = await _db.Payouts.AnyAsync(p =>
            p.InvoiceID == invoiceId && OpenPayoutStatuses.Contains(p.Status));

        if (hasOpenRequest)
            return new PayoutRequestResult
            {
                Success = false,
                ErrorCode = "AlreadyExists",
                Error = "A payout request for this invoice is already pending or has already been paid out."
            };

        var payout = new Payout
        {
            InvoiceID = invoiceId,
            SupplierID = supplierId,
            GrossAmount = invoice.TotalAmount,
            CommissionAmount = invoice.PlatformFeeAmount,
            VATAmount = invoice.VATAmount,
            PayoutAmount = invoice.SupplierPayableAmount,
            Status = "Pending",
            SupplierNotes = supplierNotes,
            RequestedDate = AppTime.Now
        };

        _db.Payouts.Add(payout);
        await _db.SaveChangesAsync();

        var saved = await _db.Payouts
            .Include(p => p.Invoice).ThenInclude(i => i.Listing)
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Include(p => p.Supplier)
            .FirstAsync(p => p.PayoutID == payout.PayoutID);

        return new PayoutRequestResult { Success = true, Payout = MapToDto(saved) };
    }

    public async Task<IEnumerable<PayoutDto>> GetAllForSupplierAsync(int supplierId)
    {
        var payouts = await _db.Payouts
            .AsNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i.Listing)
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Include(p => p.Supplier)
            .Include(p => p.ProcessedByAdmin)
            .Where(p => p.SupplierID == supplierId)
            .OrderByDescending(p => p.RequestedDate)
            .ToListAsync();

        return payouts.Select(MapToDto);
    }

    public async Task<PayoutDto?> GetByIdForSupplierAsync(int payoutId, int supplierId)
    {
        var payout = await _db.Payouts
            .AsNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i.Listing)
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Include(p => p.Supplier)
            .Include(p => p.ProcessedByAdmin)
            .FirstOrDefaultAsync(p => p.PayoutID == payoutId && p.SupplierID == supplierId);

        return payout == null ? null : MapToDto(payout);
    }

    public async Task<(IEnumerable<SupplierPaymentHistoryItemDto> Items, int TotalCount)> GetHistoryForSupplierAsync(
        int supplierId, string? status, int page, int pageSize)
    {
        var q = _db.Payouts
            .AsNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Where(p => p.SupplierID == supplierId && p.Status == "Approved")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(p => p.Status == status);

        var total = await q.CountAsync();
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var payouts = await q
            .OrderByDescending(p => p.ProcessedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = payouts.Select(p => new SupplierPaymentHistoryItemDto
        {
            PayoutID = p.PayoutID,
            Date = p.ProcessedDate ?? p.RequestedDate,
            BookingID = p.Invoice.Quotation?.BookingID,
            InvoiceNumber = p.Invoice.InvoiceNumber,
            Amount = p.PayoutAmount,
            Status = p.Status
        });

        return (items, total);
    }

    public async Task<(IEnumerable<PayoutDto> Payouts, int TotalCount)> GetAllAsync(
        string? search, string? status, int page, int pageSize)
    {
        var q = _db.Payouts
            .AsNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i.Listing)
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Include(p => p.Supplier)
            .Include(p => p.ProcessedByAdmin)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(p =>
                p.PayoutID.ToString() == search ||
                p.Invoice.InvoiceNumber.Contains(search) ||
                p.Supplier.FullName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(p => p.Status == status);

        var total = await q.CountAsync();
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;

        var payouts = await q
            .OrderByDescending(p => p.RequestedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (payouts.Select(MapToDto), total);
    }

    public async Task<PayoutDto?> GetByIdAsync(int payoutId)
    {
        var payout = await _db.Payouts
            .AsNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i.Listing)
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Include(p => p.Supplier)
            .Include(p => p.ProcessedByAdmin)
            .FirstOrDefaultAsync(p => p.PayoutID == payoutId);

        return payout == null ? null : MapToDto(payout);
    }

    public async Task<PayoutRequestResult> ProcessAsync(
        int payoutId, string newStatus, string? administratorNotes, string? declineReason, int adminId)
    {
        var validStatuses = new[] { "Approved", "Declined" };
        if (!validStatuses.Contains(newStatus))
            return new PayoutRequestResult { Success = false, ErrorCode = "ValidationError", Error = "Status must be either 'Approved' or 'Declined'." };

        var payout = await _db.Payouts
            .Include(p => p.Invoice).ThenInclude(i => i.Listing)
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.PayoutID == payoutId);

        if (payout == null)
            return new PayoutRequestResult { Success = false, ErrorCode = "NotFound", Error = "Payout request not found." };

        if (payout.Status != "Pending")
            return new PayoutRequestResult { Success = false, ErrorCode = "AlreadyProcessed", Error = "This payout request has already been processed." };

        if (newStatus == "Declined" && string.IsNullOrWhiteSpace(declineReason))
            return new PayoutRequestResult { Success = false, ErrorCode = "ValidationError", Error = "A reason is required when declining a payout." };

        payout.Status = newStatus;
        payout.AdministratorNotes = administratorNotes;
        payout.DeclineReason = newStatus == "Declined" ? declineReason : null;
        payout.ProcessedByAdminID = adminId;
        payout.ProcessedDate = AppTime.Now;

        await _db.SaveChangesAsync();

        var saved = await _db.Payouts
            .Include(p => p.Invoice).ThenInclude(i => i.Listing)
            .Include(p => p.Invoice).ThenInclude(i => i.Quotation)
            .Include(p => p.Supplier)
            .Include(p => p.ProcessedByAdmin)
            .FirstAsync(p => p.PayoutID == payout.PayoutID);

        return new PayoutRequestResult { Success = true, Payout = MapToDto(saved) };
    }

    private static PayoutDto MapToDto(Payout p) => new()
    {
        PayoutID = p.PayoutID,
        InvoiceID = p.InvoiceID,
        InvoiceNumber = p.Invoice.InvoiceNumber,
        BookingID = p.Invoice.Quotation?.BookingID,
        ListingTitle = p.Invoice.Listing.ListingTitle,
        SupplierName = p.Supplier.FullName,
        GrossAmount = p.GrossAmount,
        CommissionAmount = p.CommissionAmount,
        VATAmount = p.VATAmount,
        PayoutAmount = p.PayoutAmount,
        Status = p.Status,
        SupplierNotes = p.SupplierNotes,
        AdministratorNotes = p.AdministratorNotes,
        DeclineReason = p.DeclineReason,
        ProcessedByAdminName = p.ProcessedByAdmin?.FullName,
        RequestedDate = p.RequestedDate,
        ProcessedDate = p.ProcessedDate
    };
}
