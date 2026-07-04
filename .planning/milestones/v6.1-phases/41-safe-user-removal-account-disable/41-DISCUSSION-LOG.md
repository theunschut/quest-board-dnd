# Phase 41: Safe User Removal & Account Disable - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 41-safe-user-removal-account-disable
**Areas discussed:** Disable action placement & targets, Active session behavior on disable, Disabled vs temporary-lockout messaging, 'Delete' button semantics after the fix

---

## Disable action placement & targets

| Option | Description | Selected |
|--------|-------------|----------|
| Existing Users page, SuperAdmin-only button | Add to Views/Admin/Users.cshtml / AdminController, visible only when User.IsInRole("SuperAdmin") | |
| Platform Members page | Add to Phase 40's Members.cshtml — reaches any group without switching, but conceptual mismatch (disable is account-wide, page is per-group) | |
| New cross-group Platform "All Users" page | Biggest new surface — full new page/controller | ✓ (user-proposed) |

**User's choice:** User pushed back on both initial options, reasoning that both the group-admin Users page and the Platform Members page are fundamentally about *group membership*, while disable/enable is a genuinely account-wide admin action — a different concern entirely. User proposed a new Platform page and asked for advice.

**Claude's advice:** Recommended a new `Areas/Platform/Controllers/UsersController.cs` (SuperAdminOnly), `Index` listing all platform users cross-group (reusing existing `IUserService.GetAllAsync()` — the exact method Phase 38 removed from the group-scoped page, appropriate here since this view is intentionally cross-group), with a Disable/Enable button per row and a `.Mobile.cshtml` companion. Entry point: a link in `GroupController.Index`'s header bar next to "Create Group".

**Follow-up confirmation:**

| Option | Selected |
|--------|----------|
| Yes, build it that way (recommended) | ✓ |
| Yes, but skip the mobile view for now | |

**User's choice:** Build it fully, including the mobile view.

**Self-disable guard:**

| Option | Selected |
|--------|----------|
| Yes, block self-disable (recommended) | ✓ |
| No restriction | |

**Peer-SuperAdmin guard:**

| Option | Selected |
|--------|----------|
| No restriction (recommended) | ✓ |
| Yes, block disabling other SuperAdmins | |

**LockoutEnabled guard question (Claude-raised mid-discussion):**

| Option | Selected |
|--------|----------|
| Yes, force LockoutEnabled = true on every disable (recommended by Claude) | |
| No, only set LockoutEnd | ✓ |

**User's choice & notes:** User corrected Claude's recommendation. `LockoutEnabled` is already `true` for every existing account (backfilled by a prior migration; no in-app path changes it, DB-only). User wants to preserve this as a deliberate manual escape hatch: when they judge a specific account trustworthy (potentially including their own), they'll flip `LockoutEnabled = false` directly in the database, making that account permanently immune to the in-app disable feature by design — not something the disable feature should ever override or touch.

---

## Active session behavior on disable

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, bump SecurityStamp too (recommended) | Call UpdateSecurityStampAsync alongside SetLockoutEndDateAsync — cookie invalidated next validator re-check | ✓ |
| No, only block future logins | Leave existing session alone until cookie naturally expires (~14 days) | |

**User's choice:** Bump SecurityStamp too.

**Follow-up — SecurityStampValidator interval:**

| Option | Selected |
|--------|----------|
| Shorten it (e.g. 5 min) (recommended) | ✓ |
| Leave the 30-minute default | |

**User's choice:** Shorten the app-wide interval (~5 minutes) — negligible cost at this app's scale (17 members), makes disable take effect much sooner.

---

## Disabled vs temporary-lockout messaging

| Option | Description | Selected |
|--------|-------------|----------|
| Threshold check on LockoutEnd (recommended) | Treat as "disabled" if LockoutEnd is more than ~1 day out | |
| Exact match against DateTimeOffset.MaxValue | Only "disabled" if LockoutEnd equals MaxValue exactly | ✓ |

**User's choice:** Exact match against `DateTimeOffset.MaxValue`.

**Follow-up — message copy:**

| Option | Selected |
|--------|----------|
| "This account has been disabled. Contact an administrator." (recommended) | ✓ |
| "This account has been disabled." | |

**User's choice:** Include the "Contact an administrator" call to action.

---

## 'Delete' button semantics after the fix

| Option | Description | Selected |
|--------|-------------|----------|
| Rename to "Remove from Group" (recommended) | Button label + confirm-dialog text both updated | ✓ |
| Keep "Delete" label, just fix the confirm-dialog copy | Button stays "Delete", only the confirm() text changes | |

**User's choice:** Rename to "Remove from Group".

**Follow-up — orphaned (zero-group) user edge case:**

| Option | Selected |
|--------|----------|
| Non-issue, no extra warning (recommended) | ✓ |
| Add a warning before removing someone's last group | |

**User's choice:** No extra warning needed — existing `GroupSessionMiddleware` already handles a groupless authenticated user gracefully.

---

## Claude's Discretion

- Exact new `IIdentityService`/`IdentityService` method names/signatures for disable, enable, and lockout-end lookup.
- Exact button/icon styling for Disable/Enable and the renamed "Remove from Group" button (follow CLAUDE.md UI/UX guidelines).
- Whether the new Platform Users page needs search/filter (not required, add only if it falls out naturally).
- Exact route/action names on the new UsersController.
- Re-enable does not bump SecurityStamp (no active session to invalidate for an already-locked-out user) — not explicitly discussed, low-risk default.

## Deferred Ideas

None raised during discussion — all four selected gray areas stayed within this phase's scope.
