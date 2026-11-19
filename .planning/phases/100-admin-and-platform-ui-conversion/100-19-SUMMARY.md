---
phase: 100-admin-and-platform-ui-conversion
plan: 19
subsystem: ui
tags: [razor, platform, members, create-user-dialog, toast, confirm-submit, layered-css, guard-tests, mobile-twins]
requires:
  - phase: 100-12
    provides: Gate 1 answers (Members stacked with a Current Members heading on the phone, Dungeon Master wording, the modal keeps Cancel)
  - phase: 100-13
    provides: AdminPlatformDisplay mapping (GroupRoleOptions, GroupRoleBadge), area guard lists, .row-slip
  - phase: 100-14
    provides: _RoleBadge
  - phase: 100-16
    provides: platform-pages.css and platform-pages.mobile.css
  - phase: 100-18
    provides: the attribute confirmation pattern for Platform forms, the guard-list state
provides:
  - Platform Group/Members on both layouts, stacked full-width sections, labelled searches, shared role badges, Remove Member on the shared confirm, phone slips
  - A Create New User dialog on the opaque overlay surface that is named, labelled and posts the field names the action actually binds
  - A failed Create New User answered with the approved Error toast on a redirect to Members
  - platform-group.mobile.css deleted, the last old Platform Group phone sheet
affects: [100-21]
tech-stack:
  added: []
  patterns: [a dialog whose post is refused is answered by a redirect and a toast rather than a re-rendered page with a closed dialog; dialog inputs carry plain field names when the action binds the model unprefixed; a retired sheet moves from the pending list to the retired list in the commit that deletes it]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs
    - QuestBoard.Service/wwwroot/css/platform-pages.css
    - QuestBoard.Service/wwwroot/css/platform-pages.mobile.css
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/platform-group.mobile.css
key-decisions:
  - "O-12 answered toast, so no platform-members.js module was written and the reopen marker does not exist; GroupController.CreateMember redirects to Members with AdminActionMessages.CreateMemberFailed for an invalid model and for a Failed outcome"
  - "The dialog inputs are plain markup named Email, Name and GroupRole (the action binds CreateMemberViewModel unprefixed), not asp-for tag helpers: this is the prefix binding fix, and no typed values are kept because a refused post redirects"
  - "The Add form's role select uses AdminPlatformDisplay.GroupRoleOptions() directly; Player is the first option and so the default, which keeps the previous default without a selected flag"
  - "Group/Delete #board-delete-help (4.25:1) NOT fixed: Group/Delete links only the board deletion sheets and this plan owns none of them"
requirements-completed: [D-05, D-09, D-13, D-15, D-16, D-19, D-21, DoC-1, DoC-2, DoC-4, DoC-8, DoC-10]
duration: ~1h30m
completed: 2026-11-18
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 1
---

# Phase 100 Plan 19: Platform Members Summary

**Creating a user from the Members page works for the first time and a failed create is never silent: the dialog posts the field names the action binds, and a refused create lands on Members with the approved Error toast, on a page that reads stacked at every width.**

## What was built
- **Members (both layouts):** title card with one `h1` (`fa-users`, the existing title), Back to Groups (`btn-secondary`) and Create New User (`btn-primary`) in the wrapping `.page-bar`. Two `section.members-section` blocks, each with an `h2` (Current Members, Available Users) and a search form whose input has a visually hidden label ("Search members", "Search users"); the other section's query is carried as before. Desktop: tables in `.table-responsive`, a hairline between the sections, no half-width columns. Phone: one `.row-slip` per member and per available user, 44px controls. Roles come from `_RoleBadge` ("Dungeon Master", not the enum name), the Add role select from `GroupRoleOptions()`. Remove Member is a POST form with today's sentence in `data-confirm-message` and `fa-user-minus me-2`, no `onsubmit`; `confirm-submit.js` loads as a module in `@section Scripts`. The empty state and both search-miss lines keep their words; Clear search is a `.clear-search` link (32px desktop, 44px phone).
- **Create New User dialog:** on the opaque overlay surface (the `bg-dark`, `text-light`, `border-secondary` and `btn-close-white` utilities are gone), `aria-labelledby="createMemberModalTitle"` to an `h2.modal-title` with `fa-user-plus`, a close button named "Close", `.form-page` fields with the required marker and hidden "(required)" text, `autocomplete="off"`, the role select from the shared mapping, Cancel (Gate 1 item 19: the modal keeps it) and Create User as `btn-primary`.
- **X-3 and the O-12 outcome:** the old dialog posted `CreateMember.Email` and friends, which the action never bound, so no account could be created from the page. The inputs now post `Email`, `Name` and `GroupRole`. `GroupController.CreateMember` answers an invalid model and a `Failed` outcome with `TempData["Error"] = AdminActionMessages.CreateMemberFailed` ("The account could not be created. Check the details and try again.") and a redirect to Members; the success, already-member and stranded-account paths are untouched. The duplicate re-render code is gone.
- **Sheets:** `platform-pages.css` gained `.members-section` (+ hairline), `.members-section__title`, `.members-search`, `.add-member-form`, `.email-cell`, `.clear-search`; `platform-pages.mobile.css` gained the 44px search, add-form and Clear search rules. `platform-group.mobile.css` (the last consumer was these views) is deleted.
- **Guards and facts:** both views moved from PendingViews to ConvertedViews with their sheet links, PendingSheets is now empty and `platform-group.mobile.css` is in RetiredFiles, PhaseSourceFiles got both views and BoardPageStaticGuardTests. `BoardPageStaticGuardTests` names `platform-pages.mobile.css` as the successor (count and the Platform Group views kept). `PlatformBoardDeleteTests` needed no change: it asserts the phone Delete page carries neither the old card class nor the old sheet, which is now also true on disk. The two skipped GroupManagementIntegrationTests facts are enabled (one narrowed to the toast, one posts the names read from the rendered dialog). 14 new render cases in AdminPlatformRenderTests (7 facts, both layouts).

## Verification
- Unit 1636 passed. Integration 3025 passed, 17 skipped (19 before; the two CreateMember facts are enabled; the other 17 are the same ones as before), 0 failed.
- Sweep of `members`, `members-empty`, `members-search-miss`, `members-modal` and the afterOnly `members-modal-error` (an empty post from the opened dialog) under both User-Agents, 320 to 1440: 17 compared against BEFORE, 0 failing. Contrast failures 0, small targets 0, missing focus rings 0, overflow none, no console or page errors, no failed action or assert on all 21 captures. The error row shows the persistent Error toast with the approved sentence. allow.json holds only accessible-name renames (search inputs now named by their labels, the role selects "Role", the dialog fields now named by their labels with the required text, the dialog named by its title, the close button now "Close").
- Click-through (`tools/clicks-100-19.mjs`, `after/100-19/clicks.json`) under both User-Agents on P100 Audit One-Shot: focus enters the dialog, 12 Tab and 12 Shift+Tab presses stay inside it, Escape, Cancel and the close button each return focus to Create New User, `aria-labelledby` resolves to an `h2`, the close button is named "Close", the surface is opaque, no dark utility remains, nothing was posted. The Remove Member confirm for the long-name fixture was dismissed: dialog text equals today's sentence and the attribute, nothing sent, the row stayed. `fixtures.mjs --check` passed under both User-Agents.
- Members lists real accounts in Available Users: captures stay local and this summary is structural.

## Deviations from Plan
- **No reopen module:** O-12 was answered toast, so `platform-members.js`, the `data-reopen-on-load` marker and the KeyboardScriptGuardTests fact were not built (the plan's toast branch says to drop them). KeyboardScriptGuardTests and `AreaModules` are unchanged.
- **Tasks 1 and 2 in one commit:** the narrowed CreateMember facts read the field names from the rendered dialog, so the view conversion had to land with the controller change; a split would have left one half failing its own facts.
- **Inputs are plain markup, not tag helpers:** needed so the posted names match the unprefixed binding; the fields therefore carry no data-val attributes and rely on native `required`, `type=email`, `maxlength=100` plus the server check.
- **[Rule 3] Extra file:** `AdminActionMessages.cs` gained `CreateMemberFailed` (the shared home of the approved sentences); not in the plan's file list.
- **Reason lines under `text-muted` were dropped:** the search-miss and "all users are already members" lines lost `text-muted` (4.25:1 on glass) and take the card colour.
- **Open item (Group/Delete `#board-delete-help`, 4.25:1):** not fixed; this plan owns no sheet Group/Delete links.

## Known Stubs
None.

## Threat Flags
None. T-100-63 is closed by the toast and the narrowed fact; T-100-64 has no surface (no script); T-100-65 holds (the forms keep asp-action and the token, covered by the post-form scanner); T-100-66 held (local captures, structural report).

## Self-Check: PASSED
- Commit 41d4bc89 exists; `platform-group.mobile.css` is gone from disk and unlinked; both suites green.
