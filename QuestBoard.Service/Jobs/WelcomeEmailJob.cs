using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class WelcomeEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<WelcomeEmailJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string userName, string callbackUrl, bool isNewAccount, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var html = await renderService.RenderAsync<Welcome>(new Dictionary<string, object?>
        {
            { nameof(Welcome.UserName),      userName },
            { nameof(Welcome.CallbackUrl),   callbackUrl },
            { nameof(Welcome.AppUrl),        emailSettings.AppUrl },
            { nameof(Welcome.IsNewAccount),  isNewAccount }
        });

        await emailService.SendAsync(toEmail, "Welcome to the D&D Quest Board — set your password", html);
        logger.LogInformation("WelcomeEmailJob: sent welcome email.");
    }
}
