---
phase: quick-261019-jba
plan: 01
subsystem: ui
tags: [razor, quest-manage, dm-controls, integration-tests]
requires: []
provides:
  - Desktop Quest Manage "DM Controls" sidebar card holding View Public Page, Create Follow-Up Quest, Send Reminder and Move to Linked Board
affects: [QuestBoard.Service/Views/Quest/Manage.cshtml]
tech-stack:
  added: []
  patterns: [Quest Details DM Controls card mirrored on desktop Manage]
key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs
  modified:
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs
decisions:
  - "Card order: View Public Page, Create Follow-Up Quest, Send Reminder, Move to Linked Board; no hr and no wrapper div, mirroring Quest Details"
  - "Phone layout of Quest Manage deliberately left unchanged"
metrics:
  tasks: 2
  commits: 3
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 3
---

# Phase quick-261019-jba Plan 01: DM Controls card on desktop Quest Manage Summary

Desktop Quest Manage now has a "DM Controls" sidebar card (replacing the "View Public Page" card) that stacks View Public Page, Create Follow-Up Quest, Send Reminder and Move to Linked Board full width, each under exactly its previous visibility condition.

## What changed

- `Manage.cshtml`: the sidebar card heading is now `DM Controls`. Create Follow-Up Quest (finalized one-shot, no follow-up only) and the Send Reminder form (finalized one-shot only, anti-forgery token and POST kept) moved out of the finalized action row, which keeps Edit Quest, Open Quest and Refresh Data. The Move to Linked Board block moved from the end of the main card into the card, as `btn btn-info w-100 mt-2` (enabled link, or disabled button plus the `move-reason` paragraph). The "Send again" form, the Quest Summary card and the campaign action row are untouched.
- `QuestManageDmControlsTests.cs` (new, 6 facts): card contents and order, main card no longer holding follow-up/reminder, follow-up hidden once one exists while reminder stays, campaign board and unfinalized quest showing neither, and a phone-layout pin.
- `BoardMoveControlTests.cs`: the desktop variants of four Manage theories now assert the move control sits exactly once inside the DM Controls card after View Public Page (enabled and disabled forms), and that the card still renders when no move control applies. Mobile expectations unchanged.

## Desktop/mobile divergence (intentional)

The operator decided the phone layout of Quest Manage stays as is. `Manage.Mobile.cshtml` keeps Create Follow-Up in its finalized row and the Move control under its action stack, and has no Send Reminder and no View Public Page / DM Controls section. This is not drift and must not be "fixed" to match desktop without a new operator decision. The `QuestManageDmControlsTests` phone-layout fact (`PhoneLayout_KeepsItsOwnArrangementWithoutADmControlsCard`) pins it.

## Commits

- `855292c8` test(261019-jba): add failing facts for the desktop Manage DM Controls card (RED: 5 of 6 failed, phone pin passed)
- `5a8a4043` feat(261019-jba): add DM Controls card to desktop Quest Manage (GREEN)
- `8f9c7cb1` test(261019-jba): pin the desktop Manage move control inside the DM Controls card

## Verification

- `dotnet build`: 0 errors.
- Task 1 filter (`QuestManageDmControlsTests` + `BoardMoveControlTests`): 50 passed, 0 failed.
- Task 2 guard/neighbour filter: 200 passed, 0 failed.
- Full unit suite: 1103 passed, 0 failed.
- Full integration suite: 1324 passed, 11 skipped, 0 failed.
- Acceptance greps: `<h5>DM Controls</h5>` 1, `btn btn-info w-100 mt-2` 2, `btn btn-primary w-100 mt-2"` 1, `asp-action="SendReminder"` 2. `git diff --name-only` against the base lists exactly the three planned files (so `Manage.Mobile.cshtml`, Details views, controllers, CSS, Domain and Repository are unchanged). No planning references or clock reads in added lines; the "Send again" form is not in the diff.
- Visual rendering was not checked in a browser; the facts check markup only, on EF Core InMemory.

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None. The Send Reminder form keeps its anti-forgery token and POST method inside the existing authorized branch; a fact asserts the token inside the card.

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs
- FOUND commits 855292c8, 5a8a4043, 8f9c7cb1
