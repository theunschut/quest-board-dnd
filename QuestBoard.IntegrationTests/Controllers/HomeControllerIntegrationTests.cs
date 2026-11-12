using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class HomeControllerIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactoryBase _factory;

    public HomeControllerIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ShouldReturnSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_ShouldReturnHtmlContent()
    {
        // Act
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        content.Should().NotBeNullOrEmpty();
    }

    // Home is now a public landing page — it must show login copy and must NOT
    // display quest content, even when quests exist in the database.
    [Fact]
    public async Task Index_ShouldContainLoginButton()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        // Act
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Log in");
        content.Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task Index_WithQuests_ShouldNotDisplayQuestList()
    {
        // Arrange — even with quests seeded, the public landing page must not surface them
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "homedm", "home@example.com");

        await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Home Quest 1");
        await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Home Quest 2");

        // Act
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Home Quest 1");
        content.Should().NotContain("Home Quest 2");
        content.Should().Contain("Log in");
    }

    [Fact]
    public async Task Index_PhoneUserAgent_ServesTheLandingInThePhoneShell()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");

        // Act
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("mobile-layout");
        content.Should().Contain("/Account/Login");
        System.Text.RegularExpressions.Regex.Matches(content, "<h1").Count.Should().Be(1);
    }

    // REMOVED: Privacy and Error tests - these routes don't exist in HomeController
    // HomeController only has an Index() action
    // If these routes are needed in the future, add Privacy() and Error() actions to HomeController

    [Fact]
    public async Task NonExistentRoute_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/NonExistent/Route", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
