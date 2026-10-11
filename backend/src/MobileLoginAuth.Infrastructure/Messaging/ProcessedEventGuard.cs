using Microsoft.EntityFrameworkCore;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Domain.Entities;

namespace MobileLoginAuth.Infrastructure.Messaging;

/// <summary>Makes consumers idempotent: an event id is handled at most once per consumer.</summary>
public interface IProcessedEventGuard
{
    Task<bool> AlreadyProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
}

public class ProcessedEventGuard : IProcessedEventGuard
{
    private readonly IAppDbContext _db;
    private readonly TimeProvider _time;

    public ProcessedEventGuard(IAppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<bool> AlreadyProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default) =>
        _db.ProcessedMessages.AnyAsync(m => m.EventId == eventId && m.ConsumerName == consumerName, cancellationToken);

    public async Task MarkProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        _db.ProcessedMessages.Add(new ProcessedMessage
        {
            EventId = eventId,
            ConsumerName = consumerName,
            ProcessedAtUtc = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
