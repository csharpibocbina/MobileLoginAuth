using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MobileLoginAuth.Application.Abstractions;
using MobileLoginAuth.Application.Notifications;
using MobileLoginAuth.Domain.Entities;
using MobileLoginAuth.Domain.Events;
using MobileLoginAuth.Infrastructure.Messaging;
using MobileLoginAuth.UnitTests.Helpers;

namespace MobileLoginAuth.UnitTests.Notifications;

public class EmailNotificationConsumerTests
{
    private readonly Mock<IEmailSender> _sender = new();
    private readonly List<EmailMessage> _sent = new();

    public EmailNotificationConsumerTests()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _sent.Add(m))
            .Returns(Task.CompletedTask);
    }

    private Task<ConsumerHarness> StartAsync() => ConsumerHarness.StartAsync(
        services =>
        {
            services.AddSingleton(_sender.Object);
            services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
        },
        x => x.AddConsumer<EmailNotificationConsumer, EmailNotificationConsumerDefinition>());

    // UT-30, UT-31, UT-32
    [Theory]
    [InlineData(AuthEventType.LoginSucceeded, "New sign-in to your account")]
    [InlineData(AuthEventType.LoginFailed, "Failed sign-in attempt")]
    [InlineData(AuthEventType.Logout, "You signed out")]
    public async Task SendsEmailWithSubjectForEachEventType(AuthEventType type, string expectedSubject)
    {
        await using var h = await StartAsync();

        await h.Harness.Bus.Publish(AuthEvents.Create(type));

        (await h.Harness.GetConsumerHarness<EmailNotificationConsumer>().Consumed.Any<AuthEvent>()).Should().BeTrue();
        _sent.Should().ContainSingle();
        _sent[0].To.Should().Be("jay@example.com");
        _sent[0].Subject.Should().Be(expectedSubject);
    }

    // UT-33
    [Fact]
    public async Task WhenSmtpThrows_MessageIsRetriedThenFaulted()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));
        await using var h = await StartAsync();

        await h.Harness.Bus.Publish(AuthEvents.Create(AuthEventType.LoginSucceeded));

        (await h.Harness.Published.Any<Fault<AuthEvent>>()).Should().BeTrue();
        _sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    // UT-35
    [Fact]
    public async Task IgnoresDuplicateEventId()
    {
        await using var h = await StartAsync();
        var e = AuthEvents.Create(AuthEventType.LoginSucceeded);
        var consumer = h.Harness.GetConsumerHarness<EmailNotificationConsumer>();

        await h.Harness.Bus.Publish(e);
        (await consumer.Consumed.Any<AuthEvent>()).Should().BeTrue();
        await h.Harness.Bus.Publish(e);
        (await consumer.Consumed.SelectAsync<AuthEvent>().Take(2).Count()).Should().Be(2);

        _sent.Should().ContainSingle();
    }
}
