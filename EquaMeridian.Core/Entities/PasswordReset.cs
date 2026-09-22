public class PasswordReset
{
    public int ID { get; set; }
    public int UserID { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiryTimestamp { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = AppTime.Now;
    public User User { get; set; } = null!;
}