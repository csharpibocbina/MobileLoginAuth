using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Infrastructure.Auth;
using MobileLoginAuth.UnitTests.Helpers;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MobileLoginAuth.UnitTests.Auth;

public class TokenServiceTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly TokenService _sut;
    private readonly User _user = new() { Email = "jay@example.com", DisplayName = "Jay" };

    public TokenServiceTests() => _sut = new TokenService(MsOptions.Create(TestOptions.Jwt()), _time);

    private static TokenValidationParameters Validation(string key) => new()
    {
        ValidIssuer = "MobileLoginAuth",
        ValidAudience = "MobileLoginAuth.Mobile",
        IssuerSigningKey = TokenService.CreateSigningKey(key),
        ValidateLifetime = true
    };

    // UT-20
    [Fact]
    public void CreateAccessToken_ContainsSubAndEmailClaims()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(_sut.CreateAccessToken(_user).Token);

        token.Subject.Should().Be(_user.Id.ToString());
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "jay@example.com");
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == "Jay");
    }

    // UT-21
    [Fact]
    public void CreateAccessToken_ExpiresAfterConfiguredMinutes()
    {
        var result = _sut.CreateAccessToken(_user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        var expected = _time.GetUtcNow().UtcDateTime.AddMinutes(15);
        result.ExpiresAtUtc.Should().Be(expected);
        token.ValidTo.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    // UT-22
    [Fact]
    public void CreateAccessToken_IsValidOnlyWithConfiguredKey()
    {
        var token = _sut.CreateAccessToken(_user).Token;
        var handler = new JwtSecurityTokenHandler();

        var valid = () => handler.ValidateToken(token, Validation(TestOptions.SigningKey), out _);
        var wrongKey = () => handler.ValidateToken(token, Validation("a-completely-different-signing-key-0123456789"), out _);

        valid.Should().NotThrow();
        wrongKey.Should().Throw<SecurityTokenInvalidSignatureException>();
    }

    // UT-23
    [Fact]
    public void CreateRefreshToken_IsRandomAndHashed()
    {
        var a = _sut.CreateRefreshToken();
        var b = _sut.CreateRefreshToken();

        a.Should().NotBe(b);
        a.Length.Should().BeGreaterThan(60);
        _sut.HashToken(a).Should().NotBe(a).And.HaveLength(64).And.Be(_sut.HashToken(a));
    }

    [Fact]
    public void CreateSigningKey_RejectsShortKeys()
    {
        var act = () => TokenService.CreateSigningKey("too-short");

        act.Should().Throw<InvalidOperationException>();
    }
}
