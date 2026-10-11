using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Application.Common;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Application.Options;
using LockoutOptions = MobileLoginAuth.Application.Options.LockoutOptions;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;

namespace MobileLoginAuth.Application.Services;

public class AuthService : IAuthService
{
    public const string InvalidCredentialsMessage = "Invalid email or password.";
    public const string LockedOutMessage = "Too many failed attempts. Please try again later.";

    private readonly IAppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuthEventPublisher _publisher;
    private readonly TimeProvider _time;
    private readonly JwtOptions _jwtOptions;
    private readonly LockoutOptions _lockoutOptions;

    public AuthService(
        IAppDbContext db,
        IPasswordHasher<User> passwordHasher,
        ITokenService tokenService,
        IAuthEventPublisher publisher,
        TimeProvider time,
        IOptions<JwtOptions> jwtOptions,
        IOptions<LockoutOptions> lockoutOptions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _publisher = publisher;
        _time = time;
        _jwtOptions = jwtOptions.Value;
        _lockoutOptions = lockoutOptions.Value;
    }

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<Result<UserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Result<UserDto>.Failure(ErrorType.Conflict, "An account with this email already exists.");
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAtUtc = UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<UserDto>.Success(ToDto(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var now = UtcNow;
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Unknown email: audit only. No email is sent so non-users are never contacted.
            AddAudit(null, email, AuthEventType.LoginFailed, client, now);
            await _db.SaveChangesAsync(cancellationToken);
            return Result<AuthResponse>.Failure(ErrorType.Unauthorized, InvalidCredentialsMessage);
        }

        if (user.IsLockedOut(now))
        {
            // The attempts that caused the lockout already notified the user, so this one is only audited.
            AddAudit(user.Id, email, AuthEventType.LoginFailed, client, now);
            await _db.SaveChangesAsync(cancellationToken);
            return Result<AuthResponse>.Failure(ErrorType.Locked, LockedOutMessage);
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _lockoutOptions.MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.AddMinutes(_lockoutOptions.LockoutMinutes);
                user.FailedLoginCount = 0;
            }

            AddAudit(user.Id, email, AuthEventType.LoginFailed, client, now);
            await _db.SaveChangesAsync(cancellationToken);
            await PublishAsync(AuthEventType.LoginFailed, user, client, now, cancellationToken);
            return Result<AuthResponse>.Failure(ErrorType.Unauthorized, InvalidCredentialsMessage);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;

        var response = IssueTokens(user, client, now);
        AddAudit(user.Id, email, AuthEventType.LoginSucceeded, client, now);
        await _db.SaveChangesAsync(cancellationToken);
        await PublishAsync(AuthEventType.LoginSucceeded, user, client, now, cancellationToken);

        return Result<AuthResponse>.Success(response);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, ClientInfo client, CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var hash = _tokenService.HashToken(request.RefreshToken);
        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing?.User is null || !existing.IsActive(now))
        {
            return Result<AuthResponse>.Failure(ErrorType.Unauthorized, "Invalid or expired refresh token.");
        }

        var response = IssueTokens(existing.User, client, now);
        existing.RevokedAtUtc = now;
        existing.ReplacedByTokenHash = _tokenService.HashToken(response.RefreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Success(response);
    }

    public async Task<Result> LogoutAsync(Guid userId, LogoutRequest request, ClientInfo client, CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(ErrorType.NotFound, "User not found.");
        }

        var tokens = _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            // Sign out this device only.
            var hash = _tokenService.HashToken(request.RefreshToken);
            tokens = tokens.Where(t => t.TokenHash == hash);
        }

        foreach (var token in await tokens.ToListAsync(cancellationToken))
        {
            token.RevokedAtUtc = now;
        }

        AddAudit(user.Id, user.Email, AuthEventType.Logout, client, now);
        await _db.SaveChangesAsync(cancellationToken);
        await PublishAsync(AuthEventType.Logout, user, client, now, cancellationToken);

        return Result.Success();
    }

    private AuthResponse IssueTokens(User user, ClientInfo client, DateTime now)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = _tokenService.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwtOptions.RefreshTokenDays),
            DeviceInfo = client.DeviceInfo
        });

        return new AuthResponse(accessToken.Token, refreshToken, accessToken.ExpiresAtUtc, ToDto(user));
    }

    private void AddAudit(Guid? userId, string email, AuthEventType type, ClientInfo client, DateTime now) =>
        _db.LoginAudits.Add(new LoginAudit
        {
            UserId = userId,
            Email = email,
            EventType = type,
            IpAddress = client.IpAddress,
            DeviceInfo = client.DeviceInfo,
            OccurredAtUtc = now
        });

    private Task PublishAsync(AuthEventType type, User user, ClientInfo client, DateTime now, CancellationToken cancellationToken) =>
        _publisher.PublishAsync(
            new AuthEvent(Guid.NewGuid(), type, user.Id, user.Email, user.DisplayName, client.IpAddress, client.DeviceInfo, now),
            cancellationToken);

    public static UserDto ToDto(User user) => new(user.Id, user.Email, user.DisplayName, user.CreatedAtUtc);
}
