using EquaMeridian.Infrastructure.Data;
using EquaMeridian.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EquaMeridian.Tests.Pricing;

public class PricingEngineTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PricingEngine _engine;

    public PricingEngineTests()
    {
        _db = TestDbContextFactory.Create();
        SeedFeeConfig();
        _engine = new PricingEngine(_db);
    }

    public void Dispose() => _db.Dispose();

    private void SeedFeeConfig(
        decimal vatRate = 15m,
        decimal baseFee = 350m,
        decimal freeRadiusKm = 50m,
        decimal ratePerKm = 15m)
    {
        _db.FeeConfigurations.Add(new FeeConfiguration
        {
            FeeConfigurationID = 1,
            CommissionRate = 10m,
            MinFee = 0,
            MaxFee = 0,
            VATInclusive = false,
            VATRate = vatRate,
            DeliveryBaseFee = baseFee,
            DeliveryFreeRadiusKm = freeRadiusKm,
            DeliveryRatePerKm = ratePerKm,
            UpdatedAt = AppTime.Now
        });
        _db.SaveChanges();
    }

    private void SeedDiscountTier()
    {
        _db.DiscountTiers.AddRange(
            new DiscountTier { DiscountTierID = 1, CategoryID = null, MinDays = 7, MaxDays = 13, DiscountPercent = 5m },
            new DiscountTier { DiscountTierID = 2, CategoryID = null, MinDays = 14, MaxDays = 29, DiscountPercent = 10m },
            new DiscountTier { DiscountTierID = 3, CategoryID = null, MinDays = 30, MaxDays = null, DiscountPercent = 15m },
            new DiscountTier { DiscountTierID = 4, CategoryID = 99, MinDays = 7, MaxDays = null, DiscountPercent = 20m }
        );
        _db.SaveChanges();
    }

    [Fact]
    public async Task CalculateAsync_BasicRental_NoDiscountNoDeliveryExtra()
    {
        var result = await _engine.CalculateAsync(
            dailyRate: 1000m,
            numberOfDays: 3,
            quantity: 1,
            deliveryDistanceKm: 20m);

        result.RentalSubtotal.Should().Be(3000m);
        result.DiscountPercent.Should().Be(0m);
        result.DiscountAmount.Should().Be(0m);
        result.DeliveryFee.Should().Be(350m);
        result.PriceExclVat.Should().Be(3350m);
        result.VatRate.Should().Be(15m);
        result.VatAmount.Should().Be(502.50m);
        result.PriceInclVat.Should().Be(3852.50m);
        result.GrandTotal.Should().Be(3852.50m);
    }

    [Fact]
    public async Task CalculateAsync_AppliesLongTermDiscount()
    {
        SeedDiscountTier();

        var result = await _engine.CalculateAsync(
            dailyRate: 1000m,
            numberOfDays: 10,
            quantity: 1,
            deliveryDistanceKm: 10m);

        result.RentalSubtotal.Should().Be(10000m);
        result.DiscountPercent.Should().Be(5m);
        result.DiscountAmount.Should().Be(500m);
        result.DeliveryFee.Should().Be(350m);
        result.PriceExclVat.Should().Be(9850m);
    }

    [Fact]
    public async Task CalculateAsync_CategorySpecificTier_OverridesPlatform()
    {
        SeedDiscountTier();

        var result = await _engine.CalculateAsync(
            dailyRate: 500m,
            numberOfDays: 10,
            quantity: 2,
            deliveryDistanceKm: 0m,
            categoryId: 99);

        result.RentalSubtotal.Should().Be(10000m);
        result.DiscountPercent.Should().Be(20m);
        result.DiscountAmount.Should().Be(2000m);
    }

    [Fact]
    public async Task CalculateAsync_PromoDiscount_AddsToTierAndCapsAt100()
    {
        SeedDiscountTier();

        var result = await _engine.CalculateAsync(
            dailyRate: 1000m,
            numberOfDays: 30,
            quantity: 1,
            deliveryDistanceKm: 0m,
            promoDiscountPercent: 90m);

        result.DiscountPercent.Should().Be(100m);
        result.DiscountAmount.Should().Be(30000m);
        result.PriceExclVat.Should().Be(350m);
        result.TierDiscountPercent.Should().Be(15m);
        result.PromoDiscountPercent.Should().Be(85m);
    }

    [Fact]
    public async Task CalculateAsync_MinimumDaysAndQuantity_Enforced()
    {
        var result = await _engine.CalculateAsync(
            dailyRate: 200m,
            numberOfDays: 0,
            quantity: 0,
            deliveryDistanceKm: 5m);

        result.RentalSubtotal.Should().Be(200m);
    }

    [Fact]
    public async Task CalculateAsync_MultipliesDeliveryFeeByQuantity()
    {
        var result = await _engine.CalculateAsync(
            dailyRate: 1000m,
            numberOfDays: 3,
            quantity: 3,
            deliveryDistanceKm: 20m);

        // Per-unit delivery fee at 20km (within the 50km free radius) is the base fee of 350.
        // Delivering 3 units of machinery should cost 3x the per-unit delivery fee.
        result.DeliveryFee.Should().Be(1050m);
        result.RentalSubtotal.Should().Be(9000m);
        result.PriceExclVat.Should().Be(10050m);
    }

    [Theory]
    [InlineData(0, 350)]
    [InlineData(50, 350)]
    [InlineData(51, 365)]
    [InlineData(100, 1100)]
    public async Task CalculateDeliveryFeeAsync_RespectsFreeRadiusAndRate(decimal distanceKm, decimal expectedFee)
    {
        var fee = await _engine.CalculateDeliveryFeeAsync(distanceKm);
        fee.Should().Be(expectedFee);
    }

    [Fact]
    public async Task GetDiscountPercentAsync_NoTiers_ReturnsZero()
    {
        var percent = await _engine.GetDiscountPercentAsync(14);
        percent.Should().Be(0m);
    }

    [Fact]
    public async Task GetDiscountPercentAsync_SelectsBestMatchingTier()
    {
        SeedDiscountTier();

        (await _engine.GetDiscountPercentAsync(5)).Should().Be(0m);
        (await _engine.GetDiscountPercentAsync(7)).Should().Be(5m);
        (await _engine.GetDiscountPercentAsync(14)).Should().Be(10m);
        (await _engine.GetDiscountPercentAsync(45)).Should().Be(15m);
    }

    [Fact]
    public async Task GetDiscountPercentAsync_FallsBackToPlatformWideWhenCategoryHasNoMatch()
    {
        SeedDiscountTier();
        var percent = await _engine.GetDiscountPercentAsync(14, categoryId: 1);
        percent.Should().Be(10m);
    }
}
