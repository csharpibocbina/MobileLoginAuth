using MobileLoginAuth.Application.Options;

namespace MobileLoginAuth.UnitTests.Helpers;

public static class TestOptions
{
    public const string SigningKey = "unit-test-signing-key-0123456789abcdef-XYZ";

    public static JwtOptions Jwt() => new()
    {
        Issuer = "MobileLoginAuth",
        Audience = "MobileLoginAuth.Mobile",
        SigningKey = SigningKey,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    public static LockoutOptions Lockout() => new() { MaxFailedAttempts = 5, LockoutMinutes = 15 };
}
