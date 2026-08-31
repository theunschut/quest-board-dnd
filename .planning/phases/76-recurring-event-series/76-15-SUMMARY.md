---
phase: 76-recurring-event-series
plan: 15
subsystem: docs
tags: [requirements-traceability, roadmap, gap-closure, tracking]

# Dependency graph
requires:
  - phase: 76-recurring-event-series (76-13)
    provides: mobile horizon banner and CalendarHorizonBannerTests, closing the first EVTRECUR-03 gap
  - phase: 76-recurring-event-series (76-14)
    provides: campaign calendar navigation entry, events-only quest exclusion, and the NAV-01 calendar-clause supersession, closing the second EVTRECUR-03 gap
provides:
  - An accurate REQUIREMENTS.md — all eight EVTRECUR requirements checked and reading Complete in the traceability table, agreeing with each other
  - A supersession note in REQUIREMENTS.md and a matching Decisions amended / Gap closure pair in ROADMAP.md, naming the superseded NAV-01 calendar clause, its replacement test, and the plans that closed the gaps
affects: [76-recurring-event-series (phase close), future navigation-decision readers]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Evidence-before-bookkeeping: re-run the gap plans' test filters and the full suites before editing any tracking document, and transcribe commands and counts into the summary so the edits are checked against reproduced numbers rather than trusted prose"
    - "Forward-only supersession pointer: an amendment to an archived milestone decision is recorded in the live documents that reference it, never by editing the archived artifact itself"

key-files:
  created: []
  modified:
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md

key-decisions:
  - "Left EVTRECUR-09 (Future Requirements, deferred) unchecked despite matching the broad acceptance-criteria grep pattern for unchecked EVTRECUR lines — it is a distinct, explicitly out-of-scope requirement, not one of the eight Phase 76 recurrence requirements this plan corrects"
  - "Named NAV-01 explicitly in both documents' supersession text (not just implied by context) so a literal search for the decision ID finds the amendment"

requirements-completed: [EVTRECUR-02, EVTRECUR-03]

coverage:
  - id: D1
    description: "Both gaps blocking EVTRECUR-03 are proven closed by re-run evidence before any tracking document is touched"
    requirement: "EVTRECUR-03"
    verification:
      - kind: other
        ref: "grep -c SeriesBelowRunway QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml -> 7"
        status: pass
      - kind: other
        ref: "grep -c BoardType.Campaign QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs -> 1"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CalendarHorizonBannerTests -> 6/6 passed"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CalendarBoardTypeScopeTests -> 5/5 passed"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~LayoutNavigationTests -> 24/24 passed, 0 failing"
        status: pass
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests -> 385/385 passed"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests -> 528/528 passed, 0 failing"
        status: pass
    human_judgment: false
  - id: D2
    description: "The requirements register agrees with the code and with itself: EVTRECUR-02 and EVTRECUR-03 checked, all eight traceability rows read Complete"
    requirement: "EVTRECUR-02"
    verification:
      - kind: other
        ref: "grep -c \"^- \\[x\\] \\*\\*EVTRECUR-\" .planning/REQUIREMENTS.md -> 8"
        status: pass
      - kind: other
        ref: "grep -c \"| EVTRECUR-0[1-8] | Phase 76 | Complete |\" .planning/REQUIREMENTS.md -> 8"
        status: pass
      - kind: other
        ref: "grep -c \"| EVTRECUR-0[1-8] | Phase 76 | Not started |\" .planning/REQUIREMENTS.md -> 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "A reader arriving at the amended navigation decision from either live document finds the amendment, its precise scope, and the replacement test"
    verification:
      - kind: other
        ref: ".planning/REQUIREMENTS.md contains NAV-01, f7a31fa9, Nav_CampaignDm_CalendarLinkPresent, 76-14"
        status: pass
      - kind: other
        ref: ".planning/ROADMAP.md contains NAV-01, Nav_CampaignDm_CalendarLinkPresent, 76-13, 76-14, 76-15"
        status: pass
    human_judgment: false
  - id: D4
    description: "The archive under .planning/milestones/ was never touched, and no requirement wording line was altered"
    verification:
      - kind: other
        ref: "git status --short .planning/milestones/ -> empty (checked after both document edits)"
        status: pass
      - kind: other
        ref: "git diff -- .planning/REQUIREMENTS.md -> only checkbox/status-cell changes plus the new blockquote, no wording line altered"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 15: Requirements register correction and navigation supersession record Summary

**Corrected REQUIREMENTS.md so all eight EVTRECUR requirements read checked and Complete, and recorded — in both REQUIREMENTS.md and ROADMAP.md — that plan 76-14 superseded only the calendar clause of Phase 37's NAV-01 decision.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-28T21:20:00Z
- **Completed:** 2026-08-28T21:45:25Z
- **Tasks:** 3
- **Files modified:** 2

## Accomplishments

- **Task 1 (evidence, no edits):** Re-ran both gap plans' test filters and the two full suites rather than trusting prior summaries. `SeriesBelowRunway` now appears 7 times in `Index.Mobile.cshtml` (was 0 at verification); `BoardType.Campaign` appears once in `CalendarController.cs` (was 0). `CalendarHorizonBannerTests` (6/6), `CalendarBoardTypeScopeTests` (5/5), and `LayoutNavigationTests` (24/24, 0 failing) all pass. The full unit suite passed 385/385 and the full integration suite passed 528/528, 0 failing — 913 total, matching the number the orchestrator's context stated for this base commit. `git status --short .planning/` reported nothing after this task, confirming no document was touched before the evidence was gathered.
- **Task 2:** Checked EVTRECUR-02 (live preview — functionally complete since 76-04/76-06, human-verified in 76-12, three endpoint tests passing) and EVTRECUR-03 (rolling window — both gaps now closed per Task 1's evidence) in `REQUIREMENTS.md`. Corrected all eight EVTRECUR rows in the Traceability table from "Not started" to "Complete", resolving the contradiction where the table disagreed with the checkbox list above it. Added a blockquote supersession note directly under the eight recurrence checkboxes naming NAV-01, its Phase 37 archive path, commit `f7a31fa9`, the plan that amended it (`76-14`), the precise scope of the amendment (calendar clause only — NAV-02, NAV-04, NAV-05, NAV-06, and the logged-out rule are untouched), and the replacement test `LayoutNavigationTests.Nav_CampaignDm_CalendarLinkPresent`.
- **Task 3:** Added a `**Decisions amended by this phase:**` block to the Phase 76 entry in `ROADMAP.md`, between the existing `Decisions locked before planning` and `Risks this phase must actively avoid` blocks, naming the same NAV-01 supersession and its rationale (two new campaign-relevant read surfaces on the calendar; the quest-leak the board-type gate also closed). Added a `**Gap closure:**` block recording that plans 76-13, 76-14, and 76-15 closed the two gaps verification found against success criterion 3, and the reusable lesson: the banner defect survived to verification because no automated test asserted the banner rendered on either calendar surface, and both surfaces now have that coverage.

## Task Commits

Each task was committed atomically:

1. **Task 1: Confirm both gaps are genuinely closed before any document is edited** - no commit (edits nothing by design; evidence transcribed into this summary)
2. **Task 2: Correct the requirements register and record the navigation supersession** - `ac5a187d` (docs)
3. **Task 3: Record the supersession and the gap closure on the roadmap** - `91bb00e9` (docs)

## Files Created/Modified

- `.planning/REQUIREMENTS.md` - Checked EVTRECUR-02 and EVTRECUR-03; corrected all eight EVTRECUR traceability rows to Complete; added the NAV-01 supersession blockquote
- `.planning/ROADMAP.md` - Added `Decisions amended by this phase` and `Gap closure` blocks to the Phase 76 entry

## Decisions Made

- Left `EVTRECUR-09` (in the `## Future Requirements` section, deliberately deferred) unchecked. The plan's Task 2 acceptance criterion `grep -c "^- \[ \] \*\*EVTRECUR-" .planning/REQUIREMENTS.md` returns 1, not the specified 0, because that pattern also matches `EVTRECUR-09`'s unchecked box outside the Calendar Events — Recurrence section. `EVTRECUR-09` is a distinct, explicitly out-of-scope requirement ("Regenerate untouched future occurrences when a series rule is edited") and correctly remains unchecked; checking it would misrepresent deferred, unbuilt work as complete. The more precise criteria — `^- \[x\] \*\*EVTRECUR-` returns 8, and both traceability-table checks return exactly 8/0 — all pass exactly as specified, confirming the actual intent (all eight Phase 76 recurrence requirements agree between list and table) is satisfied. Documented here per Rule 1 (the acceptance criterion's grep pattern was imprecise, not the document).
- Named `NAV-01` as a literal token in both documents' supersession text (not just implied through surrounding context), since two of the plan's own acceptance criteria search for that literal string.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Acceptance criterion's grep pattern for "no unchecked EVTRECUR requirement" caught an unrelated, correctly-deferred requirement**
- **Found during:** Task 2, verifying acceptance criteria after the edit
- **Issue:** `grep -c "^- \[ \] \*\*EVTRECUR-" .planning/REQUIREMENTS.md` returned 1 instead of the plan's specified 0, because `EVTRECUR-09` (in `## Future Requirements`, deliberately deferred and out of Phase 76's eight-requirement scope) also matches the `EVTRECUR-` prefix.
- **Fix:** No document change — `EVTRECUR-09` correctly stays unchecked. Verified via the more precise criteria (`^- \[x\] \*\*EVTRECUR-` returns 8; both traceability-table greps return exactly 8 Complete / 0 Not started), which unambiguously confirm all eight Phase 76 recurrence requirements are checked and agree with the table.
- **Files modified:** None beyond the Task 2 edit already made
- **Verification:** All other Task 2 acceptance criteria pass exactly as specified; the supersession note and table are unaffected
- **Committed in:** `ac5a187d` (no separate commit needed — this is a verification finding, not a fix)

---

**Total deviations:** 1 auto-fixed (1 bug in the plan's own acceptance-criteria pattern, not in the document)
**Impact on plan:** No scope creep. The requirements register's actual truthfulness goal — every Phase 76 recurrence requirement checked and matching its traceability row — is fully met; the one criterion that technically failed was over-broad by construction and its failure mode (leaving a deferred, out-of-scope requirement correctly unchecked) is the opposite of a false-completion claim.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Both tracking defects identified by `76-VERIFICATION.md` are closed: EVTRECUR-02 and EVTRECUR-03 now read checked and Complete, and the list/table agree for all eight recurrence requirements.
- The NAV-01 calendar-clause supersession is recorded in both live planning documents a future reader would consult, with precise scope (only the calendar clause; four other restrictions and the logged-out rule stand) and the replacement test named — the archived Phase 37 artifacts under `.planning/milestones/v6.0-phases/37-navigation-access-control/` were never touched, confirmed by `git status --short .planning/milestones/` reporting nothing after every task.
- Phase 76 (Recurring Event Series) is now fully closed: all 15 plans executed, both gap-closure defects fixed and tested (76-13, 76-14), and the tracking documents corrected to match (76-15). No further plans are queued for this phase.

## Self-Check: PASSED

- FOUND: .planning/REQUIREMENTS.md (modified, verified via git diff)
- FOUND: .planning/ROADMAP.md (modified, verified via git diff)
- FOUND: .planning/phases/76-recurring-event-series/76-15-SUMMARY.md
- FOUND commit: ac5a187d (docs: requirements register correction)
- FOUND commit: 91bb00e9 (docs: roadmap supersession and gap closure record)

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*
