using MassTransit;
using Microsoft.AspNetCore.SignalR;
using MobileLoginAuth.Api.Hubs;
using MobileLoginAuth.Application.Notifications;
using MobileLoginAuth.Domain.Events;
using MobileLoginAuth.Infrastructure.Messaging;

namespace MobileLoginAuth.Api.Notifications;

public class SignalRNotificationConsumer : IConsumer<AuthEvent>
{
    public const string Name = "signalr";

    private readonly IHubContext<NotificationHub> _hub;
    private readonly IProcessedEventGuard _guard;

    public SignalRNotificationConsumer(IHubContext<NotificationHub> hub, IProcessedEventGuard guard)
    {
        _hub = hub;
        _guard = guard;
    }

    public async Task Consume(ConsumeContext<AuthEvent> context)
    {
        var e = context.Message;
        if (await _guard.AlreadyProcessedAsync(e.EventId, Name, context.CancellationToken))
        {
            return;
        }

        await _hub.Clients.User(e.UserId.ToString())
            .SendAsync(NotificationHub.ReceiveNotification, NotificationContent.ToNotification(e), context.CancellationToken);
        await _guard.MarkProcessedAsync(e.EventId, Name, context.CancellationToken);
    }
}

public class SignalRNotificationConsumerDefinition : ConsumerDefinition<SignalRNotificationConsumer>
{
    public SignalRNotificationConsumerDefinition() => EndpointName = "auth-events-signalr";

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SignalRNotificationConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));
    }
}
