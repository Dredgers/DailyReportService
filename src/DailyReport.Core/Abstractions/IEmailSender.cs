namespace DailyReport.Core.Abstractions;

public sealed record EmailMessage(string FromName, string From, IReadOnlyList<string> To, string Subject, string Html, string Text);

public sealed record EmailReceipt(string ProviderMessageId);

public interface IEmailSender
{
    Task<EmailReceipt> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
