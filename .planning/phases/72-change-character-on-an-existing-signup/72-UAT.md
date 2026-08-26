---
status: complete
phase: 72-change-character-on-an-existing-signup
source: [72-01-SUMMARY.md, 72-02-SUMMARY.md, 72-03-SUMMARY.md, 72-04-SUMMARY.md]
started: 2026-08-25T14:35:00Z
updated: 2026-08-26T05:55:24Z
---

## Current Test

number: 3
name: Mobile participant row height is unchanged
expected: |
  All three human verification items were run by the operator and passed.
awaiting: none — UAT complete

## Tests

### 1. Retired/Dead character pre-selects on modal open
expected: The modal opens with the signup's current character pre-selected and its status shown in parentheses; saving without touching the dropdown leaves that character assigned, on both desktop and mobile.
result: pass

### 2. Remove-character confirm dialog and toast
expected: Triggering Remove raises a native confirm() dialog that blocks removal until accepted; after accepting, a toast reading "Character removed from your signup." appears on both the desktop and mobile layouts.
result: pass

### 3. Mobile participant row height is unchanged
expected: The inline pencil/plus trigger sits on the same line as the character name without wrapping, and the participant and waitlist row heights are visually identical to before this phase.
result: pass

## Summary

total: 3
passed: 3
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
