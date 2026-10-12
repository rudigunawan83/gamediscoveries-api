using System.Text;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Administration.Features.Users;

public static class AdminUsersEndpoints
{
    public static IEndpointRouteBuilder MapAdminUsers(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/users/directory", async (
                string? search,
                string? status,
                string? role,
                string? joined,
                string? sort,
                string? dir,
                int? page,
                int? pageSize,
                IAdminUsersService users,
                CancellationToken cancellationToken) =>
            {
                var filter = AdminUsersQuery.Parse(search, status, role, joined, sort, dir, DateTimeOffset.UtcNow);
                var result = await users.ListAsync(filter, page ?? 1, pageSize ?? 10, cancellationToken);
                return Results.Ok(ApiResponse<AdminUsersPage>.Ok(result));
            })
            .WithName("AdminUsersDirectory")
            .WithTags("Administration", "Users")
            .WithSummary("Searchable user directory with role, status, last activity, and games played.")
            .WithDescription(
                "status: active | suspended. role: Player | Moderator | Admin | SuperAdmin | Developer | Advertiser. " +
                "joined: 24h | 7d | 30d | 90d | 365d. sort: lastActive | joined | games | name | xp; dir: asc | desc. " +
                "Games played counts distinct games with a valid play session.")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/v1/admin/users/stats", async (
                IAdminUsersService users,
                CancellationToken cancellationToken) =>
            {
                var stats = await users.GetStatsAsync(cancellationToken);
                return Results.Ok(ApiResponse<AdminUsersStats>.Ok(stats));
            })
            .WithName("AdminUsersStats")
            .WithTags("Administration", "Users")
            .WithSummary("Total, active, new, and suspended user counts with daily trends.")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/v1/admin/users/export", async (
                string? search,
                string? status,
                string? role,
                string? joined,
                string? sort,
                string? dir,
                IAdminUsersService users,
                CancellationToken cancellationToken) =>
            {
                var now = DateTimeOffset.UtcNow;
                var filter = AdminUsersQuery.Parse(search, status, role, joined, sort, dir, now);
                var csv = await users.ExportCsvAsync(filter, cancellationToken);
                return Results.File(
                    Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(),
                    "text/csv; charset=utf-8",
                    $"users-{now:yyyyMMdd-HHmm}.csv");
            })
            .WithName("AdminUsersExport")
            .WithTags("Administration", "Users")
            .WithSummary($"CSV of the filtered user directory (up to {AdminUsersQuery.MaxExportRows} rows).")
            .RequireAuthorization(Policies.AdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
