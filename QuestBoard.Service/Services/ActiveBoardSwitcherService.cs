using Microsoft.AspNetCore.Http;
using QuestBoard.Service.Constants;

namespace QuestBoard.Service.Services;

public class ActiveBoardSwitcherService : IActiveBoardSwitcher
{
    /// <inheritdoc/>
    public async Task SwitchAsync(HttpContext context, int groupId, string groupName, CancellationToken token = default)
    {
        // Middleware may run before anything else has touched the session this request, so it
        // cannot be assumed loaded yet.
        await context.Session.LoadAsync(token);

        context.Session.SetInt32(SessionKeys.ActiveGroupId, groupId);
        context.Session.SetString(SessionKeys.ActiveGroupName, groupName);
        // All three keys are written together on purpose: dropping this one would not break the
        // current request at all, it would silently force the session middleware to re-check
        // membership on the very next one.
        context.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
    }
}
