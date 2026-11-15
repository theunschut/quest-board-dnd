# Phase 99: Shop and Shop management UI conversion - Context

**Gathered:** 2026-11-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Convert `Shop/{Index,Details}`, `ShopManagement/{Index,Create,Edit}` (each with its `.Mobile` twin)
and the shared partial `Shared/_ShopItemDetailsContent` to the Phase 94 foundation (Direction A
"refined glass") in the navy Shop theme, variant (a), on `<html data-theme="shop">`. The phase also
moves the floating merchant button (G1-O2) and fixes four functional breaks (Q-137): the hidden
Return and Sell buttons (A-shop-002), the sell confirmation script that never runs in the modal
(A-shop-017), the dead Preview button (A-shop-027) and the View Details link that answers 404
(A-shop-026). The scope, counts, stylesheet list, `@section Styles` moves, guard tests, deferred
items and the ten-point "Definition of converted" are fixed by `.planning/ROADMAP.md` § "Phase 99"
and are not repeated here. The Shop item creation failure (A-shop-010) was fixed separately (PR
#168, "stamp new shop items with the active board") and is already on this branch.

Every content question for this area was answered at the Phase 94 G1 sign-off (2026-11-01) and is
inherited as recorded in `94-AUDIT.md` § "Operator questions for sign-off":

- **"Add it to the other view"** (the phone gains it unless noted):
  - Q-56: purchase history with Return and Sell on phone Shop/Index;
  - Q-58: Previous and Next links and the "Showing x-y of n items" line;
  - Q-59: the three empty states (search, filters, empty shop) with their Clear buttons;
  - Q-61, the other direction: desktop gains Clear Filters and the "Active" badge when a search is active;
  - Q-63: Sell to Shop on phone Shop/Details;
  - Q-64: the Availability card, Item Properties and the D&D Beyond link on phone Details;
  - Q-65: the draft-review notice, out-of-stock alert and sign-in branch on phone Details;
  - Q-66: the stock `max` and "Max: N available" on the phone purchase quantity;
  - Q-71: one status vocabulary on both views (wording decided in D-06);
  - Q-72: "No Items Yet" with Create First Item on the phone management list;
  - Q-75: the price hint starts "Select a rarity to see pricing suggestions." on both Edit views.
- **Deliberate, recorded, not changed**:
  - Q-57: card description and stock label stay desktop density;
  - Q-60: the merchant button stays desktop-only;
  - Q-62: "Buy" on phone, "Purchase" on desktop;
  - Q-67: the phone management list stays one merged list;
  - Q-68: desktop-only columns and teasers;
  - Q-69: no Bulk Actions trigger on the phone;
  - Q-70: who sees Publish, Deny and Delete is unchanged;
  - Q-73: Preview stays desktop Create only;
  - Q-74: View Details stays desktop Edit only.
- **No merges.** None qualifies under Phase 94 D-13.
- **Q-120 and Q-121, approved in principle and re-confirmed with their AFTER at Gate 1**:
  - Q-120: the purchase history moves from the fixed 280px side panel to an in-flow section under
    the item grid, with the same rows and the same Return and Sell forms;
  - Q-121: one filter card holds the rarity checkboxes, sort, search, Apply Filters and Search.
  Note that the Direction A shop renders still draw the side panel and two bars. They were drawn
  from the views as they stood, so they set the look, not the layout.

No capability is added. Browsing, filtering, searching, buying, returning, selling, item details and
creating, editing, publishing, denying, archiving, reopening and deleting items keep working as
they do today.

</domain>

<decisions>
## Implementation Decisions

### Floating merchant button (G1-O2, desktop Shop/Index only)
- **D-01: Two placements are drawn at Gate 1 and the operator picks one there.**
  - (a) Today's floating bottom-right spot, which the operator likes. It is kept but no longer
    covers the footer: it stops at the content's bottom edge, or an equivalent.
  - (b) A real `<button>` labelled "Talk to the merchant" inside the gold-bordered Emporium hero
    card.
  Either way it becomes a focusable `<button>` with an accessible name, keyboard-operable, with a
  visible focus ring and a target of at least 24px. Nothing is built until Gate 1 picks one.
- **D-02: The merchant keeps its behaviour, made accessible.**
  - The toast keeps the same 20 random quotes, wording unchanged, and still hides itself.
  - It is announced politely (`role="status"` or an equivalent live region).
  - It stays visible while hovered or focused (WCAG 2.2.1).
  - The script moves out of the view into a module, and the inline `onclick` goes.
  - The quote text is inserted as text, not through an HTML string.

### Dead controls (Q-137)
- **D-03: Preview on desktop ShopManagement/Create opens a live card preview dialog.**
  - It shows the item card as players will see it on Shop/Index: name, rarity chip, description,
    price and stock.
  - It is built in the browser from the form's current values. Nothing is saved and there is no new
    endpoint.
  - It uses the foundation's opaque modal surface and Bootstrap modal focus handling.
  - It is drawn at Gate 1.
  - The dead `#item-preview` rule goes.
- **D-04: View Details on desktop ShopManagement/Edit points at `/Shop/Details/{id}`**, the same
  target the management lists already use. The control stays. Research confirms that Shop/Details
  renders Draft and Denied items to a DM (the draft-review notice), so the link never lands on a 404
  or an access-denied page for the editing DM.
- **D-05: Return and Sell (A-shop-002) and the sell script (A-shop-017)**:
  - The purchase-row actions become ordinary visible, keyboard-reachable buttons, with both forms
    and their confirmations kept. Today they are permanently hidden by
    `.return-sell-buttons { opacity: 0; visibility: hidden }`.
  - The Sell confirmation and the "You will receive" total move to a delegated module bound by data
    attributes. That makes them work in the fetched modal and on the full Details page alike.
  - Confirmations stay native `confirm()` with today's wording, through the existing
    `wwwroot/js/confirm-submit.js` `data-confirm-message` pattern (Phase 95 and 98 precedent). This
    covers the management lists' Publish, Archive, Reopen and Delete confirms (A-shop-042) too.

### Status wording (Q-71)
- **D-06: Both management views and the Edit status alert use "Pending Review" for Draft and "Available" for Published.** "Archived" and "Denied" are unchanged. The enum is untouched, and
  this is display text only. The Edit views' `.Replace("Published", "Available")` gives way to one
  shared mapping, so the two layouts cannot drift. The desktop section heading reads "Items Awaiting
  Review" today; whether it changes is Gate 1 item 17 (default: keep the view's words).

### Item visuals
- **D-07: The grey 250px image placeholder in `_ShopItemDetailsContent` becomes a shorter emblem tile**, about 120-160px, in the navy theme. It shows the item-type icon tinted in the item's rarity
  colour and is decorative (`aria-hidden`). Shop items have no picture field, so nothing is cropped
  and no `object-fit` rule is needed. The tile is drawn at Gate 1 in the modal and on Shop/Details,
  desktop and phone.
- **D-08: Every endless animation in the shop family stops, except a gentle merchant movement**
  (A-shop-013, WCAG 2.2.2).
  - Glows and shimmers stay as still styles.
  - The merchant may bob gently, but it stops after a few cycles and does not move at all under
    `prefers-reduced-motion: reduce`.
  - Hover and focus transitions stay short, with no `transition: all` and no layout-property
    animation (A-shop-033).
- **D-09: Rarity chips follow the signed-off Direction A shop render (the shop-a renders).**
  - They are solid chips (common grey, uncommon green, rare teal, very rare purple, legendary gold).
  - Each chip's text colour is picked to reach 4.5:1, and the rarity name is always in text.
  - The same chip appears on desktop and phone, and on every shop and management page
    (A-shop-012, A-shop-016, A-shop-020, A-shop-022).
  - The Purchase button and the outline buttons reach the bar too (A-shop-011, A-shop-006,
    A-shop-021).
- **D-10: Phone purchase history (Q-56) sits under the item list and its paging links**, the same
  in-flow section as the desktop Q-120 relayout, with the same rows and Return and Sell forms. One
  structure on both layouts, drawn at Gate 1.

### Gates, fixtures and delivery (carried over from Phase 98 unchanged)
- **D-11: Two operator gates, reviewed from a local package. No review branch or ref is created or pushed.**
  - **Gate 1**, early, under `~/qb-ui-review/phase-99/gate1/` (index.html, README.md, img/). These
    are scratch mockups, desktop and phone, approved before any of it is built:
    - the Q-120 purchase history under the grid, desktop, and its D-10 phone counterpart;
    - the Q-121 single filter card;
    - both merchant placements (D-01), for the operator to pick one;
    - the Preview dialog (D-03);
    - the emblem tile in the details modal and on Shop/Details (D-07);
    - a look check: one mockup per page type (the Shop/Index grid, Shop/Details, the management
      list, a management form), each side by side with
      `~/qb-ui-review/phase-94/mockups/direction-a/png/shop-a-*` and `shop-a--empty-*`, so the navy
      theme, glass opacity, card scrim, title colours and rarity chips match and cannot drift (the
      v6.0.2 lesson);
    - any proposal from Claude's discretion below that changes visible copy or removes a control.
  - **Gate 2**, at the end, under `~/qb-ui-review/phase-99/gate2/`: one review sheet with every
    before/after pair, desktop and phone, with each contrast or target-size exception as an
    explicit accept or reject item.
- **D-12: A rejected AFTER is redrawn until the operator approves it.** Plan for a possible redraw
  loop at either gate.
- **D-13: Run on the Linux machine that ran Phases 94 to 98**, reusing `~/qb-ui-review/phase-94`
  (Playwright, tools, Direction A renders, BEFORE paths). BEFORE is re-captured from the current
  branch into `~/qb-ui-review/phase-99/`. Front-end work (mockups, gate sheets, view and CSS
  conversion) invokes the `senior-frontend` skill.
- **D-14: The first plan opens with an operator checkpoint.** The operator confirms SQL Server at
  `localhost:1433`, which the agent never provisions. The executor then creates this phase's own
  fixtures through the app's own forms (Phase 94 D-25). The local database holds real member data,
  so every fixture is clearly named and nothing else is touched.
  - **Accounts:** new `qb-audit-p99player@example.invalid`, `qb-audit-p99dm@example.invalid` and
    `qb-audit-p99empty@example.invalid`, created through Admin with the reused Audit Board admin
    login.
    - Their passwords go only in `~/qb-ui-review/phase-99/credentials.json` (mode 600) and are never
      printed.
    - No existing `qb-audit-*` account's memberships, passwords or data change.
  - **Boards:** new boards named for the phase, for example "P99 Audit Shop" for the populated pages
    and "P99 Audit Empty" for the empty states.
  - **Items:** every rarity and type, and every status (Draft, Published, Archived, Denied with a
    reason), created now that PR #168 fixed creation. They cover these stock states: unlimited, in
    stock, low ("N left") and sold out. Also included: long names, availability dates, a D&D Beyond
    link, and enough items to page.
  - **Purchases:** made by the p99 player, so that Return (24-hour window) and Sell both show. They
    are made shortly before each capture that needs Return.
    - The fixture player needs gold, granted only through the app's own means.
    - If no form can grant it, the executor stops and asks the operator. No SQL.
  - **Empty states:** every empty state listed in `94-AUDIT-shop.md`.
  - Fixtures are kept after the phase.
- **D-15: Any email the phase triggers goes to the Mailpit sink only** (`qb-mailpit`; starting it is
  pre-approved), never the real relay.
- **D-16: Work lands in slices on `milestone/v9-rolling-improvements` in this checkout, with one release at the end**, v6.4.0 through a PR into `main` once both gates have passed. Each slice
  leaves every page usable and the suite green (Phase 94 D-08). Real-phone and screen-reader checks
  are listed in the phase UAT and may run after deploy, as in Phases 96 to 98. Each plan's docs
  commit updates ROADMAP.md and STATE.md.

### Claude's Discretion
- **Bulk Actions (A-shop-041, Q-69).** The operator left this to research and planning. Advised
  default: keep the button and modal, restyle them on the foundation, and move the three `alert()`
  calls into a module with today's wording. Disabling the buttons with new copy, or removing the
  control, happens only as a Gate 1 proposal with the operator's explicit yes.
- **Preview on desktop Edit as well.** The operator left this to research and planning. Q-73
  recorded Create only. If the shared module makes it cheap, it may be proposed at Gate 1. It is not
  built without that yes.
- How the merchant stays clear of the footer in placement (a): sticky positioning inside the
  content, or an equivalent. Also the bob's amplitude and cycle count.
- The emblem tile's exact size, icon mapping per item type, and how the rarity tint reaches it.
- How the pricing script pasted into four views (A-shop-046) becomes one module. Its `alert()`
  validation stays native unless a Gate 1 item proposes otherwise.
- How `shop.css`, the five phone sheets and the Shop ranges of `legacy-pages.css` consolidate into
  page sheets loaded through `@section Styles`. Dead overrides go (WINDOWS 28), as does the
  `site.css` duplicate of shop rules (A-shop-031).
- Availability dates (A-shop-038): they move off the raw `ToString` to `Html.LocalTime` or
  `Html.WallClock`, whichever matches what the value is, without changing the helpers.
- Ports: a free pair, checked before use.
- Slice order, slice boundaries and the plan count. A sensible order:
  1. the functional breaks and the shared modules (sell, confirm, pricing, merchant);
  2. the details partial and Shop/Details;
  3. the management pages;
  4. Shop/Index with the gated relayouts, after Gate 1.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and inherited answers
- `.planning/ROADMAP.md` § "Phase 99: Shop and Shop management UI conversion": scope, counts,
  stylesheet list, `@section Styles` moves, guard tests to rewrite, deferred items, approved
  relayouts with BEFORE and AFTER paths, Definition of converted.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md`:
  - § "Operator questions for sign-off": Q-56 to Q-75, Q-120, Q-121, Q-128 and Q-137, with their
    G1 answers;
  - § "Sign-off record (G1, 2026-11-01)": Shop variant (a) and G1-O2;
  - § "Relayout list".
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-shop.md`: all 65 findings
  (A-shop-001 to 065), the twin comparison, relayout proposals R-1 and R-2, empty-state copy (kept
  verbatim; no copy changes without the operator), the stylesheet and markup metrics, and the A-11
  results.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-CONTEXT.md`:
  - D-03: the Shop theme decision;
  - D-04: relayout rules;
  - D-07 and D-08: release per phase, usable between slices;
  - D-12: inline code;
  - D-13 to D-15: twins, merges, mobile verification;
  - D-16 and D-17: the accessibility bar and signature colours;
  - D-25: fixture writes.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-UI-SPEC.md`: the design contract,
  including the Shop theme tokens.
- `.planning/phases/94-whole-app-ui-review-and-unification/deferred-items.md`: the shop alerts and
  required-mark rules this phase moves to foundation variables.
- `~/qb-ui-review/phase-94/mockups/direction-a/png/shop-a-{1440,820,390}.png` and
  `shop-a--empty-*.png`, plus their `-report.json` contrast reports (outside the repository): the
  look reference for the Gate 1 look check and D-09.
- `~/qb-ui-review/phase-94/before/` and `~/qb-ui-review/phase-94/audit/shop/`: the audit's evidence
  (probes and shots).

### Earlier area phases mirrored here
- `.planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/98-CONTEXT.md`:
  D-06 (static badge precedent), D-07 to D-13 (gates, fixtures, delivery) and the v6.3.1/v6.3.2
  lesson on image crops (not triggered here: shop items have no picture).
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-CONTEXT.md` and its summaries: the
  `confirm-submit.js` `data-confirm-message` pattern, with native confirm and alert kept.
- `.planning/WINDOWS.md` entries 12 (required asterisk contrast on the Create and Edit forms), 22
  (the board photo token through `data-theme`, with the photo kept behind `--qb-board-photo`) and 28
  (dead `!important` overrides; `shop.css` carries 70).

### UI system and project rules
- `.claude/ui-guidelines.md`: tokens and layers, components, colour, accessibility checklist, mobile
  twins, no inline code (`@section Styles` / `@section Scripts`, ES modules), vendor files, dates.
- `.claude/architecture.md` § "Time and the board clock": `Html.LocalTime` and `Html.WallClock`.
- `CLAUDE.md`: branching, no planning IDs in source or CSS comments, local database contract.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Foundation sheets `wwwroot/css/tokens.css`, `base.css`, `components.css`, `shell.css` and
  `shell.mobile.css`, including the Shop theme block, the opaque modal surface, the v6.0.2 card
  scrim, and the button-row and empty-state components.
- `wwwroot/js/confirm-submit.js`: delegated `data-confirm-message` submit guard, used for D-05.
- `wwwroot/js/row-link.js` and the Phase 97/98 delegated-listener modules: patterns for item-card
  keyboard access (A-shop-034).

### Established Patterns
- `Shop/Index.cshtml` fills `#itemDetailsModal` with `fetch` plus `innerHTML`, so scripts inside
  `_ShopItemDetailsContent` never run there. That is the root of A-shop-017, and D-05's delegated
  module fixes it.
- The floating merchant is `div.shop-merchant` with `onclick="showMerchantDialog()"` and an inline
  toast builder at `Shop/Index.cshtml:435-530`, which writes the message into the page as an HTML
  string.
- `.return-sell-buttons { opacity: 0; visibility: hidden }` in `site.css` hides the purchase-row
  actions. No rule reveals them.
- `ShopManagementController` has no `Details` action. The management lists link
  `/Shop/Details/{id}`.
- Status display today: `Index.Mobile` uses a switch to "Pending Review" and "Active". Desktop
  `Index` shows "Draft" and "Available". The Edit views use `.Replace("Published", "Available")`.
  The `ItemStatus` enum is Draft, Published, Archived and Denied.
- Inline `confirm(` on eight desktop and four phone management forms, inline `style="display:
  inline"`, four `<style>` blocks and a stray `</script>` in the management views.
- The pricing script is pasted into four views.

### Integration Points
- Guard tests to rewrite to the same intent, none deleted:
  - the `MobileViewsTests` Shop and ShopManagement entries (lines 716-733 and 1123);
  - `BoardPageStaticGuardTests`;
  - `BoardPageGateMatrixTests`;
  - the Shop controller integration tests.
  Q-56, Q-58, Q-63 and Q-64 to Q-66 change what the phone views render, so the purchase-panel
  omission facts flip.
- `shop.css` leaves the global list in `_Layout.cshtml`, and `data-theme="shop"` goes on `<html>`
  for the shop pages.

</code_context>

<specifics>
## Specific Ideas

- The operator likes the merchant's current floating spot and wants it compared, at Gate 1, with
  the in-hero button before choosing (D-01).
- The merchant is the one thing allowed to move in the shop (D-08). Everything else is still.
- The operator expects answers from earlier phases to carry over. The gate, fixture and delivery
  decisions are Phase 98's, unchanged.

</specifics>

<deferred>
## Deferred Ideas

- Real bulk actions (archive, publish or delete several items at once) would be a new capability
  for a phase of its own, if wanted. This phase only converts the existing stub.

</deferred>

---

*Phase: 99-shop-and-shop-management-ui-conversion*
*Context gathered: 2026-11-15*
