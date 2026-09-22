using EquaMeridian.DTOs.Dashboard;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;
    public DashboardRepository(AppDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var now = AppTime.Now;
        var currentPeriodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousPeriodStart = currentPeriodStart.AddMonths(-1);

        var totalUsers = await _db.Users.AsNoTracking().CountAsync();
        var previousTotalUsers = await _db.Users.CountAsync(u => u.CreatedDate < currentPeriodStart);

        var activeListings = await _db.Listings.AsNoTracking().CountAsync(l => l.AvailabilityStatus == "Active");
        var previousActiveListings = await _db.Listings.CountAsync(l =>
            l.AvailabilityStatus == "Active" && l.CreatedDate < currentPeriodStart);

        var pendingListings = await _db.Listings.AsNoTracking().CountAsync(l => l.AvailabilityStatus == "Pending");
        var previousPendingListings = await _db.Listings.CountAsync(l =>
            l.AvailabilityStatus == "Pending" && l.CreatedDate < currentPeriodStart);

        var openDisputes = await _db.Disputes.AsNoTracking().CountAsync(d => d.Status == "Open" || d.Status == "Under Review");
        var previousOpenDisputes = await _db.Disputes.CountAsync(d =>
            (d.Status == "Open" || d.Status == "Under Review") && d.RaisedDate < currentPeriodStart);
        var monthlyRevenue = await _db.Invoices
            .Where(i => i.InvoiceDate >= currentPeriodStart)
            .AsNoTracking().SumAsync(i => (decimal?)i.PlatformFeeAmount) ?? 0m;
        var previousMonthlyRevenue = await _db.Invoices
            .Where(i => i.InvoiceDate >= previousPeriodStart && i.InvoiceDate < currentPeriodStart)
            .AsNoTracking().SumAsync(i => (decimal?)i.PlatformFeeAmount) ?? 0m;

        var activeCampaigns = await _db.Campaigns.CountAsync(c => c.Status == "Active");
        var previousActiveCampaigns = await _db.Campaigns.CountAsync(c =>
            c.Status == "Active" && c.CreatedDate < currentPeriodStart);

        var recentActivity = await _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .Select(a => new { a.AuditID, a.TransactionType, a.Timestamp, a.AdminID, a.UserID })
            .ToListAsync();

        var actorIds = recentActivity
            .Select(a => a.AdminID ?? a.UserID)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var actorNames = await _db.Users
            .AsNoTracking()
            .Where(u => actorIds.Contains(u.UserID))
            .ToDictionaryAsync(u => u.UserID, u => u.FullName);

        return new DashboardSummaryDto
        {
            TotalUsers = BuildMetric(totalUsers, previousTotalUsers),
            ActiveListings = BuildMetric(activeListings, previousActiveListings),
            PendingListings = BuildMetric(pendingListings, previousPendingListings),
            OpenDisputes = BuildMetric(openDisputes, previousOpenDisputes),
            MonthlyRevenue = BuildMetric(monthlyRevenue, previousMonthlyRevenue),
            ActiveCampaigns = BuildMetric(activeCampaigns, previousActiveCampaigns),
            RecentActivity = recentActivity.Select(a => new RecentActivityDto
            {
                AuditID = a.AuditID,
                EventType = a.TransactionType,
                ActingUser = (a.AdminID ?? a.UserID) is int actorId && actorNames.TryGetValue(actorId, out var name)
                    ? name
                    : null,
                Timestamp = a.Timestamp
            }).ToList(),
            NeedsAttention = new NeedsAttentionDto
            {
                PendingListings = pendingListings,
                OpenDisputes = openDisputes,
                PendingRefunds = await _db.Refunds.AsNoTracking().CountAsync(r => r.Status == "Pending"),
                UnverifiedSuppliers = await _db.Users.AsNoTracking().CountAsync(u =>
                    u.Role == "Supplier" && u.AccountStatus != "Active"),
                PendingDocuments = await _db.Documents.AsNoTracking().CountAsync(d =>
                    d.VerificationStatus == "Pending")
            }
        };
    }

    private static MetricDto BuildMetric(decimal current, decimal previous) => new()
    {
        Value = current,
        PreviousValue = previous,
        PercentChange = previous == 0
            ? (current == 0 ? 0 : (decimal?)null)
            : Math.Round((current - previous) / previous * 100m, 1)
    };

    public async Task<DashboardChartsDto> GetChartsAsync(DateTime fromDate, DateTime toDate)
    {
        var rangeEnd = toDate.Date.AddDays(1);
        var rangeStart = fromDate.Date;

        var invoicesInRange = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= rangeStart && i.InvoiceDate < rangeEnd)
            .Include(i => i.Listing)
            .Include(i => i.Supplier)
            .ToListAsync();

        var bookingsInRange = await _db.Bookings.AsNoTracking()
            .Where(b => b.CreatedDate >= rangeStart && b.CreatedDate < rangeEnd)
            .ToListAsync();

        var revenueByDay = invoicesInRange
            .GroupBy(i => i.InvoiceDate.Date)
            .Select(g => new TimeSeriesPointDto { Date = g.Key, Value = g.Sum(i => i.PlatformFeeAmount) })
            .OrderBy(p => p.Date)
            .ToList();

        var bookingsByDay = bookingsInRange
            .GroupBy(b => b.CreatedDate.Date)
            .Select(g => new TimeSeriesPointDto { Date = g.Key, Value = g.Count() })
            .OrderBy(p => p.Date)
            .ToList();
        var statusCounts = await _db.Listings.AsNoTracking()
            .GroupBy(l => l.AvailabilityStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var breakdown = new ListingsBreakdownDto();
        foreach (var s in statusCounts)
        {
            switch (s.Status)
            {
                case "Active": breakdown.Active = s.Count; break;
                case "Pending": breakdown.Pending = s.Count; break;
                case "Draft": breakdown.Draft = s.Count; break;
                case "Rejected": breakdown.Rejected = s.Count; break;
                case "Suspended": breakdown.Suspended = s.Count; break;
                case "Inactive": breakdown.Inactive = s.Count; break;
                case "Archived": breakdown.Archived = s.Count; break;
            }
        }

        var disputeCount = await _db.Disputes.AsNoTracking()
            .CountAsync(d => d.RaisedDate >= rangeStart && d.RaisedDate < rangeEnd);
        var refundCount = await _db.Refunds.AsNoTracking()
            .CountAsync(r => r.CreatedDate >= rangeStart && r.CreatedDate < rangeEnd);
        var totalBookingsInRange = bookingsInRange.Count;

        var disputeRefund = new DisputeRefundRateDto
        {
            TotalBookings = totalBookingsInRange,
            DisputeCount = disputeCount,
            RefundCount = refundCount,
            DisputeRatePercent = totalBookingsInRange == 0 ? 0 : Math.Round(disputeCount * 100m / totalBookingsInRange, 1),
            RefundRatePercent = totalBookingsInRange == 0 ? 0 : Math.Round(refundCount * 100m / totalBookingsInRange, 1)
        };

        var topSuppliers = invoicesInRange
            .GroupBy(i => new { i.SupplierID, i.Supplier.CompanyName, i.Supplier.FullName })
            .Select(g => new TopPerformerDto
            {
                Id = g.Key.SupplierID,
                Name = g.Key.CompanyName ?? g.Key.FullName,
                BookingCount = g.Count(),
                Revenue = g.Sum(i => i.TotalAmount)
            })
            .OrderByDescending(t => t.Revenue)
            .Take(5)
            .ToList();

        var categoryNames = await _db.Categories.AsNoTracking().ToDictionaryAsync(c => c.CategoryID, c => c.Name);
        var topCategories = invoicesInRange
            .GroupBy(i => i.Listing.CategoryID)
            .Select(g => new TopPerformerDto
            {
                Id = g.Key,
                Name = categoryNames.TryGetValue(g.Key, out var name) ? name : "Uncategorized",
                BookingCount = g.Count(),
                Revenue = g.Sum(i => i.TotalAmount)
            })
            .OrderByDescending(t => t.Revenue)
            .Take(5)
            .ToList();

        return new DashboardChartsDto
        {
            FromDate = rangeStart,
            ToDate = toDate.Date,
            RevenueOverTime = revenueByDay,
            BookingsVolumeOverTime = bookingsByDay,
            ListingsBreakdown = breakdown,
            DisputeRefundRate = disputeRefund,
            TopSuppliers = topSuppliers,
            TopCategories = topCategories
        };
    }
}
