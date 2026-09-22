using EquaMeridian.DTOs.Notifications;

public interface INotificationRepository
{
    Task CreateAsync(int userId, string type, string title, string body,
        string? relatedEntityType = null, int? relatedEntityId = null, bool emailUser = true);
    Task<int> BroadcastAsync(string title, string body, string? targetRole,
        string type = "Announcement", string? relatedEntityType = null, int? relatedEntityId = null,
        bool emailUsers = true);

    Task<NotificationListResponse> GetForUserAsync(int userId, bool? unreadOnly, int page, int pageSize);
    Task<bool> MarkReadAsync(int notificationId, int userId);

    Task<int> MarkAllReadAsync(int userId);
}
