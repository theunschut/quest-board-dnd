using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

/// <summary>
/// Sends one quest-moved email to one recipient, so a Hangfire retry can only ever resend to
/// that same address.
/// </summary>
public class QuestMovedEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestMovedEmailJob> logger)
{
    public async Task ExecuteAsync(
        int questId,
        int groupId,                  // group context for the Hangfire job's query filter
        QuestMovedAudience audience,
        string recipientEmail,
        string questTitle,
        string dmName,
        string boardName,
        DateTime? sessionDate,        // wall-clock value, passed through unconverted
        CancellationToken cancellationToken = default)
    {
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, async sp =>
        {
            var renderService = sp.GetRequiredService<IEmailRenderService>();
            var emailService  = sp.GetRequiredService<IEmailService>();
            var emailSettings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;

            var questUrl = $"{emailSettings.AppUrl}/Quest/Details/{questId}";

            var html = await renderService.RenderAsync<QuestMoved>(new Dictionary<string, object?>
            {
                { nameof(QuestMoved.QuestTitle),  questTitle },
                { nameof(QuestMoved.DmName),      dmName },
                { nameof(QuestMoved.BoardName),   boardName },
                { nameof(QuestMoved.SessionDate), sessionDate },
                { nameof(QuestMoved.QuestUrl),    questUrl },
                { nameof(QuestMoved.AppUrl),      emailSettings.AppUrl },
                { nameof(QuestMoved.Audience),    audience }
            });

            var subject = audience == QuestMovedAudience.Removed
                ? $"Your signup was removed: {questTitle}"
                : $"Quest moved to {boardName}: {questTitle}";

            await emailService.SendAsync(recipientEmail, subject, html);

            logger.LogInformation(
                "Sent quest moved email ({Audience}) for quest {QuestId}.",
                audience, questId);
        });
    }
}
