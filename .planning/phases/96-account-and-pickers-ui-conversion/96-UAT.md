---
status: testing
phase: 96-account-and-pickers-ui-conversion
source: [96-VERIFICATION.md]
started: 2026-11-12T17:01:39+01:00
updated: 2026-11-12T17:01:39+01:00
---

## Current Test

number: 1
name: G2-18 item 1: on a real phone, sign in, choose a board on the picker (keyboard or switch access if available), open Profile, copy an address, open and dismiss the QR, Rename and Delete dialogs, open Edit, Change Password, Forgot Password, AccessDenied signed in and out, and the signed-out landing page
expected: |
  Phone layout is served on every page except the picker (own layout); every control is at least 44px; nothing scrolls sideways at 390; dialogs return focus to their opener
awaiting: user response

## Tests

### 1. G2-18 item 1: on a real phone, sign in, choose a board on the picker (keyboard or switch access if available), open Profile, copy an address, open and dismiss the QR, Rename and Delete dialogs, open Edit, Change Password, Forgot Password, AccessDenied signed in and out, and the signed-out landing page
expected: Phone layout is served on every page except the picker (own layout); every control is at least 44px; nothing scrolls sideways at 390; dialogs return focus to their opener
result: [pending]

### 2. G2-18 item 2: with a screen reader, submit an empty Log in and press Copy on a Profile row
expected: The refusal line (role alert) and the Copied! status are announced; a live region becomes a gap only if either stays silent
result: [pending]

### 3. G2-18 item 4: QR dialog with the clipboard-denied Copy path
expected: The revealed field sits inside the dialog's focus trap; nothing throws
result: [pending]

### 4. G2-18 item 5: a real Edit with a changed email
expected: The email-change confirmation round trip works
result: [pending]

### 5. G2-18 item 6: open a malformed (not spent) SetPassword link
expected: The persistent 'Password reset failed' toast appears
result: [pending]

### 6. G2-18 item 7: Log in with a wrong password at 320 and 390 with an identity error present
expected: Long validation messages wrap without sideways scroll
result: [pending]

### 7. Re-look at AccessDenied signed out (phone) and the desktop Edit page after the post-Gate-2 review fixes
expected: The in-sentence log in link wraps with its sentence and has a 44px tap area (WR-01); the 'Want to change your password?' line reads at 4.5:1 (WR-05)
result: [pending]

## Summary

total: 7
passed: 0
issues: 0
pending: 7
skipped: 0
blocked: 0

## Gaps
