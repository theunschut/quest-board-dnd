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
        var eventTwoId = await SeedEventAsync("Empty Cell Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
        var memberId = await SeedMemberAsync("evtoverview_partial", "Partial Roster Member");
        // Member holds a row on event one only.
        await SeedSignupAsync(eventOneId, memberId, VoteType.Yes, confirmed: true);
        _ = eventTwoId;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer4", "evtoverview_viewer4@example.com");

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("avail-cell-empty");
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

        html.Should().Contain("avail-count-headline");
        html.Should().Contain("confirmed");
        html.Should().Contain("Maybe");
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
    public async Task Index_TakeAboveMax_IsClampedAndStillReturnsOk()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        for (var i = 0; i < 5; i++)
        {
            await SeedEventAsync($"Clamp Session {i}", DateOnly.FromDateTime(DateTime.Today.AddDays(i + 1)));
        }

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer9", "evtoverview_viewer9@example.com");

        var response = await client.GetAsync("/Events?take=100000", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rowCount = html.Split("avail-row-clickable").Length - 1;
        rowCount.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task Index_TakeZeroOrNegative_StillReturnsOk()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync("Zero Take Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_viewer10", "evtoverview_viewer10@example.com");

        var zeroResponse = await client.GetAsync("/Events?take=0", TestContext.Current.CancellationToken);
        var negativeResponse = await client.GetAsync("/Events?take=-5", TestContext.Current.CancellationToken);

        zeroResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        negativeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
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
