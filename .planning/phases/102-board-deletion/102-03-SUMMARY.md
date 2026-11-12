---
phase: 102-board-deletion
plan: 03
subsystem: platform-users
tags: [platform, users, razor, mobile, ef-core]
requires: []
provides:
  - "IUserRepository/IUserService.GetBoardNamesByUserAsync (one grouped query)"
  - "Boards column on /platform/Users (desktop) and boards details per user card (phone)"
affects: [platform-users]
tech-stack:
  added: []
  patterns: ["in-memory case-insensitive sort after a single projection query", "pages-layer tokens-only phone sheet"]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/platform-users-boards.mobile.css
    - QuestBoard.UnitTests/Repository/UserRepositoryBoardNamesTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/IUserRepository.cs
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.Repository/UserRepository.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs
    - QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs
    - QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs
decisions:
  - "Lookup lives on the user side (IUserRepository/IUserService) so UsersController's constructor is unchanged"
  - "Names sorted in memory with StringComparer.OrdinalIgnoreCase, independent of database collation"
metrics:
  tasks: 2
  commits: 2
status: complete
completed: 2026-11-11
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 102 Plan 03: Boards column on the Platform Users page Summary

Display-only board count per user on `/platform/Users`: desktop column with names on hover and in visually hidden text, phone collapsed details with a 44px summary, all fed by one grouped `UserGroups` query fetched once before the controller loop.

## Tasks

| Task | Commit | Result |
|------|--------|--------|
| 1. Desktop column end to end | ae103db7 | `GetBoardNamesByUserAsync` (repo, service), `BoardNames` on the view model, controller wiring, Boards `<th>`/`<td>`, unit and integration tests |
| 2. Phone details and sheet | d6c4e5fe | `.user-boards` details per card, `platform-users-boards.mobile.css` (pages layer, tokens only, no media query), phone integration facts |

## Verification

- `dotnet test QuestBoard.UnitTests --filter UserRepositoryBoardNamesTests|UserServiceTests`: 19 passed.
- `dotnet test QuestBoard.IntegrationTests --filter UsersControllerIntegrationTests|MobileCssTests|UiFoundation`: 115 passed.
- Acceptance greps for the sheet (no colour literal, no priority flag, no media query, `@layer pages {` in the first 2000 bytes) all pass.

## Deviations from Plan

One addition: an integration fact `Index_BoardNamesAreHtmlEncoded` for threat T-102-12 (the plan's mitigation says an integration fact asserts encoding). Otherwise the plan was executed as written.

## Known Stubs

None.

## Threat Flags

None. No new route or action; `UsersController` keeps its class-level `SuperAdminOnly` policy.

## Notes

- No visual (browser) check was run: the local SQL Server is down and this ran in an isolated worktree. Markup and CSS are covered by the HTTP-level integration tests only.

## Self-Check: PASSED

- Commits ae103db7 and d6c4e5fe exist on the worktree branch.
- All created files exist.
