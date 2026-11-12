---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 01
subsystem: review-environment
tags: [ui-conversion, playwright, scratch-environment, calendar]
requires: []
provides:
  - "~/qb-ui-review/phase-97 scratch root with re-pointed tooling (ports 8300/8301)"
  - "tracer capture of /Calendar under desktop and Pixel 7 user agents"
  - "roles-check.json, ENVIRONMENT.md, phase96-landed.md (provisional)"
affects: [97-02, 97-05]
tech-stack:
  added: []
  patterns: ["Playwright 1.64.1 reused through a node_modules symlink; state files under state/<port>/"]
key-files:
  created: []
  modified: []
decisions:
  - "Ports 8000/8001 are phase 96's; phase 97 uses 8300 (working tree) and 8301 (frozen baseline)"
  - "Review refs are not pushed; Gate 1 is reviewed from local files"
metrics:
  duration: "about 25 minutes"
  completed: 2026-11-12
status: complete
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 97 Plan 01: Review environment and Calendar tracer Summary

Phase 97's scratch environment at `~/qb-ui-review/phase-97` is built (tooling re-pointed to ports 8300/8301), and the Calendar
page renders as the player under both the desktop and the Pixel 7 user agents with the real phone layout selected. No
repository source file was changed.

## Task 1: operator checkpoint, answered by the orchestrator

The orchestrator's binding overrides replaced the operator's resume message. Recorded answers:

- Phase 96 runs in parallel by design in another checkout (apps on 8000/8001, which this phase never touches). This worktree
  has its own bin/obj. Ports 8300 and 8301 are free. The 96-12 SUMMARY check and the 8000/8001-free check were skipped.
- Audit Board (id 7) and the qb-audit-* accounts are reused; credentials copied from phase-96, never printed.
- Review pushes are NOT approved. Gate 1 is built locally under `~/qb-ui-review/phase-97/gate1/` and reviewed from local files.
  `feature/phase-97-gate1-review` is not created and not pushed (no update-ref, no push).
- Post-resume checks adapted and passed: ports 8300/8301 free (`ss`), localhost:1433 answers `ok` (node net probe),
  `appsettings.json` unmodified (`git status --porcelain` empty), branch `feature/phase-97`.

## Task 2: scratch root, tooling, tracer (tracer verified end to end)

- `tools/` copied from `~/qb-ui-review/phase-95/tools` (87 files). `run-app`, `stop-app`, `login`, `audit-shoot` and `contrast`
  were taken from the phase-96 revisions, because the plan's contract (`--restart`, `--env`, `/feeds/calendar/` abort, dialog
  suppression, `state/<port>/` layout, `--board <name>`) only exists there; `mockup-shoot.mjs`, `build-gate1.py` and
  `overflow-compare.mjs` were copied as read-only starting points. Every `phase-95`/`phase-96` path became `phase-97`,
  every `8000` became `8300`, every `8001` became `8301` (tool defaults included).
- `tools/node_modules` is a symlink to the phase-94 copy; nothing was installed. `credentials.json` is mode 600.
- App started on 8300; a second `run-app` call exits 0 without a second process.
- Captures: `/Calendar` as `player`, mobile 200 with `mobileLayout=true`, desktop 200 with `mobileLayout=false`, no overflow,
  no console errors. Both PNGs and `report.json` are in `tracer/{desktop,mobile}/`.

## Task 3: handover facts, environment note, roles

`phase96-landed.md` is marked PROVISIONAL (phase 96 has not landed here) and must be re-recorded after the merge. Facts,
one line per shared file:

- shell.mobile.css: no phone `.row` gutter rule at all (only page-scoped resets in move-entry.css and quest-log.css), so 97-08 must APPLY one to `Events/Details` (bare `<div class="row">` at line 42), not just verify.
- _Layout.cshtml: global sheets lines 18-32; `calendar.css`, `events-overview.css`, `agenda.css` still global at 25-27; `legacy-pages.css` remains at 24.
- UiFoundationLayerGuardTests.cs: `LayeredFiles` lines 54-75 ends `quest-log.mobile.css`, `move-entry.css`, `markdown-content.css`, `markdown-editor.css`; `NoLiteralFiles` (79-80) ends `move-entry.css`, `markdown-content.css`, `markdown-editor.css`; no account/picker/calendar/agenda/events sheet in either.
- MobileViewsTests.cs: ten `MobileCalendar_*` facts (lines 300-407 and 1370-1503); no Agenda-page or Events-page fact.
- components.css: `.avail-card` 88 (only a colour override, no other `.avail-*`), `.row-nav-link` 634, `.move-control-reason` 649, `.cycle-mask-cell` 661, `.empty-state` 706 (+ icon/heading/text); nothing added by phase 96.
- HEAD: 2788fee2 on feature/phase-97 (includes v6.0.1 and v6.0.2 c781090c).

Every role in credentials.json is logged in on 8300 (states under `state/8300/`: superadmin, admin, player, dm, player2,
player3, dm2, multi, noboard). `roles-check.json`, six checks, all pass: player `/Calendar` 200; player `/Events/Create` ends
on `/Account/AccessDenied`; dm `/Events/Create` 200 with form; admin `/Admin/Users` 200; superadmin `/platform/Group` 200;
noboard `/Agenda` 200. Only logins were performed; no memberships, passwords, boards or profile data were changed.
`tools/roles-check.mjs` was rewritten for this area's checks.

Visible planning choice kept: no SPEC.md exists and the spec-less probe fallback was skipped because REQUIREMENTS.md carries
no requirement IDs for Phase 97; CONTEXT decisions D-01..D-17 and DoC-1..DoC-10 serve as the requirement IDs.

The 8300 app was stopped at the end with `stop-app.mjs 8300` (97-02 restarts it).

## Deviations from Plan

**1. [Rule 3 - Blocking] Core tools overlaid from phase-96 revisions.** The plan says copy from phase-95, but its own
contract (feed abort, dialog suppression, `state/<port>/`, `--restart`) lives only in the phase-96 versions. Overlaid
`run-app`, `stop-app`, `login`, `audit-shoot`, `contrast` from phase-96 before re-pointing.

**2. [Orchestrator override] Ports 8300/8301, no review-ref push, provisional phase96-landed.md, Task 1 answered.**
All per ORCHESTRATOR-OVERRIDES.md; nothing else departed from the plan.

**3. Minor process note.** One `ss -ltnp` listing was run that also matched ports 8000/8001 (read only, nothing touched).

Stubs: none. Threat flags: none. Auth gates: none.

## Self-Check: PASSED

Scratch artifacts exist (ENVIRONMENT.md, phase96-landed.md, roles-check.json, credentials.json mode 600, tracer captures,
state/8300 for nine roles); automated verifies for Tasks 2 and 3 printed `ok`; `git status --porcelain` empty before this file.
