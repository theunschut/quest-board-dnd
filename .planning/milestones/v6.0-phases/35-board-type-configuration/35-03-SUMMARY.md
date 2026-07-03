---
phase: 35-board-type-configuration
plan: 03
subsystem: ui

# Dependency graph
requires:
  - phase: 35-board-type-configuration (35-01)
    provides: "BoardType enum threaded through entity/domain/projection models"
  - phase: 35-board-type-configuration (35-02)
    provides: "BoardType on Create/Edit ViewModels and controller wiring"
provides:
  - "Board Type dropdown (blank-first, required, permanence warning) on Create (desktop + mobile)"
  - "Disabled read-only Board Type dropdown on Edit (desktop + mobile), visually and functionally non-interactive"
  - "Board Type badge column (desktop table) / card line (mobile) on Index"
  - "Global :disabled styling fix for form-control/form-select so disabled fields read as non-interactive across the app"
affects: [board-types-campaign-mode milestone follow-on phases]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Badge display: bg-primary for One-Shot, bg-info text-dark for Campaign, hyphenated display text distinct from the raw enum token"
    - "Disabled selects get no hidden input companion — omission is the tamper defense, not client-side disabled alone (D-06)"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Create.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/site.css

key-decisions:
  - "Human verification surfaced that disabled selects were visually indistinguishable from interactive ones, caused by pre-existing unconditional !important background/color rules on .form-control/.form-select (both a global site.css rule and a .modern-card-scoped rule). Fixed by adding :disabled-scoped override rules rather than touching the unconditional rules, keeping the fix minimal and not affecting non-disabled fields."
  - "The :disabled CSS fix is global (not scoped to the Group Edit view only) since the same latent bug affects other pre-existing disabled inputs in the app (e.g. ShopManagement/Edit's conditional AvailableFrom/AvailableUntil fields) — this is a pure visual improvement with no behavioral risk to non-disabled controls."

patterns-established:
  - "Disabled form controls (.form-control:disabled, .form-select:disabled) get a muted background, dimmed text, and cursor: not-allowed globally, and again inside .modern-card at matching specificity"

requirements-completed: [BOARD-01, BOARD-02]

# Metrics
duration: 45min
completed: 2026-07-03
---

# Phase 35 Plan 03: Board Type UI Summary

**Board Type UI shipped across all six Platform Group views (Create/Edit/Index, desktop + mobile) plus a global CSS fix so disabled form fields actually look disabled.**

## Performance

- **Duration:** ~45 min (including human-verify checkpoint round-trip)
- **Tasks:** 3 (2 automated, 1 human-verify checkpoint)
- **Files modified:** 7 (6 views + 1 stylesheet)

## Accomplishments
- Create (desktop + mobile): required, blank-first Board Type dropdown ("-- Select Board Type --") with permanence warning "This cannot be changed after creation."; empty submission shows "Board type is required." — the previously-red `CreateGroup_WithoutBoardType_ShouldFailValidation` integration test flipped green once the `asp-validation-for="BoardType"` span was added
- Edit (desktop + mobile): disabled dropdown displaying the current value, no hidden `BoardType` input — tamper protection remains via omission (D-06), proven by `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored`
- Index (desktop table + mobile card): Board Type badge column between Name and Members — `badge bg-primary` "One-Shot" and `badge bg-info text-dark` "Campaign", never the raw enum token
- Human verification confirmed correct rendering and behavior on desktop and mobile, and flagged that the disabled Edit dropdown didn't visually read as disabled — fixed with a targeted CSS change (see Deviations)
- All 236 integration tests + 118 unit tests green after the full plan, including all four board-type integration facts

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Board Type dropdown to Create (desktop+mobile) and disabled dropdown to Edit (desktop+mobile)** - `88099bf` (feat)
2. **Task 2: Add Board Type badge to Index (desktop table column + mobile card line)** - `074dd01` (feat)
3. **Task 3: Human verification of the Board Type UI across desktop and mobile** - approved, with a follow-up fix: `b62300a` (fix)

## Files Created/Modified
- `QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml` / `.Mobile.cshtml` - Board Type dropdown + validation span + permanence warning
- `QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml` / `.Mobile.cshtml` - disabled Board Type dropdown, no hidden input
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` / `.Mobile.cshtml` - Board Type badge column/line
- `QuestBoard.Service/wwwroot/css/site.css` - added `:disabled` styling for `.form-control`/`.form-select` (global and `.modern-card`-scoped)

## Decisions Made
- CSS fix for disabled-field styling applied globally rather than scoped to just the Group Edit view, since the same unconditional-background bug already affected other disabled inputs elsewhere in the app (see key-decisions)

## Deviations from Plan

### Auto-fixed Issues (post-checkpoint, user-directed)

**1. Disabled Board Type dropdown had no visual disabled affordance**
- **Found during:** Task 3 human verification
- **Issue:** `.modern-card .form-select` and the global `.form-control, .form-select, input, textarea, select` rules in `site.css` set background/color unconditionally with `!important`, overriding Bootstrap's default `:disabled` graying — the disabled Edit dropdown looked like a normal interactive dropdown that merely ignored clicks
- **Fix:** Added `:disabled`-scoped override rules (higher specificity, matching `!important` weight) giving disabled fields a muted background, dimmed text, and `cursor: not-allowed`, both globally and inside `.modern-card`
- **Files modified:** `QuestBoard.Service/wwwroot/css/site.css`
- **Verification:** Full test suite re-run clean (118 unit + 236 integration, 0 failures); user re-verified visually in browser and approved
- **Committed in:** `b62300a` (fix)

---

**Total deviations:** 1 auto-fixed (visual polish, user-directed during checkpoint)
**Impact on plan:** Pure CSS fix, no behavioral change to server-side logic or other views. No scope creep — user explicitly requested the fix before approving the checkpoint.

## Issues Encountered
None beyond the checkpoint deviation above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 35 (Board Type Configuration) is functionally complete: BoardType is set at group creation, immutable afterward, and visible everywhere a group is displayed in the Platform area
- No blockers identified for the next phase in the v6.0 Board Types (Campaign Mode) milestone

---
*Phase: 35-board-type-configuration*
*Completed: 2026-07-03*
