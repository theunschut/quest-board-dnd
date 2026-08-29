using AutoMapper;
using FluentAssertions;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// This class protects the whole aggregation contract the availability overview page is built
// on: the member axis is a union of signup rows rather than a membership query, the five cell
// states classify strictly off the answered marker, the three per-row counts never fold into
// each other, and HasMore never costs a second query.
public class EventsOverviewAggregationTests
{
    private static EventService CreateService(IEventRepository repository)
    {
        return new EventService(repository, Substitute.For<IMapper>());
    }

    private static EventSignup Signup(int userId, string userName, VoteType availability, bool hasAnswered)
    {
        return new EventSignup
        {
            UserId = userId,
            UserName = userName,
            Availability = availability,
            UpdatedAt = hasAnswered ? DateTime.UtcNow : null
        };
    }

    private static EventWithSignups EventWith(int eventId, params EventSignup[] signups)
    {
        return new EventWithSignups
        {
            Event = new Event { Id = eventId, Title = $"Event {eventId}" },
            Signups = signups
        };
    }

    // -------------------------------------------------------------------
    // Counts
    // -------------------------------------------------------------------

    [Fact]
    public async Task EventOverviewCounts_YesTotal_IncludesUnconfirmedDefaults()
    {
        // Arrange: 3 Yes rows, 1 of which has an unset answered marker.
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1,
            Signup(1, "Alice", VoteType.Yes, hasAnswered: true),
            Signup(2, "Bob", VoteType.Yes, hasAnswered: true),
            Signup(3, "Cara", VoteType.Yes, hasAnswered: false));
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert
        overview.Rows.Should().ContainSingle();
        overview.Rows[0].YesCount.Should().Be(3);
        overview.Rows[0].ConfirmedYesCount.Should().Be(2);
    }

    [Fact]
    public async Task EventOverviewCounts_Maybe_IsSeparateAndNeverFoldedIntoYes()
    {
        // Arrange: 2 Yes + 4 Maybe.
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1,
            Signup(1, "Alice", VoteType.Yes, hasAnswered: true),
            Signup(2, "Bob", VoteType.Yes, hasAnswered: true),
            Signup(3, "Cara", VoteType.Maybe, hasAnswered: true),
            Signup(4, "Dan", VoteType.Maybe, hasAnswered: true),
            Signup(5, "Eve", VoteType.Maybe, hasAnswered: true),
            Signup(6, "Finn", VoteType.Maybe, hasAnswered: true));
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert
        overview.Rows[0].YesCount.Should().Be(2);
        overview.Rows[0].MaybeCount.Should().Be(4);
    }

    [Fact]
    public async Task EventOverviewCounts_No_IsCountedNowhere()
    {
        // Arrange: adding No rows must change neither YesCount, ConfirmedYesCount nor MaybeCount.
        var repository = Substitute.For<IEventRepository>();
        var eventWithoutNo = EventWith(1,
            Signup(1, "Alice", VoteType.Yes, hasAnswered: true),
            Signup(2, "Bob", VoteType.Maybe, hasAnswered: true));
        var eventWithNo = EventWith(1,
            Signup(1, "Alice", VoteType.Yes, hasAnswered: true),
            Signup(2, "Bob", VoteType.Maybe, hasAnswered: true),
            Signup(3, "Cara", VoteType.No, hasAnswered: true),
            Signup(4, "Dan", VoteType.No, hasAnswered: true));
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithoutNo], [eventWithNo]);
        var service = CreateService(repository);

        // Act
        var withoutNo = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);
        var withNo = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert
        withNo.Rows[0].YesCount.Should().Be(withoutNo.Rows[0].YesCount);
        withNo.Rows[0].ConfirmedYesCount.Should().Be(withoutNo.Rows[0].ConfirmedYesCount);
        withNo.Rows[0].MaybeCount.Should().Be(withoutNo.Rows[0].MaybeCount);
    }

    // -------------------------------------------------------------------
    // Mapping / cell classification
    // -------------------------------------------------------------------

    [Fact]
    public async Task EventOverviewMapping_UnansweredYes_ClassifiesAsUnconfirmedYes()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1, Signup(1, "Alice", VoteType.Yes, hasAnswered: false));
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert
        overview.Rows[0].Cells.Should().ContainSingle().Which.Should().Be(AvailabilityCellState.UnconfirmedYes);
    }

    [Fact]
    public async Task EventOverviewMapping_AnsweredRows_ClassifyByAvailability()
    {
        // Arrange: answered Yes/Maybe/No.
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1,
            Signup(1, "Alice", VoteType.Yes, hasAnswered: true),
            Signup(2, "Bob", VoteType.Maybe, hasAnswered: true),
            Signup(3, "Cara", VoteType.No, hasAnswered: true));
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert: axis is alphabetical (Alice, Bob, Cara), so cells align in that order.
        overview.Rows[0].Cells.Should().Equal(
            AvailabilityCellState.ConfirmedYes,
            AvailabilityCellState.ConfirmedMaybe,
            AvailabilityCellState.ConfirmedNo);
    }

    [Fact]
    public async Task EventOverviewMapping_MemberWithNoRowForAnEvent_ClassifiesAsEmpty()
    {
        // Arrange: Alice holds a row on event 1 only, Bob holds a row on event 2 only. Alice is
        // on the axis and must show Empty for event 2.
        var repository = Substitute.For<IEventRepository>();
        var eventOne = EventWith(1, Signup(1, "Alice", VoteType.Yes, hasAnswered: true));
        var eventTwo = EventWith(2, Signup(2, "Bob", VoteType.Yes, hasAnswered: true));
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventOne, eventTwo]);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert: axis alphabetical -> Alice, Bob.
        var eventTwoRow = overview.Rows.Single(r => r.Event.Id == 2);
        eventTwoRow.Cells[0].Should().Be(AvailabilityCellState.Empty);
        eventTwoRow.Cells[1].Should().Be(AvailabilityCellState.ConfirmedYes);
    }

    [Fact]
    public async Task EventOverviewMapping_MemberAxis_IsAlphabeticalAndIdenticalAcrossRows()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var eventOne = EventWith(1,
            Signup(3, "Cara", VoteType.Yes, hasAnswered: true),
            Signup(1, "Alice", VoteType.Yes, hasAnswered: true));
        var eventTwo = EventWith(2, Signup(2, "Bob", VoteType.Yes, hasAnswered: true));
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventOne, eventTwo]);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert
        overview.Members.Select(m => m.Name).Should().Equal("Alice", "Bob", "Cara");
        overview.Rows.Should().OnlyContain(r => r.Cells.Count == overview.Members.Count);
    }

    // -------------------------------------------------------------------
    // HasMore / paging
    // -------------------------------------------------------------------

    [Fact]
    public async Task EventsOverviewAggregation_HasMore_TrueWhenRepositoryReturnsMoreThanTake()
    {
        // Arrange: repository returns take + 1 rows.
        var repository = Substitute.For<IEventRepository>();
        var events = Enumerable.Range(1, 4).Select(id => EventWith(id)).ToList();
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(events);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(3, TestContext.Current.CancellationToken);

        // Assert
        overview.Rows.Should().HaveCount(3);
        overview.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task EventsOverviewAggregation_HasMore_FalseWhenRepositoryReturnsAtMostTake()
    {
        // Arrange: repository returns fewer than take + 1 rows.
        var repository = Substitute.For<IEventRepository>();
        var events = Enumerable.Range(1, 2).Select(id => EventWith(id)).ToList();
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(events);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(3, TestContext.Current.CancellationToken);

        // Assert
        overview.Rows.Should().HaveCount(2);
        overview.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task EventsOverviewAggregation_RequestsTakePlusOneFromRepository_AndTodayDateOnly()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var service = CreateService(repository);
        var expectedToday = DateOnly.FromDateTime(DateTime.Today);

        // Act
        await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(1).GetUpcomingWithSignupsAsync(expectedToday, 11, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EventsOverviewAggregation_NoEvents_ReturnsEmptyMembersAndRows()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        repository.GetUpcomingWithSignupsAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var service = CreateService(repository);

        // Act
        var overview = await service.GetAvailabilityOverviewAsync(10, TestContext.Current.CancellationToken);

        // Assert
        overview.Members.Should().BeEmpty();
        overview.Rows.Should().BeEmpty();
        overview.HasMore.Should().BeFalse();
    }
}
