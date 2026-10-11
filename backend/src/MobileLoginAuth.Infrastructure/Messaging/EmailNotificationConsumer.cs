using MassTransit;
using Microsoft.Extensions.Logging;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Application.Notifications;
using MobileLoginAuth.Domain.Events;

namespace MobileLoginAuth.Infrastructure.Messaging;

public class EmailNotificationConsumer : IConsumer<AuthEvent>
{
    public const string Name = "email";

    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailSender _sender;
    private readonly IProcessedEventGuard _guard;
    private readonly ILogger<EmailNotificationConsumer> _logger;

    public EmailNotificationConsumer(
        IEmailTemplateRenderer renderer,
        IEmailSender sender,
        IProcessedEventGuard guard,
        ILogger<EmailNotificationConsumer> logger)
    {
        _renderer = renderer;
        _sender = sender;
        _guard = guard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuthEvent> context)
    {
        var e = context.Message;
        if (await _guard.AlreadyProcessedAsync(e.EventId, Name, context.CancellationToken))
        {
            _logger.LogInformation("Skipping duplicate {EventType} event {EventId}", e.Type, e.EventId);
            return;
        }

        await _sender.SendAsync(_renderer.Render(e), context.CancellationToken);
        await _guard.MarkProcessedAsync(e.EventId, Name, context.CancellationToken);
        _logger.LogInformation("Sent {EventType} email for user {UserId}", e.Type, e.UserId);
    }
}

public class EmailNotificationConsumerDefinition : ConsumerDefinition<EmailNotificationConsumer>
{
    public EmailNotificationConsumerDefinition() => EndpointName = "auth-events-email";

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<EmailNotificationConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
    }
}
