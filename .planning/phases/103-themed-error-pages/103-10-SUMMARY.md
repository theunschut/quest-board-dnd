---
phase: 103-themed-error-pages
plan: 10
subsystem: cross-board-agreement-matrix
tags: [cross-board, deep-link, agreement-matrix, oracle, parity, picker, themed-404, tests-only, tracer]

requires:
  - phase: 103-themed-error-pages
    provides: record-visibility resolver, themed error pages, ErrorPageComparison (103-05, 103-07, 103-09)
provides:
  - CrossBoardVisibilityMatrixTests: 232 agreement rows over 18 routes, four roles, every record state and a board that does not offer the page, judged by the real page as oracle
  - CrossBoardMatrixSeeder: one row on fresh boards, the served-or-refused classifier and the shared refused-like-a-nonexistent-id assertion
  - Registry coverage guard (a route without rows fails) and a non-vacuity guard (the page serves an Admin on every route)
  - Document-navigation and per-kind hidden-record parity pairs; hidden and openable picker return URLs
  - leak-probe --compare-bodies and the live masked-body comparison
affects: [103-12, 103-18]

actuals:
  tokens: 11000
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "The page with the target board active is the oracle; the test holds no status list, role check or date rule of its own"
    - "Matrix rows come from the registry's own route list read as it is, so a route added later without rows fails the coverage fact"

key-files:
  created:
    - QuestBoard.IntegrationTests/Helpers/CrossBoardMatrixSeeder.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardVisibilityMatrixTests.cs
    - .planning/phases/103-themed-error-pages/103-10-SUMMARY.md
  modified:
    - QuestBoard.IntegrationTests/Helpers/ErrorPageComparison.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs
    - QuestBoard.IntegrationTests/Controllers/CrossBoardPickerSkipTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs

key-decisions:
  - "The oracle's served class is fixed in one helper: a 2xx, or a redirect that is neither the access-denied page nor the page gate turning the visitor away; the gate is recognised by the notice it queues, which the next page shows"
  - "For contacts both sessions (the page asked directly and the one following the link) turn the page's own show-hidden switch on for the target board, so the page is asked the same question from either side; without it the settled Dungeon Master tier rule reads as a disagreement"
  - "A refused link is compared with a nonexistent id's answer with each request's own id masked in the Location, because an access-denied redirect echoes the caller's request path; nothing else is relaxed"

requirements-completed: [D-01, D-02, D-04, D-05, D-06]

duration: 75min
completed: 2026-11-21
status: complete
---

# Phase 103 Plan 10: Cross-board agreement matrix Summary

**A 232-row matrix now judges, for every registered route, role and record state, whether following the deep link from another board switches and serves exactly when the real page serves, and otherwise leaves the board alone with an answer indistinguishable from a nonexistent id's. Every row agreed on the first honest run: the resolver and the pages already agree everywhere, so no production change was needed. The live masked bodies of a hidden record's 404 and a missing id's 404 are byte-identical under both user agents.**

## The matrix

- **Size and time:** 232 rows plus 18 non-vacuity facts and one coverage fact (251 tests in the class), 26 s for the class.
- **Routes:** all 18 in the registry. Rows are generated per route from a list of record states; the coverage fact reads the registry as it is (by reflection, no production change) and requires exactly 18 routes, exactly those routes in the rows, all four roles per route, at least one record state and one page-off row per route. It was tried once: deleting the `/Series/Details` entry made it fail ("misses /Series/Details"), then restored.
- **Roles on the target board:** Player, Dungeon Master, Admin, and a SuperAdmin who is a plain Player member. The viewer is always a Player on the default board.
- **States:** shop item Draft, Denied, Scheduled, Published, Archived (both shop routes); quest and character own or another's; quest log open, finalized in the past, closed; contact revealed, hidden, own; event, series, category and profile present; plus, for every route and role, a target board that does not offer the route's page (the page is read from the registry), with the record in its plainly openable state.
- **Oracle and served class:** the real action asked from a session with the target board active. Served is a 2xx, or a redirect whose Location is not the access-denied page and whose next page does not carry the notice the page gate queues for that page. Everything else (403, 404, access-denied redirect, gate redirect) is refused. Page-off rows additionally assert the oracle refused.
- **Agreement:** when served, the deep link from board A must itself be served with the same status, and after the switch notice is spent the settled page names the target board and no longer the default board. When refused, the answer must equal a nonexistent id's (status, masked body, headers, Location with each request's own id masked), the 404 body must carry the one fixed sentence, the board must be unchanged and no switch notice may follow.
- **Distribution (measured once with a temporary throw, not committed):** 126 rows served, 106 refused; every one of the 18 routes has both served and refused rows (for example Shop/Details 17 served and 7 refused, QuestLog/Details 8 and 8, Series/Details 4 and 4, DungeonMaster/EditProfile 2 and 6).
- **Non-vacuity:** `Page_ServesAnAdminOfTheBoard_ForTheOpenableStateOfEveryRoute` runs per route so a seeding slip that made every page refuse cannot leave the matrix agreeing on refusals alone.

The test file holds no status list, role check or date rule of its own. The only things it knows are record shapes to seed (a draft item is an item whose status is Draft; a closed quest is a closed quest) and four role names.

## Parity and picker pairs

- **Document navigations** (`Accept: text/html`): the eight entity-family non-member pairs and the SuperAdmin pair now also run as document navigations through `ErrorPageComparison`; the family pairs additionally pin the 404 status and the fixed sentence.
- **One hidden record per lookup kind** on a board the viewer belongs to against a nonexistent id: quest (open quest on the quest log), event (edit as a Player), character (another's edit as a Player), contact (unrevealed), shop item (Draft), event series (events page off), contact category (edit as a Player), board member (profile edit as a Player). Each first shows the page itself refuses the record, then compares answer, masked body, headers, the board afterwards and the absence of a banner. A fact pins that the eight cover every lookup kind.
- **Picker:** a return URL naming a hidden shop item, unrevealed contact or open quest on the other member board renders the same picker as a nonexistent id's return URL (200, same masked body); a return URL naming a Published item, a revealed contact or a played quest still redirects to exactly that URL and lands on its board.

## Live comparison (fixture data, p103player, One-Shot active, new tree on 8350)

| User agent | Hidden Draft item | Nonexistent id | Masked bodies |
|------------|-------------------|----------------|---------------|
| desktop | 404, 3080 bytes | 404, 3080 bytes | byte-identical |
| mobile (Pixel 7) | 404, 3080 bytes | 404, 3080 bytes | byte-identical |

`after/103-10/bodies.json` holds both rows. The plan's assertion printed `ok`. The probe gained `--compare-bodies`; it masks every `__RequestVerificationToken` value and compares the two bodies as strings.

## Deviations from Plan

**1. [Rule 1 - Bug in the new test, not production] Access-denied redirects echo the caller's request path.** The first run failed five rows (Player on `/ShopManagement/Edit`) because the access-denied redirect carries the requested URL in its return URL, so two requests naming different ids can never have equal Locations. Authorization runs before any lookup, so this says nothing about existence. `ErrorPageComparison` gained an overload that masks each response's own id in the Location and removes Location from the header dictionary; nothing else is relaxed. Commit b7f294f4.

**2. [Rule 1 - Test design] Contact rows turn the page's show-hidden switch on in both sessions.** The first full run failed three rows (Dungeon Master tier with a hidden contact): the page asked directly (switch on) served, the link switched to the board and the page then refused because the link's session had never switched it on. That is the settled Dungeon Master tier behaviour, which a pin in the hidden-record facts already covers (switched, then the page applies its own switch). The matrix now gives both sessions the same page state so it compares the lookup with the page, not the lookup with session state it deliberately does not read. No expectation was changed to pass; the pin for the untoggled case remains. Commit 79360df2.

Both are changes to the harness, neither to a production file or to an expectation.

**3. Added beyond the plan:** a non-vacuity fact per route, and an assertion that page-off rows are refused by the oracle. Not changed: no fact was deleted and no pre-existing fact was edited apart from the guard list gaining three entries.

The tracer's mid-plan human-verify is satisfied by green automated verify plus the 32-served and 8-refused split observed for the shop rows, per the operator's checkpoint rule.

## Test results

- Unit: 1826 passed, 0 failed.
- Integration: 3450 passed, 17 skipped (the SQL Server suites, as before), 0 failed (was 3175 passed; 275 new).
- No production file changed: `git diff` of `QuestBoard.Service`, `QuestBoard.Domain` and `QuestBoard.Repository` against the plan's start commit is empty.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or file access; the matrix reads the registry's internal route list by reflection from the test project only. T-103-35 (a later page-rule change reopening an existence oracle) is mitigated by 232 rows judged by the real pages and by the coverage fact; T-103-36 by masked byte comparison in facts and live under both user agents; T-103-37 by the hidden and openable picker pairs; T-103-38 by taking every expectation from the oracle (no disagreement had to be stopped on).

## Left running

The frozen baseline on 8351 (restarted from `baseline-src/` with `run-app.mjs`, BASELINE_SHA d9e581b8, email suppression verified in the listener's environment) is running for Gate 1. 8350 was started for the live comparison and is stopped again. SQL Server was not touched and no SQL writes were made.

## Self-Check: PASSED

- Present: `CrossBoardMatrixSeeder.cs`, `CrossBoardVisibilityMatrixTests.cs`, `after/103-10/bodies.json`, `after/103-10/PLAN_START`; commits `b7f294f4` and `79360df2` exist.
- Acceptance: the matrix class reports 251 tests, all passing; `EveryRegisteredRoute_HasMatrixRows` exists and failed when a route's rows were removed; every pre-existing fact of `CrossBoardOracleParityTests` and `CrossBoardPickerSkipTests` still exists and passes.
