using Microsoft.AspNetCore.Http;
using QuestBoard.Service.Helpers;

namespace QuestBoard.UnitTests.Helpers;

public class BrowserNavigationTests
{
    private const string BrowserAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    private static HttpRequest RequestWith(string? accept, string? fetchDest = null, string? requestedWith = null)
    {
        var context = new DefaultHttpContext();
        if (accept != null)
            context.Request.Headers.Accept = accept;
        if (fetchDest != null)
            context.Request.Headers["Sec-Fetch-Dest"] = fetchDest;
        if (requestedWith != null)
            context.Request.Headers["X-Requested-With"] = requestedWith;
        return context.Request;
    }

    [Theory]
    [InlineData(BrowserAccept, null, null, true)]
    [InlineData(BrowserAccept, "document", null, true)]
    [InlineData(BrowserAccept, "iframe", null, true)]
    [InlineData(BrowserAccept, "frame", null, true)]
    [InlineData("TEXT/HTML", null, null, true)]
    [InlineData(BrowserAccept, "empty", null, false)]
    [InlineData(BrowserAccept, "image", null, false)]
    [InlineData(BrowserAccept, "script", null, false)]
    [InlineData(BrowserAccept, "style", null, false)]
    [InlineData("*/*", null, null, false)]
    [InlineData("*/*", "document", null, false)]
    [InlineData("application/json", null, null, false)]
    [InlineData("text/calendar", null, null, false)]
    [InlineData(null, null, null, false)]
    [InlineData(BrowserAccept, null, "XMLHttpRequest", false)]
    [InlineData(BrowserAccept, "document", "XMLHttpRequest", false)]
    [InlineData("text/html", null, "xmlhttprequest", false)]
    public void IsDocumentNavigation_FollowsTheAcceptAndFetchDestinationRule(
        string? accept, string? fetchDest, string? requestedWith, bool expected)
    {
        BrowserNavigation.IsDocumentNavigation(RequestWith(accept, fetchDest, requestedWith)).Should().Be(expected);
    }

    private static HttpRequest RequestFor(string host, string? referer)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        if (referer != null)
            context.Request.Headers.Referer = referer;
        return context.Request;
    }

    [Theory]
    [InlineData("host", "https://host/Quest/Details/5?x=1", "/Quest/Details/5?x=1")]
    [InlineData("host", "http://host/Quest/Details/5", "/Quest/Details/5")]
    [InlineData("HOST", "https://host/quests", "/quests")]
    [InlineData("host:5000", "http://host:5000/Shop?page=2", "/Shop?page=2")]
    [InlineData("host", "https://host/", "/")]
    [InlineData("host", "https://host", "/")]
    [InlineData("host", "https://other/Quest/Details/5", null)]
    [InlineData("host", "https://host.evil.example/Quest", null)]
    [InlineData("host", "https://evil.example/@host/Quest", null)]
    [InlineData("host:5000", "http://host:6000/Quest", null)]
    [InlineData("host", "//host/Quest", null)]
    [InlineData("host", "//other/Quest", null)]
    [InlineData("host", "/Quest/Details/5", null)]
    [InlineData("host", "Quest/Details/5", null)]
    [InlineData("host", "javascript:alert(1)", null)]
    [InlineData("host", "garbage value", null)]
    [InlineData("host", "", null)]
    [InlineData("host", null, null)]
    [InlineData("host", "https://host/Error/404", null)]
    [InlineData("host", "https://host/error/404", null)]
    [InlineData("host", "https://host/Error", null)]
    [InlineData("host", "https://host/%45rror/404", null)]
    [InlineData("host", "https://host//evil.example/path", null)]
    [InlineData("host", "https://host/\\evil.example/path", null)]
    [InlineData("host", "https://host/Errors/list", "/Errors/list")]
    public void SameSiteLocalReferer_AcceptsOnlyALocalPathOfTheSameHost(string host, string? referer, string? expected)
    {
        BrowserNavigation.SameSiteLocalReferer(RequestFor(host, referer)).Should().Be(expected);
    }
}
