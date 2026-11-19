---
phase: 103-themed-error-pages
plan: 01
subsystem: tooling
tags: [operator-checkpoint, playwright, environment, error-pages]

requires:
  - phase: 100-admin-and-platform-ui-conversion
    provides: finished tree and the phase 100 capture toolset this plan copies
provides:
  - scratch root ~/qb-ui-review/phase-103 with re-pointed tools, ports 8350/8351 and forced email suppression
  - operator answers recorded (operator-answers.json and the CONTEXT addendum)
  - overlap anchors re-read from phase 100's final tree (shared-files.md)
  - anonymous capture of Account/Login and today's bare 404 under both User-Agents
affects: [103-02, 103-03, 103-04, 103-05, 103-06, 103-07, 103-13, 103-15, 103-16]

actuals:
  tokens: 3500
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "audit-shoot.mjs anonymous mode (--role anon), comma-separated --routes and a per-record bodyLength"

key-files:
  created:
    - .planning/phases/103-themed-error-pages/103-01-SUMMARY.md
  modified:
    - .planning/phases/103-themed-error-pages/103-CONTEXT.md
    - .planning/STATE.md
    - .planning/ROADMAP.md

key-decisions:
  - "logScopes=env: Logging__Console__IncludeScopes=true on CT 102, no code change (amends D-12 and D-13)"
  - "accessDeniedStatus=403; pageGateCounts=yes; superAdminAsMember=yes; contactToggle=dm-tier (settled)"
  - "preGate1Structural=yes: 103-04 to 103-09 may ship the structural error page with shared classes only before Gate 1"

patterns-established:
  - "Phase scratch root copied from the previous phase and re-pointed by path and port, node_modules symlinked to phase 94"

requirements-completed: [D-01, D-04, D-05, D-08, D-11, D-12, D-13, D-19]

duration: 55min
completed: 2026-11-19
status: complete
---

# Phase 103 Plan 01: Opening operator checkpoint Summary

**Phase 103 opened on a finished phase 100: operator answers recorded, a phase-103 scratch root with forced email suppression, an anonymous tracer proving the phone and desktop capture paths and today's bare 404, and every overlap anchor re-read from the final tree.**

## Performance

- **Duration:** about 55 min
- **Tasks:** 3 (one operator checkpoint answered in advance, one tracer, one docs task)
- **Commits:** 1 (docs). No repository source file changed.

## Accomplishments

- Phase 100 confirmed finished in this checkout: `100-23-SUMMARY.md` exists, ROADMAP lists no unticked 100-NN plan, `docs(100-23)` is in the history, STATE.md showed current_phase 101 before this plan began phase 103. SQL Server answered on 1433 (started by the operator's authorisation, not by this plan), 8350 and 8351 were free, `appsettings.json` untouched, branch `milestone/v9-rolling-improvements`.
- Scratch root `~/qb-ui-review/phase-103/` built from the phase 100 tools (269 files, excluding `node_modules`, backups and `publish-review.sh`), every `phase-100` path and port 8340/8341 re-pointed to phase-103 and 8350/8351, `tools/node_modules` a symlink to phase 94's. `run-app.mjs` already forced Development, `EmailSettings__SuppressSending=true`, SMTP 127.0.0.1:1025: refusing `--env EmailSettings__SuppressSending=false` exits 1 without starting anything, a second run on 8350 exits 0 without starting a second process, and the running listener's `/proc/<pid>/environ` carries the forced values.
- Tracer verified end to end: Account/Login answers 200 with `mobileLayout=true` under the Pixel 7 User-Agent and `false` under the desktop one; `/no-such-page` answers 404 with `bodyLength` 0 under both. No member data in these captures.
- Overlap anchors re-read after phase 100 (see below and `shared-files.md`).

## Operator answers and their sources

| Key | Value | Source |
|-----|-------|--------|
| phase100Complete | true | operator (verified by the orchestrator) |
| fixturesApproved | true | operator |
| superAdminAsMember | yes | operator |
| pageGateCounts | yes | operator |
| accessDeniedStatus | 403 | operator |
| contactToggle | dm-tier | settled |
| logScopes | env | operator |
| preGate1Structural | yes | operator |
| journalLine | recorded (after line only) | operator |

The answers are in `~/qb-ui-review/phase-103/operator-answers.json` and in `103-CONTEXT.md` under `## Operator answers at execution` (no D-numbered bullet). contactToggle is settled as dm-tier for research's reason: a Dungeon Master or Admin of the target board is entitled to know hidden contacts exist, the per-session show-hidden toggle only filters their own list, and modelling it would put session state into the Domain resolver.

## The logging deviation, in plain words

D-12 says the 500 page's Reference is the id the exception handler already logs, and D-13 says no new logging. With the default console format, though, the handler's log line carries no request id (research finding F2), so a member quoting the Reference could not be found in `journalctl -u questboard`. The operator chose **env**: `Logging__Console__IncludeScopes=true`, operator-controlled configuration with no code change. It works only where someone sets it: CT 102 (done) and the Docker self-host path only if its operator sets it too; docs/server-setup.md must document the variable. This amends D-12 and D-13 and is recorded in the CONTEXT addendum.

**Applied and verified.** The operator added the systemd drop-in `/etc/systemd/system/questboard.service.d/20-log-scopes.conf` on CT 102 on 2026-11-19. A read-only SSH check by the orchestrator found the drop-in present, `systemctl show` reporting `Environment=Logging__Console__IncludeScopes=true`, the service restarted at 15:36:05 CET on releases/6.5.0, and `/` returning 200.

**After line from the CT 102 journal** (`journalctl -u questboard -n 5`, 15:36:19), recorded as `journalLine`:

```
=> SpanId:746b05e4a4eacbaa, TraceId:3ebbb0e0819dd3e26edc0d5e72c4aa73, ParentId:0000000000000000 => ConnectionId:0HNQ699SQF8IS => RequestPath:/quests RequestId:0HNQ699SQF8IS:00000001
```

There is no "before" line: the restart happened before anyone captured one. The journal now carries `RequestId`, which equals `HttpContext.TraceIdentifier`, the value the 500 page's Reference will show per D-12, so a member's quoted Reference can be found by grepping the journal for it.

## Shared-file facts (full list with line numbers in `shared-files.md`, HEAD 2f0d30f8)

- `Program.cs`: `AccessDeniedPath` line 100, `OnRejected` line 165, not-Development block line 345 with `UseExceptionHandler("/Error")` line 347, `UseStaticFiles()` line 352. No logging configuration (none expected, none found).
- `UiFoundationLayerGuardTests`: `Layouts` lines 40-46 (last: Platform.Mobile), last `LayeredFiles` entry line 106, `NoLiteralFiles` line 110 (both end with `platform-pages.mobile.css`); every layout must still link a legacy sheet (line 306), so the new error layout must link a non-foundation sheet or stay out of `Layouts`.
- `AccountConversionGuardTests`: AccessDenied in `ConvertedViews` (line 26), `SheetLinks` (line 54) and the whole-area fact (line 414). `AccountControllerIntegrationTests` is under `Controllers/`, and its AccessDenied fact asserts OK at line 33 and flips to Forbidden.
- Registry: 18 routes across 8 lookup kinds. `GroupSessionMiddleware.ExemptPathPrefixes` still exempts `/Error` (line 69).
- JS fetch callers: the same 12 call sites as research's audit table; phase 100 removed and added none; none sets `Accept` or `X-Requested-With`.
- Platform layouts still link `legacy-pages.css` (desktop) and `shell.mobile.css` plus `mobile.css` (phone); `_Layout.GroupPicker.cshtml` links `legacy-pages.css`.
- Read-only for this phase, with blob hashes at HEAD recorded: `components.css`, `shell.css`, `shell.mobile.css`, both layouts, both Platform layouts, `GroupService.cs`, `GroupRepository.cs` (it sits at `QuestBoard.Repository/GroupRepository.cs`), the calendar feed files, `appsettings.json`.

## Visible planning record

- REQUIREMENTS.md maps no phase 103 IDs, so CONTEXT decisions D-01..D-19 serve as the requirement IDs of every plan.
- spec-less probe skipped: no requirement IDs.
- The schema gate found no schema change (no entity, migration or column); the assumption-delta detector did not fire; no external API is integrated.
- The 18 plans form a strict serial chain, one plan per wave, because the tracking commands run in this checkout and most plans share files.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `audit-shoot.mjs` lacked the anonymous role alias and a comma-separated route list**
- **Found during:** Task 2
- **Issue:** the copied tool accepted `--role none` only and treated `--routes /a,/b` as one path, so the plan's verify command navigated to `/Account/Login,/no-such-page`.
- **Fix:** `--role anon` is an alias of `none`, `--routes` splits plain paths on commas, and each record carries `bodyLength` (response body length as received). Scratch tooling only, outside the repository.
- **Files modified:** `~/qb-ui-review/phase-103/tools/audit-shoot.mjs`
- **Commit:** none (outside the repository)

**2. [Rule 1 - Bug] The plan names `GroupRepository.cs` under `Repositories/`**
- **Issue:** the file is `QuestBoard.Repository/GroupRepository.cs`.
- **Fix:** the correct path and blob hash are in `shared-files.md`.

Otherwise the plan executed as written. No authentication gate occurred.

## Known Stubs

None.

## Threat Flags

None. No repository source was created or changed.

## Left running

The working-tree app started by `run-app.mjs` on port 8350 (log `~/qb-ui-review/phase-103/logs/app-8350.log`, pid file `app-8350.pid`) is still running for the next plans. Nothing was started on 8351. SQL Server and `mssql-dev` were started by the orchestrator, not by this plan.

## Self-Check: PASSED

- Files: `operator-answers.json`, `shared-files.md`, `ENVIRONMENT.md`, `credentials.json` (mode 600), `tracer/mobile/report.json`, `tracer/desktop/report.json` all present under `~/qb-ui-review/phase-103/`.
- No repository source files changed (`git status` clean for Service, Domain, Repository and both test projects).
