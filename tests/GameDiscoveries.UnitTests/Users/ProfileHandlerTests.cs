using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Features.Profile;
using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.UnitTests.Users;

public sealed class ProfileRulesTests
{
    [Theory]
    [InlineData("Ada")]
    [InlineData("Rudi Gamer")]
    [InlineData("Nama-40-karakter-yang-masih-diizinkan!")]
    public void Accepts_display_names(string value)
    {
        ProfileRules.IsValidDisplayName(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("this display name is definitely longer than forty")]
    [InlineData("line\nbreak")]
    public void Rejects_display_names(string value)
    {
        ProfileRules.IsValidDisplayName(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("ada")]
    [InlineData("rudi-gamer")]
    [InlineData("a1b")]
    public void Accepts_usernames(string value)
    {
        ProfileRules.IsValidUsername(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Rudi")]
    [InlineData("-rudi")]
    [InlineData("rudi-")]
    [InlineData("rudi--gamer")]
    [InlineData("rudi_gamer")]
    [InlineData("this-username-is-longer-than-thirty")]
    public void Rejects_usernames(string value)
    {
        ProfileRules.IsValidUsername(value).Should().BeFalse();
    }
}

public sealed class ProfileHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Stores_trimmed_name_and_lowercase_username()
    {
        var repo = new FakeUserRepository(Account());
        var handler = new ProfileHandler(new FakeCurrentUser(UserId.ToString()), repo);

        var result = await handler.UpdateAsync(new UpdateProfileRequest("  Rudi Baru  ", "Rudi-Baru"));

        repo.Updates.Should().ContainSingle().Which.Should().Be((UserId, "Rudi Baru", "rudi-baru"));
        result.DisplayName.Should().Be("Rudi Baru");
        result.Username.Should().Be("rudi-baru");
        result.Email.Should().Be("player@example.com");
    }

    [Fact]
    public async Task Leaves_an_unchanged_profile_unwritten()
    {
        var repo = new FakeUserRepository(Account());
        var handler = new ProfileHandler(new FakeCurrentUser(UserId.ToString()), repo);

        var result = await handler.UpdateAsync(new UpdateProfileRequest("Rudi", "RUDI"));

        repo.Updates.Should().BeEmpty();
        result.Username.Should().Be("rudi");
    }

    [Fact]
    public async Task Keeps_a_legacy_username_when_only_the_name_changes()
    {
        var repo = new FakeUserRepository(Account(username: "rudi_gamer"));
        var handler = new ProfileHandler(new FakeCurrentUser(UserId.ToString()), repo);

        var result = await handler.UpdateAsync(new UpdateProfileRequest("Rudi Baru", "rudi_gamer"));

        repo.Updates.Should().ContainSingle().Which.Username.Should().Be("rudi_gamer");
        result.DisplayName.Should().Be("Rudi Baru");
    }

    [Fact]
    public async Task Rejects_invalid_fields_without_writing()
    {
        var repo = new FakeUserRepository(Account());
        var handler = new ProfileHandler(new FakeCurrentUser(UserId.ToString()), repo);

        var act = () => handler.UpdateAsync(new UpdateProfileRequest("A", "Rudi Gamer"));

        var error = (await act.Should().ThrowAsync<ValidationException>()).Which;
        error.Errors.Keys.Should().BeEquivalentTo("displayName", "username");
        repo.Updates.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_a_taken_username()
    {
        var repo = new FakeUserRepository(Account()) { Taken = true };
        var handler = new ProfileHandler(new FakeCurrentUser(UserId.ToString()), repo);

        var act = () => handler.UpdateAsync(new UpdateProfileRequest("Rudi", "ada"));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Requires_an_authenticated_active_user()
    {
        var anonymous = new ProfileHandler(new FakeCurrentUser(null), new FakeUserRepository(Account()));
        var suspendedRepo = new FakeUserRepository(Account(status: "suspended"));
        var suspended = new ProfileHandler(new FakeCurrentUser(UserId.ToString()), suspendedRepo);

        await anonymous.Invoking(h => h.UpdateAsync(new UpdateProfileRequest("Rudi", "rudi")))
            .Should().ThrowAsync<UnauthorizedException>();
        await suspended.Invoking(h => h.UpdateAsync(new UpdateProfileRequest("Rudi", "rudi")))
            .Should().ThrowAsync<UnauthorizedException>();
        suspendedRepo.Updates.Should().BeEmpty();
    }

    private static UserAccount Account(string status = "active", string username = "rudi") => new()
    {
        Id = UserId,
        Email = "player@example.com",
        DisplayName = "Rudi",
        Username = username,
        Status = status,
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

        public bool Taken { get; init; }

        public List<(Guid Id, string DisplayName, string Username)> Updates { get; } = [];

        public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == _account.Id ? _account : null);

        public Task<ProfileUpdateStatus> UpdateProfileAsync(
            Guid id,
            string displayName,
            string username,
            CancellationToken cancellationToken = default)
        {
            if (Taken)
            {
                return Task.FromResult(ProfileUpdateStatus.UsernameTaken);
            }

            Updates.Add((id, displayName, username));
            _account = new UserAccount
            {
                Id = _account.Id,
                Email = _account.Email,
                DisplayName = displayName,
                Username = username,
                Status = _account.Status,
                PreferredLanguage = _account.PreferredLanguage,
                Roles = _account.Roles
            };
            return Task.FromResult(ProfileUpdateStatus.Updated);
        }

        public Task UpdatePreferredLanguageAsync(Guid id, string language, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
    }
}
