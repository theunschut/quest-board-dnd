using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class ForgotPasswordEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ForgotPasswordEmailJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string callbackUrl, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var html = await renderService.RenderAsync<ForgotPassword>(new Dictionary<string, object?>
        {
            { nameof(ForgotPassword.CallbackUrl), callbackUrl },
            { nameof(ForgotPassword.AppUrl),      emailSettings.AppUrl }
        });

        await emailService.SendAsync(toEmail, "Reset your D&D Quest Board password", html);
        logger.LogInformation("ForgotPasswordEmailJob: sent password-reset email.");
    }
}
