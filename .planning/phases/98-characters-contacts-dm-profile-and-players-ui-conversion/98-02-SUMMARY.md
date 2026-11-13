---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 02
subsystem: review-fixtures
tags: [ui-review, playwright, fixtures, review-environment]
requires: [98-01]
provides:
  - three p98 review accounts and five P98 Audit boards on the local database, made through the app's own forms
  - fixtures.json (31 keys), fixtures-check.json (26 keys, desktop and mobile), contrast-exceptions.json
  - tools register98.mjs and fixtures.mjs (look-up-before-create builder with a --check mode)
affects: [98-03, all later Phase 98 plans]
tech-stack:
  added: []
  patterns: [look-up-by-fantasy-name before every create; one Playwright context per role and board; --check loads each key under desktop and Pixel 7 User-Agents]
key-files:
  created: []
  modified: []
decisions:
  - "Quest rows are declared review fixtures (fixtures.json field declared); Gate 1 item 15 lists them"
  - "quest.played (a one-shot quest finalized on a past date) is added as the live route to QuestLog/Details with a character without a picture, because the app gives campaign quests no signup and the campaign Quest Log hides its Adventurers section"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 98 Plan 02: Fixtures Summary

All three p98 accounts, the five P98 Audit boards and every character, category, contact, note, Dungeon Master profile and quest fixture now exist on the local database, made through the app's own forms, recorded by key in `~/qb-ui-review/phase-98/fixtures.json` and proven on their pages under both User-Agents. No repository file changed; the only commit is this summary. No tests were run (the plan has none).

## Tasks

1. **Tracer (Task 1):** `register98.mjs` created the three accounts through Admin > Create user and Admin > Reset password (passwords only in `credentials.json`, mode 600, never printed). `fixtures.mjs` was rewritten for this phase, with `--check`. Board.characters and `char.nopic` were made and checked; both User-Agents true. Tracer gate: verified end to end, expansion went ahead. The forced email overrides on the 8310 listener were confirmed in `/proc/<pid>/environ` before any account was created (SuppressSending true, SMTP 127.0.0.1:1025).
2. **Task 2:** the four other boards, seven more characters, three categories, six contacts, three notes, two Dungeon Master profiles.
3. **Task 3:** four quests, the two Players empty-state routes, the full `--check all` run and the idempotency re-run.

Board type chosen for "P98 Audit Characters": One-Shot, because the date vote, finalized date and Date TBD quest flows the Dungeon Master profile and Quest pages need exist only on one-shot boards; the Quest Log row for a campaign comes from P98 Audit Campaign.

## Accounts (created through Admin > Create user, passwords set through Admin > Reset password)

| Role key | Address | Display name | Role on the Audit Board |
|---|---|---|---|
| p98player | qb-audit-p98player@example.invalid | Tamsin Brightwillow | Player |
| p98dm | qb-audit-p98dm@example.invalid | Orlen Vashti of the Ember Weir | Dungeon Master |
| p98empty | qb-audit-p98empty@example.invalid | Wren Quietbrook | Player |

No existing qb-audit-* account, membership, password or data was changed. Admin session for the account creation was the existing `admin` state on the Audit Board; welcome emails were suppressed by the forced overrides.

## Boards (created by the superadmin through Platform > Group > Create; members through Platform > Group > Members)

| Board (id) | Type | Members | Page selection |
|---|---|---|---|
| P98 Audit Characters (18) | One-Shot | p98player Player, p98dm Dungeon Master | every page on (the default for a new board), so Characters, Contacts, Players, Dungeon Master profile, Quests and Quest Log are all enabled |
| P98 Audit Empty (19) | One-Shot | p98empty Dungeon Master (only member) | every page on |
| P98 Audit No Categories (20) | One-Shot | p98dm Dungeon Master, p98player Player | every page on except Dungeon Master profile, switched off through Platform > Group > Edit |
| P98 Audit No Dungeon Masters (21) | One-Shot | p98player Player (only member) | every page on (Players enabled) |
| P98 Audit Campaign (22) | Campaign | p98dm Dungeon Master, p98player Player | Quests, Quest Log, Characters, Contacts, Calendar and Events on; Players, Shop and Dungeon Master profile are refused by the Campaign board type itself |

## Fixture keys, the form used, and the check result

Every key below was found or created through the named form; `created` is true in `fixtures.json` for all of them. Desktop and mobile were both true in `fixtures-check.json` for every key that has a page (26 keys; the five boards have no url).

| Key | Form | Viewer role and board |
|---|---|---|
| char.nopic, char.main, char.dead, char.retired, char.longname | Characters/Create (picture via the crop dialog's Use This Crop for char.main and char.dead; char.main has two classes, a sheet link, Markdown description and backstory; char.longname has a 59-character name and a sheet link) | p98player, P98 Audit Characters |
| char.dm-pic | Characters/Create (picture) | p98dm, P98 Audit Characters |
| char.dm-maindead | Characters/Create as Main, then Characters/Edit to status Dead (the roster shows Main and Dead together) | p98dm, P98 Audit Characters |
| char.campaign-nopic | Characters/Create | p98player, P98 Audit Campaign |
| cat.allies, cat.rivals, cat.long (56-character name) | ContactCategoryManagement add form | p98dm, P98 Audit Characters |
| contact.full | Contacts/Create (picture, category, three tags, town, sub-location, Markdown description), Reveal toggle | p98player, P98 Audit Characters |
| note.long | Contacts/Details add-note form (three notes on contact.full; the long one has a heading, a long paragraph and an unbroken 80+ character string) | p98player, P98 Audit Characters |
| contact.bare, contact.tagged, contact.longname (78-character name) | Contacts/Create, Reveal toggle | p98player, P98 Audit Characters |
| contact.hidden | Contacts/Create (picture, category) and left hidden | p98dm, P98 Audit Characters |
| contact.nocat | Contacts/Create on a board with no categories, Reveal toggle | p98player, P98 Audit No Categories |
| dm.full | DungeonMaster/EditProfile (Markdown bio, photo) | p98player, P98 Audit Characters |
| dm.bare | none; p98empty's profile left as is (shows the verbatim "No bio provided yet.") | p98empty, P98 Audit Empty |
| quest.finalized | Quest/Create (one proposed date three weeks out), signup form as p98player with char.nopic, Quest/Manage finalize with the player selected | p98player, P98 Audit Characters |
| quest.undated | Quest/Create (one proposed date, never finalized) | p98player, P98 Audit Characters |
| quest.played | Quest/Create (a proposed date 45 days back), signup with char.nopic, Quest/Manage finalize; lists in the Quest Log with the Adventurers section | p98player, P98 Audit Characters |
| quest.logged | Quest/Create on the campaign board, Quest/Manage Close; QuestLog/Details answers for it | p98player, P98 Audit Campaign |
| players.empty-players | P98 Audit Empty viewed by p98empty (its only member, a Dungeon Master): "No Players Found" | p98empty |
| players.empty-dms | P98 Audit No Dungeon Masters viewed by p98player (its only member, a Player): "No Dungeon Masters Found" | p98player |

The Contacts page for p98dm lists the categorised contacts under Allies and Rivals and an "Ungrouped" section for the uncategorised ones, so the categorised and uncategorised states are both on one page. The Characters page shows My Characters (Main, Dead, Retired, a long name, one without a picture) and the Character Roster (Seraphine Ravenscroft, Varek Duskmantle the Undying as Main and Dead).

### Second-run created counts

A full `node fixtures.mjs` and `node register98.mjs` after everything existed: `created this run` totals 0 for every counter, and `accounts created this run: 0`. The Task 1 re-run (boards and char.nopic) also created 0.

## Unavailable keys and Gate 2 decisions

None. `contrast-exceptions.json` is `{ "entries": [], "retired": [], "decisions": [] }`. See deviation 3 for the one state whose route differs from the plan, and for what the operator may want to confirm.

## Gate 1 item 15: quest rows created here (review fixtures, declared)

`fixtures.json` carries `declared: "review fixture (D-11, Phase 94 D-25)"` on each. They were made through the app's change-tracked forms, and no source change in this phase writes Events or Quests. Rows: "The Lantern Gate Vigil" (id 992056, finalized), "The Unscheduled Embermarch" (992057, proposed, never finalized), "The Hollow Bridge Crossing" (992058, finalized on a past date, played), "The Wardens of the Long Road" (992059, closed campaign quest on P98 Audit Campaign).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The plan's Task 1 automated check read credentials.json wrongly**
- **Found during:** Task 1 verify
- **Issue:** the check asserted `{'p98player','p98dm','p98empty'} <= set(credentials)`, but credentials.json is `{ board, boardId, accounts: [...] }`, so it would always fail.
- **Fix:** ran the same assertions with the role keys read from `accounts`. The three roles are present and the file is mode 600.
- **Files modified:** none (check only)

**2. [Rule 1 - Bug] Board Type options are labelled OneShot and Campaign on the create form but listed as One-Shot**
- **Found during:** Task 1
- **Issue:** `selectOption({ label: 'One-Shot' })` timed out on the Create form.
- **Fix:** `ensureBoard` selects by option value (0 or 1) and compares the listed type afterwards. Nothing was created by the failed attempt.
- **Files modified:** `~/qb-ui-review/phase-98/tools/fixtures.mjs` (outside the repository)

### Plan interpretations and additions

**3. [Rule 2 - Missing coverage] quest.played added; quest.logged has no character**
- **Found during:** Task 3
- **Issue:** the app offers no signup on a Campaign board's quest page, and the campaign Quest Log hides the Adventurers section, so a character without a picture can never show in QuestLog/Details for a closed campaign quest. The plan's route for `quest.logged` (sign up with `char.campaign-nopic`, then close) therefore stops at the signup step.
- **Fix:** `quest.logged` is kept available (the closed campaign quest reaches QuestLog/Details and passes the check under both User-Agents) with a `note` explaining the above. The state the plan really wanted, the no-picture avatar in QuestLog/Details, is captured live through the added key `quest.played`: a one-shot quest finalized on a past date (Quest/Create accepts past proposed dates), whose Adventurers list shows the character without a picture as the fallback avatar. Because that state is captured live, no `ql-avatar-not-captured` Gate 2 decision was written; the operator may still want to reject this reading. `char.campaign-nopic` exists and is checked, but is on no roster.
- **Files modified:** `~/qb-ui-review/phase-98/tools/fixtures.mjs`, `fixtures.json` (outside the repository)

**4. One signup per account**
- quest.finalized and quest.played each carry `char.nopic` for p98player; a player has one signup per quest, so `char.main` cannot also sit on the same roster. The characters with pictures appear on the Characters page and roster instead.

**5. Tool shape**
- `FIX_BOARDS=characters` limits the boards step to one board (used for the tracer). Account display names for the three p98 accounts are fantasy names chosen here.

No repository source file was created or changed; `git status --porcelain` is clean for `QuestBoard.Service`, `QuestBoard.IntegrationTests` and `QuestBoard.UnitTests`.

## Known Stubs

None.

## Threat Flags

None. No new network endpoints, auth paths or file access surface; fixtures were created through existing forms; no request to `/feeds/calendar/` (every capture context aborts it) and no email left the machine.

## Self-Check: PASSED

- `fixtures.json` (31 keys), `fixtures-check.json` (26 keys, all desktop and mobile true), `contrast-exceptions.json` (empty lists), `tools/register98.mjs`, `tools/fixtures.mjs` and the p98 entries in `credentials.json` (mode 600) exist.
- The Task 1, Task 2 and Task 3 automated checks printed `ok` (Task 1 with the credentials read corrected, see deviation 1).
- `grep -c qb-audit-p98 fixtures-check.json` is 0; no calendar feed address appears in any json or md under the scratch root; the repository source trees are clean.
- App on port 8310 (pid started by the phase-98 toolset in 98-01) is still running; nothing was started or stopped here.
