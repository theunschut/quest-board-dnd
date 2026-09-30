# Phase 88: Calendar Feed Times Anchored to the Board Timezone - Context

**Gathered:** 2026-09-30
**Status:** Ready for planning

<domain>
## Phase Boundary

The calendar feed tells every subscriber's calendar app which timezone a timed entry's wall-clock
time belongs to, so a game night set for 18:00 on the board shows as 18:00 in Google Calendar as
well as on the iPhone. The zone is the one board zone the application already has (`IBoardClock`,
configured by `TimeZoneOptions.BoardTimeZoneId`, default `Europe/Amsterdam`).

Out of this phase: a per-board timezone setting, any change to the reminder jobs, the "today"
reads or page rendering, and all-day entries.

</domain>

<decisions>
## Implementation Decisions

### Diagnosis, settled by production evidence

These facts were gathered from the operator on 2026-09-30 before any fix was chosen, as the
ROADMAP entry requires. They replace the ROADMAP's two open hypotheses.

- **Apple Calendar on the iPhone is already correct.** The operator's iPhone was subscribed
  directly in Apple Calendar, so Apple fetches the feed. It shows a board event at 18:00 and a
  quest at 18:00, both correct. Apple renders a zoneless time in the phone's own zone, which is
  what Phase 84 D-01 assumed.
- **Google Calendar is the only broken route.** The friend's phone subscribed through Google
  Calendar, so Google's servers fetch the feed. It shows the 18:00 event at 20:00, which fits
  Google reading a zoneless time as UTC and converting it to summer time (+2). After 25 October
  2026 the same mistake would show 19:00.
- **The quest path has no data bug.** An early report of a quest at 19:00 on the iPhone turned
  out to be a misreading. The live feed for quest 12039 carries `DTSTART:20261002T180000`, which
  matches the board. The stored `FinalizedDate`, the board rendering and the feed all agree.
- **Production's zone resolves.** `/health` reported `Healthy` on 2026-09-30, so the board clock
  is not running on the UTC fallback.

**Consequence:** the fix must make Google correct without changing what Apple already shows
correctly.

### Scope

- **D-01: One zone, the resolved board zone, is written onto every timed entry.** No per-board
  setting. The reminder jobs, `DailyReminderJob`'s "tomorrow", the series runway, the ambient
  "today" reads and page rendering are untouched. The operator raised a per-board timezone
  setting mid-discussion. When shown that a board's zone is a property of the clock and not of
  the feed, they said the jobs can stay as they are and the feed just needs to write the zone
  onto each quest and event time. Per-board zones are deferred (see `<deferred>`).

### How the zone is written

- **D-02: Every timed entry's `DTSTART` and `DTEND` carry `TZID=<zone>`, and the stored
  wall-clock value is written unchanged.** For example: `DTSTART;TZID=Europe/Amsterdam:20261002T180000`.
  The feed never converts the stored value into another hour. It only says which zone the hour
  belongs to. This keeps the architecture rule that a wall-clock value is never
  timezone-converted. A time that does not exist on a clock-change night (such as 02:30 on the
  last Sunday of March) is left for the client to resolve, as RFC 5545 defines, so the feed needs
  no code for that case.

  Rejected: UTC with a trailing `Z` converted per entry date (the recommendation at the first
  pass; it was set aside because the operator preferred the zone name to travel with the entry).

  — **Reversibility:** reversible — feed output only, no stored data. Changing the encoding again
  changes what each client receives at its next poll.

- **D-03: The `VTIMEZONE` block is generated from the resolved zone and lists fixed-date
  observances: only the actual offset changes inside the span the feed covers.** The changes are
  found by asking the zone for its UTC offset at a moment (`TimeZoneInfo.GetUtcOffset`), not by
  translating `TimeZoneInfo.GetAdjustmentRules()` into a yearly `RRULE`.

  Why, as explained to the operator: .NET gets Windows zone data from Windows itself and Linux
  zone data from the tz database files. Both give the same *offset at a moment*, but they
  *describe the rules* in different shapes: Windows as a short repeating rule, Linux typically as
  a year-by-year list. A block translated from the rule description would come out as different
  text on the dev machine and in production, so a byte-pinning test on Windows would not prove
  what production writes. A block built only from offset-at-a-moment is identical on both, and
  covers only the few changes the feed window spans.

  Rejected:
  - a yearly-rule `VTIMEZONE` translated from adjustment rules (compact, but platform-shaped and
    only provable on Windows);
  - a hard-coded Amsterdam block (least code, but it silently lies for a self-hoster who
    configures another zone on the supported Docker path, and it breaks the requirement that the
    declared zone is the resolved zone);
  - UTC with `Z`.

  The operator asked whether a board timezone setting would simplify this. It would not. The zone
  is already known, and the hard part is writing that zone's rules into the file. Per-board zones
  would mean one `VTIMEZONE` per distinct zone, which is more work, not less.

- **D-04: An `X-WR-TIMEZONE` calendar header is emitted as a hint, with the same zone id as the
  `TZID`.** This is the operator's choice against the recommendation, which was to omit it
  because no zoneless time is left for it to apply to. It must come from the same resolved zone
  as `TZID` and `VTIMEZONE`, so the three cannot disagree.

- **D-05: All-day entries do not change.** `DTSTART;VALUE=DATE` has no time of day, so it gets
  no `TZID` and no conversion. This comes from the ROADMAP scope and was not re-discussed.

### When the zone could not be resolved

- **D-06: The feed always declares exactly the zone the clock resolved, UTC included. There is
  no degraded branch in the writer.** When `BoardClock` has fallen back to UTC (Phase 86 D-04),
  the same code path writes `TZID=UTC`, a `VTIMEZONE` with a single +00:00 observance and no
  changes, and `X-WR-TIMEZONE` naming UTC. This is the operator's choice against the
  recommendation, which was to fall back to today's zoneless output.

  **Accepted cost, stated at the time:** while the zone is broken, every subscriber's calendar,
  including the iPhones that are correct today, shows game nights one to two hours late. The
  signal is the existing `BoardTimeZoneHealthCheck` reporting `Degraded` on `/health`. The next
  fetch after the zone is fixed puts every entry back.

  Offered and not chosen: refusing to serve the feed, and reversing Phase 86 D-04 so startup
  refuses an unresolvable zone.

### Already-synced entries

- **D-07: `SEQUENCE` goes from `0` to `1` on every entry, as a constant.** Every entry the phones
  already hold keeps its UID, and its `DTSTAMP` never moves (it is the source row's `CreatedAt`),
  so a client that decides updates by revision number would otherwise see nothing new. The bump
  costs one literal and needs no stored data. Clients that ignore `SEQUENCE` are unaffected.
  After this phase `SEQUENCE` stays at `1`. A later edit such as a moved finalized date is in the
  same position `0` is in today, which is Phase 84's Assumption A5 and still governs.

  — **Reversibility:** one-way — once `1` is published, it may never go back to `0`. A client
  that compares revisions would treat a lower number as older and ignore every later change.

- **D-08: If Google still shows an old time after the fix and a refresh, asking the group to
  remove and re-add the subscription once is an acceptable resolution, and the phase can close on
  it.** This is the operator's choice against the recommendation, which was to escalate to
  rotating the UID scheme (old entries vanish through the same disappear-to-remove path that
  cancellations use under 84 D-12) and re-verify. UID rotation is therefore not planned.

### Proof

- **D-09: Verification runs on production, on Google Calendar (the friend's phone) and on Apple
  Calendar (the operator's iPhone). Outlook is not required.** Google fetches from its own servers,
  so no localhost or LAN address can satisfy it. That is the gap Phases 84 and 85 left open, and
  this phase closes it for Google and Apple.
  - Check an entry that was already synced before the fix first. Record whether it corrects
    itself; a failure falls back to D-08 and is not a phase failure.
  - Then confirm that new entries show 18:00.
  - The iPhone check guards against a regression on the route that already works.
  - Google may take up to a day to refresh. No copy may promise a refresh latency that has not
    been observed.

### Carried from ROADMAP, not re-discussed

- **The floating-time contract is reversed deliberately, and its guards are rewritten, not
  deleted.** This covers `CalendarFeedFloatingTimeGuardTests`, the `NotContain("TZID")` /
  `NotContain("VTIMEZONE")` facts in `CalendarFeedWriterTests`, and the `…Z`-absence assertions in
  `CalendarSubscriptionFeedTests` and `CalendarSubscriptionQuestFeedTests`. Each is rewritten to
  pin the new contract: `TZID` present on timed entries, the local digits unchanged, no trailing
  `Z` on timed entries, and a `VTIMEZONE` whose `TZID` matches. The same applies to
  `.claude/architecture.md` "Time and the board clock" and to the writer's own comments.
- **The requirement family is minted in the first plan.** Phase 88's `Requirements:` is `TBD`.
  Phases 84 (`CALFEED`) and 85 (`QUESTFEED`) minted theirs into `REQUIREMENTS.md` and
  `ROADMAP.md` in their own first plan, and this phase should do the same.

### Claude's Discretion

- **How the zone reaches the writer.** It can be a property on `CalendarFeedEntry` (per entry) or
  a parameter to `ICalendarFeedWriter.Write` (per document). The only hard constraint: `TZID`,
  `VTIMEZONE` and `X-WR-TIMEZONE` all derive from the single resolved `TimeZoneInfo`
  (`IBoardClock.TimeZone`), never from the raw `TimeZoneOptions.BoardTimeZoneId` string. Per-entry
  would keep the deferred per-board idea cheap but is not required. `CalendarSubscriptionService`
  takes `TimeProvider` today, not `IBoardClock`. Adding it must not weaken
  `AmbientClockSeamTests`.
- **The `TZID` string form.** Prefer an IANA name, since Google recognises those by name. If the
  resolved `TimeZoneInfo.Id` is a Windows id, whether to convert it with
  `TimeZoneInfo.TryConvertWindowsIdToIanaId` is the planner's call. The exact UTC-fallback id
  (`UTC` vs `Etc/UTC`) is too.
- **The `VTIMEZONE` span and shape.** This covers the range to scan (the `CalendarFeedOptions`
  `MonthsBack`/`MonthsAhead` window, or the min/max of emitted timed entries), how offset changes
  are located, the leading observance for the offset in effect at the start of the span, and
  whether a feed with no timed entries emits the block at all. Placement before the first
  `VEVENT` follows RFC convention.
- **`X-WR-TIMEZONE` placement** among the existing calendar headers in `AppendCalendarHeaders`.

### Open for research, not decided

- How Google Calendar treats `TZID` with an IANA id on a "From URL" subscription: whether it uses
  the name, the `VTIMEZONE` body, or both.
- Whether Google updates an already-held entry when only `DTSTART`/`DTEND` change, and whether
  `SEQUENCE` influences that. This is where D-07 and D-08 get tested.
- Whether Apple Calendar and Google both accept a `VTIMEZONE` built from fixed-date observances
  with no `RRULE` (valid per RFC 5545 §3.6.5), and whether either has a known quirk when
  `X-WR-TIMEZONE` sits alongside `TZID` (for example, double-applying an offset).
- Confirm the Windows/Linux adjustment-rule shape difference behind D-03. If it proves narrower
  than described, D-03 still stands, because offset probing is platform-independent either way.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and origin
- `.planning/ROADMAP.md` §"Phase 88: Calendar Feed Times Anchored to the Board Timezone": origin,
  root cause, the resolved-zone caveat, and the scope notes (test-contract reversal, the stored
  value keeps its meaning, all-day entries out of scope, real-phone proof).

### Prior calendar-feed decisions
- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-CONTEXT.md`: D-01
  (floating time, reversed by this phase), D-02/D-03 (block length, `TRANSP`), D-12
  (disappear-to-remove), D-13 (rolling window), `UID` and `SEQUENCE` scheme.
- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-RESEARCH.md`:
  Assumption A5 (a plain-publish feed with no `METHOD` is replaced by `UID` on refetch), which
  that document marks as its least certain claim.
- `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-CONTEXT.md`: D-05 (four-hour
  quest block), and the inherited `SEQUENCE:0` assumption and outstanding real-device check.

### Board clock
- `.planning/phases/86-viewer-local-times-and-correct-job-scheduling/86-CONTEXT.md`: the
  real-instant vs wall-clock classification, D-03 (one configurable zone, `Europe/Amsterdam`
  default), D-04 (UTC fallback with a `Degraded` health check).
- `.claude/architecture.md` §"Time and the board clock": describes the feed as floating and must
  be rewritten by this phase.

### External standard
- RFC 5545: §3.2.19 (`TZID` parameter), §3.6.5 (`VTIMEZONE`, including observances without
  `RRULE`), §3.8.7.4 (`SEQUENCE`), §3.3.5 (DATE-TIME forms: local with `TZID`, UTC, floating).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IBoardClock.TimeZone` / `IBoardClock.IsDegraded` (`QuestBoard.Domain/Interfaces/IBoardClock.cs`,
  `QuestBoard.Domain/Services/BoardClock.cs`): the resolved zone, resolved once per process and
  already UTC on fallback. This is the single source D-02, D-03, D-04 and D-06 draw from.
  Registered as a singleton in `QuestBoard.Domain/Extensions/ServiceExtensions.cs:72`.
- `BoardTimeZoneHealthCheck` (`QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs`):
  already reports `Degraded` on fallback, and is the signal D-06 relies on. No change needed.
- `FakeBoardClock` (`QuestBoard.UnitTests/Helpers/FakeBoardClock.cs`): lets writer tests pin
  Amsterdam, UTC and a non-Amsterdam zone.

### Established Patterns
- **Hand-rolled writer.** `CalendarFeedWriter` (`QuestBoard.Domain/Services/CalendarFeedWriter.cs`)
  is a stateless singleton, deliberately not a full RFC 5545 library. Its class comment ("no
  timezone and no recurrence") becomes false and must be rewritten. The `VTIMEZONE` should keep
  that hand-rolled character. Adding a library is not implied by any decision here.
- **Line folding and CRLF** through `AppendFoldedLine`/`FoldLine`: the new `VTIMEZONE` lines go
  through the same path.
- **Code-default options** (`CalendarFeedOptions`, `TimeZoneOptions`): if any new knob appears,
  follow the refuse-to-start `IsValid()` pattern. None is required by the decisions.

### Integration Points
- `CalendarFeedWriter.AppendTimedEvent` (lines 70-84): `DTSTART`/`DTEND` gain `;TZID=`, and
  `SEQUENCE:0` becomes `SEQUENCE:1`. `AppendAllDayEvent` (89-102) keeps `VALUE=DATE` and gets the
  same `SEQUENCE:1`.
- `CalendarFeedWriter.AppendCalendarHeaders` (44-64): gains `X-WR-TIMEZONE`. The `VTIMEZONE` goes
  between the headers and the first `VEVENT`.
- `ICalendarFeedWriter.Write(IReadOnlyList<CalendarFeedEntry>, string)`: the signature may grow a
  zone, per Claude's Discretion.
- `CalendarSubscriptionService` (`QuestBoard.Domain/Services/CalendarSubscriptionService.cs`):
  builds entries. The quest mapping at lines 145-146 splits `FinalizedDate` straight into
  `Date`/`StartTime` with no conversion, which is correct and must stay that way. The comment near
  line 114 that calls `FinalizedDate` "server local time" predates Phase 86's wall-clock
  classification.
- Tests to rewrite, not delete: `QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs`
  (lines 47-48, 67-68), `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` (lines 107,
  276-277), `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs`,
  `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`.
- `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs`: a closed list of permitted
  ambient clock reads. Do not weaken it.

</code_context>

<specifics>
## Specific Ideas

- Live production bytes for the quest the operator checked (subscription address withheld):
  `UID:questboard-quest-12039`, `DTSTAMP:20260817T074848Z`, `DTSTART:20261002T180000`,
  `DTEND:20261002T220000`, `SEQUENCE:0`, `SUMMARY:[Euphoria Inn] Daggerheart: one-shot`. After
  this phase the same entry should read `DTSTART;TZID=Europe/Amsterdam:20261002T180000` and
  `SEQUENCE:1`.
- Write the rewritten guard tests on both sides of the clock change on 25 October 2026, for
  example one entry on 2 October (+02:00) and one on 30 October (+01:00). Assert that the
  `VTIMEZONE` records the change between them, and that both `DTSTART` values keep their local
  `180000` digits.
- One test should pin a non-Amsterdam configured zone. That proves the block is generated and not
  hard-coded.

</specifics>

<deferred>
## Deferred Ideas

- **Per-board timezone setting.** A timezone field on each board, defaulting to Amsterdam, so
  boards can be used internationally. It would sit beside `BoardType` on the Platform Group
  Create/Edit pages. Doing it properly means making `IBoardClock` board-aware across roughly 15
  consumers, including the single reminder cron registration, and answering what happens to an
  existing game night when a board's zone changes. The operator does not need the jobs to change
  zone, so this is parked. A feed that writes one `VTIMEZONE` per distinct zone would absorb it
  later.

</deferred>

---

*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Context gathered: 2026-09-30*
