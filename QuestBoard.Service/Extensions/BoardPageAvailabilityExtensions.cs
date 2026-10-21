using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Extensions;

/// <summary>
/// Null-safe questions views and controllers ask about the active board's pages. The resolver
/// returns null when no board type resolves, and the two helpers answer that state differently on
/// purpose so each call site keeps showing exactly what it showed before pages could be switched off.
/// </summary>
public static class BoardPageAvailabilityExtensions
{
    /// <summary>
    /// True only when a board resolved and it offers the page. Use it where the markup already
    /// required a known board type, so an unresolved board keeps hiding the item.
    /// </summary>
    public static bool Allows(this BoardPageAvailability? availability, BoardPage page)
    {
        return availability is not null && availability.IsAvailable(page);
    }

    /// <summary>
    /// True when no board resolved or the board offers the page. It mirrors what the page gate lets
    /// through. Use it where the markup had no board-type requirement, so an unresolved board keeps
    /// showing the item.
    /// </summary>
    public static bool Serves(this BoardPageAvailability? availability, BoardPage page)
    {
        return availability is null || availability.IsAvailable(page);
    }
}
