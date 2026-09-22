using EquaMeridian.DTOs.AuditLogs;
public interface IAuditLogRepository
{
    Task<(IEnumerable<AuditLogListItemDto> Logs, int TotalCount)> SearchAsync(
        string? search, string? eventType, int? userId,
        DateTime? from, DateTime? to, int page, int pageSize);
    Task<AuditLogDetailDto?> GetByIdAsync(int auditId);
    Task<List<AuditLogListItemDto>> GetForExportAsync(
        string? search, string? eventType, int? userId, DateTime? from, DateTime? to);
    Task<List<string>> GetDistinctEventTypesAsync();
    Task<List<UserFilterOptionDto>> GetDistinctUsersAsync();
}

public class UserFilterOptionDto
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
}
