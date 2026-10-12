namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

/// <summary>SQL filters that keep seeded demo accounts and staff testing out of usage metrics.</summary>
public static class MonitoringAudience
{
    /// <summary>Accounts from the demo seed migrations plus moderators, admins, and super admins.</summary>
    public const string ExcludedUserIds =
        """
        SELECT id FROM users
        WHERE lower(email) = 'demo@gamediscoveries.com'
           OR lower(email) ~ '^player[0-9]+@gamediscoveries\.com$'
        UNION
        SELECT user_id FROM user_roles WHERE role IN ('Moderator', 'Admin', 'SuperAdmin')
        """;

    /// <summary>
    /// For rows with unqualified <c>user_id</c> and <c>anonymous_id</c> columns. A device that ever signed in
    /// as an excluded account is excluded too, including its guest activity before or after sign-in.
    /// </summary>
    public const string IsRealActivity =
        $"""
        NOT (COALESCE(user_id IN ({ExcludedUserIds}), false)
             OR COALESCE(anonymous_id IN (
                 SELECT anonymous_id FROM analytics_events
                 WHERE anonymous_id IS NOT NULL AND user_id IN ({ExcludedUserIds})), false))
        """;

    /// <summary>For the <c>users</c> table, whose key is <c>id</c>.</summary>
    public const string IsRealUser = $"id NOT IN ({ExcludedUserIds})";
}
