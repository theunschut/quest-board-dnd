# Phase 99: Shop and Shop management UI conversion - Research

**Researched:** 2026-11-15
**Domain:** ASP.NET Core 10 MVC Razor views, layered vanilla CSS on the Phase 94 foundation, vanilla ES modules, Playwright review tooling
**Confidence:** HIGH (every finding below was read in the code or probed in this session; the few exceptions are tagged `[ASSUMED]`)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01: Two placements are drawn at Gate 1 and the operator picks one there.**
  - (a) Today's floating bottom-right spot, which the operator likes. It is kept but no longer covers the footer: it stops at the content's bottom edge, or an equivalent.
  - (b) A real `<button>` labelled "Talk to the merchant" inside the gold-bordered Emporium hero card.
  Either way it becomes a focusable `<button>` with an accessible name, keyboard-operable, with a visible focus ring and a target of at least 24px. Nothing is built until Gate 1 picks one.
- **D-02: The merchant keeps its behaviour, made accessible.**
  - The toast keeps the same 20 random quotes, wording unchanged, and still hides itself.
  - It is announced politely (`role="status"` or an equivalent live region).
  - It stays visible while hovered or focused (WCAG 2.2.1).
  - The script moves out of the view into a module, and the inline `onclick` goes.
  - The quote text is inserted as text, not through an HTML string.
- **D-03: Preview on desktop ShopManagement/Create opens a live card preview dialog.**
  - It shows the item card as players will see it on Shop/Index: name, rarity chip, description, price and stock.
  - It is built in the browser from the form's current values. Nothing is saved and there is no new endpoint.
  - It uses the foundation's opaque modal surface and Bootstrap modal focus handling.
  - It is drawn at Gate 1.
  - The dead `#item-preview` rule goes.
- **D-04: View Details on desktop ShopManagement/Edit points at `/Shop/Details/{id}`**, the same target the management lists already use. The control stays. Research confirms that Shop/Details renders Draft and Denied items to a DM (the draft-review notice), so the link never lands on a 404 or an access-denied page for the editing DM.
- **D-05: Return and Sell (A-shop-002) and the sell script (A-shop-017):**
  - The purchase-row actions become ordinary visible, keyboard-reachable buttons, with both forms and their confirmations kept. Today they are permanently hidden by `.return-sell-buttons { opacity: 0; visibility: hidden }`.
  - The Sell confirmation and the "You will receive" total move to a delegated module bound by data attributes. That makes them work in the fetched modal and on the full Details page alike.
  - Confirmations stay native `confirm()` with today's wording, through the existing `wwwroot/js/confirm-submit.js` `data-confirm-message` pattern (Phase 95 and 98 precedent). This covers the management lists' Publish, Archive, Reopen and Delete confirms (A-shop-042) too.
- **D-06: Both management views and the Edit status alert use "Pending Review" for Draft and "Available" for Published.** "Archived" and "Denied" are unchanged. The enum is untouched, and this is display text only. The Edit views' `.Replace("Published", "Available")` gives way to one shared mapping, so the two layouts cannot drift. The desktop section heading "Items Pending Review" already matches.
- **D-07: The grey 250px image placeholder in `_ShopItemDetailsContent` becomes a shorter emblem tile**, about 120-160px, in the navy theme. It shows the item-type icon tinted in the item's rarity colour and is decorative (`aria-hidden`). Shop items have no picture field, so nothing is cropped and no `object-fit` rule is needed. The tile is drawn at Gate 1 in the modal and on Shop/Details, desktop and phone.
- **D-08: Every endless animation in the shop family stops, except a gentle merchant movement** (A-shop-013, WCAG 2.2.2).
  - Glows and shimmers stay as still styles.
  - The merchant may bob gently, but it stops after a few cycles and does not move at all under `prefers-reduced-motion: reduce`.
  - Hover and focus transitions stay short, with no `transition: all` and no layout-property animation (A-shop-033).
- **D-09: Rarity chips follow the signed-off Direction A shop render (`shop-a-*`).**
  - They are solid chips (common grey, uncommon green, rare teal, very rare purple, legendary gold).
  - Each chip's text colour is picked to reach 4.5:1, and the rarity name is always in text.
  - The same chip appears on desktop and phone, and on every shop and management page (A-shop-012, A-shop-016, A-shop-020, A-shop-022).
  - The Purchase button and the outline buttons reach the bar too (A-shop-011, A-shop-006, A-shop-021).
- **D-10: Phone purchase history (Q-56) sits under the item list and its paging links**, the same in-flow section as the desktop Q-120 relayout, with the same rows and Return and Sell forms. One structure on both layouts, drawn at Gate 1.
- **D-11: Two operator gates, reviewed from a local package. No review branch or ref is created or pushed.**
  - **Gate 1**, early, under `~/qb-ui-review/phase-99/gate1/` (index.html, README.md, img/). Scratch mockups, desktop and phone, approved before any of it is built: the Q-120 purchase history under the grid, desktop, and its D-10 phone counterpart; the Q-121 single filter card; both merchant placements (D-01), for the operator to pick one; the Preview dialog (D-03); the emblem tile in the details modal and on Shop/Details (D-07); a look check: one mockup per page type (the Shop/Index grid, Shop/Details, the management list, a management form), each side by side with `~/qb-ui-review/phase-94/mockups/direction-a/png/shop-a-*` and `shop-a--empty-*`, so the navy theme, glass opacity, card scrim, title colours and rarity chips match and cannot drift (the v6.0.2 lesson); any proposal from Claude's discretion below that changes visible copy or removes a control.
  - **Gate 2**, at the end, under `~/qb-ui-review/phase-99/gate2/`: one review sheet with every before/after pair, desktop and phone, with each contrast or target-size exception as an explicit accept or reject item.
- **D-12: A rejected AFTER is redrawn until the operator approves it.** Plan for a possible redraw loop at either gate.
- **D-13: Run on the Linux machine that ran Phases 94 to 98**, reusing `~/qb-ui-review/phase-94` (Playwright, tools, Direction A renders, BEFORE paths). BEFORE is re-captured from the current branch into `~/qb-ui-review/phase-99/`. Front-end work (mockups, gate sheets, view and CSS conversion) invokes the `senior-frontend` skill.
- **D-14: The first plan opens with an operator checkpoint.** The operator confirms SQL Server at `localhost:1433`, which the agent never provisions. The executor then creates this phase's own fixtures through the app's own forms (Phase 94 D-25). The local database holds real member data, so every fixture is clearly named and nothing else is touched.
  - Accounts: new `qb-audit-p99player@example.invalid`, `qb-audit-p99dm@example.invalid` and `qb-audit-p99empty@example.invalid`, created through Admin with the reused Audit Board admin login. Their passwords go only in `~/qb-ui-review/phase-99/credentials.json` (mode 600) and are never printed. No existing `qb-audit-*` account's memberships, passwords or data change.
  - Boards: new boards named for the phase, for example "P99 Audit Shop" for the populated pages and "P99 Audit Empty" for the empty states.
  - Items: every rarity and type, and every status (Draft, Published, Archived, Denied with a reason), created now that PR #168 fixed creation. They cover these stock states: unlimited, in stock, low ("N left") and sold out. Also included: long names, availability dates, a D&D Beyond link, and enough items to page.
  - Purchases: made by the p99 player, so that Return (24-hour window) and Sell both show. They are made shortly before each capture that needs Return. The fixture player needs gold, granted only through the app's own means. If no form can grant it, the executor stops and asks the operator. No SQL.
  - Empty states: every empty state listed in `94-AUDIT-shop.md`.
  - Fixtures are kept after the phase.
- **D-15: Any email the phase triggers goes to the Mailpit sink only** (`qb-mailpit`; starting it is pre-approved), never the real relay.
- **D-16: Work lands in slices on `milestone/v9-rolling-improvements` in this checkout, with one release at the end**, v6.4.0 through a PR into `main` once both gates have passed. Each slice leaves every page usable and the suite green (Phase 94 D-08). Real-phone and screen-reader checks are listed in the phase UAT and may run after deploy, as in Phases 96 to 98. Each plan's docs commit updates ROADMAP.md and STATE.md.

Inherited, recorded answers (not repeated in full): Q-56, Q-58, Q-59, Q-61, Q-63, Q-64, Q-65, Q-66, Q-71, Q-72, Q-75 are "add it to the other view"; Q-57, Q-60, Q-62, Q-67, Q-68, Q-69, Q-70, Q-73, Q-74 are deliberate and unchanged; no merges; Q-120 (purchase history in flow under the grid) and Q-121 (one filter card) approved in principle and re-confirmed with their AFTER at Gate 1; Q-128 Shop variant (a) navy theme on `<html data-theme="shop">`; Q-137 fixes the four breaks.

### Claude's Discretion

- **Bulk Actions (A-shop-041, Q-69).** The operator left this to research and planning. Advised default: keep the button and modal, restyle them on the foundation, and move the three `alert()` calls into a module with today's wording. Disabling the buttons with new copy, or removing the control, happens only as a Gate 1 proposal with the operator's explicit yes.
- **Preview on desktop Edit as well.** Q-73 recorded Create only. If the shared module makes it cheap, it may be proposed at Gate 1. It is not built without that yes.
- How the merchant stays clear of the footer in placement (a): sticky positioning inside the content, or an equivalent. Also the bob's amplitude and cycle count.
- The emblem tile's exact size, icon mapping per item type, and how the rarity tint reaches it.
- How the pricing script pasted into four views (A-shop-046) becomes one module. Its `alert()` validation stays native unless a Gate 1 item proposes otherwise.
- How `shop.css`, the five phone sheets and the Shop ranges of `legacy-pages.css` consolidate into page sheets loaded through `@section Styles`. Dead overrides go (WINDOWS 28), as does the `site.css` duplicate of shop rules (A-shop-031).
- Availability dates (A-shop-038): they move off the raw `ToString` to `Html.LocalTime` or `Html.WallClock`, whichever matches what the value is, without changing the helpers.
- Ports: a free pair, checked before use.
- Slice order, slice boundaries and the plan count. A sensible order: (1) the functional breaks and the shared modules (sell, confirm, pricing, merchant); (2) the details partial and Shop/Details; (3) the management pages; (4) Shop/Index with the gated relayouts, after Gate 1.

### Deferred Ideas (OUT OF SCOPE)

- Real bulk actions (archive, publish or delete several items at once) would be a new capability for a phase of its own, if wanted. This phase only converts the existing stub.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs exist for this phase (ROADMAP: "Requirements: TBD"). The planner tracks CONTEXT decisions D-01..D-16 and the ten-point "Definition of converted" instead.

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01, D-02 | Merchant button placement, accessible toast | Section "Merchant toast"; Bootstrap 5.3.8 toast pauses autohide on mouse and keyboard interaction (verified in the vendored bundle) |
| D-03 | Preview dialog | Section "Preview dialog"; existing `/markdown/preview` endpoint gives server-faithful plain text with no new endpoint |
| D-04 | View Details target | Priority 3 (confirmed, with one gap on Denied items) |
| D-05 | Return/Sell visible, sell module, confirms | Priority 1 sub-finding: the Return/Sell forms also lack an antiforgery token (a fifth break); sell quoting bug; modal injection |
| D-06 | One status vocabulary | `ShopItemViewModel.StatusDisplayName` already exists and is unused by views |
| D-07, D-08, D-09 | Emblem tile, motion, chips | Priority 4 (image inventory); animation inventory; chips need zero new colour tokens |
| D-10, D-11 to D-16 | Phone history, gates, fixtures, slices, release | Priorities 1, 5, 6; slice table; environment audit |
</phase_requirements>

## Summary

The phase is a conversion with five facts that change how it should be planned. **First, the app has no gold economy at all.** There is no balance on a user or a character, and `Purchase` debits nothing, so the "fixture player needs gold" assumption in D-14 is false: any signed-in board member can buy any Published, in-stock item (even a 0-price item) through the Purchase form. No operator checkpoint for gold is needed. A temporary integration probe this session bought a 0-price item with a user that has no gold anywhere (302 to `/Shop`). **Second, Sell on a purchase row only appears for purchases older than 24 hours** (the Return button shows inside 24 hours, Sell after), and the service stamps `TransactionDate = DateTime.UtcNow`, so the Sell state cannot be captured from a fresh fixture and cannot be backdated without SQL. The fixtures plan must make the "Sell-aging" purchases at least 24 hours before the Sell captures. **Third, the Return and Sell forms on Shop/Index would fail even once un-hidden**: they have an explicit `action=` and no antiforgery token, and both actions carry `[ValidateAntiForgeryToken]`. A rendered probe confirmed the forms contain no `__RequestVerificationToken`. This is a fifth functional break the conversion must fix. **Fourth, `legacy-pages.css` is now entirely Shop rules** except one four-line `.container.mt-4` rule that the group picker layout and DM profile pages still use and that four test files pin, so the sheet must be trimmed to that rule, not deleted, and ROADMAP's line ranges are stale. **Fifth, no layout seam for `data-theme` exists yet**; both `<html lang="en">` tags are bare.

Primary finding for design: the five rarity chips and the emblem icon tints need zero new colour tokens (Direction A built them from the existing `neutral`, `success`, `info`, `event`, `warning` token pairs, all already in the contrast table), so the 38-of-40 opaque hex budget is untouched. Shop items have no picture field and no `<img>` exists in the family, so the v6.3.1/v6.3.2 crop lesson is not triggered; the only image risks are the decorative emblem tile (must not clip its icon) and user-authored Markdown images in descriptions (sanitizer allows `img`, no sizing rule exists).

**Primary recommendation:** Plan 14 slices (opening checkpoint, fixtures with early purchase aging, Gate 1 in three plans, then gate-independent breaks and shared modules, details, management forms, management lists, Shop/Index desktop and phone, cleanup and guards, sweeps, Gate 2 and release hand-off), each plan's last task running the four gsd-tools state commands listed in Priority 6; do not request any gold grant, and fix the antiforgery tokens, the sell-name quoting bug and the first-`form` binding bug as part of the module work.

## Priority 1: Test gold path

**Verdict: Path C (neither A nor B). There is no gold in the data model, `Purchase` checks no balance, and Return and Sell need no gold. No form grants gold because nothing stores it. No blocking operator checkpoint for gold is required, and the executor must not stop for it.**

Evidence (all read this session):

| Question | Finding | Evidence |
|----------|---------|----------|
| Does a user have a gold/balance field? | No. `UserEntity` has `Name`, `HasKey`, `Quests`, `Signups`, `UserGroups` plus Identity fields | `QuestBoard.Repository/Entities/UserEntity.cs:6-19` (`public string Name`, `public bool HasKey`, `Quests`, `Signups`, `UserGroups`) |
| Does a character have gold? | No. Properties are `Id, Name, Level, SheetLink, Status, Role, Description, Backstory, OwnerId, Owner, CreatedAt, ProfileImage, Classes, PlayerSignups, GroupId, Group` | `QuestBoard.Repository/Entities/CharacterEntity.cs:7-50`; domain `Character.cs:6-44` |
| Anything named gold/balance/wallet/currency anywhere? | Only the word "gold" in a toast key `GoldReceived`, a `gp` price suffix and UI copy. `grep -i gold` over Service, Domain and Repository `.cs` (excluding migrations) returns only `ShopController.cs:176`, `GroupRepository.cs:124` (a comment), `TempDataKeys.cs:9` (a comment). No migration mentions gold | grep this session |
| What does Purchase debit? | Nothing. `PurchaseItemAsync` checks `item.Status == Published`, stock, then decrements item stock and inserts a `UserTransaction` with `Price = item.Price * quantity` | `QuestBoard.Domain/Services/ShopService.cs:76-117` |
| What does Return/Sell credit? | Nothing; it inserts a `Sell` transaction row with the refund amount, and the "Gold Received" toast is display only | `ShopService.cs:120-175`, `ShopController.cs:176` |
| Is `Quest Rewards` a value? | A free-text string, unattributed | `PROJECT.md` (Quest Rewards field entry) |
| Do any transaction types credit gold? | `TransactionType` is `Purchase, Trade, Sell, QuestReward`; `Trade` and `QuestReward` are never constructed anywhere | `QuestBoard.Domain/Enums/TransactionType.cs`: `Purchase, Trade, Sell, QuestReward`; grep for `QuestReward` finds only the enum |
| Can a 0-price item be bought? | Yes. `Price` has `[Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]` on Create and Edit | `CreateShopItemViewModel.cs:26-28`, `EditShopItemViewModel.cs:28-30`; and the probe below |
| Empirical proof | A throwaway integration test (created and deleted, tree clean) seeded a 0-price item, had a plain authenticated user with no gold POST `/Shop/Purchase` quantity 2: `302 /Shop`; the next GET `/Shop` rendered a Return form for that purchase | probe run this session |

Consequences for the plan:

1. Rewrite D-14's purchase bullet to: "the p99 player buys items through the Purchase form; no gold is needed because the app has no balance". Phase 94's note that a purchase "would spend gold" (94-AUDIT-shop A-shop-002 evidence) was wrong; there is nothing to spend. Price can be any value, including realistic prices (no need for 0-price items).
2. **Return and Sell timing.** `canReturn = DateTime.UtcNow - TransactionDate <= 24h` (`Shop/Index.cshtml:80-81`) decides which button renders: Return inside 24 hours, **Sell only after 24 hours**, never both for one purchase row. The service sets `TransactionDate = DateTime.UtcNow` (`ShopService.cs:111`) and no form accepts a date. So: (a) Return state: purchase shortly before each capture; (b) **Sell state: purchases must exist at least 24 hours before the capture**. The fixtures plan must make a set of "aging" purchases on day 0 and record each purchase's timestamp and its "Sell-eligible after" time in `fixtures.json`; the Sell captures (AFTER, desktop and phone) cannot run until 24 hours later. Plan this as a calendar dependency, not a task detail. SQL backdating is forbidden.
3. A "(Fully Returned/Sold)" greyed row and a "(Sold/Returned)" transaction row are produced by clicking Return on one fresh purchase (needs the antiforgery fix in Pitfall 1 first, so those rows can only be created in AFTER captures, not BEFORE).
4. The separate "Sell to Shop" block on Details (`SellToShop`) needs no purchase and no age; it works on any Published item at any time.
5. Purchases are scoped per board through the item (`QuestBoardContext.cs:652-657`, "UserTransactionEntity carries no GroupId of its own — scoped through its required ShopItem"), so the purchase history shows only on the board whose items were bought.

**Server observation, out of scope and not to be fixed without the operator:** `Purchase` and `Sell` accept any `int quantity` (no `>= 1` check in `ShopController.cs:87` or `ShopService.cs:76-117`); a zero or negative quantity writes odd rows (and a zero quantity makes `Sell` divide by zero at `ShopController.cs:135`). The forms constrain it with `min="1"` only. Record in the deferred items, do not change.

## Priority 2: Shop item creation fix

**Confirmed on this branch.** `git merge-base --is-ancestor 887e5dcd HEAD` succeeds (PR #168 merge); the fix commit is `1c0002a9` "fix(shop): stamp new shop items with the active board", changing `ShopManagementController.cs` (+5 lines). Current code: `ShopManagementController.cs:81-83` reads `// Tag the item to the active board ...` and `GroupId = activeGroupContext.RequireActiveGroupId(),`. Tests `ShopManagementControllerIntegrationTests` cover create, list, edit and delete on the board (`Create_Post_DungeonMaster_StoresItemOnTheActiveBoard`, `Create_Post_DungeonMaster_ItemIsListedOnTheManagementPage`, `Edit_Post_DungeonMaster_KeepsTheItemOnItsBoard`, `Delete_Post_DungeonMaster_RemovesDraftItem`).

Fixture-relevant behaviour of the Create/Edit/Publish actions (read in `ShopManagementController.cs`):

- **Common and Uncommon auto-publish on Create** (`:88-92`); **Rare, Very Rare and Legendary start as Draft** and need a *different* DM or Admin to publish them: `Publish` returns `Forbid()` when `item.CreatedByDmId == currentUser.Id` (`:183-186`). So the p99 DM creates Rare+ items and the **Audit Board admin login publishes them** (Admin satisfies `DungeonMasterOnly`; `architecture.md`). Denied-with-reason needs the same second account (`Deny` refuses your own item, `:233-237`, and only Draft can be denied, `:239-243`).
- **Archived** comes from `Archive` on a Published item (no ownership check, `:202-214`). **Denied** items reset to Draft (or Published for Common/Uncommon) when edited (`:151-162`), so capture Denied before any edit.
- **Sold out**: create with Quantity 0 (`[Range(-1, int.MaxValue)]`, `CreateShopItemViewModel.cs:34-36`), or buy the last unit. **Low**: Quantity 1 to 5 (`<= 5` is "left"). **Unlimited**: -1. **In stock**: 6 or more.
- **Availability window**: items outside their window vanish from Shop/Index (`ShopRepository.cs:96-97` filters `AvailableFrom <= UtcNow` and `AvailableUntil >= UtcNow`) but still list in management and open on Details by id. An item whose window includes today shows both the card and the dates.
- **Paging**: page size is 12 (`ShopController.cs:24`); 13 or more Published items give page 2; 25 or more give page 3 (the window renders an ellipsis from about page 5).
- Boards must be **One-Shot** boards with Shop on: `/ShopManagement` redirects to the quest list on Campaign boards (`BoardPageGateMatrixTests` `OneShotOnlyPages_OnCampaignBoard_RedirectToQuestListWithToast`).

## Priority 3: D-04 check (View Details on Edit)

**Confirmed, with one gap.**

- Root cause of A-shop-026: `Edit.cshtml:189` links `/ShopManagement/Details/@Model.Id`; `ShopManagementController` has actions `Index, Create (GET/POST), Edit (GET/POST), Publish, Archive, Deny, Reopen, Delete` and **no `Details`** (`ShopManagementController.cs:23-298`), so it 404s.
- `/Shop/Details/{id}` applies **no status filter**: `ShopController.Details` returns `NotFound()` only when `GetItemWithDetailsAsync` returns null (`ShopController.cs:66-72`), and the repository loads by id with no status predicate (`ShopRepository.cs:62-70`). Only the board query filter narrows it (`QuestBoardContext.cs:485`, `:652-657`). So the editing DM, on the item's own board, always gets a 200 page for Draft, Denied and Archived items.
- Draft: the partial renders the notice (`_ShopItemDetailsContent.cshtml:117-123` "This item is currently under review by the Dungeon Masters and is not yet available for purchase.") and, for signed-in users, a second one in the purchase area (`:189-195`).
- **Gap (Denied and Archived):** the partial has no branch for them. For `Denied`, the purchase area renders nothing (`:155` needs Published, `:189` needs Draft, `:196` needs Published), the Sell block is Published-only (`:205`), and **the denial reason is not shown** on Details. So CONTEXT's phrase "renders Draft and Denied items to a DM (the draft-review notice)" is only true for Draft; a Denied item renders a page with price, description and no notice. It never 404s, so D-04 holds, but the page reads oddly. Options for the planner: leave as is (no copy change), or propose a Denied/Archived notice as a Gate 1 copy item (visible copy needs operator approval, D-11). Recommended default: leave as is and list it as a Gate 1 proposal.
- Side observation (existing, unchanged): any signed-in board member can open `/Shop/Details/{draftId}` directly; non-DMs are not blocked from Draft details. Do not change.

## Priority 4: Show-the-whole-image rule

Phase 98's two patch releases fixed `aspect-ratio: 1 / 1` plus `object-fit: cover` on details portraits (commits `327e4d8d` and `744155ae`: the pictures lost their top and bottom). The guard it added scans portrait rules for `aspect-ratio` and `object-fit` (`CharactersConversionGuardTests.DetailsPortrait_ShowsTheWholePictureNotASquareCrop`, `:848-865`).

**Shop items have no picture field.** `ShopItemEntity.cs:7-59` properties, verbatim: `Id, Name, Description, Type, Rarity, Price, Quantity, Status, ReferenceUrl, DenialReason, DeniedAt, CreatedAt, AvailableFrom, AvailableUntil, CreatedByDmId, CreatedByDm, GroupId, Group, Transactions`. There is no `<img>` in any of the 11 files and no `url(`/`background-image`/`object-fit`/`aspect-ratio` in the six shop sheets (grep this session).

| Image or tile | Is it content that must show whole? | Safe CSS approach |
|---------------|-------------------------------------|-------------------|
| Emblem tile in `_ShopItemDetailsContent` (D-07) | It is an icon in a tile, decorative (`aria-hidden`). The icon must not be clipped | A block of fixed `min-block-size` in a token (about 8 to 10rem) with flex centring; icon sized by font-size token smaller than the tile; **no `overflow: hidden`, no `aspect-ratio`, no `object-fit`, no `height` in a literal**. Never `style=` (today's `style="height: 250px;"` at `_ShopItemDetailsContent.cshtml:9` goes) |
| Merchant button icon (`fa-user-tie`) | Decorative glyph | Button sized by `min-inline-size`/`min-block-size` tokens (at least 24px), icon centred, no clipping; the bob uses `transform` only, so it cannot crop it |
| Hero icon (`fa-store`) | Decorative glyph | Same: no fixed height with `overflow: hidden` |
| Board photo behind the page | Decorative backdrop; cover is correct by design | On the shop pages the theme replaces `--qb-board-image` with the navy gradient (`tokens.css` `[data-theme='shop']`), so there is no photo to crop; keep the photo behind `--qb-board-photo` (WINDOWS 22) |
| Markdown images inside an item description | **Yes, user-authored content.** `MarkdownService` `WebSanitizer` allows `img` (`MarkdownService.cs:117-119`), and `markdown-content.css` has no `img` rule (grep this session), so a wide image overflows the card or modal | In the shop sheet only: `.markdown-content img { max-inline-size: 100%; block-size: auto; }`. Never `aspect-ratio` or `object-fit` |
| Item card on Shop/Index, Direction A hero art | No images; the Direction A render draws icons only | n/a |

Guard to add: a `ShopConversionGuardTests` fact that no shop sheet contains `aspect-ratio` or `object-fit` anywhere (stricter than Phase 98's per-rule scan; the shop has no excuse for either), plus a scanner self-test, as Phase 98 did.

## Priority 5: Release constraints

- v6.4.0 ships through a PR from `milestone/v9-rolling-improvements` into `main` **that the operator opens**. Plans must create no PR, no tag, no release and never touch `main` (CLAUDE.md "Branching": never commit to `main`). Executors stage files explicitly and never push; the orchestrator pushes the milestone branch after each green wave (project memory), and re-pushes after each PR because the repo auto-deletes head branches (memory "Milestone branch deleted on merge").
- `docker-publish.yml` triggers `on: push: tags: [ 'v*.*.*' ]` (`.github/workflows/docker-publish.yml:11-13`) and `release.yml` triggers on `tags: 'v[0-9]+.[0-9]+.[0-9]+'` (`release.yml:3-6`). The image is published on every tag push regardless of the draft-release deploy approval, so **no plan may push a tag**; the hand-off plan only writes the release note and states the caveat, as `98-23-SUMMARY.md` did.
- No version file exists to bump (`grep` for the previous version in `*.props`, `*.csproj`, `*.json`, `*.md` outside `.planning` finds nothing); the version is the tag the operator creates. The release hand-off plan therefore has no code change.
- The previous phase's close-out also recorded: no deployment timing assumption, Gate 2 approval before the PR, UAT items for real phone and screen reader listed for after deploy.

## Priority 6: Docs-commit gap

**Verified in git.** `git show --name-only` over every `docs(98-NN)` commit: plans 98-01 to 98-12 touched neither `.planning/ROADMAP.md` nor `.planning/STATE.md` (e.g. `c87f32de`, `961fff7e`, ... `c7f8caec`, `2bd8495b`); plans 98-13 onward touched both (e.g. `f42ff41b`: `ROADMAP.md | 4 +-`, `STATE.md | 12 +-`, SUMMARY). Phase 97 shows the same gap (summary-only commits). **Root cause:** `execute-plan.md` skips `update_current_position` and `update_roadmap` when running in a worktree and excludes STATE and ROADMAP from the metadata commit "in parallel mode (orchestrator commits these)" (`~/.claude/gsd-core/workflows/execute-plan.md:398-411, 453-481, 490-510`), and `.planning/config.json` has `workflow.use_worktrees: true`; the orchestrator did not do the central sync for those plans. D-16 runs slices in this checkout, so each plan must do it itself.

Commands exist in `~/.claude/gsd-core/bin/gsd-tools.cjs` (verified: `roadmap` lists `analyze, get-phase, update-plan-progress, annotate-dependencies, validate, upgrade`; the state router carries `advance-plan`, `update-progress`, `record-metric`, `record-session`, `add-decision`). `roadmap.update-plan-progress` without an argument fails with "phase number required for roadmap update-plan-progress". Require this block at the end of every plan (run from the repository root; `gsd_run` resolves through `~/.claude/gsd-core/bin/gsd_run`):

```bash
# 1. SUMMARY is written first; then, in this checkout (never inside a worktree):
gsd_run query state.advance-plan
gsd_run query state.update-progress
gsd_run query state.record-session --stopped-at "Completed 99-NN-PLAN.md" --resume-file "None"
gsd_run query roadmap.update-plan-progress 99
# 2. Commit all three together:
gsd_run query commit "docs(99-NN): complete <plan name> plan" \
  --files .planning/phases/99-shop-and-shop-management-ui-conversion/99-NN-SUMMARY.md .planning/STATE.md .planning/ROADMAP.md
# 3. Verify the commit carries them (a plan fails its own check if either count is 0):
git show --name-only --format= HEAD | grep -c -E '(^|/)(STATE|ROADMAP)\.md$'   # expect 2
```

What a correct commit changes (read from `f42ff41b`): ROADMAP `**Plans:** N/M plans executed` and the plan's checkbox `- [ ]` to `- [x]`; STATE `stopped_at`, `last_updated`, `completed_plans`, `Plan: N of M`, "Session Continuity". `roadmap.update-plan-progress` counts PLAN against SUMMARY files on disk, so **the planner must first list every plan under "Plans:" in ROADMAP** (it currently says `0 plans` and `- [ ] TBD`) or the tool has nothing to tick. Also add `gsd_run query state.add-decision` per notable decision when the SUMMARY records one. The Phase 98 follow-up fix `be2e61d2` ("bring STATE position text up to date") shows STATE's free-text "Last activity" lines are not touched by these commands; the closing plan should patch them by hand.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Browse, filter, search, page items | API / Backend (MVC controller + repository query) | Browser (filter card, offcanvas) | Query and paging are server-side (`ShopRepository.GetPagedPublishedItemsAsync`) |
| Purchase, Return, Sell, Sell to Shop | API / Backend (`ShopService`, antiforgery-guarded POST) | Browser (confirm dialog, "You will receive" total) | Business rules and the ledger live in the service; browser only confirms and previews the total |
| Details modal content | Frontend Server (partial rendered by `Details` with `isModal`) | Browser (fetch and insert, delegated handlers) | HTML is server-rendered; scripts inside a fetched fragment never run, so behaviour must be delegated from the host page |
| Theme (navy shop look) | CDN / Static (`tokens.css` block) | Frontend Server (layout emits `data-theme` on `<html>` from `ViewData`) | Tokens are CSS; the page opt-in is one attribute |
| Merchant toast | Browser (module) | Frontend Server (button markup, quotes may be server-rendered) | Pure client behaviour; the quotes are static text |
| Preview dialog | Browser (module builds card from form values) | API (existing `/markdown/preview` for faithful plain text, optional) | D-03: no save and no new endpoint |
| Status and type display names | Frontend Server (view model properties) | — | One mapping on `ShopItemViewModel` stops twin drift (D-06) |
| Availability dates | Frontend Server (`Html.WallClock`) | Browser (`site.js` re-wording on load only) | Value has no zone; never converted |
| Purchase window "24 hours" | API (service decides Return versus Sell price) | Frontend Server (view decides which button shows) | The view's `DateTime.UtcNow` read is a real-instant read, not a board-day read |

## Standard Stack

No new packages. Everything is the project's existing stack.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core MVC + Razor | .NET SDK 10.0.115 (`dotnet --version`) | Views, tag helpers, view-location expander for `.Mobile` twins | Project stack (CLAUDE.md) |
| Bootstrap | 5.3.8, vendored `wwwroot/lib/bootstrap-5.3.8` | Modal, offcanvas, toast, grid | Already vendored; the toast already pauses autohide on mouse and keyboard interaction (`_hasMouseInteraction`, `_hasKeyboardInteraction`, `_onInteraction` present in `bootstrap.bundle.min.js`) |
| Font Awesome | 6.7.2 vendored | Icons including the emblem glyphs | Already vendored |
| EasyMDE | 2.21.0 vendored, via `Views/Quest/_QuestFormScripts.cshtml` | Markdown editor on Create/Edit | Existing; its `easyMDE` property is set at `DOMContentLoaded` (see Pitfall 5) |
| xUnit v3 + FluentAssertions + `Microsoft.AspNetCore.Mvc.Testing` + EF InMemory | repo versions | Static guards and render tests | Phase 94 to 98 pattern |
| Playwright | 1.64.1, reached through the symlink `~/qb-ui-review/phase-98/tools/node_modules` to `phase-94/tools/node_modules` | Gate captures, probes, contrast, a11y | Reused tooling; no install |

### Supporting
| Item | Purpose | When to Use |
|------|---------|-------------|
| `wwwroot/js/confirm-submit.js` | Native `confirm()` from `form[data-confirm-message]` | Management lists (12 forms) |
| `wwwroot/js/row-link.js` | Pattern for delegated listeners | Reference for the new modules |
| `Html.LocalTime`, `Html.WallClock` | Date rendering | Purchase rows (instants), availability window (wall-clock) |
| `/markdown/preview` (existing POST) | Plain text for the Preview card | Only if Gate 1 wants server-faithful card text |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| A client-side Markdown strip for the Preview card | Existing `/markdown/preview` then inert-parse and join top-level text with a space (mirrors `MarkdownService.ExtractPlainText`, `MarkdownService.cs:160-190`) | Faithful, one async request, token already on the page via the `request-verification-token` meta; the simple strip is synchronous but can differ from the real card |
| Deleting `legacy-pages.css` | Trim it to its one shared rule | Deleting needs the group picker layout, DM profile pages and four test files reworked; trimming keeps `.container.mt-4` behaviour and every pin green |

**Installation:** none. **Version verification:** no package is added or bumped, so there is nothing to look up on a registry.

## Package Legitimacy Audit

No external packages are installed or changed by this phase. Nothing to audit.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | — |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
 GET /Shop (desktop UA or phone UA)
   -> BoardPage gate (Shop on?) -> ShopController.Index
        -> ShopService.GetPagedPublishedItemsAsync (12 per page, board filter, window filter)
        -> ShopService.GetUserTransactionsWithRemainingAsync(user)
        -> view model (Items, filters, UserPurchases)
   -> Razor picks Index.cshtml or Index.Mobile.cshtml (view-location expander)
        -> view sets ViewData["Theme"]="shop" -> layout emits <html data-theme="shop">
        -> shop sheets (@section Styles) + shop modules (@section Scripts, type=module)

 In the page:
   filter card (GET form) -> same action with query string
   item card (name link or button) --click--> shop-item-modal.js
        fetch /Shop/Details/{id}?isModal=true  -> PartialView(Details or Details.Mobile)
        -> HTML inserted into #itemDetailsBody (clears old body first)
        -> delegated handlers (document-level) already bound:
              submit on [data-sell-form]  -> confirm(), uses data-item-name, data-unit-price, quantity input
              input  on [data-sell-quantity] -> updates the total text
        -> POST /Shop/Purchase | /Shop/SellToShop  (antiforgery token present in the fragment)
   purchase history section (in flow under grid, desktop and phone)
        -> POST /Shop/Sell (Return within 24h, Sell after) -- needs an antiforgery token (today missing)
   merchant <button> -> shop-merchant.js -> random quote -> Bootstrap Toast (role=status, textContent)

 GET /ShopManagement/{Index|Create|Edit} (DungeonMasterOnly)
   -> Create/Edit: shop-item-form.js (pricing hints, availability toggle, validation) + markdown editor
   -> Create (desktop): Preview button -> dialog built from form values (no request, or /markdown/preview)
   -> Index: forms with data-confirm-message (confirm-submit.js); deny modal module; bulk modal module
```

### Recommended Project Structure

```
QuestBoard.Service/
├── Views/Shop/{Index,Details}(.Mobile).cshtml          # set ViewData["Theme"]; link sheets; no inline code
├── Views/ShopManagement/{Index,Create,Edit}(.Mobile).cshtml
├── Views/Shared/_ShopItemDetailsContent.cshtml         # emblem tile, data-attribute sell form, no <script>
├── wwwroot/css/
│   ├── shop.css                  # layered (pages): chips, stock, emblem, purchase rows, cards, filter card, pagination, merchant, management table
│   ├── shop.mobile.css           # layered: phone-only deltas (grid, offcanvas, cards) - replaces the five phone sheets
│   └── shop-form.css             # layered: Create/Edit forms, both layouts (optional; planner may fold into shop.css)
├── wwwroot/js/
│   ├── shop-item-modal.js        # fetch + insert + stale-response guard + error line
│   ├── shop-item-sell.js         # delegated sell confirm + total (works in modal and on Details)
│   ├── shop-merchant.js          # quotes, toast, bob control
│   ├── shop-item-form.js         # pricing guide, availability toggle, validation, optional preview
│   └── shop-management.js        # deny modal loader, bulk-action notice
└── wwwroot/css/legacy-pages.css  # trimmed to the one shared .container.mt-4 rule
```

Module names are recommendations. Keep each as a small ES module that builds no markup from strings (a `ShopConversionGuardTests` scan, like `CharactersConversionGuardTests.ConfirmSubmitModule_WritesNoMarkupFromStrings`).

### Pattern 1: Page opts into the theme on `<html>`
**What:** The Phase 94 contract is `[data-theme='shop']` on `<html>` (tokens.css comment: "set on the html element it also recolours the page fallback background"). The theme block overrides `--qb-board-fallback`, `--qb-board-image`, `--qb-board-veil`, `--qb-vignette`, `--qb-surface-scrim`, `--qb-surface-glass`, `--qb-surface-glass-hover`, `--qb-surface-glass-border`, `--qb-surface-overlay`, `--qb-chrome-top`, `--qb-chrome-bottom`, `--qb-surface-chrome`, `--qb-fg-muted`, `--qb-action`, `--qb-action-hover`, `--qb-action-fg`, `--bs-dark-rgb` (`tokens.css:187-204`, read this session).
**Gap:** no layout reads a theme. `_Layout.cshtml:6` and `_Layout.Mobile.cshtml:6` are `<html lang="en">`; `grep data-theme` in Views, Controllers and Filters finds nothing. UI-SPEC A4 said the hook would be added when Shop variant (a) was chosen (`94-UI-SPEC.md:713`), and its wording used `<body>`; CONTEXT and `ui-guidelines.md` say `<html>`. **Use `<html>`.**
**Example:**
```cshtml
@* _Layout.cshtml and _Layout.Mobile.cshtml *@
<html lang="en" data-theme="@ViewData["Theme"]">

@* each shop view, at the top of its code block *@
@{ ViewData["Theme"] = "shop"; }
```
Razor omits an attribute whose value is null, so every other page renders no attribute. The view body runs before the layout, so the value is set in time. Add a guard test: both layouts emit it on `<html>` and not on `<body>`, and all ten shop views set it. The existing token contrast test already scores the shop theme on the same pair table as the default (`UiFoundationLayerGuardTests.cs:643-650`), so no new colour pairs are needed.

### Pattern 2: Delegated behaviour for fetched HTML
The modal body is filled by `innerHTML` (`Shop/Index.cshtml:466`), so scripts and inline handlers in the fragment never run (root of A-shop-017). Bind once on `document` and read data attributes:
```js
// shop-item-sell.js (type=module, loaded by Shop/Index, both layouts, and Shop/Details)
document.addEventListener('input', (event) => {
  const input = event.target.closest('[data-sell-quantity]');
  if (!input) return;
  const total = input.closest('[data-sell-form]')?.querySelector('[data-sell-total]');
  if (total) total.textContent = (Number(input.form.dataset.unitPrice) * Number(input.value || 0))
    .toLocaleString(undefined, { maximumFractionDigits: 0 });
});
document.addEventListener('submit', (event) => {
  const form = event.target.closest?.('form[data-sell-form]');
  if (!form) return;
  const quantity = form.querySelector('[data-sell-quantity]').value;
  const total = (Number(form.dataset.unitPrice) * Number(quantity)).toFixed(0);
  const message = `Are you sure you want to sell ${quantity}x ${form.dataset.itemName} to the shop for ${parseInt(total).toLocaleString()} gp?`;
  if (!confirm(message)) event.preventDefault();
});
```
The wording is today's (`_ShopItemDetailsContent.cshtml:260`). Razor HTML-encodes `data-item-name`, and `dataset` reads it back as plain text, which also removes the quoting bug in Pitfall 3.

### Pattern 3: Confirm on submit (established)
`confirm-submit.js` binds `form[data-confirm-message]` that exist **at load time** (`confirm-submit.js:6-12`), so it covers the 12 management forms but **not** forms inside the fetched modal. Phase 95/98 pages load it with `<script type="module" src="~/js/confirm-submit.js" asp-append-version="true">` in `@section Scripts`.

### Pattern 4: Layered page sheet, one per area
Wrap in `@layer pages { }`, no hex/rgb/hsl literals, no `!important`, no `transition: all`, no infinite animation except the capped merchant rule; add the sheet names to `LayeredFiles` and `NoLiteralFiles` in `UiFoundationLayerGuardTests.cs` (arrays at about `:92-104`).

### Anti-Patterns to Avoid
- **A new layered rule that loses to an old unlayered one.** `shop.css` and `legacy-pages.css` are unlayered and win on their own selectors; `shop.css` is in the global list (`_Layout.cshtml:25`) and `legacy-pages.css` at `:24` (and in `_Layout.GroupPicker.cshtml:15`). Each slice must delete the old rules for the classes it re-implements in the same commit (see Pitfall 6).
- **`document.querySelector('form')`** (see Pitfall 4).
- **Binding module state at module-evaluation time to the markdown editor** (Pitfall 5).
- **Writing quotes or names into `innerHTML`** (D-02; the existing toast builder interpolates into a template string).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Toast pause on hover or focus | A custom timer | Bootstrap `Toast` (5.3.8 already pauses autohide on mouse and keyboard interaction) | Verified in the vendored bundle |
| Dialog focus trap and return | Custom focus code | Bootstrap `.modal` with the foundation's opaque surface | D-03 says so; `components.css:588-` |
| Confirm dialogs | Per-view inline `onsubmit` | `confirm-submit.js` and `data-confirm-message` | Phase 95/98 pattern |
| Rarity and status colours | New tokens or literals | Existing token pairs: common `--qb-neutral`/`--qb-neutral-fg`, uncommon `--qb-success`/`-fg`, rare `--qb-info`/`-fg`, very rare `--qb-event`/`-fg`, legendary `--qb-warning`/`--qb-warning-fg` | Direction A's own chips (`mockups/direction-a/components.css:430-458`); all already in the contrast table (`UiFoundationLayerGuardTests.cs` "The {n} label on its fill") |
| Emblem icon tint on the navy tile | New colours | `--qb-neutral-text`, `--qb-success-text`, `--qb-info-text`, `--qb-event-text`, `--qb-warning-text` | Already checked at 4.5:1 on glass and overlay |
| Empty states | Bespoke `.shop-empty-state` | `.empty-state` component (`components.css:771-795`) | Foundation |
| Form action row | Custom flex | `.form-actions` (`components.css:859-891`) and `.form-page` | Foundation |
| Date rendering | `.ToString("MMM dd")` | `Html.WallClock(value, "date")` for availability, `Html.LocalTime(value, "date-compact")` for purchase rows | `ui-guidelines.md` |
| Plain text of a Markdown description | A regex stripper | The server `MarkdownService.ExtractPlainText` (cards) or the existing preview endpoint (Preview card) | Same rule as the real card |

**Key insight:** this phase adds behaviour modules and a layered sheet but no colours and no libraries; every visual value already exists as a token.

## Runtime State Inventory

Not a rename or migration phase. Omitted. (One related fact: fixtures are real rows in the local database; see Environment Availability.)

## Current state of every in-scope file (counts verified against ROADMAP)

**View counts match ROADMAP exactly:** 15 inline `style=`, 4 `<style>`, 9 inline `<script>`, 40 inline event handlers (re-counted with grep over the 11 files). **Stylesheet counts match when `legacy-pages.css` is counted whole:** `shop.css` 107 hex, 76 rgb, 70 `!important`; `shop.mobile.css` 8, 20, 12; `shop-details.mobile.css` 0, 7, 4; `shop-management-index.mobile.css` 0, 16, 7; `shop-management-create.mobile.css` 1, 9, 5; `shop-management-edit.mobile.css` 1, 9, 5; `legacy-pages.css` (whole file, 657 lines) 72, 72, 49. Totals: 189 hex, 209 rgb, 152 important, equal to ROADMAP. **ROADMAP's line ranges (39-298, 792-1184) are stale**: the file has 657 lines and every rule in it is Shop, **except** `.container.mt-4 { max-width: none }` (`legacy-pages.css:261-264`) and `.btn-sm.btn-action` (`:524-532`, used only by `ShopManagement/Index.cshtml`).

Per-view inventory (line numbers current):

| File | Lines | style= | `<style>` | `<script>` | Handlers |
|------|-------|--------|-----------|------------|----------|
| `Shop/Index.cshtml` | 542 | 1 (`:280`) | 0 | 1 (`:457-541`) | 3 (`:350`, `:357`, `:436`) |
| `Shop/Index.Mobile.cshtml` | 285 | 0 | 0 | 1 (`:261-284`) | 2 (`:202`, `:210`) |
| `Shop/Details.cshtml` and `.Mobile` | 53 and 58 | 0 | 0 | 0 | 0 |
| `Shared/_ShopItemDetailsContent.cshtml` | 267 | 1 (`:9`) | 0 | 1 (`:249-268`) | 2 (`:216`, `:227`) |
| `ShopManagement/Index.cshtml` | 518 | 8 | 0 | 1 (`:495-518`) | 11 (8 `onsubmit` at `:87, 219, 228, 237, 252, 374, 383, 393`; 3 bulk `onclick` at `:434, 437, 440`) |
| `ShopManagement/Index.Mobile.cshtml` | 229 | 5 | 0 | 1 (`:205-228`) | 7 (4 `onsubmit` at `:77, 86, 103, 112`; 3 `onclick` at `:144, 147, 150`) |
| `ShopManagement/Create.cshtml` | 311 | 0 | 1 (`:267`) | 1 (`:164-265`) | 5 (`:59, 87, 90, 118, 149`) |
| `ShopManagement/Create.Mobile.cshtml` | 221 | 0 | 1 (`:184`) | 1 (`:133-182`) | 2 (`:54, 98`) |
| `ShopManagement/Edit.cshtml` | 334 | 0 | 1 (`:318`) plus a stray `</script>` (`:334`) | 1 (`:204-316`) | 5 (`:58, 83, 117, 120, 147`) |
| `ShopManagement/Edit.Mobile.cshtml` | 265 | 0 | 1 (`:228`) | 1 (`:173-226`) | 3 (`:57, 78, 127`) |

Infinite animations: **16 `infinite` declarations, 10 distinct animations.** `legacy-pages.css` has 6 (`:154 legendary-glow`, `:484 magic-shimmer`, `:498 magic-shine`, `:540 quest-pulse`, `:561 quest-sweep`, `:571 quest-sparkle`), `shop.css` repeats the same 6 (`:157, 270, 284, 314, 335, 345`), and the four view `<style>` blocks repeat `legendary-glow` (`Create.cshtml:291`, `Edit.cshtml:326`, `Create.Mobile.cshtml:208`, `Edit.Mobile.cshtml:252`; they style `.rarity-*` that no element on those pages carries, so they are dead). ROADMAP's "ten" counts distinct sites correctly. All 16 go; the only allowed loop is the capped merchant rule (D-08), written with a finite `animation-iteration-count` and removed under `prefers-reduced-motion`.

Other facts worth knowing:

- **Phone Index already receives purchase data.** `ShopController.Index` fills `UserPurchases` for every request (`ShopController.cs:44-60`); `Index.Mobile.cshtml` just does not render it. Q-56 needs no controller change. Controllers need no change anywhere in this phase.
- **`ShopItemViewModel` already has `RarityDisplayName`, `StatusDisplayName`, `StatusColorClass`, `RarityColorClass` and `IsAvailable`, and no view uses them** (grep). `StatusDisplayName` returns `"Draft"`, `"Available"`, `"Archived"`, `"Denied"` (`ShopItemViewModel.cs:72-79`); D-06's shared mapping is a one-word change to `Draft => "Pending Review"` plus using it in all four management/edit views, and a `TypeDisplayName` property for A-shop-061 (raw `MagicItem` and `QuestItems` leak on Details today).
- **Phone Details has no Sell, Availability, Properties, D&D Beyond link, draft notice, sign-in branch or stock `max`** (`Details.Mobile.cshtml:1-58`) and ignores `ViewBag.IsModal`, so the modal repeats "Back to Shop". Because `MobileViewLocationExpander` also resolves `_Name.Mobile.cshtml` for partials (`MobileViewLocationExpander.cs:27-35`), **the cleanest way to satisfy Q-63 to Q-66 by construction is for `Details.Mobile.cshtml` to render the same `_ShopItemDetailsContent` partial** (no partial twin exists, so the desktop partial is used) with phone styling coming from the phone sheet. That is a design choice for the planner, not a merge of the page twins (Details pages keep their own wrappers).
- **Dead branches:** the controllers are `[Authorize]`, so the "Login to Purchase" anchors and `User.Identity?.IsAuthenticated == true` branches are unreachable; keep behaviour, do not spend effort on them.
- **`Quest Items` category icon** reuses `fa-magic` (A-shop-059, both Index views); the Details partial already maps `QuestItems` to `fa-scroll` (`_ShopItemDetailsContent.cshtml:13-15`).
- **A real-instant `DateTime.UtcNow` read** sits in `Shop/Index.cshtml:80`; it is not in `AmbientClockSeamTests`'s guarded list (`AmbientClockSeamTests.cs:20-52` lists no Shop file), so it stays legal; moving it into the view model is optional.

## The four functional breaks, plus the ones research found

| # | Break | Root cause (evidence) | Fix shape |
|---|-------|----------------------|-----------|
| 1 | Return and Sell invisible (A-shop-002) | `.return-sell-buttons { opacity: 0; visibility: hidden; transition: all .3s; z-index: 10 }` at `legacy-pages.css:425-430`; no rule or script reveals it; markup `Shop/Index.cshtml:112-137` places it in `position-absolute` | Ordinary row actions in normal flow |
| 1b | **Return and Sell would 400 even when visible (new)** | Forms are `<form method="post" action="@Url.Action("Sell", "Shop")" class="d-inline">` (`Index.cshtml:118`, `:128`) with an explicit `action`; the FormTagHelper then defaults antiforgery generation off (`antiforgeryDefault = false` when `action` has a non-empty value, FormTagHelper source); rendered probe this session shows the Return form with only `id` and `quantity` hidden inputs and no `__RequestVerificationToken`; `Sell` is `[ValidateAntiForgeryToken]` (`ShopController.cs:115-117`) | `@Html.AntiForgeryToken()` inside each form (as the management forms and the Purchase and SellToShop forms already do), or `asp-controller`/`asp-action` instead of `action=`; add a render test that every `method="post"` shop form carries a token |
| 2 | Sell script never runs in the modal (A-shop-017) | Fragment inserted with `innerHTML` (`Index.cshtml:466`; phone `Index.Mobile.cshtml:270`); inline `onsubmit`/`onchange` at `_ShopItemDetailsContent.cshtml:216, 227` call functions defined in a `<script>` at `:249-268` that never executes there | Pattern 2 |
| 2b | **Item names with an apostrophe break the sell confirm (new)** | `onsubmit="return confirmSell(event, '@Model.Name', ...)"` puts an HTML-encoded name inside a JS string; the browser decodes the attribute first, so "Wizard's Staff" yields a syntax error and the sell submits unconfirmed | `data-item-name` read from `dataset` |
| 3 | Preview dead (A-shop-027) | `Create.cshtml:149` `onclick="previewItem()"`; the function exists nowhere; `#item-preview` rule at `Create.cshtml:304-309` styles nothing | Preview dialog (D-03) |
| 4 | View Details 404 (A-shop-026) | `Edit.cshtml:189` points at `/ShopManagement/Details/@Model.Id`; no such action | `/Shop/Details/@Model.Id` |
| 5 | **Client validation never runs on the item form (new)** | The desktop scripts call `document.querySelector('form')` (`Create.cshtml` near `:244`, `Edit.cshtml:294`); the first `<form>` on the page is the navbar **Logout form** (`_Layout.cshtml:257`, before `<main>` at `:280`), so the "required fields" and "at least 20 characters" alerts are bound to Logout. Consequences by source reading: the item form submits with no client check (a 19-character description saves on desktop today; server only has `[Required]`), and on Create with empty fields clicking Logout is blocked by an alert. The phone views have no such script (native `required`). **Not run in a browser this session**, so the plan should probe it first | Bind to the item form by id or data attribute. This restores a rule the original code intended but that has never applied, so it is a visible behaviour change: put it on the Gate 1 list (default: bind correctly and keep the `alert()` wording), or drop the client rules and rely on server validation |

## Merchant toast code

`Shop/Index.cshtml:435-438` markup: `<div class="shop-merchant" title="Mystical Merchant" onclick="showMerchantDialog()"><i class="fas fa-user-tie"></i></div>` (a `div`, no role, tabindex or name). `:481-539` script: 20 quotes in `showMerchantDialog`, then `showMerchantToast(message)` builds `<div class="toast merchant-toast" role="alert" data-bs-autohide="true" data-bs-delay="6000">` as a template string with `${message}` interpolated, appends it to `document.querySelector('.toast-container')`, shows it with `new bootstrap.Toast(newToast)` and removes it on `hidden.bs.toast`. The `.toast-container` is rendered by every layout through `_Toasts.cshtml:12`. Styling lives in `legacy-pages.css:604-624` (`.merchant-toast`, with a white toast body and dark text that ignores the foundation toast surface) and is duplicated in `shop.css`.

Plan notes:
- Keep the 20 quotes verbatim (copy from `Index.cshtml:483-504`). They are static text; a module constant is fine, or a server-rendered `<template>`/data list so the module needs no literals. Insert with `textContent`.
- Use `role="status" aria-live="polite" aria-atomic="true"` (the foundation's success toasts already do, `_Toasts.cshtml`). Screen readers announce live regions most reliably when the region exists before its text does; a persistent visually-hidden `role="status"` element on the page, filled with the quote when the toast opens, is the safer pattern. `[ASSUMED]` (screen-reader behaviour was not tested this session): list it in the phase UAT for a real screen reader.
- Bootstrap 5.3.8 already holds the toast open during hover and keyboard focus (the interaction flags exist in the vendored bundle), which covers WCAG 2.2.1 for the toast; the close button is already focusable.
- The button needs `type="button"`, an accessible name (visible text in placement (b); `aria-label` on the icon button in placement (a)), the foundation two-tone focus ring and a target of at least 24px (the old circle is 80px).
- Placement (a): `position: fixed` always covers the footer at the bottom of the page. A `position: sticky; bottom: ...` element inside the content column, or a fixed element whose `bottom` is raised by the footer's measured height through a CSS variable, are the equivalents; sticky inside `.shop-page` stops at the content's bottom edge by construction. Draw both at Gate 1 (D-01).
- Bob: `transform` only, `animation-iteration-count: 3` (the planner's number to tune), `@media (prefers-reduced-motion: reduce) { animation: none }` inside the sheet (the foundation reduced-motion block in `base.css` already exists, so a plain `animation: none` rule is enough there).
- Under D-08 a scanner must allow `infinite` nowhere and allow exactly one named animation with a finite count.

## Pricing script duplication (A-shop-046)

The four desktop and phone scripts are near-copies (diff this session): same `pricingGuide` (min, max, text per rarity, ranges 50-100, 101-500, 501-5000, 5001-50000, 50001-200000), same `calculatedPrices` (100, 500, 5000, 50000, 200000, matching `ShopService.CalculateItemPriceAsync`, `ShopService.cs:50-62`), same `toggleAvailabilityWindow`. **Real drift the module must settle:**

| Behaviour | Create | Edit | Create.Mobile | Edit.Mobile |
|-----------|--------|------|---------------|-------------|
| Price buttons (dice, hat) | yes | yes | none (deliberate) | none (deliberate) |
| Equipment type clears the hint and keeps buttons enabled | no | yes (`updatePriceSuggestion` reads `Type`) | no | check at build time (Edit.Mobile has an `onchange` on Type, `:57`) |
| Initial hint on load | static text "Pricing suggestion will appear here" in Edit, empty in Create | calls `updatePriceSuggestion()` on `DOMContentLoaded` | static "Select a rarity..." | static |
| Submit validation | alert, requires name, description, type, rarity, then 20 chars | alert, requires name and description, then 20 chars | none | none |
| Hint markup | `innerHTML` with an icon and a `<small>` | same | same | same |

Recommended module `shop-item-form.js`: data-attribute driven (`data-price-guide` JSON in one server-rendered attribute or `<template>`, so the numbers live once; `data-random-price`, `data-calculated-price`, `data-availability-toggle`), the superset behaviour (Edit's Equipment branch and initial call), tolerant of absent price buttons, building the hint with `textContent` plus an icon element rather than `innerHTML`. The price hint's first line is already decided for both Edit views (Q-75: "Select a rarity to see pricing suggestions."). The default pricing hint copy is today's wording.

## Bulk Actions stub

Desktop trigger `Index.cshtml:33-35`, modal `:423-452` with three `onclick="bulkAction(...)"` buttons "Discontinue Selected Items", "Activate Selected Items", "Restock Selected Items" and the note "Note: Bulk actions will be available in a future update."; the phone view carries the same modal (`Index.Mobile.cshtml:134-163`) with no trigger. The handler is `alert(\`Bulk ${action} functionality will be available in a future update.\`)` (`Index.cshtml:496-499`, `Index.Mobile.cshtml:205-209`). Nothing on either page is selectable, so the buttons act on nothing. **Advice (as CONTEXT's default):** keep the control and modal, restyle on the foundation (opaque modal surface; drop `bg-dark text-light` classes), bind the three buttons by `data-bulk-action` in `shop-management.js`, keep the wording byte for byte, keep the native `alert`. Do not build real bulk actions (deferred). Do not add a phone trigger (Q-69). Note the modal markup on the phone is orphaned today; keep it only if the planner wants parity of dead markup, otherwise a Gate 1 proposal to drop the unreachable phone modal (a markup removal, no behaviour change) is cheap.

## Availability dates (A-shop-038)

`_ShopItemDetailsContent.cshtml:77, 83, 89` call `.ToString("MMM dd")` and `.ToString("MMM dd, yyyy")` on `AvailableFrom`/`AvailableUntil`. These are `datetime-local` form values (`CreateShopItemViewModel.cs:41-47`) stored with no zone and shown as typed, so **they are wall-clock values: use `Html.WallClock`**, not `LocalTime`. Concretely `Html.WallClock(value, "date-compact")` ("MMM d") for the From part of a range and `Html.WallClock(value, "date")` ("MMM d, yyyy") for the Until part and the single-sided cases (`HtmlHelperExtensions.cs:33-36`: `["date"] = "MMM d, yyyy"`, `["date-compact"] = "MMM d"`). Visible change: "Nov 05" becomes "Nov 5"; list it as an accept item (Phase 98 treated the same change as a Gate 2 accept). Two caveats: (1) the gating query compares these floating values with `DateTime.UtcNow` (`ShopRepository.cs:96-97`), a pre-existing mismatch of up to the board offset; leave it; (2) `site.js` rewrites `time.wall-clock` and `time.local-time` only at `DOMContentLoaded` (`site.js:56`, `:82`, `:184`), so dates inside the fetched modal keep the server wording (the same invariant-culture text); acceptable, note it in the SUMMARY. Purchase-row dates stay `Html.LocalTime(..., "date-compact")` (real instants).

## Animations, transitions and perf to remove

- 16 `infinite` declarations (above), nine `transition: all` in `shop.css` (`:41, 73, 213, 255, 437, 494, 732, 748, 767` per the audit) plus the `legacy-pages.css` copies, the `.item-card::before` sweep that animates `left`, and `backdrop-filter` on repeated cards in `shop.mobile.css` and `shop-management-index.mobile.css` (one per item). The conversion replaces them with the flat glass surface per repeated item and `transition` lists of `transform`/`opacity` using `--qb-dur-base`.
- `.item-card` hover lift: keep a still-style hover (border colour), or `modern-card--interactive` only if the card becomes the link or button (`ui-guidelines.md`).

## Guard tests to rewrite (current line numbers; ROADMAP's are stale)

| Test | Where now | What it pins | Rewrite |
|------|-----------|--------------|---------|
| `MobileViewsTests.MobileShopIndex_MobileUserAgent_RendersItemGridAndFilterButton` | `Mobile/MobileViewsTests.cs:1056-1071` | Filter and Sort trigger text and `shop.mobile.css` link | Keep intent: still the offcanvas trigger and the phone sheet link (update the sheet name only if renamed) |
| `MobileShopIndex_MobileUserAgent_OmitsPurchaseHistoryPanel` | `:1078-1091` (`html.Should().NotContain("purchase-history-panel")`) | The phone omits the purchase history | **Flips** (Q-56, D-10): rename to a facts test that the phone renders the purchase history section with Return and Sell forms (seed a purchase), and that no `purchase-history-panel` fixed side panel exists on either layout |
| `GetMobilePage_ShopManagementIndex/Create/Edit_ReturnsSuccessAndMobileLayout`, `GetMobilePage_ShopDetails_ReturnsSuccessAndMobileLayout` | `:1524-1537`, `:1550-1563`, `:1576-1591`, `:1604-1621` | Each phone page 200, renders its card class and links its own phone sheet | Same intent with the new class names and the consolidated sheet link; none deleted |
| `ShopControllerIntegrationTests.Index_WithoutPageParam_Returns12Items_WhenShopHas15` | `Controllers/ShopControllerIntegrationTests.cs:275-285`, regex `class="item-card"` | 12 cards per page | **Breaks if the card gets a second class.** Keep exactly `class="item-card"` or count a stable hook such as `data-item-card` |
| `Index_PagerRendersWhenMultiplePages`, `Index_OutOfRangePage_ClampsToLastPage` | `:327-350` | `aria-label="Shop page navigation"`, `page-item active` | Keep the label and class |
| `BoardPageStaticGuardTests` | `Tests/BoardPageStaticGuardTests.cs:86-87` lists the two shop controllers | Controller file list for the board-page attribute | Unchanged (controllers untouched); re-run |
| `BoardPageGateMatrixTests` | `Controllers/BoardPageGateMatrixTests.cs:82-178` | Gating, redirect and toast, create count | Behavioural only; no markup fact; re-run, likely no edit |
| `QuestsConversionGuardTests.MarkdownOutputViews_LinkTheContentSheet_AndNoLayoutDoes` | `Tests/QuestsConversionGuardTests.cs:438-460` | Every view that renders Markdown output, hosts `_ShopItemDetailsContent` or fetches `isModal = true` links `markdown-content.css` from `@section Styles` | **Must keep passing**: Shop/Index (both) and Details (both) keep that link |
| Legacy-sheet pins | `UiFoundationLayerGuardTests.cs:319-329` (exists, no `@layer`); `QuestsConversionGuardTests.cs:290-310, 370-380` (no dead quest rules, the `.container.mt-4` rule must exist and carry no `!important`); `AccountPagesRenderTests.cs:143-150`; `UiFoundationDesktopShellGuardTests.cs:151-162, 178-180`; `MobileCssTests.cs:137-150` (a desktop response must still link `legacy-pages.css`); `MobileViewsTests.cs:875-885` | The sheet exists, is unlayered, holds only `.container.mt-4`-class rules, and is linked by the desktop and picker layouts | **Keep the file with only the shared wide-container rule** and update the comments; do not delete it or unlink it |
| `UiFoundationLayerGuardTests` arrays | `:60-104` (`LayeredFiles`, `NoLiteralFiles`) | Each layered sheet opens its layer, holds no colour literal or `!important` | Add the new shop sheets; remove retired names |
| Token tests (`:643-650`, `:693-723`) | contrast of the shop theme on the shared pair table | Shop theme passes | No change needed if no token is added |
| `AccountPagesRenderTests`, `PlatformAreaLayoutTests` etc. | untouched | — | Run in the full suite only |

New test classes (Wave 0): `ShopConversionGuardTests` (clone of `CharactersConversionGuardTests`: scope lists, no inline code, links, layer, retired files gone, planning-reference scan, raw date scan, no infinite animation, no aspect-ratio or object-fit, no markup-from-strings in modules, every shop `method="post"` form has a token, theme seam), `ShopRenderTests` (integration, both User-Agents), and extensions to `KeyboardScriptGuardTests` for the new modules.

## Phase 94 shop theme tokens

`tokens.css:187-204` `[data-theme='shop']`, verbatim values: `--qb-board-fallback: #0d1126;`, `--qb-board-image: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);`, `--qb-board-veil: transparent;`, `--qb-surface-scrim: rgba(8, 10, 28, 0.55);`, `--qb-surface-glass: rgba(255, 255, 255, 0.07);`, `--qb-surface-glass-hover: rgba(255, 255, 255, 0.12);`, `--qb-surface-glass-border: rgba(226, 214, 180, 0.3);`, `--qb-surface-overlay: #12162b;`, `--qb-chrome-top: rgba(30, 32, 62, 0.97);`, `--qb-chrome-bottom: rgba(14, 16, 36, 0.97);`, `--qb-fg-muted: #d6dbea;`, `--qb-action: #2f5f9e;`, `--qb-action-hover: #3d74bb;`, `--qb-action-fg: #ffffff;`, `--bs-dark-rgb: 18, 22, 43;`. Colour budget: "at most 40 distinct opaque hex values, 38 with the Shop theme: currently 38" (`tokens.css` header), so **two hex values of headroom**; the chip and emblem plan above needs none. The theme sets the navy gradient wholesale on `--qb-board-image`, so the board photo (`--qb-board-photo` on `:root`) is never drawn on shop pages, which is what WINDOWS 22 asks for.

Note a theme side effect: modals and toasts use `--qb-surface-overlay`, so on shop pages they are navy automatically; remove the `bg-dark text-light` utility classes from the four shop modals (`itemDetailsModal` on both Index views, `denyModal`, `bulkActionsModal`) and let `components.css:588-` apply.

## Common Pitfalls

### Pitfall 1: The un-hidden Return and Sell buttons post without an antiforgery token
**What goes wrong:** the reviewer sees the buttons, clicks Return and gets HTTP 400.
**Why:** explicit `action=` disables the FormTagHelper token (source verified), and the forms have no manual token.
**How to avoid:** token in every `method="post"` shop form; a static guard scanning the shop views for `<form ... method="post"` without `@Html.AntiForgeryToken()`, `asp-action` or `asp-controller` within the same tag block; a render test that posts a Return and a Sell and gets a redirect, not 400.
**Warning signs:** BEFORE rows cannot show it (buttons hidden), so only the AFTER click-through finds it.

### Pitfall 2: Sell state needs a 24-hour-old purchase
See Priority 1. Schedule it; record eligibility times in `fixtures.json`; the `fixtures-check` step should print the next eligible time instead of failing.

### Pitfall 3: Apostrophes in item names
`'@Model.Name'` inside an inline JS string. Use `data-item-name`; add a render test with an item named `Wizard's "Staff"` asserting the `data-item-name` attribute is encoded and no `onsubmit` exists.

### Pitfall 4: `document.querySelector('form')` hits the Logout form
Bind by id or `form[data-shop-item-form]`. Probe the current behaviour first (click Logout on `/ShopManagement/Create` with empty fields under Playwright) so the SUMMARY can state what changed.

### Pitfall 5: EasyMDE is initialised at `DOMContentLoaded`
`markdown-editor.js` is a classic script and sets `textarea.easyMDE` inside a `DOMContentLoaded` listener (`markdown-editor.js:221-229`). An ES module executes before that event, so reading `descriptionEl.easyMDE` at module load finds nothing. Read it lazily inside the submit and preview handlers, falling back to the textarea value, as the current inline scripts do (`Create.cshtml` "EasyMDE hides the raw textarea..." comment).

### Pitfall 6: Old unlayered rules beat new layered ones
`shop.css` and `legacy-pages.css` are unlayered (`ui-guidelines.md` "Legacy page sheets are unlayered"), so a layered rule on the same selector loses on every property, and `!important` in the old sheets loses to Bootstrap's layered `!important` utilities (dead, WINDOWS 28) so some old rules already do nothing. Because every shop page loads `shop.css` globally on desktop, **a slice cannot restyle `.item-rarity`, `.rarity-*`, `.purchase-*`, `.btn-action`, `.type-*` in a layered sheet without removing the old rule in the same commit.** Recommended mechanism: introduce the new layered sheet under a new name in the first styling slice, delete old rules class by class as their consumers convert, and delete the old `shop.css` file in the Shop/Index slice; fix final names in the plan. Also remember the phone layout loads neither `shop.css` nor `legacy-pages.css` (`MobileCssTests` pins the desktop link only), so phone pages need their sheet through `@section Styles` anyway.

### Pitfall 7: A partial cannot render `@section Styles`
`Details.cshtml` and `Details.Mobile.cshtml` declare `@section Styles` but the modal receives a `PartialView`, so the sheet never loads there (A-shop-005 root cause). The modal's styles must come from the **host** page (Shop/Index both layouts), which therefore links the same sheets as Details.

### Pitfall 8: Measuring on a hidden or unrendered modal
Probes that read contrast inside the modal must open it first (the Phase 98 "measure on the hidden modal" pitfall). `~/qb-ui-review/phase-98/tools/modal-probe.mjs` and `phase-94/tools/shopmodal.mjs` exist as starting points.

### Pitfall 9: Rarity chips measured on the wrong surface
Chips carry their own fill and label so they hold 4.5:1 anywhere; the emblem icon tints use the `-text` variants (light) because the tile is dark navy. Do not use the fill colour as text on the tile.

### Pitfall 10: Hard-coded 15-row cap
The history shows `Model.UserPurchases.Take(15)` and "Showing latest 15 purchases" when more (`Index.cshtml:76, 161-166`). Keep the cap and copy; the new in-flow section must keep both.

### Pitfall 11: `.mobile-layout` is chosen by User-Agent, not width
Phone views are verified under a mobile UA with touch (`Pixel 7` device in Playwright, which carries the keywords `MobileDetectionMiddleware` matches); a narrow desktop window is not a phone.

### Pitfall 12: Admin account creation sends Welcome email jobs
Creating the three p99 accounts through Admin enqueues Welcome email jobs (`AdminController.cs:142`, per the Phase 98 research); recipients are `@example.invalid` and the app runs in Development with sending suppressed (`run-app.mjs` forces `EmailSettings__SuppressSending=true` and SMTP `127.0.0.1:1025`). Mailpit (`qb-mailpit`) does not exist now; start it only if a message must be read (pre-approved, D-15).

## Code Examples

### Merchant module skeleton (text only, polite, capped bob driven by CSS)
```js
// shop-merchant.js: no markup from strings; quotes live in a server-rendered <template> or a const array
const button = document.querySelector('[data-merchant-button]');
const region = document.querySelector('[data-merchant-live]');
button?.addEventListener('click', () => {
  const quote = QUOTES[Math.floor(Math.random() * QUOTES.length)];
  const toast = document.querySelector('[data-merchant-toast]');
  toast.querySelector('[data-merchant-quote]').textContent = quote;   // textContent, never innerHTML
  region.textContent = quote;                                          // persistent live region
  bootstrap.Toast.getOrCreateInstance(toast).show();
});
```
Source: pattern from `confirm-submit.js` and `row-link.js` (no strings to markup, nothing global). Bootstrap `Toast.getOrCreateInstance` is the 5.3 API `[CITED: getbootstrap.com/docs/5.3/components/toasts]`.

### Return/Sell row (token, visible, accessible)
```cshtml
<form method="post" asp-controller="Shop" asp-action="Sell" class="purchase-row-action">
    <input type="hidden" name="id" value="@purchase.Id" />
    <input type="hidden" name="quantity" value="@purchase.RemainingQuantity" />
    <button type="submit" class="btn btn-success btn-sm">
        <i class="fas fa-undo me-2" aria-hidden="true"></i>Return (@purchase.RemainingQuantity)
    </button>
</form>
```
`asp-controller`/`asp-action` brings the token automatically. Keep the `title` text and the Return-within-24h versus Sell wording; no new confirmation is added (see Open Question 1).

### Item type display name (new view model property)
```csharp
public string TypeDisplayName => Type switch
{
    ItemType.Equipment => "Equipment",
    ItemType.MagicItem => "Magic Item",
    ItemType.QuestItems => "Quest Item",
    _ => Type.ToString()
};
```
Values quoted from `ItemType`: `Equipment, MagicItem, QuestItems`; the management lists already convert to "Magic Item" and "Quest Item" (`ShopManagement/Index.cshtml:68`).

## Gate tooling reuse

`~/qb-ui-review/phase-98/tools/` (139 files, `node_modules` symlinked to `phase-94/tools/node_modules`, Playwright 1.64.1, Node v24.19.0, Python 3.14.4) holds the Phase 98 copies of: `run-app.mjs`, `stop-app.mjs`, `login.mjs`, `audit-shoot.mjs`, `register98.mjs` (model for `register99.mjs`), `fixtures.mjs` and `fixlib.mjs` (board and membership creation), `manifest.mjs`, `sweep.mjs`, `probes.mjs`, `contrast.mjs`, `readability-compare.py`, `pair-compare.py`, `overflow-compare.mjs`, the `a11y-*` probes, `look-check-98.mjs`, `lookcheck.mjs`, `build-gate1.py`, `build-gate2.py`, `gate2-sheet.mjs`, `gate2-crops.mjs`, `gate-hygiene.py`, `modal-probe.mjs`, `shopmodal.mjs` (a Shop modal probe), `uat102-shop.mjs`. Copy and re-point to `~/qb-ui-review/phase-99/`, never edit in place, never `npm install`, never `publish-review.sh`. `phase-98/ENVIRONMENT.md` is the template for `phase-99/ENVIRONMENT.md` (ports, email, database, capture, git and gates).

Direction A references exist: `phase-94/mockups/direction-a/png/shop-a-{1440,820,390}.png`, `shop-a--empty-{1440,820,390}.png` and their `-report.json`, plus the static `shop-a.html`/`shop-a--empty.html` and `components.css`/`pages.css` (the rarity rules at `components.css:430-458`; `.shop-layout`, `.shop-history`, `.shop-hero`, `.shop-filters`, `.shop-merchant` at `pages.css:685-916`). Note CONTEXT's warning: those renders draw the side panel and two bars; they set the look, not the layout. `senior-frontend` skill is present at `~/.claude/skills/senior-frontend/`; its rules that matter here: `textContent` not `innerHTML`; no inline style, script or handler; `transform` and `opacity` only for motion, nothing repeating on many nodes; two cheap shadows per repeated item; stable variation from ids, not `Math.random()` (the merchant quote is the one deliberate random, which is behaviour, not styling); compute contrast rather than eyeball; mockups in scratch, never committed.

**Ports.** Probed this session with a Python socket connect and `ss -ltn`: 8310, 8311, 8320, 8321, 8330, 8331 are free; no 83xx or 80xx port listens. Recommend **8320 (working tree) and 8321 (frozen baseline)** so Phase 98's leftover `state/8310` storage states cannot be picked up by mistake; the opening plan re-checks with `ss -ltnp` (never a zsh `/dev/tcp` probe).

## Slice plan (research proposal; the binding list goes in ROADMAP)

> Superseded: the table below was the research proposal; the binding plan list is the 20-plan list in `.planning/ROADMAP.md` § "Phase 99".

Phases 95 to 98 ran about one plan per wave because plans share `UiFoundationLayerGuardTests`, `MobileViewsTests` and one checkout. Recommended **14 plans**.

| Plan | Wave | Autonomous | Content | Depends on |
|------|------|-----------|---------|------------|
| 99-01 | 1 | no | Opening checkpoint: operator brings SQL Server up (down now), ports 8320/8321, p99 accounts approved; scratch root `~/qb-ui-review/phase-99/` with tools copied and re-pointed; `senior-frontend` loaded; tracer capture of `/Shop` under both UAs; **record the gold finding** (no gold exists) and the 24h Sell timing | — |
| 99-02 | 2 | yes | Fixtures through the app's forms (`register99.mjs`, "P99 Audit Shop", "P99 Audit Empty", every rarity, type, status, stock state, paging, window, long names, D&D Beyond link, second-DM publishing and denying), **day-0 aging purchases** with timestamps in `fixtures.json`, frozen baseline on 8321 with `BASELINE_SHA`, `tests-before.txt`, BEFORE capture of every row under both UAs | 01 |
| 99-03 | 3 | no | Gate 1 package A: Q-120 history under the grid and its D-10 phone twin, Q-121 filter card, both merchant placements (D-01), Q-61 Active badge placement | 02 |
| 99-04 | 4 | no | Gate 1 package B: Preview dialog, emblem tile in modal and Details (desktop and phone), the four look-check pages beside `shop-a-*`, with numeric diff of computed colours as in Phase 98 | 03 |
| 99-05 | 5 | no | Gate 1 sheet: numbered items with recommendation and default (see Gate 1 items), `checkpoint:decision`, redraw loop, answers to `gate1/answers.json` with fixed keys | 04 |
| 99-06 | 6 | yes | **Gate-independent breaks and modules**: theme seam in both layouts and guard; antiforgery tokens; `shop-item-sell.js` and `shop-item-modal.js`; status and type display mapping; chip component and removal of the old unlayered `.item-rarity/.rarity-*` rules (zero new tokens); `ShopConversionGuardTests` skeleton; `UiFoundationLayerGuardTests` hooks | 02 (can start before Gate 1 closes; touches no gated pixels) |
| 99-07 | 7 | yes | Details partial and both Details views: emblem tile (after D-07 approval), data-attribute sell form, phone Details renders the partial (Q-63 to Q-66), `WallClock` dates, in-modal Back to Shop dropped, `.merchant`-free | 05, 06 |
| 99-08 | 8 | yes | Create and Edit, both layouts: `shop-item-form.js`, the four `<style>` blocks and stray `</script>` gone, dead `#item-preview`, Edit "View Details" retarget, Preview dialog on Create (after approval), WINDOWS 12 required marks, status alert wording | 05, 06 |
| 99-09 | 9 | yes | Management Index, both layouts: `data-confirm-message` on 12 forms, `d-inline` instead of `style=`, `aria-label` on desktop icon buttons, `#denyModal` and bulk modal modules, "No Items Yet" on the phone (Q-72), empty states on `.empty-state`, heading on the phone list (A-shop-044) | 06 |
| 99-10 | 10 | yes | Shop/Index desktop: filter card (Q-121), history in flow (Q-120), merchant (D-01 pick, D-02), keyboard card access, Clear Filters and Active badge on search (Q-61), no `min-height: 100vh` | 05, 06, 07 |
| 99-11 | 11 | yes | Shop/Index phone: history (Q-56, D-10), Previous/Next and count (Q-58), three empty states (Q-59), consolidated phone sheet; old shop sheets removed from the layout and deleted; `legacy-pages.css` trimmed to the shared rule | 10 |
| 99-12 | 12 | yes | Cleanup and guards: remaining old rules, `ShopConversionGuardTests` complete, `UiFoundationLayerGuardTests` arrays, `MobileViewsTests` rewrites, zero counts, test-count comparison against `tests-before.txt`, untouched-paths diff | 06-11 |
| 99-13 | 13 | yes | Sweeps: contrast, targets (24 desktop, 44 phone), overflow at 320 desktop and 390 phone, focus, reduced motion, full AFTER capture including **Sell rows only after the 24h eligibility time**, exceptions file | 12 and 24h after 99-02 |
| 99-14 | 14 | no | Gate 2 local package, `checkpoint:decision`, gap-closure loop, release hand-off (v6.4.0 via the operator's PR, no tag, no push, GHCR caveat) | 13 |

Each plan ends with the Priority 6 block. Plans 99-06 to 99-11 each leave every shop page usable and the suite green (Phase 94 D-08).

### Gate 1 items list (numbered, each with a recommendation and the default if unanswered)

1. Q-120 and D-10: purchase history under the grid, desktop and phone, rows and Return/Sell as today. Default: as drawn.
2. Q-121: one filter card (rarity checkboxes, sort, search, Apply, Search), with where the "Active" badge and Clear Filters sit (Q-61). Default: card heading with the badge.
3. D-01: merchant placement (a) clear of the footer or (b) in the hero. No default; operator picks.
4. D-03: Preview dialog content and wording; and **whether Preview also appears on Edit** (Claude's discretion; default no).
5. D-07: emblem tile size, icon per type (default: existing hammer, magic wand, scroll, box mapping from the partial) and rarity tint with the `-text` tokens.
6. Look check: Shop/Index grid, Shop/Details, management list, a management form, each beside `shop-a-*` and `shop-a--empty-*`.
7. Bulk Actions: keep and convert (default) versus disable or remove.
8. **Return and Sell confirmation:** today the purchase-row forms have none (only SellToShop does). Default: add none (no behaviour change).
9. **Client validation binding** (Pitfall 4): default bind to the item form and keep the alert wording.
10. **Denied and Archived notice on Details** (Priority 3 gap): default none.
11. Availability dates "Nov 05" to "Nov 5" (accept or reject).
12. Phone Details renders the shared partial (recommended) with phone wrapper.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Inline `onclick`/`onsubmit`, inline `<script>` in a partial | Delegated module bound by data attributes | Phases 95 to 98 | Works in fetched HTML |
| Theme on `<body>` custom property | `data-theme` on `<html>` and token override | Foundation, WINDOWS 22 | Fallback background recolours |
| `.rarity-*` gradients, white text | Solid status-token chips with labelled fills | Direction A | 4.5:1 without new colours |
| Fixed 280px side panel | In-flow section | Q-120 | Reachable at every width |

**Deprecated or outdated (to delete):** `site.css` copies are already gone (renamed `legacy-pages.css`); the `#addCharacterModal` family in `shop.css` (30 `!important` lines; no markup uses it, `shop.css:591-650`), `.empty-shop`, `.empty-shop-state` (no rule exists, A-shop-001), `.shop-merchant` fixed circle, `.merchant-toast` white body, `.return-sell-buttons` block, `.purchase-history-panel` and `.shop-container.with-panel` margin rules, the four `<style>` blocks.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | A persistent visually-hidden `role="status"` region announces the merchant quote more reliably than a dynamically inserted toast | Merchant toast | Quote not announced; the UAT screen-reader check would catch it |
| A2 | Binding the validation to the real item form (Pitfall 4 default) is what the operator wants; the 20-character rule has never applied on desktop | Breaks table 5, Gate 1 item 9 | A short description newly gets an alert; operator may prefer dropping the rule |
| A3 | The Logout form is the first `<form>` on the page in a browser (source order read, not run) | Breaks table 5 | Behaviour description may differ; the BEFORE probe in 99-03 (`before/behaviour.json`, `logoutBinding`) and the AFTER probe in 99-12 settle it |
| A4 | Direction A's `--qb-warning` amber is the intended "legendary gold" chip (the signed-off render uses it; CONTEXT says "legendary gold") | Standard Stack, D-09 | If the operator wants the true gold accent fill, `--qb-accent` with `--qb-accent-ink` also passes the table but uses a "reserved" colour that `ui-guidelines.md` limits |
| A5 | `fa-hammer`, `fa-magic`, `fa-scroll`, `fa-box` exist in Font Awesome 6.7.2 with the v4 shims loaded (the partial already uses them today) | Details partial | Icon missing; the render shows it |
| A6 | Bootstrap 5.3.8 toast hover and focus pause applies to a toast that has `role="status"` (framework code is by flags in the bundle, not exercised here) | Merchant toast | WCAG 2.2.1 not met; check in the Gate 2 behaviour probe |

## Open Questions (RESOLVED)

1. **Add a confirmation to Return and Sell on purchase rows?** RESOLVED: put to the operator as Gate 1 item 8 (99-07). Default and recommendation: the Sell row confirms with the Sell to Shop sentence, Return stays unconfirmed; on an explicit no neither confirms. Built in 99-16 from `gate1/answers.json` `sellRowConfirm`, and on the phone through the same partial in 99-17.
   - Known: D-05 says "both forms and their confirmations kept", but the purchase-row forms have no confirmation today (`Index.cshtml:118-134`); only `SellToShop` confirms.
   - Unclear: whether the operator wants a new one. Adding it is new behaviour and visible copy.
2. **Denied and Archived items on Shop/Details show no notice** (Priority 3). RESOLVED: Gate 1 item 10 (99-07), default none; the existing sentence "This item is not currently available for purchase." is added only on the operator's yes, built in 99-10 from `deniedArchivedNotice.show`. D-04 holds either way (Details answers 200 for every status; render test in 99-10).
3. **Should `legacy-pages.css` be fully retired?** RESOLVED: trim and keep (99-18). It keeps only the shared `.container.mt-4` rule used by the group picker layout and the DM profile pages and pinned by four test files; it stays unlayered and linked; a guard pins that it holds nothing else.
4. **Preview card text fidelity** (client strip versus `/markdown/preview`). RESOLVED: the existing `/markdown/preview` endpoint, no new endpoint (99-13): the dialog shows the raw text at once, then the server's plain text (inert parse, top-level text joined with spaces, cut at 120 with "..."), keeping the raw text if the request fails; wording and fallbacks confirmed at Gate 1 item 4.
5. **Sell-state capture timing.** RESOLVED: a calendar dependency, never SQL. 99-02 makes 18 aging purchases on day 0 and records each one's Sell-eligible time; slice sweeps capture Sell rows only when `fixtures.mjs --sell-status` reports eligible; 99-19 refuses to start its closing capture until then (it stops and reports the time) and Gate 2 (99-20) uses that capture.
6. **Server quantity validation** (Priority 1 note). RESOLVED: out of scope and unchanged; shown as Gate 1 item 20 and recorded in this phase's `deferred-items.md` by 99-19.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| SQL Server `localhost:1433` | Fixtures, BEFORE and AFTER captures, SQL-backed tests | **No, refused** (`python3` socket connect refused, `nc -z` closed, `bash /dev/tcp` failed; `docker ps -a` shows `mssql-dev` "Exited (137) 14 hours ago") | — | None: operator starts it in plan 99-01. The agent must not start or provision it. Integration tests use EF InMemory and need no database; browser work and fixtures do |
| .NET SDK | Build and tests | Yes | 10.0.115 | — |
| Node | Playwright tools | Yes | v24.19.0 | — |
| Python | Gate builders, scorers | Yes | 3.14.4 | — |
| Playwright with bundled Chromium | Captures, probes | Yes | 1.64.1 (symlinked node_modules) | — |
| Ports 8320, 8321 | Working tree and baseline | Free | — | 8330/8331 also free |
| Mailpit (`qb-mailpit`) | Only if an email must be read | No container exists | — | Not needed; the app runs in Development with sending suppressed; starting it is pre-approved if needed |
| `senior-frontend` skill | D-13 | Yes (`~/.claude/skills/senior-frontend/`, invoked this session) | — | — |
| gsd-tools | Docs commit commands | Yes (`~/.claude/gsd-core/bin/gsd-tools.cjs`, `gsd_run`) | — | — |
| Direction A renders and Phase 98 tools | Gate 1 look check and capture | Yes (listed above) | — | — |

**Missing dependencies with no fallback:** SQL Server at 1433 (operator).
**Missing dependencies with fallback:** Mailpit (not needed).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 with FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF Core InMemory (no database for the integration suite) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run command (integration) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~ShopConversionGuardTests\|FullyQualifiedName~ShopRenderTests\|FullyQualifiedName~ShopControllerIntegrationTests\|FullyQualifiedName~ShopManagementControllerIntegrationTests\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~MobileCssTests\|FullyQualifiedName~BoardPageGateMatrixTests\|FullyQualifiedName~BoardPageStaticGuardTests\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~KeyboardScriptGuardTests\|FullyQualifiedName~QuestsConversionGuardTests\|FullyQualifiedName~AccountPagesRenderTests"` |
| Quick run command (unit) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~ShopService\|FullyQualifiedName~UserTransactionRepository\|FullyQualifiedName~AmbientClockSeamTests\|FullyQualifiedName~WallClock"` |
| Full suite command | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (per project avoids the CLR flake). SQL-backed tests (`ShopItemSqlServerTests`, migrator tests) need `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and SQL at 1433; run once at the end |
| Browser checks | Scratch Playwright under `~/qb-ui-review/phase-99/` (desktop 1440x900 and `devices['Pixel 7']` with touch for the phone layout) |
| Estimated runtime | about 20 s quick after build, 2 to 3 minutes full (Phase 98 figures) |

Notes: re-run once on `0x80131506`; never set GC environment variables; stop the running app before `dotnet build/test` (shared `bin/obj`); stage files explicitly; no watch mode.

### Phase Requirements to Test Map
| Req | Behavior | Test Type | Automated Command | File Exists? |
|-----|----------|-----------|-------------------|-------------|
| D-01/D-02 | Merchant is a `<button>` with name, no `onclick`, module builds no markup from strings, toast `role="status"`, quotes unchanged (20) | static + integration | `~ShopConversionGuardTests` (module scan, 20-quote pin); `~ShopRenderTests` (button, name); `~KeyboardScriptGuardTests` | ❌ Wave 0 |
| D-03 | Preview button has no inline handler; dialog markup uses `.modal`; no new endpoint; `#item-preview` gone | static + integration | `~ShopConversionGuardTests`; `~ShopRenderTests` | ❌ Wave 0 |
| D-04 | Edit "View Details" href is `/Shop/Details/{id}`; GET returns 200 for Draft and Denied | integration | `~ShopRenderTests` (Edit link; Details status matrix) | ❌ Wave 0 |
| D-05 | Return/Sell forms visible (no hidden class), carry a token, POST succeeds; sell form uses `data-sell-form`, no `onsubmit`; fixture item with apostrophe | integration + static | `~ShopRenderTests` (post Sell, post Return); `~ShopConversionGuardTests` (every post form has a token) | ❌ Wave 0 |
| D-06 | One status vocabulary: "Pending Review" for Draft, "Available" for Published on both lists and both Edit views | integration | `~ShopRenderTests`; `~MobileViewsTests` (phone) | ❌ Wave 0 |
| D-07/D-08/D-09 | Emblem tile has no `style=`/`aspect-ratio`/`object-fit`; no `infinite` anywhere except one capped merchant rule; no `transition: all`; chips use token pairs | static | `~ShopConversionGuardTests` (animation scanner with self-test, whole-picture scanner with self-test, literal/important scans) | ❌ Wave 0 |
| D-10 / Q-56 | Phone renders the history section with Return and Sell; no `purchase-history-panel` on either layout | integration | `~MobileViewsTests` (rewritten), `~ShopRenderTests` | ✅ rewrite |
| Q-58/59/61/63-66/71/72/75 | Phone Previous/Next and count, three empty states with Clear, desktop Clear and Active on search, phone Details gains Sell, Availability, Properties, D&D Beyond, notices, `max`; "No Items Yet"; price hint | integration | `~ShopRenderTests` (both UAs) | ❌ Wave 0 |
| Theme | Both layouts emit `data-theme` on `<html>`; all ten views set it; token contrast passes | static + existing | `~ShopConversionGuardTests`; `~UiFoundationLayerGuardTests` | ✅ existing + ❌ new fact |
| DoC-1..3 | No inline code; no hex, rgb or `!important`; sheets in `@layer pages`; linked from `@section Styles`; shop sheets not in a layout list | static | `~ShopConversionGuardTests`; `~UiFoundationLayerGuardTests` | ❌ Wave 0 |
| DoC-4 | Twin answers pinned | integration | `~MobileViewsTests`, `~ShopRenderTests` | ✅/❌ |
| DoC-5/DoC-7 | Accessibility bar and phone checks | Playwright | scratch `sweep.mjs`, `contrast.mjs`, `a11y-*` | manual-run |
| DoC-8 | Guards rewritten, none deleted | process | test-count comparison against `tests-before.txt` | process |
| DoC-9 | Helpers untouched; no write to Events or Quests; feed files untouched | unit + diff | `~WallClockMarkupTests`, `~LocalTimeMarkupTests`; `git diff --stat` | ✅ |
| A-shop-038 | Availability dates through `Html.WallClock`; no raw `ToString` on a DateTime in the area | static | `~ShopConversionGuardTests` (clone of Phase 98's raw date scan) | ❌ Wave 0 |
| Legacy sheet | `legacy-pages.css` keeps only the shared container rule; still linked by desktop and picker layouts | static | `~QuestsConversionGuardTests`, `~UiFoundationLayerGuardTests`, `~MobileCssTests`, `~AccountPagesRenderTests` | ✅ existing |
| Markdown hosts | Shop Index and Details still link `markdown-content.css` | static | `~QuestsConversionGuardTests.MarkdownOutputViews_*` | ✅ existing |
| 12-card page | `class="item-card"` count stays 12 | integration | `~ShopControllerIntegrationTests.Index_WithoutPageParam_Returns12Items_WhenShopHas15` | ✅ keep or rewrite to a data hook |
| DoC-10 | Every page works | full suite + sweep | both full suites | ✅ |

### Sampling Rate
- **Per task commit:** the quick filters narrowed to the slice's files, plus the scratch probe on the touched pages under both User-Agents.
- **Per wave merge:** both full suites, per project.
- **Phase gate:** full suites green, `dotnet build` clean, AFTER sweep shows no new contrast failure and no hidden control against BEFORE; `git diff --stat` shows no change to `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`; no write to Events or Quests; no change to controllers or entities.
- **Max feedback latency:** 120 seconds.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs` (clone of `CharactersConversionGuardTests`: scope lists, retired files, scanners with self-tests)
- [ ] `QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs` (both User-Agents, seeded purchase, apostrophe item name, Denied/Draft/Archived details, antiforgery token on post forms, POST Sell and Return)
- [ ] `KeyboardScriptGuardTests` additions for `shop-merchant.js`, `shop-item-sell.js`, `shop-item-form.js`
- [ ] Re-pointed Phase 98 tooling under `~/qb-ui-review/phase-99/tools` and `ENVIRONMENT.md`
- Framework install: none

### Manual-Only Verifications
| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gate 1 approval (relayouts, merchant pick, Preview, emblem, look check, proposals) | D-11 | Operator taste gate | `~/qb-ui-review/phase-99/gate1/`; redraw until approved |
| Gate 2 before/after sign-off with exception accept or reject | D-11 | Operator taste gate | `~/qb-ui-review/phase-99/gate2/` |
| Sell row captures | D-14 | Needs a purchase at least 24 hours old | Run at least 24 hours after the day-0 purchases |
| Merchant announcement and Return/Sell with a real screen reader and real phone touch | D-02 | Assistive technology and device | Phase UAT after deploy |
| Rendered contrast, targets, overflow at 320 and 390 | DoC-5 | Rendered measurement | `~/qb-ui-review/phase-99/after/` |

## Security Domain

`security_enforcement` is not set to false in `.planning/config.json`, so this section applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (no change; controllers already `[Authorize]`) | existing Identity |
| V3 Session Management | no | existing |
| V4 Access Control | yes (observations) | `[Authorize(Policy = "DungeonMasterOnly")]` on management; Sell checks transaction ownership (`ShopService.cs:123`); keep unchanged |
| V5 Input Validation | yes | Razor encoding for `data-item-name`; `textContent` for quotes and Preview; model validation attributes; **observation:** quantity not validated server-side (Priority 1) |
| V6 Cryptography | no | none |
| V13 / CSRF | yes | `[ValidateAntiForgeryToken]` is on every POST; views must carry a token (Pitfall 1) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on Return and Sell | Tampering | Token in each form; guard test |
| Stored script injection through an item name interpolated into inline JS (`confirmSell('@Model.Name'...)`) | Tampering / Elevation | Move the name to a `data-` attribute read through `dataset`; no inline handlers |
| HTML injection through `innerHTML` of fetched or typed text | Tampering | Insert text with `textContent`; the fetched details fragment is same-origin, server-rendered and sanitised Markdown |
| User-authored Markdown image overflow or tracking pixel | Information disclosure / UX | Sanitiser already limits tags; size rule `max-inline-size: 100%`; no change to allowed tags |
| Direct Details access to Draft items by any member | Information disclosure | Existing behaviour, not changed in this phase; record only |
| Negative or zero quantity on Purchase, Sell | Tampering | Out of scope; record in deferred items |
| Gold or balance forgery | — | Not applicable: no balance exists |

## Project Constraints (from CLAUDE.md)

- Never commit to `main`; all work, including planning docs, on `milestone/v9-rolling-improvements` (or a feature branch). Executors stage explicitly and never push; no tag, no PR in plans.
- Local database at `localhost:1433`: never install, start or tear down; if unreachable stop and tell the user. Use SQL auth through user-secrets only, never edit `appsettings.json`; do not "fix" `Cannot generate SSPI context` by deleting `Trusted_Connection=true`. Integration tests use InMemory.
- No GSD planning references in source code, XML docs, CSS comments or string literals (no `D-06`, `Phase 99`, plan numbers, finding IDs, `RESEARCH.md` names). Commit messages may carry them. `UiFoundationLayerGuardTests` scans every sheet, script and view for them.
- Before any Razor view change read `.claude/ui-guidelines.md`: card pattern, filled buttons by role, no inline code, tokens only, no `!important` in layered sheets, `.mobile-layout` twins, `Html.LocalTime`/`Html.WallClock`.
- Never use `DateTime.Now/UtcNow/Today` for board-day reads (`IBoardClock`); `AmbientClockSeamTests` closed list. Shop views are not on the list; the purchase-window `UtcNow` read is a real instant.
- Writes to `Events` and `Quests` must go through the change tracker; this phase writes neither. `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` untouched.
- EF packages only in `QuestBoard.Repository`; migrations auto-apply; this phase needs no migration.
- On Linux never set GC-constraining env vars; `0x80131506` is a flake, re-run once.
- RIP MCP preferred for symbol navigation when present (not available in this session; used grep and reads).
- Do not use the number of members from planning docs; never print credentials.

## Sources

### Primary (HIGH confidence)
- Repository files read this session (cited with paths and lines throughout): `ShopController.cs`, `ShopManagementController.cs`, `ShopService.cs`, `ShopRepository.cs`, `UserTransactionRepository.cs`, entities and view models, all 11 in-scope views, the six shop sheets and `legacy-pages.css` in full, `tokens.css`, `components.css`, the layouts, `confirm-submit.js`, `row-link.js`, `markdown-editor.js`, `HtmlHelperExtensions.cs`, the guard and mobile tests named above.
- Git history: `887e5dcd`, `1c0002a9`, `327e4d8d`, `744155ae`, the `docs(98-NN)` commits, `f42ff41b`.
- Planning docs: CONTEXT, DISCUSSION-LOG, ROADMAP Phase 99, `94-AUDIT-shop.md` (read in full), 98-RESEARCH sections 6 to 9, 98-VALIDATION, `98-23-SUMMARY.md`, WINDOWS entries 12, 22, 28.
- `~/.claude/gsd-core/workflows/execute-plan.md` and `gsd-tools.cjs` command listing.
- `https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Mvc/Mvc.TagHelpers/src/FormTagHelper.cs`: the `antiforgeryDefault` logic for forms with an `action` attribute (fetched this session).
- A temporary integration probe (created, run and deleted; working tree clean) that proved a 0-price purchase by a user with no gold and the missing token on the Return form.

### Secondary (MEDIUM confidence)
- Direction A static renders and components under `~/qb-ui-review/phase-94/mockups/direction-a/` (outside the repository; rarity rules read at `components.css:430-458`).
- Bootstrap 5.3.8 vendored bundle: presence of `_hasMouseInteraction`, `_hasKeyboardInteraction`, `_onInteraction` (names grepped, behaviour not exercised).

### Tertiary (LOW confidence)
- None beyond the `[ASSUMED]` items in the Assumptions Log.

## Metadata

**Confidence breakdown:**
- Gold path and ledger behaviour: HIGH (code read, probe run)
- Breaks and root causes: HIGH (code and rendered markup); the Logout-form binding is HIGH by source order and unrun
- Stylesheet and view counts: HIGH (re-measured; match ROADMAP when `legacy-pages.css` is counted whole)
- Slice plan and Gate 1 items: MEDIUM (proposal)
- Screen reader behaviour: LOW until UAT

**Research date:** 2026-11-15
**Valid until:** 2026-12-15 or until another phase touches the shared layouts, `tokens.css` or `legacy-pages.css`
