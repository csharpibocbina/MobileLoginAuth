using MobileLoginAuth.Domain.Entities;

namespace MobileLoginAuth.Application.Abstractions;

public record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>Creates a cryptographically random refresh token (returned to the client, never stored raw).</summary>
    string CreateRefreshToken();

    /// <summary>SHA-256 hash of a refresh token, which is what gets stored in the database.</summary>
    string HashToken(string token);
}
