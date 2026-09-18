using Microsoft.Extensions.Options;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the application's own scoping and response logic over real HTTP for the quest half of
/// the calendar feed: an anonymous, cookie-less GET against a minted address returns the
/// finalized one-shot quest sessions the subscription's owner holds a confirmed seat on, or is
/// the Dungeon Master of.
///
/// What these facts do NOT establish, and must not be read as establishing: end-to-end
/// isolation against the database the application actually runs on. The shared harness backs
/// every suite with the EF Core InMemory provider, which evaluates every predicate as ordinary
/// LINQ-to-Objects. Several properties the repository query depends on therefore never reach a
/// relational query compiler here: the board containment test (oneShotGroupIds.Contains(...))
/// over an *empty* id collection is exercised only as List.Contains, never as its SQL
/// translation; the filter bypass interacting with the signup entity's own board-navigating
/// filter generates no join at all in memory; and the seat-or-Dungeon-Master disjunction over a
/// navigation collection is likewise exercised only as in-memory enumeration, never as its
/// translated form. The facts below are about the application's own scoping logic; proving the
/// query translates and behaves the same way on a relational provider needs a separate,
/// relational test.
/// </summary>
public class CalendarSubscriptionQuestFeedTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    // Creates a board (if it does not already exist) through the unfiltered seeding context.
    private async Task SeedBoardAsync(int groupId, string name, BoardType boardType = BoardType.OneShot)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == groupId))
        {
            ctx.Groups.Add(new GroupEntity { Id = groupId, Name = name, CreatedAt = DateTime.UtcNow, BoardType = (int)boardType });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // Adds a membership row for an already-created user on the named board.
    private async Task SeedMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Seeds a finalized quest on the named board with the given Dungeon Master, through the
    // unfiltered seeding context -- field shapes copied from TestDataHelper.CreateTestQuestAsync.
    // dungeonMasterSession defaults to false so every existing call site keeps its prior meaning.
    private async Task<int> SeedQuestAsync(int groupId, int dungeonMasterId, string title, DateTime finalizedDate, bool dungeonMasterSession = false)
    {
        await using var ctx = factory.Database.CreateContext();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "Test Description",
            ChallengeRating = 5,
            DungeonMasterId = dungeonMasterId,
            GroupId = groupId,
            IsFinalized = true,
            FinalizedDate = finalizedDate,
            TotalPlayerCount = 4,
            DungeonMasterSession = dungeonMasterSession,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quest.Id;
    }

    // Seeds a player signup for the given user on the given quest -- field shapes copied from
    // TestDataHelper.CreatePlayerSignupAsync. isSelected defaults to true (a confirmed seat) and
    // role defaults to Player, so every existing call site keeps its prior meaning.
    private async Task SeedPlayerSignupAsync(int questId, int playerId, bool isSelected = true, SignupRole role = SignupRole.Player)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.PlayerSignups.Add(new PlayerSignupEntity
        {
            QuestId = questId,
            PlayerId = playerId,
            SignupRole = (int)role,
            IsSelected = isSelected,
            SignupTime = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Mints through the real service from a scope, rather than a direct database insert, so
    // the fact exercises the same production write path the Profile page calls.
    private async Task<CalendarSubscription> MintSubscriptionAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        return await subscriptionService.MintForUserAsync(userId, TestContext.Current.CancellationToken);
    }

    // Reads the configured window and duration values from the running host's own DI
    // container, so a fact derives its expectations from configuration rather than a literal
    // that silently drifts out of sync with a future change to the defaults.
    private CalendarFeedOptions GetFeedOptions()
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<CalendarFeedOptions>>().Value;
    }

    // A fresh, plain client with no authorization header at all -- the genuinely anonymous path
    // a real calendar client takes.
    private async Task<HttpResponseMessage> FetchFeedAsync(string token)
    {
        var client = factory.CreateClient();
        return await client.GetAsync($"/feeds/calendar/{token}.ics", TestContext.Current.CancellationToken);
    }

    private static int CountVEvents(string body) => body.Split("BEGIN:VEVENT").Length - 1;

    // Counts occurrences rather than checking mere containment -- a duplicate is invisible to a
    // containment assertion, so the single-entry guarantee below needs a count.
    private static int CountOccurrences(string haystack, string needle) => haystack.Split(needle).Length - 1;

    [Fact]
    public async Task Feed_ServesASeatedReadersFinalizedOneShotQuest_ToAnonymousCaller()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_tracer_reader", "questfeed_tracer_reader@example.com", name: "Quest Feed Tracer Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_tracer_dm", "questfeed_tracer_dm@example.com", name: "Quest Feed Tracer DM");

        // Group 1 is the default seeded group -- a distinct id is used here so the board's own
        // name is under this fact's own control rather than borrowed.
        await SeedBoardAsync(2, "Quest Feed Tracer Board");
        await SeedMembershipAsync(reader.Id, 2);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(2, dungeonMaster.Id, "Quest Feed Tracer Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        var options = GetFeedOptions();

        // Set after minting and before the request, so the fact proves the path does not lean
        // on an active board -- a real calendar client sends no cookie and has none.
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.ToString().Should().Be("text/calendar; charset=utf-8");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().StartWith("BEGIN:VCALENDAR");
        body.Should().EndWith("END:VCALENDAR\r\n");
        body.Should().Contain($"UID:questboard-quest-{questId}");

        // The title line, terminated immediately by the line break -- what proves no marker of
        // any kind (no quest marker, no Dungeon Master marker, no answer suffix) was appended.
        body.Should().Contain("SUMMARY:[Quest Feed Tracer Board] Quest Feed Tracer Session\r\n");

        body.Should().Contain($"DTSTART:{finalizedDate:yyyyMMdd}T{finalizedDate:HHmmss}");
        body.Should().NotContain($"DTSTART:{finalizedDate:yyyyMMdd}T{finalizedDate:HHmmss}Z");

        // The end is exactly the configured number of hours later, read from the running host's
        // own options rather than a literal four.
        var expectedEnd = finalizedDate.AddHours(options.QuestDurationHours);
        body.Should().Contain($"DTEND:{expectedEnd:yyyyMMdd}T{expectedEnd:HHmmss}");

        body.Should().Contain("TRANSP:TRANSPARENT");

        CountVEvents(body).Should().Be(1);

        // What proves the quest went through the timed branch and never the all-day one.
        body.Should().NotContain("DTSTART;VALUE=DATE:");
    }

    [Fact]
    public async Task Feed_ServesAFinalizedOneShotQuestTheReaderRunsAsDungeonMaster_WithNoSignupRowAtAll()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_dm_route_reader", "questfeed_dm_route_reader@example.com", name: "Quest Feed DM Route Reader");

        await SeedBoardAsync(3, "Quest Feed DM Route Board");
        await SeedMembershipAsync(reader.Id, 3);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);

        // The reader owns this quest as Dungeon Master and holds no signup row on it at all -- a
        // signup-rooted query alone would give the person doing most of the board's scheduling
        // the emptiest calendar.
        var questId = await SeedQuestAsync(3, reader.Id, "Quest Feed DM Route Session", finalizedDate);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain($"UID:questboard-quest-{questId}");
        body.Should().Contain("SUMMARY:[Quest Feed DM Route Board] Quest Feed DM Route Session\r\n");
    }

    [Fact]
    public async Task Feed_QuestWhereReaderIsBothDungeonMasterAndHoldsAConfirmedSeat_EmitsExactlyOneEntryWithOneIdentifier()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_both_routes_reader", "questfeed_both_routes_reader@example.com", name: "Quest Feed Both Routes Reader");

        await SeedBoardAsync(4, "Quest Feed Both Routes Board");
        await SeedMembershipAsync(reader.Id, 4);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);

        // The reader is both the Dungeon Master and the holder of a confirmed seat on this same
        // quest -- the one case where the two routes into the feed can both be true at once.
        var questId = await SeedQuestAsync(4, reader.Id, "Quest Feed Both Routes Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Counts, not containments, because a duplicate is invisible to a containment assertion.
        // The identifier is derived from the quest alone, and two entries sharing one identifier
        // is undefined behaviour on a reader's phone -- this fact fails the moment the query
        // stops being rooted at quests and starts being two result sets merged after the fact.
        CountVEvents(body).Should().Be(1);
        CountOccurrences(body, $"questboard-quest-{questId}").Should().Be(1);
        CountOccurrences(body, "SUMMARY:[Quest Feed Both Routes Board] Quest Feed Both Routes Session\r\n").Should().Be(1);
    }

    [Fact]
    public async Task Feed_FinalizedQuestOnTheReadersOwnBoardWithNoSeat_StaysOutAlongsideAQuestTheyAreSeatedOn()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_own_board_reader", "questfeed_own_board_reader@example.com", name: "Quest Feed Own Board Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_own_board_dm", "questfeed_own_board_dm@example.com", name: "Quest Feed Own Board DM");

        await SeedBoardAsync(5, "Quest Feed Own Board");
        await SeedMembershipAsync(reader.Id, 5);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);

        var seatedQuestId = await SeedQuestAsync(5, dungeonMaster.Id, "Quest Feed Seated Session", finalizedDate);
        await SeedPlayerSignupAsync(seatedQuestId, reader.Id);

        // Same board the reader belongs to, owned by someone else, with no signup row for the
        // reader at all and the reader is not its Dungeon Master. Without this fact, the two
        // facts above are equally well satisfied by a query that returns every finalized quest
        // on every one-shot board the reader belongs to -- a real leak of the reader's own
        // board's scheduling, and precisely the shape the "only quests the reader is signed up
        // for" constraint rules out.
        var unrelatedQuestId = await SeedQuestAsync(5, dungeonMaster.Id, "Quest Feed Unseated Session", finalizedDate.AddHours(1));

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("SUMMARY:[Quest Feed Own Board] Quest Feed Seated Session\r\n");
        body.Should().NotContain($"questboard-quest-{unrelatedQuestId}");
        body.Should().NotContain("Quest Feed Unseated Session");
        CountVEvents(body).Should().Be(1);
    }

    [Fact]
    public async Task Feed_WaitlistedSignup_StaysOutUntilTheSameRowIsPromotedToAConfirmedSeat()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_waitlist_reader", "questfeed_waitlist_reader@example.com", name: "Quest Feed Waitlist Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_waitlist_dm", "questfeed_waitlist_dm@example.com", name: "Quest Feed Waitlist DM");

        await SeedBoardAsync(6, "Quest Feed Waitlist Board");
        await SeedMembershipAsync(reader.Id, 6);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(6, dungeonMaster.Id, "Quest Feed Waitlist Session", finalizedDate);

        // A row is not a seat -- the confirmed-seat flag is not set, exactly the shape a
        // waitlisted player's row takes.
        await SeedPlayerSignupAsync(questId, reader.Id, isSelected: false);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var beforePromotionResponse = await FetchFeedAsync(subscription.Token);
        var beforePromotionBody = await beforePromotionResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Accepted cost of this rule: a waitlisted player gets no advance warning of a night
        // they may well end up playing.
        beforePromotionBody.Should().NotContain($"questboard-quest-{questId}");
        beforePromotionBody.Should().NotContain("Quest Feed Waitlist Session");
        CountVEvents(beforePromotionBody).Should().Be(0);

        // Flip the same row's confirmed-seat flag through the seeding context -- nothing else
        // about the row changes. This is the shape a real promotion off the waitlist takes.
        await using (var ctx = factory.Database.CreateContext())
        {
            var signup = await ctx.PlayerSignups.IgnoreQueryFilters()
                .SingleAsync(ps => ps.QuestId == questId && ps.PlayerId == reader.Id, TestContext.Current.CancellationToken);
            signup.IsSelected = true;
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var afterPromotionResponse = await FetchFeedAsync(subscription.Token);
        var afterPromotionBody = await afterPromotionResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // A promotion off the waitlist reaches the phone at the next fetch like any other change.
        afterPromotionBody.Should().Contain($"questboard-quest-{questId}");
        CountVEvents(afterPromotionBody).Should().Be(1);
    }

    [Fact]
    public async Task Feed_AllThreeSignupRoles_ReachTheFeedIdenticallyWhenTheSeatIsConfirmed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_seat_kind_reader", "questfeed_seat_kind_reader@example.com", name: "Quest Feed Seat Kind Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_seat_kind_dm", "questfeed_seat_kind_dm@example.com", name: "Quest Feed Seat Kind DM");

        await SeedBoardAsync(7, "Quest Feed Seat Kind Board");
        await SeedMembershipAsync(reader.Id, 7);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);

        var playerQuestId = await SeedQuestAsync(7, dungeonMaster.Id, "Quest Feed Player Seat Session", finalizedDate);
        await SeedPlayerSignupAsync(playerQuestId, reader.Id, role: SignupRole.Player);

        var spectatorQuestId = await SeedQuestAsync(7, dungeonMaster.Id, "Quest Feed Spectator Seat Session", finalizedDate.AddHours(1));
        await SeedPlayerSignupAsync(spectatorQuestId, reader.Id, role: SignupRole.Spectator);

        var assistantDmQuestId = await SeedQuestAsync(7, dungeonMaster.Id, "Quest Feed Assistant DM Seat Session", finalizedDate.AddHours(2));
        await SeedPlayerSignupAsync(assistantDmQuestId, reader.Id, role: SignupRole.AssistantDM);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // No seat-kind branch exists in the predicate at all: the confirmed-seat flag is set
        // unconditionally for Spectator and AssistantDM signups, and only a Player ever lands on
        // the waitlist, so everyone holding a confirmed seat is at the table that night whatever
        // the seat is called.
        body.Should().Contain("Quest Feed Player Seat Session");
        body.Should().Contain("Quest Feed Spectator Seat Session");
        body.Should().Contain("Quest Feed Assistant DM Seat Session");
        CountVEvents(body).Should().Be(3);
    }

    [Fact]
    public async Task Feed_QuestFlaggedAsADungeonMasterSession_StillReachesAReaderHoldingAConfirmedSeat()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_dm_session_reader", "questfeed_dm_session_reader@example.com", name: "Quest Feed DM Session Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_dm_session_dm", "questfeed_dm_session_dm@example.com", name: "Quest Feed DM Session DM");

        await SeedBoardAsync(8, "Quest Feed DM Session Board");
        await SeedMembershipAsync(reader.Id, 8);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(8, dungeonMaster.Id, "Quest Feed DM-Flagged Session", finalizedDate, dungeonMasterSession: true);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // This flag hides a quest from the board listing, but the quest details request applies
        // no gate whatsoever -- anyone holding the link can already open one. Honouring a seat
        // that was actually granted therefore widens nothing. Accepted cost: a Dungeon Master
        // who flips this flag after players signed up leaves those readers' phones carrying a
        // title the board no longer lists for them.
        body.Should().Contain($"questboard-quest-{questId}");
        body.Should().Contain("Quest Feed DM-Flagged Session");
    }
}
