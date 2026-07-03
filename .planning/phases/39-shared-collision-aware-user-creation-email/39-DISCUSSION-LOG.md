# Phase 39: Shared Collision-Aware User Creation & Email - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 39-shared-collision-aware-user-creation-email
**Areas discussed:** Stranded-account edge case, AddedToGroup email content, Name field on collision, Flash message copy

---

## Stranded-account edge case

| Option | Description | Selected |
|--------|-------------|----------|
| Resend Welcome/SetPassword link | Detect EmailConfirmed==false and send the existing Welcome email flow (fresh SetPassword token/link) instead of the plain AddedToGroup notification — mirrors the existing SendConfirmationEmail resend pattern | ✓ |
| Always send AddedToGroup, unconditionally | Matches CREATE-02 literally; they keep whatever set-password link they already had (possibly expired) | |
| Send both emails | AddedToGroup + resent Welcome/SetPassword link, two separate emails | |

**User's choice:** Resend Welcome/SetPassword link (recommended option)

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it generic | Reuse Welcome.razor exactly as-is, no group-specific copy | ✓ |
| Add a group mention | Extend Welcome.razor with an optional line naming the new group | |

**User's choice:** Keep it generic — after a clarifying exchange confirming this was still part of the same stranded-account scenario (a user created in Group A who never set a password, then added to Group B).

| Option | Description | Selected |
|--------|-------------|----------|
| Same message either way | Admin flash message doesn't distinguish which email variant fired | ✓ |
| Distinguish it | Flash message calls out that a new sign-in link was sent instead | |

**User's choice:** Same message either way

---

## AddedToGroup email content

| Option | Description | Selected |
|--------|-------------|----------|
| Match Welcome's full styling | Same wax-seal/parchment/Cinzel treatment via shared _EmailLayout.razor | ✓ |
| Simpler/lighter style | Break from the established visual pattern since it's a pure FYI | |

**User's choice:** Match Welcome's full styling

| Option | Description | Selected |
|--------|-------------|----------|
| "Log In" button to /Account/Login | Plain link, no token, since they already have a password | ✓ |
| No button, informational only | Purely FYI text, no CTA | |

**User's choice:** "Log In" button to /Account/Login

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, name both group and role | "You've been added to {GroupName} as a {Role}." | ✓ |
| Group name only | Role omitted, visible once logged in anyway | |

**User's choice:** Yes, name both group and role

---

## Name field on collision

| Option | Description | Selected |
|--------|-------------|----------|
| Ignore it, keep existing name | Existing user's account Name left untouched | ✓ |
| Overwrite with submitted name | Updates the existing user's Name to what was typed | |

**User's choice:** Ignore it, keep existing name

---

## Flash message copy

| Option | Description | Selected |
|--------|-------------|----------|
| Success styling (green) | Reuse existing RedirectWithSuccess for "already a member" | |
| Error styling (red) | Reuse existing RedirectWithError | |
| Warning styling (yellow) — user-proposed | New RedirectWithWarning() helper, Bootstrap alert-warning | ✓ |

**User's choice:** Warning styling — user proposed this via free text ("yellow color as a 'warning'?"), confirmed after Claude clarified it requires a new `RedirectWithWarning()` helper in `ControllerExtensions.cs` (currently only Success/Error exist).

| Option | Description | Selected |
|--------|-------------|----------|
| Distinct copy per case | New account / collision-add / already-member each get tailored wording | ✓ |
| Same generic copy for both | One generic "{Name} is now a member" message for all success cases | |

**User's choice:** Distinct copy per case

**Notes:** Mid-discussion, user raised converting flash messages to toast notifications (referencing the Shop view's existing per-view toast pattern). Claude researched the Shop's implementation (`Views/Shop/Index.cshtml:450-489`, Bootstrap `.toast`, per-view not global) and presented a scope question: apply toasts just to the Admin pages this phase touches, or redesign site-wide. User chose site-wide, which Claude flagged as scope creep beyond CREATE-01..04 (a Phase-39-fixed roadmap boundary). User agreed to defer it as its own future roadmap phase rather than bundle it in or partially apply it — see Deferred Ideas below. Phase 39 ships with the existing static alert-banner pattern (extended with the new `RedirectWithWarning` helper).

---

## Claude's Discretion

- Exact name/signature of the shared collision-aware creation method (e.g. `IUserService.CreateOrAddToGroupAsync`)
- Whether to reuse/mirror `GroupRepository.AddMemberAsync`'s throw-on-collision pattern vs. building new detection logic
- Naming for the new email job/component (`GroupMembershipAddedEmailJob` / `AddedToGroup.razor` suggested in STATE.md's Risk Flags, treated as a hint not a hard requirement)
- Email subject line for the AddedToGroup email
- Whether the shared method takes GroupRole as a parameter or the caller sets role separately

## Deferred Ideas

- **Site-wide toast notification redesign** — convert all flash messages app-wide from static alert banners to Bootstrap toast notifications, matching the Shop view's existing local toast pattern. User explicitly deferred this to a future roadmap phase after discussing scope tradeoffs, rather than bundling into Phase 39 or applying it partially.
