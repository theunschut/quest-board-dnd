using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;
using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Services;

public class MarkdownServiceTests
{
    private static readonly IMarkdownService Service = new MarkdownService();
    private const string TestReadMoreUrl = "https://qb.test/Quest/42";

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Hello <img src=x onerror=alert(1)>")]
    [InlineData("[Click me](javascript:alert(document.cookie))")]
    [InlineData("<javascript:alert(document.cookie)>")]
    [InlineData("[Click me](http://example.com){onmouseover=\"alert(1)\"}")]
    public void RenderToHtml_XssPayload_ProducesNoLiveScriptOrHandler(string markdown)
    {
        var html = Service.RenderToHtml(markdown);

        // No live <script> element (escaped &lt;script is fine).
        html.Should().NotContainEquivalentOf("<script");

        // No on*-event-handler attribute inside any tag. The regex cannot cross a '>', so inert
        // "{onmouseover=...}" text sitting outside any tag does not match.
        Regex.IsMatch(html, @"<[^>]*\son\w+\s*=", RegexOptions.IgnoreCase).Should().BeFalse();

        // No javascript: scheme inside an href/src attribute. Inert link TEXT containing
        // "javascript:" (e.g. an autolink whose href was stripped but whose visible text survived)
        // does not match this attribute-scoped pattern.
        Regex.IsMatch(html, @"(?i)\b(href|src)\s*=\s*[""']?\s*javascript:").Should().BeFalse();
    }

    [Fact]
    public void RenderToHtml_SingleNewlineNoBlankLine_StaysOneParagraph()
    {
        var html = Service.RenderToHtml("Line one\nLine two");

        Regex.Matches(html, "<p>").Count.Should().Be(1);
        html.Should().NotContain("<br");
    }

    [Fact]
    public void RenderToHtml_BlankLineBetweenLines_ProducesTwoParagraphs()
    {
        var html = Service.RenderToHtml("Paragraph one\n\nParagraph two");

        Regex.Matches(html, "<p>").Count.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RenderToHtml_NullOrWhitespace_ReturnsEmpty(string? markdown)
    {
        var html = Service.RenderToHtml(markdown);

        html.Should().BeEmpty();
    }

    [Fact]
    public void RenderToHtml_PipeTable_PreservesCellText()
    {
        var html = Service.RenderToHtml("| Item |\n| --- |\n| Gold |");

        html.Should().Contain("<td");
        html.Should().Contain("Gold");
    }

    [Fact]
    public void RenderToHtml_DefinitionList_PreservesTermAndDefinition()
    {
        var html = Service.RenderToHtml("Sword\n:   A blade");

        html.Should().Contain("<dt");
        html.Should().Contain("<dd");
        html.Should().Contain("Sword");
        html.Should().Contain("A blade");
    }

    [Fact]
    public void RenderToHtml_Footnote_PreservesFootnoteId()
    {
        var html = Service.RenderToHtml("Text[^1]\n\n[^1]: note body");

        html.Should().Contain("id=\"fn");
    }

    [Fact]
    public void RenderToHtml_TaskList_RendersDisabledCheckbox()
    {
        var html = Service.RenderToHtml("- [x] done");

        html.Should().Contain("type=\"checkbox\"");
        html.Should().Contain("disabled");
    }

    [Fact]
    public void RenderToHtml_Strikethrough_RendersDelTag()
    {
        var html = Service.RenderToHtml("~~gone~~");

        html.Should().Contain("<del");
    }

    [Fact]
    public void RenderToHtml_WebTarget_KeepsImage()
    {
        var html = Service.RenderToHtml("![cat](http://h/c.png)", MarkdownRenderTarget.Web);

        html.Should().Contain("<img");
        html.Should().Contain("http://h/c.png");
    }

    [Fact]
    public void RenderToHtml_EmailTarget_StripsImage()
    {
        var html = Service.RenderToHtml("![cat](http://h/c.png)", MarkdownRenderTarget.Email);

        html.Should().NotContain("<img");
    }

    [Fact]
    public void RenderToHtml_NoImageContent_WebAndEmailIdentical()
    {
        var webHtml = Service.RenderToHtml("**bold** text", MarkdownRenderTarget.Web);
        var emailHtml = Service.RenderToHtml("**bold** text", MarkdownRenderTarget.Email);

        webHtml.Should().Be(emailHtml);
    }

    [Fact]
    public void RenderToHtml_DeeplyNestedBlockquotes_FallsBackToEncodedTextInsteadOfThrowing()
    {
        var markdown = new string('>', 200) + " deeply nested";

        var html = Service.RenderToHtml(markdown);

        html.Should().Contain("deeply nested");
        html.Should().NotContain("<blockquote");
    }

    [Fact]
    public void RenderToHtml_DeeplyNestedEmphasis_FallsBackToEncodedTextInsteadOfThrowing()
    {
        var markdown = new string('*', 300) + "deeply nested" + new string('*', 300);

        var html = Service.RenderToHtml(markdown);

        html.Should().Contain("deeply nested");
        html.Should().NotContain("<em");
        html.Should().NotContain("<strong");
    }

    [Fact]
    public void ExtractPlainText_MultiBlockInput_PreservesWordBoundariesAcrossBlocks()
    {
        var plainText = Service.ExtractPlainText("# Heading\n\nSome text.\n\n- item one\n- item two");

        plainText.Should().Contain("Heading Some text.");
        plainText.Should().Contain("item one item two");
        plainText.Should().NotContain("HeadingSome");
        plainText.Should().NotContain("item oneitem two");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractPlainText_NullOrWhitespace_ReturnsEmpty(string? markdown)
    {
        var plainText = Service.ExtractPlainText(markdown);

        plainText.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPlainText_BoldParagraph_StripsMarkdownSyntaxAndHtmlTags()
    {
        var plainText = Service.ExtractPlainText("**Bold** text");

        plainText.Should().Be("Bold text");
        plainText.Should().NotContain("<");
        plainText.Should().NotContain("*");
    }

    [Fact]
    public void ExtractPlainText_DeeplyNestedInput_ReturnsFallbackEncodedTextInsteadOfEmpty()
    {
        // When RenderToHtml's nesting-depth guard trips, it returns bare HTML-encoded text with no
        // wrapping element, which parses to zero Body.Children -- ExtractPlainText must still
        // surface that text via Body.TextContent rather than silently returning "".
        var markdown = new string('>', 200) + " deeply nested";

        var plainText = Service.ExtractPlainText(markdown);

        plainText.Should().Contain("deeply nested");
        plainText.Should().NotBeEmpty();
    }

    [Fact]
    public void RenderEmailHtml_Heading1And2_UseLargeStyle()
    {
        var h1Html = Service.RenderEmailHtml("# Big", TestReadMoreUrl);
        var h2Html = Service.RenderEmailHtml("## Big", TestReadMoreUrl);

        h1Html.Should().Contain("font-size:20px");
        h1Html.Should().Contain("font-style:normal");
        h2Html.Should().Contain("font-size:20px");
        h2Html.Should().Contain("font-style:normal");
    }

    [Fact]
    public void RenderEmailHtml_Heading3And4_UseMediumStyle()
    {
        var h3Html = Service.RenderEmailHtml("### Med", TestReadMoreUrl);
        var h4Html = Service.RenderEmailHtml("#### Med", TestReadMoreUrl);

        h3Html.Should().Contain("font-size:18px");
        h4Html.Should().Contain("font-size:18px");
    }

    [Fact]
    public void RenderEmailHtml_Heading5And6_UseSmallStyle()
    {
        var h5Html = Service.RenderEmailHtml("##### Small", TestReadMoreUrl);
        var h6Html = Service.RenderEmailHtml("###### Small", TestReadMoreUrl);

        h5Html.Should().Contain("font-size:16px");
        h6Html.Should().Contain("font-size:16px");
    }

    [Theory]
    [InlineData("# H1")]
    [InlineData("## H2")]
    [InlineData("### H3")]
    [InlineData("#### H4")]
    [InlineData("##### H5")]
    [InlineData("###### H6")]
    public void RenderEmailHtml_AllHeadingLevels_AreBoldAndNeverItalic(string markdown)
    {
        var html = Service.RenderEmailHtml(markdown, TestReadMoreUrl);

        html.Should().Contain("font-weight:700");
        html.Should().Contain("font-style:normal");
        Regex.IsMatch(html, @"<h[1-6][^>]*font-style:italic").Should().BeFalse();
    }

    [Fact]
    public void RenderEmailHtml_Paragraph_UsesBodyStyle()
    {
        var html = Service.RenderEmailHtml("body", TestReadMoreUrl);

        html.Should().Contain("font-style:italic");
        html.Should().Contain("font-size:15px");
        html.Should().Contain("margin:0 0 12px 0");
    }

    [Fact]
    public void RenderEmailHtml_UnorderedList_StylesListAndBulletItems()
    {
        var html = Service.RenderEmailHtml("- a\n- b", TestReadMoreUrl);

        html.Should().Contain("padding:0 0 0 20px");
        Regex.Matches(html, "list-style-type:disc").Count.Should().Be(2);
        Regex.Matches(html, "display:list-item").Count.Should().Be(2);
    }

    [Fact]
    public void RenderEmailHtml_OrderedList_UsesDecimalListItemStyle()
    {
        var html = Service.RenderEmailHtml("1. a\n2. b", TestReadMoreUrl);

        Regex.Matches(html, "list-style-type:decimal").Count.Should().Be(2);
    }

    [Fact]
    public void RenderEmailHtml_ListItems_HaveMsoBulletFallbackComment()
    {
        var html = Service.RenderEmailHtml("- a\n- b\n- c", TestReadMoreUrl);

        Regex.Matches(html, Regex.Escape("<!--[if mso]>&#8226;&nbsp;<![endif]-->")).Count.Should().Be(3);
    }

    [Fact]
    public void RenderEmailHtml_Blockquote_UsesGoldLeftBorder()
    {
        var html = Service.RenderEmailHtml("> quote", TestReadMoreUrl);

        html.Should().Contain("border-left:3px solid #FFD700");
    }

    [Fact]
    public void RenderEmailHtml_Link_UsesBrownColorNotGold()
    {
        var html = Service.RenderEmailHtml("[t](https://x)", TestReadMoreUrl);

        html.Should().Contain("color:#8B4513");
        html.Should().NotContain("color:#FFD700");
    }

    [Fact]
    public void RenderEmailHtml_StrongAndEmphasis_HaveExplicitStyles()
    {
        var html = Service.RenderEmailHtml("**bold** and *em*", TestReadMoreUrl);

        html.Should().Contain("<strong style=\"font-weight:700;\">");
        html.Should().Contain("<em style=\"font-style:italic;\">");
    }

    [Fact]
    public void RenderEmailHtml_ThematicBreak_UsesGoldTopBorder()
    {
        var html = Service.RenderEmailHtml("text\n\n---\n\nmore", TestReadMoreUrl);

        html.Should().Contain("border-top:1px solid #FFD700");
    }

    [Fact]
    public void RenderEmailHtml_OutOfScopeTags_ReceiveNoInjectedStyle()
    {
        // Table and definition-list are sandwiched between two plain paragraphs so neither is the
        // first/last kept block -- keeps this test isolated from the margin-top/margin-bottom
        // corrections (Task 2), which legitimately apply to whichever block ends up first/last
        // regardless of its tag.
        var markdown = "Intro paragraph.\n\n| Item |\n| --- |\n| Gold |\n\n`code` and ~~gone~~ and a footnote[^1].\n\nSword\n:   A blade\n\n[^1]: note body\n\nOutro paragraph.";

        var html = Service.RenderEmailHtml(markdown, TestReadMoreUrl);

        Regex.IsMatch(html, @"<table[^>]*\sstyle=").Should().BeFalse();
        Regex.IsMatch(html, @"<code[^>]*\sstyle=").Should().BeFalse();
        Regex.IsMatch(html, @"<del[^>]*\sstyle=").Should().BeFalse();
        Regex.IsMatch(html, @"<sup[^>]*\sstyle=").Should().BeFalse();
        Regex.IsMatch(html, @"<dl[^>]*\sstyle=").Should().BeFalse();
    }

    [Fact]
    public void RenderToHtml_EmailTarget_StillEmitsNoStyleAttribute()
    {
        var html = Service.RenderToHtml("# H", MarkdownRenderTarget.Email);

        html.Should().NotContain("style=");
    }

    [Fact]
    public void RenderEmailHtml_Image_StripsImgTag()
    {
        var html = Service.RenderEmailHtml("![cat](http://h/c.png)", TestReadMoreUrl);

        html.Should().NotContain("<img");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RenderEmailHtml_NullOrWhitespace_ReturnsEmpty(string? markdown)
    {
        var html = Service.RenderEmailHtml(markdown, TestReadMoreUrl);

        html.Should().BeEmpty();
    }
}
