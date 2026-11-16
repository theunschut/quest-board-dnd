---
phase: 99-shop-and-shop-management-ui-conversion
plan: 02
subsystem: review-fixtures
tags: [ui-review, fixtures, playwright, shop]
requires: ["99-01"]
provides:
  - three p99 review accounts and two P99 boards made through the app's own forms
  - 30 listed shop items plus every status, stock, rarity and type state, and every empty-state route, addressable by fixture key
  - 18 day-0 aging purchases whose Return turns into Sell on a known time
  - fixtures.mjs (--check, --fresh-purchases, --sell-status) and register99.mjs in the scratch tools
affects: [99-03, 99-19, every later Phase 99 plan]
tech-stack:
  added: []
  patterns: [look-up-before-create fixture builder, per-key desktop and Pixel 7 page proof]
key-files:
  created: []
  modified: []
decisions:
  - "item.* keys carry the Details url and the role that can open them (p99player for Published, p99dm otherwise); the item's management status and rarity are read back from the owner's management list after the last step"
  - "An optional expectMobile per key records the phone text where it differs from desktop (phone shop shows No items found, no history; phone management shows No items yet and no denial reason)"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 9000
  tasks: 3
  commits: 1
---

# Phase 99 Plan 02: Fixtures Summary

All Phase 99 fixtures were created through the app's own forms (no SQL, no repository change): the p99 accounts, both boards, 36 shop items in every state, the empty-state routes and 18 day-0 purchases. 93 fixture keys exist; every one with a page answers 200 and shows its expected text under the desktop and the Pixel 7 User-Agent (`fixtures-check.json`, 72 of 72 keys true on both). The only commit is this docs commit (SUMMARY, STATE.md, ROADMAP.md); the three source trees are unchanged.

## Day-0 purchases and the Sell date

- Purchases made by p99player through the Purchase form on 2026-11-16 (UTC): earliest `purchase.aging.01` at 2026-11-16T11:07:02Z, latest `purchase.aging.18` at 2026-11-16T11:12:54Z.
- `sellEligibleAfter` is each purchase time plus 24 hours 5 minutes. The latest is **2026-11-17T11:17:54Z**: plan 19 (Sell captures) cannot start before then; `node tools/fixtures.mjs --sell-status` prints `{"eligible":false,"eligibleAt":"2026-11-17T11:17:54Z","agingCount":18}` and never waits.
- By item: `item.unlimited` 7 purchases, `item.longname` 6, `item.wizard` 5 (total 18). No other item was bought; no purchase by p99dm or p99empty; no stock-state item bought.
- No gold was needed. Proof: the first Purchase post redirected to `/Shop` (not back to Details with an error) and the next desktop `/Shop` render as p99player held a Return form for the item; all 18 posts did the same, none was refused.
- The history shows only the latest 15 rows, so with 18 aging purchases the oldest 3 are off the visible list from the start; `--fresh-purchases` (only when fewer than two purchases are younger than 20 hours; it made 0 now because 18 are) adds two more on top before a Return capture, pushing the oldest aging rows further out. After 24 hours the visible rows are the newest 15; plan 19 should expect that when choosing which row to Sell.

## Accounts and boards

- Accounts (Admin > Create user then Admin > Reset password, reused admin login): `qb-audit-p99player` (Player), `qb-audit-p99dm` and `qb-audit-p99empty` (Dungeon Masters), all @example.invalid; passwords only in `credentials.json` (mode 600), never printed. Admin > Create user also lists them on the Audit Board as the app's form does; no existing qb-audit-* account, board or membership was changed.
- P99 Audit Shop (board id 23): One-Shot; members p99player Player, p99dm Dungeon Master, p99empty Dungeon Master; Shop page on. Made by the superadmin through Platform > Group > Create, members added through Platform > Group > Members.
- P99 Audit Empty (board id 24): One-Shot; only member p99empty (Dungeon Master); Shop page on.
- Email: before register99.mjs ran, the 8320 listener's environment held `EmailSettings__SuppressSending=true`, `EmailSettings__SmtpServer=127.0.0.1`, `EmailSettings__SmtpPort=1025`.

## Fixture keys, form and account

All item rows were created through ShopManagement/Create. Item creation worked on first try (the separately fixed creation failure stays fixed).

| Keys | Form and acting account |
|---|---|
| item.unlimited, common, uncommon, low (qty 3), soldout (qty 0), longname (67 chars), wizard (`Wizard's "Staff"`), window (yesterday to +30 days), dndb (D&D Beyond link), wideimage (`/images/notice-board-1600.webp`) | Create by p99dm; Common and Uncommon auto-published |
| item.rare, veryrare, legendary | Create by p99dm (Draft), then Publish by p99empty from its Items Awaiting Review list |
| item.draft, item.questitem-draft | Create by p99dm, left Draft (Rare) |
| item.denied | Create by p99dm (Rare Draft), then Deny with a fantasy reason by p99empty through the deny dialog; never edited afterwards |
| item.archived, item.questitem-archived | Create by p99dm (auto-published), then Archive by p99dm |
| item.questitem-future | Create by p99dm (Quest Items, window starting in 30 days) |
| item.other-dm | Create by p99empty (Common) |
| item.draft-other | Create by p99empty (Rare Draft) |
| item.filler-01 to 16 | Create by p99dm (Common or Uncommon, Equipment or Magic Item, mixed stock) |
| purchase.aging.01 to 18 | Purchase form by p99player |

- Rare, Very Rare and Legendary items were published, and the Rare item denied, by the second Dungeon Master (p99empty); p99dm never acted on its own Rare+ items. Every Draft or Denied item is Rare or higher; no Quest Item is listed (Draft, Archived and future-window only), so the Quest Items category is the empty-category state.
- Listed items on P99 Audit Shop: 30 (three pages: `shop.page2` "Showing 13", `shop.page3` "Showing 25").
- Every `item.*` key records rarity and status as read back from the owner's management list (e.g. item.rare Rare/Published, item.draft Rare/Draft, item.denied Rare/Denied, item.questitem-draft Rare/Draft).
- Page keys: `shop.populated`, `shop.page2`, `shop.page3`, `shop.category-empty` (`/Shop?type=QuestItems`), `shop.search-empty`, `shop.filter-empty` (`?type=QuestItems&rarity=Rare`) and `history.empty` (p99dm), `history.populated` (p99player, "Showing latest 15 purchases"), `mgmt.index` (p99dm, shows the denial reason), `mgmt.empty` ("No Items Yet") and `mgmt.no-other` ("No Other Items") on P99 Audit Empty as p99empty, `shop.empty` (P99 Audit Empty), `details.*` for each named item, `edit.draft`, `edit.published`, `edit.denied` (GET only).
- Unavailable keys: none. No Gate 2 decision was needed; `contrast-exceptions.json` is `{ "entries": [], "retired": [], "decisions": [] }`.
- Phone expectations that differ (`expectMobile`): empty shop states "No items found", history keys the page title (the phone shop has no history), management empty "No items yet", `mgmt.index` the denied item's name (no denial reason on the phone list), edit pages "Edit Item", page 2 and 3 the first item name on that page.

## Idempotence and checks

- Second run of `fixtures.mjs` (all steps) and `register99.mjs`: created counts all 0 (`{"boards":0,"members":0,"settings":0,"items":0,"transitions":0,"purchases":0}` and "accounts created this run: 0").
- `fixtures.mjs --check all`: 72 keys, desktop and mobile true for every key; `grep -c qb-audit-p99 fixtures-check.json` prints 0.
- First creation run counts: boards 2, memberships 4, items 36, publish/deny/archive transitions 6, purchases 18.

## Deviations from Plan

**1. [Rule 1 - Bug] Task 1 verify read credentials.json as a flat dict**
- **Found during:** Task 1 verify.
- **Issue:** the plan's python check does `{'p99player','p99dm','p99empty'}<=set(c)`, but credentials.json is `{ board, boardId, accounts: [...] }`, so that expression is always false.
- **Fix:** ran the same check against the roles in `accounts` (all three present, mode 600). No file shape changed, because the login and capture tools depend on it.
- **Files modified:** none (verification wording only).

Otherwise none - plan executed as written. `shop.populated` expects the name of the first item card on page 1, re-recorded when the item set grew, so it stays true on a 30-item shop.

## Known Stubs

None. No repository file was created or changed.

## Threat Flags

None.

## Self-Check: PASSED

Scratch artifacts present (`fixtures.json`, `fixtures-check.json`, `contrast-exceptions.json`, `credentials.json` mode 600, `tools/register99.mjs`, `tools/fixtures.mjs`); `git status --porcelain` on QuestBoard.Service, QuestBoard.IntegrationTests and QuestBoard.UnitTests prints nothing.
