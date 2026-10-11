namespace MobileLoginAuth.Domain.Entities;

public class LoginAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public AuthEventType EventType { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
