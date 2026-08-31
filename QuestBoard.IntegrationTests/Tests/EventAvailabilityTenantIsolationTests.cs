using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Two-group tenant isolation tests for event availability. The shared integration harness
/// defaults its active board to group 1 for every test class, so an ordinary fact is
/// structurally blind to a cross-board leak on this feature -- it would need to already be
/// looking at the wrong board to notice one. These facts therefore seed a genuine second board
/// through the unfiltered seeding context and act as a member of the first, and they prove both
/// directions matter: the read-side query filter constrains reads only and offers nothing at all
/// on an insert, so a refused write has to be confirmed by re-reading the table afterward rather
/// than trusted from a status code alone.
/// </summary>
public class EventAvailabilityTenantIsolationTests(WebApplicationFactoryBase factory)
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
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherAvailabilityBoard", CreatedAt = DateTime.UtcNow });
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

    // Seeds a user, their membership on the given board, and a signup row for the given event --
    // this is what makes the withdraw and roster facts able to detect a deletion or a leak
    // rather than merely observing an empty table.
    private async Task<int> SeedSignupAsync(int eventId, int groupId, string name)
    {
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "isoavail_seed", "isoavail_seed@example.com", name: name);

        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = groupId, GroupRole = (int)GroupRole.Player });
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = user.Id,
            Availability = (int)VoteType.Yes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    [Fact]
    public async Task Details_EventFromOtherGroup_ReturnsNotFound_AndBodyNamesNothingFromTheOtherBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var otherEventId = await SeedOtherBoardEventAsync("Group Two Secret Session", DateOnly.FromDateTime(DateTime.Today));
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two Secret Member");

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoavail_details_viewer", "isoavail_details_viewer@example.com");

        var response = await client.GetAsync($"/Events/Details/{otherEventId}", TestContext.Current.CancellationToken);

        // A not-found response is deliberate here: it is indistinguishable from a genuinely
        // non-existent event and therefore reveals nothing about the other board.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("Group Two Secret Session");
        body.Should().NotContain("Group Two Secret Member");
    }

    [Fact]
    public async Task SetAvailability_EventFromOtherGroup_IsRefused_AndWritesNoRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var otherEventId = await SeedOtherBoardEventAsync("Group Two Write Target", DateOnly.FromDateTime(DateTime.Today));

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoavail_write_actor", "isoavail_write_actor@example.com");

        var response = await client.PostAsync($"/Events/SetAvailability/{otherEventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);

        // The point here is refusal rather than a specific status code, so any of these is
        // acceptable -- the database assertion below is what actually proves nothing leaked.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

        await using var ctx = factory.Database.CreateContext();
        var row = ctx.EventSignups.IgnoreQueryFilters()
            .FirstOrDefault(es => es.EventId == otherEventId && es.UserId == user.Id);
        row.Should().BeNull();
    }

    [Fact]
    public async Task Withdraw_EventFromOtherGroup_IsRefused_AndDeletesNothing()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var otherEventId = await SeedOtherBoardEventAsync("Group Two Withdraw Target", DateOnly.FromDateTime(DateTime.Today));
        var otherMemberId = await SeedSignupAsync(otherEventId, groupId: 2, name: "Group Two Withdraw Member");

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoavail_withdraw_actor", "isoavail_withdraw_actor@example.com");

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/Events/Withdraw/{otherEventId}");
        var response = await client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

        // A status code alone cannot tell "refused" apart from "accepted and silently deleted
        // the wrong board's row" -- the seeded row has to still be there.
        await using var ctx = factory.Database.CreateContext();
        var stillExists = ctx.EventSignups.IgnoreQueryFilters()
            .Any(es => es.EventId == otherEventId && es.UserId == otherMemberId);
        stillExists.Should().BeTrue();
    }

    [Fact]
    public async Task Details_EventFromOtherGroup_WithNoActiveBoardSelected_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var otherEventId = await SeedOtherBoardEventAsync("Group Two No Board Session", DateOnly.FromDateTime(DateTime.Today));

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoavail_noboard_viewer", "isoavail_noboard_viewer@example.com");

        // The filters are fail-closed, so a request with no board selected at all must never
        // fall through to every board's rows merged together. In practice a GET with no active
        // group is caught even earlier, by the same redirect-to-group-picker path the SuperAdmin
        // no-active-group facts already pin -- either way, nothing about the other board reaches
        // the response.
        factory.TestGroupContext.ActiveGroupId = null;
        var response = await client.GetAsync($"/Events/Details/{otherEventId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Redirect, HttpStatusCode.Found);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("Group Two No Board Session");
    }

    [Fact]
    public async Task Roster_ForGroupOneEvent_ContainsOnlyGroupOneMembers()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var groupOneEventId = await SeedGroupOneEventAsync("Group One Roster Session", DateOnly.FromDateTime(DateTime.Today));
        var (groupOneClient, groupOneUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoavail_roster_member", "isoavail_roster_member@example.com", name: "Shared Display Name");

        var otherEventId = await SeedOtherBoardEventAsync("Group Two Roster Session", DateOnly.FromDateTime(DateTime.Today));
        // Same display name as the group-1 member, so a leak would be visible in the roster
        // rather than coincidentally distinguishable by name alone.
        await SeedSignupAsync(otherEventId, groupId: 2, name: "Shared Display Name");

        factory.TestGroupContext.ActiveGroupId = 1;
        await groupOneClient.PostAsync($"/Events/SetAvailability/{groupOneEventId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var signupService = scope.ServiceProvider.GetRequiredService<IEventSignupService>();
        var roster = await signupService.GetRosterForEventAsync(groupOneEventId, TestContext.Current.CancellationToken);

        roster.Should().ContainSingle();
        roster[0].UserId.Should().Be(groupOneUser.Id);
    }
}
