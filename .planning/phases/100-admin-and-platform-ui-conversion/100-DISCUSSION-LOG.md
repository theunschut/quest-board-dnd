# Phase 100: Admin and Platform UI conversion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-18
**Phase:** 100-admin-and-platform-ui-conversion
**Areas discussed:** Server-rule audit scope, Fixtures and real data, Delete/remove behaviour, Gate 1 contents and look

Carried over from the G1 sign-off without re-asking: Q-85 to Q-94, Q-108 to Q-110 (twins kept), and
Q-124 to Q-126 (relayouts in principle). Gates, fixtures and delivery are as in Phases 98 and 99.

---

## Server-rule audit scope

| Option | Description | Selected |
|--------|-------------|----------|
| Audit early, fix in-phase | Audit every action behind the 25 views first; confirmed gaps get slices before the UI work | ✓ |
| Audit early, fix in a separate phase | Audit and record here; fixes in a follow-up phase | |
| Pure UI conversion | No server-rule work | |

**User's choice:** Audit early, fix in-phase.

| Option | Description | Selected |
|--------|-------------|----------|
| Block on higher/other-board admins | A board Admin may reset or edit only accounts whose highest role anywhere is no higher than theirs; SuperAdmin acts on anyone | ✓ |
| SuperAdmin only for password reset | Board Admin keeps name and email edits and the welcome resend | |
| Keep today's rule, record it | Accepted residual | |
| Decide after the audit | Pick at the checkpoint | |

**User's choice:** Block on higher/other-board admins.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, guard both | Refuse self-demotion/self-removal and leaving a board with no Admin | ✓ |
| Guard last Admin only | | |
| No guard | | |

**User's choice:** Guard both.

| Option | Description | Selected |
|--------|-------------|----------|
| Fix if the page calls it | In scope when reachable from the 25 views; otherwise deferred | ✓ |
| Only controller-level fixes | | |
| Fix everything found | | |

**User's choice:** Fix if the page calls it.

| Option | Description | Selected |
|--------|-------------|----------|
| Operator checkpoint after audit | Approve, adjust or accept each gap before fix slices | ✓ |
| Fix the obvious, ask on judgment calls | | |

**User's choice:** Operator checkpoint after audit.

---

## Fixtures and real data

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse existing audit SuperAdmin | Phase 94's SuperAdmin login; no new SuperAdmin | ✓ |
| Operator creates a p100 SuperAdmin | | |
| Operator logs in for Platform captures | | |

**User's choice:** Reuse the existing audit SuperAdmin.

| Option | Description | Selected |
|--------|-------------|----------|
| Capture as-is, local only | Screenshots local; no names quoted; not published unless masked | ✓ |
| Mask real rows in captures | | |
| Fixtures-only views | | |

**User's choice:** Capture as-is, local only.

| Option | Description | Selected |
|--------|-------------|----------|
| All, on p100 fixtures only | Every state-changing action, only on p100 accounts, boards and sets | ✓ |
| All except Disable/Enable | | |
| View-only, guards tested in integration tests | | |

**User's choice:** All, on p100 fixtures only.

| Option | Description | Selected |
|--------|-------------|----------|
| Test-only stub of the stats client | Canned figures through a fake `ResendStatsClient`; no key, no appsettings edit | ✓ |
| Real key via user-secrets | | |
| Source-only | | |

**User's choice:** Test-only stub.

---

## Delete/remove behaviour

| Option | Description | Selected |
|--------|-------------|----------|
| Ordinary POST forms | confirm-submit.js confirm, PRG, toasts; DELETE endpoints become POST | ✓ |
| Keep fetch, toast on failure | | |

**User's choice:** Ordinary POST forms.

| Option | Description | Selected |
|--------|-------------|----------|
| Native confirm, today's wording | confirm-submit.js precedent | ✓ |
| Foundation confirm modal | New UI, drawn at Gate 1 | |

**User's choice:** Native confirm, today's wording.

---

## Gate 1 contents and look

| Option | Description | Selected |
|--------|-------------|----------|
| quest-form render + converted 98/99 pages | One mockup per page type: table list, form, confirmation, Members modal | ✓ |
| quest-form render only | | |

**User's choice:** quest-form render plus the converted 98/99 pages.

| Option | Description | Selected |
|--------|-------------|----------|
| Fix now, one display mapping | Shared board-type and role labels; each wording change a Gate 1 item | ✓ |
| Leave for a later copy pass | | |

**User's choice:** Fix A-admin-044 now.

| Option | Description | Selected |
|--------|-------------|----------|
| Fix in this phase | Any still-failing Platform layout item is a slice here | ✓ |
| Report back to foundation | | |

**User's choice:** Fix in this phase.

---

## Claude's Discretion

- Where the "highest role anywhere" check lives (layer-correct).
- Slice order and plan count; stylesheet consolidation; the small audit fixes (A-admin-037, 039, 040).
- Ports.

## Deferred Ideas

- Audit gaps not reachable from the converted views go to `deferred-items.md`.
