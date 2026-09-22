public interface IContentModerationService
{
    Task<bool> IsCleanAsync(string title, string reviewText);

    Task<List<string>> GetBlockedTermsAsync();
    Task<List<BlockedTermDto>> GetBlockedTermRecordsAsync();
    Task<(bool Success, string? Error)> AddBlockedTermAsync(string term, int adminId);
    Task<(bool Success, string? Error)> RemoveBlockedTermAsync(int blockedTermId);
}

public class BlockedTermDto
{
    public int BlockedTermID { get; set; }
    public string Term { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
