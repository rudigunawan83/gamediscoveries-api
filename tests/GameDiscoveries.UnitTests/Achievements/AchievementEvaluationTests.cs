using FluentAssertions;
using GameDiscoveries.Modules.Achievements.Data;
using GameDiscoveries.Modules.Achievements.Domain;
using GameDiscoveries.Modules.Achievements.Evaluation;
using GameDiscoveries.Modules.Achievements.Models;
using GameDiscoveries.Modules.Achievements.Services;

namespace GameDiscoveries.UnitTests.Achievements;

public sealed class AchievementEvaluationTests
{
    [Fact]
    public void Unlocks_at_target()
    {
        var definition = Definition(target: 5);
        var result = Evaluate(definition, 5);

        result.IsComplete.Should().BeTrue();
        result.Percentage.Should().Be(100);
    }

    [Fact]
    public void Does_not_unlock_below_target()
    {
        var definition = Definition(target: 5);
        var result = Evaluate(definition, 4);

        result.IsComplete.Should().BeFalse();
        result.Percentage.Should().Be(80);
    }

    [Fact]
    public void Already_unlocked_renders_complete_idempotently()
    {
        var definition = Definition(target: 5);
        var unlock = new AchievementUnlockEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AchievementDefinitionId = definition.Id,
            UnlockedAt = DateTimeOffset.UtcNow,
            ProgressValue = 5,
            TargetValue = 5
        };

        var dto = AchievementService.ToUserDto(definition, unlock, Evaluate(definition, 99));

        dto.IsUnlocked.Should().BeTrue();
        dto.ProgressPercentage.Should().Be(100);
        dto.ProgressValue.Should().Be(5);
    }

    [Fact]
    public void Secret_achievement_is_masked_until_unlock()
    {
        var definition = Definition(isSecret: true, rewardXp: 250);
        var dto = AchievementService.ToUserDto(definition, null, Evaluate(definition, 2));

        dto.Title.Should().Be("???");
        dto.Description.Should().Be("Keep exploring to discover this achievement.");
        dto.TargetValue.Should().Be(0);
        dto.RewardXp.Should().Be(0);
    }

    [Fact]
    public void Inactive_definition_can_be_detected_before_unlock()
    {
        var definition = Definition(isActive: false);

        definition.IsActive.Should().BeFalse();
        Evaluate(definition, 10).IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Requirement_mapping_targets_session_metrics()
    {
        var requirements = AchievementTriggerRequirements.ForTrigger(AchievementTriggers.ValidSessionEnded);

        requirements.Should().Contain(AchievementRequirementTypes.FirstGamePlayed);
        requirements.Should().Contain(AchievementRequirementTypes.GamesPlayed);
        requirements.Should().Contain(AchievementRequirementTypes.UniqueGamesPlayed);
        requirements.Should().Contain(AchievementRequirementTypes.GamesDiscovered);
        requirements.Should().Contain(AchievementRequirementTypes.UniqueGenresPlayed);
        requirements.Should().Contain(AchievementRequirementTypes.TotalActiveTime);
        requirements.Should().Contain(AchievementRequirementTypes.ValidSessionsCount);
        requirements.Should().NotContain(AchievementRequirementTypes.FavoritesCount);
    }

    [Fact]
    public void Progress_percentage_is_capped_at_100()
    {
        var definition = Definition(target: 5);
        var result = Evaluate(definition, 7);

        result.Percentage.Should().Be(100);
    }

    private static RequirementProgressResult Evaluate(AchievementDefinitionEntity definition, int current)
    {
        var store = new FakeAchievementStore(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [definition.RequirementType] = current
        });
        var evaluator = new CompositeRequirementEvaluator(store);
        return evaluator.Evaluate(definition, new RequirementProgressContext(Guid.NewGuid(), store.Metrics));
    }

    private static AchievementDefinitionEntity Definition(
        int target = 5,
        bool isSecret = false,
        bool isActive = true,
        int rewardXp = 50) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = "TEST",
            Title = "Test",
            Description = "Test description",
            Category = AchievementCategories.Discovery,
            Difficulty = AchievementDifficulties.Easy,
            RequirementType = AchievementRequirementTypes.UniqueGamesPlayed,
            TargetValue = target,
            RewardXp = rewardXp,
            IsSecret = isSecret,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private sealed class FakeAchievementStore(IReadOnlyDictionary<string, int> metrics) : IAchievementStore
    {
        public IReadOnlyDictionary<string, int> Metrics { get; } = metrics;

        public Task<IReadOnlyDictionary<string, int>> GetMetricsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Metrics);

        public Task<IReadOnlyList<AchievementDefinitionEntity>> GetDefinitionsAsync(
            bool activeOnly,
            IReadOnlySet<string>? requirementTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AchievementDefinitionEntity>>([]);

        public Task<AchievementDefinitionEntity?> GetDefinitionByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AchievementDefinitionEntity?>(null);

        public Task<AchievementDefinitionEntity?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<AchievementDefinitionEntity?>(null);

        public Task<AchievementDefinitionEntity> CreateDefinitionAsync(UpsertAchievementRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AchievementDefinitionEntity> UpdateDefinitionAsync(Guid id, UpsertAchievementRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyDictionary<Guid, AchievementUnlockEntity>> GetUnlocksAsync(Guid userId, bool includeRevoked = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AchievementUnlockEntity>>(new Dictionary<Guid, AchievementUnlockEntity>());

        public Task<AchievementUnlockEntity?> GetUnlockAsync(Guid userId, Guid definitionId, bool includeRevoked = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<AchievementUnlockEntity?>(null);

        public Task<AchievementUnlockEntity?> TryInsertUnlockAsync(Guid userId, AchievementDefinitionEntity definition, int progressValue, Guid? grantedByAdminId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AchievementUnlockEntity?>(null);

        public Task SetRewardTransactionAsync(Guid unlockId, Guid rewardTransactionId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> RevokeUnlockAsync(Guid userId, Guid definitionId, Guid adminId, string? reason, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task InsertHistoryAsync(Guid userId, Guid definitionId, string eventType, Guid? adminId, string? reason, object? metadata = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AchievementHistoryDto>> GetRecentHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AchievementHistoryDto>>([]);

        public Task<IReadOnlyList<AdminAchievementUserDto>> GetUsersForDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminAchievementUserDto>>([]);

        public Task<AdminAchievementOverviewStats> GetOverviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminAchievementOverviewStats(0, 0, 0, 0, 0));
    }
}
