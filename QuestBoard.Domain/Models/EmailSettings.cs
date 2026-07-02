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
}
