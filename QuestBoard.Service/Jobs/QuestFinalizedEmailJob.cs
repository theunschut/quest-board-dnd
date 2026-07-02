using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class QuestFinalizedEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestFinalizedEmailJob> logger)
{
    public async Task ExecuteAsync(
        int questId,
        int groupId,                  // group context for the Hangfire job's query filter
        DateTime finalizedDate,
        string[] recipientEmails,
        string[] playerNames,
        string questTitle,
        string dmName,
        string questDescription,
        int challengeRating,
        CancellationToken cancellationToken = default)
    {
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, async sp =>
        {
            var questRepository = sp.GetRequiredService<IQuestRepository>();
            var renderService   = sp.GetRequiredService<IEmailRenderService>();
            var emailService    = sp.GetRequiredService<IEmailService>();
            var emailSettings   = sp.GetRequiredService<IOptions<EmailSettings>>().Value;

            // Dedup guard: use .Date comparison — "same session date" intent, not same millisecond
            var quest = await questRepository.GetQuestWithDetailsAsync(questId, cancellationToken);
            if (quest?.FinalizedEmailSentForDate?.Date == finalizedDate.Date)
            {
                logger.LogInformation(
                    "Finalized email already sent for quest {QuestId} on {Date}. Skipping.",
                    questId, finalizedDate);
                return;
            }

            var questUrl = $"{emailSettings.AppUrl}/Quest/Details/{questId}";

            for (var i = 0; i < recipientEmails.Length; i++)
            {
                var html = await renderService.RenderAsync<QuestFinalized>(new Dictionary<string, object?>
                {
                    { nameof(QuestFinalized.QuestTitle),           questTitle },
                    { nameof(QuestFinalized.DmName),               dmName },
                    { nameof(QuestFinalized.QuestDate),            finalizedDate },
                    { nameof(QuestFinalized.QuestDescription),     questDescription },
                    { nameof(QuestFinalized.ConfirmedPlayerNames), playerNames.ToList() },
                    { nameof(QuestFinalized.QuestUrl),             questUrl },
                    { nameof(QuestFinalized.ChallengeRating),      challengeRating },
                    { nameof(QuestFinalized.AppUrl),               emailSettings.AppUrl }
                });

                await emailService.SendAsync(
                    recipientEmails[i],
                    $"Your quest has been confirmed: {questTitle}",
                    html);
            }

            await questRepository.SetFinalizedEmailSentForDateAsync(questId, finalizedDate, cancellationToken);
        });
    }
}
