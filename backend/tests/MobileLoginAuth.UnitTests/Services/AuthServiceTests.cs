using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Application.Common;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Application.Services;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;
using MobileLoginAuth.Infrastructure.Auth;
using MobileLoginAuth.UnitTests.Helpers;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MobileLoginAuth.UnitTests.Services;

public class AuthServiceTests : IDisposable
{
    private const string Email = "jay@example.com";
    private const string Password = "Passw0rd!";

    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Mock<IAuthEventPublisher> _publisher = new();
    private readonly List<AuthEvent> _published = new();
    private readonly TokenService _tokens;
    private readonly AuthService _sut;
    private readonly ClientInfo _client = new("10.0.0.1", "iPhone 15");

    public AuthServiceTests()
    {
        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<AuthEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AuthEvent, CancellationToken>((e, _) => _published.Add(e))
            .Returns(Task.CompletedTask);

        _tokens = new TokenService(MsOptions.Create(TestOptions.Jwt()), _time);
        _sut = new AuthService(
            _db.Context,
            new PasswordHasher<User>(),
            _tokens,
            _publisher.Object,
            _time,
            MsOptions.Create(TestOptions.Jwt()),
            MsOptions.Create(TestOptions.Lockout()));
    }

    public void Dispose() => _db.Dispose();

    private async Task<UserDto> RegisterAsync() =>
        (await _sut.RegisterAsync(new RegisterRequest(Email, Password, "Jay"))).Value!;

    private Task<Result<AuthResponse>> LoginAsync(string password = Password, string email = Email) =>
        _sut.LoginAsync(new LoginRequest(email, password), _client);

    private User SavedUser() => _db.CreateContext().Users.Single(u => u.Email == Email);

    // UT-01
    [Fact]
    public async Task Register_WithNewEmail_CreatesUserWithHashedPassword()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest("  Jay@Example.com ", Password, "Jay"));

        result.Succeeded.Should().BeTrue();
        var user = SavedUser();
        user.Email.Should().Be(Email);
        user.PasswordHash.Should().NotBeNullOrEmpty().And.NotBe(Password);
    }

    // UT-02
    [Fact]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        await RegisterAsync();

        var result = await _sut.RegisterAsync(new RegisterRequest(Email.ToUpperInvariant(), Password, "Other"));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
        _db.CreateContext().Users.Count().Should().Be(1);
    }

    // UT-03
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        await RegisterAsync();

        var result = await LoginAsync();

        result.Succeeded.Should().BeTrue();
        result.Value!.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.ExpiresAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddMinutes(15));
        result.Value.User.Email.Should().Be(Email);
    }

    // UT-04
    [Fact]
    public async Task Login_WithValidCredentials_PublishesLoginSucceededEvent()
    {
        var user = await RegisterAsync();

        await LoginAsync();

        _published.Should().ContainSingle();
        var e = _published[0];
        e.Type.Should().Be(AuthEventType.LoginSucceeded);
        e.UserId.Should().Be(user.Id);
        e.Email.Should().Be(Email);
        e.IpAddress.Should().Be("10.0.0.1");
        e.DeviceInfo.Should().Be("iPhone 15");
    }

    // UT-05
    [Fact]
    public async Task Login_WithValidCredentials_ResetsFailedLoginCount()
    {
        await RegisterAsync();
        await LoginAsync("wrong-1");
        await LoginAsync("wrong-2");

        await LoginAsync();

        SavedUser().FailedLoginCount.Should().Be(0);
    }

    // UT-06
    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorizedWithGenericMessage()
    {
        await RegisterAsync();

        var result = await LoginAsync("wrong");

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be(AuthService.InvalidCredentialsMessage);
    }

    // UT-07
    [Fact]
    public async Task Login_WithWrongPassword_PublishesLoginFailedEvent()
    {
        await RegisterAsync();

        await LoginAsync("wrong");

        _published.Should().ContainSingle().Which.Type.Should().Be(AuthEventType.LoginFailed);
    }

    // UT-08
    [Fact]
    public async Task Login_WithWrongPassword_IncrementsFailedLoginCount()
    {
        await RegisterAsync();

        await LoginAsync("wrong");

        SavedUser().FailedLoginCount.Should().Be(1);
    }

    // UT-09
    [Fact]
    public async Task Login_WithUnknownEmail_DoesNotPublishEventButWritesAudit()
    {
        var result = await LoginAsync(email: "nobody@example.com");

        result.Error.Should().Be(AuthService.InvalidCredentialsMessage);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<AuthEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        var audit = _db.CreateContext().LoginAudits.Single();
        audit.UserId.Should().BeNull();
        audit.EventType.Should().Be(AuthEventType.LoginFailed);
    }

    // UT-10
    [Fact]
    public async Task Login_AfterFiveFailures_LocksAccount()
    {
        await RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync("wrong");
        }

        SavedUser().LockoutEndUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddMinutes(15));

        var withCorrectPassword = await LoginAsync();
        withCorrectPassword.Succeeded.Should().BeFalse();
        withCorrectPassword.ErrorType.Should().Be(ErrorType.Locked);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_Succeeds()
    {
        await RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync("wrong");
        }

        _time.Advance(TimeSpan.FromMinutes(16));

        (await LoginAsync()).Succeeded.Should().BeTrue();
    }

    // UT-11
    [Fact]
    public async Task Login_WritesLoginAudit_ForEveryAttempt()
    {
        await RegisterAsync();

        await LoginAsync("wrong");
        await LoginAsync();

        var audits = _db.CreateContext().LoginAudits.OrderBy(a => a.OccurredAtUtc).ThenBy(a => a.EventType).ToList();
        audits.Select(a => a.EventType).Should().BeEquivalentTo(new[] { AuthEventType.LoginSucceeded, AuthEventType.LoginFailed });
        audits.Should().OnlyContain(a => a.IpAddress == "10.0.0.1" && a.DeviceInfo == "iPhone 15");
    }

    // UT-12
    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var user = await RegisterAsync();
        var login = (await LoginAsync()).Value!;

        var result = await _sut.LogoutAsync(user.Id, new LogoutRequest(login.RefreshToken), _client);

        result.Succeeded.Should().BeTrue();
        _db.CreateContext().RefreshTokens.Single().RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Logout_WithoutRefreshToken_RevokesAllSessions()
    {
        var user = await RegisterAsync();
        await LoginAsync();
        await LoginAsync();

        await _sut.LogoutAsync(user.Id, new LogoutRequest(null), _client);

        _db.CreateContext().RefreshTokens.ToList().Should().HaveCount(2).And.OnlyContain(t => t.RevokedAtUtc != null);
    }

    // UT-13
    [Fact]
    public async Task Logout_PublishesLogoutEvent()
    {
        var user = await RegisterAsync();
        var login = (await LoginAsync()).Value!;
        _published.Clear();

        await _sut.LogoutAsync(user.Id, new LogoutRequest(login.RefreshToken), _client);

        _published.Should().ContainSingle().Which.Type.Should().Be(AuthEventType.Logout);
    }

    [Fact]
    public async Task Logout_ForUnknownUser_ReturnsNotFound()
    {
        var result = await _sut.LogoutAsync(Guid.NewGuid(), new LogoutRequest(null), _client);

        result.ErrorType.Should().Be(ErrorType.NotFound);
        _published.Should().BeEmpty();
    }

    // UT-14
    [Fact]
    public async Task Refresh_WithValidToken_RotatesToken()
    {
        await RegisterAsync();
        var login = (await LoginAsync()).Value!;

        var result = await _sut.RefreshAsync(new RefreshRequest(login.RefreshToken), _client);

        result.Succeeded.Should().BeTrue();
        result.Value!.RefreshToken.Should().NotBe(login.RefreshToken);
        var tokens = _db.CreateContext().RefreshTokens.ToList();
        var old = tokens.Single(t => t.TokenHash == _tokens.HashToken(login.RefreshToken));
        old.RevokedAtUtc.Should().NotBeNull();
        old.ReplacedByTokenHash.Should().Be(_tokens.HashToken(result.Value.RefreshToken));
    }

    // UT-15
    [Fact]
    public async Task Refresh_WithRevokedToken_ReturnsUnauthorized()
    {
        await RegisterAsync();
        var login = (await LoginAsync()).Value!;
        await _sut.RefreshAsync(new RefreshRequest(login.RefreshToken), _client);

        var reuse = await _sut.RefreshAsync(new RefreshRequest(login.RefreshToken), _client);

        reuse.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    // UT-16
    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsUnauthorized()
    {
        await RegisterAsync();
        var login = (await LoginAsync()).Value!;
        _time.Advance(TimeSpan.FromDays(8));

        var result = await _sut.RefreshAsync(new RefreshRequest(login.RefreshToken), _client);

        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsUnauthorized()
    {
        var result = await _sut.RefreshAsync(new RefreshRequest("not-a-real-token"), _client);

        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task RefreshTokens_AreStoredHashedNotRaw()
    {
        await RegisterAsync();
        var login = (await LoginAsync()).Value!;

        var stored = await _db.CreateContext().RefreshTokens.SingleAsync();

        stored.TokenHash.Should().NotBe(login.RefreshToken).And.Be(_tokens.HashToken(login.RefreshToken));
    }
}
