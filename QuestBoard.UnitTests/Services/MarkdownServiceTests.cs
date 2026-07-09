using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;
using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Services;

public class MarkdownServiceTests
{
    private static readonly IMarkdownService Service = new MarkdownService();

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
}
