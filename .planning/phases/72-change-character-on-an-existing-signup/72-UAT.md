---
status: testing
phase: 72-change-character-on-an-existing-signup
source: [72-01-SUMMARY.md, 72-02-SUMMARY.md, 72-03-SUMMARY.md, 72-04-SUMMARY.md]
started: 2026-08-25T14:35:00Z
updated: 2026-08-25T14:35:00Z
---

## Current Test

number: 1
name: Retired/Dead character pre-selects on modal open
expected: |
  Open the change control on a signup that holds a Retired or Dead character, on both
  desktop and mobile. The modal opens with that character already selected and its status
  shown in parentheses. Without touching the dropdown, click Save. The signup still holds
  the same character — it must not fall back to the placeholder and clear the character.
awaiting: user response

## Tests

### 1. Retired/Dead character pre-selects on modal open
expected: The modal opens with the signup's current character pre-selected and its status shown in parentheses; saving without touching the dropdown leaves that character assigned, on both desktop and mobile.
result: [pending]

### 2. Remove-character confirm dialog and toast
expected: Triggering Remove raises a native confirm() dialog that blocks removal until accepted; after accepting, a toast reading "Character removed from your signup." appears on both the desktop and mobile layouts.
result: [pending]

### 3. Mobile participant row height is unchanged
expected: The inline pencil/plus trigger sits on the same line as the character name without wrapping, and the participant and waitlist row heights are visually identical to before this phase.
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps
