# Phase 70: DM Profile & Shop Fields - Pattern Map

**Mapped:** 2026-07-10
**Files analyzed:** 12 (2 DM Profile write forms, 2 DM Profile read views, 4 Shop write forms, 2 Shop read views, 2 Shop preview-teaser list views)
**Analogs found:** 12 / 12

This phase mechanically repeats Phase 68 (Character)/Phase 69 (Contact)'s Markdown editor wiring. No new infra files — `_MarkdownEditor.cshtml`, `MarkdownEditorViewModel`, `markdown-editor.js`, `HtmlHelperExtensions.Html.Markdown()`, and `IMarkdownService.ExtractPlainText()` are all reused verbatim, unmodified.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml` (Bio field) | component (write form) | CRUD | `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124` | exact |
| `QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml` (Bio field) | component (write form) | CRUD | `QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml` (same partial call shape) | role-match |
| `QuestBoard.Service/Views/DungeonMaster/Profile.cshtml` (Bio read) | component (read view) | request-response | `QuestBoard.Service/Views/Characters/Details.cshtml:148-154` | role-match (structure differs slightly — no label needed here, has dedicated "About" card) |
| `QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml` (Bio read) | component (read view) | request-response | Contact's mobile Description read (`Contacts/Details.Mobile.cshtml`, class-preserving wrapper pattern) | role-match |
| `QuestBoard.Service/Views/ShopManagement/Create.cshtml` (Description field) | component (write form) | CRUD | `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124` | exact (this field is `Required = true`, first in milestone — see Shared Patterns) |
| `QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml` (Description field) | component (write form) | CRUD | same as Create.cshtml | role-match |
| `QuestBoard.Service/Views/ShopManagement/Edit.cshtml` (Description field) | component (write form) | CRUD | same as Create.cshtml | exact |
| `QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml` (Description field) | component (write form) | CRUD | same as Create.cshtml | role-match |
| `QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml` (Description read) | component (read view) | request-response | `QuestBoard.Service/Views/Characters/Details.cshtml:148-154` | role-match |
| `QuestBoard.Service/Views/Shop/Details.Mobile.cshtml` (Description read) | component (read view) | request-response | Contact's mobile Description read (class-preserving wrapper) | role-match |
| `QuestBoard.Service/Views/Shop/Index.cshtml` (D-01 preview teaser) | component (list/card view) | transform | `QuestBoard.Service/Views/Quest/Index.cshtml:1-4,36,120` (`ExtractPlainText()` injection + call) | exact |
| `QuestBoard.Service/Views/ShopManagement/Index.cshtml` (D-02/D-03 preview teaser, 3 call sites → consolidated) | component (list view) | transform | `QuestBoard.Service/Views/Quest/Index.cshtml:36` (same `ExtractPlainText()` mechanism, but this file additionally needs D-03 consolidation — no existing consolidated-helper analog in the codebase, first of its kind) | role-match (mechanism); no-analog (consolidation shape) |

## Pattern Assignments

### `Views/DungeonMaster/EditProfile.cshtml` / `EditProfile.Mobile.cshtml` (component, CRUD) — Bio field

**Analog:** `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124`

**Current markup to replace** (`EditProfile.cshtml:45-53`):
```cshtml
<div class="mb-3">
    <label asp-for="Bio" class="form-label">Bio</label>
    <textarea asp-for="Bio" class="form-control"
              rows="6" maxlength="2000"></textarea>
    <span asp-validation-for="Bio" class="text-danger"></span>
    <small class="form-text text-muted">
        Introduce yourself to the players (max 2000 characters).
    </small>
</div>
```

**Target pattern** (Character's shipped equivalent — keep the existing helper `<small>` text below the partial, unchanged, since `_MarkdownEditor.cshtml` renders its own separate tooltip, not this app-specific copy):
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Bio",
       Value = Model.Bio,
       Label = "Bio",
       Required = false,
       Placeholder = null
   }); }
<small class="form-text text-muted">
    Introduce yourself to the players (max 2000 characters).
</small>
```

**Required companion edits:**
- Add `@using QuestBoard.Service.ViewModels.Shared` to the top of both files (confirmed absent today — file currently only has `@using QuestBoard.Service.ViewModels.DungeonMasterViewModels`).
- Add `@{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }` inside `@section Scripts` — `EditProfile.cshtml`'s existing Scripts section only loads `cropper.min.js` + `image-crop.js` (lines 122-130); append the loader, don't replace the cropper scripts.
- Mobile file: confirm the analogous `<textarea asp-for="Bio">` block at `EditProfile.Mobile.cshtml:46-52` and apply the identical partial-call swap; add the same `@using` and Scripts-section loader if not already present (Character's `Edit.Mobile.cshtml` already has both — mirror it).

---

### `Views/DungeonMaster/Profile.cshtml` (component, request-response) — Bio read

**Analog:** `QuestBoard.Service/Views/Characters/Details.cshtml:148-154`

**Current markup to replace** (`Profile.cshtml:49-56`):
```cshtml
@if (!string.IsNullOrWhiteSpace(Model.Bio))
{
    <p style="white-space: pre-wrap;">@Model.Bio</p>
}
else
{
    <p class="text-muted fst-italic">No bio provided yet.</p>
}
```

**Target pattern:**
```cshtml
@if (!string.IsNullOrWhiteSpace(Model.Bio))
{
    <div class="markdown-content">
        @Html.Markdown(Model.Bio)
    </div>
}
else
{
    <p class="text-muted fst-italic">No bio provided yet.</p>
}
```
Note: unlike Character's Details.cshtml, this card already has a dedicated `<h5><i class="fas fa-scroll me-2"></i>About</h5>` header — do not add a redundant inline `<label>Description</label>` the way Character's read view does; the card header already serves that role. This `<div>` must remain a **direct child** of `.card-body.modern-card-body` (per CONTEXT.md/UI-SPEC.md's structural note, though UI-SPEC found the `!important` heading-color rule wins regardless — keep the direct-child structure anyway for consistency with every other field in the milestone).

---

### `Views/DungeonMaster/Profile.Mobile.cshtml` (component, request-response) — Bio read

**Analog:** Contact's mobile Description read pattern (class-preserving wrapper, `69-PATTERNS.md`'s "Read-view restructuring" entry) — no exact file found since DM Profile mobile uses bespoke classes, but the wrapper technique is identical.

**Current markup** (`Profile.Mobile.cshtml:45-52`, per CONTEXT.md's Integration Points — no inline pre-wrap style present today):
```cshtml
<p class="dm-profile-bio-text">@Model.Bio</p>
```

**Target pattern** (keep the existing class on the wrapper so `dm-profile.mobile.css`'s color/text-shadow/font-size rules keep applying via inheritance — per UI-SPEC.md's Typography section, this needs **zero new CSS**):
```cshtml
<div class="markdown-content dm-profile-bio-text">
    @Html.Markdown(Model.Bio)
</div>
```

---

### `Views/ShopManagement/{Create,Create.Mobile,Edit,Edit.Mobile}.cshtml` (component, CRUD) — Description field

**Analog:** `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124`

**Current markup to replace** (`Create.cshtml:43-49`, confirmed by direct read; `Edit.cshtml:67-71` has the identical shape per CONTEXT.md):
```cshtml
<div class="mb-3">
    <label asp-for="Description" class="form-label"></label>
    <textarea asp-for="Description" class="form-control" rows="4"
              placeholder="Describe the item's appearance, properties, and lore..."></textarea>
    <span asp-validation-for="Description" class="text-danger"></span>
    <div class="form-text">Provide a detailed description including mechanical effects, appearance, and any lore.</div>
</div>
```

**Target pattern — this is the milestone's first `Required = true` call site:**
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Description",
       Value = Model.Description,
       Label = "Description",
       Required = true,
       Placeholder = "Describe the item's appearance, properties, and lore..."
   }); }
<div class="form-text">Provide a detailed description including mechanical effects, appearance, and any lore.</div>
```
Per UI-SPEC.md's Copywriting Contract: `Edit.cshtml`/`Edit.Mobile.cshtml` never had the helper `<div class="form-text">` — do not add it there, only on Create/Create.Mobile (closing that gap is explicitly out of scope). Apply the same `Placeholder` text to `Edit.cshtml`/`Edit.Mobile.cshtml` too (they currently have none — this closes a pre-existing 2-of-4 inconsistency, same technique Character/Contact used).

**Required companion edits:**
- Add `@using QuestBoard.Service.ViewModels.Shared` to all 4 files.
- Add `@{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }` to `@section Scripts` on all 4 — none of these 6 forms across the whole phase currently reference this loader (per CONTEXT.md's Integration Points).
- **Bespoke inline-JS fix (desktop only, functional not copy) — `Create.cshtml:237-256`:**
```javascript
document.querySelector('form').addEventListener('submit', function(e) {
    const name = document.getElementById('Name').value.trim();
    const description = document.getElementById('Description').value.trim();
    // ...
    if (description.length < 20) {
        e.preventDefault();
        alert('Please provide a more detailed description (at least 20 characters).');
        return false;
    }
    return true;
});
```
Once EasyMDE hides the raw `<textarea id="Description">`, `.value` reads stale/empty content. Fix by reading from the CodeMirror instance instead — same class of bug Phase 69 Wave 3 fixed at the shared `markdown-editor.js` submit-sync level, but this is bespoke inline JS on a different path (a `submit` listener, not the shared submit-sync hook), so it needs its own explicit fix. Read the live value via the EasyMDE instance registered by `markdown-editor.js` (check `markdown-editor.js`'s public API/registry — e.g. `document.getElementById('Description').easyMDE?.value()` or equivalent lookup mechanism used elsewhere) instead of `.value.trim()` directly on the hidden textarea. `Edit.cshtml:289-306` has the identical pattern and needs the identical fix. Mobile forms (`Create.Mobile.cshtml`/`Edit.Mobile.cshtml`) have no equivalent inline script — confirmed by CONTEXT.md, no work needed there.

---

### `Views/Shared/_ShopItemDetailsContent.cshtml` (component, request-response) — Description read

**Analog:** `QuestBoard.Service/Views/Characters/Details.cshtml:148-154`

**Current markup to replace** (`_ShopItemDetailsContent.cshtml:128-138`):
```cshtml
<!-- Item Description -->
<div class="card modern-card mb-3">
    <div class="card-header modern-card-header">
        <h6 class="mb-0">
            <i class="fas fa-scroll me-2"></i>Description
        </h6>
    </div>
    <div class="card-body modern-card-body">
        <p class="mb-0" style="white-space: pre-wrap;">@Model.Description</p>
    </div>
</div>
```

**Target pattern** (card header already serves as the label, same reasoning as DM Profile's About card — do not add a redundant inline label):
```cshtml
<!-- Item Description -->
<div class="card modern-card mb-3">
    <div class="card-header modern-card-header">
        <h6 class="mb-0">
            <i class="fas fa-scroll me-2"></i>Description
        </h6>
    </div>
    <div class="card-body modern-card-body">
        <div class="markdown-content">
            @Html.Markdown(Model.Description)
        </div>
    </div>
</div>
```
Since `Description` is `[Required]`, the `else`/empty-state branch present on every other field's read view is not needed here — none exists today (confirmed, no `if`/`else` guard around this block).

---

### `Views/Shop/Details.Mobile.cshtml` (component, request-response) — Description read

**Analog:** Contact's mobile Description read wrapper pattern.

**Current markup** (`Details.Mobile.cshtml:21` per CONTEXT.md):
```cshtml
<p class="mt-2 parchment-text-muted" style="white-space: pre-wrap;">@Model.Description</p>
```

**Target pattern** (keep the class, drop the inline pre-wrap style — `.markdown-content { white-space: normal }` already neutralizes it):
```cshtml
<div class="markdown-content parchment-text-muted mt-2">
    @Html.Markdown(Model.Description)
</div>
```

---

### `Views/Shop/Index.cshtml` (component, transform) — D-01 customer-facing card preview

**Analog:** `QuestBoard.Service/Views/Quest/Index.cshtml:1-4` (injection) and `:36`/`:120` (call sites)

**Analog injection pattern** (`Quest/Index.cshtml:1-4`):
```cshtml
@using QuestBoard.Domain.Enums
@using QuestBoard.Domain.Interfaces
@using QuestBoard.Domain.Models.QuestBoard
@inject QuestBoard.Domain.Interfaces.IMarkdownService MarkdownService
```

**Analog call site** (`Quest/Index.cshtml:120`):
```cshtml
<div class="quest-description">@MarkdownService.ExtractPlainText(quest.Description)</div>
```

**Current Shop markup to replace** (`Shop/Index.cshtml:304-312`):
```cshtml
<div class="item-description">
    @{
        var shortDescription = item.Description.Length > 120
            ? item.Description.Substring(0, 120) + "..."
            : item.Description;
    }
    @shortDescription
</div>
```

**Target pattern** (add `@inject IMarkdownService MarkdownService` near the top of the file if not already present, then extract-before-truncate; 120-char length and `"..."` suffix unchanged per CONTEXT.md's Claude's Discretion):
```cshtml
<div class="item-description">
    @{
        var plainDescription = MarkdownService.ExtractPlainText(item.Description);
        var shortDescription = plainDescription.Length > 120
            ? plainDescription.Substring(0, 120) + "..."
            : plainDescription;
    }
    @shortDescription
</div>
```

---

### `Views/ShopManagement/Index.cshtml` (component, transform) — D-02/D-03 DM-facing list preview (3 sites, consolidated)

**Analog (mechanism):** `Quest/Index.cshtml:36`, same `ExtractPlainText()` call.
**Analog (consolidation shape):** none in the codebase — first place in the milestone where 3 duplicate inline snippets are consolidated into one helper (per CONTEXT.md D-03, user-directed narrow exception to the milestone's minimal-diff default).

**Current markup, 3 identical occurrences** (`ShopManagement/Index.cshtml:56,128,300`):
```cshtml
<small class="text-muted">@(item.Description.Length > 50 ? item.Description.Substring(0, 50) + "..." : item.Description)</small>
```

**Target pattern** — add `@inject IMarkdownService MarkdownService` near the top, then define one `@functions` block (or `HtmlHelper` extension, per CONTEXT.md's discretion note — a view-local `@functions` block is the lower-friction choice since no other file needs this helper):
```cshtml
@functions {
    string DescriptionTeaser(string description)
    {
        var plain = MarkdownService.ExtractPlainText(description);
        return plain.Length > 50 ? plain.Substring(0, 50) + "..." : plain;
    }
}
```
Then replace all 3 call sites:
```cshtml
<small class="text-muted">@DescriptionTeaser(item.Description)</small>
```
50-char truncation length unchanged per CONTEXT.md's Claude's Discretion (same reasoning as D-01's 120-char length).

---

## Shared Patterns

### Markdown editor partial (write forms)
**Source:** `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` + `QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs` (shipped Phase 66, unmodified since — Phase 69's `ElementId` addition does not apply here, all 6 of this phase's call sites are single-instance)
**Apply to:** all 6 write forms (`EditProfile`, `EditProfile.Mobile`, `ShopManagement/Create`, `Create.Mobile`, `Edit`, `Edit.Mobile`)
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "<FieldName>",
       Value = Model.<FieldName>,
       Label = "<Label>",
       Required = <true|false>,
       Placeholder = "<placeholder or null>"
   }); }
```

### Editor scripts loader (write forms only — read views in this phase need no scripts)
**Source:** `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml` (entity-agnostic, resolves via `~/` root-relative path)
**Apply to:** all 6 write forms' `@section Scripts` blocks.
```cshtml
@{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }
```
`EditProfile.cshtml` and `ShopManagement/Create.cshtml`/`Edit.cshtml` already have non-empty `@section Scripts` blocks (cropper JS, price-suggestion JS) — append the loader call, do not replace existing script content.

### `Html.Markdown()` read-side rendering
**Source:** `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs`
**Apply to:** DM Profile Bio (`Profile.cshtml`, `Profile.Mobile.cshtml`) and Shop Item Description (`_ShopItemDetailsContent.cshtml`, `Shop/Details.Mobile.cshtml`).
```cshtml
<div class="markdown-content">@Html.Markdown(Model.Bio)</div>
```
Wrapper must stay a direct child of `.card-body.modern-card-body` on desktop cards (structural convention carried from Phase 66-69, though UI-SPEC.md's research confirmed the `!important` heading-color rule is not actually dependent on this — keep the convention for consistency anyway).

### `IMarkdownService.ExtractPlainText()` plain-text teaser
**Source:** `QuestBoard.Domain/Interfaces/IMarkdownService.cs` implementation, first used `QuestBoard.Service/Views/Quest/Index.cshtml:1-4,36,120` (Phase 66 D-06)
**Apply to:** `Shop/Index.cshtml` (D-01, 120-char) and `ShopManagement/Index.cshtml` (D-02/D-03, 50-char, consolidated into one `@functions` helper across 3 call sites).
```cshtml
@inject QuestBoard.Domain.Interfaces.IMarkdownService MarkdownService
@* ... *@
@MarkdownService.ExtractPlainText(item.Description)
```

### Pre-wrap cleanup (companion edit, not a Markdown-migration mechanism per se)
**Source:** `markdown-content.css`'s `.markdown-content { white-space: normal }` (Phase 67), already neutralizes doubled-spacing globally.
**Apply to:** remove (not move) 3 inline `style="white-space: pre-wrap;"` occurrences: `DungeonMaster/Profile.cshtml:51`, `_ShopItemDetailsContent.cshtml:136`, `Shop/Details.Mobile.cshtml:21`. Leave `dm-profile.mobile.css:62`'s `.dm-profile-bio-text { white-space: pre-wrap }` class rule in place (not inline, optional cleanup only, per Phase 64/68 precedent).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `ShopManagement/Index.cshtml`'s D-03 consolidated `@functions` teaser helper | component (list view helper) | transform | First place in the milestone the user chose consolidation over independent inline duplication (3 identical copies → 1 shared helper). No prior consolidated-helper precedent exists in `Views/Quest` or `Views/Characters`/`Contacts` to copy structurally — the `ExtractPlainText()` call itself is a direct analog (Quest/Index.cshtml), but the consolidation shape is novel to this phase. |
| `ShopManagement/Create.cshtml`/`Edit.cshtml`'s bespoke inline submit-validation JS fix (reading live EasyMDE value instead of raw textarea) | utility (inline script fix) | event-driven | Distinct from Phase 69 Wave 3's shared `markdown-editor.js` submit-sync fix — that fix covers the *shared* submit-sync listener path; this is bespoke per-page inline JS on a different `submit` listener that never routed through the shared fix. No existing analog for "bespoke inline JS reading a hidden EasyMDE textarea's live value" exists elsewhere in the codebase; implement by locating `markdown-editor.js`'s public instance-lookup mechanism (used internally by the shared submit-sync fix) and reusing that same lookup here. |

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/{DungeonMaster,ShopManagement,Shop,Shared,Characters,Contacts,Quest}`, `QuestBoard.Service/ViewModels/Shared`, `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs`
**Files scanned:** 12 (read directly this session) + `68-PATTERNS.md`/`69-PATTERNS.md` (prior-phase pattern maps, synthesized)
**Pattern extraction date:** 2026-07-10
