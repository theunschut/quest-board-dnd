using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Covers the cross-board agenda's happy path, its three distinguishable empty states,
/// reachability with no active board selected, and the two-variant row action (direct link
/// on the active board, switch-confirm control on any other board). The shared integration
/// harness defaults its active board to group 1, so these facts seed a genuine second board
/// through the unfiltered seeding context wherever a case needs one.
/// </summary>
public class AgendaControllerIntegrationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
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

    private async Task RemoveAllMembershipsAsync(int userId)
    {
        await using var ctx = factory.Database.CreateContext();
        var memberships = ctx.UserGroups.Where(ug => ug.UserId == userId);
        ctx.UserGroups.RemoveRange(memberships);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> SeedEventAsync(int groupId, string title, DateOnly date, TimeOnly? startTime = null, bool cancelled = false)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = date,
            StartTime = startTime,
            CancelledAt = cancelled ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    private async Task SeedSignupAsync(int eventId, int userId, VoteType vote, bool confirmed = true)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)vote,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = confirmed ? DateTime.UtcNow : null
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Agenda_MemberOfOneBoardWithOneUpcomingEvent_ShowsTitleBoardNameOwnAnswerAndRoster()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_happy_path", "agenda_happy_path@example.com", name: "Happy Path Viewer");

        var eventId = await SeedEventAsync(1, "Agenda Happy Path Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Agenda Happy Path Session");
        body.Should().Contain("EuphoriaInn");
        body.Should().Contain("Happy Path Viewer");
        body.Should().Contain("(you)");
    }

    [Fact]
    public async Task Agenda_UpcomingEventWithNoViewerSignup_StillAppears()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_no_signup", "agenda_no_signup@example.com");

        await SeedEventAsync(1, "Agenda No Signup Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Agenda No Signup Session");
    }

    [Fact]
    public async Task Agenda_CancelledUpcomingEvent_DoesNotAppear()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_cancelled", "agenda_cancelled@example.com");

        await SeedEventAsync(1, "Agenda Cancelled Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1), cancelled: true);

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("Agenda Cancelled Session");
    }

    [Fact]
    public async Task Agenda_ViewerWithNoBoardMemberships_ShowsNoBoardsEmptyState_AndNoRowMarkup()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_no_boards", "agenda_no_boards@example.com", roles: []);

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("No Boards Yet");
        body.Should().NotContain("agenda-row");
    }

    [Fact]
    public async Task Agenda_ViewerWithBoardButNothingScheduled_ShowsNoUpcomingEventsEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_nothing_scheduled", "agenda_nothing_scheduled@example.com");

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("No Upcoming Events");
    }

    [Fact]
    public async Task Agenda_ViewerDeselectedEveryBoard_ShowsAllBoardsFilteredOutEmptyState_WithResetControl()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_filtered_out", "agenda_filtered_out@example.com");

        await SeedEventAsync(1, "Agenda Filtered Out Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        // The desktop filter form always submits a leading empty "boards" field; unticking
        // every box submits only that empty value, which is what this query string simulates.
        var response = await client.GetAsync("/Agenda?boards=", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("All Boards Filtered Out");
        body.Should().Contain("Show All Boards");
        body.Should().Contain("boards=all");
        body.Should().NotContain("Agenda Filtered Out Session");
    }

    [Fact]
    public async Task Agenda_MultipleBoardsChecked_NarrowsToExactlyThoseBoards()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_multi_select", "agenda_multi_select@example.com");

        await SeedGroupAsync(2, "Multi Select Board Two");
        await SeedGroupAsync(3, "Multi Select Board Three");
        await SeedMembershipAsync(user.Id, 2);
        await SeedMembershipAsync(user.Id, 3);

        await SeedEventAsync(1, "Multi Select Board One Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedEventAsync(2, "Multi Select Board Two Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        await SeedEventAsync(3, "Multi Select Board Three Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        // Mirrors the real filter form's submission shape: the leading empty hidden field
        // plus one repeated "boards" query entry per checked box (board 3 left unchecked).
        var response = await client.GetAsync("/Agenda?boards=&boards=1&boards=2", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("2 of 3");
        body.Should().Contain("Multi Select Board One Session");
        body.Should().Contain("Multi Select Board Two Session");
        body.Should().NotContain("Multi Select Board Three Session");
    }

    [Fact]
    public async Task Agenda_ForeignBoardIdInFilter_NeverWidensTheResult()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_foreign_id", "agenda_foreign_id@example.com");

        await SeedGroupAsync(2, "Foreign Board The Viewer Is Not In");
        await SeedEventAsync(1, "Own Board Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        var foreignEventId = await SeedEventAsync(2, "Foreign Board Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        var (_, foreignMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_foreign_member", "agenda_foreign_member@example.com", name: "Foreign Board Member", roles: []);
        await SeedMembershipAsync(foreignMember.Id, 2);
        await SeedSignupAsync(foreignEventId, foreignMember.Id, VoteType.Yes);

        // Requesting a board the viewer does not belong to must never reach the query, and
        // must never widen the result to include that board's rows or names -- the
        // intersection against a fresh membership read drops it silently instead.
        var response = await client.GetAsync("/Agenda?boards=2", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("Foreign Board Session");
        body.Should().NotContain("Foreign Board The Viewer Is Not In");
        body.Should().NotContain(foreignMember.Name);
        // The foreign id is dropped rather than substituted, so the effective selection
        // becomes empty -- the viewer's own board is filtered out too, not silently kept.
        body.Should().Contain("All Boards Filtered Out");
        body.Should().NotContain("Own Board Session");
    }

    [Fact]
    public async Task Agenda_SuperAdminWithNoMemberships_ShowsNoBoardsEmptyState_NotOtherBoardsEvents()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "agenda_superadmin", "agenda_superadmin@example.com");

        // CreateAuthenticatedSuperAdminClientAsync seeds membership on group 1 by default (the
        // same seeding every non-empty-roles caller gets); remove it so this SuperAdmin
        // genuinely has zero group memberships, which is the case this fact exists to prove.
        await RemoveAllMembershipsAsync(user.Id);

        await SeedGroupAsync(2, "Other SuperAdmin Board");
        var otherEventId = await SeedEventAsync(2, "Other Board SuperAdmin Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        var (_, otherMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_other_board_member", "agenda_other_board_member@example.com", roles: []);
        await SeedMembershipAsync(otherMember.Id, 2);
        await SeedSignupAsync(otherEventId, otherMember.Id, VoteType.Yes);

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("No Boards Yet");
        body.Should().NotContain("Other SuperAdmin Board");
        body.Should().NotContain("Other Board SuperAdmin Session");
    }

    [Fact]
    public async Task Agenda_NoActiveBoardSelected_RendersAgenda_NotRedirectToPicker()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_no_active_board", "agenda_no_active_board@example.com");

        await SeedEventAsync(1, "Agenda No Active Board Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        // The middleware's own null-active-board redirect must never fire on this path, whether
        // or not the client follows redirects -- assert both the status and the final request
        // path so the fact fails on a redirect to the board picker either way.
        factory.TestGroupContext.ActiveGroupId = null;
        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/Agenda");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Agenda No Active Board Session");
    }

    [Fact]
    public async Task Agenda_RowOnActiveBoard_RendersDirectLink_RowOnOtherBoard_RendersSwitchModalControl()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "agenda_two_boards", "agenda_two_boards@example.com");

        await SeedGroupAsync(2, "Second Agenda Board");
        await SeedMembershipAsync(user.Id, 2);

        var activeEventId = await SeedEventAsync(1, "Active Board Session", DateOnly.FromDateTime(DateTime.Today).AddDays(1));
        var otherEventId = await SeedEventAsync(2, "Other Board Session", DateOnly.FromDateTime(DateTime.Today).AddDays(2));

        var response = await client.GetAsync("/Agenda", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Open Event");
        body.Should().Contain($"/Events/Details/{activeEventId}");
        body.Should().Contain("Switch &amp; Open");
        body.Should().Contain("data-group-id=\"2\"");
        body.Should().Contain($"/Events/Details/{otherEventId}?from=agenda");
    }
}
