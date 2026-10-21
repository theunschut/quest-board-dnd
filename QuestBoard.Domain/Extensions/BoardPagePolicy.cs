using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Extensions;

/// <summary>
/// The one rule set for which pages a board offers and which page is its home. Menus, in-page
/// links, the page gate, the home redirects and the Platform form all ask this class, so none of
/// them can disagree about what a board shows.
/// </summary>
/// <remarks>
/// A board's type is a hard ceiling: switches can only narrow what the type allows, never widen it.
/// </remarks>
public static class BoardPagePolicy
{
    /// <summary>Every page, in the order the Platform form lists them.</summary>
    public static IReadOnlyList<BoardPage> Catalogue { get; } =
    [
        BoardPage.Quests,
        BoardPage.Calendar,
        BoardPage.Events,
        BoardPage.QuestLog,
        BoardPage.Characters,
        BoardPage.Contacts,
        BoardPage.Players,
        BoardPage.Shop,
        BoardPage.DungeonMasterProfile
    ];

    /// <summary>The pages that may be a board's home. DM profiles never are.</summary>
    public static IReadOnlyList<BoardPage> HomeCandidates { get; } =
    [
        BoardPage.Quests,
        BoardPage.Calendar,
        BoardPage.Events,
        BoardPage.QuestLog,
        BoardPage.Characters,
        BoardPage.Contacts,
        BoardPage.Players,
        BoardPage.Shop
    ];

    /// <summary>
    /// Whether a board of this type can ever offer the page. Players, Shop and DM profiles exist
    /// on one-shot boards only.
    /// </summary>
    public static bool AllowedOn(BoardType type, BoardPage page) => page switch
    {
        BoardPage.Players or BoardPage.Shop or BoardPage.DungeonMasterProfile => type == BoardType.OneShot,
        _ => true
    };

    /// <summary>
    /// Whether a super admin may switch the page off. Every page can be switched off, the quest
    /// list included; the board type stays a ceiling on what can be switched on.
    /// </summary>
    public static bool IsSwitchable(BoardPage page) => true;

    /// <summary>
    /// Whether the board offers the page: its type allows it and it is not switched off. The Quest
    /// Log needs the quest list, so it is unavailable whenever Quests is, without the stored
    /// Quest Log bit changing; switching Quests back on restores it as it was.
    /// </summary>
    public static bool IsAvailable(BoardType type, BoardPageSet disabled, BoardPage page)
    {
        if (!AllowedOn(type, page) || disabled.Contains(page))
        {
            return false;
        }

        return page != BoardPage.QuestLog || IsAvailable(type, disabled, BoardPage.Quests);
    }

    /// <summary>Whether the page may be chosen as a board's home.</summary>
    public static bool IsHomeCandidate(BoardPage page) => HomeCandidates.Contains(page);

    /// <summary>
    /// The page a visitor lands on: the stored home when it is a candidate and available,
    /// otherwise the quest list when available, otherwise the first available candidate, otherwise
    /// null. A non-null result is always available, so redirecting to it cannot loop.
    /// </summary>
    public static BoardPage? EffectiveHome(BoardType type, BoardPageSet disabled, BoardPage storedHome)
    {
        if (IsHomeCandidate(storedHome) && IsAvailable(type, disabled, storedHome))
        {
            return storedHome;
        }

        if (IsAvailable(type, disabled, BoardPage.Quests))
        {
            return BoardPage.Quests;
        }

        foreach (var candidate in HomeCandidates)
        {
            if (IsAvailable(type, disabled, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The name a page goes by in the Platform form and in badges.</summary>
    public static string Label(BoardPage page) => page switch
    {
        BoardPage.Quests => "Quest list",
        BoardPage.Calendar => "Calendar",
        BoardPage.Events => "Events",
        BoardPage.QuestLog => "Quest Log",
        BoardPage.Characters => "Characters",
        BoardPage.Contacts => "Contacts",
        BoardPage.Players => "Players",
        BoardPage.Shop => "Shop",
        BoardPage.DungeonMasterProfile => "DM profile",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
    };

    /// <summary>
    /// Why a page cannot be switched on for this board, or null when nothing locks it.
    /// </summary>
    public static string? LockReason(BoardType type, BoardPageSet disabled, BoardPage page)
    {
        if (!AllowedOn(type, page))
        {
            return "Not available on Campaign boards.";
        }

        if (page == BoardPage.QuestLog && !IsAvailable(type, disabled, BoardPage.Quests))
        {
            return "Needs the quest list";
        }

        return null;
    }
}
