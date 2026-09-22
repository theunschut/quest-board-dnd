using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace QuestBoard.IntegrationTests.Controllers;

public class BoardTimeZoneHealthCheckTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Health_WithResolvableZone_ReturnsOkAndHealthy()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body);
    }

    // Swaps in an unresolvable board time zone id so BoardClock degrades to UTC, following the
    // same WithWebHostBuilder variant-factory pattern used elsewhere in this suite.
    private WebApplicationFactory<Program> CreateDegradedZoneFactory()
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TimeZone:BoardTimeZoneId"] = "Definitely/NotAZone"
                });
            });
        });
    }

    [Fact]
    public async Task Health_WithUnresolvableZone_ReturnsOkAndDegraded()
    {
        // Arrange
        var variantFactory = CreateDegradedZoneFactory();
        var client = variantFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert — must stay HTTP 200 or docker-compose's `curl -f` restart-loops the container.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Degraded", body);
    }

    [Fact]
    public async Task Health_WithUnresolvableZone_DoesNotDiscloseConfiguredZoneId()
    {
        // Arrange
        var variantFactory = CreateDegradedZoneFactory();
        var client = variantFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert — the degraded state is disclosed, the configuration value is not.
        Assert.DoesNotContain("Definitely/NotAZone", body);
    }
}
