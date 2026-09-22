using EquaMeridian.DTOs.Dashboard;

public interface IDashboardRepository
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<DashboardChartsDto> GetChartsAsync(DateTime fromDate, DateTime toDate);
}
