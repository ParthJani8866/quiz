using System.Net;
using System.Net.Mail;

namespace QuizFlow.Api.Services;

public interface IAccountEmailSender
{
    Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken);
}

public sealed class SmtpAccountEmailSender(IConfiguration configuration) : IAccountEmailSender
{
    public async Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Email SMTP settings must be configured to send account messages.");
        using var client = new SmtpClient(host, int.TryParse(configuration["Email:SmtpPort"], out var port) ? port : 587)
        {
            EnableSsl = !string.Equals(configuration["Email:UseTls"], "false", StringComparison.OrdinalIgnoreCase),
            Credentials = string.IsNullOrWhiteSpace(configuration["Email:Username"])
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(configuration["Email:Username"], configuration["Email:Password"])
        };
        using var message = new MailMessage(from, email, subject, body) { IsBodyHtml = true };
        await client.SendMailAsync(message, cancellationToken);
    }
}
