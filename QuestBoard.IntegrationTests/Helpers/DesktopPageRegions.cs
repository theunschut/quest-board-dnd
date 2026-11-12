namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Cuts a desktop-layout page into the two regions facts need to tell apart: the page's own content
/// and the navigation bar. The bar repeats words the page content may not (a link's name), so an
/// assertion on the whole document cannot say where a link lives.
/// </summary>
public static class DesktopPageRegions
{
    /// <summary>The markup between the main element's opening and closing tags.</summary>
    public static string PageContent(string html) => Between(html, "<main", "</main>");

    /// <summary>The markup of the fixed navigation bar, from its header element to the end of it.</summary>
    public static string NavigationBar(string html) => Between(html, "<header", "</header>");

    private static string Between(string html, string open, string close)
    {
        var start = html.IndexOf(open, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"the desktop layout must contain {open}");
        var end = html.IndexOf(close, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"the desktop layout must close {open} with {close}");
        return html[start..end];
    }
}
