---
phase: 66-quest-description-editor-rendering-proof-of-concept
reviewed: 2026-07-09T00:00:00Z
depth: standard
files_reviewed: 29
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IMarkdownService.cs
  - QuestBoard.Domain/QuestBoard.Domain.csproj
  - QuestBoard.Domain/Services/MarkdownService.cs
  - QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Emails/QuestFinalizedMarkdownRenderTests.cs
  - QuestBoard.IntegrationTests/Helpers/AntiForgeryHelper.cs
  - QuestBoard.Service/Components/Emails/QuestFinalized.razor
  - QuestBoard.Service/Controllers/MarkdownController.cs
  - QuestBoard.Service/Extensions/HtmlHelperExtensions.cs
  - QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs
  - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Create.cshtml
  - QuestBoard.Service/Views/Quest/CreateFollowUp.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Edit.cshtml
  - QuestBoard.Service/Views/Quest/Index.cshtml
  - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
  - QuestBoard.Service/wwwroot/css/markdown-content.css
  - QuestBoard.Service/wwwroot/css/markdown-editor.css
  - QuestBoard.Service/wwwroot/js/markdown-editor.js
  - QuestBoard.Service/wwwroot/js/site.js
  - QuestBoard.UnitTests/Services/MarkdownServiceTests.cs
findings:
  critical: 1
  warning: 5
  info: 1
  total: 7
status: issues_found
---

# Phase 66: Code Review Report

**Reviewed:** 2026-07-09T00:00:00Z
**Depth:** standard
**Files Reviewed:** 29
**Status:** issues_found

## Summary

Reviewed the Markdown editor/rendering proof-of-concept: `MarkdownService` (Markdig + HtmlSanitizer), the `/markdown/preview` controller, the shared `_MarkdownEditor` partial + EasyMDE client wiring, the sanitized-HTML view helper, and its four call sites (Quest Details, Manage, Index card teaser, and the QuestFinalized email). The sanitizer/XSS design itself is solid — dual-target sanitizer profiles, an explicit allowlist with a documented rationale for excluding the generic-attribute extension, a nesting-depth-guard fallback, and unit tests that exercise real XSS payloads (`javascript:` hrefs, `onerror`, raw `<script>`) all pass.

Ran `dotnet test` for both the unit and integration Markdown test suites — all 30 tests pass. I also empirically verified two of the findings below (AngleSharp text-node handling, and Markdig's multi-block HTML output) against the actual package versions pinned in this repo rather than relying on inference.

The one Critical finding is a real, unrenderable-HTML defect in the Quest Finalized email template that the existing test suite doesn't exercise (it only feeds single-paragraph markdown). The Warnings are edge cases and a couple of UI/robustness gaps that don't compromise the sanitizer's security properties but do affect correctness or UX.

## Critical Issues

### CR-01: QuestFinalized email wraps multi-block Markdown HTML in a single `<p>`, producing invalid nested-block HTML that silently drops the description's styling

**File:** `QuestBoard.Service/Components/Emails/QuestFinalized.razor:43`
**Issue:** The rendered, sanitized Markdown HTML is injected directly inside a `<p>` element:
```razor
<p style="font-size:15px; ... font-style:italic; ...">@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</p>
```
`MarkdownService.RenderToHtml` (and the Email sanitizer's allowlist, which includes `p`, `h1`-`h6`, `ul`/`ol`/`li`, `blockquote`, `table`, `dl`) can legitimately emit multiple sibling block-level elements for any Quest Description that has more than one paragraph, or that uses a heading/list/quote/table — all features the EasyMDE toolbar and the Markdig pipeline explicitly support. `<p>` cannot legally contain other block-level elements; per the HTML5 parsing algorithm, the browser (and most email-rendering engines) implicitly closes the outer `<p>` as soon as it hits the first nested block tag.

I verified this directly against the pipeline configured in `MarkdownService.cs` (Markdig 1.3.2, same extensions):
```
Input:  "Paragraph one\n\nParagraph two\n\n## A heading\n\n- item 1\n- item 2"
Output: <p>Paragraph one</p>
        <p>Paragraph two</p>
        <h2>A heading</h2>
        <ul><li>item 1</li><li>item 2</li></ul>
```
Wrapped in the outer `<p style="font-style:italic;...">`, the browser parses this as an **empty**, immediately-closed `<p style="...">` followed by the paragraphs/heading/list as unstyled top-level siblings — the entire italic/Georgia/color/text-shadow styling block is dropped for any Description beyond a single short paragraph. This is the primary rendering surface for the Quest Description in the confirmation email (the app's core "DM posts quest, players get notified" loop), and it is untested: `QuestFinalizedMarkdownRenderTests` only feeds `"**bold description** with an ![logo](...) image"`, a single paragraph, so the nested-`<p>` case never triggers in CI.

**Fix:** Use a block-safe container instead of `<p>`:
```razor
<div style="font-size:15px;font-family:Georgia,serif;color:#1a0f08;line-height:1.6;font-style:italic;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0;">@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</div>
```
and add a regression test in `QuestFinalizedMarkdownRenderTests` that feeds a two-paragraph (or heading/list) Description and asserts the styled wrapper tag is not itself closed/emptied (e.g. assert the italic font-style is still applied to rendered paragraph content, or simply assert the output contains no `<p ...></p><p>` empty-wrapper artifact).

## Warnings

### WR-01: `MarkdownController.Preview` throws an unhandled `NullReferenceException` on an empty/malformed request body

**File:** `QuestBoard.Service/Controllers/MarkdownController.cs:20-26`
**Issue:** `MarkdownController` is a plain `Controller`, not `[ApiController]`, so invalid/empty `[FromBody]` model state is not auto-rejected with a 400. If a POST to `/markdown/preview` has an empty body, a non-JSON body, or a literal JSON `null`, ASP.NET Core's body-model-binder leaves `request` as `null` (or records deserialize as null on top-level JSON `null`) without the framework short-circuiting the action. The action then dereferences `request.Markdown` unconditionally:
```csharp
public IActionResult Preview([FromBody] PreviewRequest request)
{
    var html = markdownService.RenderToHtml(request.Markdown, MarkdownRenderTarget.Web);
    ...
}
```
This throws a `NullReferenceException`, which the client-side `previewRender` in `markdown-editor.js` will surface as the exception-handler page's HTML body being `fetch().then(r => r.text())`'d directly into `previewElement.innerHTML` (via `.catch` only covering network-level failures, not non-2xx responses with a body). None of the existing integration tests (`MarkdownControllerIntegrationTests`) send an empty/malformed body — all three POST-based tests use the literal `"**bold**"` payload — so this path is untested.
**Fix:**
```csharp
public IActionResult Preview([FromBody] PreviewRequest? request)
{
    var html = markdownService.RenderToHtml(request?.Markdown, MarkdownRenderTarget.Web);
    return Content(html, "text/html");
}
```
and add an integration test that POSTs an empty body / `Content-Type: application/json` with no payload and asserts a non-500 response.

### WR-02: Removing `.quest-description-mobile` in favor of `.markdown-content` drops the Quest Description's parchment text color on the mobile Details page

**File:** `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:98-102`
**Issue:** Before this phase, the Description was rendered inside a `<div class="quest-description-mobile">`, which (`quests.mobile.css:103-110`) explicitly sets `color: #F4E4BC` (parchment gold) with a dark text-shadow, specifically because it's rendered inside `.quest-header-card-mobile` — a semi-transparent glass card (`rgba(255,255,255,0.15)` background) composited over a dark background image. This phase replaces that wrapper with `@Html.Markdown(Model.Quest.Description)`, which instead wraps the output in `<div class="markdown-content">` (`HtmlHelperExtensions.cs:23`). `markdown-content.css` only sets a color for `h1`-`h6`; body `<p>` text has no color rule, so it falls through to the page's default Bootstrap body color (near-black, no explicit override in `site.css`/`mobile.css`). Unlike the Manage/Manage.Mobile Description blocks (which are wrapped in `.card.modern-card`, and `site.css`'s `.modern-card p, li, span, small { color: #F4E4BC !important; ... }` rule still applies to nested `<p>` at any depth), `.quest-header-card-mobile` has no such rule, so the rendered Description paragraph text loses its intended contrast/legibility against the dark glass card.
**Fix:** Either scope a color rule for this context, e.g. in `quests.mobile.css`:
```css
.quest-header-card-mobile .markdown-content {
    color: #F4E4BC;
    text-shadow: 1px 1px 3px rgba(0, 0, 0, 0.9), -1px -1px 1px rgba(0, 0, 0, 0.9);
}
```
or keep the original `.quest-description-mobile` div as an additional wrapper around `@Html.Markdown(...)`.

### WR-03: `ExtractPlainText` silently returns empty string instead of the fallback-encoded text when `RenderToHtml`'s pathological-nesting guard trips

**File:** `QuestBoard.Domain/Services/MarkdownService.cs:127-129`
**Issue:** `ExtractPlainText` joins `document.Body!.Children` (an `IElement`-only collection) after parsing the sanitized HTML. When `RenderToHtml` hits Markdig's nesting-depth guard, it falls back to `System.Net.WebUtility.HtmlEncode(markdown)` — plain encoded text with **no wrapping element at all**. I verified empirically against AngleSharp 0.17.1 (the pinned version) that parsing bare text into a document produces zero `Body.Children` (element children), even though `Body.TextContent`/`Body.ChildNodes` correctly contain the text as a `Text` node:
```
Body.Children.Length = 0
Body.ChildNodes.Length = 1   (a Text node)
```
Since `ExtractPlainText` only reads `.Children`, this text is dropped entirely — the method returns `""` for a Description that trips the depth guard, instead of the encoded fallback text `RenderToHtml` itself would return. This is used by `Index.cshtml`'s quest-board card teaser and poster-selection heuristic (`SelectPosterByContent`), so a pathological Description would render an unexpectedly blank card teaser and under-count `totalCharacters` (using only the title).
**Fix:** Fall back to `document.Body.TextContent` when there are no element children:
```csharp
var parts = document.Body!.Children
    .Select(el => el.TextContent.Trim())
    .Where(t => !string.IsNullOrEmpty(t))
    .ToList();

if (parts.Count == 0)
{
    return WhitespaceRun.Replace(document.Body.TextContent, " ").Trim();
}
```

### WR-04: `MarkdownEditorViewModel.Required` only renders a visual asterisk — it is never applied to the `<textarea>` as an HTML5 `required` attribute

**File:** `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml:9-20`
**Issue:** Every call site (`Create.cshtml`, `CreateFollowUp.cshtml`, `Edit.cshtml`, and their `.Mobile` counterparts) passes `Required = true` for the Description field, and the partial uses `Model.Required` to conditionally render the red `*` label decoration:
```razor
@if (Model.Required)
{
    <span class="text-danger">*</span>
}
```
but the `<textarea>` itself never gets a `required` attribute:
```razor
<textarea name="@Model.FieldName" id="@elementId" class="form-control markdown-editor-textarea" rows="4" placeholder="@Model.Placeholder">@Model.Value</textarea>
```
Every other required field in the same forms (`ChallengeRating`, `TotalPlayerCount`, `ProposedDates[i]`) does set `required` on its `<input>`. This means the Description field gives no client-side validation feedback for an empty submission (relying entirely on server-side validation, if present, for a field the UI visually marks as mandatory) — an inconsistency with the rest of the form and a `Required` flag on the view model that only does half its job.
**Fix:**
```razor
<textarea name="@Model.FieldName" id="@elementId" class="form-control markdown-editor-textarea" rows="4" placeholder="@Model.Placeholder" @(Model.Required ? "required" : "")>@Model.Value</textarea>
```

### WR-05: `markdown-editor.js`'s `previewRender` has no request sequencing — a slower in-flight preview response can overwrite a newer one

**File:** `QuestBoard.Service/wwwroot/js/markdown-editor.js:20-43`
**Issue:** Every keystroke (while the Preview pane is open) fires a new `fetch('/markdown/preview', ...)`. There is no `AbortController`, request id, or "is this still the latest request" guard:
```javascript
previewRender: function (plainText, previewElement) {
    fetch('/markdown/preview', {...})
        .then(function (response) { return response.text(); })
        .then(function (html) { previewElement.innerHTML = html; })
        .catch(...);
    return previewElement.innerHTML || '<p>Loading preview...</p>';
}
```
If two requests are in flight and the earlier one resolves after the later one (plausible under normal network jitter, and highly plausible if the `MarkdownController.Preview` NRE in WR-01 causes one request in the sequence to error/retry), `previewElement.innerHTML` is set to the stale (out-of-order) response last, silently reverting the visible preview to older content while the editor's text has already moved on.
**Fix:** Track a monotonically increasing request counter (or use `AbortController`) and ignore/drop responses that aren't for the most recent request:
```javascript
let latestRequestId = 0;
previewRender: function (plainText, previewElement) {
    const requestId = ++latestRequestId;
    fetch('/markdown/preview', {...})
        .then(r => r.text())
        .then(html => {
            if (requestId === latestRequestId) {
                previewElement.innerHTML = html;
            }
        })
        .catch(...);
    return previewElement.innerHTML || '<p>Loading preview...</p>';
}
```

## Info

### IN-01: `AntiForgeryHelper.ExtractHeaderAntiForgeryTokenAsync` is a pure pass-through wrapper with no behavioral difference from `ExtractAntiForgeryTokenAsync`

**File:** `QuestBoard.IntegrationTests/Helpers/AntiForgeryHelper.cs:60-64`
**Issue:** Despite the detailed XML doc explaining the header-vs-form-field semantics, the method body is identical to calling `ExtractAntiForgeryTokenAsync` directly and simply renaming the tuple fields:
```csharp
public static async Task<(string HeaderToken, string CookieValue)> ExtractHeaderAntiForgeryTokenAsync(HttpResponseMessage response)
{
    var (token, cookieValue) = await ExtractAntiForgeryTokenAsync(response);
    return (token, cookieValue);
}
```
This is harmless (test-only code) but is needless indirection — a reader has to open both methods to confirm there's no hidden transport-specific logic.
**Fix:** Either inline the call at the two call sites in `MarkdownControllerIntegrationTests.cs`, or leave as-is purely for the documentation value, but consider a one-line comment at the call site instead of a full wrapper method if no future divergence is expected.

---

_Reviewed: 2026-07-09T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
