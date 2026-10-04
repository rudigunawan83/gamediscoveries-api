namespace GameDiscoveries.Modules.Community.Configuration;

public sealed class CommunityOptions
{
    public const string SectionName = "Community";

    public int TitleMaxLength { get; set; } = 160;
    public int ContentMaxLength { get; set; } = 4000;
    public int CommentMaxLength { get; set; } = 1500;
    public int BioMaxLength { get; set; } = 500;
    public int PostsPerWindow { get; set; } = 5;
    public int PostWindowMinutes { get; set; } = 10;
    public int CommentsPerWindow { get; set; } = 20;
    public int CommentWindowMinutes { get; set; } = 10;
    public int ReviewsPerWindow { get; set; } = 5;
    public int ReviewWindowMinutes { get; set; } = 30;
    public bool RequirePlayedForReview { get; set; } = false;
    public int FeedPageSize { get; set; } = 20;
    public int TrendingCacheSeconds { get; set; } = 180;
    public int LeaderboardCacheSeconds { get; set; } = 300;
    public int ReviewSummaryCacheSeconds { get; set; } = 120;
}
