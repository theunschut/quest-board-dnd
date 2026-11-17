---
phase: "99"
slug: "shop-and-shop-management-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-15"
---

# Phase 99 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `99-RESEARCH.md` § "Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 + FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF Core InMemory (no database) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` |
| **Quick run command (integration)** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~ShopConversionGuardTests\|FullyQualifiedName~ShopRenderTests\|FullyQualifiedName~ShopControllerIntegrationTests\|FullyQualifiedName~ShopManagementControllerIntegrationTests\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~MobileCssTests\|FullyQualifiedName~BoardPageGateMatrixTests\|FullyQualifiedName~BoardPageStaticGuardTests\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~KeyboardScriptGuardTests\|FullyQualifiedName~QuestsConversionGuardTests\|FullyQualifiedName~AccountPagesRenderTests"` |
| **Quick run command (unit)** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~ShopService\|FullyQualifiedName~UserTransactionRepository\|FullyQualifiedName~AmbientClockSeamTests\|FullyQualifiedName~WallClock"` |
| **Full suite command** | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (per project avoids the CLR flake). SQL-backed tests need `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and SQL Server at `localhost:1433`; run once at the end only, and only on the operator's yes |
| **Browser checks** | Scratch Playwright reused from `~/qb-ui-review/phase-94/tools`, under `~/qb-ui-review/phase-99/` (desktop 1440x900; phone `devices['Pixel 7']` with touch) |
| **Estimated runtime** | ~20 s quick (after build), ~2-3 min full |

Notes: re-run once on a transient `0x80131506`; never set GC env vars; stop the running app before `dotnet build/test`; no watch mode.

---

## Sampling Rate

- **After every task commit:** the quick filters narrowed to the slice's files, plus the scratch probe on the touched pages under both User-Agents.
- **After every plan wave:** full suite, per project.
- **Before `/gsd-verify-work`:** full suite green, `dotnet build` clean, AFTER sweep shows no new contrast failure and no hidden control against BEFORE; `git diff --stat` shows no change to `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`; nothing writes to Events or Quests.
- **Max feedback latency:** 120 seconds.

---

## Per-Task Verification Map

Requirement-level map (task IDs are filled in by the plans; each plan's `<verify>` cites one of these commands):

| Req | Behavior | Test Type | Automated Command | File Exists? | Status |
|-----|----------|-----------|-------------------|-------------|--------|
| D-01/D-02 | Merchant is a `<button>` with name, no `onclick`, module builds no markup from strings, toast `role="status"`, quotes unchanged (20) | static + integration | `~ShopConversionGuardTests` (module scan, 20-quote pin); `~ShopRenderTests` (button, name); `~KeyboardScriptGuardTests` | ✅ exists | ✅ green |
| D-03 | Preview button has no inline handler; dialog markup uses `.modal`; no new endpoint; `#item-preview` gone | static + integration | `~ShopConversionGuardTests`; `~ShopRenderTests` | ✅ exists | ✅ green |
| D-04 | Edit "View Details" href is `/Shop/Details/{id}`; GET returns 200 for Draft and Denied | integration | `~ShopRenderTests` (Edit link; Details status matrix) | ✅ exists | ✅ green |
| D-05 | Return/Sell forms visible (no hidden class), carry a token, POST succeeds; sell form uses `data-sell-form`, no `onsubmit`; fixture item with apostrophe | integration + static | `~ShopRenderTests` (post Sell, post Return); `~ShopConversionGuardTests` (every post form has a token); `~ShopFormPostEffectTests` (posts each form as rendered, reads stock and transaction) | ✅ exists | ✅ green |
| D-06 | One status vocabulary: "Pending Review" for Draft, "Available" for Published on both lists and both Edit views | integration | `~ShopRenderTests`; `~MobileViewsTests` (phone) | ✅ exists | ✅ green |
| D-07/D-08/D-09 | Emblem tile has no `style=`/`aspect-ratio`/`object-fit`; no `infinite` anywhere except one capped merchant rule; no `transition: all`; chips use token pairs | static | `~ShopConversionGuardTests` (animation scanner with self-test, whole-picture scanner with self-test, literal/important scans) | ✅ exists | ✅ green |
| D-10 / Q-56 | Phone renders the history section with Return and Sell; no `purchase-history-panel` on either layout | integration | `~MobileViewsTests` (rewritten), `~ShopRenderTests` | ✅ rewrite | ✅ green |
| Q-58/59/61/63-66/71/72/75 | Phone Previous/Next and count, three empty states with Clear, desktop Clear and Active on search, phone Details gains Sell, Availability, Properties, D&D Beyond, notices, `max`; "No Items Yet"; price hint | integration | `~ShopRenderTests` (both UAs) | ✅ exists | ✅ green |
| Theme | Both layouts emit `data-theme` on `<html>`; all ten views set it; token contrast passes | static + existing | `~ShopConversionGuardTests`; `~UiFoundationLayerGuardTests` | ✅ existing + ✅ new fact | ✅ green |
| DoC-1..3 | No inline code; no hex, rgb or `!important`; sheets in `@layer pages`; linked from `@section Styles`; shop sheets not in a layout list | static | `~ShopConversionGuardTests`; `~UiFoundationLayerGuardTests` | ✅ exists | ✅ green |
| DoC-4 | Twin answers pinned | integration | `~MobileViewsTests`, `~ShopRenderTests` | ✅ exists | ✅ green |
| DoC-5/DoC-7 | Accessibility bar and phone checks | Playwright | scratch `sweep.mjs`, `contrast.mjs`, `a11y-*` | manual-run | manual-only |
| DoC-8 | Guards rewritten, none deleted | process | test-count comparison against `tests-before.txt` | process | process (manual) |
| DoC-9 | Helpers untouched; no write to Events or Quests; feed files untouched | unit + diff | `~WallClockMarkupTests`, `~LocalTimeMarkupTests`; `git diff --stat` | ✅ | ✅ green |
| A-shop-038 | Availability dates through `Html.WallClock`; no raw `ToString` on a DateTime in the area | static | `~ShopConversionGuardTests` (clone of Phase 98's raw date scan) | ✅ exists | ✅ green |
| Legacy sheet | `legacy-pages.css` keeps only the shared container rule; still linked by desktop and picker layouts | static | `~QuestsConversionGuardTests`, `~UiFoundationLayerGuardTests`, `~MobileCssTests`, `~AccountPagesRenderTests` | ✅ existing | ✅ green |
| Markdown hosts | Shop Index and Details still link `markdown-content.css` | static | `~QuestsConversionGuardTests.MarkdownOutputViews_*` | ✅ existing | ✅ green |
| 12-card page | `class="item-card"` count stays 12 | integration | `~ShopControllerIntegrationTests.Index_WithoutPageParam_Returns12Items_WhenShopHas15` | ✅ keep or rewrite to a data hook | ✅ green |
| DoC-10 | Every page works | full suite + sweep | both full suites | ✅ | ✅ green |
| D-11 (operator decision) | Archive accepts only a Published item, for every role, so Archive then Reopen cannot publish a Draft or Denied item; the lists offer Archive only on Published rows and Reopen only on Archived rows | integration | `~ShopManagementControllerIntegrationTests`; `~ShopRenderTests` | ✅ exists | ✅ green |
| D-11 (operator decision) | The availability window on the board clock: one on-sale rule (Published, inside the window, both ends inclusive) for the shop list, Purchase and Sell to Shop; Details 404 for members before the start, reviewers without buy or sell forms, closed items open without them; the rule, service and repository hold no ambient clock | unit + integration | `~ShopItemAvailabilityTests`; `~ShopRepositoryAvailabilityTests`; `~ShopServiceTests`; `~ShopAvailabilityWindowTests`; `~AmbientClockSeamTests` | ✅ exists | ✅ green |


*Status per task: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs` (clone of `CharactersConversionGuardTests`: scope lists, retired files, scanners with self-tests)
- [x] `QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs` (both User-Agents, seeded purchase, apostrophe item name, Denied/Draft/Archived details, antiforgery token on post forms, POST Sell and Return)
- [x] `KeyboardScriptGuardTests` additions for `shop-merchant.js`, `shop-item-sell.js`, `shop-item-form.js`
- [x] Re-pointed Phase 98 tooling under `~/qb-ui-review/phase-99/tools` and `ENVIRONMENT.md`
- Framework install: none


---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gate 1 approval (relayouts, merchant pick, Preview, emblem, look check, proposals) | D-11 | Operator taste gate | `~/qb-ui-review/phase-99/gate1/`; redraw until approved |
| Gate 2 before/after sign-off with exception accept or reject | D-11 | Operator taste gate | `~/qb-ui-review/phase-99/gate2/` |
| Sell row captures | D-14 | Needs a purchase at least 24 hours old | Run at least 24 hours after the day-0 purchases |
| Merchant announcement and Return/Sell with a real screen reader and real phone touch | D-02 | Assistive technology and device | Phase UAT after deploy |
| Rendered contrast, targets, overflow at 320 and 390 | DoC-5 | Rendered measurement | `~/qb-ui-review/phase-99/after/` |
| Sell to Shop in the dialog and on the page: running total follows quantity, native confirm asks first (also for a quoted name) | D-05 | JS runtime behaviour; no browser runner in the repo | Deployed build: open an item with quotes in its name in the dialog and on Details, change quantity, submit, confirm the prompt |
| Merchant toast: focusable button, one of the 20 quotes, stays while hovered/focused, polite announcement, three bobs, none under reduced motion | D-01, D-02, D-08 | JS runtime and motion | Deployed build, desktop Shop/Index, with and without reduced motion |
| Create Preview dialog built from the form's current values, saving nothing | D-03 | JS runtime | Deployed build, desktop ShopManagement/Create: fill fields, Preview, close, confirm nothing saved |
| Details dialog loader (clears body, aria-busy, ignores stale responses), My Transactions toggle and phone drawers | D-05 | JS runtime | Deployed build, both layouts, open several items quickly |
| Management confirmations (Publish, Archive, Reopen, Delete), history Sell confirm, Deny dialog filled from data attributes, Bulk Actions notices, shared pricing module on the four item forms | D-05 | JS runtime | Deployed build, both management lists and all four forms |
| Production culture check of availability date wording ("Nov 5") | A-shop-038 | Depends on production culture | Post-deploy UAT |
| Operator decision on the three pre-existing server findings (availability window not enforced by id; Archive/Reopen accept any status; draft deep link switches board before 404) | D-11 | Operator decision, not a test | Answered "Fix + 2 gaps": the availability window and Archive/Reopen were fixed by 99-23; the deep-link board switch is kept as known debt in `deferred-items.md` |


---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated

---

## Validation Audit 2026-11-17

| Metric | Count |
|--------|-------|
| Gaps found | 2 (both PARTIAL, under D-05) |
| Resolved | 1 |
| Escalated | 1 |
| Moved to Manual-Only | 7 rows (5 behaviour-unverified truths, culture check, operator decision) |

Suites after the audit: unit 1446 passed; integration 2613 passed, 20 skipped (16 SQL Server suites plus the 4 escalated cases).

- **Gap 1, resolved.** The Details-page Sell to Shop form was checked only for its markup and never posted. `ShopFormPostEffectTests.SellToShopForm_SubmittedFromTheDetailsPage_AddsTheStockAndRecordsHalfPrice` (both layouts, item name with an apostrophe and quotes) submits the form as rendered and checks that the stock rose and a half-price Sell transaction was stored. Green.
- **Gap 2, escalated (BLOCKER).** `ShopRenderTests.HistoryRowForm_CarriesAnAntiforgeryTokenAndPostsToARedirect` only checks for a redirect, and a refused post also redirects. `ShopFormPostEffectTests.HistorySlipForm_SubmittedAsRendered_RefundsTheRemainingQuantityAndRestocks` reads what the post actually did. On an unlimited item, Return and Sell refund correctly on both layouts (4 cases, green). On a limited-stock item every case fails: the post comes back with the error toast "The association between entities 'ShopItemEntity' and 'UserTransactionEntity' ... has been severed", and no refund or restock happens. Those 4 cases are skipped with a reason and stand as the reproduction. The fault is in `ShopService.ReturnOrSellItemAsync`: it updates the item through the repository while the purchase graph loaded by `GetTransactionWithDetailsAsync` is tracked in the same context.

  **Resolved by 99-23.** Root cause: the item update mapped the domain item over the tracked entity navigations included, replacing the tracked purchases that the earlier history reads had fixed up into the item's `Transactions` with untracked copies and severing them from their required item; the shop domain-to-entity maps now ignore navigations, so every write stays on the change tracker and touches only the item's own columns and foreign keys. Fix commit `7a323074`; the 4 cases are un-skipped and green. Suites after 99-23: unit 1489 passed, 0 skipped, 0 failed; integration 2649 passed, 16 skipped (the SQL Server suites only), 0 failed.
