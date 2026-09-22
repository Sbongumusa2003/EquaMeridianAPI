public class Notification
{
    public int NotificationID { get; set; }

    public int UserID { get; set; }
    public User User { get; set; } = null!;

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityID { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime CreatedDate { get; set; } = AppTime.Now;
}
