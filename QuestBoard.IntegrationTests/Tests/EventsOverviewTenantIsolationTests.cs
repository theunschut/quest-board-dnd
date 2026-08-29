using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Two-group tenant isolation tests for the availability overview page. The shared integration
/// harness defaults its active board to group 1 for every test class, so an ordinary fact on this
/// page is structurally blind to a cross-board leak -- it would need to already be looking at the
/// wrong board to notice one. This page also joins across events, signups and members in a single
/// aggregating read, which is exactly the shape where a filter bypass gets reached for, so these
/// facts seed a genuine second board through the unfiltered seeding context and prove neither its
/// events nor its members ever surface on the first board's page.
/// </summary>
public class EventsOverviewTenantIsolationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    // Seeds a genuine second board (creating it if needed) and one event on it, returning the
    // seeded event's id. Runs with no active board selected on the seeding context, which is
    // exactly what lets it write rows for a board the request pipeline itself could never read
    // or select.
    private async Task<int> SeedOtherBoardEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == 2))
        {
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherOverviewBoard", CreatedAt = DateTime.UtcNow });
        }

        var otherEvent = new EventEntity
        {
            Title = title,
            GroupId = 2,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(otherEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return otherEvent.Id;
    }

    // Seeds an event directly on group 1 through the unfiltered seeding context, returning the
    // new event's id.
    private async Task<int> SeedGroupOneEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = 1,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    // Seeds a user, their membership row on the given board, and a signup row for the given
    // event -- this is what makes a leaked column or a leaked count able to be detected rather
    // than merely observing an unfamiliar name.
    private async Task<int> SeedSignupAsync(
        int eventId, int groupId, string name, VoteType availability = VoteType.Yes, bool answered = true)
    {
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "evtoviso_seed", "evtoviso_seed@example.com", name: name);

        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = groupId, GroupRole = (int)GroupRole.Player });
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = user.Id,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = answered ? DateTime.UtcNow : null
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    [Fact]
    public async Task Overview_ContainsOnlyActiveBoardEventsAndMembers()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var groupOneEventId = await SeedGroupOneEventAsync(
            "Group One Overview Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(groupOneEventId, groupId: 1, name: "Group One Overview Member");

        var otherEventId = await SeedOtherBoardEventAsync(
            "Group Two Overview Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two Overview Member");

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoviso_viewer1", "evtoviso_viewer1@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Group One Overview Session");
        // Both halves matter -- events and members are two separate leak surfaces on this page.
        body.Should().NotContain("Group Two Overview Session");
        body.Should().NotContain("Group Two Overview Member");
    }

    [Fact]
    public async Task Overview_SameNamedMemberOnAnotherBoard_AppearsOnlyOnce()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var groupOneEventId = await SeedGroupOneEventAsync(
            "Shared Name Board One Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(groupOneEventId, groupId: 1, name: "Shared Overview Name");

        var otherEventId = await SeedOtherBoardEventAsync(
            "Shared Name Board Two Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Shared Overview Name");

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoviso_viewer2", "evtoviso_viewer2@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // With identical display names a leaked column is invisible to a plain containment
        // check, so the occurrence count is what actually detects a leak here.
        var occurrences = body.Split("Shared Overview Name").Length - 1;
        occurrences.Should().Be(1);
    }

    [Fact]
    public async Task Overview_OtherBoardEventOnSameDate_DoesNotContributeToCounts()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var sharedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        var groupOneEventId = await SeedGroupOneEventAsync("Group One Count Session", sharedDate);
        await SeedSignupAsync(groupOneEventId, groupId: 1, name: "Group One Count Member");

        var otherEventId = await SeedOtherBoardEventAsync("Group Two Count Session", sharedDate);
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two Count Member One");
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two Count Member Two");
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two Count Member Three");

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoviso_viewer3", "evtoviso_viewer3@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().NotContain("Group Two Count Session");
        // The headline figure renders as <strong>N</strong> Yes inside avail-count-headline, so
        // assert against that rendered shape rather than a bare digit, which would match
        // unrelated markup.
        body.Should().Contain("<strong>1</strong> Yes");
        body.Should().NotContain("<strong>3</strong> Yes");
    }

    [Fact]
    public async Task Overview_LargeTakeParameter_DoesNotWidenBeyondActiveBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var groupOneEventId = await SeedGroupOneEventAsync(
            "Group One Take Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(groupOneEventId, groupId: 1, name: "Group One Take Member");

        var otherEventId = await SeedOtherBoardEventAsync(
            "Group Two Take Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two Take Member");

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoviso_viewer4", "evtoviso_viewer4@example.com");

        var response = await client.GetAsync("/Events?take=100000", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Widening the page size must not widen the tenant boundary.
        body.Should().NotContain("Group Two Take Session");
        body.Should().NotContain("Group Two Take Member");
    }

    [Fact]
    public async Task Overview_WithNoActiveBoardSelected_ShowsNothingFromEitherBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var groupOneEventId = await SeedGroupOneEventAsync(
            "Group One No Board Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(groupOneEventId, groupId: 1, name: "Group One No Board Member");

        var otherEventId = await SeedOtherBoardEventAsync(
            "Group Two No Board Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two No Board Member");

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoviso_viewer5", "evtoviso_viewer5@example.com");

        try
        {
            // The filters are fail-closed, so a request with no board selected at all must never
            // fall through to every board's rows merged together. In practice the session
            // middleware redirects a GET with no active group before the action runs, but the
            // filters behind it are fail-closed either way.
            factory.TestGroupContext.ActiveGroupId = null;
            var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);

            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.NotFound, HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.Should().NotContain("Group One No Board Session");
            body.Should().NotContain("Group Two No Board Session");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }
}
