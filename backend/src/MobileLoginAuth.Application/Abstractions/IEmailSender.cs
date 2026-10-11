namespace MobileLoginAuth.Application.Abstractions;

public record EmailMessage(string To, string ToName, string Subject, string HtmlBody, string TextBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
