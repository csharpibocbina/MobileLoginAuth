namespace MobileLoginAuth.Application.Dtos;

/// <summary>Payload of the SignalR "ReceiveNotification" client method.</summary>
public record NotificationDto(Guid EventId, string Type, string Title, string Message, DateTime OccurredAtUtc);
