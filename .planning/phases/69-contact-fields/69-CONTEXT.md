# Phase 69: Contact Fields - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Mechanically repeat Phase 66's Markdown editor + rendering pattern onto Contact Description and Contact Notes. CONTACTMD-01, CONTACTMD-02 only — no new capabilities beyond what's already scoped by ROADMAP.md. The read/write plumbing (toolbar, Preview, `Html.Markdown()`, pre-wrap removal) is identical repetition of Phase 66/67/68. Two things are genuinely new in this phase, not present in any prior field-migration phase: Contacts is the only field so far where the requirement wording explicitly covers an Index list view that currently shows nothing of the field in question, and Contact Notes are the only field so far that's a collaborative, multi-instance, inline-toggle-edited list on one shared page rather than a single-owner field on a dedicated Create/Edit page.

</domain>

<decisions>
## Implementation Decisions

### Locked at milestone-research / Phase 66 level (not re-discussed, carried forward)
- **Toolbar, preview mechanism, touch targets, CDN+SRI loading:** identical to Phase 66 — reuse the shared `_MarkdownEditor.cshtml` partial, `MarkdownEditorViewModel`, `markdown-editor.js`'s `initMarkdownEditor`, and the `POST /markdown/preview` endpoint verbatim. No new editor infrastructure needed.
- **Read-side rendering:** `Html.Markdown()` helper, calling the same `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)` used for every prior field.
- **Paragraph-break hint (EDITOR-05):** same info-icon-next-to-label + Bootstrap tooltip pattern, same tooltip text — already baked into `_MarkdownEditor.cshtml` itself, nothing new to wire.
- **Pre-wrap cleanup:** both `Description`'s inline `style="white-space: pre-wrap;"` (Details desktop + mobile) and each note's `.note-view-text` inline `style="white-space: pre-wrap;"` (desktop + mobile) get removed as a companion edit — same precedent as every prior phase. `.markdown-content { white-space: normal }` (Phase 67, global) already handles the doubled-spacing mechanism once wrapped in that class; no new CSS needed for that part.
- **Per-note independent rendering (CONTACTMD-02's core constraint, and milestone research Pitfall 7):** already satisfied structurally today — `Details.cshtml`/`Details.Mobile.cshtml` render each `ContactNote` inside its own `<div class="note-item">` via a `@foreach`, calling `Html.Markdown()` per-note (never concatenating note text into one string). Migrating preserves this by construction as long as the per-note loop shape isn't changed. No design decision needed here — confirmed by direct code read, not just research inference.

### Index card Description
- **D-01:** Contact Index cards (desktop grid `.contact-card` in `Index.cshtml`, mobile list rows `.contact-member-row` in `Index.Mobile.cshtml`) do **not** gain a Description preview. Both currently show only Name/Town-City/Sub-location — confirmed by direct read, zero Description references on either. This stays unchanged.
- **Rationale (important for planner/verifier — this deliberately does not literally satisfy ROADMAP.md's success criterion #1 and REQUIREMENTS.md CONTACTMD-01's "Details/Index" wording as a literal per-card render requirement):** Character's equivalent requirement (CHARMD-01) only said "Details," and Character's roster Index genuinely has no Description/Backstory preview either (68-CONTEXT.md D: confirmed, no card/list preview) — establishing the actual precedent in this milestone. Contact's REQUIREMENTS.md wording differs only in text ("Details/Index" vs. "Details"), not in any stated design intent to add a new card surface. The user confirmed directly: "Index" in CONTACTMD-01 is satisfied by Index being the entry point that leads to Details (click-through), not by requiring inline preview text on every card. No plain-text-teaser mechanism (à la Phase 66 D-06's board-card treatment) is needed for this phase.

### Notes: Add Note toolbar
- **D-02:** The always-visible "Add Note" textarea (top of the Notes list on both desktop `Details.cshtml` and mobile `Details.Mobile.cshtml`) shows the full 6-button toolbar (Bold/Italic/Heading/List/Link/Blockquote) + Preview immediately, exactly like every other field's write form in this milestone. No progressive-disclosure/on-focus variant — stay consistent with the established pattern rather than introduce a new interaction style unique to this one field.

### Notes: editor exclusivity
- **D-03:** Opening a note's inline Edit form auto-collapses (cancels) any other currently-open note Edit form on the same page — **a deliberate behavior change from today's independent-toggle behavior** (today, clicking Edit on multiple notes independently opens each one, no mutual exclusivity). Only one note editor is open at a time going forward.
  - **Rationale:** not scale-based — this is a UX-clutter reduction (avoids multiple full toolbars stacked on screen at once when browsing/editing several notes in one sitting), while the underlying collaborative editing model (any group member can edit any note, no ownership gate) is otherwise unaffected — auto-collapse only prevents multiple editors being *visually open on the same client at the same time*, it does not restrict which notes different users across different sessions can edit.
  - **Mechanism (Claude's Discretion for exact implementation):** reuse the existing per-note Cancel logic (`.note-cancel-btn` handler already hides the edit form and restores the view state) — when a new note's Edit button is clicked, run every other open note's existing cancel path first (hide + `form.reset()` to discard unsaved typed text), then open the newly-clicked note's editor. Once EasyMDE wraps each note's textarea, canceling also needs to sync the visible CodeMirror content back to the original saved text (`.value(originalText)` on that note's EasyMDE instance), not just hide the container — otherwise a canceled-but-not-reset editor would show stale unsaved text if reopened later in the same page load. This requires a small lookup (e.g., a `Map` from note id → EasyMDE instance) that doesn't exist yet in `markdown-editor.js`; `initMarkdownEditor`'s current DOMContentLoaded loop doesn't retain returned instances anywhere.

### Claude's Discretion
- Exact markup/CSS restructuring of Description's `<p class="mb-0" style="white-space: pre-wrap;">@Model.Description</p>` and each note's `<p class="mb-0 mt-2 note-view-text" style="white-space: pre-wrap;">@note.Text</p>` away from `<p>` where `Html.Markdown()`'s block-level output (headings, lists) requires it — follow the `<div>`-wrapper precedent already established in Phase 66/67/68.
- Whether the Add Note textarea and each note's Edit textarea share one `MarkdownEditorViewModel`-driven partial invocation pattern, or need per-instance handling given the note-edit forms are inside a `@foreach` loop (each needs a unique element id — `_MarkdownEditor.cshtml`'s `elementId` is derived from `FieldName`, which will collide across notes unless the field name is made unique per note, e.g. `Text_{note.Id}` or similar). This is exactly the kind of per-instance-id problem the planner/research should resolve; not a user-facing decision.
- Whether `markdown-editor.js`'s existing DOMContentLoaded-eager-init-all-`.markdown-editor-textarea` behavior needs to change for Notes specifically (each note's Edit form starts `display: none` in the DOM at page load — EasyMDE/CodeMirror initializing inside a hidden container is a known sizing-bug source). Lazy-initializing each note's editor only when its Edit button is first clicked (rather than eagerly on page load) is one way to solve both this and the D-03 instance-registry need at once, but the exact approach (lazy-init vs. eager-init-then-refresh-on-show) is an implementation decision for research/planning, not something the user needs to weigh in on.
- Order of implementation (Description vs. Notes, desktop vs. mobile) — no user preference expressed; sequence for planning convenience.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 66 (pattern this phase mechanically repeats)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-CONTEXT.md` — full decision set (D-01 through D-09), especially D-06 (plain-text-stripped card teaser precedent — explicitly NOT applied to Contact Index per D-01 above, but the mechanism exists if ever revisited) and D-08/D-09 (paragraph-break hint placement/copy)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-07-SUMMARY.md` — CSS specificity-bump pattern for editor/Preview text nested inside `.modern-card`-styled cards (Contact Details uses `.modern-card`/`.contact-detail-card` — re-check whether the same leak applies), and the accepted 44px-tall/~30-34px-wide mobile touch-target deviation

### Phase 67 (most recent repeat; established pre-wrap/specificity lessons)
- `.planning/phases/67-remaining-quest-fields-email-templates/67-REVIEW.md` — CR-01 (CSS specificity collision once plain-text content routes through `Html.Markdown()` for the first time) and WR-01 (pale/invisible headings when `.markdown-content` renders directly inside a light card with no dark-overlay box) — both worth re-checking against Contact Details' actual card styling

### Phase 68 (immediately prior phase; establishes the Index "no card preview" precedent this phase's D-01 follows)
- `.planning/phases/68-character-fields/68-CONTEXT.md` — confirms Character's Index/roster list shows no Description/Backstory snippet, the direct precedent for this phase's D-01 decision

### Milestone Research
- `.planning/research/SUMMARY.md` (line 64, "Two more pitfalls...") — explicitly names the Contact Notes per-note independent-rendering pitfall this phase must preserve (already satisfied structurally, see Decisions above)

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — CONTACTMD-01, CONTACTMD-02 (this phase's requirements)
- `.planning/ROADMAP.md` — Phase 69 goal, success criteria, dependency on Phase 66

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Views/Shared/_MarkdownEditor.cshtml` + `ViewModels/Shared/MarkdownEditorViewModel.cs` (Phase 66) — the shared EasyMDE toolbar/preview partial. Note its `elementId` is derived from `Model.FieldName` (`.Replace('.', '_')`) — needs a per-note-unique `FieldName` when used inside the Notes `@foreach` loop (see Claude's Discretion above).
- `POST /markdown/preview` endpoint (`Controllers/MarkdownController.cs`, Phase 66) — reused verbatim for both Description's and every note's Preview toggle; no new endpoint needed.
- `Html.Markdown()` HtmlHelper extension (Phase 66) — reused verbatim for all read-side rendering in this phase.
- `wwwroot/js/markdown-editor.js`'s `initMarkdownEditor(textarea, antiforgeryToken)` — currently loops over every `.markdown-editor-textarea` on `DOMContentLoaded` and does not retain returned EasyMDE instances anywhere. This phase's D-03 (auto-collapse) needs a small instance registry this file doesn't currently have.
- `.markdown-content { white-space: normal }` (Phase 67, `markdown-content.css`) — already solves doubled-spacing globally; no new CSS needed for that mechanism.

### Established Patterns
- Mobile view resolution is automatic (`MobileDetectionMiddleware` + `MobileViewLocationExpander`) — no controller branching needed for any of this phase's view edits.
- `Contact.cs` (Domain model): `Description` is `[StringLength(2000)]`, nullable `string?`, no `[Required]`. `ContactNote.Text` is `[StringLength(2000)]`, non-nullable but defaults to `string.Empty` — matches the `Required = false` pattern already used elsewhere, except Notes' textarea currently has no `asp-for`/`MarkdownEditorViewModel` wiring at all (plain `<textarea name="Text">`), unlike every other field migrated so far which already used `asp-for`.
- Notes use inline JS toggle for edit mode (`.note-edit-btn`/`.note-cancel-btn` handlers in a `<script>` block at the bottom of `Details.cshtml`/`Details.Mobile.cshtml`), not a dedicated Edit page/action-redirect like every other field in this milestone. This is the one structurally different write-UI shape in the whole migration.

### Integration Points
- **Description write forms:** `Views/Contacts/Create.cshtml:52-56`, `Create.Mobile.cshtml` (not yet read but same shape expected), `Edit.cshtml:60-64`, `Edit.Mobile.cshtml` — existing plain `<textarea asp-for="Description">` blocks (no `_MarkdownEditor.cshtml` yet) to wire with the editor partial.
- **Description read views:** `Views/Contacts/Details.cshtml:89-96` (`<p class="mb-0" style="white-space: pre-wrap;">@Model.Description</p>`), `Details.Mobile.cshtml:77-84` (identical shape) — swap for `Html.Markdown()`.
- **Notes write form (Add):** `Details.cshtml:106-114`, `Details.Mobile.cshtml:91-99` — `<textarea name="Text" ...>` (no `asp-for`, posts to `AddNote` action) — needs the editor partial wired in, with a fresh/empty value each time (this is a create form, not bound to an existing model value).
- **Notes write form (Edit, per-note):** `Details.cshtml:147-155`, `Details.Mobile.cshtml:132-140` — one `.note-edit-form` per note inside the `@foreach`, each `<textarea name="Text">@note.Text</textarea>`, `display: none` until its Edit button is clicked. Needs per-note-unique element ids (see Claude's Discretion) and the D-03 auto-collapse wiring.
- **Notes read (per-note display):** `Details.cshtml:145` / `Details.Mobile.cshtml:130` — `<p class="mb-0 mt-2 note-view-text" style="white-space: pre-wrap;">@note.Text</p>` inside the `@foreach` — swap for `Html.Markdown(note.Text)`, still one call per note (preserves the independent-rendering constraint).
- **Confirmed out of scope:** `Views/Contacts/Index.cshtml`/`Index.Mobile.cshtml` (no Description preview per D-01), `Edit.cshtml`/`Edit.Mobile.cshtml`'s Notes section (Notes aren't edited via the Contact Edit form — only via Details' inline per-note forms), no email template references any Contact field.

</code_context>

<specifics>
## Specific Ideas

The user pushed back twice during this discussion, both times correctly:
1. Challenged the Index-card-preview framing directly — pointed out Character's Index already sets the "no card preview" precedent, and asked why Contacts would need an exception. That challenge is what turned D-01 from an open question into a confirmed, precedent-consistent decision.
2. Corrected a scale-based justification ("~17 users") used in the note-editing-exclusivity recommendation — group size is actually 25 and growing, and per standing project guidance scale should never be the justification either way. The recollected, merit-only justification (Notes are collaborative-by-design; auto-collapse only affects on-screen clutter for one client, not who can edit what) is what's captured in D-03.

</specifics>

<deferred>
## Deferred Ideas

None — no scope creep surfaced during this discussion. Both non-mechanical decisions (Index card, Notes editor exclusivity) were resolved as in-scope implementation choices, not deferred to a future phase.

### Reviewed Todos (not folded)
None — no pending todos existed for this phase (`todo.match-phase 69` returned 0 matches).

</deferred>

---

*Phase: 69-contact-fields*
*Context gathered: 2026-07-10*
