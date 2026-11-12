namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Cuts a phone-layout page into the two regions facts need to tell apart: the page's own content
/// and the navigation menu. The menu repeats words the page content may not (a link's name), so an
/// assertion on the whole document cannot say where a link lives.
/// </summary>
public static class PhonePageRegions
{
    /// <summary>The markup between the main element's opening and closing tags.</summary>
    public static string PageContent(string html) => Between(html, "<main", "</main>");

    /// <summary>The markup of the navigation menu the phone layout opens from its toggler.</summary>
    public static string NavigationMenu(string html) => Between(html, "<nav class=\"offcanvas-body\"", "</nav>");

    private static string Between(string html, string open, string close)
    {
        var start = html.IndexOf(open, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"the phone layout must contain {open}");
        var end = html.IndexOf(close, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"the phone layout must close {open} with {close}");
        return html[start..end];
    }
}
