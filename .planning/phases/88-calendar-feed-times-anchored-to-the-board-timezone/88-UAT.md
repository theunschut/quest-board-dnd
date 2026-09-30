---
status: testing
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
source: [88-VERIFICATION.md]
started: 2026-09-30T11:45:23Z
updated: 2026-09-30T12:52:00Z
---

Tests 1–4 need the production deployment: Google and Apple fetch the feed from their own servers,
so they cannot reach a localhost address. They run once this work is merged to master and released.
Test 5 is the local pre-deploy check of the same endpoint and has passed.

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

### 5. Local pre-deploy check — the live subscription endpoint on localhost carries the board's times, anchored to the board zone
expected: An anonymous GET of a real subscription address returns 200 text/calendar; every timed entry declares TZID=Europe/Amsterdam with the stored wall-clock digits unchanged; the generated VTIMEZONE resolves every entry to the same instant as the IANA zone rules; the board pages show the same times.
result: pass
observed: |
  2026-09-30, local dev server (http://localhost:8000) against the local SQL Server data, one active subscription.
  - GET /feeds/calendar/{token}.ics anonymously: 200, Content-Type text/calendar; charset=utf-8, 378 lines, all CRLF.
  - X-WR-TIMEZONE:Europe/Amsterdam; one VTIMEZONE (TZID Europe/Amsterdam) with the 2026-10-25 03:00 +0200→+0100 and
    2027-03-28 02:00 +0100→+0200 transitions.
  - 39 VEVENTs: 38 timed, all DTSTART/DTEND;TZID=Europe/Amsterdam with no trailing Z; 1 all-day (VALUE=DATE, no zone);
    SEQUENCE:1 on every entry, SEQUENCE:0 nowhere.
  - Stored value vs feed vs board page:
    quest 12039 FinalizedDate 2026-10-02 18:00 → DTSTART;TZID=Europe/Amsterdam:20261002T180000 → quest page "Friday, October 02, 2026 at 6:00 pm";
    event 1 2026-08-28 16:00 → 20260828T160000 → event page "Friday, August 28, 2026 16:00";
    event 82 2026-10-03 19:00 → 20261003T190000; event 64 2026-10-31 19:00 → 20261031T190000 (after the clock change).
  - Independent cross-check: all 76 timed DTSTART/DTEND lines resolve to the same UTC instant through the feed's own
    VTIMEZONE as through ICU's Europe/Amsterdam rules (e.g. 20261002T180000 → 16:00Z, 20261031T190000 → 18:00Z).
  - The Profile page's subscription row updated "Last fetched" to the check's fetch time.

## Summary

total: 5
passed: 1
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
