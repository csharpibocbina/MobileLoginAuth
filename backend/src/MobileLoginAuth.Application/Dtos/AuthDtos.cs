namespace MobileLoginAuth.Application.Dtos;

public record RegisterRequest(string Email, string Password, string DisplayName);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string? RefreshToken);

public record UserDto(Guid Id, string Email, string DisplayName, DateTime CreatedAtUtc);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserDto User);

public record LoginHistoryItemDto(string EventType, string? IpAddress, string? DeviceInfo, DateTime OccurredAtUtc);
