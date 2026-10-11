namespace MobileLoginAuth.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>"RabbitMq" (default) or "InMemory" (tests and running without Docker).</summary>
    public string Transport { get; set; } = "RabbitMq";
    public string Host { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
