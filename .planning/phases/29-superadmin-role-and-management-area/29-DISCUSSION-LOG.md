# Phase 29: SuperAdmin Role & Management Area - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-30
**Phase:** 29-superadmin-role-and-management-area
**Areas discussed:** Auth handler migration, Broken promote/demote fix, SuperAdmin bootstrap, Platform area views

---

## Auth Handler Migration

| Option | Description | Selected |
|--------|-------------|----------|
| Extend IUserService | Add GetGroupRoleAsync to IUserService; handlers already inject it — minimal change | ✓ |
| New IGroupService method | Create IGroupService with GetUserGroupRoleAsync; cleanest domain separation | |
| Handler injects IActiveGroupContext directly | Handler adds IActiveGroupContext + IUserGroupRepository; lowest abstraction | |

**User's choice:** Extend IUserService with `GetGroupRoleAsync(ClaimsPrincipal, int groupId)`

**Follow-up — active group ID source in handler:**

| Option | Description | Selected |
|--------|-------------|----------|
| Handler injects IActiveGroupContext | AdminHandler(IUserService, IActiveGroupContext); calls GetGroupRoleAsync with activeGroupContext.ActiveGroupId | ✓ |
| ClaimsPrincipal claim | Store ActiveGroupId in auth cookie as claim | |

**User's choice:** Handler also injects `IActiveGroupContext`

**Follow-up — null ActiveGroupId behavior:**

| Option | Description | Selected |
|--------|-------------|----------|
| Deny unless SuperAdmin | context.User.IsInRole("SuperAdmin") → succeed; null + not SuperAdmin → fail | ✓ |
| Deny all | Same in practice | |

**User clarification:** SuperAdmin check uses `context.User.IsInRole("SuperAdmin")` in the handler (ClaimsPrincipal has the Identity claim). `IsSuperAdmin` is NOT a property on `IActiveGroupContext` — the user corrected the initial framing.

**Follow-up — IActiveGroupContext.IsSuperAdmin (Phase 28 deferred):**

| Option | Description | Selected |
|--------|-------------|----------|
| Confirmed — split makes sense | IsSuperAdmin on IActiveGroupContext for HasQueryFilter; context.User.IsInRole for handlers | |
| No IsSuperAdmin on IActiveGroupContext at all | IActiveGroupContext stays as int? ActiveGroupId only | ✓ |

**Discussion:** User asked why IsSuperAdmin would be needed on IActiveGroupContext. Explained: QuestBoardContext (Repository layer) has no ClaimsPrincipal access, so the HasQueryFilter predicate can only read from IActiveGroupContext. The IsSuperAdmin property was proposed in Phase 28 to distinguish "null because SuperAdmin" from "null because no session" in the predicate.

User's response: The full v5.0 milestone merges as one PR before production deployment. Phase 30's group-picker will enforce that regular users always have ActiveGroupId set before reaching content pages. Therefore the null-gap interim state never hits production — no IsSuperAdmin tightening needed.

**Final decision:** `IActiveGroupContext` stays as `int? ActiveGroupId { get; }` only. HasQueryFilter predicate unchanged. Phase 28 deferred item cancelled.

---

## Broken Promote/Demote Fix

| Option | Description | Selected |
|--------|-------------|----------|
| Fix in Phase 29 | Update PromoteToAdmin, DemoteFromAdmin, PromoteToDM, DemoteToPlayer to write to UserGroups.GroupRole | ✓ |
| Defer to Phase 30 | Leave broken; acceptable since full milestone merges as one PR | |

**User's choice:** Fix in Phase 29

**Follow-up — which group to target:**

| Option | Description | Selected |
|--------|-------------|----------|
| Active group from IActiveGroupContext | Operations target the admin's currently active group | ✓ |
| Hardcode GroupId = 1 | Quick fix; breaks when second group exists | |

**User's choice:** Active group from IActiveGroupContext

---

## SuperAdmin Bootstrap

| Option | Description | Selected |
|--------|-------------|----------|
| EF Core migration InsertData | Role seeded in migration; consistent with existing role seeding pattern | ✓ |
| Startup RoleManager.CreateAsync | Idempotent but different pattern from rest of project | |

**User's choice:** EF Core migration InsertData (Id=4, Name="SuperAdmin")

**Follow-up — first SuperAdmin user assignment:**

| Option | Description | Selected |
|--------|-------------|----------|
| Config + startup assignment | appsettings.json SuperAdmin:Email; startup assigns role | |
| Hardcoded in migration | Migration inserts AspNetUserRoles row for specific user ID | |
| Manual post-deploy step | Document one-time SQL command; no automation | ✓ |

**User's choice:** Manual post-deploy step

**Follow-up — documentation form:**

| Option | Description | Selected |
|--------|-------------|----------|
| SQL command in deployment docs | One-time INSERT into AspNetUserRoles; added to co-deployment constraint doc | ✓ |
| Admin endpoint in /platform | Bootstrap form on first visit; self-service | |

**User's choice:** SQL command in deployment docs

---

## Platform Area Views

**Context question:** User asked to clarify what /platform is before deciding on layout.

Explanation provided: /platform is the SuperAdmin-only back-office for managing groups and memberships. Completely separate from the quest board. Regular players and DMs never see it.

**Second question:** User asked whether they can use the same account for SuperAdmin (/platform) and Admin/DM in EuphoriaInn.

Explanation provided: Yes — SuperAdmin is an Identity role (AspNetUserRoles, system-wide) and Admin/DM in EuphoriaInn is a GroupRole in UserGroups (per-group). One account can hold both. Phase 30's group-picker handles the transition between contexts.

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated _Layout.Platform.cshtml | Clean layout: logo, user name, logout, "Back to quest board" link; no quest board nav | ✓ |
| Share _Layout.cshtml | Full quest board nav visible in /platform | |

**User's choice:** Dedicated _Layout.Platform.cshtml

**Visual polish:**

| Option | Description | Selected |
|--------|-------------|----------|
| Functional admin panel | modern-card + tables + standard form-based CRUD; same as Admin > Users pages | ✓ |
| More polished | Charts, status badges, animations | |

**User's choice:** Functional admin panel. User noted: "The more polished option can always be done in a later milestone."

---

## /players Page Fix (surfaced during Platform area discussion)

User recalled the broken /players page noticed during Phase 28 human verification. Root cause identified: `GetAllPlayersAsync` and `GetAllDungeonMastersAsync` read from `AspNetUserRoles`, which Phase 27 cleared.

| Option | Description | Selected |
|--------|-------------|----------|
| Fix in Phase 29 | Update both methods to read from UserGroups.GroupRole for the active group | ✓ |
| Defer to Phase 30 | Leave broken; acceptable since full milestone merges as one PR | |

**User's choice:** Fix in Phase 29

---

## Claude's Discretion

- Exact SuperAdminOnly policy registration in Program.cs
- GroupController vs. PlatformController naming inside the Area
- Whether group detail and member management live on one controller or two
- Exact column layout and button placement in platform views

## Deferred Ideas

- Platform visual polish (charts, badges, animations) — future milestone
- SuperAdmin link in main quest board nav — Phase 30
- IsSuperAdmin on IActiveGroupContext — cancelled
- Group admin user creation and role management — Phase 30 (MGMT-07/08)
