# Phase 68: Character Fields - Pattern Map

**Mapped:** 2026-07-10
**Files analyzed:** 6 (4 write forms + 2 read views) + 2 CSS edits
**Analogs found:** 8 / 8 (all files have a direct or near-direct analog from Phase 66/67's shipped Quest pattern)

This phase is a mechanical repeat of Phase 66/67's Markdown editor + rendering wiring. Every file below has a byte-for-byte-close analog already shipped on the Quest side. No new infrastructure (partial, controller, service, CSS class family) is introduced — only new call sites of existing infrastructure, plus two small CSS edits.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `QuestBoard.Service/Views/Characters/Create.cshtml` | view (write form) | request-response (form POST) | `QuestBoard.Service/Views/Quest/Create.cshtml` | exact |
| `QuestBoard.Service/Views/Characters/Create.Mobile.cshtml` | view (write form, mobile) | request-response | `QuestBoard.Service/Views/Quest/Create.cshtml` (no direct Quest Create.Mobile equivalent shipped Rewards the same way — Quest desktop is the cleaner analog; Character's own Create.cshtml sibling is the closer structural analog since Create/Create.Mobile share the exact same 4 `<textarea>` blocks already) | role-match |
| `QuestBoard.Service/Views/Characters/Edit.cshtml` | view (write form) | request-response | `QuestBoard.Service/Views/Quest/Edit.cshtml` | exact |
| `QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml` | view (write form, mobile) | request-response | `QuestBoard.Service/Views/Quest/Edit.cshtml` | role-match |
| `QuestBoard.Service/Views/Characters/Details.cshtml` | view (read) | request-response | `QuestBoard.Service/Views/Quest/Details.cshtml` (Description block, line 31) | exact |
| `QuestBoard.Service/Views/Characters/Details.Mobile.cshtml` | view (read, mobile) | request-response | `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (Description/Rewards block, lines 98-109) | exact |
| `QuestBoard.Service/wwwroot/css/markdown-editor.css` | config (CSS) | n/a | itself (Phase 66-authored file, this phase strengthens its own selector) | exact (self-modification) |
| `QuestBoard.Service/wwwroot/css/character-detail.mobile.css` | config (CSS) | n/a | `QuestBoard.Service/wwwroot/css/quest-log-detail.mobile.css` (CR-01's catch-all-`p`/`!important` pattern) — informational only; no fix needed here, see below | n/a (no change required, see Findings) |

No new C# files (controller, service, view model) — `MarkdownController`, `IMarkdownService`, `MarkdownEditorViewModel`, and `HtmlHelperExtensions.Markdown()` are all reused verbatim from Phase 66.

## Pattern Assignments

### `QuestBoard.Service/Views/Characters/Create.cshtml` (view, request-response)

**Analog:** `QuestBoard.Service/Views/Quest/Create.cshtml:29-45`

Replace the two plain `<textarea asp-for="Description">` / `<textarea asp-for="Backstory">` blocks (current file, lines 118-128) with `_MarkdownEditor` partial calls, mirroring Quest Create's Description/Rewards wiring exactly:

**Current state to replace** (`Views/Characters/Create.cshtml:118-128`):
```cshtml
<div class="mb-3">
    <label asp-for="Description" class="form-label">Description</label>
    <textarea asp-for="Description" class="form-control" rows="3" placeholder="Brief character description"></textarea>
    <span asp-validation-for="Description" class="text-danger"></span>
</div>

<div class="mb-3">
    <label asp-for="Backstory" class="form-label">Backstory</label>
    <textarea asp-for="Backstory" class="form-control" rows="5" placeholder="Character backstory"></textarea>
    <span asp-validation-for="Backstory" class="text-danger"></span>
</div>
```

**Analog pattern to copy** (`Views/Quest/Create.cshtml:29-45`):
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Description",
       Value = Model.Description,
       Label = "Description",
       Required = true,
       Placeholder = "Describe the quest, what players can expect, any special requirements..."
   }); }

@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Rewards",
       Value = Model.Rewards,
       Label = "Rewards",
       Required = false,
       Placeholder = "Describe the loot, gold, or other rewards for this quest..."
   }); }
```

**Adapted for this file** — `FieldName` is the bare model-binding name (`"Description"`/`"Backstory"`), matching Quest Create's own top-level `QuestViewModel` binding (not `Quest.Description`, which is Edit's nested-model pattern — see Edit section below). `Required = false` for both (confirmed: `CharacterViewModel.Description`/`Backstory` are `string?` with `[StringLength]` only, no `[Required]`). Placeholders per UI-SPEC Copywriting Contract: `"Brief character description..."` / `"Character backstory..."` (note the UI-SPEC adds a trailing `...` that the current plain-textarea placeholder lacks — apply the UI-SPEC copy exactly).

**Required `@using`:** add `@using QuestBoard.Service.ViewModels.Shared` to the top of the file (already present in `Views/Quest/Create.cshtml:4`; Character views do not currently import this namespace — confirmed by direct read, lines 1-2 only import `Domain.Enums` and `CharacterViewModels`).

**No Scripts-section change needed** — `_MarkdownEditor.cshtml` itself has no script dependency; the EasyMDE CDN `<script>`/`<link>` tags and the `markdown-editor.js` init call live in `_Layout.cshtml`/`_Layout.Mobile.cshtml` globally (confirm during implementation — Phase 66 wired this once at the layout level, not per-view, per its D-03/discretion notes and the absence of any editor `<script>` tag in Quest's own Create.cshtml Scripts section above).

---

### `QuestBoard.Service/Views/Characters/Create.Mobile.cshtml` (view, request-response, mobile)

**Analog:** same `_MarkdownEditor` partial call pattern as desktop Create.cshtml above; structurally this file's own Description/Backstory blocks (current lines 98-108) are already identical markup to desktop Create.cshtml's, so the edit is a straight copy of the same two `RenderPartialAsync` calls into the `character-form-card` mobile wrapper instead of `modern-card-body`. No Quest mobile Create view was ever built with Rewards wired the same way as a single reference — Quest's mobile Create/Edit(.Mobile) forms follow the identical desktop pattern verbatim per Phase 66/67 (confirmed by the shared `_MarkdownEditor` partial being platform-agnostic markup), so treat desktop `Views/Quest/Create.cshtml` as the pattern source for both Character Create.cshtml and Create.Mobile.cshtml.

**Placeholder copy per UI-SPEC:** `Create.Mobile.cshtml` already has `"Brief character description"` / `"Character backstory"` placeholders (1 of the 3-of-4 forms UI-SPEC says already has them) — apply UI-SPEC's exact copy (`"Brief character description..."` with trailing ellipsis) for consistency across all 4 forms per the Copywriting Contract.

**Required `@using`:** same as desktop Create.cshtml — add `@using QuestBoard.Service.ViewModels.Shared`.

---

### `QuestBoard.Service/Views/Characters/Edit.cshtml` (view, request-response)

**Analog:** `QuestBoard.Service/Views/Quest/Edit.cshtml:31-47`

Same partial-swap as Create, but `FieldName` uses the nested-model dotted path since `Edit.cshtml`'s model is `CharacterViewModel` directly bound at top level here too (unlike Quest's `EditQuestViewModel` wrapper) — verify at implementation time which binding path applies. Quest's Edit uses `"Quest.Description"` because its model is `EditQuestViewModel { Quest, ... }`; Character's `Edit.cshtml` model is `CharacterViewModel` directly (same as Create), so `FieldName` should stay `"Description"`/`"Backstory"` (bare), matching Create's binding path, not Quest Edit's nested one.

**Current state to replace** (`Views/Characters/Edit.cshtml:116-126`):
```cshtml
<div class="mb-3">
    <label asp-for="Description" class="form-label">Description</label>
    <textarea asp-for="Description" class="form-control" rows="3"></textarea>
    <span asp-validation-for="Description" class="text-danger"></span>
</div>

<div class="mb-3">
    <label asp-for="Backstory" class="form-label">Backstory</label>
    <textarea asp-for="Backstory" class="form-control" rows="5"></textarea>
    <span asp-validation-for="Backstory" class="text-danger"></span>
</div>
```

**Analog pattern** (`Views/Quest/Edit.cshtml:31-47`, adapt `FieldName` to bare `"Description"`/`"Backstory"` per above):
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Description",
       Value = Model.Description,
       Label = "Description",
       Required = false,
       Placeholder = "Brief character description..."
   }); }

@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Backstory",
       Value = Model.Backstory,
       Label = "Backstory",
       Required = false,
       Placeholder = "Character backstory..."
   }); }
```

**Note (closes a pre-existing gap, per CONTEXT.md/UI-SPEC):** desktop `Edit.cshtml` currently has **no placeholder at all** on either textarea (confirmed above — bare `<textarea asp-for="Description" class="form-control" rows="3"></textarea>`, no `placeholder=` attribute), unlike the other 3 write forms. Adding `Placeholder = "Brief character description..."` / `"Character backstory..."` here via the partial closes that 1-of-4 inconsistency as a byproduct of routing through `_MarkdownEditor`.

**Required `@using`:** add `@using QuestBoard.Service.ViewModels.Shared`.

---

### `QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml` (view, request-response, mobile)

**Analog:** same as `Edit.cshtml` above (its own desktop sibling) — this file already has both placeholders (`"Brief character description"` / `"Character backstory"`, lines 107/113), so only the `<textarea>`→partial swap is needed, plus appending `...` to match UI-SPEC copy exactly. `FieldName` stays bare (`"Description"`/`"Backstory"`), same binding path as desktop Edit.

**Required `@using`:** add `@using QuestBoard.Service.ViewModels.Shared`.

---

### `QuestBoard.Service/Views/Characters/Details.cshtml` (view, request-response, read)

**Analog:** `QuestBoard.Service/Views/Quest/Details.cshtml:31` (Description, direct child of `.card-body.modern-card-body`) — this is the exact structural precedent for Character Details' contrast situation (a `.markdown-content` render site with no dark-overlay box wrapper).

**Current state to replace** (`Views/Characters/Details.cshtml:147-161`, confirmed by direct read):
```cshtml
@if (!string.IsNullOrEmpty(Model.Description))
{
    <div class="mb-3">
        <label class="form-label fw-bold">Description</label>
        <p class="form-control-plaintext" style="white-space: pre-wrap;">@Model.Description</p>
    </div>
}

@if (!string.IsNullOrEmpty(Model.Backstory))
{
    <div>
        <label class="form-label fw-bold">Backstory</label>
        <p class="form-control-plaintext" style="white-space: pre-wrap;">@Model.Backstory</p>
    </div>
}
```

**Analog pattern** (`Views/Quest/Details.cshtml:30-31`, direct `Html.Markdown()` call inside `.card-body.modern-card-body`, no `<p>` wrapper):
```cshtml
<div class="card-body modern-card-body">
    @Html.Markdown(Model.Quest?.Description)
```

**Adapted for this file** — per `68-CONTEXT.md`'s Claude's-Discretion and `68-UI-SPEC.md`'s Copywriting Contract "Read-view restructuring" row, replace the illegal `<p class="form-control-plaintext">` wrapper (Markdig emits block-level HTML — headings, lists — that cannot legally nest inside a `<p>`) with the existing `<div>` wrapper calling `Html.Markdown()` directly, keeping the label:
```cshtml
@if (!string.IsNullOrEmpty(Model.Description))
{
    <div class="mb-3">
        <label class="form-label fw-bold">Description</label>
        @Html.Markdown(Model.Description)
    </div>
}

@if (!string.IsNullOrEmpty(Model.Backstory))
{
    <div>
        <label class="form-label fw-bold">Backstory</label>
        @Html.Markdown(Model.Backstory)
    </div>
}
```
Note the inline `style="white-space: pre-wrap;"` is dropped entirely (not moved) — `.markdown-content { white-space: normal }` (Phase 67, `markdown-content.css:13-18`) already neutralizes the need for it, and per CONTEXT.md the guard logic (`@if (!string.IsNullOrEmpty(...))`, whole-block omission when blank) is unchanged.

**No CSS fix needed here** — verified via direct read of `site.css`'s `.modern-card h1-h6/p/li/span/small { color: #F4E4BC !important; ... }` rule (cited by UI-SPEC at lines 1094-1116) combined with the fact that `.markdown-content` here nests one level deeper (`<div class="mb-3">` → `Html.Markdown()`'s own `<div class="markdown-content">`) than Quest Details' direct-child case — `.modern-card-body > .markdown-content h1..h6` (the WR-01 fix in `markdown-content.css:78-86`) requires a **direct child** relationship that does not hold here, but the `!important` catch-all rule wins regardless of specificity, so headings/paragraphs render consistently gold-with-shadow either way. **Do not port the `.modern-card-body > .markdown-content` override into a Character-specific selector** — confirmed dead CSS in this context by the UI-SPEC's own analysis.

---

### `QuestBoard.Service/Views/Characters/Details.Mobile.cshtml` (view, request-response, mobile, read)

**Analog:** `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:98-109` (Description/Rewards mobile block)

**Current state to replace** (`Views/Characters/Details.Mobile.cshtml:80-93`, confirmed by direct read):
```cshtml
@if (!string.IsNullOrEmpty(Model.Description))
{
    <div class="character-info-row">
        <span class="form-label">Description</span>
        <p class="character-info-value mb-0">@Model.Description</p>
    </div>
}
@if (!string.IsNullOrEmpty(Model.Backstory))
{
    <div class="character-info-row">
        <span class="form-label">Backstory</span>
        <p class="character-info-value mb-0">@Model.Backstory</p>
    </div>
}
```

**Analog pattern** (`Views/Quest/Details.Mobile.cshtml:98-102`):
```cshtml
@if (!string.IsNullOrWhiteSpace(Model.Quest?.Description))
{
    <hr class="quest-card-divider mt-2 mb-2" />
    @Html.Markdown(Model.Quest.Description)
}
```

**Adapted for this file** — same `<p>`→direct-`Html.Markdown()`-call restructuring as desktop Details.cshtml, keeping the existing `<span class="form-label">` label and `.character-info-row` wrapper (this class already isn't a `<p>`, so it stays; only the inner `<p class="character-info-value">` is dropped in favor of calling `Html.Markdown()` directly inside `.character-info-row`):
```cshtml
@if (!string.IsNullOrEmpty(Model.Description))
{
    <div class="character-info-row">
        <span class="form-label">Description</span>
        @Html.Markdown(Model.Description)
    </div>
}
@if (!string.IsNullOrEmpty(Model.Backstory))
{
    <div class="character-info-row">
        <span class="form-label">Backstory</span>
        @Html.Markdown(Model.Backstory)
    </div>
}
```
Whether `form-control-plaintext`/`character-info-value` classes are kept, dropped, or replaced on the outer wrapper is an implementation detail per CONTEXT.md — the `.markdown-content` div `Html.Markdown()` emits already carries its own typography, so no class is required on the new call site itself.

---

## Shared Patterns

### `_MarkdownEditor` partial (write forms)
**Source:** `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` (full file, 21 lines — reused verbatim, zero changes)
**Apply to:** all 4 Character write forms (`Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`)
```cshtml
@using QuestBoard.Service.ViewModels.Shared
@model MarkdownEditorViewModel
@{
    var elementId = Model.FieldName.Replace('.', '_');
}

<div class="mb-3">
    <label for="@elementId" class="form-label">
        @Model.Label
        <i class="fas fa-info-circle text-warning ms-1" data-bs-toggle="tooltip" title="Supports Markdown formatting. Leave a blank line between paragraphs."></i>
        @if (Model.Required)
        {
            <span class="text-danger">*</span>
        }
    </label>
    <textarea name="@Model.FieldName" id="@elementId" class="form-control markdown-editor-textarea" rows="4" placeholder="@Model.Placeholder" required="@Model.Required">@Model.Value</textarea>
    <span class="text-danger" data-valmsg-for="@Model.FieldName" data-valmsg-replace="true"></span>
</div>
```
Invoke via `@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel { FieldName = "...", Value = Model.X, Label = "...", Required = false, Placeholder = "..." }); }`. `MarkdownEditorViewModel` is `QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs` (5 properties: `FieldName`, `Value`, `Label`, `Required`, `Placeholder` — no changes needed).

### Read-side rendering
**Source:** `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs:19-24`
**Apply to:** `Details.cshtml`, `Details.Mobile.cshtml`
```csharp
internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
{
    var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
    var rendered = service.RenderToHtml(markdown, MarkdownRenderTarget.Web);
    return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
}
```
Call as `@Html.Markdown(Model.Description)` / `@Html.Markdown(Model.Backstory)`. No changes needed — reused verbatim. `MarkdownRenderTarget.Web` is correct (not `.Email` — Character fields never appear in an email template, confirmed by CONTEXT.md).

### Preview endpoint (no change needed)
**Source:** `QuestBoard.Service/Controllers/MarkdownController.cs` (full file, 28 lines)
**Apply to:** implicitly all 4 write forms via `_MarkdownEditor`'s client-side JS (`markdown-editor.js`, not read in detail — out of scope for this phase, zero changes needed per CONTEXT.md's "reuse verbatim").
`[Authorize]` + `POST /markdown/preview` + `[ValidateAntiForgeryToken]`, calls `IMarkdownService.RenderToHtml(request?.Markdown, MarkdownRenderTarget.Web)`, returns raw HTML `Content(...)`. Nothing to change; the endpoint is field-agnostic (operates on any Markdown string).

### CSS fix 1 — editor/Preview text-color leak (REQUIRED, global file, apply once)
**Source/Target:** `QuestBoard.Service/wwwroot/css/markdown-editor.css:29-35` (current full file content confirmed by direct read — matches UI-SPEC's cited before-state exactly)

**Current state (confirmed as-is on disk):**
```css
.CodeMirror.CodeMirror,
.CodeMirror.CodeMirror *,
.editor-preview.editor-preview,
.editor-preview.editor-preview * {
    color: #000 !important;
    text-shadow: none !important;
}
```

**Required change per `68-UI-SPEC.md`** (triples the class for a specificity bump that beats `character-form.mobile.css`'s `.character-form-card span:not(.badge)` catch-all — the `:not(.badge)` pseudo-class plus a `span` type-selector currently outranks the doubled-class version specifically for `<span>` syntax-highlighting nodes inside CodeMirror):
```css
.CodeMirror.CodeMirror.CodeMirror,
.CodeMirror.CodeMirror.CodeMirror *,
.editor-preview.editor-preview.editor-preview,
.editor-preview.editor-preview.editor-preview * {
    color: #000 !important;
    text-shadow: none !important;
}
```
**Verified accurate:** this file's current selector text (`.CodeMirror.CodeMirror`/`.editor-preview.editor-preview`, doubled) matches exactly what `68-UI-SPEC.md` describes as the "before" state — the prescribed fix (triple the class) is confirmed applicable as written, no adjustment needed. This is a global, class-scoped edit — applies to every current and future Markdown editor instance app-wide, not scoped to Characters. Apply to this one file only; no view-file changes required for this fix.

### CSS fix 2 — mobile list-item color gap (REQUIRED per UI-SPEC, Typography section)
**Source/Target:** `QuestBoard.Service/wwwroot/css/character-detail.mobile.css:82-87` (confirmed current content by direct read — matches UI-SPEC's cited lines exactly)

**Current state (confirmed as-is on disk):**
```css
/* All text inside card — catch-all parchment override */
.character-detail-card p,
.character-detail-card a,
.character-detail-card span:not(.badge) {
    color: #F4E4BC !important;
    text-shadow: 2px 2px 4px rgba(0,0,0,0.9), -1px -1px 2px rgba(0,0,0,0.9);
}
```

**Required change** — add `li` so Markdown list items rendered inside `.markdown-content` match full-opacity gold instead of falling back to `.character-info-value`'s muted `rgba(244, 228, 188, 0.7)`:
```css
.character-detail-card p,
.character-detail-card a,
.character-detail-card li,
.character-detail-card span:not(.badge) {
    color: #F4E4BC !important;
    text-shadow: 2px 2px 4px rgba(0,0,0,0.9), -1px -1px 2px rgba(0,0,0,0.9);
}
```

### CSS — `.character-info-value` pre-wrap rule (OPTIONAL, either choice is correct)
**Location:** `QuestBoard.Service/wwwroot/css/character-detail.mobile.css:62-65` (confirmed exact current lines by direct read — matches CONTEXT.md's cited "57-65" range approximately; the specific `white-space: pre-wrap` declaration is lines 63-65 inside a 2-line comment + rule block starting at 62):
```css
/* Preserve typed line breaks in Description and Backstory */
.character-info-value {
    white-space: pre-wrap;
}
```
Per CONTEXT.md/UI-SPEC: safe to leave in place (the only other consumers of this shared class — `Owned by @Model.OwnerName`, `@Model.Level` — are confirmed single-line, so pre-wrap vs. normal is visually identical for them) or remove outright (also safe, since `.markdown-content { white-space: normal }` neutralizes it for the new Markdown call sites regardless). No action required either way; pick whichever is less invasive during implementation.

## No Analog Found

None — every file in this phase's scope has a shipped Phase 66/67 Quest-side analog covering the same role and data flow.

## Flagged Discrepancies / Verification Notes for Planner

- **`Views/Characters/Details.cshtml:151,159`** — confirmed exact current markup via direct read: `<p class="form-control-plaintext" style="white-space: pre-wrap;">@Model.Description</p>` / `...Backstory</p>`, matching CONTEXT.md's citation precisely. No drift between CONTEXT.md's description and the file's actual state.
- **`character-detail.mobile.css`** — `.character-info-value`'s `white-space: pre-wrap` rule is at lines 63-65 (CONTEXT.md cites "57-65", which is the whole comment+selector span including the "Muted / secondary text" rule directly above it at lines 54-60 — the two are adjacent but distinct rules; only lines 62-65 are the pre-wrap declaration proper). The catch-all parchment rule CONTEXT.md/UI-SPEC describes needing the `li` fix is at lines 82-87, confirmed exact match to UI-SPEC's quoted snippet.
- **`markdown-editor.css`** — UI-SPEC's prescribed fix (triple the `.CodeMirror`/`.editor-preview` class) was verified against the actual current file content (29 lines total) and matches the "before" state UI-SPEC quotes exactly, character-for-character. The fix is safe to apply as written.
- **`Views/Characters/Edit.cshtml`** confirmed to have zero `placeholder=` attributes on Description/Backstory (the "1-of-4 inconsistency" CONTEXT.md/UI-SPEC describe) — verified by direct read, lines 116-126.
- **All 4 write forms confirmed to already have both `asp-for="Description"` and `asp-for="Backstory"` fields** (no missing-field backfill needed, unlike Phase 67's Quest Rewards gap) — verified during this pattern-mapping pass by reading all 4 files in full.
- **`@using QuestBoard.Service.ViewModels.Shared` is missing from all 4 Character write form files today** — this import must be added to each (`Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`) for `MarkdownEditorViewModel` to resolve unqualified, mirroring `Views/Quest/Create.cshtml:4`/`Edit.cshtml:4`.
- **Partial-view short-name resolution lesson (Phase 67-05, `_QuestFormScripts`)** does not apply here — `_MarkdownEditor.cshtml` lives in `Views/Shared/`, which Razor's view-location search always checks regardless of the calling controller, unlike `_QuestFormScripts.cshtml` which lives in `Views/Quest/`. No full-path workaround needed for Character views calling `_MarkdownEditor`.

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/Characters/`, `QuestBoard.Service/Views/Quest/`, `QuestBoard.Service/Views/Shared/`, `QuestBoard.Service/wwwroot/css/` (character-*.css, markdown-*.css, quest*.css), `QuestBoard.Service/Extensions/`, `QuestBoard.Service/Controllers/`, `QuestBoard.Service/ViewModels/Shared/` and `ViewModels/CharacterViewModels/`
**Files scanned:** 16 (8 Character views, 8 Quest/Shared views + CSS/controller/extension/viewmodel files)
**Pattern extraction date:** 2026-07-10
