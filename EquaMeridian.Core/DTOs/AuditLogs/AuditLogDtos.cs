namespace EquaMeridian.DTOs.AuditLogs
{
    public class AuditLogListItemDto
    {
        public int AuditID { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? UserID { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public DateTime EventDate { get; set; }
        public string EventTime { get; set; } = string.Empty;
        public string Status { get; set; } = "Info";
    }
    public class AuditLogDetailDto : AuditLogListItemDto
    {
        public string? PreviousValues { get; set; }
        public string? NewValues { get; set; }
        public string? IPAddress { get; set; }
        public int? ListingID { get; set; }
        public int? AdminID { get; set; }
    }
}
