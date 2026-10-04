---
phase: "88"
slug: "calendar-feed-times-anchored-to-the-board-timezone"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-09-30"
---

# Phase 88 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

The calendar feed stopped writing floating local times and now declares the board's zone: `TZID` on
every timed line, a generated `VTIMEZONE`, and an `X-WR-TIMEZONE` header, all derived from
`IBoardClock.TimeZone`. The endpoint, its bearer-address credential and its scoping rules are
unchanged. Only the document body changed. That body is parsed by third-party calendar servers
(Google) and by devices, so a malformed or wrongly-zoned line fails silently on the server side. The
risk is concentrated in two places: the text a zone id becomes, and whether the stored wall-clock
digits reach the document unconverted.

A gap-closure round (plans 88-05..88-09, shipped as v5.3.3) added a stored per-entry revision: a
migration adds `FeedRevision`/`FeedRevisedAt`, a change-tracker stamper raises it, and the writer
publishes it as `SEQUENCE`/`DTSTAMP`/`LAST-MODIFIED`. That round registered T-88-09..T-88-22, audited
below against HEAD `f08f62be`. The new risk is concentrated in one invariant: a published `SEQUENCE`
must never go down.

Register authored at plan time across the four `*-PLAN.md` `<threat_model>` blocks. It was verified
by the security auditor against HEAD `f445f6d3`, which includes the code-review fixes `b43a4f74`
(feed window measured from the board clock's date) and `e98ad91d` (DTSTAMP labelled as UTC instead
of converted through the host zone).

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| operator configuration → board clock | `TimeZone:BoardTimeZoneId` is operator-supplied (IANA, Windows-style or unusual). `BoardClock` filters it through the operating system's zone lookup and falls back to UTC | configuration string (low) |
| board clock → feed document | the resolved zone id becomes text in a document read by third-party calendar servers and devices; an unresolvable configured id must never surface there | zone id text (low) |
| feed document → third-party calendar servers | the pinned bytes are what Google's servers and Apple devices parse; a regression is silent on the server | event titles, board names, wall-clock times (member-private) |
| anonymous calendar client → feed endpoint | unchanged: the 256-bit address is the only credential; the phase changes the document body only | bearer address (high) |
| operator → production feed | the production check needs a subscription address, which is a bearer credential | bearer address (high) |
| repository → Linux container | the committed tree is copied into a throwaway SDK container; nothing is mounted from the host | source tree (low) |
| future code change → feed seam | source-level guards keep a later edit from re-introducing a read of the configured string or a host-zone conversion | — |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-88-01 | Tampering | `CalendarFeedWriter`: zone id rendered into content lines | low | mitigate | The zone arrives only as a resolved `TimeZoneInfo` (`ICalendarFeedWriter.cs:18`; sole caller `CalendarSubscriptionService.cs:168`; `BoardClock` gives an OS-lookup result or `TimeZoneInfo.Utc`). `FormatTzidParameter` wraps values containing `;:,` in double quotes (`CalendarFeedWriter.cs:98-99`). The `TZID:` and `X-WR-TIMEZONE:` text goes through `EscapeText`, which flattens CR/LF. Every zone-bearing line is folded. Pinned by `Write_ZoneIdCarryingReservedCharacters_QuotesTheParameterAndEscapesTheText` (`CalendarFeedWriterTests.cs:915-926`) | closed |
| T-88-02 | Repudiation | `CalendarSubscriptionService`: declared zone vs resolved zone | medium | mitigate | The service takes `IBoardClock` and only `IOptions<CalendarFeedOptions>`, and the writer derives one tzid (`CalendarFeedWriter.cs:26`). Guard facts assert the writer receives the clock's own instance and that a degraded clock declares UTC (`CalendarFeedBoardZoneGuardTests.cs:194-268`). A live fact checks that `Definitely/NotAZone` declares UTC (`CalendarFeedBoardZoneHttpTests.cs:228-247`). A source fact forbids `TimeZoneOptions`/`BoardTimeZoneId`/`FindSystemTimeZoneById` in both feed files (`AmbientClockSeamTests.cs:298-327`) | closed |
| T-88-03 | Tampering | stored wall-clock value → emitted digits | medium | mitigate | `start = entry.Date.ToDateTime(entry.StartTime!.Value)` with no conversion (`CalendarFeedWriter.cs:203-204`); no conversion or host-zone API in the writer or service. Guarded at source by `CalendarFeedSources_NeverConvertThroughTheHostsLocalZone` (`AmbientClockSeamTests.cs:355-370`). Unit facts cover Amsterdam, Auckland, the spring gap and autumn overlap, a Windows id and UTC; live facts cover Auckland, UTC and Berlin with `T190000` digits unchanged (`CalendarFeedBoardZoneHttpTests.cs:212-261`) | closed |
| T-88-04 | Denial of service | `AppendTimeZone` probing cost | low | accept | See AR-88-01 | closed |
| T-88-05 | Tampering | subscriber-held entries after a lowered sequence | low | mitigate | `SEQUENCE:1` on both writer branches (`CalendarFeedWriter.cs:219`, `:237`); no `SEQUENCE:0` in source. Pinned by unit facts, including whole-block byte pins (`CalendarFeedWriterTests.cs:164-219`), and by integration facts on both fetches of a rescheduled quest | closed |
| T-88-06 | Information disclosure | configured-but-unresolvable zone id in the feed body | low | mitigate | The degraded body is asserted never to contain `Definitely/NotAZone` (`CalendarFeedBoardZoneHttpTests.cs:246`); the health endpoint's non-disclosure fact is unchanged (`BoardTimeZoneHealthCheckTests.cs:57-68`) | closed |
| T-88-07 | Information disclosure | subscription address used in the production check | medium | mitigate | The operator's permanent address was never exposed. For the production check the operator created a dedicated temporary subscription and shared that address in the working session only. It appears in no tracked file, commit or working-tree file (`88-UAT.md` records `{temporary token}`). Deviation: the plan's literal instruction ("never paste the address into … chat") was not followed for the temporary address. The protection is its revocation, which returns `410 Gone` (`CalendarSubscriptionService.cs:47-48`, `CalendarFeedController.cs:59-62`). The operator revoked it after the check; a GET at 2026-09-30T14:02Z returned **410** | closed |
| T-88-08 | Tampering | Linux container run | low | accept | See AR-88-02 | closed |
| T-88-SC | Tampering | package-manager installs | n/a | accept | See AR-88-03 | closed |
| T-88-09 | Tampering | FeedRevision / FeedRevisedAt via mass assignment | medium | mitigate | Both domain-to-entity maps ignore the columns (`EntityProfile.cs:33-34`, `:169-170`), and the stamper restores the stored values before deciding a bump (`FeedRevisionStamper.cs:178-181`). No request-bound model carries them. The domain `Event`/`Quest` do, and they are embedded in four display view models, none of which is an action parameter. Tests: `FeedRevisionStamperTests.cs:367-463` | closed |
| T-88-10 | Denial of service | SEQUENCE regression locking subscribers out of every later change | high | mitigate | The only runtime write is stored + 1 (`FeedRevisionStamper.cs:175,180,196`). FeedRevision is a concurrency token (`QuestBoardContext.cs:123-124`), and a bounded retry adopts the stored revision and stamp (`:74-114`, `FeedRevisionStamper.cs:148-152`). The migration only increments, and the writer floors at 1 (`CalendarFeedWriter.cs:249-250`). Hand-traced: a stale copy at 2 against a stored 4 publishes 5, never 3. Tests: `FeedRevisionStamperTests.cs:495-627` plus the writer and integration pins | closed |
| T-88-11 | Tampering | writes bypassing the change tracker | medium | mitigate | `FeedRevisionWriteSeamTests` has an empty allowlist and bans bulk/raw SQL, `Attach`/`Update(`, `.Entry(`, `.TrackGraph(` and `.State =` assignment (`:15`, `:51-53`), with a self-check (`:177-189`). An independent grep of production source finds none | closed |
| T-88-12 | Information disclosure | DTSTAMP reveals when an entry was last edited | low | accept | See AR-88-04 | closed |
| T-88-13 | Tampering | migration bump on production data | medium | mitigate | `AddColumn` default 1 plus two set-based UPDATEs inside EF's migration transaction; `Down` drops all four columns (`AddFeedEntryRevisions.cs:14-69`). Proven on the local SQL Server (43/62 rows, 0 mismatches) and on production (41 untouched entries at SEQUENCE:2, stamped at the deploy migration) | closed |
| T-88-14 | Information disclosure | one reader's answer time leaking into another reader's document | medium | mitigate | The feed query is rooted at the owner's own `EventSignups` rows (`EventSignupRepository.cs:90-110`), and SEQUENCE comes from the event alone (`CalendarSubscriptionService.cs:119`). The two-reader fact shows B's document is byte-identical with a 304 (`CalendarFeedEventRevisionTests.cs:511-530`) | closed |
| T-88-15 | Denial of service | a per-reader signal resetting and locking a client out | high | mitigate | `LaterOf(FeedRevisedAt, AnswerWrittenAt)` (`CalendarSubscriptionService.cs:50`, `:119-120`). Both inputs only move forward. A withdraw deletes the signup row, which removes the entry from that reader's feed instead of falling back to an earlier stamp, and a re-created row stamps at or after anything published. SEQUENCE is never touched by answers. Tests: `CalendarFeedEventRevisionTests.cs:579-592`, `:507`, `:525`. Residual: `AnswerWrittenAt` uses ambient `DateTime.UtcNow` with no clamp. A backward host-clock step could publish an earlier per-reader stamp at the same SEQUENCE, affecting only that reader's availability marker; nothing is locked out | closed |
| T-88-16 | Tampering | a member forcing other members' calendars to refetch by voting | low | accept | See AR-88-05 | closed |
| T-88-17 | Tampering | quest Open, Finalize and Edit posts | low | accept | See AR-88-06 | closed |
| T-88-19 | Tampering | DTSTAMP and LAST-MODIFIED disagreeing | low | mitigate | Both lines are written from one computed string per branch (`CalendarFeedWriter.cs:211-213`, `:232-234`); pinned by `CalendarFeedWriterTests.cs:226-251` | closed |
| T-88-20 | Denial of service | a byte pin loosened so a regression to a constant SEQUENCE passes | medium | mitigate | Whole-block pins use SEQUENCE:4 and a revision time distinct from creation (`CalendarFeedWriterTests.cs:287-329`, `:166-177`), and the guard fixture's revision time differs from its creation time (`CalendarFeedBoardZoneGuardTests.cs:129-131`) | closed |
| T-88-21 | Tampering | local SQL Server schema change | low | mitigate | Only the one migration was applied, forward only, with read-only queries otherwise (`88-VALIDATION.md`); the server was never installed, started or torn down | closed |
| T-88-22 | Information disclosure | subscription addresses in the production re-test | medium | mitigate | No token column was read, and no token appears in any tracked file or any commit on any branch (`git log --all -G`). **Deviation:** the "never paste an address into chat" instruction failed twice. (1) Two temporary production addresses were pasted into the session chat; both are revoked and answer 410. (2) The operator's **permanent** iPhone subscription address was exposed in the session transcript: a directory listing of the operator's Downloads folder printed a saved feed file whose name is the token, and a later command carried a prefix of it. **Mitigation required:** the operator revokes that subscription and re-subscribes the iPhone with a fresh address, then deletes the downloaded file. Open until then | open — below high threshold (non-blocking) |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-88-01 | T-88-04 | The `VTIMEZONE` probe span runs from the earliest entry start to the latest end, padded one day each side, in one-day steps (`CalendarFeedWriter.cs:123-167`). Entries are limited to the feed window by the repositories, and the window comes from operator configuration: 3 months back and 12 ahead by default (`CalendarFeedOptions.cs:11,15`), about 460 probes, measured at 0.04–0.08 ms. No request input sets the span. `CalendarFeedOptions.IsValid` sets no upper limit, so the cost is bounded by a window the operator chooses, not by a hard cap; an operator who configures years pays for years. | plan-time register (88-01), re-verified by security auditor | 2026-09-30 |
| AR-88-02 | T-88-08 | The Linux check fed `git archive HEAD` on stdin to a `--rm` SDK container with no volume mount, so the container could neither read uncommitted host files nor write back. Commit `af8e1ff7` touches only `88-VALIDATION.md`, and no archive remains in the tree. | plan-time register (88-04), re-verified by security auditor | 2026-09-30 |
| AR-88-03 | T-88-SC | No install task in any plan. `git diff 79e9adb7..HEAD` over `*.csproj`, `*.props`, `*.targets`, `packages.lock.json`, `global.json` and `nuget.config` is empty. The container restored only packages the unit test project already references. | plan-time register (88-01..04), re-verified by security auditor | 2026-09-30 |

| AR-88-04 | T-88-12 | The feed is bearer-scoped to one member (`CalendarSubscriptionService.cs:55-64,88-89`). A quest's stamp is the time of the DM's last feed-visible change, and an event's stamp is at most the reader's own answer time. Both are information a board member can already infer from the board. | plan-time register (88-05), re-verified by security auditor | 2026-09-30 |
| AR-88-05 | T-88-16 | A vote writes only the voter's own signup row and never raises the event revision, so it can only churn the voter's own document. The two-reader integration fact proves other readers' documents stay byte-identical. Re-posting the same answer moves only the voter's own stamp. | plan-time register (88-06), re-verified by security auditor | 2026-09-30 |
| AR-88-06 | T-88-17 | `23b1aedd` touches only `CalendarFeedQuestRevisionTests.cs`, and no `QuestBoard.Service` file changed in `6908b7d4..HEAD`, so the quest controllers' ownership and role checks are unchanged. | plan-time register (88-07), re-verified by security auditor | 2026-09-30 |

AR-88-02 and AR-88-03 were re-verified for the gap-closure round. The second Linux run's commit `8f15847c` touches only `88-VALIDATION.md`, and no build or package file changed in `6908b7d4..HEAD`.

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-09-30 | 9 | 8 | 1 (T-88-07, non-blocking) | gsd-security-auditor (ASVS L1, block_on high), HEAD `f445f6d3`; unit filter 134/134, integration filter 55/55 |
| 2026-09-30 | 9 | 9 | 0 | orchestrator: T-88-07 closed after the operator revoked the temporary subscription (GET returned 410 at 14:02Z) |
| 2026-09-30 | 22 | 21 | 1 (T-88-22, non-blocking) | gsd-security-auditor, gap-closure round (T-88-09..22, plans 88-05..09, plus round-2 fixes `2dc50af0`/`4c722f40`), HEAD `f08f62be`; unit filter 205/205, integration filter 62/62. The orchestrator reopened T-88-22 because the permanent address was exposed in the session transcript. |

Gap-closure round notes (auditor, not findings):
- **Real-database concurrency path.** The SQL Server path for the concurrency-token predicate and rollback has only run on InMemory; UAT test 8 was skipped. The predicate is standard EF behaviour.
- **Rollbacks lower published revisions.** Rolling back is an operator action, not a runtime path, but it lowers entries already published at higher revisions: `Down()` then `Up()` again resets every row to revision 2, and redeploying v5.3.2 would publish SEQUENCE:1. Any rollback of this release should be followed by a forward fix, not left in place.
- **Bypass shapes the write-seam guard doesn't ban.** Raw ADO.NET through `GetDbConnection`, `FromSql`/`SqlQuery` carrying DML, `Entries()…OriginalValues.SetValues`, and a dot-less `Update(`/`Attach(` inside a DbContext subclass. None exists in production today.
- **Missing Threat Flags sections.** The 88-05, 88-06 and 88-07 SUMMARYs have no `## Threat Flags` section. No unregistered flags were found; the round-2 retry behaviour maps to T-88-10.
- **Process lesson.** The "never paste an address" control failed in both production checks. Future checks should declare the control that actually works: a dedicated temporary subscription, revoked afterwards, with a 410 confirmed, and no listing of files named after a token.

## Security Audit 2026-09-30

| Metric | Count |
|--------|-------|
| Threats found | 9 |
| Closed | 9 |
| Open | 0 |

No unregistered flags. All four SUMMARY `## Threat Flags` sections say "None". The code-review fixes
add no attack surface: the feed-window change touches only the `today` line, and the DTSTAMP change
only labels `CreatedAt` as UTC.

Auditor notes (not findings):
- `FormatTzidParameter` quotes but does not reject a double quote or CR/LF inside the parameter. It
  can't be reached, because the only production zone comes from `BoardClock` (OS lookup or UTC) and is
  set by the operator.
- The "absent from writer source" check for `SEQUENCE:0` was a plan-time acceptance grep, not a
  standing test. The document-level pins would still catch a regression.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter
- [x] T-88-07: operator revoked the temporary production subscription (410 confirmed)
- [x] Gap-closure round: T-88-09..T-88-21 closed, and AR-88-04..06 logged
- [ ] T-88-22: operator revokes the permanent iPhone subscription exposed in the session transcript, re-subscribes with a fresh address, and deletes the downloaded feed file (non-blocking)

**Approval:** verified 2026-09-30
