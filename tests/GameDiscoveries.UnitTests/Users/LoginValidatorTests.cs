using FluentAssertions;
using GameDiscoveries.Modules.Users.Features.Login;

namespace GameDiscoveries.UnitTests.Users;

public sealed class LoginValidatorTests
{
    private readonly LoginValidator _validator = new();

    [Fact]
    public async Task Rejects_empty_email()
    {
        var result = await _validator.ValidateAsync(new LoginCommand("", "password123"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task Rejects_invalid_email()
    {
        var result = await _validator.ValidateAsync(new LoginCommand("not-an-email", "password123"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_empty_password()
    {
        var result = await _validator.ValidateAsync(
            new LoginCommand("demo@gamediscoveries.com", ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Accepts_valid_credentials_format()
    {
        var result = await _validator.ValidateAsync(
            new LoginCommand("demo@gamediscoveries.com", "GameDiscoveries!Demo1"));
        result.IsValid.Should().BeTrue();
    }
}
