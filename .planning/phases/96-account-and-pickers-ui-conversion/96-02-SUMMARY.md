---
phase: 96-account-and-pickers-ui-conversion
plan: 02
subsystem: review-environment
tags: [playwright, scratch-tooling, fixtures, manifest, probes, before-capture, frozen-baseline]
requires:
  - phase: 96-01
    provides: scratch root ~/qb-ui-review/phase-96, nine review role states for port 8000, working-tree app on 8000
provides:
  - fixture states on the review boards (9 keys, none unavailable) made through the app's own forms
  - 28-row route manifest (25 phase, 3 foreign) addressed by stable id
  - probe set (text contrast, non-text control contrast, overflow, text-spacing overflow, small targets, focus rings, controls by role and name), sweep, readability-compare (with --zero-contrast and exceptions) and diff tools
  - frozen baseline of the start-of-plan commit on port 8001
  - BEFORE record for every non-afterOnly manifest row under both User-Agents (114 records)
affects: [96-03, all later plans in phase 96]
tech-stack:
  added: []
  patterns: [fixtures through the app's forms with name lookup before create and created:true kept across re-runs, manifest rows with actions/asserts/measure/expectMobileLayout/afterOnly, per-record probes keyed by id x UA x width, residual-contrast gate with an exceptions file, rate-limited POST rows split over two loopback addresses]
key-files:
  created: []
  modified: []
key-decisions:
  - "No repository file changes: everything lives under ~/qb-ui-review/phase-96 (outside the repo)"
  - "The existing Phase 94 event (Events row) is reused instead of creating one, so this plan writes no Events or Quests row at all"
  - "Rate-limited error rows (forgot-errors, setpassword-errors) send the desktop UA through 127.0.0.1 and the phone UA through [::1] so a full sweep stays inside the 3-per-15-minutes limit"
  - "Control inventory keys carry role and accessible name only, never an href or a value, so a calendar subscription address cannot reach a report"
requirements-completed: [D-04, D-05, DoC-5, DoC-6, DoC-7]
duration: ~1.5 h
completed: 2026-11-12
status: complete
actuals:
  tokens: 25200
  tasks: 3
  commits: 1
---

# Phase 96 Plan 02: Fixtures, manifest, probes, frozen baseline and BEFORE capture Summary

The account and picker conversion now has a complete review harness: every capture state exists as a fixture made through the app's own forms, a 28-row manifest addresses each page state by id, the probes and comparison tools are in place, a frozen copy of the start-of-plan commit answers on port 8001, and a 114-record BEFORE capture covers every row under the desktop and the phone (touch) User-Agent, re-captured on this machine. The repository source is unchanged and no task commit was needed (the only commit is the docs commit for this summary and the state files).

All paths are under `~/qb-ui-review/phase-96` (`QBR`).

## Task 1 (tracer): multi-board picker end to end (verify printed `ok`)

`fixtures.mjs` looks every name up before creating it and drives only the app's forms as the review accounts (antiforgery posts, exact-email search on the Members page so no other member is touched, no SQL). Boards created as `superadmin` through Platform > Group > Create with the review board's type (One-Shot): **Ashenford Watch** (id 13) and **The Grand Convocation of Ashenford Wardens and Hollow Queens** (id 14, name length asserted to be exactly 60 before creation). `multi` was added as Player to both through Platform > Group > Members > Add, so it is a member of Audit Board, Ashenford Watch and the 60-character board and the picker shows three rows. Second run of `node tools/fixtures.mjs`: created this run 0.

`contrast.mjs`, `probes.mjs` (loaded by `sweep.mjs`), `manifest.mjs` and `sweep.mjs` were copied from phase 95 and extended: `textSpacingOverflow`, `controlBorders[]` (border against fill, fill against surface, border against surface, `meets` at the 3:1 bar, composited with the ancestor and board-reference method, raster surfaces sampled from pixels), control keys with no href or value. The first sweep recorded `picker-multi` under both User-Agents (4 records), all probe fields present.

## Task 2: remaining fixtures, comparison tools, frozen baseline (verify printed `ok`)

| Key | Form used | Created |
|-----|-----------|---------|
| `board.second` (Ashenford Watch, id 13), `board.long` (60 characters, id 14) | Platform > Group > Create as `superadmin` | yes |
| `membership.multi` | Platform > Group > Members > Add (Player) on both new boards | yes |
| `membership.superadmin` | Platform > Group > Members > Add (Player) on Ashenford Watch | yes |
| `membership.noboard` | Platform > Group > Members > Remove on the Audit Board, then `login.mjs noboard` re-run; the picker shows the no-groups alert (0 board rows) | yes (the removal) |
| `profile.one` | Add Subscription once as `player` on /Account/Profile | yes |
| `profile.three` | Add Subscription three times as `multi` (11 seconds apart), second row renamed through the Rename dialog to a 60-character name | yes |
| `profile.ten` | Add Subscription ten times as `dm` (11 seconds apart) | yes |
| `event.single` (id 2) | Reused: the Phase 94 event, found by title on Events as `dm` | no |

Subscription rows were counted by delete form on the Profile page and names read only from the Rename trigger's own name attribute; no address was read, recorded or fetched. `profile-empty` uses `admin`, which has no subscription (checked, 0).

**Fixture writes to Events or Quests:** none. `event.single` (id 2, "/Events/Details/2") is the Phase 94 event that already existed on the Audit Board, found by title through Events and reused; it is the phase's one Events row, a declared review fixture under D-05 and Phase 94 D-25 (its `fixtures.json` entry carries `declared: review fixture (D-05, Phase 94 D-25)` and `reusedFrom: phase-94`), not a code write. Events/Create was not used because the lookup-before-create rule found the row, and no quest was written.

Tools: `readability-compare.py` (new contrast failure, missing control, new overflow, new text-spacing overflow, new small target, new focus-ring miss, new console/page/failed request, new assert failure, new non-text control-border failure; `--zero-contrast` and `--exceptions`), `diff.py` (self-diff 0 differing pixels), `contrast-exceptions.json` (`{ "entries": [] }`). BEFORE against itself prints `ok`.

**Frozen baseline:** `baseline-src/` holds `git archive` of the commit that was HEAD at the start of this plan, **BASELINE_SHA `e84ca9e078f2bf5771871665ae3f3fd811a2f54e`**, served on port 8001 (`/health` 200). `run-app.mjs` against the repository tree on 8001 refuses (exit 1: expected the repo tree, actual the baseline copy). The working tree runs on 8000.

## Task 3: manifest and BEFORE capture (both verifies passed)

Manifest ids (28 rows; 25 phase, 3 foreign; `profile-one-delete` is `afterOnly` and has no BEFORE record):
`picker-multi`, `picker-superadmin`, `picker-noboard`, `picker-keyboard`, `login`, `login-errors`, `login-invalid`, `forgot`, `forgot-errors`, `setpassword`, `setpassword-errors`, `changepassword`, `changepassword-errors`, `edit`, `edit-errors`, `profile-empty`, `profile-one`, `profile-three`, `profile-ten`, `profile-one-qr`, `profile-one-rename`, `profile-one-delete` (afterOnly), `accessdenied-in`, `accessdenied-out`, `home`; foreign: `fx-events-details`, `fx-row-events-details`, `fx-row-shared-accessdenied`.

Foreign `fx-row-*` views: `tools/find-row-views.py` lists every view rendered under the phone layout (each `.Mobile.cshtml` and each view without a twin) whose content root is a `div.row`. It finds exactly two: `Events/Details` (an unindented page-level row after a conditional breadcrumb) and `Shared/AccessDenied`. Both are captured under the `fx-row-*` names (same pages as `fx-events-details` and `accessdenied-in`), at phone Pixel 7 and 390 and desktop 320.

`before/report.json` has 114 records (27 non-afterOnly rows), all status 200. Widths: desktop 1440 and 320, phone 412 (Pixel 7) and 390; the populated Profile rows also desktop 1280, 1024 and 768. The picker rows carry no mobile-layout class under either User-Agent; every other phone record carries it. `before/summary.md` (scratch) holds the per-page table.

BEFORE totals (all widths, both User-Agents):
- Text contrast failures 644, all inside `#main-content`; heaviest: `profile-ten` (105 desktop, 96 phone), `picker-superadmin` (44 each; 11 boards listed).
- Horizontal overflow (also under the text-spacing stress): Profile at desktop 320 (`profile-one`, `-three`, `-ten`, `-qr`, `-rename`); AccessDenied (`accessdenied-in`, `accessdenied-out`, `fx-row-shared-accessdenied`) and Events/Details (`fx-events-details`, `fx-row-events-details`) under the phone User-Agent at 390 and 412. AccessDenied scrollWidth at 390 is 394, matching the ROADMAP figure.
- Phone small targets 8 (Login 2, Login errors 2, Login invalid 2, AccessDenied signed out 2), focus-ring misses 0, console, page and failed requests 0.
- Picker keyboard barrier (`picker-keyboard`): every record has `assertsEvaluated: 1` and one assert failure. After Tab leaves the top bar, focus is not inside a board row: the rows are mouse-only cards.
- Fixture-presence asserts (3, at least 2, 0 board forms on the pickers; 0, 1, 3, 10 delete forms on the Profiles) are evaluated and pass in every record.
- Measured minimum ratios today: desktop Edit outline button 1.31 (the plan expected about 1.88), phone account labels 2.99 (as expected), Profile subscription meta text 1.26 on desktop and 2.99 on the phone, picker card text 3.27 desktop and 3.07 phone, error text 3.63 desktop and 3.36 phone.
- Non-text bar: every credential, Edit and Rename-dialog field meets the 3:1 bar in BEFORE (border against fill at least 4.91; fill or border against surface at least 4.94); BEFORE is the figure later slices must not drop below.
- The ROADMAP's 12 failing contrast rows on 7 elements on 4 pages counts default widths on the first-class pages; this record counts every width and state, so it is larger and is recorded only for comparison.
- `readability-compare.py --zero-contrast phase` on BEFORE exits 1 as required (the residual failures are visible to the gate).

## Deviations from Plan

### Operator-approved environment substitutions (not defects)

**1. [Environment] Windows plan assumptions replaced with Linux equivalents** (per `~/qb-ui-review/phase-96/ENVIRONMENT.md`): ports 8000 (working tree) and 8001 (baseline) instead of 8096 and 8097, bundled Chromium, `python3`, `qb-audit-*` accounts for `qb96-*`, review board "Audit Board" (id 7), SQL Server in docker `mssql-dev` untouched. Files: scratch only.

### Auto-fixed Issues

**2. [Rule 3 - Blocking] Rate-limited error rows.** Forgot password and Set password POSTs are limited to 3 per 15 minutes per client address and per process; four captures of `forgot-errors` and `setpassword-errors` hit 429 on the fourth (found when the first full sweep failed the layout check on the 390 record). Fix: those two rows carry `hosts` (desktop UA through 127.0.0.1, phone UA through [::1]), two POSTs per address per sweep, the app on 8000 was restarted and the two rows recaptured, and `sweep.mjs` records `rateLimited[]` on every record. Consequence for later plans: restart the app before each sweep that includes these two rows (`node tools/run-app.mjs <tree> 8000 --restart`), as the limiter lives in the process. The app on 8000 was restarted at the end of this plan, so its limiter is clean. Files: scratch only.

**3. [Rule 3 - Blocking] `event.single` reused, not created.** The plan asks `dm` to create one fixture event, but its own rule is to look a name up before creating; the Phase 94 event already exists on the Audit Board. Reused (`created: false`, `reusedFrom: phase-94`), so nothing was written to Events.

**4. [Rule 1 - Bug] Plan's `id` type.** Task 2's verify requires integer ids for the boards and the event, while the phase-95 tooling stored strings; `fixtures.mjs` now stores digit-only ids as integers.

**5. [Rule 1 - Bug] `created` flag lost on re-run.** A second `fixtures.mjs` run overwrote `created: true` with false for rows made earlier; `rec` now keeps `created: true`, so the operator can still see what to remove.

**6. [Rule 3 - Blocking] HEAD moved during the plan.** A `docs(97)` commit landed on the branch while the plan ran; the baseline was extracted from the new HEAD and then corrected to the start-of-plan commit (the two phase-97 planning files were removed from `baseline-src/`, `BASELINE_SHA` records `e84ca9e0`). Source trees are identical between the two commits.

**7. [Plan wording] Manifest actions are objects** (`{ type: 'press', key, n }`, `tabPastBar`, `submit`, `waitFor`, ...) as in the phase-95 sweep, not the plan's `click <selector>` prose. `picker-keyboard` uses `tabPastBar` (Tab until focus is neither in the top bar nor the skip link, at most six presses).

**8. [Finding] A super admin sees every board on the picker.** `picker-superadmin` therefore lists boards that are not review fixtures; the row is `noSamples: true` so no board name is recorded in any report (screenshots under `before/` still show them, scratch only). `membership.superadmin` was still created as the plan asks.

## Auth gates

None.

## Known Stubs

None. No repository files were created or modified by this plan.

## Threat Flags

None. `git status --porcelain` over the three source trees is empty; fixtures went through the app's forms on fantasy-named review boards (members added by exact email); `grep` for a calendar feed address across the scratch json and md files prints nothing, no `/feeds/calendar/` request was made (aborted by the harness), control keys hold role and name only, and the only `token=` in any record is the literal render-only placeholder of the SetPassword render captures.

## State left behind

- Working-tree app on `http://localhost:8000` (restarted at the end, limiter clean) and frozen baseline on `http://localhost:8001`; both left running for the later plans. Port 8100 (hotfix session) was not touched.
- Fixture rows to remove after Gate 2 (all `created: true` in `fixtures.json`): boards Ashenford Watch and the 60-character board with their memberships, the `multi` and `superadmin` memberships, `noboard` removed from the Audit Board (add back to restore), and the subscriptions of `player` (1), `multi` (3) and `dm` (10), which must be revoked after Gate 2. The Phase 94 event was reused and is not a new row.

## Self-Check: PASSED

Verified: Task 1 verify `ok`, second fixtures run created 0; Task 2 verify `ok` (8001 answers 200, repo tree refused on 8001, every fixture key present and none unavailable, `event.single` declared, ids integer); BEFORE against itself `ok`; Task 3 verify `ok` (114 records, every row and User-Agent present, mobile-layout expectations hold, fixture asserts pass, Login records carry the Email and Password fields in `controlBorders`); `--zero-contrast phase` on BEFORE exits 1; `BASELINE_SHA` present; no repository source file changed.
