using System.Net;
using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the page resolver is built and answers through the production board-type resolver rather
/// than the harness's stand-in, so a circular registration between the two would fail here instead
/// of at the first request in production.
/// </summary>
public class BoardPageDependencyInjectionTests(ProductionResolverWebApplicationFactory factory)
    : IClassFixture<ProductionResolverWebApplicationFactory>
{
    [Fact]
    public async Task PageResolver_ResolvesThroughTheProductionBoardTypeResolver_AndAnswersForTheActiveBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        await using var scope = factory.Services.CreateAsyncScope();
        var boardTypes = scope.ServiceProvider.GetRequiredService<IBoardTypeResolver>();
        boardTypes.Should().BeOfType<QuestBoard.Service.Services.BoardTypeResolver>(
            because: "the factory exists to put the real board-type resolver behind the page resolver");

        var resolver = scope.ServiceProvider.GetRequiredService<IBoardPageResolver>();
        var availability = await resolver.GetAsync(TestContext.Current.CancellationToken);

        availability.Should().NotBeNull();
        await using var ctx = factory.Database.CreateContext();
        var stored = (BoardType)ctx.Groups.Single(g => g.Id == 1).BoardType;
        availability!.BoardType.Should().Be(stored);
        availability.Disabled.Should().Be(BoardPageSet.Empty);
        availability.EffectiveHome.Should().Be(BoardPage.Quests);
    }

    [Fact]
    public async Task Root_ThroughTheProductionBoardTypeResolver_RedirectsAnUnconfiguredBoardToTheQuestList()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/quests");
    }
}
