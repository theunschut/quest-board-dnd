---
status: partial
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
source: [88-VERIFICATION.md]
started: 2026-09-30T11:45:23Z
updated: 2026-09-30T19:19:16Z
---

Tests 1–4 need the production deployment: Google and Apple fetch the feed from their own servers,
so they cannot reach a localhost address. They run once this work is merged to master and released.
Test 5 is the local pre-deploy check of the same endpoint and has passed.

## Current Test

[testing paused — 1 item outstanding: test 4, re-check a rescheduled entry in Google Calendar]

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
result: pass
observed: |
  2026-09-30, after the v5.3.2 deploy: with Time Zone Override set to London, an 18:00 game night reads
  17:00, so the iPhone is reading the zoned entries rather than a floating copy. The override had
  previously been set to Amsterdam, which is why Apple showed even the old floating times correctly on
  this phone. It may not have done so for someone without that override.
note: |
  2026-09-30, after the v5.3.2 deploy: Apple Calendar still shows the correct times, and the Profile page's
  "Last fetched" is later than the deploy. That does not yet prove the iPhone read the new document. Apple
  showed the old floating form correctly too, because the phone is in Amsterdam, and the post-deploy fetch
  may have been Google's. To confirm, turn on Settings → Apps → Calendar → Time Zone Override and set it to
  London: an 18:00 game night should then read 17:00. If it still reads 18:00, the iPhone is still showing
  the floating copy.

### 4. Optional but recommended — reschedule an entry that both apps already hold and confirm it moves
expected: The changed time shows in both apps. SEQUENCE is a constant 1 and DTSTAMP is the constant CreatedAt, so a client that applies updates only on a higher revision could ignore later reschedules; this is the one design risk the byte tests cannot rule out.
result: blocked
blocked_by: third-party
reason: "Re-test after gap G-88-4 was fixed in v5.3.3. Apple Calendar passes: test 7, an entry the iPhone already held updated after a real change. Google Calendar has not been re-checked for a reschedule since the fix; the operator can't check Google now. This closes once a rescheduled entry that Google already holds is seen moving in Google Calendar. Google refreshes subscribed calendars on its own schedule, and a new subscription address is the accepted fallback."
first_run: "issue on v5.3.2: 'doesn't seem to work. I checked it's fetched after the change, but it's not updated in my calendar' (major). Diagnosed as gap G-88-4 and fixed by plans 88-05..88-09."

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
  2026-09-30, after the v5.3.2 deploy, using a temporary subscription the operator created for this check; revoked afterwards (GET returned 410 at 14:02Z).
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

### 7. Re-test of gap G-88-4 on production after the v5.3.3 deploy — a rescheduled or retitled entry moves in both apps
expected: After v5.3.3 is deployed, reschedule (or retitle) an entry that Google Calendar and Apple Calendar already hold. After each app's next fetch, the entry shows the new time or title in place under the same event, with no duplicate. Entries held from before the deploy should also repair on their first fetch, because the migration moved every existing entry to SEQUENCE:2. To attribute the fetch, confirm "Last fetched" advances on an Apple-only subscription row before checking the iPhone. Promise no refresh latency. For Google, a new subscription address is the accepted fallback.
result: pass
observed: |
  2026-09-30, on v5.3.3 in production. The operator changed "GameNight: Blood on the Clocktower"
  (questboard-event-64), an entry the iPhone already held, and it updated on the iPhone (Apple Calendar,
  direct "Subscribed" calendar). Before that, a first report ("it's merged and deployed. However, my iphone
  still shows the old time after a fetch") turned out to be a mix-up, not a defect: the edit had been
  made to a different event ("Gamenight", questboard-event-62, moved to 17:15) than the one being
  watched on the phone.
  Server-side evidence from the same session:
  - A temporary production subscription (revoked afterwards) and the iPhone's own address, downloaded
    in a browser, both served v5.3.3. All 42 entries carried LAST-MODIFIED. 41 untouched entries were
    at SEQUENCE:2, stamped at the deploy migration (2026-09-30T18:33:25Z). The edited event was at
    SEQUENCE:3, with DTSTAMP = LAST-MODIFIED = its save time (18:40:25Z) and the new DTSTART.
  - The response came straight from Kestrel with a fresh ETag; no caching proxy is in the path.
  Google was not re-checked separately for this test. Its earlier behaviour is in tests 1–2.

An optional test 8 (a two-tab concurrent edit against the local SQL Server) was dropped from this UAT
by operator decision on 2026-09-30. Two browser tabs don't reach the race, because each save reloads
the row. The concurrency fix rests on six FeedRevisionStamperTests (InMemory) and the security audit's
hand trace. The SQL Server predicate and rollback path is recorded as a residual note in 88-SECURITY.md.

## Summary

total: 7
passed: 6
issues: 0
pending: 0
skipped: 0
blocked: 1

## Gaps

- gap_id: G-88-4
  truth: "Rescheduling an entry that Google and Apple Calendar already hold moves it to the new time in both apps after their next fetch"
  status: failed
  reason: "User reported: doesn't seem to work. I checked it's fetched after the change, but it's not updated in my calendar"
  severity: major
  test: 4
  root_cause: "After first publication the feed carries no revision signal. Every entry is written with a stable UID, DTSTAMP = its CreatedAt, the literal SEQUENCE:1 and no LAST-MODIFIED, so after a reschedule only DTSTART/DTEND differ. A client deciding whether a re-fetched copy is newer compares SEQUENCE, then DTSTAMP (RFC 5546 §2.1.5), finds them equal and keeps its stale copy. RFC 5545 §3.8.7.2 defines DTSTAMP without METHOD as the time the entry was last revised, so CreatedAt is wrong once an entry is edited. Enabling cause: Events and Quests have no UpdatedAt or revision column. Server side ruled out: the ETag is a SHA-256 of the body, and a reschedule yields a new ETag and a 200 with the new DTSTART. This is Phase 84 Assumption A5 (constant SEQUENCE harmless) proving wrong. Phase 88 D-07 bumped SEQUENCE once and left later edits unsignalled."
  artifacts:
    - path: "QuestBoard.Domain/Services/CalendarFeedWriter.cs"
      issue: "SEQUENCE is the literal 1 (lines 219, 237); DTSTAMP is taken from CreatedAt (208, 232); no LAST-MODIFIED"
    - path: "QuestBoard.Domain/Models/CalendarFeedEntry.cs"
      issue: "carries only CreatedAt, so the writer has no revision input"
    - path: "QuestBoard.Domain/Services/CalendarSubscriptionService.cs"
      issue: "builds feed entries with no revision data (lines 101-153)"
    - path: "QuestBoard.Repository/Entities/EventEntity.cs"
      issue: "no UpdatedAt or revision column"
    - path: "QuestBoard.Repository/Entities/QuestEntity.cs"
      issue: "no UpdatedAt or revision column"
    - path: "QuestBoard.Service/Controllers/Events/EventsController.cs, QuestBoard.Repository/EventRepository.cs (ApplyTemplateToOccurrencesAsync), QuestBoard.Repository/QuestRepository.cs (FinalizeQuestAsync)"
      issue: "reschedule and retitle paths update rows in place without raising any revision"
    - path: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs, QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs"
      issue: "pin SEQUENCE:1 on both fetches of a rescheduled quest and DTSTAMP == CreatedAt, so they enforce the defect"
  missing:
    - "A per-entry revision on Events and Quests (EF Core migration) raised on every write that changes what the feed shows: date, start time, title, finalized date and state"
    - "SEQUENCE derived from that revision so it rises on each change and never drops below 1 (one-way rule, D-07)"
    - "DTSTAMP (and optionally LAST-MODIFIED) from the entry's last revision time instead of CreatedAt"
    - "Tests rewritten so a rescheduled entry proves a higher SEQUENCE and a later DTSTAMP, while an unedited entry stays byte-identical between fetches (ETag/304 and determinism pins intact)"
  debug_session: ".planning/debug/calendar-reschedule-not-propagating.md"
  resolved_by: [88-05, 88-06, 88-07, 88-08, 88-09]
  resolved_at: 2026-09-30
  resolution: "Shipped in v5.3.3 and re-tested on production as test 7 (pass)."

- gap_id: G-88-7
  truth: "After the v5.3.3 deploy, a rescheduled entry that Apple Calendar already holds moves to its new time after the iPhone's next fetch"
  status: resolved
  reason: "User reported: it's merged and deployed. However, my iphone still shows the old time after a fetch"
  severity: major
  test: 7
  root_cause: "Not a defect. The edit had been made to a different event (Gamenight, questboard-event-62) than the one being watched on the iPhone (GameNight: Blood on the Clocktower, questboard-event-64). Production and the iPhone's own address both served the edited event at SEQUENCE:3 with its new DTSTART. Once the watched event was changed, it updated on the iPhone."
  artifacts: []
  missing: []
  resolved_at: 2026-09-30
  resolution: "Closed without a code change; test 7 passes."
