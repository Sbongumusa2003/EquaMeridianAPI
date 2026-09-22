using FluentAssertions;
using Xunit;

namespace EquaMeridian.Tests.Services;

public class PlaceholderDeliveryDistanceServiceTests
{
    private readonly PlaceholderDeliveryDistanceService _sut = new();

    [Fact]
    public async Task SameOriginAndDestination_ReturnsZero()
    {
        var km = await _sut.CalculateDistanceKmAsync("Johannesburg", "Johannesburg");
        km.Should().Be(0m);
    }

    [Fact]
    public async Task EmptyOrigin_ReturnsZero()
    {
        var km = await _sut.CalculateDistanceKmAsync(null, "Cape Town");
        km.Should().Be(0m);
    }

    [Fact]
    public async Task DifferentAddresses_ReturnsStablePositiveDistance()
    {
        var a = await _sut.CalculateDistanceKmAsync("Johannesburg Depot", "Cape Town Site");
        var b = await _sut.CalculateDistanceKmAsync("Johannesburg Depot", "Cape Town Site");

        a.Should().Be(b);
        a.Should().BeGreaterThanOrEqualTo(5m);
        a.Should().BeLessThanOrEqualTo(120m);
    }
}
