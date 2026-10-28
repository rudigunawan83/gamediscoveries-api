using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Catalog.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Catalog.Features.Sitemap;

public sealed record SitemapGameEntry(string Slug, DateTimeOffset LastModified);

/// <summary>
/// Game URLs that may be indexed: published, playable, and canonical for their title.
/// Uses the same rules as the game page so the sitemap never lists a page that says noindex
/// or points its canonical elsewhere.
/// </summary>
public sealed class SitemapGamesHandler(IDbConnectionFactory connectionFactory)
{
    /// <summary>The sitemap protocol allows 50,000 URLs per file.</summary>
    public const int MaxEntries = 45_000;

    private const string Sql =
        $"""
        WITH ranked AS (
            SELECT g.slug,
                   g.content_updated_at,
                   ROW_NUMBER() OVER (PARTITION BY lower(g.title) ORDER BY {CanonicalGames.TitleOrder}) AS title_rank
            FROM games g
            WHERE g.status = 'published'
              AND {CanonicalGames.PlayableFilter}
              AND {CanonicalGames.ProviderNotBlockedFilter}
              AND g.slug <> ''
              AND btrim(g.title) <> ''
        )
        SELECT slug AS Slug, content_updated_at AS LastModified
        FROM ranked
        WHERE title_rank = 1
        ORDER BY content_updated_at DESC, slug
        LIMIT @Limit
        """;

    public async Task<IReadOnlyList<SitemapGameEntry>> HandleAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SitemapRow>(
            new CommandDefinition(Sql, new { Limit = MaxEntries }, cancellationToken: cancellationToken));
        return rows.Select(row => new SitemapGameEntry(row.Slug, row.LastModified)).ToList();
    }

    private sealed class SitemapRow
    {
        public string Slug { get; init; } = string.Empty;
        public DateTimeOffset LastModified { get; init; }
    }
}

public static class SitemapGamesEndpoint
{
    public static RouteHandlerBuilder MapSitemapGames(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/api/v1/sitemap/games", async (
                SitemapGamesHandler handler,
                CancellationToken cancellationToken) =>
            {
                var items = await handler.HandleAsync(cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<SitemapGameEntry>>.Ok(items));
            })
            .WithName("ListSitemapGames")
            .WithTags("Catalog")
            .WithSummary("Indexable game URLs with content last-modified times.")
            .Produces<ApiResponse<IReadOnlyList<SitemapGameEntry>>>(StatusCodes.Status200OK)
            .RequireRateLimiting("public")
            .AllowAnonymous();
    }
}
