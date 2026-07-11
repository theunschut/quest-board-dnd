---
phase: 69-contact-fields
fixed_at: 2026-07-10T00:00:00Z
review_path: .planning/phases/69-contact-fields/69-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
---

# Phase 69: Code Review Fix Report

**Fixed at:** 2026-07-10T00:00:00Z
**Source review:** .planning/phases/69-contact-fields/69-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (WR-01 through WR-05; IN-01 out of scope for this run)
- Fixed: 5
- Skipped: 0

## Fixed Issues

### WR-01: Contact Note Markdown headings are illegible on the desktop Details view (mobile-only fix, desktop untouched)

**Files modified:** `QuestBoard.Service/wwwroot/css/contacts.css`
**Commit:** f3c1fda3
**Applied fix:** Added a `.modern-card-body .note-item h1..h6` override (dark text, no text-shadow) scoped to the desktop-only `contacts.css` stylesheet, mirroring the existing mobile fix in `contact-detail.mobile.css`. Placed the rule in `contacts.css` rather than the shared `markdown-content.css` because `.note-item` is a Contacts-only class — this keeps the fix isolated from the Quest/Character consumers of `markdown-content.css`, which was verified to have no other `.note-item` usages in the codebase.

### WR-02: Mobile Contact Note Markdown links keep dark-background styling against the light note background

**Files modified:** `QuestBoard.Service/wwwroot/css/contact-detail.mobile.css`
**Commit:** a146a7d6
**Applied fix:** Added `.contact-detail-card .note-item a` to the existing scope-out selector list (alongside `p`, `li`, `span:not(.badge)`, `h1`-`h6`) so note links also get `color: inherit` / `text-shadow: none` instead of the catch-all pale-gold parchment styling.

### WR-03: Reopening a previously canceled note editor never calls `codemirror.refresh()`

**Files modified:** `QuestBoard.Service/Views/Contacts/Details.cshtml`, `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml`
**Commit:** 3a98641d
**Applied fix:** Added an `else` branch to the existing `if (!noteEditors.has(noteId))` lazy-init check in both the desktop and mobile note-edit click handlers, calling `noteEditors.get(noteId).instance.codemirror.refresh()` when the editor was already constructed. Confirmed `initMarkdownEditor` returns the raw EasyMDE instance (which exposes `.codemirror`), so the fix's property access is valid. Applied identically to both files since they share the same script structure.

### WR-04: Preview fetch does not check `response.ok` before injecting the response body as HTML

**Files modified:** `QuestBoard.Service/wwwroot/js/markdown-editor.js`
**Commit:** a1d34848
**Applied fix:** Added a `response.ok` check in the first `.then()` of the preview fetch chain that throws on non-2xx responses, which is caught by the existing `.catch()` fallback (`'Preview failed to load.'`). This is shared infrastructure used by Quest, Character, and Contact fields alike — the change only tightens error handling for all consumers uniformly and does not alter behavior for successful (2xx) responses, so no consumer-specific regression risk.

### WR-05: Submit-sync fix depends on an explicit `type="submit"` attribute and silently no-ops for implicit-submit buttons

**Files modified:** `QuestBoard.Service/wwwroot/js/markdown-editor.js`
**Commit:** 330a4424
**Applied fix:** Broadened the submit-control selector from `button[type="submit"], input[type="submit"]` to `button, input[type="submit"]`, then added a guard that skips a `<button>` only when its (spec-defaulted) `.type` property is not `"submit"` — correctly treating a bare `<button>` with no `type` attribute as an implicit submit control per the HTML spec. Verified against all current shared-editor consumers (Quest, Character, Contacts): every existing submit button already declares `type="submit"` explicitly (still matched), and every existing non-submit button in the same `<form>` scope as a markdown textarea (e.g. Contact's `note-cancel-btn`) declares `type="button"` explicitly (still correctly skipped by the guard). No behavior change for any current consumer; the fix only prevents a future regression. `node -c` syntax check passed on the modified file.

## Skipped Issues

### IN-01: EasyMDE's live Preview pane doesn't mirror heading styling from the saved page

**File:** `QuestBoard.Service/wwwroot/css/markdown-content.css:67-77`
**Reason:** Out of scope for this run — `fix_scope` is `critical_warning`, which excludes Info-severity findings. Not attempted; left for a future `--fix` run with `fix_scope: all` or manual follow-up.
**Original issue:** `.markdown-content h1..h6` styling (font-size scale, color, line-height) is never duplicated onto `.editor-preview`, so EasyMDE's live Preview tab shows default preview typography for headings instead of the size/color the heading will actually render at once saved.

---

_Fixed: 2026-07-10T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
