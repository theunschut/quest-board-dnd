---
status: testing
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
source: [88-VERIFICATION.md]
started: 2026-09-30T11:45:23Z
updated: 2026-09-30T13:49:00Z
---

Tests 1–4 need the production deployment: Google and Apple fetch the feed from their own servers,
so they cannot reach a localhost address. They run once this work is merged to master and released.
Test 5 is the local pre-deploy check of the same endpoint and has passed.

## Current Test

number: 3
name: Apple Calendar on an iPhone — the same event and quest
expected: |
  Still reads 18:00. To confirm the iPhone has read the zoned document, not a cached floating copy:
  with Settings → Apps → Calendar → Time Zone Override set to London, an 18:00 game night reads 17:00.
awaiting: user response

## Tests

### 1. Google Calendar, already-subscribed phone — a game night Google held before the fix (the entry that read 19:00 or 20:00)
expected: After Google's next refresh the entry reads 18:00 (the stored board time). If Google keeps the old time, remove and re-add the subscription once; that is the accepted resolution. Record the observation only; promise no refresh latency.
result: pass
observed: |
  2026-09-30, after the v5.3.2 deploy. The existing Google subscription did not pick up the corrected times
  on its own in the time the user waited. Google caches a subscribed calendar per address and decides for
  itself when to re-fetch, and this check did not wait long enough to see whether the old subscription
  would eventually correct itself. Google showed the correct times after the user created a NEW
  subscription in the Quest Board (a new feed address) and added that address as a source in Google.
  That falls under the accepted resolution of re-subscribing once. Because Google caches by address, the
  practical form of it is a new subscription address rather than re-adding the same URL.

### 2. Google Calendar — a new game night created at 18:00 on production, after Google fetches the feed
expected: The new entry reads 18:00 on the subscribed phone.
result: pass
observed: |
  2026-09-30: with the new subscription address added to Google, the calendar shows the board's times.
  Reported as "google calendar works now".

### 3. Apple Calendar on an iPhone — the same event and quest
expected: Still reads 18:00 (no regression from the previous floating form).
result: [pending]
note: |
  2026-09-30, after the v5.3.2 deploy: Apple Calendar still shows the correct times, and the Profile page's
  "Last fetched" is later than the deploy. That does not yet prove the iPhone read the new document. Apple
  showed the old floating form correctly too, because the phone is in Amsterdam, and the post-deploy fetch
  may have been Google's. To confirm, turn on Settings → Apps → Calendar → Time Zone Override and set it to
  London: an 18:00 game night should then read 17:00. If it still reads 18:00, the iPhone is still showing
  the floating copy.

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

### 6. Production endpoint check — the deployed feed serves the zoned document
expected: A real production subscription address, fetched anonymously, returns the same zoned structure as the local check. Every timed entry carries TZID=Europe/Amsterdam with the board's wall-clock digits, and its VTIMEZONE agrees with the IANA rules.
result: pass
observed: |
  2026-09-30, after the v5.3.2 deploy, using a temporary subscription the operator created for this check and then revoked.
  - GET https://questboard.theunschut.com/feeds/calendar/{temporary token}.ics anonymously: 200,
    Content-Type text/calendar; charset=utf-8, 405 lines, all CRLF.
  - X-WR-TIMEZONE:Europe/Amsterdam; one VTIMEZONE with the 2026-10-25 and 2027-03-28 transitions.
  - 42 VEVENTs: 41 timed (all TZID=Europe/Amsterdam, no trailing Z), 1 all-day (VALUE=DATE); SEQUENCE:1 on
    every entry, SEQUENCE:0 nowhere. Start times 18:00 ×14, 19:00 ×25, 13:00 ×2.
  - All 82 timed DTSTART/DTEND lines resolve to the same UTC instant through the feed's VTIMEZONE as through
    ICU's Europe/Amsterdam rules, on both sides of the 25 October clock change.
  - Spot-check against the board: Session Chris on 2026-11-14 reads 13:00 on the board (operator-confirmed)
    and DTSTART;TZID=Europe/Amsterdam:20261114T130000 in the feed.
  The server side is proven on production. Tests 1–4 are the client-side confirmation.

## Summary

total: 6
passed: 4
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
