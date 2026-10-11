using System.Net;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;

namespace MobileLoginAuth.Application.Notifications;

public interface IEmailTemplateRenderer
{
    EmailMessage Render(AuthEvent authEvent);
}

public class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public EmailMessage Render(AuthEvent e)
    {
        var title = NotificationContent.Title(e.Type);
        var name = string.IsNullOrWhiteSpace(e.DisplayName) ? e.Email : e.DisplayName;
        var device = string.IsNullOrWhiteSpace(e.DeviceInfo) ? "Unknown device" : e.DeviceInfo;
        var ip = string.IsNullOrWhiteSpace(e.IpAddress) ? "Unknown" : e.IpAddress;
        var when = e.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

        var intro = e.Type switch
        {
            AuthEventType.LoginSucceeded => "Your account was just signed in to.",
            AuthEventType.LoginFailed => "Someone just tried to sign in to your account with a wrong password.",
            AuthEventType.Logout => "Your account was just signed out.",
            _ => "There was activity on your account."
        };

        var advice = e.Type == AuthEventType.Logout
            ? "No action is needed."
            : "If this wasn't you, change your password right away.";

        var text = $"Hi {name},\n\n{intro}\n\nTime: {when}\nDevice: {device}\nIP address: {ip}\n\n{advice}\n\n— MobileLoginAuth";

        string H(string s) => WebUtility.HtmlEncode(s);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:520px">
              <h2>{H(title)}</h2>
              <p>Hi {H(name)},</p>
              <p>{H(intro)}</p>
              <table cellpadding="4">
                <tr><td><b>Time</b></td><td>{H(when)}</td></tr>
                <tr><td><b>Device</b></td><td>{H(device)}</td></tr>
                <tr><td><b>IP address</b></td><td>{H(ip)}</td></tr>
              </table>
              <p>{H(advice)}</p>
              <p style="color:#888">— MobileLoginAuth</p>
            </div>
            """;

        return new EmailMessage(e.Email, name, title, html, text);
    }
}
