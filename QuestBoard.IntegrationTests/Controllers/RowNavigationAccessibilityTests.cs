using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// Proves a focusable link exists on a representative desktop surface and a representative
// mobile surface, using the availability overview because it is the one page with both a
// desktop and a mobile clickable-row implementation.
public class RowNavigationAccessibilityTests(WebApplicationFactoryBase factory)
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

    private async Task<int> SeedEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var eventEntity = new EventEntity
        {
            Title = title,
            GroupId = 1,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(eventEntity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return eventEntity.Id;
    }

    private async Task<int> SeedQuestAsync(int dungeonMasterId, string title)
    {
        await using var ctx = factory.Database.CreateContext();
        var questEntity = new QuestEntity
        {
            Title = title,
            Description = "Test description",
            ChallengeRating = 5,
            DungeonMasterId = dungeonMasterId,
            GroupId = 1,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Quests.Add(questEntity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return questEntity.Id;
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
    public async Task Desktop_ClickableRow_ExposesFocusableLinkToSameDestination()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync(
            "Keyboard Reachable Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "rownav_desktop", "rownav_desktop@example.com", roles: ["Player"]);

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The anchor's class, its attribute order, and the destination id together fail if the
        // anchor is removed, renamed, or pointed somewhere else, and they survive a routing
        // change because the id assertion does not pin down the whole href.
        html.Should().MatchRegex($"""<a class="row-nav-link" href="[^"]*Events/Details/{eventId}[^"]*">""");
        // The row's own click handler survives alongside the new anchor.
        html.Should().Contain("avail-row-clickable");
    }

    [Fact]
    public async Task Mobile_ClickableCard_ExposesFocusableLinkToSameDestination()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync(
            "Keyboard Reachable Mobile Session", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "rownav_mobile", "rownav_mobile@example.com", roles: ["Player"]);

        var (response, html) = await GetMobileAsync(client, "/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().MatchRegex($"""<a class="row-nav-link" href="[^"]*Events/Details/{eventId}[^"]*">""");
        // Confirms the mobile view actually rendered rather than the desktop one.
        html.Should().Contain("avail-card");
    }

    [Fact]
    public async Task Desktop_QuestCard_QuestOwnerSeesManageLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "quest_owner", "quest_owner@example.com", roles: ["Player"]);

        // Ensure the owner is in group 1
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity
        {
            UserId = owner.Id,
            GroupId = 1,
            GroupRole = (int)GroupRole.Player
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var questId = await SeedQuestAsync(owner.Id, "Quest Owned By Player");

        var response = await ownerClient.GetAsync("/quests", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The anchor's href must point to the Manage action for the quest owner.
        html.Should().MatchRegex($"""<a class="row-nav-link" href="[^"]*Quest/Manage/{questId}[^"]*">""");
    }

    [Fact]
    public async Task Desktop_QuestCard_NonOwnerSeesDetailsLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "quest_owner2", "quest_owner2@example.com", roles: ["Player"]);

        // Ensure the owner is in group 1
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity
        {
            UserId = owner.Id,
            GroupId = 1,
            GroupRole = (int)GroupRole.Player
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var questId = await SeedQuestAsync(owner.Id, "Quest Owned By Other Player");

        // Create a different user (non-owner)
        var (viewerClient, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "quest_viewer", "quest_viewer@example.com", roles: ["Player"]);

        // Ensure the viewer is in group 1
        ctx.UserGroups.Add(new UserGroupEntity
        {
            UserId = viewer.Id,
            GroupId = 1,
            GroupRole = (int)GroupRole.Player
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await viewerClient.GetAsync("/quests", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The anchor's href must point to the Details action for non-owners.
        html.Should().MatchRegex($"""<a class="row-nav-link" href="[^"]*Quest/Details/{questId}[^"]*">""");
    }

    [Fact]
    public async Task Mobile_QuestCard_QuestOwnerSeesManageLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "quest_owner_mobile", "quest_owner_mobile@example.com", roles: ["Player"]);

        // Ensure the owner is in group 1
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity
        {
            UserId = owner.Id,
            GroupId = 1,
            GroupRole = (int)GroupRole.Player
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var questId = await SeedQuestAsync(owner.Id, "Quest Owned By Player Mobile");

        var (response, html) = await GetMobileAsync(ownerClient, "/quests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The anchor's href must point to the Manage action for the quest owner on mobile.
        html.Should().MatchRegex($"""<a class="row-nav-link" href="[^"]*Quest/Manage/{questId}[^"]*">""");
        // Confirms the mobile view rendered.
        html.Should().Contain("quest-list-mobile");
    }

    [Fact]
    public async Task Mobile_QuestCard_NonOwnerSeesDetailsLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "quest_owner_mobile2", "quest_owner_mobile2@example.com", roles: ["Player"]);

        // Ensure the owner is in group 1
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity
        {
            UserId = owner.Id,
            GroupId = 1,
            GroupRole = (int)GroupRole.Player
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var questId = await SeedQuestAsync(owner.Id, "Quest Owned By Other Player Mobile");

        // Create a different user (non-owner)
        var (viewerClient, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "quest_viewer_mobile", "quest_viewer_mobile@example.com", roles: ["Player"]);

        // Ensure the viewer is in group 1
        ctx.UserGroups.Add(new UserGroupEntity
        {
            UserId = viewer.Id,
            GroupId = 1,
            GroupRole = (int)GroupRole.Player
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (response, html) = await GetMobileAsync(viewerClient, "/quests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The anchor's href must point to the Details action for non-owners on mobile.
        html.Should().MatchRegex($"""<a class="row-nav-link" href="[^"]*Quest/Details/{questId}[^"]*">""");
        // Confirms the mobile view rendered.
        html.Should().Contain("quest-list-mobile");
    }
}
