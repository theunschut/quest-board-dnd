---
phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co
plan: 05
subsystem: testing
tags: [integration-tests, xunit, rename-refactor, aspnet-core-mvc]

# Dependency graph
requires:
  - phase: 58-03
    provides: CharactersController and Views/Characters/* (the /Characters/* route surface these tests exercise)
provides:
  - CharactersControllerIntegrationTests.cs (renamed from GuildMembersControllerIntegrationTests.cs) exercising /Characters/* routes
  - MobileViewsTests.cs updated to hit /Characters/* routes and assert characters.mobile.css
  - LayoutNavigationTests.cs asserting the "Characters" nav link text
affects: [58-06]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs

key-decisions:
  - "Renamed the MobileViewsTests.cs test-user identifier 'guild_browse16' to 'char_browse16' to satisfy the plan's strict grep -ci 'guild' == 0 acceptance criterion, even though it was only a test-data string, not a route/class/CSS reference"
  - "Dropped the stale 'NAV-03:' planning-ID prefix from LayoutNavigationTests.cs's section comment per CLAUDE.md's Code Comments rule, while updating the same comment's guild-members text"

patterns-established: []

requirements-completed: []

# Metrics
duration: 5min
completed: 2026-07-06
status: complete
---

# Phase 58 Plan 05: Rename integration tests from GuildMembers to Characters routes Summary

**Renamed and updated 3 integration test files (17 + 4 route strings, 3 method names, 1 loose assertion) so the entire integration test suite exercises the `/Characters/*` route surface instead of `/GuildMembers/*`, proving zero behavior change post-rename.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-07-06T17:58:19Z
- **Completed:** 2026-07-06T18:03:24Z
- **Tasks:** 3 completed
- **Files modified:** 3

## Accomplishments
- `GuildMembersControllerIntegrationTests.cs` renamed to `CharactersControllerIntegrationTests.cs` (file + class), all 16 hardcoded `/GuildMembers/*` route strings updated to `/Characters/*`, the loose `ContainAny("Guild", "Members")` assertion corrected to `ContainAny("Character", "Characters")` — all 17 tests pass, including every Phase 56 owner-or-admin authorization regression case (Admin/SuperAdmin/Player/cross-group Edit/Delete/ToggleRetirement)
- `MobileViewsTests.cs` (the file CONTEXT.md missed per RESEARCH.md Pitfall 2) updated: method renamed `MobileGuildMembers_MobileUserAgent_RendersListRows` → `MobileCharacters_MobileUserAgent_RendersListRows`, 4 route strings updated to `/Characters/*`, the `guild-members.mobile.css` assertion updated to `characters.mobile.css`, and all doc comments/test-identifiers referencing guild terminology reworded — all 49 tests in the file pass
- `LayoutNavigationTests.cs` updated: method renamed `Nav_CampaignAuthenticated_GuildMembersLinkPresent` → `Nav_CampaignAuthenticated_CharactersLinkPresent`, assertion updated from `Contain("Guild Members")` to `Contain("Characters")`, stale planning-ID comment prefix dropped

## Task Commits

Each task was committed atomically:

1. **Task 1: Rename the controller integration test class + update all 16 route strings and the loose assertion** - `752953f` (test)
2. **Task 2: Update MobileViewsTests.cs route strings, method name, CSS assertion, and doc comments** - `b8096ca` (test)
3. **Task 3: Update LayoutNavigationTests.cs nav assertion, method name, and comment; run full suite** - `5bcc0dc` (test)

**Plan metadata:** (this commit, docs: complete plan)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` - Renamed from `GuildMembersControllerIntegrationTests.cs`; all routes point at `/Characters/*`
- `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` - 4 route strings, 1 method name, 1 CSS assertion, doc comments updated
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` - 1 method name, 1 assertion, 1 comment updated

## Decisions Made
- Renamed a test-user identifier string (`guild_browse16` → `char_browse16`) in `MobileViewsTests.cs` beyond what the plan's action list literally named, to satisfy its own acceptance criterion (`grep -ci "guild"` returns 0) — a Rule 1 consistency fix, not scope creep
- Dropped the `NAV-03:` planning-ID prefix from a `LayoutNavigationTests.cs` comment while touching that same line for the guild→characters wording, per CLAUDE.md's Code Comments rule (the prefix was pre-existing, out of this plan's stated scope, but the plan's own task text explicitly authorized dropping it "per CLAUDE.md if present")

## Deviations from Plan

None - plan executed exactly as written. All three tasks' acceptance criteria (grep counts, method names, assertion text) were met exactly as specified.

## Issues Encountered

**Expected same-wave cross-plan test failure (not a bug in this plan):** `LayoutNavigationTests.Nav_CampaignAuthenticated_CharactersLinkPresent` (both `[Theory]` cases, desktop + mobile UA) fails in this worktree in isolation, because `_Layout.cshtml`/`_Layout.Mobile.cshtml` still render the nav link text as "Guild Members" — that rename is Plan 04's responsibility (Wave 3, parallel with this plan, disjoint files, no dependency between 04 and 05). The plan's own Task 3 action text explicitly anticipated this ("matches Plan 04's renamed nav link text"). Full suite run in this worktree: `QuestBoard.UnitTests` 183/183 passed; `QuestBoard.IntegrationTests` 329/331 passed, 2 failed (both `Nav_CampaignAuthenticated_CharactersLinkPresent` theory cases). No other tests reference "GuildMembers" or "guild-members" — confirmed via `grep -rin "guild" QuestBoard.IntegrationTests/` returning only this plan's now-correctly-renamed content. This will resolve automatically once the orchestrator merges Plan 04's worktree into this wave; no action needed from this plan.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All 3 integration test files are fully renamed/updated per the RESEARCH.md manifest; the test suite will be fully green once Plan 04's nav-link-text change merges into the same wave.
- Plan 06 (final cleanup/verification sweep) can proceed once all Wave 3 plans (03, 04, 05) are merged — at that point a full `dotnet test` run should show 0 failures and a repo-wide `grep -ri "guild"` (scoped to `QuestBoard.Service/` and `QuestBoard.IntegrationTests/`) should return 0 hits.

---
*Phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co*
*Completed: 2026-07-06*
