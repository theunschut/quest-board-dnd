using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the shared toast partial: error and warning toasts persist until dismissed and are announced at once,
/// success and info toasts are announced politely and leave after five seconds, every close button has a name, the
/// container carries no inline style, and the moved toast still checks that its link stays on this site. The
/// partial renders only when TempData is set, so a rendered-markup suite reaches a few of these states at best;
/// a scan of the source covers all of them.
/// </summary>
public class UiFoundationToastGuardTests
{
    private const string ToastsFile = "QuestBoard.Service/Views/Shared/_Toasts.cshtml";

    private static string ReadToasts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.EnumerateFiles("*.slnx").Any() && !dir.EnumerateDirectories(".git").Any())
            dir = dir.Parent;

        dir.Should().NotBeNull("the repository root must be found walking up from the test binaries");
        return File.ReadAllText(Path.Combine(dir!.FullName, Path.Combine(ToastsFile.Split('/'))));
    }

    // The partial without its Razor comments, which describe the markup in the same words it uses.
    private static string Markup() => Regex.Replace(ReadToasts(), @"@\*[\s\S]*?\*@", " ");

    // The opening tag of the first toast in the block that renders for this TempData key.
    private static string ToastTag(string tempDataExpression)
    {
        var markup = Markup();
        var start = markup.IndexOf("@if (TempData[" + tempDataExpression + "] != null)", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"the partial must render a toast for TempData[{tempDataExpression}]");

        var tag = Regex.Match(markup[start..], @"<div class=""toast show[^""]*""[^>]*>");
        tag.Success.Should().BeTrue($"the block for TempData[{tempDataExpression}] must open a toast");
        return tag.Value;
    }

    [Fact]
    public void ErrorAndWarningToasts_PersistUntilDismissed()
    {
        foreach (var key in new[] { "\"Error\"", "\"Warning\"" })
        {
            var tag = ToastTag(key);
            tag.Should().Contain("role=\"alert\"");
            tag.Should().Contain("aria-live=\"assertive\"");
            tag.Should().Contain("aria-atomic=\"true\"");
            tag.Should().Contain("data-bs-autohide=\"false\"", $"the {key} toast stays until the viewer dismisses it");
            tag.IndexOf("data-bs-autohide", StringComparison.Ordinal).Should().BeGreaterThan(
                tag.IndexOf("role=\"alert\"", StringComparison.Ordinal), "the autohide switch follows the role");
            tag.Should().NotContain("data-bs-delay");
        }

        foreach (var key in new[] { "TempDataKeys.BoardSwitchTargetName", "TempDataKeys.MovedMessage" })
        {
            var tag = ToastTag(key);
            tag.Should().Contain("role=\"alert\"");
            tag.Should().Contain("data-bs-autohide=\"false\"", "the banners stay up until dismissed");
        }
    }

    [Fact]
    public void SuccessAndInfoToasts_UseStatusRoleAndAutoHide()
    {
        foreach (var (key, delay) in new[] { ("\"Success\"", "5000"), ("\"Info\"", "5000"), ("\"GoldReceived\"", "6000") })
        {
            var tag = ToastTag(key);
            tag.Should().Contain("role=\"status\"");
            tag.Should().Contain("aria-live=\"polite\"");
            tag.Should().Contain("aria-atomic=\"true\"");
            tag.Should().Contain("data-bs-autohide=\"true\"");
            tag.Should().Contain($"data-bs-delay=\"{delay}\"");
            tag.Should().NotContain("role=\"alert\"", "a confirmation is not an interruption");
        }
    }

    [Fact]
    public void EveryToastCloseButton_IsNamed()
    {
        var buttons = Regex.Matches(Markup(), @"<button\b[^>]*\bbtn-close\b[^>]*>");

        buttons.Count.Should().BeGreaterThanOrEqualTo(7, "every toast has a close button");
        buttons.Select(b => b.Value).Should().OnlyContain(
            b => b.Contains("aria-label=\"Close\""), "an icon-only control needs an accessible name");
    }

    [Fact]
    public void ToastContainer_HasNoInlineStyle()
    {
        var markup = Markup();

        markup.Should().NotContain("style=", "layout and stacking belong to a stylesheet class");

        var container = Regex.Match(markup, @"<div class=""toast-container[^""]*""[^>]*>").Value;
        container.Should().Contain("qb-toast-stack", "the class positions the stack below the navigation bar");
        container.Should().NotContain("top-0", "the stack starts below the bar, not at the top of the window");
    }

    [Fact]
    public void MovedToast_KeepsLocalUrlCheck()
    {
        var markup = Markup();

        markup.Should().Contain("Url.IsLocalUrl(movedEntryUrl)",
            "the link in the moved toast is built from stored data and must only ever stay on this site");
        markup.Should().NotContain("Html.Raw", "toast text is user-influenced and must stay encoded");
        markup.Should().NotContain("modern-card.css", "the partial states its reasons itself");
    }
}
