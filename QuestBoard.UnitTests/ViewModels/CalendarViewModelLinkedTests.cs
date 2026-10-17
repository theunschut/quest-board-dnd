using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.ViewModels.CalendarViewModels;

namespace QuestBoard.UnitTests.ViewModels;

// Proves how the calendar view model places entries that belong to other boards. A linked quest
// must land on exactly the days and times an own quest with the same data would, and within a
// day the linked entries keep a stable, readable order. Placement is the one thing the shared
// calendar cannot show wrongly without misleading someone about when a game happens.
public class CalendarViewModelLinkedTests
{
    private const int Year = 2031;
    private const int Month = 3;

    private static DateTime At(int day, int hour = 19) => new(Year, Month, day, hour, 0, 0);

    // An own quest and a linked entry built from the same data: title, status, proposed
    // dates and finalized date.
    private static (Quest Own, LinkedQuestEntry Linked) QuestPair(
        bool finalized, DateTime? finalizedDate, params DateTime[] proposed)
    {
        var own = new Quest
        {
            Id = 1,
            Title = "Crypt Run",
            IsFinalized = finalized,
            FinalizedDate = finalizedDate,
            ProposedDates = [.. proposed.Select((date, index) => new ProposedDate { Id = index + 1, QuestId = 1, Date = date })]
        };
        var linked = new LinkedQuestEntry
        {
            QuestId = 1,
            Title = "Crypt Run",
            IsFinalized = finalized,
            FinalizedDate = finalizedDate,
            ProposedDates = [.. proposed]
        };
        return (own, linked);
    }

    private static CalendarViewModel ModelWith(Quest? own = null, LinkedQuestEntry? linked = null) => new()
    {
        Year = Year,
        Month = Month,
        Quests = own == null ? [] : [own],
        LinkedQuests = linked == null ? [] : [linked]
    };

    // Every real day of the month with the number of entries and the times placed on it.
    private static List<(int Day, string Times)> OwnPlacement(CalendarViewModel model) =>
        [.. model.GetCalendarDays()
            .Where(d => !d.IsEmpty && d.QuestsOnDay.Count > 0)
            .Select(d => (d.Day, string.Join(",", d.QuestsOnDay.Select(q => $"{q.ProposedDate.Date:HH:mm}/{q.IsFinalized}"))))];

    private static List<(int Day, string Times)> LinkedPlacement(CalendarViewModel model) =>
        [.. model.GetCalendarDays()
            .Where(d => !d.IsEmpty && d.Linked.Quests.Count > 0)
            .Select(d => (d.Day, string.Join(",", d.Linked.Quests.Select(q => $"{q.When:HH:mm}/{q.IsFinalized}"))))];

    [Fact]
    public void LinkedQuest_OpenWithTwoProposedDatesOnDifferentDays_IsPlacedLikeAnOwnQuest()
    {
        var (own, linked) = QuestPair(false, null, At(10), At(17, 20));

        var ownPlacement = OwnPlacement(ModelWith(own: own));
        var linkedPlacement = LinkedPlacement(ModelWith(linked: linked));

        ownPlacement.Should().HaveCount(2);
        linkedPlacement.Should().Equal(ownPlacement);
    }

    [Fact]
    public void LinkedQuest_OpenWithTwoProposedDatesOnTheSameDay_ProducesTwoEntriesLikeAnOwnQuest()
    {
        var (own, linked) = QuestPair(false, null, At(10, 18), At(10, 20));

        var ownPlacement = OwnPlacement(ModelWith(own: own));
        var linkedPlacement = LinkedPlacement(ModelWith(linked: linked));

        ownPlacement.Should().ContainSingle().Which.Times.Should().Be("18:00/False,20:00/False");
        linkedPlacement.Should().Equal(ownPlacement);
    }

    [Fact]
    public void LinkedQuest_FinalizedOnADayWithAProposedDate_IsPlacedOnceAsFinalizedLikeAnOwnQuest()
    {
        var (own, linked) = QuestPair(true, At(12), At(12), At(14));

        var ownPlacement = OwnPlacement(ModelWith(own: own));
        var linkedPlacement = LinkedPlacement(ModelWith(linked: linked));

        ownPlacement.Should().ContainSingle().Which.Should().Be((12, "19:00/True"));
        linkedPlacement.Should().Equal(ownPlacement);
    }

    [Fact]
    public void LinkedQuest_FinalizedOnADayWithNoProposedDate_IsPlacedNowhereLikeAnOwnQuest()
    {
        var (own, linked) = QuestPair(true, At(13), At(12), At(14));

        var ownPlacement = OwnPlacement(ModelWith(own: own));
        var linkedPlacement = LinkedPlacement(ModelWith(linked: linked));

        ownPlacement.Should().BeEmpty();
        linkedPlacement.Should().BeEmpty();
    }

    [Fact]
    public void LinkedQuest_ProposedDateInAnotherMonth_IsNotPlacedInTheShownMonth()
    {
        var (own, linked) = QuestPair(false, null, new DateTime(Year, Month + 1, 2, 19, 0, 0));

        OwnPlacement(ModelWith(own: own)).Should().BeEmpty();
        LinkedPlacement(ModelWith(linked: linked)).Should().BeEmpty();
    }

    [Fact]
    public void LinkedEvents_OnOneDay_PutAllDayFirstThenByTimeThenByTitle()
    {
        var model = new CalendarViewModel
        {
            Year = Year,
            Month = Month,
            LinkedEvents =
            [
                new LinkedEventEntry { EventId = 1, Title = "Late Night", Date = new DateOnly(Year, Month, 5), StartTime = new TimeOnly(21, 0) },
                new LinkedEventEntry { EventId = 2, Title = "beta Early", Date = new DateOnly(Year, Month, 5), StartTime = new TimeOnly(18, 0) },
                new LinkedEventEntry { EventId = 3, Title = "Zulu All Day", Date = new DateOnly(Year, Month, 5), StartTime = null },
                new LinkedEventEntry { EventId = 4, Title = "Alpha Early", Date = new DateOnly(Year, Month, 5), StartTime = new TimeOnly(18, 0) },
                new LinkedEventEntry { EventId = 5, Title = "alpha All Day", Date = new DateOnly(Year, Month, 5), StartTime = null }
            ]
        };

        var day = model.GetCalendarDays().Single(d => !d.IsEmpty && d.Day == 5);

        day.Linked.Events.Select(e => e.Entry.Title).Should().Equal(
            "alpha All Day", "Zulu All Day", "Alpha Early", "beta Early", "Late Night");
        day.Linked.Events.Select(e => e.IsAllDay).Should().Equal(true, true, false, false, false);
    }

    [Fact]
    public void LinkedQuests_OnOneDay_AreOrderedByTimeOfDayThenByTitle()
    {
        var model = new CalendarViewModel
        {
            Year = Year,
            Month = Month,
            LinkedQuests =
            [
                new LinkedQuestEntry { QuestId = 1, Title = "Night Watch", ProposedDates = [At(8, 21)] },
                new LinkedQuestEntry { QuestId = 2, Title = "beta Dusk", ProposedDates = [At(8, 18)] },
                new LinkedQuestEntry { QuestId = 3, Title = "Alpha Dusk", ProposedDates = [At(8, 18)] }
            ]
        };

        var day = model.GetCalendarDays().Single(d => !d.IsEmpty && d.Day == 8);

        day.Linked.Quests.Select(q => q.Entry.Title).Should().Equal("Alpha Dusk", "beta Dusk", "Night Watch");
    }

    [Fact]
    public void LinkedEntries_OwnAndLinkedOnTheSameDay_StayInSeparateCollections()
    {
        var (own, linked) = QuestPair(false, null, At(10));
        var model = ModelWith(own, linked);

        var day = model.GetCalendarDays().Single(d => !d.IsEmpty && d.Day == 10);

        day.QuestsOnDay.Should().ContainSingle();
        day.Linked.Quests.Should().ContainSingle();
        day.Linked.Count.Should().Be(1);
    }

    [Fact]
    public void Calendar_WithNoLinkedData_HasNoLinkedEntriesOnAnyDay()
    {
        var (own, _) = QuestPair(false, null, At(10));
        var model = ModelWith(own: own);

        var days = model.GetCalendarDays();

        days.Should().NotBeEmpty();
        days.Should().OnlyContain(d => !d.Linked.HasEntries && d.Linked.Count == 0);
    }

    [Fact]
    public void LinkedEvent_Cancelled_IsPlacedOnItsDateAndCounted()
    {
        var model = new CalendarViewModel
        {
            Year = Year,
            Month = Month,
            LinkedEvents =
            [
                new LinkedEventEntry
                {
                    EventId = 1,
                    Title = "Called Off",
                    Date = new DateOnly(Year, Month, 20),
                    StartTime = new TimeOnly(17, 0),
                    IsCancelled = true
                }
            ]
        };

        var days = model.GetCalendarDays();

        var day = days.Single(d => !d.IsEmpty && d.Day == 20);
        day.Linked.Events.Should().ContainSingle().Which.Entry.IsCancelled.Should().BeTrue();
        day.Linked.Count.Should().Be(1);
        day.Linked.HasEntries.Should().BeTrue();
        days.Where(d => !d.IsEmpty && d.Day != 20).Should().OnlyContain(d => !d.Linked.HasEntries);
    }

    [Fact]
    public void LinkedDay_WithAnEventAndAQuest_CountsBoth()
    {
        var model = new CalendarViewModel
        {
            Year = Year,
            Month = Month,
            LinkedEvents = [new LinkedEventEntry { EventId = 1, Title = "Market Day", Date = new DateOnly(Year, Month, 9) }],
            LinkedQuests = [new LinkedQuestEntry { QuestId = 2, Title = "Bandit Camp", ProposedDates = [At(9)] }]
        };

        var day = model.GetCalendarDays().Single(d => !d.IsEmpty && d.Day == 9);

        day.Linked.Count.Should().Be(2);
    }
}
