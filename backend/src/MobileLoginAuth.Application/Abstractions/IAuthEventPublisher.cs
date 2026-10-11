using MobileLoginAuth.Domain.Events;

namespace MobileLoginAuth.Application.Abstractions;

public interface IAuthEventPublisher
{
    Task PublishAsync(AuthEvent authEvent, CancellationToken cancellationToken = default);
}
