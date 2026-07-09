using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Covers the POST /markdown/preview round trip (EDITOR-04's "preview matches saved render"
/// guarantee) using this codebase's first header-based-antiforgery + JSON-body integration test.
///
/// The CSRF token check below is structural (reflection), not a live 400 assertion: this test
/// harness's <c>WebApplicationFactoryBase</c> replaces <c>IAntiforgery</c> with a
/// <c>TestAntiforgeryDecorator</c> that always succeeds validation regardless of what a request
/// sends, so a live HTTP POST missing the header cannot observe a 400 here -- see the identical,
/// already-established precedent in <c>Security/AntiForgeryTokenCoverageTests.cs</c> and
/// <c>UsersControllerIntegrationTests.Disable_And_Enable_Actions_CarryValidateAntiForgeryToken</c>.
/// </summary>
public class MarkdownControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string PreviewUrl = "/markdown/preview";

    private static async Task<(HttpClient Client, string HeaderToken)> CreateAuthenticatedClientWithHeaderTokenAsync(
        WebApplicationFactoryBase factory, int questId)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mdpreviewer", "mdpreviewer@example.com");

        // Any authenticated page that renders a standard asp-action form (Quest Details does)
        // emits the same request-verification token this app's existing header-based fetch()
        // calls reuse -- see AntiForgeryHelper.ExtractHeaderAntiForgeryTokenAsync.
        var getResponse = await client.GetAsync($"/Quest/Details/{questId}", TestContext.Current.CancellationToken);
        var (headerToken, _) = await AntiForgeryHelper.ExtractHeaderAntiForgeryTokenAsync(getResponse);

        return (client, headerToken);
    }

    private static StringContent MarkdownJsonBody(string markdown)
    {
        var json = $"{{\"markdown\":{System.Text.Json.JsonSerializer.Serialize(markdown)}}}";
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    [Fact]
    public async Task Preview_AuthenticatedWithHeaderToken_ReturnsSanitizedHtmlMatchingRenderToHtml()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "mdquestdm", "mdquestdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Markdown Preview Quest");

        var (client, headerToken) = await CreateAuthenticatedClientWithHeaderTokenAsync(factory, quest.Id);

        var request = new HttpRequestMessage(HttpMethod.Post, PreviewUrl)
        {
            Content = MarkdownJsonBody("**bold**")
        };
        request.Headers.Add("RequestVerificationToken", headerToken);

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("<strong>bold</strong>");

        // EDITOR-04: preview output must be byte-identical to the same Web-target render used
        // for saved page display, not just "structurally similar".
        var markdownService = factory.Services.GetRequiredService<IMarkdownService>();
        var expectedHtml = markdownService.RenderToHtml("**bold**", MarkdownRenderTarget.Web);
        body.Should().Be(expectedHtml);
    }

    [Fact]
    public void MarkdownController_Preview_CarriesValidateAntiForgeryToken()
    {
        // Looked up by name (not typeof) so this test file compiles before MarkdownController
        // exists -- it fails here (type not found) during RED rather than at build time.
        var controllerType = typeof(Program).Assembly.GetType("QuestBoard.Service.Controllers.MarkdownController");
        controllerType.Should().NotBeNull("MarkdownController must exist in QuestBoard.Service.Controllers");

        var previewAction = controllerType!.GetMethod("Preview");
        previewAction.Should().NotBeNull("MarkdownController must define a Preview action");

        previewAction!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().Should().NotBeNull(
            "POST /markdown/preview must require a valid antiforgery token (T-66-03)");

        var authorizeAttr = controllerType.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        authorizeAttr.Should().NotBeNull("MarkdownController must require authentication for any group member");
    }

    [Fact]
    public async Task Preview_WithoutAntiForgeryHeader_DoesNotServerError()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "mdquestdm2", "mdquestdm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Markdown Preview Quest 2");

        var (client, _) = await CreateAuthenticatedClientWithHeaderTokenAsync(factory, quest.Id);

        // Act -- POST with no RequestVerificationToken header at all. As documented above, the
        // TestAntiforgeryDecorator always validates successfully in this harness, so this cannot
        // observe a 400 here; MarkdownController_Preview_CarriesValidateAntiForgeryToken above is
        // what actually proves T-66-03's mitigation is wired up.
        var response = await client.PostAsync(PreviewUrl, MarkdownJsonBody("**bold**"), TestContext.Current.CancellationToken);

        // Assert -- no server error; reaches (or would reach) the controller's own logic.
        ((int)response.StatusCode).Should().BeLessThan(500);
    }

    [Fact]
    public async Task Preview_Unauthenticated_IsNotServedSuccessfully()
    {
        // Act
        var response = await factory.CreateNonRedirectingClient()
            .PostAsync(PreviewUrl, MarkdownJsonBody("**bold**"), TestContext.Current.CancellationToken);

        // Assert -- anonymous access must not succeed (redirect/401/403 all acceptable denials).
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
