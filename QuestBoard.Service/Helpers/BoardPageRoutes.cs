using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Helpers;

/// <summary>
/// Maps a board page to the controller action that serves it, and to the sentence shown when a
/// visitor is turned away from it. Redirect targets are built from these names only, never from
/// request data.
/// </summary>
public static class BoardPageRoutes
{
    /// <summary>The controller and action that serve a page.</summary>
    public static (string Controller, string Action) RouteFor(BoardPage page) => page switch
    {
        BoardPage.Quests => ("Quest", "Index"),
        BoardPage.Calendar => ("Calendar", "Index"),
        BoardPage.Events => ("Events", "Index"),
        BoardPage.QuestLog => ("QuestLog", "Index"),
        BoardPage.Characters => ("Characters", "Index"),
        BoardPage.Contacts => ("Contacts", "Index"),
        BoardPage.Players => ("Players", "Index"),
        BoardPage.Shop => ("Shop", "Index"),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "This page has no route of its own.")
    };

    /// <summary>
    /// Where a visitor goes when a page is refused, and where the board's front door leads: the
    /// effective home. Unresolved availability keeps the quest list. A board with no page that can
    /// be a home falls back to the account page, which is never gated, so the redirect cannot loop.
    /// </summary>
    public static (string Controller, string Action) HomeRoute(BoardPageAvailability? availability)
    {
        if (availability is null)
        {
            return RouteFor(BoardPage.Quests);
        }

        return availability.EffectiveHome is { } home ? RouteFor(home) : ("Account", "Profile");
    }

    /// <summary>The one-sentence toast shown after a visitor is turned away from a switched-off page.</summary>
    public static string NotUsedMessage(BoardPage page) => page switch
    {
        BoardPage.Quests => "The quest list isn't used on this board.",
        BoardPage.Calendar => "The calendar isn't used on this board.",
        BoardPage.Events => "Events aren't used on this board.",
        BoardPage.QuestLog => "The Quest Log isn't used on this board.",
        BoardPage.Characters => "Characters aren't used on this board.",
        BoardPage.Contacts => "Contacts aren't used on this board.",
        BoardPage.Players => "The Players page isn't used on this board.",
        BoardPage.Shop => "Shop isn't used on this board.",
        BoardPage.DungeonMasterProfile => "DM profiles aren't used on this board.",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
    };
}
