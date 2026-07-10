using AngleSharp.Dom;
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

    // Every style string below is copied verbatim from the email typography design contract and
    // is a compile-time C# constant -- never built from markdown/user input. RenderEmailHtml
    // applies these via IElement.SetAttribute() strictly after EmailSanitizer.Sanitize() has
    // already run, so this table never needs to pass through the sanitizer's CSS-value parser
    // (a known source of whole-attribute-drop bugs in that parser for other, unrelated values).
    private const string LargeHeadingStyle = "font-size:20px;font-weight:700;font-style:normal;font-family:Georgia,serif;line-height:1.25;color:#1a0f08;margin:16px 0 8px 0;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);";
    private const string MediumHeadingStyle = "font-size:18px;font-weight:700;font-style:normal;font-family:Georgia,serif;line-height:1.25;color:#1a0f08;margin:16px 0 8px 0;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);";
    private const string SmallHeadingStyle = "font-size:16px;font-weight:700;font-style:normal;font-family:Georgia,serif;line-height:1.3;color:#1a0f08;margin:16px 0 4px 0;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);";
    private const string ParagraphStyle = "font-size:15px;font-weight:400;font-style:italic;font-family:Georgia,serif;line-height:1.6;color:#1a0f08;margin:0 0 12px 0;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);";
    private const string ListStyle = "margin:0 0 12px 0;padding:0 0 0 20px;list-style-position:outside;";
    private const string UnorderedListItemStyle = "margin:0 0 4px 0;font-size:15px;font-weight:400;font-style:italic;font-family:Georgia,serif;line-height:1.5;color:#1a0f08;list-style-type:disc;display:list-item;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);";
    private const string OrderedListItemStyle = "margin:0 0 4px 0;font-size:15px;font-weight:400;font-style:italic;font-family:Georgia,serif;line-height:1.5;color:#1a0f08;list-style-type:decimal;display:list-item;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);";
    private const string BlockquoteStyle = "margin:0 0 12px 0;padding:4px 0 4px 12px;border-left:3px solid #FFD700;font-size:15px;font-weight:400;font-style:italic;font-family:Georgia,serif;line-height:1.6;color:#1a0f08;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);";
    private const string LinkStyle = "color:#8B4513;font-weight:700;text-decoration:underline;";
    private const string StrongStyle = "font-weight:700;";
    private const string EmphasisStyle = "font-style:italic;";
    private const string ThematicBreakStyle = "border:none;border-top:1px solid #FFD700;margin:12px 0;";
    private const string ReadMoreLinkStyle = "color:#8B4513;font-weight:700;text-decoration:underline;font-style:italic;font-family:Georgia,serif;font-size:15px;";
    private const string ReadMoreLinkText = "…continue reading on the quest board";

    // A single hard-coded literal, never parameterized by request data. Outlook's Word rendering
    // engine still fails to show a bullet marker for some list-style-type:disc/decimal content
    // even with the CSS above set correctly, so a literal bullet character is injected as an
    // MSO-only conditional comment as a belt-and-suspenders fallback. Every other client (Gmail,
    // browsers, mobile mail apps) parses this as an inert, invisible HTML comment.
    private const string OutlookBulletFallbackComment = "[if mso]>&#8226;&nbsp;<![endif]";

    // Tag -> style lookup for every element RenderEmailHtml styles except <li>, whose style
    // depends on its parent (<ul> vs <ol>) and is therefore resolved separately below.
    private static readonly Dictionary<string, string> EmailBlockStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["h1"] = LargeHeadingStyle,
        ["h2"] = LargeHeadingStyle,
        ["h3"] = MediumHeadingStyle,
        ["h4"] = MediumHeadingStyle,
        ["h5"] = SmallHeadingStyle,
        ["h6"] = SmallHeadingStyle,
        ["p"] = ParagraphStyle,
        ["ul"] = ListStyle,
        ["ol"] = ListStyle,
        ["blockquote"] = BlockquoteStyle,
        ["a"] = LinkStyle,
        ["strong"] = StrongStyle,
        ["em"] = EmphasisStyle,
        ["hr"] = ThematicBreakStyle,
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

    /// <inheritdoc/>
    public string RenderEmailHtml(string? markdown, string readMoreUrl, int maxTopLevelBlocks = 5, int maxPlainTextChars = 650)
    {
        var sanitizedHtml = RenderToHtml(markdown, MarkdownRenderTarget.Email);
        if (string.IsNullOrEmpty(sanitizedHtml))
        {
            return string.Empty;
        }

        var parser = new AngleSharp.Html.Parser.HtmlParser();
        using var document = parser.ParseDocument(sanitizedHtml);

        // Every style value comes from the constant table above, keyed only by tag name -- never
        // by anything read out of the DOM we're walking -- so this pass cannot be steered by
        // markdown content no matter how the sanitized HTML is shaped.
        foreach (var (tag, style) in EmailBlockStyles)
        {
            foreach (var element in document.Body!.QuerySelectorAll(tag))
            {
                element.SetAttribute("style", style);
            }
        }

        // <li> needs a style that depends on its parent list type, which the flat tag lookup above
        // can't express, so it's handled in its own pass.
        foreach (var li in document.Body!.QuerySelectorAll("li"))
        {
            var isOrdered = li.ParentElement?.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase) == true;
            li.SetAttribute("style", isOrdered ? OrderedListItemStyle : UnorderedListItemStyle);

            var bulletFallback = document.CreateComment(OutlookBulletFallbackComment);
            li.InsertBefore(bulletFallback, li.FirstChild);
        }

        // Markdig's pathological-nesting guard (see RenderToHtml) can fall back to bare
        // HTML-encoded text with no wrapping element at all -- there's nothing to walk/truncate in
        // that case, so pass the already-sanitized text straight through rather than losing it.
        if (!document.Body!.Children.Any())
        {
            return sanitizedHtml;
        }

        return TruncateAtBlockBoundary(document, readMoreUrl, maxTopLevelBlocks, maxPlainTextChars);
    }

    // Cuts only at whole top-level block boundaries so the emitted HTML is always well-formed --
    // never a partially-closed <li>/<blockquote>/heading. This is deliberately not built on top of
    // ExtractPlainText's flat-text truncation, which would discard the Markdown structure this
    // method exists to preserve.
    private static string TruncateAtBlockBoundary(IDocument document, string readMoreUrl, int maxTopLevelBlocks, int maxPlainTextChars)
    {
        var kept = new List<IElement>();
        var plainTextLength = 0;
        var truncated = false;

        foreach (var block in document.Body!.Children)
        {
            var blockText = block.TextContent.Trim();
            var wouldExceedBlocks = kept.Count >= maxTopLevelBlocks;
            var wouldExceedChars = plainTextLength + blockText.Length > maxPlainTextChars;

            if (wouldExceedBlocks || wouldExceedChars)
            {
                truncated = true;
                break;
            }

            kept.Add(block);
            plainTextLength += blockText.Length;
        }

        if (kept.Count == 0)
        {
            return truncated ? BuildReadMoreParagraph(document, readMoreUrl) : string.Empty;
        }

        // The Description column's own wrapper already supplies top/bottom spacing, so the first
        // and last rendered elements must not stack a second gap on top of that -- these
        // corrections append a compile-time-constant fragment to an already-set style value, the
        // same trust argument as the style table above.
        var firstStyle = kept[0].GetAttribute("style") ?? string.Empty;
        kept[0].SetAttribute("style", firstStyle + "margin-top:0;");

        if (truncated)
        {
            return string.Concat(kept.Select(e => e.OuterHtml)) + BuildReadMoreParagraph(document, readMoreUrl);
        }

        var lastStyle = kept[^1].GetAttribute("style") ?? string.Empty;
        kept[^1].SetAttribute("style", lastStyle + "margin-bottom:0;");

        return string.Concat(kept.Select(e => e.OuterHtml));
    }

    // Built through the AngleSharp DOM (create element, set attributes, set text) rather than
    // string concatenation so the href is DOM-encoded even though readMoreUrl is always a
    // server-built QuestUrl, never user content.
    private static string BuildReadMoreParagraph(IDocument document, string readMoreUrl)
    {
        var paragraph = document.CreateElement("p");
        paragraph.SetAttribute("style", "margin:0;margin-bottom:0;");

        var link = document.CreateElement("a");
        link.SetAttribute("href", readMoreUrl);
        link.SetAttribute("style", ReadMoreLinkStyle);
        link.TextContent = ReadMoreLinkText;

        paragraph.AppendChild(link);
        return paragraph.OuterHtml;
    }
}
