using AutoMapper;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

/// <summary>
/// Pins the "has this game night passed" rule to the board's own today. FinalizedDate is a
/// floating wall-clock value, so measuring it against a UTC instant puts a quest in or out of
/// the quest log a day early or late for every board whose zone is not UTC.
/// </summary>
public class QuestCompletionBoardClockTests
{
    private static readonly DateOnly BoardToday = new(2026, 9, 6);

    private static Quest MakeFinalizedQuest(DateTime finalizedDate) =>
        new()
        {
            Id = 1,
            Title = "The Sunken Crypt",
            Description = "A quest",
            IsFinalized = true,
            FinalizedDate = finalizedDate
        };

    [Theory]
    // Yesterday's game night and anything older has passed.
    [InlineData(2026, 9, 5, true)]
    [InlineData(2026, 9, 1, true)]
    // Tonight's game night has not -- a session still runs on the day it is dated.
    [InlineData(2026, 9, 6, false)]
    // Nor has a future one.
    [InlineData(2026, 9, 7, false)]
    public void HasFinalizedGameNightPassed_TurnsOverAtBoardLocalMidnight(int year, int month, int day, bool expected)
    {
        var quest = MakeFinalizedQuest(new DateTime(year, month, day, 18, 0, 0));

        quest.HasFinalizedGameNightPassed(BoardToday).Should().Be(expected);
    }

    [Fact]
    public void HasFinalizedGameNightPassed_IgnoresTheTimeOfDayOnTheGameNight()
    {
        // Both ends of the same board-local day answer identically: the rule is a date
        // comparison, and a wall-clock time of day carries no instant to compare against.
        MakeFinalizedQuest(new DateTime(2026, 9, 5, 0, 1, 0)).HasFinalizedGameNightPassed(BoardToday).Should().BeTrue();
        MakeFinalizedQuest(new DateTime(2026, 9, 5, 23, 59, 0)).HasFinalizedGameNightPassed(BoardToday).Should().BeTrue();
    }

    [Fact]
    public void HasFinalizedGameNightPassed_IsFalseForAQuestThatWasNeverFinalized()
    {
        var unfinalized = MakeFinalizedQuest(new DateTime(2026, 1, 1, 18, 0, 0));
        unfinalized.IsFinalized = false;

        unfinalized.HasFinalizedGameNightPassed(BoardToday).Should().BeFalse();
    }

    [Fact]
    public void HasFinalizedGameNightPassed_IsFalseForAFinalizedQuestCarryingNoDate()
    {
        var quest = MakeFinalizedQuest(new DateTime(2026, 1, 1, 18, 0, 0));
        quest.FinalizedDate = null;

        quest.HasFinalizedGameNightPassed(BoardToday).Should().BeFalse();
    }

    [Fact]
    public void HasFinalizedGameNightPassed_IsFalseForAQuestThatIsNotThere()
    {
        Quest? absent = null;

        absent.HasFinalizedGameNightPassed(BoardToday).Should().BeFalse();
    }

    /// <summary>
    /// The midnight-crossing case the UTC comparison got wrong: at 01:30 board time on the 6th,
    /// UTC still reads the 5th. The 5th's game night is over as far as the board is concerned,
    /// so the quest belongs in the quest log -- measured against UTC it would stay out of it
    /// until UTC's own midnight, hours later.
    /// </summary>
    [Fact]
    public async Task GetCompletedQuestsAsync_UsesTheBoardsOwnTodayRatherThanUtc()
    {
        var repository = Substitute.For<IQuestRepository>();
        var questFinalizedLastNight = MakeFinalizedQuest(new DateTime(2026, 9, 5, 18, 0, 0));
        repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns([questFinalizedLastNight]);

        // 2026-09-05T23:30Z is already 2026-09-06T01:30 on a board two hours ahead of UTC.
        var boardClock = new FakeBoardClock
        {
            Today = new DateOnly(2026, 9, 6),
            Now = new DateTime(2026, 9, 6, 1, 30, 0)
        };

        var sut = new QuestService(
            repository,
            Substitute.For<IPlayerSignupRepository>(),
            Substitute.For<IQuestEmailDispatcher>(),
            Substitute.For<IMapper>(),
            boardClock);

        var completed = await sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        completed.Should().ContainSingle().Which.Id.Should().Be(questFinalizedLastNight.Id);
    }

    [Fact]
    public async Task GetCompletedQuestsAsync_LeavesTonightsGameNightOffTheQuestLog()
    {
        var repository = Substitute.For<IQuestRepository>();
        repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns([MakeFinalizedQuest(new DateTime(2026, 9, 6, 18, 0, 0))]);

        var boardClock = new FakeBoardClock
        {
            Today = new DateOnly(2026, 9, 6),
            Now = new DateTime(2026, 9, 6, 1, 30, 0)
        };

        var sut = new QuestService(
            repository,
            Substitute.For<IPlayerSignupRepository>(),
            Substitute.For<IQuestEmailDispatcher>(),
            Substitute.For<IMapper>(),
            boardClock);

        var completed = await sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        completed.Should().BeEmpty();
    }
}
