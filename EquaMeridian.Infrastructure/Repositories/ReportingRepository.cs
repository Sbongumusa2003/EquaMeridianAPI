using EquaMeridian.DTOs.Reports;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class ReportingRepository : IReportingRepository
{
    private readonly AppDbContext _db;
    public ReportingRepository(AppDbContext db) => _db = db;

    public async Task<RevenueControlBreakReportDto> GetRevenueControlBreakReportAsync(
        DateTime? from, DateTime? to, int? categoryId)
    {
        var query = _db.Invoices.AsNoTracking().Where(i => i.Status != "Cancelled");

        if (from.HasValue) query = query.Where(i => i.InvoiceDate >= from.Value);
        if (to.HasValue) query = query.Where(i => i.InvoiceDate <= to.Value);
        if (categoryId.HasValue) query = query.Where(i => i.Listing.CategoryID == categoryId.Value);

        var rows = await query
            .Select(i => new
            {
                i.InvoiceDate,
                i.Subtotal,
                i.PlatformFeeAmount,
                i.TotalAmount,
                CategoryID = i.Listing.CategoryID
            })
            .ToListAsync();

        var categoryIds = rows.Select(r => r.CategoryID).Distinct().ToList();
        var categoryNames = await _db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.CategoryID))
            .ToDictionaryAsync(c => c.CategoryID, c => c.Name);

        var categories = rows
            .GroupBy(r => r.CategoryID)
            .Select(g => new RevenueCategoryBreakDto
            {
                CategoryID = g.Key,
                CategoryName = categoryNames.TryGetValue(g.Key, out var name) ? name : "Uncategorized",
                Months = g.GroupBy(x => new { x.InvoiceDate.Year, x.InvoiceDate.Month })
                    .OrderBy(mg => mg.Key.Year).ThenBy(mg => mg.Key.Month)
                    .Select(mg => new RevenueMonthBreakDto
                    {
                        Year = mg.Key.Year,
                        Month = mg.Key.Month,
                        MonthLabel = new DateTime(mg.Key.Year, mg.Key.Month, 1).ToString("MMMM yyyy"),
                        TransactionCount = mg.Count(),
                        Subtotal = mg.Sum(x => x.Subtotal),
                        PlatformFeeAmount = mg.Sum(x => x.PlatformFeeAmount),
                        TotalAmount = mg.Sum(x => x.TotalAmount)
                    })
                    .ToList(),
                CategoryTransactionCount = g.Count(),
                CategorySubtotal = g.Sum(x => x.Subtotal),
                CategoryPlatformFeeAmount = g.Sum(x => x.PlatformFeeAmount),
                CategoryTotalAmount = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(c => c.CategoryTotalAmount)
            .ToList();

        return new RevenueControlBreakReportDto
        {
            From = from,
            To = to,
            Categories = categories,
            GrandTransactionCount = rows.Count,
            GrandSubtotal = rows.Sum(r => r.Subtotal),
            GrandPlatformFeeAmount = rows.Sum(r => r.PlatformFeeAmount),
            GrandTotalAmount = rows.Sum(r => r.TotalAmount)
        };
    }

    public async Task<DemandTrendReportDto> GetDemandTrendReportAsync(DateTime? from, DateTime? to)
    {
        var quotationQuery = _db.Quotations.AsNoTracking().AsQueryable();
        if (from.HasValue) quotationQuery = quotationQuery.Where(q => q.RequestedDate >= from.Value);
        if (to.HasValue) quotationQuery = quotationQuery.Where(q => q.RequestedDate <= to.Value);

        var bookingQuery = _db.Bookings.AsNoTracking().AsQueryable();
        if (from.HasValue) bookingQuery = bookingQuery.Where(b => b.CreatedDate >= from.Value);
        if (to.HasValue) bookingQuery = bookingQuery.Where(b => b.CreatedDate <= to.Value);

        var quotations = await quotationQuery
            .Select(q => new { q.RequestedDate, CategoryID = q.Listing.CategoryID })
            .ToListAsync();
        var bookings = await bookingQuery
            .Select(b => new { b.CreatedDate, CategoryID = b.Listing.CategoryID })
            .ToListAsync();

        var categoryIds = quotations.Select(q => q.CategoryID)
            .Concat(bookings.Select(b => b.CategoryID))
            .Distinct()
            .ToList();
        var categoryNames = await _db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.CategoryID))
            .ToDictionaryAsync(c => c.CategoryID, c => c.Name);

        var categories = categoryIds.Select(id =>
        {
            var quoteCount = quotations.Count(x => x.CategoryID == id);
            var bookingCount = bookings.Count(x => x.CategoryID == id);
            return new CategoryDemandDto
            {
                CategoryID = id,
                CategoryName = categoryNames.TryGetValue(id, out var name) ? name : "Uncategorized",
                QuotationRequestCount = quoteCount,
                BookingCount = bookingCount,
                ConversionRatePercent = quoteCount == 0 ? 0m : Math.Round((decimal)bookingCount / quoteCount * 100m, 1)
            };
        })
        .OrderByDescending(c => c.BookingCount)
        .ThenByDescending(c => c.QuotationRequestCount)
        .ToList();

        var monthKeys = bookings.Select(b => new { b.CreatedDate.Year, b.CreatedDate.Month })
            .Concat(quotations.Select(q => new { q.RequestedDate.Year, q.RequestedDate.Month }))
            .Distinct()
            .OrderBy(k => k.Year).ThenBy(k => k.Month)
            .ToList();

        var monthlyUsage = monthKeys.Select(k => new MonthlyUsageDto
        {
            MonthLabel = new DateTime(k.Year, k.Month, 1).ToString("MMMM yyyy"),
            QuotationCount = quotations.Count(q => q.RequestedDate.Year == k.Year && q.RequestedDate.Month == k.Month),
            BookingCount = bookings.Count(b => b.CreatedDate.Year == k.Year && b.CreatedDate.Month == k.Month)
        }).ToList();

        return new DemandTrendReportDto
        {
            From = from,
            To = to,
            Categories = categories,
            MonthlyUsage = monthlyUsage
        };
    }

    public async Task<ActiveListingsReportDto> GetActiveListingsReportAsync()
    {
        var rows = await _db.Listings.AsNoTracking()
            .Where(l => l.AvailabilityStatus == "Active")
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => new
            {
                l.ListingID,
                l.ListingTitle,
                l.CategoryID,
                SupplierName = l.Supplier.FullName,
                l.DailyRateZAR,
                l.Location,
                l.AverageRating,
                l.CreatedDate
            })
            .ToListAsync();

        var categoryIds = rows.Select(r => r.CategoryID).Distinct().ToList();
        var categoryNames = await _db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.CategoryID))
            .ToDictionaryAsync(c => c.CategoryID, c => c.Name);

        var listings = rows.Select(r => new ActiveListingRowDto
        {
            ListingID = r.ListingID,
            ListingTitle = r.ListingTitle,
            CategoryName = categoryNames.TryGetValue(r.CategoryID, out var name) ? name : "Uncategorized",
            SupplierName = r.SupplierName,
            DailyRateZAR = r.DailyRateZAR,
            Location = r.Location,
            AverageRating = r.AverageRating,
            CreatedDate = r.CreatedDate
        }).ToList();

        return new ActiveListingsReportDto { Listings = listings, TotalCount = listings.Count };
    }

    public async Task<MonthlyOverviewReportDto> GetMonthlyOverviewReportAsync(DateTime? from, DateTime? to)
    {
        var invoiceQuery = _db.Invoices.AsNoTracking().Where(i => i.Status != "Cancelled");
        if (from.HasValue) invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= from.Value);
        if (to.HasValue) invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= to.Value);

        var invoiceRows = await invoiceQuery
            .Select(i => new { i.InvoiceDate, i.Subtotal, i.PlatformFeeAmount })
            .ToListAsync();

        var bookingQuery = _db.Bookings.AsNoTracking().AsQueryable();
        if (from.HasValue) bookingQuery = bookingQuery.Where(b => b.CreatedDate >= from.Value);
        if (to.HasValue) bookingQuery = bookingQuery.Where(b => b.CreatedDate <= to.Value);
        var bookingDates = await bookingQuery.Select(b => b.CreatedDate).ToListAsync();

        var monthKeys = invoiceRows.Select(r => new { r.InvoiceDate.Year, r.InvoiceDate.Month })
            .Concat(bookingDates.Select(d => new { d.Year, d.Month }))
            .Distinct()
            .OrderBy(k => k.Year).ThenBy(k => k.Month)
            .ToList();

        var months = monthKeys.Select(k => new MonthlyOverviewRowDto
        {
            Year = k.Year,
            Month = k.Month,
            MonthLabel = new DateTime(k.Year, k.Month, 1).ToString("MMMM yyyy"),
            GrossRevenue = invoiceRows.Where(r => r.InvoiceDate.Year == k.Year && r.InvoiceDate.Month == k.Month).Sum(r => r.Subtotal),
            PlatformCommission = invoiceRows.Where(r => r.InvoiceDate.Year == k.Year && r.InvoiceDate.Month == k.Month).Sum(r => r.PlatformFeeAmount),
            BookingCount = bookingDates.Count(d => d.Year == k.Year && d.Month == k.Month)
        }).ToList();

        return new MonthlyOverviewReportDto { From = from, To = to, Months = months };
    }

    public async Task<PendingSupplierApprovalsReportDto> GetPendingSupplierApprovalsReportAsync()
    {
        var now = AppTime.Now;
        var rows = await _db.Users.AsNoTracking()
            .Where(u => u.Role == "Supplier" && u.AccountStatus == "Pending")
            .OrderBy(u => u.CreatedDate)
            .Select(u => new PendingSupplierRowDto
            {
                UserID = u.UserID,
                FullName = u.FullName,
                Email = u.Email,
                CompanyName = u.CompanyName,
                RegistrationNumber = u.RegistrationNumber,
                CreatedDate = u.CreatedDate,
                DaysPending = 0
            })
            .ToListAsync();

        foreach (var row in rows)
            row.DaysPending = (int)(now - row.CreatedDate).TotalDays;

        return new PendingSupplierApprovalsReportDto { Suppliers = rows, TotalCount = rows.Count };
    }

    public async Task<OpenDisputesReportDto> GetOpenDisputesReportAsync()
    {
        var rows = await _db.Disputes.AsNoTracking()
            .Where(d => d.Status != "Resolved")
            .OrderByDescending(d => d.RaisedDate)
            .Select(d => new OpenDisputeRowDto
            {
                DisputeID = d.DisputeID,
                BookingReference = d.BookingReference,
                ContractorName = d.Contractor.FullName,
                SupplierName = d.Supplier.FullName,
                ReasonCategory = d.ReasonCategory,
                BookingAmount = d.BookingAmount,
                Status = d.Status,
                RaisedDate = d.RaisedDate
            })
            .ToListAsync();

        return new OpenDisputesReportDto { Disputes = rows, TotalCount = rows.Count };
    }

    public async Task<SupplierPerformanceReportDto> GetSupplierPerformanceReportAsync(DateTime? from, DateTime? to)
    {
        var query = _db.Invoices.AsNoTracking().Where(i => i.Status != "Cancelled");
        if (from.HasValue) query = query.Where(i => i.InvoiceDate >= from.Value);
        if (to.HasValue) query = query.Where(i => i.InvoiceDate <= to.Value);

        var rows = await query
            .Select(i => new
            {
                i.InvoiceDate,
                i.Subtotal,
                i.PlatformFeeAmount,
                i.TotalAmount,
                SupplierID = i.Listing.SupplierID
            })
            .ToListAsync();

        var supplierIds = rows.Select(r => r.SupplierID).Distinct().ToList();
        var supplierNames = await _db.Users.AsNoTracking()
            .Where(u => supplierIds.Contains(u.UserID))
            .ToDictionaryAsync(u => u.UserID, u => u.FullName);

        var suppliers = rows
            .GroupBy(r => r.SupplierID)
            .Select(g => new SupplierPerformanceBreakDto
            {
                SupplierID = g.Key,
                SupplierName = supplierNames.TryGetValue(g.Key, out var name) ? name : "Unknown Supplier",
                Months = g.GroupBy(x => new { x.InvoiceDate.Year, x.InvoiceDate.Month })
                    .OrderBy(mg => mg.Key.Year).ThenBy(mg => mg.Key.Month)
                    .Select(mg => new RevenueMonthBreakDto
                    {
                        Year = mg.Key.Year,
                        Month = mg.Key.Month,
                        MonthLabel = new DateTime(mg.Key.Year, mg.Key.Month, 1).ToString("MMMM yyyy"),
                        TransactionCount = mg.Count(),
                        Subtotal = mg.Sum(x => x.Subtotal),
                        PlatformFeeAmount = mg.Sum(x => x.PlatformFeeAmount),
                        TotalAmount = mg.Sum(x => x.TotalAmount)
                    })
                    .ToList(),
                SupplierTransactionCount = g.Count(),
                SupplierSubtotal = g.Sum(x => x.Subtotal),
                SupplierPlatformFeeAmount = g.Sum(x => x.PlatformFeeAmount),
                SupplierTotalAmount = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(s => s.SupplierTotalAmount)
            .ToList();

        return new SupplierPerformanceReportDto
        {
            From = from,
            To = to,
            Suppliers = suppliers,
            GrandTransactionCount = rows.Count,
            GrandSubtotal = rows.Sum(r => r.Subtotal),
            GrandPlatformFeeAmount = rows.Sum(r => r.PlatformFeeAmount),
            GrandTotalAmount = rows.Sum(r => r.TotalAmount)
        };
    }
}
