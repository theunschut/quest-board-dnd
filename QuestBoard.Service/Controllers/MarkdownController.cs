using QuestBoard.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers;

/// <summary>
/// Server round-trip Markdown preview used by the client-side editor's Preview toggle. Renders
/// through the same <see cref="IMarkdownService.RenderToHtml"/> call/target used for saved page
/// display, so preview output is guaranteed byte-identical to what a reader ultimately sees.
/// </summary>
[Authorize]
public class MarkdownController(IMarkdownService markdownService) : Controller
{
    public record PreviewRequest(string? Markdown);

    [HttpPost]
    [Route("markdown/preview")]
    [ValidateAntiForgeryToken]
    public IActionResult Preview([FromBody] PreviewRequest? request)
    {
        // Always the Web target: preview must match the page display (Details/Manage), which
        // never strips images the way the Email target does. Email rendering is a separate,
        // asynchronous surface the author never sees live.
        var html = markdownService.RenderToHtml(request?.Markdown, MarkdownRenderTarget.Web);
        return Content(html, "text/html");
    }
}
