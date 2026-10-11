using FluentAssertions;
using MobileLoginAuth.Application.Notifications;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.UnitTests.Helpers;

namespace MobileLoginAuth.UnitTests.Notifications;

public class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _sut = new();

    // UT-50
    [Theory]
    [InlineData(AuthEventType.LoginSucceeded, "signed in to")]
    [InlineData(AuthEventType.LoginFailed, "wrong password")]
    [InlineData(AuthEventType.Logout, "signed out")]
    public void Render_IncludesDisplayNameTimeDeviceAndIp(AuthEventType type, string expectedText)
    {
        var message = _sut.Render(AuthEvents.Create(type));

        message.To.Should().Be("jay@example.com");
        foreach (var body in new[] { message.TextBody, message.HtmlBody })
        {
            body.Should().Contain("Jay")
                .And.Contain("2026-01-15 10:30:00 UTC")
                .And.Contain("iPhone 15")
                .And.Contain("10.0.0.1")
                .And.Contain(expectedText);
        }
    }

    [Fact]
    public void Render_HtmlEncodesUserSuppliedValues()
    {
        var e = AuthEvents.Create(AuthEventType.LoginSucceeded) with { DisplayName = "<script>x</script>" };

        var message = _sut.Render(e);

        message.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
    }

    [Fact]
    public void Render_FailedLogin_AdvisesChangingPassword()
    {
        _sut.Render(AuthEvents.Create(AuthEventType.LoginFailed)).TextBody.Should().Contain("change your password");
    }
}
