# Phase 69: Contact Fields - Research

**Researched:** 2026-07-10
**Domain:** Mechanical repeat of the Phase 66/67/68 Markdown editor/rendering pattern, applied to a per-note-multi-instance, inline-toggle-edited list (not a single dedicated Create/Edit page like every prior field)
**Confidence:** HIGH (all core findings verified by direct read of the actual repo files; one external claim — the CodeMirror hidden-container sizing bug — is CITED from the library's own issue tracker, not independently reproduced this session)

## Summary

This phase is 90% a mechanical repeat of Phase 66/67/68's shipped pattern: reuse `_MarkdownEditor.cshtml` + `MarkdownEditorViewModel` + `markdown-editor.js` + `POST /markdown/preview` + `Html.Markdown()` verbatim, wire Contact Description onto the 4 write forms + 2 Details reads, and swap Notes' `<p style="white-space:pre-wrap;">` for `Html.Markdown()`. No new C# infrastructure, no new packages, no new CSS class families.

The genuinely new work is entirely in the Notes surface, because Notes are edited inline on the Details page itself (not a separate Create/Edit route like every prior field), rendered N-at-a-time inside one `@foreach`. Direct code reads resolved all three unknowns CONTEXT.md flagged:

1. **Per-note element IDs collide with POST binding today.** `_MarkdownEditor.cshtml` derives its `id` attribute from the same `FieldName` string used for the `name` attribute. But `ContactsController.AddNote`/`EditNote` bind `ContactNoteViewModel viewModel` from a bare (unprefixed) `Text` form field — confirmed by direct read of both the controller and the current plain `<textarea name="Text">` markup. Renaming the field name to `Text_{note.Id}` to fix the id collision would silently break server-side model binding (`viewModel.Text` would come back empty, tripping the `[Required]` validator). The fix is a **small additive change**: give `MarkdownEditorViewModel` a new optional `ElementId` property that overrides the id derivation while `FieldName`/`name` stays `"Text"` for every note. This is the one piece of shared infrastructure this phase must touch — not purely a new call site.
2. **EasyMDE inside `display:none`.** Confirmed via direct read: nothing in this codebase today initializes CodeMirror inside a hidden container — the closest-looking precedent (`Quest/Manage.cshtml`'s collapsed sections) only renders pre-rendered `Html.Markdown()` output there, never an editor. The CodeMirror-in-`display:none` sizing bug is a real, well-documented upstream issue; the safest fix given this app's lazy-init-friendly architecture is to **not create the EasyMDE instance until the note's Edit button is first clicked** (sidesteps the bug entirely — no `.refresh()` timing to get right) rather than eager-init-then-refresh.
3. **D-03 registry.** Lazy-init-on-first-click (point 2) naturally gives a place to store the created instance: key a `Map` by the note's existing `data-note-id` attribute (already present on `.note-item`, confirmed — no new attribute needed). Auto-collapse walks that same DOM structure to find any other note whose edit form is currently visible, reverts its EasyMDE value to the note's original saved text, and hides it before opening the newly clicked note.

Also confirmed by direct read: `Create.Mobile.cshtml`'s Description textarea is structurally identical to desktop's (same plain `<textarea asp-for="Description">`, no `_MarkdownEditor` wiring yet) — no surprises there. And Contact's Details page will need to load EasyMDE's CDN assets + `markdown-editor.js` for the first time on a **read/Details page** (via the existing `Views/Quest/_QuestFormScripts.cshtml` loader partial, already reused verbatim by Character's write forms) — every prior field kept EasyMDE confined to Create/Edit pages only; Contact Notes' inline-editing-on-Details breaks that assumption.

**Primary recommendation:** Add `ElementId` to `MarkdownEditorViewModel` (id override, `FieldName`/`name` stays `"Text"` for POST binding); lazy-init each note's EasyMDE instance on first Edit-button click (never eagerly on `DOMContentLoaded`) and store it in a `Map` keyed by `note.Id`; drive D-03 auto-collapse off that same map plus the existing `data-note-id`/`.note-edit-form` DOM structure. Everything else in this phase is a byte-for-byte repeat of the shipped Phase 66/67/68 pattern.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Markdown toolbar / cursor insertion | Browser / Client | — | EasyMDE/CodeMirror, pure client-side text manipulation, already locked infra (Phase 66) |
| Preview rendering | Browser / Client (trigger) | API / Backend (render) | JS `previewRender` POSTs to `/markdown/preview`; actual Markdown→HTML conversion happens server-side via `IMarkdownService` — guarantees preview == saved output |
| Per-note editor instance lifecycle (lazy-init, auto-collapse) | Browser / Client | — | Pure DOM/JS state; no server round-trip needed to decide which editor is open |
| Description/Note persistence | API / Backend | Database / Storage | `ContactsController` actions bind `ContactViewModel`/`ContactNoteViewModel`, `ContactService` persists via `ContactRepository` — unchanged by this phase |
| Markdown sanitization + HTML rendering | API / Backend | — | `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)`, reused verbatim; the only XSS-relevant boundary in this phase |
| Read-side display (Details/Index) | Frontend Server (SSR) | — | Razor view calls `Html.Markdown()` at render time; no client-side re-render needed |

## User Constraints

<user_constraints>

### Locked Decisions

**Locked at milestone-research / Phase 66 level (not re-discussed, carried forward):**
- Toolbar, preview mechanism, touch targets, CDN+SRI loading: identical to Phase 66 — reuse `_MarkdownEditor.cshtml`, `MarkdownEditorViewModel`, `markdown-editor.js`'s `initMarkdownEditor`, and `POST /markdown/preview` verbatim.
- Read-side rendering: `Html.Markdown()` helper, calling `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)`.
- Paragraph-break hint (EDITOR-05): same info-icon + tooltip pattern, already baked into `_MarkdownEditor.cshtml`.
- Pre-wrap cleanup: both Description's and each note's inline `style="white-space: pre-wrap;"` (desktop + mobile) get removed as a companion edit. `.markdown-content { white-space: normal }` (Phase 67, global) already handles doubled-spacing once wrapped in that class — no new CSS needed for that part.
- Per-note independent rendering (CONTACTMD-02's core constraint): already satisfied structurally today — each `ContactNote` renders inside its own `<div class="note-item">` via `@foreach`, calling `Html.Markdown()` per-note. No design decision needed; preserved by construction as long as the per-note loop shape isn't changed.

**D-01 — Index card Description:** Contact Index cards (desktop `.contact-card`, mobile `.contact-member-row`) do **not** gain a Description preview. Confirmed by direct read: zero Description references on `Index.cshtml`/`Index.Mobile.cshtml`, stays that way. Matches Character's CHARMD-01 precedent (no roster preview either). "Index" in CONTACTMD-01 is satisfied by Index being the click-through entry point to Details, not by inline preview text on every card.

**D-02 — Add Note toolbar:** The always-visible "Add Note" textarea (top of Notes list, desktop + mobile) shows the full 6-button toolbar + Preview immediately — no progressive-disclosure/on-focus variant.

**D-03 — Notes editor exclusivity (deliberate behavior change from today):** Opening a note's inline Edit form auto-collapses (cancels: hide + revert to original saved text) any other currently-open note Edit form on the same page. Only one note editor open at a time going forward. Rationale: UX-clutter reduction only — does not restrict which notes different users across different sessions can edit; the collaborative editing model (any group member can edit any note, no ownership gate) is unaffected. Mechanism is Claude's Discretion (see below — resolved by this research).

### Claude's Discretion

- Exact markup/CSS restructuring of Description's and each note's `<p>` wrapper away from `<p>` where `Html.Markdown()`'s block-level output requires it — follow the `<div>`-wrapper precedent from Phase 66/67/68.
- Whether the Add Note textarea and each note's Edit textarea share one `MarkdownEditorViewModel`-driven partial invocation, or need per-instance handling given the `@foreach` loop's unique-id requirement — **resolved by this research: see "Unknown 1" below.**
- Whether `markdown-editor.js`'s eager-init-all behavior needs to change for Notes specifically — **resolved by this research: see "Unknown 2" below.**
- Order of implementation (Description vs. Notes, desktop vs. mobile) — no user preference expressed; sequence for planning convenience.

### Deferred Ideas (OUT OF SCOPE)

None — no scope creep surfaced during discussion. Both non-mechanical decisions (Index card, Notes editor exclusivity) were resolved as in-scope implementation choices, not deferred.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CONTACTMD-01 | Contact Description supports the Markdown editor and renders as formatted HTML on Contact Details/Index | Standard Stack + Code Examples (Description write/read wiring); D-01 confirms Index is satisfied by click-through, no card preview needed |
| CONTACTMD-02 | Each Contact Note supports the Markdown editor and renders independently as formatted HTML — one author's formatting never bleeds into another note | Unknowns 1–3 below (per-note `ElementId`, lazy-init, D-03 registry) + confirmation that per-note `Html.Markdown()` calls inside the existing `@foreach` already satisfy independent rendering structurally |

</phase_requirements>

## Standard Stack

No new libraries this phase. Everything is reused verbatim from the milestone-locked stack (Phase 65/66).

### Core (reused, unchanged)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Markdig | 1.3.2 | Server-side Markdown → HTML | Locked at milestone level (Phase 65), already installed |
| Ganss.Xss (HtmlSanitizer) | 9.0.892 | Sanitizes rendered HTML before display | Locked at milestone level (Phase 65), already installed |
| EasyMDE | 2.21.0 | Client-side Markdown textarea + toolbar + Preview UI | Locked at milestone level (Phase 66), loaded via CDN+SRI, no npm/local install |

### Alternatives Considered
None — this phase makes zero new library-selection decisions. All stack choices were made and verified in Phase 65/66's research.

**Installation:** N/A — no new packages. `dotnet build` will compile against the already-installed `Markdig`/`Ganss.Xss` NuGet packages; EasyMDE continues to load from the pinned `cdn.jsdelivr.net` URLs already present in `Views/Quest/_QuestFormScripts.cshtml`.

**Version verification:** Not re-run this session — no version changes proposed. Phase 66's research (`.planning/phases/66-.../66-RESEARCH.md`) verified EasyMDE 2.21.0 live against the CDN (HTTP 200) and ran the slopcheck package-legitimacy scan (`[OK]`) at that time; Phase 68's `68-SECURITY.md` re-confirmed "No new package installs... EasyMDE + FA v4-shims reused verbatim from Phase 66 (slopcheck `[OK]`, 2026-07-09)". `[VERIFIED: direct file read, .planning/phases/68-character-fields/68-SECURITY.md]`

## Package Legitimacy Audit

**Not applicable — this phase installs no new packages.** All dependencies (Markdig, Ganss.Xss, EasyMDE 2.21.0 CDN) were legitimacy-checked and locked in Phase 65/66 and re-confirmed with zero new installs in Phase 68. `[VERIFIED: direct file read, 68-SECURITY.md AR-68-04]`

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Contact Details page load (GET /Contacts/Details/{id})
  │
  ▼
ContactsController.Details → ContactService → ContactRepository
  │  (loads Contact + Notes, maps to ContactViewModel/ContactNoteViewModel)
  ▼
Details.cshtml / Details.Mobile.cshtml render:
  ├─ Description: @Html.Markdown(Model.Description)  ─┐
  │                                                     ├─► IMarkdownService.RenderToHtml(md, Web)
  └─ @foreach note in Model.Notes:                      │     → Markdig parse → HtmlSanitizer (Web profile)
       ├─ view text:  @Html.Markdown(note.Text)  ───────┘     → sanitized HTML string
       └─ hidden edit form: _MarkdownEditor partial
             (FieldName="Text", ElementId=$"Text_{note.Id}")
  │
  ▼ (client, after DOMContentLoaded)
_QuestFormScripts.cshtml loads EasyMDE CDN + markdown-editor.js
  │  (NEW for a Details/read page — every prior field only loaded this on Create/Edit)
  ▼
markdown-editor.js DOMContentLoaded loop:
  - initializes the always-visible Add-Note textarea eagerly
  - SKIPS textareas inside .note-edit-form (still display:none at load)
  │
  ▼ (user clicks a note's Edit button)
note-edit-btn click handler:
  1. auto-collapse any OTHER open .note-edit-form (D-03):
       for each open form → editorMap.get(otherNoteId).value(originalText) → hide
  2. if editorMap has this note's id → just show the form (already initialized)
     else → initMarkdownEditor(textarea, token) → store in editorMap.set(noteId, instance)
  3. show this note's .note-edit-form
  │
  ▼ (user types, clicks Preview)
EasyMDE previewRender → POST /markdown/preview → MarkdownController
  → IMarkdownService.RenderToHtml(plainText, Web) → returns HTML → injected into preview pane
  │
  ▼ (user clicks Save)
POST /Contacts/EditNote (name="Text", not "Text_{id}" — CodeMirror syncs back to the
  underlying <textarea> via its own fromTextArea() submit listener before the browser submits)
  → ContactsController.EditNote(id, contactId, ContactNoteViewModel viewModel)
  → binds viewModel.Text from the bare "Text" field → ContactService.UpdateNoteAsync
  → redirect back to Details (full page reload, note now shows updated rendered HTML)
```

### Recommended Project Structure

No new files/folders — this phase only modifies existing views and one shared ViewModel/partial:

```
QuestBoard.Service/
├── ViewModels/Shared/
│   └── MarkdownEditorViewModel.cs      # MODIFIED — add optional ElementId property
├── Views/Shared/
│   └── _MarkdownEditor.cshtml          # MODIFIED — elementId derivation uses ElementId if set
├── Views/Contacts/
│   ├── Create.cshtml / Create.Mobile.cshtml   # MODIFIED — Description → _MarkdownEditor
│   ├── Edit.cshtml / Edit.Mobile.cshtml       # MODIFIED — Description → _MarkdownEditor
│   └── Details.cshtml / Details.Mobile.cshtml # MODIFIED — Description + Notes → Html.Markdown()
│                                               #            + per-note _MarkdownEditor + lazy-init JS
└── wwwroot/js/
    └── markdown-editor.js              # MODIFIED — skip-hidden-on-eager-init behavior (see Unknown 2)
```

### Unknown 1 (resolved): Per-note-unique element IDs without breaking POST binding

**What:** `_MarkdownEditor.cshtml` currently derives BOTH the `name` and `id` attribute from the single `Model.FieldName` string (`elementId = Model.FieldName.Replace('.', '_')`). For every prior field (Quest Description, Character Backstory, etc.) this was safe because each page renders exactly one instance of that field. Contact Notes render N instances of the same field name (`"Text"`) on one page.

**Why `FieldName` itself cannot become `"Text_{note.Id}"`:** `[VERIFIED: direct file read, QuestBoard.Service/Controllers/Contacts/ContactsController.cs:299,327]`
```csharp
public async Task<IActionResult> AddNote(int contactId, ContactNoteViewModel viewModel, ...)
public async Task<IActionResult> EditNote(int id, int contactId, ContactNoteViewModel viewModel, ...)
```
Both actions bind `viewModel` with no `[Bind(Prefix=...)]` — ASP.NET Core's default complex-type binder resolves this against the **bare, unprefixed** form field name. The current markup confirms this: `<textarea name="Text">@note.Text</textarea>` (no `viewModel.Text` prefix) is what's actually posted today and works. `ContactNoteViewModel.Text` also carries `[Required]`. If a note's editor textarea posted as `name="Text_5"` instead, the binder would find no `Text` field, `viewModel.Text` would bind to its default (`string.Empty`), `[Required]` would fail, `ModelState.IsValid` would be false, and `EditNote` would silently redirect back showing "Note text is required" — even though the user typed real content. This is a genuine, easy-to-miss data-loss bug if `FieldName` is naively made per-note-unique.

**Fix — add a new optional `ElementId` property to `MarkdownEditorViewModel`:**
```csharp
// QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs
public class MarkdownEditorViewModel
{
    public string FieldName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? Placeholder { get; set; }

    // NEW: overrides the derived DOM id when multiple instances of the same FieldName
    // render on one page (e.g. Contact Notes' per-note inline editors). Leave null for
    // every existing single-instance call site — falls back to the current behavior.
    public string? ElementId { get; set; }
}
```
```cshtml
@* QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml *@
@{
    var elementId = Model.ElementId ?? Model.FieldName.Replace('.', '_');
}
```
**Call sites:**
```cshtml
@* Add Note (single instance, top of list) — unchanged shape, ElementId omitted (defaults to "Text") *@
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Text",
       Value = null,
       Label = "Add a Note",
       Required = true,
       Placeholder = "Add a note about this contact..."
   }); }

@* Per-note Edit form, inside @foreach — FieldName stays "Text" for correct POST binding,
   ElementId made unique per note for DOM/label correctness *@
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Text",
       ElementId = $"Text_{note.Id}",
       Value = note.Text,
       Label = "Edit Note",
       Required = true
   }); }
```
This is a 2-line change to shared infrastructure plus straightforward call-site parameters — not a per-page fork, and it does not touch any existing call site's behavior (every other field passes `ElementId = null` implicitly, preserving today's derivation exactly).

### Unknown 2 (resolved): EasyMDE must not initialize while its container is `display:none`

**What goes wrong (upstream, well-documented):** CodeMirror computes gutter/line width from the container's rendered layout at construction time. If the container is `display:none`, that computation reads zero width, producing a broken/zero-width editor that doesn't fix itself until an explicit `.refresh()` call after the container becomes visible. `[CITED: github.com/codemirror/codemirror5 issues #61 "codemirror is empty when inside element with display:none", #5985 "Editor is bugged if created in a hidden div"]` — confirmed via WebSearch this session, not independently reproduced against this app's exact EasyMDE 2.21.0 build.

**Confirmed via direct read: this exact scenario is new to this codebase.** `Quest/Manage.cshtml`'s Bootstrap-`collapse` sections (the closest-looking "hidden at page load" precedent, cited in CONTEXT.md's canonical refs) only render **pre-rendered `Html.Markdown()` output** inside the collapsed div — never an EasyMDE-wrapped `<textarea>`. No existing view in this app has ever put a `.markdown-editor-textarea` inside a `display:none` ancestor at `DOMContentLoaded` time. `markdown-editor.js`'s current loop (`document.querySelectorAll('.markdown-editor-textarea').forEach(...)`) has therefore never needed to handle this case.

**Recommendation: lazy-init on first reveal, not eager-init-then-refresh.** Two options exist; lazy-init is preferred:
- **Option A (recommended) — lazy-init on first Edit-button click.** Never call `initMarkdownEditor` for a note's textarea until the moment its container becomes visible. The editor is always constructed while `display: block`, so the sizing bug never triggers — no `.refresh()` timing to get right, no risk of missing a resize event. This also directly solves Unknown 3 (the D-03 registry needs a natural place to create-and-store instances, and "first click" is exactly that place).
- Option B (eager-init-all, then call `.codemirror.refresh()` on `show`) — works per the upstream fix, but requires remembering to call `.refresh()) at exactly the right moment (the existing `.note-edit-btn` click handler) and offers no benefit over Option A for this app (Notes' edit forms are never needed until clicked, so there's no eager-init use case like "the field is likely to be edited immediately" that would justify paying the extra work).

**Concrete change to `markdown-editor.js`'s eager loop — skip anything not currently visible, let the revealer init it:**
```javascript
// Generic, not Contacts-specific: skip any textarea that isn't visible yet (offsetParent is
// null for display:none ancestors). Whatever code later reveals it is responsible for calling
// initMarkdownEditor at that point -- this keeps the eager loop reusable for any future
// hidden-until-interaction editor, not just Contact Notes.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.markdown-editor-textarea').forEach(function (textarea) {
        if (textarea.offsetParent === null) return; // hidden — caller must lazy-init on reveal
        initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
    });
});
```
The Add Note textarea (always visible) is unaffected — `offsetParent !== null` for it, so it still eager-inits exactly as today.

### Unknown 3 (resolved): D-03 auto-collapse instance registry

**Design:** A single `Map` (keyed by note id, using the `.note-item`'s existing `data-note-id="@note.Id"` attribute — confirmed already present, no new attribute needed) holding `{ instance, originalText }` for every note whose editor has been lazily created so far on this page load.

```javascript
// Contacts/Details.cshtml's own @section Scripts (page-specific, not markdown-editor.js —
// this is Contacts' own inline-toggle-edit orchestration, same file/pattern as the existing
// .note-edit-btn/.note-cancel-btn handlers already there today).
const noteEditors = new Map(); // noteId -> { instance, originalText }

function collapseNote(noteItem) {
    const noteId = noteItem.dataset.noteId;
    const entry = noteEditors.get(noteId);
    if (entry) {
        entry.instance.value(entry.originalText); // revert visible CodeMirror content
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
        // D-03: collapse every OTHER currently-open note editor first.
        document.querySelectorAll('.note-item').forEach(function (other) {
            if (other !== noteItem && other.querySelector('.note-edit-form').style.display !== 'none') {
                collapseNote(other);
            }
        });

        if (!noteEditors.has(noteId)) {
            // First time this note's Edit button has been clicked this page load —
            // container is about to become visible, so init happens AFTER the display
            // toggle below to guarantee CodeMirror never measures a display:none box.
        }

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
Note: `textarea.value` at the moment of first-init is exactly the server-rendered `@note.Text` (nothing has edited it yet) — safe to capture as `originalText` right there, no separate data attribute needed.

### Anti-Patterns to Avoid
- **Making `FieldName` per-note-unique to solve the id collision.** Breaks server-side model binding silently (see Unknown 1) — the bug only surfaces as a confusing "Note text is required" validation error on save, not a compile-time or obvious runtime failure.
- **Eager-initializing every note's EasyMDE instance on `DOMContentLoaded` and hoping it "just works."** Will produce broken/zero-width editors on first reveal per the upstream CodeMirror issue — lazy-init avoids the entire class of bug rather than requiring a correctly-timed `.refresh()` call.
- **Reusing `_QuestSection.cshtml` or any other single-item partial pattern for Notes.** Not applicable here — Notes already has its own working `@foreach`/inline-toggle structure; only the textarea internals change.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cursor-position Markdown insertion (Bold/Italic/Heading/List/Link/Blockquote) | Custom `selectionStart`/`selectionEnd` text-wrapping JS | EasyMDE's built-in toolbar actions | Already correct, already locked, zero marginal cost |
| Toolbar-disable-during-preview | Manual `disabled` toggling | EasyMDE's `disabled-for-preview` CSS class | Automatic, already verified functionally real |
| Textarea-to-form sync on submit | Manual `input`/`change` listener | CodeMirror's built-in `fromTextArea()` auto-wired `submit` listener | Already handled; a redundant manual sync risks double-processing |
| Note-editor visible/hidden state tracking | A separate `openNoteId` boolean flag | Query `.note-edit-form`'s own `style.display` at collapse time | One less piece of state to keep in sync; the DOM is already the source of truth |
| Markdown → HTML conversion + XSS sanitization | Any new parser/sanitizer call | `Html.Markdown()` / `IMarkdownService.RenderToHtml(..., Web)` | Single sanitizing choke point for the whole app (RENDER-01/02); a second call site risks divergence |

**Key insight:** every UI mechanic this phase needs (insertion, preview-disable, textarea sync, per-note independent rendering) is already solved by either the locked EasyMDE dependency or the existing per-note `@foreach` structure. The only genuinely new code is the `ElementId` plumbing and the lazy-init/registry JS — both are thin, additive, and narrowly scoped.

## Common Pitfalls

### Pitfall 1: CSS specificity leak — CodeMirror/Preview text tinted gold-with-shadow inside `.modern-card`/`.contact-form-card`
**What goes wrong:** Site-wide `.modern-card p/li/span/small { color: #F4E4BC !important; }` (site.css) and `.contact-form-card p/span:not(.badge)/legend { color: #F4E4BC !important; }` (contact-form.mobile.css) both match CodeMirror's internal syntax-highlighting `<span>`s and the Preview pane's injected `<p>`/`<li>`, tinting the raw editing surface gold instead of black-on-white.
**Why it happens:** These catch-all rules were written for read-only card text, before any card ever contained a live text editor.
**Already fixed, app-wide, before this phase started:** `markdown-editor.css`'s `.CodeMirror.CodeMirror.CodeMirror`/`.editor-preview.editor-preview.editor-preview` triple-class specificity bump (landed during Phase 68, confirmed by direct read of the current file — `QuestBoard.Service/wwwroot/css/markdown-editor.css:34-40`) already outranks any single `:not()`-qualified catch-all like `.contact-form-card span:not(.badge)` — this is a global CSS file loaded in both `_Layout.cshtml` and `_Layout.Mobile.cshtml`, so it automatically covers Contact's write forms too. **No new CSS needed for this pitfall in Phase 69** — verify visually during UAT rather than re-deriving the fix. `[VERIFIED: direct file read, QuestBoard.Service/wwwroot/css/markdown-editor.css]`

### Pitfall 2: Markdown heading color assumes a dark-overlay box that Contact's cards don't always have
**What goes wrong:** `.markdown-content h1-h6 { color: #F4E4BC; text-shadow: ...; }` (markdown-content.css) assumes every Markdown field renders inside a dark-overlay box like `.quest-description-box`. Contact Description renders directly inside `.card-body.modern-card-body` (desktop) / `.contact-detail-card` (mobile) — neither is a dark-overlay box.
**Already resolved for the desktop case:** `markdown-content.css`'s `.modern-card-body > .markdown-content h1-h6 { color: #1a1a1a; text-shadow: none; }` override (landed Phase 67, WR-01 fix) is a **direct-child** selector that matches Contact Details desktop's structure exactly the same way it matches Character/Quest Details desktop (all three render `Html.Markdown()`'s output as a direct child of `.card-body.modern-card-body`). **No new desktop CSS needed.**
**Mobile case — same as Character, already an accepted precedent, not a regression:** `.contact-detail-card` has no `.markdown-content h1-h6` override at all (confirmed by direct read — matches `.character-detail-card`'s identical gap, byte-for-byte, in `character-detail.mobile.css`). Character's Phase 68 review did not flag this as a defect for mobile Character Details — headings render pale gold with dark shadow directly against the semi-transparent glass card, consistent with the rest of that card's text. Contact mobile will render identically. **Treat as an accepted app-wide pattern for glass-card mobile detail pages, not a new bug to fix in this phase** — but re-verify visually during UAT since CONTEXT.md explicitly asked for this to be re-checked, and it's worth confirming the glass-card-over-dark-page contrast still reads fine with real heading content (not just paragraph text). `[VERIFIED: direct file read, QuestBoard.Service/wwwroot/css/{markdown-content.css, contact-detail.mobile.css, character-detail.mobile.css}]`

### Pitfall 3: Notes' desktop card already handles the note-item/card catch-all distinction; mobile CSS is already Notes-aware
**What goes wrong (potential, not actual):** One might expect the same `.modern-card p { color: #F4E4BC !important; }` catch-all to make Notes' rendered `<p>`/`<li>` illegible against `.note-item`'s near-white background (`background: rgba(0, 0, 0, 0.03)`, contacts.css:214-216).
**Why this is actually fine:** `.note-item`'s plain-text `<p class="note-view-text">` *already* renders gold-with-shadow via this exact rule TODAY, before this phase — it's the established, working, pre-existing look for note text on desktop (not a Markdown-migration side effect). Migrating to `Html.Markdown()` preserves the same `<p>`-inside-`.modern-card` structure, so the visual treatment is unchanged. **No new CSS needed on desktop.**
**Mobile is already prepared for this migration, confirmed by direct read of the CSS comment itself:** `contact-detail.mobile.css:84-98` explicitly scopes the parchment catch-all OUT of `.note-item` (`color: inherit !important`) specifically because "note items keep their own bordered/plain treatment (not parchment)... mirrors the desktop note-item convention" — this file was already written anticipating Notes rendering real HTML content, unlike Character's mobile CSS which needed a WR-04 follow-up fix (`li` added to its catch-all) after the fact. **No new mobile CSS needed for `<p>`/`<span>` inside `.note-item`.** One residual gap: `.contact-detail-card .note-item` scoping does not explicitly list `li` (only `p`/`span:not(.badge)`) — if a note author uses a Markdown list, verify during UAT that `<li>` renders with sane inherited color rather than falling through to some other unintended rule; if it looks wrong, add `li` to the existing scope-out selector (same one-line fix Character's WR-02 already established as the pattern). `[VERIFIED: direct file read, QuestBoard.Service/wwwroot/css/{contacts.css, contact-detail.mobile.css}]`

### Pitfall 4: Notes' inline Edit forms currently have no `_MarkdownEditor`/`asp-for` wiring at all
**What goes wrong:** Unlike every other field migrated in this milestone (which already used `asp-for="Description"` etc. before migration), Notes' textareas are bare `<textarea name="Text">@note.Text</textarea>` with no model binding attribute. It's easy to assume the "swap textarea for partial" step is as simple as it was for Character/Quest — but Notes also need the `ElementId` wiring (Unknown 1) and the lazy-init JS change (Unknown 2/3) layered on top, which no prior field needed.
**How to avoid:** Treat Notes as its own implementation unit distinct from Description — Description truly is a mechanical repeat (dedicated Create/Edit page, single instance), Notes is not (inline, multi-instance, needs the three resolved unknowns above).

### Pitfall 5: Contact Details is the first Details/read page in this app to ever load EasyMDE
**What goes wrong:** Every prior field's read view (Quest Details, Character Details, QuestLog Details) never needed `Views/Quest/_QuestFormScripts.cshtml` (the EasyMDE CDN + `markdown-editor.js` + antiforgery-token loader) because editing always happened on a separate Create/Edit page. Contact Notes' inline editing lives directly on `Details.cshtml`/`Details.Mobile.cshtml`, so those two views must now include that loader partial too — something easy to miss since "Details pages don't need editor scripts" was true for every prior phase.
**How to avoid:** Add `@{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }` to Contacts' `Details.cshtml`/`Details.Mobile.cshtml` `@section Scripts` blocks (same call Character's write forms already make, confirmed reusable across controllers since it lives in `Views/Quest/` but Razor's view-location search still resolves it via the explicit `~/` root-relative path). The name is Quest-specific but the content is fully generic (guarded internally by a `form[action*="/Quest/"]` selector that safely no-ops on non-Quest forms, confirmed by direct read) — reusing it as-is matches the precedent Character already established rather than renaming it in this phase.
**Warning sign to check during UAT:** every visitor to any Contact's Details page now downloads EasyMDE's CDN JS/CSS (previously zero-cost on read pages), since the Add Note form is visible to any authenticated group member regardless of `CanManage`. Not a blocker — just a first-time-for-this-app cost worth being aware of, not something to "fix."

## Code Examples

### Description write-form wiring (mechanical repeat — Character's exact pattern)
```cshtml
@* Source: Views/Characters/Edit.cshtml:117-124 (shipped, Phase 68) — apply identically to
   Views/Contacts/Create.cshtml, Create.Mobile.cshtml, Edit.cshtml, Edit.Mobile.cshtml *@
@{ await Html.RenderPartialAsync("_MarkdownEditor", new MarkdownEditorViewModel
   {
       FieldName = "Description",
       Value = Model.Description,
       Label = "Description",
       Required = false,
       Placeholder = "Brief contact description..."
   }); }
```
Requires `@using QuestBoard.Service.ViewModels.Shared` added to each of the 4 Contact write-form views (confirmed absent today by direct read, same gap Character had before Phase 68).

### Description read-side wiring (mechanical repeat — Character's exact pattern)
```cshtml
@* Source: Views/Characters/Details.cshtml:148-154 (shipped, Phase 68) *@
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
Drop the inline `style="white-space: pre-wrap;"` entirely (not moved) — `.markdown-content { white-space: normal }` already neutralizes the need for it.

### Notes — see "Unknown 1/2/3" in Architecture Patterns above for the full, Contacts-specific code (per-note `ElementId`, lazy-init, D-03 registry). Those are the load-bearing new code for this phase; everything else is the Description pattern shown above.

## State of the Art

Not applicable — no external ecosystem shifts since Phase 66/67/68 (same milestone, days apart). No ecosystem research needed.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EasyMDE 2.21.0's CodeMirror sizing bug (initializing inside `display:none`) reproduces in exactly the way the linked GitHub issues describe, for this app's specific EasyMDE build/config | Unknown 2 | Low — even if the exact symptom differs, lazy-init-on-first-click sidesteps the entire class of "hidden-container init" bugs by construction (the editor is never constructed while hidden), so the recommendation holds regardless of the bug's precise mechanics. Worth a quick manual click-test during UAT (open a note editor, confirm the toolbar/textarea render at full width immediately, not just after a resize). |

**If this table is short:** every other claim in this research was verified via direct read of the actual repository files (controllers, views, CSS, prior-phase review/pattern docs) during this session, not training-data recall.

## Open Questions

1. **Should `Views/Quest/_QuestFormScripts.cshtml` be renamed to something ecosystem-neutral, given it's now reused by Character, Contact, and future DM Profile/Shop forms?**
   - What we know: it's already reused verbatim by Character (Phase 68) without renaming; its content is fully field/entity-agnostic (guarded Quest-only logic no-ops elsewhere).
   - What's unclear: whether a rename is worth the diff noise mid-milestone vs. deferring to a dedicated cleanup phase.
   - Recommendation: reuse as-is in this phase (matches established precedent); leave renaming as a backlog/cleanup item, not blocking for Phase 69.

2. **Does a Markdown list (`- item`) inside a Contact Note render with correct contrast on mobile, given `.contact-detail-card .note-item`'s scope-out rule doesn't explicitly list `li`?**
   - What we know: the scope-out rule (`color: inherit !important`) covers `p`/`span:not(.badge)` but not `li`; without an explicit rule, `<li>` will inherit color from its nearest ancestor with a set `color`, which should resolve to a sane dark color given `.note-item`'s light background — but this wasn't independently verified against a rendered browser this session.
   - What's unclear: the exact inherited color chain in practice.
   - Recommendation: verify visually during UAT; if `<li>` looks wrong, the fix is a one-line addition (`li` to the existing scope-out selector list), matching the exact pattern Character's WR-02 already established.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Markdig (NuGet) | Server-side Markdown parsing | ✓ (already installed, Phase 65) | 1.3.2 | — |
| Ganss.Xss / HtmlSanitizer (NuGet) | HTML sanitization | ✓ (already installed, Phase 65) | 9.0.892 | — |
| EasyMDE (CDN) | Client-side editor | ✓ (verified live in Phase 66, re-confirmed no changes since) | 2.21.0, SRI-pinned | — |
| Font Awesome 6 + v4-shim | Toolbar icons | ✓ (already loaded app-wide) | 6.4.0 | — |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (`QuestBoard.IntegrationTests`, `QuestBoard.UnitTests`) |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (WebApplicationFactory-based) |
| Quick run command | `dotnet test --filter FullyQualifiedName~Contacts` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CONTACTMD-01 | Description renders as Markdown on Details, edits via toolbar | manual-only (UAT) | — | N/A — view-rendering behavior; `IMarkdownService.RenderToHtml` itself is already unit-tested (Phase 65), this phase only adds call sites |
| CONTACTMD-02 | Each note renders independently, no formatting bleed between notes | manual-only (UAT) | — | N/A — structural guarantee (separate `Html.Markdown()` call per note) already exists pre-phase; verify visually with 2+ notes containing unclosed formatting (e.g. one note with `**bold` no closing) |
| CONTACTMD-02 (D-03) | Opening one note's editor auto-collapses any other open editor | manual-only (UAT) | — | N/A — pure client-side interaction, no automated test infra for this in the codebase today (matches Character phase precedent: zero new test files added) |
| CONTACTMD-01/02 (access control regression) | `AddNote`/`EditNote`/`DeleteNote`/`Details` authorization unchanged | automated (existing) | `dotnet test --filter FullyQualifiedName~ContactsControllerIntegrationTests` | ✅ — `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` already covers `AddNote_AnyGroupMember_CanAddNoteToVisibleContact` and hidden-contact visibility; re-run to confirm no regression from the view changes, no new tests needed for this phase |

### Sampling Rate
- **Per task commit:** `dotnet build` (view/CS compile check) + `dotnet test --filter FullyQualifiedName~Contacts`
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`; manual UAT click-through covering: Description toolbar/preview/render, 2+ notes with independent formatting, D-03 auto-collapse with unsaved-text-discard, mobile viewport for both.

### Wave 0 Gaps
None — existing test infrastructure (`ContactsControllerIntegrationTests.cs`) already covers the access-control surface this phase's view changes touch; no new automated coverage is being added, matching the precedent Phase 68 (Character) established (zero new test files, verification via manual UAT + existing regression suite).

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Unchanged — this phase touches no auth surface |
| V3 Session Management | no | Unchanged |
| V4 Access Control | no (regression-only) | Existing `[Authorize]`/group-scoping on `ContactsController` actions unchanged; view-only changes don't alter who can call which action |
| V5 Input Validation | yes | `ContactNoteViewModel.Text` `[Required]`/`[StringLength(2000)]`, `ContactViewModel.Description` `[StringLength(2000)]` — server-side validation unchanged by this phase; client-side `maxlength` is dropped by adopting `_MarkdownEditor.cshtml` (already true for every prior migrated field — not a new gap) |
| V6 Cryptography | no | N/A |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stored XSS via Description/Note free-text | Tampering | `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)` (Markdig + HtmlSanitizer Web profile) — reused verbatim, no new server surface. Same mitigation already verified for Character (`68-SECURITY.md` T-68-02-I) and Quest (Phase 65 RENDER-01 unit tests). Verify no `Html.Raw()` bypass is introduced anywhere in this phase's view edits. |
| CSRF on `POST /markdown/preview` (Notes' per-note Preview buttons) | Spoofing | Endpoint already enforces `[Authorize]` + `[ValidateAntiForgeryToken]`; `window.markdownAntiforgeryToken` supplied via `_QuestFormScripts.cshtml`'s `Antiforgery.GetAndStoreTokens` call — same token now needs to be present on `Details.cshtml`/`Details.Mobile.cshtml` too (new for those two views, see Pitfall 5) |
| Preview/saved-output divergence | Repudiation / integrity | Preview and saved render both call the same `MarkdownRenderTarget.Web` pipeline (RENDER-02) — no divergence to exploit, unchanged by this phase |
| Note-editor id collision enabling one user's Save to silently overwrite the wrong note | Tampering (client-side data integrity, not server-exploitable) | Resolved by Unknown 1's `ElementId` fix — `name="Text"` stays constant so each `<form asp-action="EditNote">` posts to its own `id`/`contactId` hidden fields regardless of the textarea's DOM `id`; the POST target is determined by which `<form>` was submitted, not by the textarea's id, so this was never actually server-exploitable — but a duplicate-id DOM (if `ElementId` were skipped) would still break the `<label for>` association and could visually mislead a user about which note they're editing |

## Sources

### Primary (HIGH confidence — direct file reads this session)
- `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml`, `ViewModels/Shared/MarkdownEditorViewModel.cs`, `wwwroot/js/markdown-editor.js`, `Extensions/HtmlHelperExtensions.cs`
- `QuestBoard.Service/Views/Contacts/{Details,Details.Mobile,Create,Create.Mobile,Edit,Edit.Mobile,Index,Index.Mobile}.cshtml`
- `QuestBoard.Service/Views/Characters/{Details,Edit}.cshtml` (shipped Phase 68 analog)
- `QuestBoard.Service/Views/Quest/Manage.cshtml`, `Views/Quest/_QuestFormScripts.cshtml`
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (AddNote/EditNote/DeleteNote signatures)
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs`
- `QuestBoard.Service/wwwroot/css/{markdown-content.css, markdown-editor.css, site.css, contacts.css, contact-detail.mobile.css, contact-form.mobile.css, character-detail.mobile.css}`
- `.planning/phases/{66,67,68}-*/{CONTEXT,PATTERNS,REVIEW,SECURITY}.md`

### Secondary (MEDIUM confidence)
- none beyond the primary set above

### Tertiary (LOW confidence, flagged for UAT verification)
- CodeMirror 5 hidden-container sizing bug: github.com/codemirror/codemirror5 issues #61, #5985 — WebSearch this session, not independently reproduced against this app's exact build; mitigated by design (lazy-init sidesteps the bug class entirely, see Assumption A1)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new dependencies, all reused/locked from Phase 65/66, re-verified via direct file reads
- Architecture (Description): HIGH — byte-for-byte shipped analog exists (Character Phase 68)
- Architecture (Notes/Unknowns 1-3): HIGH — resolved via direct read of controller model-binding signature and current markup, not speculation
- Pitfalls: HIGH for CSS specificity findings (all confirmed by direct read of current CSS state, not assumed) — LOW only for the CodeMirror hidden-container bug's exact reproduction (mitigated by design choice, see Assumptions Log)

**Research date:** 2026-07-10
**Valid until:** 30 days (stable, same-milestone continuation — no external ecosystem dependency)
