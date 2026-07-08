---
phase: 43-mobile-parity-fixes
plan: 02
subsystem: ui
tags: [razor, css, mobile, quest-log]

# Dependency graph
requires: []
provides:
  - "Session Recap Available badge ported into the mobile Quest Log list view, matching desktop"
affects: [43-mobile-parity-fixes]

# Tech tracking
tech-stack:
  added: []
  patterns: [mobile-component ports desktop's already-shipped markup/CSS pixel-for-pixel rather than re-deriving visual values]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/quest-log.mobile.css

key-decisions:
  - "Badge span uses d-block (not a new wrapper div) to force its own row, since mobile has no quest-log-card-footer flex-column parent like desktop does"
  - "New .quest-log-item .recap-badge CSS rule scoped one level shallower than desktop (no .quest-log-card-footer ancestor) and given its own !important color/text-shadow so it does not inherit the sibling faded meta-text rule"

patterns-established:
  - "When porting a desktop component to mobile, scope new CSS directly under the mobile component root (.quest-log-item) rather than reproducing desktop's nested wrapper class that doesn't exist on mobile"

requirements-completed:
  - MOBILE-02

# Metrics
duration: n/a
completed: 2026-07-04
status: complete
---

# Phase 43 Plan 02: Mobile Recap Badge Summary (Partial — Task 3 Pending)

**Ported desktop's amber/gold "Session Recap Available" badge into the mobile Quest Log card (markup + CSS); real-device verification still outstanding.**

## Performance

- **Tasks:** 2 of 3 completed (Task 3 is a blocking real-device checkpoint, not yet run)
- **Files modified:** 2

## Accomplishments
- `Index.Mobile.cshtml` now renders a `<span class="recap-badge d-block">` row (guarded by `!string.IsNullOrWhiteSpace(quest.Recap)`) as the last element of the `.quest-log-item` card, directly after the DM-name row — verbatim port of desktop's guard/markup, no `quest-log-card-footer` wrapper.
- `quest-log.mobile.css` gained a new `.quest-log-item .recap-badge` rule carrying desktop's exact amber/gold pill values (`rgba(255, 193, 7, 0.2)` background, `rgba(255, 193, 7, 0.4)` border, `12px` radius, full-opacity `#F4E4BC` text) — scoped so it does not inherit the sibling `.quest-log-item small, .quest-log-item .text-muted` faded-parchment rule.
- `dotnet build` succeeds (Razor view compiles cleanly, 0 warnings / 0 errors).

## Task Commits

Each task was committed atomically:

1. **Task 1: Port the recap-badge markup into the mobile Quest Log card** - `06c4345` (feat)
2. **Task 2: Add the amber/gold .recap-badge CSS to quest-log.mobile.css** - `7bda4d4` (feat)

Task 3 (real-device checkpoint) has not been executed — see below.

## Files Created/Modified
- `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` — added conditional recap-badge row after the DM-name `<small>`, before the card's closing `</div>`
- `QuestBoard.Service/wwwroot/css/quest-log.mobile.css` — added `.quest-log-item .recap-badge` rule after the existing `.quest-log-item .badge` rule

## Decisions Made
- Used `d-block` on the badge `<span>` instead of introducing a new wrapper div, since mobile's card has no `.quest-log-card-footer` flex-column ancestor to force block placement the way desktop does.
- Scoped the new CSS rule directly under `.quest-log-item` (dropping desktop's `.quest-log-card-footer` ancestor, which doesn't exist on mobile) and gave it its own `!important` color/text-shadow declarations so it renders full-opacity parchment text rather than inheriting the sibling rule's faded `rgba(244, 228, 188, 0.7)` variant.

## Deviations from Plan

None — plan executed exactly as written for Tasks 1-2.

## Issues Encountered

None for Tasks 1-2. Both automated `grep` verification commands and `dotnet build` passed on first attempt.

## User Setup Required

None for the code changes. Task 3's real-device verification requires temporary manual setup (dev server bound to `0.0.0.0:8000`, a temporary Windows Firewall rule, and a physical iPhone on the same LAN) — this setup is verification-time only, not a persistent environment requirement, and is not yet performed.

## Task 3: Real-Device Verification — APPROVED

**Device:** iPhone 17 Pro, iOS 26 (physical device, real Wi-Fi LAN session — not devtools emulation).

**Result:** "Session Recap Available" amber/gold pill badge confirmed rendering correctly on the mobile Quest Log, matching desktop's styling, placement, and copy. User confirmed approval after retesting against the merged fix (an initial test cycle used an unmerged build worktree; after merging both plans' worktrees into the branch and restarting the dev server, retest passed).

All 3 tasks now complete. MOBILE-02 fully satisfied.

---
*Phase: 43-mobile-parity-fixes*
*Completed: 2026-07-04*
