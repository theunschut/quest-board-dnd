---
phase: 94-whole-app-ui-review-and-unification
verified: 2026-11-09T00:00:00Z
status: passed
score: 9/9 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
---

# Phase 94: Whole-App UI Review and Unification Verification Report

**Phase Goal:** Audit every page (desktop and mobile), get an operator-signed visual direction, build the foundation (tokens, layered cascade, shared components, all layouts, self-hosted vendor assets, dead dependencies removed), rewrite `.claude/ui-guidelines.md`, and append the area phases that move the remaining pages onto it.
**Verified:** 2026-11-09
**Status:** passed
**Re-verification:** No, initial verification

ROADMAP lists no Success Criteria array and no requirement IDs (`Requirements: TBD`), so the must-haves below are derived from the goal sentence and the lead's check list.

## Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Page-by-page audit exists, with a signed-off direction | VERIFIED | `94-AUDIT.md` plus 7 area reports (`94-AUDIT-*.md`); G1 sign-off record in `94-UI-SPEC.md` (Direction A, Shop (a), 2026-11-01) |
| 2 | Token and layer foundation exists and is wired on all five layouts | VERIFIED | `app.css` declares `@layer vendor, tokens, base, components, pages, utilities`. Bootstrap and Font Awesome are imported into `layer(vendor)`. `_Layout`, `_Layout.Mobile`, `_Layout.GroupPicker`, `_Layout.Platform` and `_Layout.Platform.Mobile` each link app, tokens, base, components and shell, in that order. Desktop and GroupPicker layouts also link `legacy-pages.css`; the mobile layouts link `shell.mobile.css`. |
| 3 | `modern-card.css` and `site.css` are gone | VERIFIED | Neither file is on disk or tracked. The only reference is a guard test asserting the string is absent. `legacy-pages.css` is the renamed remainder. |
| 4 | Both shells have skip links, landmarks and named togglers | VERIFIED | All five layouts have `skip-link` to `#main-content`, a `<main id="main-content">` and a named `<nav>`. Desktop togglers carry `aria-label="Toggle navigation"`. Mobile opener and close buttons carry `aria-label`s ("Open/Close navigation menu"). |
| 5 | Toasts persist for errors and warnings | VERIFIED | `_Toasts.cshtml`: Error and Warning have `role="alert"` and `data-bs-autohide="false"`. Success and Info autohide at 5000 ms. `site.js` initialises `bootstrap.Toast`, which reads the data attributes. Partial is rendered in the desktop, mobile and Platform layouts. |
| 6 | Vendor assets self-hosted, dead dependencies removed | VERIFIED | `wwwroot/lib` holds bootstrap-5.3.8, fontawesome-6.7.2, cinzel, cropperjs, easymde and tagify. No CDN, jQuery or Google Fonts reference remains in views, CSS, JS or C# outside `wwwroot/lib`. |
| 7 | Guard tests exist and pass | VERIFIED | `dotnet build`: 0 errors. `dotnet test --filter FullyQualifiedName~UiFoundation`: 95 passed, 0 failed (9 `UiFoundation*` test classes). Full `dotnet test`: UnitTests 1300 passed. IntegrationTests 1751 passed, 11 skipped (SQL Server facts), 0 failed. No CLR flake hit. |
| 8 | `.claude/ui-guidelines.md` rewritten to record the system | VERIFIED | 288 lines covering tokens and layers, the card pattern and the layout-by-User-Agent note. No planning references in CSS or the guidelines. |
| 9 | Phases 95-101 are appended with operator approval recorded; every deferred item maps to an area phase or a separate fix | VERIFIED | ROADMAP has Phase 95 to 101 sections, each with Goal, Depends on and Scope, in the approved order. The 6 web phases cover 121 views and the email phase covers 10 templates. G2 approval ("Approved on 2026-11-09") is in `94-AUDIT.md` and `94-27-SUMMARY.md`. The "Deferred items and where each lands" table has no unmapped row. The two bug fixes (Shop create `GroupId`, `WallClockUnmovedTests` flake) are listed as separate fixes in the audit and `deferred-items.md`. |

**Score:** 9/9 truths verified (0 present, behavior-unverified)

## Requirements Coverage

ROADMAP has `Requirements: TBD`, and `REQUIREMENTS.md` maps nothing to Phase 94. The plans cite design-decision IDs (D-xx) only. Nothing is orphaned.

## WINDOWS.md register (phase 94 entries)

| Entry | State | Disposition |
|-------|-------|-------------|
| 9, 18, 21, 24, 25 | waived | Operator-accepted, as briefed |
| 12 | open | Operator-accepted; mapped to Quests, Characters and Shop in the deferred table |
| 27 | open | Operator-accepted scope proof (`site.js` aria sync plus two comment-only partials) |
| 22 | open | Mapped to the Shop phase (Phase 99) in the deferred table |
| 2, 3, 5 | open | Phase 84 and 85 items with no UI scope, listed as separate backlog in the audit |
| All other phase 94 entries | fixed | none |

## Anti-Patterns

No GSD or planning references were found in CSS or `ui-guidelines.md`. `git status` was clean before verification, so there is no uncommitted drift. The 623 page-level contrast failures (none on a layout, footer, toast or alert) and 6 overflow flags are owned by the area phases (95 to 100), as accepted by the operator.

## Human Verification

None outstanding. G1 sign-off (2026-11-01), the 94-24 readability review and the G2 cut approval (2026-11-09) are recorded as done by the operator.

## Gaps Summary

None. Minor note for housekeeping only: the register still carries WINDOWS 2, 3 and 5 as open; they are outside the UI scope and tracked as separate items.

---

_Verified: 2026-11-09_
_Verifier: Claude (gsd-verifier)_
