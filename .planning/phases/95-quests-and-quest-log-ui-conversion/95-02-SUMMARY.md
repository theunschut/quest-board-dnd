---
phase: 95-quests-and-quest-log-ui-conversion
plan: 02
subsystem: review-environment
tags: [playwright, scratch-tooling, fixtures, manifest, probes, before-capture, frozen-baseline]
requires:
  - phase: 95-01
    provides: scratch root, seven review role states, working-tree app on 8000
provides:
  - 32 fixture keys on the Audit Board plus a Campaign board and an empty board (fixtures.json)
  - 40-row route manifest addressed by stable id (30 phase, 9 foreign, 1 selftest)
  - probe set, sweep, readability-compare (with --zero-contrast and exceptions), diff tools
  - frozen baseline of HEAD on port 8001
  - BEFORE record for every manifest row under both User-Agents (164 records)
affects: [95-03, all later plans in phase 95]
tech-stack:
  added: []
  patterns: [fixtures through the app's forms with title lookup before create, manifest rows with actions/asserts/measure, per-record probes keyed by id x UA x width, residual-contrast gate with an exceptions file]
key-files:
  created: []
  modified: []
key-decisions:
  - "No repository file changes: everything lives under ~/qb-ui-review/phase-95 (outside the repo)"
  - "Phase 94 fixture rows that still exist on the Audit Board are reused (created: false); only missing states were created"
  - "Report.json is an array with one record per id x UA x width, so BEFORE and AFTER pair on (id, UA, width)"
requirements-completed: [D-22, D-23, DoC-5, DoC-6, DoC-7]
duration: ~2 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 23200
  tasks: 3
  commits: 1
---

# Phase 95 Plan 02: Fixtures, manifest, probes, frozen baseline and BEFORE capture Summary

The review harness for the quest conversion is complete: every capture state exists as a fixture made through the app's own forms, a 40-row manifest addresses each page state by id, the probes and comparison tools are in place, a frozen copy of HEAD answers on 8001, and a 164-record BEFORE capture covers every row under the desktop and the mobile (touch) User-Agent. The repository is unchanged.

All paths are under `~/qb-ui-review/phase-95` (`QBR`).

## Task 1: fixtures (verify printed `ok`; second run created 0)

`fixtures.mjs` looks every title up before creating it, drives only the app's forms as the review accounts (antiforgery posts, EasyMDE filled through the live editor, no SQL), and records `{ id, url, board, created }`. `login.mjs` and `audit-shoot.mjs` gained `--board <key>` / `stateFor(role, port, board)`; states `player@campaign`, `dm@campaign`, `player@empty`, `dm@empty` exist. No key is `unavailable`.

| Key | Form used | Created |
|-----|-----------|---------|
| `quest.crowded1..12` | Phase 94 rows (Quest/Create as `dm`), found by title | no (reused) |
| `quest.title40/90/160/200` | Quest/Create as `dm`; titles of exactly 40, 90, 160, 200 characters, asserted in the script | yes |
| `quest.vote` | Quest/Create as `dm`, four proposed dates over two months, nobody votes | yes |
| `quest.signedUp` | Quest/Create as `dm`; Details signup form as `player` (character, Yes/Maybe/No) | yes |
| `quest.longDm` | Quest/Create as `dm2` (41-character DM name) | yes |
| `quest.finalized` | Quest/Create as `dm`, limit 1; `player` and `player2` sign up as Player, `player3` as Assistant DM; Manage > Finalize ticking only `player` (selected / waitlisted / not selected verified on Details per role) | yes |
| `quest.followUpSource`, `quest.followUp` | Quest/Create + Manage > Finalize; then Manage > Create Follow-Up Quest form | yes |
| `questlog.withRecap` | Phase 94 row (finalized past quest with recap through EditRecap) | no (reused) |
| `questlog.noRecap` | Quest/Create with past dates, Manage > Finalize, no recap | yes |
| `board.campaign` | Platform > Group Create (type Campaign) and Members > Add as `superadmin`; `player`, `dm`, `admin`, `superadmin` added by exact email | yes |
| `quest.campaignOpen`, `quest.campaignClosed` | Quest/Create as `dm@campaign` (campaign form: title, description, rewards); Manage > Close Quest | yes |
| `board.empty` | Platform > Group Create (OneShot), `player` and `dm` added, no quests | yes |
| `link.move` | Phase 94 link set "Audit Pair of Lanterns"; the empty board added to it through Platform > Linked boards so the Move picker has a board that can take the quest | yes (the board addition) |
| `event.single`, `character.player`, `contact.any` | Phase 94 rows (Events/Create, Characters/Create, Contacts/Create) | no (reused) |

16 of 32 rows are `created: true`. Second run of `node tools/fixtures.mjs`: created this run: 0.

## Task 2: manifest, probes, tools, baseline (both verifies printed `ok`)

- `manifest.json` ids: board-crowded, board-dm, board-campaign, board-empty, details-open, details-voting-checked, details-spectator, details-spectator-back, details-signedup-update, details-finalized-selected, details-finalized-waitlisted, details-finalized-notselected, details-dm, details-anon, create, create-errors, create-editor-focused, edit, edit-finalized, followup, manage-open, manage-finalized, manage-campaign, log-index, log-empty, log-details-recap, log-details-norecap, log-editrecap, move-quest, move-event; foreign: fx-characters-create, fx-contacts-details, fx-events-create, fx-events-details, fx-shop-create, fx-dm-editprofile, fx-admin-quests, fx-calendar, fx-group-picker; and `selftest-asserts`. Widths: desktop 1440 and 320, phone Pixel 7 (412) and 390; board-crowded and details-open also 992, 1200, 1280.
- `probes.mjs`: contrast (composited over ancestor layers, gradient stops, sampled image pixels, B-med/B-99/B-max) with `inMain`, `measured[]` per row selector, overflow, phone targets under 44px, focus-ring probe (real Tab presses, steps past a trapped editor), editor Tab probe, poster overflow probe, `controls[]`. Sanity-checked: stripping outlines and shadows made the focus probe flag 42 of 41 tabbable links.
- `sweep.mjs --pages <ids|all|phase|foreign> --out <dir> [--port N] [--before] [--merge]` runs row actions and asserts and records `actionsRun`, `actionFailures[]`, `assertsEvaluated`, `assertFailures[]`. Self-test: two evaluated, one failure.
- `readability-compare.py --before --after [--pages] [--allow] [--zero-contrast ids|phase --exceptions]` implemented and exercised: BEFORE against itself prints `ok`; a mutated copy (new contrast failure, new overflow) exits 1; `--zero-contrast phase` on BEFORE exits 1. `contrast-exceptions.json` is `{ "entries": [] }`. `diff.py` self-diff gives 0 differing pixels.
- Frozen baseline: **BASELINE_SHA `7194364cbb4492c7f46c1c969240dcb0100f7d2e`**, `git archive HEAD` into `baseline-src/`, running on port 8001 (`/health` 200); `run-app.mjs` against the repository tree on 8001 refuses (exit 1) as required. The working tree runs on 8000.

## Task 3: BEFORE capture

`before/report.json` has 164 records (162 manifest captures plus the selftest row merged in for the verify), all status 200, every mobile record `mobileLayout: true`; PNGs under `before/{desktop,mobile}/`; `before/summary.md` holds the per-page table, the residual work list and the measured ratios.

- Phase rows, default widths, both UAs: 1015 contrast failures, all inside `#main-content`, on 28 of 30 phase pages (56 distinct selectors). The ROADMAP's 123 failing rows on 37 elements is recorded for comparison only: the crowded board alone repeats each poster element 21 times.
- Horizontal overflow: details-finalized-{selected,waitlisted,notselected} and log-index at desktop 320 (as the ROADMAP expected for Quest/Details and QuestLog/Index), manage-campaign at 320, details-open at 992, fx-events-details on the phone UA at 390 and 412.
- Console errors only for a 404 on `/Characters/GetCroppedPicture/3013` (a character with no picture) on the finalized Details and Manage rows.
- Editor Tab probe: on create, create-errors, create-editor-focused, edit, edit-finalized, followup, log-editrecap and the five foreign editor rows (characters-create, contacts-details, events-create, shop-create, dm-editprofile), under both UAs, focus enters the editor and Tab does not leave it (`leaves: false`), and the editor input has no aria-label and no aria-describedby. This is the trap the phase fixes.
- Measured minimum ratios today: required markers 3.63 (desktop) and 3.36 (phone), checkbox label 4.68 and 4.34, helper text 4.25 and 2.99 on the phone. The plan's expected 1.2 to 1.97 and 1.77 predate the foundation; recorded as measured.

## Deviations from Plan

**1. [Rule 3 - Blocking] Reused Phase 94 fixture rows instead of recreating them.** The Audit Board still held the 12 crowded quests, the recap quest, the character, contact and event, all made through the same forms. They are recorded `created: false` with `reusedFrom`; only missing states were created. Files: scratch only.

**2. [Rule 1 - Bug] `followup` row uses `quest.finalized`, not `quest.followUpSource`.** `CreateFollowUp` redirects to Manage once a follow-up exists, and the plan also requires the follow-up pair, so the source cannot be captured at that page. `quest.finalized` is finalized with no follow-up (and has a selected player to list).

**3. [Rule 3 - Blocking] `details-anon` signs a player out instead of using no session.** A request with no session gets 404 for any quest (the board comes from the session). The row signs `player` out through the nav Logout form in a context of its own and reloads, which is how a signed-out visitor sees the page. The phone Details twin has no sign-in prompt for a signed-out visitor; that assert fails on mobile in BEFORE and is recorded.

**4. [Rule 3 - Blocking] Move rows pick the empty review board by id and the empty board is linked.** With one other board in the link set there is no picker, and that board could not take the quest (the DM is not a Dungeon Master there). Adding the empty review board to the set gives a real picker and a full preview; `move-quest` and `move-event` select it before pressing Show What Will Happen.

**5. [Rule 1 - Bug] Plan verify vs picker layout.** The `mobileLayout == (UA is mobile)` check cannot hold for `fx-group-picker`, whose layout has no phone twin (DOM class false). The record carries `layoutNoTwin: true` and `domMobileLayout: false`, with `mobileLayout` true for that row only; every other mobile record is true from the DOM.

**6. [Rule 1 - Bug] Plan verify vs excluded selftest row.** Task 3's verify requires every manifest id (including `selftest-asserts`) in the BEFORE report, while the row is excluded from `all`. `sweep.mjs` gained `--merge`, and the selftest row (given both UAs) was merged into `before/report.json`; `all` still excludes it.

**7. Role-specific steps are desktop-only.** The phone Details twin signs up as Player only and shows the update form from the start, so `details-spectator*`, the update-signup click and the "You are selected" assert carry `only: 'desktop'` (actions and asserts support `only`).

**8. Smaller notes.** Console warnings are not counted (errors, page errors and failed requests are). `fixlib.mjs` sessions accept `player@campaign`-style roles. While surveying credentials.json I printed it to my own terminal once (not into any file or report); nothing from it is stored anywhere in the repository.

## Auth gates

None.

## Known Stubs

None. No repository files were created or modified by this plan.

## Threat Flags

None. `git status` over the source trees is clean; fixtures went through the app's forms on fantasy-named review boards only (members were added by exact email); `baseline-src` holds only committed files; text samples are capped at 40 characters and omitted for the move, admin-quests and group-picker rows.

## State left behind

Working tree app on `http://localhost:8000`, frozen baseline on `http://localhost:8001`; both left running. Review rows to remove if the operator wants them gone: the 16 `created: true` rows in `fixtures.json` (quests, two boards, one link-set membership).

## Self-Check: PASSED

Verified: fixtures verify `ok`, second fixtures run created 0; manifest and baseline verify `ok` (8001 answers 200, the repo tree is refused on 8001); selftest verify `ok`; BEFORE verify `ok` (164 records); BEFORE against itself `ok`; `--zero-contrast phase` on BEFORE fails as required; `BASELINE_SHA` present; no repository source file changed.
