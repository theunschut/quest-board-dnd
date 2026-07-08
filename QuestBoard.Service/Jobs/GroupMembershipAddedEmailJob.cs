using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class GroupMembershipAddedEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<GroupMembershipAddedEmailJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string userName, string groupName, string role, string loginUrl, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var html = await renderService.RenderAsync<AddedToGroup>(new Dictionary<string, object?>
        {
            { nameof(AddedToGroup.UserName),  userName },
            { nameof(AddedToGroup.GroupName), groupName },
            { nameof(AddedToGroup.Role),      role },
            { nameof(AddedToGroup.LoginUrl),  loginUrl },
            { nameof(AddedToGroup.AppUrl),    emailSettings.AppUrl }
        });

        await emailService.SendAsync(toEmail, $"You've been added to {groupName}", html);
        logger.LogInformation("GroupMembershipAddedEmailJob: sent added-to-group email.");
    }
}
