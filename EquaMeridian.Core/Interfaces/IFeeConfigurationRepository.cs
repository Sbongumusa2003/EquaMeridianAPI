using EquaMeridian.DTOs.Fees;

public interface IFeeConfigurationRepository
{
    Task<FeeConfigurationDto> GetCurrentAsync();
    Task<(FeeConfigurationDto Updated, string PreviousValuesJson)> UpdateAsync(
        UpdateFeeConfigurationDto dto, int adminId);
}
