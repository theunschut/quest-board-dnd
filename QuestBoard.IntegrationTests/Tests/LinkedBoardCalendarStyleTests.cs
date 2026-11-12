namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the visual rules that say a linked booking is borrowed and a cancelled one is called
/// off: the dashed outline on the month's linked rows and the dashed edge on Quest Details' linked
/// chips, the strike-through on cancelled entries on every surface, the month's detail block that
/// never shows in the row, and Quest Details' detail line that stays hidden at rest and appears on
/// hover or keyboard focus. Nothing in the request pipeline evaluates CSS, so a regression in
/// these rules would otherwise only be caught by eye.
/// </summary>
public class LinkedBoardCalendarStyleTests
{
    private const string CalendarCss = "calendar.css";
    private const string CalendarMobileCss = "calendar.mobile.css";
    private const string QuestsMobileCss = "quests.mobile.css";

    /// <summary>
    /// Resolves a stylesheet under QuestBoard.Service/wwwroot/css by walking up from
    /// AppContext.BaseDirectory until the project folder is found, so the facts run from any
    /// working directory. Fails with a descriptive message naming the attempted path if the file
    /// cannot be resolved.
    /// </summary>
    private static string ReadStylesheet(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "QuestBoard.Service", "wwwroot", "css", fileName);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        var attemptedBase = AppContext.BaseDirectory;
        var attemptedPath = Path.Combine(attemptedBase, "QuestBoard.Service", "wwwroot", "css", fileName);
        throw new FileNotFoundException(
            $"{fileName} not found. Searched upward from '{attemptedBase}'. " +
            $"Last attempted path: '{attemptedPath}'. " +
            $"Ensure QuestBoard.Service/wwwroot/css/{fileName} exists in the repo.",
            attemptedPath);
    }

    /// <summary>
    /// Returns the declaration block of the rule whose selector list holds the given text as one
    /// whole selector. The text counts only when, ignoring whitespace, it is preceded by the start
    /// of the file, "}", "{", "," or the end of a comment, and followed by "{" or ",". So a longer
    /// class that starts with the same text (".linked-entry" inside ".linked-entry-title") and a
    /// selector that merely ends with it (".x .linked-entry", "a.linked-entry") never match. The
    /// block runs from the next "{" to the next "}". Returns an empty string when no such
    /// selector exists, so a deleted rule fails the containing assertion instead of throwing.
    /// </summary>
    private static string ExtractRuleBody(string css, string selector)
    {
        var from = 0;
        while (true)
        {
            var at = css.IndexOf(selector, from, StringComparison.Ordinal);
            if (at < 0)
                return string.Empty;

            var before = at - 1;
            while (before >= 0 && char.IsWhiteSpace(css[before]))
                before--;

            var after = at + selector.Length;
            while (after < css.Length && char.IsWhiteSpace(css[after]))
                after++;

            var startsASelector = before < 0 || css[before] is '}' or '{' or ',' or '/';
            if (startsASelector && after < css.Length && (css[after] == '{' || css[after] == ','))
            {
                var open = css.IndexOf('{', at + selector.Length);
                if (open < 0)
                    return string.Empty;

                var close = css.IndexOf('}', open);
                return close < 0 ? string.Empty : css[open..(close + 1)];
            }

            from = at + selector.Length;
        }
    }

    [Fact]
    public void LinkedEntry_OnTheMonth_HasADashedOutlineToMarkItBorrowed()
    {
        var css = ReadStylesheet(CalendarCss);

        var rule = ExtractRuleBody(css, ".calendar-page .linked-entry");

        rule.Should().Contain("border: 1px dashed",
            because: "a linked row on the month must read as borrowed from another board, not as one of this board's own entries");
    }

    [Fact]
    public void LinkedEntry_OnQuestDetails_HasDashedLeftEdgeToMarkItBorrowed()
    {
        var css = ReadStylesheet(CalendarCss);

        var rule = ExtractRuleBody(css, ".details-page .linked-entry");

        rule.Should().Contain("border-left: 4px dashed",
            because: "a linked chip on Quest Details must read as borrowed from another board, not as one of this board's own entries");
    }

    [Fact]
    public void EntryDetailBlock_OnTheMonth_StaysHiddenAtRest()
    {
        var css = ReadStylesheet(CalendarCss);

        var rule = ExtractRuleBody(css, ".calendar-page .cal-entry-detail");

        rule.Should().Contain("display: none",
            because: "a month row is one line; its board, DM and time wait in a block that never shows in the row itself");
    }

    [Fact]
    public void LinkedEntryDetail_OnQuestDetails_IsHiddenAtRestAndRevealedByHoverAndKeyboardFocus()
    {
        var css = ReadStylesheet(CalendarCss);

        ExtractRuleBody(css, ".details-page .linked-entry-detail").Should().Contain("display: none",
            because: "on Quest Details the board, DM and time line stays out of the chip until a pointer or keyboard user asks for it");
        ExtractRuleBody(css, ".details-page .linked-entry:hover .linked-entry-detail").Should().Contain("display: block",
            because: "hovering a linked chip on Quest Details must show its detail line");
        ExtractRuleBody(css, ".details-page .linked-entry--open:focus-visible .linked-entry-detail").Should().Contain("display: block",
            because: "a keyboard user focusing an openable linked chip must get the same detail line a hover shows");
    }

    [Theory]
    [InlineData(CalendarCss, ".calendar-page .linked-entry--cancelled .cal-entry__title")]
    [InlineData(CalendarCss, ".calendar-page .calendar-event.cancelled .cal-entry__title")]
    [InlineData(CalendarCss, ".details-page .linked-entry--cancelled")]
    [InlineData(CalendarCss, ".details-page .linked-entry--cancelled .linked-entry-title")]
    [InlineData(CalendarMobileCss, ".agenda-linked-entry--cancelled .agenda-slip__title")]
    [InlineData(QuestsMobileCss, ".also-that-day--cancelled")]
    public void CancelledEntry_OnEverySurface_IsStruckThrough(string fileName, string selector)
    {
        var css = ReadStylesheet(fileName);

        var rule = ExtractRuleBody(css, selector);

        rule.Should().Contain("text-decoration: line-through",
            because: $"{selector} in {fileName} must strike the cancelled entry through so it reads as called off");
    }

    [Fact]
    public void CancelledLinkedEntry_OnThePhone_TakesTheQuieterPaperInTheLayeredSheet()
    {
        var css = ReadStylesheet(CalendarMobileCss);

        var rule = ExtractRuleBody(css, ".agenda-linked-entry--cancelled");

        rule.Should().Contain("background:",
            because: "a called-off linked slip sits on quieter paper as well as carrying the struck-through title");
        css.TrimStart().Should().Contain("@layer pages {",
            because: "the phone sheet belongs to the pages layer");
    }

    [Fact]
    public void AlsoThatDayLink_OnThePhone_TakesTheTouchTargetFloor()
    {
        var css = ReadStylesheet(QuestsMobileCss);

        var rule = ExtractRuleBody(css, ".also-that-day .row-nav-link");

        rule.Should().Contain("min-height: var(--qb-hit-min)",
            because: "the title link in an also-that-day line is something a thumb taps, so it keeps the 44px floor");
    }

    [Fact]
    public void LinkedLegend_AndNote_OnThePhone_UseTheFullTextToneAndNoSecondGlass()
    {
        var css = ReadStylesheet(CalendarMobileCss);

        ExtractRuleBody(css, ".agenda-linked-note").Should().Contain("color: var(--qb-fg);",
            because: "the muted tone measures under the contrast bar on the page panel");
        ExtractRuleBody(css, ".agenda-legend").Should().Contain("background-image: none",
            because: "a second sheet of glass over the panel would lighten the legend under its text");
    }
}
