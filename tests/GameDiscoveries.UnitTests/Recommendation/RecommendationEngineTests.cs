using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Data;
using GameDiscoveries.Modules.Recommendation.Domain;
using GameDiscoveries.Modules.Recommendation.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.UnitTests.Recommendation;

public sealed class RecommendationEngineTests
{
    [Fact]
    public async Task Cold_Start_Returns_Non_Empty_With_Version()
    {
        var repo = new FakeRecommendationRepository();
        var cache = new RecommendationCache(new InMemoryCache(), Options.Create(new RecommendationOptions()));
        var engine = new RecommendationEngine(
            repo,
            cache,
            Options.Create(new RecommendationOptions()),
            NullLogger<RecommendationEngine>.Instance);

        var result = await engine.GetAsync(RecommendationType.ForYou, userId: null, seedGameId: null, limit: 8);
        result.Items.Should().NotBeEmpty();
        result.AlgorithmVersion.Should().Be("v1");
        result.Type.Should().Be("for-you");
        result.Items.Select(i => i.Game.Id).Should().OnlyHaveUniqueItems();
        result.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Reason));
    }

    [Fact]
    public async Task Cache_Is_User_Isolated()
    {
        var options = Options.Create(new RecommendationOptions());
        var cache = new RecommendationCache(new InMemoryCache(), options);
        var keyA = cache.BuildKey(RecommendationType.ForYou, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), null, 20);
        var keyB = cache.BuildKey(RecommendationType.ForYou, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), null, 20);
        keyA.Should().NotBe(keyB);
        keyA.Should().Contain("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
    }

    [Fact]
    public async Task Similar_Games_Exclude_Seed()
    {
        var repo = new FakeRecommendationRepository();
        var seedId = repo.Seed.Id;
        var engine = new RecommendationEngine(
            repo,
            new RecommendationCache(new InMemoryCache(), Options.Create(new RecommendationOptions())),
            Options.Create(new RecommendationOptions()),
            NullLogger<RecommendationEngine>.Instance);

        var result = await engine.GetAsync(RecommendationType.SimilarGames, null, seedId, 10);
        result.Items.Should().NotContain(i => i.Game.Id == seedId);
    }

    private sealed class InMemoryCache : ICacheService
    {
        private readonly Dictionary<string, object> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(key, out var value) ? (T?)value : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            _store[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.ContainsKey(key));
    }

    private sealed class FakeRecommendationRepository : IRecommendationRepository
    {
        public CandidateGame Seed { get; } = new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Slug = "seed-racer",
            Title = "Seed Racer",
            Category = "Racing",
            Tags = ["cars"],
            Orientation = "landscape",
            MobileReady = true,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-20),
            PopularityProxy = 3
        };

        private List<CandidateGame> Catalog()
        {
            var list = new List<CandidateGame> { Seed };
            for (var i = 0; i < 30; i++)
            {
                list.Add(new CandidateGame
                {
                    Id = Guid.Parse($"22222222-2222-2222-2222-{i:D12}"),
                    Slug = $"game-{i}",
                    Title = $"Game {i}",
                    Category = i % 2 == 0 ? "Racing" : "Action",
                    Tags = i % 2 == 0 ? ["cars"] : ["fight"],
                    Orientation = "landscape",
                    MobileReady = i % 3 == 0,
                    PublishedAt = DateTimeOffset.UtcNow.AddDays(-i),
                    PopularityProxy = i % 5,
                    EngagementProxy = i * 10,
                    SourceBucket = i % 7 == 0 ? "hidden" : "catalog"
                });
            }

            return list;
        }

        public Task<UserPreferenceProfile> BuildProfileAsync(Guid? userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new UserPreferenceProfile { UserId = userId });

        public Task<CandidateGame?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default)
            => Task.FromResult(Catalog().FirstOrDefault(g => g.Id == gameId));

        public Task<IReadOnlyList<CandidateGame>> GetCandidatesAsync(int limit, IEnumerable<Guid>? excludeIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Filter(Catalog(), excludeIds).Take(limit).ToList() as IReadOnlyList<CandidateGame>);

        public Task<IReadOnlyList<CandidateGame>> GetByFeedAsync(string feedType, int limit, IEnumerable<Guid>? excludeIds = null, CancellationToken cancellationToken = default)
            => GetCandidatesAsync(limit, excludeIds, cancellationToken);

        public Task<IReadOnlyList<CandidateGame>> GetNewestAsync(int limit, IEnumerable<Guid>? excludeIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Filter(Catalog().OrderByDescending(g => g.PublishedAt), excludeIds).Take(limit).ToList() as IReadOnlyList<CandidateGame>);

        public Task<IReadOnlyList<CandidateGame>> GetTrendingAsync(int limit, IEnumerable<Guid>? excludeIds = null, CancellationToken cancellationToken = default)
            => GetNewestAsync(limit, excludeIds, cancellationToken);

        public Task<IReadOnlyList<CandidateGame>> GetHiddenGemCandidatesAsync(int limit, IEnumerable<Guid>? excludeIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Filter(Catalog().Where(g => g.SourceBucket == "hidden"), excludeIds).Take(limit).ToList() as IReadOnlyList<CandidateGame>);

        public Task<IReadOnlyList<CandidateGame>> GetSimilarCandidatesAsync(CandidateGame seed, int limit, IEnumerable<Guid>? excludeIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Filter(
                    Catalog().Where(g => g.Id != seed.Id && (g.Category == seed.Category || g.Tags.Intersect(seed.Tags).Any())),
                    excludeIds).Take(limit).ToList() as IReadOnlyList<CandidateGame>);

        private static IEnumerable<CandidateGame> Filter(IEnumerable<CandidateGame> source, IEnumerable<Guid>? excludeIds)
        {
            var set = excludeIds?.ToHashSet() ?? [];
            return source.Where(g => !set.Contains(g.Id));
        }
    }
}
