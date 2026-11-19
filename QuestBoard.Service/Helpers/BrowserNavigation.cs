using Microsoft.AspNetCore.Http;

namespace QuestBoard.Service.Helpers;

/// <summary>
/// The one rule for "is this request a browser asking for a page to show". An HTML error page is
/// only ever the right answer to such a request; a script's fetch, an image, a stylesheet or a
/// calendar client must keep receiving the bare status it receives today.
///
/// Accept is the primary test because not every browser sends the Sec-Fetch headers. Sec-Fetch-Dest
/// is the guard that stops a later fetch call that happens to send an HTML Accept header from being
/// handed a page: fetch and XHR send "empty", images send "image", a top-level navigation sends
/// "document" and frames send "iframe" or "frame".
/// </summary>
public static class BrowserNavigation
{
    public static bool IsDocumentNavigation(HttpRequest request)
    {
        if (string.Equals(request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!request.Headers.Accept.Any(value => value != null && value.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
            return false;

        var destination = request.Headers["Sec-Fetch-Dest"].ToString();
        return destination.Length == 0
            || string.Equals(destination, "document", StringComparison.OrdinalIgnoreCase)
            || string.Equals(destination, "iframe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(destination, "frame", StringComparison.OrdinalIgnoreCase);
    }
}
