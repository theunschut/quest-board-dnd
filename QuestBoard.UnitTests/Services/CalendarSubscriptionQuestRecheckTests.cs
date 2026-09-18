using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// The integration facts on the live feed address cannot reach the quest branch's second-layer
// re-check's drop-and-log path, and that is not a gap in them -- it is a property of the
// design. The real repository is handed the same one-shot board-id set the re-check tests
// against, so it structurally cannot return a row outside it. The branch exists for the case
// where that predicate is lost or mistranslated somewhere between the service and the
// database, which is exactly the case no healthy integration test can produce. Reaching it
// means driving the domain service directly with a fake repository that misbehaves on
// purpose -- returning a row outside the set it was handed, something the real, filtered query
// can never do.
//
// This branch is worth testing precisely because it is the only signal that would exist if the
// query's board predicate were ever dropped or mistranslated, on a surface read by a machine
// where a leak has no reader to notice it. A re-check nobody has ever seen fire is a re-check
// nobody knows works.
public class CalendarSubscriptionQuestRecheckTests
{
    private static readonly DateTimeOffset DefaultClockInstant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const int OneShotBoardId = 101;
    private const int CampaignBoardId = 102;
    private const int SubscriptionOwnerUserId = 555;
    private const string KnownFeedToken = "quest-recheck-known-address";

    // Hand-written rather than a testing-time-provider package, so the fixed clock costs no
    // new dependency -- matching the convention this codebase already uses for a domain
    // service's own unit suite (CrossBoardAgendaTests, EventsOverviewAggregationTests).
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Hand-rolled rather than substituted: CalendarSubscriptionService is internal, so a
    // dynamic proxy over ILogger<CalendarSubscriptionService> cannot be generated, and
    // recording the entries directly is what the assertions below actually want anyway.
    private sealed class RecordingLogger : ILogger<CalendarSubscriptionService>
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

    // One one-shot board and one campaign board -- both the reader's own memberships. The
    // one-shot board is the only one that belongs in the board-id set the service hands the
    // quest repository; the campaign board proves the set is genuinely narrowed rather than
    // just non-empty.
    private static IList<GroupWithMemberCount> Memberships() =>
    [
        new GroupWithMemberCount { Id = OneShotBoardId, Name = "Quest Recheck One-Shot Board", BoardType = BoardType.OneShot, CreatedAt = DateTime.UtcNow, MemberCount = 1 },
        new GroupWithMemberCount { Id = CampaignBoardId, Name = "Quest Recheck Campaign Board", BoardType = BoardType.Campaign, CreatedAt = DateTime.UtcNow, MemberCount = 1 }
    ];

    private static CalendarSubscription LiveSubscription() => new()
    {
        Id = 1,
        UserId = SubscriptionOwnerUserId,
        Name = "Quest Recheck Subscription",
        Token = KnownFeedToken,
        CreatedAt = DateTime.UtcNow,
        RevokedAt = null
    };

    // Constructs the domain service directly with a fake for every dependency. The writer is
    // the real one -- CalendarFeedWriter, internal but reachable from this assembly through
    // QuestBoard.Domain's InternalsVisibleTo grant -- so the facts below assert on a real
    // rendered document rather than on a mock's recorded arguments.
    private static (CalendarSubscriptionService Service, IQuestRepository QuestRepository, RecordingLogger Logger) CreateService(
        IList<Quest> questRows)
    {
        var subscriptionRepository = Substitute.For<ICalendarSubscriptionRepository>();
        subscriptionRepository.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LiveSubscription());
        subscriptionRepository.TouchLastFetchedAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var eventSignupRepository = Substitute.For<IEventSignupRepository>();
        eventSignupRepository.GetFeedRowsForUserAsync(
                Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<EventFeedRow>());

        var questRepository = Substitute.For<IQuestRepository>();
        questRepository.GetFeedQuestsForUserAsync(
                Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(questRows);

        var groupService = Substitute.For<IGroupService>();
        groupService.GetGroupsForUserAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Memberships());

        var writer = new CalendarFeedWriter();
        var logger = new RecordingLogger();
        var feedOptions = Options.Create(new CalendarFeedOptions());

        var service = new CalendarSubscriptionService(
            subscriptionRepository,
            eventSignupRepository,
            questRepository,
            groupService,
            writer,
            new FixedTimeProvider(DefaultClockInstant),
            feedOptions,
            logger);

        return (service, questRepository, logger);
    }

    private static Quest MakeQuest(int id, int groupId, string title, DateTime finalizedDate) => new()
    {
        Id = id,
        Title = title,
        Description = "Test Description",
        GroupId = groupId,
        IsFinalized = true,
        FinalizedDate = finalizedDate,
        CreatedAt = DateTime.UtcNow
    };

    private static int CountVEvents(string body) => body.Split("BEGIN:VEVENT").Length - 1;

    [Fact]
    public async Task DroppedRow_NeverReachesTheDocument()
    {
        // Arrange: the fake repository returns one quest inside the one-shot set it was handed
        // and one quest on the campaign board -- a row the real, filtered repository could
        // never produce, standing in for a lost or mistranslated board predicate.
        var inSetQuest = MakeQuest(1, OneShotBoardId, "Quest Recheck In-Set Session", new DateTime(2026, 1, 15, 19, 0, 0));
        var foreignQuest = MakeQuest(2, CampaignBoardId, "Quest Recheck Foreign Session", new DateTime(2026, 1, 16, 19, 0, 0));
        var (service, _, _) = CreateService([inSetQuest, foreignQuest]);

        // Act
        var result = await service.GetFeedAsync(KnownFeedToken, TestContext.Current.CancellationToken);

        // Assert: the foreign row must never reach the document, whatever the repository did.
        result.Body.Should().NotBeNull();
        result.Body!.Should().Contain("Quest Recheck In-Set Session");
        result.Body.Should().NotContain("Quest Recheck Foreign Session");
        CountVEvents(result.Body).Should().Be(1);
    }

    [Fact]
    public async Task DroppedRow_IsRecordedAsASingleErrorCarryingBothCounts()
    {
        // Arrange: same misbehaving repository as above.
        var inSetQuest = MakeQuest(1, OneShotBoardId, "Quest Recheck In-Set Session", new DateTime(2026, 1, 15, 19, 0, 0));
        var foreignQuest = MakeQuest(2, CampaignBoardId, "Quest Recheck Foreign Session", new DateTime(2026, 1, 16, 19, 0, 0));
        var (service, _, logger) = CreateService([inSetQuest, foreignQuest]);

        // Act
        await service.GetFeedAsync(KnownFeedToken, TestContext.Current.CancellationToken);

        // Assert: dropping the row protects the reader, but on its own it tells nobody. A
        // surviving foreign row can only mean the predicate was lost, and an operator has to be
        // able to see that happening rather than infer it from an occasionally short feed.
        // Asserted on the presence of the counts rather than the whole sentence, because the
        // sentence is copy and will be reworded, while an operator reading the log needs the
        // numbers: one row dropped, out of two fetched.
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error);
        var entry = logger.Entries.Single(e => e.Level == LogLevel.Error);
        entry.Message.Should().Contain("1").And.Contain("2");
    }

    [Fact]
    public async Task HealthyRead_LogsNothing()
    {
        // Arrange: every row the fake repository returns is genuinely inside the one-shot set.
        var firstQuest = MakeQuest(1, OneShotBoardId, "Quest Recheck Healthy Session One", new DateTime(2026, 1, 15, 19, 0, 0));
        var secondQuest = MakeQuest(2, OneShotBoardId, "Quest Recheck Healthy Session Two", new DateTime(2026, 1, 16, 19, 0, 0));
        var (service, _, logger) = CreateService([firstQuest, secondQuest]);

        // Act
        var result = await service.GetFeedAsync(KnownFeedToken, TestContext.Current.CancellationToken);

        // Assert: this is the fact that keeps the log fact above meaningful -- a re-check that
        // fires on healthy data is a re-check that will be ignored when it matters.
        result.Body.Should().NotBeNull();
        result.Body!.Should().Contain("Quest Recheck Healthy Session One");
        result.Body.Should().Contain("Quest Recheck Healthy Session Two");
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task TheArgumentsCrossingIntoTheRepository_CarryOnlyTheReadersOwnOneShotBoards()
    {
        // Arrange: this pins the two independent scoping rules at their origin -- the arguments
        // the service passes to the repository -- rather than only at their effect on the
        // document, which the facts above already cover.
        var inSetQuest = MakeQuest(1, OneShotBoardId, "Quest Recheck In-Set Session", new DateTime(2026, 1, 15, 19, 0, 0));
        var (service, questRepository, _) = CreateService([inSetQuest]);

        // Act
        await service.GetFeedAsync(KnownFeedToken, TestContext.Current.CancellationToken);

        // Assert: the subscription's own owner id, not any other value, and a board-id set
        // containing only the one-shot board from the membership list -- never the campaign
        // board, and never a board the membership list did not contain at all.
        await questRepository.Received(1).GetFeedQuestsForUserAsync(
            Arg.Is<int>(userId => userId == SubscriptionOwnerUserId),
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { OneShotBoardId })),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }
}
