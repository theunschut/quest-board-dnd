using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class PlayersControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // DM directory page links to each DM's profile at /DungeonMaster/Profile/{id}
    [Fact]
    public async Task Index_DmDirectory_ContainsProfileLinkForEachDm()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (_, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "directorydm", "directorydm@example.com", name: "Directory DM",
            roles: ["DungeonMaster"]);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        var response = await client.GetAsync("/Players", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // DM name in directory must be wrapped in a link to their profile
        content.Should().Contain($"/DungeonMaster/Profile/{dmUser.Id}");
        content.Should().Contain("Directory DM");
    }
}
