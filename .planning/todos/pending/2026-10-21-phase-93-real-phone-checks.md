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
