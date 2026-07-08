---
phase: 43-mobile-parity-fixes
plan: 01
subsystem: ui
tags: [css, ios-safari, mobile, background-attachment, pseudo-element]

# Dependency graph
requires: []
provides:
  - body::before fixed-background pseudo-element pattern in mobile.css and site.css, replacing the iOS-broken background-attachment: fixed
affects: [43-02]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "body::before position:fixed pseudo-element (100dvh with 100vh fallback, z-index:-1) as the cross-browser-safe way to render a full-viewport fixed background image, instead of background-attachment: fixed"

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/mobile.css
    - QuestBoard.Service/wwwroot/css/site.css

key-decisions:
  - "Applied the identical body::before fix to site.css even though it's unreachable by real iOS Safari sessions today (MobileDetectionMiddleware routes iPhone/iPad to mobile.css) — both files carried the identical broken rule, so both get the identical fix for consistency at no extra cost (per plan D-02)."

patterns-established:
  - "Fixed full-viewport background layers use body::before with position:fixed + 100vh/100dvh + z-index:-1, not background-attachment:fixed on body directly."

requirements-completed:
  - MOBILE-01

# Metrics
duration: 15min
completed: 2026-07-04
status: complete
---

# Phase 43 Plan 01: iOS Safari Fixed-Background Fix Summary

**Replaced `background-attachment: fixed` in both `mobile.css` and `site.css` with a `position: fixed` `body::before` pseudo-element layer (100dvh with 100vh fallback, `z-index: -1`), fixing the iOS Safari bug where the notice-board background scrolled with page content instead of staying visually fixed — automated tasks complete, real-device verification checkpoint still pending.**

## Performance

- **Duration:** 15 min (automated tasks only; checkpoint pending)
- **Started:** 2026-07-04T20:16:00Z
- **Completed (automated portion):** 2026-07-04T20:31:49Z
- **Tasks:** 2 of 3 completed (Task 3 is a blocking real-device checkpoint, not yet run)
- **Files modified:** 2

## Accomplishments
- `mobile.css`'s `body` rule stripped of all five background declarations, keeping only `font-size`/`line-height`; new `body::before` rule carries the background via `position: fixed`.
- `site.css`'s `body` rule (which held only the five background declarations) replaced entirely by the equivalent `body::before` rule, kept in parity with `mobile.css` even though this file is unreachable by real iOS Safari today.
- `dotnet build` succeeds with 0 warnings / 0 errors after both edits — no bundled asset references broken.
- Verified no non-comment line in either file still contains `background-attachment`, no `-webkit-` prefix was added, and no scroll-listener JS was introduced.

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace background-attachment: fixed with a body::before layer in mobile.css** - `5c53dd3` (fix)
2. **Task 2: Apply the identical body::before fix in site.css** - `bdd0951` (fix)

Task 3 (real-device checkpoint) has not run yet — see "Next Phase Readiness" below.

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/mobile.css` - `body` rule now carries only typography (`font-size`, `line-height`); new `body::before` rule renders the notice-board background as a fixed, full-viewport layer behind all content.
- `QuestBoard.Service/wwwroot/css/site.css` - `body` rule (previously only background declarations) replaced by the equivalent `body::before` rule; fixed for consistency even though unreachable by iOS Safari today (routed to `mobile.css` instead).

## Decisions Made
- None beyond what the plan already locked (D-01/D-02) — followed the plan's `body::before` technique exactly in both files.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Explanatory comment accidentally reintroduced the literal string "background-attachment" in mobile.css**
- **Found during:** Task 1 acceptance-criteria verification
- **Issue:** The first draft of the `body::before` explanatory comment in `mobile.css` used the literal phrase "background-attachment: fixed" in a continuation comment line (not prefixed with `/*` on that line). The plan's acceptance criterion greps for `background-attachment` on any line not starting with `/*`, so this comment wording caused a false-positive match (count of 1 instead of 0), even though the CSS declaration itself was correctly removed.
- **Fix:** Reworded the comment to describe the bug without using the literal property:value string ("a fixed background on body" instead of "background-attachment: fixed").
- **Files modified:** `QuestBoard.Service/wwwroot/css/mobile.css`
- **Verification:** Re-ran `grep -v '^\s*/\*' mobile.css | grep -c background-attachment` — returns 0. Full acceptance-criteria re-run, all PASS.
- **Committed in:** `5c53dd3` (part of Task 1 commit — caught before commit, not a separate fix commit)

---

**Total deviations:** 1 auto-fixed (1 bug, caught pre-commit during the mandatory acceptance-criteria verification loop)
**Impact on plan:** No functional impact — the CSS behavior was correct on the first pass; only a comment's wording needed adjustment to satisfy the plan's literal grep-based acceptance criterion. No scope creep.

## Issues Encountered
None beyond the deviation above.

## User Setup Required
None - no external service configuration required.

## Task 3: Real-Device Verification — APPROVED

**Device:** iPhone 17 Pro, iOS 26 (physical device, real Wi-Fi LAN session — not devtools emulation).

**Result:** Background stays visually fixed in place while page content scrolls over it. User confirmed approval after retesting against the merged fix (an initial test failed because the fix was still isolated in an unmerged build worktree; after merging both plans' worktrees into the branch and restarting the dev server, retest passed).

All 3 tasks now complete. MOBILE-01 fully satisfied: `background-attachment` is gone from both `mobile.css` and `site.css`, replaced by a `body::before` fixed-position layer, confirmed working on a real iOS Safari session.

---
*Phase: 43-mobile-parity-fixes*
*Completed: 2026-07-04*
