using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Administration.Features.Monitoring;

namespace GameDiscoveries.UnitTests.Administration;

public class MonitoringWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, "30d")]
    [InlineData("30d", "30d")]
    [InlineData("7d", "7d")]
    [InlineData("24h", "24h")]
    public void Parse_accepts_presets(string? raw, string key)
    {
        var window = MonitoringWindow.Parse(raw, Now);

        window.Key.Should().Be(key);
        window.To.Should().Be(Now);
        window.From.Should().BeBefore(Now);
    }

    [Fact]
    public void Parse_rejects_unknown_range()
    {
        var act = () => MonitoringWindow.Parse("365d", Now);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Parse_accepts_inclusive_custom_dates()
    {
        var window = MonitoringWindow.Parse(null, "2026-10-01", "2026-10-08", Now);

        window.Key.Should().Be("custom:2026-10-01:2026-10-08");
        window.From.Should().Be(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        window.To.Should().Be(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        window.Hourly.Should().BeFalse();
        window.Previous.From.Should().Be(new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero));
        window.Previous.To.Should().Be(window.From);
    }

    [Fact]
    public void Parse_rejects_custom_range_over_90_days()
    {
        var act = () => MonitoringWindow.Parse(null, "2026-01-01", "2026-05-01", Now);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Parse_rejects_custom_without_both_dates()
    {
        var act = () => MonitoringWindow.Parse("custom", "2026-10-21", null, Now);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Change_percent_is_null_without_a_baseline()
    {
        MonitoringMath.ChangePercent(4, 0).Should().BeNull();
    }

    [Fact]
    public void Change_percent_uses_the_previous_window()
    {
        MonitoringMath.ChangePercent(15, 10).Should().Be(50);
    }
}
