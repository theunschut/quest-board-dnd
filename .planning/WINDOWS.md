---
schema_version: 1
open_count: 7
waived_count: 1
fixed_count: 2
total_count: 10
last_updated: 2026-11-01T17:43:22.717Z
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
| 5 | 85 | unrun-verify | .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md |  | Relational SQL translation of the one-shot-quest predicate (seat-or-Dungeon-Master disjunction + board-type narrowing) has no automated relational coverage -- the third consecutive phase to defer this gap. Every integration fact runs on the EF Core InMemory provider. UPDATE 2026-09-18: the compensating manual check WAS performed against real SQL Server 2022 on the real dev dataset -- the predicate translated and executed relationally, no client-side-evaluation exception, and the served quest-id set matched an independently written SQL query exactly; evidence in 85-VALIDATION.md 'Manual verification performed'. This stays OPEN because the deferred item is relational test INFRASTRUCTURE (a SQL Server test fixture CI can run), not a single hand-run observation of one query shape. | open |  | 2026-09-18T17:11:30.295Z |  |
| 6 | 85 | deviation | QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs |  | NoPlanningOrTrackingReference_ReachedTheSourceTree fails on Linux dev environments: its ResolveRepoFile helper's existence-based repo-root walk stops early because the Linux apphost binary (bare name QuestBoard.Service, no extension) collides with the project-folder name it is looking for. Predates this phase (Phase 84, commit 78aa5286); this plan's Task 1 was explicitly instructed to leave the planning-reference guard exactly as it is, so it was not fixed here. Causes 'dotnet test' to exit 1 for the whole solution on Linux even though every fact this phase added passes individually. See deferred-items.md item 1. | fixed |  | 2026-09-18T17:11:39.383Z | 2026-09-18T18:46:48.793Z |
| 7 | 94 | deviation | QuestBoard.Service/wwwroot/lib/cinzel/cinzel-latin-wght.woff2 |  | 94-11 Task 2 zero-difference gate printed FAIL: all 7 desktop gate pages 0 px, all 7 mobile pages 4.5k-9.5k px confined to the Cinzel brand/heading edges. Google Fonts serves an Android UA a differently hinted build of the same Cinzel than a desktop UA; the self-hosted files are the desktop-UA build. Orchestrator reviewed the overlay at 3x: identical glyph shapes and weight, edge anti-aliasing plus a sub-pixel advance shift only, brand box unchanged 198x46 CSS px - judged not visible and accepted. Strict-zero alternative (both builds, selected by @media (hover:none) and (pointer:coarse), +~40 KB) not taken. Revisit only if the user objects; commit 0a5604ac. | open |  | 2026-11-01T14:12:21.045Z |  |
| 8 | 94 | deviation | ~/qb-ui-review/phase-94/tools/probes.mjs (scratch) |  | 94-16 found the shared contrast probe flattens a multi-layer glass background and orders the colour above its own image, giving 134 false failures on Direction A through audit-shoot --probes. 94-16 used a corrected package-local copy (mockups/package/probe-lib.mjs). The wave-3 area audits scored legacy glass with the shared probe, so any S1/S2 contrast finding measured on a multi-layer glass background may be a false positive; re-check those before an area phase acts on them. | open |  | 2026-11-01T15:35:45.120Z |  |
| 9 | 94 | deviation | QuestBoard.Service/Views/Calendar/Index.cshtml |  | Calendar month view (Events area phase): operator-accepted trade-off of the fit-without-scrolling requirement - compact entry rows and the '+N more' button are 20px tall, under the 24px WCAG 2.5.8 target-size minimum; titles truncate at about 13-16 characters (full title in the hover/focus card); footer sits below the fold. Meeting 2.5.8 would cost one visible entry per day cell. Recorded in the UI-SPEC G1 sign-off record (G1-O3). | waived | Operator decision 2026-11-01: fitting the whole month on one screen takes priority over the 24px target size; accepted after trying the calendar-fit mockup | 2026-11-01T17:34:27.324Z | 2026-11-01T17:34:27.469Z |
| 10 | 94 | unmet-truth | QuestBoard.Service/wwwroot/css/tokens.css |  | Signed-off status text colours (success, danger, warning, info) measure 4.23-4.31:1 on HOVERED glass at B-max (~4.46 at B-99), under 4.5:1; the G1 sign-off never scored status text on the hover surface. 94-18 kept the signed-off values and the token-contrast guard lists these four pairs as an explicit, reasoned exclusion. Resolve in the component layer (94-20): prefer easing the hover glass tint so status text reaches 4.5:1 without changing the signed-off status colours; otherwise lighten the status text tokens minimally (e.g. success ~#8ce4b0 = 4.62) and re-score. Then remove the guard exclusion. | open |  | 2026-11-01T17:43:22.717Z |  |

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
    "description": "Relational SQL translation of the one-shot-quest predicate (seat-or-Dungeon-Master disjunction + board-type narrowing) has no automated relational coverage -- the third consecutive phase to defer this gap. Every integration fact runs on the EF Core InMemory provider. UPDATE 2026-09-18: the compensating manual check WAS performed against real SQL Server 2022 on the real dev dataset -- the predicate translated and executed relationally, no client-side-evaluation exception, and the served quest-id set matched an independently written SQL query exactly; evidence in 85-VALIDATION.md 'Manual verification performed'. This stays OPEN because the deferred item is relational test INFRASTRUCTURE (a SQL Server test fixture CI can run), not a single hand-run observation of one query shape.",
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
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-09-18T17:11:39.383Z",
    "resolved_at": "2026-09-18T18:46:48.793Z"
  },
  {
    "id": 7,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/lib/cinzel/cinzel-latin-wght.woff2",
    "line": null,
    "description": "94-11 Task 2 zero-difference gate printed FAIL: all 7 desktop gate pages 0 px, all 7 mobile pages 4.5k-9.5k px confined to the Cinzel brand/heading edges. Google Fonts serves an Android UA a differently hinted build of the same Cinzel than a desktop UA; the self-hosted files are the desktop-UA build. Orchestrator reviewed the overlay at 3x: identical glyph shapes and weight, edge anti-aliasing plus a sub-pixel advance shift only, brand box unchanged 198x46 CSS px - judged not visible and accepted. Strict-zero alternative (both builds, selected by @media (hover:none) and (pointer:coarse), +~40 KB) not taken. Revisit only if the user objects; commit 0a5604ac.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T14:12:21.045Z",
    "resolved_at": null
  },
  {
    "id": 8,
    "kind": "deviation",
    "phase": "94",
    "file": "~/qb-ui-review/phase-94/tools/probes.mjs (scratch)",
    "line": null,
    "description": "94-16 found the shared contrast probe flattens a multi-layer glass background and orders the colour above its own image, giving 134 false failures on Direction A through audit-shoot --probes. 94-16 used a corrected package-local copy (mockups/package/probe-lib.mjs). The wave-3 area audits scored legacy glass with the shared probe, so any S1/S2 contrast finding measured on a multi-layer glass background may be a false positive; re-check those before an area phase acts on them.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T15:35:45.120Z",
    "resolved_at": null
  },
  {
    "id": 9,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/Views/Calendar/Index.cshtml",
    "line": null,
    "description": "Calendar month view (Events area phase): operator-accepted trade-off of the fit-without-scrolling requirement - compact entry rows and the '+N more' button are 20px tall, under the 24px WCAG 2.5.8 target-size minimum; titles truncate at about 13-16 characters (full title in the hover/focus card); footer sits below the fold. Meeting 2.5.8 would cost one visible entry per day cell. Recorded in the UI-SPEC G1 sign-off record (G1-O3).",
    "status": "waived",
    "reason": "Operator decision 2026-11-01: fitting the whole month on one screen takes priority over the 24px target size; accepted after trying the calendar-fit mockup",
    "recorded_at": "2026-11-01T17:34:27.324Z",
    "resolved_at": "2026-11-01T17:34:27.469Z"
  },
  {
    "id": 10,
    "kind": "unmet-truth",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/tokens.css",
    "line": null,
    "description": "Signed-off status text colours (success, danger, warning, info) measure 4.23-4.31:1 on HOVERED glass at B-max (~4.46 at B-99), under 4.5:1; the G1 sign-off never scored status text on the hover surface. 94-18 kept the signed-off values and the token-contrast guard lists these four pairs as an explicit, reasoned exclusion. Resolve in the component layer (94-20): prefer easing the hover glass tint so status text reaches 4.5:1 without changing the signed-off status colours; otherwise lighten the status text tokens minimally (e.g. success ~#8ce4b0 = 4.62) and re-score. Then remove the guard exclusion.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T17:43:22.717Z",
    "resolved_at": null
  }
]
````
