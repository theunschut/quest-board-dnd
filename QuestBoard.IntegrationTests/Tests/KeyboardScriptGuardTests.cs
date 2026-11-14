using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the keyboard and confirmation behaviour of the scripts that no repository test can run, by reading their
/// source: the Markdown editor lets Tab and Shift-Tab leave the field, the crop dialog's buttons and keys each map to
/// a handler with the same step, and the confirm module cancels a submit when the question is answered no. A real
/// browser is still needed to prove the runtime result; these scans keep the wiring from being quietly removed.
/// </summary>
public class KeyboardScriptGuardTests
{
    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.EnumerateFiles("*.slnx").Any() && !dir.EnumerateDirectories(".git").Any())
            dir = dir.Parent;
        dir.Should().NotBeNull("the repository root holds the solution file");
        var path = Path.Combine(dir!.FullName, relativePath);
        File.Exists(path).Should().BeTrue($"{relativePath} is pinned by this guard");
        return File.ReadAllText(path);
    }

    [Fact]
    public void MarkdownEditor_ReleasesTabAndShiftTabSoFocusCanLeaveTheField()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/markdown-editor.js");

        Regex.IsMatch(script, @"['""]Tab['""]\s*:\s*false").Should().BeTrue("Tab must be handed back to the browser");
        Regex.IsMatch(script, @"['""]Shift-Tab['""]\s*:\s*false").Should().BeTrue("Shift-Tab must be handed back to the browser");
        Regex.IsMatch(script, @"['""](?:Tab|Shift-Tab)['""]\s*:(?>\s*)(?!false)").Should().BeFalse(
            "no later binding may take Tab or Shift-Tab back for list indentation");
        script.Should().Contain("setOption('extraKeys'", "the release is applied to the live editor");
    }

    [Fact]
    public void CropDialog_EveryControlButtonHasAHandlerAndEveryHandlerAButton()
    {
        var dialog = ReadRepoFile("QuestBoard.Service/Views/Shared/_ImageCropDialog.cshtml");
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/image-crop.js");

        var buttons = Regex.Matches(dialog, @"data-crop-action=""(\w+)""").Select(m => m.Groups[1].Value).ToHashSet();
        var block = Regex.Match(script, @"const actions = \{(?<body>.*?)\n    \};", RegexOptions.Singleline);
        block.Success.Should().BeTrue("the module keeps one table of crop actions");
        var handlers = Regex.Matches(block.Groups["body"].Value, @"^\s*(\w+)\s*:", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToHashSet();

        buttons.Should().BeEquivalentTo(new[] { "up", "down", "left", "right", "grow", "shrink", "reset" });
        handlers.Should().BeEquivalentTo(buttons, "a button without a handler does nothing and a handler without a button is unreachable");
    }

    [Fact]
    public void CropSelection_ArrowsMoveShiftArrowsResizeAndOtherKeysAreLeftAlone()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/image-crop.js");

        script.Should().MatchRegex(@"const STEP = 8;", "every press moves or resizes by eight pixels");
        script.Should().MatchRegex(@"ARROW_KEYS = new Set\(\['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'\]\)");

        var handler = Regex.Match(script, @"selection\.addEventListener\('keydown'.*?\n    \}\);", RegexOptions.Singleline);
        handler.Success.Should().BeTrue("keys are heard on the focusable selection");
        var body = handler.Value;
        body.Should().Contain("event.ctrlKey || event.altKey || event.metaKey", "browser shortcuts are not swallowed");
        body.Should().Contain("!ARROW_KEYS.has(event.key)", "only the four arrows are handled");
        body.Should().Contain("event.preventDefault()", "a handled arrow does not also scroll the page");
        body.Should().Contain("event.shiftKey").And.Contain("resizeBy(").And.Contain("moveBy(");
        body.IndexOf("return;", StringComparison.Ordinal).Should().BeLessThan(body.IndexOf("event.preventDefault()", StringComparison.Ordinal),
            "the early return for other keys comes before the default is prevented");
    }

    [Fact]
    public void CropResize_KeepsTheAspectRatioAndStaysInsideThePicture()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/image-crop.js");

        var resize = Regex.Match(script, @"function resizeBy\(delta\) \{.*?\n    \}", RegexOptions.Singleline).Value;
        resize.Should().NotBeEmpty();
        resize.Should().Contain("width / ratio", "the height always follows the width through the configured ratio");
        resize.Should().Contain("MIN_SIZE", "the selection never shrinks below its minimum");
        resize.Should().Contain("clamp(", "the box is kept inside the picture");

        var move = Regex.Match(script, @"function moveBy\(dx, dy\) \{.*?\n    \}", RegexOptions.Singleline).Value;
        move.Should().Contain("clamp(").And.Contain("bounds.right - selection.width").And.Contain("bounds.bottom - selection.height");
    }

    [Fact]
    public void ConfirmModule_CancelsTheSubmitWhenTheQuestionIsAnsweredNo()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/confirm-submit.js");

        Regex.IsMatch(script, @"addEventListener\('submit'").Should().BeTrue("the question is asked on submit");
        Regex.IsMatch(script, @"if \(!confirm\(form\.dataset\.confirmMessage\)\) \{\s*event\.preventDefault\(\);\s*\}")
            .Should().BeTrue("answering no must cancel the submit and answering yes must leave it alone");
    }

    [Fact]
    public void ContactNotes_EditOpensOneNoteAndCancelClosesItAndReturnsFocus()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/contact-notes.js");

        script.Should().Contain("editButton.addEventListener('click'");
        script.Should().Contain("collapse(other)", "opening one note closes any other open editor");
        script.Should().Contain("expand(slip)");
        var cancel = Regex.Match(script, @"cancelButton\?\.addEventListener\('click'.*?\}\);", RegexOptions.Singleline).Value;
        cancel.Should().Contain("collapse(slip)").And.Contain("editButton.focus()", "closing the editor returns focus to the button that opened it");
    }
}
