---
phase: 94-whole-app-ui-review-and-unification
plan: 24
subsystem: ui-foundation
tags: [css, legacy-retirement, readability-sweep, guard-tests, alerts, heading-scale]
requires:
  - phase: 94-23
    provides: "legacy-pages.css and the retired global element, component and phone families"
  - phase: 94-19
    provides: "heading and strong type scale"
provides:
  - "modern-card.css deleted and unlinked from the four layouts; card text inherits from the card surface"
  - "Phone card-title step, --qb-fg-heading token, read-only plain-text and CR badge fixes"
  - "Alerts as the dark overlay panel with light status text and stripe"
  - "Full readability sweep `final` reviewed and approved by the operator"
affects: [94-25, 94-26, 94-27]
tech-stack:
  added: []
  patterns:
    - "Surface colour comes from the surface; a status colour on paper means ink, on a dark panel means the light text variant"
key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/wwwroot/css/base.css
    - QuestBoard.Service/wwwroot/css/tokens.css
    - QuestBoard.Service/wwwroot/css/mobile.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.IntegrationTests/Controllers/PlatformAreaLayoutTests.cs
    - QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationBaseGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationComponentGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Alerts stay dark (Direction A): overlay surface, light status variants for text, links and stripe"
  - "Status words as ink on paper surfaces (tables, rows) stays"
  - "Desktop menu collapsing to the hamburger below 1200px is accepted"
  - "Phone backdrop softness (900px WebP scaled about 1.4x) is accepted"
  - "Page links under 44px (quest titles on /quests, email links on /Platform/Users) are left to the area phases"
metrics:
  tasks: 3
  commits: 10
status: complete
actuals:
  tokens: 15600
  tasks: 3
  commits: 10
---

# Phase 94 Plan 24: Retire modern-card.css and review the full readability sweep Summary

modern-card.css is gone and every card now takes its text colour from its surface, with the full 118-page, two-User-Agent readability sweep reviewed and approved by the operator; alerts went back to Direction A's dark overlay panel with light status text and stripes (9.87:1 to 12.98:1).

## Tasks

| Task | What | Commits |
|------|------|---------|
| 1 | Removed the element-enumerated cream colour rules from modern-card.css | `5b1ca9a0` |
| 2 | Deleted modern-card.css, unlinked it from the four layouts, re-pinned PlatformAreaLayoutTests, ContactCategoryContrastGuardTests and LinkedBoardStaticGuardTests | `ba750d65` |
| 3 | Full sweep `final` against `before`, operator review, and the one change the review asked for | `2b32dab6`, `fab1f3fe`, `95ec8190`, `a8de5cf7`, `4a5fdc3e`, `f2cf6296`, `4e4df4da` |

Details of the sweep fixes (from the checkpoint report):

- Heading and strong scale landed in base.css with a guard (`2b32dab6`); the guard now reads every rule of a selector because `body` is declared twice (`fab1f3fe`).
- Phone card titles take the 20px step in components.css; the stopgap bold-headings rule left mobile.css (`95ec8190`).
- A real regression of the scale: with h4 to h6 at 16px/600, 42 phone rows of unconverted cards on plain glass measured 4.34:1. New token `--qb-fg-heading` (5:1 on plain glass) fixes it on the page sheets (`a8de5cf7`).
- `.form-control-plaintext` inherits colour (was 1.04:1 once the card-wide cream rule left) and `.cr-badge` text uses the heading token (`4a5fdc3e`).
- Sweep `final` against `before`: 5 flagged rows, none a real regression (date drift, active-board difference, one probe false positive). Failure rows fell from 2,435 to 838 over the manifest.

## Operator review (Task 3)

1. Alerts: keep dark. Switched from the parchment panel back to the dark panel (`4e4df4da`).
2. Status words as ink on paper surfaces: keep.
3. Desktop menu collapsing below 1200px: the hamburger is accepted.
4. Phone backdrop softness: accepted.
5. Page links under 44px left to the area phases: accepted.

No page was reported unreadable or unreachable; the review is approved with change 1.

## Alert change (decision 1)

`.alert` now sets `--bs-alert-bg: var(--qb-surface-overlay)` (the toast surface), text and link colour through `--qb-alert-text`, and the left stripe through `--qb-alert-accent`, all via `--bs-alert-*` and `--qb-alert-*` variables: no literal and no important flag. Each status variant maps both to its light text variant (made for dark surfaces; the solid fills only reach 2.3 to 3.06:1 on the overlay, so they are not used for the stripe). `.alert` left the paper block, so a status colour inside an alert maps to the light text variant while tables and rows keep status-as-ink.

Measured on the overlay `rgb(47, 27, 14)` on the live app (desktop and phone identical):

| Variant | Text | Heading | Link | Stripe |
|---------|------|---------|------|--------|
| success | 9.87 | 9.87 | 9.87 | 9.87 |
| danger | 10.05 | 10.05 | 10.05 | 10.05 |
| warning | 10.02 | 10.02 | 10.02 | 10.02 |
| info | 9.91 | 9.91 | 9.91 | 9.91 |
| primary | 12.98 | 12.98 | 12.98 | 11.99 |
| secondary / default | 12.98 | 12.98 | 12.98 | 10.88 |

Guards: new fact `ComponentsCss_AlertsAreDarkPanelsWithLightStatusText` pins the surface and the per-variant mapping, and a new contrast-table row pins every status text token and the accent text on the overlay at the 4.5:1 text bar (95 UiFoundation tests pass).

Sweep `alerts-dark` (17 manifest ids that render an alert, 34 records, both User-Agents, 0 non-200) against `final` by `readability-compare.py`: no new contrast failure, hidden control, console error or overflow (291 contrast failure rows in both). Screenshots: `plans/94-24/review/img/decisions/alert-dark-final-{success,danger,warning,info}-*.png` (real pages) and `alert-dark-final-probe-{desktop,mobile}.png` (all six variants), measurements in `alert-dark-final.json`.

## Tests

`dotnet build`: 0 errors. Full `dotnet test`: UnitTests 1,300 passed; IntegrationTests 1,751 passed, 11 skipped (SQL Server migrator facts), 0 failed. No flake hit.

## WINDOWS closure list

- Resolved: 11 (heading and strong scale), 13 (important blue focus shadow), 15 and 26 (`.toast` restatement), 16 (phone 20px card title), 17 (dark-panel alert, now done).
- 12 partly resolved: on desktop the lower ratios were a probe artefact; the phone required-field asterisk (`span.text-danger`, Characters-Create/Edit, Contacts-Create/Edit/Details) stays at 3.36:1 until those cards move to the scrimmed surface in their area phases.
- Accepted by the operator: 18, 21, 24, 25.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Heading scale guard threw on a selector declared by two rules**
- **Found during:** Task 3, `fab1f3fe`. The guard now reads every rule of a selector.

**2. [Rule 1 - Bug] 42 phone rows regressed to 4.34:1 on unconverted cards**
- **Found during:** first full sweep. Fixed with the `--qb-fg-heading` token on the page sheets (`a8de5cf7`).

**3. [Rule 1 - Bug] `.form-control-plaintext` (1.04:1) and `.cr-badge` (4.0:1) lost their colour with the card-wide cream rule**
- **Fix:** `4a5fdc3e`.

The heading scale commit `2b32dab6` carries a 94-19 label because it came from a stopped run and was kept unchanged.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

All ten commits resolve on the branch (`git log b61869b1..HEAD`), `modern-card.css` is absent, and the alert guard, contrast row and full test run pass.
