using FluentAssertions;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;

namespace GameDiscoveries.UnitTests.Xp;

public sealed class LevelCalculatorTests
{
    private static IReadOnlyList<LevelDefinitionDto> StandardCurve()
    {
        // Mirrors seed: level 1 = 0, then +50*n per step
        long req = 0;
        var list = new List<LevelDefinitionDto>();
        for (var n = 1; n <= 100; n++)
        {
            if (n > 1)
            {
                req += 50L * n;
            }

            var title = n switch
            {
                >= 100 => "Ultimate Discoverer",
                >= 50 => "Game Legend",
                >= 30 => "Game Master",
                >= 20 => "Adventurer",
                >= 10 => "Game Hunter",
                >= 5 => "Explorer",
                _ => "Newcomer"
            };
            list.Add(new LevelDefinitionDto(n, req, title, null, true));
        }

        return list;
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(249, 2)]
    [InlineData(250, 3)]
    [InlineData(850, 5)]
    public void Calculates_expected_level(long totalXp, int expectedLevel)
    {
        var info = LevelCalculator.Calculate(totalXp, StandardCurve());
        info.Level.Should().Be(expectedLevel);
    }

    [Fact]
    public void Progress_at_850_is_50_percent_for_level_5()
    {
        // Level 5 = 700, Level 6 = 1000 → current 150 / next 300 = 50%
        var info = LevelCalculator.Calculate(850, StandardCurve());
        info.Level.Should().Be(5);
        info.Title.Should().Be("Explorer");
        info.CurrentLevelXp.Should().Be(150);
        info.NextLevelXp.Should().Be(300);
        info.ProgressPercentage.Should().Be(50);
        info.IsMaxLevel.Should().BeFalse();
    }

    [Fact]
    public void Max_level_returns_full_progress()
    {
        var curve = StandardCurve();
        var maxXp = curve[^1].RequiredTotalXp + 10_000;
        var info = LevelCalculator.Calculate(maxXp, curve);
        info.Level.Should().Be(100);
        info.IsMaxLevel.Should().BeTrue();
        info.NextLevelXp.Should().Be(0);
        info.ProgressPercentage.Should().Be(100);
        info.Title.Should().Be("Ultimate Discoverer");
    }

    [Fact]
    public void Empty_curve_defaults_to_level_1()
    {
        var info = LevelCalculator.Calculate(500, []);
        info.Level.Should().Be(1);
        info.IsMaxLevel.Should().BeTrue();
    }

    [Fact]
    public void Inactive_levels_are_ignored()
    {
        var levels = new List<LevelDefinitionDto>
        {
            new(1, 0, "Newcomer", null, true),
            new(2, 100, "Hidden", null, false),
            new(3, 250, "Explorer", null, true)
        };

        var info = LevelCalculator.Calculate(200, levels);
        info.Level.Should().Be(1);
        info.NextLevelXp.Should().Be(250); // jumps to next active level 3
    }
}
