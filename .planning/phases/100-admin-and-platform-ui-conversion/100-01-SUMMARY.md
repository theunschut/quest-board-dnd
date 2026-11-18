---
phase: 100-admin-and-platform-ui-conversion
plan: 01
subsystem: review-environment
tags: [operator-checkpoint, scratch-environment, playwright, email-suppression, measured-scope]
requires: []
provides:
  - "~/qb-ui-review/phase-100 scratch root: tools (re-pointed to ports 8340/8341), credentials, operator answers, environment contract, shared-files facts, measured scope"
  - "proven capture path for /platform/Group as SuperAdmin under desktop and Pixel 7 User-Agents with the Platform phone layout selected"
affects: [100-02, 100-03, all later Phase 100 plans]
tech-stack:
  added: []
  patterns: ["Phase 99 tooling copied and re-pointed", "forced email suppression verified in /proc environ"]
key-files:
  created: []
  modified: []
decisions:
  - "Plans form a serial chain, one per wave, in this checkout (tracking commands cannot run in a worktree)"
  - "Requirement IDs for the phase are the CONTEXT decisions D-01..D-21 and DoC-1..DoC-10 (REQUIREMENTS.md maps none)"
metrics:
  duration: "about 25 minutes"
  completed: 2026-11-18
status: complete
actuals:
  tokens: 5000
  tasks: 3
  commits: 1
---

# Phase 100 Plan 01: Opening checkpoint and review environment Summary

Phase 100 review environment brought up on this Linux machine: the operator's checkpoint answers are recorded, the Phase 99 tooling is re-pointed to a phase-100 scratch root on ports 8340/8341 with email suppression forced and verified on the running process, `/platform/Group` is captured as the SuperAdmin under both User-Agents, and the measured scope and shared-file facts are written down. No repository source file changed.

## Operator confirmations (Task 1, checkpoint answered before this run)

- SQL Server up on localhost:1433 (node `net.connect` printed `ok`), ports 8340 and 8341 free (`ss -ltnp` empty), `appsettings.json` unchanged, branch `milestone/v9-rolling-improvements`. These checks were re-run at the start of this run and passed.
- The p100 fixture set (D-07) is approved; fixtures stay after the phase.
- SuperAdmin as a plain Player of one p100 board: **yes** (the SuperAdmin-target refusal can be proven live).
- SQL-backed suites in the closing sweep: **yes**; `migrator.env` copied from phase-99 (mode 600, never printed).
- All four are recorded in `~/qb-ui-review/phase-100/operator-answers.json` (mode 600).

## Task 2: scratch root, tooling, tracer

- Created `~/qb-ui-review/phase-100/{tools,state,logs,tracer,before,after,audit}`. Copied the Phase 99 tools (225 files, excluding `node_modules`, `*.bak`, `*.pre-*`, `publish-review.sh`), replaced every `phase-99` path with `phase-100`, `8320` with `8340`, `8321` with `8341`. Copied the Phase 94 admin probes (`probe320`, `probe-modal`, `probe-layout`, `probe-spacing`, `targets.py`, `fails.py`) and re-pointed their imports, fixtures path, output folder and port (they previously targeted port 8001 and Phase 94 folders).
- `tools/node_modules` is a symlink to the Phase 94 Playwright 1.64.1; nothing was installed.
- Email suppression: `run-app.mjs` in the copy already forced `EmailSettings__SuppressSending=true`, `EmailSettings__SmtpServer=127.0.0.1`, `EmailSettings__SmtpPort=1025` after any `--env` pairs, refuses unsafe pairs, keeps Development and verifies the listener's `/proc/<pid>/environ` once `/health` answers. Nothing needed restoring. Verified live: start on 8340 printed "email overrides verified in pid", and the Task 2 verify grepped `SuppressSending=true` and `SmtpServer=127.0.0.1` from `/proc/<pid>/environ` itself. `--env EmailSettings__SuppressSending=false` exits 1 without starting anything; a second run against the same tree exits 0 without a second process.
- `audit-shoot.mjs`, `sweep.mjs` and `mockup-shoot.mjs` each abort `/feeds/calendar/`, dismiss native dialogs and record failed-request URLs without query strings (confirmed by reading the copies).
- `credentials.json` holds only the `superadmin` and `admin` accounts copied from phase-99 (mode 600, never printed).
- Tracer: `/platform/Group` as `superadmin`: desktop record `status 200, mobileLayout false`; Pixel 7 record `status 200, mobileLayout true`; each folder holds one PNG and `report.json`, zero console, page or failed-request errors. Captures list real boards and stay under the scratch root.
- Verify output: first automated check printed `ok`; second printed `ok`.

## Task 3: records and docs

- `shared-files.md` written with line-numbered facts (layouts' stylesheet lists and Styles/Scripts sections, guard arrays, area tests and what each pins, measured scope, HEAD).
- `ENVIRONMENT.md` restates the contract (ports, forced email, Mailpit rule, local-only gates, real-data rule, p100 fixture rule, git rules).
- `roles-check.json`: `superadmin` GET `/platform/Group` 200, `superadmin` GET `/platform/Users` 200, `admin` GET `/Admin/Users` 200, all `pass: true` (the copied `roles-check.mjs` checks were re-pointed to these three).
- `tools/counts.py` re-pointed to this phase's area; `counts-start.json` holds the per-file start data.

## Shared-file facts (one line each)

- Desktop Platform layout links app, tokens, base, components, shell, legacy-pages then `Styles` (lines 10-16) and renders `Scripts` at line 66; the phone layout links app, tokens, base, components, shell, shell.mobile, mobile then `Styles` (lines 10-17) and `Scripts` at line 77.
- `UiFoundationLayerGuardTests.LayeredFiles` (lines 54-103) and `NoLiteralFiles` (106-107) end at `shop-pages.css`, `shop-pages.mobile.css`, `shop-form.css`; no `admin-*` or `platform-*` sheet is in either.
- `MobileViewsTests` Admin entries (1424-1535) and `QuestStatusBoardClockTests` (line 120) pin the classes `admin-users-card-mobile`, `admin-form-card-mobile`, `admin-quests-card-mobile` and the sheets `admin-users.mobile.css`, `admin-form.mobile.css`, `admin-quests.mobile.css`.
- `UsersControllerIntegrationTests` (310-311) pins `platform-users-boards.mobile.css` and `platform-users.mobile.css`; `LinkedBoardsPlatformIntegrationTests` (598, 610-611) pins `platform-group-card-mobile` and `platform-linked-boards.mobile.css`.
- `PlatformBoardDeleteTests` (390-391) pins the absence of `platform-group-card-mobile` and `platform-group.mobile.css` on the delete page; `BoardDeletionStaticGuardTests` (38-261) pins the Delete pair, the three delete/boards sheets and their layering, no `!important`, no colour literal and no media query on the phone sheets.
- `BoardPageStaticGuardTests` (48-147, 163) scans Platform Group Edit/Index views and `platform-group.mobile.css` for planning references with a presence floor of 50 files; `PlatformAreaLayoutTests` (14-35) pins that the desktop Platform layout links components, tokens, base and app.

## Measured scope and drift against the ROADMAP

- 26 view files (11 under Views/Admin, 11 under Platform Group including `_BoardDeleteBody.cshtml`, 2 LinkedBoards, 2 Platform Users) against the ROADMAP's 25; nine sheets against its six (the three extra are `platform-group-delete.css`, `platform-group-delete.mobile.css`, `platform-users-boards.mobile.css`, already layered and clean).
- Start totals: hex 6, `rgb(` 57, `!important` 28, utility-colour rules 5; inline census `style=` 1, `<style` 0, `<script` without src 4, `on*=` handlers 12. The six legacy phone sheets are not yet layered.
- `modern-card.css` and `site.css` do not exist.

## Roles checked

The reused SuperAdmin renders `/platform/Group` and `/platform/Users`; the reused Audit Board Admin renders `/Admin/Users` (all 200).

## Visible planning choices (carried from the plan)

- Requirement IDs are the CONTEXT decisions D-01..D-21 and DoC-1..DoC-10 because REQUIREMENTS.md maps no Phase 100 IDs.
- Serial plan chain, one plan per wave in this checkout; several plans are large, as Phase 99's were.
- No schema change; no assumption-delta; the API-coverage checkpoint is answered by COVERAGE.md.

## Deviations from Plan

None - plan executed exactly as written. Two small notes, neither a deviation: the Phase 94 probes also needed their port (8001), fixtures path and output folder re-pointed (the plan only named root and ports), and `counts.py` carried Phase 99's area lists so it was re-pointed to this phase's 26 views and nine sheets. Task 2 produced no repository change and therefore no per-task commit; the only commit is the docs commit.

## Authentication Gates

None.

## Known Stubs

None (no repository file created or modified).

## Threat Flags

None.

## Task Commits

- Task 1: operator checkpoint, answered before this run (no commit).
- Task 2: scratch environment outside the repository (no commit).
- Task 3: docs commit carrying this SUMMARY, STATE.md and ROADMAP.md.

## Self-Check: PASSED

Scratch artifacts present (ENVIRONMENT.md, shared-files.md, roles-check.json, operator-answers.json, credentials.json and migrator.env at mode 600, both tracer reports); repository source trees unchanged; the app started on 8340 was stopped with the copied stop-app.mjs at the end (nothing listens on 8340 or 8341).
