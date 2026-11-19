using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// The resolver narrows the viewer's own boards before any record is looked up, so each fact here
// asserts which board ids reach the repository's lookup (or that it is never reached), with the
// group service and the repository substituted.
public class CrossBoardLinkResolverServiceTests
{
    private const int ViewerId = 7;
    private const int BoardB = 2;
    private const int BoardC = 3;

    private readonly IGroupService _groupService = Substitute.For<IGroupService>();
    private readonly ICrossBoardLinkRepository _repository = Substitute.For<ICrossBoardLinkRepository>();
    private readonly CrossBoardLinkResolverService _sut;

    public CrossBoardLinkResolverServiceTests()
    {
        _sut = new CrossBoardLinkResolverService(_groupService, _repository);

        // The lookup answers with the first board it was offered, so the returned target also
        // shows which of the offered boards was chosen.
        _repository.ResolveBoardIdAsync(Arg.Any<CrossBoardLookupKind>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(call => (int?)call.ArgAt<IReadOnlyCollection<int>>(2).First());
    }

    private void GivenViewerBoards(params CrossBoardViewerBoard[] boards)
    {
        _groupService.GetGroupsForUserAsync(ViewerId, Arg.Any<CancellationToken>())
            .Returns(boards.Select(b => new GroupWithMemberCount { Id = b.GroupId, Name = $"Board {b.GroupId}", BoardType = b.BoardType }).ToList());
        _repository.GetViewerBoardsAsync(ViewerId, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(boards);
    }

    private static CrossBoardViewerBoard Board(int id, GroupRole role, BoardType type = BoardType.OneShot, BoardPageSet? disabled = null)
        => new(id, role, type, disabled ?? BoardPageSet.Empty);

    private Task<CrossBoardTarget?> ResolveAsync(CrossBoardAccess access, BoardPage page, CrossBoardLookupKind kind = CrossBoardLookupKind.Quest, bool superAdmin = false)
        => _sut.ResolveAsync(kind, access, page, 5, ViewerId, superAdmin, TestContext.Current.CancellationToken);

    private void ThenLookupReceivedBoards(CrossBoardLookupKind kind, params int[] expected)
        => _repository.Received(1).ResolveBoardIdAsync(
            kind,
            5,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(i => i).SequenceEqual(expected.OrderBy(i => i))),
            Arg.Any<CancellationToken>());

    private void ThenNoLookupWasMade()
    {
        _repository.DidNotReceiveWithAnyArgs().ResolveBoardIdAsync(default, default, default!, default);
        _repository.DidNotReceiveWithAnyArgs().ResolveSharedBoardIdsForUserAsync(default, default!, default);
    }

    [Fact]
    public async Task QuestOwnerOrAdmin_PlayerOnOneBoardAndDungeonMasterOnAnother_OffersOnlyTheDungeonMasterBoard()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player), Board(BoardC, GroupRole.DungeonMaster));

        var target = await ResolveAsync(CrossBoardAccess.QuestOwnerOrAdmin, BoardPage.Quests);

        target.Should().NotBeNull();
        target!.GroupId.Should().Be(BoardC);
        ThenLookupReceivedBoards(CrossBoardLookupKind.Quest, BoardC);
    }
}
