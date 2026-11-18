---
phase: 100-admin-and-platform-ui-conversion
plan: 02
subsystem: review-fixtures
tags: [fixtures, platform-forms, playwright, scratch-environment, empty-states]
requires: ["100-01"]
provides:
  - "p100 fixtures created through the app's own forms: five boards, six accounts (plus the reused platform account as a Player), memberships, two linked sets, five quests"
  - "~/qb-ui-review/phase-100/fixtures.json (no secrets): ids, roles, boards, sets, quests, emptyStates, unreachable, auditBoardMembersUnchanged"
  - "register100.mjs (idempotent boards, accounts, memberships, secrets, sets) and fixtures.mjs (--quests, --empty-states, --check)"
affects: [100-03, 100-04, all later Phase 100 plans]
tech-stack:
  added: []
  patterns: ["idempotent form-driven fixture tooling", "secrets only in credentials.json (mode 600)", "check re-reads every fixture under both User-Agents"]
key-files:
  created: []
  modified: []
decisions:
  - "The Members Create New User modal is submitted with the CreateMember. prefix removed from its own field names, because the action binds unprefixed names (recorded in deferred-items.md)"
  - "login.mjs picks the account's own board (credentials entry) before the global board, so an account on a p100 board signs in to that board"
  - "Tracer gate treated as autonomous: the tracer verify was re-run end to end and passed before expansion"
metrics:
  duration: "about 45 minutes"
  completed: 2026-11-18
status: complete
actuals:
  tokens: 7500
  tasks: 3
  commits: 1
---

# Phase 100 Plan 02: p100 fixtures Summary

The p100 fixture set now exists on the local app, created only through the app's own forms by the reused SuperAdmin (and the p100 Dungeon Master and Campaign Admin for quests): five boards, six fantasy-named accounts, the memberships, two linked sets and five quests. `fixtures.mjs --check` re-reads all of it under both User-Agents and passes, and the baseline board's member count is the same as before. No repository source file changed.

## Fixtures created (fantasy names, no secrets)

Boards (Platform > Group > Create): P100 Audit One-Shot (One-Shot), P100 Audit Campaign (Campaign), P100 Audit Campaign Two (Campaign, no members), P100 Audit Empty (One-Shot, no members, no quests), P100 Audit Lone (One-Shot, no members).

| Account (all @example.invalid) | Display name | Board and role | Created through |
|---|---|---|---|
| qb-audit-p100admin | Maerwen Ashgrove the Ledger-Keeper | One-Shot: Admin | Members > Create New User |
| qb-audit-p100dm | Vashti Emberweir | One-Shot: Dungeon Master | Members > Create New User |
| qb-audit-p100player | Brindle Oakenfeather | One-Shot: Player | Members > Create New User |
| qb-audit-p100unconfirmed | Tamsin Hollowreed | One-Shot: Player, no credential set (unconfirmed, with an address) | Members > Create New User |
| qb-audit-p100otheradmin | Orrin Duskmantle | Campaign: Admin; One-Shot: Player | Create New User on Campaign, then Members > Add on One-Shot |
| qb-audit-p100longname | 75-character fantasy name | One-Shot: Player | Members > Create New User |

- Credentials for p100admin, p100dm, p100player, p100otheradmin and p100longname were set through Admin > Reset Password with the right p100 board opened through Open Board, and exist only in `credentials.json` (mode 600); p100unconfirmed has none. `fixtures.json` was checked: it holds no stored secret and not even the word for it.
- Linked sets (Platform > Linked Boards): "P100 Audit Set" (Campaign, Active) holds the two Campaign boards; "P100 Audit Lone Set" (One-Shot, Inactive) holds P100 Audit Lone.
- SuperAdmin as a member: operator answered yes, so the reused platform account was added to P100 Audit One-Shot as a Player through Members > Add. Nothing else about it changed.

## Quests (Quest > Create, then Manage)

| Quest | Board | Made by | Status read back from Manage |
|---|---|---|---|
| The Lantern of Hollowmere | One-Shot | p100dm | open |
| The Siege of Thistlegate | One-Shot | p100dm, finalized through Manage | finalized |
| A Long Tale of the Gloamwood Pact (long Markdown: paragraphs, heading, list, emphasis) | One-Shot | p100dm | open |
| The Ember Road Chronicle | Campaign | p100otheradmin | open |
| The Ashen Crown Chronicle | Campaign | p100otheradmin, closed through Manage | closed |

Unreachable through any form, recorded as render-test-only in `fixtures.json` (`unreachable`): a played One-Shot quest (finalized date in the past; Create needs future dates), and a quest with a follow-up on another board.

## Empty states (94-AUDIT-admin.md "Empty-state copy")

| State | Reachability |
|---|---|
| Admin/Users "No users found." | live: SuperAdmin with P100 Audit Empty open, `/Admin/Users` |
| Admin/Quests "No quests found." | live: SuperAdmin with P100 Audit Empty open, `/Admin/Quests` |
| Admin/EmailStats "ResendApiKey not configured" | live: `/Admin/EmailStats` (the local app has no provider key) |
| Admin/EmailStats "Could not fetch email stats" | render-test-only: needs the provider to answer with an error |
| Platform Group/Index "No groups yet." | render-test-only: real boards exist |
| Group/Members "No members in this group yet." | live: SuperAdmin, `/platform/Group/Members/<P100 Audit Empty id>` |
| Group/Members "No members match" and "No users match" | live: same board with `?memberSearch=` / `?search=` |
| Group/Members "All users are already members of this group." | render-test-only: accounts on no p100 board always exist |
| Platform Users/Index "No users found." | render-test-only: real accounts, no search on the page |
| LinkedBoards "No linked sets yet." | render-test-only: sets always exist |
| LinkedBoards "Inactive: add at least one more board" | live: SuperAdmin, `/platform/LinkedBoards` (P100 Audit Lone Set) |
| LinkedBoards "No boards in this set yet." | render-test-only: an empty set would be a third fixture the operator did not approve |
| LinkedBoards "No unlinked ... boards are available." | render-test-only: other unlinked boards of the set type exist |

## Checks

- Task 1 verify: Admin/Users as p100admin rendered 200 on `/Admin/Users`, board recorded, credentials mode 600, printed `ok`; a second run created nothing and changed no secret.
- Task 2 verify: `register100.mjs` (second full run: created 0, nothing changed), `fixtures.mjs --quests` (5 quests recognised as already present), `fixtures.mjs --check` ("check passed under both User-Agents") and the fixture-set assertion printed `ok`; `auditBoardMembersUnchanged` is true (count read from Platform > Group before and after).
- `git status --porcelain` for the three source projects prints nothing; email suppression stayed forced by `run-app.mjs` (verified on the process); every state-changing action targeted p100 rows only. The app started for this plan was stopped with `stop-app.mjs` (ports 8340 and 8341 free).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug, found not fixed] Create New User modal cannot post as built**
- **Found during:** Task 1 (tracer)
- **Issue:** The modal's fields post as `CreateMember.Email` and so on, the `CreateMember` action binds unprefixed fields; a submit as built arrives empty, fails validation and re-renders the Members page without a visible message. This is a product bug in a file this plan must not change.
- **Fix:** The tool submits the same form (same endpoint, same antiforgery token) after dropping the `CreateMember.` prefix from the form's own field names. Logged in `deferred-items.md` for an owner.
- **Files modified:** none in the repository (scratch tool only)
- **Commit:** none

**2. [Rule 3 - Blocking] login tool used one global board**
- **Found during:** Task 1
- **Issue:** `login.mjs` picked `credentials.board` for every role, but p100 accounts live on p100 boards.
- **Fix:** it now picks the account's own `board` entry first (scratch tool).
- **Commit:** none

### Planning notes

- The tracer gate was run as autonomous: after Task 1 the tracer verify was re-run and passed before Task 2 began. The config flags `workflow._auto_chain_active` and `workflow.auto_advance` both read false, but the spawning orchestrator ran this plan sequentially and unattended, as 100-01 was.
- Task 2's `fixtures.mjs` replaces the phase-99 shop fixtures script (kept as `tools/fixtures99.mjs.keep`).
- Tasks 1 and 2 changed nothing in the repository, so they have no per-task commits; the only commit is the docs commit.
- The platform account's display name in `fixtures.json` is a placeholder; its real name is visible on Admin/Users and is an audit account name, not a real person.

## Authentication Gates

None.

## Known Stubs

None (no repository source created or modified).

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: form-binding | QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml | the Create New User modal posts prefixed field names the action does not bind; see `deferred-items.md` (a correctness bug, not a trust-boundary change) |

## Task Commits

- Task 1: scratch tooling and fixtures outside the repository (no commit).
- Task 2: scratch tooling and fixtures outside the repository (no commit).
- Task 3: docs commit carrying this SUMMARY, `deferred-items.md`, STATE.md and ROADMAP.md.

## What the next plans must know

- `fixtures.json` keys: `boards` (key, id, type, members), `accounts` (email, name, id, memberships, `credentialsRole`), `sets`, `quests` (id, board, status, `by` role), `emptyStates`, `unreachable`, `auditBoardMembersAtStart`/`Unchanged`. Board ids: one-shot 25, campaign 26, campaign-two 27, empty 28, lone 29; sets 2004 (active) and 2005 (inactive); quests 992060 to 992064.
- Roles in `credentials.json`: superadmin, admin, p100admin, p100dm, p100player, p100otheradmin, p100longname. `stateFor('<role>')` signs each in to its own board; `p100otheradmin` lands on P100 Audit Campaign, its One-Shot membership is reachable by `--board`.
- The phone Admin/Users list shows display names only (no addresses); checks on the phone twin must match on names.
- `register100.mjs` and `fixtures.mjs --check` are safe to re-run; `fixtures.mjs --quests` also re-classifies the empty states.

## Self-Check: PASSED

Scratch artifacts present (`register100.mjs`, `fixtures.mjs`, `fixtures.json`, `credentials.json` mode 600 with the five p100 roles and none for the unconfirmed account); repository source trees unchanged; no listener on 8340 or 8341.
