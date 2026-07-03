# Phase 37: Navigation & Access Control - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-03
**Phase:** 37-navigation-access-control
**Areas discussed:** SuperAdmin nav visibility, Nav visibility with no known board type, Email Stats access-denied UX

---

## SuperAdmin nav visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Follow active group's type | SuperAdmin's nav mirrors whatever group they're currently viewing, exactly like everyone else — no special-casing. | ✓ |
| Always full nav for SuperAdmin | SuperAdmin never loses nav items regardless of active group, mirroring the existing SuperAdmin-bypass precedent used for functional checks elsewhere. | |

**User's choice:** Follow active group's type
**Notes:** None — first-pass pick, no follow-up needed.

---

## Nav visibility with no known board type

| Option | Description | Selected |
|--------|-------------|----------|
| Default to visible | Treat "unknown board type" the same as One-Shot — nav looks exactly like it does today until a campaign group is actually active. | |
| Default to hidden | Conservative — hide campaign-gated items whenever board type can't be determined. | ✓ (for the no-active-group case) |

**User's choice:** User initially questioned the premise — pointed out that for an anonymous visitor, shouldn't nothing but the title show already? This surfaced a real, separate pre-existing gap: Calendar's nav link currently renders unconditionally (outside the `IsAuthenticated` check) for anonymous visitors, even though `CalendarController` requires `[Authorize]` — a visible-but-dead link. Follow-up split this into two questions:

1. **Hide Calendar for anonymous visitors too?** → Yes, fix it now (recommended and selected) — one-line change in the same code region already being touched for NAV-01.
2. **Nav for an authenticated user with no active group yet (on GroupPicker)?** → Hide (user's explicit choice, overriding Claude's "show" recommendation).

**Notes:** Combined with the SuperAdmin answer, this converges on a single rule: show the 5 gated nav items (Calendar, Shop, Manage Shop, Edit My Profile, Players) only when the active group's board type resolves to One-Shot — an allowlist, not a Campaign-only blocklist. Captured as D-01 in CONTEXT.md.

---

## Email Stats access-denied UX

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add it now | Small, contained addition (one route + one simple view) directly serving ACCESS-01's "rejected, not just hidden" intent; fixes the same 404 gap for every other policy-gated page as a side effect. | ✓ |
| No, leave the 404 and log as tech debt | Keep this phase strictly to nav/access gating logic; a 404 does technically reject access. | |

**User's choice:** Yes, add it now
**Notes:** Explicitly flagged to the user that `AccessDeniedPath` is a single app-wide cookie-auth setting, so this fix isn't scoped to Email Stats alone — it resolves the same 404 for every `AdminOnly`/`DungeonMasterOnly`/`SuperAdminOnly` failure across the app. User approved the wider blast radius.

---

## Claude's Discretion

- Exact mechanism for exposing the active group's `BoardType` to `_Layout.cshtml` (session-mirror following the `ActiveGroupName` precedent vs. extending `IActiveGroupContext`) — not discussed with the user, left to research/planning.
- Exact wording/styling of the new Access Denied page — follow CLAUDE.md's modern-card pattern; copy left to planner/implementer.
- Whether the `AccessDenied` action lives on `AccountController` or elsewhere — implementation detail.

## Deferred Ideas

None — both adjacent items raised during discussion (anonymous Calendar link, Access Denied page) were pulled into this phase's scope rather than deferred.
