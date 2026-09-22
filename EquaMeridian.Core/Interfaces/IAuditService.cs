public interface IAuditService
{
    Task LogAsync(int? userId, string transactionType, string? description,
                  int? adminId, int? listingId, string? previousValues,
                  string? ipAddress, string? newValues = null);
}