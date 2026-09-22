using EquaMeridian.Infrastructure.Data;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(int? userId, string transactionType,
        string? description, int? adminId, int? listingId,
        string? previousValues, string? ipAddress, string? newValues = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserID = userId,
            AdminID = adminId,
            ListingID = listingId,
            TransactionType = transactionType,
            Description = description,
            PreviousValues = previousValues,
            NewValues = newValues,
            IPAddress = ipAddress,
            Timestamp = AppTime.Now
        });
        await _db.SaveChangesAsync();
    }
}