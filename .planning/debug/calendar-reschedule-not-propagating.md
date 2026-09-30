---
status: diagnosed
trigger: "UAT gap G-88-4 (phase 88, test 4): Rescheduling an entry that Google Calendar and Apple Calendar already hold should move it to the new time in both apps after their next fetch. Operator report: \"doesn't seem to work. I checked it's fetched after the change, but it's not updated in my calendar\""
created: 2026-09-30T15:00:00Z
updated: 2026-09-30T15:45:00Z
---

## Current Focus

hypothesis: CONFIRMED (server side) -- the feed carries no revision signal for an edit made after an entry is first published. UID, DTSTAMP (=CreatedAt) and SEQUENCE (=literal 1) are byte-identical before and after a reschedule, so a client that decides "is this a newer revision of a UID I hold?" by SEQUENCE and/or DTSTAMP sees the same revision and keeps its copy. No column exists from which a correct revision could be derived.
test: complete -- code read, spec check (RFC 5545 3.8.7.2 / 3.8.7.4), live-feed integration facts run, schema read, design-history read, UAT differential (test 3 vs test 4)
expecting: n/a (diagnose-only)
next_action: none -- diagnose-only session; hand root cause to plan-phase --gaps. Suggested verification after the fix: reschedule an entry, confirm "Last fetched" advances on the Apple-only subscription row specifically, then check the iPhone.
bug_class: bohrbug  (deterministic: every reschedule, on every path, emits the identical no-revision VEVENT; SBFL skipped -- no failing test exists, the suite pins the behaviour as passing)
reasoning_checkpoint:
  hypothesis: "Rescheduled entries do not move on subscribed clients because CalendarFeedWriter emits SEQUENCE:1 (constant) and DTSTAMP=CreatedAt (constant) with no LAST-MODIFIED, so after an edit the VEVENT a client already holds and the one it re-fetches have the same UID, same SEQUENCE and same DTSTAMP; revision-comparing clients treat the fetched copy as not newer and keep the old DTSTART."
  confirming_evidence:
    - "CalendarFeedWriter.cs:207-219 and 231-237 write UID from Source+SourceId, DTSTAMP from entry.CreatedAt, literal SEQUENCE:1; no other revision property exists"
    - "Integration fact Feed_RescheduledQuest_UpdatesInPlace... (run, passes) proves the second fetch carries the new DTSTART under a byte-identical UID line and SEQUENCE:1 again"
    - "RFC 5545 3.8.7.2: for an object with no METHOD (this feed), DTSTAMP is 'the date and time that the information associated with the calendar component was last revised in the calendar store' and 'is equivalent to the LAST-MODIFIED property'; stamping CreatedAt after an edit asserts the component was never revised"
    - "Events/Quests tables (live schema) have no UpdatedAt/rowversion; every reschedule path updates the row in place"
    - "Phase 84 A5 recorded 'constant SEQUENCE is harmless because clients fully replace by UID' as LOW confidence and the highest-impact wrong assumption, to be re-tested on the first stale-event report; D-07 explicitly left post-phase-88 edits in that position"
    - "UAT differential: test 3 -- the operator's iPhone DID apply a change to entries it already held when SEQUENCE rose 0->1 (London override shows 17:00 for 18:00). Test 4 -- a later DTSTART change with SEQUENCE unchanged at 1 was not applied after a recorded fetch"
  falsification_test: "If the displaying client is shown to fetch the post-reschedule document (Last fetched advancing on a subscription row only that client uses) and still keeps the old time after an entry is served with a HIGHER SEQUENCE and later DTSTAMP, the hypothesis is wrong. Equally, if the same client moves an entry after a same-SEQUENCE reschedule once it is proven to have fetched, the client is full-replace and the observation was fetch attribution/latency."
  fix_rationale: "Emitting a monotonic per-entry revision (SEQUENCE >= 1 that rises on every significant edit, DTSTAMP/LAST-MODIFIED = last-revised time) gives revision-comparing clients the signal RFC 5545/5546 define, and is harmless to full-replace clients. Deriving it from stored data keeps output byte-identical between fetches, so the ETag/304 behaviour and determinism pins survive."
  blind_spots: "Which client made the post-change fetch the operator saw is unrecorded (no user agent is stored; the Apple subscription address may also have been held by the old Google subscription). Whether the operator checked Apple or Google in test 4 is unrecorded. Google also batches polls (reported 12-24h) and may lag even with a correct revision. Neither vendor documents its subscribed-calendar merge rule; external evidence is anecdotal."
  candidate_causes:
    - "code: writer emits constant SEQUENCE and creation-time DTSTAMP (no revision signal) -- CONFIRMED"
    - "data/schema: no UpdatedAt/revision column on Events or Quests, so no correct revision is derivable -- CONFIRMED (enabling cause)"
    - "config/server: ETag/304 or a response cache hides the change -- ELIMINATED"
    - "environment/client: fetch seen on Profile was by a different client than the one displaying, or client fetch-to-display lag -- NOT RULED OUT, caveat only (cannot by itself explain a same-client failure, and would be transient, not permanent)"
  and_gate: "yes -- the symptom needs (A) a feed with no revision signal AND (B) a client that gates updates on revision. A alone is harmless to a full-replace client; B alone is harmless when SEQUENCE/DTSTAMP advance. A is the defect in this codebase; B is external behaviour the feed must accommodate. The fetch-attribution caveat (C) may additionally confound this one observation."
tdd_checkpoint: null

## Symptoms

expected: Rescheduling an entry that Google and Apple Calendar already hold moves it to the new time in both apps after their next fetch (UAT test 4).
actual: Operator verbatim: "doesn't seem to work. I checked it's fetched after the change, but it's not updated in my calendar". Profile page "Last fetched" shows a fetch after the change, but the calendar still shows the old time.
errors: none reported
reproduction: UAT test 4 in .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-UAT.md. On production (v5.3.2), reschedule an entry the subscribed calendars already hold; wait for a fetch; calendar still shows old time.
started: discovered during UAT 2026-09-30 after v5.3.2 deploy. Tests 1-3 and 6 passed (new subscriptions render correct zoned times; production endpoint serves zoned document).

## Eliminated

- hypothesis: server returns 304 Not Modified (or a cached body) after a reschedule, so the client never receives the new time
  evidence: ETag is SHA-256 of the freshly composed body (CalendarSubscriptionService.cs:175); 304 only on exact If-None-Match match (CalendarFeedController.cs:72); no OutputCache/ResponseCaching/Cache-Control on the path; facts Feed_ChangesTheEntityTag_WhenAnEventIsEdited, Feed_ReturnsAFreshBody_WhenTheClientPresentsAStaleEntityTag and Feed_RescheduledQuest_UpdatesInPlace... all pass -- an edit yields a new ETag and a 200 with the moved DTSTART
  timestamp: 2026-09-30T15:14:00Z

- hypothesis: the reschedule changes the UID (row re-created), so clients add a duplicate or lose the entry rather than update it
  evidence: all reschedule paths update in place (EventsController edit -> UpdateAsync, EventRepository.ApplyTemplateToOccurrencesAsync, QuestRepository.FinalizeQuestAsync); the reschedule fact asserts a byte-identical UID line across fetches; operator reports the old time still shown, not a duplicate
  timestamp: 2026-09-30T15:18:00Z

- hypothesis: "Last fetched" is misleading because the throttle writes a stamp that is newer than the real fetch
  evidence: TouchLastFetchedAsync (CalendarSubscriptionRepository.cs:80-95) writes the actual request time and only when >=15 min since the previous write; the throttle can only make the stamp OLDER than the latest fetch, never newer. A post-change stamp proves a post-change fetch of that address (not which client made it)
  timestamp: 2026-09-30T15:22:00Z

- hypothesis: production's older build (without unpushed WR-01/WR-02) explains the symptom
  evidence: WR-01 only moves the feed window's "today" from UTC date to board date (edge effect of at most one day at the 3-months-back / 12-months-ahead bounds); WR-02 only changes how the constant CreatedAt is labelled as UTC. Neither touches SEQUENCE, UID or makes DTSTAMP vary. Both builds emit the same no-revision VEVENT
  timestamp: 2026-09-30T15:30:00Z

## Evidence

- timestamp: 2026-09-30T15:05:00Z
  checked: Phase 0 knowledge base (.planning/debug/knowledge-base.md); MemPalace
  found: no knowledge base file and no prior debug sessions exist; MemPalace not queried (no tool available in this session)
  implication: no known-pattern candidate; proceed with open investigation

- timestamp: 2026-09-30T15:06:00Z
  checked: QuestBoard.Domain/Services/CalendarFeedWriter.cs AppendTimedEvent (201-221) and AppendAllDayEvent (226-239)
  found: every VEVENT emits UID=questboard-{source}-{id} (stable), DTSTAMP=FormatUtcStamp(entry.CreatedAt) (constant for the row's life), SEQUENCE:1 (hard-coded literal), no LAST-MODIFIED, no METHOD. After a reschedule the only bytes that differ in that VEVENT are DTSTART/DTEND (and the VTIMEZONE window if the span moves).
  implication: from a client's point of view the rescheduled VEVENT is the "same revision" (same UID, same SEQUENCE, same DTSTAMP) with different content

- timestamp: 2026-09-30T15:07:00Z
  checked: git log -S"SEQUENCE" on writer; commit 5faeab3e (88-02) and 88-CONTEXT.md D-07
  found: phase 84 shipped SEQUENCE:0; phase 88 bumped it once to the literal 1 so revision-comparing clients would apply the one-time floating->zoned correction. D-07 says verbatim "After this phase SEQUENCE stays at 1. A later edit such as a moved finalized date is in the same position 0 is in today, which is Phase 84's Assumption A5 and still governs."
  implication: the design knowingly leaves every post-phase-88 reschedule with no revision signal; the one-time bump cannot help a second change

- timestamp: 2026-09-30T15:08:00Z
  checked: .planning/phases/84-calendar-feed-foundation-and-event-subscription/*RESEARCH*.md Pitfall 4, A5, Open Question 3
  found: A5 = "a plain-PUBLISH feed is fully replaced by UID on each client refetch, so a constant SEQUENCE is harmless" -- rated LOW confidence, "the highest-impact wrong-assumption in the document", and "should be the first thing re-tested against a real device during UAT if any report of 'stale event' surfaces". Open Question 3: ship constant SEQUENCE, flag a modified-timestamp column as follow-up if a stale-event report surfaces.
  implication: the current symptom is exactly the failure mode A5 predicted if the assumption was wrong

- timestamp: 2026-09-30T15:09:00Z
  checked: CalendarSubscriptionService.GetFeedAsync (51-185) and CalendarFeedController.Feed (35-79); grep for OutputCache/ResponseCaching/Cache-Control/IMemoryCache in *.cs
  found: body is rebuilt from the DB on every request; ETag = quoted SHA-256 hex of the body; controller returns 304 only when If-None-Match string-equals that ETag; no output/response caching middleware and no Cache-Control header anywhere on the feed path (the only IMemoryCache is in AdminController). TouchLastFetchedAsync runs on every live request (throttled), before the 200/304 decision.
  implication: a reschedule changes the body, hence the ETag, hence the next fetch is a 200 with the new DTSTART. Server-side caching/304 is ruled out as the cause (confirm by test run below)

- timestamp: 2026-09-30T15:10:00Z
  checked: QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs:644-704 Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt; QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs Write_AnyEntry_EmitsSequenceOne, Write_TimedEntry_EmitsTheExactEventBlock, Write_Entry_DtstampMatchesCreatedAt, Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange
  found: the reschedule fact asserts SEQUENCE:1 exactly once on BOTH fetches with the comment "The revision number is a constant, so a rescheduled entry carries the same one on both fetches: the moved start is what tells a client the entry changed". The writer byte-pins hard-code SEQUENCE:1 and DTSTAMP=CreatedAt.
  implication: the tests pin the defect as intended behaviour; a fix must deliberately rewrite these assertions (they are not a regression net for this bug, they enforce it)

- timestamp: 2026-09-30T15:14:00Z
  checked: ran `dotnet test QuestBoard.IntegrationTests --filter` for Feed_RescheduledQuest_UpdatesInPlace..., Feed_ChangesTheEntityTag_WhenAnEventIsEdited, Feed_Returns304_WhenTheClientPresentsTheMatchingEntityTag, Feed_ReturnsAFreshBody_WhenTheClientPresentsAStaleEntityTag
  found: 4/4 passed. After a reschedule the second fetch carries the new DTSTART, a byte-identical UID line, and SEQUENCE:1 again; an edit changes the ETag; a stale If-None-Match gets 200 with a full body.
  implication: server side is correct at the transport level. The client receives the new time; it is the client's decision to apply it that fails

- timestamp: 2026-09-30T15:16:00Z
  checked: QuestBoard.Repository/Entities/EventEntity.cs, QuestEntity.cs; grep for rowversion/[Timestamp]/IsTemporal/UpdatedAt/ModifiedAt/SaveChangesInterceptor in Repository+Domain; model snapshot
  found: Events columns = Id, Title, Description, Date, StartTime, SeriesId, SeriesSlotIndex, CancelledAt, CreatedAt, GroupId. Quests = ..., CreatedAt, FinalizedDate, IsFinalized, FinalizedEmailSentForDate, ClosedDate, IsClosed, ... No UpdatedAt/rowversion/temporal/interceptor on either. Only EventSignups and ContactNotes carry UpdatedAt (EventSignups.UpdatedAt tracks the availability answer).
  implication: no existing column records when an event's or quest's time/title last changed. A per-entry monotonic revision needs a new column (migration) or a SaveChanges hook writing one

- timestamp: 2026-09-30T15:18:00Z
  checked: write paths that move an entry: EventsController.cs:283-291 (single edit -> eventService.UpdateAsync, same row), EventRepository.ApplyTemplateToOccurrencesAsync (series "this and future" sweep: Title/Description/StartTime in place across many rows), QuestRepository.FinalizeQuestAsync:119-143 (FinalizedDate overwritten in place), QuestRepository.OpenQuestAsync (FinalizedDate=null -> entry leaves feed; re-finalize later brings back the SAME UID with a new DTSTART)
  found: every reschedule keeps the row Id, hence the UID, and touches neither CreatedAt nor any revision field
  implication: every reschedule path (event, series sweep, quest re-finalize) produces the identical "same UID, same SEQUENCE, same DTSTAMP, new DTSTART" VEVENT. Title edits and availability changes (SUMMARY suffix) are in the same position

- timestamp: 2026-09-30T15:22:00Z
  checked: CalendarSubscriptionEntity.cs; CalendarSubscriptionRepository.TouchLastFetchedAsync:80-95; CalendarFeedOptions.LastFetchedThrottleMinutes (=15)
  found: the subscription row stores only LastFetchedAt -- no user agent, no client identity, no ETag served. The stamp is the real request time, written at most every 15 min
  implication: a post-change "Last fetched" proves some client fetched that address after the change, but nothing can attribute the fetch to Apple vs Google (lead 5 caveat cannot be closed from server data)

- timestamp: 2026-09-30T15:25:00Z
  checked: RFC 5545 (rfc-editor.org/rfc/rfc5545.txt) 3.8.7.2 DTSTAMP and 3.8.7.4 SEQUENCE; 88-RESEARCH.md citation of RFC 5546 2.1.5
  found: 3.8.7.2 -- for an object with no METHOD, DTSTAMP "specifies the date and time that the information associated with the calendar component was last revised in the calendar store" and "is equivalent to the LAST-MODIFIED property". 3.8.7.4 -- SEQUENCE starts at 0 and "is monotonically incremented ... each time the Organizer makes a significant revision". RFC 5546 2.1.5 -- highest SEQUENCE obsoletes others; with equal UID+SEQUENCE, DTSTAMP is the tie-breaker.
  implication: the feed has no METHOD, so DTSTAMP=CreatedAt is a spec violation once an entry is edited (it asserts "never revised"), and SEQUENCE is never incremented on significant revisions. Under the RFC's own ordering rules the refetched copy is an equal, not newer, revision. The writer's own comment (CalendarFeedWriter.cs:280-285, "DTSTAMP records when this representation of the entry was produced") describes the METHOD case, not this feed's case

- timestamp: 2026-09-30T15:27:00Z
  checked: external reports (GitHub AI-Shipping-Labs/website#1030; vendor articles surfaced by search; Apple/Google community threads, mostly JS-rendered and unreadable)
  found: practitioner consensus (anecdotal, no vendor documentation) that Apple and Google treat a same-UID entry as an update only when SEQUENCE rises (and DTSTAMP does not go backwards). Counter-evidence: in #1030 production served a raised SEQUENCE and fresh validators and Google still stayed stale for a long time -- Google's own poll/merge latency (reported 12-24h) is independent of SEQUENCE
  implication: revision gating is plausible for both clients, but Google may lag even after a fix; no latency may be promised. Evidence quality for client behaviour: weak/anecdotal

- timestamp: 2026-09-30T15:30:00Z
  checked: 88-UAT.md tests 1, 3, 4 plus git history of the UAT file (fffdb1c6..229e6fd7) for timing and the app checked
  found: test 3 (recorded 13:52Z) -- the operator's pre-existing iPhone subscription applied the floating->zoned change to entries it ALREADY held (London override: 18:00 reads 17:00); that change coincided with SEQUENCE 0->1. Test 4 (between 13:52Z and 14:26Z) -- after a reschedule with SEQUENCE staying 1 and DTSTAMP unchanged, a post-change fetch was recorded but the entry did not move. The app checked in test 4 and the subscription row inspected were NOT recorded. The Google subscription is a new address added ~13:49Z; Google's reported poll interval makes a Google poll inside that ~34-minute window unlikely but not impossible
  implication: differential evidence -- the same iPhone applies an update when SEQUENCE rises and (if it was the fetcher) did not when only DTSTART changed, which is what revision gating predicts and full-replace (Phase 84 A5) does not. Confidence is conditional on attributing the test-4 fetch to the displaying client (caveat, not resolvable from server data)

- timestamp: 2026-09-30T15:33:00Z
  checked: live local schema via sqlcmd (read-only) INFORMATION_SCHEMA.COLUMNS for Events/Quests/EventSignups; __EFMigrationsHistory head
  found: Events = Id,Title,Description,Date,StartTime,SeriesId,SeriesSlotIndex,CreatedAt,GroupId,CancelledAt. Quests = Id,Title,Description,DungeonMasterId,CreatedAt,FinalizedDate,IsFinalized,TotalPlayerCount,ChallengeRating,DungeonMasterSession,Recap,OriginalQuestId,FinalizedEmailSentForDate,GroupId,ClosedDate,IsClosed,Rewards. EventSignups has UpdatedAt. Latest migration 20260918080027_AddCalendarSubscriptions.
  implication: no existing column can serve as a monotonic per-entry revision for Events or Quests (FinalizedEmailSentForDate records a date value, not a revision; CreatedAt never moves). A correct fix needs a migration. EventSignups.UpdatedAt could feed DTSTAMP for availability-only changes

## Resolution

root_cause: "The calendar feed publishes no revision signal for an entry edited after it was first published. CalendarFeedWriter.AppendTimedEvent/AppendAllDayEvent emit a stable UID, DTSTAMP = the row's CreatedAt, a literal SEQUENCE:1 and no LAST-MODIFIED, so after a reschedule the VEVENT differs only in DTSTART/DTEND. A client that decides whether a re-fetched copy of a UID it holds is a newer revision (by SEQUENCE, then DTSTAMP, per RFC 5546 2.1.5) sees an equal revision and keeps the old time. For a METHOD-less feed RFC 5545 3.8.7.2 defines DTSTAMP as the last-revised time, so CreatedAt is wrong after any edit. Enabling cause: Events and Quests have no UpdatedAt/revision column, so the writer has nothing to derive a correct value from. This is the Phase 84 Assumption A5 failure mode ('clients fully replace by UID, so constant SEQUENCE is harmless', LOW confidence), which D-07 in phase 88 explicitly left in place for post-phase edits; the phase-88 bump 0->1 was one-time and cannot signal any later change. The feed's tests pin the defect as intended (Feed_RescheduledQuest_UpdatesInPlace... asserts SEQUENCE:1 on both fetches). Caveat (not a cause): which client made the post-change fetch the operator saw is unrecorded, so this one observation could also be confounded by fetch attribution or Google poll latency."
fix:
verification:
oracle_type:
files_changed: []
