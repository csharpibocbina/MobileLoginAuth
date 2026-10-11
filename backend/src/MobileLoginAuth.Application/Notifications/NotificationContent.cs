using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;

namespace MobileLoginAuth.Application.Notifications;

/// <summary>Titles and short messages shared by email subjects and SignalR notifications.</summary>
public static class NotificationContent
{
    public static string Title(AuthEventType type) => type switch
    {
        AuthEventType.LoginSucceeded => "New sign-in to your account",
        AuthEventType.LoginFailed => "Failed sign-in attempt",
        AuthEventType.Logout => "You signed out",
        _ => "Account activity"
    };

    public static string Message(AuthEvent e)
    {
        var device = string.IsNullOrWhiteSpace(e.DeviceInfo) ? "an unknown device" : e.DeviceInfo;
        var when = e.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");
        return e.Type switch
        {
            AuthEventType.LoginSucceeded => $"You signed in from {device} at {when}.",
            AuthEventType.LoginFailed => $"Someone tried to sign in with a wrong password from {device} at {when}.",
            AuthEventType.Logout => $"You signed out from {device} at {when}.",
            _ => $"Account activity at {when}."
        };
    }

    public static NotificationDto ToNotification(AuthEvent e) =>
        new(e.EventId, e.Type.ToString(), Title(e.Type), Message(e), e.OccurredAtUtc);
}
