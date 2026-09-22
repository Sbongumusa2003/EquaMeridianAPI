using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Notifications
{
    public class NotificationDto
    {
        public int NotificationID { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityID { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class NotificationListResponse
    {
        public List<NotificationDto> Notifications { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class CreateAnnouncementRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public string? TargetRole { get; set; }
    }

    public class AnnouncementResultDto
    {
        public int RecipientCount { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime SentDate { get; set; }
    }
}
