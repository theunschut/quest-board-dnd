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
}
