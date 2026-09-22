using EquaMeridian.DTOs.Messages;
using Microsoft.AspNetCore.Http;

public class SendMessageResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public MessageDto? Message { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
}

public interface IMessageRepository
{
    Task<SendMessageResult> SendAsync(int senderId, SendMessageDto dto, IFormFile? attachment);
    Task<SendMessageResult> ReplyAsync(int senderId, int threadId, ReplyMessageDto dto, IFormFile? attachment);
    Task<(IEnumerable<ThreadListItemDto> Threads, int TotalCount)> GetThreadsForUserAsync(
        int userId, string? search, int page, int pageSize);

    Task<ThreadDetailDto?> GetThreadDetailAsync(int threadId, int userId);
    Task<IEnumerable<RecipientSearchResultDto>> SearchRecipientsAsync(int currentUserId, string search);
}