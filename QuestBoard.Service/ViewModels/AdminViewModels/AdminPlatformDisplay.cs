using Microsoft.AspNetCore.Mvc.Rendering;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

/// <summary>
/// The one place that turns a group role, a board type, a quest status, an account's state and a linked set's state
/// into the words, badge classes and icons the Admin and Platform pages show. Views and partials call these instead of
/// branching on an enum or printing its name, so a word is spelled the same way on every page and in both layouts.
/// The enums, the option values and the database keep their own names; only the displayed text is mapped here.
/// </summary>
public static class AdminPlatformDisplay
{
    /// <summary>What a status badge shows: the word, the badge fill class and the icon class.</summary>
    public readonly record struct Badge(string Word, string FillClass, string Icon);

    /// <summary>The word shown for a role on a board.</summary>
    public static string GroupRoleName(GroupRole role) => role switch
    {
        GroupRole.Admin => "Admin",
        GroupRole.DungeonMaster => "Dungeon Master",
        GroupRole.Player => "Player",
        _ => "Player"
    };

    /// <summary>The badge fill class for a role.</summary>
    public static string GroupRoleBadgeClass(GroupRole role) => role switch
    {
        GroupRole.Admin => "bg-danger",
        GroupRole.DungeonMaster => "bg-warning",
        GroupRole.Player => "bg-primary",
        _ => "bg-primary"
    };

    /// <summary>The icon class for a role.</summary>
    public static string GroupRoleIcon(GroupRole role) => role switch
    {
        GroupRole.Admin => "fa-shield-alt",
        GroupRole.DungeonMaster => "fa-crown",
        GroupRole.Player => "fa-dice-d20",
        _ => "fa-dice-d20"
    };

    /// <summary>A role as one badge.</summary>
    public static Badge GroupRoleBadge(GroupRole role) =>
        new(GroupRoleName(role), GroupRoleBadgeClass(role), GroupRoleIcon(role));

    /// <summary>The word shown for a board type.</summary>
    public static string BoardTypeName(BoardType boardType) => boardType switch
    {
        BoardType.OneShot => "One-Shot",
        BoardType.Campaign => "Campaign",
        _ => "One-Shot"
    };

    /// <summary>The badge fill class for a board type.</summary>
    public static string BoardTypeBadgeClass(BoardType boardType) => boardType switch
    {
        BoardType.OneShot => "bg-primary",
        BoardType.Campaign => "bg-info",
        _ => "bg-primary"
    };

    /// <summary>The icon class for a board type.</summary>
    public static string BoardTypeIcon(BoardType boardType) => boardType switch
    {
        BoardType.OneShot => "fa-dice-d20",
        BoardType.Campaign => "fa-book-open",
        _ => "fa-dice-d20"
    };

    /// <summary>A board type as one badge.</summary>
    public static Badge BoardTypeBadge(BoardType boardType) =>
        new(BoardTypeName(boardType), BoardTypeBadgeClass(boardType), BoardTypeIcon(boardType));

    /// <summary>The badge fill class for the page a board opens on, when that is not the quest list.</summary>
    public const string BoardHomeBadgeClass = "bg-secondary";

    /// <summary>The icon class for the page a board opens on.</summary>
    public const string BoardHomeIcon = "fa-house";

    /// <summary>The words that name the page a board opens on.</summary>
    public static string BoardHomeWord(string pageLabel) => "Home: " + pageLabel;

    /// <summary>
    /// The status of a quest on the admin list. A campaign board has no game night, so a quest there is open or closed;
    /// a one-shot board shows a quest as done once its finalized night has passed (judged by the caller against the
    /// board's own today), as finalized once a night is fixed, and as open before that.
    /// </summary>
    public static Badge QuestStatus(BoardType boardType, bool isClosed, bool isFinalized, bool hasBeenPlayed)
    {
        if (boardType == BoardType.Campaign)
            return isClosed ? Done("Closed") : Open();

        if (hasBeenPlayed)
            return Done("Done");

        return isFinalized
            ? new Badge("Finalized", "bg-primary", "fa-check-circle")
            : Open();

        static Badge Done(string word) => new(word, "bg-dark", "fa-flag-checkered");
        static Badge Open() => new("Open", "bg-success", "fa-clock");
    }

    /// <summary>An account's email confirmation as one badge.</summary>
    public static Badge AccountConfirmation(bool emailConfirmed) => emailConfirmed
        ? new Badge("Confirmed", "bg-success", "fa-check-circle")
        : new Badge("Unconfirmed", "bg-secondary", "fa-question-circle");

    /// <summary>Whether an account can sign in, as one badge.</summary>
    public static Badge AccountState(bool isDisabled) => isDisabled
        ? new Badge("Disabled", "bg-danger", "fa-user-lock")
        : new Badge("Active", "bg-success", "fa-check-circle");

    /// <summary>Whether a set of linked boards is in effect, as one badge.</summary>
    public static Badge LinkedSetState(bool isActive) => isActive
        ? new Badge("Active", "bg-success", "fa-check-circle")
        : new Badge("Inactive", "bg-secondary", "fa-pause-circle");

    /// <summary>The options of a board type select: the value stays the enum number, the text is the mapped word.</summary>
    public static IReadOnlyList<SelectListItem> BoardTypeOptions() =>
        Enum.GetValues<BoardType>()
            .Select(type => new SelectListItem(BoardTypeName(type), ((int)type).ToString()))
            .ToList();

    /// <summary>The options of a role select: the value stays the enum number, the text is the mapped word.</summary>
    public static IReadOnlyList<SelectListItem> GroupRoleOptions() =>
        Enum.GetValues<GroupRole>()
            .Select(role => new SelectListItem(GroupRoleName(role), ((int)role).ToString()))
            .ToList();
}
