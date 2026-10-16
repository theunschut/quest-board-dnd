using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the /platform/LinkedBoards page: the super admin creates sets and
/// manages their boards, and the server holds the same-type and one-set-per-board rules.
/// </summary>
public class LinkedBoardsPlatformIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public LinkedBoardsPlatformIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.ToDictionary(f => f.Key, f => f.Value));

    private async Task SeedBoardAsync(int id, string name, BoardType boardType = BoardType.OneShot, int? linkedBoardSetId = null)
    {
        await using var context = _factory.Database.CreateContext();
        context.Groups.Add(new GroupEntity
        {
            Id = id,
            Name = name,
            BoardType = (int)boardType,
            CreatedAt = DateTime.UtcNow,
            LinkedBoardSetId = linkedBoardSetId
        });
        await context.SaveChangesAsync(Token);
    }

    private async Task<int> SeedSetAsync(string name, BoardType boardType = BoardType.OneShot)
    {
        await using var context = _factory.Database.CreateContext();
        var set = new LinkedBoardSetEntity { Name = name, BoardType = (int)boardType, CreatedAt = DateTime.UtcNow };
        context.LinkedBoardSets.Add(set);
        await context.SaveChangesAsync(Token);
        return set.Id;
    }

    private async Task<int?> GetBoardSetIdAsync(int groupId)
    {
        await using var context = _factory.Database.CreateContext();
        return (await context.Groups.SingleAsync(g => g.Id == groupId, Token)).LinkedBoardSetId;
    }

    [Fact]
    public async Task Index_WhenSuperAdmin_ShouldReturn200WithLinkedBoardsHeading()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await client.GetAsync("/platform/LinkedBoards", Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Token)).Should().Contain("Linked Boards");
    }

    [Fact]
    public async Task CreateSetAndAddTwoBoards_StoresTheLinkAndListsBothBoardsAsActive()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedBoardAsync(11, "Friday Table");
        await SeedBoardAsync(12, "Saturday Table");

        var create = await client.PostAsync("/platform/LinkedBoards/Create",
            Form(("Name", "Weekend Set"), ("BoardType", ((int)BoardType.OneShot).ToString())), Token);
        create.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        int setId;
        await using (var context = _factory.Database.CreateContext())
        {
            var set = await context.LinkedBoardSets.SingleAsync(s => s.Name == "Weekend Set", Token);
            setId = set.Id;
        }

        foreach (var groupId in new[] { 11, 12 })
        {
            var add = await client.PostAsync($"/platform/LinkedBoards/AddBoard/{setId}",
                Form(("groupId", groupId.ToString())), Token);
            add.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        }

        (await GetBoardSetIdAsync(11)).Should().Be(setId);
        (await GetBoardSetIdAsync(12)).Should().Be(setId);

        var index = await client.GetAsync("/platform/LinkedBoards", Token);
        var html = await index.Content.ReadAsStringAsync(Token);
        html.Should().Contain("Weekend Set");
        html.Should().Contain("Friday Table");
        html.Should().Contain("Saturday Table");
        html.Should().Contain("badge bg-success\">Active");
    }
}
