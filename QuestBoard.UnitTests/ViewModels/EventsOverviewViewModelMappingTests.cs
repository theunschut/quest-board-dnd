using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Automapper;
using QuestBoard.Service.ViewModels.EventViewModels;

namespace QuestBoard.UnitTests.ViewModels;

// Proves the domain-to-view-model mapping for the availability overview page preserves the
// three per-row counts, the positional cell order, and the member identity -- the exact
// facts the grid depends on to stay meaningful.
public class EventsOverviewViewModelMappingTests
{
    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<ViewModelProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    [Fact]
    public void EventOverviewMapping_Row_CopiesEventIdentityAndAllThreeCounts()
    {
        var mapper = CreateMapper();
        var source = new EventAvailabilityRow
        {
            Event = new Event
            {
                Id = 42,
                Title = "Session 14",
                Date = new DateOnly(2026, 9, 5),
                StartTime = new TimeOnly(19, 0)
            },
            YesCount = 5,
            ConfirmedYesCount = 3,
            MaybeCount = 2,
            Cells = [AvailabilityCellState.ConfirmedYes]
        };

        var result = mapper.Map<EventOverviewRowViewModel>(source);

        result.EventId.Should().Be(42);
        result.Title.Should().Be("Session 14");
        result.Date.Should().Be(new DateOnly(2026, 9, 5));
        result.StartTime.Should().Be(new TimeOnly(19, 0));
        result.YesCount.Should().Be(5);
        result.ConfirmedYesCount.Should().Be(3);
        result.MaybeCount.Should().Be(2);
    }

    [Fact]
    public void EventOverviewMapping_Row_PreservesCellOrderAndCount()
    {
        var mapper = CreateMapper();
        var source = new EventAvailabilityRow
        {
            Event = new Event { Id = 1, Title = "Session 1", Date = new DateOnly(2026, 9, 1) },
            Cells =
            [
                AvailabilityCellState.Empty,
                AvailabilityCellState.ConfirmedYes,
                AvailabilityCellState.UnconfirmedYes,
                AvailabilityCellState.ConfirmedMaybe,
                AvailabilityCellState.ConfirmedNo
            ]
        };

        var result = mapper.Map<EventOverviewRowViewModel>(source);

        result.Cells.Should().HaveCount(5);
        result.Cells.Should().ContainInOrder(
            AvailabilityCellState.Empty,
            AvailabilityCellState.ConfirmedYes,
            AvailabilityCellState.UnconfirmedYes,
            AvailabilityCellState.ConfirmedMaybe,
            AvailabilityCellState.ConfirmedNo);
    }

    [Fact]
    public void EventOverviewMapping_Member_CopiesUserIdAndName()
    {
        var mapper = CreateMapper();
        var source = new AvailabilityMember { UserId = 7, Name = "Alice" };

        var result = mapper.Map<OverviewMemberViewModel>(source);

        result.UserId.Should().Be(7);
        result.Name.Should().Be("Alice");
    }
}
