using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuizFlow.Infrastructure.Identity;

namespace QuizFlow.Api.Services;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "QuizFlow";
    public string Audience { get; init; } = "QuizFlow.Web";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, UserManager<AppUser> users)
{
    private readonly JwtOptions _options = options.Value;

    public async Task<(string AccessToken, DateTimeOffset ExpiresAt)> CreateAccessTokenAsync(AppUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id.ToString())
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims,
            now.UtcDateTime, now.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), now.AddMinutes(_options.AccessTokenMinutes));
    }

    public static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
