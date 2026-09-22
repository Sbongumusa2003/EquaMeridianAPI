using FluentAssertions;
using Xunit;

namespace EquaMeridian.Tests.Services;

public class AppTimeTests
{
    [Fact]
    public void Now_IsInSouthAfricaTimezone()
    {
        var now = AppTime.Now;
        var utc = AppTime.UtcNow;
        var offsetHours = (now - utc).TotalHours;
        offsetHours.Should().BeApproximately(2, 0.05);
    }

    [Fact]
    public void ToSouthAfrica_ConvertsUtcCorrectly()
    {
        var utc = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var sa = AppTime.ToSouthAfrica(utc);
        sa.Hour.Should().Be(12);
    }

    [Fact]
    public void Format_AppendsSastLabel()
    {
        var value = new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Unspecified);
        var formatted = AppTime.Format(value);
        formatted.Should().Contain("SAST");
        formatted.Should().Contain("2026");
    }

    [Fact]
    public void DisplayLabel_IsSast()
    {
        AppTime.DisplayLabel.Should().Be("SAST");
    }
}
