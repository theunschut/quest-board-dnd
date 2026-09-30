# Phase 88: Calendar Feed Times Anchored to the Board Timezone - Research

**Researched:** 2026-09-30
**Domain:** RFC 5545 iCalendar output (hand-rolled writer), .NET `TimeZoneInfo` behaviour on Windows vs Linux, calendar-client handling of `TZID`
**Confidence:** HIGH for the .NET mechanics and the codebase map (both run and read this session). MEDIUM for RFC conformance of the encoding. LOW for how Google Calendar and Apple Calendar react (no primary vendor documentation exists; only the on-device checks in D-09 can settle it).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

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

### Deferred Ideas (OUT OF SCOPE)

- **Per-board timezone setting.** A timezone field on each board, defaulting to Amsterdam, so
  boards can be used internationally. It would sit beside `BoardType` on the Platform Group
  Create/Edit pages. Doing it properly means making `IBoardClock` board-aware across roughly 15
  consumers, including the single reminder cron registration, and answering what happens to an
  existing game night when a board's zone changes. The operator does not need the jobs to change
  zone, so this is parked. A feed that writes one `VTIMEZONE` per distinct zone would absorb it
  later.

### Open for research, not decided (CONTEXT.md; answered in this document)

- How Google Calendar treats `TZID` with an IANA id on a "From URL" subscription.
- Whether Google updates an already-held entry when only `DTSTART`/`DTEND` change, and whether `SEQUENCE` influences that.
- Whether Apple Calendar and Google accept a fixed-date `VTIMEZONE` with no `RRULE`, and any `X-WR-TIMEZONE`-plus-`TZID` quirk.
- Confirm the Windows/Linux adjustment-rule shape difference behind D-03.
</user_constraints>

<phase_requirements>
## Phase Requirements

No IDs exist yet (ROADMAP `Requirements: TBD`); CONTEXT says the first plan mints them. Proposed family: **`CALTZ`** (Calendar Timezone), matching the `CALFEED` / `QUESTFEED` naming. Candidate wording below is written to be testable and to keep the plan-checker's traceability table (`.planning/REQUIREMENTS.md` lines 339-363 use `| ID | Phase N | Status |` rows).

| ID (proposed) | Description (candidate wording) | Research Support |
|----|-------------|------------------|
| CALTZ-01 | Every timed calendar entry (event and quest) carries `TZID` naming the board's resolved zone on both `DTSTART` and `DTEND`, with the stored wall-clock digits written unchanged and no trailing `Z`. | Encoding per D-02; RFC 5545 §3.3.5 Form #3; Code Examples "Timed event lines" |
| CALTZ-02 | A feed containing at least one timed entry carries exactly one `VTIMEZONE` whose `TZID` equals the entries' `TZID`, placed after the calendar headers and before the first `VEVENT`, listing a leading observance plus every UTC-offset change inside the span of the timed entries as fixed-date observances (no `RRULE`). | RFC 5545 §3.6.5 (DTSTART-only example); offset-probing algorithm (verified byte-identical on Windows and Linux) |
| CALTZ-03 | The calendar carries one `X-WR-TIMEZONE` header whose value is the same id as the `TZID`, and `TZID`, `VTIMEZONE` and `X-WR-TIMEZONE` all derive from the one `TimeZoneInfo` the board clock resolved. | D-04; single derivation in Code Examples |
| CALTZ-04 | A board configured with a zone other than the default (or with a Windows-style id) has that zone declared, and its offset changes listed, rather than Amsterdam's; proves the block is generated, not hard-coded. | Pitfalls 1 and 2; non-Amsterdam test |
| CALTZ-05 | When the board clock has fallen back to UTC, the same code path declares UTC (`TZID`, a single +0000 observance, `X-WR-TIMEZONE`); the writer has no zoneless branch. | D-06; `BoardClock.ResolveZone` returns `TimeZoneInfo.Utc` |
| CALTZ-06 | Every entry, timed and all-day, carries `SEQUENCE:1`, while `UID` and `DTSTAMP` are unchanged. | D-07; RFC 5546 §2.1.5 rule 4 |
| CALTZ-07 | All-day entries carry no `TZID` and stay `VALUE=DATE`; a feed with no timed entry emits no `VTIMEZONE`. | D-05; RFC 5545 §3.2.19 |
| CALTZ-08 | The feed never converts a stored wall-clock value: a game night stored as 18:00 is written with `180000` digits whatever zone is configured. | D-02; architecture rule "wall-clock value is never timezone-converted" |
| CALTZ-09 | Verified on production on Google Calendar and Apple Calendar per D-09 (already-synced entry first, then new entry, then iPhone regression check). Manual-only. | Validation Architecture "Manual-only" |
| CALTZ-10 | The superseded floating-feed statements are corrected: `CALFEED-10` wording, `.claude/architecture.md` "Time and the board clock", the writer's comments, the `CalendarSubscriptionService` "server local time" comment. | Code map "Stale statements" |
</phase_requirements>

## Summary

The encoding decided in CONTEXT (D-02/D-03/D-04/D-07) is implementable in the existing hand-rolled writer with no new package. I prototyped the `VTIMEZONE` generator and ran it on Windows (dotnet 10.0.400) and inside `mcr.microsoft.com/dotnet/sdk:10.0` (Linux, Ubuntu 24.04) across six zones (Amsterdam with two different spans, UTC, Auckland, Lord Howe with its 30-minute DST, New York). The concatenated output hashes to the same SHA-256 on both platforms: `7D443E9032107D54BD5FC019DA698FCBF932D84DB89834CDA3B59F78CB608457`. D-03's premise is also confirmed empirically: for `Europe/Amsterdam`, `GetAdjustmentRules()` returns 1 rule on Windows and 147 on Linux, yet offset probing yields the identical two transitions (`2026-10-25T01:00Z` and `2027-03-28T01:00Z`).

Client behaviour is where evidence is thin. RFC 5545 itself supports the shape (its own second `VTIMEZONE` example uses DTSTART-only observances with no `RRULE`, "suitable for" events inside the covered interval). RFC 5546 §2.1.5 says `SEQUENCE` is the secondary key and `DTSTAMP` the tiebreaker, which explains why D-07 is needed for any client that follows iTIP ordering (our `DTSTAMP` is a constant `CreatedAt`). For Google specifically, only third-party articles (no primary documentation, no cited test evidence) claim that Google resolves `TZID` by IANA name and may ignore the `VTIMEZONE` body. Using an IANA id plus a correct body covers both readings. Nothing citable says `X-WR-TIMEZONE` next to `TZID` double-applies an offset; it is a Google-originated convention and, when it names the same zone as `TZID`, any client that honours either produces the same hour. D-09's on-device checks are the only real proof, and D-08 is the accepted fallback.

Code impact is small but has wide test churn: `ICalendarFeedWriter.Write` gains a zone, `CalendarSubscriptionService` gains `IBoardClock`, two test files that construct the service need a new constructor argument, 33 writer call sites and one NSubstitute setup need the new parameter, and roughly 20 assertions that pin `DTSTART:`/`DTEND:` prefixes or `SEQUENCE:0` must be rewritten (full list below). `AmbientClockSeamTests` is not affected by adding `IBoardClock` to the service, because neither `CalendarSubscriptionService.cs` nor the writer is in its guarded lists.

**Primary recommendation:** Add a required `TimeZoneInfo boardZone` parameter to `ICalendarFeedWriter.Write` (per-document, so `TZID`/`VTIMEZONE`/`X-WR-TIMEZONE` cannot disagree by construction), inject `IBoardClock` into `CalendarSubscriptionService` and pass `boardClock.TimeZone`, derive the `TZID` string once (IANA id; `"UTC"` for the fallback), generate the `VTIMEZONE` from the span of the timed entries by 1-day stepping plus whole-minute bisection over `GetUtcOffset`, omit `TZNAME`, and bump `SEQUENCE` to a literal `1`.

## Architectural Responsibility Map

The application tiers here are Domain services, the MVC edge, and the external calendar client (which is a tier this phase must respect but cannot change).

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Resolving the board zone (and degrading to UTC) | Domain (`IBoardClock` / `BoardClock`, singleton) | — | Already the single seam; resolved once per process. The feed must read it, never re-resolve from `TimeZoneOptions`. |
| Declaring the zone in the document (`TZID`, `VTIMEZONE`, `X-WR-TIMEZONE`, `SEQUENCE`) | Domain (`CalendarFeedWriter`, pure text-in/text-out) | — | Stateless singleton; takes the zone as data so it stays pure and testable without DI. |
| Choosing which zone the writer is handed | Domain (`CalendarSubscriptionService`) | — | Only place that both builds entries and holds DI; reads `IBoardClock.TimeZone`. |
| HTTP delivery, ETag | MVC edge (`CalendarFeedController`) | — | Unchanged. ETag is a SHA-256 of the body, so the new bytes give every subscriber a new tag automatically. |
| Interpreting `TZID`/`VTIMEZONE`/`X-WR-TIMEZONE`, deciding updates | External calendar client (Google servers, Apple Calendar) | — | Out of our control; can only be proven on-device (D-09). |
| Stored game-night value | Database / Repository | — | Unchanged. Stays a wall-clock value; the writer never converts it. |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET `System.TimeZoneInfo` (BCL, `net10.0`) | SDK 10.0.400 [VERIFIED: `dotnet --version` run this session] | `GetUtcOffset`, `IsDaylightSavingTime`, `TryConvertWindowsIdToIanaId` | Already the type `IBoardClock.TimeZone` exposes. No package needed. |
| Hand-rolled `CalendarFeedWriter` | in repo | Emits the document | CONTEXT "Established Patterns": the writer is deliberately not an RFC 5545 library and "the `VTIMEZONE` should keep that hand-rolled character". |

### Supporting (test only, already referenced)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.2 | Test framework | All new facts [VERIFIED: `QuestBoard.UnitTests.csproj:21`, `QuestBoard.IntegrationTests.csproj:21`] |
| FluentAssertions | 8.10.0 | Assertions (`body.Should().Contain(...)`) | Existing style [VERIFIED: csproj line 13 in both] |
| NSubstitute | 5.3.0 | Substitute `ICalendarFeedWriter` in the service guard test | Only in `QuestBoard.UnitTests` [VERIFIED: csproj line 16] |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 | `WithWebHostBuilder` zone-variant factories | Integration tests [VERIFIED: `QuestBoard.IntegrationTests.csproj:14`] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled `VTIMEZONE` | Ical.Net / other library | Decided against by CONTEXT; adding a library is "not implied by any decision here". Also a new dependency for ~60 lines. |
| Per-document zone parameter (recommended) | `CalendarFeedEntry.TimeZone` per entry | Per-entry lets entries disagree with the one `X-WR-TIMEZONE` and needs multi-zone `VTIMEZONE` logic no decision asks for. It would only pay off when per-board zones (deferred) arrive; changing the signature then is cheap. |
| `GetAdjustmentRules()` translation | offset probing | Rejected by D-03; verified below to be platform-shaped (1 vs 147 rules). |

**Installation:** none. No new package.

**Version verification:** no external packages are added, so the registry commands (`npm view`, `pip index`) do not apply. Test-package versions above were read from the csproj files.

## Package Legitimacy Audit

No external package is installed by this phase. `gsd_run query package-legitimacy check` was therefore not run.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | — |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Calendar client poll (Google servers / iPhone)
        |  GET /feeds/calendar/{token}.ics   (anonymous)
        v
CalendarFeedController  --(unchanged)--> ETag = SHA-256(body); If-None-Match -> 304
        |
        v
CalendarSubscriptionService.GetFeedAsync
   |  membership + event rows + quest rows  (unchanged; Date/StartTime split, never converted)
   |  IBoardClock.TimeZone  (singleton; UTC when BoardClock fell back)   <-- NEW read
   v
ICalendarFeedWriter.Write(entries, calendarName, boardZone)             <-- NEW parameter
   |
   |-- tzid = one string derived from boardZone (IANA form)
   |-- headers: VERSION, PRODID, CALSCALE, X-WR-CALNAME, X-WR-TIMEZONE:tzid (NEW), TTL, REFRESH
   |-- if any timed entry:
   |      span = [min start, max end] of timed entries (wall-clock digits)
   |      window = span, treated as UTC instants, padded 1 day each side
   |      offset probing over window  -> leading observance + one observance per offset change
   |      BEGIN:VTIMEZONE ... END:VTIMEZONE   (NEW; after headers, before first VEVENT)
   |-- per entry:
   |      timed  : DTSTART;TZID=tzid:<digits unchanged>, DTEND;TZID=tzid:<digits unchanged>, SEQUENCE:1
   |      all-day: DTSTART;VALUE=DATE (unchanged), SEQUENCE:1
   v
text/calendar body  -->  client resolves the local time in tzid (client's job, RFC 5545 §3.3.5 Form #3)
```

### Recommended Project Structure

No new files are required. Changes stay inside:

```
QuestBoard.Domain/
├── Interfaces/ICalendarFeedWriter.cs        # Write gains `TimeZoneInfo boardZone`
├── Services/CalendarFeedWriter.cs           # headers, VTIMEZONE builder, TZID lines, SEQUENCE
└── Services/CalendarSubscriptionService.cs  # + IBoardClock ctor parameter, passes .TimeZone
QuestBoard.UnitTests/Services/               # 3 test classes touched (writer, guard, quest recheck)
QuestBoard.IntegrationTests/Tests/           # 2 feed test classes touched
.claude/architecture.md                      # rewrite the feed paragraph
.planning/REQUIREMENTS.md, ROADMAP.md        # mint CALTZ family, amend CALFEED-10
```

If the `VTIMEZONE` builder grows past ~60 lines, extracting a small `internal static class` (for example `VTimeZoneBuilder`) inside `QuestBoard.Domain/Services/` is reasonable; the class comment on the writer ("five-field VEVENT with no timezone") must be rewritten either way.

### Pattern 1: Zone travels per document, derived once
**What:** `Write` takes the resolved `TimeZoneInfo`; the writer derives `tzid` once and passes that one string to the header, the `VTIMEZONE` and every timed event.
**When to use:** always; this makes "the three cannot disagree" a structural fact instead of a test-only fact.

### Pattern 2: Offset probing (no `GetAdjustmentRules`)
**What:** find offset changes only through `GetUtcOffset` at instants; step by at most one day, then bisect on whole minutes.
**Why:** verified identical bytes on Windows and Linux (see Code Examples and Sources).

### Anti-Patterns to Avoid
- **Injecting `IOptions<TimeZoneOptions>` into the service or writer.** Reading the raw configured id is exactly the "declares the configured zone while the clock fell back to UTC" bug. Only `IBoardClock.TimeZone` may feed the writer.
- **Calling `TimeZoneInfo.ConvertTime*` on an entry.** The stored value is a wall-clock value. `entry.Date.ToDateTime(entry.StartTime)` produces an `Unspecified`-kind `DateTime`; keep it that way.
- **Using `StandardName`/`DisplayName` for `TZNAME`.** The names differ by platform (Windows `W. Europe Standard Time` vs Linux `Central European Standard Time` for the same zone; verified below), which would break byte-identical output. Omit `TZNAME`.
- **Emitting `TZID` on `DTSTAMP` or on `VALUE=DATE` lines.** RFC 5545 §3.2.19 forbids it (quote under Sources).
- **Putting GSD ids in source comments** (project rule).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Finding UTC-offset transitions | A translator from `TimeZoneInfo.AdjustmentRule` to observances/`RRULE` | `GetUtcOffset` probing (bisect) | Rule shapes differ per platform (1 vs 147 rules for Amsterdam). Probing is platform-independent. |
| Windows id to IANA name | A lookup table | `TimeZoneInfo.TryConvertWindowsIdToIanaId` | BCL mapping; note its canonical-region result (below). |
| Line folding, CRLF, text escaping | New folding for the new lines | `AppendFoldedLine`, `EscapeText` already in the writer | Existing tests pin them. |
| DST-gap / overlap resolution for entries | Any code | The client, per RFC 5545 §3.3.5 | D-02: "the feed needs no code for that case." |
| Cross-platform confidence | Assuming Windows green means production green | The prototype's exact-bytes assertions (identical on both platforms) plus an optional Docker probe | The Windows dev box is not the production platform. |

**Key insight:** the only genuinely hard part is not writing `TZID`; it is producing a `VTIMEZONE` that a byte-pinning test on Windows proves for Linux. Offset probing solves that because both platforms answer "what is the offset at instant T" identically.

## Runtime State Inventory

Not a rename/refactor/migration phase (the stored value and identifiers do not change). For completeness, the one runtime-state question that matters:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None. `FinalizedDate`, event `Date`/`StartTime`, subscription rows are not touched. Verified: the only DB read paths are repository calls in `CalendarSubscriptionService`. | None |
| Live service config | None in this repo. Subscriber-side state exists outside git: Google's servers and each phone hold a copy of every entry keyed by `UID` (`questboard-event-N`, `questboard-quest-N`). | The on-device re-check in D-09 / fallback D-08; no code |
| OS-registered state | None | None |
| Secrets/env vars | `TimeZone__BoardTimeZoneId` in the server env file (default `Europe/Amsterdam` in code; `TimeZoneOptions.cs:9`). Not renamed. | None |
| Build artifacts | None | None |

## Common Pitfalls

### Pitfall 1: `TZID` string is a Windows id or the wrong IANA alias
**What goes wrong:** `BoardClock` calls `TimeZoneInfo.FindSystemTimeZoneById(options.Value.BoardTimeZoneId)`. Both platforms accept both id families; the `.Id` you get back is whatever was asked for. Verified on Windows and Linux: `FindSystemTimeZoneById("Europe/Amsterdam").Id` is `Europe/Amsterdam`, `FindSystemTimeZoneById("W. Europe Standard Time").Id` is `W. Europe Standard Time`. `TryConvertWindowsIdToIanaId("W. Europe Standard Time")` returns `Europe/Berlin` (canonical region), not Amsterdam; `TryConvertWindowsIdToIanaId("UTC")` returns `Etc/UTC`.
**Why it happens:** Windows ids map to one canonical IANA id per Windows zone.
**How to avoid:** Recommended rule: `id.Contains('/') || id == "UTC" ? id : (TryConvertWindowsIdToIanaId(id, out var iana) ? iana : id)`. This keeps `Europe/Amsterdam` and the UTC fallback verbatim, turns a Windows-style id into an IANA name (the wrong-city cosmetic is harmless because the `VTIMEZONE` body carries the true rules and Berlin and Amsterdam share rules), and falls back to the raw id (declared exactly as resolved, body still correct) if there is no mapping. Recommend `UTC` (literal, equals `TimeZoneInfo.Utc.Id` on both platforms) rather than `Etc/UTC` for the fallback: it is the string the clock actually resolved, and `X-WR-TIMEZONE:UTC` is a common value. [ASSUMED that Google and Apple both recognise the bare name `UTC`; the `VTIMEZONE` body covers it if they do not.]
**Warning signs:** a test that configures `W. Europe Standard Time` and still sees a space in `TZID`.

### Pitfall 2: `TZNAME` or observance labels differ by platform
**What goes wrong:** `StandardName` for Amsterdam is `W. Europe Standard Time` on Windows and `Central European Standard Time` on Linux [VERIFIED: probe output]. Using it for `TZNAME` makes the Windows test bytes differ from production.
**How to avoid:** omit `TZNAME` (RFC 5545 §3.6.5 lists it as optional: "The optional "TZNAME" property is the customary name..."). Label observances STANDARD/DAYLIGHT with `IsDaylightSavingTime(instant)`; for the odd negative-DST zones I probed (`Europe/Dublin`, `Africa/Casablanca`) Windows and Linux agree [VERIFIED: probe output identical], and the label does not affect the offset a client computes anyway (RFC: the offset comes from the observance with the last onset before the time in question).

### Pitfall 3: Bisecting to the tick instead of the minute
**What goes wrong:** a "until within 1 second" bisect leaves fractional ticks (my first probe printed `01:00:00.4394532Z`), which formats into wrong seconds or unstable bytes.
**How to avoid:** bisect on integer minutes since the step start (all real transitions fall on whole minutes) and format with `yyyyMMdd'T'HHmmss`.

### Pitfall 4: Span computed from the wrong quantity
**What goes wrong:** the `VTIMEZONE` must cover every `DTSTART` and every `DTEND` (a 4-hour quest ending after a clock change, or a 23:30 start whose end is on the next day).
**How to avoid:** span = min of timed starts to max of `start + Duration`. Probe window = that span read as UTC instants, padded one day each side (a wall-clock L maps to a UTC instant within `[L-14h, L+12h]` for every real zone, so the pad guarantees any offset change relevant to an entry lies in the window). The pad can add one extra observance just outside the entries; harmless.

### Pitfall 5: `TZID` emitted without a `VTIMEZONE`, or a `VTIMEZONE` nobody references
**What goes wrong:** RFC 5545 §3.6.5: "An individual "VTIMEZONE" calendar component MUST be specified for each unique "TZID" parameter value specified in the iCalendar object."
**How to avoid:** emit the block iff at least one timed entry exists. `X-WR-TIMEZONE` is emitted regardless (single code path per D-06).

### Pitfall 6: Existing tests assert the old prefixes and fail, not only the `Z`-absence ones
The scope notes name the `NotContain` guards, but the positive assertions on `DTSTART:` / `DTEND:` prefixes and `SEQUENCE:0` also break (full list under Code Map).

### Pitfall 7: UTC-fallback drift
`BoardClock` swallows `TimeZoneNotFoundException`/`InvalidTimeZoneException` and returns `TimeZoneInfo.Utc` with `Degraded = true`. If the feed read the options string instead, a typo would declare `TZID=Europe/Amsterdm` with UTC rules. Structural defence: the service receives only `IBoardClock`, never the options.

### Pitfall 8: Deploy-time cache and stale copies
The body changes for every subscriber, so every ETag changes and every client refetches; Google may take up to a day (CONTEXT D-09). Do not add copy that promises a refresh time.

### Pitfall 9: TZID characters
IANA ids contain only letters, digits, `/`, `_`, `-`, `+`. A Windows id can contain spaces, dots and parentheses. RFC param values must be DQUOTE-quoted if they contain `;`, `:` or `,`. Defence-in-depth: quote the parameter value when it contains any of those, and route the `TZID:` text line and `X-WR-TIMEZONE` through `EscapeText`. The source of the value is operator configuration filtered through `FindSystemTimeZoneById`, so this is low risk.

## Code Examples

### Existing writer facts this phase edits (read this session)

`QuestBoard.Domain/Services/CalendarFeedWriter.cs` [VERIFIED: read lines 44-102 this session]:

```csharp
AppendFoldedLine(builder, "X-WR-CALNAME:" + EscapeText(calendarName));   // line 50
...
AppendFoldedLine(builder, "DTSTART:" + FormatBasicDateTime(start));      // line 78
AppendFoldedLine(builder, "DTEND:" + FormatBasicDateTime(end));          // line 79
AppendFoldedLine(builder, "SEQUENCE:0");                                  // lines 82 and 100
```

`ICalendarFeedWriter` [VERIFIED: `ICalendarFeedWriter.cs:12`]: `string Write(IReadOnlyList<CalendarFeedEntry> entries, string calendarName);`
`IBoardClock` [VERIFIED: `IBoardClock.cs:7-15`]: `TimeZoneInfo TimeZone { get; }`, `bool IsDegraded { get; }`, `DateOnly Today { get; }`, `DateTime Now { get; }`
`TimeZoneOptions` default [VERIFIED: `TimeZoneOptions.cs:9`]: `public string BoardTimeZoneId { get; set; } = "Europe/Amsterdam";`
`BoardClock` fallback [VERIFIED: `BoardClock.cs:27`]: `return (TimeZoneInfo.Utc, true);`

### Target writer shape (prototype-verified algorithm, adapted to the writer's style)

```csharp
// Source: prototype run on Windows and Linux (identical SHA-256); RFC 5545 §3.6.5 DTSTART-only example.
// Signature: string Write(IReadOnlyList<CalendarFeedEntry> entries, string calendarName, TimeZoneInfo boardZone);

private static string ResolveTzid(TimeZoneInfo zone)
{
    var id = zone.Id;
    if (id.Contains('/') || id == "UTC") return id;
    return TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana) ? iana : id;
}

// Wall-clock span of the timed entries. Both bounds are the same digits the DTSTART/DTEND
// lines carry, so the block covers exactly what the document references.
private static void AppendTimeZone(StringBuilder builder, TimeZoneInfo zone, string tzid,
    IReadOnlyList<CalendarFeedEntry> entries)
{
    var timed = entries.Where(e => e.StartTime.HasValue)
        .Select(e => (Start: e.Date.ToDateTime(e.StartTime!.Value), e.Duration)).ToList();
    if (timed.Count == 0) return;

    var windowStart = DateTime.SpecifyKind(timed.Min(t => t.Start), DateTimeKind.Utc).AddDays(-1);
    var windowEnd = DateTime.SpecifyKind(timed.Max(t => t.Start + t.Duration), DateTimeKind.Utc).AddDays(1);

    AppendFoldedLine(builder, "BEGIN:VTIMEZONE");
    AppendFoldedLine(builder, "TZID:" + EscapeText(tzid));

    var previous = zone.GetUtcOffset(windowStart);
    // Leading observance: the offset in effect at the start of the window, anchored far in the
    // past so it precedes every entry. FROM equals TO because nothing changes at that onset.
    AppendObservance(builder, zone.IsDaylightSavingTime(windowStart), "19700101T000000", previous, previous);

    var step = TimeSpan.FromDays(1);
    for (var t = windowStart; t < windowEnd;)
    {
        var next = t + step < windowEnd ? t + step : windowEnd;
        if (zone.GetUtcOffset(next) != previous)
        {
            long lo = 0, hi = (long)(next - t).TotalMinutes;          // lo: still 'previous'; hi: changed
            while (hi - lo > 1)
            {
                var mid = (lo + hi) / 2;
                if (zone.GetUtcOffset(t.AddMinutes(mid)) == previous) lo = mid; else hi = mid;
            }
            var instant = t.AddMinutes(hi);                           // first whole minute on the new offset
            var after = zone.GetUtcOffset(instant);
            // DTSTART is the onset expressed in the PRIOR offset's local time (RFC: combined with TZOFFSETFROM).
            AppendObservance(builder, zone.IsDaylightSavingTime(instant),
                FormatBasicDateTime(instant + previous), previous, after);
            previous = after;
        }
        t = next;
    }
    AppendFoldedLine(builder, "END:VTIMEZONE");
}

private static void AppendObservance(StringBuilder b, bool daylight, string dtstartLocal, TimeSpan from, TimeSpan to)
{
    var kind = daylight ? "DAYLIGHT" : "STANDARD";
    AppendFoldedLine(b, "BEGIN:" + kind);
    AppendFoldedLine(b, "DTSTART:" + dtstartLocal);
    AppendFoldedLine(b, "TZOFFSETFROM:" + FormatUtcOffset(from));
    AppendFoldedLine(b, "TZOFFSETTO:" + FormatUtcOffset(to));
    AppendFoldedLine(b, "END:" + kind);
}

// RFC 5545 utc-offset: sign always present, hhmm, plus ss only when non-zero; "-0000" is not allowed
// (zero is written "+0000").
private static string FormatUtcOffset(TimeSpan o)
{
    var a = o.Duration();
    var text = $"{(o < TimeSpan.Zero ? "-" : "+")}{a.Hours:00}{a.Minutes:00}";
    return a.Seconds != 0 ? text + $"{a.Seconds:00}" : text;
}
```

Timed event lines: `"DTSTART;TZID=" + tzid + ":" + FormatBasicDateTime(start)` and the same for `DTEND` (both unchanged digits); `SEQUENCE:1` in both branches; `AppendCalendarHeaders` gains `AppendFoldedLine(builder, "X-WR-TIMEZONE:" + EscapeText(tzid));` directly after `X-WR-CALNAME`. Insert `AppendTimeZone(...)` between `AppendCalendarHeaders(...)` and the entry loop in `Write`.

Note the writer code must contain no `DateTime.Now`/`UtcNow`/`Today` (it never did) and, per project rule, no planning ids in comments.

### Expected bytes (from the prototype) for the two-sides-of-the-change case (entries 2 Oct 18:00 and 30 Oct 22:00 end, Amsterdam)

```
BEGIN:VTIMEZONE
TZID:Europe/Amsterdam
BEGIN:DAYLIGHT
DTSTART:19700101T000000
TZOFFSETFROM:+0200
TZOFFSETTO:+0200
END:DAYLIGHT
BEGIN:STANDARD
DTSTART:20261025T030000
TZOFFSETFROM:+0200
TZOFFSETTO:+0100
END:STANDARD
END:VTIMEZONE
```
(each line CRLF-terminated). A span running to 31 Mar 2027 adds `BEGIN:DAYLIGHT / DTSTART:20270328T020000 / TZOFFSETFROM:+0100 / TZOFFSETTO:+0200`. UTC yields one `STANDARD` observance `19700101T000000`, `+0000`, `+0000`. These match the familiar real-world shape (Standard onset 03:00 with FROM +0200, Daylight onset 02:00 with FROM +0100).

### Service wiring

```csharp
internal class CalendarSubscriptionService(
    /* existing parameters unchanged */
    ICalendarFeedWriter writer,
    IBoardClock boardClock,          // NEW: singleton into scoped service is fine
    TimeProvider timeProvider,
    ...)
// ...
var body = writer.Write(allEntries, "D&D Quest Board", boardClock.TimeZone);
```
Keep `TimeProvider` (still used for the fetch window and timestamps). Do not touch `today`/`windowStart` on lines 78-80.

## Code Map: what pins the old contract

### Assertions that will fail or must be rewritten [VERIFIED: files read this session]

`QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs`:
- Lines 104-107 `Write_TimedEntry_EmitsStartAndEndOneHourApart`: `Contain("DTSTART:20260920T190000")`, `Contain("DTEND:20260920T200000")`, `NotContain("DTSTART:20260920T190000Z")`, `NotContain("TZID")`.
- Lines 117-118 `Write_TimedEntryLateStart_RollsDateForwardForEnd`: `Contain("DTSTART:20260920T233000")`, `Contain("DTEND:20260921T003000")`.
- Lines 163-171 `Write_AnyEntry_EmitsSequenceZero`: `body.Split("SEQUENCE:0").Length.Should().Be(3)` -> rename to `...EmitsSequenceOne`, `SEQUENCE:1`, plus `NotContain("SEQUENCE:0")`.
- Lines 268-281 `Write_Document_NeverEmitsForbiddenProperties`: delete only the `NotContain("VTIMEZONE")` (line 276) and `NotContain("TZID")` (line 277) lines; keep `VALARM`, `STATUS:CANCELLED`, `DESCRIPTION`, `URL`. Check: none of the new lines contains the substring `URL` or `DESCRIPTION`.
- Lines 483-484, 498-499, 515-516 (interpolated `$"DTSTART:{startInstant:yyyyMMdd}T..."` and `DTEND:`) and line 576 (`Contain("DTSTART:20260920T190000")`): prefix changes to `DTSTART;TZID=Europe/Amsterdam:`. Lines 574-575 (`NotContain("DTSTART;VALUE=DATE:")`) stay valid.
- All 33 `Writer.Write(...)` call sites need the new argument (mechanical; a private static helper or a shared `AmsterdamZone` field keeps the diff small).
- Unchanged and still valid: lines 128-129 (`DTSTART;VALUE=DATE:`), 143-146 (all-day parse), 174-182 DTSTAMP, 184-195 determinism, 300-303 CRLF (the new lines go through `AppendFoldedLine`), 366-368 headers, 385-398 headers-once (add X-WR-TIMEZONE once).

`QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs` (rewrite, keep the file):
- Lines 36-49: exact `DTSTART:20260920T190000\r\n` plus `NotContain("TZID")` / `NotContain("VTIMEZONE")` -> pin `DTSTART;TZID=Europe/Amsterdam:20260920T190000\r\n`, no `T190000Z`, `BEGIN:VTIMEZONE`, `TZID:Europe/Amsterdam`.
- Lines 51-69 ("UnaffectedByANonDefaultTimeZoneHeldInScope", built on `FindSystemTimeZoneById("Pacific/Auckland")`): its reason ("the writer takes DateOnly/TimeOnly, so there is no instant in scope") is gone. Rewrite as the never-converts pin: writer given Auckland still writes `190000` digits and declares `TZID=Pacific/Auckland` and an Auckland `VTIMEZONE`.
- Lines 88-169 (service fact, comment says "CalendarSubscriptionService takes a plain TimeProvider, not IBoardClock, so there is no seam"): now false. Pass the Auckland `FakeBoardClock` into the service, keep asserting `Date == 2026-09-20` and `StartTime == 19:00`, and additionally capture the zone argument (`writer.Write(Arg.Do<...>, Arg.Any<string>(), Arg.Do<TimeZoneInfo>(z => capturedZone = z))`) and assert it is that clock's zone. Add a second fact with `FakeBoardClock { TimeZone = TimeZoneInfo.Utc, IsDegraded = true }` asserting the writer receives UTC.
- Line 150-158: `new CalendarSubscriptionService(...)` gains the `IBoardClock` argument (position: after `writer`).
- Class comment (lines 11-16) and the substitute setup on line 147 (`Arg.Any<string>()` needs a third matcher).

`QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs`: constructs the service at lines 113-121 with a real `CalendarFeedWriter`; only the constructor gains `new FakeBoardClock()`. It has no `DTSTART`/`SEQUENCE`/`TZID` assertions (grep: zero matches).

`QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs`: line 190 `Contain($"DTSTART:{eventDate:yyyyMMdd}T190000")` and line 191 `NotContain($"...T190000Z")`. Line 734 (`DTSTART;VALUE=DATE:`) is unchanged.
`QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`: line 258 `Contain($"DTSTART:{finalizedDate:...}")`, line 259 `NotContain(...Z)`, line 264 `Contain($"DTEND:{expectedEnd:...}")`, line 675 `Contain($"DTSTART:{newDate:...}")`, and line 271 (`NotContain("DTSTART;VALUE=DATE:")`) stays valid. Line 675 is easy to miss: it is inside the moved-date UID test, not an obvious "floating" assertion.

Integration-test recipe already in the repo: `WallClockUnmovedTests.CreateZoneVariantFactory(zoneId)` builds a variant host with `["TimeZone:BoardTimeZoneId"] = zoneId` [VERIFIED: `WallClockUnmovedTests.cs:22-34`]; `BoardTimeZoneHealthCheckTests` uses `"Definitely/NotAZone"` for the degraded host [VERIFIED: grep `BoardTimeZoneHealthCheckTests.cs:34`].

### Stale statements to rewrite (not delete)
- `.claude/architecture.md:46-47`: "The calendar feed emits floating local `DTSTART` with no `TZID`/`VTIMEZONE`. `CalendarFeedWriter.cs` and `CalendarSubscriptionService.cs` are guarded by tests that pin this -- treat changes there as high-risk." Rewrite to the zoned contract. Keep line 37's vocabulary ("A wall-clock value -- a floating local time with no UTC instant"): it describes the stored value, which stays true. Only the feed sentence is stale.
- `CalendarFeedWriter.cs` lines 9-12 (class comment: "a five-field VEVENT with no timezone and no recurrence"), 66-69 (timed-branch comment), 149-150 (`FormatBasicDateTime` comment).
- `CalendarSubscriptionService.cs` lines 114-119 (comment calls `FinalizedDate` "stored in server local time (a standing, separately tracked known issue this phase does not touch)"; predates the wall-clock classification). Reword without ids; do not change the code beneath it.
- `.planning/REQUIREMENTS.md:111` `CALFEED-10`: "…one-hour entry in floating local time with no timezone declared…" is superseded by `CALTZ-01`; amend or annotate it.
- `CalendarFeedOptions.cs`, `TimeZoneOptions.cs`: no change required (no new knob).

### DI registration [VERIFIED: `QuestBoard.Domain/Extensions/ServiceExtensions.cs:62, 70, 73`]
`services.AddScoped<ICalendarSubscriptionService, CalendarSubscriptionService>();` / `services.AddSingleton<ICalendarFeedWriter, CalendarFeedWriter>();` / `services.TryAddSingleton<IBoardClock, BoardClock>();`. Constructor injection of `IBoardClock` into the scoped service needs no registration change.

### AmbientClockSeamTests [VERIFIED: read lines 15-75 this session]
`GuardedRelativePaths` (lines 15-37) and `BoardClockConsumerPaths` (lines 62-75) list neither `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` nor `CalendarFeedWriter.cs`, and no theory scans the tree. Adding `IBoardClock` to the service therefore cannot fail or weaken it. Optional strengthening: add the service's path to both lists (its source contains no `DateTime.Today`/`Now`/`UtcNow` tokens; `.UtcDateTime` does not match `DateTime.UtcNow`), so a future ambient read there is caught and the positive `IBoardClock` mention is asserted. That is an addition, not a weakening.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Feed emits floating local `DTSTART` | `DTSTART;TZID=<zone>` + generated `VTIMEZONE` + `X-WR-TIMEZONE` | This phase | Google (which reads floating as UTC per the operator's evidence) gets an explicit zone; Apple keeps rendering the same hour. |
| `SEQUENCE:0` constant | `SEQUENCE:1` constant | This phase | Nudges revision-comparing clients to apply the changed `DTSTART`. One-way (never lower again). |

**Deprecated/outdated:** the "floating feed" contract and its guard tests (Phase 84 D-01), to be rewritten.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Google Calendar resolves an IANA `TZID` by name and may ignore the `VTIMEZONE` body (third-party blogs only; no primary Google documentation found) | Summary; Pitfall 1 | Low: body is also emitted and correct, so either reading gives 18:00. If Google trusts only the body and rejects a fixed-date one, times could still be wrong; D-09 catches it. |
| A2 | Google updates an already-held entry when only `DTSTART`/`DTEND` change under the same `UID` | Summary; D-08 | Medium: if not, the friend's existing entries stay wrong until re-subscribe (D-08 already accepts this). |
| A3 | `SEQUENCE` influences Google's update decision (the only source is a promotional blog, "no evidence cited") | Summary | Low: the bump is one literal and RFC 5546 supports it for iTIP-ordering clients. |
| A4 | Apple Calendar and Google accept a `VTIMEZONE` of fixed-date observances with no `RRULE` | Summary | Medium: valid per RFC 5545 (verified), but no vendor confirmation. Mitigation: D-09 iPhone and Google checks. |
| A5 | `X-WR-TIMEZONE` naming the same zone as `TZID` does not double-apply an offset in Google or Apple. Google's own exports are believed to carry both together (training knowledge, not verified this session) | Summary | Medium-low: only one Apple forum post (`TZID=America/Los_Angeles` with a `VTIMEZONE`, no `X-WR-TIMEZONE` stated) reports a double-subtraction on iOS import, unresolved; D-04 was the operator's explicit choice and D-09 tests it. Cheap fallback: drop the header (one line). |
| A6 | Clients accept a leading observance with `TZOFFSETFROM = TZOFFSETTO` dated `19700101T000000` and no `TZNAME` | Code Examples | Low-medium: RFC-legal (TZNAME optional, verified), common practice for single-offset zones; not vendor-verified. |
| A7 | The bare id `UTC` is recognised by Google/Apple (alternative `Etc/UTC`) | Pitfall 1 | Low: only matters while degraded (D-06) |
| A8 | `TryConvertWindowsIdToIanaId` behaves the same under invariant-globalization mode on Linux (only reachable for a Windows-style configured id; default `Europe/Amsterdam` never hits it) | Pitfall 1 | Low: falls back to the raw id |
| A9 | Google refresh latency "up to a day" (CONTEXT's figure; community threads say sync "within 12 hours" is not reliably met) | Pitfall 8 | None for code; copy must not promise it |

## Open Questions

1. **Does Google refresh an already-synced entry after the encoding change?**
   - What we know: UID unchanged, `DTSTAMP` unchanged, `SEQUENCE` 0 to 1, `DTSTART` semantics change (floating read as UTC becomes a zoned instant). RFC 5546 orders by SEQUENCE then DTSTAMP for iTIP clients.
   - What's unclear: whether Google's subscribed-calendar sync applies it (it is not an iTIP flow).
   - Recommendation: plan the D-09 sequence (already-synced entry first) and accept D-08 as the documented fallback. No code change needed either way.
2. **Rename `CalendarFeedFloatingTimeGuardTests`?** Its name now describes the reversed contract. Renaming (git mv plus class rename) is cleaner; keeping preserves history. Planner's call; every command below uses `FullyQualifiedName~CalendarFeed` so it works with either name.
3. **Add `CalendarSubscriptionService.cs` to the `AmbientClockSeamTests` lists?** Optional strengthening described above.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build and tests | ✓ | 10.0.400 [VERIFIED: `dotnet --version`] | — |
| Local dotnet tests (unit and integration) | validation | ✓ | 87 unit and 51 integration calendar tests passed this session | — |
| Docker Desktop with `mcr.microsoft.com/dotnet/sdk:10.0` (already pulled) | optional Linux cross-check of the byte pins | ✓ | Docker 29.7.2 | Skip; the Windows result is already known equal to Linux for the prototype |
| SQL Server localhost:1433 | not needed (integration tests use EF InMemory) | n/a | — | — |
| Production, a Google account on a phone, an iPhone | D-09 manual checks | operator-provided | — | none; manual |

**Missing dependencies with no fallback:** none for the code work. D-09 requires the operator and the friend's phone.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 on `net10.0`, FluentAssertions 8.10.0, NSubstitute 5.3.0 (unit) [VERIFIED: csproj] |
| Config file | none beyond the csproj files |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeed\|FullyQualifiedName~CalendarSubscriptionQuestRecheck\|FullyQualifiedName~AmbientClockSeamTests"` (a near-identical run: 87 tests, 293 ms test time, about 19 s including build) |
| Full suite command | `dotnet test` |

Integration filter (verified this session, 51 tests, about 6 s): `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CalendarSubscriptionFeedTests|FullyQualifiedName~CalendarSubscriptionQuestFeedTests|FullyQualifiedName~BoardTimeZoneHealthCheckTests"`. If `dotnet build` fails on locked files, ask the user to stop the debugger.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CALTZ-01 | `DTSTART;TZID=Europe/Amsterdam:20260920T190000\r\n` and `DTEND;TZID=...:20260920T200000`, no `T190000Z` (event and quest) | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"` | rewrite existing |
| CALTZ-01 | Late start still rolls the end date with TZID, digits unchanged | unit | same filter | rewrite existing |
| CALTZ-02 | Two-sides-of-25-October-2026 entries: exact `VTIMEZONE` bytes (leading DAYLIGHT `+0200/+0200` at `19700101T000000`, STANDARD `20261025T030000` `+0200` to `+0100`); both `DTSTART` values keep `180000`; block sits after headers and before the first `BEGIN:VEVENT`; exactly one block | unit | `... --filter "FullyQualifiedName~CalendarFeed"` | new (guard class) |
| CALTZ-02 | Span reaching March 2027 adds the `20270328T020000` DAYLIGHT observance; a summer-only span emits just the leading observance | unit | same | new |
| CALTZ-02 | No `RRULE` anywhere in the document | unit | same | new |
| CALTZ-03 | One `X-WR-TIMEZONE` header, before the first `VEVENT`, equal to the `TZID` of the block and of every timed line | unit | same | new |
| CALTZ-04 | `Pacific/Auckland` (southern hemisphere) declared and its transitions listed; a Windows-style id (`W. Europe Standard Time`) yields an IANA-form `TZID` with no space, same id in `VTIMEZONE`/`X-WR-TIMEZONE` | unit | same | new |
| CALTZ-04 | Integration: variant host with `TimeZone:BoardTimeZoneId=Pacific/Auckland` serves `TZID=Pacific/Auckland` | integration | integration filter above | new |
| CALTZ-05 | `TimeZoneInfo.Utc`: `TZID=UTC`, single `STANDARD` `+0000/+0000`, `X-WR-TIMEZONE:UTC`; service passes the degraded clock's UTC to the writer | unit | `--filter "FullyQualifiedName~CalendarFeedFloatingTimeGuardTests"` (or its new name) | new |
| CALTZ-05 | Integration: host with `Definitely/NotAZone` serves `TZID=UTC` (declared zone equals resolved zone, not configured string) | integration | integration filter above | new |
| CALTZ-06 | `SEQUENCE:1` on timed and all-day entries; none `SEQUENCE:0`; UID line and DTSTAMP identical to before | unit | `...CalendarFeedWriterTests` | rewrite existing |
| CALTZ-07 | All-day-only feed: no `TZID` on `VALUE=DATE` lines, no `VTIMEZONE`; mixed feed: `TZID` only on timed lines, `DTSTAMP` keeps `Z` and no `TZID` | unit | same | new |
| CALTZ-08 | Service test: `FakeBoardClock` with Auckland zone, `FinalizedDate` 19:00 -> entry `Date`/`StartTime` unchanged | unit | guard class filter | rewrite existing |
| CALTZ-08 | Integration: `DTSTART;TZID=...` carries the seeded local digits (event `190000`, quest `finalizedDate:HHmmss`) | integration | integration filter | rewrite existing |
| CALTZ-10 | `AmbientClockSeamTests` still green; every physical line ≤ 75 octets and CRLF-only (existing facts cover the new lines) | unit | `--filter "FullyQualifiedName~AmbientClockSeamTests"` and writer tests | exists |
| CALTZ-09 | Google (friend's phone) and Apple (operator's iPhone) per D-09 | manual | see below | n/a |

### Sampling Rate
- **Per task commit:** the quick unit command above.
- **Per wave merge:** unit quick command plus the integration filter above.
- **Phase gate:** `dotnet test` (full suite) green before `/gsd-verify-work`, then the manual D-09 checks on production.

### Wave 0 Gaps
- [ ] None for infrastructure: framework, `FakeBoardClock` (`TimeZone`, `IsDegraded`, `Today`, `Now` settable, default `TimeZoneInfo.Utc`), zone-variant factory pattern and the `Pacific/Auckland` id (already used in a test on Windows) all exist. Add a shared `Amsterdam` zone field in the two test classes that construct the writer.
- [ ] Optional cross-platform proof: run the same unit filter inside the `sdk:10.0` container against the repo (read-only mount) so the exact-byte `VTIMEZONE` facts are executed on Linux, not only Windows. The prototype already showed equality; this proves the shipped test.

### Manual-only (cannot be automated), per D-09 (production)
1. Before deploy, note one entry the friend's Google Calendar already holds (for example the 2 Oct 2026 quest 12039, currently `20:00`). After deploy and after Google's next refresh, record whether it now reads 18:00. If not, fall back to D-08 (remove and re-add the subscription once) and record it; that is not a phase failure.
2. After the correction, create or find a new entry and confirm it reads 18:00 on the friend's phone.
3. On the operator's iPhone (Apple Calendar), confirm the same event and quest still read 18:00 (regression guard for the route that already works).
4. Optional: `curl` the live feed and eyeball `DTSTART;TZID=Europe/Amsterdam:20261002T180000`, `SEQUENCE:1` for quest 12039 (per CONTEXT Specifics).
5. Record observed refresh latency; do not put any latency into copy.

## Security Domain

`security_enforcement` is not disabled in `.planning/config.json` (key absent), so it applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (feed token auth unchanged) | existing 256-bit token, unchanged |
| V3 Session Management | no | anonymous endpoint, unchanged |
| V4 Access Control | no (membership predicate and re-check unchanged) | unchanged |
| V5 Input Validation / output encoding | yes (new text lines built from a zone id) | `EscapeText` for `TZID:` and `X-WR-TIMEZONE:` values, DQUOTE-quote the `TZID=` parameter when it contains `;` `:` `,`; ids only come from `FindSystemTimeZoneById`, so not user-controlled |
| V6 Cryptography | no | none added; ETag hashing unchanged |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Content-line / property injection via a zone id containing CR/LF or `;` `:` | Tampering | Value is operator configuration filtered through the OS zone lookup; still escape/quote as above and keep it out of `/health` |
| Configuration disclosure via the feed | Information disclosure | Zone id is not secret and is only visible to a token holder; `/health` keeps its fixed copy (the health check must not interpolate the zone id; `BoardTimeZoneHealthCheck` already does not) |
| Declaring a wrong zone silently | Repudiation / integrity | Derive only from `IBoardClock.TimeZone`; test with `Definitely/NotAZone` (declares UTC) |

## Sources

### Primary (HIGH confidence)
- RFC 5545, downloaded this session (`https://www.rfc-editor.org/rfc/rfc5545.txt`): §3.6.5 VTIMEZONE grammar and DTSTART-only example, §3.3.5 Form #3 and gap/overlap rule, §3.2.19 TZID, §3.8.7.4 SEQUENCE. Quotes used: "An individual "VTIMEZONE" calendar component MUST be specified for each unique "TZID" parameter value specified in the iCalendar object."; "The "STANDARD" or "DAYLIGHT" sub-component MUST include the "DTSTART", "TZOFFSETFROM", and "TZOFFSETTO" properties."; "The offset to apply at any given time is found by locating the observance that has the last onset date and time before the time in question, and using the offset value from that observance."; "If the local time described does not occur (when changing from standard to daylight time), the DATE-TIME value is interpreted using the UTC offset before the gap in local times."; "The use of local time in a DATE-TIME value without the "TZID" property parameter is to be interpreted as floating time, regardless of the existence of "VTIMEZONE" calendar components in the iCalendar object."; the TZID MUST NOT apply to DATE or UTC-valued properties (icalendar.org §3.2.19 mirror).
- RFC 5546 §2.1.5 (downloaded this session): rule 2 "the component with the highest numeric value for the "SEQUENCE" property obsoletes all other revisions"; rule 4 "In situations where the "UID", "RECURRENCE-ID", and "SEQUENCE" property values match, the "DTSTAMP" property is used as the tie-breaker."
- Empirical probes run this session on Windows 11 (.NET 10.0.400) and in `mcr.microsoft.com/dotnet/sdk:10.0` (Ubuntu 24.04): adjustment-rule counts (Amsterdam 1 on Windows, 147 on Linux; Auckland 3 vs 175; Lord Howe 4 vs 121), `StandardName` divergence, `FindSystemTimeZoneById` id behaviour, `TryConvertWindowsIdToIanaId` results (`W. Europe Standard Time` -> `Europe/Berlin`, `UTC` -> `Etc/UTC`), identical offset transitions (`2026-10-25T01:00:00Z` +02:00 to +01:00 and `2027-03-28T01:00:00Z` +01:00 to +02:00), identical prototype `VTIMEZONE` SHA-256 across six zones, identical labels for `Europe/Dublin` and `Africa/Casablanca`, probe cost about 0.04 ms (Windows Debug) to 0.08 ms (Linux Release) per feed for 460 probes.
- Repository files read this session (paths and line ranges cited inline).

### Secondary (MEDIUM confidence)
- `https://icalendar.org/iCalendar-RFC-5545/3-6-5-time-zone-component.html` and `.../3-2-19-time-zone-identifier.html` (mirrors of the RFC text, consistent with the downloaded RFC).

### Tertiary (LOW confidence, marked for on-device validation)
- `https://synara.events/articles/ics-timezone-wrong-in-google-calendar-why-events-shift-and-how-to-fix-it` and the Product Hunt post "Why Google Calendar Sometimes Ignores ICS Updates": vendor-blog claims that Google uses IANA names, may ignore an untrusted `VTIMEZONE`, and applies updates only when SEQUENCE is higher; the fetches showed no cited tests or Google documentation and one is promotional.
- `https://github.com/u01jmg3/ics-parser/issues/245`: a PHP parser (not a calendar client) prefers `X-WR-TIMEZONE` over `VTIMEZONE` for UTC-`Z` times; not our case.
- `https://discussions.apple.com/thread/255684115`: one unresolved user report of iOS showing times 7-8 hours early for an ICS with `TZID=America/Los_Angeles` and a `VTIMEZONE`; anecdotal.
- `https://pypi.org/project/x-wr-timezone/` (fetched page failed to load; the search snippet says "Strict interpretations according to RFC 5545 ignore the X-WR-TIMEZONE parameter" and that Google introduced it).
- Google Calendar Community threads on refresh latency (titles only; content did not load).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new dependency; BCL and repo facts read and run this session.
- Architecture (writer/service/test changes): HIGH, every file and assertion read.
- Algorithm and cross-platform determinism: HIGH, prototype run on both platforms with matching hashes.
- RFC conformance of the block: MEDIUM-HIGH, derived from the RFC text and its own DTSTART-only example.
- Client behaviour (Google update semantics, TZID resolution, X-WR-TIMEZONE interplay): LOW, thin third-party evidence only; D-09 is the real test and D-08 the fallback.
- Pitfalls: HIGH for the .NET and test-churn items, LOW for client-side ones.

**Research date:** 2026-09-30
**Valid until:** 2026-10-30 for the code map and .NET facts (the phase should land before the 25 October 2026 clock change); client-behaviour findings are stable-but-unverified until the D-09 checks run.
