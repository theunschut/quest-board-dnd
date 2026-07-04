---
phase: quick-260420-n5f
plan: 01
status: complete
subsystem: account
tags: [haskey, account-edit, viewmodel, user-facing]
dependency_graph:
  requires: []
  provides: [HasKey round-trip on user-facing Edit form]
  affects: [Account/Edit page]
tech_stack:
  added: []
  patterns: [ViewModel property restoration, GET/POST round-trip]
key_files:
  created: []
  modified:
    - EuphoriaInn.Service/ViewModels/AccountViewModels/EditProfileViewModel.cs
    - EuphoriaInn.Service/Controllers/Admin/AccountController.cs
    - EuphoriaInn.Service/Views/Account/Edit.cshtml
decisions:
  - HasKey is informational only (not a privilege), so user-facing exposure is safe; SEC-04 only stripped it from privilege-elevation paths
metrics:
  duration_minutes: 3
  completed_date: "2026-04-20T16:06:07Z"
  tasks_completed: 1
  files_modified: 3
---

# Quick Task 260420-n5f: Restore HasKey Checkbox to User-Facing Account/Edit Form

**One-liner:** Restored `HasKey` bool property to `EditProfileViewModel` and wired GET population + POST persistence in `AccountController`, plus a form-check checkbox in `Edit.cshtml`.

## Objective

HasKey tracks who physically holds a building key — informational only, not a security permission. It was mistakenly removed from the user-facing edit form during SEC-04. This task restores the full GET/POST round-trip so users can view and update their own key-holder status.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Restore HasKey to EditProfileViewModel, controller, and view | e934add | EditProfileViewModel.cs, AccountController.cs, Edit.cshtml |

## Changes Made

### EditProfileViewModel.cs
Added `HasKey` bool property after `IsDungeonMaster`:
```csharp
[Display(Name = "Has Building Key")]
public bool HasKey { get; set; }
```

### AccountController.cs — Edit GET
Added `HasKey = user.HasKey` to the model initialiser so the current value is pre-populated.

### AccountController.cs — Edit POST
Added `user.HasKey = model.HasKey;` before `UpdateAsync` so the change is persisted.

### Edit.cshtml
Inserted a `form-check` block between the Email field and the first `<hr>`, using `asp-for="HasKey"`.

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None.

## Verification

- Build: `dotnet build` exits 0 with 0 errors (1 pre-existing warning unrelated to this task)
- All three artifacts satisfy the `must_haves.artifacts` conditions in the plan

## Self-Check: PASSED

- `EuphoriaInn.Service/ViewModels/AccountViewModels/EditProfileViewModel.cs` — modified, HasKey property present
- `EuphoriaInn.Service/Controllers/Admin/AccountController.cs` — modified, GET and POST assignments present
- `EuphoriaInn.Service/Views/Account/Edit.cshtml` — modified, form-check checkbox present
- Commit `e934add` exists in git log
