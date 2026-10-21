---
created: 2026-10-21T17:00:00.000Z
title: Run the Phase 93 real-phone checks deferred at phase close
area: ui
source: .planning/phases/93-per-board-page-selection-and-home-page/93-UAT.md
files:
  - QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml
  - QuestBoard.Service/wwwroot/css/platform-group.mobile.css
  - QuestBoard.Service/Views/Move/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
---

## Problem

Phase 93 closed with its automated verification passing (7/7 must-haves) but four checks that only a
real phone can answer were deferred by the operator as an accepted risk. Mobile views are chosen by
user agent, so devtools emulation does not exercise them.

## Solution

On a real phone (and desktop where noted), run the four items in `93-UAT.md`:

1. Platform Edit Pages section: switch rows tappable (44px), locked reasons legible, no overflow at ~390px.
2. Move picker: a long linked-board name plus the "page off" suffix fits at 390px; the option is disabled.
3. A trimmed board (Shop, Contacts, Events off, Calendar home) on phone and desktop: no dead menu
   entries, the Quests item appears, pages that lost links look intentional.
4. Refusal lines on the mobile Platform Edit page are legible: no mobile `.text-danger` rule exists, so
   they fall back to Bootstrap's default red on the glass card (UI review warning 1). Add a scoped rule
   in `platform-group.mobile.css` if they are not.

Record results with `/gsd-verify-work 93`, which re-opens the deferred items.

## Findings from the local SQL Server run (2026-10-21, in-app browser, not a real phone)

Already confirmed there, on the local `QuestBoard` database: the `AddBoardPageSettings` migration applies
(two `int NOT NULL DEFAULT 0` columns, every board on the defaults); a board with Shop and Contacts off
and Calendar as home opens on `/Calendar`, drops both from both menus, shows the Quests item, and
refuses `/Shop` and `/Contacts` with the Info toast; the home-page refusal and the stale-form refusal
(two tabs) both render; the mobile Edit page has no horizontal scroll at 375px and every switch row is
44px. Settings were restored afterwards.

Seen in mobile-UA emulation, to settle on the real phone:

- Item 4 leans towards "not legible enough": the mobile refusal line renders in Bootstrap's `#dc3545`,
  noticeably dimmer on the brown glass card than desktop's `#ff6b6b`. Likely fix:
  `.platform-group-card-mobile .text-danger { color: #ff6b6b; }` in `platform-group.mobile.css`.
- The mobile switches render as Bootstrap's default blue toggle, not the app's amber accent; consider
  scoping `.form-check-input:checked` to the amber used on desktop.
- Desktop at ~800px wide: the top menu wraps "Quest Log" onto two lines once the Quests item is shown.
