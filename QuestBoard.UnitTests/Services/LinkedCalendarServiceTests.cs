using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// This class protects the fail-closed behaviour of the overlay that puts the other boards of a
// linked set on a calendar: nothing is read across boards when nothing is linked, the ids and
// window handed to both reads are exactly the linked set and the shown month, a row from outside
// the set is dropped and logged before anything renders, a campaign set never fetches quests,
// and a DM-only session follows the viewer's role on the board that owns it.
public class LinkedCalendarServiceTests
{
    private const int ActiveBoard = 1;
    private const int Viewer = 42;
    private static readonly DateTime From = new(2031, 3, 1);
    private static readonly DateTime To = new(2031, 4, 1);

    // Hand-rolled rather than substituted: LinkedCalendarService is internal, so a dynamic proxy
    // over ILogger<LinkedCalendarService> cannot be generated, and recording the entries
    // directly is what the assertions below want anyway.
    private sealed class RecordingLogger : ILogger<LinkedCalendarService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private sealed class Fixture
    {
        public ILinkedBoardSetRepository Sets { get; } = Substitute.For<ILinkedBoardSetRepository>();
        public IQuestRepository Quests { get; } = Substitute.For<IQuestRepository>();
        public IEventRepository Events { get; } = Substitute.For<IEventRepository>();
        public RecordingLogger Logger { get; } = new();

        public Fixture()
        {
            Events.GetLinkedEventsForCalendarAsync(default!, default, default, default)
                .ReturnsForAnyArgs(Task.FromResult<IList<LinkedEventEntry>>([]));
            Quests.GetLinkedQuestsForCalendarAsync(default!, default, default, default)
                .ReturnsForAnyArgs(Task.FromResult<IList<LinkedQuestEntry>>([]));
        }

        public LinkedCalendarService Service => new(Sets, Quests, Events, Logger);

        public void LinkedBoardsAre(BoardType setType, params LinkedBoard[] boards)
        {
            Sets.GetLinkedBoardsForViewerAsync(ActiveBoard, Viewer, Arg.Any<CancellationToken>())
                .Returns(new ViewerLinkedBoards { SetBoardType = setType, Boards = boards });
        }

        public Task<LinkedCalendarData> GetAsync(bool superAdmin = false) =>
            Service.GetForViewerAsync(ActiveBoard, Viewer, superAdmin, From, To, TestContext.Current.CancellationToken);

        public void ReadsReturn(IList<LinkedEventEntry> events, IList<LinkedQuestEntry> quests)
        {
            Events.GetLinkedEventsForCalendarAsync(default!, default, default, default)
                .ReturnsForAnyArgs(Task.FromResult(events));
            Quests.GetLinkedQuestsForCalendarAsync(default!, default, default, default)
                .ReturnsForAnyArgs(Task.FromResult(quests));
        }

        public async Task AssertNoReadsAsync()
        {
            await Events.DidNotReceiveWithAnyArgs().GetLinkedEventsForCalendarAsync(default!, default, default, default);
            await Quests.DidNotReceiveWithAnyArgs().GetLinkedQuestsForCalendarAsync(default!, default, default, default);
        }
    }

    private static LinkedBoard Board(int id, string name, GroupRole? viewerRole = null) =>
        new() { Id = id, Name = name, ViewerRole = viewerRole };

    private static LinkedEventEntry EventRow(int id, int groupId) =>
        new() { EventId = id, GroupId = groupId, Title = $"Event {id}", Date = new DateOnly(2031, 3, 10) };

    private static LinkedQuestEntry QuestRow(int id, int groupId, bool dmOnly = false) =>
        new()
        {
            QuestId = id,
            GroupId = groupId,
            Title = $"Quest {id}",
            DungeonMasterName = "Dana",
            DungeonMasterSession = dmOnly,
            ProposedDates = [new DateTime(2031, 3, 10, 19, 0, 0)]
        };

    [Fact]
    public async Task GetForViewer_NoActiveBoard_ReturnsEmptyAndCallsNothing()
    {
        var f = new Fixture();

        var result = await f.Service.GetForViewerAsync(null, Viewer, false, From, To, TestContext.Current.CancellationToken);

        result.Boards.Should().BeEmpty();
        result.Events.Should().BeEmpty();
        result.Quests.Should().BeEmpty();
        await f.Sets.DidNotReceiveWithAnyArgs().GetLinkedBoardsForViewerAsync(default, default, default);
        await f.AssertNoReadsAsync();
    }

    [Fact]
    public async Task GetForViewer_NoViewer_ReturnsEmptyAndCallsNothing()
    {
        var f = new Fixture();

        var result = await f.Service.GetForViewerAsync(ActiveBoard, null, false, From, To, TestContext.Current.CancellationToken);

        result.Boards.Should().BeEmpty();
        await f.Sets.DidNotReceiveWithAnyArgs().GetLinkedBoardsForViewerAsync(default, default, default);
        await f.AssertNoReadsAsync();
    }

    [Fact]
    public async Task GetForViewer_NoOtherBoardInTheSet_ReturnsEmptyWithoutAnyCrossBoardRead()
    {
        var f = new Fixture();
        f.Sets.GetLinkedBoardsForViewerAsync(ActiveBoard, Viewer, Arg.Any<CancellationToken>())
            .Returns(ViewerLinkedBoards.Empty);

        var result = await f.GetAsync();

        result.Boards.Should().BeEmpty();
        result.Events.Should().BeEmpty();
        result.Quests.Should().BeEmpty();
        await f.AssertNoReadsAsync();
    }

    [Fact]
    public async Task GetForViewer_PassesExactlyTheLinkedIdsAndTheWindowToBothReads()
    {
        var f = new Fixture();
        f.LinkedBoardsAre(BoardType.OneShot, Board(2, "Bravo"), Board(5, "Echo"));

        await f.GetAsync();

        await f.Events.Received(1).GetLinkedEventsForCalendarAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 2, 5 })),
            DateOnly.FromDateTime(From),
            DateOnly.FromDateTime(To),
            Arg.Any<CancellationToken>());
        await f.Quests.Received(1).GetLinkedQuestsForCalendarAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 2, 5 })),
            From,
            To,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetForViewer_RowFromOutsideTheLinkedSet_IsDroppedAndLoggedAsAnError()
    {
        var f = new Fixture();
        f.LinkedBoardsAre(BoardType.OneShot, Board(2, "Bravo"));
        f.ReadsReturn(
            [EventRow(10, 2), EventRow(11, 3)],
            [QuestRow(20, 2), QuestRow(21, 3), QuestRow(22, 3)]);

        var result = await f.GetAsync();

        result.Events.Select(e => e.EventId).Should().Equal(10);
        result.Quests.Select(q => q.QuestId).Should().Equal(20);
        var errors = f.Logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.Message.Contains("dropped 1 of 2 event"));
        errors.Should().Contain(e => e.Message.Contains("dropped 2 of 3 quest"));
    }

    [Fact]
    public async Task GetForViewer_RowsAllInsideTheSet_LogNothing()
    {
        var f = new Fixture();
        f.LinkedBoardsAre(BoardType.OneShot, Board(2, "Bravo"));
        f.ReadsReturn([EventRow(10, 2)], [QuestRow(20, 2)]);

        await f.GetAsync();

        f.Logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForViewer_CampaignSet_NeverReadsQuestsButStillReadsEvents()
    {
        var f = new Fixture();
        f.LinkedBoardsAre(BoardType.Campaign, Board(2, "Bravo"));
        f.ReadsReturn([EventRow(10, 2)], [QuestRow(20, 2)]);

        var result = await f.GetAsync();

        await f.Quests.DidNotReceiveWithAnyArgs().GetLinkedQuestsForCalendarAsync(default!, default, default, default);
        await f.Events.ReceivedWithAnyArgs(1).GetLinkedEventsForCalendarAsync(default!, default, default, default);
        result.Events.Should().HaveCount(1);
        result.Quests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(GroupRole.Player, false, false)]
    [InlineData(GroupRole.DungeonMaster, false, true)]
    [InlineData(GroupRole.Admin, false, true)]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    public async Task GetForViewer_DmOnlyQuest_IsKeptOnlyForTheOwningBoardsDmTierOrASiteAdmin(
        GroupRole? roleOnOwningBoard, bool superAdmin, bool expectedKept)
    {
        var f = new Fixture();
        f.LinkedBoardsAre(BoardType.OneShot, Board(2, "Bravo", roleOnOwningBoard));
        f.ReadsReturn([], [QuestRow(20, 2, dmOnly: true), QuestRow(21, 2)]);

        var result = await f.GetAsync(superAdmin);

        result.Quests.Select(q => q.QuestId).Should().Contain(21);
        result.Quests.Any(q => q.QuestId == 20).Should().Be(expectedKept);
    }

    [Fact]
    public async Task GetForViewer_DmOnlyQuestDecision_UsesTheRoleOnTheBoardThatOwnsIt()
    {
        var f = new Fixture();
        f.LinkedBoardsAre(
            BoardType.OneShot,
            Board(2, "Bravo", GroupRole.DungeonMaster),
            Board(3, "Charlie", GroupRole.Player));
        f.ReadsReturn([], [QuestRow(20, 2, dmOnly: true), QuestRow(30, 3, dmOnly: true)]);

        var result = await f.GetAsync();

        result.Quests.Select(q => q.QuestId).Should().Equal(20);
    }

    [Fact]
    public async Task GetForViewer_StampsBoardNameAndMembershipFromTheOwningBoard_AndListsEveryBoard()
    {
        var f = new Fixture();
        f.LinkedBoardsAre(
            BoardType.OneShot,
            Board(2, "Bravo", GroupRole.Player),
            Board(3, "Charlie"),
            Board(4, "Delta"));
        f.ReadsReturn([EventRow(10, 2), EventRow(11, 3)], [QuestRow(20, 3)]);

        var result = await f.GetAsync();

        result.Boards.Select(b => b.Name).Should().Equal("Bravo", "Charlie", "Delta");
        var bravoEvent = result.Events.Single(e => e.EventId == 10);
        bravoEvent.BoardName.Should().Be("Bravo");
        bravoEvent.ViewerIsMember.Should().BeTrue();
        var charlieEvent = result.Events.Single(e => e.EventId == 11);
        charlieEvent.BoardName.Should().Be("Charlie");
        charlieEvent.ViewerIsMember.Should().BeFalse();
        var charlieQuest = result.Quests.Single();
        charlieQuest.BoardName.Should().Be("Charlie");
        charlieQuest.ViewerIsMember.Should().BeFalse();
    }
}
