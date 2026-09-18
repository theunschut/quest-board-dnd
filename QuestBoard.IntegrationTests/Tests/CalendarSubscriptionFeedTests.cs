using Microsoft.Extensions.Options;
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

    // Seeds one event on the named board and returns its id. cancelledAt matches
    // EventEntity.CancelledAt's own meaning -- null is live, a value is a tombstone.
    private async Task<int> SeedEventAsync(int groupId, string title, DateOnly date, TimeOnly? startTime = null, DateTime? cancelledAt = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = date,
            StartTime = startTime,
            CancelledAt = cancelledAt,
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

    // Seeds a signup row for the given user on the given event. UpdatedAt null is what an
    // automatically created board-wide row looks like -- no person ever set that answer -- so
    // callers proving that shape pass answered: false rather than seeding a plain row and
    // hoping the default matches.
    private async Task SeedSignupAsync(int eventId, int userId, VoteType availability, bool answered = true)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = answered ? DateTime.UtcNow : null
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

    // Retires a subscription through the real service, matching the production write path a
    // member's Delete control will call in a later plan.
    private async Task RevokeSubscriptionAsync(int subscriptionId, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        await subscriptionService.RevokeAsync(subscriptionId, userId, TestContext.Current.CancellationToken);
    }

    // Reads the persisted last-fetched timestamp back through a fresh, unfiltered context, so
    // a throttle fact observes the write the request path actually made rather than a value
    // held in memory from before the request.
    private async Task<DateTime?> ReadLastFetchedAsync(int subscriptionId)
    {
        await using var ctx = factory.Database.CreateContext();
        var entity = await ctx.CalendarSubscriptions
            .AsNoTracking()
            .FirstAsync(cs => cs.Id == subscriptionId, TestContext.Current.CancellationToken);
        return entity.LastFetchedAt;
    }

    // Reads the configured window and throttle values from the running host's own DI
    // container, so a window fact derives its dates from configuration rather than from a
    // literal that silently drifts out of sync with a future change to the defaults.
    private CalendarFeedOptions GetFeedOptions()
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<CalendarFeedOptions>>().Value;
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

    // ---- Response codes ----

    [Fact]
    public async Task Feed_Returns410_ForARetiredAddress()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_retired", "calfeed_retired@example.com", name: "Calendar Feed Retired User");

        var subscription = await MintSubscriptionAsync(user.Id);
        await RevokeSubscriptionAsync(subscription.Id, user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Feed_Returns404_ForAnAddressThatNeverExisted()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        // Well-formed (43 URL-safe characters, matching a real minted address's shape) but
        // never minted.
        var unknownAddress = new string('B', 43);
        var response = await client.GetAsync(
            $"/feeds/calendar/{unknownAddress}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feed_Returns404_ForAnAddressThatIsNotWellFormed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        // Short and containing characters outside the Base64Url alphabet a real address is
        // always drawn from. An ill-formed address simply matches no row in the lookup, so
        // this is defence in depth rather than a separate validation rule -- the endpoint does
        // not need to recognise "malformed" as its own case to answer correctly.
        var malformedAddress = "not!well@formed";
        var response = await client.GetAsync(
            $"/feeds/calendar/{malformedAddress}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feed_KeepsAnsweringGone_AfterARetiredAddressIsFetchedRepeatedly()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_retired_twice", "calfeed_retired_twice@example.com", name: "Calendar Feed Retired Twice User");

        var subscription = await MintSubscriptionAsync(user.Id);
        await RevokeSubscriptionAsync(subscription.Id, user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();

        // Proves the tombstone persists rather than being cleaned up on first ask.
        var first = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        var second = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.Gone);
        second.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    // ---- Which events reach the feed ----

    [Fact]
    public async Task Feed_ExcludesAnEventTheViewerHoldsNoSignupRowOn()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_no_signup", "calfeed_no_signup@example.com", name: "Calendar Feed No Signup User");

        await SeedBoardAsync(2, "No Signup Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        var answeredEventId = await SeedEventAsync(2, "No Signup Suite Answered Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(answeredEventId, user.Id, VoteType.Yes);

        // No signup row is ever seeded for this event -- a one-shot session nobody has
        // answered yet never reaches the phone, an accepted cost of scoping the feed to
        // signup rows rather than board membership.
        await SeedEventAsync(2, "No Signup Suite Unanswered Session", DateOnly.FromDateTime(DateTime.Today).AddDays(2));

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("No Signup Suite Answered Session");
        body.Should().NotContain("No Signup Suite Unanswered Session");
    }

    [Fact]
    public async Task Feed_ExcludesACancelledEvent()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_cancelled", "calfeed_cancelled@example.com", name: "Calendar Feed Cancelled User");

        await SeedBoardAsync(2, "Cancelled Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        // A live signup row on a cancelled event -- the cancellation reaches the subscriber
        // only as a silent disappearance, the accepted cost of dropping the event entirely
        // rather than emitting a cancelled marker.
        var eventId = await SeedEventAsync(
            2, "Cancelled Suite Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1), cancelledAt: DateTime.UtcNow);
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().NotContain("Cancelled Suite Session");
    }

    [Fact]
    public async Task Feed_IncludesAnAutomaticallyCreatedSignupRow_WithAPlainTitle()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_auto_row", "calfeed_auto_row@example.com", name: "Calendar Feed Auto Row User");

        await SeedBoardAsync(2, "Auto Row Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventId = await SeedEventAsync(2, "Auto Row Suite Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        // answered: false is what an automatically created board-wide signup row looks like --
        // a Yes nobody chose.
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes, answered: false);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("SUMMARY:[Auto Row Suite Board] Auto Row Suite Session\r\n");
        body.Should().NotContain("(maybe)");
        body.Should().NotContain("(declined)");
    }

    [Fact]
    public async Task Feed_MarksAMaybeAnswer()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_maybe", "calfeed_maybe@example.com", name: "Calendar Feed Maybe User");

        await SeedBoardAsync(2, "Maybe Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventId = await SeedEventAsync(2, "Maybe Suite Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Maybe);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("SUMMARY:[Maybe Suite Board] Maybe Suite Session (maybe)\r\n");
    }

    [Fact]
    public async Task Feed_MarksADeclinedAnswer()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_declined", "calfeed_declined@example.com", name: "Calendar Feed Declined User");

        await SeedBoardAsync(2, "Declined Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventId = await SeedEventAsync(2, "Declined Suite Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.No);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("SUMMARY:[Declined Suite Board] Declined Suite Session (declined)\r\n");
    }

    [Fact]
    public async Task Feed_EmitsADateValuedEntry_ForAnEventWithNoStartTime()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_allday", "calfeed_allday@example.com", name: "Calendar Feed All Day User");

        await SeedBoardAsync(2, "All Day Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        // No startTime -- a true all-day entry, matching EventEntity.StartTime's own
        // documented meaning.
        var eventDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var eventId = await SeedEventAsync(2, "All Day Suite Session", eventDate);
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain($"DTSTART;VALUE=DATE:{eventDate:yyyyMMdd}");
    }

    // ---- Window bounds, both directions ----

    [Fact]
    public async Task Feed_ExcludesAnEventBeforeTheWindowStarts()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var options = GetFeedOptions();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowStart = today.AddMonths(-options.MonthsBack);

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_before_window", "calfeed_before_window@example.com", name: "Calendar Feed Before Window User");

        await SeedBoardAsync(2, "Before Window Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        // One day before the configured window start -- derived from the running host's own
        // configuration, not a literal, so a future change to MonthsBack cannot silently
        // invalidate this fact.
        var eventId = await SeedEventAsync(2, "Before Window Suite Session", windowStart.AddDays(-1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("Before Window Suite Session");
    }

    [Fact]
    public async Task Feed_IncludesAnEventInsideThePastWindow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var options = GetFeedOptions();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowStart = today.AddMonths(-options.MonthsBack);

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_inside_past", "calfeed_inside_past@example.com", name: "Calendar Feed Inside Past Window User");

        await SeedBoardAsync(2, "Inside Past Window Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        // One day inside the configured window start -- derived from configuration, mirroring
        // the excluded fact above so the two together pin the exact boundary.
        var eventId = await SeedEventAsync(2, "Inside Past Window Suite Session", windowStart.AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Inside Past Window Suite Session");
    }

    [Fact]
    public async Task Feed_ExcludesAnEventBeyondTheWindowEnd()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var options = GetFeedOptions();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowEnd = today.AddMonths(options.MonthsAhead);

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_beyond_window", "calfeed_beyond_window@example.com", name: "Calendar Feed Beyond Window User");

        await SeedBoardAsync(2, "Beyond Window Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventId = await SeedEventAsync(2, "Beyond Window Suite Session", windowEnd.AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("Beyond Window Suite Session");
    }

    [Fact]
    public async Task Feed_IncludesAnEventInsideTheFutureWindow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var options = GetFeedOptions();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowEnd = today.AddMonths(options.MonthsAhead);

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_inside_future", "calfeed_inside_future@example.com", name: "Calendar Feed Inside Future Window User");

        // Shorter names than the past-window fact's, deliberately: the board-prefixed,
        // suffix-carrying SUMMARY line folds at 75 octets (RFC 5545), and a folded line would
        // break this fact's own substring assertion across the fold rather than the feature it
        // is testing.
        await SeedBoardAsync(2, "Future Window Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventId = await SeedEventAsync(2, "Future Window Suite Session", windowEnd.AddDays(-1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Future Window Suite Session");
    }

    // ---- The throttle, over HTTP ----

    [Fact]
    public async Task Feed_RecordsAFetchTime_OnTheFirstFetch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_first_fetch", "calfeed_first_fetch@example.com", name: "Calendar Feed First Fetch User");

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        (await ReadLastFetchedAsync(subscription.Id)).Should().BeNull();

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadLastFetchedAsync(subscription.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Feed_DoesNotRewriteTheFetchTime_OnAnImmediateSecondFetch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_second_fetch", "calfeed_second_fetch@example.com", name: "Calendar Feed Second Fetch User");

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        await client.GetAsync($"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        var afterFirst = await ReadLastFetchedAsync(subscription.Id);
        afterFirst.Should().NotBeNull();

        // Works against the real clock precisely because the configured throttle interval is
        // minutes and this second request lands within milliseconds -- no fake clock needed.
        await client.GetAsync($"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        var afterSecond = await ReadLastFetchedAsync(subscription.Id);

        afterSecond.Should().Be(afterFirst);
    }

    // ---- Conditional requests ----

    [Fact]
    public async Task Feed_ReturnsAnEntityTag_OnALiveResponse()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_etag", "calfeed_etag@example.com", name: "Calendar Feed ETag User");

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);

        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.IsWeak.Should().BeFalse();
        response.Headers.ETag!.Tag.Should().StartWith("\"").And.EndWith("\"");
    }

    [Fact]
    public async Task Feed_Returns304_WhenTheClientPresentsTheMatchingEntityTag()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_matching_etag", "calfeed_matching_etag@example.com", name: "Calendar Feed Matching ETag User");

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var first = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        var tag = first.Headers.ETag!.Tag;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/feeds/calendar/{subscription.Token}.ics");
        request.Headers.TryAddWithoutValidation("If-None-Match", tag);
        var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task Feed_ReturnsAFreshBody_WhenTheClientPresentsAStaleEntityTag()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_stale_etag", "calfeed_stale_etag@example.com", name: "Calendar Feed Stale ETag User");

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/feeds/calendar/{subscription.Token}.ics");
        request.Headers.TryAddWithoutValidation("If-None-Match", "\"not-the-real-tag\"");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().StartWith("BEGIN:VCALENDAR");
    }

    [Fact]
    public async Task Feed_ChangesTheEntityTag_WhenAnEventIsEdited()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_etag_changes", "calfeed_etag_changes@example.com", name: "Calendar Feed ETag Changes User");

        await SeedBoardAsync(2, "ETag Changes Suite Board");
        await SeedMembershipAsync(user.Id, 2);

        var eventId = await SeedEventAsync(2, "ETag Changes Suite Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var first = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        var firstTag = first.Headers.ETag!.Tag;

        // The tag is taken over the emitted document, so an edit produces a new tag with no
        // modified-timestamp column the schema does not have. IgnoreQueryFilters() is
        // load-bearing here: this context's own ActiveGroupId is null (an anonymous request
        // selects no board), and EventEntity's HasQueryFilter treats a null active group as
        // zero rows rather than every row, so an unfiltered read is required to reach the row
        // at all.
        await using (var ctx = factory.Database.CreateContext())
        {
            var eventEntity = await ctx.Events.IgnoreQueryFilters().FirstAsync(e => e.Id == eventId);
            eventEntity.Title = "ETag Changes Suite Session (Edited)";
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var second = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        var secondTag = second.Headers.ETag!.Tag;

        secondTag.Should().NotBe(firstTag);
        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("ETag Changes Suite Session (Edited)");
    }

    [Fact]
    public async Task Feed_RecordsTheFetchTime_EvenOnANotModifiedResponse()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "calfeed_touch_on_304", "calfeed_touch_on_304@example.com", name: "Calendar Feed Touch On 304 User");

        var subscription = await MintSubscriptionAsync(user.Id);
        factory.TestGroupContext.ActiveGroupId = null;

        var client = factory.CreateClient();
        var first = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        var tag = first.Headers.ETag!.Tag;

        // Arranges a stored fetch time older than the configured throttle interval by writing
        // it directly, rather than waiting out the real interval, so this fact runs in
        // milliseconds like every other fact in the suite.
        var options = GetFeedOptions();
        var staleFetchTime = DateTime.UtcNow - TimeSpan.FromMinutes(options.LastFetchedThrottleMinutes) - TimeSpan.FromMinutes(1);
        await using (var ctx = factory.Database.CreateContext())
        {
            var entity = await ctx.CalendarSubscriptions.FirstAsync(cs => cs.Id == subscription.Id);
            entity.LastFetchedAt = staleFetchTime;
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/feeds/calendar/{subscription.Token}.ics");
        request.Headers.TryAddWithoutValidation("If-None-Match", tag);
        var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);

        // A poll that transferred nothing is still a poll -- the fetch time is the only way to
        // tell a live subscription from a dead one before retiring it.
        var fetchTimeAfter = await ReadLastFetchedAsync(subscription.Id);
        fetchTimeAfter.Should().NotBeNull();
        fetchTimeAfter.Should().BeAfter(staleFetchTime);
    }
}
