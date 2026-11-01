---
phase: 94-whole-app-ui-review-and-unification
plan: 06
subsystem: ui-audit
tags: [audit, shop, twin-comparison, contrast, navy-theme]
requires:
  - phase: 94-02
    provides: "manifest, fixtures, frozen baseline on port 8001, BEFORE record, capture tools"
provides:
  - "94-AUDIT-shop.md: 65 findings (A-shop-001 to 065), twin comparison, 21 operator questions, relayout proposals R-1 and R-2, navy theme inventory, empty-state copy, area-phase scope notes"
affects: [94-AUDIT index, shop theme decision, area phase 4 Shop]
tech-stack:
  added: []
  patterns: ["twin inventories extracted by script from both .cshtml files", "live probes on the frozen baseline (port 8001), read-only, no form submitted"]
key-files:
  created:
    - ".planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-shop.md"
  modified: []
decisions:
  - "No merge proposals: no Shop pair qualifies under D-13 (all content-differs, or a deliberate difference on Create and Edit)"
  - "The price helper buttons missing on Create.Mobile and Edit.Mobile are recorded as deliberate by the views' own comment; the purchase-panel omission on mobile is test-pinned but still goes to the operator"
  - "Shop Create failure recorded as A-shop-010 (S1, functional, handed off, no area phase) and the Create form was not submitted"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 21300
  tasks: 2
  commits: 2
---

# Phase 94 Plan 06: Shop and Shop Management Audit Summary

Complete Critic-mode audit of the Shop area (4 view pairs plus Shop Index, one shared partial, 6 stylesheets) in the fixed report format: 65 findings, a twin comparison with extracted inventories, 21 operator questions, two relayout proposals, no merge proposals, and a navy theme inventory with computed contrast for the (a)/(b) decision.

Only `94-AUDIT-shop.md` was added to the repository. Probes, notes and extractors live under `~/qb-ui-review/phase-94/audit/shop/`.

## What was found (headlines)

- **10 S1.** Unreadable text: desktop Shop empty states (1.02:1, the class `empty-shop-state` has no CSS), purchase panel empty text (1.00:1), item-details modal labels and Sell block on desktop (1.09 to 1.22:1) and item title in the mobile modal (1.22:1), "View Shop" outline buttons (1.22 / 1.70:1), management table teasers (1.14 to 1.31:1), "N left" badges (1.29:1), mobile Edit "Item ID" (1.06:1). A control unreachable: the purchase history Return and Sell buttons are `opacity: 0; visibility: hidden` with no rule or script that reveals them (`site.css:1360`). The functional Create failure is logged as A-shop-010.
- **Behaviour that is broken today (verified live on the baseline):** the sell confirmation and sell-price total never run in the modal (inline script from `innerHTML` does not execute: `typeof confirmSell` is `undefined`); the Create "Preview" button calls an undefined function; the Edit "View Details" link returns 404.
- **Twin differences:** every pair is content-differs. The mobile layout has no purchase history, no Sell to Shop, no availability or properties on Details, a merged management list and a different empty state. Mobile modal also receives `Details.Mobile` as a partial, so its stylesheet never loads.
- **Navy theme:** exists on desktop Shop Index only; computed ratios show it is the more compliant of the two looks (7 to 19:1 on nearly every pair), the failures on that page sit outside the navy field. Written twice (`shop.css`, `site.css`).
- **Animations:** ten infinite declarations, not six (four are dead copies inside the Create and Edit `<style>` blocks, which restyle classes no element on those pages carries).
- **Other counts:** `shop.css` 107 hex, 76 `rgba`, 70 `!important`, 9 `transition: all`; the Shop rule families are duplicated in `site.css`; `shop.css` is loaded on every desktop page.

## Deviations from Plan

**1. [Process] Both tasks executed in one pass.** The plan's tracer task (Shop Index) and the expansion task share one output file, and the evidence tooling (probes, inventories, contrast computation) was built once for both. Task 1's automated verify was run against the final file together with Task 2's, and both passed. The tracer-gate re-run was therefore at the end rather than between the tasks; no expansion happened on an unverified method because the per-finding fields and index format were fixed up front by the UI-SPEC and checked by a script (headings 65 of 65, ids contiguous, classes consistent).

**2. [Limitation] No mobile focus findings.** The BEFORE focus probe is desktop-only by design, so mobile focus behaviour was read from the views only and no mobile focus finding is claimed.

**3. [Judgement] Return and Sell invisibility is static evidence.** The Audit Board has no purchase rows and none were created (a purchase spends fixture gold and the plan forbids side effects here), so A-shop-002 cites the CSS and markup lines; the area phase should create a purchase through the Purchase form to confirm after the fix.

**4. [Judgement] Twin-content question for the purchase panel.** The audit contract says no deliberate differences are pre-recorded for this area. An existing integration test pins the panel's omission on mobile (`MobileViewsTests.cs:716-733`); it is cited in the finding and still asked as Q-shop-01. The price helper buttons are treated as deliberate by the mobile views' own comment, per the contract's "commented in the view" rule, and are not asked.

## Auth gates

None.

## Known Stubs

None in this plan (documentation only). The audit records existing stubs in the product: the Bulk Actions placeholder (A-shop-041) and the dead Preview button (A-shop-027).

## Threat Flags

None new. The report quotes no member name, email or user-entered text (grep for mail domains and fixture item names is clean), cites screenshots by scratch path only and uses fixture ids. No form was submitted; nothing was written to the database.

## Self-Check: PASSED

- `94-AUDIT-shop.md` exists, nine required sections plus Navy theme inventory and Stylesheet and markup inventory; all five Shop view pairs present in the index.
- Task 1 and Task 2 automated verify commands pass (grep checks, no real mail domain).
- `git status` showed only the new report before the SUMMARY; no changes to tracked source, STATE.md or ROADMAP.md.
