# Phase 69: Contact Fields - Pattern Map

**Mapped:** 2026-07-10
**Files analyzed:** 9 (2 shared infra + 4 Description write-form views + 2 Details read views with Notes + 1 shared JS)
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|-----------------|---------------|
| `QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs` | model (view model) | transform | itself (Phase 66/68 shipped version) | exact — additive property only |
| `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` | component (Razor partial) | request-response | itself (Phase 66/68 shipped version) | exact — additive id-override branch only |
| `QuestBoard.Service/wwwroot/js/markdown-editor.js` | utility (client init) | event-driven | itself (Phase 66/68 shipped version) | exact — additive visibility guard only |
| `QuestBoard.Service/Views/Contacts/Create.cshtml` (Description field) | component (write form) | CRUD | `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124` | exact |
| `QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml` (Description field) | component (write form) | CRUD | `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124` (mobile shape mirrors desktop `<textarea asp-for>`) | role-match |
| `QuestBoard.Service/Views/Contacts/Edit.cshtml` (Description field) | component (write form) | CRUD | `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124` | exact |
| `QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml` (Description field) | component (write form) | CRUD | `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124` | role-match |
| `QuestBoard.Service/Views/Contacts/Details.cshtml` (Description read + Notes read/write) | component (read + inline-edit) | request-response / event-driven | `QuestBoard.Service/Views/Characters/Details.cshtml:148-154` (Description read only — Notes has no analog anywhere in the app) | role-match (Description) / no-analog (Notes multi-instance editor) |
| `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml` (Description read + Notes read/write) | component (read + inline-edit) | request-response / event-driven | same as desktop | role-match (Description) / no-analog (Notes multi-instance editor) |

## Pattern Assignments

### `QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs` (model, transform)

**Analog:** itself — additive change confirmed by RESEARCH.md "Unknown 1"

**Current shape** (full file, 20 lines):
```csharp
namespace QuestBoard.Service.ViewModels.Shared;

public class MarkdownEditorViewModel
{
    public string FieldName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? Placeholder { get; set; }
}
```

**Required change:** add one nullable property, no other edits:
```csharp
    // Overrides the derived DOM id when multiple instances of the same FieldName render on
    // one page (e.g. Contact Notes' per-note inline editors). Leave null for every existing
    // single-instance call site -- falls back to the current FieldName-derived behavior.
    public string? ElementId { get; set; }
```
Every existing call site (Quest Description, Character Description/Backstory, Contact Description on Create/Edit) omits `ElementId` and is completely unaffected.

---

### `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` (component, request-response)

**Analog:** itself

**Current id derivation** (lines 3-7):
```cshtml
@{
    // Mirrors ASP.NET Core's own tag-helper id generation (IdAttributeDotReplacement, default
    // "_") so this computed id matches what asp-for would have produced for the same field name.
    var elementId = Model.FieldName.Replace('.', '_');
}
```

**Required change:** one-line fallback swap, everything else in the partial (label, tooltip, textarea, validation span, lines 9-20) stays byte-for-byte identical:
```cshtml
@{
    var elementId = Model.ElementId ?? Model.FieldName.Replace('.', '_');
}
```

---

### `QuestBoard.Service/wwwroot/js/markdown-editor.js` (utility, event-driven)

**Analog:** itself

**Current eager-init loop** (lines 61-65):
```javascript
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.markdown-editor-textarea').forEach(function (textarea) {
        initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
    });
});
```

**Required change:** skip anything not currently visible (Contact Notes' per-note edit-form textareas start `display:none`), so a later click handler can lazy-init them instead. `initMarkdownEditor` itself (lines 9-56, the EasyMDE constructor + `/markdown/preview` fetch wiring) is reused verbatim — do not touch it:
```javascript
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.markdown-editor-textarea').forEach(function (textarea) {
        if (textarea.offsetParent === null) return; // hidden -- caller must lazy-init on reveal
        initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
    });
});
```
The always-visible Add Note textarea is unaffected (`offsetParent !== null`), so it still eager-inits exactly as today.

---

### `QuestBoard.Service/Views/Contacts/{Create,Create.Mobile,Edit,Edit.Mobile}.cshtml` — Description field (component, CRUD)

**Analog:** `QuestBoard.Service/Views/Characters/Edit.cshtml:117-124` (shipped Phase 68)

**Current Contact markup to replace** (`Create.cshtml:52-56`, `Edit.cshtml:60-64` — identical shape on both, mobile variants match structurally):
```cshtml
<div class="mb-3">
    <label asp-for="Description" class="form-label">Description</label>
    <textarea asp-for="Description" class="form-control" rows="3" maxlength="2000" placeholder="Brief contact description"></textarea>
    <span asp-validation-for="Description" class="text-danger"></span>
</div>
```

**Target pattern** (copy verbatim, adjust `Placeholder` per Create vs. Edit as today):
```cshtml
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Description",
       Value = Model.Description,
       Label = "Description",
       Required = false,
       Placeholder = "Brief contact description..."
   }); }
```

**Required companion edit:** add `@using QuestBoard.Service.ViewModels.Shared` to the top of all 4 files (confirmed absent today — Character's Edit.cshtml has it at line 3).

**Scripts section:** all 4 files need `@{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }` added to `@section Scripts` if not already present — check each file; Create.cshtml/Edit.cshtml currently only load the cropper scripts, not `_QuestFormScripts.cshtml` (confirmed: neither file's `@section Scripts` block calls it today, unlike Character's Edit.cshtml line 197).

---

### `QuestBoard.Service/Views/Contacts/{Details,Details.Mobile}.cshtml` — Description read (component, request-response)

**Analog:** `QuestBoard.Service/Views/Characters/Details.cshtml:148-154`

**Current Contact markup to replace** (`Details.cshtml:89-96`, `Details.Mobile.cshtml:77-84` — identical shape):
```cshtml
@if (!string.IsNullOrEmpty(Model.Description))
{
    <p class="mb-0" style="white-space: pre-wrap;">@Model.Description</p>
}
else
{
    <p class="text-muted mb-0">No description provided.</p>
}
```

**Target pattern** (Character's shipped equivalent):
```cshtml
@if (!string.IsNullOrEmpty(Model.Description))
{
    <div class="mb-3">
        <label class="form-label fw-bold">Description</label>
        @Html.Markdown(Model.Description)
    </div>
}
else
{
    <p class="text-muted mb-0">No description provided.</p>
}
```
Note: Contact's card already has an `<h4>Description</h4>` header in `.card-header modern-card-header` (desktop) / `.contact-section-heading` (mobile) — unlike Character which puts the label inline. Preserve Contact's existing header structure; just drop the `<label>` duplication if it would be redundant, or keep it per Claude's Discretion in CONTEXT.md. Drop `style="white-space: pre-wrap;"` entirely (not moved) — `.markdown-content { white-space: normal }` already neutralizes the need for it.

---

### `QuestBoard.Service/Views/Contacts/{Details,Details.Mobile}.cshtml` — Notes (component, event-driven / request-response)

**Analog:** none in the codebase — this is genuinely new (per-note multi-instance inline editor). RESEARCH.md's "Unknown 1/2/3" (Architecture Patterns section) is the closest thing to a pattern and should be treated as load-bearing design, not merely descriptive.

**Current Add Note markup** (`Details.cshtml:106-114`, `Details.Mobile.cshtml:91-99` — identical shape, no `asp-for`, posts to `AddNote`):
```cshtml
<form asp-action="AddNote" method="post">
    <input type="hidden" name="contactId" value="@Model.Id" />
    <textarea name="Text" class="form-control" rows="3" maxlength="2000" placeholder="Add a note about this contact..."></textarea>
    <div class="d-flex justify-content-end mt-2">
        <button type="submit" class="btn btn-warning">
            <i class="fas fa-plus me-2"></i>Add Note
        </button>
    </div>
</form>
```
**Target:** replace the `<textarea>` with the `_MarkdownEditor` partial, `FieldName = "Text"`, `Value = null`, `ElementId` omitted (single instance — falls back to `"Text"`), `Required = true`, `Label = "Add a Note"`.

**Current per-note view+edit markup** (`Details.cshtml:120-156`, `Details.Mobile.cshtml:105-141` — identical, inside `@foreach (var note in Model.Notes)`):
```cshtml
<div class="note-item border rounded p-3 mb-3" data-note-id="@note.Id">
    ...
    <p class="mb-0 mt-2 note-view-text" style="white-space: pre-wrap;">@note.Text</p>

    <form asp-action="EditNote" method="post" class="note-edit-form" style="display: none;">
        <input type="hidden" name="id" value="@note.Id" />
        <input type="hidden" name="contactId" value="@Model.Id" />
        <textarea name="Text" class="form-control mt-2" rows="3" maxlength="2000">@note.Text</textarea>
        <div class="d-flex justify-content-end gap-2 mt-2">
            <button type="button" class="btn btn-sm btn-secondary note-cancel-btn">Cancel</button>
            <button type="submit" class="btn btn-sm btn-warning">Save</button>
        </div>
    </form>
</div>
```
**Target:**
1. View text: `<p class="mb-0 mt-2 note-view-text" style="white-space: pre-wrap;">@note.Text</p>` -> `<div class="mb-0 mt-2 note-view-text">@Html.Markdown(note.Text)</div>` (drop `<p>`, drop pre-wrap, one `Html.Markdown()` call per note — preserves CONTACTMD-02's independent-rendering constraint by construction).
2. Edit textarea -> `_MarkdownEditor` partial with `FieldName = "Text"` (unchanged, required for POST binding to `ContactNoteViewModel.Text`), `ElementId = $"Text_{note.Id}"` (required — see RESEARCH.md Unknown 1 for why `FieldName` itself must NOT become per-note-unique), `Value = note.Text`, `Required = true`, `Label = "Edit Note"`.

**Current page-bottom inline JS** (`Details.cshtml:180-200`, `Details.Mobile.cshtml:164-184` — byte-for-byte identical between desktop/mobile, independent-toggle, no exclusivity):
```javascript
document.querySelectorAll('.note-item').forEach(function (noteItem) {
    var editBtn = noteItem.querySelector('.note-edit-btn');
    var cancelBtn = noteItem.querySelector('.note-cancel-btn');
    var viewActions = noteItem.querySelector('.note-view-actions');
    var viewText = noteItem.querySelector('.note-view-text');
    var editForm = noteItem.querySelector('.note-edit-form');

    editBtn?.addEventListener('click', function () {
        viewText.style.display = 'none';
        viewActions.style.display = 'none';
        editForm.style.display = 'block';
    });

    cancelBtn?.addEventListener('click', function () {
        editForm.style.display = 'none';
        viewText.style.display = '';
        viewActions.style.display = '';
    });
});
```
**Target — replace with D-03 auto-collapse + lazy-init registry** (full design in RESEARCH.md "Unknown 3", reproduced here as the concrete pattern to implement; both `Details.cshtml` and `Details.Mobile.cshtml` need this identical script, matching their current byte-for-byte duplication convention):
```javascript
const noteEditors = new Map(); // noteId -> { instance, originalText }

function collapseNote(noteItem) {
    const noteId = noteItem.dataset.noteId;
    const entry = noteEditors.get(noteId);
    if (entry) {
        entry.instance.value(entry.originalText);
    }
    noteItem.querySelector('.note-edit-form').style.display = 'none';
    noteItem.querySelector('.note-view-text').style.display = '';
    noteItem.querySelector('.note-view-actions').style.display = '';
}

document.querySelectorAll('.note-item').forEach(function (noteItem) {
    const noteId = noteItem.dataset.noteId;
    const editBtn = noteItem.querySelector('.note-edit-btn');
    const cancelBtn = noteItem.querySelector('.note-cancel-btn');
    const editForm = noteItem.querySelector('.note-edit-form');
    const textarea = editForm.querySelector('.markdown-editor-textarea');

    editBtn?.addEventListener('click', function () {
        document.querySelectorAll('.note-item').forEach(function (other) {
            if (other !== noteItem && other.querySelector('.note-edit-form').style.display !== 'none') {
                collapseNote(other);
            }
        });

        noteItem.querySelector('.note-view-text').style.display = 'none';
        noteItem.querySelector('.note-view-actions').style.display = 'none';
        editForm.style.display = 'block';

        if (!noteEditors.has(noteId)) {
            const instance = initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
            noteEditors.set(noteId, { instance: instance, originalText: textarea.value });
        }
    });

    cancelBtn?.addEventListener('click', function () {
        collapseNote(noteItem);
    });
});
```
Requires `_QuestFormScripts.cshtml` (loads EasyMDE CDN + `markdown-editor.js` + antiforgery token) added to both Details views' `@section Scripts` — new for these two views, see Shared Patterns below.

---

## Shared Patterns

### Markdown editor scripts loader (new for Details pages)
**Source:** `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml` (already reused verbatim by Character's write forms — see `Views/Characters/Edit.cshtml:197`)
**Apply to:** `Contacts/Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` (if not already present), and — new for this phase — `Contacts/Details.cshtml`, `Details.Mobile.cshtml` (every prior field's read view never needed this; Contact Notes' inline-editing-on-Details is the first exception).
```cshtml
@section Scripts {
    @{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }
    <script> /* page-specific scripts, e.g. the note-editor registry above, go after this */ </script>
}
```
The partial's name is Quest-specific but its content is entity-agnostic (internally guarded, confirmed by direct read in RESEARCH.md) — resolves via the `~/` root-relative path from any controller.

### `Html.Markdown()` read-side rendering
**Source:** `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` (calls `IMarkdownService.RenderToHtml(md, MarkdownRenderTarget.Web)`)
**Apply to:** Contact `Description` (Details, Details.Mobile) and every `note.Text` (per-note, inside the `@foreach`, one call each — never concatenate note text into a single string).

### CSS — no new files/rules needed
Per RESEARCH.md Pitfalls 1-3, all CSS specificity issues (CodeMirror-inside-`.modern-card` gold-tint, heading-color-on-non-dark-box, `.note-item` catch-all scoping) are already resolved by existing global CSS (`markdown-editor.css`, `markdown-content.css`, `contact-detail.mobile.css`) landed in Phase 67/68. Only verify visually during UAT; do not add new CSS unless a genuine regression is observed (in which case follow the exact one-line `li`-added-to-scope-out-selector pattern Character's WR-02 established).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| Contact Notes per-note inline lazy-init + D-03 auto-collapse registry (`Details.cshtml`/`Details.Mobile.cshtml` inline `@section Scripts`) | component (multi-instance inline editor) | event-driven | First multi-instance-per-page Markdown editor in the app; no dedicated Create/Edit page precedent exists. RESEARCH.md's "Unknown 1/2/3" (reproduced above under Pattern Assignments) is the authoritative design — treat it as the pattern source since no shipped analog exists. |

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/{Contacts,Characters,Quest,Shared}`, `QuestBoard.Service/ViewModels/Shared`, `QuestBoard.Service/wwwroot/js`, `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`
**Files scanned:** 12 (read directly this session) + prior-phase CONTEXT/RESEARCH docs (66, 67, 68) already synthesized into 69-RESEARCH.md
**Pattern extraction date:** 2026-07-10
