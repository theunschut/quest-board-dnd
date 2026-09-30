---
status: testing
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
source: [88-VERIFICATION.md]
started: 2026-09-30T11:45:23Z
updated: 2026-09-30T11:45:23Z
---

## Current Test

number: 1
name: Google Calendar, already-subscribed phone — a game night Google held before the fix
expected: |
  After Google's next refresh the entry reads 18:00 (the stored board time). If Google keeps the
  old time, removing and re-adding the subscription once is the accepted resolution. Record the
  observation only; promise no refresh latency.
awaiting: user response

## Tests

### 1. Google Calendar, already-subscribed phone — a game night Google held before the fix (the entry that read 19:00 or 20:00)
expected: After Google's next refresh the entry reads 18:00 (the stored board time). If Google keeps the old time, remove and re-add the subscription once; that is the accepted resolution. Record the observation only; promise no refresh latency.
result: [pending]

### 2. Google Calendar — a new game night created at 18:00 on production, after Google fetches the feed
expected: The new entry reads 18:00 on the subscribed phone.
result: [pending]

### 3. Apple Calendar on an iPhone — the same event and quest
expected: Still reads 18:00 (no regression from the previous floating form).
result: [pending]

### 4. Optional but recommended — reschedule an entry that both apps already hold and confirm it moves
expected: The changed time shows in both apps. SEQUENCE is a constant 1 and DTSTAMP is the constant CreatedAt, so a client that applies updates only on a higher revision could ignore later reschedules; this is the one design risk the byte tests cannot rule out.
result: [pending]

Record the app, the phone's OS and the date of each entry checked.

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
