using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Security;

/// <summary>
/// The drift guard between the board switch and the pages. For every route the switch can resolve, every
/// role the viewer can hold on the target board and every record state worth trying, the real action
/// with the target board active is the oracle: following the same link from another of the viewer's
/// boards must switch and serve exactly when that action served, and otherwise leave the board
/// unchanged with a response indistinguishable from a nonexistent id's. Nothing about who may open
/// what is restated here, so a later change to a page's own rule shows up as a failing row instead of a
/// reopened existence leak.
/// </summary>
public class CrossBoardVisibilityMatrixTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    public static IEnumerable<object[]> ShopRows() =>
        CrossBoardMatrixSeeder.Rows()
            .Where(row => CrossBoardMatrixSeeder.RouteOf(row.Route).Family == "ShopItem")
            .Select(row => new object[] { row.Route, row.Role, row.State });

    [Theory]
    [MemberData(nameof(ShopRows))]
    public async Task ShopRoute_DeepLinkAgreesWithThePage(string route, string role, string state) =>
        await AssertAgreesWithThePageAsync(new MatrixRow(route, role, state));

    private async Task AssertAgreesWithThePageAsync(MatrixRow row)
    {
        var scenario = await CrossBoardMatrixSeeder.SeedAsync(factory, row);

        // The oracle: the page itself, asked with the target board already active.
        using var oracleResponse = await CrossBoardMatrixSeeder.GetAsync(scenario.OracleClient, scenario.Url);
        var served = await CrossBoardMatrixSeeder.IsServedAsync(scenario.OracleClient, oracleResponse, scenario.Page);

        if (row.State == CrossBoardMatrixSeeder.PageOffState)
        {
            served.Should().BeFalse("a board that does not offer the page never serves it");
        }

        if (served)
        {
            await AssertSwitchedAndServedAsync(scenario, oracleResponse, row);
        }
        else
        {
            await AssertRefusedLikeANonexistentIdAsync(scenario, row);
        }
    }

    private static async Task AssertSwitchedAndServedAsync(MatrixScenario scenario, HttpResponseMessage oracleResponse, MatrixRow row)
    {
        using var response = await CrossBoardMatrixSeeder.GetAsync(scenario.DeepLinkClient, scenario.Url);

        (await CrossBoardMatrixSeeder.IsServedAsync(scenario.DeepLinkClient, response, scenario.Page))
            .Should().BeTrue($"the page serves {Describe(row)} on its own board, so the link must reach it");
        response.StatusCode.Should().Be(oracleResponse.StatusCode, $"the page answers {Describe(row)} the same way once the board is active");

        // The notice that follows a switch may still be waiting; the second page is the settled state.
        await CrossBoardMatrixSeeder.GetAsync(scenario.DeepLinkClient, "/quests");
        using var settled = await CrossBoardMatrixSeeder.GetAsync(scenario.DeepLinkClient, "/quests");
        var body = await settled.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(scenario.TargetBoardName, $"{Describe(row)} is on the target board, which stays switched to");
        body.Should().NotContain(CrossBoardMatrixSeeder.ActiveBoardName);
    }

    private static async Task AssertRefusedLikeANonexistentIdAsync(MatrixScenario scenario, MatrixRow row)
    {
        await ErrorPageComparison.PrimeAsync(scenario.DeepLinkClient, scenario.ControlClient);

        using var missing = await CrossBoardMatrixSeeder.GetAsync(scenario.ControlClient, scenario.MissingUrl);
        using var response = await CrossBoardMatrixSeeder.GetAsync(scenario.DeepLinkClient, scenario.Url);

        await ErrorPageComparison.AssertIndistinguishableAsync(
            response, missing, scenario.RecordId.ToString(), int.MaxValue.ToString());

        using var followUp = await CrossBoardMatrixSeeder.GetAsync(scenario.DeepLinkClient, "/quests");
        var body = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(CrossBoardMatrixSeeder.ActiveBoardName, $"a refused {Describe(row)} must leave the board as it was");
        body.Should().NotContain(scenario.TargetBoardName);
        body.Should().NotContain("board-switch-toast", "no switch notice may follow a refused link");
    }

    private static string Describe(MatrixRow row) => $"{row.Route} for a {row.Role} with the record {row.State}";
}
