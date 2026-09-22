using EquaMeridian.DTOs.Reports;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class SupplierReportingRepository : ISupplierReportingRepository
{
    private readonly AppDbContext _db;
    public SupplierReportingRepository(AppDbContext db) => _db = db;

    public async Task<SupplierReportSummaryDto> GetMySummaryAsync(int supplierId, DateTime? from, DateTime? to)
    {
        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.SupplierID == supplierId && i.Status != "Cancelled");

        if (from.HasValue) query = query.Where(i => i.InvoiceDate >= from.Value);
        if (to.HasValue) query = query.Where(i => i.InvoiceDate <= to.Value);

        var rows = await query
            .Select(i => new
            {
                i.InvoiceDate,
                i.Subtotal,
                i.PlatformFeeAmount,
                i.SupplierPayableAmount
            })
            .ToListAsync();

        var monthly = rows
            .GroupBy(r => new { r.InvoiceDate.Year, r.InvoiceDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new SupplierMonthlyReportDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                MonthLabel = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                BookingCount = g.Count(),
                GrossRevenue = g.Sum(x => x.Subtotal),
                CommissionAmount = g.Sum(x => x.PlatformFeeAmount),
                NetPayout = g.Sum(x => x.SupplierPayableAmount)
            })
            .ToList();

        return new SupplierReportSummaryDto
        {
            From = from,
            To = to,
            MonthlyBreakdown = monthly,
            GrandBookingCount = rows.Count,
            GrandGrossRevenue = rows.Sum(r => r.Subtotal),
            GrandCommissionAmount = rows.Sum(r => r.PlatformFeeAmount),
            GrandNetPayout = rows.Sum(r => r.SupplierPayableAmount)
        };
    }
}
