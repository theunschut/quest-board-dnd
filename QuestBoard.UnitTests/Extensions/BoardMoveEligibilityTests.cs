using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.UnitTests.Extensions;

public class BoardMoveEligibilityTests
{
    private static readonly DateOnly Today = new(2031, 6, 15);

    private static Quest MakeQuest(bool isClosed = false, DateOnly? finalizedOn = null) => new()
    {
        Id = 1,
        Title = "Goblin Market",
        Description = "D",
        IsClosed = isClosed,
        IsFinalized = finalizedOn != null,
        FinalizedDate = finalizedOn?.ToDateTime(new TimeOnly(19, 0))
    };

    private static Event MakeEvent(DateOnly date, int? seriesId = null, bool cancelled = false) => new()
    {
        Id = 1,
        Title = "Game night",
        Date = date,
        SeriesId = seriesId,
        CancelledAt = cancelled ? new DateTime(2031, 6, 1, 12, 0, 0, DateTimeKind.Utc) : null
    };

    [Fact]
    public void ForQuest_OpenQuest_IsMovable()
    {
        MoveEligibility.ForQuest(MakeQuest(), Today).Should().Be(MoveBlockReason.None);
    }

    [Fact]
    public void ForQuest_ClosedQuest_IsRefusedAsClosed()
    {
        MoveEligibility.ForQuest(MakeQuest(isClosed: true), Today).Should().Be(MoveBlockReason.Closed);
    }

    [Fact]
    public void ForQuest_ClosedAndPlayedQuest_ReportsClosedBecauseClosedIsCheckedFirst()
    {
        var quest = MakeQuest(isClosed: true, finalizedOn: Today.AddDays(-30));

        MoveEligibility.ForQuest(quest, Today).Should().Be(MoveBlockReason.Closed);
    }

    [Fact]
    public void ForQuest_FinalizedYesterday_IsRefusedAsPlayed()
    {
        MoveEligibility.ForQuest(MakeQuest(finalizedOn: Today.AddDays(-1)), Today)
            .Should().Be(MoveBlockReason.Played);
    }

    [Fact]
    public void ForQuest_FinalizedToday_IsMovable()
    {
        MoveEligibility.ForQuest(MakeQuest(finalizedOn: Today), Today).Should().Be(MoveBlockReason.None);
    }

    [Fact]
    public void ForQuest_FinalizedInTheFuture_IsMovable()
    {
        MoveEligibility.ForQuest(MakeQuest(finalizedOn: Today.AddDays(10)), Today).Should().Be(MoveBlockReason.None);
    }

    [Fact]
    public void ForEvent_SeriesOccurrence_IsRefused()
    {
        MoveEligibility.ForEvent(MakeEvent(Today.AddDays(5), seriesId: 7), Today)
            .Should().Be(MoveBlockReason.SeriesOccurrence);
    }

    [Fact]
    public void ForEvent_PastSeriesOccurrence_ReportsSeriesBecauseSeriesIsCheckedFirst()
    {
        MoveEligibility.ForEvent(MakeEvent(Today.AddDays(-5), seriesId: 7), Today)
            .Should().Be(MoveBlockReason.SeriesOccurrence);
    }

    [Fact]
    public void ForEvent_DatedBeforeToday_IsRefusedAsPast()
    {
        MoveEligibility.ForEvent(MakeEvent(Today.AddDays(-1)), Today).Should().Be(MoveBlockReason.Past);
    }

    [Fact]
    public void ForEvent_DatedToday_IsMovable()
    {
        MoveEligibility.ForEvent(MakeEvent(Today), Today).Should().Be(MoveBlockReason.None);
    }

    [Fact]
    public void ForEvent_CancelledAndInTheFuture_IsMovable()
    {
        MoveEligibility.ForEvent(MakeEvent(Today.AddDays(3), cancelled: true), Today)
            .Should().Be(MoveBlockReason.None);
    }
}
