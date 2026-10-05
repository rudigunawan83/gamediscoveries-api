using GameDiscoveries.Modules.Achievements.Data;
using GameDiscoveries.Modules.Achievements.Models;

namespace GameDiscoveries.Modules.Achievements.Evaluation;

public sealed record RequirementProgressContext(
    Guid UserId,
    IReadOnlyDictionary<string, int> Metrics);

public sealed record RequirementProgressResult(
    string RequirementType,
    int CurrentValue,
    int TargetValue,
    bool IsComplete,
    double Percentage);

public interface IRequirementEvaluator
{
    Task<RequirementProgressContext> CreateContextAsync(Guid userId, CancellationToken cancellationToken = default);

    RequirementProgressResult Evaluate(AchievementDefinitionEntity definition, RequirementProgressContext context);
}

public sealed class CompositeRequirementEvaluator(IAchievementStore store) : IRequirementEvaluator
{
    public async Task<RequirementProgressContext> CreateContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var metrics = await store.GetMetricsAsync(userId, cancellationToken);
        return new RequirementProgressContext(userId, metrics);
    }

    public RequirementProgressResult Evaluate(
        AchievementDefinitionEntity definition,
        RequirementProgressContext context)
    {
        var current = context.Metrics.TryGetValue(definition.RequirementType, out var value) ? value : 0;
        var target = Math.Max(1, definition.TargetValue);
        var percentage = Math.Round(100d * Math.Min(current, target) / target, 2);
        return new RequirementProgressResult(
            definition.RequirementType,
            current,
            target,
            current >= target,
            percentage);
    }
}
