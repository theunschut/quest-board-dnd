---
status: testing
phase: 75-event-availability-signups
source: [75-VERIFICATION.md, 75-VALIDATION.md]
started: 2026-08-28T10:14:49Z
updated: 2026-08-28T10:14:49Z
---

## Current Test

number: 1
name: Roster and the three availability buttons render correctly on a real mobile device
expected: |
  On an event's details page opened with a real mobile User-Agent, the three
  availability buttons (Yes / Maybe / No) and the named roster are visible and
  usable, and the layout does not break.

  Why this cannot be automated: Events/Details.cshtml has no .Mobile variant, so
  one view serves both platforms. Devtools emulation has previously masked a live
  case in this codebase where mobile markup was never selected at all.
awaiting: user response

## Tests

### 1. Roster and three availability buttons render correctly on a real mobile device
expected: Buttons and roster usable on a real mobile User-Agent; layout does not break. (EVTAVAIL-01/03)
result: [pending]

### 2. Both confirmation dialogs read correctly
expected: |
  Delete an event that has signups — the dialog states how much availability the
  delete destroys. Remove a member from the Platform group page — the dialog states
  that their availability answers for events on this board will be deleted.
  Both dialogs must actually appear.

  Extra attention here: the member-removal dialog was broken in this phase and fixed
  during it. The handler had an HTML-entity apostrophe that terminated the JS string
  early, so the handler never compiled and the form submitted with NO confirmation at
  all. The code fix is verified and committed (62cfa06), but only a real browser can
  confirm the dialog now actually pops up. (D-24, D-25)
result: [pending]

### 3. Answering a past-dated event is acceptable in practice
expected: |
  As a player, open an event dated well in the past and change your answer; the code
  permits this. As a Dungeon Master, judge whether that reads as "correcting the record
  of a session that happened" or as a bug that should have been blocked.

  This is a product-intent judgment, not a correctness question — the automated tests
  prove the code permits it; they cannot say whether it should. (PD-01)
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps
