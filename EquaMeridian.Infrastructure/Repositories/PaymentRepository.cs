using EquaMeridian.DTOs.Payments;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _db;
    public PaymentRepository(AppDbContext db) => _db = db;

    public async Task<Invoice?> GetPaymentForBookingAsync(int bookingId, int userId)
    {
        return await _db.Invoices
            .Include(i => i.Quotation)
            .FirstOrDefaultAsync(i =>
                i.Quotation.BookingID == bookingId && (i.ContractorID == userId || i.SupplierID == userId));
    }

    public async Task SaveSyncedStatusAsync(Invoice invoice, string newStatus)
    {
        invoice.PaymentStatus = newStatus;
        invoice.LastSyncedDate = AppTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task<Invoice?> GetInvoiceForPaymentAsync(int invoiceId, int contractorId)
    {
        return await _db.Invoices
            .Include(i => i.Quotation)
            .Include(i => i.Contractor)
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId && i.ContractorID == contractorId);
    }

    public async Task<Invoice?> GetByIdAsync(int invoiceId)
    {
        return await _db.Invoices
            .Include(i => i.Quotation)
            .Include(i => i.Contractor)
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
    }

    public async Task SetGatewayReferenceAsync(int invoiceId, string gatewayReference)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
        if (invoice == null) return;
        invoice.GatewayReference = gatewayReference;
        await _db.SaveChangesAsync();
    }

    public async Task<(IEnumerable<PaymentHistoryItemDto>, int)> GetHistoryAsync(
        int contractorId, PaymentHistoryQueryDto query)
    {
        var q = _db.Invoices
            .Include(i => i.Quotation)
            .Where(i => i.ContractorID == contractorId || i.SupplierID == contractorId)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "All")
            q = q.Where(i => i.PaymentStatus == query.Status);

        if (query.From.HasValue)
            q = q.Where(i => i.InvoiceDate >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(i => i.InvoiceDate <= query.To.Value);

        q = query.Sort == "Oldest"
            ? q.OrderBy(i => i.InvoiceDate)
            : q.OrderByDescending(i => i.InvoiceDate);

        var total = await q.CountAsync();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var invoices = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = invoices.Select(i => new PaymentHistoryItemDto
        {
            PaymentID = i.InvoiceID,
            Date = i.InvoiceDate,
            BookingID = i.Quotation != null ? (i.Quotation.BookingID ?? 0) : 0,
            Amount = i.TotalAmount,
            Status = i.PaymentStatus
        });

        return (items, total);
    }

    public async Task<ReceiptData?> GetReceiptDataAsync(int invoiceId, int contractorId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Quotation)
            .Include(i => i.Contractor)
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId && i.ContractorID == contractorId);

        if (invoice == null) return null;

        return new ReceiptData
        {
            PaymentID = invoice.InvoiceID,
            Amount = invoice.TotalAmount,
            VATAmount = invoice.VATAmount,
            TransactionDate = invoice.InvoiceDate,
            BookingID = invoice.Quotation.BookingID ?? 0,
            SupplierName = invoice.Supplier.FullName,
            ContractorName = invoice.Contractor.FullName,
            InvoiceNumber = invoice.InvoiceNumber,
            PaymentStatus = invoice.PaymentStatus
        };
    }
}