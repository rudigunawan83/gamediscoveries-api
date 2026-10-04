namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public sealed class GameMonetizeOptions
{
    public const string SectionName = "GameMonetize";

    public bool Enabled { get; set; }

    /// <summary>
    /// Optional default/base feed URL kept for backward compatibility.
    /// Used as Latest fallback when Feeds.Latest is empty.
    /// </summary>
    public string? FeedUrl { get; set; }

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetryAttempts { get; set; } = 3;

    public GameMonetizeFeedsOptions Feeds { get; set; } = new();

    public string? ResolveFeedUrl(BuildingBlocks.Feeds.GameFeedType feedType)
    {
        var configured = feedType switch
        {
            BuildingBlocks.Feeds.GameFeedType.Latest => Feeds.Latest,
            BuildingBlocks.Feeds.GameFeedType.Popular => Feeds.Popular,
            BuildingBlocks.Feeds.GameFeedType.Action => Feeds.Action,
            BuildingBlocks.Feeds.GameFeedType.Puzzle => Feeds.Puzzle,
            BuildingBlocks.Feeds.GameFeedType.Racing => Feeds.Racing,
            BuildingBlocks.Feeds.GameFeedType.Sports => Feeds.Sports,
            BuildingBlocks.Feeds.GameFeedType.Multiplayer => Feeds.Multiplayer,
            BuildingBlocks.Feeds.GameFeedType.Mobile => Feeds.Mobile,
            BuildingBlocks.Feeds.GameFeedType.TwoPlayer => Feeds.TwoPlayer,
            BuildingBlocks.Feeds.GameFeedType.Featured => Feeds.Featured,
            BuildingBlocks.Feeds.GameFeedType.HotGames => Feeds.HotGames,
            BuildingBlocks.Feeds.GameFeedType.BestGames => Feeds.BestGames,
            BuildingBlocks.Feeds.GameFeedType.MostPlayed => Feeds.MostPlayed,
            BuildingBlocks.Feeds.GameFeedType.ExclusiveGames => Feeds.ExclusiveGames,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (feedType == BuildingBlocks.Feeds.GameFeedType.Latest
            && !string.IsNullOrWhiteSpace(FeedUrl))
        {
            return FeedUrl;
        }

        return null;
    }
}

public sealed class GameMonetizeFeedsOptions
{
    public string? Latest { get; set; }
    public string? Popular { get; set; }
    public string? Action { get; set; }
    public string? Puzzle { get; set; }
    public string? Racing { get; set; }
    public string? Sports { get; set; }
    public string? Multiplayer { get; set; }
    public string? Mobile { get; set; }
    public string? TwoPlayer { get; set; }
    public string? Featured { get; set; }
    public string? HotGames { get; set; }
    public string? BestGames { get; set; }
    public string? MostPlayed { get; set; }
    public string? ExclusiveGames { get; set; }
}
