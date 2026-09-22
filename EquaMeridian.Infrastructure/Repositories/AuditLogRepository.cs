using EquaMeridian.DTOs.AuditLogs;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;
    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task<(IEnumerable<AuditLogListItemDto>, int)> SearchAsync(
        string? search, string? eventType, int? userId,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        var query = BuildFilteredQuery(search, eventType, userId, from, to);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.AuditID, a.TransactionType, a.Description, a.UserID, a.Timestamp })
            .ToListAsync();

        var userIds = rows.Where(r => r.UserID.HasValue).Select(r => r.UserID!.Value).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserID))
            .ToDictionaryAsync(u => u.UserID, u => u);

        var logs = rows.Select(r =>
        {
            users.TryGetValue(r.UserID ?? -1, out var user);
            return MapToListItemDto(r.AuditID, r.TransactionType, r.Description, r.UserID,
                user?.FullName, user?.Email, r.Timestamp);
        });

        return (logs, total);
    }

    public async Task<AuditLogDetailDto?> GetByIdAsync(int auditId)
    {
        var log = await _db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.AuditID == auditId);
        if (log == null) return null;

        var user = log.UserID.HasValue
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == log.UserID.Value)
            : null;

        var item = MapToListItemDto(log.AuditID, log.TransactionType, log.Description,
            log.UserID, user?.FullName, user?.Email, log.Timestamp);

        return new AuditLogDetailDto
        {
            AuditID = item.AuditID,
            EventType = item.EventType,
            Description = item.Description,
            UserID = item.UserID,
            UserName = item.UserName,
            UserEmail = item.UserEmail,
            EventDate = item.EventDate,
            EventTime = item.EventTime,
            Status = item.Status,
            PreviousValues = log.PreviousValues,
            NewValues = log.NewValues,
            IPAddress = log.IPAddress,
            ListingID = log.ListingID,
            AdminID = log.AdminID
        };
    }

    public async Task<List<AuditLogListItemDto>> GetForExportAsync(
        string? search, string? eventType, int? userId, DateTime? from, DateTime? to)
    {
        var query = BuildFilteredQuery(search, eventType, userId, from, to);

        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new { a.AuditID, a.TransactionType, a.Description, a.UserID, a.Timestamp })
            .ToListAsync();

        var userIds = rows.Where(r => r.UserID.HasValue).Select(r => r.UserID!.Value).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserID))
            .ToDictionaryAsync(u => u.UserID, u => u);

        return rows.Select(r =>
        {
            users.TryGetValue(r.UserID ?? -1, out var user);
            return MapToListItemDto(r.AuditID, r.TransactionType, r.Description, r.UserID,
                user?.FullName, user?.Email, r.Timestamp);
        }).ToList();
    }

    public async Task<List<string>> GetDistinctEventTypesAsync() =>
        await _db.AuditLogs
            .Select(a => a.TransactionType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

    public async Task<List<UserFilterOptionDto>> GetDistinctUsersAsync() =>
        await _db.AuditLogs
            .Where(a => a.UserID.HasValue)
            .Select(a => a.UserID!.Value)
            .Distinct()
            .Join(_db.Users, id => id, u => u.UserID, (id, u) => new UserFilterOptionDto
            {
                UserID = u.UserID,
                FullName = u.FullName
            })
            .OrderBy(u => u.FullName)
            .ToListAsync();

    private IQueryable<AuditLog> BuildFilteredQuery(
        string? search, string? eventType, int? userId, DateTime? from, DateTime? to)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(a => a.TransactionType == eventType);

        if (userId.HasValue)
            query = query.Where(a => a.UserID == userId.Value);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp.Date >= from.Value.Date);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp.Date <= to.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                a.TransactionType.Contains(term) ||
                (a.Description != null && a.Description.Contains(term)) ||
                (a.UserID != null && _db.Users.Any(u => u.UserID == a.UserID && u.FullName.Contains(term))));
        }

        return query;
    }

    private static AuditLogListItemDto MapToListItemDto(
        int auditId, string transactionType, string? description, int? userId,
        string? userName, string? userEmail, DateTime timestamp) => new()
    {
        AuditID = auditId,
        EventType = transactionType,
        Description = description,
        UserID = userId,
        UserName = userName,
        UserEmail = userEmail,
        EventDate = timestamp.Date,
        EventTime = timestamp.ToString("HH:mm:ss"),
        Status = DeriveStatus(transactionType)
    };
    private static string DeriveStatus(string transactionType)
    {
        var t = transactionType.ToUpperInvariant();
        if (t.Contains("FAIL") || t.Contains("ERROR") || t.Contains("DENIED"))
            return "Error";
        if (t.Contains("REJECT") || t.Contains("WARNING") || t.Contains("LOCK") || t.Contains("EXPIRED"))
            return "Warning";
        if (t.Contains("VIEW"))
            return "Info";
        return "Success";
    }
}
