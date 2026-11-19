---
phase: 103-themed-error-pages
plan: 03
subsystem: testing
tags: [baseline, before-capture, cross-board, deep-link, error-pages, integration-tests]

requires:
  - phase: 103-themed-error-pages
    provides: p103 fixtures, scratch root, forced email suppression (103-01, 103-02)
provides:
  - frozen pre-change tree served on 8351 (baseline-src, BASELINE_SHA)
  - manifest.json with 12 error rows and a BEFORE record per row under both User-Agents
  - leak.json proving the cross-board deep-link leak live on fixture data
  - CrossBoardHiddenRecordTests: seven reproduction facts (skipped, red output saved) and three green pins
  - tests-before.txt, the starting test inventory
affects: [103-04, 103-05, 103-06, 103-07, 103-08, 103-09, 103-10, 103-11, 103-12, 103-13, 103-14, 103-15, 103-16, 103-17, 103-18]

actuals:
  tokens: 9200
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Error rows in the manifest (kind error) swept one request at a time by error-rows.mjs, with the active board and the switch banner read from the next GET of the quests page"
    - "Hidden-record facts compare the hidden record against a nonexistent id requested from a second client that starts from the same session state"

key-files:
  created:
    - QuestBoard.IntegrationTests/Security/CrossBoardHiddenRecordTests.cs
    - .planning/phases/103-themed-error-pages/103-03-SUMMARY.md
  modified:
    - .planning/STATE.md
    - .planning/ROADMAP.md

key-decisions:
  - "manifest.json is a flat array of rows; sweep.mjs and readability-compare.py accept both that and the older wrapped shape"
  - "Rate-limited row uses a loopback host per User-Agent (127.0.0.1 and [::1]) so each has its own forgot-password budget"

requirements-completed: [D-01, D-04, D-05, D-06, D-07, D-08, D-10, D-14, D-19]

duration: 75min
completed: 2026-11-19
status: complete
---

# Phase 103 Plan 03: Baseline, BEFORE and leak reproduction Summary

**The pre-change tree is frozen on 8351, every error row has a BEFORE record under both User-Agents, the deep-link leak is proven live on p103 fixtures and pinned as seven failing-then-skipped facts, and three green pins keep what must keep switching.**

## Baseline

- `BASELINE_SHA`: `d9e581b83a5eb7ec60dfa71125d515a5a7f542e9` (HEAD when this plan started). Every commit since phase 103 began (`ee62716b`, `d9e581b8`) is a docs commit; `git diff <BASELINE_SHA> -- QuestBoard.Service QuestBoard.Domain QuestBoard.Repository` is empty both before and after this plan.
- Extracted with `git archive` into `~/qb-ui-review/phase-103/baseline-src/` and started through `run-app.mjs` on port 8351 (email suppression verified in the listener's environment). The baseline copy was never edited.

## Manifest and BEFORE

12 rows in `~/qb-ui-review/phase-103/manifest.json`: nf-missing-single, nf-missing-multi, nf-foreign, nf-hidden, nf-anon, forbidden-auth, forbidden-anon, too-many, bad-request, server-error, conflict-direct, generic-direct. 42 records in `before/report.json` (30 desktop, 12 phone): desktop 1440 and 320 for every row, 1024 for the five 404 rows, 820 for nf-missing-multi; the Pixel 7 User-Agent (touch) for every row. Two rows have `allowPost`: too-many and bad-request. Tooling: `tools/manifest.mjs`, `tools/error-rows.mjs`, `tools/board-lib.mjs`, `tools/sweep.mjs` (error-row glue), `tools/leak-probe.mjs`, `tools/before-summary-103.py` (writes `before/summary.md`, fixture data only).

Today's behaviour, recorded (status, final-response body length):

| Row | Status | Body length | Note |
|-----|--------|-------------|------|
| nf-missing-multi, nf-missing-single, nf-foreign, nf-anon | 404 | 0 | bare, both User-Agents; board unchanged, no banner |
| nf-hidden (Draft item on Two, One active) | 404 | 0 | the board moved to Two and the next page showed the banner |
| forbidden-auth (Player, `/ShopManagement`) | 200 | 8980 desktop, 7782 phone | one redirect (302) to `/Account/AccessDenied?ReturnUrl=...` |
| forbidden-anon (`/Account/AccessDenied`) | 200 | 5297 desktop, 5215 phone | the rendered page |
| too-many (fourth forgot-password post) | 429 | 42 | posts answered 302, 302, 302, 429; the plain sentence |
| bad-request (token-less Logout post) | 400 | 0 | |
| server-error (`/Error`) | 404 | 0 | the exception handler's own path has no endpoint |
| conflict-direct (`/Error/409`), generic-direct (`/Error/418`) | 404 | 0 | bare |

**Research assumption A2:** a document POST to `/Account/Logout` without a token answers 400 with a body length of 0, so it is bodiless today.

The too-many row's budget is per loopback address and 15 minutes; a re-run inside the window makes the first posts answer 429 too, though the fourth stays 429. Nothing was sent: the address has no account, and suppression is forced.

## The leak, live

As `p103player` with P103 Audit One-Shot active, document navigations on the baseline (`before/leak.json`):

| Request | Status | Board before -> after | Banner on next page |
|---------|--------|-----------------------|---------------------|
| Draft shop item on Two | 404 | One -> One-Shot Two | shown |
| scheduled shop item on Two | 404 | One -> One-Shot Two | shown |
| hidden contact on Two | 404 | One -> One-Shot Two | shown |
| open quest on the quest log (Two) | 404 | One -> One-Shot Two | shown |
| future-finalized quest on the quest log (Two) | 404 | One -> One-Shot Two | shown |
| nonexistent shop id | 404 | One -> One | none |

A member who follows a link to a hidden record is moved to the other board and told so, while a nonexistent id moves nothing: the oracle the later plans close.

## Reproduction facts and pins

`QuestBoard.IntegrationTests/Security/CrossBoardHiddenRecordTests.cs`, commit `e66b750d`. Each reproduction fact compares the hidden record to a nonexistent id requested from a second client that starts from the same state (status, Location, body, headers except Date, Request-Id and traceparent), then checks the quests list still names the first board with no switch banner. Every navigation sends the document navigation headers and `Accept: text/html`.

| Fact (skipped, plain-language reason) | Red output |
|---------------------------------------|------------|
| Draft shop item, Player of the target board | extra `Set-Cookie` (the queued banner) |
| Denied shop item | extra `Set-Cookie` |
| scheduled shop item | extra `Set-Cookie` |
| unrevealed contact the viewer did not create | extra `Set-Cookie` |
| open quest on the quest log | extra `Set-Cookie` |
| another Dungeon Master's quest edit, viewer DM on the active board and Player on the target | 302 instead of 404 |
| contact on a board that does not offer the Contacts page (pageGateCounts yes) | 302 instead of 404 |

Each ran unskipped once and failed against the unchanged tree; the failing output is one file per fact in `~/qb-ui-review/phase-103/audit/red/` (7 files). Then each was committed skipped. The Denied item is covered here although the live set could not hold one.

Pins (active, green today): a Player is switched to a Published, opened item and served 200; a Dungeon Master of the target board is switched to its Draft item and served 200; a member is switched to a revealed contact and served 200.

`dotnet test --filter CrossBoardHiddenRecordTests`: 3 passed, 7 skipped. With the oracle-parity and authorization-boundary classes: 19 passed, 7 skipped, 0 failed.

## Starting test inventory

`~/qb-ui-review/phase-103/tests-before.txt`: 4713 fully qualified names (1626 unit, 3087 integration), taken before the new test class was added. 8350 was stopped for this (build output lock) and is not running.

## For the resolver plans

Two existing facts in `CrossBoardAuthorizationBoundaryTests` assert today's leaking behaviour and will have to change with the resolver: `ResolvedDraftShopItem_SwitchesTheBoardAndLetsThePageDecideForItself` (a Player is switched before the 404) and `DungeonMasterOnActiveBoard_PlayerOnTargetBoard_IsRefusedTheQuestEditPage` (the board switches before the refusal). The inventory comparison will then need them in the renamed-or-changed record.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The sweep and its readers expected a wrapped manifest and no error rows**
- **Found during:** Task 1
- **Issue:** the plan's checks read `manifest.json` as a flat list of rows, while `sweep.mjs` and `readability-compare.py` read `{ rows }`, and the page sweep cannot record bare responses, redirect chains, posts or the board and banner after a request.
- **Fix:** manifest.json is a flat array; both readers accept either shape; error rows are captured by a new `error-rows.mjs` and run sequentially from `sweep.mjs`. Scratch tooling only, outside the repository.

**2. [Rule 3 - Blocking] Rows sharing a signed-in session**
- **Issue:** a request that moves the active board would leak into the next capture through the server-side session shared by every context using the same storage state.
- **Fix:** every signed-in capture starts by choosing the row's board through the picker and loading the quests page (which also consumes a pending banner).

The manifest was written with all twelve rows in Task 1 and only nf-missing-multi swept there, as the plan's verify command requires. No authentication gate occurred.

## Known Stubs

None. The seven skipped facts are intentional and wait for the resolver plans.

## Threat Flags

None. No production source file was created or changed.

## Left running

The frozen baseline on port 8351 (`~/qb-ui-review/phase-103/logs/app-8351.log`, pid file `app-8351.pid`) is still running for the Gate 1 and Gate 2 comparisons. 8350 is stopped. SQL Server was not touched.

## Self-Check: PASSED

- Present: `baseline-src/`, `BASELINE_SHA`, `manifest.json`, `before/report.json`, `before/leak.json`, `before/summary.md`, `tests-before.txt`, 7 files in `audit/red/`, `CrossBoardHiddenRecordTests.cs`.
- Commit `e66b750d` exists; `git status` clean for Service, Domain, Repository.
