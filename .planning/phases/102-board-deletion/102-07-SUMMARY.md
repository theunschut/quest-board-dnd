---
phase: 102-board-deletion
plan: 07
subsystem: platform-ui
tags: [razor, platform, board-deletion, confirmation-page]
requires:
  - phase: 102-06
    provides: IGroupService.GetDeletionPreviewAsync and BoardDeletionPreview
  - phase: 102-04
    provides: full board delete and the confirmName POST
provides:
  - BoardDeletePageViewModel (Create, DeletedRows, UnlinkedRows, NameMismatch, HasNothingStored) and BoardDeleteRowViewModel
  - Shared _BoardDeleteBody partial rendered by both Delete twins, with the typed-name form and refusal markup
  - CSS class contract for plan 102-08 sheets
affects: [102-08, 102-09]
tech-stack:
  added: []
  patterns: [one shared partial under two layout-specific twins, Razor local function for repeated row markup]
key-files:
  created:
    - QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeleteRowViewModel.cs
    - QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml
  modified:
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Delete.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
key-decisions:
  - "The Delete button is not rendered disabled in this plan; plan 102-08 adds the disabled attribute together with the gate script, so a browser delete keeps working in between."
  - "Refusal markup uses Razor null-valued conditional attributes so is-invalid, aria-invalid and autofocus are absent unless NameMismatch is set."
metrics:
  tasks: 2
  commits: 2
status: complete
actuals:
  tasks: 2
  commits: 2
---

# Phase 102 Plan 07: Board delete confirmation page Summary

GET Platform Group Delete now shows, per type, what a delete removes (counts, collapsed name lists), the rows on other boards that are only unlinked, an always-present Not affected row and the typed-name form, on desktop and the phone twin through one shared partial.

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 | Confirmation page on both twins through one shared partial | 1b4529fc |
| 2 | Refusal markup on the shared partial and Group management delete tests | ef724cc5 |

## What was built

- `BoardDeletePageViewModel.Create(group, preview, nameMismatch)` keeps only non-zero kinds in preview order and maps each through one label table taken from the UI contract (icon, singular, plural, hint, summary). Singular exactly at count 1, blank names become "(untitled)", duplicates stay. Unlinked rows (follow-up quests named "{title} (on {board})", signups losing a character, events leaving a series) appear only above zero.
- `_BoardDeleteBody.cshtml` renders intro, facts line (type badge, `Html.LocalTime` created date, "No members"), danger alert, What will be deleted (or the empty-board sentence), Kept but unlinked (hidden at zero), Not affected, and the form (`data-disable-on-submit`, `confirmName` input with `data-board-delete-name`, help, status region, Keep board / Delete board row). Names are Razor-encoded only; no inline style, script or handler; no `value` attribute on the input.
- When `NameMismatch` is set the partial renders `#board-delete-refusal` (`role="alert"` refusal line) above an empty input with `is-invalid`, `aria-invalid="true"`, `autofocus` and an extended `aria-describedby`.
- `Delete.cshtml` and `Delete.Mobile.cshtml` keep their own header chrome (h2 desktop, h1 in `container-fluid px-2 mt-2` phone) and drop the legacy phone card wrapper and sheet link.
- Tests: eight new GET facts in `PlatformBoardDeleteTests` (counts and collapsed names, unlinked rows, hidden unlinked section, empty board, Not affected, typed-name field, encoding, phone twin); `GroupManagementIntegrationTests` delete facts now assert the new page and post `confirmName`, expecting a redirect and a removed row.

## Deviations from Plan

None - plan executed as written. The refusal markup has no HTTP-level test yet because nothing sets `NameMismatch` until plan 102-09 re-renders the page on a wrong name; the GET fact asserts it is absent in the normal state.

## Verification

- Verify filters for both tasks passed (110 and 44 tests).
- Full unit suite 1353 passed; full integration suite 1824 passed, 13 skipped (SQL Server tests skip without the connection variable), 0 failed.
- Acceptance greps: no `Html.Raw`, no inline style/script/handler, no `value=`, no legacy phone wrapper or sheet in the phone twin.

## Known Stubs

None.

## Threat Flags

None. No new route; the page is served only by the existing `SuperAdminOnly` GET action, the form uses the `asp-action` tag helper so the antiforgery token renders, and encoding is covered by `Get_EncodesUserAuthoredNames`.

## Self-Check: PASSED

All created files exist and commits 1b4529fc and ef724cc5 are on the branch.
