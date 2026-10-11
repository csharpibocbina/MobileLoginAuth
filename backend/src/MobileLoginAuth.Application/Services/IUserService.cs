using MobileLoginAuth.Application.Common;
using MobileLoginAuth.Application.Dtos;

namespace MobileLoginAuth.Application.Services;

public interface IUserService
{
    Task<Result<UserDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoginHistoryItemDto>> GetLoginHistoryAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
