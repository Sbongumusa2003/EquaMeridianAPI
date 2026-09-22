using EquaMeridian.DTOs.Reports;

public interface ISupplierReportingRepository
{
    Task<SupplierReportSummaryDto> GetMySummaryAsync(int supplierId, DateTime? from, DateTime? to);
}
