---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 04
subsystem: calendar-feed
tags: [icalendar, tzid, vtimezone, linux, cross-platform, architecture-guidance, validation]

requires:
  - phase: 88
    plan: 02
    provides: exact-byte time-zone block pins and SEQUENCE:1
  - phase: 88
    plan: 03
    provides: live-feed zone proof and the ambient-clock seam guard
provides:
  - Linux proof that the exact-byte time-zone pins pass on the platform production runs
  - The standing architecture guidance rewritten to the zoned calendar-feed contract
  - A signed-off validation contract with nyquist_compliant true
affects: [phase-88-verification]

plan_head_before: 3d4e5aa4b60c42ec947a7d24b4373777ec232f94

actuals:
  tokens: 900
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Cross-platform proof by feeding git archive HEAD on stdin to a --rm SDK container with no volume mount"

key-files:
  created: []
  modified:
    - .claude/architecture.md
    - .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-VALIDATION.md

key-decisions:
  - "The Linux run used the committed tree only (git archive to a temp file outside the repo, fed on stdin), so uncommitted host files could not leak in and nothing could be written back"
  - "The architecture paragraph replaces only the calendar-feed paragraph; the wall-clock definition and the never-converted rule are untouched"

requirements-completed: [CALTZ-02, CALTZ-09, CALTZ-10]

coverage:
  - id: D1
    description: "The calendar unit filter reports the same Total on Linux (mcr.microsoft.com/dotnet/sdk:10.0) as on Windows, zero failed, so the exact-byte time-zone pins are proven on the production platform"
    requirement: "CALTZ-02"
    verification:
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter CalendarFeed|CalendarSubscriptionQuestRecheck|AmbientClockSeamTests (Windows 129 passed, Linux container 129 passed)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The calendar-feed paragraph of the architecture guidance states TZID with unchanged digits, generated VTIMEZONE, X-WR-TIMEZONE, all sourced from IBoardClock.TimeZone, zone-free all-day entries and SEQUENCE:1, keeps the high-risk warning, and leaves the wall-clock rule unchanged"
    requirement: "CALTZ-10"
    verification:
      - kind: other
        ref: "grep acceptance checks on .claude/architecture.md (all required terms present, 'emits floating local' absent, wall-clock definition intact, no planning ids)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Full solution suite passes and the validation contract is signed off with every automated row green"
    verification:
      - kind: unit
        ref: "dotnet test (729 unit passed, 937 integration passed, 0 failed)"
        status: pass
    human_judgment: false
  - id: D4
    description: "On production, an entry Google already holds and a new entry both read 18:00 on a subscribed phone, and the same event and quest still read 18:00 in Apple Calendar on an iPhone (or one remove-and-re-add is recorded as the accepted resolution)"
    requirement: "CALTZ-09"
    verification: []
    human_judgment: true
    rationale: "Google fetches the feed from its own servers so no local address can reach it, and how each calendar app renders a declared zone cannot be pinned by byte tests"

duration: 4min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 04: Linux Proof, Zoned Architecture Guidance and Validation Sign-Off Summary

**The exact-byte time-zone pins now pass on Linux with the same 129-test count as Windows, the architecture guidance describes the zoned calendar feed instead of the floating one, and the validation contract is signed off with the production phone check queued for the operator.**

## Performance

- **Duration:** about 4 min
- **Started:** 2026-09-30T11:28:00Z
- **Completed:** 2026-09-30T11:32:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Ran the calendar unit filter on Windows (129 passed) and on `mcr.microsoft.com/dotnet/sdk:10.0` against `git archive HEAD` with no volume mount (129 passed, 0 failed). The byte-pinned time-zone blocks are therefore proven on the platform production runs, and no writer change was needed.
- Replaced the three-line "emits floating local" paragraph in `.claude/architecture.md` with the zoned contract: `TZID` on timed entries carrying unchanged wall-clock digits, a generated `VTIMEZONE` found by asking the zone for its offset at a moment, `X-WR-TIMEZONE`, all from `IBoardClock.TimeZone` (a degraded clock declares UTC), all-day entries zone-free, `SEQUENCE:1` never going down, and the high-risk warning on the two feed files.
- Full suite green: 729 unit and 937 integration tests, 0 failed.
- 88-VALIDATION.md: every automated row green, CALTZ-09 row left pending, `nyquist_compliant: true`, sign-off items ticked for automated verify, sampling continuity, no watch-mode flags, feedback latency. `status: draft` and `wave_0_complete: false` left for `/gsd-validate-phase`.

## Task Commits

1. **Task 1: Exact-byte pins pass on Linux** - `af8e1ff7` (test)
2. **Task 2: Architecture guidance, full suite, validation sign-off** - `f74c1fd1` (docs)

**Plan metadata:** the docs(88-04) commit that follows this summary.

## Files Created/Modified
- `.claude/architecture.md` - calendar-feed paragraph rewritten to the zoned contract
- `.planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-VALIDATION.md` - Linux evidence, green rows, sign-off

## Decisions Made
- Linux check ran from a temporary archive of the committed tree outside the repository, fed on stdin, so uncommitted host files could not influence it (T-88-08).
- Only the feed paragraph of the architecture section changed; the wall-clock definition and never-converted rule are byte-identical.

## Deviations from Plan

None - plan executed exactly as written. One transient slip during the sign-off edit: the first `nyquist_compliant` replacement hit the explanatory comment on line 5 rather than the frontmatter key; it was caught by the pre-commit check and corrected before the commit, so the committed file has the comment unchanged and the key set to `true`.

## Issues Encountered
None.

## Production Check Pending (CALTZ-09, human)

The production check on real phones is queued for the end-of-phase UAT and was not waited for here:
1. Before deploying, confirm `/health` on production reports Healthy and note what the friend's Google Calendar shows for an entry it already holds (for example quest 12039 on 2 October 2026, 20:00 today).
2. Deploy this phase by the usual route.
3. Optionally fetch your own live feed and check `UID:questboard-quest-12039` is followed by `DTSTART;TZID=Europe/Amsterdam:20261002T180000` and `SEQUENCE:1`, plus one `BEGIN:VTIMEZONE` and `X-WR-TIMEZONE:Europe/Amsterdam`. Never paste the subscription address into a log, issue or chat.
4. After Google's next refresh record whether the held entry reads 18:00; if not, one remove-and-re-add of the subscription is the accepted resolution, not a phase failure.
5. Confirm a new entry reads 18:00 on the friend's phone, and that the same event and quest still read 18:00 in Apple Calendar on the iPhone.
6. Record Google's refresh time as an observation only; no latency may be promised anywhere.

If `/health` reads Degraded, the feed declares UTC and every subscriber sees late times until the zone configuration is fixed.

## Known Stubs
None.

## Threat Flags
None. T-88-07 (subscription address) is handled by the human-check wording; T-88-08 (container run) by the stdin-archive, no-mount design.

## Next Phase Readiness
All four plans of phase 88 have summaries. Remaining: `/gsd-verify-work` for the CALTZ-09 production check, and `/gsd-validate-phase` to flip validation status.

## Self-Check: PASSED

`.claude/architecture.md` and `88-VALIDATION.md` are modified and committed; commits `af8e1ff7` and `f74c1fd1` are present on the branch; commit count measured as 2 from the recorded base; acceptance greps all pass.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
