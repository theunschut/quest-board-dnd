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

    /// <summary>
    /// The page the viewer came from, as a local path and query, or null. The Referer header is
    /// whatever the client chose to send, so it is only ever reduced to a path: it must parse as an
    /// absolute address on the host this request was made to (the scheme is ignored, the port is not),
    /// and what comes back never carries an authority, so it cannot point anywhere else. An address
    /// that begins with a second slash or backslash would read as protocol-relative and is refused,
    /// as is a page under /Error, which would only lead back to an error page.
    /// </summary>
    public static string? SameSiteLocalReferer(HttpRequest request)
    {
        var raw = request.Headers.Referer.ToString();
        if (raw.Length == 0 || !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
            return null;

        var local = uri.PathAndQuery;
        if (local.Length == 0 || local[0] != '/')
            return null;

        if (local.Length > 1 && (local[1] == '/' || local[1] == '\\'))
            return null;

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (string.Equals(path, "/Error", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Error/", StringComparison.OrdinalIgnoreCase))
            return null;

        return local;
    }
}
