using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardDeletion;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

/// <summary>
/// The board delete confirmation page: the board's identity, one row per kind of content the delete
/// removes, the rows on other boards that are only unlinked, and whether the typed name was refused.
/// </summary>
public class BoardDeletePageViewModel
{
    private const string UntitledName = "(untitled)";

    private sealed record KindLabels(string Icon, string Singular, string Plural, string? Hint, string? SummaryText);

    private static readonly Dictionary<BoardContentKind, KindLabels> Labels = new()
    {
        [BoardContentKind.Quests] = new("fa-scroll", "quest", "quests", null, "Show quest titles"),
        [BoardContentKind.ProposedDates] = new("fa-calendar-day", "proposed date", "proposed dates", null, null),
        [BoardContentKind.DateVotes] = new("fa-check-to-slot", "date vote", "date votes", null, null),
        [BoardContentKind.PlayerSignups] = new("fa-user-check", "player signup", "player signups", null, null),
        [BoardContentKind.ReminderLogs] = new("fa-envelope", "reminder record", "reminder records",
            "Logs of reminder emails already sent for these quests.", null),
        [BoardContentKind.Events] = new("fa-calendar-days", "event", "events", null, "Show event titles"),
        [BoardContentKind.EventSignups] = new("fa-user-check", "event signup", "event signups", null, null),
        [BoardContentKind.EventSeries] = new("fa-repeat", "event series", "event series", null, "Show event series names"),
        [BoardContentKind.Characters] = new("fa-user-shield", "character", "characters",
            "Deleted together with their classes and images.", "Show character names"),
        [BoardContentKind.Contacts] = new("fa-address-book", "contact", "contacts",
            "Deleted together with their images.", "Show contact names"),
        [BoardContentKind.ContactNotes] = new("fa-note-sticky", "contact note", "contact notes", null, null),
        [BoardContentKind.ContactCategories] = new("fa-folder", "contact category", "contact categories", null, "Show category names"),
        [BoardContentKind.ContactTags] = new("fa-tag", "contact tag", "contact tags", null, "Show tag names"),
        [BoardContentKind.ShopItems] = new("fa-store", "shop item", "shop items", null, "Show shop item names"),
        [BoardContentKind.Transactions] = new("fa-receipt", "purchase or return", "purchases and returns",
            "The only record of what members paid on this board.", null)
    };

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The name the admin is asked to type: the stored name without its edge whitespace, which the
    /// confirmation ignores on both sides.
    /// </summary>
    public string ConfirmationName => BoardNameText.TrimEdges(Name);

    /// <summary>When the board was created; a real UTC instant.</summary>
    public DateTime CreatedAt { get; init; }

    public BoardType BoardType { get; init; }

    /// <summary>The kinds of content the delete removes, only those with at least one row.</summary>
    public IReadOnlyList<BoardDeleteRowViewModel> DeletedRows { get; init; } = [];

    /// <summary>Rows on other boards that stay and only lose their link to this board.</summary>
    public IReadOnlyList<BoardDeleteRowViewModel> UnlinkedRows { get; init; } = [];

    /// <summary>True when the page is shown again because the typed name did not match.</summary>
    public bool NameMismatch { get; init; }

    public bool HasNothingStored => DeletedRows.Count == 0;

    public static BoardDeletePageViewModel Create(Group group, BoardDeletionPreview preview, bool nameMismatch = false)
    {
        var deleted = preview.Items
            .Where(item => item.Count > 0 && Labels.ContainsKey(item.Kind))
            .Select(item =>
            {
                var labels = Labels[item.Kind];
                var names = item.Names is { Count: > 0 } ? item.Names.Select(DisplayName).ToList() : [];
                return new BoardDeleteRowViewModel
                {
                    Icon = labels.Icon,
                    Text = $"{item.Count} {(item.Count == 1 ? labels.Singular : labels.Plural)}",
                    Hint = labels.Hint,
                    SummaryText = names.Count > 0 ? labels.SummaryText : null,
                    Names = names
                };
            })
            .ToList();

        return new BoardDeletePageViewModel
        {
            Id = group.Id,
            Name = group.Name,
            CreatedAt = group.CreatedAt,
            BoardType = group.BoardType,
            DeletedRows = deleted,
            UnlinkedRows = BuildUnlinkedRows(preview),
            NameMismatch = nameMismatch
        };
    }

    private static List<BoardDeleteRowViewModel> BuildUnlinkedRows(BoardDeletionPreview preview)
    {
        const string icon = "fa-link-slash";
        var rows = new List<BoardDeleteRowViewModel>();

        var followUps = preview.FollowUpsOnOtherBoards;
        if (followUps.Count > 0)
        {
            rows.Add(new BoardDeleteRowViewModel
            {
                Icon = icon,
                Text = followUps.Count == 1
                    ? "1 follow-up quest on another board"
                    : $"{followUps.Count} follow-up quests on other boards",
                Hint = "Each loses its link to the original quest on this board.",
                SummaryText = "Show follow-up quests",
                Names = followUps.Select(f => $"{DisplayName(f.Title)} (on {f.BoardName})").ToList()
            });
        }

        if (preview.SignupsLosingCharacter > 0)
        {
            var count = preview.SignupsLosingCharacter;
            rows.Add(new BoardDeleteRowViewModel
            {
                Icon = icon,
                Text = count == 1
                    ? "1 signup on another board uses a character from this board"
                    : $"{count} signups on other boards use a character from this board",
                Hint = "Each stays signed up and loses only the character."
            });
        }

        if (preview.EventsLeavingSeries > 0)
        {
            var count = preview.EventsLeavingSeries;
            rows.Add(new BoardDeleteRowViewModel
            {
                Icon = icon,
                Text = count == 1
                    ? "1 event on another board belongs to a series on this board"
                    : $"{count} events on other boards belong to a series on this board",
                Hint = "Each stays and is detached from the series."
            });
        }

        return rows;
    }

    private static string DisplayName(string? name) => string.IsNullOrWhiteSpace(name) ? UntitledName : name;
}
