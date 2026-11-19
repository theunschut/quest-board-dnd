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
}
