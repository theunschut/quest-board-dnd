using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the shared component layer: the card surface sets the text colour once and its descendants inherit it, and
/// only a card that is itself a link or a button lifts on hover. A rendered-markup suite cannot see which rules a
/// stylesheet holds or which media query wraps them, so only a scan of the source can hold the line.
/// </summary>
public class UiFoundationComponentGuardTests
{
    // The repository root is the directory holding the solution file, found before any path is joined.
    private static DirectoryInfo FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateDirectories(".git").Any())
                return dir;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Repository root not found: no directory containing a solution file or '.git' was " +
            $"found walking up from '{AppContext.BaseDirectory}'.");
    }

    private const string CssRoot = "QuestBoard.Service/wwwroot/css";

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, Path.Combine(relativePath.Split('/'))));

    private static string StripComments(string css) => Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");

    /// <summary>One rule that holds declarations: its selector list, its body, and the at-rule preludes around it.</summary>
    private sealed record CssRule(string Selector, string Body, IReadOnlyList<string> Enclosing);

    // Every rule that holds declarations directly, at any depth of @layer and @media nesting. A block that holds
    // other blocks (an at-rule) is a container, not a rule, and is reported only through Enclosing.
    private static List<CssRule> ParseRules(string css)
    {
        var rules = new List<CssRule>();
        var preludes = new List<string>();
        var bodies = new List<System.Text.StringBuilder>();
        var holdsBlocks = new List<bool>();
        var preludeStart = 0;

        for (var i = 0; i < css.Length; i++)
        {
            switch (css[i])
            {
                case '{':
                    if (holdsBlocks.Count > 0)
                        holdsBlocks[^1] = true;
                    preludes.Add(Regex.Replace(css[preludeStart..i].Trim(), @"\s+", " "));
                    bodies.Add(new System.Text.StringBuilder());
                    holdsBlocks.Add(false);
                    preludeStart = i + 1;
                    break;
                case '}':
                    if (preludes.Count > 0)
                    {
                        var last = preludes.Count - 1;
                        var tail = css[preludeStart..i];
                        bodies[last].Append(tail);
                        if (!holdsBlocks[last])
                            rules.Add(new CssRule(preludes[last], bodies[last].ToString(), preludes.Take(last).ToList()));

                        preludes.RemoveAt(last);
                        bodies.RemoveAt(last);
                        holdsBlocks.RemoveAt(last);
                    }

                    preludeStart = i + 1;
                    break;
            }
        }

        return rules;
    }

    private static List<CssRule> ComponentRules() =>
        ParseRules(StripComments(ReadRepoFile(CssRoot + "/components.css")));

    // A selector list as its parts.
    private static IEnumerable<string> SelectorParts(CssRule rule) =>
        rule.Selector.Split(',').Select(s => s.Trim());

    private static readonly Regex ColourProperty = new(@"(?<![\w-])color\s*:", RegexOptions.Compiled);

    private static bool Declares(CssRule rule, string property, string value) =>
        Regex.IsMatch(rule.Body, @"(?<![\w-])" + Regex.Escape(property) + @"\s*:\s*" + Regex.Escape(value) + @"\s*(;|$)");

    [Fact]
    public void ComponentsCss_CardSurfaceSetsColourForDescendants()
    {
        var rules = ComponentRules();

        var surface = rules.Where(r => SelectorParts(r).Contains(".modern-card")).ToList();
        surface.Should().NotBeEmpty("components.css must define the card surface");
        surface.Any(r => Declares(r, "color", "var(--qb-fg)")).Should().BeTrue(
            "the card surface sets the text colour once, so everything inside it inherits it");

        // A rule that reaches inside the card and sets its own text colour is a second source of truth for what the
        // card's text looks like. Variables (the per-surface Bootstrap mapping) are the way to differ, not color.
        var descendantColour = rules
            .Where(r => SelectorParts(r).Any(s => Regex.IsMatch(s, @"^\.modern-card[\s>+~]")))
            .Where(r => ColourProperty.IsMatch(r.Body))
            .Select(r => r.Selector)
            .ToList();
        descendantColour.Should().BeEmpty(
            "text inside a card inherits the card colour; a descendant rule that names a colour fights the surface");
    }

    [Fact]
    public void ComponentsCss_HoverLiftOnlyOnInteractiveCard()
    {
        var rules = ComponentRules();

        // Nothing but the interactive card moves on hover: no hover rule for any other card class sets a transform.
        var otherLift = rules
            .Where(r => SelectorParts(r).Any(s =>
                Regex.IsMatch(s, @"^\.modern-card(-header|-body|-footer)?(?![\w-])(?!.*--interactive).*:hover")))
            .Where(r => Regex.IsMatch(r.Body, @"(?<![\w-])transform\s*:"))
            .Select(r => r.Selector)
            .ToList();
        otherLift.Should().BeEmpty("a static card must not move when the pointer passes over it");

        var lift = rules
            .Where(r => SelectorParts(r).Contains(".modern-card--interactive:hover"))
            .Where(r => Regex.IsMatch(r.Body, @"(?<![\w-])transform\s*:"))
            .ToList();
        lift.Should().NotBeEmpty("the interactive card lifts on hover");
        lift.Should().OnlyContain(
            r => r.Enclosing.Any(p => Regex.IsMatch(p, @"^@media\s*\(\s*prefers-reduced-motion\s*:\s*no-preference\s*\)$")),
            "the lift is movement, so it exists only inside the no-preference media query");
    }

    [Fact]
    public void RuleParser_SeesRulesInsideNestedBlocks()
    {
        // Without a known sample the guards above would pass vacuously if the parser lost the nesting.
        var sample = StripComments(
            "@layer components { .a { color: red; } @media (x) { .b:hover { transform: none; } } }");

        var rules = ParseRules(sample);

        rules.Should().HaveCount(2);
        rules[0].Selector.Should().Be(".a");
        rules[1].Selector.Should().Be(".b:hover");
        rules[1].Enclosing.Should().Equal("@layer components", "@media (x)");
    }
}
