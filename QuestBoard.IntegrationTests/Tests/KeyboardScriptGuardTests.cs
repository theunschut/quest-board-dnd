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
    public void ShopSellModule_ConfirmsOnSubmitAndUpdatesTheTotal()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-item-sell.js");

        Regex.IsMatch(script, @"document\.addEventListener\('submit'").Should().BeTrue("the question is asked on submit, from the document, so a form written into the dialog is covered");
        Regex.IsMatch(script, @"document\.addEventListener\('input'").Should().BeTrue("the total follows the quantity as it is typed");
        script.Should().Contain("form[data-sell-form]", "only forms marked for selling are touched");
        script.Should().Contain("dataset.itemName", "the name is read as plain text from the data attribute");
        script.Should().Contain("[data-sell-total]").And.Contain("textContent", "the total is written as text");
        script.Should().Contain("confirm(");
        Regex.IsMatch(script, @"if \(!confirm\(message\)\) \{\s*event\.preventDefault\(\);\s*\}")
            .Should().BeTrue("answering no must cancel the submit and answering yes must leave it alone");
    }

    [Fact]
    public void ShopPreviewModule_BuildsTheCardFromATemplate()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-item-preview.js");

        script.Should().Contain("itemPreviewModal").And.Contain("show.bs.modal", "the card is built each time the dialog opens");
        script.Should().Contain("template[data-preview-card]").And.Contain("cloneNode(true)", "the card is cloned from the server-rendered template");
        script.Should().Contain("textContent").And.Contain("classList", "values are written as text and the chip by class");
        script.Should().Contain("[data-preview-host]").And.Contain("replaceChildren", "the host is cleared before each card is placed");
        script.Should().Contain("/markdown/preview").And.Contain("RequestVerificationToken", "the excerpt comes from the existing endpoint with the page's token");
        script.Should().Contain("meta[name=\"request-verification-token\"]");
        script.Should().Contain("DOMParser", "the server's answer is read as an inert document, never inserted");
        Regex.IsMatch(script, @"requestId\s*===\s*latestRequestId").Should().BeTrue("only the latest answer is applied");
        Regex.IsMatch(script, @"easyMDE[\s\S]*?\.value\(\)").Should().BeTrue("the editor's value is read when the dialog opens");
        script.Should().NotMatchRegex(@"\.submit\(|requestSubmit|method:\s*['""](?!POST)", "the item form is never submitted");
        Regex.Matches(script, @"fetch\(").Should().HaveCount(1, "the preview request is the only request the module makes");
    }

    [Fact]
    public void ShopPreviewModule_UsesTheStockWordsAndRulesOfTheRealCard()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-item-preview.js");
        var card = ReadRepoFile("QuestBoard.Service/Views/Shop/Index.cshtml");

        foreach (var word in new[] { "Unlimited", "Sold Out" })
        {
            script.Should().Contain($"'{word}'");
            card.Should().Contain(word);
        }
        script.Should().Contain("`${quantity} left`").And.Contain("`${quantity} in stock`");
        card.Should().Contain("@item.Quantity left").And.Contain("@item.Quantity in stock");
        Regex.IsMatch(script, @"quantity === -1[\s\S]*?!\(quantity > 0\)[\s\S]*?quantity <= 5").Should().BeTrue(
            "unlimited is -1, sold out is nothing left, one to five is low, six or more is in stock");
        script.Should().Contain("120", "the excerpt is cut at the real card's length");
    }

    [Fact]
    public void ShopItemModalModule_ClearsTheBodyAndIgnoresStaleResponses()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-item-modal.js");

        script.Should().Contain("itemDetailsModal").And.Contain("itemDetailsBody").And.Contain("show.bs.modal", "the body is filled each time the dialog opens");
        script.Should().Contain("relatedTarget").And.Contain("dataset.itemUrl", "the address comes from the trigger's data attribute");
        script.Should().Contain("replaceChildren()", "the body is emptied before the fetch starts");
        script.Should().Contain("aria-busy", "the dialog says it is loading until the fragment arrives");
        Regex.IsMatch(script, @"requestId\s*!==\s*latestRequestId").Should().BeTrue("an answer that is not for the latest request is ignored");
        script.Should().Contain("DOMParser", "the fragment is read as an inert document, never inserted as a string");
        script.Should().Contain("replaceChildren(...", "only the parsed nodes are moved into the body");
        script.Should().NotMatchRegex(@"innerHTML|outerHTML|insertAdjacentHTML|document\.write", "no markup is built from strings");
        script.Should().NotMatchRegex(@"window\.\w+\s*=", "nothing is exposed globally");
        Regex.Matches(script, @"fetch\(").Should().HaveCount(1, "the details address is the only request the module makes");
    }

    [Fact]
    public void ShopHistoryModule_FlipsTheDisclosureAndRemembersNothing()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-history.js");

        script.Should().Contain("[data-history-toggle]").And.Contain("aria-controls", "the pane is found through the toggle's own aria-controls");
        script.Should().Contain("aria-expanded").And.Contain(".hidden", "the state is the toggle's aria-expanded and the pane's hidden attribute");
        script.Should().NotMatchRegex(@"localStorage|sessionStorage|document\.cookie", "the pane opens collapsed on every load");
        script.Should().NotMatchRegex(@"innerHTML|outerHTML|insertAdjacentHTML|document\.write", "no markup is built from strings");
        script.Should().NotMatchRegex(@"window\.\w+\s*=", "nothing is exposed globally");
    }

    [Fact]
    public void ShopDrawersModule_MirrorsTheDrawerStateOntoItsOpenersAndRemembersNothing()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-drawers.js");

        script.Should().Contain("show.bs.offcanvas").And.Contain("hidden.bs.offcanvas", "the state follows Bootstrap's own drawer events");
        script.Should().Contain("aria-controls").And.Contain("aria-expanded", "the openers are found through aria-controls and carry aria-expanded");
        script.Should().NotMatchRegex(@"localStorage|sessionStorage|document\.cookie", "the drawers open closed on every load");
        script.Should().NotMatchRegex(@"innerHTML|outerHTML|insertAdjacentHTML|document\.write", "no markup is built from strings");
        script.Should().NotMatchRegex(@"window\.\w+\s*=", "nothing is exposed globally");
    }

    [Fact]
    public void ShopMerchantModule_WritesTheQuoteAsTextIntoTheToastAndTheLiveRegion()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-merchant.js");

        script.Should().Contain("[data-merchant-button]").And.Contain("data-merchant-quotes").And.Contain("data-merchant-toast")
            .And.Contain("data-merchant-live", "every part is found through its data attribute");
        script.Should().Contain("cloneNode(true)", "the toast is copied from the server's template");
        Regex.Matches(script, @"\.textContent\s*=").Count.Should().BeGreaterThanOrEqualTo(2, "the quote is written as text, into the toast and the live region");
        script.Should().Contain("getOrCreateInstance(toast, { autohide: true, delay: TOAST_DELAY_MS })").And.Contain("TOAST_DELAY_MS = 6000");
        script.Should().Contain("hidden.bs.toast").And.Contain("toast.remove()", "the toast leaves the page when it is hidden");
        script.Should().Contain("data-merchant-toast-open", "at most one merchant toast exists");
        script.Should().Contain("window.bootstrap", "a page without the bundle still fills the live region");
        script.Should().NotMatchRegex(@"innerHTML|outerHTML|insertAdjacentHTML|document\.write", "no markup is built from strings");
        script.Should().NotMatchRegex(@"window\.\w+\s*=", "nothing is exposed globally");
        script.Should().NotContain("Welcome to my shop").And.NotContain("haunted", "the module holds no quote");
    }

    [Fact]
    public void ShopManagementModule_LoadsTheDenyDialogFromItsTrigger()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-management.js");

        script.Should().Contain("denyModal").And.Contain("show.bs.modal", "the dialog is loaded each time it opens");
        script.Should().Contain("relatedTarget").And.Contain("dataset.denyName").And.Contain("dataset.denyUrl",
            "the name and the address come from the trigger's data attributes");
        script.Should().Contain("textContent", "the item name is written as text");
        script.Should().Contain(".action", "the form's action is set from the server-rendered address");
        script.Should().Contain("[data-bulk-action]").And.Contain("future update", "the bulk buttons show today's notice");
        script.Should().NotMatchRegex(@"innerHTML|outerHTML|insertAdjacentHTML|document\.write", "no markup is built from strings");
        script.Should().NotMatchRegex(@"window\.\w+\s*=", "nothing is exposed globally");
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

    // The recorded answer on whether the item form shows today's two alert messages when a required field is empty.
    // Changing the answer means changing this constant and the module together.
    private static readonly bool ItemFormClientChecksBound = false;

    [Fact]
    public void ShopItemFormModule_BindsOnlyTheItemForm()
    {
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/shop-item-form.js");

        script.Should().Contain("form[data-shop-item-form]", "the module opts in by a data attribute on the form");
        Regex.Matches(script, @"querySelector(?:All)?\(\s*['""]form").Select(m => m.Value).Should().BeEquivalentTo(
            new[] { "querySelectorAll('form" }, "the only form lookup is the attribute selector");
        script.Should().NotMatchRegex(@"document\.forms|forms\[\d", "a form is never picked by its position, so the sign-out form in the navigation bar is out of reach");
        script.Should().NotMatchRegex(@"document\.addEventListener\('submit'", "no submit listener sits on the document");

        if (ItemFormClientChecksBound)
        {
            script.Should().Contain("Please fill in all required fields.");
            script.Should().Contain("Please provide a more detailed description (at least 20 characters).");
            Regex.IsMatch(script, @"addEventListener\('submit',\s*function[^{]*\{[\s\S]*easyMDE").Should().BeTrue(
                "the editor is read inside the handler, because it is built after a module has run");
        }
        else
        {
            script.Should().NotContain("alert(", "the item form shows no client alert");
            script.Should().NotContain("easyMDE", "nothing reads the editor");
        }
    }
}
