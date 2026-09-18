using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the application's own scoping and response logic over real HTTP for the calendar
/// feed's proven tracer slice: an anonymous, cookie-less GET against a freshly minted address
/// returns exactly one VEVENT for one event the subscription's owner holds a signup row on,
/// with no board selected anywhere. This does not prove the query translates the same way on a
/// relational provider, because the shared harness backs it with the EF Core InMemory provider,
/// which evaluates every predicate as ordinary LINQ-to-Objects.
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
