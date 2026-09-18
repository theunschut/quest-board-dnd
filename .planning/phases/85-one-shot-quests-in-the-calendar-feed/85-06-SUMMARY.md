---
phase: 85-one-shot-quests-in-the-calendar-feed
plan: 06
subsystem: docs
tags: [static-guard, ledger-reconciliation, requirements, roadmap, validation, calendar-feed]

requires:
  - phase: 85-one-shot-quests-in-the-calendar-feed
    plan: 05
    provides: "The full behavioural surface (85-02 through 85-05) this plan's ledger flips are verified against"
provides:
  - "Five new forbidden-claim cases in CalendarSubscriptionStaticGuardTests pinning that no application copy may present the invented four-hour quest session length as a fact the board knows, in the same data-driven theory that already forbids the equivalent event claim"
  - "All eighteen QUESTFEED requirements reading Complete in both REQUIREMENTS.md's body checklist and its Traceability table"
  - "ROADMAP.md's Phase 85 block reading 6/6 plans complete, with a Decisions Reached summary replacing the four open pre-planning questions and each of the three named risks marked with how it was addressed"
  - "85-VALIDATION.md signed off: every per-task row green, Wave 0 checklist complete, nyquist_compliant: true, with both Manual-Only Verifications rows left open and strengthened"
affects: []

actuals:
  tokens: 9100
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Extended an existing data-driven forbidden-claim theory with new literal-string cases rather than introducing a second theory or a second walk over the two Profile layouts"
    - "Ledger reconciliation flips a requirement to Complete only against a named green row in the per-task verification map, never against a memory of having written the behavior"

key-files:
  modified:
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md
    - .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md

key-decisions:
  - "Chose five new forbidden-claim cases scoped to the number 4 (the actual configured default QuestDurationHours) rather than a bare 'hours' pattern, and kept every case adjacent to 'session' or 'quest' wording, per the task's own instruction that a bare number of hours anywhere on the page is not the target"
  - "Did not touch CalendarSubscriptionStaticGuardTests.ResolveRepoFile or NoPlanningOrTrackingReference_ReachedTheSourceTree, even though the environment context named this file as the one file this plan owns and the pre-existing Linux-only bug lives inside it -- the plan's Task 1 explicitly instructs leaving the planning-reference guard exactly as it is because touching it risks its self-exclusion logic; this is documented below as a known-open item rather than silently left unfixed"
  - "Added a new 85-06-T1 row to 85-VALIDATION.md's per-task verification map for this plan's own copy-guard task, following the precedent set by 84-VALIDATION.md's 84-08-T1/T2 rows for that phase's own closing plan"
  - "Scoped the validation sign-off's fact-count command list to a narrower filter for the forbidden-claim theory (FullyQualifiedName~NeitherLayout_MakesAForbiddenClaim) rather than the whole CalendarSubscriptionStaticGuardTests class, so every listed command genuinely exits 0 -- the whole-class command does not, for the pre-existing reason documented below"

patterns-established: []

requirements-completed: [QUESTFEED-01, QUESTFEED-02, QUESTFEED-08, QUESTFEED-09, QUESTFEED-10, QUESTFEED-11, QUESTFEED-12, QUESTFEED-17]

coverage:
  - id: D1
    description: "Application copy never presents the invented four-hour quest session length as something the board knows, in the same forbidden-claim theory that already covers the equivalent event claim, proven to trip by a mutation check"
    requirement: "QUESTFEED-08"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs#NeitherLayout_MakesAForbiddenClaim (5 new cases)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every QUESTFEED requirement with a green automated fact behind it reads Complete in both REQUIREMENTS.md's checklist and its Traceability table"
    requirement: "QUESTFEED-01, QUESTFEED-02, QUESTFEED-08, QUESTFEED-09, QUESTFEED-10, QUESTFEED-11, QUESTFEED-12, QUESTFEED-17"
    verification:
      - kind: manual
        ref: "grep -c '^- \\[x\\] \\*\\*QUESTFEED-' .planning/REQUIREMENTS.md == 18, matched against 85-02-SUMMARY.md's coverage rows and 85-VALIDATION.md's per-task map"
        status: pass
    human_judgment: false
  - id: D3
    description: "85-VALIDATION.md is signed off against an observed suite: every per-task row green, Wave 0 complete, nyquist_compliant true, both manual-only gaps left open"
    requirement: "n/a (phase governance)"
    verification:
      - kind: manual
        ref: "dotnet test QuestBoard.UnitTests (525/525) + dotnet test QuestBoard.IntegrationTests (838/839, one pre-existing documented failure) run before any tick was written"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-09-18
status: complete
---

# Phase 85 Plan 06: Ledger Close-Out and the Session-Length Copy Guard Summary

**Five new forbidden-claim cases close the phase's last shipped-behavior gap — no copy may present the invented four-hour quest session as a fact the board knows — and both REQUIREMENTS.md and ROADMAP.md are brought into agreement with what the prior five plans actually proved, while 85-VALIDATION.md is signed off against an observed green suite with both of the phase's genuine open gaps (relational translation, the real-device check) restated in place, not closed.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-09-18T17:12:00Z
- **Tasks:** 3 (all `type="auto"`)
- **Files modified:** 4

## Accomplishments

- Extended `CalendarSubscriptionStaticGuardTests`'s existing forbidden-claim theory with five new cases (`4-hour session`, `four-hour session`, `session lasts 4 hours`, `quest runs for 4 hours`, `session ends after 4 hours`) — case-insensitive, tight enough to require "hours" adjacent to session/quest wording rather than matching a bare number, and proven to trip by a one-shot mutation check (temporarily inserted into `Profile.cshtml`, confirmed red at 18/19, reverted, confirmed green at 19/19)
- Flipped the eight remaining QUESTFEED requirements (01, 02, 08, 09, 10, 11, 12, 17) from Pending to Complete in both `REQUIREMENTS.md`'s body checklist and its Traceability table, each against a named green row in `85-VALIDATION.md`'s per-task map (85-02's tracer fact, options-validation suite, and writer suite) — all eighteen QUESTFEED requirements now read Complete; CALFEED-01..17 unchanged at 17
- `ROADMAP.md`'s Phase 85 block: all six plan bullets checked, `**Plans**: 6/6 plans complete`; the four open pre-planning questions replaced with a Decisions Reached summary covering all ten locked decisions (D-01 through D-10) plus the query-shape and merge-ordering calls, pointing at `85-CONTEXT.md` as authoritative; each of the three named risks marked with how it was addressed; and a closing paragraph naming both gaps this phase does not claim to have closed
- `85-VALIDATION.md` signed off: every per-task row (85-02 through 85-05, plus a new 85-06-T1 row for this plan's own copy guard) set to green with File Exists confirmed, Wave 0 checklist fully ticked, Validation Sign-Off checklist ticked, `nyquist_compliant: true` set (status stays `draft` per the plan's own instruction — promotion belongs to `/gsd-validate-phase`), and a line beneath the sign-off recording the 39 automated facts this phase added across five test classes and the five commands that individually prove them
- Both Manual-Only Verifications rows strengthened and kept open: the relational-translation row now names the concrete route (`GET /feeds/calendar/{feedToken}.ics`) and repository method (`QuestRepository.GetFeedQuestsForUserAsync`) under test, and records this as the third consecutive phase to defer that gap; the real-device row now states explicitly that no client poll-and-render behaviour was observed, since the silent-disappearance decision (D-09) rests on exactly that unobserved behaviour
- Two Broken Windows Ledger entries recorded (`.planning/WINDOWS.md`, ids 5 and 6): the relational-translation gap as `unrun-verify`, and the pre-existing Linux-only static-guard failure as `deviation`

## Task Commits

Each task was committed atomically:

1. **Task 1: Forbid any copy claiming the board knows how long a session runs** - `fefe7623` (test)
2. **Task 2: Bring REQUIREMENTS.md and ROADMAP.md into agreement with what shipped** - `109c3feb` (docs)
3. **Task 3: Sign off the validation contract, and leave the two open gaps open** - `033955e5` (docs)

**Plan metadata:** pending (this commit, plus `.planning/WINDOWS.md`)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs` - five new forbidden-claim cases plus an extended comment explaining the session-length rationale; the planning-reference guard method left untouched
- `.planning/REQUIREMENTS.md` - eight QUESTFEED body bullets and eight Traceability rows flipped Pending → Complete
- `.planning/ROADMAP.md` - Phase 85 block: plan bullets checked, Plans line updated, Decisions Reached block replacing the open-questions block, risks marked addressed, two open gaps appended
- `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md` - per-task map greened, new 85-06-T1 row added, Wave 0 and Sign-Off checklists ticked, frontmatter updated, Manual-Only rows strengthened
- `.planning/WINDOWS.md` - two entries appended (ids 5, 6), not part of `files_modified` but part of standard summary-time ledger population

## Decisions Made

- Five forbidden-claim cases scoped to the literal number 4 (the actual `QuestDurationHours` default) and always adjacent to session/quest wording, rather than a bare-hours pattern that would risk matching unrelated copy elsewhere on the Profile page
- Left `ResolveRepoFile`/`NoPlanningOrTrackingReference_ReachedTheSourceTree` untouched per the plan's explicit Task 1 instruction, despite this plan nominally "owning" the file that contains the pre-existing Linux-only bug — see Known Open Items below
- Added a new `85-06-T1` row to the per-task verification map (precedent: `84-VALIDATION.md`'s `84-08-T1`/`84-08-T2` rows for that phase's own closing plan)
- Scoped the sign-off's fact-count commands to `FullyQualifiedName~NeitherLayout_MakesAForbiddenClaim` rather than the whole `CalendarSubscriptionStaticGuardTests` class, so every command listed actually exits 0 on this host

## Deviations from Plan

### Auto-fixed Issues

None — no bugs, missing critical functionality, or blocking issues required a fix outside the plan's own instructions.

### Acceptance-criteria discrepancies (not code defects, both pre-existing and out of this plan's scope)

**1. `dotnet test` does not exit 0 for the whole solution on this Linux host, and neither does `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionStaticGuardTests`.**
- **Found during:** Task 1 and Task 3 verification.
- **Cause:** `CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree`'s `ResolveRepoFile` helper walks up from the test binary's directory and stops at the first path segment satisfying `File.Exists || Directory.Exists`. On Linux, `dotnet build` emits a native apphost binary literally named `QuestBoard.Service` (no extension) inside the test's own `bin/` directory, which collides with the project-folder name the helper is looking for and makes it return a file path where a directory is expected, throwing `DirectoryNotFoundException`. On Windows the apphost carries a `.exe` extension, so this never occurs there. This bug predates this phase (shipped in Phase 84, commit `78aa5286`) and was already logged in `deferred-items.md` by plan 85-02, which observed and named this exact failure as belonging to plan 85-06.
- **Why not fixed here:** this plan's Task 1 explicitly instructs "Leave the existing planning-reference guard exactly as it is... nothing needs adding to it for this phase, and touching it risks its self-exclusion logic." The bug lives inside the method this instruction forbids touching. Fixing `ResolveRepoFile` would be a legitimate, scoped one-line change (e.g. requiring `Directory.Exists` specifically when a directory is wanted), but doing so was not authorized by this plan's own task text, so it was left as documented, not silently passed over.
- **Verified scope of impact:** every automated fact this plan added passes on its own — the forbidden-claim theory's 19 cases (14 pre-existing + 5 new) all pass under `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~NeitherLayout_MakesAForbiddenClaim"`, and the full solution's only failure is this one pre-existing, unrelated test. `QuestBoard.UnitTests` passes 525/525 on its own. `QuestBoard.IntegrationTests` passes 838/839 (833 baseline + this plan's 5 new cases), with the one documented failure.
- **Files modified:** none (left as-is)
- **Recorded:** `.planning/WINDOWS.md` id 6, `.planning/phases/85-one-shot-quests-in-the-calendar-feed/deferred-items.md` item 1 (pre-existing)

---

**Total deviations:** 0 auto-fixed. One pre-existing, out-of-scope acceptance-criteria discrepancy, inherited from Phase 84 and already logged before this plan began, left open per this plan's own explicit scope instruction.
**Impact on plan:** None on the phase's actual scope or correctness — every fact this phase's tasks produce passes individually, and the two genuine open gaps (relational translation, real-device check) are the ones this plan was written to leave open, not this one.

## Known Open Items

Restated here, as instructed, rather than allowed to quietly disappear:

1. **`CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree` fails on Linux dev environments.** Pre-existing (Phase 84), not caused by this plan, not fixed by this plan per Task 1's explicit instruction not to touch the planning-reference guard. See Deviations above and `deferred-items.md` item 1. Suggested follow-up (not part of this phase): make `ResolveRepoFile` require `Directory.Exists` specifically when the caller wants a directory.
2. **Relational SQL translation of the quest predicate remains unproven.** Third consecutive phase (82, 84, 85) to carry this gap. Compensating manual check recorded in `85-VALIDATION.md`'s Manual-Only Verifications table, naming the concrete route and repository method. Not closed here, per this plan's explicit prohibition.
3. **The real-device subscription check inherited from Phase 84 remains unobserved.** No claim about refresh latency, in-place update, or client behaviour when an entry disappears between polls is made anywhere in this phase's output. Not closed here, per this plan's explicit prohibition.

## Issues Encountered

None beyond the documented pre-existing Linux-only test failure above.

## Verification

- `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~NeitherLayout_MakesAForbiddenClaim"` exits 0 with 19 cases (14 pre-existing + 5 new)
- Mutation check run and reverted: inserting `4-hour session` into `Profile.cshtml` turned the theory red (18/19), removing it turned it back green (19/19); `git diff --stat QuestBoard.Service/Views/Account/Profile.cshtml` shows no output after revert
- `grep -c '^- \[x\] \*\*QUESTFEED-' .planning/REQUIREMENTS.md` == 18, matching `grep -c '^| QUESTFEED-[0-9][0-9] | Phase 85 | Complete |$' .planning/REQUIREMENTS.md` == 18
- `grep -c '^- \[x\] \*\*CALFEED-' .planning/REQUIREMENTS.md` == 17, unchanged
- `grep -c '^### Phase ' .planning/ROADMAP.md` == 14, unchanged before and after
- `git diff .planning/ROADMAP.md` confined entirely to the Phase 85 block (lines 889-954 of the pre-edit numbering)
- `grep -c '⬜ pending' .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md` == 0
- `85-VALIDATION.md` frontmatter reads `nyquist_compliant: true`, `wave_0_complete: true`, `status: draft`
- Manual-Only Verifications table still carries exactly two rows, neither described as closed, resolved, verified or observed
- `dotnet test QuestBoard.UnitTests` — 525/525 passed
- `dotnet test QuestBoard.IntegrationTests` — 838/839 passed (one pre-existing, documented, out-of-scope failure — see Deviations)
- `git status --porcelain` (across this plan's three task commits) names exactly the four files in `files_modified`: `git diff --name-only f517872e HEAD` confirms `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `85-VALIDATION.md`, `CalendarSubscriptionStaticGuardTests.cs`

## What This Phase Does Not Claim

Stated once more, at the close, per this plan's own instruction that these statements must not quietly disappear:

- **Relational translation of the new quest predicate is unproven.** Every integration fact runs on the EF Core in-memory provider. This is the third consecutive phase to carry that gap; it is recorded with a runnable manual compensating check, and it is not closed here.
- **No calendar client's behaviour has been observed.** The real-device subscription check was deferred by operator decision in Phase 84. Nothing in this phase observed refresh latency, in-place update, or what a client does when an entry disappears between polls — and the silent-disappearance decision rests on precisely that last behaviour. No output of this phase promises any of it.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

This closes Phase 85. All eighteen QUESTFEED requirements read Complete, `ROADMAP.md`'s Phase 85 block reads 6/6 with its decisions reached and risks addressed, and `85-VALIDATION.md` is signed off with `nyquist_compliant: true`. Two genuine gaps remain open by design (relational translation, real-device check), and one pre-existing, unrelated, Linux-only test failure remains open and documented. No blockers for closing the phase or starting the next.

## Self-Check: PASSED

- FOUND: `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs`
- FOUND: `.planning/REQUIREMENTS.md`
- FOUND: `.planning/ROADMAP.md`
- FOUND: `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md`
- FOUND: `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-06-SUMMARY.md`
- FOUND commit `fefe7623` (Task 1)
- FOUND commit `109c3feb` (Task 2)
- FOUND commit `033955e5` (Task 3)

---
*Phase: 85-one-shot-quests-in-the-calendar-feed*
*Completed: 2026-09-18*
