---
status: testing
phase: 99-shop-and-shop-management-ui-conversion
source: [99-VERIFICATION.md, 99-20-SUMMARY.md]
started: 2026-11-17T18:00:00Z
updated: 2026-11-17T18:00:00Z
---

## Current Test

number: 1
name: Real phone with touch across the shop and shop management
expected: |
  Phone layout is served on every shop page; controls are at least 44px; nothing scrolls sideways; Return and Sell on the My Transactions drawer post under touch
awaiting: post-deploy check (real-phone, screen-reader and production checks are deferred until after the v6.4.0 deploy by the operator's standing rule, as for phase 98)

## Tests

### 1. Real phone with touch: Shop index, filters drawer, My Transactions drawer (Return and Sell), item details dialog and page, Sell to Shop, and the shop management lists and item forms
expected: Phone layout is served on every page; controls are at least 44px; nothing scrolls sideways; Return and Sell post and land with the success toast
result: [pending: after deploy]

### 2. Screen reader: the merchant button and its polite announcement, the item details dialog, the Preview dialog, the Deny dialog and the My Transactions pane toggle
expected: Names, roles and the merchant quote are announced once; dialogs take and return focus; nothing is announced twice or silent
result: [pending: after deploy]

### 3. Production culture check of the date wording on the purchase history and item details
expected: Dates read like "Nov 5", not "Nov 05", under the production culture
result: [pending: after deploy]

### 4. On the deployed build: the five behaviour-unverified items in 99-VERIFICATION.md (Sell to Shop total and confirmation incl. an apostrophe name; the merchant toast, hold, auto-hide and three-cycle bob under reduced motion; Preview on desktop Create; the details dialog loader and the My Transactions toggle and phone drawers; the management confirmations, the Deny dialog, the Bulk Actions notices and the pricing module)
expected: As listed under behavior_unverified_items in 99-VERIFICATION.md (scratch browser records in the review folder passed locally at the 99-19 closing sweep)
result: [pending: after deploy]

### 5. On the deployed build: a plain member opening a Draft item's /Shop/Details/{id} gets the not-found page; a DM still opens it
expected: 404 for the member, the page for the DM; Published and Archived items open as before
result: [pending: after deploy]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps

[none: 99-VERIFICATION.md found 0 gaps. Three server rules found by 99-22 after Gate 2 (availability window not enforced by id; Archive/Reopen can skip the second-DM review; cross-board Draft deep link switches the board before the 404) are recorded in deferred-items.md and await an operator decision.]
