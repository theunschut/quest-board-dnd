namespace QuestBoard.Domain.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends an HTML email via the configured SMTP relay. Used by all Hangfire email jobs.
    /// No-ops (does not throw) if SMTP settings are not configured. If SMTP settings ARE configured
    /// but the send itself fails (network/auth/recipient error), the exception is logged and rethrown —
    /// callers that must not fail the caller's flow on a delivery error need to catch this themselves.
    /// </summary>
    Task SendAsync(string toEmail, string subject, string htmlBody);
}
