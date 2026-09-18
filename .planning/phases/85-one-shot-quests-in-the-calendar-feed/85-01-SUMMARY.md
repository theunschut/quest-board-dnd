---
phase: 85-one-shot-quests-in-the-calendar-feed
plan: 01
subsystem: docs
tags: [requirements, roadmap, validation-strategy, ledger]

# Dependency graph
requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: The CALFEED-* requirement family and the closed Phase 84 ROADMAP block this plan appends alongside without touching
provides:
  - The QUESTFEED-01 through QUESTFEED-18 requirement family, registered in REQUIREMENTS.md and ROADMAP.md
  - A validation contract (85-VALIDATION.md) keyed by real task ids across plans 85-02 through 85-05
affects: [85-02-tracer-and-configurable-duration, 85-03-seat-and-dm-predicates, 85-04-disappearance-and-window, 85-05-board-type-and-isolation, 85-06-closeout]

actuals:
  tokens: 4219
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md
    - .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md

key-decisions:
  - "QUESTFEED-* is a new prefix rather than extending CALFEED-*, so the shipped Phase 84 family stays a single-phase block in both traceability tables (rationale carried from the plan's frontmatter, not decided fresh here)"
  - "The Requirement column in 85-VALIDATION.md's Per-Task Verification Map now carries the literal QUESTFEED id(s) for each row, with the original decision reference (D-01, D-02/D-03, etc.) kept parenthetically for provenance rather than discarded"
  - "Board-type-narrowing, tenant-isolation and UID-collision rows had their placeholder threat refs (T-85-BOARDTYPE, T-85-TENANT, T-85-UID) replaced with the real threat ids from the phase's STRIDE register (T-85-02, T-85-01, T-85-04) as the plan's Task 3 instructed"

patterns-established: []

requirements-completed: [QUESTFEED-01, QUESTFEED-02, QUESTFEED-03, QUESTFEED-04, QUESTFEED-05, QUESTFEED-06, QUESTFEED-07, QUESTFEED-08, QUESTFEED-09, QUESTFEED-10, QUESTFEED-11, QUESTFEED-12, QUESTFEED-13, QUESTFEED-14, QUESTFEED-15, QUESTFEED-16, QUESTFEED-17, QUESTFEED-18]

coverage:
  - id: D1
    description: "REQUIREMENTS.md carries a new Calendar Feed — One-Shot Quest Sessions section with 18 unchecked QUESTFEED bullets, 18 Pending traceability rows, and updated 134/134 coverage counters"
    verification:
      - kind: other
        ref: "grep -c '^- \\[ \\] \\*\\*QUESTFEED-' .planning/REQUIREMENTS.md == 18; grep -c '^| QUESTFEED-[0-9][0-9] | Phase 85 | Pending |$' .planning/REQUIREMENTS.md == 18"
        status: pass
    human_judgment: false
  - id: D2
    description: "ROADMAP.md's Phase 85 block names all 18 QUESTFEED ids instead of TBD, and its Requirements Coverage table gains 18 Phase 85 rows with a 134/134 footer naming no phase still awaiting requirements"
    verification:
      - kind: other
        ref: "grep -c '^| QUESTFEED-[0-9][0-9] | Phase 85 |$' .planning/ROADMAP.md == 18; grep -c '^### Phase ' .planning/ROADMAP.md unchanged at 14"
        status: pass
    human_judgment: false
  - id: D3
    description: "85-VALIDATION.md's Per-Task Verification Map is keyed by 14 real task-id rows across plans 85-02 through 85-05, with no remaining TBD, and both Manual-Only Verifications rows remain open and unchanged"
    verification:
      - kind: other
        ref: "grep -c TBD .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md == 0; grep -cE '^\\| 85-0[2-5]-T[0-9]+ \\|' == 14"
        status: pass
    human_judgment: false

duration: ~10min
completed: 2026-09-18
status: complete
---

# Phase 85 Plan 01: Mint QUESTFEED Requirements and Key the Validation Contract Summary

**Minted the 18-member QUESTFEED-* requirement family into REQUIREMENTS.md and ROADMAP.md, and re-keyed 85-VALIDATION.md's per-task verification map from TBD placeholders to real 85-02 through 85-05 task ids.**

## Performance

- **Duration:** ~10 min
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- Added a new `### Calendar Feed — One-Shot Quest Sessions` section to REQUIREMENTS.md holding QUESTFEED-01 through QUESTFEED-18 as unchecked, observable-outcome bullets, immediately after the shipped `CALFEED` section and before `Link Previews`
- Appended 18 Pending traceability rows mapped to Phase 85, and updated the Coverage block from 116/116 to 134/134 total requirements
- Replaced ROADMAP.md's Phase 85 `**Requirements**: TBD` line with all 18 QUESTFEED ids; confirmed the existing 6-plan, 5-wave Plans list already matched the plan files on disk (no discrepancy found — the planner had already written it correctly)
- Appended 18 QUESTFEED rows to ROADMAP.md's Requirements Coverage table and rewrote its footer to `134/134 requirements mapped ✓ · 0 unmapped · 0 phases awaiting requirements`
- Re-keyed 85-VALIDATION.md's Per-Task Verification Map: replaced every `TBD` Task ID/Plan/Wave with real ids (`85-02-T1` through `85-05-T3`), reassigned the board-type/tenant-isolation/UID-collision threat refs to the phase's actual STRIDE ids (`T-85-02`, `T-85-01`, `T-85-04`), added four previously-missing rows (tracer, config refuse-to-start, logged-drop, merged-ordering), and named the creating/extending plan for each Wave 0 Requirements checklist item

## Task Commits

Each task was committed atomically:

1. **Task 1: Mint the eighteen QUESTFEED requirements into REQUIREMENTS.md and its Traceability table** - `f7a8d635` (docs)
2. **Task 2: Replace ROADMAP.md's Phase 85 TBD requirements, plan count and plan list, and extend its coverage table** - `5dc80b1b` (docs)
3. **Task 3: Key 85-VALIDATION.md's per-task map to this phase's real task ids** - `95f764d0` (docs)

**Plan metadata:** committed alongside this SUMMARY (see final commit in worktree log)

## Files Created/Modified
- `.planning/REQUIREMENTS.md` - New QUESTFEED-01–18 section, 18 traceability rows, coverage counters updated to 134/134
- `.planning/ROADMAP.md` - Phase 85's Requirements line filled in, 18 coverage rows appended, footer updated
- `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md` - Per-task map keyed by real task ids, Wave 0 checklist annotated with owning plans

## Decisions Made
- Kept the original decision references (`D-01`, `D-02/D-03`, etc.) parenthetically alongside the new QUESTFEED ids in 85-VALIDATION.md's Requirement column, rather than discarding them, so provenance from `85-RESEARCH.md`'s decision→test map stays traceable
- Confirmed rather than rewrote ROADMAP.md's Plans line and wave-grouped plan list — the planner had already registered it correctly across the 6 plan files on disk, so no discrepancy note was needed

## Deviations from Plan

None - plan executed exactly as written. Task 2's "reconciliation" step (confirming the Plans line and list against the six plan files on disk) found the two already in agreement, so no correction commit was needed for that sub-step.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plans 85-02 through 85-06 can now carry non-empty `requirements` frontmatter — the QUESTFEED ids they reference all exist and resolve
- 85-VALIDATION.md's per-task map is ready for the executor of 85-02 through 85-05 to flip rows from `⬜ pending` to `✅ green` as each plan's facts land
- No source file was touched by this plan; `dotnet build`/`dotnet test` were not run and are not part of this plan's verification

## Self-Check: PASSED

All three modified files verified present on disk; all three task commits (`f7a8d635`, `5dc80b1b`, `95f764d0`) verified present in `git log`.

---
*Phase: 85-one-shot-quests-in-the-calendar-feed*
*Completed: 2026-09-18*
