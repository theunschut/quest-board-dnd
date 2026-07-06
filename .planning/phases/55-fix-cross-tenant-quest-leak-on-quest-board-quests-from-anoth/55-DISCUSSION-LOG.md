# Phase 55: Fix cross-tenant quest leak on quest board - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
**Areas discussed:** Root cause investigation, SuperAdmin fix mechanism, filter hardening, SelectGroup scope, defense-in-depth

---

## Root Cause Investigation

A background code-investigation agent was dispatched before any decisions were asked, to ground the discussion in actual code rather than the user's initial hypothesis. It traced `QuestBoardContext.cs`'s filters, `GroupSessionMiddleware.cs`, `ActiveGroupContextService.cs`, session/cookie config in `Program.cs`, and `GroupPickerController.cs`.

**Finding:** The user's original hypothesis (session/`AspNetSessionState` expiration causing a regular user to see another tenant's data) does not hold — `GroupSessionMiddleware` already blocks that path for every non-SuperAdmin user (redirect on GET, 409 on writes). The investigation separately surfaced a real, unrelated authorization gap: `GroupPickerController.SelectGroup` never validates the caller is a member of the posted `groupId`.

## Root Cause Confirmation

| Option | Description | Selected |
|--------|-------------|----------|
| SuperAdmin account | Manages multiple/all groups via /platform | ✓ |
| Regular account (Admin/DM/Player) | Scoped to one group/tenant | |

**User's choice:** SuperAdmin.
**Notes:** User reported opening the app on a different computer a day later, on a SuperAdmin account, and seeing "all quests on the same page." This matches the `ActiveGroupId == null` escape hatch on the Quest/ShopItem/vote-entity filters — reachable because `GroupSessionMiddleware` exempts SuperAdmin from the group-required gate everywhere, not just group-agnostic areas. Confirmed as intentional-but-undesired: `.planning/codebase/CONCERNS.md` had explicitly documented this as correct SuperAdmin behavior; the user's reaction supersedes that prior design call.

## SuperAdmin Fix Mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Force group pick first | Redirect to /groups/pick before /quests, same as every other role | ✓ |
| Keep all-groups overview, but label it clearly | Preserve the aggregate view, add an unmistakable banner | |

**User's choice:** Force group pick first.

## Extra Scope — SelectGroup Membership Gap

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, fix it in this phase | Same theme (validate the target, not just the caller); matches Phase 49 precedent of folding in adjacent confirmed leaks | ✓ |
| No, separate follow-up phase | Keep this phase focused on the SuperAdmin issue | |

**User's choice:** Yes, fix it in this phase.

## SelectGroup Response Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| 404 Not Found | Matches established convention (Phase 49 D-04/D-09/D-13) — hide existence | ✓ |
| 403 Forbidden with a message | Tell the user plainly they lack access | |

**User's choice:** 404 Not Found.

## Fix Breadth

| Option | Description | Selected |
|--------|-------------|----------|
| Broad — all group-scoped pages | Close the escape hatch everywhere at once via the middleware change | ✓ |
| Narrow — /quests only | Fix only the reported page, defer Shop/vote pages | |

**User's choice:** Broad — all group-scoped pages.
**Notes:** User's own framing: "a super admin can access everything, but should do so as a normal user... the content should be displayed as if a normal user views the site. This way there cannot be any confusion about what a user sees vs what a super admin sees." This statement became the governing principle for the whole phase, driving the broad middleware fix, the filter-hardening decision, and the defense-in-depth decision below.

## Filter Hardening

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, harden filters too | Belt-and-suspenders, matching Phase 49's own established lesson about single-layer fragility | ✓ |
| No, middleware fix is enough | Simpler diff, no second line of defense | |

**User's choice:** Yes, harden filters too.

## Defense in Depth (mid-session membership revocation)

**User's choice:** Deferred to Claude's judgment ("your choice, please investigate what's best").
**Resolution:** Recommended and locked as in-scope (D-06 in CONTEXT.md) — grounded in the same governing principle above (a user removed from a group mid-session shouldn't retain stale access) and in genuine architectural precedent already present in this codebase (Phase 41's `SecurityStampValidatorOptions.ValidationInterval` was shortened to 5 minutes specifically to solve the identical "revoke stale session privilege quickly" problem for account disable). Exact mechanism left as Claude's Discretion for research to resolve — investigate reusing the existing SecurityStamp re-validation interval before building a separate check.

---

## Claude's Discretion

- Exact hook point/implementation for the periodic membership re-validation (D-06).
- Whether `GroupPickerController.Index` already scopes its listed groups correctly for non-SuperAdmin callers (verify during research; not blocking, since D-04's SelectGroup guard makes any mismatch non-exploitable regardless).
- Whether any additional group-scoped entity beyond the five identified shares the same escape-hatch filter shape and needs the same D-03 treatment.

## Deferred Ideas

None — all four fixes were folded into this phase's scope. Two alternate root-cause theories raised during investigation (reverse-proxy/CDN cookie caching, session fixation on login) were considered and explicitly not pursued, since the user's own account (SuperAdmin, new device, no group ever picked) fully explains the reported symptom without them. Documented in CONTEXT.md D-07 as leads to re-open only if a similar leak is ever reported from a non-SuperAdmin account.
