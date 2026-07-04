using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class QuestWaitlistPromotedEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestWaitlistPromotedEmailJob> logger)
{
    public async Task ExecuteAsync(
        int questId,
        int groupId,                  // group context for the Hangfire job's query filter
        DateTime finalizedDate,
        string recipientEmail,
        string playerName,
        string questTitle,
        string dmName,
        string questDescription,
        int challengeRating,
        CancellationToken cancellationToken = default)
    {
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, async sp =>
        {
            var renderService = sp.GetRequiredService<IEmailRenderService>();
            var emailService  = sp.GetRequiredService<IEmailService>();
            var emailSettings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;

            var questUrl = $"{emailSettings.AppUrl}/Quest/Details/{questId}";

            var html = await renderService.RenderAsync<WaitlistPromoted>(new Dictionary<string, object?>
            {
                { nameof(WaitlistPromoted.QuestTitle),       questTitle },
                { nameof(WaitlistPromoted.DmName),           dmName },
                { nameof(WaitlistPromoted.QuestDate),        finalizedDate },
                { nameof(WaitlistPromoted.QuestDescription), questDescription },
                { nameof(WaitlistPromoted.PlayerName),       playerName },
                { nameof(WaitlistPromoted.QuestUrl),         questUrl },
                { nameof(WaitlistPromoted.ChallengeRating),  challengeRating },
                { nameof(WaitlistPromoted.AppUrl),           emailSettings.AppUrl }
            });

            await emailService.SendAsync(recipientEmail, $"A seat opened up: {questTitle}", html);

            logger.LogInformation(
                "Sent waitlist promotion email for quest {QuestId} to {RecipientEmail}.",
                questId, recipientEmail);
        });
    }
}
