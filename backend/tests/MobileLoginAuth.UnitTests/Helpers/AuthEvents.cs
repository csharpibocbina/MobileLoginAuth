using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;

namespace MobileLoginAuth.UnitTests.Helpers;

public static class AuthEvents
{
    public static AuthEvent Create(AuthEventType type, Guid? eventId = null, Guid? userId = null) => new(
        eventId ?? Guid.NewGuid(),
        type,
        userId ?? Guid.NewGuid(),
        "jay@example.com",
        "Jay",
        "10.0.0.1",
        "iPhone 15",
        new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc));
}
