using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using QuestBoard.Service.Extensions;

namespace QuestBoard.UnitTests.Extensions;

// Pins BuildLocalTime's markup contract directly, with no IHtmlHelper/Razor context needed --
// the exact attribute shape (class, data-style, datetime, title) and the four canonical
// styles' rendered text. Each style gets its own fact (rather than
// one parameterised theory) so the full-precision title assertion is pinned once per style in
// the source itself, not just once per test run.
public class LocalTimeMarkupTests
{
    // A fixed +02:00 offset with no daylight-saving transitions, built rather than resolved by
    // id, so these assertions never depend on host tzdata.
    private static readonly TimeZoneInfo PlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test/PlusTwo", TimeSpan.FromHours(2), "Test +02:00", "+02:00");

    private static readonly DateTime FixedUtcInstant = new(2026, 9, 20, 9, 45, 0, DateTimeKind.Utc);

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static void AssertCommonAttributes(string rendered, string style)
    {
        rendered.Should().Contain("class=\"local-time\"");
        rendered.Should().Contain($"data-style=\"{style}\"");
        rendered.Should().Contain("datetime=\"2026-09-20T09:45:00Z\"");
    }

    [Fact]
    public void BuildLocalTime_DateStyle_RendersTheExactUiSpecMarkup()
    {
        var rendered = Render(HtmlHelperExtensions.BuildLocalTime(FixedUtcInstant, "date", PlusTwo));

        AssertCommonAttributes(rendered, "date");
        rendered.Should().Contain(
            "title=\"2026-09-20 09:45 UTC\"",
            because: "the title is full date-and-time precision even for this date-only style");
        rendered.Should().Contain(">Sep 20, 2026<");
    }

    [Fact]
    public void BuildLocalTime_DateTimeStyle_RendersTheExactUiSpecMarkup()
    {
        var rendered = Render(HtmlHelperExtensions.BuildLocalTime(FixedUtcInstant, "date-time", PlusTwo));

        AssertCommonAttributes(rendered, "date-time");
        rendered.Should().Contain("title=\"2026-09-20 09:45 UTC\"");
        rendered.Should().Contain(">Sep 20, 2026, 11:45 AM<");
    }

    [Fact]
    public void BuildLocalTime_DateCompactStyle_RendersTheExactUiSpecMarkup()
    {
        var rendered = Render(HtmlHelperExtensions.BuildLocalTime(FixedUtcInstant, "date-compact", PlusTwo));

        AssertCommonAttributes(rendered, "date-compact");
        rendered.Should().Contain(
            "title=\"2026-09-20 09:45 UTC\"",
            because: "the title is full date-and-time precision even for this date-only style");
        rendered.Should().Contain(">Sep 20<");
    }

    [Fact]
    public void BuildLocalTime_DateTimeCompactStyle_RendersTheExactUiSpecMarkup()
    {
        var rendered = Render(HtmlHelperExtensions.BuildLocalTime(FixedUtcInstant, "date-time-compact", PlusTwo));

        AssertCommonAttributes(rendered, "date-time-compact");
        rendered.Should().Contain("title=\"2026-09-20 09:45 UTC\"");
        rendered.Should().Contain(">Sep 20, 11:45 AM<");
    }

    public static TheoryData<string, string> StylesAndExpectedText => new()
    {
        { "date", "Sep 20, 2026" },
        { "date-time", "Sep 20, 2026, 11:45 AM" },
        { "date-compact", "Sep 20" },
        { "date-time-compact", "Sep 20, 11:45 AM" },
    };

    [Theory]
    [MemberData(nameof(StylesAndExpectedText))]
    public void BuildLocalTime_UnspecifiedKind_RendersIdenticallyToUtcKind(string style, string expectedText)
    {
        // EF hands back DateTimeKind.Unspecified for a value stored as UTC -- BuildLocalTime must
        // treat it exactly as it would an explicitly-Utc-kinded value.
        var unspecified = DateTime.SpecifyKind(FixedUtcInstant, DateTimeKind.Unspecified);

        var utcRendered = Render(HtmlHelperExtensions.BuildLocalTime(FixedUtcInstant, style, PlusTwo));
        var unspecifiedRendered = Render(HtmlHelperExtensions.BuildLocalTime(unspecified, style, PlusTwo));

        unspecifiedRendered.Should().Be(utcRendered);
        unspecifiedRendered.Should().Contain($">{expectedText}<");
    }

    [Fact]
    public void BuildLocalTime_UnrecognisedStyle_ThrowsArgumentOutOfRangeException()
    {
        var act = () => HtmlHelperExtensions.BuildLocalTime(FixedUtcInstant, "fortnight", PlusTwo);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(StylesAndExpectedText))]
    public void BuildLocalTime_LocalTimeRender_ProducesOneWellFormedElement(string style, string _)
    {
        var rendered = Render(HtmlHelperExtensions.BuildLocalTime(FixedUtcInstant, style, PlusTwo));

        // A single, well-formed <time>...</time> element: exactly one opening tag, and no
        // unescaped quote breaking an attribute boundary into an empty adjacent attribute.
        rendered.Should().Contain("<time");
        (rendered.Length - rendered.Replace("<time", string.Empty).Length).Should().Be("<time".Length,
            because: "there must be exactly one <time opening tag");
        rendered.Should().EndWith("</time>");
        rendered.Should().NotContain("\"\"",
            because: "an unescaped quote inside an attribute value would produce an empty adjacent attribute");
    }
}
