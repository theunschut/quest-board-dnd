# Phase 60: Stop creating AspNetUserRoles entries for new users; role assignment has moved to UserGroups - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 60-stop-creating-aspnetuserroles-entries-for-new-users-role-ass
**Areas discussed:** Cleanup scope, Existing data, Test helpers

---

## Cleanup scope

| Option | Description | Selected |
|--------|-------------|----------|
| Remove dead API surface too | Delete the unused AddToRoleAsync/RemoveFromRoleAsync/IsInRoleAsync(User)/GetRolesAsync(User) methods from IUserService, IIdentityService, and their implementations. Keeps the codebase honest — no dead methods implying AspNetUserRoles is still a live mechanism. | ✓ |
| Minimal fix only | Just remove the one AddToRoleAsync call from CreateUserAsync. Leave the rest of the role API surface in place even though unused today. | |

**User's choice:** Remove dead API surface too.
**Notes:** Confirmed via grep before asking that these methods have zero callers in the Service or IntegrationTests projects. The `ClaimsPrincipal`-keyed `IsInRoleAsync` overload was flagged in CONTEXT.md as needing its own caller check at plan time since it wasn't part of this discussion's grep scope.

---

## Existing data

| Option | Description | Selected |
|--------|-------------|----------|
| Leave existing rows alone | Only stop new rows from being created going forward. Historic AspNetUserRoles rows stay in the DB as harmless cruft. Lowest risk — no data migration in production. | ✓ |
| Purge stale rows via migration | Add an EF Core migration that deletes existing Player/DungeonMaster/Admin AspNetUserRoles rows (and optionally the AspNetRoles definitions). Auto-applies on next deploy. | |

**User's choice:** Leave existing rows alone.
**Notes:** No data-cleanup migration in this phase. This app auto-applies migrations on startup in production, so a data-deleting migration is a deliberate production change the user chose to skip for now.

---

## Test helpers

| Option | Description | Selected |
|--------|-------------|----------|
| Leave test helpers untouched | Test auth uses a header-based "Test" scheme; doesn't reproduce the production bug. Out of scope. | |
| Align test helpers too | Update AuthenticationHelper to stop seeding AspNetUserRoles Player/Admin/DungeonMaster roles, relying on UserGroups seeding only, for consistency with the production fix. | ✓ |

**User's choice:** Align test helpers too.
**Notes:** Critical constraint surfaced during research (not asked as a separate question, since it's a technical detail rather than a vision choice): the `SuperAdmin` AddToRoleAsync seeding in `CreateAuthenticatedClientWithUserAsync` must NOT be removed — it's a real, live role the Test auth scheme's claims depend on. Only the Player/Admin/DungeonMaster seeding (the retired per-group-via-Identity-role model) should be removed. Also flagged a behavioral risk: `CreateAuthenticatedClientAsync`'s `roles ??= ["Player"]` fallback only triggers on null, not empty array — removing the seed changes `GetRolesAsync` from returning null to returning `[]`, silently changing this method's default role-claim behavior. Logged in CONTEXT.md as a verify-at-execution-time risk, not re-asked as a question since it doesn't change the user's decision, just how carefully the executor needs to test it.

---

## Claude's Discretion

None — all three areas had clear, decisive answers from the user.

## Deferred Ideas

None. The existing-data cleanup and AspNetRoles role-definition removal were explicitly declined (not deferred to a future phase — no follow-up action item was created).
