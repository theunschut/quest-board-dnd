---
phase: 103-themed-error-pages
plan: 02
subsystem: tooling
tags: [fixtures, playwright, error-pages, multi-board]

requires:
  - phase: 103-themed-error-pages
    provides: scratch root, forced email suppression and operator answers (103-01)
provides:
  - three p103 boards, four p103 accounts, the cross-board memberships and the SuperAdmin memberships
  - shop items, a hidden contact and quests on P103 Audit One-Shot Two and P103 Audit Campaign
  - register103.mjs, fixtures.mjs (--records and --check) and fixtures.json
affects: [103-03, 103-04, 103-13, 103-15, 103-16]

actuals:
  tokens: 7000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Fixtures made only through the app's own forms by the reused SuperAdmin or by a p103 account; idempotent by name or email; passwords only in credentials.json"

key-files:
  created:
    - .planning/phases/103-themed-error-pages/103-02-SUMMARY.md
  modified:
    - .planning/STATE.md
    - .planning/ROADMAP.md

key-decisions:
  - "A Denied shop item is recorded as integration-test-only: the p103 boards have one Dungeon Master, and the leak is proven on the Draft item"
  - "login.mjs skips the board picker for an account with no board of its own (the platform account), so the picker of real boards is never visited"

requirements-completed: [D-03, D-04, D-05, D-19]

duration: 35min
completed: 2026-11-19
status: complete
---

# Phase 103 Plan 02: p103 fixtures Summary

**The p103 boards, accounts, memberships and records exist through the app's own forms, re-read cleanly under both User-Agents, with no SQL, no existing row changed and no repository source touched.**

## Performance

- **Duration:** about 35 min
- **Tasks:** 3 (a tracer, the full set, this summary)
- **Commits:** 1 (docs). Tasks 1 and 2 changed only scratch tooling outside the repository, so they have no commit.

## Fixtures created

All addresses are `@example.invalid`; passwords live only in `~/qb-ui-review/phase-103/credentials.json` (mode 600) and `fixtures.json` was checked to hold none.

| Kind | Name | Created through |
|------|------|-----------------|
| Board | P103 Audit One-Shot (One-Shot) | Platform > Group > Create |
| Board | P103 Audit One-Shot Two (One-Shot, so the Shop page exists) | Platform > Group > Create |
| Board | P103 Audit Campaign (Campaign) | Platform > Group > Create |
| Account | qb-audit-p103player (Brindle Oakenfeather): Player on One, Player on Two | Members > Create New User on One, then Members > Add on Two |
| Account | qb-audit-p103dm (Vashti Emberweir): Dungeon Master on Two, Player on One | Members > Create New User on Two, then Members > Add on One |
| Account | qb-audit-p103single (Tamsin Hollowreed): Player on One only | Members > Create New User on One |
| Account | qb-audit-p103admin (Maerwen Ashgrove the Ledger-Keeper): Admin of Campaign and of Two | Members > Create New User on Campaign, then Members > Add on Two |

Passwords for the four accounts were set through Admin > Reset Password with the account's board opened. Storage states are under `state/8350/`; `p103player@p103-audit-one-shot-two.json` and `p103dm@p103-audit-one-shot-two.json` start with Two active.

## The SuperAdmin

`superAdminAsMember` was yes, so the reused platform SuperAdmin was added as a Player of P103 Audit One-Shot and P103 Audit One-Shot Two through Members > Add, and nothing else about it changed. `fixtures.json` records `superAdminMember: true`. No capture of the picker or of Platform lists was made.

## Records

| Key | Record | Board | By | State |
|-----|--------|-------|----|-------|
| draft-item | P103 Ember Lantern (Rare magic item) | Two | p103dm | Draft ("Pending Review") until another Dungeon Master reviews it |
| scheduled-item | P103 Dawn Tonic (Common) | Two | p103dm | Published, sale start seven days ahead; absent from the member shop list |
| published-item | P103 Plain Rope (Common) | Two | p103dm | Published, no window; present in the member shop list |
| hidden-contact | P103 Veiled Informant | Two | p103dm | hidden, not revealed |
| open-quest | P103 Lantern Road | Two | p103dm | open (two future proposed dates) |
| future-finalized-quest | P103 Quiet Ford | Two | p103dm | finalized on a future date; the quest log refuses it to members until played |
| campaign-quest | P103 Far Keep | Campaign | p103admin | open |

Ids, board ids and roles are in `fixtures.json`.

## Integration-test-only states

- **One-Shot quest finalized on a past date.** The Create form requires future proposed dates and Finalize picks one of them, so no form reaches a past finalized date.
- **Denied shop item.** Deny needs a reviewer other than the creator; the p103 boards have a single Dungeon Master, and the leak is proven on the Draft item, so it stays out of the live set.

Neither was forced by SQL.

## Verification

- `register103.mjs` run twice: the second run creates nothing and changes no password (log: "nothing created, no secret changed"). The same holds for `fixtures.mjs --records`.
- `fixtures.mjs --check` passed under desktop and Pixel 7 User-Agents: each account signs in; p103player lists two boards on the picker and p103single is routed past it (one board); every record opens for its creator on its own board; the Draft and the scheduled items are absent from p103player's shop list on Two and the Published item is present.
- `git status` clean for Service, Domain, Repository and both test projects.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `login.mjs` required a board name, which the platform account does not have**
- **Found during:** Task 1
- **Issue:** phase 100's credentials carried a `board` for the SuperAdmin; this phase's `credentials.json` has none, so `getByText(undefined)` threw and no SuperAdmin state could be saved.
- **Fix:** with no board name, login skips the `/groups/pick` visit entirely (which would also have listed real boards) and saves the state straight after sign-in.
- **Files modified:** `~/qb-ui-review/phase-103/tools/login.mjs` (scratch, outside the repository)

**2. [Rule 1 - Bug] The copied Create New User modal had been renamed by phase 100**
- **Issue:** the script waited for `#CreateMember_Email` and relied on prefixed field names; the modal now uses `#createMemberEmail`, `#createMemberName`, `#createMemberRole` with unprefixed names, and the role option reads "Dungeon Master".
- **Fix:** selectors updated, the prefix workaround removed, a role-label mapping added.
- **Files modified:** `~/qb-ui-review/phase-103/tools/register103.mjs`

**3. [Rule 3 - Blocking] `fixtures.mjs` is a rewrite, not a copy**
- The phase 100 file's quest and empty-state steps do not fit this plan, so it was rewritten around `--records` and `--check`; records carry both `state` and `status` (the plan's verify reads `status`).

No authentication gate occurred.

## Known Stubs

None.

## Threat Flags

None. No repository source was created or changed.

## Left running

The working-tree app on port 8350 (started in 103-01, log `~/qb-ui-review/phase-103/logs/app-8350.log`) is still running. Nothing was started on 8351. SQL Server was not touched.

## Self-Check: PASSED

- `register103.mjs`, `fixtures.mjs`, `fixtures.json` and `credentials.json` (mode 600, entries for p103player, p103dm, p103single, p103admin and the SuperAdmin) present under `~/qb-ui-review/phase-103/`.
- No password found in `fixtures.json`; `git status` clean for all source projects.
