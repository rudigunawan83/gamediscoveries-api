using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.Modules.Recommendation.Data;

public interface IRecommendationTrackingStore
{
    Task<Guid> CreateRequestAsync(
        Guid? userId,
        Guid? anonymousId,
        string strategy,
        string section,
        int profileLevel,
        int candidateCount,
        int resultCount,
        string algorithmVersion,
        CancellationToken cancellationToken = default);

    Task RecordImpressionAsync(RecommendationImpressionRequest request, Guid? userId, CancellationToken cancellationToken = default);

    Task RecordFeedbackAsync(
        Guid gameId,
        RecommendationFeedbackRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<AdminRecommendationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

public sealed class RecommendationTrackingStore(IDbConnectionFactory connectionFactory) : IRecommendationTrackingStore
{
    public async Task<Guid> CreateRequestAsync(
        Guid? userId,
        Guid? anonymousId,
        string strategy,
        string section,
        int profileLevel,
        int candidateCount,
        int resultCount,
        string algorithmVersion,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        try
        {
            await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO recommendation_requests (
                    id, user_id, anonymous_id, strategy, section, profile_level,
                    candidate_count, result_count, algorithm_version, created_at)
                VALUES (
                    @Id, @UserId, @AnonymousId, @Strategy, @Section, @ProfileLevel,
                    @CandidateCount, @ResultCount, @AlgorithmVersion, (NOW() AT TIME ZONE 'utc'))
                """,
                new
                {
                    Id = id,
                    UserId = userId,
                    AnonymousId = anonymousId,
                    Strategy = strategy,
                    Section = section,
                    ProfileLevel = profileLevel,
                    CandidateCount = candidateCount,
                    ResultCount = resultCount,
                    AlgorithmVersion = algorithmVersion
                },
                cancellationToken: cancellationToken));
        }
        catch
        {
            // Tracking is best-effort
        }

        return id;
    }

    public async Task RecordImpressionAsync(
        RecommendationImpressionRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO recommendation_impressions (
                id, recommendation_request_id, user_id, anonymous_id, game_id,
                section, position, strategy, score, reason_type, event_type, created_at)
            VALUES (
                @Id, @RequestId, @UserId, @AnonymousId, @GameId,
                @Section, @Position, 'PERSONALIZED', 0, NULL, @EventType, (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                RequestId = request.RecommendationRequestId,
                UserId = userId,
                request.AnonymousId,
                request.GameId,
                request.Section,
                request.Position,
                EventType = string.IsNullOrWhiteSpace(request.EventType) ? "IMPRESSION" : request.EventType.ToUpperInvariant()
            },
            cancellationToken: cancellationToken));
    }

    public async Task RecordFeedbackAsync(
        Guid gameId,
        RecommendationFeedbackRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO recommendation_feedback (
                id, user_id, anonymous_id, game_id, recommendation_request_id,
                feedback_type, section, created_at)
            VALUES (
                @Id, @UserId, @AnonymousId, @GameId, @RequestId,
                @FeedbackType, @Section, (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                request.AnonymousId,
                GameId = gameId,
                RequestId = request.RecommendationRequestId,
                FeedbackType = request.FeedbackType.ToUpperInvariant(),
                request.Section
            },
            cancellationToken: cancellationToken));
    }

    public async Task<AdminRecommendationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        try
        {
            var row = await connection.QuerySingleAsync(new CommandDefinition(
                """
                SELECT
                    (SELECT COUNT(*)::bigint FROM recommendation_requests) AS Requests,
                    (SELECT COUNT(*)::bigint FROM recommendation_impressions WHERE event_type = 'IMPRESSION') AS Impressions,
                    (SELECT COUNT(*)::bigint FROM recommendation_impressions WHERE event_type = 'CLICK') AS Clicks,
                    (SELECT COUNT(*)::bigint FROM recommendation_feedback) AS FeedbackCount,
                    COALESCE((
                        SELECT AVG(CASE WHEN strategy LIKE 'PERSONALIZED%' THEN 1.0 ELSE 0.0 END)
                        FROM recommendation_requests
                    ), 0)::float8 AS PersonalizedShare,
                    (SELECT MAX(created_at) FROM recommendation_requests) AS LastRequestAt
                """,
                cancellationToken: cancellationToken));

            var impressions = (long)row.impressions;
            var clicks = (long)row.clicks;
            var ctr = impressions <= 0 ? 0 : Math.Round(100d * clicks / impressions, 2);

            return new AdminRecommendationOverviewDto(
                (long)row.requests,
                impressions,
                clicks,
                ctr,
                (long)row.feedbackcount,
                Math.Round((double)row.personalizedshare * 100, 2),
                (DateTimeOffset?)row.lastrequestat);
        }
        catch
        {
            return new AdminRecommendationOverviewDto(0, 0, 0, 0, 0, 0, null);
        }
    }
}
