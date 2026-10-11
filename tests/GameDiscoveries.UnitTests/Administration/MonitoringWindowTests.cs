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
