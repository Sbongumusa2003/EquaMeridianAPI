public class OtpCode
{
    public int OtpCodeID { get; set; }

    public string Reference { get; set; } = Guid.NewGuid().ToString("N");

    public int UserID { get; set; }
    public User User { get; set; } = null!;

    public string CodeHash { get; set; } = string.Empty;
    public int AttemptCount { get; set; } = 0;
    public bool IsUsed { get; set; } = false;
    public string Purpose { get; set; } = "Login";

    public DateTime ExpiryTimestamp { get; set; }
    public DateTime CreatedAt { get; set; } = AppTime.Now;
}
