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

    [Fact]
    public async Task ViewerWithNoMemberships_ResolvesToNothingWithoutReadingAnyBoard()
    {
        _groupService.GetGroupsForUserAsync(ViewerId, Arg.Any<CancellationToken>()).Returns(new List<GroupWithMemberCount>());

        var target = await ResolveAsync(CrossBoardAccess.Member, BoardPage.Quests);

        target.Should().BeNull();
        await _repository.DidNotReceiveWithAnyArgs().GetViewerBoardsAsync(default, default!, default);
        ThenNoLookupWasMade();
    }

    [Fact]
    public async Task OnlyTheViewersOwnMemberBoardsAreReadAndOffered()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player), Board(BoardC, GroupRole.DungeonMaster));

        await ResolveAsync(CrossBoardAccess.Member, BoardPage.Quests);

        await _repository.Received(1).GetViewerBoardsAsync(
            ViewerId,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(i => i).SequenceEqual(new[] { BoardB, BoardC })),
            Arg.Any<CancellationToken>());
        ThenLookupReceivedBoards(CrossBoardLookupKind.Quest, BoardB, BoardC);
    }

    [Theory]
    [InlineData(CrossBoardAccess.DungeonMasterTier, BoardPage.Events, CrossBoardLookupKind.Event)]
    [InlineData(CrossBoardAccess.QuestOwnerOrAdmin, BoardPage.Quests, CrossBoardLookupKind.Quest)]
    public async Task PlayerOnlyBoard_IsDroppedForARolePage_AndNothingIsLookedUp(CrossBoardAccess access, BoardPage page, CrossBoardLookupKind kind)
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player));

        var target = await ResolveAsync(access, page, kind);

        target.Should().BeNull();
        ThenNoLookupWasMade();
    }

    [Theory]
    [InlineData(CrossBoardAccess.Member, BoardPage.Quests, CrossBoardLookupKind.Quest)]
    [InlineData(CrossBoardAccess.CharacterOwnerOrAdmin, BoardPage.Characters, CrossBoardLookupKind.Character)]
    [InlineData(CrossBoardAccess.CompletedQuest, BoardPage.QuestLog, CrossBoardLookupKind.Quest)]
    [InlineData(CrossBoardAccess.ContactVisible, BoardPage.Contacts, CrossBoardLookupKind.Contact)]
    [InlineData(CrossBoardAccess.ShopItemVisible, BoardPage.Shop, CrossBoardLookupKind.ShopItem)]
    public async Task PlayerOnlyBoard_IsKeptWhenTheRuleTurnsOnTheRecordRatherThanTheRole(CrossBoardAccess access, BoardPage page, CrossBoardLookupKind kind)
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player));

        var target = await ResolveAsync(access, page, kind);

        target.Should().NotBeNull();
        target!.GroupId.Should().Be(BoardB);
        ThenLookupReceivedBoards(kind, BoardB);
    }

    [Theory]
    [InlineData(GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Admin)]
    public async Task DungeonMasterTierBoard_IsKeptForADungeonMasterPage(GroupRole role)
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player), Board(BoardC, role));

        var target = await ResolveAsync(CrossBoardAccess.DungeonMasterTier, BoardPage.Shop, CrossBoardLookupKind.ShopItem);

        target!.GroupId.Should().Be(BoardC);
        ThenLookupReceivedBoards(CrossBoardLookupKind.ShopItem, BoardC);
    }

    [Theory]
    [InlineData(CrossBoardAccess.DungeonMasterTier)]
    [InlineData(CrossBoardAccess.QuestOwnerOrAdmin)]
    [InlineData(CrossBoardAccess.AdminForOtherUser)]
    public async Task SuperAdminWhoIsAPlayerOnABoard_IsTreatedAsAnAdminThere(CrossBoardAccess access)
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player));
        var kind = access == CrossBoardAccess.AdminForOtherUser ? CrossBoardLookupKind.BoardMember : CrossBoardLookupKind.Quest;
        var page = access == CrossBoardAccess.AdminForOtherUser ? BoardPage.DungeonMasterProfile : BoardPage.Quests;
        _repository.ResolveSharedBoardIdsForUserAsync(5, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<int>>(1).ToList());

        var target = await ResolveAsync(access, page, kind, superAdmin: true);

        target.Should().NotBeNull();
        target!.GroupId.Should().Be(BoardB);
    }

    [Fact]
    public async Task SuperAdminWhoIsAPlayerOnABoard_IsOnlyAnAdminOnThatBoardNotOnOthers()
    {
        // Admin is raised only where the viewer is already a member, so the boards offered are
        // exactly the member boards and never any other.
        GivenViewerBoards(Board(BoardB, GroupRole.Player), Board(BoardC, GroupRole.Player));

        await ResolveAsync(CrossBoardAccess.DungeonMasterTier, BoardPage.Events, CrossBoardLookupKind.Event, superAdmin: true);

        await _repository.Received(1).GetViewerBoardsAsync(
            ViewerId,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(i => i).SequenceEqual(new[] { BoardB, BoardC })),
            Arg.Any<CancellationToken>());
        ThenLookupReceivedBoards(CrossBoardLookupKind.Event, BoardB, BoardC);
    }

    [Theory]
    [InlineData(CrossBoardAccess.Member)]
    [InlineData(CrossBoardAccess.DungeonMasterTier)]
    [InlineData(CrossBoardAccess.QuestOwnerOrAdmin)]
    [InlineData(CrossBoardAccess.AdminForOtherUser)]
    public async Task SuperAdminWhoIsNotAMember_ResolvesToNothingWithoutReadingAnyBoard(CrossBoardAccess access)
    {
        _groupService.GetGroupsForUserAsync(ViewerId, Arg.Any<CancellationToken>()).Returns(new List<GroupWithMemberCount>());

        var target = await ResolveAsync(access, BoardPage.Quests, superAdmin: true);

        target.Should().BeNull();
        await _repository.DidNotReceiveWithAnyArgs().GetViewerBoardsAsync(default, default!, default);
        ThenNoLookupWasMade();
    }

    [Fact]
    public async Task BoardThatSwitchedThePageOff_IsDropped_AndTheOtherBoardIsOffered()
    {
        GivenViewerBoards(
            Board(BoardB, GroupRole.Admin, disabled: BoardPageSet.Empty.With(BoardPage.Shop)),
            Board(BoardC, GroupRole.Admin));

        var target = await ResolveAsync(CrossBoardAccess.ShopItemVisible, BoardPage.Shop, CrossBoardLookupKind.ShopItem);

        target!.GroupId.Should().Be(BoardC);
        ThenLookupReceivedBoards(CrossBoardLookupKind.ShopItem, BoardC);
    }

    [Fact]
    public async Task BoardWhoseTypeNeverOffersThePage_IsDropped()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Admin, BoardType.Campaign));

        var target = await ResolveAsync(CrossBoardAccess.ShopItemVisible, BoardPage.Shop, CrossBoardLookupKind.ShopItem);

        target.Should().BeNull();
        ThenNoLookupWasMade();
    }

    [Fact]
    public async Task QuestLogPage_IsUnavailableWhenTheBoardSwitchedTheQuestListOff()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Admin, disabled: BoardPageSet.Empty.With(BoardPage.Quests)));

        var target = await ResolveAsync(CrossBoardAccess.CompletedQuest, BoardPage.QuestLog);

        target.Should().BeNull();
        ThenNoLookupWasMade();
    }

    [Fact]
    public async Task BoardThatOffersEveryPage_AdminOnIt_IsOfferedForEveryPage()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Admin));

        foreach (var page in new[] { BoardPage.Quests, BoardPage.Events, BoardPage.Characters, BoardPage.Contacts, BoardPage.QuestLog, BoardPage.Shop, BoardPage.DungeonMasterProfile })
        {
            (await ResolveAsync(CrossBoardAccess.Member, page)).Should().NotBeNull($"{page} is offered on a One-Shot board with nothing switched off");
        }
    }

    [Fact]
    public async Task LookupFindingNothing_ResolvesToNothing()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Admin));
        _repository.ResolveBoardIdAsync(Arg.Any<CrossBoardLookupKind>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);

        var target = await ResolveAsync(CrossBoardAccess.Member, BoardPage.Quests);

        target.Should().BeNull();
    }

    [Fact]
    public async Task ProfileEdit_TwoSharedBoardsOfWhichOneIsAdmin_ResolvesToThatOne()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player), Board(BoardC, GroupRole.Admin));
        SharedBoardsOfTargetAre(BoardB, BoardC);

        var target = await ResolveAsync(CrossBoardAccess.AdminForOtherUser, BoardPage.DungeonMasterProfile, CrossBoardLookupKind.BoardMember);

        target!.GroupId.Should().Be(BoardC);
        await _repository.Received(1).ResolveSharedBoardIdsForUserAsync(
            5,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { BoardC })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProfileEdit_NoBoardWhereTheViewerIsAnAdmin_ResolvesToNothing()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player), Board(BoardC, GroupRole.DungeonMaster));
        SharedBoardsOfTargetAre(BoardB, BoardC);

        var target = await ResolveAsync(CrossBoardAccess.AdminForOtherUser, BoardPage.DungeonMasterProfile, CrossBoardLookupKind.BoardMember);

        target.Should().BeNull();
        ThenNoLookupWasMade();
    }

    [Fact]
    public async Task ProfileEdit_TwoSharedAdminBoards_IsAmbiguousAndResolvesToNothing()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Admin), Board(BoardC, GroupRole.Admin));
        SharedBoardsOfTargetAre(BoardB, BoardC);

        var target = await ResolveAsync(CrossBoardAccess.AdminForOtherUser, BoardPage.DungeonMasterProfile, CrossBoardLookupKind.BoardMember);

        target.Should().BeNull();
    }

    [Fact]
    public async Task ProfileView_BoardWithoutTheProfilePage_IsDroppedBeforeTheExactlyOneRule()
    {
        GivenViewerBoards(
            Board(BoardB, GroupRole.Player, BoardType.Campaign),
            Board(BoardC, GroupRole.Player));
        SharedBoardsOfTargetAre(BoardB, BoardC);

        var target = await ResolveAsync(CrossBoardAccess.Member, BoardPage.DungeonMasterProfile, CrossBoardLookupKind.BoardMember);

        target!.GroupId.Should().Be(BoardC);
        await _repository.Received(1).ResolveSharedBoardIdsForUserAsync(
            5,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { BoardC })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProfileView_TwoSharedBoardsThatBothOfferThePage_IsAmbiguousAndResolvesToNothing()
    {
        GivenViewerBoards(Board(BoardB, GroupRole.Player), Board(BoardC, GroupRole.Player));
        SharedBoardsOfTargetAre(BoardB, BoardC);

        var target = await ResolveAsync(CrossBoardAccess.Member, BoardPage.DungeonMasterProfile, CrossBoardLookupKind.BoardMember);

        target.Should().BeNull();
    }

    // The target user's boards that are among the offered ones, as the repository would answer.
    private void SharedBoardsOfTargetAre(params int[] targetBoardIds)
        => _repository.ResolveSharedBoardIdsForUserAsync(5, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<int>)call.ArgAt<IReadOnlyCollection<int>>(1).Where(targetBoardIds.Contains).ToList());
}
