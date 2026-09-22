---
phase: 86-viewer-local-times-and-correct-job-scheduling
plan: 05
subsystem: frontend
tags: [razor, html-localtime, timezone, quest, shop, admin]

requires: [86-01]
provides:
  - Fourteen real-instant render sites across six Razor views converted to Html.LocalTime, closing the two 86-RESEARCH.md corrections (SignupTime, Admin/EmailStats.AsOf) over 86-CONTEXT.md's original classification
affects: [86-06]

actuals:
  tokens: 2571
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Explicit Razor ternary conditional (Model.X != null ? Html.LocalTime(...) : Html.Raw(string.Empty)) replaces a `?.` null-conditional at a call site, since Html.LocalTime takes a non-nullable DateTime and cannot carry the null-conditional through"

key-files:
  modified:
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/_QuestCard.cshtml
    - QuestBoard.Service/Views/Shop/Index.cshtml
    - QuestBoard.Service/Views/ShopManagement/Index.cshtml
    - QuestBoard.Service/Views/Admin/EmailStats.cshtml

key-decisions:
  - "Used `Model.Quest != null ? Html.LocalTime(Model.Quest.CreatedAt, \"date\") : Html.Raw(string.Empty)` for Quest/Details.cshtml's null-conditional CreatedAt, and `Model.AsOf.HasValue ? Html.LocalTime(Model.AsOf.Value, \"date-time\") : Html.Raw(string.Empty)` for Admin/EmailStats.cshtml's AsOf -- both preserve today's exact empty-text-on-null output since Html.LocalTime requires a non-nullable DateTime and has no null overload per the design contract's null contract."

requirements-completed: [D-01, D-02, D-05, D-06]

coverage:
  - id: D5-1
    description: "Seven SignupTime render sites across Quest/Manage.cshtml (4) and Quest/Details.cshtml (3) converted to Html.LocalTime with date-time-compact / date-compact styles; their OrderBy-only mobile twins left untouched"
    requirement: "D-06"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestManage|FullyQualifiedName~QuestFinalize (12 tests) and FullyQualifiedName~QuestDetails|FullyQualifiedName~QuestController (52 tests)"
        status: pass
    human_judgment: false
  - id: D5-2
    description: "Model.CreatedAt/Model.Quest?.CreatedAt converted to Html.LocalTime(date) on Quest/Manage.cshtml, Quest/Details.cshtml and the shared Quest/_QuestCard.cshtml partial, with the null-conditional preserved via an explicit ternary"
    requirement: "D-06"
    verification:
      - kind: integration
        ref: "Same QuestDetails/QuestController filter run above"
        status: pass
    human_judgment: false
  - id: D5-3
    description: "Shop/Index.cshtml's two TransactionDate render sites converted to date-compact; the DateTime.UtcNow - purchase.TransactionDate subtraction and the <= 24 comparison remain byte-identical UTC-to-UTC arithmetic"
    requirement: "D-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests --filter FullyQualifiedName~Shop|FullyQualifiedName~Admin (117 tests)"
        status: pass
      - kind: manual-grep
        ref: "grep -c 'DateTime.UtcNow - purchase.TransactionDate' and 'timeSincePurchase.TotalHours <= 24' both return 1, unchanged"
        status: pass
    human_judgment: false
  - id: D5-4
    description: "ShopManagement/Index.cshtml's DeniedAt and Admin/EmailStats.cshtml's AsOf converted to Html.LocalTime, with existing null guards (HasValue checks) preserved as explicit conditionals"
    requirement: "D-01, D-02"
    verification:
      - kind: integration
        ref: "Same Shop/Admin filter run above"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-09-20
status: complete
---

# Phase 86 Plan 05: Quest, Shop, and Admin View Conversions Summary

**Fourteen real-instant render sites across six Razor views (Quest Manage/Details/shared card, Shop, ShopManagement, Admin EmailStats) converted from raw `.ToString(...)` to `Html.LocalTime`, closing both 86-RESEARCH.md corrections over 86-CONTEXT.md's classification while leaving the shop's UTC-based 24-hour return window and every game-night wall-clock value byte-identical.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-09-20 (approx.)
- **Completed:** 2026-09-20
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `Quest/Manage.cshtml`: converted the four `SignupTime` render sites (player, assistant DM, spectator, participant table) to `date-time-compact`, and `Model.CreatedAt` to `date`. Left `FinalizedDate`, `ProposedDate` and every `OrderBy(ps => ps.SignupTime)` key untouched. Confirmed `Manage.Mobile.cshtml` uses `SignupTime` only as an ordering key and never renders `CreatedAt` -- zero edits to that file.
- `Quest/Details.cshtml`: converted `Model.Quest?.CreatedAt` via an explicit `!= null` ternary (preserving today's empty-render-on-null behavior, since `Html.LocalTime` takes a non-nullable `DateTime`), and the three `signup.SignupTime` sites (Players/AssistantDM/Spectator) to `date-compact`. `Details.Mobile.cshtml` confirmed untouched.
- `Quest/_QuestCard.cshtml`: converted the shared partial's `Model.CreatedAt` to `date`, covering both desktop and mobile layouts through the single `_QuestSection.cshtml` render path. `Model.FinalizedDate` left untouched.
- `Shop/Index.cshtml`: converted both `purchase.TransactionDate` render sites (two tabs) to `date-compact`. The `DateTime.UtcNow - purchase.TransactionDate` subtraction and `timeSincePurchase.TotalHours <= 24` comparison that drive the 24-hour return window remain byte-identical UTC arithmetic -- no converted value was ever bound to a local.
- `ShopManagement/Index.cshtml`: converted `item.DeniedAt.Value` to `date`, preserving the `item.DeniedAt.HasValue` guard verbatim.
- `Admin/EmailStats.cshtml`: converted `Model.AsOf` via an explicit `HasValue` ternary to `date-time`, preserving the "Stats as of ... -- cached for 5 minutes" copy and empty-render-on-null behavior.

## Task Commits

Each task was committed atomically:

1. **Task 1: Quest Manage -- four signup times and the creation date** - `3bce5ae4` (feat)
2. **Task 2: Quest Details and the shared quest card** - `de54eca5` (feat)
3. **Task 3: Shop, shop management and the admin email-stats timestamp** - `3b01a22b` (feat)

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - Four `SignupTime` sites + `CreatedAt` converted to `Html.LocalTime`
- `QuestBoard.Service/Views/Quest/Details.cshtml` - Null-guarded `CreatedAt` + three `SignupTime` sites converted
- `QuestBoard.Service/Views/Quest/_QuestCard.cshtml` - Shared partial's `CreatedAt` converted
- `QuestBoard.Service/Views/Shop/Index.cshtml` - Two `TransactionDate` sites converted; UTC subtraction untouched
- `QuestBoard.Service/Views/ShopManagement/Index.cshtml` - `DeniedAt` converted, guard preserved
- `QuestBoard.Service/Views/Admin/EmailStats.cshtml` - Null-guarded `AsOf` converted

## Decisions Made
- For both null-guarded call sites (`Model.Quest?.CreatedAt` in Details.cshtml and `Model.AsOf?` in EmailStats.cshtml), used an explicit ternary (`condition ? Html.LocalTime(...) : Html.Raw(string.Empty)`) rather than any nullable overload, since `Html.LocalTime` is contractually non-nullable per `86-UI-SPEC.md`'s implementation seam notes. Both branches return `IHtmlContent`, so the ternary type-checks cleanly in Razor and preserves today's exact empty-text-on-null output.

## Deviations from Plan

None - plan executed exactly as written. All acceptance-criteria greps (LocalTime counts, style-name counts, zero remaining raw `.ToString(...)` calls, unchanged `OrderBy` count, untouched UTC subtraction/comparison, zero `.Mobile.cshtml` diffs) matched the plan's specified values on first pass.

One grep-pattern false positive worth noting for future plans in this phase: the plan's "no zone label reaches visible text" acceptance grep (`grep -ciE "(UTC|CET|CEST|Amsterdam|timezone)"`) incidentally matches the substring `UTC` inside pre-existing, unrelated `DateTime.UtcNow` business-logic expressions (quest finalization staleness checks at `Manage.cshtml:705` and `Details.cshtml:760,1009`, both pre-existing and untouched by this plan's diff). These are not visible rendered text and not introduced by this plan -- verified via `git diff` against the pre-task baseline showing zero changes to those lines.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All fourteen real-instant render sites in this plan's scope are converted; `86-06` can proceed with its own remaining call sites and the floating-time guard test referenced in the threat register (`T-86-14`).
- Full test suite green: 548 unit + 847 integration tests, 0 failures -- matches the pre-task baseline exactly, confirming no regression.
- No blockers.

## Self-Check: PASSED

All 6 modified files verified present on disk with expected content. All 3 commit hashes (`3bce5ae4`, `de54eca5`, `3b01a22b`) verified present in `git log --oneline`.

---
*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Completed: 2026-09-20*
