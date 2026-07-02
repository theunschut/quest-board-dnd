# Deferred Items — Phase 30

## Plan 30-03

### Pre-existing test failure (out of scope)

**Test:** `QuestBoard.IntegrationTests.Controllers.GroupManagementIntegrationTests.AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow`

**Status:** FAILS on `main`/this branch independent of plan 30-03 changes (verified: working tree was clean — no uncommitted diff — when the failure was reproduced).

**Symptom:** `Expected membershipAfter not to be <null>` — POST to `/platform/Group/AddMember/1` does not appear to create the expected `UserGroups` row in this test run.

**Scope:** This test exercises the Platform area `GroupController.AddMember` action (Phase 29), not `AdminController` or `UserRepository` (plan 30-03's files). Out of scope for this plan per the executor scope boundary rule — not fixed here.

**Action needed:** Investigate in a future phase/plan, or flag to the phase verifier for Phase 29/30 follow-up.
