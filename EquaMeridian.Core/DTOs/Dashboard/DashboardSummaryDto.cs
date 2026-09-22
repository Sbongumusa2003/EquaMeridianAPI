namespace EquaMeridian.DTOs.Dashboard
{
    public class MetricDto
    {
        public decimal Value { get; set; }
        public decimal PreviousValue { get; set; }
        public decimal? PercentChange { get; set; }
    }

    public class RecentActivityDto
    {
        public int AuditID { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? ActingUser { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DashboardSummaryDto
    {
        public MetricDto TotalUsers { get; set; } = new();
        public MetricDto ActiveListings { get; set; } = new();
        public MetricDto PendingListings { get; set; } = new();
        public MetricDto OpenDisputes { get; set; } = new();
        public MetricDto MonthlyRevenue { get; set; } = new();
        public MetricDto ActiveCampaigns { get; set; } = new();
        public List<RecentActivityDto> RecentActivity { get; set; } = new();
        public NeedsAttentionDto NeedsAttention { get; set; } = new();
    }

    public class NeedsAttentionDto
    {
        public int PendingListings { get; set; }
        public int OpenDisputes { get; set; }
        public int PendingRefunds { get; set; }
        public int UnverifiedSuppliers { get; set; }
        public int PendingDocuments { get; set; }
        public int Total => PendingListings + OpenDisputes + PendingRefunds + UnverifiedSuppliers + PendingDocuments;
    }

    public class TimeSeriesPointDto
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
    }

    public class ListingsBreakdownDto
    {
        public int Active { get; set; }
        public int Pending { get; set; }
        public int Draft { get; set; }
        public int Rejected { get; set; }
        public int Suspended { get; set; }
        public int Inactive { get; set; }
        public int Archived { get; set; }
    }

    public class DisputeRefundRateDto
    {
        public int TotalBookings { get; set; }
        public int DisputeCount { get; set; }
        public int RefundCount { get; set; }
        public decimal DisputeRatePercent { get; set; }
        public decimal RefundRatePercent { get; set; }
    }

    public class TopPerformerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int BookingCount { get; set; }
        public decimal Revenue { get; set; }
    }

    /// <summary>Req 3.7: chart data, all scoped to an Admin-selected date range with drill-down filters attached client-side.</summary>
    public class DashboardChartsDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<TimeSeriesPointDto> RevenueOverTime { get; set; } = new();
        public List<TimeSeriesPointDto> BookingsVolumeOverTime { get; set; } = new();
        public ListingsBreakdownDto ListingsBreakdown { get; set; } = new();
        public DisputeRefundRateDto DisputeRefundRate { get; set; } = new();
        public List<TopPerformerDto> TopSuppliers { get; set; } = new();
        public List<TopPerformerDto> TopCategories { get; set; } = new();
    }
}
