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

    /// <summary>
    /// Renders the same sanitized HTML used for display, then strips it down to plain text so
    /// the board card can show a short preview without Markdown syntax characters inflating the
    /// displayed length. A null, empty, or whitespace-only input returns <see cref="string.Empty"/>.
    /// </summary>
    string ExtractPlainText(string? markdown);

    /// <summary>
    /// Renders Quest Description Markdown for the 3 transactional quest emails: the same sanitized
    /// structural HTML as <see cref="RenderToHtml"/> with <see cref="MarkdownRenderTarget.Email"/>,
    /// with every block/inline element Markdig can emit carrying its own explicit inline
    /// <c>style=</c> attribute plus an Outlook bullet-visibility fallback on every list item. Every
    /// injected style string is a hard-coded C# constant keyed by tag name -- never built from any
    /// part of <paramref name="markdown"/> or the sanitized HTML -- so this step never re-opens the
    /// XSS surface the sanitizer already closed. A null, empty, or whitespace-only input returns
    /// <see cref="string.Empty"/>. When the rendered content exceeds <paramref name="maxTopLevelBlocks"/>
    /// top-level block elements or <paramref name="maxPlainTextChars"/> of extracted plain text
    /// (whichever limit is reached first), the output is cut at the last complete block boundary
    /// -- never mid-element -- and a "read more" link to <paramref name="readMoreUrl"/> is appended;
    /// content within both budgets is returned untouched with no link appended.
    /// </summary>
    string RenderEmailHtml(string? markdown, string readMoreUrl, int maxTopLevelBlocks = 5, int maxPlainTextChars = 650);
}
