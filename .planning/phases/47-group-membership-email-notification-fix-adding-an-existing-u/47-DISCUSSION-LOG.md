# Phase 47: Group Membership Email Notification Fix - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 47-Group Membership Email Notification Fix
**Areas discussed:** Stranded-account email choice, Implementation approach, Success toast copy

---

## Stranded-account email choice

| Option | Description | Selected |
|--------|-------------|----------|
| Route to WelcomeEmailJob (set-password link) | Mirrors CreateMember's AddedToGroupStrandedAccount branch exactly | ✓ (via rerouting decision) |
| Always send GroupMembershipAddedEmailJob regardless | One job, no branching — but dead-end email for stranded users | |
| Skip email entirely for stranded accounts | No notification sent, silent success | |

**User's choice:** Asked to confirm whether this logic already existed via the `/Admin/Users` flow before deciding. Confirmed: yes — `UserService.CreateOrAddToGroupAsync` already implements this exact branching, shared by `AdminController.CreateUser` and `GroupController.CreateMember`. This resolved the question by inheritance — see Implementation Approach below.
**Notes:** User's instinct to check for existing logic before answering directly led straight to the correct answer (D-05 in CONTEXT.md).

---

## Implementation approach

| Option | Description | Selected |
|--------|-------------|----------|
| Reroute through CreateOrAddToGroupAsync | Reuse existing shared method; inherits all outcome branching | ✓ |
| Minimal patch: keep AddMemberAsync, add enqueue inline | Smaller diff, but duplicates branching logic, only fixes confirmed-user case | |

**User's choice:** Reroute through `CreateOrAddToGroupAsync`.
**Notes:** User clarified the real trigger for filing this phase: as SuperAdmin, added a confirmed, already-has-a-password user to a different group via Platform → Group → Members → available-users panel, and got no email. Confirmed this exact scenario maps to the `AddedToGroup` outcome under the reroute fix. User then asked directly: "should there be a service with the logic that both controllers call, so it will always work the same?" — confirmed this already exists (`UserService.CreateOrAddToGroupAsync`, already shared by `CreateMember` and `AdminController.CreateUser`); the fix makes `AddMember` its third caller rather than building something new.

---

## Success toast copy

| Option | Description | Selected |
|--------|-------------|----------|
| Match CreateMember's copy exactly | Toast reflects that an email was sent, consistent with CreateMember | ✓ |
| Keep AddMember's current shorter wording | No mention of email in the toast | |

**User's choice:** Match CreateMember's copy exactly.
**Notes:** None beyond the selection.

---

## Claude's Discretion

- Exact `TempData` message wording for outcome arms beyond the confirmed-account case — follow `CreateMember`'s existing per-outcome copy verbatim.
- Whether/how to add a regression test asserting the correct job is enqueued from `AddMember` per outcome — left to planning.

## Deferred Ideas

None — discussion stayed within phase scope.
