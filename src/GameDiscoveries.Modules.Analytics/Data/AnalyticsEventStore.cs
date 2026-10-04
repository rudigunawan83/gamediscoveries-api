using System.Data.Common;
using System.Text.Json;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;

namespace GameDiscoveries.Modules.Analytics.Data;

public interface IAnalyticsEventStore
{
    Task TrackAsync(AnalyticsEventWriteModel model, CancellationToken cancellationToken = default);
}

public sealed record AnalyticsEventWriteModel(
    string EventName,
    Guid? UserId,
    string? SessionId,
    Guid? GameId,
    IReadOnlyDictionary<string, object?> Properties);

public sealed class AnalyticsEventStore(IDbConnectionFactory connectionFactory) : IAnalyticsEventStore
{
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "recommendation_impression",
        "recommendation_clicked",
        "recommendation_started",
        "recommendation_completed",
        "recommendation_dismissed",
        "page_view",
        "game_viewed",
        "game_started",
        "game_exited",
        "community_viewed",
        "community_post_created",
        "community_post_viewed",
        "community_comment_created",
        "community_reaction_added",
        "community_review_created",
        "community_game_shared",
        "community_user_followed",
        "community_achievement_unlocked",
        "community_leaderboard_viewed",
        "community_report_created"
    };

    public async Task TrackAsync(AnalyticsEventWriteModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.EventName) || !AllowedEvents.Contains(model.EventName))
        {
            throw new BuildingBlocks.Errors.ValidationException("Unsupported analytics event.");
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            INSERT INTO analytics_events (id, event_name, user_id, session_id, game_id, properties, created_at)
            VALUES (@Id, @EventName, @UserId, @SessionId, @GameId, CAST(@Properties AS jsonb), (NOW() AT TIME ZONE 'utc'));
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                EventName = model.EventName.Trim().ToLowerInvariant(),
                model.UserId,
                SessionId = string.IsNullOrWhiteSpace(model.SessionId) ? null : model.SessionId.Trim(),
                model.GameId,
                Properties = JsonSerializer.Serialize(model.Properties ?? new Dictionary<string, object?>())
            },
            cancellationToken: cancellationToken));
    }
}
