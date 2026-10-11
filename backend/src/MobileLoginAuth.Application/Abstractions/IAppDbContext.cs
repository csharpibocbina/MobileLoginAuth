using Microsoft.EntityFrameworkCore;
using MobileLoginAuth.Domain.Entities;

namespace MobileLoginAuth.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<LoginAudit> LoginAudits { get; }
    DbSet<ProcessedMessage> ProcessedMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
