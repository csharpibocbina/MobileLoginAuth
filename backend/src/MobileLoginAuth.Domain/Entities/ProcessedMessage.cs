namespace MobileLoginAuth.Domain.Entities;

/// <summary>Records which consumer already handled which event, so redelivered messages are not sent twice.</summary>
public class ProcessedMessage
{
    public Guid EventId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}
