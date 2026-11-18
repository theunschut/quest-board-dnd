---
phase: 100-admin-and-platform-ui-conversion
plan: 03
subsystem: review-baseline
tags: [baseline, before-capture, manifest, playwright, scratch-environment]
requires: ["100-02"]
provides:
  - "~/qb-ui-review/phase-100/baseline-src: frozen git archive of HEAD (BASELINE_SHA 34ede3a83fdb04c90795829cc94526e273cadefb), served on 8341"
  - "manifest.json: 22 rows (id, route, role, board, state, realData, widths) for the whole Admin and Platform area"
  - "before/: 95 capture records (report.json) with frames under desktop/ and mobile/, findings-before.json, findings-owners.json, extras.json, summary.md"
  - "tests-before.txt: 4369 fully qualified test names of both test projects"
affects: [100-04, every later Phase 100 slice, 100-22 closing sweep, both gates]
tech-stack:
  added: []
  patterns: ["frozen git-archive baseline served beside the working tree", "read-only DOM facts stored on every sweep record", "real-data rows flagged and described by structure only"]
key-files:
  created: []
  modified: []
decisions:
  - "Every Members row that renders the Available Users section is flagged realData (that section lists every real account not on the board); only the search-miss row, where both searches match nothing, is not"
  - "The three validation rows post an invalid form (empty fields, a cleared name with a bad address, mismatched passwords); the server refuses each and writes nothing, so the sweep does not abort those POSTs"
  - "The extra per-record facts (badges, button looks, headings, landmarks, inline script counts and so on) live in a read-only in-page function added to the copied sweep, so BEFORE and every AFTER are measured by the same code"
metrics:
  duration: "about 50 minutes"
  completed: 2026-11-18
status: complete
actuals:
  tokens: 7800
  tasks: 3
  commits: 1
---

# Phase 100 Plan 03: Frozen baseline and BEFORE Summary

The pre-change tree is frozen and serving on 8341, and BEFORE exists for every row of the Admin and Platform area under both User-Agents: 22 manifest rows, 95 capture records, every one answered 200 with no failed action, no failed assert and a clean console. No repository source file changed; all artifacts live under `~/qb-ui-review/phase-100`.

## Baseline

- BASELINE_SHA: `34ede3a83fdb04c90795829cc94526e273cadefb` (HEAD when the plan started). `git log` from the branch point shows docs commits only, `git diff` of the three source projects against it is empty, and `baseline-src/` is a `git archive` of that commit (with the SHA recorded inside it).
- It runs on 8341 through `run-app.mjs`, so email suppression is forced and verified on its process. It is left running, as the plan says, for later comparisons. 8340 was not running.
- The baseline is already the shipped foundation (tokens, `components.css`, the navy sheets), not the Phase 94 audit tree. Several audit values therefore differ today (for example the role badges now measure 5.4:1 to 8.9:1); `findings-before.json` records what the frozen tree measures now, which is what every slice must not regress against.

## Rows and capture shape

22 rows: Admin (`admin-users`, `admin-users-empty`, `admin-quests`, `admin-quests-campaign`, `admin-quests-empty`, `admin-createuser` and `-invalid`, `admin-edituser` and `-invalid`, `admin-resetpassword` and `-invalid`, `admin-emailstats` key-missing) and Platform (`group-index`, `group-create`, `group-edit`, `group-delete`, `members`, `members-empty`, `members-search-miss`, `members-modal`, `platform-users`, `linked-boards`). Desktop at 1440 and 320, plus 1024 on the list pages (Admin/Users both states, Admin/Quests One-Shot, Group/Index, Members, Platform Users, LinkedBoards); phone under Pixel 7 with touch at 412 and 390. 51 desktop records and 44 phone records.

Per-page BEFORE failure counts (frozen tree):

| Group | contrast failures, desktop | contrast failures, phone | overflow records | phone targets under 44px |
|---|---|---|---|---|
| Admin (users, quests, forms, EmailStats) | 27 | 56 | 0 | 0 |
| Platform (groups, members, users, linked boards) | 23 | 1438 | 2 | 127 |

- Overflow: Group/Index and Platform Users at 320 desktop only (the 390 phone layout fits everywhere).
- Phone small targets: Platform Users 63 at 412 and 60 at 390; the Members search-miss row 2. Nothing else under 44px.
- Focus rings missing, console, page and request errors: 0 on every record.
- Highest contrast concentration is the phone Platform pages (Group/Index 78, Members 126 to 127, Platform Users 224, LinkedBoards 30 failing pairs, lowest 2.35:1) and the Edit User page (1.3:1 desktop, 1.21:1 phone, the outline Reset Password button). Admin/Users desktop and Admin/Quests desktop measure 0 failing pairs now.
- Q-126 BEFORE (populated Admin/Users on P100 Audit One-Shot as its Admin, long-name, unconfirmed, peer and Dungeon Master rows): `before/desktop/admin-users.png` (1440), `admin-users--w1024.png`, `admin-users--w320.png`, `before/mobile/admin-users.png` (412) and `admin-users--w390.png` under the scratch root.
- Real-data rows (frames stay local, text not stored, summary describes counts only): `group-index`, `members`, `members-empty`, `members-modal`, `platform-users`, `linked-boards`. The summary and `findings-before.json` contain no real name, email or board.

## Owned findings

`before/findings-before.json` holds a measured BEFORE value for 45 of the 46 owned findings (A-admin-001 to 044, A-shell-037, A-shell-038); A-admin-036 (the EmailStats statistics tiles) is notMeasurable with the reason that the statistics branch needs a provider key the local app does not have (only the key-missing state is measured). `before/findings-owners.json` is parsed from the plan's "Finding owners" table (46 entries) plus one added entry for the Members modal binding bug found in the fixture plan, proposed to 100-19 for the planner to confirm.

## Test inventory

`tests-before.txt` lists 4369 fully qualified names: 1588 from `QuestBoard.UnitTests` and 2781 from `QuestBoard.IntegrationTests` (`dotnet test --list-tests`, theory cases included as printed). The cleanup plan compares against it.

## Verification

- Task 1: baseline started on 8341, `manifest.mjs` and `sweep.mjs --pages admin-users` ran, the assertion printed `ok` (both User-Agents, all 200, a 1024 desktop record); source diff against BASELINE_SHA is empty and `AdminController.cs` is present in `baseline-src/`.
- Task 2: manifest has 22 rows, every id has records, `findings-before.json` covers A-admin-001 to 044, `summary.md` has no account address, `tests-before.txt` has more than 1000 lines, owners cover every finding; the assertion printed `ok`. `fixtures.mjs --check` passed afterwards under both User-Agents (the three posting rows changed no fixture).
- `git status --porcelain` is empty for the source and test projects.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Measurements the copied sweep did not take**
- **Found during:** Task 1
- **Issue:** The copied sweep records contrast, targets, focus rings, overflow and controls, but not the DOM facts the owned findings need (badge label contrast per fill, headings, landmarks, inline handlers and dialogs, backdrop-filter and `transition: all` counts, autocomplete, icon gaps, action-button geometry, modal naming, stylesheets). Without them DoC-5 values for roughly half the findings could not be recorded.
- **Fix:** `tools/facts100.mjs` (read-only in-page function, stores no names on real-data rows) wired into `sweep.mjs`; `tools/findings-extra-100.mjs` for the two probes that need real input (card hover lift, focus looks of form controls; POSTs answered locally).
- **Files modified:** scratch tools only; **Commit:** none

**2. [Planning note] Members rows flagged realData more widely than the plan's list**
- The plan lists Group/Index, Platform Users and LinkedBoards as real data. Members of any board also renders the Available Users section with every real account not on the board, so `members`, `members-empty` and `members-modal` are flagged realData too. `members-search-miss` uses both `memberSearch` and `search` on P100 Audit One-Shot so both sections show their miss state and no real account is listed.

**3. [Planning note] Tracer gate run as autonomous**
- As in 100-01 and 100-02 the spawning orchestrator ran this plan unattended; after Task 1 the tracer verify was re-run end to end and passed before Task 2 began.

Tasks 1 and 2 changed nothing in the repository, so they have no per-task commits; the only commit is the docs commit.

## Authentication Gates

None.

## Known Stubs

None (no repository source created or modified).

## Threat Flags

None. The sweep posted only the three invalid-form rows, each refused by the server before any write.

## Task Commits

- Task 1: scratch baseline and tooling (no commit).
- Task 2: scratch manifest, sweep, probes and summaries (no commit).
- Task 3: docs commit carrying this SUMMARY, STATE.md and ROADMAP.md.

## What the next plans must know

- Port 8341 still serves the frozen baseline; start the working tree on 8340 with `node tools/run-app.mjs /mnt/Data/repos/quest-board-dnd 8340` and sweep with `node tools/sweep.mjs --port 8340 --pages <ids|all> --out <dir>`; compare against `before/` with `readability-compare.py --before before --after <dir>`. `sweep.mjs` takes `--before` only to tag records.
- Row shape: `id, path, role, board, boardId, state, realData, widths`; `manifest.mjs` is the one place to add AFTER-only rows. Each record carries `facts` (see `facts100.mjs`) and `rowState`/`realData`; the phone Admin and Platform lists show display names only.
- `before-summary-100.py` re-derives `findings-before.json` style values; run it against an AFTER report by pointing `QBR`-relative paths, or copy and adapt it, for the closing trace.
- Baseline findings already differ from the Phase 94 audit where the foundation shipped; trace each finding from `findings-before.json`, not from the audit text.
- The Members modal binding bug (see `deferred-items.md`) is now in `findings-owners.json` as a proposed 100-19 item.

## Self-Check: PASSED

Scratch artifacts present (`BASELINE_SHA`, `baseline-src/`, `manifest.json`, `before/report.json`, `findings-before.json`, `findings-owners.json`, `extras.json`, `summary.md`, `tests-before.txt`); source trees unchanged; 8341 listening, 8340 free.
