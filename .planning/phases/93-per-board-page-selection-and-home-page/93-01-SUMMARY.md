---
phase: 93-per-board-page-selection-and-home-page
plan: 01
subsystem: ui
tags: [aspnet-mvc, resource-filter, ef-core, migration, automapper, board-settings]

requires:
  - phase: 92
    provides: board type resolver, active group context and the per-board query filters this builds beside
provides:
  - BoardPage catalogue and BoardPageSet bit mask (stored contract)
  - BoardPagePolicy, the single rule set for availability and the effective home
  - IBoardPageResolver / BoardPageAvailability and the scoped BoardPageResolver
  - Groups.DisabledPages and Groups.HomePage columns (migration AddBoardPageSettings) with entity and map support
  - BoardPageGateFilter global resource filter with [BoardPage] and [BoardPageExempt] attributes
  - BoardPageRoutes (RouteFor, HomeRoute, NotUsedMessage)
  - Shop gated end to end; BoardPageSeed test helper and BoardPageGateTests
affects: [93-02, 93-03, 93-04, 93-05, 93-06, 93-07, 93-08, 93-09, 93-10, 93-11, 93-12, 93-13, 93-14, 93-15]

actuals:
  tokens: 33000
  tasks: 2
  commits: 3
plan_head_before: be0762ea224e6ed853a14131ada34a1e9073fde3

tech-stack:
  added: []
  patterns:
    - "One pure Domain policy plus one scoped resolver decide page availability; menus, gates and redirects all ask them"
    - "Global IAsyncResourceFilter reads endpoint metadata attributes and redirects with an Info toast"
    - "Disabled-pages bit mask so the CLR default (0) means nothing switched off"

key-files:
  created:
    - QuestBoard.Domain/Enums/BoardPage.cs
    - QuestBoard.Domain/Extensions/BoardPagePolicy.cs
    - QuestBoard.Domain/Interfaces/IBoardPageResolver.cs
    - QuestBoard.Repository/Migrations/20261021112418_AddBoardPageSettings.cs
    - QuestBoard.Repository/Migrations/20261021112418_AddBoardPageSettings.Designer.cs
    - QuestBoard.Service/Services/BoardPageResolver.cs
    - QuestBoard.Service/Filters/BoardPageGateFilter.cs
    - QuestBoard.Service/Helpers/BoardPageRoutes.cs
    - QuestBoard.IntegrationTests/Helpers/BoardPageSeed.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs
  modified:
    - QuestBoard.Domain/Models/Group.cs
    - QuestBoard.Repository/Entities/GroupEntity.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
    - QuestBoard.Service/Program.cs
    - QuestBoard.Service/Controllers/Shop/ShopController.cs

key-decisions:
  - "Disabled-pages mask (not enabled-pages) so existing boards and every test seed default to nothing off and the quest list as home"
  - "The gate passes through when no board type resolves, matching today's behaviour"
  - "Resolver memo keyed on the active group id rather than the request, because the board picker can switch boards inside one request"
  - "Group -> GroupEntity map keeps ignoring LinkedBoardSetId and LinkedBoardSet so a settings save cannot overwrite the link"

patterns-established:
  - "BoardPageGateFilter: [BoardPage(page)] on a controller or action, [BoardPageExempt] to opt an action out"
  - "Refusal target comes only from BoardPageRoutes.HomeRoute, which is available by construction or the ungated Account/Profile"

requirements-completed: [D-01, D-05, D-06, D-09, D-10, D-11, D-12, D-18, D-19]

coverage:
  - id: D1
    description: "With Shop off and Calendar as home, GET /Shop redirects to /Calendar and the landing page shows the Shop toast"
    requirement: "D-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs#Shop_OffWithCalendarHome_RedirectsToCalendarWithToast"
        status: pass
    human_judgment: false
  - id: D2
    description: "Default settings leave /Shop served exactly as before (200)"
    requirement: "D-18"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs#Shop_WithDefaultSettings_IsServed"
        status: pass
    human_judgment: false
  - id: D3
    description: "A Campaign board refuses /Shop with nothing stored and lands on the quest list with the toast"
    requirement: "D-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs#Shop_OnCampaignBoardWithNothingStored_RedirectsToQuestListWithToast"
        status: pass
    human_judgment: false
  - id: D4
    description: "Shop off is refused for a super admin as well as a player (no role exemption)"
    requirement: "D-06"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs#Shop_Off_IsRefusedForSuperAdminToo"
        status: pass
    human_judgment: false
  - id: D5
    description: "With no resolved board type the gate does nothing"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs#Shop_WithNoResolvedBoardType_AnswersAsItDoesWithShopOn"
        status: pass
    human_judgment: false
  - id: D6
    description: "Migration adds Groups.DisabledPages and Groups.HomePage as int NOT NULL default 0 with no pending model changes"
    requirement: "D-18"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Migrator/MigrationPreflightTests.cs"
        status: pass
    human_judgment: false

duration: 14min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 01: Page gate tracer Summary

**A board whose stored settings switch Shop off and open on Calendar refuses `/Shop` with a toast and lands on `/Calendar`, through two new Groups columns, one Domain policy, one scoped resolver and a global MVC resource filter.**

## Performance

- **Duration:** 14 min
- **Started:** 2026-10-21T11:21:33Z
- **Completed:** 2026-10-21T11:35:30Z
- **Tasks:** 2
- **Files modified:** 16

## Accomplishments
- `BoardPage` catalogue, `BoardPageSet` mask and `BoardPagePolicy` (availability, effective home, labels, lock reasons), including the Quest Log depending on the quest list without touching its stored bit.
- `Groups.DisabledPages` and `Groups.HomePage` (`int NOT NULL DEFAULT 0`) with entity, domain model and both-way mapping; the `LinkedBoardSetId`/`LinkedBoardSet` ignores are kept.
- `BoardPageResolver` (scoped, memoised on the active group id, board type from the existing `IBoardTypeResolver`) and `BoardPageGateFilter` registered globally; Shop is the first gated controller.
- Boundary facts hold: default settings serve `/Shop`, Campaign boards refuse it with nothing stored, a super admin is refused too, and an unresolved board type passes through.

## Task Commits

1. **Task 1: tracer, Shop off with Calendar home refuses /Shop and lands on /Calendar** - `79388b9e` (feat)
2. **Task 2: gate boundaries (default, Campaign, super admin, unresolved)** - `98f8701a` (test)
3. **Line-ending repair for Program.cs** - `748b71d6` (fix)

**Plan metadata:** recorded in the docs commit that carries this file.

## Files Created/Modified
- `QuestBoard.Domain/Enums/BoardPage.cs` - `BoardPage` enum (stored values, never renumber) and `BoardPageSet` mask
- `QuestBoard.Domain/Extensions/BoardPagePolicy.cs` - the single availability and home rule set
- `QuestBoard.Domain/Interfaces/IBoardPageResolver.cs` - resolver contract and `BoardPageAvailability`
- `QuestBoard.Service/Services/BoardPageResolver.cs` - per-request availability of the active board
- `QuestBoard.Service/Filters/BoardPageGateFilter.cs` - gate filter plus `[BoardPage]` and `[BoardPageExempt]`
- `QuestBoard.Service/Helpers/BoardPageRoutes.cs` - route table, home route and refusal sentences
- `QuestBoard.Repository/Migrations/20261021112418_AddBoardPageSettings*.cs` and the snapshot - the two new columns
- `QuestBoard.Domain/Models/Group.cs`, `GroupEntity.cs`, `EntityProfile.cs` - storage and mapping
- `QuestBoard.Service/Program.cs`, `ShopController.cs` - filter and resolver registration, Shop gated
- `QuestBoard.IntegrationTests/Helpers/BoardPageSeed.cs`, `Controllers/BoardPageGateTests.cs` - seed helper and the five gate facts

## Decisions Made
Followed the plan's recorded choices. No new architectural decisions.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Program.cs line endings rewritten by a scripted edit**
- **Found during:** wrap-up check of the plan's total diff (448 insertions and deletions on a file that should differ by 6 lines)
- **Issue:** a `sed -i` used to add the `using` line converted the whole file from CRLF to LF, which would have buried the real change in a full-file diff.
- **Fix:** regenerated the file from the previous revision with a byte-exact edit that keeps CRLF, so only the three intended edits differ.
- **Files modified:** `QuestBoard.Service/Program.cs`
- **Verification:** `git diff` against the pre-plan revision shows +5 -1; build and the gate tests pass.
- **Committed in:** `748b71d6`. The feat commit `79388b9e` still carries the LF version of the file, so a plain `git diff` of that one commit shows a whole-file change; the net diff across the plan is correct.

**2. [Rule 1 - Test fidelity] Toast assertion decodes HTML**
- **Found during:** Task 1
- **Issue:** Razor encodes the apostrophe in "Shop isn't used on this board.", so a literal substring check would never match.
- **Fix:** the tests assert on `WebUtility.HtmlDecode(html)`.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs`

---

**Total deviations:** 2 auto-fixed (2 Rule 1)
**Impact on plan:** no scope change.

## TDD Gate Compliance

Task 2 is `tdd="true"`, but its four facts exercise behaviour the Task 1 tracer already implements (gate, policy and unresolved pass-through are part of the one path), so they were green on first run and there is no failing RED. This was investigated rather than ignored: a temporary mutation of `BoardPagePolicy.AllowedOn` (Shop allowed on Campaign) made the Campaign fact fail, so the tests do detect the behaviour. A `test(93-01)` commit exists; no `feat(93-01)` commit follows it because the implementation was committed in Task 1 as the tracer (`feat(93-01)` `79388b9e`).

## Issues Encountered
- Pre-existing and unrelated: `MigratorBackupAndApplyCliTests.StateChangingCommand_WhenServerIsUnreachable...` fails intermittently (one of its `apply`/`backup` theory cases) when the `Migrator` test group runs together, and fails the same way on a clean checkout of the commit before this plan. Logged in `deferred-items.md`. The full `dotnet test` run was green (1107 unit, 1333 integration, 11 skipped).
- The EF scaffold added `.IsConcurrencyToken()` for `LinkedBoardSetId` to the model snapshot next to the two new properties. That attribute was already in the model configuration but missing from the snapshot; it is scaffold output, not a hand edit, and produces no schema change.

## Known Stubs

None.

## Threat Flags

None. The new redirect target is built from `BoardPageRoutes` route names only, as the plan's threat model requires.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 02 and later can add `[BoardPage]` attributes and the view helpers directly against `BoardPageSeed` and the resolver.
- The Quests page is deliberately not switchable yet (`IsSwitchable`), as recorded for the later plan that builds it.

## Self-Check: PASSED

- Created files exist on disk, including the migration, Designer and the new test files.
- Commits `79388b9e`, `98f8701a` and `748b71d6` exist; `git rev-list --count be0762ea..HEAD` was 2 before the line-ending fix commit and the plan now has 3.
- Acceptance criteria re-run: the five `BoardPageGateTests` pass, `MigrationPreflightTests` pass (9/9), layout, group management and linked boards suites pass unchanged, the migration has two `AddColumn<int>` calls with `nullable: false, defaultValue: 0`.
- No added source line carries a planning reference (grep count 0).

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
