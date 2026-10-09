namespace GameDiscoveries.Modules.Community.Domain;

public static class PostTypes
{
    public const string Discussion = "discussion";
    public const string Recommendation = "recommendation";
    public const string Question = "question";
    public const string AchievementShare = "achievement_share";
    public const string GameShare = "game_share";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Discussion, Recommendation, Question, AchievementShare, GameShare
    };
}

public static class ReactionKinds
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "like", "helpful", "love", "funny"
    };
}

public sealed record CommunityUserDto(
    Guid Id,
    string Username,
    string? DisplayName,
    string? AvatarUrl);

public sealed record CommunityGameDto(
    Guid Id,
    string Slug,
    string Title,
    string? ThumbnailUrl);

public sealed record CommunityPostDto(
    Guid Id,
    string Slug,
    string Type,
    string Title,
    string Content,
    string Status,
    int CommentCount,
    int ReactionCount,
    int ViewCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    CommunityUserDto Author,
    CommunityGameDto? Game,
    string? ViewerReaction);

public sealed record CommunityCommentDto(
    Guid Id,
    Guid PostId,
    Guid? ParentId,
    string Content,
    int ReactionCount,
    DateTimeOffset CreatedAt,
    CommunityUserDto Author,
    IReadOnlyList<CommunityCommentDto> Replies,
    string? ViewerReaction);

public sealed record GameReviewDto(
    Guid Id,
    Guid GameId,
    int Rating,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    CommunityUserDto Author);

public sealed record MyReviewDto(
    Guid Id,
    int Rating,
    string Content,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    CommunityGameDto Game);

public sealed record PrivacySettingsDto(
    bool ShowFavorites,
    bool ShowHistory,
    bool ShowAchievements,
    bool ShowActivity,
    bool ShowOnLeaderboards,
    string? Bio);

public sealed record GameReviewSummaryDto(
    Guid GameId,
    double AverageRating,
    int ReviewCount);

public sealed record UserProfileDto(
    Guid Id,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    string? Bio,
    DateTimeOffset JoinedAt,
    int GamesPlayed,
    int Favorites,
    int Achievements,
    int Reviews,
    int Followers,
    int Following,
    bool IsFollowing,
    bool IsBlocked,
    bool ShowFavorites,
    bool ShowHistory,
    bool ShowAchievements,
    bool ShowActivity,
    IReadOnlyList<CommunityGameDto> FavoriteGames,
    IReadOnlyList<AchievementDto> RecentAchievements);

public sealed record AchievementDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Icon,
    string Rarity,
    DateTimeOffset? UnlockedAt);

public sealed record ChallengeDto(
    Guid Id,
    string Title,
    string Description,
    string Type,
    int TargetValue,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    int Progress,
    bool Completed);

public sealed record LeaderboardEntryDto(
    int Rank,
    Guid UserId,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    int Score);

public sealed record NotificationDto(
    Guid Id,
    string Type,
    Guid? ActorId,
    string? ActorUsername,
    string EntityType,
    Guid EntityId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    string? Message);

public sealed record FeedItemDto(
    Guid Id,
    string ActivityType,
    DateTimeOffset CreatedAt,
    CommunityUserDto User,
    CommunityGameDto? Game,
    string Message,
    int ReactionCount,
    int CommentCount,
    Guid? EntityId,
    string? EntityType);

public sealed record CommunityHomeDto(
    IReadOnlyList<FeedItemDto> Feed,
    IReadOnlyList<CommunityPostDto> TrendingDiscussions,
    IReadOnlyList<CommunityGameDto> PopularGames,
    IReadOnlyList<FeedItemDto> RecentAchievements,
    IReadOnlyList<ChallengeDto> ActiveChallenges,
    IReadOnlyList<LeaderboardEntryDto> TopPlayers);

public sealed record CommunityReportDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt);

public interface ICommunityGameSignalsService
{
    Task<GameReviewSummaryDto?> GetGameEngagementAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommunityGameDto>> GetTrendingGamesAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, object?>> GetCommunitySignalsAsync(Guid gameId, CancellationToken cancellationToken = default);
}
