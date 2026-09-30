using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

// Pins which revision inputs the service hands the writer for each entry. An event entry's
// sequence number is the event's own stored revision, shared by every reader of the event, and
// is never touched by a single reader's answer. Its stamp is the later of the event's revision
// time and the time this reader's own answer row was written, so an answer moves that reader's
// stamp and nobody else's. A quest entry takes both values from the quest alone.
public class CalendarFeedRevisionInputTests
{
    private const int EventId = 7;
    private const int QuestId = 42;

    private static readonly DateOnly BoardToday = new(2026, 9, 20);

    private static readonly DateTime EventRevisedAt = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    // Hand-rolled rather than substituted: CalendarSubscriptionService is internal, so a
    // dynamic proxy over ILogger<CalendarSubscriptionService> cannot be generated.
    private sealed class SilentLogger : ILogger<CalendarSubscriptionService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private static EventFeedRow MakeEventRow(int feedRevision, DateTime feedRevisedAt, DateTime answerWrittenAt) => new()
    {
        Event = new Event
        {
            Id = EventId,
            Title = "Revision Input Session",
            GroupId = 1,
            Date = new DateOnly(2026, 9, 25),
            StartTime = new TimeOnly(19, 0),
            FeedRevision = feedRevision,
            FeedRevisedAt = feedRevisedAt,
        },
        Availability = VoteType.Maybe,
        AnswerWrittenAt = answerWrittenAt,
    };

    private static QuestBoard.Domain.Models.QuestBoard.Quest MakeQuest(int feedRevision, DateTime feedRevisedAt) => new()
    {
        Id = QuestId,
        Title = "Revision Input Quest",
        GroupId = 1,
        DungeonMasterId = 1,
        FinalizedDate = new DateTime(2026, 9, 26, 19, 0, 0),
        IsFinalized = true,
        CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        FeedRevision = feedRevision,
        FeedRevisedAt = feedRevisedAt,
    };

    // One subscription for token "test-token" on one one-shot board, the given event rows and
    // quests, and a substitute writer that records the entries it is handed.
    private static async Task<IReadOnlyList<CalendarFeedEntry>> CaptureEntriesAsync(
        IList<EventFeedRow> eventRows,
        IList<QuestBoard.Domain.Models.QuestBoard.Quest> quests)
    {
        var subscriptionRepository = Substitute.For<ICalendarSubscriptionRepository>();
        subscriptionRepository.GetByTokenAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(new CalendarSubscription { Id = 1, UserId = 1, Name = "Test Subscription", Token = "test-token" });

        var eventSignupRepository = Substitute.For<IEventSignupRepository>();
        eventSignupRepository
            .GetFeedRowsForUserAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(eventRows);

        var questRepository = Substitute.For<IQuestRepository>();
        questRepository
            .GetFeedQuestsForUserAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(quests.ToList());

        var groupService = Substitute.For<IGroupService>();
        groupService.GetGroupsForUserAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GroupWithMemberCount>
            {
                new() { Id = 1, Name = "Test Board", BoardType = BoardType.OneShot },
            });

        IReadOnlyList<CalendarFeedEntry>? captured = null;
        var writer = Substitute.For<ICalendarFeedWriter>();
        writer.Write(
                Arg.Do<IReadOnlyList<CalendarFeedEntry>>(entries => captured = entries),
                Arg.Any<string>(),
                Arg.Any<TimeZoneInfo>())
            .Returns("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");

        var service = new CalendarSubscriptionService(
            subscriptionRepository,
            eventSignupRepository,
            questRepository,
            groupService,
            writer,
            new FakeBoardClock { TimeZone = TimeZoneInfo.Utc, Today = BoardToday },
            TimeProvider.System,
            Options.Create(new CalendarFeedOptions()),
            new SilentLogger());

        await service.GetFeedAsync("test-token", TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        return captured!;
    }

    [Fact]
    public async Task GetFeedAsync_AnswerWrittenAfterTheEventRevision_StampsTheEntryWithTheAnswerTime()
    {
        var answerWrittenAt = EventRevisedAt.AddDays(3);
        var row = MakeEventRow(feedRevision: 4, feedRevisedAt: EventRevisedAt, answerWrittenAt: answerWrittenAt);

        var entries = await CaptureEntriesAsync([row], []);

        var entry = entries.Single(e => e.Source == CalendarFeedSource.Event && e.SourceId == EventId);
        entry.LastRevisedAt.Should().Be(answerWrittenAt);
        entry.Sequence.Should().Be(4);
    }

    [Fact]
    public async Task GetFeedAsync_AnswerWrittenBeforeTheEventRevision_StampsTheEntryWithTheEventRevisionTime()
    {
        var row = MakeEventRow(feedRevision: 4, feedRevisedAt: EventRevisedAt, answerWrittenAt: EventRevisedAt.AddDays(-3));

        var entries = await CaptureEntriesAsync([row], []);

        var entry = entries.Single(e => e.Source == CalendarFeedSource.Event && e.SourceId == EventId);
        entry.LastRevisedAt.Should().Be(EventRevisedAt);
        entry.Sequence.Should().Be(4);
    }

    [Fact]
    public async Task GetFeedAsync_AnswerWrittenAtTheEventRevisionTime_StampsTheEntryWithThatTime()
    {
        var row = MakeEventRow(feedRevision: 2, feedRevisedAt: EventRevisedAt, answerWrittenAt: EventRevisedAt);

        var entries = await CaptureEntriesAsync([row], []);

        var entry = entries.Single(e => e.Source == CalendarFeedSource.Event && e.SourceId == EventId);
        entry.LastRevisedAt.Should().Be(EventRevisedAt);
        entry.Sequence.Should().Be(2);
    }

    [Fact]
    public async Task GetFeedAsync_QuestEntry_TakesItsSequenceAndStampFromTheQuestAlone()
    {
        var questRevisedAt = new DateTime(2026, 9, 12, 8, 30, 0, DateTimeKind.Utc);

        // The event row carries a later answer time, and it must not leak into the quest entry.
        var row = MakeEventRow(feedRevision: 9, feedRevisedAt: EventRevisedAt, answerWrittenAt: questRevisedAt.AddDays(5));
        var quest = MakeQuest(feedRevision: 3, feedRevisedAt: questRevisedAt);

        var entries = await CaptureEntriesAsync([row], [quest]);

        var questEntry = entries.Single(e => e.Source == CalendarFeedSource.Quest && e.SourceId == QuestId);
        questEntry.Sequence.Should().Be(3);
        questEntry.LastRevisedAt.Should().Be(questRevisedAt);
    }
}
