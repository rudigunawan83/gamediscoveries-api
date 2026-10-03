using FluentAssertions;
using GameDiscoveries.Infrastructure.PostgreSQL;
using GameDiscoveries.Infrastructure.Providers.GameMonetize;
using GameDiscoveries.Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.UnitTests.Configuration;

public sealed class OptionsValidationTests
{
    [Fact]
    public void DatabaseOptions_requires_connection_string()
    {
        var services = new ServiceCollection();
        services.AddOptions<DatabaseOptions>()
            .Configure(o => o.ConnectionString = string.Empty)
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Database:ConnectionString is required.")
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void GameMonetizeOptions_binds_defaults()
    {
        var options = new GameMonetizeOptions
        {
            Enabled = false,
            FeedUrl = null
        };

        options.Enabled.Should().BeFalse();
        options.FeedUrl.Should().BeNull();
    }

    [Fact]
    public void RedisOptions_has_expected_defaults()
    {
        var options = new RedisOptions();
        options.ConnectionString.Should().Be("localhost:6379");
        options.InstanceName.Should().Be("gamediscoveries:");
        options.Enabled.Should().BeTrue();
    }
}
