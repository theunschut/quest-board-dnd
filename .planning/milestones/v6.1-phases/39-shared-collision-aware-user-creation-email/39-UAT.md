---
status: resolved
phase: 39-shared-collision-aware-user-creation-email
source: [39-VERIFICATION.md]
started: 2026-07-04T00:14:44.263Z
updated: 2026-07-04T02:15:00.000Z
---

## Current Test

number: 2
name: All items resolved
expected: N/A
awaiting: none

## Tests

### 1. CR-01 — accept or fix the unguarded null-forgiving deref in the new-account branch
expected: Either a fix-plan lands converting `QuestBoard.Domain/Services/UserService.cs:173-174`'s `newUserId!.Value` into a graceful `Failed` result when the re-resolution returns null, or an explicit acceptance is recorded.
result: FIXED — commit 7379f6a. `newUserId == null` now returns `CreateOrAddToGroupOutcome.Failed` with an explanatory error instead of dereferencing. Covered by new unit test `CreateOrAddToGroupAsync_WhenReResolutionAfterCreateReturnsNull_ReturnsFailed`.

### 2. WR-01 — accept or fix the TOCTOU gap in membership-collision detection
expected: Either `UserService.CreateOrAddToGroupAsync`'s catch block is updated to also catch `DbUpdateException` alongside `InvalidOperationException` (unique-constraint race in `GroupRepository.AddMemberAsync`'s check-then-insert), or the narrow race is explicitly accepted given current scale (~17 users).
result: FIXED — commit 7379f6a. `GroupRepository.AddMemberAsync` now catches `DbUpdateException` around `SaveChangesAsync` and re-throws the same `InvalidOperationException` the pre-check already uses, keeping EF Core exception types confined to the Repository layer. Full unit (130/130) and integration (265/265) suites pass after the change.

## Summary

total: 2
passed: 2
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
