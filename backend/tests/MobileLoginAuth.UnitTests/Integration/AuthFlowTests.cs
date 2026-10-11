using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;
using MobileLoginAuth.Infrastructure.Messaging;
using MassTransit;

namespace MobileLoginAuth.UnitTests.Integration;

/// <summary>End-to-end through HTTP with SQLite and an in-memory MassTransit test harness instead of RabbitMQ.</summary>
public class AuthFlowTests : IClassFixture<AuthFlowTests.Factory>
{
    private readonly Factory _factory;

    public AuthFlowTests(Factory factory) => _factory = factory;

    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mla-test-{Guid.NewGuid():N}.db");

        public Mock<IEmailSender> EmailSender { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting("RabbitMq:Transport", "InMemory");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(EmailSender.Object);
                services.AddMassTransitTestHarness(x =>
                {
                    x.AddConsumer<EmailNotificationConsumer, EmailNotificationConsumerDefinition>();
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Register_Login_Me_Logout_PublishesAllEventsAndSendsEmails()
    {
        var client = _factory.CreateClient();
        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        const string email = "flow@example.com";

        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passw0rd!", "Flow")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        (await client.GetAsync("/api/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var me = await client.GetFromJsonAsync<UserDto>("/api/users/me");
        me!.Email.Should().Be(email);

        (await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(login.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var types = new List<AuthEventType>();
        await foreach (var m in harness.Published.SelectAsync<AuthEvent>(m => m.Context.Message.Email == email).Take(3))
        {
            types.Add(m.Context.Message.Type);
        }

        types.Should().BeEquivalentTo(new[] { AuthEventType.LoginFailed, AuthEventType.LoginSucceeded, AuthEventType.Logout });

        // Wait for the email consumer specifically (the SignalR consumer also consumes AuthEvent).
        var emailConsumer = harness.GetConsumerHarness<EmailNotificationConsumer>();
        (await emailConsumer.Consumed.SelectAsync<AuthEvent>(m => m.Context.Message.Email == email).Take(3).Count()).Should().Be(3);
        _factory.EmailSender.Verify(s => s.SendAsync(It.Is<EmailMessage>(m => m.To == email), It.IsAny<CancellationToken>()), Times.Exactly(3));

        var history = await client.GetFromJsonAsync<List<LoginHistoryItemDto>>("/api/users/me/login-history");
        history!.Select(h => h.EventType).Should().BeEquivalentTo(new[] { "LoginFailed", "LoginSucceeded", "Logout" });
    }

    [Fact]
    public async Task Refresh_ReturnsNewTokens_AndOldTokenStopsWorking()
    {
        var client = _factory.CreateClient();
        const string email = "refresh@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passw0rd!", "R"));
        var login = (await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!")))
            .Content.ReadFromJsonAsync<AuthResponse>())!;

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_RemovesUser()
    {
        var client = _factory.CreateClient();
        const string email = "delete@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passw0rd!", "D"));
        var login = (await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!")))
            .Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        (await client.DeleteAsync("/api/users/me")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/users/me")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_BehindProxy_RecordsForwardedClientIp()
    {
        var client = _factory.CreateClient();
        const string email = "proxy@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passw0rd!", "P"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, "Passw0rd!"))
        };
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        var login = (await (await client.SendAsync(request)).Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var history = await client.GetFromJsonAsync<List<LoginHistoryItemDto>>("/api/users/me/login-history");

        history!.Single().IpAddress.Should().Be("203.0.113.7");
    }
}
