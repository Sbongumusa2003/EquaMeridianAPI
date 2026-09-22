public class AuditLog
{
    public int AuditID { get; set; }
    public int? UserID { get; set; }

    public int? AdminID { get; set; }
    public int? ListingID { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PreviousValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; } = AppTime.Now;
    public string? IPAddress { get; set; }
}