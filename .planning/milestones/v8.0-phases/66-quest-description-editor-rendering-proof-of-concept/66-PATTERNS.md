# Phase 66: Quest Description Editor & Rendering (Proof-of-Concept) - Pattern Map

**Mapped:** 2026-07-09
**Files analyzed:** 22
**Analogs found:** 20 / 22

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `QuestBoard.Service/Controllers/MarkdownController.cs` | controller | request-response | `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` | role-match (small focused controller, primary-ctor DI) — antiforgery specifics from `QuestController.cs` |
| `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` | utility | transform | `QuestBoard.Service/Extensions/ControllerExtensions.cs` | exact (same "internal static class, one extension method per concern" convention) |
| `QuestBoard.Domain/Interfaces/IMarkdownService.cs` (MODIFIED — add `ExtractPlainText`) | service (interface) | transform | itself (existing file) | exact — extend existing interface |
| `QuestBoard.Domain/Services/MarkdownService.cs` (MODIFIED — add `ExtractPlainText`) | service | transform | itself (existing file) | exact — extend existing implementation |
| `QuestBoard.Domain/QuestBoard.Domain.csproj` (MODIFIED — add AngleSharp ref) | config | — | itself (existing file) | exact — one more `<PackageReference>` line |
| `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` | component (partial view) | request-response | `QuestBoard.Service/Views/Quest/_QuestSection.cshtml` (structure only) + the 6 existing `<textarea asp-for="...Description">` blocks | role-match (new partial; markup convention borrowed, not a partial-of-a-partial) |
| `QuestBoard.Service/wwwroot/js/markdown-editor.js` | utility (client init module) | event-driven | `QuestBoard.Service/wwwroot/js/image-crop.js` | exact (same "shared init module, `DOMContentLoaded` + `querySelectorAll`, safe no-op if markup absent" convention) |
| `QuestBoard.Service/wwwroot/css/markdown-editor.css` | config (stylesheet) | — | `QuestBoard.Service/wwwroot/css/image-crop.css` | exact (same "narrowly-scoped override for a 3rd-party widget, not a full skin" convention) |
| `QuestBoard.Service/Views/Quest/Create.cshtml` (MODIFIED) | component (view) | CRUD (write) | itself (existing file, lines 28-32) | exact — swap `<textarea>` block for `_MarkdownEditor` partial |
| `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml` (MODIFIED) | component (view) | CRUD (write) | `Create.cshtml` (desktop sibling) | exact |
| `QuestBoard.Service/Views/Quest/Edit.cshtml` (MODIFIED) | component (view) | CRUD (write) | itself (existing file, lines 30-34) | exact |
| `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` (MODIFIED) | component (view) | CRUD (write) | `Edit.cshtml` (desktop sibling) | exact |
| `QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml` (MODIFIED) | component (view) | CRUD (write) | itself (existing file, lines 34-38) + `Create.cshtml`/`Edit.cshtml` for the `_QuestFormScripts` inclusion it currently lacks | exact for textarea swap; role-match for script-inclusion (new to this file) |
| `QuestBoard.Service/Views/Quest/CreateFollowUp.Mobile.cshtml` (MODIFIED) | component (view) | CRUD (write) | `CreateFollowUp.cshtml` (desktop sibling) | exact |
| `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml` (MODIFIED, generalized) | component (shared JS partial) | event-driven | itself (existing file) | exact — extend existing partial with EasyMDE init call |
| `QuestBoard.Service/Views/Quest/Details.cshtml` (MODIFIED) | component (view) | CRUD (read) | itself (existing file, line 31) | exact — swap `pre-wrap <p>` for `Html.Markdown()` |
| `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (MODIFIED) | component (view) | CRUD (read) | itself (existing file, lines 97-103) | exact — CAUTION: shared CSS class with Rewards, see Shared Patterns |
| `QuestBoard.Service/Views/Quest/Manage.cshtml` (MODIFIED — new section) | component (view) | CRUD (read) | `QuestBoard.Service/Views/Quest/_QuestSection.cshtml` (collapse markup structure only, NOT included as a partial — see Anti-Pattern note) | role-match |
| `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` (MODIFIED — new section) | component (view) | CRUD (read) | `Manage.cshtml` (desktop sibling) + `_QuestSection.cshtml` structure | role-match |
| `QuestBoard.Service/Views/Quest/Index.cshtml` (MODIFIED) | component (view) | CRUD (read) | itself (existing file, lines 32-53 poster calc, line 119 description div) | exact — swap to `Html.MarkdownPlainText()`, feed same value into `SelectPosterByContent` |
| `QuestBoard.Service/Components/Emails/QuestFinalized.razor` (MODIFIED) | component (Blazor email) | request-response (render-only) | itself (existing file, line 41) | exact — first `@inject` in `Components/Emails/`, no other email component to copy the `@inject` idiom from |
| `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` (MODIFIED — add tests) | test | transform | itself (existing file) | exact — extend existing test class |
| `QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs` (NEW) | test | request-response | `QuestBoard.IntegrationTests/Controllers/QuestReminderTests.cs` (antiforgery-required-POST shape) | role-match — **no exact analog for header-based JSON antiforgery**, see No Analog Found |

## Pattern Assignments

### `QuestBoard.Service/Controllers/MarkdownController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` (structure) + `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (antiforgery attributes)

**Imports pattern** (`EmailPreviewController.cs` lines 1-6):
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
```

**Controller shape — primary-constructor DI, class-level `[Authorize]`** (`EmailPreviewController.cs` lines 8-11):
```csharp
namespace QuestBoard.Service.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
public class EmailPreviewController(IEmailRenderService emailRenderService, IOptions<EmailSettings> emailOptions) : Controller
```
For `MarkdownController`, use plain `[Authorize]` (any authenticated group member — not DM-only; confirmed by RESEARCH.md's ASVS section) and inject `IMarkdownService` instead.

**Antiforgery + HTTP-verb attribute pattern** (`QuestController.cs` lines 598-601, header-based DELETE analog — same mechanism applies to a header-based POST):
```csharp
[HttpDelete]
[ValidateAntiForgeryToken]
[Authorize]
public async Task<IActionResult> RevokeSignup(int id)
```
For the new action: `[HttpPost]`, `[Route("markdown/preview")]`, `[ValidateAntiForgeryToken]`, returning `Content(html, "text/html")` (mirrors `EmailPreviewController`'s `Content(html, "text/html")` return convention verbatim — see lines 37, 55, 71 etc.).

**No new `Program.cs` antiforgery config needed** — the app's default `AntiforgeryOptions.HeaderName` (`"RequestVerificationToken"`) is already what the existing `fetch()` calls send (see Shared Patterns → Antiforgery).

---

### `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` (utility, transform)

**Analog:** `QuestBoard.Service/Extensions/ControllerExtensions.cs`

**Full file pattern to mirror** (lines 1-9):
```csharp
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Extensions;

/// <summary>
/// Reusable MVC-layer boilerplate helpers for controller actions: the
/// TempData-message-then-redirect pattern and the ModelState-invalid guard pattern.
/// </summary>
internal static class ControllerExtensions
```
Mirror this exactly: `internal static class HtmlHelperExtensions` in the same namespace, one extension method per concern (`Markdown()`, `MarkdownPlainText()`), each with an XML-doc summary explaining *why*, not a phase reference (per CLAUDE.md's comment rule).

**Resolving a Domain singleton from a Razor view's `IHtmlHelper`** — no existing analog does this (this is genuinely new), but the DI-resolution idiom (`RequestServices.GetRequiredService<T>()`) is the standard ASP.NET Core pattern already used at the request-scope in this app (e.g. controller constructor injection throughout `QuestController.cs`). RESEARCH.md's Pattern 2 code example is the concrete target:
```csharp
internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
{
    var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
    var rendered = service.RenderToHtml(markdown, MarkdownRenderTarget.Web);
    return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
}
```

---

### `QuestBoard.Domain/Interfaces/IMarkdownService.cs` + `QuestBoard.Domain/Services/MarkdownService.cs` (service, transform)

**Analog:** itself — extend the existing Phase 65 file, do not create a new service.

**Existing interface shape to extend** (`IMarkdownService.cs`, full file, 20 lines):
```csharp
namespace QuestBoard.Domain.Interfaces;

public enum MarkdownRenderTarget { Web, Email }

public interface IMarkdownService
{
    string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web);
}
```
Add `string ExtractPlainText(string? markdown);` following the same XML-doc-summary convention already on `RenderToHtml`.

**Existing implementation shape to extend** (`MarkdownService.cs` lines 80-104, the method this new one sits beside):
```csharp
public string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web)
{
    if (string.IsNullOrWhiteSpace(markdown))
    {
        return string.Empty;
    }

    string rawHtml;
    try
    {
        rawHtml = Markdown.ToHtml(markdown, Pipeline);
    }
    catch (ArgumentException)
    {
        return System.Net.WebUtility.HtmlEncode(markdown);
    }

    return target == MarkdownRenderTarget.Email
        ? EmailSanitizer.Sanitize(rawHtml)
        : WebSanitizer.Sanitize(rawHtml);
}
```
`ExtractPlainText` should call `RenderToHtml(markdown, MarkdownRenderTarget.Web)` internally (never re-parse Markdown directly), per D-06's "post-process the already-rendered HTML" decision and RESEARCH.md Pattern 3's exact `AngleSharp.Html.Parser.HtmlParser` implementation (already vetted — copy it directly rather than re-deriving).

**Error-handling convention already established:** catch `ArgumentException` from Markdig's nesting-depth guard, fail closed to HTML-encoded raw text — reuse this same fail-safe philosophy if `ExtractPlainText` needs its own guard (in practice it won't need one, since it calls the already-guarded `RenderToHtml`).

---

### `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` (component/partial, request-response)

**Analog:** the 6 existing `<textarea asp-for="...Description">` blocks (structure), `_QuestSection.cshtml` (collapse/card idiom reference only, not applicable here directly)

**Existing field markup to replace, verbatim structure to preserve** (`Create.cshtml` lines 28-32):
```html
<div class="mb-3">
    <label asp-for="Description" class="form-label">Description <span class="text-danger">*</span></label>
    <textarea asp-for="Description" class="form-control" rows="4" placeholder="Describe the quest, what players can expect, any special requirements..."></textarea>
    <span asp-validation-for="Description" class="text-danger"></span>
</div>
```
The new partial must accept the same field-name flexibility that already varies across the 3 forms (`Description` on Create/CreateFollowUp vs. `Quest.Description` on Edit) — likely via a small ViewModel or by having callers pass the `asp-for` expression in. Per RESEARCH.md's Pitfall, the textarea gets a shared class (`markdown-editor-textarea`) for JS targeting since `asp-for` generates different `id` values per form (`Description` vs `Quest_Description`).

**Tooltip hint markup (D-08/D-09)** — no existing Bootstrap-tooltip precedent anywhere in this codebase (confirmed zero `data-bs-toggle="tooltip"` matches). Closest structural precedent for an icon-next-to-label is the existing `<span class="text-danger">*</span>` required-marker convention shown above — follow that placement idiom (inline, right after the label text) but with `<i class="fas fa-info-circle ..." data-bs-toggle="tooltip" title="Supports Markdown formatting. Leave a blank line between paragraphs."></i>`.

---

### `QuestBoard.Service/wwwroot/js/markdown-editor.js` (utility, event-driven)

**Analog:** `QuestBoard.Service/wwwroot/js/image-crop.js`

**File-level comment convention** (lines 1-3):
```javascript
// Shared client-side crop pipeline for every photo-upload form (character, contact, DM profile).
// Loaded per-view via a plain <script> include (matching site.js's no-module, no-bundler
// convention) and initialized per-view by calling initImageCrop({...}) with that view's element IDs.
```

**Safe-no-op-if-markup-absent init pattern** (lines 80-95, the shape to mirror):
```javascript
function initImageCrop(config) {
    config = config || {};
    const fileInputId = config.fileInputId;
    ...
    const fileInput = fileInputId ? document.getElementById(fileInputId) : null;
    if (!fileInput) {
        // Nothing to wire on this page -- safe no-op so the same script can be included
        // defensively without checking which view is currently rendered.
        return;
    }
    ...
}
```
`markdown-editor.js` differs in that it must initialize *N* editors (`querySelectorAll('.markdown-editor-textarea')`, per RESEARCH.md Pattern 1 — not `getElementById`, since 3 forms generate 3 different ids), each independently — same defensive "no-op if nothing present" spirit, applied per-element in a `forEach` rather than a single early-return.

**CDN+SRI script include precedent** (`Views/Characters/Create.cshtml` lines 234-237, the exact pattern to mirror for EasyMDE, per-view not in `_Layout.cshtml`):
```html
<script src="https://cdn.jsdelivr.net/npm/cropperjs@2.1.1/dist/cropper.min.js"
        integrity="sha384-pDSc1bjpfKbaO0DjoZ/uKmzKaARM4658N3xT1ARgy5AKyR6O2UrecaAO8fdv39y9"
        crossorigin="anonymous"></script>
<script src="~/js/image-crop.js" asp-append-version="true"></script>
<script>
    initImageCrop({ fileInputId: 'profilePictureInput', hiddenCroppedInputName: 'CroppedPictureFile', aspectRatio: 1 });
</script>
```

---

### `QuestBoard.Service/wwwroot/css/markdown-editor.css` (config/stylesheet)

**Analog:** `QuestBoard.Service/wwwroot/css/image-crop.css` (full file, 23 lines)

**Scoping-comment convention to mirror** (lines 1-3):
```css
/* Crop modal stage layout. Cropper.js v2's Web Components carry their own internal styling,
   so this file only handles the stage wrapper's layout and a touch-drag fix -- it does not
   restyle any Cropper.js internal component. */
```
Apply the identical "we are not restyling the third-party widget, only fixing one narrow thing" framing for `markdown-editor.css`'s comment header, then scope strictly to touch-target sizing:
```css
.editor-toolbar button {
    min-width: 44px;
    min-height: 44px;
}
```
(Exact selector/values per RESEARCH.md Pitfall 4 — do not add color/border/icon changes, per D-03.)

**App's existing global 44px rule, for context on why this override is additionally needed** (`mobile.css` lines 1-9):
```css
/* Touch target sizing */
.btn,
a.nav-link,
input:not([type=checkbox]):not([type=radio]),
select,
textarea,
.form-control,
.form-select {
    min-height: 44px;
}
```
EasyMDE's toolbar `<button>` elements carry none of these classes, so this existing rule does not cascade to them — confirms the new override is additive, not a duplicate.

---

### `QuestBoard.Service/Views/Quest/Details.cshtml` (view, CRUD read)

**Analog:** itself, line 31

**Current code to replace:**
```html
<p class="lead" style="white-space: pre-wrap;">@Model.Quest?.Description</p>
```
**Replace with** `@Html.Markdown(Model.Quest?.Description)` wrapped in whatever container class replaces `p.lead` styling needs (the helper itself emits `<div class="markdown-content">...</div>` per the `HtmlHelperExtensions` pattern above) — remove the inline `white-space: pre-wrap` entirely per D-06/State-of-the-Art table (safe here since this container is Description-only, unlike the mobile page).

---

### `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (view, CRUD read) — CAUTION

**Analog:** itself, lines 97-103

**Current code — shared class with Rewards, do not blanket-edit the class:**
```html
@if (!string.IsNullOrWhiteSpace(Model.Quest?.Description))
{
    <hr class="quest-card-divider mt-2 mb-2" />
    <div class="quest-description-mobile">
        @Model.Quest.Description
    </div>
}
@if (!string.IsNullOrWhiteSpace(Model.Quest?.Rewards))
{
    <hr class="quest-card-divider mt-2 mb-2" />
    <div class="quest-description-mobile">
        <i class="fas fa-coins me-1"></i>@Model.Quest.Rewards
    </div>
}
```
Both blocks share `quest-description-mobile`, which carries `white-space: pre-wrap` (`quests.mobile.css` lines 102-110). Per RESEARCH.md Pitfall 2: give Description's `<div>` a **second, more specific class** (e.g. `quest-description-mobile markdown-content`) and override `white-space: normal` only on the combined selector — do NOT touch `.quest-description-mobile` alone, since Rewards (unmigrated until Phase 67) still needs `pre-wrap`.

---

### `QuestBoard.Service/Views/Quest/Manage.cshtml` / `Manage.Mobile.cshtml` (view, CRUD read, new section)

**Analog:** `_QuestSection.cshtml` (structure reference only — see Anti-Pattern)

**Collapse markup structure to replicate** (`_QuestSection.cshtml` lines 8-19, adapt for a single field not a quest list):
```html
<div class="row mb-4">
    <div class="col-12">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h4 class="mb-0">
                    <button class="btn btn-link text-decoration-none text-warning fw-bold p-0" type="button" data-bs-toggle="collapse" data-bs-target="#@Model.CollapseTargetId" aria-expanded="@Model.IsExpanded.ToString().ToLower()" aria-controls="@Model.CollapseTargetId">
                        <i class="@Model.Icon me-2"></i>@Model.Title
                        <i class="fas fa-chevron-down ms-2"></i>
                    </button>
                </h4>
            </div>
            <div id="@Model.CollapseTargetId" class="@expandedClass">
                <div class="card-body modern-card-body">
                    ...
                </div>
            </div>
        </div>
    </div>
</div>
```
`expandedClass = Model.IsExpanded ? "collapse show" : "collapse"` — for Manage's Description section, hardcode to plain `"collapse"` (collapsed by default per D-02, no per-request expand state needed). **Do NOT `@await Html.PartialAsync("_QuestSection", ...)`** — its model (`QuestSectionViewModel`) expects `IEnumerable<Quest>` and cannot render a single field (confirmed Anti-Pattern in RESEARCH.md).

**Where to insert it in `Manage.cshtml`:** the existing card at lines 22-34 (`ViewData["Title"]` header + CR badge) is the containing `col-md-8` card — the new collapsible Description section is a sibling block within `card-body modern-card-body` (line 35 onward), not a nested card-within-card.

---

### `QuestBoard.Service/Views/Quest/Index.cshtml` (view, CRUD read, board card)

**Analog:** itself, lines 32-53 (poster selection) and line 119 (description div)

**Current code to replace** (line 119):
```html
<div class="quest-description">@quest.Description</div>
```
Replace with `@Html.MarkdownPlainText(quest.Description)`.

**Current poster-selection code that must consume the same plain-text length** (lines 32-53):
```csharp
string SelectPosterByContent(Quest quest, string[] availablePosters)
{
    var title = quest.Title ?? "";
    var description = quest.Description ?? "";
    var totalCharacters = title.Length + description.Length;

    switch (totalCharacters)
    {
        case <= 130:
            return "Poster4.png"; // Very short content
        case <= 250:
            return "Poster5.png"; // Short content
        case >= 500:
            return "Poster3.png"; // Very long content
        default:
        {
            var posterIndex = Math.Abs(quest.Id.GetHashCode()) % availablePosters.Length;
            return availablePosters[posterIndex];
        }
    }
}
```
Per D-07: change `description.Length` to measure the same stripped plain text `Html.MarkdownPlainText` produces — e.g. call the Domain service's `ExtractPlainText(quest.Description)` (via `IMarkdownService` injected into this view, or a `Html.MarkdownPlainText(...)`-adjacent overload that returns a raw string for length purposes) rather than raw `quest.Description.Length`. Do not change the 130/250/500 thresholds themselves (deferred).

---

### `QuestBoard.Service/Components/Emails/QuestFinalized.razor` (Blazor email component, request-response render-only)

**Analog:** itself, line 41 — no other file in `Components/Emails/` has an `@inject` to copy from; this is genuinely first-of-its-kind here.

**Current code to replace** (line 41):
```razor
<p style="font-size:15px;font-family:Georgia,serif;color:#1a0f08;line-height:1.6;font-style:italic;text-shadow:2px 2px 4px rgba(255,255,255,0.9),1px 1px 6px rgba(0,0,0,0.5);margin:0;">@QuestDescription</p>
```
**Replace with:**
```razor
@using QuestBoard.Domain.Interfaces
@inject IMarkdownService MarkdownService
...
<p style="...">@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))</p>
```
**Why `@inject` resolves correctly despite being email-job-scoped, not HTTP-request-scoped** — verified in `RazorEmailRenderService.cs`: `RenderAsync<T>` constructs `HtmlRenderer` from the real app `IServiceProvider` (scoped per Hangfire job via `HangfireJobHelper.RunInScopeAsync`), and `IMarkdownService` is registered `AddSingleton` (per Phase 65's `ServiceExtensions.cs`) — singletons resolve from any scope.

**Caller/parameter contract unaffected** — `EmailPreviewController.QuestFinalized()` and `QuestFinalizedEmailJob.cs` (lines ~48-58) both pass `QuestDescription` as a plain string via a `Dictionary<string, object?>` keyed by `nameof(...)` — no changes needed there; only the component's internal rendering of that existing parameter changes.

---

### `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` (test, transform)

**Analog:** itself — extend the existing test class

**Existing test-class shape and assertion style to mirror** (lines 1-20):
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;
using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Services;

public class MarkdownServiceTests
{
    private static readonly IMarkdownService Service = new MarkdownService();

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    ...
    public void RenderToHtml_XssPayload_ProducesNoLiveScriptOrHandler(string markdown)
    {
        var html = Service.RenderToHtml(markdown);
        html.Should().NotContainEquivalentOf("<script");
        ...
    }
```
Add `ExtractPlainText_...` test methods following the same `MethodUnderTest_Scenario_ExpectedBehavior` naming convention already used throughout this file (`RenderToHtml_SingleNewlineNoBlankLine_StaysOneParagraph`, `RenderToHtml_NullOrWhitespace_ReturnsEmpty`, etc.) and FluentAssertions style (`.Should().Be(...)`). RESEARCH.md flags the specific multi-block word-boundary case this needs (`"# Heading\n\nSome text.\n\n- item one\n- item two"` must not smash words together).

---

## Shared Patterns

### Antiforgery (header-based, existing app-wide default)
**Source:** `QuestBoard.Service/Views/_ViewImports.cshtml` line 16 (`@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery`, global to all views) + `QuestBoard.Service/Views/Quest/Manage.cshtml` lines 685-701
**Apply to:** `MarkdownController.Preview` action, `_MarkdownEditor.cshtml`'s antiforgery-token exposure to JS, `markdown-editor-js`'s `fetch()` call

Token retrieval in a view (already the app-wide convention, `Manage.cshtml` line 7 / `Details.cshtml` line 12):
```csharp
var tokens = Antiforgery.GetAndStoreTokens(ViewContext.HttpContext);
```
Header-based `fetch()` usage (`Manage.cshtml` lines 686-692):
```javascript
function deleteQuest(id) {
    if (confirm("Are you sure?")) {
        fetch(`/Quest/Delete/${id}`, {
            method: "DELETE",
            headers: {
                'RequestVerificationToken': '@tokens.RequestToken'
            }
        }).then(res => { ... });
    }
}
```
For `/markdown/preview`, add `'Content-Type': 'application/json'` alongside the same `RequestVerificationToken` header and a JSON `body` — the header check is content-type-independent, so this is additive, not a different mechanism. **No `Program.cs` changes** — no explicit `AddAntiforgery(...)` call exists today; ASP.NET Core's default `HeaderName` (`"RequestVerificationToken"`) is already what this pattern relies on.

### Server-rendered HTML container convention
**Source:** `QuestBoard.Domain/Services/MarkdownService.cs` (Phase 65, unchanged)
**Apply to:** `Details.cshtml`, `Details.Mobile.cshtml`, `Manage.cshtml`/`.Mobile.cshtml`, `Index.cshtml`

Every read-side surface must call through `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)` (via the new `Html.Markdown()` helper) — never a second Markdown parser, never bypass the sanitizer. `MarkdownRenderTarget.Email` is reserved exclusively for `QuestFinalized.razor` and must never be used by the Preview endpoint (EDITOR-04 requires Preview to byte-match the page display, which uses `Web`).

### Shared write-form JS partial (`_QuestFormScripts.cshtml`)
**Source:** `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml` (full file, 48 lines)
**Apply to:** `Create.cshtml`/`.Mobile.cshtml`, `Edit.cshtml`/`.Mobile.cshtml` (already include it) and `CreateFollowUp.cshtml`/`.Mobile.cshtml` (currently do NOT — must gain the same `@section Scripts { @{ await Html.RenderPartialAsync("_QuestFormScripts"); } }` inclusion, per `Create.cshtml` lines 140-141 / `Edit.cshtml` lines 165-166, as the vehicle for wiring EasyMDE init across all 3 forms uniformly).

```html
@section Scripts {
@{ await Html.RenderPartialAsync("_QuestFormScripts"); }
}
```

### Class-selector-driven JS init (not ID-driven)
**Source:** RESEARCH.md Pattern 1, grounded in this codebase's own `asp-for` id-generation behavior (`Description` on Create/CreateFollowUp vs. `Quest_Description` on Edit)
**Apply to:** `markdown-editor.js`, `_MarkdownEditor.cshtml`

```javascript
document.querySelectorAll('.markdown-editor-textarea').forEach(function (el) {
    initMarkdownEditor(el, window.markdownAntiforgeryToken);
});
```
Mirrors `image-crop.js`'s defensive "safe no-op if markup absent" philosophy, generalized to multiple elements via `forEach` instead of a single `getElementById` early return.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `QuestBoard.IntegrationTests/Controllers/MarkdownControllerIntegrationTests.cs` (header+JSON antiforgery round trip specifically) | test | request-response | Confirmed by RESEARCH.md (Wave 0 Gaps): zero existing integration tests cover ANY header-based antiforgery action in this codebase — `QuestController.Delete`/`RemovePlayerSignup` (the other header-based actions) also have no integration-test coverage today. `AntiForgeryHelper.cs` only supports the form-field (`__RequestVerificationToken` hidden input) extraction path, not header construction. The planner should note this test file establishes a new integration-test helper method (header-token extraction + JSON body POST), not just a new test class using existing helpers. |
| `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` (Bootstrap tooltip init specifically) | component | event-driven | Confirmed zero `data-bs-toggle="tooltip"` usage anywhere in this codebase — RESEARCH.md's Bootstrap-5-official-docs code example (Code Examples section) is the pattern to use verbatim, not a repo analog. |

## Metadata

**Analog search scope:** `QuestBoard.Service/Controllers/`, `QuestBoard.Service/Extensions/`, `QuestBoard.Service/Views/Quest/`, `QuestBoard.Service/Views/Shared/`, `QuestBoard.Service/Views/Characters/`, `QuestBoard.Service/Components/Emails/`, `QuestBoard.Service/wwwroot/js/`, `QuestBoard.Service/wwwroot/css/`, `QuestBoard.Domain/Interfaces/`, `QuestBoard.Domain/Services/`, `QuestBoard.UnitTests/Services/`, `QuestBoard.IntegrationTests/Controllers/`, `QuestBoard.IntegrationTests/Helpers/`
**Files scanned:** ~30 (targeted reads, no full-repo scan needed — CONTEXT.md/RESEARCH.md already pre-identified exact file:line locations for nearly every touchpoint)
**Pattern extraction date:** 2026-07-09

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
