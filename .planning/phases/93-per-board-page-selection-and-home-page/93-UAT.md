---
status: complete
phase: 93-per-board-page-selection-and-home-page
source: [93-VERIFICATION.md, 93-UI-REVIEW.md]
started: 2026-10-21T16:30:00Z
updated: 2026-10-22T07:00:00Z
---

## Current Test

[testing complete — the operator confirmed all four on a real phone after v5.7.0 was released (2026-10-22); the two mobile CSS findings were fixed afterwards]

## Tests

### 1. Platform Edit Pages section on a real phone
Open a board's Platform Edit page on a real phone (real mobile user agent, not devtools emulation). Flip several Pages switches, change the Home page, Save, and trigger a refusal (for example switch off the board's current home page).
expected: Switch rows are comfortably tappable, locked rows show their reason legibly, the refusal message is readable on the form, nothing overflows at roughly 390px wide.
result: pass
note: passed on the operator's real phone after the v5.7.0 release (2026-10-22); originally deferred at phase close as an accepted risk

### 2. Move picker with a long board name at phone width
On a phone-width real device, open the Move page for a quest or event on a board that is linked to a board with a very long name whose Quests or Events page is off.
expected: The picker option for the board with the page off is disabled, its suffix is readable, and the long name does not break the layout at 390px.
result: pass
note: passed on the operator's real phone after the v5.7.0 release (2026-10-22); originally deferred at phase close as an accepted risk

### 3. Trimmed board on phone and desktop
Switch off several pages on a board (for example Shop, Contacts, Events) with Calendar as home, then browse it on a real phone and on desktop.
expected: Both menus show no dead entries, the Quests menu item appears, and pages that lost links (Calendar, My Agenda, Players, Quest Details) look intentional, with no empty gaps.
result: pass
note: passed on the operator's real phone after the v5.7.0 release (2026-10-22); originally deferred at phase close as an accepted risk

### 4. Refusal lines legible on the mobile Platform Edit page
On a real phone, trigger a refusal on the Platform Edit page (for example switch off the board's current home page) and read the refusal lines.
expected: The refusal lines read as errors on the glass card. No mobile `.text-danger` rule exists, so they use Bootstrap's default red; add a scoped rule in platform-group.mobile.css if they are not legible.
source: 93-UI-REVIEW.md warning 1
result: pass
note: passed on the operator's real phone after the v5.7.0 release (2026-10-22); originally deferred at phase close as an accepted risk

## Summary

total: 4
passed: 4
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
