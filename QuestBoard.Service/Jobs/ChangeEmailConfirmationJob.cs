using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class ChangeEmailConfirmationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ChangeEmailConfirmationJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string userName, string callbackUrl, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var html = await renderService.RenderAsync<ChangeEmailConfirm>(new Dictionary<string, object?>
        {
            { nameof(ChangeEmailConfirm.UserName),    userName },
            { nameof(ChangeEmailConfirm.CallbackUrl), callbackUrl },
            { nameof(ChangeEmailConfirm.AppUrl),      emailSettings.AppUrl }
        });

        await emailService.SendAsync(toEmail, "Confirm your new D&D Quest Board email address", html);
        logger.LogInformation("ChangeEmailConfirmationJob: sent email-change confirmation.");
    }
}
