namespace QuestBoard.IntegrationTests.Mobile;

/// <summary>
/// File-content tests over site.js pinning hydrateLocalTimes()'s contract from
/// 86-UI-SPEC.md's "## UI Considerations" E2 rows: one shared listener, the scoped selector,
/// per-element failure isolation, no default-style fallback, and no explicit locale argument.
/// Assertions match on substrings rather than exact line text, so a later reformat of the file
/// does not break this test.
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

        body.Should().Contain("date-time-compact");
        body.Should().Contain("date-compact");
        body.Should().Contain("date-time");
        body.Should().Contain("'date'");
        body.Should().Contain(
            "Intl.DateTimeFormat(undefined",
            because: "no explicit locale argument is passed, so the browser's own resolved locale applies");
    }

    [Fact]
    public void SiteJs_HydrateLocalTimesBody_HasNoAriaLiveOrMinWidth()
    {
        var source = File.ReadAllText(ResolveSiteJsPath());
        var body = ExtractHydrateLocalTimesBody(source);

        body.Should().NotContain("aria-live");
        body.Should().NotContain("min-width");
    }
}
