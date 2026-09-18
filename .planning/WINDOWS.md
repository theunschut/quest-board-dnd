---
schema_version: 1
open_count: 1
waived_count: 0
fixed_count: 1
total_count: 2
last_updated: 2026-09-18T09:38:07.149Z
---

# Broken Windows Ledger

> Cross-phase defect register. With `workflow.windows_enforce` enabled, `/gsd-ship` blocks while `open_count > 0`.
> Waive with `gsd-tools windows waive <id> "<reason>"` (reason required).
> Mark fixed with `gsd-tools windows fixed <id>`.

| id | phase | kind | file | line | description | status | reason | recorded_at | resolved_at |
|----|-------|------|------|------|-------------|--------|--------|-------------|-------------|
| 1 | 84 | deviation | .planning/ROADMAP.md |  | 84-01 Task 2 ROADMAP.md edits (Requirements line + 16 Coverage rows) computed and verified but not committed - worktree harness blocks commits touching ROADMAP.md; orchestrator must apply after wave merge (see 84-01-SUMMARY.md) | fixed |  | 2026-09-17T20:23:58.316Z | 2026-09-18T07:10:44.090Z |
| 2 | 84 | deviation | QuestBoard.Service/Controllers/Admin/AccountController.cs |  | Task 3 acceptance criterion 'grep -cE Request.Scheme\|Request.Host outputs 0' is unsatisfiable without touching two pre-existing, unrelated Url.Action(..., Request.Scheme) call sites in ForgotPassword/Edit (email callback URLs) - out of this plan's scope, left as-is | open |  | 2026-09-18T09:38:07.149Z |  |

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
    "resolved_at": null,
    "milestone": "v9.0"
  }
]
````
