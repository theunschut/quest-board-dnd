using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using QuestBoard.Service.Extensions;

namespace QuestBoard.UnitTests.Extensions;

// Pins BuildWallClock's markup contract directly, with no IHtmlHelper/Razor context needed --
// the exact attribute shape (class, data-style, datetime, NO title) and the four canonical
// styles' invariant-culture rendered text. Unlike LocalTimeMarkupTests' BuildLocalTime facts,
// there is no TimeZoneInfo parameter anywhere here: a wall-clock value has no zone to convert
// from, and this whole test class exists to pin that it stays that way.
public class WallClockMarkupTests
{
    private static readonly DateTime FixedWallClock = new(2026, 9, 4, 18, 0, 0);

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public void BuildWallClock_EmitsWallClockClass_NotLocalTime()
    {
        var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, "date"));

        rendered.Should().Contain("class=\"wall-clock\"");
        rendered.Should().NotContain("class=\"local-time\"");
    }

    [Fact]
    public void BuildWallClock_DatetimeAttribute_HasNoZAndNoOffset()
    {
        var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, "date-time"));

        rendered.Should().Contain("datetime=\"2026-09-04T18:00\"");
        rendered.Should().NotContain("Z\"");
        rendered.Should().NotMatchRegex(
            "datetime=\"[^\"]*[+-]\\d{2}:\\d{2}\"",
            because: "a wall-clock value has no UTC instant, so its datetime attribute must carry no offset either");
    }

    [Fact]
    public void BuildWallClock_NeverEmitsATitleAttribute()
    {
        var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, "date-time"));

        rendered.Should().NotContain(
            "title=",
            because: "a UTC tooltip would be a lie -- a wall-clock value has no UTC instant to show");
    }

    public static TheoryData<string, string> StylesAndExpectedText => new()
    {
        { "date", "Sep 4, 2026" },
        { "date-time", "Sep 4, 2026, 6:00 PM" },
        { "date-compact", "Sep 4" },
        { "date-time-compact", "Sep 4, 6:00 PM" },
    };

    [Theory]
    [MemberData(nameof(StylesAndExpectedText))]
    public void BuildWallClock_RendersInvariantCultureFormat_ForEachStyle(string style, string expectedText)
    {
        var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, style));

        rendered.Should().Contain($"data-style=\"{style}\"");
        rendered.Should().Contain($">{expectedText}<");
    }

    [Fact]
    public void BuildWallClock_UnrecognisedStyle_ThrowsArgumentOutOfRangeException()
    {
        var act = () => HtmlHelperExtensions.BuildWallClock(FixedWallClock, "fortnight");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(StylesAndExpectedText))]
    public void BuildWallClock_ProducesOneWellFormedElement(string style, string _)
    {
        var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, style));

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
