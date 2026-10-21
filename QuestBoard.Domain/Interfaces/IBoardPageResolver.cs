using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Resolves which pages the active board offers. A sibling of <see cref="IBoardTypeResolver"/>
/// and kept off IActiveGroupContext for the same reason: it needs group data, and putting that on
/// the active-group context would create a circular dependency back through QuestBoardContext.
/// </summary>
public interface IBoardPageResolver
{
    /// <summary>
    /// Returns the active board's page availability, or null when no board type resolves (no
    /// active board, or it cannot be found). Callers must treat null as its own state rather than
    /// assuming a default.
    /// </summary>
    Task<BoardPageAvailability?> GetAsync(CancellationToken token = default);
}

/// <summary>
/// What one board offers: its type, the pages it switched off and the home page it stored.
/// Every answer is delegated to <see cref="BoardPagePolicy"/> so there is a single rule set.
/// </summary>
public sealed class BoardPageAvailability(BoardType boardType, BoardPageSet disabled, BoardPage storedHome)
{
    public BoardType BoardType { get; } = boardType;

    public BoardPageSet Disabled { get; } = disabled;

    public BoardPage StoredHome { get; } = storedHome;

    /// <summary>True when the page is offered on this board.</summary>
    public bool IsAvailable(BoardPage page) => BoardPagePolicy.IsAvailable(BoardType, Disabled, page);

    /// <summary>
    /// The page a visitor lands on, or null when this board offers no page that can be a home.
    /// </summary>
    public BoardPage? EffectiveHome => BoardPagePolicy.EffectiveHome(BoardType, Disabled, StoredHome);
}
