using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace QuestBoard.Domain.Services;

public class EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = options.Value;

    private SmtpClient? CreateSmtpClient()
    {
        if (string.IsNullOrEmpty(_settings.FromEmail))
        {
            logger.LogWarning("Email settings not configured. Skipping email notification.");
            return null;
        }

        var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
        {
            EnableSsl = _settings.EnableSsl
        };

        if (!string.IsNullOrEmpty(_settings.SmtpUsername))
            client.Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword);

        return client;
    }

    /// <inheritdoc/>
    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            using var client = CreateSmtpClient();
            if (client == null) return;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);
            await client.SendMailAsync(mailMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email with subject {Subject}", subject);
            throw;
        }
    }
}
