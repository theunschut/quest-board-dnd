using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The desktop quest board draws nothing when it has no quests (only a visually hidden heading a screen reader
/// can find) and, with quests, draws one poster per quest whose single link carries the quest's status in its
/// name. The phone board has its own view and is not covered here.
/// </summary>
public class QuestBoardEmptyStateTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<(HttpClient Client, int UserId)> SignInAsync(string name)
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, name + "@example.com", roles: ["Player"]);

        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (client, user.Id);
    }

    private async Task<int> SeedQuestAsync(int dungeonMasterId, string title, bool finalized = false, params int[] signedUpPlayerIds)
    {
        await using var ctx = factory.Database.CreateContext();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "Test description",
            ChallengeRating = 5,
            DungeonMasterId = dungeonMasterId,
            GroupId = 1,
            CreatedAt = DateTime.UtcNow,
            IsFinalized = finalized,
            FinalizedDate = finalized ? new DateTime(2026, 11, 29, 19, 0, 0) : null
        };
        foreach (var playerId in signedUpPlayerIds)
            quest.PlayerSignups.Add(new PlayerSignupEntity { PlayerId = playerId, IsSelected = true });
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quest.Id;
    }

    private static async Task<string> GetBoardAsync(HttpClient client, CancellationToken token)
    {
        var response = await client.GetAsync("/quests", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(token);
    }

    [Fact]
    public async Task DesktopBoard_WithNoQuests_RendersOnlyTheHiddenHeading()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await SignInAsync("poster_empty_viewer");

        var html = await GetBoardAsync(client, TestContext.Current.CancellationToken);

        html.Should().MatchRegex(@"<h2 class=""visually-hidden"">No quests available</h2>");
        html.Should().NotContain("quest-poster");
        html.Should().NotContain("quest-board-container");
        html.Should().NotContain("empty-state");
    }

    [Fact]
    public async Task DesktopBoard_WithOneQuest_RendersOnePosterWithOneLinkAndNoClickHandler()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, ownerId) = await SignInAsync("poster_one_owner");
        await SeedQuestAsync(ownerId, "The Ember Gate Assize");

        var html = await GetBoardAsync(client, TestContext.Current.CancellationToken);

        var posters = Regex.Matches(html, @"<article class=""quest-poster quest-poster--[a-z0-9]+"">[\s\S]*?</article>");
        posters.Should().HaveCount(1);
        Regex.Matches(posters[0].Value, @"<a class=""row-nav-link"" href=""").Should().HaveCount(1);
        posters[0].Value.Should().NotContain("onclick");
        posters[0].Value.Should().NotContain("style=");
        html.Should().NotContain("No quests available");
    }

    [Fact]
    public async Task DesktopBoard_WithFinalizedSignedUpQuest_NamesTheStatusInTheLinkAndBothSeals()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, viewerId) = await SignInAsync("poster_signed_viewer");
        var (_, dmId) = await SignInAsync("poster_signed_dm");
        await SeedQuestAsync(dmId, "The Salt Road Reckoning", finalized: true, viewerId);

        var html = await GetBoardAsync(client, TestContext.Current.CancellationToken);

        html.Should().Contain(@"<span class=""visually-hidden"">, finalized, signed up</span>");
        html.Should().Contain(@"alt=""Finalized quest""");
        html.Should().Contain(@"alt=""You are signed up for this quest""");
        html.Should().Contain("Quest Date:");
        html.Should().Contain("Selected Adventurers:");
    }

    [Fact]
    public async Task DesktopBoard_WithOpenQuest_HasNoStatusSealAndNamesItOpen()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await SignInAsync("poster_open_viewer");
        var (_, dmId) = await SignInAsync("poster_open_dm");
        await SeedQuestAsync(dmId, "A Quiet Open Quest");

        var html = await GetBoardAsync(client, TestContext.Current.CancellationToken);

        html.Should().Contain(@"<span class=""visually-hidden"">, open</span>");
        html.Should().NotContain("quest-poster-seal");
        html.Should().Contain("Adventurers signed up:");
        html.Should().Contain("DM:");
    }
}
