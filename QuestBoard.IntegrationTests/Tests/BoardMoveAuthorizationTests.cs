using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Only a super admin reaches the Move page or its confirm POST. Seeded: board 1 "Board Alpha"
/// (active) and board 2 "Board Bravo" linked, and an open quest on board 1 whose DM is a Dungeon
/// Master on both boards. A board Admin or Dungeon Master of both boards, a Player and an
/// anonymous visitor are each refused, and the quest never leaves board 1.
///
/// What these facts do NOT establish: how the policy is evaluated in a real browser session.
/// The harness authenticates with a test scheme.
/// </summary>
public class BoardMoveAuthorizationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<int> SeedQuestAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardAlpha, BoardBravo);

        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_auth_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);

        return await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Goblin Market", dm.Id,
            [new DateTime(2031, 5, 10, 19, 0, 0), new DateTime(2031, 5, 11, 19, 0, 0)]);
    }

    private async Task<string> PreviewStampAsync(int questId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuestMoveService>();
        var actor = await BoardMoveSeed.CreateUserAsync(factory, $"move_auth_stamp_{Guid.NewGuid():N}", "Stamp Actor");
        var preview = await service.PreviewAsync(questId, BoardBravo, actor.Id, TestContext.Current.CancellationToken);
        preview.Stamp.Should().NotBeNullOrEmpty();
        return preview.Stamp;
    }

    private static void ShouldBeRefused(HttpResponseMessage response)
    {
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Forbidden);
        if (response.Headers.Location != null)
        {
            response.Headers.Location.OriginalString.Should().Contain("AccessDenied");
        }
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("DungeonMaster", true)]
    [InlineData("Player", false)]
    public async Task NonSuperAdmin_IsRefusedOnGetAndPostAndTheQuestStaysOnItsBoard(string role, bool memberOfBothBoards)
    {
        var questId = await SeedQuestAsync();
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"move_auth_{role.ToLowerInvariant()}", $"move_auth_{role.ToLowerInvariant()}@example.com",
            roles: [role]);
        if (memberOfBothBoards)
        {
            await BoardMoveSeed.SeedMembershipAsync(factory, user.Id, BoardBravo, Enum.Parse<GroupRole>(role));
        }

        var stamp = await PreviewStampAsync(questId);

        var get = await client.GetAsync($"/Move/Quest/{questId}?targetBoardId={BoardBravo}", TestContext.Current.CancellationToken);
        ShouldBeRefused(get);

        var post = await client.PostAsync("/Move/Quest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = questId.ToString(),
            ["targetBoardId"] = BoardBravo.ToString(),
            ["stamp"] = stamp
        }), TestContext.Current.CancellationToken);
        ShouldBeRefused(post);

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, questId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Fact]
    public async Task Anonymous_IsRedirectedToLoginOnGetAndPost()
    {
        var questId = await SeedQuestAsync();
        var client = factory.CreateNonRedirectingClient();

        var get = await client.GetAsync($"/Move/Quest/{questId}", TestContext.Current.CancellationToken);
        get.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        get.Headers.Location!.OriginalString.Should().Contain("Login");

        var post = await client.PostAsync("/Move/Quest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = questId.ToString(),
            ["targetBoardId"] = BoardBravo.ToString(),
            ["stamp"] = "anything"
        }), TestContext.Current.CancellationToken);
        post.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.BadRequest);

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, questId);
        stored.GroupId.Should().Be(BoardAlpha);
    }
}
