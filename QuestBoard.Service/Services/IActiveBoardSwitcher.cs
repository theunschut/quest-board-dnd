using Microsoft.AspNetCore.Http;

namespace QuestBoard.Service.Services;

/// <summary>
/// The one place in the application permitted to write the three active-board session keys.
/// Every writer -- the group picker's explicit selection, its single-group auto-select, and the
/// cross-board deep-link middleware -- goes through this so the keys can never drift apart.
/// </summary>
public interface IActiveBoardSwitcher
{
    Task SwitchAsync(HttpContext context, int groupId, string groupName, CancellationToken token = default);
}
