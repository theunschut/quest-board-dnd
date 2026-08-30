using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// Exercises the /Events availability overview action: all-members access with no role gate,
// the five cell states, the three headline counts, the cancelled/today boundary facts, the
// empty state, the clamped take parameter and the SuperAdmin-with-no-active-group edge case.
public class EventsOverviewControllerIntegrationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    // Mobile views in this app are selected from the request's user agent, so a request
    // without this header renders the desktop view instead.
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<int> SeedEventAsync(
        string title, DateOnly date, TimeOnly? startTime = null, DateTime? cancelledAt = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var eventEntity = new EventEntity
        {
            Title = title,
            GroupId = 1,
            Date = date,
            StartTime = startTime,
            CancelledAt = cancelledAt,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(eventEntity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return eventEntity.Id;
    }

    // Seeds many events in a single context and one SaveChangesAsync, since seeding a
    // hundred-plus events one context at a time is slow enough to matter for a clamp test.
    private async Task SeedEventsAsync(string titlePrefix, int count)
    {
        await using var ctx = factory.Database.CreateContext();
        for (var i = 0; i < count; i++)
        {
            ctx.Events.Add(new EventEntity
            {
                Title = $"{titlePrefix} {i}",
                GroupId = 1,
                Date = DateOnly.FromDateTime(DateTime.Today.AddDays(i + 1)),
                CreatedAt = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Seeds a member on group 1 (not a client of their own) so the axis has someone besides
    // the acting caller.
    private async Task<int> SeedMemberAsync(string userNamePrefix, string name)
    {
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, userNamePrefix, $"{userNamePrefix}@example.com", name: name);

        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    private async Task SeedSignupAsync(int eventId, int userId, VoteType availability, bool confirmed)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = confirmed ? DateTime.UtcNow : null
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Attaches the mobile user agent header to a request and sends it through the supplied
    // authenticated client, so the client's default authorization header still applies.
    private async Task<(HttpResponseMessage Response, string Html)> GetMobileAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    [Fact]
    public async Task Index_PlayerOnCampaignBoard_ReturnsOk()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_player", "evtoverview_player@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Events", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // The page is deliberately open to every board member while every link that leads to it is
    // shown only to Dungeon Masters -- that split is intentional, and this is the only case in
    // the suite that fails if someone later attaches an authorization policy to the action. The
    // explicit Player role and the rendered-grid assertions are what make it a control rather
    // than a smoke test.
    [Fact]
    public async Task Index_PlayerWithoutDmRole_ReturnsOkAndRendersGrid()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync("Player Reachable Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var memberId = await SeedMemberAsync("evtoverview_player_grid_member", "Player Grid Member");
        await SeedSignupAsync(eventId, memberId, VoteType.Yes, confirmed: true);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_player_grid", "evtoverview_player_grid@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("avail-grid");
        html.Should().Contain("Player Reachable Session");
        html.Should().Contain("Player Grid Member");
    }

    [Fact]
    public async Task Index_Desktop_RendersOneRowPerEventAndOneColumnPerMember()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventOneId = await SeedEventAsync("Overview Session One", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var eventTwoId = await SeedEventAsync("Overview Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
        var memberOneId = await SeedMemberAsync("evtoverview_m1", "Overview Member Alpha");
        var memberTwoId = await SeedMemberAsync("evtoverview_m2", "Overview Member Beta");
        await SeedSignupAsync(eventOneId, memberOneId, VoteType.Yes, confirmed: true);
        await SeedSignupAsync(eventOneId, memberTwoId, VoteType.Maybe, confirmed: true);
        await SeedSignupAsync(eventTwoId, memberOneId, VoteType.Yes, confirmed: true);
        await SeedSignupAsync(eventTwoId, memberTwoId, VoteType.No, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer1", "evtoverview_viewer1@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Overview Session One");
        html.Should().Contain("Overview Session Two");
        html.Should().Contain("Overview Member Alpha");
        html.Should().Contain("Overview Member Beta");
    }

    [Fact]
    public async Task Index_UnconfirmedDefault_RendersMutedChip()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync("Muted Default Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var memberId = await SeedMemberAsync("evtoverview_muted", "Muted Default Member");
        await SeedSignupAsync(eventId, memberId, VoteType.Yes, confirmed: false);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer2", "evtoverview_viewer2@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("avail-cell-yes-muted");
        html.Should().Contain("fa-clock");
    }

    [Fact]
    public async Task Index_ConfirmedAnswer_RendersSolidChip()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync("Confirmed Answer Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var memberId = await SeedMemberAsync("evtoverview_confirmed", "Confirmed Answer Member");
        await SeedSignupAsync(eventId, memberId, VoteType.Yes, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer3", "evtoverview_viewer3@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("badge bg-success");
    }

    [Fact]
    public async Task Index_MemberWithNoRowForOneEvent_RendersEmptyCell()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventOneId = await SeedEventAsync("Empty Cell Session One", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        // Seeded purely for its side effect -- the second event is what makes the member's
        // row on the first event a partial roster rather than a complete one.
        await SeedEventAsync("Empty Cell Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
        var memberId = await SeedMemberAsync("evtoverview_partial", "Partial Roster Member");
        // Member holds a row on event one only.
        await SeedSignupAsync(eventOneId, memberId, VoteType.Yes, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer4", "evtoverview_viewer4@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("avail-cell-empty");
    }

    [Fact]
    public async Task Index_MobileUserAgent_RendersCardList()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventOneId = await SeedEventAsync("Mobile Card Session One", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedEventAsync("Mobile Card Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
        var memberId = await SeedMemberAsync("evtoverview_mobilecard", "Mobile Card Member");
        await SeedSignupAsync(eventOneId, memberId, VoteType.Yes, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_mobile1", "evtoverview_mobile1@example.com");

        var (response, html) = await GetMobileAsync(client, "/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("avail-card");
        html.Should().Contain("avail-card-title");
        html.Should().Contain("Mobile Card Session One");
        html.Should().Contain("Mobile Card Session Two");
        // The negative half is what proves the mobile view rendered rather than the desktop one.
        html.Should().NotContain("avail-grid");
    }

    [Fact]
    public async Task Index_MobileUserAgent_RendersRosterToggle()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync("Mobile Roster Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var memberId = await SeedMemberAsync("evtoverview_mobileroster", "Mobile Roster Member");
        await SeedSignupAsync(eventId, memberId, VoteType.Yes, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_mobile2", "evtoverview_mobile2@example.com");

        var (response, html) = await GetMobileAsync(client, "/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("avail-expand-toggle");
        html.Should().Contain($"roster-{eventId}");
        // One guard on the toggle button and one on the collapse container it opens, so a tap
        // inside an expanded roster cannot bubble up to the card's navigation handler.
        var guardCount = html.Split("event.stopPropagation()").Length - 1;
        guardCount.Should().Be(2);
    }

    [Fact]
    public async Task Index_MobileUserAgent_NoEvents_RendersEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_mobile3", "evtoverview_mobile3@example.com");

        var (response, html) = await GetMobileAsync(client, "/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("No Upcoming Events");
        html.Should().NotContain("avail-card");
    }

    [Fact]
    public async Task Index_MobileUserAgent_MoreEventsThanTake_ShowsShowMoreControl()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync("Mobile Paging Session One", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedEventAsync("Mobile Paging Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_mobile4", "evtoverview_mobile4@example.com");

        var (response, html) = await GetMobileAsync(client, "/Events?take=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Show More Events");
        // The window size (1) plus the configured page increment (10).
        html.Should().Contain("take=11");
    }

    [Fact]
    public async Task Index_RendersAllThreeCounts()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync("Three Counts Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var memberOneId = await SeedMemberAsync("evtoverview_c1", "Counts Member One");
        var memberTwoId = await SeedMemberAsync("evtoverview_c2", "Counts Member Two");
        var memberThreeId = await SeedMemberAsync("evtoverview_c3", "Counts Member Three");
        var memberFourId = await SeedMemberAsync("evtoverview_c4", "Counts Member Four");
        var memberFiveId = await SeedMemberAsync("evtoverview_c5", "Counts Member Five");
        await SeedSignupAsync(eventId, memberOneId, VoteType.Yes, confirmed: true);
        await SeedSignupAsync(eventId, memberTwoId, VoteType.Yes, confirmed: true);
        await SeedSignupAsync(eventId, memberThreeId, VoteType.Yes, confirmed: false);
        await SeedSignupAsync(eventId, memberFourId, VoteType.Maybe, confirmed: true);
        await SeedSignupAsync(eventId, memberFiveId, VoteType.Maybe, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer5", "evtoverview_viewer5@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Asserts the three rendered figures themselves -- the headline total including the
        // unconfirmed default, the parenthesised confirmed subset, and the separately tracked
        // maybe figure -- rather than substrings the legend also renders unconditionally.
        html.Should().Contain("<strong>3</strong> Yes");
        html.Should().Contain("(2 confirmed)");
        html.Should().Contain("2 Maybe");
    }

    [Fact]
    public async Task Index_CancelledOccurrence_IsNotListed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync(
            "Cancelled Overview Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)), cancelledAt: DateTime.UtcNow);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer6", "evtoverview_viewer6@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().NotContain("Cancelled Overview Session");
    }

    [Fact]
    public async Task Index_EventDatedToday_IsListed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        // Dated today with a start time already in the past -- still upcoming for this page's
        // date-only lower bound.
        await SeedEventAsync(
            "Today Overview Session", DateOnly.FromDateTime(DateTime.Today), startTime: new TimeOnly(0, 1));

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer7", "evtoverview_viewer7@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("Today Overview Session");
    }

    [Fact]
    public async Task Index_NoUpcomingEvents_RendersEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer8", "evtoverview_viewer8@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("No Upcoming Events");
    }

    [Fact]
    public async Task Index_TakeAboveMax_IsClampedToMaxTake()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventsAsync("Clamp Session", 105);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer9", "evtoverview_viewer9@example.com");

        var response = await client.GetAsync("/Events?take=100000", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rowCount = html.Split("avail-row-clickable").Length - 1;
        // Exactly the configured ceiling, not merely no greater than it -- with 105 events
        // seeded, deleting the clamp would render 105 rows and fail this assertion.
        rowCount.Should().Be(100);
        // The window is already clamped to the ceiling while further events still exist, so
        // the paging control must be absent -- it would otherwise link back to this same page.
        html.Should().NotContain("Show More Events");
    }

    [Fact]
    public async Task Index_TakeZeroOrNegative_StillReturnsOk()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventsAsync("Zero Take Session", 3);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer10", "evtoverview_viewer10@example.com");

        var zeroResponse = await client.GetAsync("/Events?take=0", TestContext.Current.CancellationToken);
        var zeroHtml = await zeroResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var negativeResponse = await client.GetAsync("/Events?take=-5", TestContext.Current.CancellationToken);
        var negativeHtml = await negativeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        zeroResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        negativeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        // With three events seeded, exactly one rendered row proves the clamp-to-one lower
        // bound actually ran -- with only one event seeded this would be indistinguishable
        // from no clamp at all.
        (zeroHtml.Split("avail-row-clickable").Length - 1).Should().Be(1);
        (negativeHtml.Split("avail-row-clickable").Length - 1).Should().Be(1);
    }

    [Fact]
    public async Task Index_MoreEventsThanTake_ShowsShowMoreControl()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync("Show More Session One", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedEventAsync("Show More Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer11", "evtoverview_viewer11@example.com");

        var response = await client.GetAsync("/Events?take=1", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("Show More Events");
    }

    [Fact]
    public async Task Index_Get_SuperAdminWithNoActiveGroup_DoesNotThrow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await superAdminClient.GetAsync("/Events", TestContext.Current.CancellationToken);

            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }
}
