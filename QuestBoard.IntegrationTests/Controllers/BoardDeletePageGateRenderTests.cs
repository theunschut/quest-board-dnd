using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Pins what the board delete confirmation page ships before any script runs: the Delete board
/// button is disabled on both layouts and the typed-name gate module is loaded. Without JavaScript the
/// button never unlocks, so the server stays the only authority on whether a delete goes ahead.
/// </summary>
public class BoardDeletePageGateRenderTests : IClassFixture<WebApplicationFactoryBase>
{
    private const int BoardId = 71;
    private const string BoardName = "Gate Check Table";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public static TheoryData<string> UserAgents => new() { DesktopUserAgent, MobileUserAgent };

    private readonly WebApplicationFactoryBase _factory;

    public BoardDeletePageGateRenderTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Get_RendersTheDeleteButtonDisabled(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/platform/Group/Delete/{BoardId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));

        var button = Regex.Match(html, "<button\\b[^>]*data-board-delete-submit[^>]*>");
        button.Success.Should().BeTrue("the page should carry the Delete board button");
        button.Value.Should().MatchRegex("data-board-delete-submit\\s+disabled",
            "the server renders the button disabled and only the gate module may enable it");

        html.Should().Contain("platform-board-delete.js", "the page must load the typed-name gate module");
        html.Should().MatchRegex("<script\\b[^>]*type=\"module\"[^>]*platform-board-delete\\.js|<script\\b[^>]*platform-board-delete\\.js[^>]*type=\"module\"");
    }
}
