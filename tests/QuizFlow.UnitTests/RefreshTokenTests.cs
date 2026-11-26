using QuizFlow.Api.Services;

namespace QuizFlow.UnitTests;

public sealed class RefreshTokenTests
{
    [Fact]
    public void NewRefreshToken_IsHighEntropyAndUnique()
    {
        var first = JwtTokenService.NewRefreshToken();
        var second = JwtTokenService.NewRefreshToken();
        Assert.NotEqual(first, second);
        Assert.True(Convert.FromBase64String(first).Length >= 64);
    }

    [Fact]
    public void Hash_IsStableAndDoesNotRevealTheRawToken()
    {
        const string raw = "sample-refresh-token";
        var hash = JwtTokenService.Hash(raw);
        Assert.Equal(hash, JwtTokenService.Hash(raw));
        Assert.Equal(64, hash.Length);
        Assert.NotEqual(raw, hash);
    }
}
