using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using QuestBoard.Service.Extensions;

namespace QuestBoard.UnitTests.Extensions;

// Pins the four server-only wall-clock styles: the exact text, the data-style and the invariant
// datetime attribute, and that their visible text follows the current culture (the culture the
// view code they replaced formatted with) while the four client-known styles keep invariant text.
// The culture swap touches process-wide state, so the class sits in its own non-parallel collection.
[CollectionDefinition(nameof(WallClockAreaStylesTests), DisableParallelization = true)]
public class WallClockAreaStylesCollection;

[Collection(nameof(WallClockAreaStylesTests))]
public class WallClockAreaStylesTests
{
    private static readonly DateTime FixedWallClock = new(2026, 9, 4, 19, 0, 0);

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    public static TheoryData<string, string, string> StylesFormatsAndText => new()
    {
        { "time-24h", "HH:mm", "19:00" },
        { "weekday-short", "ddd, MMM d", "Fri, Sep 4" },
        { "weekday-long", "dddd, MMMM d", "Friday, September 4" },
        { "weekday-long-year", "dddd, MMMM d, yyyy", "Friday, September 4, 2026" },
    };

    [Theory]
    [MemberData(nameof(StylesFormatsAndText))]
    public void BuildWallClock_ServerOnlyStyle_RendersExactTextAndInvariantDatetime(
        string style, string format, string expectedText)
    {
        _ = format;
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, style));

            rendered.Should().Contain("class=\"wall-clock\"");
            rendered.Should().Contain($"data-style=\"{style}\"");
            rendered.Should().Contain("datetime=\"2026-09-04T19:00\"");
            rendered.Should().NotContain("title=");
            rendered.Should().Contain($">{expectedText}<");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [MemberData(nameof(StylesFormatsAndText))]
    public void BuildWallClock_ServerOnlyStyle_FollowsTheCurrentCulture_AndKeepsDatetimeInvariant(
        string style, string format, string englishText)
    {
        var dutch = new CultureInfo("nl-NL");
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = dutch;
            var expected = FixedWallClock.ToString(format, dutch);

            var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, style));

            rendered.Should().Contain($">{expected}<");
            rendered.Should().Contain("datetime=\"2026-09-04T19:00\"");
            if (style != "time-24h")
            {
                expected.Should().NotBe(englishText, because: "the swap must reach the helper for this to prove anything");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    public static TheoryData<string, string> ExistingStylesAndInvariantText => new()
    {
        { "date", "Sep 4, 2026" },
        { "date-time", "Sep 4, 2026, 7:00 PM" },
        { "date-compact", "Sep 4" },
        { "date-time-compact", "Sep 4, 7:00 PM" },
    };

    [Theory]
    [MemberData(nameof(ExistingStylesAndInvariantText))]
    public void BuildWallClock_ExistingClientStyles_StayInvariantUnderADutchCulture(string style, string expectedText)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

            var rendered = Render(HtmlHelperExtensions.BuildWallClock(FixedWallClock, style));

            rendered.Should().Contain($">{expectedText}<");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("time-24h")]
    [InlineData("weekday-short")]
    [InlineData("weekday-long")]
    [InlineData("weekday-long-year")]
    public void BuildLocalTime_RejectsTheServerOnlyStyles(string style)
    {
        var act = () => HtmlHelperExtensions.BuildLocalTime(FixedWallClock, style, TimeZoneInfo.Utc);

        act.Should().Throw<ArgumentOutOfRangeException>(
            because: "these styles format a wall-clock value, never a real instant");
    }

    [Fact]
    public void BuildWallClock_UnrecognisedStyle_StillThrows()
    {
        var act = () => HtmlHelperExtensions.BuildWallClock(FixedWallClock, "weekday-medium");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
