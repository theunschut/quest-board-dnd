---
phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co
verified: 2026-07-06T20:30:00Z
status: passed
score: 8/8 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 58: Rename the Guild Members feature to Characters everywhere Verification Report

**Phase Goal:** The character-roster feature is called "Characters" everywhere in the Service project — `CharactersController` serving `/Characters/*`, `Views/Characters/`, `characters.css`/`characters.mobile.css`, nav labels, and all user-facing copy — matching the Domain/Repository layers that were already "Character"-named, with zero behavior change. The stray `GuildMembersIndexViewModel` (an unrelated Players-page view model) is renamed to `PlayersIndexViewModel` in the same pass so a repo-wide grep for "GuildMembers" returns zero hits in the Service and IntegrationTests projects.

**Verified:** 2026-07-06T20:30:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `CharactersController` exists and serves `/Characters/*` (no `GuildMembersController` remains) | ✓ VERIFIED | `QuestBoard.Service/Controllers/Characters/CharactersController.cs` exists, declares `public class CharactersController(...)`. `GuildMembersController.cs` confirmed absent (ls: No such file). No `[Route]`/`[Area]` overrides — convention-based routing moves the route surface automatically. |
| 2 | `Views/Characters/` holds all 8 views; `Views/GuildMembers/` no longer exists | ✓ VERIFIED | `ls QuestBoard.Service/Views/Characters/` lists exactly 8 files (Index/Details/Edit/Create × desktop/mobile). `Views/GuildMembers/` confirmed absent. |
| 3 | `characters.css`/`characters.mobile.css` exist with `character-*` classes; old `guild-members*.css` gone | ✓ VERIFIED | Both new files exist. Old files absent. `characters.css` has 40 occurrences of `.characters-page`; `characters.mobile.css` contains `.character-section-card`, `.character-member-row`, `.character-member-thumbnail`, `.character-empty-state`, etc. |
| 4 | `GuildMembersIndexViewModel` renamed to `PlayersIndexViewModel` in `ViewModels/PlayersViewModels/`; old folder gone | ✓ VERIFIED | `PlayersIndexViewModel.cs` exists with correct namespace/class/properties (`DungeonMasters`, `Players` both `IEnumerable<User> = []`). `ViewModels/GuildMembersViewModels/` confirmed absent. `PlayersController.cs` constructs `new PlayersIndexViewModel`. Views/Players/Index(.Mobile).cshtml reference `@model PlayersIndexViewModel`. `_ViewImports.cshtml` updated to the new namespace. |
| 5 | Nav labels read "Characters" (desktop + mobile), not "Guild Members"; desktop layout loads `characters.css` | ✓ VERIFIED | `_Layout.cshtml:131-132` — `asp-controller="Characters"` with visible text `Characters`; line 19 loads `~/css/characters.css`. `_Layout.Mobile.cshtml:113-114` — same `asp-controller="Characters"` + visible "Characters" text. |
| 6 | All 6 `Url.Action("GetProfilePicture", ...)` cross-references target `"Characters"` (profile pictures don't 404 on Quest/QuestLog pages) | ✓ VERIFIED | `Quest/Details.cshtml` (×2), `Quest/Manage.cshtml`, `Quest/_QuestCard.cshtml`, `QuestLog/Details.cshtml`, `QuestLog/Details.Mobile.cshtml` all use `Url.Action("GetProfilePicture", "Characters", ...)`. The in-controller views (`Views/Characters/*`) correctly omit the controller-name argument (implicit same-controller resolution) — this is correct MVC usage, not a miss. |
| 7 | D-03: no `[Route("GuildMembers...")]` alias or redirect exists (clean break, no backward-compat) | ✓ VERIFIED | `grep -rn 'Route("GuildMembers' QuestBoard.Service/` → 0 hits. |
| 8 | Repo-wide grep for "guild" returns zero hits in Service + IntegrationTests projects; solution builds; full test suite green | ✓ VERIFIED | `grep -ri "guild" --include="*.cs" --include="*.cshtml" --include="*.css" QuestBoard.Service/ QuestBoard.IntegrationTests/` → 0 hits (independently re-run, not just trusting SUMMARY). `dotnet build` → Build succeeded, 0 Warning(s), 0 Error(s). `dotnet test` → QuestBoard.UnitTests 183/183 passed; QuestBoard.IntegrationTests 331/331 passed. Total 514/514, 0 failed. |

**Score:** 8/8 truths verified (0 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/Controllers/Characters/CharactersController.cs` | Renamed controller, 7 actions, convention routing | ✓ VERIFIED | Exists, class declared, no "GuildMembers" text remains (comment at former line 152 reworded per SUMMARY). |
| `QuestBoard.Service/Views/Characters/*.cshtml` (8 files) | Moved view folder | ✓ VERIFIED | All 8 present; folder move confirmed via git history in SUMMARY (git mv) and directory listing. |
| `QuestBoard.Service/wwwroot/css/characters.css` / `.mobile.css` | Renamed CSS, `character-*` classes | ✓ VERIFIED | Both exist, correct class names, zero "guild" text. |
| `QuestBoard.Service/ViewModels/PlayersViewModels/PlayersIndexViewModel.cs` | Renamed/relocated ViewModel | ✓ VERIFIED | Exists with correct namespace, class, and properties; wired to `PlayersController` and both Players views. |
| `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` | Renamed integration test class | ✓ VERIFIED | Exists; class named `CharactersControllerIntegrationTests`; contains all Phase 56 owner-or-admin authorization test methods (`Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed`, etc.) with 17 `"/Characters` route strings; old file absent. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `CharactersController.cs` | `Views/Characters/Index.cshtml` | MVC default view-location convention | ✓ WIRED | Controller class name and view folder name match; `dotnet build`/`dotnet test` confirm no view-resolution errors. |
| `Views/Characters/Index.Mobile.cshtml` | `wwwroot/css/characters.mobile.css` | `<link>` tag | ✓ WIRED | `Index.Mobile.cshtml` references `characters.mobile.css`; verified in Plan 03 SUMMARY and cross-checked via grep. |
| `PlayersController.cs` | `PlayersIndexViewModel.cs` | constructs and returns | ✓ WIRED | `new PlayersIndexViewModel` construction confirmed via grep. |
| `Views/Players/Index.cshtml` | `PlayersIndexViewModel.cs` | `@model PlayersIndexViewModel` | ✓ WIRED | Both desktop and mobile Players views use the correct `@using`/`@model`. |
| `Quest/_QuestCard.cshtml`, `Quest/Details.cshtml`, `Quest/Manage.cshtml`, `QuestLog/Details(.Mobile).cshtml` | `CharactersController.GetProfilePicture` | `Url.Action("GetProfilePicture", "Characters", ...)` | ✓ WIRED | All 6 call sites confirmed pointing at `"Characters"`. |
| `_Layout.cshtml` / `_Layout.Mobile.cshtml` | `CharactersController` | `asp-controller="Characters"` nav link | ✓ WIRED | Both layouts confirmed; visible text reads "Characters". |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution builds | `dotnet build` (repo root) | Build succeeded, 0 Warning(s), 0 Error(s) | ✓ PASS |
| Full test suite passes | `dotnet test` (repo root) | QuestBoard.UnitTests 183/183; QuestBoard.IntegrationTests 331/331; total 514/514, 0 failed | ✓ PASS |
| Phase 56 authorization regression tests survive rename | `grep -n "Edit_.*Should" CharactersControllerIntegrationTests.cs` | 6 Phase-56-style authorization test methods present and passing (part of the 331 passed) | ✓ PASS |
| Zero-guild completeness sweep (phase's own bar) | `grep -ri "guild" --include="*.cs" --include="*.cshtml" --include="*.css" QuestBoard.Service/ QuestBoard.IntegrationTests/` | 0 hits | ✓ PASS |
| D-03 no route-alias | `grep -rn 'Route("GuildMembers' QuestBoard.Service/` | 0 hits | ✓ PASS |
| Domain defensive cross-check | `grep -ri "guild" --include="*.cs" QuestBoard.Domain/` | 0 hits | ✓ PASS |

### Requirements Coverage

No requirement IDs apply to this phase. PLAN frontmatter across all 6 plans (58-01 through 58-06) declares `requirements: []`. `.planning/REQUIREMENTS.md` contains no reference to "Phase 58" and no orphaned REQ-IDs point at this phase — consistent with the ad-hoc backlog classification (source of truth: 58-CONTEXT.md decisions D-01 through D-03).

### Anti-Patterns Found

None. Scanned all phase-modified files (`CharactersController.cs`, `Views/Characters/*`, `PlayersIndexViewModel.cs`, `PlayersController.cs`, `Views/Players/*`, `characters.css`/`.mobile.css`, `_Layout.cshtml`/`.Mobile.cshtml`, `CharactersControllerIntegrationTests.cs`, `MobileViewsTests.cs`, `LayoutNavigationTests.cs`) for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` — zero hits.

### Human Verification Required

None. This is a mechanical, zero-behavior-change rename phase; all must-haves are grep/build/test verifiable and were independently re-run (not just trusted from SUMMARY.md).

### Gaps Summary

No gaps found. All 8 derived must-haves (roadmap goal decomposed) verified against the actual codebase:
- Controller/route rename complete and building.
- View folder move complete, MVC convention intact (build + tests prove view resolution).
- CSS rename complete with class-token parity.
- ViewModel rename/relocation (D-02) complete and wired through all 3 consumers.
- Nav labels and cross-controller references updated everywhere.
- D-03 clean-break honored (no route alias).
- Phase's own completeness bar (repo-wide zero-guild grep in Service + IntegrationTests) independently reproduced with 0 hits.
- Full build (0 errors) and full test suite (514/514 passed) independently reproduced, not just accepted from SUMMARY.md claims.

---

_Verified: 2026-07-06T20:30:00Z_
_Verifier: Claude (gsd-verifier)_
