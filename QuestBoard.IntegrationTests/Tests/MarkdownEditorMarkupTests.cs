using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the markup the shared Markdown editor partial renders: a visible hint under the field that
/// the editor input is described by, a server-rendered label the script turns into the input's
/// accessible name, and no hover-only tooltip icon. The behaviour that Tab leaves the editor can
/// only be seen in a browser; what a rendered request can hold is that the markup the script relies
/// on is there on a quest form and on a form of another area, for both layouts.
/// </summary>
public class MarkdownEditorMarkupTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string HintSentence = "Supports Markdown formatting. Leave a blank line between paragraphs.";

    private static readonly Regex HintElement = new(
        @"<div id=""(?<id>[^""]+-help)"" class=""form-text"">(?<text>[^<]*)</div>",
        RegexOptions.Compiled);

    private static readonly Regex EditorTextarea = new(
        @"<textarea\b[^>]*\bclass=""[^""]*markdown-editor-textarea[^""]*""[^>]*>",
        RegexOptions.Compiled);

    private readonly WebApplicationFactoryBase _factory;

    public MarkdownEditorMarkupTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} should render for this user");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertEditorMarkup(string html)
    {
        var textarea = EditorTextarea.Match(html);
        textarea.Success.Should().BeTrue("the page renders the shared editor textarea");

        var hint = HintElement.Match(html);
        hint.Success.Should().BeTrue("the editor has a visible help element under it");
        hint.Groups["text"].Value.Should().Be(HintSentence);

        textarea.Value.Should().Contain($"aria-describedby=\"{hint.Groups["id"].Value}\"");
        textarea.Value.Should().Contain("data-editor-label=\"");

        // The hint used to live behind a hover-only tooltip icon on the label.
        html.Should().NotContain("data-bs-toggle=\"tooltip\" title=\"" + HintSentence);
    }

    [Fact]
    public async Task QuestCreate_DesktopUserAgent_RendersEditorHintAndLabel()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "editor_markup_dm1", "editor_markup_dm1@test.com", name: "Editor Markup Dm1");

        var html = await GetHtmlAsync(client, "/Quest/Create", DesktopUserAgent);

        AssertEditorMarkup(html);
    }

    [Fact]
    public async Task QuestCreate_PhoneUserAgent_RendersEditorHintAndLabel()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "editor_markup_dm2", "editor_markup_dm2@test.com", name: "Editor Markup Dm2");

        var html = await GetHtmlAsync(client, "/Quest/Create", MobileUserAgent);

        AssertEditorMarkup(html);
    }

    [Fact]
    public async Task CharacterCreate_DesktopUserAgent_RendersEditorHintAndLabel()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "editor_markup_player1", "editor_markup_player1@test.com", name: "Editor Markup Player1");

        var html = await GetHtmlAsync(client, "/Characters/Create", DesktopUserAgent);

        AssertEditorMarkup(html);
    }

    [Fact]
    public async Task QuestCreate_EditorToken_ReachesTheScriptThroughAMetaElement()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "editor_markup_dm3", "editor_markup_dm3@test.com", name: "Editor Markup Dm3");

        var html = await GetHtmlAsync(client, "/Quest/Create", DesktopUserAgent);

        html.Should().Contain("<meta name=\"request-verification-token\" content=\"");
        html.Should().NotContain("window.markdownAntiforgeryToken =");
    }
}
