using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// This class protects the event details page's conditional rendering only: the three answer
// buttons appear for every signed-in board member, the withdraw control appears only where a
// one-shot board's viewer already holds a signup row, the named roster shows plain Yes/Maybe/No
// badges without ever leaking whether an answer was automatic, and the delete confirmation
// carries the live signup count. Confirming the layout renders correctly on a real mobile
// device stays a manual check -- this page has no mobile variant, and devtools emulation has
// masked a live case of mobile markup never actually being selected before.
public class EventDetailsAvailabilityRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // The harness context is a shared singleton, so leaving either mutated bleeds into
    // whichever test class runs next.
    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<int> SeedEventAsync(string title = "Council Session")
    {
        await using var ctx = factory.Database.CreateContext();
        var eventEntity = new EventEntity
        {
            Title = title,
            GroupId = 1,
            Date = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(eventEntity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return eventEntity.Id;
    }

    private async Task SeedSignupAsync(int eventId, int userId, VoteType availability)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.Set<EventSignupEntity>().Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Details_SignedInMember_SeesAllThreeAnswerButtons()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_buttons_viewer", "evtavail_buttons_viewer@example.com");

        var response = await client.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        (body.Split("onclick=\"setAvailability(", StringSplitOptions.None).Length - 1).Should().Be(3);
    }

    [Fact]
    public async Task Details_OneShotBoard_ViewerHoldsRow_ShowsWithdraw()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        var eventId = await SeedEventAsync();

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_withdraw_shown", "evtavail_withdraw_shown@example.com", name: "Withdraw Shown Person");
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var response = await client.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("withdrawAvailability(");
    }

    [Fact]
    public async Task Details_OneShotBoard_ViewerHoldsNoRow_HidesWithdraw()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        var eventId = await SeedEventAsync();

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_withdraw_norow", "evtavail_withdraw_norow@example.com");

        var response = await client.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().NotContain("withdrawAvailability(");
    }

    [Fact]
    public async Task Details_CampaignBoard_ViewerHoldsRow_HidesWithdraw()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var eventId = await SeedEventAsync();

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_withdraw_campaign", "evtavail_withdraw_campaign@example.com", name: "Withdraw Campaign Person");
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var response = await client.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The control is hidden here even though the row exists -- opting out on a campaign
        // board means answering No, not deleting the row.
        body.Should().NotContain("withdrawAvailability(");
    }

    [Fact]
    public async Task Details_ThreeMembersAnswered_RosterShowsNamesAndBadges()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();

        var (viewerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_roster_viewer", "evtavail_roster_viewer@example.com");
        var (_, yesUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_roster_yes", "evtavail_roster_yes@example.com", name: "Roster Yes Person");
        var (_, maybeUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_roster_maybe", "evtavail_roster_maybe@example.com", name: "Roster Maybe Person");
        var (_, noUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_roster_no", "evtavail_roster_no@example.com", name: "Roster No Person");

        await SeedSignupAsync(eventId, yesUser.Id, VoteType.Yes);
        await SeedSignupAsync(eventId, maybeUser.Id, VoteType.Maybe);
        await SeedSignupAsync(eventId, noUser.Id, VoteType.No);

        var response = await viewerClient.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain(yesUser.Name);
        body.Should().Contain(maybeUser.Name);
        body.Should().Contain(noUser.Name);
        body.Should().Contain("badge bg-success");
        body.Should().Contain("badge bg-warning text-dark");
        body.Should().Contain("badge bg-danger");
    }

    [Fact]
    public async Task Details_Body_NeverLeaksAnsweredMarkerOrRawTimestampField()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_leak_viewer", "evtavail_leak_viewer@example.com");
        await SeedSignupAsync(eventId, user.Id, VoteType.Yes);

        var response = await client.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().NotContain("HasAnswered");
        body.Should().NotContain("UpdatedAt");
    }

    [Fact]
    public async Task Details_DungeonMaster_SeesDeleteConfirmationWithSignupCount()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_dm_delete", "evtavail_dm_delete@example.com", roles: ["DungeonMaster"]);
        var (_, otherUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_dm_delete_other", "evtavail_dm_delete_other@example.com");

        await SeedSignupAsync(eventId, dmUser.Id, VoteType.Yes);
        await SeedSignupAsync(eventId, otherUser.Id, VoteType.No);

        var response = await dmClient.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("2 people have signed up");
    }

    [Fact]
    public async Task Details_DeleteConfirmation_UsesSingularWordingForOneSignup()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_dm_one", "evtavail_dm_one@example.com", roles: ["DungeonMaster"]);

        await SeedSignupAsync(eventId, dmUser.Id, VoteType.Yes);

        var response = await dmClient.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("1 person has signed up");
        body.Should().NotContain("1 people have signed up");
    }

    [Fact]
    public async Task Details_DeleteConfirmation_PromisesNoLostAvailabilityWhenNobodySignedUp()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();

        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtavail_dm_zero", "evtavail_dm_zero@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync($"/Events/Details/{eventId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().NotContain("0 people have signed up");
        body.Should().Contain("Delete this event? This action cannot be undone.");
    }
}
