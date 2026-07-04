---
status: testing
phase: 42-site-wide-toast-notification-redesign
source: [42-VERIFICATION.md]
started: 2026-07-04T00:00:00Z
updated: 2026-07-04T00:00:00Z
---

## Current Test

number: 1
name: All 5 layouts render toasts correctly
expected: |
  Success = green header + check-circle icon, auto-hides ~5s; Error = red header + exclamation-circle icon, stays until manually dismissed; Warning = yellow header (dark text) + exclamation-triangle icon, stays until manually dismissed; Info = blue header + info-circle icon, auto-hides ~5s. Container is fixed top-right on all 5 layouts (_Layout, _Layout.Mobile, _Layout.GroupPicker, _Layout.Platform, _Layout.Platform.Mobile).
awaiting: user response

## Tests

### 1. All 5 layouts render toasts correctly
expected: Load a view under each of the 5 layouts with a TempData flash key set and confirm a toast renders top-right with correct color/icon per type, and correct autohide/sticky timing per type.
result: [pending]

### 2. GoldReceived + Success dual-toast on Shop sale
expected: Complete a sale in Shop Details and confirm the GoldReceived "+X gp" badge toast and the generic Success toast both appear together — two toasts stacked vertically top-right, not one replacing the other.
result: [pending]

### 3. Mystical Merchant toast regression check
expected: Trigger the Mystical Merchant dialog on Shop Index (click the merchant icon) and confirm its yellow toast still displays flavor text with unchanged styling/behavior, now that its container is the shared partial's instead of a local block.
result: [pending]

### 4. Profile email-change Info toast
expected: Request an email change from Account/Profile and confirm a blue Info toast now appears (previously this path was silently dropped — intentional fix, not scope creep).
result: [pending]

### 5. Login flash on mobile
expected: Trigger a Login-page flash on both desktop and mobile (e.g. an expired ConfirmEmailChange link, or a successful password reset) and confirm a toast appears on both, including Login.Mobile.cshtml which previously showed nothing.
result: [pending]

### 6. Platform admin action toast
expected: Perform a Platform admin action (add/remove group member, disable/enable a user) and confirm the resulting message appears as a top-right toast under the Platform layout — not as an inline banner, and not missing entirely.
result: [pending]

## Summary

total: 6
passed: 0
issues: 0
pending: 6
skipped: 0
blocked: 0

## Gaps
