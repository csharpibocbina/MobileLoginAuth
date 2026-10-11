using FluentAssertions;
using MobileLoginAuth.Application.Common;
using MobileLoginAuth.Application.Services;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.UnitTests.Helpers;

namespace MobileLoginAuth.UnitTests.Services;

public class UserServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly UserService _sut;
    private readonly User _user = new() { Email = "jay@example.com", DisplayName = "Jay", PasswordHash = "x" };

    public UserServiceTests()
    {
        _sut = new UserService(_db.Context);
        _db.Context.Users.Add(_user);
        _db.Context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Get_ReturnsProfile()
    {
        var result = await _sut.GetAsync(_user.Id);

        result.Value!.Email.Should().Be("jay@example.com");
    }

    [Fact]
    public async Task Get_UnknownUser_ReturnsNotFound()
    {
        (await _sut.GetAsync(Guid.NewGuid())).ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task LoginHistory_ReturnsNewestFirst_OnlyForThatUser()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _db.Context.LoginAudits.AddRange(
            new LoginAudit { UserId = _user.Id, EventType = AuthEventType.LoginSucceeded, OccurredAtUtc = start },
            new LoginAudit { UserId = _user.Id, EventType = AuthEventType.Logout, OccurredAtUtc = start.AddHours(1) },
            new LoginAudit { UserId = Guid.NewGuid(), EventType = AuthEventType.LoginFailed, OccurredAtUtc = start.AddHours(2) });
        await _db.Context.SaveChangesAsync();

        var history = await _sut.GetLoginHistoryAsync(_user.Id);

        history.Select(h => h.EventType).Should().Equal("Logout", "LoginSucceeded");
    }

    // FR-12 (App Store account deletion)
    [Fact]
    public async Task Delete_RemovesUserTokensAndAudits()
    {
        _db.Context.RefreshTokens.Add(new RefreshToken { UserId = _user.Id, TokenHash = "abc", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
        _db.Context.LoginAudits.Add(new LoginAudit { UserId = _user.Id, EventType = AuthEventType.LoginSucceeded });
        await _db.Context.SaveChangesAsync();

        var result = await _sut.DeleteAsync(_user.Id);

        result.Succeeded.Should().BeTrue();
        var check = _db.CreateContext();
        check.Users.Should().BeEmpty();
        check.RefreshTokens.Should().BeEmpty();
        check.LoginAudits.Should().BeEmpty();
    }
}
