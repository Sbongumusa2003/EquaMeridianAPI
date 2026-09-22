using EquaMeridian.DTOs.Fees;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class FeeConfigurationRepository : IFeeConfigurationRepository
{
    private readonly AppDbContext _db;
    public FeeConfigurationRepository(AppDbContext db) => _db = db;

    public async Task<FeeConfigurationDto> GetCurrentAsync()
    {
        var config = await GetOrCreateDefaultAsync();
        return await MapToDtoAsync(config);
    }

    public async Task<(FeeConfigurationDto Updated, string PreviousValuesJson)> UpdateAsync(
        UpdateFeeConfigurationDto dto, int adminId)
    {
        var config = await GetOrCreateDefaultAsync();

        var previousValuesJson = JsonSerializer.Serialize(new
        {
            config.CommissionRate,
            config.MinFee,
            config.MaxFee,
            config.VATInclusive,
            config.VATRate,
            config.DeliveryBaseFee,
            config.DeliveryFreeRadiusKm,
            config.DeliveryRatePerKm
        });

        config.CommissionRate = dto.CommissionRate;
        config.MinFee = dto.MinFee;
        config.MaxFee = dto.MaxFee;
        config.VATInclusive = dto.VATInclusive;
        config.VATRate = dto.VATRate;
        config.DeliveryBaseFee = dto.DeliveryBaseFee;
        config.DeliveryFreeRadiusKm = dto.DeliveryFreeRadiusKm;
        config.DeliveryRatePerKm = dto.DeliveryRatePerKm;
        config.UpdatedByAdminID = adminId;
        config.UpdatedAt = AppTime.Now;

        await _db.SaveChangesAsync();

        return (await MapToDtoAsync(config), previousValuesJson);
    }

    private async Task<FeeConfiguration> GetOrCreateDefaultAsync()
    {
        var config = await _db.FeeConfigurations
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();

        if (config == null)
        {
            config = new FeeConfiguration
            {
                CommissionRate = 8m,
                MinFee = 0m,
                MaxFee = 0m,
                VATInclusive = false,
                DeliveryBaseFee = 350m,
                DeliveryFreeRadiusKm = 50m,
                DeliveryRatePerKm = 15m,
                UpdatedAt = AppTime.Now
            };
            _db.FeeConfigurations.Add(config);
            await _db.SaveChangesAsync();
        }

        return config;
    }

    private async Task<FeeConfigurationDto> MapToDtoAsync(FeeConfiguration config)
    {
        string? adminName = null;
        if (config.UpdatedByAdminID.HasValue)
        {
            adminName = await _db.Users
                .Where(u => u.UserID == config.UpdatedByAdminID.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
        }

        return new FeeConfigurationDto
        {
            FeeConfigurationID = config.FeeConfigurationID,
            CommissionRate = config.CommissionRate,
            MinFee = config.MinFee,
            MaxFee = config.MaxFee,
            VATInclusive = config.VATInclusive,
            VATRate = config.VATRate,
            DeliveryBaseFee = config.DeliveryBaseFee,
            DeliveryFreeRadiusKm = config.DeliveryFreeRadiusKm,
            DeliveryRatePerKm = config.DeliveryRatePerKm,
            UpdatedByAdminName = adminName,
            UpdatedAt = config.UpdatedAt
        };
    }
}
