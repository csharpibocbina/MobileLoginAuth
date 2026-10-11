using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MobileLoginAuth.Api.Hubs;

/// <summary>Real-time channel to the mobile app. Clients listen for "ReceiveNotification".</summary>
[Authorize]
public class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string ReceiveNotification = "ReceiveNotification";
}
