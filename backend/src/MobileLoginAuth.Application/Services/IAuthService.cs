using MobileLoginAuth.Application.Common;
using MobileLoginAuth.Application.Dtos;

namespace MobileLoginAuth.Application.Services;

public interface IAuthService
{
    Task<Result<UserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, ClientInfo client, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(Guid userId, LogoutRequest request, ClientInfo client, CancellationToken cancellationToken = default);
}
