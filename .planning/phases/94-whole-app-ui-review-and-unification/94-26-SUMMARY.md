---
phase: 94-whole-app-ui-review-and-unification
plan: 26
subsystem: ui-foundation
tags: [verification, audit, scope-proof, rendered-contrast, area-phase-cut]
requires:
  - phase: 94-24
    provides: "final sweep and alerts-dark sweep of the finished foundation"
  - phase: 94-25
    provides: "settled tree (guidelines, guard test) so the checks read final code"
provides:
  - "94-AUDIT.md sections: Final verification, Overflow, Rendered contrast, Scope, After the foundation (Cascade), Dead dependencies final outcome"
  - "Refreshed area-phase cut with post-foundation numbers, deferred-items map and append descriptions (feeds the 94-27 G2 gate)"
affects: [94-27]
tech-stack:
  added: []
  patterns:
    - "Shell checks scripted once per layout and User-Agent (skip link, main focus, named toggler, errors, requests, overflow); rendered contrast measured with the corrected glass probe, not the shared sweep probe"
key-files:
  created: []
  modified:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md
decisions:
  - "Rendered contrast section uses the corrected probe (623 rows) instead of the shared sweep probe (838 rows), because the shared probe over-reports multi-layer glass"
  - "Contrast tables list element, ratio and pages only; no sample text, so no member names or emails reach the repository"
  - "The scope proof is recorded as it is, not forced to the plan's 19-path list: two extra comment-only partials and one site.js addition are left for the operator to accept"
metrics:
  tasks: 3
  commits: 3
  completed: 2026-11-09
status: complete
actuals:
  tokens: 15200
  tasks: 3
  commits: 3
---

# Phase 94 Plan 26: Final verification, scope proof and refreshed area-phase cut Summary

All five layouts pass the final shell checks under both User-Agents, the 623 remaining rendered-contrast rows and six overflow flags are assigned to their area phases, and the area-phase cut now carries post-foundation numbers with every deferred item mapped; the scope proof holds except for three recorded exceptions the operator should accept.

## Commits

| Task | Commit | What |
|------|--------|------|
| 1 (tracer) | `87ba5bf1` | `shell-final.mjs` written and run on `/quests` under both User-Agents; "Final verification" section with the two `_Layout` rows |
| 2 | `221542e0` | All five layouts in the table; Overflow, Rendered contrast and Scope sections; Cascade after-state and vendor bump differences; Dead dependencies final outcome |
| 3 | `7d575c87` | Area-phase cut recounted per phase, deferred-items map, separate fixes, one append description per phase |

## Results

- **Shell checks (live app, port 8026):** skip link first and visible, Enter lands on `main#main-content`, togglers and close buttons named and operable by keyboard (desktop bar proven at 800px, where it collapses), 0 console errors, 0 page errors, 0 failed requests, 0 third-party requests, no overflow, on `_Layout`, `_Layout.Mobile`, `_Layout.GroupPicker` (both User-Agents), `_Layout.Platform` and `_Layout.Platform.Mobile`.
- **Overflow:** no flag on a layout or the GroupPicker. Six page flags, each owned: `Quest/Details` and `QuestLog/Index` (320 desktop, Quests), `ContactCategoryManagement/Manage` (320, Characters), `Platform/Group/Index` (320, Admin and Platform; the table and button row, every overflowing element inside `main`), `Events/Details` (390 phone, Events), `Shared/AccessDenied` (390 phone, Account).
- **Rendered contrast:** corrected probe over all 118 records on the current code: 3,920 samples, 623 failing rows on 59 records, none on a layout, footer, toast or alert. Per phase: Quests 123, Account 12, Events 78, Characters 97, Shop 59, Admin and Platform 254. Compared with `before` for failures present in both: the only ones that measure worse are the phone required asterisk (4.34 to 3.36:1, WINDOWS 12), already known. Two rows with no match (`code` on `Admin/EmailStats`, phone `Move/Index` `p`) exist in `before` under the shared probe, so they are probe differences; the frozen 8001 baseline cannot reproduce those two pages (Access Denied for the audit accounts there).
- **Cascade after-state:** 38 of the 45 utility fights remain, all in page sheets; the foundation sheets carry no literal colour; 47 unlayered sheets hold 460 hex, 870 `rgb()` and 561 `!important` for the area phases to take to zero.
- **Cut:** 10 subsections (seven phases including Email templates, deferred-items map, adjustments, order). No deferred item is unmapped.

## Deviations from Plan

### Scope proof is not exactly the plan's allowed set (reported, not resolved by editing the list)

The plan's `diff` against the 19-path allowed set does not come out empty:

1. **[Plan allowed-set gap]** `QuestBoard.Service/Views/Shared/_Calendar.cshtml` and `QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml` also changed, in commit `ba750d65` (94-24): Razor comments only, rewritten because they named the retired `modern-card.css` span rule. Not rendered, no markup change. All 19 allowed views did change (none missing).
2. **[Plan truth "site.js unchanged" not met]** `QuestBoard.Service/wwwroot/js/site.js` carries a 13-line offcanvas `aria-expanded` sync added by 94-22 (`c1c48e1b`), after the existing `hydrateWallClockTimes()` call. It touches no date handling; the accessibility bar needs it. The other protected paths (Emails, Domain, Repository, `HtmlHelperExtensions.cs`) are empty, so nothing writes to `Events` or `Quests`.

Both are written into the audit's Scope section for the operator. The automated Task 2 verify (exact 19-path `diff`) therefore fails by design of those two facts; the remaining checks of that verify (empty protected diff except `site.js`, the three section headings) pass.

### Auto-fixed Issues

**1. [Rule 1 - Bug] `shell-final.mjs` rejected the GroupPicker on the phone and the Platform desktop toggler**
- **Found during:** Task 2 first run. The picker has its own layout (no `mobile-layout` class, no menu), and the desktop Platform bar still expands at 1024px.
- **Fix:** picker exempt from the mobile-layout assertion; toggler proven at 800px. Scratch script only.

### Overflow row reporting

The Platform desktop row for `/platform/Group/Index` shows the page's own overflow; the same layout on `/platform/Group/Create` is clear (`who-overflows.mjs`: 0 elements outside `main`), recorded as two rows.

## Ledger items for WINDOWS.md (not edited here)

- **Closable:** 7 (Cinzel hinting, accepted by the operator at 94-11), 8 (shared probe over-reports glass: this plan re-measured everything with the corrected probe, 623 against 838), 4 (stale ROADMAP grep criterion), 12 (desktop part a probe artefact; the phone asterisk is mapped to Quests, Characters and Shop in the cut).
- **Stay open, mapped:** 22 (Shop theme through `data-theme` and `--qb-board-photo`, Shop phase), 24 (waived; Quests and Admin and Platform phases).
- **Stay open, outside the UI work:** 2 (`Request.Scheme` call sites in `AccountController`), 3 (real-device calendar subscription check, deployment), 5 (SQL Server relational coverage of the one-shot predicate).
- **New, for the operator to accept:** the two comment-only partials and the `site.js` addition above (kind `deviation`).

## Separate fixes (outside the area cut, listed in the audit)

Shop item creation missing `GroupId` (`ShopManagementController.Create`), and the `WallClockUnmovedTests` 20:00 UTC flake (test-only).

## Tests

`dotnet build`: 0 errors. Full `dotnet test`: UnitTests 1,300 passed; IntegrationTests 1,751 passed, 11 skipped (SQL Server migrator facts), 0 failed. No flake hit. Port 8026 stopped; 8000 and 8001 untouched; no wait loops left.

## Known Stubs

None.

## Threat Flags

None. The only repository change is `94-AUDIT.md` (counts, element names and scratch paths; no member names, emails or user text, T-94-50). Scope proof against the baseline commit is recorded (T-94-51).

## Self-Check: PASSED

`94-AUDIT.md` carries the sections `## Final verification`, `## Overflow`, `## Rendered contrast`, `## Scope` and the refreshed cut; commits `87ba5bf1`, `221542e0` and `7d575c87` exist on the branch; `shell-final.mjs` is at `~/qb-ui-review/phase-94/plans/94-26/`.
