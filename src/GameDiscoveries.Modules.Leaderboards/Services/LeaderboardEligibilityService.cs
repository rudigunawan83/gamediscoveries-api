using GameDiscoveries.Modules.Leaderboards.Options;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Leaderboards.Services;

public interface ILeaderboardEligibilityService
{
    bool IsXpTransactionEligible(string ruleCode, string eventType, int xpAmount);
    bool IsCompetitionReward(string ruleCode, string eventType);
}

public sealed class LeaderboardEligibilityService(IOptions<LeaderboardOptions> options) : ILeaderboardEligibilityService
{
    private static readonly HashSet<string> ExcludedRuleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN_ADJUSTMENT",
        "XP_REVERSAL",
        "COMPETITION_REWARD"
    };

    private static readonly HashSet<string> ExcludedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN_ADJUSTMENT",
        "COMPETITION_REWARD"
    };

    public bool IsCompetitionReward(string ruleCode, string eventType) =>
        string.Equals(ruleCode, "COMPETITION_REWARD", StringComparison.OrdinalIgnoreCase)
        || string.Equals(eventType, "COMPETITION_REWARD", StringComparison.OrdinalIgnoreCase);

    public bool IsXpTransactionEligible(string ruleCode, string eventType, int xpAmount)
    {
        if (xpAmount == 0) return false;

        // Reversals of eligible XP should still adjust score (negative amount).
        if (string.Equals(ruleCode, "XP_REVERSAL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "XP_REVERSAL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsCompetitionReward(ruleCode, eventType))
        {
            return false;
        }

        if (ExcludedRuleCodes.Contains(ruleCode) || ExcludedEventTypes.Contains(eventType))
        {
            if (string.Equals(ruleCode, "ADMIN_ADJUSTMENT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "ADMIN_ADJUSTMENT", StringComparison.OrdinalIgnoreCase))
            {
                return options.Value.IncludeAdminAdjustmentInLeaderboard;
            }

            return false;
        }

        return true;
    }
}
