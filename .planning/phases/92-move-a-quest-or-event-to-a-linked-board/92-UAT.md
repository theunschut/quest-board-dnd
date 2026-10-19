---
status: testing
phase: 92-move-a-quest-or-event-to-a-linked-board
source: [92-VERIFICATION.md]
started: 2026-10-19T00:00:00Z
updated: 2026-10-19T00:00:00Z
---

## Current Test

number: 1
name: Phone-width layout of the Move page
expected: |
  Open the Move page at 390px width (devtools or a phone) for a quest and an event, with a long
  target board name and a long quest title. Heading, target select and the confirm button wrap
  without horizontal scroll or clipped text; the .Mobile twin renders the cards.
awaiting: user response

## Tests

### 1. Phone-width layout of the Move page
expected: At 390px, for a quest and an event with a long target board name and a long title, the heading, target select and confirm button wrap with no horizontal scroll or clipped text; the .Mobile twin renders the cards.
result: [pending]

### 2. Toast and link after a move
expected: Moving a quest as a super admin who is a member of the target board redirects to the source Quest Board with the Moved toast and a working "Open it on {Board}" link; as a non-member super admin the toast shows plain text with no link.
result: [pending]

### 3. Quest Moved and Waitlist Promoted email rendering
expected: In Email Preview, the Quest Moved email (player, Dungeon Master and removed audiences) and the Waitlist Promoted moved line render with a very long quest title without overflow or broken layout at a real mail-client width.
result: [pending]

### 4. Double press on Confirm (review WR-01)
expected: Pressing Confirm twice quickly on a slow save does not end on a bare 404; the move itself is done after the first press. (Known gap: no submit-disable guard — decide whether to fix.)
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
