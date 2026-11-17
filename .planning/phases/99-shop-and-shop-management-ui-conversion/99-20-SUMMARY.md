---
phase: 99-shop-and-shop-management-ui-conversion
plan: 20
subsystem: gate2-review-and-release-handoff
tags: [ui-review, gate-2, operator-decision, exceptions, accessibility, release-handoff, shop]
requires: ["99-19", "99-21"]
provides:
  - "The Gate 2 package under ~/qb-ui-review/phase-99/gate2/ (index.html, README.md, img/ with 375 JPEGs, answers.json), sheet gate2-v1, passing gate-hygiene.py: 129 before and after pairs, 11 after-only pairs beside their approved Gate 1 frames, 4 drawer pairs and 16 whole-picture captures"
  - "gate2/answers.json: the operator's approval, every answer with its source, the fixtures answer (keep) and versions [gate2-v1]"
  - "contrast-exceptions.json with every entry and decision answered (7 of 7 accepted, none pending)"
  - "WINDOWS ledger entry 33 waived by the operator's d1 answer; deferred-items.md records the df items as deferred by operator decision"
  - "tools/build-gate2.py (phase 99 builder)"
affects: []
tech-stack:
  added: []
  patterns: [one self-contained sheet with relative JPEG paths and eager images; every exception and visible-text change asked once with an explicit answer token]
key-files:
  created:
    - .planning/phases/99-shop-and-shop-management-ui-conversion/99-20-SUMMARY.md
  modified:
    - .planning/phases/99-shop-and-shop-management-ui-conversion/deferred-items.md
    - .planning/WINDOWS.md
    - .planning/STATE.md
    - .planning/ROADMAP.md
decisions:
  - "Gate 2 approved on sheet gate2-v1 with every recommended answer: every pair approved, a10 accepted, the inherited questions approved, e1 and e3 accepted, e2 acknowledged as inherited, d1 to d4 accepted, t1 to t11 accepted, df deferred, fixtures kept"
  - "d1:accept waives WINDOWS entry 33 (no submit lock on the shop forms, as at baseline); it stays a candidate follow-up of one data-disable-on-submit attribute per form plus a test"
  - "df:defer: the Draft Details authorization gap, Markdown images as a tracking pixel, dialog dates and the stale guard message stay deferred by operator decision; the authorization gap is worth its own small plan next"
metrics:
  tasks: 3
  commits: 1
  completed: 2026-11-17
status: complete
actuals:
  tokens: 9000
  tasks: 3
  commits: 1
---

# Phase 99 Plan 20: Gate 2 and release handoff Summary

The operator approved the Gate 2 sheet (gate2-v1) for the shop and shop management conversion with every recommended answer, so the phase is ready for its one release, v6.4.0, through a pull request the operator opens; no repository source changed in this plan.

## Gate 2 decision

- **Sheet versions:** `gate2-v1` only (no rejection, so no gap closure and no v2). The package is `~/qb-ui-review/phase-99/gate2/` (index.html, README.md, img/ with 375 JPEGs, answers.json); `gate-hygiene.py gate2` passes with answers.json in place (`hygiene ok: 375 jpeg, entries ['README.md', 'answers.json', 'img', 'index.html']`).
- **Contents:** 129 before and after pairs, 11 after-only pairs beside their approved Gate 1 frames, 4 drawer pairs, 16 whole-picture captures; 140 AFTER records with 0 errors, 0 contrast failures, 0 missing focus rings and 0 sideways scroll.
- **Operator, 2026-11-17:** "mockups gate 2 look good so far. I didn't see anything that at first glance that needed changing". Asked explicitly whether to record every recommended answer, including d1 (which waives WINDOWS entry 33) and df (defer), the operator chose "Approve all (Recommended)".
- **Reply line recorded:**

      approved; pairs:approved a10:accept q:approved e1:accept e2:acknowledge e3:accept d1:accept d2:accept d3:accept d4:accept t1:accept t2:accept t3:accept t4:accept t5:accept t6:accept t7:accept t8:accept t9:accept t10:accept t11:accept df:defer fixtures:keep

- **Visible-text changes:** t1 to t11 are all accepted. They are the phone empty states and "No Items Yet"; "Filter & Sort" with its "Active" badge and "Rarity" legend; the Preview dialog wording; "Pending Review" and "Available"; "Magic Item" and "Quest Item"; "Nov 5"; the Quest Items scroll icon; the required markers; the desktop Edit price hint (found by the executor, not in the plan's strings table); the Denied and Archived notice; and the My Transactions toggle with its count pill.
- **Inherited questions (q):** the outcomes of Q-56 to Q-75, Q-120, Q-121, Q-128 and Q-137 as built are approved.
- **Disclosed on the sheet:** in the 99-19 closing sweep, up to two Sell to Shop posts of the unlimited Ember-Forged Traveler's Pack went unrecorded. A first run of the click tool timed out after its form had probably posted. Those posts cannot be told apart in the history, and the item is unlimited, so stock did not change. Quantity validation is listed under "Resolved since Gate 1" (99-21, `5b77dddc`).
- Answers with their source: `~/qb-ui-review/phase-99/gate2/answers.json` (`approved: true`, 23 answers, `fixtures.answer: keep`, `versions: ["gate2-v1"]`).

## Exceptions

Each is recorded in `~/qb-ui-review/phase-99/contrast-exceptions.json` with `approved.status: accepted`, the answer token, the date and the source; none is left pending.

| Id | Entry | Answer | Consequence |
|---|---|---|---|
| e1 | `exception-99-toast-close-21px`: the merchant toast's Close is 21 by 21 px on desktop (same before; lone target with clear space) | accept | accepted under the 2.5.8 spacing allowance; raising it to 24 px stays an optional follow-up |
| e2 | `exception-99-editor-inherited`: the shared Markdown editor's 16 px hidden source box on desktop and 24 px Description region on the phone (unchanged by this phase) | acknowledge | recorded as inherited, not an exception of this phase; the editor toolbar measures 44 by 44 px, so the UI contract's 30 to 34 px figure no longer reproduces |
| e3 | `exception-99-text-spacing-320-edit`: Edit at 320 px overshoots by 1 px only under the full text-spacing stress (same before) | accept | no change |
| d1 | `decision-99-no-submit-lock`: no shop form opts in to the site.js submit lock, at baseline or now | accept | WINDOWS entry 33 waived (no longer blocks /gsd-ship); still a candidate follow-up: `data-disable-on-submit` on each shop form (one attribute per form) plus a test |
| d2 | `decision-99-look-phone-drawer`: the phone history card has no glass panel of its own and a 60 px right pad, because the approved drawer is the panel | accept | as built |
| d3 | `decision-99-look-phone-action-row`: the phone form action row stretches (the foundation row) instead of centring | accept | as built |
| d4 | `decision-99-unmoved-sample`: the quest board and Calendar proved unmoved on a board with no quests (0 differences) | accept | no comparison on a busier board (the local database holds real member data) |

Deferred by operator decision (`df:defer`), recorded in `deferred-items.md`:

- any member can open a Draft item's Details by id. This is an authorization gap, worth its own small plan next.
- Markdown images in item descriptions can act as a tracking pixel.
- Dialog dates keep the server wording until the page reloads.
- The stale `QuestsConversionGuardTests` message.

## Owned findings

All 29 owned findings are accounted for (`findings-trace.json`). 28 are closed with their owning plan and BEFORE and AFTER evidence on the sheet, and one is carried:

| Owning plan | Findings closed |
|---|---|
| 99-04 | A-shop-017 (the dialog's Sell confirmation never appeared) |
| 99-08 | A-shop-012, A-shop-020 (rarity chips) |
| 99-10 | A-shop-004, A-shop-005, A-shop-018, A-shop-019, A-shop-029 (details dialog and Details page) |
| 99-11 | A-shop-024, A-shop-026 (Create and Edit desktop) |
| 99-12 | A-shop-009, A-shop-025 (phone forms) |
| 99-13 | A-shop-027 (Preview) |
| 99-14 | A-shop-006, A-shop-007, A-shop-008, A-shop-021, A-shop-022, A-shop-023 (management list) |
| 99-15 | A-shop-001, A-shop-011, A-shop-014 (desktop browse) |
| 99-16 | A-shop-002, A-shop-003, A-shop-013, A-shop-015 (history and merchant) |
| 99-17 | A-shop-016, A-shop-028 (phone shop) |

A-shop-010 (item creation always failed) is **carried**: fixed separately before the phase and proven by 99-02, which created 36 items through the form. The operator accepted it (a10:accept). No owned finding stands open.

## Release handoff

The phase is ready for one release, **v6.4.0**, through a pull request from `milestone/v9-rolling-improvements` into `main` that the operator opens, followed by the existing draft-release deploy approval. The operator chooses when. Every slice is on the milestone branch. No plan of this phase opened a pull request, created a tag, made a release or pushed to `main`; this plan pushed nothing.

**Caveat:** `docker-publish.yml` publishes the image on every tag push regardless of the draft-release approval. Do not push a tag before the image should be published.

Still open after the release, outside the release itself: the phase UAT below and the deferred items above. The verification steps (verify-work, code review, validate, secure) were not run by this plan.

## Fixtures for the operator (kept)

Kept by the operator's answer `fixtures:keep`. Every fixture was created through the app's own forms; nothing was written by SQL and no existing account, membership or data changed.

- **Accounts (3):** p99player (Player), p99dm (Dungeon Master) and p99empty (Dungeon Master of the empty board).
- **Boards (2):** P99 Audit Shop (One-Shot, Shop on; all three accounts) and P99 Audit Empty (One-Shot, Shop on; p99empty).
- **Items (37):** filler, edge and status items. The denial reason is on Hexbound Siege Ring.
- **Purchases (22 recorded):** 18 aging purchases from 2026-11-16 and 4 fresh ones. The sweep returned the 2 fresh 99-19 purchases and sold 2 aging ones.
- **Not in the fixture list:** the stray archived "Ember-Forged Traveler's Pack" (id 5485, p99dm), and up to two unrecorded Sell to Shop posts of the unlimited Ember-Forged Traveler's Pack from the timed-out 99-19 tool run.

## Phase UAT checklist

For the operator after deploy (the plan's backstop truth; DoC-7):

- [ ] On a real phone with touch, go through the whole shop area: Return and Sell in the My Transactions drawer, the details dialog, the filter drawer, Details, and the management list, Create and Edit.
- [ ] Use a screen reader on the merchant announcement (desktop) and on the dialogs (details, Preview, Deny, Bulk Actions, the Sell confirmation).
- [ ] Production culture check for the "Nov 5" date wording: confirm that availability dates on production read as accepted at Gate 1 item 11 and t6.

## Deviations from Plan

None. Gate 2 was approved on the first sheet, so no gap closure ran. Two items were recorded as the operator's answers required, beyond the plan's listed `--files`:
- WINDOWS entry 33 was waived with `gsd-tools windows waive 33`, which keeps the table row, the JSON entry and the counts consistent: open 6 to 5, waived 7 to 8, total 33.
- `.planning/WINDOWS.md` was added to the docs commit.

## Known Stubs

None. This plan made no source change.

## Threat Flags

None. No new surface. T-99-54 is mitigated: no tag points at HEAD, the branch is the milestone branch, and nothing was pushed.

## Self-Check: PASSED

- FOUND: 99-20-SUMMARY.md, gate2/answers.json, gate2/index.html, gate2/README.md; both JSON files parse; contrast-exceptions.json holds 0 pending and 7 accepted
- The docs commit carries the SUMMARY, deferred-items.md, STATE.md, ROADMAP.md and WINDOWS.md; `docs-ok` printed; no tag points at HEAD; the branch is milestone/v9-rolling-improvements
