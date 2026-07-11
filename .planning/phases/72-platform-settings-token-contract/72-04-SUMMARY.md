---
phase: 72-platform-settings-token-contract
plan: 04
subsystem: platform-ui
tags: [razor, mvc, superadmin, omphalos-settings]

# Dependency graph
requires:
  - "IPlatformSettingService — GetForScopeAsync/SaveAsync/GenerateAndSaveSecretAsync (72-03)"
provides:
  - "IntegrationsController — SuperAdmin-only /platform/Integrations page for the instance-wide default (GroupId == null)"
  - "IntegrationSettingsViewModel — masked-secret ViewModel, never carries the real stored secret"
  - "Integrations nav entry on Group/Index (desktop + mobile)"
affects: [72-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sibling <form> outside the main form + HTML5 form=\"id\" attribute on the Save button, avoiding nested <form> elements while keeping Generate Secret and Save Settings in one button row"
    - "TempData-carried one-time plaintext secret (GeneratedSecret key), read once in the GET action and mapped into SharedSecret for a single masked-field render"

key-files:
  created:
    - QuestBoard.Service/Areas/Platform/Controllers/IntegrationsController.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/IntegrationSettingsViewModel.cs
    - QuestBoard.Service/Areas/Platform/Views/Integrations/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Integrations/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/platform-integrations.mobile.css
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml

key-decisions:
  - "Generate Secret is its own sibling <form> (not nested inside the main Index form); Save Settings uses the HTML5 form=\"integrationsForm\" attribute to submit the main form from outside its markup, keeping Back/Generate Secret/Save Settings in a single d-flex button row without violating HTML's no-nested-forms rule."
  - "The freshly generated secret is read from TempData once in the GET action and assigned to IntegrationSettingsViewModel.SharedSecret so it populates the masked field for exactly one render, per D-03/UI-SPEC's literal 'shown once in the masked field' wording — no reveal/eye-icon toggle was added since none was specified in the UI-SPEC or plan."
  - "Added a .form-text color rule to platform-integrations.mobile.css (not present in the platform-users.mobile.css template, which has no form) so the secret-status caption and URL helper text stay readable against the blurred glass background, consistent with the .text-muted treatment already established in the sibling file."

requirements-completed: [SETT-01, SETT-02, SETT-03, SETT-04, SETT-05, SETT-07]

coverage:
  - id: D1
    description: "A SuperAdmin reaches an Integrations page from /platform and saves an Omphalos URL, masked shared secret, and enabled toggle as the instance-wide default"
    requirement: SETT-01
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service"
        status: pass
    human_judgment: true
  - id: D2
    description: "The GET view never renders the real stored secret; a configured/not-configured badge and caption are shown instead"
    requirement: SETT-03
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service (code inspection: GET Index never assigns settings.SharedSecret to the ViewModel)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Submitting with the secret field blank preserves the previously-saved secret"
    requirement: SETT-04
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service (code inspection: POST Index maps IsNullOrWhiteSpace(model.SharedSecret) to null before calling SaveAsync)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Generate Secret creates a CSPRNG secret, persists it immediately, and shows it once"
    requirement: SETT-02
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service (code inspection: GenerateSecret calls GenerateAndSaveSecretAsync then redirects, never defers persistence)"
        status: pass
    human_judgment: true
  - id: D5
    description: "IntegrationsController is gated SuperAdminOnly at the class level and both mutating actions carry ValidateAntiForgeryToken"
    requirement: SETT-07
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service (code inspection: [Authorize(Policy = \"SuperAdminOnly\")] on the class; [ValidateAntiForgeryToken] on Index POST and GenerateSecret)"
        status: pass
    human_judgment: false
  - id: D6
    description: "Desktop and mobile views render the masked-secret input and modern-card shell; a third header button links Group Management to Integrations"
    requirement: SETT-01
    verification:
      - kind: unit
        ref: "grep gates: type=\"password\" on both views, modern-card on desktop, asp-controller=\"Integrations\" on both Group/Index views"
        status: pass
    human_judgment: false

# Metrics
duration: 6min
completed: 2026-07-11
status: complete
---

# Phase 72 Plan 04: Instance-Wide Integrations Settings Page Summary

**SuperAdmin-only `/platform/Integrations` page (controller + ViewModel + desktop/mobile modern-card views + mobile CSS + nav entry) that reads and writes the instance-wide Omphalos default through `IPlatformSettingService` with scope `null`, never exposing the stored secret.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-11T14:43:56Z
- **Completed:** 2026-07-11T14:49:52Z
- **Tasks:** 3
- **Files modified:** 7 (5 created, 2 modified)

## Accomplishments
- `IntegrationsController` — `[Area("Platform")] [Authorize(Policy = "SuperAdminOnly")]`, `Index` GET (resolves scope `null` via `GetForScopeAsync`, never assigns the real secret to the ViewModel, surfaces a freshly generated secret from TempData for one render), `Index` POST (`[ValidateAntiForgeryToken]`, blank-preserve guard, re-renders with `HasSecretConfigured` on validation failure), `GenerateSecret` POST (`[ValidateAntiForgeryToken]`, persists immediately, carries the plaintext once via `TempData["GeneratedSecret"]`)
- `IntegrationSettingsViewModel` — `[Url]`-annotated `OmphalosUrl` with the UI-SPEC's exact validation message, `[StringLength(200)] SharedSecret`, `IsEnabled`, `HasSecretConfigured`; no property ever carries the persisted raw secret
- `Index.cshtml` / `Index.Mobile.cshtml` — modern-card (desktop) / `.platform-integrations-card-mobile` (mobile) settings form matching the UI-SPEC's copy, colors (`btn-warning` Save Settings, `btn-danger` confirm-guarded Generate Secret), plain `form-check` toggle (never `form-switch`), `<hr>` before a `d-flex justify-content-between` button row
- `platform-integrations.mobile.css` — follows `platform-users.mobile.css`'s glass-card structure verbatim, plus one added `.form-text` rule for this page's form fields (the template has no form to style)
- Third header button ("Integrations", `btn-secondary`, `fa-plug`) added to `Group/Index.cshtml` and `Index.Mobile.cshtml`, alongside "Create Group"/"Manage Users"

## Task Commits

Each task was committed atomically:

1. **Task 1: IntegrationsController + IntegrationSettingsViewModel** - `b4e63fc5` (feat)
2. **Task 2: Desktop + mobile Integrations views and mobile CSS** - `fa2f1b11` (feat)
3. **Task 3: Nav entry — Integrations header button on Group/Index (desktop + mobile)** - `2d459084` (feat)

## Files Created/Modified
- `QuestBoard.Service/Areas/Platform/Controllers/IntegrationsController.cs` - GET/POST Index, POST GenerateSecret
- `QuestBoard.Service/ViewModels/PlatformViewModels/IntegrationSettingsViewModel.cs` - masked-secret ViewModel
- `QuestBoard.Service/Areas/Platform/Views/Integrations/Index.cshtml` - desktop settings form
- `QuestBoard.Service/Areas/Platform/Views/Integrations/Index.Mobile.cshtml` - mobile settings form
- `QuestBoard.Service/wwwroot/css/platform-integrations.mobile.css` - mobile glass-card styling
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` - added Integrations header button
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` - added Integrations header button

## Decisions Made
- Generate Secret is a sibling `<form>` outside the main Index form (not nested), and the Save Settings button uses the HTML5 `form="integrationsForm"`/`form="integrationsFormMobile"` attribute to submit the main form from outside its own markup — keeps Back/Generate Secret/Save Settings in one `d-flex justify-content-between` row without nesting `<form>` elements, which HTML disallows.
- The freshly generated secret populates `IntegrationSettingsViewModel.SharedSecret` for exactly one GET render (read from `TempData["GeneratedSecret"]`, which is cleared after that render) — implemented literally per D-03/UI-SPEC's "shown once in the masked field" wording; no reveal/eye-icon toggle was added since the UI-SPEC and plan do not call for one.
- Added a `.form-text` color rule to `platform-integrations.mobile.css` beyond what `platform-users.mobile.css` has, since the template page (Users) has no form fields to style — keeps the URL helper text and secret-status caption readable against the blurred glass background, using the same faded-parchment treatment already applied to `.text-muted`/`small` in the sibling file.

## Deviations from Plan

None — plan executed exactly as written. The one addition beyond the literal template copy (the `.form-text` CSS rule) is a direct, same-pattern extension of the existing `platform-users.mobile.css` convention to cover this page's form fields, not a new visual language.

## Issues Encountered

None. `dotnet build QuestBoard.Service` succeeded after every task; all three plan-specified grep verification gates (masked-secret `type="password"` on both views, `modern-card` on desktop, `asp-controller="Integrations"` on both `Group/Index` views) passed.

## User Setup Required

None — no external service configuration, no new packages, no migration in this plan (schema and service landed in 72-01/72-03).

## Next Phase Readiness
- The instance-wide Integrations page is fully wired to `IPlatformSettingService` with scope `null` and ready for the Wave 4 authorization matrix (72-06) to exercise `SuperAdminOnly` end-to-end.
- 72-05 (group-override page) can proceed independently — no shared files between this plan's Platform-area surface and 72-05's group-scoped Admin-area surface.
- No blockers for 72-05/72-06.

---
*Phase: 72-platform-settings-token-contract*
*Completed: 2026-07-11*

## Self-Check: PASSED

All 5 created files verified present on disk; all 3 task commit hashes (`b4e63fc5`, `fa2f1b11`, `2d459084`) plus the summary commit (`03d0dbff`) verified present in git log.
