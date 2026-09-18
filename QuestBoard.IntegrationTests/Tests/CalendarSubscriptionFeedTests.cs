using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the application's own scoping and response logic over real HTTP for the calendar
/// feed endpoint: an anonymous, cookie-less GET against a minted address returns exactly the
/// events the subscription's owner holds a signup row on, across every board that owner
/// belongs to, and never a row from a board that owner does not belong to -- re-checked on
/// every fetch rather than cached.
///
/// What these facts do NOT establish, and must not be read as establishing: end-to-end
/// isolation against the database the application actually runs on. The shared harness backs
/// every suite with the EF Core InMemory provider, which evaluates every predicate as ordinary
/// LINQ-to-Objects. Two properties the repository query depends on therefore never reach a
/// relational query compiler here: the board containment test (memberGroupIds.Contains(...))
/// over an *empty* id collection is exercised only as List.Contains, never as its SQL
/// translation; and the filter bypass interacting with the signup entity's own board-navigating
/// filter -- which reaches the board through the event's own navigation, not a direct column --
/// generates no join at all in memory. The facts below are about the application's own scoping
/// logic; proving the query translates and behaves the same way on a relational provider needs
/// a separate, relational test, matching the precedent AgendaTenantIsolationTests set.
/// </summary>
public class CalendarSubscriptionFeedTests(WebApplicationFactoryBase factory)
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

    // Seeds one event on the named board and returns its id.
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

    // Adds a membership row for an already-created user on the named board.
    private async Task SeedMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Seeds a signup row for the given user on the given event.
    private async Task SeedSignupAsync(int eventId, int userId, VoteType availability)
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

    // Mints through the real service from a scope, rather than a direct database insert, so
    // the fact exercises the same production write path the Profile page will call in a
    // later plan -- including the cryptographically random address generation.
    private async Task<CalendarSubscription> MintSubscriptionAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        return await subscriptionService.MintForUserAsync(userId, TestContext.Current.CancellationToken);
    }

    // Deletes a single membership row through the unfiltered seeding context, mirroring
    // production's "leaving a board" outcome on the UserGroups table directly rather than
    // through the domain service -- the fact that uses this only cares that the membership row
    // is gone before the next fetch, not which code path removed it.
    private async Task RemoveMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        var membership = ctx.UserGroups.First(ug => ug.UserId == userId && ug.GroupId == groupId);
        ctx.UserGroups.Remove(membership);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Feed_ServesSubscribedEvent_ToAnonymousCaller()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_tracer", "calfeed_tracer@example.com", name: "Calendar Feed Tracer User");

        // Group 1 is the default seeded group ("EuphoriaInn") -- a distinct id is used here so
        // the board's own name is under this fact's control rather than borrowed.
        await SeedBoardAsync(2, "Calendar Feed Tracer Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var eventId = await SeedEventAsync(2, "Calendar Feed Tracer Session", eventDate, new TimeOnly(19, 0));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);

        // Set after minting and before the request, so the fact proves the feed does not lean
        // on an active board -- a real calendar client sends no cookie and has none.
        factory.TestGroupContext.ActiveGroupId = null;

        // A fresh, plain client with no authorization header at all -- the genuinely anonymous
        // path a real calendar client takes.
        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.ToString().Should().Be("text/calendar; charset=utf-8");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().StartWith("BEGIN:VCALENDAR");
        body.Should().EndWith("END:VCALENDAR\r\n");
        body.Should().Contain($"UID:questboard-event-{eventId}");
        body.Should().Contain("SUMMARY:[Calendar Feed Tracer Board] Calendar Feed Tracer Session");
        body.Should().Contain("TRANSP:TRANSPARENT");
        body.Should().Contain($"DTSTART:{eventDate:yyyyMMdd}T190000");
        body.Should().NotContain($"DTSTART:{eventDate:yyyyMMdd}T190000Z");

        var vEventCount = body.Split("BEGIN:VEVENT").Length - 1;
        vEventCount.Should().Be(1);

        // Every line is CRLF-terminated: splitting on "\r\n" and rejoining reproduces the body
        // exactly, and the body contains no lone "\n" that is not preceded by "\r".
        var lines = body.Split("\r\n");
        string.Join("\r\n", lines).Should().Be(body);
        body.Replace("\r\n", string.Empty).Should().NotContain("\n");
    }

    [Fact]
    public async Task Feed_IncludesEventsFromEveryBoardTheViewerBelongsTo()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_two_boards", "calfeed_two_boards@example.com", name: "Calendar Feed Two Board User");

        await SeedBoardAsync(2, "Two Board Suite Board Two");
        await SeedBoardAsync(3, "Two Board Suite Board Three");
        await SeedMembershipAsync(user.Id, 2);
        await SeedMembershipAsync(user.Id, 3);

        var boardTwoEventId = await SeedEventAsync(2, "Two Board Suite Board Two Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(boardTwoEventId, user.Id, VoteType.Yes);

        var boardThreeEventId = await SeedEventAsync(3, "Two Board Suite Board Three Session", DateOnly.FromDateTime(DateTime.Today).AddDays(2));
        await SeedSignupAsync(boardThreeEventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // This is the fact that proves aggregation rather than mere absence: a feed that
        // silently collapsed to a single joined board would still pass every exclusion fact in
        // this suite while failing only the positive assertions below, so this is the one place
        // a collapse-to-one-board bug would actually be caught.
        body.Should().Contain("[Two Board Suite Board Two] Two Board Suite Board Two Session");
        body.Should().Contain("[Two Board Suite Board Three] Two Board Suite Board Three Session");

        var vEventCount = body.Split("BEGIN:VEVENT").Length - 1;
        vEventCount.Should().Be(2);
    }

    [Fact]
    public async Task Feed_ExcludesAnEventOnABoardTheViewerDoesNotBelongTo()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_non_member", "calfeed_non_member@example.com", name: "Calendar Feed Non-Member User");

        await SeedBoardAsync(2, "Non-Member Suite Board Two");
        await SeedBoardAsync(3, "Non-Member Suite Board Three");
        await SeedMembershipAsync(user.Id, 2);
        // The viewer is deliberately NOT a member of board three.

        var ownEventId = await SeedEventAsync(2, "Non-Member Suite Board Two Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(ownEventId, user.Id, VoteType.Yes);

        var foreignEventId = await SeedEventAsync(3, "Non-Member Suite Board Three Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        // Seeded on purpose: membership is the authorisation and is checked at read time, and
        // the existence of a signup row is not proof of it -- a row can outlive a membership
        // (leaving a board deletes signup rows as a cleanup, but that is housekeeping, not
        // access control), so this fact proves the read does not trust the row.
        await SeedSignupAsync(foreignEventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("Non-Member Suite Board Two Session");
        body.Should().NotContain("Non-Member Suite Board Three Session");
        body.Should().NotContain("Non-Member Suite Board Three");
    }

    [Fact]
    public async Task Feed_StopsIncludingABoardTheViewerHasLeft()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_leaves", "calfeed_leaves@example.com", name: "Calendar Feed Leaving User");

        await SeedBoardAsync(2, "Leaving Suite Board Two");
        await SeedMembershipAsync(user.Id, 2);

        var eventId = await SeedEventAsync(2, "Leaving Suite Board Two Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();

        var before = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyBefore = await before.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Proves a change happened rather than an absence that was always true.
        bodyBefore.Should().Contain("Leaving Suite Board Two Session");

        // Membership is re-read from the database on every fetch, so a board a member leaves
        // disappears on the very next poll rather than whenever a cache happens to expire.
        await RemoveMembershipAsync(user.Id, 2);

        var after = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyAfter = await after.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        bodyAfter.Should().NotContain("Leaving Suite Board Two Session");
    }

    [Fact]
    public async Task Feed_ReturnsAValidEmptyCalendar_ForAViewerWithNoBoards()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        // A user seeded with no membership row at all -- not even the default board.
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_no_boards", "calfeed_no_boards@example.com", name: "Calendar Feed No Boards User");

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        factory.LogCapture.Clear();

        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.ToString().Should().Be("text/calendar; charset=utf-8");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().StartWith("BEGIN:VCALENDAR");
        body.Should().EndWith("END:VCALENDAR\r\n");

        var vEventCount = body.Split("BEGIN:VEVENT").Length - 1;
        vEventCount.Should().Be(0);

        // An empty membership set is a legitimate state, not an invariant violation -- the
        // repository read is still performed for this caller rather than short-circuited,
        // because a short-circuit would hide a predicate regression for exactly the caller with
        // no rights. That means this caller's read must never produce the second-layer
        // re-check's error log either.
        factory.LogCapture.Records.Should().NotContain(record => record.Contains("[Error]"));
    }

    // Matches the "calendar-feed" rate-limiting policy's PermitLimit in Program.cs. The policy
    // is defined in code rather than configuration, so this cannot be read at runtime -- kept
    // as a single named constant with this comment so a future change to the policy's
    // PermitLimit does not silently desynchronize this fact from the value it must exceed.
    private const int CalendarFeedPolicyPermitLimit = 20;

    [Fact]
    public async Task Feed_LogsNoSubscriptionAddress_WhenServingALiveAddress()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_logsafe", "calfeed_logsafe@example.com", name: "Calendar Feed Log Safety User");

        await SeedBoardAsync(2, "Calendar Feed Log Safety Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var eventId = await SeedEventAsync(2, "Calendar Feed Log Safety Session", eventDate, new TimeOnly(19, 0));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        factory.LogCapture.Clear();

        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The address itself must never reach a captured record -- neither in a rendered
        // message nor as a structured state value, which is exactly what CapturingLoggerProvider
        // renders alongside the message.
        factory.LogCapture.Records.Should().NotContain(record => record.Contains(subscription.Token));

        // This does not by itself prove the harness is collecting anything: an accidentally
        // disabled provider would also produce zero records and this assertion would pass for
        // the wrong reason. A revoked-subscription fetch is guaranteed to log --
        // CalendarFeedController's Revoked branch calls LogInformation on every request -- so it
        // is used here as the harness's own smoke test, independent of whether the live-fetch
        // path above happens to log anything at all.
        var revokedSubscription = await MintSubscriptionAsync(user.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
            await subscriptionService.RevokeAsync(revokedSubscription.Id, user.Id, TestContext.Current.CancellationToken);
        }

        var revokedResponse = await client.GetAsync(
            $"/feeds/calendar/{revokedSubscription.Token}.ics", TestContext.Current.CancellationToken);
        revokedResponse.StatusCode.Should().Be(HttpStatusCode.Gone);

        factory.LogCapture.Records.Should().NotBeEmpty();
        factory.LogCapture.Records.Should().NotContain(record => record.Contains(revokedSubscription.Token));
    }

    [Fact]
    public async Task Feed_LogsNothing_ForAnAddressThatNeverExisted()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        factory.LogCapture.Clear();

        // Well-formed (43 URL-safe characters, matching a real minted address's shape) but
        // never minted.
        var unknownAddress = new string('A', 43);
        var response = await client.GetAsync(
            $"/feeds/calendar/{unknownAddress}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A stream of unknown addresses is what guessing looks like, so the NotFound branch logs
        // nothing at all rather than converting an attacker's traffic into unbounded log volume.
        // Checked against the category rather than the message text, so a future log call added
        // to this branch under a different category still fails this fact.
        factory.LogCapture.Records.Should().NotContain(record => record.Contains("CalendarFeedController"));
    }

    [Fact]
    public async Task Feed_Returns429_WhenTheAddressExceedsItsRequestBudget()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_budget", "calfeed_budget@example.com", name: "Calendar Feed Budget User");

        await SeedBoardAsync(2, "Calendar Feed Budget Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var eventId = await SeedEventAsync(2, "Calendar Feed Budget Session", eventDate, new TimeOnly(19, 0));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        // A dedicated address used by no other fact in this suite: the rate-limit budget is
        // partitioned per address (Program.cs), so exhausting a shared one here would poison
        // every other fact that fetches it.
        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < CalendarFeedPolicyPermitLimit + 1; i++)
        {
            lastResponse = await client.GetAsync(
                $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
