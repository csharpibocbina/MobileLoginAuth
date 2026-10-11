using MobileLoginAuth.Domain.Entities;

namespace MobileLoginAuth.Domain.Events;

/// <summary>Message published to RabbitMQ for every auth event that should notify the user.</summary>
public record AuthEvent(
    Guid EventId,
    AuthEventType Type,
    Guid UserId,
    string Email,
    string DisplayName,
    string? IpAddress,
    string? DeviceInfo,
    DateTime OccurredAtUtc);
