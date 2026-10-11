using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MobileLoginAuth.Application.Notifications;
using MobileLoginAuth.Application.Services;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Application.Validators;

namespace MobileLoginAuth.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        return services;
    }
}
