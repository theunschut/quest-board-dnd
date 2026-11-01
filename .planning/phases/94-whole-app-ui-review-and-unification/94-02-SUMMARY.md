---
phase: 94-whole-app-ui-review-and-unification
plan: 02
subsystem: ui-audit-tooling
tags: [playwright, audit, manifest, fixtures, baseline, contrast, gates, scratch-tooling]
requires:
  - phase: 94-01
    provides: "Playwright capture harness, four audit accounts on the Audit Board, per-port storage states"
provides:
  - "Generated route manifest covering all 57 routable view pairs (51 with a .Mobile twin, 6 without) plus 3 state-variant rows"
  - "Fixture states on the Audit Board, created through the app's own forms and listed in fixtures.json"
  - "Frozen baseline source (milestone HEAD b6770322) runnable on port 8001, with a run-app.sh that refuses to reuse a listener serving different code"
  - "Full BEFORE record: desktop 1440 and mobile Pixel 7 full-page PNG per manifest row plus a machine-readable report"
  - "Reusable tools: contrast.mjs, diff.py, probes.mjs, sweep.mjs, gate.mjs, gate-compare.py, readability-compare.py"
affects: [94-03, 94-04, 94-05, 94-06, 94-07, 94-08, 94-09, 94-10, 94-11, 94-12, 94-13, 94-14, 94-15, 94-16, 94-17, 94-18, 94-19, 94-20, 94-21, 94-22, 94-23, 94-24, 94-25, 94-26, 94-27]
tech-stack:
  added: []
  patterns: ["manifest generated from the .cshtml inventory, never typed by hand", "fixtures made only through the app's own forms (EasyMDE fields filled through the live editor)", "contrast probe composites text over each ancestor layer, gradient stops, sampled image pixels or the B-med/B-99/B-max board", "listener identity check by /proc exe path before reusing a port"]
key-files:
  created: []
  modified: []
decisions:
  - "All output lives under ~/qb-ui-review/phase-94 (scratch, outside the repository); the repository gains only this SUMMARY and deferred-items.md"
  - "Fixtures use only the Audit Board plus one new empty board and one new link set joining the two; the Phase 92 UAT rows (quests 991001/991002, event 1, link sets 1003/1004) and every real board were left untouched"
  - "The tracer feedback gate was satisfied by re-running the tracer verify end to end rather than a human checkpoint, because the verify is fully automated and the orchestrator asked for the complete plan"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 21000
  tasks: 3
  commits: 1
---

# Phase 94 Plan 02: Manifest, Fixtures, Frozen Baseline and BEFORE Sweep Summary

A generated 57-pair route manifest, fantasy-named fixtures on the Audit Board, a frozen baseline app on port 8001 and a full desktop and mobile BEFORE capture of 57 manifest rows (120 records, 114 captured, 6 skipped with reasons) with contrast, interactive-inventory, alt, focus and reflow findings.

Nothing was written into the repository except this SUMMARY and `deferred-items.md`. All paths below are under `~/qb-ui-review/phase-94` (`QBR`).

## Task 1: manifest generator and tracer (done, verify passed)

- `tools/manifest-gen.mjs` walks `QuestBoard.Service/Views` and `Areas/Platform/Views`, skips underscore files, pairs `X.cshtml` with `X.Mobile.cshtml`, applies the route table and exceptions, assigns the lowest role and fixture key per row, and fails loudly unless pairs = 57, withTwin = 51, twinless = 6 and the twinless set equals Admin/EmailStats, Events/Create, Events/Details, Events/Edit, Series/Details, Shared/AccessDenied. Output: `manifest.json` (60 rows: 57 pairs plus 3 state variants `Quest-Details--voting`, `Calendar-Index--crowded`, `Shop-Index--empty-category`).
- `tools/contrast.mjs` written fresh from the research code example (the old scratchpad copy no longer exists). It reproduces the UI-SPEC numbers that do not depend on the unrounded median board pixel: parchment on glass at B-max 4.34, C2 4.75 at B-99, C3 2.99, C4 3.45, C5 1.97, C6 and C1 (1.38 and 2.11) at the blocking boards, white on `#8B4513` 7.10, white on `#dc3545` 4.53, `#1a0f08` on `#ffc107` 11.55. At B-med the spec's four secondary-text figures (4.34, 5.53, 3.16, 5.38) come out 0.02 to 0.03 lower (4.32, 5.50, 3.14, 5.35): the spec's `#4D2108` is the rounded hex of the median pixel. All blocking-board numbers match exactly; B-med only matters where the bar is already missed or cleared by more than 0.03.
- `tools/diff.py` (Pillow + numpy only): equal-size padding, tolerance, mask rectangles, overlay output, JSON `{differing, ratio, bbox, sizeA, sizeB}`, always exit 0. Self-diff prints `"differing": 0`.
- Tracer: Quest board (`/quests`) as Player under both UAs, desktop `mobileLayout false`, mobile `mobileLayout true`, no errors. Tracer verify re-run end to end before expansion.

## Task 2: fixtures and frozen baseline (done, verify passed)

Fixtures (`fixtures.json`, 28 keys, 22 with `created: true`, all through the app's forms as the audit DM, Player or SuperAdmin; nothing by SQL):

| Key group | What exists |
|-----------|-------------|
| `quest.voteFixture` | voting-open quest, two proposed dates, `created: true`, never voted on |
| `quest.crowded1..12` | 12 open quests; with the vote fixture and the finalized quest the board shows 14 cards |
| `quest.finalized` | finalized quest with the Player signed up (character, Yes votes) and selected |
| `questlog.closedWithRecap` | past finalized quest with a recap written through the recap form |
| `character.own`, `contact.any`, `contactCategory.any` | one character, one revealed contact, one contact category |
| `event.single`, `series.any` | one event and one weekly series |
| `calendar.crowded` | November 2026 with entries on 25 distinct days |
| `user.player`, `user.dm`, `group.audit` | ids read from the app's own pages (not created here) |
| `group.empty`, `linkset.audit` | one new empty board and one link set joining it to the Audit Board |
| `shop.emptyCategory` | literal `QuestItems`: no quest items exist, so the category is empty |
| `shop.item` | recorded as `unavailable: true` (see Deviations 2) |

Frozen baseline: `baseline-src/` holds `git archive HEAD` of commit `b67703220dde8500867fcc4a7fe20b13743c31f0`, written to `baseline-src/BASELINE_SHA` (equal to the repository HEAD at capture time and to the milestone branch tip). `run-app.sh <src> <port>` starts `dotnet run` with `--no-launch-profile` in Development, logs under `logs/`, waits for `/health` 200, and when the port is already served identifies the listener from `/proc/<pid>/exe` (or the `.dll` argument) and compares it with `realpath <src>`: inside the tree it exits 0 without starting anything, anywhere else or unresolvable it prints port, expected and actual and exits 1. Proven: `run-baseline.sh` twice exits 0 with one process, `run-app.sh /mnt/Data/repos/quest-board-dnd 8001` exits 1, `run-app.sh baseline-src 8000` exits 1. `stop-app.sh <port>` stops only an instance this toolset started. A second gate capture of port 8000 (working tree) against the baseline gave PASS with zero differing pixels, so today's working tree equals the baseline.

## Task 3: full BEFORE sweep (done, verify passed)

`tools/sweep.mjs before --port 8001` ran every manifest row with its own role under desktop 1440 and mobile Pixel 7, plus reflow checks at desktop 320 and mobile 390. Results in `before/{desktop,mobile}/<id>.png` (57 each, equal to the non-skipped records per UA) and `before/report.json` (120 records).

- 114 records captured with status 200; 6 skipped with a reason: `Account-SetPassword` (needs an emailed token, 2 records) and `Shop-Details`, `ShopManagement-Edit` (no shop item can be created, 4 records).
- Every mobile record with status 200 has `mobileLayout: true` except `GroupPicker-Index`, recorded with `mobileLayout: false` and `layoutNoTwin: true` as the plan documents (its layout has no twin). The plan's automated verify string asserts no mobile record lacks `mobileLayout`, which contradicts its own acceptance exception; the acceptance criterion was followed.
- Findings recorded per record: console and page errors, failed requests, horizontal overflow, contrast failures, interactive inventory (key, visibility, box, accessible name), `img` without alt, desktop focus probe with 200x120 crops per Tab stop.
- Headline counts (BEFORE, for the audits to start from): 2318 contrast failures across 114 captures (the probe scores each text element at B-med, B-99 and B-max, over gradient stops and over sampled image pixels); 0 `img` without alt; console or request errors only on `Quest-Details` and `Quest-Manage` (a 404 for the character cropped picture of a character with no uploaded image); 39 focus stops with no drawn indicator and 45 whose indicator box a clipping ancestor cuts off; horizontal overflow on desktop at 320px for Calendar (both rows), ContactCategoryManagement/Manage, Platform/Group Index, Quest/Details and QuestLog/Index, and on mobile at 390px for Events/Details and Shared/AccessDenied (both twin-less views).
- Dynamic regions (`time`, `.local-time`) are stored per record as `masks` for `diff.py`.

Gate tools: `gate.mjs --label <name> --port <p> [--extra routes.json] [--probes]` captures the seven gate pages under both UAs into `gates/<label>/`; `gate-compare.py A B` prints per-page JSON and PASS only for zero differing pixels with no new error, failed request or status change. Selftest: `gate-compare.py selftest selftest` prints PASS, and two independent captures also compare PASS (deterministic with masks). `readability-compare.py A B` implements the readability bar (new contrast failure, interactive element hidden, zero-sized, outside or missing, new error or failed request, new horizontal overflow, non-200); `before` against itself prints PASS.

## Deviations from Plan

### Orchestrator-directed

**1. [Fixture scope] Audit Board only, no Phase 92 UAT rows**
- The plan said to reuse quests 991001/991002 and event 1 from the operator's data. The orchestrator ruled that those live on real boards. Every fixture was created on the Audit Board (ids 992001 and up for quests, event 2, series 1) through the app's forms with `created: true`. The two cross-board or platform views that can only show other boards (`/groups/pick`, `/platform/Group`, `/platform/Users`, `/platform/LinkedBoards`, the move page, `/Admin/Users`) were captured, with no text samples stored for them, and their images stay under `~/qb-ui-review/phase-94` only.
- The existing UAT link sets (1003, 1004) offer real boards for linking; they were never touched.

### Auto-fixed Issues

**2. [Rule 3 - Blocking] Shop items cannot be created through the app**
- **Found during:** Task 2
- **Issue:** `ShopManagement/Create` answers with a database foreign-key error (`FK_ShopItems_Groups_GroupId`): the controller never sets the new item's `GroupId`. No shop item can exist on the audit board.
- **Fix:** none in code (the plan modifies no application code and the operator forbade SQL fixtures). `shop.item` is recorded `unavailable: true` with the reason; `Shop-Details` and `ShopManagement-Edit` carry a `skipReason`; the Shop index and its empty-category state are captured. The defect is written to `deferred-items.md`. The orchestrator may want a decision on fixing it or seeding one item for the item-detail audit.
- **Files modified:** none (record only)

**3. [Rule 3 - Blocking] A new contact is hidden from players**
- **Found during:** Task 3 (first sweep returned 404 for `Contacts-Details`)
- **Fix:** the DM revealed the contact through the contact page's own Reveal button (`tools/reveal-contact.mjs`), then the sweep was re-run from scratch. Run that script after the `contacts` fixture step on a fresh database.

**4. [Rule 3 - Blocking] Picker, platform Delete and Move pages did not render their own view**
- **Found during:** Task 3 (first sweep)
- **Issue:** `/groups/pick` redirects a one-board member past the picker; `/platform/Group/Delete/{board}` redirects when the board has members; `/Move/Quest/{id}` redirects when the board is linked to nothing.
- **Fix:** the `GroupPicker` row now uses the SuperAdmin role (who sees every board); the Delete row uses `group.empty`, a new empty board "The Hollow Vault of Nehru"; the Move row uses `quest.crowded1` after the Audit Board and that empty board were joined into the link set "Audit Pair of Lanterns" through Platform > Linked Boards. The Calendar legend now shows the linked-board entry; the linked-entry partials stay unexercised until the second board holds content.

**5. [Rule 1 - Bug] Duplicate fixture quest from interrupted runs**
- Two early runs created the same finalized-quest title twice before the id lookup was fixed. The extra row was deleted through the quest page's own Delete flow; the fixtures step now looks a title up before creating it.

### Other differences

- `git -C /mnt/Data/repos/quest-board-dnd archive HEAD` is refused inside this worktree, so the baseline was archived from the worktree's own HEAD, which the main checkout's branch ref confirms is the same commit.
- Tracer gate: `auto_advance` is false, which would normally mean a human checkpoint after the tracer. Its verify is fully automated, passed, and was re-run before expansion; the orchestrator asked for the complete plan, so no checkpoint was returned.
- The `Shared-AccessDenied` row is reached by the Player requesting the DM-only `/Quest/Create`; the record carries `finalPathOk: true` for `/Account/AccessDenied`.
- `fixtures.json` shop and contact steps use the visible tag box and the disabled availability inputs the forms actually have (availability left unset).

## Auth gates

None.

## Known Stubs

None (no repository source changed).

## Threat Flags

None new. Output stays under `~/qb-ui-review/phase-94` (storage states and credentials mode 600); text samples are limited to 40 characters and are not stored for pages that list other boards or users; no passwords, tokens or member data appear in this file or in commits. One new empty board, one link set and the fixture rows exist in the operator's local database, each recorded with `created: true` in `fixtures.json` so they can be removed.

## Notes for later plans

- Ports: 8000 working-tree dev app (operator-started), 8001 frozen baseline (left running). Each later plan starts its own build on its own port with `run-app.sh <src> <port>`; role states per port are logged in on first use.
- Tools to reuse, not rewrite: `audit-shoot.mjs --probes`, `sweep.mjs <label> --port <p>`, `gate.mjs`, `gate-compare.py`, `readability-compare.py`, `contrast.mjs`, `diff.py`, `resolve.mjs` (manifest row to URL).
- The contrast probe scores raster surfaces by sampling pixels under each text element (mean and 10th/90th percentile); gradient surfaces by each colour stop; the page by B-med, B-99 and B-max. Text over the finalized-card wax seals scores low by construction.
- `deferred-items.md` in this phase directory records the shop-creation defect.

## Self-Check: PASSED

- Files exist under `~/qb-ui-review/phase-94`: `manifest.json`, `fixtures.json`, `before/report.json`, `before/desktop` and `before/mobile` (57 PNG each), `baseline-src/BASELINE_SHA`, `tools/{manifest-gen,fixtures,sweep,probes,contrast,gate,resolve}.mjs`, `tools/{diff,gate-compare,readability-compare}.py`, `tools/{run-app,stop-app,run-baseline}.sh`, `gates/selftest/report.json`.
- Task 1, Task 2 and Task 3 automated verify commands pass (Task 3's mobile-layout assertion passes apart from the documented GroupPicker exception).
- `BASELINE_SHA` equals the repository HEAD; `git status --porcelain` in the worktree showed only the new `deferred-items.md` before this SUMMARY.
