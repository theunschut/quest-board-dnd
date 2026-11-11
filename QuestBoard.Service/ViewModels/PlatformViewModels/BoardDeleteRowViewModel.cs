namespace QuestBoard.Service.ViewModels.PlatformViewModels;

/// <summary>
/// One line of the board delete confirmation page: a kind of content with how many rows of it are
/// affected, an optional hint and, for kinds that have names worth showing, the names to list.
/// </summary>
public class BoardDeleteRowViewModel
{
    /// <summary>The Font Awesome class of the row icon, such as <c>fa-scroll</c>.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>The count and its label together, such as "12 quests" or "1 quest".</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>An optional one-line explanation shown under the row.</summary>
    public string? Hint { get; init; }

    /// <summary>The text of the collapsed names list's summary; null when the row lists no names.</summary>
    public string? SummaryText { get; init; }

    /// <summary>The names to list, empty for a row that is only counted.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
}
