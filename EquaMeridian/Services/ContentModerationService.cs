using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
public class ContentModerationService : IContentModerationService
{
    private static List<string>? _cachedTerms;
    private static DateTime _cacheExpiresAt = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly object CacheLock = new();

    private readonly AppDbContext _db;
    public ContentModerationService(AppDbContext db) => _db = db;

    public async Task<bool> IsCleanAsync(string title, string reviewText)
    {
        var terms = await GetCachedTermsAsync();
        var combined = $"{title} {reviewText}".ToLowerInvariant();
        return !terms.Any(term => combined.Contains(term));
    }

    public async Task<List<string>> GetBlockedTermsAsync()
    {
        return await _db.BlockedTerms.AsNoTracking()
            .OrderBy(t => t.Term)
            .Select(t => t.Term)
            .ToListAsync();
    }
    public async Task<List<BlockedTermDto>> GetBlockedTermRecordsAsync()
    {
        return await _db.BlockedTerms.AsNoTracking()
            .OrderBy(t => t.Term)
            .Select(t => new BlockedTermDto
            {
                BlockedTermID = t.BlockedTermID,
                Term = t.Term,
                CreatedDate = t.CreatedDate
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error)> AddBlockedTermAsync(string term, int adminId)
    {
        term = term.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(term)) return (false, "Term cannot be empty.");

        var exists = await _db.BlockedTerms.AnyAsync(t => t.Term == term);
        if (exists) return (false, "That term is already blocked.");

        _db.BlockedTerms.Add(new BlockedTerm { Term = term, AddedByAdminID = adminId, CreatedDate = AppTime.Now });
        await _db.SaveChangesAsync();

        InvalidateCache();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveBlockedTermAsync(int blockedTermId)
    {
        var term = await _db.BlockedTerms.FirstOrDefaultAsync(t => t.BlockedTermID == blockedTermId);
        if (term == null) return (false, "Blocked term not found.");

        _db.BlockedTerms.Remove(term);
        await _db.SaveChangesAsync();

        InvalidateCache();
        return (true, null);
    }

    private async Task<List<string>> GetCachedTermsAsync()
    {
        lock (CacheLock)
        {
            if (_cachedTerms != null && AppTime.Now < _cacheExpiresAt)
                return _cachedTerms;
        }

        var terms = await GetBlockedTermsAsync();

        lock (CacheLock)
        {
            _cachedTerms = terms;
            _cacheExpiresAt = AppTime.Now.Add(CacheDuration);
        }

        return terms;
    }

    private static void InvalidateCache()
    {
        lock (CacheLock)
        {
            _cachedTerms = null;
            _cacheExpiresAt = DateTime.MinValue;
        }
    }
}
