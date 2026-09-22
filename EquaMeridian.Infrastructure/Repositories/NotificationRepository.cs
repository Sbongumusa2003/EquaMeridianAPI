using EquaMeridian.DTOs.Notifications;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public NotificationRepository(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task CreateAsync(int userId, string type, string title, string body,
        string? relatedEntityType = null, int? relatedEntityId = null, bool emailUser = true)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Type = type,
            Title = title,
            Body = body,
            RelatedEntityType = relatedEntityType,
            RelatedEntityID = relatedEntityId,
            IsRead = false,
            CreatedDate = AppTime.Now
        });
        await _db.SaveChangesAsync();

        if (emailUser)
            await TryEmailUserAsync(userId, type, title, body);
    }

    public async Task<int> BroadcastAsync(string title, string body, string? targetRole,
        string type = "Announcement", string? relatedEntityType = null, int? relatedEntityId = null,
        bool emailUsers = true)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.AccountStatus == "Active");

        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            query = query.Where(u => u.Role.ToLower() == targetRole.ToLower());
        }

        var recipients = await query
            .Select(u => new { u.UserID, u.Email, u.FullName })
            .ToListAsync();
        var now = AppTime.Now;

        var notifications = recipients.Select(r => new Notification
        {
            UserID = r.UserID,
            Type = type,
            Title = title,
            Body = body,
            RelatedEntityType = relatedEntityType,
            RelatedEntityID = relatedEntityId,
            IsRead = false,
            CreatedDate = now
        });

        await _db.Notifications.AddRangeAsync(notifications);
        await _db.SaveChangesAsync();

        if (emailUsers)
        {
            foreach (var r in recipients)
            {
                if (string.IsNullOrWhiteSpace(r.Email)) continue;
                try
                {
                    await _email.SendNotificationEmailAsync(r.Email, r.FullName, title, body, type);
                }
                catch
                {
                }
            }
        }

        return recipients.Count;
    }

    public async Task<NotificationListResponse> GetForUserAsync(int userId, bool? unreadOnly, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.Notifications.AsNoTracking().Where(n => n.UserID == userId);
        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync();
        var unreadCount = await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserID == userId && !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                NotificationID = n.NotificationID,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                RelatedEntityType = n.RelatedEntityType,
                RelatedEntityID = n.RelatedEntityID,
                IsRead = n.IsRead,
                CreatedDate = n.CreatedDate
            })
            .ToListAsync();

        return new NotificationListResponse
        {
            Notifications = items,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> MarkReadAsync(int notificationId, int userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.UserID == userId);

        if (notification == null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(int userId)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserID == userId && !n.IsRead)
            .ToListAsync();

        if (unread.Count == 0) return 0;

        foreach (var n in unread)
            n.IsRead = true;

        await _db.SaveChangesAsync();
        return unread.Count;
    }

    private async Task TryEmailUserAsync(int userId, string type, string title, string body)
    {
        try
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.UserID == userId)
                .Select(u => new { u.Email, u.FullName })
                .FirstOrDefaultAsync();

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return;

            await _email.SendNotificationEmailAsync(user.Email, user.FullName, title, body, type);
        }
        catch
        {
        }
    }
}
