using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Timers
{
    public class TimerConfigurationDto
    {
        public int TimerConfigurationID { get; set; }
        public string TimerKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int IntervalMinutes { get; set; }
        public int QuoteExpiryHours { get; set; }
        public int SessionIdleMinutes { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime? LastRunAt { get; set; }
        public int? LastRunExpiredCount { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateTimerConfigurationRequest
    {
        [Range(1, 1440, ErrorMessage = "Interval must be between 1 and 1440 minutes.")]
        public int IntervalMinutes { get; set; }

        [Range(1, 8760, ErrorMessage = "Quote expiry must be between 1 and 8760 hours.")]
        public int QuoteExpiryHours { get; set; }

        [Range(1, 480, ErrorMessage = "Session idle timeout must be between 1 and 480 minutes.")]
        public int SessionIdleMinutes { get; set; } = 30;

        public bool IsEnabled { get; set; }
    }

    public class SessionSettingsDto
    {
        public int SessionIdleMinutes { get; set; }
    }
}
