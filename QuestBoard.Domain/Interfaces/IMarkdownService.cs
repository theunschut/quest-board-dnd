namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Selects which sanitization policy a rendered HTML fragment is intended for. Both targets are
/// produced from the same parsed Markdown -- only the allowed-tag policy differs (the web target
/// keeps images, the email target strips them).
/// </summary>
public enum MarkdownRenderTarget { Web, Email }

public interface IMarkdownService
{
    /// <summary>
    /// Converts Markdown text into sanitized HTML safe to render directly in a browser or email
    /// client. A null, empty, or whitespace-only input returns <see cref="string.Empty"/>. Input
    /// that trips Markdig's own nesting-depth guard (e.g. hundreds of nested blockquote or
    /// emphasis markers) is never thrown into the caller -- it is returned HTML-encoded instead,
    /// so callers never need to catch an exception from this method.
    /// </summary>
    string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web);
}
