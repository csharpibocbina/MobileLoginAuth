using MassTransit;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MobileLoginAuth.Api.Hubs;
using MobileLoginAuth.Api.Notifications;
using MobileLoginAuth.Application.Dtos;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;
using MobileLoginAuth.UnitTests.Helpers;

namespace MobileLoginAuth.UnitTests.Notifications;

public class SignalRNotificationConsumerTests
{
    private readonly Mock<IHubContext<NotificationHub>> _hub = new();
    private readonly Mock<IHubClients> _clients = new();
    private readonly Mock<IClientProxy> _proxy = new();
    private readonly List<(string Method, object?[] Args)> _calls = new();
    private string? _addressedUser;

    public SignalRNotificationConsumerTests()
    {
        _hub.Setup(h => h.Clients).Returns(_clients.Object);
        _clients.Setup(c => c.User(It.IsAny<string>()))
            .Callback<string>(u => _addressedUser = u)
            .Returns(_proxy.Object);
        _proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((m, a, _) => _calls.Add((m, a)))
            .Returns(Task.CompletedTask);
    }

    private Task<ConsumerHarness> StartAsync() => ConsumerHarness.StartAsync(
        services => services.AddSingleton(_hub.Object),
        x => x.AddConsumer<SignalRNotificationConsumer, SignalRNotificationConsumerDefinition>());

    // UT-34
    [Theory]
    [InlineData(AuthEventType.LoginSucceeded, "New sign-in to your account")]
    [InlineData(AuthEventType.LoginFailed, "Failed sign-in attempt")]
    [InlineData(AuthEventType.Logout, "You signed out")]
    public async Task SendsNotificationToTheUser(AuthEventType type, string expectedTitle)
    {
        await using var h = await StartAsync();
        var userId = Guid.NewGuid();

        await h.Harness.Bus.Publish(AuthEvents.Create(type, userId: userId));

        (await h.Harness.GetConsumerHarness<SignalRNotificationConsumer>().Consumed.Any<AuthEvent>()).Should().BeTrue();
        _addressedUser.Should().Be(userId.ToString());
        _calls.Should().ContainSingle();
        _calls[0].Method.Should().Be("ReceiveNotification");
        var dto = _calls[0].Args.Single().Should().BeOfType<NotificationDto>().Subject;
        dto.Type.Should().Be(type.ToString());
        dto.Title.Should().Be(expectedTitle);
        dto.Message.Should().Contain("iPhone 15");
    }

    // UT-35
    [Fact]
    public async Task IgnoresDuplicateEventId()
    {
        await using var h = await StartAsync();
        var e = AuthEvents.Create(AuthEventType.Logout);
        var consumer = h.Harness.GetConsumerHarness<SignalRNotificationConsumer>();

        await h.Harness.Bus.Publish(e);
        (await consumer.Consumed.Any<AuthEvent>()).Should().BeTrue();
        await h.Harness.Bus.Publish(e);
        (await consumer.Consumed.SelectAsync<AuthEvent>().Take(2).Count()).Should().Be(2);

        _calls.Should().ContainSingle();
    }
}
