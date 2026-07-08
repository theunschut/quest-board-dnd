---
phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co
plan: 01
subsystem: Players page ViewModel (unrelated to the Characters/GuildMembers roster rename)
tags: [rename, viewmodel, players]
dependency-graph:
  requires: []
  provides:
    - PlayersIndexViewModel
  affects:
    - QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs
    - QuestBoard.Service/Views/Players/Index.cshtml
    - QuestBoard.Service/Views/Players/Index.Mobile.cshtml
    - QuestBoard.Service/Views/_ViewImports.cshtml
tech-stack:
  added: []
  patterns:
    - "ViewModels/[Feature]ViewModels/ folder-per-feature convention"
key-files:
  created:
    - QuestBoard.Service/ViewModels/PlayersViewModels/PlayersIndexViewModel.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs
    - QuestBoard.Service/Views/Players/Index.cshtml
    - QuestBoard.Service/Views/Players/Index.Mobile.cshtml
    - QuestBoard.Service/Views/_ViewImports.cshtml
decisions:
  - "New class name: PlayersIndexViewModel, matching the ViewModels/[Feature]ViewModels/ convention already used by CharacterViewModels, AccountViewModels, CalendarViewModels, QuestViewModels"
  - "Reworded 'The guild registry is currently empty. Brave souls may register as new quest leaders.' to 'No dungeon masters are registered yet. Brave souls may register as new quest leaders.' — drops the guild framing while keeping the same tone and second sentence"
metrics:
  duration: "~15 minutes"
  completed: 2026-07-06
status: complete
---

# Phase 58 Plan 01: Rename GuildMembersIndexViewModel to PlayersIndexViewModel Summary

Renamed and relocated the misnamed `GuildMembersIndexViewModel` (actually the Players/DM-roster page's view model, unrelated to the Characters feature) to `PlayersIndexViewModel` in a new `ViewModels/PlayersViewModels/` folder, updated its three consumers, and reworded the "guild registry" empty-state copy — closing the D-02 naming collision ahead of the broader Guild Members → Characters rename in the other plans of this phase.

## What Was Built

- **`QuestBoard.Service/ViewModels/PlayersViewModels/PlayersIndexViewModel.cs`** — new file, namespace `QuestBoard.Service.ViewModels.PlayersViewModels`, class `PlayersIndexViewModel` with `IEnumerable<User> DungeonMasters = []` and `IEnumerable<User> Players = []`, carried over verbatim from the deleted `GuildMembersIndexViewModel`.
- Deleted `QuestBoard.Service/ViewModels/GuildMembersViewModels/GuildMembersIndexViewModel.cs` and the now-empty `GuildMembersViewModels/` folder.
- Updated all three consumers:
  - `PlayersController.cs` — `using` statement and `new PlayersIndexViewModel { ... }` construction.
  - `Views/Players/Index.cshtml` — `@using`/`@model` updated; empty-state copy reworded.
  - `Views/Players/Index.Mobile.cshtml` — same `@using`/`@model` update; identical copy reword for consistency.
- Updated the global `@using QuestBoard.Service.ViewModels.GuildMembersViewModels` in `Views/_ViewImports.cshtml` to `@using QuestBoard.Service.ViewModels.PlayersViewModels`, per the research's Open Question #1 recommendation (update the namespace rather than remove the line — lowest risk).

## Deviations from Plan

None - plan executed exactly as written.

## Verification

- `grep -rn "GuildMembersIndexViewModel" QuestBoard.Service/` → 0 hits
- `grep -rn "GuildMembersViewModels" QuestBoard.Service/` → 0 hits
- `grep -rin "guild registry" QuestBoard.Service/Views/Players/` → 0 hits
- `dotnet build QuestBoard.Service` → Build succeeded, 0 Warning(s), 0 Error(s)

## Self-Check: PASSED

- FOUND: QuestBoard.Service/ViewModels/PlayersViewModels/PlayersIndexViewModel.cs
- FOUND (confirmed absent): QuestBoard.Service/ViewModels/GuildMembersViewModels/ (directory no longer exists)
- FOUND: commit 96f24a8 (feat(58-01): rename GuildMembersIndexViewModel to PlayersIndexViewModel)
- FOUND: commit 1119c16 (feat(58-01): update PlayersIndexViewModel consumers and reword empty-state copy)
