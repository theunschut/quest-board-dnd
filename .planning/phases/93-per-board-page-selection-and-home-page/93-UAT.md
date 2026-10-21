---
status: testing
phase: 93-per-board-page-selection-and-home-page
source: [93-VERIFICATION.md]
started: 2026-10-21T16:30:00Z
updated: 2026-10-21T16:30:00Z
---

## Current Test

number: 1
name: Platform Edit Pages section on a real phone
expected: |
  Switch rows are comfortably tappable, locked rows show their reason legibly, the refusal message is readable on the form, nothing overflows at roughly 390px wide.
awaiting: user response

## Tests

### 1. Platform Edit Pages section on a real phone
Open a board's Platform Edit page on a real phone (real mobile user agent, not devtools emulation). Flip several Pages switches, change the Home page, Save, and trigger a refusal (for example switch off the board's current home page).
expected: Switch rows are comfortably tappable, locked rows show their reason legibly, the refusal message is readable on the form, nothing overflows at roughly 390px wide.
result: [pending]

### 2. Move picker with a long board name at phone width
On a phone-width real device, open the Move page for a quest or event on a board that is linked to a board with a very long name whose Quests or Events page is off.
expected: The picker option for the board with the page off is disabled, its suffix is readable, and the long name does not break the layout at 390px.
result: [pending]

### 3. Trimmed board on phone and desktop
Switch off several pages on a board (for example Shop, Contacts, Events) with Calendar as home, then browse it on a real phone and on desktop.
expected: Both menus show no dead entries, the Quests menu item appears, and pages that lost links (Calendar, My Agenda, Players, Quest Details) look intentional, with no empty gaps.
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps
