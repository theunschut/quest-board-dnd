namespace QuestBoard.IntegrationTests.Mobile;

/// <summary>
/// File-content tests over site.js pinning the timestamp hydration contract: one shared
/// listener, the scoped selector, per-element failure isolation, no default-style fallback, and
/// no explicit locale argument. Assertions match on substrings rather than exact line text, so a
/// later reformat of the file does not break this test.
/// </summary>
public class SiteJsHydrationTests
{
    /// <summary>
    /// Resolves the path to site.js by walking up from AppContext.BaseDirectory until a
    /// "QuestBoard.Service" child directory is found, following MobileCssTests' own resolver
    /// shape. Fails with a descriptive message naming the attempted path if it cannot be found.
    /// </summary>
    private static string ResolveSiteJsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "QuestBoard.Service", "wwwroot", "js", "site.js");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        var attemptedBase = AppContext.BaseDirectory;
        var attemptedPath = Path.Combine(attemptedBase, "QuestBoard.Service", "wwwroot", "js", "site.js");
        throw new FileNotFoundException(
            $"site.js not found. Searched upward from '{attemptedBase}'. " +
            $"Last attempted path: '{attemptedPath}'. " +
            "Ensure QuestBoard.Service/wwwroot/js/site.js exists in the repo.",
            attemptedPath);
    }

    // Isolates the hydrateLocalTimes function body -- from its declaration to the closing brace
    // that returns the brace depth to zero -- so assertions about what is (and is not) inside
    // the function do not accidentally match unrelated code elsewhere in the file.
    private static string ExtractHydrateLocalTimesBody(string source)
    {
        var declarationIndex = source.IndexOf("function hydrateLocalTimes()", StringComparison.Ordinal);
        declarationIndex.Should().BeGreaterThanOrEqualTo(0, "hydrateLocalTimes must be declared in site.js");

        var braceStart = source.IndexOf('{', declarationIndex);
        braceStart.Should().BeGreaterThan(declarationIndex);

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(declarationIndex, i - declarationIndex + 1);
                }
            }
        }

        throw new InvalidOperationException("Could not find the closing brace of hydrateLocalTimes().");
    }

    // Isolates the one shared style table both hydration passes read from, using the same
    // brace-depth walk as the function extractors so a nested options object cannot end the
    // match early.
    private static string ExtractTimestampFormatsTable(string source)
    {
        var declarationIndex = source.IndexOf("const TIMESTAMP_FORMATS", StringComparison.Ordinal);
        declarationIndex.Should().BeGreaterThanOrEqualTo(0, "the shared style table must be declared in site.js");

        var braceStart = source.IndexOf('{', declarationIndex);
        braceStart.Should().BeGreaterThan(declarationIndex);

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(declarationIndex, i - declarationIndex + 1);
                }
            }
        }

        throw new InvalidOperationException("Could not find the closing brace of TIMESTAMP_FORMATS.");
    }

    // Mirrors ExtractHydrateLocalTimesBody's brace-depth walk for the sibling wall-clock pass,
    // so assertions about the conversion-proof UTC anchoring do not accidentally match unrelated
    // code elsewhere in the file.
    private static string ExtractHydrateWallClockTimesBody(string source)
    {
        var declarationIndex = source.IndexOf("function hydrateWallClockTimes()", StringComparison.Ordinal);
        declarationIndex.Should().BeGreaterThanOrEqualTo(0, "hydrateWallClockTimes must be declared in site.js");

        var braceStart = source.IndexOf('{', declarationIndex);
        braceStart.Should().BeGreaterThan(declarationIndex);

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(declarationIndex, i - declarationIndex + 1);
                }
            }
        }

        throw new InvalidOperationException("Could not find the closing brace of hydrateWallClockTimes().");
    }

    [Fact]
    public void SiteJs_HasExactlyOneDOMContentLoadedListener()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());

        var occurrences = 0;
        var index = 0;
        const string needle = "addEventListener('DOMContentLoaded'";
        while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            occurrences++;
            index += needle.Length;
        }

        occurrences.Should().Be(1, because: "hydrateLocalTimes must run inside the file's existing single listener, not register a second one");
    }

    [Fact]
    public void SiteJs_ContainsTheScopedSelector()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());

        source.Should().Contain(
            "time.local-time[datetime]",
            because: "the hydration pass must be scoped to elements this phase produces, not the bare time[datetime] selector");
    }

    [Fact]
    public void SiteJs_DeclaresAndInvokesHydrateLocalTimes()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());

        source.Should().Contain("function hydrateLocalTimes()");
        source.Should().Contain("hydrateLocalTimes();");
    }

    [Fact]
    public void SiteJs_HydrateLocalTimesBody_WrapsPerElementFormattingInTryCatch()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());
        var body = ExtractHydrateLocalTimesBody(source);

        body.Should().Contain("try");
        body.Should().Contain("catch");
    }

    [Fact]
    public void SiteJs_HydrateLocalTimesBody_CoversAllFourCanonicalStylesAndUsesNoExplicitLocale()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());
        var body = ExtractHydrateLocalTimesBody(source);

        // The four canonical styles live in one shared table rather than a copy per pass, so that
        // a new style can never reach one hydration pass but not its sibling. Assert against the
        // shared table, and assert that this pass actually reads from it.
        var sharedTable = ExtractTimestampFormatsTable(source);
        sharedTable.Should().Contain("date-time-compact");
        sharedTable.Should().Contain("date-compact");
        sharedTable.Should().Contain("date-time");
        sharedTable.Should().Contain("'date'");

        body.Should().Contain(
            "TIMESTAMP_FORMATS[style]",
            because: "the pass must resolve its options from the one shared table, not a local copy");
        body.Should().Contain(
            "Intl.DateTimeFormat(undefined",
            because: "no explicit locale argument is passed, so the browser's own resolved locale applies");
    }

    [Fact]
    public void SiteJs_DefinesExactlyOneStyleTable_SharedByBothHydrationPasses()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());

        // A second table literal would reintroduce the drift this shared constant exists to
        // prevent: the two passes could then disagree on what a style name means.
        (source.Split("'date-time-compact':").Length - 1).Should().Be(
            1,
            because: "exactly one client-side style table may exist, shared by both passes");

        ExtractHydrateLocalTimesBody(source).Should().Contain("TIMESTAMP_FORMATS[style]");
        ExtractHydrateWallClockTimesBody(source).Should().Contain("TIMESTAMP_FORMATS[style]");
    }

    [Fact]
    public void SiteJs_HydrateLocalTimesBody_HasNoAriaLiveOrMinWidth()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());
        var body = ExtractHydrateLocalTimesBody(source);

        body.Should().NotContain("aria-live");
        body.Should().NotContain("min-width");
    }

    [Fact]
    public void SiteJs_DeclaresAndInvokesHydrateWallClockTimes()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());

        source.Should().Contain("function hydrateWallClockTimes()");
        source.Should().Contain("hydrateWallClockTimes();");
    }

    [Fact]
    public void SiteJs_ContainsTheWallClockScopedSelector()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());

        source.Should().Contain(
            "time.wall-clock[datetime]",
            because: "the wall-clock hydration pass must never touch time.local-time elements, and vice versa");
    }

    [Fact]
    public void SiteJs_HydrateWallClockTimesBody_AnchorsAndFormatsInUtc()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());
        var body = ExtractHydrateWallClockTimesBody(source);

        // The whole safety argument for a wall-clock render: the parsed components are
        // re-anchored through Date.UTC(...) and formatted with timeZone: 'UTC', so the viewer's
        // own zone is structurally unable to enter the calculation.
        body.Should().Contain("Date.UTC(");
        body.Should().Contain("timeZone: 'UTC'");
    }

    [Fact]
    public void SiteJs_HydrateWallClockTimesBody_NeverBuildsADateFromBareLocalComponents()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());
        var body = ExtractHydrateWallClockTimesBody(source);

        // Every "new Date(" in this function must be immediately followed by "Date.UTC(" -- a
        // bare "new Date(y, m, d, h, min)" component constructor builds the value in the
        // viewer's own local zone and can shift it across a DST gap, which is exactly the
        // regression this whole helper exists to make structurally impossible.
        body.Should().NotMatchRegex(
            @"new Date\((?!Date\.UTC\()",
            because: "a wall-clock value must only ever be constructed via new Date(Date.UTC(...)), never the local-zone component constructor");
    }

    [Fact]
    public void SiteJs_HydrateWallClockTimesBody_WrapsPerElementFormattingInTryCatch()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());
        var body = ExtractHydrateWallClockTimesBody(source);

        body.Should().Contain("try");
        body.Should().Contain("catch");
    }
}
