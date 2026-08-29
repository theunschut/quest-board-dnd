using AutoMapper;
using FluentAssertions;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// This class protects the whole cross-board agenda contract: the query is called
// unconditionally including with an empty membership set, a row outside the caller's own
// membership set is dropped before the window is trimmed, the viewer's own cell and the
// full roster ride along on every row, and the paging idiom matches the sibling overview
// feature's fetch-one-extra shape.
public class CrossBoardAgendaTests
{
    private static readonly DateTimeOffset DefaultClockInstant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Hand-written rather than a testing-time-provider package, so the fixed clock costs no
    // new dependency: this phase's package legitimacy position is that it installs nothing.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static EventService CreateService(IEventRepository repository, DateTimeOffset? now = null)
    {
        return new EventService(repository, Substitute.For<IMapper>(), new FixedTimeProvider(now ?? DefaultClockInstant));
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

    private static EventWithSignups EventWith(int eventId, int groupId, params EventSignup[] signups)
    {
        return new EventWithSignups
        {
            Event = new Event { Id = eventId, Title = $"Event {eventId}", GroupId = groupId },
            Signups = signups
        };
    }

    // -------------------------------------------------------------------
    // Paging
    // -------------------------------------------------------------------

    [Fact]
    public async Task CrossBoardAgenda_HasMore_TrueWhenRepositoryReturnsMoreThanTake()
    {
        // Arrange: repository returns take + 1 rows, all within the membership set.
        var repository = Substitute.For<IEventRepository>();
        var events = Enumerable.Range(1, 3).Select(id => EventWith(id, groupId: 1)).ToList();
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(events);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 2, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows.Should().HaveCount(2);
        agenda.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task CrossBoardAgenda_HasMore_FalseWhenRepositoryReturnsExactlyTake()
    {
        // Arrange: repository returns exactly take rows.
        var repository = Substitute.For<IEventRepository>();
        var events = Enumerable.Range(1, 2).Select(id => EventWith(id, groupId: 1)).ToList();
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(events);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 2, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows.Should().HaveCount(2);
        agenda.HasMore.Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // Second-layer re-check
    // -------------------------------------------------------------------

    [Fact]
    public async Task CrossBoardAgenda_RowOutsideMembershipSet_IsDroppedBeforeReachingTheCaller()
    {
        // Arrange: the repository hands back a row on a board the caller does not belong to --
        // this must never reach the returned agenda, even though the repository returned it.
        var repository = Substitute.For<IEventRepository>();
        var ownRow = EventWith(1, groupId: 1);
        var foreignRow = EventWith(2, groupId: 99);
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([ownRow, foreignRow]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 10, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows.Should().ContainSingle();
        agenda.Rows[0].Event.Id.Should().Be(1);
    }

    [Fact]
    public async Task CrossBoardAgenda_DroppedRow_IsExcludedBeforeHasMoreIsComputed()
    {
        // Arrange: 2 own-board rows plus 1 foreign-board row, asked for take = 2. If the drop
        // happened after trimming, HasMore would read true off the foreign row occupying a
        // window slot; dropping first must leave exactly 2 surviving rows and HasMore false.
        var repository = Substitute.For<IEventRepository>();
        var ownRowOne = EventWith(1, groupId: 1);
        var ownRowTwo = EventWith(2, groupId: 1);
        var foreignRow = EventWith(3, groupId: 99);
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([ownRowOne, foreignRow, ownRowTwo]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 2, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows.Should().HaveCount(2);
        agenda.HasMore.Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // Empty membership set
    // -------------------------------------------------------------------

    [Fact]
    public async Task CrossBoardAgenda_EmptyMembershipSet_StillCallsRepository_AndReturnsEmptyAgenda()
    {
        // Arrange: no short-circuit -- the repository must still be called with the empty
        // collection, so a future short-circuit would fail this assertion.
        var repository = Substitute.For<IEventRepository>();
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([], currentUserId: 1, take: 5, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows.Should().BeEmpty();
        agenda.HasMore.Should().BeFalse();
        await repository.Received(1).GetUpcomingAcrossGroupsWithSignupsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 0), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------
    // Viewer's own cell
    // -------------------------------------------------------------------

    [Fact]
    public async Task CrossBoardAgenda_ViewerHoldsNoSignupRow_MyCellIsEmpty()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1, groupId: 1, Signup(2, "Bob", VoteType.Yes, hasAnswered: true));
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 5, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows[0].MyCell.Should().Be(AvailabilityCellState.Empty);
    }

    [Fact]
    public async Task CrossBoardAgenda_ViewerHoldsASignupRow_MyCellIsItsClassification()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1, groupId: 1, Signup(1, "Alice", VoteType.Maybe, hasAnswered: true));
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 5, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows[0].MyCell.Should().Be(AvailabilityCellState.ConfirmedMaybe);
    }

    // -------------------------------------------------------------------
    // Cell classification
    // -------------------------------------------------------------------

    [Fact]
    public async Task CrossBoardAgenda_UnansweredYes_ClassifiesAsUnconfirmedYes()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1, groupId: 1, Signup(1, "Alice", VoteType.Yes, hasAnswered: false));
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 5, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows[0].MyCell.Should().Be(AvailabilityCellState.UnconfirmedYes);
        agenda.Rows[0].Roster.Should().ContainSingle().Which.Cell.Should().Be(AvailabilityCellState.UnconfirmedYes);
    }

    [Fact]
    public async Task CrossBoardAgenda_AnsweredRows_ClassifyByAvailability()
    {
        // Arrange: answered Yes/Maybe/No.
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1, groupId: 1,
            Signup(1, "Alice", VoteType.Yes, hasAnswered: true),
            Signup(2, "Bob", VoteType.Maybe, hasAnswered: true),
            Signup(3, "Cara", VoteType.No, hasAnswered: true));
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 5, TestContext.Current.CancellationToken);

        // Assert: roster is alphabetical, so order lines up with the arrange order here.
        agenda.Rows[0].Roster.Select(r => r.Cell).Should().Equal(
            AvailabilityCellState.ConfirmedYes,
            AvailabilityCellState.ConfirmedMaybe,
            AvailabilityCellState.ConfirmedNo);
    }

    // -------------------------------------------------------------------
    // Roster
    // -------------------------------------------------------------------

    [Fact]
    public async Task CrossBoardAgenda_Roster_ContainsEverySignup_OrderedAlphabeticallyWithViewerFlagged()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(1, groupId: 1,
            Signup(3, "Cara", VoteType.Yes, hasAnswered: true),
            Signup(1, "Alice", VoteType.Maybe, hasAnswered: true),
            Signup(2, "Bob", VoteType.No, hasAnswered: true));
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 2, take: 5, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows[0].Roster.Select(r => r.Name).Should().Equal("Alice", "Bob", "Cara");
        agenda.Rows[0].Roster.Should().ContainSingle(r => r.IsViewer).Which.UserId.Should().Be(2);
    }

    [Fact]
    public async Task CrossBoardAgenda_EventFieldsAreCarriedThroughUntouched()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var eventWithSignups = EventWith(7, groupId: 1);
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([eventWithSignups]);
        var service = CreateService(repository);

        // Act
        var agenda = await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 5, TestContext.Current.CancellationToken);

        // Assert
        agenda.Rows[0].Event.Id.Should().Be(7);
        agenda.Rows[0].Event.Title.Should().Be("Event 7");
        agenda.Rows[0].Event.GroupId.Should().Be(1);
    }

    // -------------------------------------------------------------------
    // Clock / request shape
    // -------------------------------------------------------------------

    [Fact]
    public async Task CrossBoardAgenda_RequestsTakePlusOneFromRepository_AndUtcDateOnly()
    {
        // Arrange: a fixed instant, so the assertion pins an exact DateOnly instead of
        // recomputing the expression the implementation uses.
        var repository = Substitute.For<IEventRepository>();
        repository.GetUpcomingAcrossGroupsWithSignupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var fixedInstant = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(repository, fixedInstant);

        // Act
        await service.GetCrossBoardAgendaAsync([1], currentUserId: 1, take: 10, TestContext.Current.CancellationToken);

        // Assert
        await repository.Received(1).GetUpcomingAcrossGroupsWithSignupsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1 })),
            new DateOnly(2026, 3, 15),
            11,
            Arg.Any<CancellationToken>());
    }
}
