using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Features.Preferences;
using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.UnitTests.Users;

public sealed class PreferredLanguageTests
{
    [Theory]
    [InlineData("SYSTEM", "SYSTEM")]
    [InlineData("system", "SYSTEM")]
    [InlineData("en", "en")]
    [InlineData(" ID ", "id")]
    public void Normalizes_allowed_values(string input, string expected)
    {
        PreferredLanguage.TryNormalize(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("en-US")]
    [InlineData("id'; DROP TABLE users;--")]
    public void Rejects_unsupported_values(string? input)
    {
        PreferredLanguage.TryNormalize(input, out _).Should().BeFalse();
    }
}

public sealed class PreferencesHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Stores_canonical_language_and_returns_updated_user()
    {
        var repo = new FakeUserRepository(Account());
        var handler = new PreferencesHandler(new FakeCurrentUser(UserId.ToString()), repo);

        var result = await handler.UpdateAsync(new UpdatePreferencesRequest("ID"));

        repo.Updates.Should().ContainSingle().Which.Should().Be((UserId, "id"));
        result.PreferredLanguage.Should().Be("id");
        result.Id.Should().Be(UserId.ToString());
    }

    [Fact]
    public async Task Rejects_unsupported_language_without_writing()
    {
        var repo = new FakeUserRepository(Account());
        var handler = new PreferencesHandler(new FakeCurrentUser(UserId.ToString()), repo);

        var act = () => handler.UpdateAsync(new UpdatePreferencesRequest("fr"));

        await act.Should().ThrowAsync<ValidationException>();
        repo.Updates.Should().BeEmpty();
    }

    [Fact]
    public async Task Requires_an_authenticated_active_user()
    {
        var anonymous = new PreferencesHandler(new FakeCurrentUser(null), new FakeUserRepository(Account()));
        var suspendedRepo = new FakeUserRepository(Account(status: "suspended"));
        var suspended = new PreferencesHandler(new FakeCurrentUser(UserId.ToString()), suspendedRepo);

        await anonymous.Invoking(h => h.UpdateAsync(new UpdatePreferencesRequest("en")))
            .Should().ThrowAsync<UnauthorizedException>();
        await suspended.Invoking(h => h.UpdateAsync(new UpdatePreferencesRequest("en")))
            .Should().ThrowAsync<UnauthorizedException>();
        suspendedRepo.Updates.Should().BeEmpty();
    }

    [Fact]
    public void Current_user_response_exposes_preferred_language()
    {
        UserResponseMapper.From(Account()).PreferredLanguage.Should().Be(PreferredLanguage.System);
    }

    private static UserAccount Account(string status = "active", string language = PreferredLanguage.System) => new()
    {
        Id = UserId,
        Email = "player@example.com",
        Status = status,
        PreferredLanguage = language,
        Roles = ["Player"]
    };

    private sealed class FakeCurrentUser(string? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;
        public string? UserId => userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
    }

    private sealed class FakeUserRepository(UserAccount account) : IUserRepository
    {
        private UserAccount _account = account;

        public List<(Guid, string)> Updates { get; } = [];

        public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == _account.Id ? _account : null);

        public Task UpdatePreferredLanguageAsync(Guid id, string language, CancellationToken cancellationToken = default)
        {
            Updates.Add((id, language));
            _account = new UserAccount
            {
                Id = _account.Id,
                Email = _account.Email,
                Status = _account.Status,
                PreferredLanguage = language,
                Roles = _account.Roles
            };
            return Task.CompletedTask;
        }

        public Task<(UserAccount User, string PasswordHash)?> GetByEmailWithPasswordAsync(
            string email,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CreateUserAsync(
            Guid id,
            string email,
            string passwordHash,
            string? displayName,
            IEnumerable<string> roles,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> UpdateAvatarUrlAsync(Guid id, string? avatarUrl, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProfileUpdateStatus> UpdateProfileAsync(
            Guid id,
            string displayName,
            string username,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
