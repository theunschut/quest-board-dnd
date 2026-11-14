---
status: testing
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
source: [98-VERIFICATION.md]
started: 2026-11-14T16:00:00+01:00
updated: 2026-11-14T16:00:00+01:00
---

## Current Test

number: 1
name: Real phone with touch across the whole area
expected: |
  Phone layout is served on every page; controls are at least 44px; nothing scrolls sideways; the crop selection drags and the control row works under touch
awaiting: post-deploy check (real-phone and screen-reader checks are deferred until after the v6.3.0 deploy by the operator's standing rule)

## Tests

### 1. Real phone with touch: the whole area (Characters list, details and forms, Contacts list, details and forms, Contact Categories, DM profile and edit, Players), including dragging the crop selection and using the control row
expected: Phone layout is served on every page; controls are at least 44px; nothing scrolls sideways; the crop selection drags and the control row works under touch
result: [pending: after deploy]

### 2. Screen reader: the crop selection (group, role description, label), the control row and the status line; the Contacts note buttons (Edit note, Delete note); the picture file inputs read their visible labels
expected: Names, roles and the status line are announced sensibly; nothing is announced twice or silent
result: [pending: after deploy]

### 3. Quest Details, Quest Manage and Quest Log Details in production with a character that has no picture
expected: The placeholder shows, no request is made to /Characters/GetCroppedPicture, and the console is clean
result: [pending: after deploy]

### 4. Re-look at the five picture-input hosts after the post-Gate-2 label change (5a6f7c84)
expected: Each Profile Picture, Contact Image and Profile Photo label points at its own input, nothing moves visibly, and the crop dialog still opens from the input
result: [pending: after deploy]

### 5. On the deployed build: the crop dialog non-drag path (arrow keys, Shift+arrow, move/grow/shrink/Reset buttons, kept aspect ratio, dialog focus), the Markdown editor Tab release on the six editor pages, and the delete confirmations and Contacts note controls
expected: As listed under behavior_unverified_items in 98-VERIFICATION.md (scratch records: 10/10 crop behaviour records and 16/16 editor records passed locally)
result: [pending: after deploy]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps

[none: 98-VERIFICATION.md found 0 gaps; the 320px desktop navbar (d2) is an approved follow-up phase]
