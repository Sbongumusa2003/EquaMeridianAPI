public class TimerConfiguration
{
    public int TimerConfigurationID { get; set; }
    public string TimerKey { get; set; } = "QuoteExpiryCheck";

    public string Description { get; set; } =
        "Automatically expires quotations that have not been accepted within the configured window.";
    public int IntervalMinutes { get; set; } = 60;
    public int QuoteExpiryHours { get; set; } = 48;

    /// <summary>Minutes of inactivity before the SPA signs the user out. Applies to every browser.</summary>
    public int SessionIdleMinutes { get; set; } = 30;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastRunAt { get; set; }
    public int? LastRunExpiredCount { get; set; }

    public int? UpdatedByAdminID { get; set; }
    public User? UpdatedByAdmin { get; set; }
    public DateTime UpdatedAt { get; set; } = AppTime.Now;
}
