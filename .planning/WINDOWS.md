---
schema_version: 1
open_count: 5
waived_count: 0
fixed_count: 1
total_count: 6
last_updated: 2026-09-18T17:11:39.383Z
---

# Broken Windows Ledger

> Cross-phase defect register. With `workflow.windows_enforce` enabled, `/gsd-ship` blocks while `open_count > 0`.
> Waive with `gsd-tools windows waive <id> "<reason>"` (reason required).
> Mark fixed with `gsd-tools windows fixed <id>`.

| id | phase | kind | file | line | description | status | reason | recorded_at | resolved_at |
|----|-------|------|------|------|-------------|--------|--------|-------------|-------------|
| 1 | 84 | deviation | .planning/ROADMAP.md |  | 84-01 Task 2 ROADMAP.md edits (Requirements line + 16 Coverage rows) computed and verified but not committed - worktree harness blocks commits touching ROADMAP.md; orchestrator must apply after wave merge (see 84-01-SUMMARY.md) | fixed |  | 2026-09-17T20:23:58.316Z | 2026-09-18T07:10:44.090Z |
| 2 | 84 | deviation | QuestBoard.Service/Controllers/Admin/AccountController.cs |  | Task 3 acceptance criterion 'grep -cE Request.Scheme\|Request.Host outputs 0' is unsatisfiable without touching two pre-existing, unrelated Url.Action(..., Request.Scheme) call sites in ForgotPassword/Edit (email callback URLs) - out of this plan's scope, left as-is | open |  | 2026-09-18T09:38:07.149Z |  |
| 3 | 84 | unrun-verify | .planning/phases/84-calendar-feed-foundation-and-event-subscription/84-08-PLAN.md |  | Task 3's real-device checkpoint (real phone subscribing via iOS Calendar/Google Calendar/Outlook, calendar naming, refresh latency, stale-entry check, camera QR scan) was deferred to deployment by operator decision, not run or approved -- server-side coverage and an external RFC 5545 validator pass do not substitute for it. | open |  | 2026-09-18T13:02:54.169Z |  |
| 4 | 84 | deviation | .planning/ROADMAP.md |  | 84-08 Task 4 acceptance criterion 'grep -c ^**Plans**: 8/8 plans complete$ outputs 1' is unsatisfiable as literally written -- two other completed phases in ROADMAP.md already carry the identical string; Phase 84's own Plans line was correctly set to 8/8 plans complete, verified by content not by the raw whole-file count | open |  | 2026-09-18T13:03:05.007Z |  |
| 5 | 85 | unrun-verify | .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md |  | Relational SQL translation of the one-shot-quest predicate (seat-or-Dungeon-Master disjunction + board-type narrowing) is unproven -- the third consecutive phase to defer this gap. Every integration fact runs on the EF Core InMemory provider. Compensating manual check recorded in 85-VALIDATION.md's Manual-Only Verifications table, naming GET /feeds/calendar/{feedToken}.ics and QuestRepository.GetFeedQuestsForUserAsync. | open |  | 2026-09-18T17:11:30.295Z |  |
| 6 | 85 | deviation | QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs |  | NoPlanningOrTrackingReference_ReachedTheSourceTree fails on Linux dev environments: its ResolveRepoFile helper's existence-based repo-root walk stops early because the Linux apphost binary (bare name QuestBoard.Service, no extension) collides with the project-folder name it is looking for. Predates this phase (Phase 84, commit 78aa5286); this plan's Task 1 was explicitly instructed to leave the planning-reference guard exactly as it is, so it was not fixed here. Causes 'dotnet test' to exit 1 for the whole solution on Linux even though every fact this phase added passes individually. See deferred-items.md item 1. | open |  | 2026-09-18T17:11:39.383Z |  |

````json
[
  {
    "id": 1,
    "kind": "deviation",
    "phase": "84",
    "file": ".planning/ROADMAP.md",
    "line": null,
    "description": "84-01 Task 2 ROADMAP.md edits (Requirements line + 16 Coverage rows) computed and verified but not committed - worktree harness blocks commits touching ROADMAP.md; orchestrator must apply after wave merge (see 84-01-SUMMARY.md)",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-09-17T20:23:58.316Z",
    "resolved_at": "2026-09-18T07:10:44.090Z"
  },
  {
    "id": 2,
    "kind": "deviation",
    "phase": "84",
    "file": "QuestBoard.Service/Controllers/Admin/AccountController.cs",
    "line": null,
    "description": "Task 3 acceptance criterion 'grep -cE Request.Scheme|Request.Host outputs 0' is unsatisfiable without touching two pre-existing, unrelated Url.Action(..., Request.Scheme) call sites in ForgotPassword/Edit (email callback URLs) - out of this plan's scope, left as-is",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T09:38:07.149Z",
    "resolved_at": null
  },
  {
    "id": 3,
    "kind": "unrun-verify",
    "phase": "84",
    "file": ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-08-PLAN.md",
    "line": null,
    "description": "Task 3's real-device checkpoint (real phone subscribing via iOS Calendar/Google Calendar/Outlook, calendar naming, refresh latency, stale-entry check, camera QR scan) was deferred to deployment by operator decision, not run or approved -- server-side coverage and an external RFC 5545 validator pass do not substitute for it.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T13:02:54.169Z",
    "resolved_at": null
  },
  {
    "id": 4,
    "kind": "deviation",
    "phase": "84",
    "file": ".planning/ROADMAP.md",
    "line": null,
    "description": "84-08 Task 4 acceptance criterion 'grep -c ^**Plans**: 8/8 plans complete$ outputs 1' is unsatisfiable as literally written -- two other completed phases in ROADMAP.md already carry the identical string; Phase 84's own Plans line was correctly set to 8/8 plans complete, verified by content not by the raw whole-file count",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T13:03:05.007Z",
    "resolved_at": null
  },
  {
    "id": 5,
    "kind": "unrun-verify",
    "phase": "85",
    "file": ".planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md",
    "line": null,
    "description": "Relational SQL translation of the one-shot-quest predicate (seat-or-Dungeon-Master disjunction + board-type narrowing) is unproven -- the third consecutive phase to defer this gap. Every integration fact runs on the EF Core InMemory provider. Compensating manual check recorded in 85-VALIDATION.md's Manual-Only Verifications table, naming GET /feeds/calendar/{feedToken}.ics and QuestRepository.GetFeedQuestsForUserAsync.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T17:11:30.295Z",
    "resolved_at": null
  },
  {
    "id": 6,
    "kind": "deviation",
    "phase": "85",
    "file": "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs",
    "line": null,
    "description": "NoPlanningOrTrackingReference_ReachedTheSourceTree fails on Linux dev environments: its ResolveRepoFile helper's existence-based repo-root walk stops early because the Linux apphost binary (bare name QuestBoard.Service, no extension) collides with the project-folder name it is looking for. Predates this phase (Phase 84, commit 78aa5286); this plan's Task 1 was explicitly instructed to leave the planning-reference guard exactly as it is, so it was not fixed here. Causes 'dotnet test' to exit 1 for the whole solution on Linux even though every fact this phase added passes individually. See deferred-items.md item 1.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T17:11:39.383Z",
    "resolved_at": null
  }
]
````
