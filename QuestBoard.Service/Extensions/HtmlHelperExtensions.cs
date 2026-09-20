using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Extensions;

/// <summary>
/// Read-side Razor helper for rendering sanitized Markdown. Views never touch the sanitizer or
/// Markdig directly -- this is the single call site for turning a stored Markdown string into
/// display-ready HTML.
/// </summary>
internal static class HtmlHelperExtensions
{
    /// <summary>
    /// Renders sanitized Markdown HTML wrapped in a `.markdown-content` div, giving every rendered
    /// field a single, consistent styling hook. Resolves <see cref="IMarkdownService"/> per-request
    /// from RequestServices rather than requiring every view to inject it directly.
    /// </summary>
    internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
    {
        var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
        var rendered = service.RenderToHtml(markdown, MarkdownRenderTarget.Web);
        return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
    }

    // The four canonical timestamp styles this whole application collapses down to -- every
    // real-instant render site picks exactly one of these rather than keeping its own bespoke
    // format string. Kept in sync with the client-side Intl.DateTimeFormat options table in
    // site.js: the two must agree on what each style name means.
    private static readonly IReadOnlyDictionary<string, string> LocalTimeFormats = new Dictionary<string, string>
    {
        ["date"] = "MMM d, yyyy",
        ["date-time"] = "MMM d, yyyy, h:mm tt",
        ["date-compact"] = "MMM d",
        ["date-time-compact"] = "MMM d, h:mm tt",
    };

    /// <summary>
    /// Builds the `&lt;time&gt;` element a real (UTC) instant renders as: board-zone text on
    /// first paint, the raw UTC instant in `datetime` for the client to re-format, and the raw
    /// UTC instant at full precision in `title` for anyone hovering to check "what time is this,
    /// really." A pure function with no <see cref="IHtmlHelper"/> dependency, so it is directly
    /// unit-testable without a Razor context.
    /// </summary>
    internal static IHtmlContent BuildLocalTime(DateTime utcInstant, string style, TimeZoneInfo boardZone)
    {
        if (!LocalTimeFormats.TryGetValue(style, out var format))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "Unrecognised local-time style.");
        }

        // EF hands back DateTimeKind.Unspecified for a value stored as UTC; ConvertTimeFromUtc
        // throws on DateTimeKind.Local, so the instant is always normalised to Utc first.
        var utc = DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);
        var boardTime = TimeZoneInfo.ConvertTimeFromUtc(utc, boardZone);

        var tag = new TagBuilder("time");
        tag.MergeAttribute("class", "local-time");
        tag.MergeAttribute("data-style", style);
        tag.MergeAttribute("datetime", utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture));
        tag.MergeAttribute("title", utc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        tag.InnerHtml.SetContent(boardTime.ToString(format, CultureInfo.InvariantCulture));

        return tag;
    }

    /// <summary>
    /// Renders <paramref name="utcInstant"/> in the board's configured time zone on first paint,
    /// wrapped in the markup the client-side hydration pass later swaps to the viewer's own
    /// zone. Resolves <see cref="IBoardClock"/> per-request from RequestServices, mirroring how
    /// <see cref="Markdown"/> resolves <see cref="IMarkdownService"/> above.
    /// </summary>
    internal static IHtmlContent LocalTime(this IHtmlHelper html, DateTime utcInstant, string style)
    {
        var boardClock = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IBoardClock>();
        return BuildLocalTime(utcInstant, style, boardClock.TimeZone);
    }
}
