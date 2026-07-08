namespace QuestBoard.Domain.Models;

public record EmailSettings
{
    public string SmtpServer { get; init; } = "localhost";
    public int SmtpPort { get; init; } = 25;
    public bool EnableSsl { get; init; } = false;
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "D&D Quest Board";
    public string AppUrl { get; init; } = string.Empty;
    public string ResendApiKey { get; init; } = string.Empty;

    // Defaults to false so production (which never loads appsettings.Development.json)
    // sends real emails without needing this key at all. Local dev overrides it to true
    // via appsettings.Development.json so the full pipeline — Hangfire jobs, rendering,
    // logging — still runs end to end without hitting a real SMTP server.
    public bool SuppressSending { get; init; } = false;
}
