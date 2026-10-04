using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.Modules.Users.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GameDiscoveries.Modules.Users.Services;

public sealed class JwtTokenService(IOptions<AuthenticationOptions> options) : IJwtTokenService
{
    public (string AccessToken, int ExpiresInSeconds) CreateAccessToken(UserAccount user)
    {
        var auth = options.Value;
        if (string.IsNullOrWhiteSpace(auth.SigningKey))
        {
            throw new InvalidOperationException(
                "Authentication:SigningKey is required to issue local access tokens.");
        }

        var expiresInSeconds = Math.Max(60, auth.AccessTokenMinutes * 60);
        var expiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.DisplayName));
        }

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: auth.Issuer,
            audience: auth.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresInSeconds);
    }
}
