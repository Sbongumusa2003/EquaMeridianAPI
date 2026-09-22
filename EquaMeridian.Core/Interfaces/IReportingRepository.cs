using EquaMeridian.DTOs.Reports;

public interface IReportingRepository
{
    Task<RevenueControlBreakReportDto> GetRevenueControlBreakReportAsync(
        DateTime? from, DateTime? to, int? categoryId);
    Task<DemandTrendReportDto> GetDemandTrendReportAsync(DateTime? from, DateTime? to);
    Task<ActiveListingsReportDto> GetActiveListingsReportAsync();
    Task<MonthlyOverviewReportDto> GetMonthlyOverviewReportAsync(DateTime? from, DateTime? to);
    Task<PendingSupplierApprovalsReportDto> GetPendingSupplierApprovalsReportAsync();
    Task<OpenDisputesReportDto> GetOpenDisputesReportAsync();
    Task<SupplierPerformanceReportDto> GetSupplierPerformanceReportAsync(DateTime? from, DateTime? to);
}
