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

    // Mutates an already-seeded quest's finalized state or finalized date through the
    // unfiltered seeding context -- covers un-finalizing a quest back to voting and moving its
    // finalized date, the two exits that change the row in place rather than remove it outright.
    private async Task MutateQuestAsync(int questId, Action<QuestEntity> mutate)
    {
        await using var ctx = factory.Database.CreateContext();
        var quest = await ctx.Quests.IgnoreQueryFilters()
            .SingleAsync(q => q.Id == questId, TestContext.Current.CancellationToken);
        mutate(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Deletes a quest row outright through the unfiltered seeding context -- distinct from
    // un-finalizing, which leaves the row in place with its finalized fields cleared.
    private async Task DeleteQuestAsync(int questId)
    {
        await using var ctx = factory.Database.CreateContext();
        var quest = await ctx.Quests.IgnoreQueryFilters()
            .SingleAsync(q => q.Id == questId, TestContext.Current.CancellationToken);
        ctx.Quests.Remove(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Deletes a signup row outright through the unfiltered seeding context -- distinct from the
    // waitlist-promotion fact elsewhere in this file, which flips the confirmed-seat flag on a
    // surviving row. Withdrawing from a quest removes the row itself rather than leaving a
    // demoted one behind.
    private async Task DeleteSignupAsync(int questId, int playerId)
    {
        await using var ctx = factory.Database.CreateContext();
        var signup = await ctx.PlayerSignups.IgnoreQueryFilters()
            .SingleAsync(ps => ps.QuestId == questId && ps.PlayerId == playerId, TestContext.Current.CancellationToken);
        ctx.PlayerSignups.Remove(signup);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Seeds one event on the named board and returns its id -- copied from
    // CalendarSubscriptionFeedTests' own event seeder rather than inventing a second shape.
    // The campaign-board fact and the ordering fact both need to seed events alongside quests.
    private async Task<int> SeedEventAsync(int groupId, string title, DateOnly date, TimeOnly? startTime = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = date,
            StartTime = startTime,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    // Seeds an event signup row for the given user on the given event -- distinct from
    // SeedPlayerSignupAsync above, which seeds a quest signup row on a different entity.
    private async Task SeedEventSignupAsync(int eventId, int userId, VoteType availability)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Deletes a single membership row through the unfiltered seeding context, mirroring
    // production's "leaving a board" outcome on the UserGroups table directly -- the fact that
    // uses this only cares that the membership row is gone before the next fetch.
    private async Task RemoveMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        var membership = ctx.UserGroups.First(ug => ug.UserId == userId && ug.GroupId == groupId);
        ctx.UserGroups.Remove(membership);
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

    // ---- Disappearance: every route by which a quest stops qualifying ----
    //
    // Each fact below is a transition -- seed a qualifying quest, fetch and assert it is
    // present, change exactly one thing through the seeding context, fetch the same address
    // again, and assert on the second body. A predicate that lost the clause under test would
    // pass a fact that only ever fetched once against a feed that simply never updates; fetching
    // twice against one subscription is what actually proves the removal rather than merely two
    // unrelated end states.
    //
    // No fact here exercises a closed quest. Close is campaign-only and rejects with BadRequest
    // on a one-shot board, so IsClosed is unreachable for every quest this phase can emit -- a
    // fact asserting behaviour for it would be asserting on dead code.

    [Fact]
    public async Task Feed_UnfinalizedQuest_DisappearsFromTheVeryNextFetch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_unfinalize_reader", "questfeed_unfinalize_reader@example.com", name: "Quest Feed Unfinalize Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_unfinalize_dm", "questfeed_unfinalize_dm@example.com", name: "Quest Feed Unfinalize DM");

        await SeedBoardAsync(9, "Quest Feed Unfinalize Board");
        await SeedMembershipAsync(reader.Id, 9);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(9, dungeonMaster.Id, "Quest Feed Unfinalize Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var beforeResponse = await FetchFeedAsync(subscription.Token);
        var beforeBody = await beforeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeBody.Should().Contain("Quest Feed Unfinalize Session");
        CountVEvents(beforeBody).Should().Be(1);

        // Sends the quest back to voting -- both the finalized flag and the finalized date clear
        // together, matching what un-finalizing a quest actually does to the row.
        await MutateQuestAsync(questId, q =>
        {
            q.IsFinalized = false;
            q.FinalizedDate = null;
        });

        var afterResponse = await FetchFeedAsync(subscription.Token);
        var afterBody = await afterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        afterBody.Should().NotContain("Quest Feed Unfinalize Session");
        afterBody.Should().NotContain($"questboard-quest-{questId}");
        CountVEvents(afterBody).Should().Be(0);

        // The decision locked here is that no tombstone is emitted at all -- not that a
        // particular status value is avoided -- so this checks the whole document for the
        // property itself rather than for one value of it. A cancellation therefore reaches the
        // subscriber only as a silent disappearance, which is most acute for a called-off game
        // night: a reader who is no longer looking for the entry is unlikely to notice it left.
        afterBody.Should().NotContain("STATUS");
    }

    [Fact]
    public async Task Feed_DeletedQuest_DisappearsFromTheVeryNextFetch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_delete_reader", "questfeed_delete_reader@example.com", name: "Quest Feed Delete Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_delete_dm", "questfeed_delete_dm@example.com", name: "Quest Feed Delete DM");

        await SeedBoardAsync(10, "Quest Feed Delete Board");
        await SeedMembershipAsync(reader.Id, 10);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(10, dungeonMaster.Id, "Quest Feed Delete Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var beforeResponse = await FetchFeedAsync(subscription.Token);
        var beforeBody = await beforeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeBody.Should().Contain("Quest Feed Delete Session");
        CountVEvents(beforeBody).Should().Be(1);

        // Removes the row entirely -- the quest is gone, not merely un-finalized.
        await DeleteQuestAsync(questId);

        var afterResponse = await FetchFeedAsync(subscription.Token);
        var afterBody = await afterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        afterBody.Should().NotContain("Quest Feed Delete Session");
        afterBody.Should().NotContain($"questboard-quest-{questId}");
        CountVEvents(afterBody).Should().Be(0);
        afterBody.Should().NotContain("STATUS");
    }

    [Fact]
    public async Task Feed_QuestWhoseReaderSignupRowIsDeleted_DisappearsFromTheVeryNextFetch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_seat_withdrawn_reader", "questfeed_seat_withdrawn_reader@example.com", name: "Quest Feed Seat Withdrawn Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_seat_withdrawn_dm", "questfeed_seat_withdrawn_dm@example.com", name: "Quest Feed Seat Withdrawn DM");

        await SeedBoardAsync(11, "Quest Feed Seat Withdrawn Board");
        await SeedMembershipAsync(reader.Id, 11);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(11, dungeonMaster.Id, "Quest Feed Seat Withdrawn Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var beforeResponse = await FetchFeedAsync(subscription.Token);
        var beforeBody = await beforeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeBody.Should().Contain("Quest Feed Seat Withdrawn Session");
        CountVEvents(beforeBody).Should().Be(1);

        // Withdrawing from a quest removes the signup row itself -- distinct from the
        // waitlist-promotion fact elsewhere in this file, which flips the confirmed-seat flag on
        // a surviving row.
        await DeleteSignupAsync(questId, reader.Id);

        var afterResponse = await FetchFeedAsync(subscription.Token);
        var afterBody = await afterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        afterBody.Should().NotContain("Quest Feed Seat Withdrawn Session");
        afterBody.Should().NotContain($"questboard-quest-{questId}");
        CountVEvents(afterBody).Should().Be(0);
        afterBody.Should().NotContain("STATUS");
    }

    [Fact]
    public async Task Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_reschedule_reader", "questfeed_reschedule_reader@example.com", name: "Quest Feed Reschedule Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_reschedule_dm", "questfeed_reschedule_dm@example.com", name: "Quest Feed Reschedule DM");

        await SeedBoardAsync(12, "Quest Feed Reschedule Board");
        await SeedMembershipAsync(reader.Id, 12);

        var options = GetFeedOptions();
        var finalizedDate = DateTime.Today.AddDays(3).AddHours(19);
        var questId = await SeedQuestAsync(12, dungeonMaster.Id, "Quest Feed Reschedule Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var firstResponse = await FetchFeedAsync(subscription.Token);
        var firstBody = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var firstUidLine = firstBody.Split("\r\n").Single(line => line.StartsWith($"UID:questboard-quest-{questId}", StringComparison.Ordinal));

        // Move the finalized date to a different date, still comfortably inside the window.
        var newDate = finalizedDate.AddDays(2);
        await MutateQuestAsync(questId, q => q.FinalizedDate = newDate);

        var secondResponse = await FetchFeedAsync(subscription.Token);
        var secondBody = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var secondUidLine = secondBody.Split("\r\n").Single(line => line.StartsWith($"UID:questboard-quest-{questId}", StringComparison.Ordinal));

        secondBody.Should().Contain($"DTSTART:{newDate:yyyyMMdd}T{newDate:HHmmss}");

        // The load-bearing assertion: the identifier captured from the first fetch is
        // byte-identical to the one captured from the second, rather than a freshly-derived
        // expectation from the quest id. An identifier that changed between fetches would make
        // every subscriber's phone accumulate a fresh copy of the session on every reschedule
        // instead of updating the existing entry in place, and nothing server-side would show it.
        secondUidLine.Should().Be(firstUidLine);
        CountVEvents(secondBody).Should().Be(1);

        // Move the finalized date outside the window entirely -- the fourth exit route, and the
        // same predicate clause the window facts below pin from the other direction.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowEnd = today.AddMonths(options.MonthsAhead);
        var outsideWindowDate = windowEnd.AddDays(1).ToDateTime(new TimeOnly(19, 0));
        await MutateQuestAsync(questId, q => q.FinalizedDate = outsideWindowDate);

        var thirdResponse = await FetchFeedAsync(subscription.Token);
        var thirdBody = await thirdResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        thirdBody.Should().NotContain("Quest Feed Reschedule Session");
        thirdBody.Should().NotContain($"questboard-quest-{questId}");
        CountVEvents(thirdBody).Should().Be(0);
        thirdBody.Should().NotContain("STATUS");
    }

    // ---- The shared rolling window, both bounds ----

    [Fact]
    public async Task Feed_QuestJustInsideTheBackwardWindowBound_AppearsWhileOneJustOutsideDoesNot()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_backward_window_reader", "questfeed_backward_window_reader@example.com", name: "Quest Feed Backward Window Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_backward_window_dm", "questfeed_backward_window_dm@example.com", name: "Quest Feed Backward Window DM");

        // Board name kept short deliberately -- at the octet count RFC 5545 folds a SUMMARY
        // line, a folded continuation line splits a plain Contain() assertion's expected text
        // across two physical lines even though the underlying title is correct and unchanged.
        await SeedBoardAsync(13, "Quest Feed Window Board");
        await SeedMembershipAsync(reader.Id, 13);

        var options = GetFeedOptions();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowStart = today.AddMonths(-options.MonthsBack);

        // Every date here is derived from the running host's own configured month count, never
        // from a literal number of months -- a fact that hard-coded "three months back" would
        // stop testing the bound the moment the default changed, and would silently keep
        // passing. Each date sits a full day clear of the bound rather than an hour: the window
        // bounds are derived from a coordinated-universal clock while the quest's finalized date
        // is stored in server local time, so a date within the host's own UTC offset of a bound
        // is genuinely ambiguous by design, not flaky by accident. This fact does not probe the
        // exact boundary instant.
        var insideDate = windowStart.AddDays(1).ToDateTime(new TimeOnly(19, 0));
        var outsideDate = windowStart.AddDays(-1).ToDateTime(new TimeOnly(19, 0));

        var insideQuestId = await SeedQuestAsync(13, dungeonMaster.Id, "Quest Feed Backward Inside Session", insideDate);
        await SeedPlayerSignupAsync(insideQuestId, reader.Id);

        var outsideQuestId = await SeedQuestAsync(13, dungeonMaster.Id, "Quest Feed Backward Outside Session", outsideDate);
        await SeedPlayerSignupAsync(outsideQuestId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("Quest Feed Backward Inside Session");
        body.Should().NotContain("Quest Feed Backward Outside Session");
        CountVEvents(body).Should().Be(1);
    }

    [Fact]
    public async Task Feed_QuestJustInsideTheForwardWindowBound_AppearsWhileOneJustOutsideDoesNot()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_forward_window_reader", "questfeed_forward_window_reader@example.com", name: "Quest Feed Forward Window Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_forward_window_dm", "questfeed_forward_window_dm@example.com", name: "Quest Feed Forward Window DM");

        await SeedBoardAsync(14, "Quest Feed Window Board");
        await SeedMembershipAsync(reader.Id, 14);

        var options = GetFeedOptions();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowEnd = today.AddMonths(options.MonthsAhead);

        var insideDate = windowEnd.AddDays(-1).ToDateTime(new TimeOnly(19, 0));
        var outsideDate = windowEnd.AddDays(1).ToDateTime(new TimeOnly(19, 0));

        var insideQuestId = await SeedQuestAsync(14, dungeonMaster.Id, "Quest Feed Forward Inside Session", insideDate);
        await SeedPlayerSignupAsync(insideQuestId, reader.Id);

        var outsideQuestId = await SeedQuestAsync(14, dungeonMaster.Id, "Quest Feed Forward Outside Session", outsideDate);
        await SeedPlayerSignupAsync(outsideQuestId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("Quest Feed Forward Inside Session");
        body.Should().NotContain("Quest Feed Forward Outside Session");
        CountVEvents(body).Should().Be(1);

        // No second pair of window knobs exists for quests: CalendarFeedOptions exposes exactly
        // one backward bound and one forward bound, and both window facts in this file read
        // those same two properties -- there is no separate Quest-only pair sitting alongside
        // them for a quest predicate to drift out of sync with.
        typeof(CalendarFeedOptions).GetProperties()
            .Count(p => p.Name.Contains("MonthsBack", StringComparison.Ordinal)).Should().Be(1);
        typeof(CalendarFeedOptions).GetProperties()
            .Count(p => p.Name.Contains("MonthsAhead", StringComparison.Ordinal)).Should().Be(1);
    }

    // ---- Membership and board type, each proven independently against a second board ----
    //
    // Every fact below seeds two boards and asserts an outcome on each within one fetch. A
    // suite built only from a single-board absence fact would stay entirely green even if the
    // predicate collapsed to one board or one scope entirely -- absence alone cannot
    // distinguish "correctly scoped" from "accidentally scoped to nothing."

    [Fact]
    public async Task Feed_BoardTypeNarrowsQuestsButNotEvents_WithinTheSameFetch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_board_type_reader", "questfeed_board_type_reader@example.com", name: "Quest Feed Board Type Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_board_type_dm", "questfeed_board_type_dm@example.com", name: "Quest Feed Board Type DM");

        await SeedBoardAsync(15, "Quest Feed One-Shot Board", BoardType.OneShot);
        await SeedBoardAsync(16, "Quest Feed Campaign Board", BoardType.Campaign);
        await SeedMembershipAsync(reader.Id, 15);
        await SeedMembershipAsync(reader.Id, 16);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);

        var oneShotQuestId = await SeedQuestAsync(15, dungeonMaster.Id, "Quest Feed One-Shot Session", finalizedDate);
        await SeedPlayerSignupAsync(oneShotQuestId, reader.Id);

        var campaignQuestId = await SeedQuestAsync(16, dungeonMaster.Id, "Quest Feed Campaign Session", finalizedDate);
        await SeedPlayerSignupAsync(campaignQuestId, reader.Id);

        var campaignEventDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var campaignEventId = await SeedEventAsync(16, "Quest Feed Campaign Board Event", campaignEventDate);
        await SeedEventSignupAsync(campaignEventId, reader.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("Quest Feed One-Shot Session");
        body.Should().NotContain("Quest Feed Campaign Session");
        body.Should().Contain("Quest Feed Campaign Board Event");

        // The previous phase gave events every board's reach, this phase narrows quests only,
        // and a change that retroactively restricted events to one-shot boards would satisfy
        // every other assertion in this suite while quietly emptying half of every existing
        // subscriber's calendar. This is the guard against that, and it is the only one.
        CountVEvents(body).Should().Be(2);
    }

    [Fact]
    public async Task Feed_CampaignQuestTheReaderRunsAsDungeonMaster_StaysOutAlongsideAQualifyingOneShotQuest()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_dm_board_type_reader", "questfeed_dm_board_type_reader@example.com", name: "Quest Feed DM Board Type Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_dm_board_type_dm", "questfeed_dm_board_type_dm@example.com", name: "Quest Feed DM Board Type DM");

        await SeedBoardAsync(17, "Quest Feed DM One-Shot Board", BoardType.OneShot);
        await SeedBoardAsync(18, "Quest Feed DM Campaign Board", BoardType.Campaign);
        await SeedMembershipAsync(reader.Id, 17);
        await SeedMembershipAsync(reader.Id, 18);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);

        var oneShotQuestId = await SeedQuestAsync(17, dungeonMaster.Id, "Quest Feed DM One-Shot Session", finalizedDate);
        await SeedPlayerSignupAsync(oneShotQuestId, reader.Id);

        // Deliberately the same scenario as the previous fact from a different route in: the
        // reader owns this campaign quest as Dungeon Master, with no signup row of their own on
        // it. This is the route that can escape board scoping if the two conditions -- seat and
        // Dungeon Master ownership -- are ever composed as separate queries rather than one
        // predicate. A second way into the feed is a second way to bypass a filter that is not
        // applied to both ways at once.
        var campaignQuestId = await SeedQuestAsync(18, reader.Id, "Quest Feed DM Campaign Session", finalizedDate.AddHours(1));

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("Quest Feed DM One-Shot Session");
        body.Should().NotContain("Quest Feed DM Campaign Session");
        body.Should().NotContain($"questboard-quest-{campaignQuestId}");
        CountVEvents(body).Should().Be(1);
    }

    [Fact]
    public async Task Feed_ConfirmedSeatOnABoardTheReaderNeverJoined_StaysOutAlongsideAQualifyingQuest()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_non_member_board_reader", "questfeed_non_member_board_reader@example.com", name: "Quest Feed Non-Member Board Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_non_member_board_dm", "questfeed_non_member_board_dm@example.com", name: "Quest Feed Non-Member Board DM");

        await SeedBoardAsync(19, "Quest Feed Member Board");
        await SeedBoardAsync(20, "Quest Feed Non-Member Board");
        await SeedMembershipAsync(reader.Id, 19);
        // Deliberately no membership row for the reader on board 20.

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);

        var memberQuestId = await SeedQuestAsync(19, dungeonMaster.Id, "Quest Feed Member Board Session", finalizedDate);
        await SeedPlayerSignupAsync(memberQuestId, reader.Id);

        // An impossible state in production, seeded deliberately: a confirmed seat row for the
        // reader on a board they never joined. Without this row, a query that scoped on seat
        // alone and ignored boards entirely would still pass this fact, because in normal data
        // a non-member never holds a seat at all -- this row is what makes the fact actually
        // test the board predicate rather than the seat predicate.
        var nonMemberQuestId = await SeedQuestAsync(20, dungeonMaster.Id, "Quest Feed Non-Member Board Session", finalizedDate.AddHours(1));
        await SeedPlayerSignupAsync(nonMemberQuestId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var response = await FetchFeedAsync(subscription.Token);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("Quest Feed Member Board Session");
        body.Should().NotContain("Quest Feed Non-Member Board Session");
        body.Should().NotContain($"questboard-quest-{nonMemberQuestId}");
        CountVEvents(body).Should().Be(1);
    }

    [Fact]
    public async Task Feed_LeavingABoard_RemovesItsQuestFromTheVeryNextFetch_WithNoErrorLoggedOnAHealthyFetch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var reader = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_leave_board_reader", "questfeed_leave_board_reader@example.com", name: "Quest Feed Leave Board Reader");
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questfeed_leave_board_dm", "questfeed_leave_board_dm@example.com", name: "Quest Feed Leave Board DM");

        await SeedBoardAsync(21, "Quest Feed Leave Board");
        await SeedMembershipAsync(reader.Id, 21);

        var finalizedDate = DateTime.Today.AddDays(1).AddHours(19);
        var questId = await SeedQuestAsync(21, dungeonMaster.Id, "Quest Feed Leave Board Session", finalizedDate);
        await SeedPlayerSignupAsync(questId, reader.Id);

        var subscription = await MintSubscriptionAsync(reader.Id);
        factory.TestGroupContext.ActiveGroupId = null;
        factory.LogCapture.Clear();

        var beforeResponse = await FetchFeedAsync(subscription.Token);
        var beforeBody = await beforeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeBody.Should().Contain("Quest Feed Leave Board Session");
        CountVEvents(beforeBody).Should().Be(1);

        // A normal, correctly scoped fetch must produce no error -- the drop-and-log branch is
        // for a predicate that has gone wrong, and a re-check that fires on healthy data is a
        // re-check that will be ignored when it matters.
        factory.LogCapture.Records.Should().NotContain(record => record.Contains("[Error]"));

        // Membership is re-read from the database on every fetch, so a board a member leaves
        // disappears on the very next poll rather than whenever a cache happens to expire.
        await RemoveMembershipAsync(reader.Id, 21);

        var afterResponse = await FetchFeedAsync(subscription.Token);
        var afterBody = await afterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        afterBody.Should().NotContain("Quest Feed Leave Board Session");
        afterBody.Should().NotContain($"questboard-quest-{questId}");
        CountVEvents(afterBody).Should().Be(0);
    }
}
