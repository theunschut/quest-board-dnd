using Ganss.Xss;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Domain.Services;

internal class MarkdownService : IMarkdownService
{
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

        var rawHtml = Markdown.ToHtml(markdown, Pipeline);

        return target == MarkdownRenderTarget.Email
            ? EmailSanitizer.Sanitize(rawHtml)
            : WebSanitizer.Sanitize(rawHtml);
    }
}
