using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.Modules.Users.Data;

public interface IUserRepository
{
    Task<(UserAccount User, string PasswordHash)?> GetByEmailWithPasswordAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task CreateUserAsync(
        Guid id,
        string email,
        string passwordHash,
        string? displayName,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>Sets the avatar URL and returns the previous one.</summary>
    Task<string?> UpdateAvatarUrlAsync(
        Guid id,
        string? avatarUrl,
        CancellationToken cancellationToken = default);
}

public sealed class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<(UserAccount User, string PasswordHash)?> GetByEmailWithPasswordAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                u.id AS Id,
                u.email AS Email,
                u.password_hash AS PasswordHash,
                u.display_name AS DisplayName,
                u.username AS Username,
                u.avatar_url AS AvatarUrl,
                u.status AS Status
            FROM users u
            WHERE LOWER(u.email) = LOWER(@Email)
            LIMIT 1;
            """;

        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserAuthRow>(
            new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var roles = await LoadRolesAsync(connection, row.Id, cancellationToken);
        var user = new UserAccount
        {
            Id = row.Id,
            Email = row.Email,
            DisplayName = row.DisplayName,
            Username = row.Username,
            AvatarUrl = row.AvatarUrl,
            Status = row.Status,
            Roles = roles
        };

        return (user, row.PasswordHash);
    }

    public async Task<UserAccount?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                u.id AS Id,
                u.email AS Email,
                u.display_name AS DisplayName,
                u.username AS Username,
                u.avatar_url AS AvatarUrl,
                u.status AS Status
            FROM users u
            WHERE u.id = @Id
            LIMIT 1;
            """;

        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var roles = await LoadRolesAsync(connection, row.Id, cancellationToken);
        return new UserAccount
        {
            Id = row.Id,
            Email = row.Email,
            DisplayName = row.DisplayName,
            Username = row.Username,
            AvatarUrl = row.AvatarUrl,
            Status = row.Status,
            Roles = roles
        };
    }

    public async Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM users);";

        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1
                FROM users
                WHERE LOWER(email) = LOWER(@Email)
            );
            """;

        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken));
    }

    public async Task CreateUserAsync(
        Guid id,
        string email,
        string passwordHash,
        string? displayName,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        const string insertUserSql = """
            INSERT INTO users (id, email, password_hash, display_name, username, status)
            VALUES (
                @Id,
                @Email,
                @PasswordHash,
                @DisplayName,
                LOWER(REGEXP_REPLACE(COALESCE(NULLIF(@DisplayName, ''), SPLIT_PART(@Email, '@', 1)) || '-' || SUBSTRING(REPLACE(@Id::text, '-', ''), 1, 6), '[^a-zA-Z0-9]+', '-', 'g')),
                'active'
            );
            """;

        const string insertRoleSql = """
            INSERT INTO user_roles (user_id, role)
            VALUES (@UserId, @Role)
            ON CONFLICT DO NOTHING;
            """;

        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                insertUserSql,
                new
                {
                    Id = id,
                    Email = email,
                    PasswordHash = passwordHash,
                    DisplayName = displayName
                },
                transaction: tx,
                cancellationToken: cancellationToken));

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertRoleSql,
                    new { UserId = id, Role = role },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<string?> UpdateAvatarUrlAsync(
        Guid id,
        string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE users u
            SET avatar_url = @AvatarUrl,
                updated_at = (NOW() AT TIME ZONE 'utc')
            FROM (SELECT avatar_url FROM users WHERE id = @Id) previous
            WHERE u.id = @Id
            RETURNING previous.avatar_url;
            """;

        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { Id = id, AvatarUrl = avatarUrl }, cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<string>> LoadRolesAsync(
        System.Data.Common.DbConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT role
            FROM user_roles
            WHERE user_id = @UserId
            ORDER BY role;
            """;

        var roles = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));

        return roles.ToArray();
    }

    private sealed class UserAuthRow
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string? Username { get; init; }
        public string? AvatarUrl { get; init; }
        public string Status { get; init; } = "active";
    }

    private sealed class UserRow
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string? Username { get; init; }
        public string? AvatarUrl { get; init; }
        public string Status { get; init; } = "active";
    }
}
