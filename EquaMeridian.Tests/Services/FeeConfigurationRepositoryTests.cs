using EquaMeridian.DTOs.Fees;
using EquaMeridian.Infrastructure.Data;
using EquaMeridian.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EquaMeridian.Tests.Services;

public class FeeConfigurationRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FeeConfigurationRepository _sut;

    public FeeConfigurationRepositoryTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new FeeConfigurationRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetCurrentAsync_WhenEmpty_CreatesDefault()
    {
        var dto = await _sut.GetCurrentAsync();

        dto.Should().NotBeNull();
        dto.CommissionRate.Should().Be(8m);
        dto.VATRate.Should().Be(15m);
        dto.DeliveryBaseFee.Should().Be(350m);
        dto.DeliveryFreeRadiusKm.Should().Be(50m);
        dto.DeliveryRatePerKm.Should().Be(15m);

        _db.FeeConfigurations.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_PersistsNewValues_AndReturnsPreviousSnapshot()
    {
        await _sut.GetCurrentAsync();

        var update = new UpdateFeeConfigurationDto
        {
            CommissionRate = 12m,
            MinFee = 50m,
            MaxFee = 5000m,
            VATInclusive = true,
            VATRate = 15m,
            DeliveryBaseFee = 400m,
            DeliveryFreeRadiusKm = 40m,
            DeliveryRatePerKm = 18m
        };

        var (updated, previousJson) = await _sut.UpdateAsync(update, adminId: 7);

        updated.CommissionRate.Should().Be(12m);
        updated.DeliveryBaseFee.Should().Be(400m);
        updated.DeliveryFreeRadiusKm.Should().Be(40m);
        previousJson.Should().Contain("\"CommissionRate\":8");
        previousJson.Should().Contain("\"DeliveryBaseFee\":350");

        var reloaded = await _sut.GetCurrentAsync();
        reloaded.CommissionRate.Should().Be(12m);
        reloaded.MinFee.Should().Be(50m);
    }
}
