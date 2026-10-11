using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MobileLoginAuth.Infrastructure.Persistence;

namespace MobileLoginAuth.UnitTests.Helpers;

/// <summary>A real SQLite database kept in memory for the lifetime of the test.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Context = CreateContext();
        Context.Database.EnsureCreated();
    }

    public AppDbContext Context { get; }

    /// <summary>A fresh context on the same database, for asserting what was actually saved.</summary>
    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
