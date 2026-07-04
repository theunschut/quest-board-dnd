# Phase 38: Group-Scoped User List - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-03
**Phase:** 38-Group-Scoped User List
**Areas discussed:** SuperAdmin visibility, Promote/Demote write-path gap

---

## SuperAdmin visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Apply uniformly | Same query for everyone — Admin or SuperAdmin — scoped strictly to the active group. Simplest code, matches the "this page is always group-scoped" framing of USERS-01. SuperAdmin temporarily loses the platform-wide overview on this exact page until Phase 40's dedicated cross-group Members page ships. | ✓ |
| Exempt SuperAdmin | Keep the unscoped `GetAllAsync()` call specifically when the viewer is SuperAdmin, mirroring the existing "null ActiveGroupId = see all" convention used for Quest/ShopItem query filters. Zero functionality loss, at the cost of an extra branch that becomes dead code once Phase 40 ships. | |
| You decide | Let Claude pick based on codebase conventions and the PROJECT.md "no functionality removed" constraint. | |

**User's choice:** Apply uniformly (recommended)
**Notes:** SuperAdmin can pick any group via the group picker, even ones they don't belong to (`GroupPickerController.Index` uses `GetAllWithMemberCountAsync`, not filtered to their own memberships) — confirmed this in code before asking. The lost "see everyone" ability was an unintentional side effect of the bug being fixed, not a designed feature, so it doesn't trigger the PROJECT.md "no user-facing functionality removed" constraint.

---

## Promote/Demote write-path gap

| Option | Description | Selected |
|--------|-------------|----------|
| Fix it now | Add a server-side membership check (target userId must already be in the active group) to all four role-change POST actions before calling `SetGroupRoleAsync`. Same controller, same page, same PR. | ✓ |
| Track separately | Leave Promote/Demote as-is for this phase; log as tech debt/deferred for a follow-up phase, keeping Phase 38 strictly to the literal USERS-01 read-path wording. | |
| You decide | Let Claude decide based on severity and how cleanly it fits alongside the read-path change. | |

**User's choice:** Fix it now (recommended)
**Notes:** Discovered while scouting `AdminController.cs` — `PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer` accept a raw `userId` with no membership check; `SetGroupRoleAsync` silently creates a new `UserGroups` row for any submitted userId. A crafted POST could still grant/revoke a group role cross-group even after the read-path list is scoped. Not named in the original USERS-01 wording — flagged proactively.

**Follow-up question:** What should happen when a Promote/Demote POST targets a non-member userId?

| Option | Description | Selected |
|--------|-------------|----------|
| Silent redirect | Treat it like the existing null-groupId guard elsewhere in this controller — redirect back to `Users()` with no role change and no error banner. Matches the existing pattern; the UI never renders a button for a non-member, so no user-facing message is needed. | ✓ |
| Redirect with error message | Redirect to `Users()` with a visible error/warning (e.g. "User is not a member of this group"), giving explicit feedback even though it should never happen through the normal UI. | |
| You decide | Let Claude pick based on how similar guards are handled elsewhere in AdminController. | |

**User's choice:** Silent redirect (recommended)

---

## Claude's Discretion

- New repository method vs. reusing a union of `GetAllDungeonMasters`/`GetAllPlayers` for the group-scoped user list.
- Where the membership-check helper for the write-path fix lives (controller-private helper vs. new repository/service method).
- Whether to collapse the existing per-user `GetGroupRoleByIdAsync` loop in `Users()` into a single query — not required, only if it falls out naturally.

## Deferred Ideas

None — discussion stayed within phase scope (including its extended write-path fix, which the user explicitly pulled into scope rather than deferring).
