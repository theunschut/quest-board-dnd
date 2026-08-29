using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the mobile agenda view actually renders under a real mobile user agent -- distinct
/// from the desktop view -- with a collapsed roster, board identity and the row's own action
/// control, and that all three empty states carry their own copy on mobile too. Devtools
/// viewport emulation cannot exercise any of this: the mobile view is selected purely from the
/// request's own User-Agent header.
/// </summary>
public class AgendaMobileRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
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

    private async Task SeedGroupAsync(int groupId, string name, BoardType boardType = BoardType.OneShot)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == groupId))
        {
            ctx.Groups.Add(new GroupEntity { Id = groupId, Name = name, CreatedAt = DateTime.UtcNow, BoardType = (int)boardType });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task SeedMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.UserGroups.Any(ug => ug.UserId == userId && ug.GroupId == groupId))
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<int> SeedEventAsync(int groupId, string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    private async Task SeedSignupAsync(int eventId, int userId, VoteType vote)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)vote,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Agenda_MobileUserAgent_RendersCardLayout_NotDesktopList()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_card", "agenda_mobile_card@example.com", name: "Mobile Card Viewer");
        var eventId = await SeedEventAsync(1, "Mobile Card Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var (response, html) = await GetMobileAsync(client, "/Agenda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-card");
        html.Should().NotContain("agenda-row");
    }

    [Fact]
    public async Task Agenda_DesktopUserAgent_RendersDesktopLayout_NotMobileCards()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_desktop_row", "agenda_desktop_row@example.com", name: "Desktop Row Viewer");
        var eventId = await SeedEventAsync(1, "Desktop Row Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-row");
        html.Should().NotContain("agenda-card mb-3");
    }

    [Fact]
    public async Task Agenda_MobileRoster_CollapsedByDefaultAndContainsMemberNames()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_roster", "agenda_mobile_roster@example.com", name: "Roster Viewer");
        var eventId = await SeedEventAsync(1, "Mobile Roster Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, viewer.Id, VoteType.Yes);

        var (_, otherMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_roster_other", "agenda_mobile_roster_other@example.com", name: "Roster Other Member");
        await SeedSignupAsync(eventId, otherMember.Id, VoteType.Maybe);

        var (response, html) = await GetMobileAsync(client, "/Agenda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"id=\"roster-{eventId}\"");
        html.Should().Contain("Roster Viewer");
        html.Should().Contain("Roster Other Member");
        html.Should().Contain("agenda-roster-toggle");
    }

    [Fact]
    public async Task Agenda_MobileRowOnActiveBoard_RendersDetailsLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_active_row", "agenda_mobile_active_row@example.com");
        var eventId = await SeedEventAsync(1, "Mobile Active Board Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        var (response, html) = await GetMobileAsync(client, "/Agenda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Open Event");
        html.Should().Contain($"/Events/Details/{eventId}");
    }

    [Fact]
    public async Task Agenda_MobileRowOnOtherBoard_RendersSwitchModalTriggerWithGroupIdAndReturnUrl()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_other_row", "agenda_mobile_other_row@example.com");
        await SeedGroupAsync(2, "Mobile Other Board");
        await SeedMembershipAsync(user.Id, 2);
        var otherEventId = await SeedEventAsync(2, "Mobile Other Board Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        var (response, html) = await GetMobileAsync(client, "/Agenda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Switch &amp; Open");
        html.Should().Contain("data-group-id=\"2\"");
        html.Should().Contain($"/Events/Details/{otherEventId}?from=agenda");
    }

    [Fact]
    public async Task Agenda_MobileRow_ShowsBoardNameAndTypeBadge()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_identity", "agenda_mobile_identity@example.com");
        await SeedEventAsync(1, "Mobile Board Identity Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        var (response, html) = await GetMobileAsync(client, "/Agenda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("EuphoriaInn");
        html.Should().Contain("One-Shot");
    }

    [Fact]
    public async Task Agenda_MobileNoBoardsEmptyState_RendersOwnCopy()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_no_boards", "agenda_mobile_no_boards@example.com", roles: []);

        var (response, html) = await GetMobileAsync(client, "/Agenda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("No Boards Yet");
    }

    [Fact]
    public async Task Agenda_MobileNoUpcomingEventsEmptyState_RendersOwnCopy()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_no_events", "agenda_mobile_no_events@example.com");

        var (response, html) = await GetMobileAsync(client, "/Agenda");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("No Upcoming Events");
    }

    [Fact]
    public async Task Agenda_MobileAllBoardsFilteredEmptyState_RendersOwnCopyWithResetControl()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_mobile_filtered", "agenda_mobile_filtered@example.com");
        await SeedEventAsync(1, "Mobile Filtered Out Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        var (response, html) = await GetMobileAsync(client, "/Agenda?boards=");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("All Boards Filtered Out");
        html.Should().Contain("Show All Boards");
        html.Should().NotContain("Mobile Filtered Out Session");
    }
}
