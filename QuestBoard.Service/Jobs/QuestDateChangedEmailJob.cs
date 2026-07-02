using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class QuestDateChangedEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestDateChangedEmailJob> logger)
{
    public async Task ExecuteAsync(
        int questId,
        string[] recipientEmails,
        string[] playerNames,
        string questTitle,
        string dmName,
        DateTime oldDate,
        DateTime newDate,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var questUrl = $"{emailSettings.AppUrl}/Quest/Details/{questId}";

        logger.LogInformation(
            "Sending date-changed emails for quest {QuestId} to {Count} recipients.",
            questId, recipientEmails.Length);

        for (var i = 0; i < recipientEmails.Length; i++)
        {
            var html = await renderService.RenderAsync<QuestDateChanged>(new Dictionary<string, object?>
            {
                { nameof(QuestDateChanged.QuestTitle), questTitle },
                { nameof(QuestDateChanged.DmName),     dmName },
                { nameof(QuestDateChanged.AppUrl),     emailSettings.AppUrl },
                { nameof(QuestDateChanged.QuestUrl),   questUrl },
                { nameof(QuestDateChanged.OldDate),    oldDate },
                { nameof(QuestDateChanged.NewDate),    newDate }
            });

            await emailService.SendAsync(
                recipientEmails[i],
                $"Session date changed: {questTitle}",
                html);
        }
    }
}
