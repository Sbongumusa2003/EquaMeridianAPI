using EquaMeridian.DTOs.Timers;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class TimerConfigurationRepository : ITimerConfigurationRepository
{
    private const string QuoteExpiryKey = "QuoteExpiryCheck";
    private readonly AppDbContext _db;
    public TimerConfigurationRepository(AppDbContext db) => _db = db;

    public async Task<TimerConfigurationDto> GetAsync()
    {
        var config = await _db.TimerConfigurations.FirstOrDefaultAsync(t => t.TimerKey == QuoteExpiryKey);
        if (config == null)
        {
            config = new TimerConfiguration { TimerKey = QuoteExpiryKey };
            _db.TimerConfigurations.Add(config);
            await _db.SaveChangesAsync();
        }
        return ToDto(config);
    }

    public async Task<TimerConfigurationDto> UpdateAsync(int adminId, UpdateTimerConfigurationRequest dto)
    {
        var config = await _db.TimerConfigurations.FirstOrDefaultAsync(t => t.TimerKey == QuoteExpiryKey);
        if (config == null)
        {
            config = new TimerConfiguration { TimerKey = QuoteExpiryKey };
            _db.TimerConfigurations.Add(config);
        }

        config.IntervalMinutes = dto.IntervalMinutes;
        config.QuoteExpiryHours = dto.QuoteExpiryHours;
        config.SessionIdleMinutes = dto.SessionIdleMinutes > 0 ? dto.SessionIdleMinutes : 30;
        config.IsEnabled = dto.IsEnabled;
        config.Description =
            $"Quote expiry runs every {config.IntervalMinutes} min and expires quotes after {config.QuoteExpiryHours} h " +
            $"(enabled: {config.IsEnabled}). Session idle logout: {config.SessionIdleMinutes} minute(s) for every signed-in user.";
        config.UpdatedByAdminID = adminId;
        config.UpdatedAt = AppTime.Now;

        await _db.SaveChangesAsync();
        return ToDto(config);
    }

    private static TimerConfigurationDto ToDto(TimerConfiguration c) => new()
    {
        TimerConfigurationID = c.TimerConfigurationID,
        TimerKey = c.TimerKey,
        Description = c.Description,
        IntervalMinutes = c.IntervalMinutes,
        QuoteExpiryHours = c.QuoteExpiryHours,
        SessionIdleMinutes = c.SessionIdleMinutes > 0 ? c.SessionIdleMinutes : 30,
        IsEnabled = c.IsEnabled,
        LastRunAt = c.LastRunAt,
        LastRunExpiredCount = c.LastRunExpiredCount,
        UpdatedAt = c.UpdatedAt
    };
}