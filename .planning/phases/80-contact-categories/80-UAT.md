---
status: testing
phase: 80-contact-categories
source: [80-VERIFICATION.md]
started: 2026-08-30T12:57:24Z
updated: 2026-08-30T12:57:24Z
---

## Current Test

number: 1
name: Confirm the live SQL Server collation is case-insensitive and a case-differing duplicate category name is refused
expected: |
  `SELECT DATABASEPROPERTYEX('QuestBoard', 'Collation');` returns a `*_CI_*` collation
  (e.g. `SQL_Latin1_General_CP1_CI_AS`). Then, as a DM on one board, create the category
  "Guild Members" and attempt to create "guild members". The second submission is refused
  with the validation message and is NOT persisted as a second row.
awaiting: user response

## Tests

### 1. Live database collation and case-differing duplicate category name

expected: Collation query reports a case-insensitive (`*_CI_*`) collation, and the case-differing duplicate is refused rather than stored.
result: [pending]

why_human: The entire test suite runs on EF Core InMemory, which enforces neither `HasIndex().IsUnique()` nor any collation behavior. The unique index `IX_ContactCategories_GroupId_Name` carries no explicit `COLLATE` clause and relies on the container's ambient default (`MSSQL_COLLATION` is unset in docker-compose.yml). This is the one must-have of fifteen that no automated test in this repository can close.

### 2. Real-handset mobile pass over the category surfaces

expected: On a real device (not devtools emulation) layout is usable, tap targets are adequate, and a long category name does not break the heading.
result: [pending]

why_human: Mobile views are selected by real User-Agent. The automated suite proves the mobile view is selected and renders, but cannot judge layout, tap targets, or legibility. Carried forward from 80-VALIDATION.md's Manual-Only table (D-08, D-09).

steps: Open Contacts and confirm category headings render legibly; open Manage Categories from the index button; add, rename, reorder and delete a category; confirm the up/down controls are tappable and the delete confirmation names the contact count.

### 3. First-run discovery on a board with zero categories

expected: The category select on Contacts -> Create is disabled with helper text linking to Manage Categories, and reads as an obvious invitation to create the first category. The index shows no headings at all.
result: [pending]

why_human: The markup was independently confirmed present and correct during verification. What cannot be automated is the subjective judgement of whether the disabled state reads as an invitation rather than as a broken control. Carried forward from 80-VALIDATION.md's Manual-Only table (D-07).

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps
