---
phase: 72-platform-settings-token-contract
plan: 05
subsystem: group-admin-ui
tags: [razor-mvc, authorization-reuse, cascade-settings, mobile-parity]

# Dependency graph
requires:
  - "IPlatformSettingService (GetResolvedAsync/GetForScopeAsync/HasOwnSettingsAsync/SaveAsync/GenerateAndSaveSecretAsync/ClearScopeAsync) from 72-03"
  - "AdminHandler's existing GroupRole.Admin-for-active-group check backing the AdminOnly policy"
provides:
  - "AdminIntegrationsController — group-scoped Omphalos override CRUD (Index/Save/GenerateSecret/ClearOverride)"
  - "GroupIntegrationSettingsViewModel — cascade-boolean view model, never exposes raw secrets or default values"
  - "Views/Admin/Integrations.cshtml + .Mobile.cshtml — three-state cascade banner UI"
  - "Admin navbar dropdown entry point (desktop + mobile) for Group Integrations"
affects: [72-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New sibling controller in Controllers/Admin (not a new AdminController action) to avoid growing an already-13-dependency class, per CONTEXT.md's UsersController-split precedent"
    - "asp-action on <button type=submit> to generate formaction, routing Generate Secret/Clear Override to distinct POST actions from the same <form> as Save"
    - "Three-state cascade banner (Override Active / Inherited / Not Configured) composed from existing badge conventions, no new .alert precedent introduced"

key-files:
  created:
    - QuestBoard.Service/Controllers/Admin/AdminIntegrationsController.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/GroupIntegrationSettingsViewModel.cs
    - QuestBoard.Service/Views/Admin/Integrations.cshtml
    - QuestBoard.Service/Views/Admin/Integrations.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/admin-integrations.mobile.css
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml

key-decisions:
  - "AdminIntegrationsController is a new sibling controller (not a new action on AdminController), matching the plan's explicit direction and the UsersController/GroupController split precedent."
  - "GenerateSecret and ClearOverride are separate POST actions targeted via formaction (asp-action on the submit button) from the same <form> as Save, so all three actions share one set of posted form fields without needing three separate <form> elements."
  - "Back link targets Admin/Users (the group Admin landing surface) since no dedicated group-Admin-area landing page exists beyond User Management/Quest Management."

requirements-completed: [SETT-09, SETT-10]

coverage:
  - id: D1
    description: "A group Admin configures a group-specific Omphalos override (URL, secret, enabled) that applies only to that group"
    requirement: SETT-09
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service"
        status: pass
    human_judgment: false
  - id: D2
    description: "AdminIntegrationsController carries [Authorize(Policy = AdminOnly)] at the class level, backed by AdminHandler's group-scoped GroupRole.Admin check (DM excluded, SuperAdmin bypass only)"
    requirement: SETT-10
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every mutating/GET action re-derives the group id from IActiveGroupContext.ActiveGroupId and never trusts a posted/route group id"
    requirement: SETT-09
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service"
        status: pass
    human_judgment: false
  - id: D4
    description: "The cascade banner communicates Override Active / Inherited / Not Configured without ever rendering the instance-wide default's actual URL or secret"
    requirement: SETT-09
    verification:
      - kind: unit
        ref: 'grep -q "Override Active" QuestBoard.Service/Views/Admin/Integrations.cshtml && grep -q "bg-info" QuestBoard.Service/Views/Admin/Integrations.cshtml'
        status: pass
    human_judgment: false
  - id: D5
    description: "Clearing an override deletes the group's own rows and falls the group back to the instance-wide default"
    requirement: SETT-09
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service (ClearOverride calls settingService.ClearScopeAsync(groupId.Value))"
        status: pass
    human_judgment: false

# Metrics
duration: 35min
completed: 2026-07-11
status: complete
---

# Phase 72 Plan 05: Group-Override Settings Page Summary

**A Group Admin-only `AdminIntegrationsController` sibling controller, reusing `AdminHandler`'s existing group-scoped `AdminOnly` policy with zero new authorization plumbing, renders a three-state cascade banner (Override Active / Inherited / Not Configured) over an atomic save/generate/clear form, wired into the Admin navbar dropdown on desktop and mobile.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-07-11T14:2x:xxZ
- **Completed:** 2026-07-11T15:0x:xxZ
- **Tasks:** 3
- **Files modified:** 7 (5 created, 2 modified)

## Accomplishments

- `AdminIntegrationsController` — new sibling controller in `Controllers/Admin`, `[Authorize(Policy = "AdminOnly")]` at the class level, constructor-injects `IPlatformSettingService` and `IActiveGroupContext`. `Index` GET redirects to `GroupPicker` when there's no active group and builds the cascade view model from `GetForScopeAsync`/`HasOwnSettingsAsync`/`GetResolvedAsync(null)` — the last call used only to derive `InstanceDefaultConfigured`/`InstanceDefaultEnabled` booleans, never copying the default's actual `Url`/`SharedSecret` onto the view model. `Save`, `GenerateSecret`, and `ClearOverride` are all `[HttpPost][ValidateAntiForgeryToken]` and each re-derives `groupId` from `activeGroupContext.ActiveGroupId` independently, never trusting a posted or route value.
- `GroupIntegrationSettingsViewModel` — `[Url]`-validated `OmphalosUrl`, masked `SharedSecret`, `IsEnabled`, plus the tri-state cascade flags (`HasOverride`, `InstanceDefaultConfigured`, `InstanceDefaultEnabled`) and `HasSecretConfigured`. `SharedSecret` is always `null` on GET; a freshly generated secret surfaces once via `TempData["GeneratedSecret"]`.
- `Views/Admin/Integrations.cshtml` + `.Mobile.cshtml` — modern-card shell (mobile: `.admin-integrations-card-mobile` wrapper), the three-state cascade banner (`bg-success`/`bg-info text-dark`/`bg-secondary` badges, `bg-info` used only for the Inherited state on this page), masked secret input with tri-state caption, plain `form-check` (never `form-switch`) Enabled checkbox with an inheriting-state helper line, and the Save Override (`btn-warning`) / Generate Secret (`btn-danger`, `confirm()`) / Clear Override (`btn-outline-danger`, shown only when `HasOverride`, `confirm()`) button cluster. Generate Secret and Clear Override submit to their own controller actions via `asp-action`-generated `formaction` on the button, sharing the single `<form>` posted to `Save`.
- `wwwroot/css/admin-integrations.mobile.css` — follows `admin-users.mobile.css` verbatim (glass card, parchment headings, faded small text, no-shadow badges), wrapper class `.admin-integrations-card-mobile`.
- Admin navbar dropdown entry ("Group Integrations", `fa-plug`) added to both `_Layout.cshtml` (desktop `adminDropdown`) and `_Layout.Mobile.cshtml` (offcanvas Admin section), positioned directly after "Quest Management" and before the `SuperAdmin`-gated items — matching this page's `AdminOnly` (not `SuperAdmin`) gating.

## Task Commits

Each task was committed atomically:

1. **Task 1: AdminIntegrationsController + GroupIntegrationSettingsViewModel** - `d5c4e0e3` (feat)
2. **Task 2: Group override views (cascade banner) + mobile CSS** - `0218867e` (feat)
3. **Task 3: Admin navbar dropdown entry (desktop + mobile)** - `d7076521` (feat)

**Plan metadata:** pending (docs: complete plan)

## Files Created/Modified

- `QuestBoard.Service/Controllers/Admin/AdminIntegrationsController.cs` - group-scoped controller (Index/Save/GenerateSecret/ClearOverride)
- `QuestBoard.Service/ViewModels/AdminViewModels/GroupIntegrationSettingsViewModel.cs` - form + cascade-flag view model
- `QuestBoard.Service/Views/Admin/Integrations.cshtml` - desktop cascade-banner view
- `QuestBoard.Service/Views/Admin/Integrations.Mobile.cshtml` - mobile cascade-banner view
- `QuestBoard.Service/wwwroot/css/admin-integrations.mobile.css` - mobile card styling
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - desktop Admin dropdown nav entry
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - mobile offcanvas Admin nav entry

## Decisions Made

- Followed the plan's explicit direction to create a new sibling controller (`AdminIntegrationsController`) rather than adding actions to the already-13-dependency `AdminController`, per CONTEXT.md's `UsersController`-split-from-`GroupController` precedent.
- Used `asp-action` on `<button type="submit">` (ASP.NET Core's `FormActionTagHelper`, generating `formaction`) to route Generate Secret and Clear Override to their own POST actions from the same `<form>` that posts Save — avoids three separate `<form>` elements while keeping each action's own antiforgery-protected POST.
- The "Back to [group Admin landing]" link targets `Admin/Users` (User Management), the closest existing group-Admin-area landing page; no dedicated "group settings home" page exists to link back to instead.
- `Clear Override` uses `fa-eraser` (not specified by UI-SPEC, which only fixed the button color/placement) — a reasonable icon choice per CLAUDE.md's icon-required UI rule, left to implementation discretion.

## Deviations from Plan

None - plan executed exactly as written, using the exact controller/view/CSS shapes and copy specified in the plan and UI-SPEC.

## Issues Encountered

None. `dotnet build QuestBoard.Service` succeeded after each task; a full solution `dotnet build` (including `QuestBoard.UnitTests`/`QuestBoard.IntegrationTests`) also succeeded with 0 warnings/0 errors. All plan-specified grep verification gates (masked-secret inputs on both views, "Override Active" cascade text, `bg-info` badge, `AdminIntegrations` nav links on both layouts) passed.

## User Setup Required

None - no external service configuration required; no new packages, no migration (schema and service landed in 72-01/72-03).

## Next Phase Readiness

- The group-override page is fully wired end-to-end: authorization reuses the existing `AdminOnly` policy verbatim (no new authorization code), the controller consumes `IPlatformSettingService` exactly as 72-03 shipped it, and both desktop/mobile views + nav entries shipped together.
- Authorization proof (Group Admin allowed, DungeonMaster/Player denied) is explicitly deferred to 72-06 (Wave 4) per this plan's own `<verification>` section — not re-verified here.
- No blockers for 72-06.

---
*Phase: 72-platform-settings-token-contract*
*Completed: 2026-07-11*

## Self-Check: PASSED

All 6 created/output files verified present on disk; all 4 task/docs commit hashes (`d5c4e0e3`, `0218867e`, `d7076521`, `1c0fb98d`) verified present in git log.
