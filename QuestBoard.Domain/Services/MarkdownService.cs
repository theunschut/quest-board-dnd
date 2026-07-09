using Ganss.Xss;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using QuestBoard.Domain.Interfaces;
using System.Text.RegularExpressions;

namespace QuestBoard.Domain.Services;

internal class MarkdownService : IMarkdownService
{
    // Collapses runs of whitespace (including literal newlines emitted by Markdig's own HTML
    // formatting between sibling elements) into a single space during plain-text extraction.
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    // Extensions are composed individually, one Use*() call per feature, rather than via the
    // bundled convenience method that chains 19 extensions at once -- that bundle includes generic
    // attributes ({...} syntax), which lets arbitrary HTML attributes, including event handlers, be
    // injected through Markdown text. Composing only the extensions actually needed keeps that
    // attack surface out entirely.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoLinks()
        .UsePipeTables()
        .UseTaskLists()
        .UseDefinitionLists()
        .UseFootnotes()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .Build();

    // Tags shared by both sanitizer profiles. HtmlSanitizer removes an entire subtree -- not just
    // the wrapping tag -- when a tag isn't allowlisted, so every container tag produced by the
    // extensions above (table family, dl/dt/dd, the footnote-group div) must be present here or
    // its text content is silently deleted, not just unwrapped.
    private static readonly HashSet<string> BaseAllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "strong", "em", "code", "pre", "blockquote", "hr", "a",
        "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li",
        "del",
        "table", "thead", "tbody", "tr", "th", "td",
        "input",
        "dl", "dt", "dd",
        "sup", "div",
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href",
        "src", "alt",
        // "id" is allowed only so footnote jump-links (fnref:N / fn:N) work -- Markdig emits only
        // sequential integers for these, never a user-chosen value, so this does not open a
        // DOM-clobbering vector via arbitrary attacker-controlled id values.
        "id",
        "type", "disabled", "checked",
    };

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto",
    };

    private static readonly HashSet<string> UriAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "src",
    };

    // Two separate sanitizer instances rather than one instance reconfigured per call: a singleton
    // service serving concurrent requests must never mutate a shared sanitizer's AllowedTags set,
    // since that would race with in-flight Sanitize() calls on other threads.
    private static readonly HtmlSanitizer WebSanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
    {
        AllowedTags = new HashSet<string>(BaseAllowedTags, StringComparer.OrdinalIgnoreCase) { "img" },
        AllowedAttributes = AllowedAttributes,
        AllowedSchemes = AllowedSchemes,
        UriAttributes = UriAttributes,
    });

    private static readonly HtmlSanitizer EmailSanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
    {
        AllowedTags = new HashSet<string>(BaseAllowedTags, StringComparer.OrdinalIgnoreCase),
        AllowedAttributes = AllowedAttributes,
        AllowedSchemes = AllowedSchemes,
        UriAttributes = UriAttributes,
    });

    /// <inheritdoc/>
    public string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        string rawHtml;
        try
        {
            rawHtml = Markdown.ToHtml(markdown, Pipeline);
        }
        catch (ArgumentException)
        {
            // Markdig's own nesting-depth guard tripped on pathologically nested input (e.g.
            // hundreds of nested blockquote/emphasis markers). Fail safe by HTML-encoding the raw
            // input instead of throwing into the caller.
            return System.Net.WebUtility.HtmlEncode(markdown);
        }

        return target == MarkdownRenderTarget.Email
            ? EmailSanitizer.Sanitize(rawHtml)
            : WebSanitizer.Sanitize(rawHtml);
    }

    /// <inheritdoc/>
    public string ExtractPlainText(string? markdown)
    {
        var html = RenderToHtml(markdown, MarkdownRenderTarget.Web);
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var parser = new AngleSharp.Html.Parser.HtmlParser();
        using var document = parser.ParseDocument(html);

        // Join top-level block elements (p, ul, ol, h1-h6, blockquote, table, dl) with a space so
        // word boundaries at block edges survive. document.Body.TextContent alone concatenates
        // descendant text nodes with zero inserted whitespace at block-element boundaries --
        // "<h1>Foo</h1><p>Bar</p>".TextContent would be "FooBar", not "Foo Bar".
        var parts = document.Body!.Children
            .Select(el => el.TextContent.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        // RenderToHtml's pathological-nesting guard falls back to bare HTML-encoded text with no
        // wrapping element at all, so parsing that fallback yields zero element children here --
        // fall back to the parsed document's raw text content instead of silently returning "".
        if (parts.Count == 0)
        {
            return WhitespaceRun.Replace(document.Body.TextContent, " ").Trim();
        }

        var joined = string.Join(" ", parts);

        // Markdig's own HTML formatting inserts newlines between sibling elements inside a block
        // (e.g. between <li> items), which surface as literal newlines in TextContent. Collapse
        // any run of whitespace down to a single space so word boundaries stay uniform.
        return WhitespaceRun.Replace(joined, " ").Trim();
    }
}
