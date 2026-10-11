using Microsoft.EntityFrameworkCore;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Application.Common;
using MobileLoginAuth.Application.Dtos;

namespace MobileLoginAuth.Application.Services;

public class UserService : IUserService
{
    private readonly IAppDbContext _db;

    public UserService(IAppDbContext db) => _db = db;

    public async Task<Result<UserDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null
            ? Result<UserDto>.Failure(ErrorType.NotFound, "User not found.")
            : Result<UserDto>.Success(AuthService.ToDto(user));
    }

    public async Task<IReadOnlyList<LoginHistoryItemDto>> GetLoginHistoryAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default)
    {
        var audits = await _db.LoginAudits.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return audits
            .Select(a => new LoginHistoryItemDto(a.EventType.ToString(), a.IpAddress, a.DeviceInfo, a.OccurredAtUtc))
            .ToList();
    }

    public async Task<Result> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(ErrorType.NotFound, "User not found.");
        }

        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(cancellationToken);
        var audits = await _db.LoginAudits.Where(a => a.UserId == userId).ToListAsync(cancellationToken);
        _db.RefreshTokens.RemoveRange(tokens);
        _db.LoginAudits.RemoveRange(audits);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
