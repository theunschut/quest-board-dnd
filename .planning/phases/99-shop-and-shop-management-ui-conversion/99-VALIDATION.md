---
phase: "99"
slug: "shop-and-shop-management-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
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


*Status per task: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs` (clone of `CharactersConversionGuardTests`: scope lists, retired files, scanners with self-tests)
- [ ] `QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs` (both User-Agents, seeded purchase, apostrophe item name, Denied/Draft/Archived details, antiforgery token on post forms, POST Sell and Return)
- [ ] `KeyboardScriptGuardTests` additions for `shop-merchant.js`, `shop-item-sell.js`, `shop-item-form.js`
- [ ] Re-pointed Phase 98 tooling under `~/qb-ui-review/phase-99/tools` and `ENVIRONMENT.md`
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


---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
