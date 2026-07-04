---
status: testing
phase: 39-shared-collision-aware-user-creation-email
source: [39-VERIFICATION.md]
started: 2026-07-04T00:14:44.263Z
updated: 2026-07-04T00:14:44.263Z
---

## Current Test

number: 1
name: Decide whether CR-01 (unhandled null-forgiving deref in UserService.CreateOrAddToGroupAsync) must be fixed before shipping, or is an accepted risk.
expected: |
  Either a follow-up fix converts the null re-resolution into a Failed outcome (as 39-REVIEW.md CR-01 recommends), or the maintainer explicitly accepts the race as out-of-scope/negligible-likelihood for this milestone.
awaiting: user response

## Tests

### 1. CR-01 — accept or fix the unguarded null-forgiving deref in the new-account branch
expected: Either a fix-plan lands converting `QuestBoard.Domain/Services/UserService.cs:173-174`'s `newUserId!.Value` into a graceful `Failed` result when the re-resolution returns null, or an explicit acceptance is recorded.
result: [pending]

### 2. WR-01 — accept or fix the TOCTOU gap in membership-collision detection
expected: Either `UserService.CreateOrAddToGroupAsync`'s catch block is updated to also catch `DbUpdateException` alongside `InvalidOperationException` (unique-constraint race in `GroupRepository.AddMemberAsync`'s check-then-insert), or the narrow race is explicitly accepted given current scale (~17 users).
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
