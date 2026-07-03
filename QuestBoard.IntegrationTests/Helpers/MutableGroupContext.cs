using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Settable implementation of IActiveGroupContext for integration tests.
/// Defaults to GroupId = 1 (EuphoriaInn seed group). Tests override as needed.
/// Registered as Singleton in WebApplicationFactoryBase so test code can mutate it directly.
/// </summary>
public class MutableGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; } = 1;

    /// <summary>
    /// Settable BoardType for nav-visibility and board-type-gating tests.
    /// Defaults to OneShot so existing nav-visible tests stay green without modification.
    /// </summary>
    public BoardType? BoardType { get; set; } = QuestBoard.Domain.Enums.BoardType.OneShot;

    public Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default) => Task.FromResult(BoardType);
}
