namespace GameDiscoveries.Modules.Catalog.Domain;

/// <summary>
/// The feed lists some titles under several provider ids. Among published, playable games
/// with the same title (case-insensitive) the earliest published one is canonical; the
/// others point their canonical URL at it and stay out of the sitemap.
/// </summary>
public static class CanonicalGames
{
    public const string PlayableFilter = "(g.game_url IS NOT NULL OR g.embed_url IS NOT NULL)";

    public const string TitleOrder = "COALESCE(g.published_at, g.created_at), g.created_at, g.id";

    /// <summary>Scalar subquery for the canonical slug of the game aliased <c>g</c>.</summary>
    public const string CanonicalSlugSql =
        """
        (SELECT c.slug
         FROM games c
         WHERE c.status = 'published'
           AND lower(c.title) = lower(g.title)
           AND (c.game_url IS NOT NULL OR c.embed_url IS NOT NULL)
         ORDER BY COALESCE(c.published_at, c.created_at), c.created_at, c.id
         LIMIT 1)
        """;
}
