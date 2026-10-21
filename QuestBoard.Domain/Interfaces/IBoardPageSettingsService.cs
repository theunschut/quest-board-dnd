using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// The save-time rules for a board's page switches and home page. The Platform form posts what the
/// super admin chose; this decides what may actually change.
/// </summary>
public interface IBoardPageSettingsService
{
    /// <summary>
    /// Applies the posted switches and home page to the board as stored and returns the resulting
    /// settings, or the refusals that stop the save. Writes nothing: the caller saves the outcome
    /// only when <see cref="BoardPageSettingsOutcome.IsValid"/>. Switches the board's type does not
    /// allow, pages that cannot be switched, and a Quest Log switch while the quest list is
    /// unavailable are ignored and keep their stored value, whatever was posted.
    /// </summary>
    Task<BoardPageSettingsOutcome> EvaluateAsync(Group group, BoardPageSettingsRequest request, CancellationToken token = default);
}

/// <summary>
/// What the form posted. <see cref="Switches"/> maps a page to whether it is on; a page missing
/// from it is left as stored. A null <see cref="HomePage"/> leaves the stored home as it is.
/// </summary>
public sealed record BoardPageSettingsRequest(IReadOnlyDictionary<BoardPage, bool> Switches, BoardPage? HomePage);

/// <summary>
/// The settings that would result from a request, and why it cannot be saved when
/// <see cref="Errors"/> is not empty.
/// </summary>
public sealed record BoardPageSettingsOutcome(BoardPageSet Disabled, BoardPage HomePage, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
