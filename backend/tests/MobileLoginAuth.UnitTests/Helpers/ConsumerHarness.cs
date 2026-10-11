using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Infrastructure.Messaging;
using MobileLoginAuth.Infrastructure.Persistence;

namespace MobileLoginAuth.UnitTests.Helpers;

/// <summary>MassTransit in-memory test harness with a real SQLite database for the idempotency guard.</summary>
public sealed class ConsumerHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private ConsumerHarness(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        Provider = provider;
        Harness = provider.GetRequiredService<ITestHarness>();
    }

    public ServiceProvider Provider { get; }
    public ITestHarness Harness { get; }

    public static async Task<ConsumerHarness> StartAsync(
        Action<IServiceCollection> configureServices,
        Action<IBusRegistrationConfigurator> addConsumers)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProcessedEventGuard, ProcessedEventGuard>();
        configureServices(services);
        services.AddMassTransitTestHarness(addConsumers);

        var provider = services.BuildServiceProvider(true);
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        }

        var harness = new ConsumerHarness(connection, provider);
        await harness.Harness.Start();
        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        await Harness.Stop();
        await Provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
