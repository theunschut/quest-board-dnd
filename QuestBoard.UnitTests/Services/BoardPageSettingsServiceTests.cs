using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

public class BoardPageSettingsServiceTests
{
    private const int GroupId = 7;

    private static readonly DateOnly Today = new(2031, 6, 15);

    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IQuestRepository _quests = Substitute.For<IQuestRepository>();
    private readonly FakeBoardClock _clock = new() { Today = Today };
    private readonly BoardPageSettingsService _sut;

    public BoardPageSettingsServiceTests()
    {
        _events.CountUpcomingForBoardAsync(default, default, default).ReturnsForAnyArgs(new UpcomingEventCount(0, 0));
        _events.CountActiveSeriesForBoardAsync(default, default, default).ReturnsForAnyArgs(0);
        _quests.CountLiveForBoardAsync(default, default, default).ReturnsForAnyArgs(0);
        _sut = new BoardPageSettingsService(_events, _quests, _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Group Board(
        BoardType type = BoardType.OneShot,
        BoardPageSet? disabled = null,
        BoardPage home = BoardPage.Quests) => new()
    {
        Id = GroupId,
        Name = "Board",
        BoardType = type,
        DisabledPages = disabled ?? BoardPageSet.Empty,
        HomePage = home
    };

    private static BoardPageSettingsRequest Request(BoardPage? home = null, params (BoardPage Page, bool On)[] switches) =>
        new(switches.ToDictionary(s => s.Page, s => s.On), home);

    private Task<BoardPageSettingsOutcome> Evaluate(Group group, BoardPageSettingsRequest request) =>
        _sut.EvaluateAsync(group, request, Token);

    // -------------------------------------------------------------------
    // Switches
    // -------------------------------------------------------------------

    [Fact]
    public async Task OneShot_SwitchShopOff_DisablesShop()
    {
        var outcome = await Evaluate(Board(), Request(null, (BoardPage.Shop, false)));

        Assert.True(outcome.IsValid);
        Assert.True(outcome.Disabled.Contains(BoardPage.Shop));
        Assert.Equal(BoardPage.Quests, outcome.HomePage);
    }

    [Fact]
    public async Task OneShot_SwitchShopOn_ClearsTheStoredBit()
    {
        var outcome = await Evaluate(Board(disabled: BoardPageSet.Empty.With(BoardPage.Shop)), Request(null, (BoardPage.Shop, true)));

        Assert.True(outcome.IsValid);
        Assert.False(outcome.Disabled.Contains(BoardPage.Shop));
    }

    [Fact]
    public async Task Campaign_PostedSwitchesForTypeDisallowedPages_AreIgnored()
    {
        var stored = BoardPageSet.Empty.With(BoardPage.Contacts);
        var outcome = await Evaluate(
            Board(BoardType.Campaign, stored),
            Request(null, (BoardPage.Shop, true), (BoardPage.Players, false), (BoardPage.DungeonMasterProfile, false)));

        Assert.True(outcome.IsValid);
        Assert.Equal(stored, outcome.Disabled);
    }

    [Fact]
    public async Task Campaign_PostedSwitchOnForTypeDisallowedPageWithStoredBitSet_KeepsTheBit()
    {
        var stored = BoardPageSet.Empty.With(BoardPage.Players);
        var outcome = await Evaluate(Board(BoardType.Campaign, stored), Request(null, (BoardPage.Players, true)));

        Assert.True(outcome.Disabled.Contains(BoardPage.Players));
    }

    [Fact]
    public async Task PageMissingFromTheRequest_KeepsItsStoredBit()
    {
        var stored = BoardPageSet.Empty.With(BoardPage.Contacts).With(BoardPage.Shop);
        var outcome = await Evaluate(Board(disabled: stored), Request(null, (BoardPage.Calendar, false)));

        Assert.True(outcome.Disabled.Contains(BoardPage.Contacts));
        Assert.True(outcome.Disabled.Contains(BoardPage.Shop));
        Assert.True(outcome.Disabled.Contains(BoardPage.Calendar));
    }

    [Fact]
    public async Task NullHomePage_KeepsTheStoredHome()
    {
        var outcome = await Evaluate(Board(home: BoardPage.Calendar), Request(null, (BoardPage.Shop, false)));

        Assert.True(outcome.IsValid);
        Assert.Equal(BoardPage.Calendar, outcome.HomePage);
    }

    [Fact]
    public async Task UndefinedPageValue_IsIgnored()
    {
        var stored = BoardPageSet.Empty.With(BoardPage.Shop);
        var outcome = await Evaluate(Board(disabled: stored), Request(null, ((BoardPage)42, false)));

        Assert.True(outcome.IsValid);
        Assert.Equal(stored, outcome.Disabled);
    }

    [Fact]
    public async Task QuestLogSwitchOn_WhileQuestsIsUnavailable_KeepsTheStoredQuestLogBit()
    {
        var stored = BoardPageSet.Empty.With(BoardPage.Quests).With(BoardPage.QuestLog);
        var outcome = await Evaluate(Board(disabled: stored, home: BoardPage.Calendar), Request(null, (BoardPage.QuestLog, true)));

        Assert.True(outcome.Disabled.Contains(BoardPage.QuestLog));
    }

    [Fact]
    public async Task QuestLogSwitchOff_WhileQuestsIsAvailable_IsApplied()
    {
        var outcome = await Evaluate(Board(), Request(null, (BoardPage.QuestLog, false)));

        Assert.True(outcome.Disabled.Contains(BoardPage.QuestLog));
    }

    // -------------------------------------------------------------------
    // Home page
    // -------------------------------------------------------------------

    [Fact]
    public async Task HomePageCalendar_WhenCalendarIsOn_BecomesTheHome()
    {
        var outcome = await Evaluate(Board(), Request(BoardPage.Calendar));

        Assert.True(outcome.IsValid);
        Assert.Equal(BoardPage.Calendar, outcome.HomePage);
    }

    [Fact]
    public async Task HomePageDungeonMasterProfile_IsRefused()
    {
        var outcome = await Evaluate(Board(), Request(BoardPage.DungeonMasterProfile));

        Assert.Equal(["Choose a home page from the list."], outcome.Errors);
    }

    [Fact]
    public async Task HomePageUndefinedValue_IsRefused()
    {
        var outcome = await Evaluate(Board(), Request((BoardPage)42));

        Assert.Equal(["Choose a home page from the list."], outcome.Errors);
    }

    [Fact]
    public async Task HomePageShop_OnCampaign_IsRefusedAsUnavailable()
    {
        var outcome = await Evaluate(Board(BoardType.Campaign), Request(BoardPage.Shop));

        Assert.Equal(["Shop can't be the home page because it isn't available on this board."], outcome.Errors);
    }

    [Fact]
    public async Task HomePageCalendar_WhileSwitchingCalendarOffInTheSameRequest_IsRefusedAsUnavailable()
    {
        var outcome = await Evaluate(Board(), Request(BoardPage.Calendar, (BoardPage.Calendar, false)));

        Assert.Equal(["Calendar can't be the home page because it isn't available on this board."], outcome.Errors);
    }

    [Fact]
    public async Task SwitchingOffTheStoredHome_WithHomeUnchanged_IsRefused()
    {
        var outcome = await Evaluate(Board(home: BoardPage.Calendar), Request(null, (BoardPage.Calendar, false)));

        Assert.Equal(["Calendar is this board's home page; pick another home page first."], outcome.Errors);
    }

    [Fact]
    public async Task SwitchingOffTheStoredHome_WithTheSameHomePosted_IsRefused()
    {
        var outcome = await Evaluate(Board(home: BoardPage.Calendar), Request(BoardPage.Calendar, (BoardPage.Calendar, false)));

        Assert.Equal(["Calendar is this board's home page; pick another home page first."], outcome.Errors);
    }

    [Fact]
    public async Task SwitchingOffTheStoredHome_WhileMovingTheHome_IsAccepted()
    {
        var outcome = await Evaluate(Board(home: BoardPage.Calendar), Request(BoardPage.Events, (BoardPage.Calendar, false)));

        Assert.True(outcome.IsValid);
        Assert.Equal(BoardPage.Events, outcome.HomePage);
        Assert.True(outcome.Disabled.Contains(BoardPage.Calendar));
    }

    [Fact]
    public async Task StoredHomeAlreadyUnavailable_UnrelatedSaveStillSucceeds()
    {
        var outcome = await Evaluate(
            Board(disabled: BoardPageSet.Empty.With(BoardPage.Calendar), home: BoardPage.Calendar),
            Request(null, (BoardPage.Shop, false)));

        Assert.True(outcome.IsValid);
        Assert.Equal(BoardPage.Calendar, outcome.HomePage);
    }

    // -------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------

    [Fact]
    public async Task SwitchingEventsOff_WithUpcomingAndCancelledEvents_IsRefusedWithBothCounts()
    {
        _events.CountUpcomingForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>()).Returns(new UpcomingEventCount(2, 1));

        var outcome = await Evaluate(Board(), Request(null, (BoardPage.Events, false)));

        Assert.Equal(
            ["Events can't be switched off while this board has 2 upcoming events (1 of them cancelled). Delete them, move them to a linked board, or wait until they have passed."],
            outcome.Errors);
    }

    [Fact]
    public async Task SwitchingEventsOff_WithOneUpcomingEvent_UsesTheSingularAndNoCancelledNote()
    {
        _events.CountUpcomingForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>()).Returns(new UpcomingEventCount(1, 0));

        var outcome = await Evaluate(Board(), Request(null, (BoardPage.Events, false)));

        Assert.Equal(
            ["Events can't be switched off while this board has 1 upcoming event. Delete them, move them to a linked board, or wait until they have passed."],
            outcome.Errors);
    }

    [Fact]
    public async Task SwitchingEventsOff_WithRunningSeries_IsRefusedTellingTheAdminToEndThem()
    {
        _events.CountActiveSeriesForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>()).Returns(2);

        var outcome = await Evaluate(Board(), Request(null, (BoardPage.Events, false)));

        Assert.Equal(
            ["Events can't be switched off while 2 recurring series still produce events on this board. End each series first."],
            outcome.Errors);
    }

    [Fact]
    public async Task SwitchingEventsOff_WithEventsAndSeries_ReportsBothRefusals()
    {
        _events.CountUpcomingForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>()).Returns(new UpcomingEventCount(3, 0));
        _events.CountActiveSeriesForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>()).Returns(1);

        var outcome = await Evaluate(Board(), Request(null, (BoardPage.Events, false)));

        Assert.Equal(2, outcome.Errors.Count);
    }

    [Fact]
    public async Task SwitchingEventsOff_WithNothingUpcomingAndNoSeries_IsAccepted()
    {
        var outcome = await Evaluate(Board(), Request(null, (BoardPage.Events, false)));

        Assert.True(outcome.IsValid);
        Assert.True(outcome.Disabled.Contains(BoardPage.Events));
    }

    [Fact]
    public async Task EventsAlreadyOff_NoCountIsTaken()
    {
        var outcome = await Evaluate(
            Board(disabled: BoardPageSet.Empty.With(BoardPage.Events)),
            Request(null, (BoardPage.Events, false)));

        Assert.True(outcome.IsValid);
        await _events.DidNotReceiveWithAnyArgs().CountUpcomingForBoardAsync(default, default, default);
        await _events.DidNotReceiveWithAnyArgs().CountActiveSeriesForBoardAsync(default, default, default);
    }

    [Fact]
    public async Task EventsLeftOn_NoCountIsTaken()
    {
        await Evaluate(Board(), Request(null, (BoardPage.Shop, false)));

        await _events.DidNotReceiveWithAnyArgs().CountUpcomingForBoardAsync(default, default, default);
        await _events.DidNotReceiveWithAnyArgs().CountActiveSeriesForBoardAsync(default, default, default);
    }

    [Fact]
    public async Task Counts_UseTheEditedBoardsIdAndTheBoardClockToday()
    {
        await Evaluate(Board(), Request(null, (BoardPage.Events, false)));

        await _events.Received(1).CountUpcomingForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>());
        await _events.Received(1).CountActiveSeriesForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------
    // Quests
    // -------------------------------------------------------------------

    [Fact]
    public async Task SwitchingQuestsOff_WithNoLiveQuests_AndAnotherHome_IsAccepted()
    {
        var outcome = await Evaluate(Board(), Request(BoardPage.Calendar, (BoardPage.Quests, false)));

        Assert.True(outcome.IsValid);
        Assert.True(outcome.Disabled.Contains(BoardPage.Quests));
        Assert.Equal(BoardPage.Calendar, outcome.HomePage);
    }

    [Fact]
    public async Task SwitchingQuestsOff_WithLiveQuests_IsRefusedWithThePluralCount()
    {
        _quests.CountLiveForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>()).Returns(2);

        var outcome = await Evaluate(Board(), Request(BoardPage.Calendar, (BoardPage.Quests, false)));

        Assert.Equal(
            ["Quests can't be switched off while this board has 2 live quests. Close or delete them first."],
            outcome.Errors);
    }

    [Fact]
    public async Task SwitchingQuestsOff_WithOneLiveQuest_UsesTheSingular()
    {
        _quests.CountLiveForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>()).Returns(1);

        var outcome = await Evaluate(Board(), Request(BoardPage.Calendar, (BoardPage.Quests, false)));

        Assert.Equal(
            ["Quests can't be switched off while this board has 1 live quest. Close or delete them first."],
            outcome.Errors);
    }

    [Fact]
    public async Task SwitchingQuestsOff_WhileTheQuestListIsTheHome_WithoutPickingAnother_IsRefused()
    {
        var outcome = await Evaluate(Board(home: BoardPage.Quests), Request(null, (BoardPage.Quests, false)));

        Assert.Equal(["Quest list is this board's home page; pick another home page first."], outcome.Errors);
    }

    [Fact]
    public async Task SwitchingQuestsOff_KeepsTheStoredQuestLogBit_WhateverTheQuestLogSwitchSays()
    {
        var stored = BoardPageSet.Empty.With(BoardPage.QuestLog);
        var outcome = await Evaluate(
            Board(disabled: stored, home: BoardPage.Calendar),
            Request(null, (BoardPage.Quests, false), (BoardPage.QuestLog, true)));

        Assert.True(outcome.IsValid);
        Assert.True(outcome.Disabled.Contains(BoardPage.Quests));
        Assert.True(outcome.Disabled.Contains(BoardPage.QuestLog));
    }

    [Fact]
    public async Task SwitchingQuestsBackOn_RestoresTheQuestLogSettingAsItWas()
    {
        var stored = BoardPageSet.Empty.With(BoardPage.Quests).With(BoardPage.QuestLog);
        var outcome = await Evaluate(Board(disabled: stored, home: BoardPage.Calendar), Request(null, (BoardPage.Quests, true)));

        Assert.True(outcome.IsValid);
        Assert.False(outcome.Disabled.Contains(BoardPage.Quests));
        Assert.True(outcome.Disabled.Contains(BoardPage.QuestLog));
        await _quests.DidNotReceiveWithAnyArgs().CountLiveForBoardAsync(default, default, default);
    }

    [Fact]
    public async Task QuestsAlreadyOff_NoCountIsTaken()
    {
        var outcome = await Evaluate(
            Board(disabled: BoardPageSet.Empty.With(BoardPage.Quests), home: BoardPage.Calendar),
            Request(null, (BoardPage.Quests, false)));

        Assert.True(outcome.IsValid);
        await _quests.DidNotReceiveWithAnyArgs().CountLiveForBoardAsync(default, default, default);
    }

    [Fact]
    public async Task QuestsLeftOn_NoCountIsTaken()
    {
        await Evaluate(Board(), Request(null, (BoardPage.Shop, false)));

        await _quests.DidNotReceiveWithAnyArgs().CountLiveForBoardAsync(default, default, default);
    }

    [Fact]
    public async Task LiveQuestCount_UsesTheEditedBoardsIdAndTheBoardClockToday()
    {
        await Evaluate(Board(), Request(BoardPage.Calendar, (BoardPage.Quests, false)));

        await _quests.Received(1).CountLiveForBoardAsync(GroupId, Today, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_WritesNothing()
    {
        var group = Board();

        await Evaluate(group, Request(BoardPage.Calendar, (BoardPage.Shop, false)));

        Assert.Equal(BoardPageSet.Empty, group.DisabledPages);
        Assert.Equal(BoardPage.Quests, group.HomePage);
    }
}
