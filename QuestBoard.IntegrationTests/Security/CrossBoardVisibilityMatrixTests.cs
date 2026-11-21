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
///
/// A target board that does not offer the route's page is part of the matrix: the page gate sends every
/// role elsewhere, so the link must never switch to such a board.
/// </summary>
public class CrossBoardVisibilityMatrixTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    public static IEnumerable<object[]> AllRows() =>
        CrossBoardMatrixSeeder.Rows().Select(row => new object[] { row.Route, row.Role, row.State });

    public static IEnumerable<object[]> AllRoutes() =>
        CrossBoardMatrixSeeder.Routes.Select(route => new object[] { route.Key });

    [Theory]
    [MemberData(nameof(AllRows))]
    public async Task DeepLink_AgreesWithThePage(string route, string role, string state) =>
        await AssertAgreesWithThePageAsync(new MatrixRow(route, role, state));

    // Without this a seeding slip that made the page refuse every row would leave the whole matrix agreeing on
    // refusals alone. The page must serve an Admin of the board on every route for the record's plainly
    // openable state, so the served half of every route is exercised.
    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Page_ServesAnAdminOfTheBoard_ForTheOpenableStateOfEveryRoute(string routeKey)
    {
        var route = CrossBoardMatrixSeeder.RouteOf(routeKey);
        var scenario = await CrossBoardMatrixSeeder.SeedAsync(factory, new MatrixRow(route.Key, "Admin", route.PresentState));

        using var response = await CrossBoardMatrixSeeder.GetAsync(scenario.OracleClient, scenario.Url);

        (await CrossBoardMatrixSeeder.IsServedAsync(scenario.OracleClient, response, scenario.Page))
            .Should().BeTrue($"{route.Key} must serve an Admin of the board once a record is in its openable state");
    }

    [Fact]
    public void EveryRegisteredRoute_HasMatrixRows()
    {
        var registered = CrossBoardMatrixSeeder.RegisteredRoutes();
        registered.Should().HaveCount(18, "the switch reaches exactly the routes the registry holds");

        var covered = CrossBoardMatrixSeeder.Rows().Select(row => row.Route).Distinct().ToList();
        covered.Should().BeEquivalentTo(registered, "a route added to the registry needs matrix rows before it can ship");

        foreach (var route in registered)
        {
            var rows = CrossBoardMatrixSeeder.Rows().Where(row => row.Route == route).ToList();
            rows.Select(row => row.Role).Distinct().Should().BeEquivalentTo(CrossBoardMatrixSeeder.Roles, $"{route} is tried with every role");
            rows.Should().Contain(row => row.State == CrossBoardMatrixSeeder.PageOffState, $"{route} is tried on a board that does not offer its page");
            rows.Should().Contain(row => row.State != CrossBoardMatrixSeeder.PageOffState, $"{route} is tried with at least one record state");
        }
    }

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
            await CrossBoardMatrixSeeder.AssertRefusedLikeANonexistentIdAsync(scenario, Describe(row));
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

    private static string Describe(MatrixRow row) => $"{row.Route} for a {row.Role} with the record {row.State}";
}
