using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Application.Options;
using MobileLoginAuth.Domain.Events;
using MobileLoginAuth.Infrastructure.Auth;
using MobileLoginAuth.Infrastructure.Email;
using MobileLoginAuth.Infrastructure.Messaging;
using MobileLoginAuth.Infrastructure.Persistence;

namespace MobileLoginAuth.Infrastructure;

public static class DependencyInjection
{
    public const string AuthEventsExchange = "auth-events";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? addConsumers = null)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<LockoutOptions>(configuration.GetSection(LockoutOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite(configuration.GetConnectionString("Default") ?? "Data Source=mobileloginauth.db"));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddScoped<IAuthEventPublisher, MassTransitAuthEventPublisher>();
        services.AddScoped<IProcessedEventGuard, ProcessedEventGuard>();

        var rabbit = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<EmailNotificationConsumer, EmailNotificationConsumerDefinition>();
            addConsumers?.Invoke(x);

            if (string.Equals(rabbit.Transport, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
                return;
            }

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbit.Host, rabbit.VirtualHost, h =>
                {
                    h.Username(rabbit.Username);
                    h.Password(rabbit.Password);
                });
                cfg.Message<AuthEvent>(m => m.SetEntityName(AuthEventsExchange));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
