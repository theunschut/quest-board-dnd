---
status: complete
phase: 82-personal-cross-board-event-agenda
source:
  - 82-01-SUMMARY.md
  - 82-02-SUMMARY.md
  - 82-03-SUMMARY.md
  - 82-04-SUMMARY.md
  - 82-05-SUMMARY.md
  - 82-06-SUMMARY.md
started: 2026-08-30T00:00:00Z
updated: 2026-08-30T00:00:00Z
---

## Current Test

number: 1
name: Cold Start Smoke Test
expected: Stop any running QuestBoard instance, then start the app fresh (`dotnet run --project QuestBoard.Service`, or the questboard-service launch config on http://localhost:8000). The service boots with no startup exception, the auto-applied migration step completes with nothing new to apply, and signing in then opening My Agenda returns your real upcoming events across both of your boards — each row naming which board it belongs to.
awaiting: none - session complete

## Tests

### 1. Cold Start Smoke Test
expected: Service boots clean; no new migration applies; My Agenda returns live data showing upcoming events from both of your boards, each row naming its board.
result: [pending]

### 2. Mobile tap targets — roster disclosure vs row action
expected: On a real phone, the roster disclosure control and the row's action control are two separate targets that a thumb can hit unambiguously, and tapping inside an expanded roster never navigates away.
result: [pending]
coverage_id: 04-D2
requirement: EVTAGENDA-04

### 3. Mobile availability chips are visually styled
expected: On a real phone, the availability chips render with their full styling — the unconfirmed-Yes chip is visibly distinct from a confirmed Yes (clock icon, italic label, dashed border), and the empty cell reads as an em dash rather than an unstyled badge.
result: [pending]
coverage_id: 04-D3
requirement: EVTAGENDA-02

### 4. Back-link from event details to the agenda
expected: Opening an event from the agenda shows a visible way back to the agenda on the event's details page; the availability answer buttons on that page still work exactly as before.
result: [pending]
coverage_id: 05-T4
requirement: EVTAGENDA-06

### 5. Ten EVTAGENDA requirement IDs defined in REQUIREMENTS.md with traceability reconciled to 60/60
expected: Ten EVTAGENDA requirement IDs defined in a new REQUIREMENTS.md section and added to its Traceability table, with coverage counters reconciled to 60/60
result: pass
source: automated
coverage_id: 01-D1

### 6. Ten EVTAGENDA rows mapped to Phase 82 in the ROADMAP Requirements Coverage table
expected: Ten EVTAGENDA rows mapped to Phase 82 in ROADMAP.md's Requirements Coverage table, with the placeholder replaced
result: pass
source: automated
coverage_id: 01-D2

### 7. Validation map rewritten with real task ids and a Status column
expected: 82-VALIDATION.md Per-Task Verification Map rewritten with real 82-NN task ids and a Status column
result: pass
source: automated
coverage_id: 01-D3

### 8. Cross-board repository query returns the next N events in one round trip
expected: Repository query returns the next N upcoming, non-cancelled events across an explicitly supplied set of board ids, with every signup and signer name from one round trip, ordered deterministically
result: pass
source: automated
coverage_id: 02-D1

### 9. Service composes each row with the viewer's own cell and the full roster
expected: Service composes each row with the viewer's own availability cell and the event's complete roster
result: pass
source: automated
coverage_id: 02-D2

### 10. Empty membership set yields an empty agenda, not every board's events
expected: A caller with an empty membership set gets an empty agenda, not every board's events, and the re-check holds
result: pass
source: automated
coverage_id: 02-D3

### 11. AgendaOptions provides configurable page sizes with start-up validation
expected: AgendaOptions provides configurable page sizes with code defaults, config binding and start-up validation
result: pass
source: automated
coverage_id: 02-D4

### 12. Signed-in member sees every upcoming event across all their boards
expected: A signed-in member can open /Agenda and see every upcoming event across all of their boards, one row per event, with the board named and the whole roster on the row
result: pass
source: automated
coverage_id: 03-T1
requirement: EVTAGENDA-01

### 13. Page loads with no active board selected
expected: The page loads when the viewer has no active board selected, instead of diverting to the board picker
result: pass
source: automated
coverage_id: 03-T2
requirement: EVTAGENDA-10

### 14. A foreign board id can never enter the query
expected: A board id the viewer does not belong to can never enter the query, whether on the query string or in session
result: pass
source: automated
coverage_id: 03-T3

### 15. A non-active-board row prompts before switching
expected: A row on a non-active board prompts before switching via the existing board-selection action; an active-board row goes straight through
result: pass
source: automated
coverage_id: 03-T4

### 16. Three empty states are distinguishable and the recoverable one resets
expected: The three empty states are told apart, and the recoverable one carries its own reset control
result: pass
source: automated
coverage_id: 03-T5

### 17. Board filter narrows correctly for multiple checked boxes
expected: The board filter narrows correctly for multiple checked boxes submitted alongside the form's leading hidden field
result: pass
source: automated
coverage_id: 03-T6

### 18. Phone renders the agenda as cards with roster behind a disclosure
expected: A phone renders the agenda as cards, with each event's roster behind its own disclosure control rather than always expanded
result: pass
source: automated
coverage_id: 04-D1

### 19. Board filter reachable on mobile without a dropdown
expected: The board filter is reachable on mobile without introducing a dropdown into a layout that has none
result: pass
source: automated
coverage_id: 04-D4

### 20. Agenda reachable from the user menu on both layouts
expected: Every authenticated user can reach the agenda from the user menu on both layouts
result: pass
source: automated
coverage_id: 05-T1
requirement: EVTAGENDA-05

### 21. Agenda entry never leaks into anonymous navigation
expected: The agenda entry never leaks into the public/anonymous navigation
result: pass
source: automated
coverage_id: 05-T2

### 22. Overview and calendar both cross-link to the agenda
expected: The availability overview and the calendar both link across to the agenda
result: pass
source: automated
coverage_id: 05-T3

### 23. A non-member board contributes nothing
expected: A non-member board contributes nothing to the agenda — no event title, member name, or board name
result: pass
source: automated
coverage_id: 06-D1
requirement: EVTAGENDA-09

### 24. Two of three boards both appear interleaved, third never does
expected: A viewer in two of three boards sees both joined boards interleaved by date and never the third
result: pass
source: automated
coverage_id: 06-D2

### 25. Leaving a board removes it on the very next request
expected: Leaving a board removes it from the agenda on the very next request, with the pre-leave state asserted first
result: pass
source: automated
coverage_id: 06-D3
requirement: EVTAGENDA-07

### 26. The filter can never widen the set
expected: The board filter can never widen the set, whether the foreign id arrives on the query string or in session
result: pass
source: automated
coverage_id: 06-D4

### 27. SuperAdmin scoped by their own memberships
expected: A SuperAdmin is scoped by their own membership rows exactly like anyone else — no all-groups branch
result: pass
source: automated
coverage_id: 06-D5
requirement: EVTAGENDA-08

### 28. Filter narrows before the window is taken, and is resettable
expected: The filter narrows before the window is taken, is remembered across requests, and is resettable
result: pass
source: automated
coverage_id: 06-D6

### 29. Phase-gate static audit
expected: Exactly one filter bypass in the repository layer, zero anywhere in Domain or Service
result: pass
source: automated
coverage_id: 06-D7

## Summary

total: 29
passed: 25
issues: 0
pending: 4
skipped: 0

## Gaps

[none yet]

## Notes

- 25 of 29 deliverables are deterministically covered by passing automated tests and are recorded
  as `source: automated` rather than presented as checkpoints.
- One malformed coverage entry was found and is being surfaced rather than dropped: `82-05-SUMMARY.md`
  entry `T4` declares `verification[1].kind: manual`, which is not a valid kind (allowed: unit,
  integration, e2e, automated_ui, manual_procedural, other). The intent was almost certainly
  `manual_procedural`. Under the fail-safe rule the deliverable is presented as human checkpoint 4
  rather than auto-passed. The underlying evidence (insertions-only diff on the details view) is sound.
- Test 1 (cold start) is injected because this phase changed a startup-path DI registration
  (`ServiceExtensions.cs`) and request-pipeline middleware (`GroupSessionMiddleware.cs`). A missing
  options registration or a mis-ordered middleware exemption fails only on a real boot, not in tests.
