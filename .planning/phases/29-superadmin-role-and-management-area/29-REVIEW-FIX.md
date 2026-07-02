---
phase: 29-superadmin-role-and-management-area
fixed_at: 2026-06-30T00:00:00Z
review_path: .planning/phases/29-superadmin-role-and-management-area/29-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 29: Code Review Fix Report

**Fixed at:** 2026-06-30
**Source review:** `.planning/phases/29-superadmin-role-and-management-area/29-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 4
- Fixed: 4
- Skipped: 0

## Fixed Issues

### WR-01: `DemoteFromAdmin` sets role to `DungeonMaster` instead of `Player`

**Files modified:** `QuestBoard.Service/Controllers/Admin/AdminController.cs`
**Commit:** 1cc40e6
**Applied fix:** Changed `GroupRole.DungeonMaster` to `GroupRole.Player` in the `DemoteFromAdmin` action body (line 70). The `PromoteToDM` method directly below retains its `DungeonMaster` argument, which was correct and untouched.

---

### WR-02: Hangfire dashboard blocks SuperAdmin

**Files modified:** `QuestBoard.Service/Program.cs`, `QuestBoard.Service/Authorization/AdminDashboardAuthFilter.cs`
**Commit:** b54cfc5
**Applied fix:** In both locations that guard the Hangfire dashboard, added `&& !context.User.IsInRole("SuperAdmin")` alongside the existing `!IsInRole("Admin")` check. The inline middleware in Program.cs and the `AdminDashboardAuthFilter` class were updated consistently.

---

### WR-03: `AddMemberViewModel.UserId` default of `0` passes `[Required]` silently

**Files modified:** `QuestBoard.Service/ViewModels/PlatformViewModels/AddMemberViewModel.cs`
**Commit:** 86349f3
**Applied fix:** Added `[Range(1, int.MaxValue, ErrorMessage = "Please select a user.")]` attribute to `UserId`. The type remains `int` (non-nullable) as per the fix guidance. Model validation will now reject a zero value before it can reach the service layer.

---

### WR-04: Area route registration uses `MapControllerRoute` instead of `MapAreaControllerRoute`

**Files modified:** `QuestBoard.Service/Program.cs`
**Commit:** 98b3120
**Applied fix:** Replaced the `MapControllerRoute` call (which used `defaults: new { area = "Platform" }` and `constraints: new { area = "Platform" }` as a workaround) with the canonical `MapAreaControllerRoute(name: "platform", areaName: "Platform", pattern: "platform/{controller=Group}/{action=Index}/{id?}")`. The redundant constraint-based registration was removed.

---

_Fixed: 2026-06-30_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
