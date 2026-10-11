using MassTransit;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Domain.Events;

namespace MobileLoginAuth.Infrastructure.Messaging;

public class MassTransitAuthEventPublisher : IAuthEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitAuthEventPublisher(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task PublishAsync(AuthEvent authEvent, CancellationToken cancellationToken = default) =>
        _publishEndpoint.Publish(authEvent, cancellationToken);
}
