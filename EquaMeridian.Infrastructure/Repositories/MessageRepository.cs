using EquaMeridian.DTOs.Messages;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public class MessageRepository : IMessageRepository
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
    private const string AttachmentRejectedMessage =
        "File must be under 10MB and one of: PDF, DOC, DOCX, JPG, PNG";

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public MessageRepository(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }
    public async Task<SendMessageResult> SendAsync(int senderId, SendMessageDto dto, IFormFile? attachment)
    {
        if (string.IsNullOrWhiteSpace(dto.Body) && attachment == null)
            return new SendMessageResult { Success = false, ErrorCode = "ValidationError", Error = "This field is required." };

        if (!TryValidateAttachment(attachment, out var attachmentError))
            return new SendMessageResult { Success = false, ErrorCode = "ValidationError", Error = attachmentError };

        var sender = await _db.Users.FirstOrDefaultAsync(u => u.UserID == senderId);
        var recipient = await _db.Users.FirstOrDefaultAsync(u => u.UserID == dto.RecipientID);

        if (sender == null || recipient == null)
            return new SendMessageResult { Success = false, ErrorCode = "NotFound", Error = "Recipient not found." };
        var thread = await _db.Threads.FirstOrDefaultAsync(t =>
            (t.ParticipantOneID == senderId && t.ParticipantTwoID == dto.RecipientID) ||
            (t.ParticipantOneID == dto.RecipientID && t.ParticipantTwoID == senderId));

        if (thread == null)
        {
            thread = new MessageThread
            {
                ParticipantOneID = senderId,
                ParticipantTwoID = dto.RecipientID,
                CreatedDate = AppTime.Now,
                LastActivityDate = AppTime.Now
            };
            _db.Threads.Add(thread);
            await _db.SaveChangesAsync();
        }

        var (url, fileName) = await StoreAttachmentAsync(attachment, thread.ThreadID);

        var message = new Message
        {
            ThreadID = thread.ThreadID,
            SenderID = senderId,
            RecipientID = dto.RecipientID,
            Body = dto.Body,
            AttachmentURL = url,
            AttachmentFileName = fileName,
            DateSent = AppTime.Now,
            IsRead = false
        };

        _db.Messages.Add(message);
        thread.LastActivityDate = message.DateSent;
        await _db.SaveChangesAsync();

        return new SendMessageResult
        {
            Success = true,
            Message = MapToDto(message, sender.FullName, recipient.FullName),
            RecipientEmail = recipient.Email,
            RecipientName = recipient.FullName,
            SenderName = sender.FullName
        };
    }
    public async Task<SendMessageResult> ReplyAsync(int senderId, int threadId, ReplyMessageDto dto, IFormFile? attachment)
    {
        if (string.IsNullOrWhiteSpace(dto.Body) && attachment == null)
            return new SendMessageResult { Success = false, ErrorCode = "ValidationError", Error = "This field is required." };

        if (!TryValidateAttachment(attachment, out var attachmentError))
            return new SendMessageResult { Success = false, ErrorCode = "ValidationError", Error = attachmentError };

        var thread = await _db.Threads
            .Include(t => t.ParticipantOne)
            .Include(t => t.ParticipantTwo)
            .FirstOrDefaultAsync(t => t.ThreadID == threadId &&
                (t.ParticipantOneID == senderId || t.ParticipantTwoID == senderId));

        if (thread == null)
            return new SendMessageResult { Success = false, ErrorCode = "NotFound", Error = "Conversation not found." };

        var recipientId = thread.ParticipantOneID == senderId ? thread.ParticipantTwoID : thread.ParticipantOneID;
        var sender = thread.ParticipantOneID == senderId ? thread.ParticipantOne : thread.ParticipantTwo;
        var recipient = thread.ParticipantOneID == senderId ? thread.ParticipantTwo : thread.ParticipantOne;

        var (url, fileName) = await StoreAttachmentAsync(attachment, thread.ThreadID);

        var message = new Message
        {
            ThreadID = thread.ThreadID,
            SenderID = senderId,
            RecipientID = recipientId,
            Body = dto.Body,
            AttachmentURL = url,
            AttachmentFileName = fileName,
            DateSent = AppTime.Now,
            IsRead = false
        };

        _db.Messages.Add(message);
        thread.LastActivityDate = message.DateSent;
        await _db.SaveChangesAsync();

        return new SendMessageResult
        {
            Success = true,
            Message = MapToDto(message, sender.FullName, recipient.FullName),
            RecipientEmail = recipient.Email,
            RecipientName = recipient.FullName,
            SenderName = sender.FullName
        };
    }
    public async Task<(IEnumerable<ThreadListItemDto>, int)> GetThreadsForUserAsync(
        int userId, string? search, int page, int pageSize)
    {
        var threads = await _db.Threads
            .Include(t => t.ParticipantOne)
            .Include(t => t.ParticipantTwo)
            .Include(t => t.Messages)
            .Where(t => t.ParticipantOneID == userId || t.ParticipantTwoID == userId)
            .OrderByDescending(t => t.LastActivityDate)
            .AsNoTracking()
            .ToListAsync();

        var items = threads.Select(t =>
        {
            var other = t.ParticipantOneID == userId ? t.ParticipantTwo : t.ParticipantOne;
            var lastMessage = t.Messages.OrderByDescending(m => m.DateSent).FirstOrDefault();
            var unread = t.Messages.Count(m => m.RecipientID == userId && !m.IsRead);

            return new ThreadListItemDto
            {
                ThreadID = t.ThreadID,
                OtherParticipantID = other.UserID,
                OtherParticipantName = other.FullName,
                MessagePreview = lastMessage?.Body ?? string.Empty,
                LastActivityDate = t.LastActivityDate,
                UnreadCount = unread
            };
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(i =>
                i.OtherParticipantName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.MessagePreview.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var itemsList = items.ToList();
        var total = itemsList.Count;
        var currentPage = page < 1 ? 1 : page;
        var size = pageSize < 1 ? 20 : pageSize;

        var paged = itemsList.Skip((currentPage - 1) * size).Take(size);

        return (paged, total);
    }

    public async Task<ThreadDetailDto?> GetThreadDetailAsync(int threadId, int userId)
    {
        var thread = await _db.Threads
            .Include(t => t.ParticipantOne)
            .Include(t => t.ParticipantTwo)
            .Include(t => t.Messages)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ThreadID == threadId &&
                (t.ParticipantOneID == userId || t.ParticipantTwoID == userId));

        if (thread == null) return null;

        var other = thread.ParticipantOneID == userId ? thread.ParticipantTwo : thread.ParticipantOne;

        var unreadMessages = thread.Messages.Where(m => m.RecipientID == userId && !m.IsRead).ToList();
        foreach (var m in unreadMessages) m.IsRead = true;
        if (unreadMessages.Count > 0) await _db.SaveChangesAsync();

        var orderedMessages = thread.Messages.OrderBy(m => m.DateSent).ToList();

        return new ThreadDetailDto
        {
            ThreadID = thread.ThreadID,
            OtherParticipantID = other.UserID,
            OtherParticipantName = other.FullName,
            Messages = orderedMessages.Select(m => new MessageDto
            {
                MessageID = m.MessageID,
                ThreadID = m.ThreadID,
                SenderID = m.SenderID,
                SenderName = m.SenderID == userId ? "You" : other.FullName,
                RecipientID = m.RecipientID,
                RecipientName = m.RecipientID == userId ? "You" : other.FullName,
                Body = m.Body,
                AttachmentURL = m.AttachmentURL,
                AttachmentFileName = m.AttachmentFileName,
                IsRead = m.IsRead,
                DateSent = m.DateSent
            }).ToList()
        };
    }

    private static bool TryValidateAttachment(IFormFile? attachment, out string? error)
    {
        error = null;
        if (attachment == null || attachment.Length == 0) return true;

        var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        if (attachment.Length > MaxAttachmentBytes || !AllowedExtensions.Contains(ext))
        {
            error = AttachmentRejectedMessage;
            return false;
        }

        return true;
    }

    private async Task<(string? Url, string? FileName)> StoreAttachmentAsync(IFormFile? attachment, int threadId)
    {
        if (attachment == null || attachment.Length == 0) return (null, null);

        var uploadDir = Path.Combine(_env.ContentRootPath, "uploads", "messages", threadId.ToString());
        Directory.CreateDirectory(uploadDir);

        var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadDir, storedFileName);

        using (var stream = File.Create(fullPath))
            await attachment.CopyToAsync(stream);

        var url = $"/uploads/messages/{threadId}/{storedFileName}";
        return (url, attachment.FileName);
    }

    public async Task<IEnumerable<RecipientSearchResultDto>> SearchRecipientsAsync(int currentUserId, string search)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
            return Enumerable.Empty<RecipientSearchResultDto>();

        var term = search.Trim();

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.UserID != currentUserId
                && u.AccountStatus == "Active"
                && (EF.Functions.Like(u.FullName, $"%{term}%")
                    || (u.CompanyName != null && EF.Functions.Like(u.CompanyName, $"%{term}%"))
                    || EF.Functions.Like(u.Email, $"%{term}%")
                    || EF.Functions.Like(u.Role, $"%{term}%")))
            .OrderBy(u => u.FullName)
            .Take(10)
            .Select(u => new RecipientSearchResultDto
            {
                UserID = u.UserID,
                FullName = u.FullName,
                Role = u.Role,
                CompanyName = u.CompanyName
            })
            .ToListAsync();
    }

    private static MessageDto MapToDto(Message m, string senderName, string recipientName) => new()
    {
        MessageID = m.MessageID,
        ThreadID = m.ThreadID,
        SenderID = m.SenderID,
        SenderName = senderName,
        RecipientID = m.RecipientID,
        RecipientName = recipientName,
        Body = m.Body,
        AttachmentURL = m.AttachmentURL,
        AttachmentFileName = m.AttachmentFileName,
        IsRead = m.IsRead,
        DateSent = m.DateSent
    };
}
