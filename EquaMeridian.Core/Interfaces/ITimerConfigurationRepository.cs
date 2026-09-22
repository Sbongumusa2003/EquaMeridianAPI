using EquaMeridian.DTOs.Timers;

public interface ITimerConfigurationRepository
{
    Task<TimerConfigurationDto> GetAsync();

    Task<TimerConfigurationDto> UpdateAsync(int adminId, UpdateTimerConfigurationRequest dto);
}
