using System.Net;
using QuestBoard.Service.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

public class HealthVersionHeaderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Health_ReportsTheRunningBuildVersionInAHeader()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AppVersion.Current, response.Headers.GetValues("X-QuestBoard-Version").Single());
        Assert.Contains("Healthy", body);
    }
}
