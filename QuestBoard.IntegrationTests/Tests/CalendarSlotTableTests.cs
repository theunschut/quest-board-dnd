using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the desktop month's slot table in the calendar sheet. The month script reads the slot
/// count from the stylesheet and hides every entry past it behind "+N more", so the table decides
/// whether the month fits the window: zero slots (no fit rule, every entry shows) below the
/// desktop breakpoint, then two to five slots as the window grows taller. A rendered page cannot
/// show which media query a custom property came from, so only a scan of the sheet can hold this.
/// </summary>
public class CalendarSlotTableTests
{
    private static string ReadCalendarSheet()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.EnumerateFiles("*.slnx").Any() && !dir.EnumerateDirectories(".git").Any())
            dir = dir.Parent;

        dir.Should().NotBeNull("the repository root must be found above the test output");
        var css = File.ReadAllText(Path.Combine(dir!.FullName, "QuestBoard.Service", "wwwroot", "css", "calendar.css"));
        return Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
    }

    // Every --cal-slots declaration with the media condition it sits under ("" at the top level of the layer).
    private static List<(string Media, int Slots)> SlotDeclarations(string css)
    {
        var result = new List<(string, int)>();
        var mediaStack = new Stack<string?>();
        var i = 0;
        var segmentStart = 0;
        while (i < css.Length)
        {
            var c = css[i];
            if (c == '{')
            {
                var prelude = css[segmentStart..i].Trim();
                mediaStack.Push(prelude.StartsWith("@media") ? Regex.Replace(prelude["@media".Length..].Trim(), @"\s+", " ") : null);
                segmentStart = i + 1;
            }
            else if (c == '}')
            {
                CollectSlots(css[segmentStart..i], mediaStack, result);
                mediaStack.Pop();
                segmentStart = i + 1;
            }
            else if (c == ';')
            {
                CollectSlots(css[segmentStart..(i + 1)], mediaStack, result);
                segmentStart = i + 1;
            }
            i++;
        }
        return result;
    }

    private static void CollectSlots(string text, Stack<string?> mediaStack, List<(string, int)> result)
    {
        var m = Regex.Match(text, @"--cal-slots\s*:\s*(\d+)\s*;?");
        if (!m.Success) return;
        var media = string.Join(" and ", mediaStack.Reverse().Where(x => x != null));
        result.Add((media, int.Parse(m.Groups[1].Value)));
    }

    [Fact]
    public void SlotTable_IsZeroBelowDesktopAndGrowsWithWindowHeight()
    {
        var declarations = SlotDeclarations(ReadCalendarSheet());

        declarations.Should().BeEquivalentTo(new[]
        {
            ("", 0),
            ("(min-width: 992px)", 2),
            ("(min-width: 992px) and (min-height: 712px)", 3),
            ("(min-width: 992px) and (min-height: 850px)", 4),
            ("(min-width: 992px) and (min-height: 980px)", 5),
        }, options => options.WithStrictOrdering(),
            "below 992px every entry shows, and from 992px the slot count steps up with the window height so the month fits without scrolling");
    }

    [Fact]
    public void SlotTable_DeclaresNoSlotsUnderANarrowOrMaxWidthQuery()
    {
        var declarations = SlotDeclarations(ReadCalendarSheet());

        declarations.Where(d => d.Media.Length > 0)
            .Should().OnlyContain(d => d.Media.Contains("min-width: 992px") && !d.Media.Contains("max-width"),
                "a fit rule must never apply below the desktop breakpoint");
    }
}
