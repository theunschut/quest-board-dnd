# Phase 69: Contact Fields - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-10
**Phase:** 69-contact-fields
**Areas discussed:** Index card Description, Notes editing UX (Add Note toolbar, note editor exclusivity)

---

## Scope check: how is this phase different from 67/68?

Before selecting areas, the user asked whether this phase differs meaningfully from the mechanical repeats in Phase 67 (Quest Rewards/Recap) and Phase 68 (Character Description/Backstory). Claude's answer: the read/write plumbing is identical repetition, but two things are new — CONTACTMD-01's requirement wording explicitly covers "Details/**Index**" (Character's CHARMD-01 only said "Details"), and Contact Notes are a collaborative, multi-instance, inline-toggle-edited list on one shared page rather than a single-owner field on a dedicated Create/Edit page (a UI shape none of Quest/Character/Rewards/Recap have).

**User's response:** Chose to discuss both differences ("Discuss both") after initially asking to reconsider scope via free text.

---

## Index card Description

| Option | Description | Selected |
|--------|-------------|----------|
| Add plain-text snippet (recommended) | Mirrors Phase 66's Quest board-card precedent (D-06): strip Markdown syntax, show a short plain-text teaser. | |
| Leave Index unchanged | Description stays Details-only; treat CONTACTMD-01's "Details/Index" wording as satisfied because Index is the entry point that leads to Details. | ✓ |
| Add full rendered HTML, clamped | Show real formatted HTML inside each card, height-clamped via CSS overflow. | |

**User's choice:** Leave Index unchanged.

**Notes:** The user pushed back on the initial framing, pointing out Character's Index (Phase 68) already shows no Description/Backstory preview, and asking why Contacts would need an exception. Claude agreed there was no structural reason — only a wording difference in REQUIREMENTS.md ("Details/Index" vs. Character's "Details" only) — and that Character's actual behavior (no card preview) is the real precedent to follow. Locked as D-01 in CONTEXT.md, with an explicit note that this deliberately does not literally satisfy the roadmap's "Index" wording as a per-card-render requirement.

---

## Notes editing UX

### Add Note toolbar visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Always show toolbar (recommended) | Matches every other field in this milestone — same treatment as Quest/Character forms. | ✓ |
| Show toolbar only on focus | Keeps the Notes section visually lighter when scrolling past it. New interaction pattern not used elsewhere in the app. | |

**User's choice:** Always show toolbar.

### Note editor exclusivity

| Option | Description | Selected |
|--------|-------------|----------|
| Keep independent | No behavior change from today — multiple notes' edit forms can be open at once. | |
| Auto-collapse to one at a time | Opening a new note's editor cancels/closes any other currently-open note editor. | ✓ (after reconsideration) |

**User's choice:** Auto-collapse to one at a time.

**Notes:** Claude's first-pass recommendation ("keep independent") was justified in the AskUserQuestion option text partly by "small trusted group (~17 users)." The user corrected this directly — the group is 25 members and still growing, and cited standing project guidance that scale must never be the justification for a design choice either way. Claude re-grounded the recommendation on merit alone (Notes are collaborative-by-design — any group member can edit any note, no ownership gate — so independent editing preserves a real concurrent-use capability, not just tolerance of clutter at small scale) and re-presented "keep independent" as still recommended on that basis.

The user then asked how hard auto-collapse would be to implement. Claude explained it's cheap: the existing per-note Cancel button/handler already hides a note's edit form and can be reused; the one new piece is resetting each note's EasyMDE instance to its original saved value (not just hiding the DOM), requiring a small note-id → EasyMDE-instance lookup that `markdown-editor.js` doesn't currently have. Given the low implementation cost, the user chose auto-collapse instead. Locked as D-03 in CONTEXT.md, with the mechanism (reuse Cancel logic, add an instance registry) noted as Claude's Discretion for exact implementation.

---

## Claude's Discretion

- Exact markup/CSS restructuring away from `<p>` wrappers for Description and each note's text, where `Html.Markdown()`'s block-level output requires it (follows the `<div>`-wrapper precedent from Phase 66/67/68).
- Per-note-unique element `id`/`FieldName` scheme for `_MarkdownEditor.cshtml` instances inside the Notes `@foreach` loop (the partial's `elementId` is derived from `FieldName`, which would collide across notes without a per-note-unique value).
- Whether `markdown-editor.js`'s eager DOMContentLoaded initialization needs to change for Notes specifically (each note's Edit form starts `display: none`, a known EasyMDE/CodeMirror hidden-container sizing-bug source) — lazy-init-on-first-Edit-click vs. eager-init-then-refresh-on-show is left to research/planning.
- Order of implementation (Description vs. Notes, desktop vs. mobile).

## Deferred Ideas

None — no scope creep surfaced during this discussion.
