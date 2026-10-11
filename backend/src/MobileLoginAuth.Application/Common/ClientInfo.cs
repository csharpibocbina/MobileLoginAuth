namespace MobileLoginAuth.Application.Common;

/// <summary>Where a request came from; included in audits and notification emails.</summary>
public record ClientInfo(string? IpAddress, string? DeviceInfo);
