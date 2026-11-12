# Phase 94 Audit: Shop and Shop management (area phase 4)

Finding IDs `A-shop-nnn`. Critic-mode review (senior-frontend skill, `critique-checklist.md`) of every Shop-area page on desktop and mobile, baseline app frozen at milestone commit `b6770322`.

## Scope

**Views audited (4 pairs plus Shop Index plus one partial, 11 files):**
`Views/Shop/Index` (+ `.Mobile`), `Views/Shop/Details` (+ `.Mobile`), `Views/ShopManagement/Index` (+ `.Mobile`), `Views/ShopManagement/Create` (+ `.Mobile`), `Views/ShopManagement/Edit` (+ `.Mobile`) and the partial `Views/Shared/_ShopItemDetailsContent` (audited through the pages that render it: desktop Details page and the item-details modal on both layouts; findings on it are tagged "shared partial").

**Stylesheets audited (6):** `shop.css`, `shop.mobile.css`, `shop-details.mobile.css`, `shop-management-index.mobile.css`, `shop-management-create.mobile.css`, `shop-management-edit.mobile.css`. Shop rules that also live in `site.css` (a second copy, see A-shop-031) were read as part of the Shop stylesheet family.

**Manifest rows covered:** `Shop-Index`, `Shop-Index--empty-category`, `Shop-Details`, `ShopManagement-Index`, `ShopManagement-Create`, `ShopManagement-Edit`, each desktop 1440 and mobile Pixel 7 (with reflow checks at desktop 320 and mobile 390).

**Evidence sources.** Screenshots stay outside the repository and are cited by scratch path (`QBR` = `~/qb-ui-review/phase-94`):
- BEFORE captures: `QBR/before/desktop/<row>.png`, `QBR/before/mobile/<row>.png` (rows above); the four Shop rows re-captured with seeded items are also in `QBR/before-shop/`.
- Machine record: `QBR/before/report.json` (per record: console and request errors, overflow at 1440/320 and 412/390, contrast failures at B-med/B-99/B-max, interactive inventory, `img` without alt, desktop focus probe with crops under `QBR/before/desktop/focus/<row>/NN.png`).
- Audit probes (this plan): `QBR/audit/shop/probe-live.json` (modal behaviour, handler existence, link status, running animations, panel geometry, target sizes), `QBR/audit/shop/probe-modal.json` (text contrast inside the item-details modal, flat colour composited over its ancestors, text-shadow ignored), `QBR/audit/shop/navy-contrast.txt` (computed D-16 ratios for the navy theme), `QBR/audit/shop/inv-*.txt` (twin inventories), `QBR/audit/shop/modal-desktop.png` and `modal-mobile.png`.
- Fixtures: shop items seeded by the orchestrator on the Audit Board (ids 5441 to 5444: three published at Common, Rare and Legendary, one Draft; the Quest Items category is empty). No real member data was used; no form was submitted.

**Bar.** D-16: 4.5:1 text, 3:1 large and non-text, translucent surfaces composited over B-max (`#794317`); contrast numbers below come from the BEFORE report unless a probe file is named. D-17 governs the proposed fixes (parchment and gold only where the table allows). Proposed fixes name `--qb-*` tokens and component classes from the UI-SPEC token set only; no free-hand values.

**Severity totals** are in "Area-phase scope notes". **Counts by class** per page are in the index below.

**Known items (recorded, not re-discovered):** the infinite animations (A-shop-013), the four ShopManagement Create/Edit views that carry `<style>` blocks (A-shop-045) and the `.purchase-btn` white label (A-shop-011, C12).

## Index

Twins on one row. `Counts by class` is `visual-bug / inconsistency / cascade-fight / twin-drift / twin-content / a11y / perf / dead-code`. Shared-partial findings are counted on the page that renders them in a fixture (Shop/Details).

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|---------------------------|-------------------------|
| Shop/Index | `Views/Shop/Index.cshtml` | `Views/Shop/Index.Mobile.cshtml` | 4 Shop | 3 / 2 / 2 / 0 / 7 / 8 / 1 / 2 (25) | S1 | Desktop: "The shop is currently empty / Check back later for new items, or contact a Dungeon Master about stocking the shop."; "No items match your filters / Try removing some rarity filters or changing the sort order."; "No items match your search / Try a different name or adjust your filters."; panel "No purchases yet / Your purchase history will appear here". Mobile: "No items found / Try adjusting your filters or search query." (see "Empty-state copy") | 7 (Q-shop-01 to 07), plus theme decision Q-shop-22 and relayout sign-off R-1, R-2 |
| Shop/Details | `Views/Shop/Details.cshtml` (+ partial `Views/Shared/_ShopItemDetailsContent.cshtml`) | `Views/Shop/Details.Mobile.cshtml` | 4 Shop | 1 / 2 / 0 / 0 / 3 / 4 / 0 / 2 (12) | S1 | none (no list). Out-of-stock and draft notices: desktop "This item is currently out of stock." and "This item is currently under review and not available for purchase."; mobile "Out of stock." and "This item is not currently available for purchase." | 4 (Q-shop-08 to 11) |
| ShopManagement/Index | `Views/ShopManagement/Index.cshtml` | `Views/ShopManagement/Index.Mobile.cshtml` | 4 Shop | 0 / 0 / 0 / 1 / 5 / 9 / 0 / 2 (17) | S1 | Desktop: "No Items Yet / You haven't created any shop items yet. Start by adding your first item!" and "No Other Items / No other DMs have published items yet." Mobile: "No items yet / Add the first item to your shop." | 6 (Q-shop-12 to 17) |
| ShopManagement/Create | `Views/ShopManagement/Create.cshtml` | `Views/ShopManagement/Create.Mobile.cshtml` | 4 Shop | 1 / 0 / 0 / 0 / 0 / 4 / 0 / 3 (8; Create and Edit shared findings counted here) | S1 | none (form) | 1 (Q-shop-18) |
| ShopManagement/Edit | `Views/ShopManagement/Edit.cshtml` | `Views/ShopManagement/Edit.Mobile.cshtml` | 4 Shop | 0 / 0 / 0 / 0 / 1 / 1 / 0 / 1 (3) | S1 | none (form) | 2 (Q-shop-19, Q-shop-21) |


## Findings

Ordered by severity (S1 first). Field order per finding: page and layout, class, severity (and WCAG for `a11y`), evidence, proposed fix, area phase, operator question. Every area phase is "4 Shop" unless stated. Contrast ratios are over the composited surface; "opaque" means a solid component surface.

### S1: text hidden or unreadable, or a control unreachable

#### A-shop-001: Desktop Shop empty states render dark ink on the navy panel
- **Page and layout:** `Shop/Index`, desktop (all three empty variants).
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** `QBR/before/desktop/Shop-Index--empty-category.png`; BEFORE report `h3` "The shop is currently empty" 1.02:1 (large-text bar 3:1) and `p` 1.02:1. Markup `Views/Shop/Index.cshtml:362-387` uses class `empty-shop-state`, for which no rule exists in any stylesheet; the only look-alike rules are `.empty-shop` at `shop.css:232` and `site.css:273`, which no markup uses. The text therefore inherits the global dark ink onto the near-black `.shop-inventory` panel (`shop.css:54-60`).
- **Proposed fix:** give the block the shared `.empty-state` component with `--qb-fg` for the heading and `--qb-fg-muted` for the line, on whichever surface the theme decision (A-shop-037) leaves under it; delete `.empty-shop` and the dead class name.
- **Operator question:** N.

#### A-shop-002: Return and Sell buttons in the purchase history are permanently invisible
- **Page and layout:** `Shop/Index`, desktop (purchase history panel).
- **Class / severity:** visual-bug, S1 (a control is unreachable).
- **Evidence:** `Views/Shop/Index.cshtml:108-133` renders per-purchase Return (within 24 h) and Sell forms inside `div.return-sell-buttons`. `site.css:1360-1364` sets `.return-sell-buttons { opacity: 0; visibility: hidden }`, and no rule or script anywhere reveals it (a search of the Service project for `return-sell` finds only the markup and these `site.css` rules; there is no `:hover` reveal and no JavaScript). `visibility: hidden` also removes the buttons from the tab order. Static evidence only: the Audit Board holds no purchase rows, and none was created because that would spend gold. This is the only place the 24-hour Return exists.
- **Proposed fix:** show the actions as an ordinary part of each purchase row (`.purchase-row`, `.btn-success` and `.btn-warning` with the foundation's label tokens `--qb-success-fg` and near-black on amber), reachable by keyboard; keep both forms and their `confirm` behaviour.
- **Operator question:** N (a regression to restore, not a content choice). Related to A-shop-028.

#### A-shop-003: Purchase panel empty text is invisible
- **Page and layout:** `Shop/Index`, desktop.
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** `QBR/before/desktop/Shop-Index.png` (left panel); BEFORE report `p.mb-0` "No purchases yet" 1.00:1 and `small` "Your purchase history will appear here" 1.00:1, dark ink (`rgb(26,15,8)`) on `rgba(15,15,25,.95)`; `Views/Shop/Index.cshtml:164-171` (`text-muted` has no effect there); panel rules `shop.css:378-394`. The panel's own parchment rules cover `.purchase-name` and `.purchase-details`, not this block.
- **Proposed fix:** `.empty-state` component with `--qb-fg` and `--qb-fg-muted`.
- **Operator question:** N.

#### A-shop-004: Item-details modal on desktop: labels, helper text and the Sell block read at 1.1 to 1.8:1
- **Page and layout:** shared partial `_ShopItemDetailsContent`, rendered in the `#itemDetailsModal` on desktop (modal content `bg-dark`, `rgb(33,37,41)`).
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** `QBR/audit/shop/modal-desktop.png`, `probe-modal.json`: "Quantity" and "Quantity to Sell" labels 1.22:1, "Max: N available" 1.09:1, "Sell to Shop" heading 1.22:1, "Sell this item to the shop for ..." 1.09:1, "(half the shop price)" 1.22:1, "You will receive:" 1.09:1, Type and Rarity values 1.82:1, "Only N left in stock" badge 1.29:1. The same partial reads fine on the full Details page (glass card) because the modal is a different surface: the page-level text rules assume glass over wood. Markup `_ShopItemDetailsContent.cshtml:38-125` and `:151-239`.
- **Proposed fix:** one overlay surface for modals (`--qb-surface-overlay`) with `--qb-fg` and `--qb-fg-muted` text, `.badge.bg-warning` label near-black (`--qb-warning-fg`), `text-halo` removed from labels on an opaque surface.
- **Operator question:** N.

#### A-shop-005: Item-details modal on mobile: item title unreadable
- **Page and layout:** `Shop/Details.Mobile` content loaded into the mobile Shop modal.
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** `QBR/audit/shop/modal-mobile.png`; `probe-modal.json`: `h5.parchment-text` 1.22:1 (item name on the dark modal). The modal fetch (`Shop/Index.Mobile.cshtml:262-272`) receives `Details.Mobile` rendered as a partial (`ShopController.Details` returns `PartialView` when `isModal`), so `@section Styles` (`Details.Mobile.cshtml:6-8`) never runs: `shop-details.mobile.css` is not loaded (`probe-live.json`: `stylesheetLoaded: false`) and the rarity, name and card rules are missing. The modal also contains the page's full-width "Back to Shop" link.
- **Proposed fix:** the modal body uses the same overlay surface as A-shop-004 with `--qb-fg`; drop the in-modal "Back to Shop" (operator-approved only if A-shop-054 is answered "add"); load area CSS through the foundation, not a section a partial cannot render.
- **Operator question:** N.

#### A-shop-006: "View Shop" outline button text unreadable
- **Page and layout:** `ShopManagement/Index`, desktop and mobile.
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** BEFORE report `a.btn.btn-outline-primary` "View Shop" 1.22:1 (blue `rgb(13,110,253)` on wood at B-max), mobile `a.btn.btn-outline-secondary` 1.70:1; `QBR/before/desktop/ShopManagement-Index.png`, `QBR/before/mobile/ShopManagement-Index.png`; `Views/ShopManagement/Index.cshtml:30` and `Index.Mobile.cshtml:24-26`.
- **Proposed fix:** `.btn-secondary` filled (the "filled, not outline" rule) with label at least 4.5:1; same class on both layouts (see A-shop-063).
- **Operator question:** N.

#### A-shop-007: Description teasers and muted text on the parchment table rows
- **Page and layout:** `ShopManagement/Index`, desktop.
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** BEFORE report `small.text-muted` 1.14 to 1.31:1 for every item teaser (parchment at 70% alpha on a parchment table cell, banned by D-17 row "Parchment"); `QBR/before/desktop/ShopManagement-Index.png`; markup `Index.cshtml:65`, `:137`, `:309`; `modern-card.css:102` and `site.css:639` recolour `.text-muted`.
- **Proposed fix:** `--qb-ink-muted` on `--qb-surface-paper` for table cells (the paper row is the surface); never parchment on paper.
- **Operator question:** N.

#### A-shop-008: "N left" stock badges unreadable in the item table
- **Page and layout:** `ShopManagement/Index`, desktop.
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** BEFORE report `span.badge.bg-warning` "3 left", "1 left", "4 left" 1.29:1 (parchment label on amber, `rgb(244,228,188)`); `Views/ShopManagement/Index.cshtml:178-181` and `:335-338`; shop rules `shop.css:464-466` pull parchment onto every `small`, and the badge inherits it. The spec requires near-black on amber (12.9:1).
- **Proposed fix:** `.badge.bg-warning` uses `--qb-warning` fill and `--qb-warning-fg` label; same for the Details page badge ("Only N left in stock", also 1.29:1).
- **Operator question:** N.

#### A-shop-009: Edit (mobile): "Item ID" unreadable on the status alert
- **Page and layout:** `ShopManagement/Edit.Mobile`.
- **Class / severity / WCAG:** a11y, S1, 1.4.3.
- **Evidence:** BEFORE report `small.text-muted` "Item ID: #5441" 1.06:1 on the light-blue `alert-info` surface; `QBR/before/mobile/ShopManagement-Edit.png`; `shop-management-edit.mobile.css:26-31` forces `rgba(244,228,188,.7) !important` onto every `small` inside the card, including those on a light alert. Same markup on desktop (`Edit.cshtml:42`) measures 2.02:1.
- **Proposed fix:** alert surfaces get their own `--qb-info`/`--qb-info-fg` pair and the info-alert text uses the status text variant; the card-wide `small` recolour goes.
- **Operator question:** N.

#### A-shop-010: Creating a shop item always fails (functional break, not a UI fix this phase makes)
- **Page and layout:** `ShopManagement/Create`, both layouts.
- **Class / severity:** visual-bug (nearest class; it is a functional defect), S1.
- **Evidence:** `ShopManagementController.Create` (POST) never sets `GroupId`, so the insert violates `FK_ShopItems_Groups_GroupId`; recorded in `deferred-items.md` (found in plan 02). The Create form was not submitted in this audit. It is why the fixtures were seeded directly.
- **Proposed fix:** none in Phase 94. A separate bug fix (set the item's group from the active board, as the quest controller does) outside this phase.
- **Area phase:** none (handed off as a separate fix). **Operator question:** N.

### S2: visibly wrong or fails the AA bar

#### A-shop-011: Purchase button label 2.13:1
- **Page and layout:** `Shop/Index`, desktop.
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** BEFORE report `button.purchase-btn` "Purchase" 2.13:1 (white 13.6px on the green gradient); `shop.css:206-217` (`linear-gradient(45deg, #28a745, #20c997)`); markup `Index.cshtml:343-349`, `:353-356`. Hover state `shop.css:219-223`.
- **Proposed fix:** one solid `--qb-success` fill with `--qb-success-fg` label (at least 4.5:1 in rest, hover, active, focus-visible); the single commit action is `--qb-action` only where it is the view's primary action.
- **Operator question:** N.

#### A-shop-012: Rarity badges: legendary 1.63:1, and rarity chips differ from desktop to mobile
- **Page and layout:** `Shop/Index` desktop and the badges in `ShopManagement/Index`.
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** BEFORE report `span.item-rarity.rarity-legendary` 1.63:1 (white on the red to gold gradient, `shop.css:154-158`); on `ShopManagement/Index` the rarity chips read 3.57 (Rare), 3.72 (Common), 1.29 (Legendary, parchment label on gradient). Rules `shop.css:124-158`, mirrored in `site.css:165-200`.
- **Proposed fix:** a single badge component with a solid status fill per rarity and a label token per fill (legendary uses gold with `--qb-accent-ink`, D-17 row "Gold": a button or badge fill with a near-black label is allowed); drop the gradient.
- **Operator question:** N.

#### A-shop-013: Ten infinite animations in the Shop family (WCAG 2.2.2); only decorative, none paused
- **Page and layout:** `Shop/Index`, `ShopManagement/Index`, shared item type and rarity badges, plus the four Create and Edit views.
- **Class / severity / WCAG:** a11y (also perf), S2, 2.2.2 (and A-7).
- **Evidence:** `shop.css:157` (`legendary-glow`), `:270` and `:284` (`magic-shimmer`, `magic-shine`), `:314`, `:335`, `:345` (`quest-pulse`, `quest-sweep`, `quest-sparkle`): six sites. Four more infinite uses of `legendary-glow` sit in the `<style>` blocks at `Create.cshtml:291`, `Edit.cshtml:326`, `Create.Mobile.cshtml:208`, `Edit.Mobile.cshtml:252` (dead copies, A-shop-045). Live probe `probe-live.json`: `Shop/Index` runs 2 infinite animations (`legendary-glow` twice); `ShopManagement/Index` runs 5 (`magic-shimmer`, `magic-shine`, `legendary-glow`, again `magic-shimmer`, `magic-shine`). Animated properties are `box-shadow` and a `transform: scale` pulse (`shop.css:348-357`), not only `transform` and `opacity`. No `prefers-reduced-motion` block exists in the Shop CSS.
- **Proposed fix:** cap per A-7: no infinite loop (run the glow a few seconds or draw a static ornament), animate only `transform` and `opacity`, honour reduced motion through the foundation block; one rule per effect in one place.
- **Operator question:** N.

#### A-shop-014: Filter checkboxes have a 1px near-black focus ring on a near-black panel
- **Page and layout:** `Shop/Index`, desktop (`.shop-filter-row`).
- **Class / severity / WCAG:** a11y, S2, 2.4.7 (and A-2).
- **Evidence:** focus probe stops 13 to 15: `input[name=rarity]` outline `1px rgb(16,16,16)` over `rgba(0,0,0,.5)` on navy; crops `QBR/before/desktop/focus/Shop-Index/13.png` to `15.png`; markup `Index.cshtml:214-226`; `shop.css:690-695` styles the checkbox (14 by 14, `accent-color`), not its focus.
- **Proposed fix:** the foundation two-tone ring (`--qb-focus-inner`, `--qb-focus-outer`, `--qb-focus-width`, `--qb-focus-offset`) on `.form-check-input` and checkbox inputs; keep the surrounding label as the hit target.
- **Operator question:** N.

#### A-shop-015: Purchase history panel disappears below 992px with no way to open it
- **Page and layout:** `Shop/Index`, desktop layout in a narrow window.
- **Class / severity:** visual-bug, S2.
- **Evidence:** `shop.css:520-528` and `site.css:1330-1352` translate the panel off-screen below 992px and define a `.mobile-open` state, but nothing adds that class (a search of the Service project finds no other use of `mobile-open` in views or scripts), and there is no toggle. `probe-live.json`: at 320px `panelLeft` is -250 and `toggle` is false; at 1000px the panel occupies 250px of a 1000px window. Between 992 and 1200px the panel takes 250 to 280px of fixed width. The 24-hour Return lives only here (A-shop-002).
- **Proposed fix:** relayout R-1 (panel becomes an ordinary section reachable at every width; nothing collapses behind a toggle).
- **Operator question:** Y (relayout sign-off, see "Relayout proposals").

#### A-shop-016: Shop Index (mobile): rarity, subtitle, name and price all below 4.5:1
- **Page and layout:** `Shop/Index.Mobile`.
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** BEFORE report at B-max: rarity "Rare" 2.15:1 (`#60a5fa`), "Legendary" 3.28:1 (`#fbbf24`), "Common" 3.55:1 (`#d0d0d0`), subtitle 2.99:1 (parchment 70%), item name and price 4.34:1 (parchment on glass); empty-category variant: line "Try adjusting your filters or search query." 2.99:1. `QBR/before/mobile/Shop-Index.png`; `shop.mobile.css:29-44` and `:60-66`, `:107-129`. Note the mobile chips are plain coloured text with no fill, so their colour is the only rarity carrier beside the word (A-10 satisfied by the word).
- **Proposed fix:** `--qb-fg` for name and price, `--qb-fg-muted` for the subtitle, rarity chip as the shared badge component (A-shop-012) so desktop and mobile agree.
- **Operator question:** N.

#### A-shop-017: Item-details partial: inline script never runs in the modal, so Sell has no confirmation
- **Page and layout:** shared partial `_ShopItemDetailsContent`, desktop modal.
- **Class / severity:** dead-code (a functional break), S2.
- **Evidence:** the Sell form uses `onsubmit="return confirmSell(...)"` and the quantity input `onchange="updateSellPrice(...)"` (`_ShopItemDetailsContent.cshtml:216`, `:227`) with the functions defined in a `<script>` at `:249-268`. The modal is filled with `innerHTML` (`Shop/Index.cshtml:462`), and scripts inserted that way do not execute. `probe-live.json` (desktop modal): `typeof confirmSell` and `typeof updateSellPrice` are both `undefined`. The inline handler then throws instead of returning false, so submitting the Sell form from the modal goes ahead without the confirmation dialog, and the "You will receive" total never updates. On the full Details page the script runs, so the two entry points behave differently. Not verified by submitting (a Sell spends fixture gold).
- **Proposed fix:** move the behaviour into a script file that binds to the form by data attribute (event delegation on the modal body) so it works for fetched HTML; keep the confirmation. D-12.
- **Operator question:** N.

#### A-shop-018: Shop Details (desktop page): many text pairs and buttons below the bar
- **Page and layout:** `Shop/Details`, desktop (partial rendered inside `modern-card`).
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** `QBR/before/desktop/Shop-Details.png`; BEFORE report: card headings "Availability", "Item Properties", "Description" 2.92:1 (parchment on the tan card header); "Type:" and "Rarity:" 2.35:1; the values "Equipment" and "Common" 3.00:1; description paragraph 3.18:1; helper "Max: 4 available" 2.99:1; "Purchase Item" `btn-success` 2.87:1; "Sell to Shop" `btn-warning` white on orange 2.19:1 (spec: near-black label, never white); "Back to Shop" `btn-secondary` 3.49:1; badges 3.57:1 (Equipment), 3.72:1 (Common), 1.29:1 (stock warning); "Quantity" 4.34:1; "18 gp" gold on glass 3.36:1 (gold as body text on glass, banned by D-17). Sources: `_ShopItemDetailsContent.cshtml:38-239`, `Details.cshtml:30-47`, `modern-card.css:102`, `site.css:639`.
- **Proposed fix:** card header on `--qb-surface-paper` with `--qb-ink`, body text `--qb-ink` or `--qb-fg` per surface, status fills with `*-fg` labels, `btn-warning` label `--qb-warning-fg`, currency uses `--qb-accent-text` (the lighter gold text variant, D-17 item 4 allows currency).
- **Operator question:** N.

#### A-shop-019: "Back to Shop" shows no focus indicator
- **Page and layout:** `Shop/Details`, desktop.
- **Class / severity / WCAG:** a11y, S2, 2.4.7 (A-2).
- **Evidence:** focus probe stop 13 on `Shop-Details`: `outlineWidth: 0`, no box-shadow; crop `QBR/before-shop/desktop/focus/Shop-Details/13.png`; link at `Details.cshtml:42`. The probe also reports stop 15 with no focused element after the last link (end of page; no issue).
- **Proposed fix:** foundation ring on `.btn`.
- **Operator question:** N.

#### A-shop-020: Rarity text unreadable on mobile pages that do not load `shop.mobile.css`
- **Page and layout:** `Shop/Details.Mobile` and `ShopManagement/Index.Mobile` (rarity chip); same defect pattern for the Details modal.
- **Class / severity / WCAG:** visual-bug, S2, 1.4.3.
- **Evidence:** BEFORE report mobile `Shop-Details`: "Common" 1.77:1 (`rgb(33,37,41)` on glass at B-med); mobile `ShopManagement-Index`: "Common", "Rare", "Legendary" 2.45:1 and price `small.text-muted` 2.46:1 at B-max. The rarity colours (`shop.mobile.css:107-129`) are defined in the Shop Index stylesheet only, loaded by `Index.Mobile.cshtml:55-57`; the other mobile pages use the class `item-rarity` without them (`Details.Mobile.cshtml:17-19`, `ShopManagement/Index.Mobile.cshtml:34-36`). `QBR/before/mobile/Shop-Details.png`, `QBR/before/mobile/ShopManagement-Index.png`.
- **Proposed fix:** the shared rarity badge component (A-shop-012) in `components`, so the class carries its own colour wherever it appears.
- **Operator question:** N.

#### A-shop-021: Management buttons: "Add New Item" 2.87:1, "Bulk Actions" 3.04:1
- **Page and layout:** `ShopManagement/Index`, desktop.
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** BEFORE report `a.btn.btn-success` "Add New Item" 2.87:1, `button.btn.btn-info` "Bulk Actions" 3.04:1 (white labels in cards); `Views/ShopManagement/Index.cshtml:26-35`.
- **Proposed fix:** `--qb-success`/`--qb-success-fg`, `--qb-info`/`--qb-info-fg` pairs (UI-SPEC "Button and status semantics").
- **Operator question:** N.

#### A-shop-022: Item type, rarity and status chips in the table
- **Page and layout:** `ShopManagement/Index`, desktop.
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** BEFORE report: `badge.type-equipment` 3.72:1, `badge.type-magic-item` 3.43:1, `badge.bg-secondary` "Draft" 3.72:1, `badge.bg-success` "Available" 3.60:1 (parchment label, not the white the rule asks for); `shop.css:264-307` sets `color: white !important` but `shop.css:464-466` (`.purchase-details small`) and the table rules deliver parchment; `Index.cshtml:68`, `:155`, `:193-205`.
- **Proposed fix:** badge component with `--qb-{name}-fg` labels (A-shop-012).
- **Operator question:** N.

#### A-shop-023: Management empty states and section counts
- **Page and layout:** `ShopManagement/Index`, desktop.
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** BEFORE report: "No Other Items" `h5` 2.69:1 and "No other DMs have published items yet." 2.69:1 (parchment on the blue `alert-info` fill); count `small.text-muted` "(4 items)" and "(0 items from other DMs)" 2.99:1 at 21px; `QBR/before/desktop/ShopManagement-Index.png`; `Index.cshtml:44`, `:113`, `:284`, `:269-276`, `:410-414`.
- **Proposed fix:** `.empty-state` component (not a Bootstrap `alert-info`), counts in `--qb-fg-muted` at body size.
- **Operator question:** N.

#### A-shop-024: Create and Edit (both layouts): labels, helper text, required marker and buttons below the bar
- **Page and layout:** `ShopManagement/Create` and `Edit`, desktop and mobile.
- **Class / severity / WCAG:** a11y, S2, 1.4.3 (and 1.4.11 for the outline button).
- **Evidence:** BEFORE report Create desktop: labels 4.34:1 (parchment on glass at B-max, 600 weight at 16px), `div.form-text` 2.99:1, `#price-suggestion` 2.99:1, `h5.text-info` "Availability Window" 4.34:1, required asterisk `span.text-danger` 1.97:1 (`#ff6b6b`, banned as text, C5) on desktop and 1.21:1 (`#dc3545`) on mobile, `a.btn.btn-secondary` "Cancel" 3.49:1, `button.btn-outline-info` "Preview" 2.79:1, `button.btn-primary` "Create Item" 3.15:1 (white on the in-card blue gradient). Edit desktop identical plus "Back to Management" 3.49:1, "View Details" 2.79:1, "Save Changes" 3.15:1. `QBR/before/desktop/ShopManagement-Create.png`, `QBR/before/desktop/ShopManagement-Edit.png`, `QBR/before/mobile/ShopManagement-Create.png`, `QBR/before/mobile/ShopManagement-Edit.png`; mobile stylesheets `shop-management-create.mobile.css:14-31` and `shop-management-edit.mobile.css:14-31`. The asterisk comes from the shared `_MarkdownEditor` partial (Quest area owns it).
- **Proposed fix:** `--qb-fg` labels, `--qb-fg-muted` helper text, `--qb-danger-text` for the marker with the word "required" kept for the field, `--qb-action`/`--qb-action-fg` for the one commit action, filled secondary for Cancel and Back.
- **Operator question:** N.

#### A-shop-025: Availability checkbox label unreadable on the mobile forms
- **Page and layout:** `ShopManagement/Create.Mobile` and `Edit.Mobile`.
- **Class / severity / WCAG:** a11y, S2, 1.4.3.
- **Evidence:** BEFORE report `label.form-check-label` "Set specific availability date..." 1.77:1 (dark `rgb(33,37,41)` on glass at B-med); `QBR/before/mobile/ShopManagement-Edit.png` (faint line under "Availability Window"). The mobile stylesheets recolour `.form-label` but not `.form-check-label` (`shop-management-edit.mobile.css:14-17`); desktop pairs read 4.34:1 (A-shop-024). Markup `Create.Mobile.cshtml:97-103`, `Edit.Mobile.cshtml:126-132`.
- **Proposed fix:** `--qb-fg` on form labels including check labels; label is the whole-row tap target (A-5).
- **Operator question:** N.

#### A-shop-026: Edit (desktop): "View Details" leads to a page that does not exist
- **Page and layout:** `ShopManagement/Edit`, desktop only.
- **Class / severity:** dead-code (broken link), S2.
- **Evidence:** `Edit.cshtml:189` links to `/ShopManagement/Details/@Model.Id`; `ShopManagementController` has no `Details` action (actions are Index, Create, Edit, Publish, Archive, Deny, Reopen, Delete); `probe-live.json`: `GET /ShopManagement/Details/5441` as the DM answers 404 (`editViewDetailsStatus`). The mobile Edit view has no such button.
- **Proposed fix:** point at `/Shop/Details/{id}` (as the management lists already do); do not remove the control (D-04).
- **Operator question:** Y (desktop-only control, see Q-shop-19).

#### A-shop-027: Create (desktop): "Preview" button does nothing
- **Page and layout:** `ShopManagement/Create`, desktop only.
- **Class / severity:** dead-code, S2.
- **Evidence:** `Create.cshtml:149` calls `previewItem()`; the function is defined nowhere in the views, partials or scripts (`probe-live.json`: `typeof previewItem` is `undefined`; the click throws a ReferenceError). The only trace of a preview feature is the dead rule `#item-preview` at `Create.cshtml:304-309`, whose element does not exist. Mobile has no Preview.
- **Proposed fix:** keep the control; wire it in the Shop phase only if the operator wants a preview, otherwise answer the twin question as "deliberate". No removal without an answer (D-14).
- **Operator question:** Y (Q-shop-18).

#### A-shop-028: Purchase history exists on desktop only, and the mobile layout has no Return or Sell at all (twin-content)
- **Page and layout:** `Shop/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S2.
- **Evidence:** `Shop/Index.cshtml:59-174` (My Transactions panel with the last 15 rows, Return and Sell forms, "Showing latest 15 purchases"); `Index.Mobile.cshtml` has no counterpart (twin inventory `QBR/audit/shop/inv-Shop-Index.txt`: desktop-only action `Url:Sell@Shop`, fields `id` and `quantity`). The omission is pinned by an existing test: `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs:716-733` (`MobileShopIndex_MobileUserAgent_OmitsPurchaseHistoryPanel`, doc comment "Purchase History side panel is omitted on mobile"). Because of A-shop-002 the desktop Return and Sell buttons are not reachable today either, so the practical loss on mobile is the history list.
- **Proposed fix:** if the operator answers "add it", a compact history section below the item grid on mobile using the shared purchase row, with its own test; if "deliberate", the guard test already pins it.
- **Operator question:** Y (Q-shop-01).

#### A-shop-029: Sell to Shop exists on desktop only (twin-content)
- **Page and layout:** `Shop/Details` (partial), desktop versus mobile.
- **Class / severity:** twin-content, S2.
- **Evidence:** `_ShopItemDetailsContent.cshtml:204-239` (Sell block with quantity and "You will receive"); `Details.Mobile.cshtml` has only Purchase and Back. A mobile player has no Sell path anywhere (the history panel is also desktop only, A-shop-028).
- **Proposed fix:** per the answer; "add" needs its own approval and tests.
- **Operator question:** Y (Q-shop-08).


### S3: inconsistency with the system

#### A-shop-030: Stock level colours are dead rules
- **Page and layout:** `Shop/Index`, desktop.
- **Class / severity:** cascade-fight, S3.
- **Evidence:** `shop.css:188-191` sets `.item-stock { color: rgba(244,228,188,.7) !important }`; the variants `.item-stock.unlimited`, `.low`, `.sold-out` (`shop.css:193-204`) are not `!important` and lose, so every stock label is parchment and the intended green, red and grey never appear (the icon and words still carry meaning, A-10 holds). Computed against the navy card the intended colours would have been 4.07:1 (`#198754`), 4.07:1 (`#dc3545`) and 3.93:1 (`#6c757d`), all under 4.5:1 for 13.6px text (`navy-contrast.txt`).
- **Proposed fix:** one stock component using `--qb-success-text`, `--qb-warning-text`/`--qb-danger-text`, `--qb-fg-muted` text variants chosen at 4.5:1; remove the `!important`.
- **Operator question:** N.

#### A-shop-031: `shop.css` fights the framework with 70 `!important` and is loaded on every desktop page; its rules are duplicated in `site.css`
- **Page and layout:** all desktop pages (global load), Shop family.
- **Class / severity:** cascade-fight, S3.
- **Evidence:** `Views/Shared/_Layout.cshtml:25` loads `shop.css` in the global list (the mobile layout does not, which is why mobile pages need their own files). `shop.css` has 70 `!important`; four rules re-colour Bootstrap `!important` utilities: `.item-name.text-light` (`:119`), `.purchase-details .text-muted`, `.text-warning`, `.text-success` (`:468`, `:473`, `:482`). 49 of the 70 are the two modal families at `shop.css:535-650`. The same rule families are written a second time in `site.css`: shop container and cards (`site.css:46-310`), purchase panel (`:1202-1354`), `.return-sell-buttons` (`:1360-1393`), `.purchase-item.sold-returned` (`:1562`), `.btn-sm.btn-action` (`:1460`). `shop.css` loads after `site.css` and wins; the `site.css` copies are dead except where they differ (the Return and Sell block exists only in `site.css`, A-shop-002).
- **Proposed fix:** one Shop stylesheet in the `pages` layer loaded through `@section Styles` on the Shop views only; the `site.css` copies deleted; the modal rules moved to the owning view's stylesheet (A-shop-032).
- **Operator question:** N.

#### A-shop-032: Dead modal rule family and dead class names in `shop.css`
- **Page and layout:** Shop family stylesheets.
- **Class / severity:** dead-code, S3.
- **Evidence:** `#addCharacterModal` rules (`shop.css:591-650`, 30 `!important` lines) match no markup anywhere in the Service project (the only `addCharacterModal` hits are these rules). `#denyModal` rules (`shop.css:535-589`) do style a real modal, but it lives in `ShopManagement/Index` and `Index.Mobile`, not in the Shop. `.empty-shop` (`shop.css:232`) is unused. `.purchase-details-text` and `.empty-shop` also appear in `site.css`.
- **Proposed fix:** delete the `#addCharacterModal` block and `.empty-shop`; move `#denyModal` to the management stylesheet or the shared modal component.
- **Operator question:** N.

#### A-shop-033: Layout-property animation, `transition: all` and `backdrop-filter` on repeated items
- **Page and layout:** Shop family, both layouts.
- **Class / severity:** perf, S3.
- **Evidence:** `transition: all` at `shop.css:41`, `:73`, `:213`, `:255`, `:437`, `:494`, `:732`, `:748`, `:767` (nine) and again in the `site.css` copies; `.item-card::before` animates `left` (`shop.css:82-95`); `backdrop-filter: blur` on repeated items: `.shop-item-card-mobile` (`shop.mobile.css:14`, one per item), `.shop-mgmt-item-card` (`shop-management-index.mobile.css:17`, one per item), plus single large panels at `shop.css:391`, `shop.mobile.css:51` and `:82`, `shop-details.mobile.css:7`, `shop-management-index.mobile.css:7` and `:52`, `shop-management-create.mobile.css:7`, `shop-management-edit.mobile.css:7`. The UI-SPEC forbids blur on a repeated item and `transition: all`.
- **Proposed fix:** the glass surface as a flat `--qb-surface-glass` for repeated cards (no blur), blur only on the shell and the single outer panel; transitions list `transform` and `opacity` with `--qb-dur-base`.
- **Operator question:** N.

#### A-shop-034: Item cards and the floating merchant cannot be reached or used by keyboard
- **Page and layout:** `Shop/Index`, desktop and mobile (cards); desktop (merchant).
- **Class / severity / WCAG:** a11y, S3, 2.1.1 and 4.1.2.
- **Evidence:** `Index.cshtml:293-295` and `Index.Mobile.cshtml:186-188`: a `div` with `data-bs-toggle="modal"` and no `tabindex` or `role` (the inner Purchase button opens the same modal, so the card has a keyboard equivalent). `Index.cshtml:432-434`: the floating merchant is a `div` with an `onclick`, no `tabindex`, `role` or `aria-label` (`probe-live.json`: all null, 80 by 80px); it is the only trigger of the merchant toast.
- **Proposed fix:** make the merchant a real `button` with an `aria-label`; mark the card click as a pointer convenience over the existing button.
- **Operator question:** N.

#### A-shop-035: Shop Index scripts, inline handlers and inline style (and two dead bits)
- **Page and layout:** `Shop/Index` desktop and mobile.
- **Class / severity:** dead-code, S3.
- **Evidence:** `<script>` blocks `Index.cshtml:453-537` and `Index.Mobile.cshtml:260-283` (identical modal loader); inline `onclick` at `Index.cshtml:346`, `:353`, `:432` and `Index.Mobile.cshtml:201`, `:209`; inline `style="max-width: 700px; margin: 0 auto;"` at `Index.cshtml:276`. Dead: the `DOMContentLoaded` loop at `Index.cshtml:468-475` repeats the inline `stopPropagation`; on mobile the same loop (`Index.Mobile.cshtml:276`) selects `.purchase-btn`, a class the mobile buttons do not have, so it never matches. The 20 merchant lines in `showMerchantDialog` and the toast builder inject strings with template literals (`:509-520`).
- **Proposed fix:** one ES module for the modal loader and the merchant toast, bound by `data-` attributes; the width on a component class.
- **Operator question:** N.

#### A-shop-036: `min-height: 100vh` forces a scroll and the panel hard-codes the navbar height
- **Page and layout:** `Shop/Index`, desktop.
- **Class / severity:** visual-bug, S3.
- **Evidence:** `.shop-container { min-height: 100vh; padding: 2rem 0 }` (`shop.css:5-9`, again `site.css:46-50`) on top of the navbar makes the document taller than the viewport even with three items: `probe-live.json` `docH` 1042 at a 900px viewport (about 140px of forced scroll, footer pushed below the fold). The fixed panel sits at `top: 56px` and `height: calc(100vh - 56px)` (`shop.css:378-383`) instead of `--qb-nav-h`. In the full-page captures the oak board ends where the fixed panel ends and a pale strip shows under the navy block (`QBR/before/desktop/Shop-Index.png`, lower edge); that strip may be a capture effect of a fixed background and should be re-checked live, the forced scroll is real.
- **Proposed fix:** no viewport-height minimum on the container; `top` and `height` from `--qb-nav-h`; the content width from the foundation container.
- **Operator question:** N.

#### A-shop-037: The navy theme exists on one page only
- **Page and layout:** `Shop/Index`, desktop (the only page with the navy surfaces; `Shop/Details`, every `ShopManagement` page and every mobile Shop page use oak and glass).
- **Class / severity:** inconsistency, S3.
- **Evidence:** see "Navy theme inventory" below; `QBR/before/desktop/Shop-Index.png` against `QBR/before/desktop/Shop-Details.png` and `QBR/before/mobile/Shop-Index.png`. The navy hexes are used for two declarations (`shop.css:6`, `:720`) plus their `site.css` copy (`:47`). Measured, the navy pages are the more accessible of the two looks: nearly every navy text pair is 7:1 to 19:1 (`navy-contrast.txt`), whereas the glass pages fail in the dozens (parchment on glass at B-max is 4.34:1).
- **Proposed fix:** decided at sign-off (D-03 variant a or b); either a `[data-theme="shop"]` block of semantic tokens or the navy tokens deleted and the page moved to the common surfaces.
- **Operator question:** Y (theme decision, Q-shop-22; not a twin question).

#### A-shop-038: Availability dates formatted with a raw `ToString` on a `DateTime`
- **Page and layout:** shared partial `_ShopItemDetailsContent`, both Details entry points on desktop.
- **Class / severity:** inconsistency, S3.
- **Evidence:** `_ShopItemDetailsContent.cshtml:77`, `:83`, `:89` call `.ToString("MMM dd")` and `.ToString("MMM dd, yyyy")` directly; the project rule (`.claude/ui-guidelines.md`, "Rendering dates and times") is to use `Html.WallClock` for a floating value such as an availability window entered through `datetime-local`. Not a defect in the displayed day today; it is the pattern the rule exists to prevent.
- **Proposed fix:** `Html.WallClock(value, "date")` (the helpers themselves stay untouched).
- **Operator question:** N.

#### A-shop-039: Partial carries an inline style, an inline script and inline handlers
- **Page and layout:** shared partial `_ShopItemDetailsContent`.
- **Class / severity:** dead-code, S3.
- **Evidence:** `style="height: 250px;"` at `_ShopItemDetailsContent.cshtml:9`; `<script>` at `:249-268`; `onsubmit` at `:216` and `onchange` at `:227`; a native `confirm(` at `:262` (see A-shop-017).
- **Proposed fix:** a `.item-image-placeholder` height token, behaviour in a script file, the confirmation through the shared confirm pattern.
- **Operator question:** N.

#### A-shop-040: Icon-only action buttons carry a `title` but no accessible name on desktop (mobile has `aria-label`)
- **Page and layout:** `ShopManagement/Index`, desktop; mobile has the labels.
- **Class / severity / WCAG:** a11y (twin-drift on the label), S3, 4.1.2 (A-8).
- **Evidence:** `Index.cshtml:84-98`, `:211-258`, `:366-398` (about 16 icon-only controls, no `aria-label`); `probe-live.json` `deskMgmtActionTargets`: `title` set, `aria` null; focus probe stops 13 to 15 on `ShopManagement-Index` report an empty name; `Index.Mobile.cshtml:69-115` has `aria-label="Edit item"` and the like on the equivalent buttons.
- **Proposed fix:** `aria-label` on each icon-only control (same strings as the mobile twin); target size is already 32 by 32px on desktop.
- **Operator question:** N.

#### A-shop-041: Bulk Actions is a placeholder: three buttons that only show an alert
- **Page and layout:** `ShopManagement/Index`, desktop (trigger and modal) and mobile (modal markup only, A-shop-057).
- **Class / severity:** dead-code, S3.
- **Evidence:** `Index.cshtml:423-452` (modal with "Discontinue", "Activate", "Restock Selected Items" and the note "Bulk actions will be available in a future update.") and `:496-499` (`bulkAction` raises `alert(...)`); `onclick` handlers at `:434`, `:437`, `:440`. Nothing is selectable on the page, so there is nothing to act on.
- **Proposed fix:** keep the control and message as they are (D-04, nothing removed), move the handler to a script and the alert to the shared toast; whether to build it is a separate decision.
- **Operator question:** N.

#### A-shop-042: Inline `confirm`, inline `style` and an inline script on both management lists
- **Page and layout:** `ShopManagement/Index` desktop and mobile.
- **Class / severity:** dead-code, S3.
- **Evidence:** desktop: eight `onsubmit="return confirm(...)"` forms (`Index.cshtml:87`, `:219`, `:228`, `:237`, `:252`, `:374`, `:383`, `:393`), eight `style="display: inline;"` attributes on those forms, `<script>` at `:494-518`; mobile: four `onsubmit` confirms (`Index.Mobile.cshtml:77`, `:86`, `:103`, `:112`), five inline styles including `style="color: rgba(244, 228, 188, 0.7);"` on the empty-state icon (`:126`), `<script>` at `:204-228` (the deny-modal loader repeated from the desktop view).
- **Proposed fix:** forms use `.d-inline` or a component class; confirmations through one delegated script reading a `data-confirm` message; one shared deny-modal module.
- **Operator question:** N.

#### A-shop-043: Mobile management action buttons are 29 to 34px wide
- **Page and layout:** `ShopManagement/Index.Mobile`.
- **Class / severity / WCAG:** a11y, S3, A-5 (2.5.8 is met at 24px; the project bar is 44 by 44).
- **Evidence:** `probe-live.json` `mobileMgmtTargets`: View 34 by 44, Edit 32 by 44, Deny 29 by 44, Archive 32 by 44; `shop-management-index.mobile.css:46-49` sets `padding: 4px 8px` to fit five buttons on a 320px card; `QBR/before/mobile/ShopManagement-Index.png`.
- **Proposed fix:** `--qb-hit-min` width and height on `.btn` inside the mobile layout; let the action row wrap.
- **Operator question:** N.

#### A-shop-044: Mobile management list has no page heading
- **Page and layout:** `ShopManagement/Index.Mobile`.
- **Class / severity / WCAG:** a11y, S3, 1.3.1 and 2.4.6.
- **Evidence:** `probe-live.json` `mobileMgmtHeadings`: after the navbar brand the first heading is an item name; the page has no title (desktop has "Shop Management Dashboard"). `Index.Mobile.cshtml:15-24`.
- **Proposed fix:** a page title in the shared page-header component on both layouts (a title, not a collapsed section; nothing removed).
- **Operator question:** N (adding a heading is not a capability change; flag if the operator disagrees).

#### A-shop-045: Four `<style>` blocks of dead CSS (with `!important`, an infinite animation and `transition: all`) and a stray `</script>`
- **Page and layout:** `ShopManagement/Create`, `Create.Mobile`, `Edit`, `Edit.Mobile`.
- **Class / severity:** dead-code, S3.
- **Evidence:** `<style>` at `Create.cshtml:267-311`, `Create.Mobile.cshtml:184-220`, `Edit.cshtml:318-333`, `Edit.Mobile.cshtml:228-264`; they restyle `.rarity-*` with `!important` and re-declare `@keyframes legendary-glow`, and `Create.cshtml:304-309` adds `#item-preview { transition: all }`. No element on any of the four pages carries `rarity-` or `item-rarity` (search of the four files) and `#item-preview` does not exist, so the blocks do nothing except add an infinite animation declaration. `Edit.cshtml:334` closes `</script>` a second time after the `</style>`.
- **Proposed fix:** delete the blocks; remove the stray tag.
- **Operator question:** N.

#### A-shop-046: The same pricing script is pasted into four views, with `alert()` validation and inline handlers
- **Page and layout:** `ShopManagement/Create`, `Create.Mobile`, `Edit`, `Edit.Mobile`.
- **Class / severity:** dead-code, S3.
- **Evidence:** `<script>` blocks `Create.cshtml:164-265`, `Edit.cshtml:204-316`, `Create.Mobile.cshtml:133-182`, `Edit.Mobile.cshtml:173-226` repeat `pricingGuide`, `calculatedPrices`, `toggleAvailabilityWindow`, `updatePriceSuggestion`; `alert('Please fill in all required fields.')` and the 20-character message at `Create.cshtml:253`, `:259` and `Edit.cshtml:304`, `:310` (the fields already have `required`/server validation); inline handlers `onchange` (`Create.cshtml:59`, `:118`; `Edit.cshtml:58`, `:83`, `:147`; `Create.Mobile.cshtml:54`, `:98`; `Edit.Mobile.cshtml:57`, `:78`, `:127`) and `onclick` (`Create.cshtml:87`, `:90`, `:149`; `Edit.cshtml:117`, `:120`).
- **Proposed fix:** one `shop-item-form` module shared by all four, validation messages through the form's own validation UI.
- **Operator question:** N.

#### A-shop-047: Price helper buttons (dice and hat) are icon-only with a `title`, no `aria-label`
- **Page and layout:** `ShopManagement/Create` and `Edit`, desktop.
- **Class / severity / WCAG:** a11y, S3, 4.1.2.
- **Evidence:** `Create.cshtml:87-93`, `Edit.cshtml:117-123` (`#randomPriceBtn`, `#calculatedPriceBtn`, `title` only); the mobile views omit both buttons (deliberate by view comment, see "Twin comparison").
- **Proposed fix:** `aria-label` equal to the current titles.
- **Operator question:** N.

#### A-shop-048: Markdown editor focus indicator is clipped (shared partial, owned by the Quest area)
- **Page and layout:** `ShopManagement/Create` (and Edit), desktop; shared partial `_MarkdownEditor`.
- **Class / severity / WCAG:** a11y, S3, 2.4.7 and A-3.
- **Evidence:** focus probe stops 12 to 15 on `ShopManagement-Create`: `textarea`, `indicator: false`, `clippedBy: div`; crops `QBR/before/desktop/focus/ShopManagement-Create/12.png` to `15.png`.
- **Proposed fix:** none in the Shop phase; recorded here so the Quest and Quest Log area phase (which owns `_MarkdownEditor`) closes it for every page that includes it.
- **Area phase:** 1 Quests and Quest Log. **Operator question:** N.

#### A-shop-049: Card description and stock label exist on desktop only (twin-content)
- **Page and layout:** `Shop/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** `Index.cshtml:305-338` (description snippet cut at 120 characters; stock label with four states); `Index.Mobile.cshtml:186-195` has name, rarity chip and price only. Twin inventory `QBR/audit/shop/inv-Shop-Index.txt` (headings `h5:@item.Name` desktop only).
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-02).

#### A-shop-050: Pagination differs (twin-content)
- **Page and layout:** `Shop/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** desktop `Index.cshtml:390-429`: Previous and Next links, numbered pages, an ellipsis, "Showing x-y of n items"; mobile `Index.Mobile.cshtml:227-242`: numbered buttons and ellipsis only, no Previous, Next or count, no disabled state.
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-03).

#### A-shop-051: Empty-state variants differ (twin-content)
- **Page and layout:** `Shop/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** desktop has three states (search with "Clear search", filters with "Clear filters", empty shop) at `Index.cshtml:364-386`; mobile has one ("No items found" with no action) at `Index.Mobile.cshtml:219-224`. Verbatim copy in "Empty-state copy".
- **Proposed fix:** per the answer; the copy contract owns the wording.
- **Operator question:** Y (Q-shop-04).

#### A-shop-052: "Clear Filters" appears under different conditions (twin-content)
- **Page and layout:** `Shop/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** desktop shows Clear when rarities or a sort are active (`Index.cshtml:265`), not for a search alone; mobile includes the search term (`Index.Mobile.cshtml:60`, `:171`) and adds an "Active" badge on the Filter and Sort trigger (`:72-75`).
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-06).

#### A-shop-053: Details: availability card, item properties and D&D Beyond link exist on desktop only (twin-content)
- **Page and layout:** `Shop/Details` (partial), desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** `_ShopItemDetailsContent.cshtml:36-125` (stock badge, availability window dates, Type and Rarity), `:141-149` (View on D&D Beyond link), `:168-176` (quantity `max` and "Max: N available" or "Unlimited stock available"); `Details.Mobile.cshtml` has none of these and its quantity input has no `max` (`:36`). Twin inventory `QBR/audit/shop/inv-Shop-Details.txt` (desktop-only cards and headings).
- **Proposed fix:** per the answer; the quantity `max` is validated on the server either way.
- **Operator question:** Y (Q-shop-09 and Q-shop-11).

#### A-shop-054: Details: draft-review and sign-in messages, and the in-modal "Back to Shop" differ (twin-content)
- **Page and layout:** `Shop/Details`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** desktop shows a draft notice and an under-review alert (`_ShopItemDetailsContent.cshtml:117-123`, `:189-195`), an out-of-stock alert (`:196-201`) and a sign-in prompt (`:241-247`); mobile has plain italics for out-of-stock and "not currently available" (`Details.Mobile.cshtml:44-49`), no sign-in branch (the purchase form renders unconditionally), and a "Back to Shop" button that also appears inside the modal (A-shop-005).
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-10).

#### A-shop-055: Management list structure and columns differ (twin-content)
- **Page and layout:** `ShopManagement/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** desktop has three sections (Awaiting Review, My Items, All Other Published; `Index.cshtml:38-416`) with columns Type, Stock, Created By, a description teaser and a denial reason with date (`:138-152`); mobile merges the three lists into one de-duplicated card list ordered Draft first (`Index.Mobile.cshtml:10-17`) with rarity, name, price and status only.
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-12 and Q-shop-13).

#### A-shop-056: Management action sets differ (twin-content)
- **Page and layout:** `ShopManagement/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** desktop Denied rows get a separate "Edit and Resubmit" button (`Index.cshtml:244-249`); desktop review-section rows show Publish and Deny to everyone who can open the page (`:82-99`) whereas mobile shows Publish only to Admin and only on Draft (`Index.Mobile.cshtml:101-110`); desktop shows Delete on another DM's Draft (`Index.cshtml:390-399`), mobile does not (`Index.Mobile.cshtml:111-120`).
- **Proposed fix:** per the answer; any authorization difference is confirmed against the controller policies before the area phase touches it.
- **Operator question:** Y (Q-shop-15).

#### A-shop-057: Bulk Actions modal exists on mobile with no trigger (twin-content)
- **Page and layout:** `ShopManagement/Index`, mobile versus desktop.
- **Class / severity:** twin-content, S3.
- **Evidence:** `Index.Mobile.cshtml:134-163` carries the full modal and `bulkAction`; no element opens it (`probe-live.json`: `mobileBulkTrigger: false`), while desktop has the button (`Index.cshtml:33-35`).
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-14).

#### A-shop-058: Management empty-state copy differs (twin-content)
- **Page and layout:** `ShopManagement/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S3.
- **Evidence:** desktop `Index.cshtml:269-276` and `:410-414`, mobile `Index.Mobile.cshtml:124-130`; verbatim in "Empty-state copy". The desktop pair also has an action ("Create First Item"), mobile has no button (the top "Add New Item" button is always present on both).
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-17).

### S4: polish

#### A-shop-059: Quest Items category reuses the Magic Items icon
- **Page and layout:** `Shop/Index`, both layouts.
- **Class / severity:** inconsistency, S4.
- **Evidence:** `Index.cshtml:198` and `Index.Mobile.cshtml:104` both use `fa-magic` for "Quest Items", the icon of "Magic Items" (`:194`, `:100`).
- **Proposed fix:** a distinct icon (for example the scroll used for the type on the Details page, `fa-scroll`).
- **Operator question:** N.

#### A-shop-060: Buy button copy differs (twin-content)
- **Page and layout:** `Shop/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S4.
- **Evidence:** desktop "Purchase" and "Login to Purchase" (`Index.cshtml:348`, `:354`); mobile "Buy" and "Login to Buy" (`Index.Mobile.cshtml:203`, `:210`). The Details view says "Purchase Item" on both.
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-07).

#### A-shop-061: Raw enum names shown to the reader
- **Page and layout:** `Shop/Details` (both layouts and the modal), shared partial.
- **Class / severity:** inconsistency, S4.
- **Evidence:** `Details.cshtml:18`, `:34` and `Details.Mobile.cshtml:14` print `@Model.Type` (renders "MagicItem", "QuestItems"); `_ShopItemDetailsContent.cshtml:109` the same for Type; the management lists convert them ("Magic Item", "Quest Item"; `ShopManagement/Index.cshtml:68`). `QBR/audit/shop/modal-desktop.png` shows "MagicItem".
- **Proposed fix:** one display-name helper for item type used by every Shop view.
- **Operator question:** N.

#### A-shop-062: Status labels differ between management twins (twin-content)
- **Page and layout:** `ShopManagement/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S4.
- **Evidence:** desktop "Draft" and "Available" (`Index.cshtml:193-199`), mobile "Pending Review" and "Active" (`Index.Mobile.cshtml:50`, `:53`); the Edit views say "Available" and "This item is awaiting DM approval" on both.
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-16).

#### A-shop-063: "View Shop" uses a different outline colour on each layout
- **Page and layout:** `ShopManagement/Index`, desktop versus mobile.
- **Class / severity:** twin-drift, S4.
- **Evidence:** `btn-outline-primary` (`Index.cshtml:30`) against `btn-outline-secondary` (`Index.Mobile.cshtml:24-26`); both fail contrast (A-shop-006).
- **Proposed fix:** one filled `.btn-secondary` on both.
- **Operator question:** N.

#### A-shop-064: Edit price hint differs in its first line (twin-content)
- **Page and layout:** `ShopManagement/Edit`, desktop versus mobile.
- **Class / severity:** twin-content, S4.
- **Evidence:** desktop initial hint "Pricing suggestion will appear here" (`Edit.cshtml:125`), mobile "Select a rarity to see pricing suggestions." (`Edit.Mobile.cshtml:108`); the script replaces both once a rarity is set.
- **Proposed fix:** per the answer (copy contract owns it).
- **Operator question:** Y (Q-shop-21).

#### A-shop-065: The floating merchant exists on desktop only (twin-content)
- **Page and layout:** `Shop/Index`, desktop versus mobile.
- **Class / severity:** twin-content, S4.
- **Evidence:** `Index.cshtml:431-434` and `:477-535` (20 canned lines and a toast); `Index.Mobile.cshtml` has neither. The control is not keyboard reachable (A-shop-034).
- **Proposed fix:** per the answer.
- **Operator question:** Y (Q-shop-05).


## Navy theme inventory

Every surface, selector and colour of the dark-navy "Witcher 3" theme, for the Shop (a) versus (b) mockups and the D-03 decision. The theme exists on **desktop `Shop/Index` only**; it is written twice (`shop.css`, which wins, and a copy in `site.css`). `Shop/Details`, all `ShopManagement` pages and every mobile Shop page use the oak board and glass instead.

| Surface | Selector | Colour (literal) | `shop.css` | `site.css` copy |
|---------|----------|------------------|------------|-----------------|
| Page field behind the whole shop (the only use of the three navy hexes as a surface) | `.shop-container` | `linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)`, `min-height: 100vh`, padding 2rem 0 | 5-9 | 46-50 |
| Sort dropdown options | `.filter-sort-select option` | `background: #1a1a2e` (fourth navy use) | 719-722 | none |
| Header plate | `.shop-header` | `rgba(0,0,0,.8)` fill, 2px `#ffc107` border, glow `rgba(255,193,7,.3)` | 11-19 | 52-60 |
| Header title | `.shop-header h1` | `#ffc107`, Cinzel, shadow `rgba(0,0,0,.8)` | 21-27 | 62-68 |
| Category tabs | `.category-btn`, `:hover`, `.active` | `rgba(0,0,0,.7)` fill, `#444` border, `#fff` label; active `#ffc107` fill, `#000` label | 33-52 | 74-93 |
| Inventory field | `.shop-inventory` | `rgba(0,0,0,.6)` fill, 2px `#333` border | 54-60 | 95-101 |
| Item card | `.item-card`, `:hover`, `::before` | `rgba(20,20,30,.9)` fill, 2px `#444` border; hover `#ffc107` border and glow; sweep `rgba(255,255,255,.1)` | 68-101 | 109-145 |
| Item name and description | `.item-name`, `.item-description` | `#F4E4BC !important`; description `rgba(244,228,188,.8) !important` | 110-122, 165-171 | none |
| Price | `.item-price` | `#ffc107` | 182-186 | none |
| Stock label | `.item-stock` (+ variants) | `rgba(244,228,188,.7) !important`; dead variants `#198754`, `#dc3545`, `#6c757d` | 188-204 | none |
| Purchase button | `.purchase-btn` | `linear-gradient(45deg, #28a745, #20c997)`, white label; hover `#218838`/`#1ea085`; disabled `#6c757d` | 206-230 | 247-271 |
| Filter row | `.shop-filter-row` | `rgba(0,0,0,.5)` fill, `#444` border | 656-666 | none |
| Filter labels and controls | `.filter-label`, `.filter-sort-select`, `.filter-apply-btn`, `.filter-clear-btn` | label `#F4E4BC`; select `rgba(0,0,0,.7)`, `#444`; button `rgba(0,0,0,.7)`, `#444`, hover `#ffc107`; clear `rgba(244,228,188,.7)`, hover `#dc3545` | 668-754 | none |
| Rarity checkbox accent | `.filter-check-label input` | `accent-color: #ffc107` | 690-695 | none |
| Pagination | `.shop-pagination .page-link.shop-page-link`, `.active`, `.disabled`, count | `rgba(0,0,0,.7)`, `#444`, `#fff`; active `#ffc107`/`#000`; disabled `rgba(0,0,0,.4)`, `#333`, `rgba(244,228,188,.3)` | 760-796 | none |
| Purchase panel | `.purchase-history-panel` (+ header, body, scrollbar) | `rgba(15,15,25,.95)` fill, 3px `#ffc107` right border, 2px `rgba(139,69,19,.9)` top; header `rgba(0,0,0,.8)`; scrollbar `#ffc107` | 378-431 | 1202-1256 |
| Purchase rows | `.purchase-item`, `.sold-returned` | `rgba(0,0,0,.4)`, `#444`; hover `rgba(255,193,7,.1)`; returned `opacity: .6`, `grayscale(50%)` | 433-501 | 1257-1305, 1562 |
| Return and Sell buttons | `.return-sell-buttons .btn-success`, `.btn-warning` | gradients `#28a745`/`#20c997` and `#ffc107`/`#e0a800` (invisible, A-shop-002) | none | 1360-1393 |
| Floating merchant | `.shop-merchant` | `linear-gradient(45deg, #ffc107, #fd7e14)`, 3px `#fff` border, `#000` icon | 239-262 | 280-305 |
| Item-details modal chrome | `#itemDetailsModal .modal-content.bg-dark` | Bootstrap `rgb(33,37,41)` (not navy; inline utility classes in `Index.cshtml:440`) | n/a | n/a |
| Dark modal restyle (management deny modal) | `#denyModal ...` | `#fff`, `#ffc107`, `#444`, `rgba(255,255,255,.05)` | 535-589 | none |
| Dead dark modal restyle | `#addCharacterModal ...` | `#fff`, `#0dcaf0`, `#1a1a1a` | 591-650 | none |

**Measured (computed, WCAG formula, translucent layers composited; `QBR/audit/shop/navy-contrast.txt`):** over the three navy stops the card surface is about `#131319`. Parchment name 14.6:1, description 9.6:1, muted stock text 7.6:1, gold price 11.3:1, gold header title 12.4:1, white category label 19.8:1, filter label 15.0:1, purchase-panel text 15.0:1 and muted 7.7:1; the only pairs under 4.5:1 are the dead stock variants (3.9 to 4.1:1) and the disabled pagination label (2.35:1, a disabled control). Borders `#444` on the card are 1.89:1 (decorative; the interactive controls carry a label and a fill). Gold border on the header plate 12.4:1. The failures that the BEFORE report records on this page come from outside the navy field: the purchase button (A-shop-011), the legendary chip (A-shop-012), the empty-state and panel text (A-shop-001, A-shop-003).

**For the decision.** Variant (a) keeps one page that is already compliant almost everywhere and costs a `[data-theme="shop"]` token block that would have to define surface, fg, accent and `--qb-board`; the navy hexes would become tokens used by that block only (D-17). Variant (b) deletes the navy hexes and gradient and moves the page onto glass or paper surfaces, which is the path every other Shop page and the whole mobile Shop already take; it inherits the glass pairs that fail today (parchment on glass 4.34:1 at B-max) until the foundation tokens fix them. Mobile and the other desktop Shop pages are unaffected by either choice.

## Stylesheet and markup inventory

Counts are measured from the files at the baseline commit.

| Stylesheet | Hex literals | `rgb()`/`rgba()` literals | `!important` | Bootstrap component selectors re-themed | Cascade-fight (re-colours a Bootstrap `!important` utility) | `transition: all` | Infinite animations | `backdrop-filter` (repeated item) |
|------------|--------------|----------------------------|--------------|------------------------------------------|---------------------------------------------------------------|-------------------|---------------------|--------------------------------------|
| `shop.css` (796 lines, global load) | 107 | 76 | 70 | 25 selectors (`.page-link`, `#denyModal` and `#addCharacterModal` form and alert rules, `.btn` look-alikes) | 4 (`:119`, `:468`, `:473`, `:482`) | 9 | 6 | 1 (single panel, `:391`) |
| `shop.mobile.css` (149) | 10 | 20 | 12 | 2 (`.badge`, offcanvas label) | 1 (`.text-muted`, `:101-105`) | 0 | 0 | 3 (one per item, `:14`) |
| `shop-details.mobile.css` (31) | 1 | 7 | 4 | 1 (`.badge`) | 0 | 0 | 0 | 1 (single card, `:7`) |
| `shop-management-index.mobile.css` (71) | 2 | 16 | 7 | 2 (`.btn-sm`, `.badge`) | 0 | 0 | 0 | 3 (one per item, `:17`) |
| `shop-management-create.mobile.css` (41) | 2 | 9 | 5 | 2 (`.form-label`, `.badge`) | 0 | 0 | 0 | 1 (single card, `:7`) |
| `shop-management-edit.mobile.css` (46) | 2 | 9 | 5 | 3 (`.form-label`, `.badge`, `.alert-info`) | 0 | 0 | 0 | 1 (single card, `:7`) |

Also in `site.css`: about 50 Shop selectors repeated from `shop.css` (A-shop-031). None of the six files has a `prefers-reduced-motion` block or a `@media` rule for the Shop other than `shop.css:510` and `:520` (panel width); the six files contain no other `@media` block (the mobile files only mention it in a comment).

**Views (per view: inline `style=""` attributes, `<style>`, inline `<script>`, inline event handlers, native `confirm(` or `alert(`):**

| View | `style=""` | `<style>` | `<script>` | Inline handlers | `confirm(` / `alert(` |
|------|-----------|-----------|------------|-----------------|-----------------------|
| `Shop/Index` | 1 (`:276`) | 0 | 1 (`:453-537`) | 3 (`onclick` `:346`, `:353`, `:432`) | 0 |
| `Shop/Index.Mobile` | 0 | 0 | 1 (`:260-283`) | 2 (`:201`, `:209`) | 0 |
| `Shop/Details`, `Details.Mobile` | 0 | 0 | 0 | 0 | 0 |
| `_ShopItemDetailsContent` | 1 (`:9`) | 0 | 1 (`:249-268`) | 2 (`:216`, `:227`) | 1 `confirm(` (`:262`) |
| `ShopManagement/Index` | 8 | 0 | 1 (`:494-518`) | 11 (8 `onsubmit` confirms, 3 `onclick`) | 8 `confirm(` strings, 1 `alert(` (`:498`) |
| `ShopManagement/Index.Mobile` | 5 | 0 | 1 (`:205-228`) | 7 (4 `onsubmit`, 3 `onclick`) | 4 `confirm(` strings, 1 `alert(` (`:208`) |
| `ShopManagement/Create` | 0 | 1 (`:267-311`) | 1 (`:164-265`) | 5 | 2 `alert(` |
| `ShopManagement/Create.Mobile` | 0 | 1 (`:184-220`) | 1 (`:133-182`) | 2 | 0 (validation by native `required`) |
| `ShopManagement/Edit` | 0 | 1 (`:318-333`) | 1 (`:204-316`) | 5 | 2 `alert(` |
| `ShopManagement/Edit.Mobile` | 0 | 1 (`:228-264`) | 1 (`:173-226`) | 3 | 0 |

The four views that carry `<style>` match the known item (A-shop-045).

**A-11 items that apply.** 1.4.12 text spacing: probed on all ten Shop captures (five pages, both layouts) with line-height 1.5, letter-spacing .12em, word-spacing .16em and paragraph spacing 2em injected (`QBR/audit/shop/probe-spacing.json`): no horizontal overflow and no clipped text on any; the mobile item name uses a two-line clamp (`shop.mobile.css:34-37`), which is a deliberate truncation rather than a clipping failure. 2.5.7 dragging: no drag interaction in the area. 3.3.7 redundant entry: the Create and Edit forms ask for nothing twice. 3.3.8 accessible authentication: no authentication in the area. Reflow (A-6): no horizontal overflow at desktop 320 or mobile 390 on any Shop row (BEFORE report); the Shop page below 992px loses the purchase panel (A-shop-015). Target size (A-5): mobile Shop Index and Create and Edit targets are 44px high; the exceptions are A-shop-043 and the 14px filter checkboxes on desktop, which sit inside labels (the label is the target).

## Twin comparison

Inventories extracted from both files of each pair with `QBR/audit/shop/inv.mjs` (`asp-for` and `name=` fields, `asp-action` and `href` links, `type="submit"` buttons, `h1` to `h6` and card blocks, `BoardPages` conditions). No Shop view contains a `BoardPages` condition, so the board-page condition set is empty on both sides of every pair (`grep` of the area for `BoardPages` finds nothing). Raw output: `inv-Shop-Index.txt`, `inv-Shop-Details.txt`, `inv-ShopManagement-Index.txt`, `inv-ShopManagement-Create.txt`, `inv-ShopManagement-Edit.txt`. No deliberate differences are pre-recorded for this area by the audit contract; the evidence of intent that does exist is cited per row and still goes to the operator unless stated.

| Pair | Class | Inventory behind it |
|------|-------|---------------------|
| Shop/Index | **content-differs** | Shared: filter fields (`rarity` checkboxes, `sort`, `search`, hidden `page` and `type`), four category links, `Index@Shop`, `Details@Shop`, `Login@Account`, the modal. Desktop only: purchase panel with `Sell@Shop` forms (hidden `id` and `quantity`), card description and stock label, `h1` and `h5` and three `h3` empty states and `h6` "My Transactions", Previous and Next links with "Showing x-y of n items", the merchant. Mobile only: offcanvas (`shopFilterOffcanvas`, `sortSelectMobile`, `searchMobile`), "Active" badge on the trigger, `h5` "Filter and Sort" and "No items found", glass header card, `shop-item-card-mobile`. The only recorded intent is the test pin on the panel (`MobileViewsTests.cs:716-733`). Operator questions Q-shop-01 to Q-shop-07. |
| Shop/Details (including `_ShopItemDetailsContent`) | **content-differs** | Desktop (page plus partial): `name=quantity` (Purchase and Sell, two inputs), `Purchase@Shop`, `SellToShop@Shop`, links `/Shop`, the D&D Beyond reference link and `/Account/Login`, `h1`/`h2` item name, `h6` Availability, Item Properties, Description, Sell to Shop, three `modern-card` blocks, availability dates, draft and out-of-stock alerts. Mobile: `name=Quantity` (one input, no `max`), `Purchase@Shop`, link `/Shop`, `h5` item name, one glass card. Operator questions Q-shop-08 to Q-shop-11. |
| ShopManagement/Index | **content-differs** | Fields, hrefs and action URLs are identical sets (`Create`, `/Shop`, `Details`, `Edit`, `Publish`, `Archive`, `Reopen`, `Delete`, `Deny`). Desktop only: three sections with `h4`, Bulk Actions trigger, Type, Stock and Created By columns, teaser, denial reason and date, "Edit and Resubmit", Delete on another DM's Draft. Mobile only: one merged list, no trigger for its Bulk Actions modal, `aria-label` on every icon button (desktop has none; A-shop-040). Operator questions Q-shop-12 to Q-shop-17. |
| ShopManagement/Create | **content-differs** (everything else layout-only) | Ten fields identical, one form action, one cancel link, one submit. Desktop only: Preview button (`previewItem`, undefined), the two price helper buttons (`#randomPriceBtn`, `#calculatedPriceBtn`), two-column rows in `modern-card`. Mobile: single column in a glass card. The price helper buttons are marked as intentionally absent by the mobile view's own comment "Price (no input-group, no buttons)" (`Create.Mobile.cshtml:73`); recorded below as deliberate by comment. Operator question Q-shop-18 (Preview). |
| ShopManagement/Edit | **content-differs** (everything else layout-only) | Eleven fields identical, one form action, one cancel link, one submit. Desktop only: "View Details" link (dead, A-shop-026), the two price helper buttons (deliberate by the comment at `Edit.Mobile.cshtml:103`), `h2` page title in the card header. First-line price hint copy differs (A-shop-064). Operator questions Q-shop-19 and Q-shop-21. |

**Deliberate by view comment (recorded, not asked).** The absence of the two price helper buttons on `Create.Mobile` and `Edit.Mobile` (both views carry the comment "Price (no input-group, no buttons)"). They are listed here so the area phase does not add them back; the operator may overrule at sign-off.

## Operator questions

Standard wording, two answers only (D-14). Removal is never offered.

| Q | Finding | Question |
|---|---------|----------|
| Q-shop-01 | A-shop-028 | Shop/Index: the desktop view has the My Transactions panel (purchase history with Return and Sell) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-02 | A-shop-049 | Shop/Index: the desktop view has a description snippet and a stock label on each item card and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-03 | A-shop-050 | Shop/Index: the desktop view has Previous and Next links and a "Showing x-y of n items" line and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-04 | A-shop-051 | Shop/Index: the desktop view has three separate empty states (search, filters, empty shop) with Clear buttons and the mobile view has one generic empty state. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-05 | A-shop-065 | Shop/Index: the desktop view has the floating Mystical Merchant and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-06 | A-shop-052 | Shop/Index: the mobile view shows Clear Filters (and an "Active" badge) when a search is active and the desktop view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-07 | A-shop-060 | Shop/Index: the desktop view says "Purchase" and "Login to Purchase" and the mobile view says "Buy" and "Login to Buy". Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-08 | A-shop-029 | Shop/Details: the desktop view has Sell to Shop (quantity and "You will receive") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-09 | A-shop-053 | Shop/Details: the desktop view has the Availability card (stock and availability dates), Item Properties and the View on D&D Beyond link and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-10 | A-shop-054 | Shop/Details: the desktop view has a draft-review notice, an out-of-stock alert and a sign-in prompt and the mobile view has plain short text and no sign-in branch; the mobile view also repeats Back to Shop inside the modal. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-11 | A-shop-053 | Shop/Details: the desktop view limits the Purchase quantity to the stock (`max`) and shows "Max: N available" and the mobile view does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-12 | A-shop-055 | ShopManagement/Index: the desktop view has three sections (Awaiting Review, My Items, All Other Published) and the mobile view has one merged list. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-13 | A-shop-055 | ShopManagement/Index: the desktop view has Type, Stock and Created By, a description teaser and the denial reason with date and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-14 | A-shop-057 | ShopManagement/Index: the desktop view has a Bulk Actions button and the mobile view has the modal but no button. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-15 | A-shop-056 | ShopManagement/Index: the desktop view has "Edit and Resubmit" for Denied items, Publish and Deny on the review list for every DM, and Delete on another DM's Draft; the mobile view limits Publish to Admin on Draft and has none of the others. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-16 | A-shop-062 | ShopManagement/Index: the desktop view labels items "Draft" and "Available" and the mobile view labels them "Pending Review" and "Active". Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-17 | A-shop-058 | ShopManagement/Index: the desktop view has the empty states "No Items Yet" (with a Create First Item button) and "No Other Items" and the mobile view has "No items yet" without a button. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-18 | A-shop-027 | ShopManagement/Create: the desktop view has a Preview button (currently non-functional) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-19 | A-shop-026 | ShopManagement/Edit: the desktop view has a View Details button (currently a dead link) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-21 | A-shop-064 | ShopManagement/Edit: the desktop view starts the price hint with "Pricing suggestion will appear here" and the mobile view with "Select a rarity to see pricing suggestions." Is this deliberate? [Deliberate, record it] [Add it to the other view] |
| Q-shop-22 | A-shop-037 | Shop theme (D-03, decided from the mockups, not a twin question): variant (a) keep the navy theme as a `[data-theme="shop"]` block, or variant (b) retire it. |
| (R-1, R-2) | A-shop-015, relayouts | Relayout sign-off, see "Relayout proposals". |

Q-shop-20 is intentionally unused (the price helper buttons are recorded as deliberate by view comment above). The seeded footer-text and brand-text questions belong to the layout audit and are not repeated here.

## Merge proposals (D-13)

**None.** A pair qualifies only if it is `identical` or `layout-only`, has no deliberate difference and its mobile differences are CSS-only. Checked against the precedent views `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details` (already merged: one view, CSS differences only):

| Pair | Why it does not qualify |
|------|-------------------------|
| Shop/Index | content-differs (A-shop-028 and A-shop-049 to A-shop-052); markup differs (offcanvas against inline filter row) |
| Shop/Details | content-differs (Sell, availability, properties); the mobile view is also the modal body |
| ShopManagement/Index | content-differs (sections, columns, trigger); table against cards |
| ShopManagement/Create | has a deliberate difference (price helper buttons) and a desktop-only Preview; markup differs (glass card against `modern-card` and row grid) |
| ShopManagement/Edit | has a deliberate difference (price helper buttons) and a desktop-only View Details; markup differs |

Create and Edit are the closest to `layout-only`. If the operator answers Q-shop-18 and Q-shop-19 as "add it" and the price helper buttons were added to mobile, each pair would reduce to a layout-only difference and could then be re-proposed with the CSS-loading change in both layouts (the `shop-management-create.mobile.css` and `shop-management-edit.mobile.css` files folded into the shared stylesheet) and the `MobileViewsTests` facts that would change (the tests that assert each mobile view links its own stylesheet, for example `GetMobilePage_ShopManagementIndex_ReturnsSuccessAndMobileLayout` and the Create and Edit equivalents in `MobileViewsTests.cs`). Costly to reverse; no proposal is made now.

## Relayout proposals (D-04)

Both keep every field, link, button and action, keep each control's type and collapse no section behind a toggle. Both need the operator's sign-off before the Shop phase builds them. BEFORE screenshots are listed; AFTER screenshots are produced in the area phase.

**R-1: Purchase history from a fixed side panel to an in-flow section (desktop, `Shop/Index`).**
- Today: a fixed 280px panel on the left (250px between 992 and 1200px) that pushes the content right by 280px and disappears below 992px with no way back (A-shop-015); the 24-hour Return lives only there.
- Proposed: the same My Transactions block (same rows, same Return and Sell forms, same "Showing latest 15 purchases") as an ordinary section of the page flow, placed under the item grid (a reorder of two blocks, nothing hidden), so it is reachable at every width and the grid regains the 280px.
- BEFORE: `QBR/before/desktop/Shop-Index.png`, `QBR/before/desktop/Shop-Index--empty-category.png`. Feeds A-shop-002, A-shop-015, A-shop-036.

**R-2: One filter section instead of two stacked blocks (desktop, `Shop/Index`).**
- Today: the rarity and sort row and the search bar are two separate bars (`Index.cshtml:206-284`), tied together only by the `form="shop-filter-form"` attribute.
- Proposed: one filter card containing the rarity checkboxes, sort select, search input and the Apply Filters and Search buttons, in a sensible order for a phone-width window (a merge of two adjacent blocks); the form and all fields stay.
- BEFORE: `QBR/before/desktop/Shop-Index.png`.

## Empty-state copy

Verbatim, per page and layout (heading / line / action). A difference between twins is a twin-content finding.

| Page | Layout | Condition | Copy |
|------|--------|-----------|------|
| Shop/Index | desktop | no items, no filters | "The shop is currently empty" / "Check back later for new items, or contact a Dungeon Master about stocking the shop." / no action |
| Shop/Index | desktop | filters active | "No items match your filters" / "Try removing some rarity filters or changing the sort order." / "Clear filters" |
| Shop/Index | desktop | search active | "No items match your search" / "Try a different name or adjust your filters." / "Clear search" |
| Shop/Index | desktop | purchase panel, no purchases | "No purchases yet" / "Your purchase history will appear here" |
| Shop/Index | mobile | any (no variants) | "No items found" / "Try adjusting your filters or search query." / no action |
| ShopManagement/Index | desktop | no own items | "No Items Yet" / "You haven't created any shop items yet. Start by adding your first item!" / "Create First Item" |
| ShopManagement/Index | desktop | no other published items | "No Other Items" / "No other DMs have published items yet." / no action |
| ShopManagement/Index | mobile | no items | "No items yet" / "Add the first item to your shop." / no action |
| Shop/Details | desktop (partial) | out of stock | "This item is currently out of stock." |
| Shop/Details | desktop (partial) | draft | "This item is currently under review and not available for purchase." and "This item is currently under review by the Dungeon Masters and is not yet available for purchase." |
| Shop/Details | mobile | out of stock / not available | "Out of stock." / "This item is not currently available for purchase." |
| ShopManagement/Create, Edit | both | none | no empty state |

Observed on the Audit Board with the seeded items: Shop (3 items), empty Quest Items category, and the management lists (4 own items, no other items) all render the strings above; the Draft item appears only in the management views.

## Area-phase scope notes

**Owned by the Shop and Shop management phase (area phase 4):**
- Views (10 files): `Views/Shop/{Index,Details}` and `Views/ShopManagement/{Index,Create,Edit}`, each with its `.Mobile` twin.
- Partial: `Views/Shared/_ShopItemDetailsContent.cshtml`.
- Stylesheets (6): `shop.css`, `shop.mobile.css`, `shop-details.mobile.css`, `shop-management-index.mobile.css`, `shop-management-create.mobile.css`, `shop-management-edit.mobile.css`, plus the Shop rule families in `site.css` (lines 46-310, 1202-1393, 1460, 1562) which the phase deletes as duplicates, and the `shop.css` line in `_Layout.cshtml:25`.
- The static guard tests that pin these views: `MobileViewsTests.cs` (Shop and ShopManagement entries, including the purchase-panel omission at `:716-733` and the management mobile layout test at `:1123`), `BoardPageStaticGuardTests.cs` (controller file list), `BoardPageGateMatrixTests.cs` and the Shop controller integration tests. Each is rewritten to pin the same intent, none deleted.

**Not owned here, handed to another area phase:**
- `_MarkdownEditor` (the editor focus indicator, A-shop-048, and the required asterisk colour inside A-shop-024) and `Views/Quest/_QuestFormScripts.cshtml` (loaded by Create and Edit): area phase 1 Quests and Quest Log.
- `modern-card.css` and `site.css` `.text-muted` rules that recolour Bootstrap utilities (A-shop-007, A-shop-018): the foundation retires them.

**Counts (S1/S2 etc.):** 65 findings. S1: 10 (A-shop-001 to 010, one of which, A-shop-010, is the functional Create failure that is handed off, not fixed in this phase). S2: 19 (A-shop-011 to 029). S3: 29 (A-shop-030 to 058). S4: 7 (A-shop-059 to 065). Operator questions: 21 numbered (Q-shop-01 to 19, 21, 22) plus the relayout sign-off.

**Adjustments to the draft cut:**
- The draft cut counts "Shop 4, ShopManagement 6, `_ShopItemDetailsContent` (11)". Measured: 4 Shop views (Index, Details, each with a twin) plus 6 ShopManagement views plus one partial = 11 files, as drafted. No page belongs better to another area.
- The phase carries more than the file count suggests: the six stylesheets (1,134 lines) and a second copy of the Shop rules in `site.css`, ten infinite-animation declarations (not six; four are dead copies inside the Create and Edit `<style>` blocks), two behaviours that fail today (the sell confirmation in the modal, A-shop-017; the hidden Return and Sell buttons, A-shop-002) and the theme decision. It stays one phase, ordered after the foundation and after the D-03 decision (variant a adds a token block to this phase; variant b removes the navy rules).
- The Shop creation failure (A-shop-010) is outside the phase and should be fixed first or alongside, because the area phase's own after-capture of Create needs a working form (the fixtures were seeded directly instead).
- Fixtures: the seeded items (5441 to 5444) suffice for the area phase; Return and Sell remain unverifiable without a purchase row, which the area phase should create through the Purchase form on the audit board.

