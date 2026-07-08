---
phase: 40-platform-members-page-redesign
plan: 03
subsystem: ui
tags: [aspnet-core-mvc, razor, bootstrap, ef-core, controller]

# Dependency graph
requires:
  - phase: 40-01
    provides: "IUserService.GetAvailableUsersAsync, GroupMembersViewModel.SearchQuery/CreateMember, CreateMemberViewModel"
  - phase: 40-02
    provides: "GroupController.Members(id, search) / AddMember(id, model, search) / CreateMember(id, model) route-scoped to the group"
provides:
  - "Two-column (desktop) / stacked (mobile) Platform Members page: current members left, searchable non-members right"
  - "Server-side search on BOTH the Current Members list and the Available Users list, each preserving the other's search term across every action"
  - "Create New User Bootstrap modal reusing Phase 39's CreateOrAddToGroupAsync, scoped to the route group"
  - "IGroupRepository/IGroupService.GetMembersAsync(groupId, search?) — new optional search parameter"
  - "Restructured page headers (Members + Group Management) with action buttons moved into the card header row"
affects:
  - "Any future Platform Group view work touching Members.cshtml, Members.Mobile.cshtml, Index.cshtml, or GroupController"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dual-search-preservation pattern: every form/link on a two-search page carries the OTHER column's current search term as a hidden asp-route value, so one filter never resets the other on submit/redirect"
    - "Card-header action button row: d-flex justify-content-between align-items-center in the card-header, title left / primary actions right, replacing standalone buttons in the card body"

key-files:
  created:
    - QuestBoard.UnitTests/Services/GroupServiceTests.cs
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs
    - QuestBoard.Domain/Interfaces/IGroupRepository.cs
    - QuestBoard.Domain/Interfaces/IGroupService.cs
    - QuestBoard.Domain/Services/GroupService.cs
    - QuestBoard.Repository/GroupRepository.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs

key-decisions:
  - "Current Members search added as a full server-side round-trip (query + controller + view + tests), matching Available Users' existing rigor, rather than a client-side/JS filter — this was a mid-checkpoint scope addition confirmed by the user, not in the original plan text"
  - "Both search terms (search for Available Users, memberSearch for Current Members) are threaded through every action (AddMember, RemoveMember, CreateMember, and CreateMember's invalid-ModelState/Failed re-render branches) so neither list's filter is silently reset by an action taken on the other list"
  - "Group/Index.cshtml header restructure (Create Group button moved into the card header) was applied as an explicit user-confirmed scope addition beyond this plan's declared files_modified — see Deviations"

patterns-established:
  - "Pattern: Dual-search-preservation via hidden asp-route values — reusable for any future page with two independent, mutually-visible search/filter lists"

requirements-completed: [MEMBERS-01, MEMBERS-02, MEMBERS-03]

# Metrics
duration: ~95min (across 4 rounds: initial 2 tasks + 3 checkpoint round-trips)
completed: 2026-07-04
status: complete
---

# Phase 40 Plan 03: Members Page Redesign (Two-Column Layout + Dual Search) Summary

**Two-column Platform Members page (current members left, searchable non-members right) with a Create New User modal, later extended mid-checkpoint with a matching server-side search on the Current Members list, dual-search-preservation across every Add/Remove/Create action, and header restructures on both Members and Group Management pages.**

## Performance

- **Duration:** ~95 min total (Tasks 1-2 execution, plus 3 checkpoint round-trips for fixes/scope additions)
- **Tasks:** 2 planned tasks + 1 checkpoint (revisited 3 times before approval)
- **Files modified:** 10 (2 created/new test file + 9 modified across Service/Domain/Repository/IntegrationTests layers)

## Accomplishments

- Redesigned `Members.cshtml` into a two-column Bootstrap grid (`row g-4`): left column = current members table (unchanged core markup), right column = GET search form + searchable Available Users table with per-row inline role-select + Add, plus a "Create New User" button opening a Bootstrap modal that posts to Phase 39's `CreateMember` action.
- Redesigned `Members.Mobile.cshtml` into a single vertical stack (members cards → search + Available Users cards with per-row Add → Create New User trigger/modal), reusing the desktop modal markup verbatim.
- Added a full server-side search feature to the **Current Members** list (not in the original plan — see Deviations): `GetMembersAsync` gained an optional `search` parameter at the repository/service/interface level, filtering on `User.Name`/`User.Email`, mirroring the existing `GetAvailableUsers` pattern exactly.
- Implemented dual-search preservation: every form and link on the page (search forms, per-row Add, per-row Remove, Create New User modal) now carries **both** `search` (Available Users) and `memberSearch` (Current Members) as hidden route values, so acting on one list never resets the other list's active filter.
- Restructured the Members page header (desktop + mobile) to a `d-flex justify-content-between align-items-center` row: title left, "Back to Groups" + "Create New User" buttons right, removing standalone buttons from the card body.
- Restructured the Group Management page header (`Index.cshtml`) the same way — "Create Group" button moved into the card header, right-aligned. (`Index.Mobile.cshtml` already used this layout; no change needed.)
- Fixed a visual alignment bug the user caught mid-checkpoint (see Deviations) where the two Members-page columns' table headers didn't line up.

## Task Commits

Each task/round was committed atomically:

1. **Task 1: Redesign Members.cshtml** — `c8c4a77` (feat) — two-column layout, search, per-row Add, Create New User modal
2. **Task 2: Redesign Members.Mobile.cshtml** — `aeb9208` (feat) — stacked layout, search, per-row Add cards, reused modal
3. **Checkpoint fix 1: Table alignment** — `a16dc56` (fix) — invisible spacer added to align both columns' table headers (later superseded, see Deviations)
4. **Checkpoint scope addition 1: Current Members search (backend)** — `fcc820f` (feat) — repository/service/interface/controller/viewmodel changes + unit + integration tests
5. **Checkpoint scope addition 2: Current Members search (view) + Members header restructure** — `ad84a76` (feat) — real search form replacing the invisible spacer, dual-search-preservation markup, header restructure
6. **Checkpoint scope addition 3: Group Management header restructure** — `bad762e` (feat) — `Index.cshtml` header restructure (out-of-plan file, user-confirmed)

**Plan metadata:** this commit (docs: complete plan) — SUMMARY.md only; STATE.md/ROADMAP.md are owned by the orchestrator and not touched by this worktree agent.

_Note: Task 3 in the plan (`type="checkpoint:human-verify" gate="blocking-human"`) was a genuine blocking checkpoint, not a committed task — it required 3 round-trips (alignment fix → search feature + header restructure → out-of-plan header restructure) before the user approved with "yes, looks good"._

## Files Created/Modified

- `QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml` — two-column layout, dual search forms, per-row Add, Create New User modal, restructured header
- `QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml` — stacked layout, dual search, per-row Add cards, restructured header, reused modal
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` — restructured header (Create Group button moved into card header) — **out-of-plan file, see Deviations**
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — `Members`, `AddMember`, `CreateMember` (all outcome branches), `RemoveMember` now accept and preserve both `search` and `memberSearch`
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs` — added `MemberSearchQuery`
- `QuestBoard.Domain/Interfaces/IGroupRepository.cs` / `IGroupService.cs` — `GetMembersAsync` gained an optional `search` parameter
- `QuestBoard.Domain/Services/GroupService.cs` — `GetMembersAsync` delegates the new parameter
- `QuestBoard.Repository/GroupRepository.cs` — `GetMembersAsync` filters on `User.Name`/`User.Email` when search is provided
- `QuestBoard.UnitTests/Services/GroupServiceTests.cs` — new file, 3 tests covering `GetMembersAsync` search delegation
- `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — 5 new tests: member-search round-trip + term-echo, `GetMembersAsync` email-match filtering, and dual-search-preservation across `AddMember`/`RemoveMember`/`CreateMember`

## Decisions Made

- Current Members search implemented as a full server-side feature (repository/service/controller/view/tests) rather than client-side filtering, matching the rigor and pattern of the existing Available Users search — user-directed mid-checkpoint scope addition.
- Both search terms are preserved as hidden route values on every form/link on the page (dual-search-preservation pattern), so no action on one list ever silently clears the other list's active filter.
- The Group/Index.cshtml header restructure was applied as an explicit, user-confirmed scope addition beyond this plan's declared `files_modified` (see Deviations below) rather than silently included or silently refused.

## Deviations from Plan

### Checkpoint-driven scope additions (user-directed, not auto-fixed)

These were not autonomous Rule 1-3 deviations — each was explicitly requested by the user during the blocking human-verify checkpoint and confirmed before being applied. Documented here per plan-execution convention since they materially changed the delivered scope beyond the original plan text.

**1. Table alignment fix (superseded)**
- **Found during:** first checkpoint pass — user reported "the two tables are not aligned, this looks weird"
- **Root cause:** the left column ("Current Members") went straight from its `<h5>` to its table; the right column had a search form between its `<h5>` and table, pushing its table header down relative to the left one.
- **Fix (round 1):** added an `invisible` Bootstrap-utility spacer to the left column mirroring the search form's exact structure, so both columns reserved identical vertical space.
- **Superseded by:** the Current Members search feature (round 2) — the invisible spacer was removed and replaced with a real, functionally identical search form, which produces the same alignment as a side effect of being structurally real rather than a dummy spacer.
- **Commit:** `a16dc56` (spacer), later removed in `ad84a76` when the real search form replaced it.

**2. [Scope addition] Server-side search on Current Members**
- **Requested during:** second checkpoint pass — user wanted a real search bar on Current Members with the same server-side round-trip rigor as Available Users, not a client-side filter.
- **Not in original plan:** `40-03-PLAN.md`'s `files_modified` only declared `Members.cshtml`/`Members.Mobile.cshtml`; this addition also touched `IGroupRepository.cs`, `IGroupService.cs`, `GroupService.cs`, `GroupRepository.cs`, `GroupController.cs`, `GroupMembersViewModel.cs`, plus new/updated unit and integration tests.
- **Implementation:** `GetMembersAsync(groupId, search?)` added at repository/service/interface level, mirroring `GetAvailableUsers`'s existing `.Contains()` filter pattern exactly. Controller actions (`Members`, `AddMember`, `CreateMember` — all outcome branches and re-render paths —, `RemoveMember`) now thread both `search` and `memberSearch` through every redirect/re-render so neither list's filter is reset by an action on the other.
- **Tests added:** `GroupServiceTests.cs` (3 unit tests: delegation, null-forwarding, default-parameter behavior) and 5 new integration tests (member-search round-trip, email-match filtering, dual-search-preservation across Add/Remove/CreateMember).
- **Verification:** `dotnet build` succeeded; full unit suite 135/135 and full integration suite 279/279 passed after this round.
- **Committed in:** `fcc820f` (backend), `ad84a76` (view)

**3. [Scope addition] Header restructures**
- **Requested during:** second and third checkpoint passes.
- **Members.cshtml / Members.Mobile.cshtml:** header changed to `d-flex justify-content-between align-items-center` — title left, "Back to Groups" + "Create New User" buttons right — removing the standalone button row and its `<hr>` from the card body. In scope for this plan (same files already being modified).
- **Group/Index.cshtml (out-of-plan file):** the coordinator initially relayed a request to also restructure `Index.cshtml`'s header (move "Create Group" into the header bar). This was explicitly deferred back to the user rather than silently applied, since `Index.cshtml` is not in `40-03-PLAN.md`'s `files_modified` or `<artifacts_this_phase_produces>`, and it's a different page the human verifier hadn't looked at in this checkpoint. **The user then explicitly confirmed they wanted it applied**, at which point it was implemented: "Create Group" button moved into the card header, right-aligned, matching the same pattern used on the Members pages. `Index.Mobile.cshtml` was checked and required no change — it already used this exact header/button layout.
- **Verification:** `dotnet build` succeeded; full unit suite 135/135 and full integration suite 279/279 passed with no regressions (pure markup change).
- **Committed in:** `ad84a76` (Members headers), `bad762e` (Group/Index.cshtml header — out-of-plan file)

---

**Total deviations:** 1 superseded fix + 2 user-confirmed scope additions (spanning 3 checkpoint round-trips)
**Impact on plan:** No unapproved scope creep — every deviation beyond the original plan text was either a direct fix for a defect the user found, or a feature/change the user explicitly requested and confirmed mid-checkpoint (including one item — the Group/Index.cshtml header — that was deliberately surfaced back to the user for confirmation rather than silently applied, before being implemented once approved). All rounds passed a full build + full test suite before proceeding.

## Issues Encountered

- The local dev server (started in this worktree to support checkpoint verification) repeatedly held `QuestBoard.Domain.dll`/`QuestBoard.Repository.dll` locked across rebuild attempts (same failure mode documented in `CLAUDE.md`'s "Build failures due to locked files" note, but caused by this worktree's own background dev-server process rather than a Visual Studio debugger). Resolved each time by identifying and killing the specific `QuestBoard.Service`/`dotnet run` process (never a blanket kill of all `dotnet.exe` processes, which include unrelated MSBuild/IDE worker nodes) before rebuilding, then restarting the dev server fresh after each successful build.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 40 (Platform Members Page Redesign) is now fully complete across all 3 plans (40-01 query/viewmodel layer, 40-02 controller wiring, 40-03 this plan's view redesign + dual search + header restructures).
- All three plan requirements (MEMBERS-01, MEMBERS-02, MEMBERS-03) are satisfied and verified end-to-end by the user in the running app.
- Full test suite green: 135/135 unit tests, 279/279 integration tests, no known regressions.
- No blockers for closing out this phase or moving to the next item in the v6.1 Bugfixes milestone (group-admin "Delete" button / SuperAdmin disable-account work, per `PROJECT.md`'s Active requirements).

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml
- FOUND: QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml
- FOUND: QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
- FOUND: QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
- FOUND: QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs
- FOUND: QuestBoard.Domain/Interfaces/IGroupRepository.cs
- FOUND: QuestBoard.Domain/Interfaces/IGroupService.cs
- FOUND: QuestBoard.Domain/Services/GroupService.cs
- FOUND: QuestBoard.Repository/GroupRepository.cs
- FOUND: QuestBoard.UnitTests/Services/GroupServiceTests.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
- FOUND commit c8c4a77: feat(40-03): redesign Members.cshtml into two-column layout with search, per-row Add, and Create New User modal
- FOUND commit aeb9208: feat(40-03): redesign Members.Mobile.cshtml into stacked layout with search, per-row Add cards, and reused Create New User modal
- FOUND commit a16dc56: fix(40-03): align Current Members and Available Users table headers
- FOUND commit fcc820f: feat(40-03): add server-side search to Current Members list
- FOUND commit ad84a76: feat(40-03): add Current Members search bar and restructure page header
- FOUND commit bad762e: feat(40-03): restructure Group Management page header
- CONFIRMED: dotnet build QuestBoard.slnx succeeded on every round (final state re-verified)
- CONFIRMED: dotnet test QuestBoard.UnitTests — 135/135 passed (final full-suite run)
- CONFIRMED: dotnet test QuestBoard.IntegrationTests — 279/279 passed (final full-suite run)
- CONFIRMED: dev server stopped (port 8000/8001 unreachable after taskkill)

---
*Phase: 40-platform-members-page-redesign*
*Completed: 2026-07-04*
