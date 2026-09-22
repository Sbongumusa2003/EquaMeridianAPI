namespace EquaMeridian.DTOs.Reports
{

    public class RevenueMonthBreakDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal PlatformFeeAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class RevenueCategoryBreakDto
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<RevenueMonthBreakDto> Months { get; set; } = new();
        public int CategoryTransactionCount { get; set; }
        public decimal CategorySubtotal { get; set; }
        public decimal CategoryPlatformFeeAmount { get; set; }
        public decimal CategoryTotalAmount { get; set; }
    }

    public class RevenueControlBreakReportDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<RevenueCategoryBreakDto> Categories { get; set; } = new();
        public int GrandTransactionCount { get; set; }
        public decimal GrandSubtotal { get; set; }
        public decimal GrandPlatformFeeAmount { get; set; }
        public decimal GrandTotalAmount { get; set; }
    }

    public class CategoryDemandDto
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int QuotationRequestCount { get; set; }
        public int BookingCount { get; set; }
        public decimal ConversionRatePercent { get; set; }
    }

    public class MonthlyUsageDto
    {
        public string MonthLabel { get; set; } = string.Empty;
        public int QuotationCount { get; set; }
        public int BookingCount { get; set; }
    }

    public class ActiveListingRowDto
    {
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal DailyRateZAR { get; set; }
        public string? Location { get; set; }
        public decimal AverageRating { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ActiveListingsReportDto
    {
        public List<ActiveListingRowDto> Listings { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class MonthlyOverviewRowDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public decimal GrossRevenue { get; set; }
        public decimal PlatformCommission { get; set; }
        public int BookingCount { get; set; }
    }

    public class MonthlyOverviewReportDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<MonthlyOverviewRowDto> Months { get; set; } = new();
    }
    public class SupplierMonthlyReportDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public int BookingCount { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal NetPayout { get; set; }
    }

    public class DemandTrendReportDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<CategoryDemandDto> Categories { get; set; } = new();
        public List<MonthlyUsageDto> MonthlyUsage { get; set; } = new();
    }


    public class SupplierReportSummaryDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<SupplierMonthlyReportDto> MonthlyBreakdown { get; set; } = new();
        public int GrandBookingCount { get; set; }
        public decimal GrandGrossRevenue { get; set; }
        public decimal GrandCommissionAmount { get; set; }
        public decimal GrandNetPayout { get; set; }
    }

    public class PendingSupplierRowDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? RegistrationNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public int DaysPending { get; set; }
    }

    public class PendingSupplierApprovalsReportDto
    {
        public List<PendingSupplierRowDto> Suppliers { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class OpenDisputeRowDto
    {
        public int DisputeID { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ReasonCategory { get; set; } = string.Empty;
        public decimal BookingAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RaisedDate { get; set; }
    }

    public class OpenDisputesReportDto
    {
        public List<OpenDisputeRowDto> Disputes { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class SupplierPerformanceBreakDto
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public List<RevenueMonthBreakDto> Months { get; set; } = new();
        public int SupplierTransactionCount { get; set; }
        public decimal SupplierSubtotal { get; set; }
        public decimal SupplierPlatformFeeAmount { get; set; }
        public decimal SupplierTotalAmount { get; set; }
    }

    public class SupplierPerformanceReportDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<SupplierPerformanceBreakDto> Suppliers { get; set; } = new();
        public int GrandTransactionCount { get; set; }
        public decimal GrandSubtotal { get; set; }
        public decimal GrandPlatformFeeAmount { get; set; }
        public decimal GrandTotalAmount { get; set; }
    }
}
