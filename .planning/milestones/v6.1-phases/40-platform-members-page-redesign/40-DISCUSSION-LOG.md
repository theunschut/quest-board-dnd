# Phase 40: Platform Members Page Redesign - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 40-platform-members-page-redesign
**Areas discussed:** Search & non-member table, Add-to-group interaction, Create New User entry point, Mobile layout

---

## Search & non-member table

| Option | Description | Selected |
|--------|-------------|----------|
| Client-side instant filter | JS filters table rows as you type, no page reload. New pattern for this codebase. | |
| Server round-trip search | GET with a query param + page reload, mirroring Shop/Index.cshtml's existing filter-row pattern. | ✓ |
| Let Claude decide | | |

**User's choice:** Server round-trip search
**Notes:** Consistency with the existing Shop filter pattern preferred over introducing a new client-side JS filtering approach.

| Option | Description | Selected |
|--------|-------------|----------|
| Name + Email only | Matches the info in today's dropdown option text exactly. | ✓ |
| Name + Email + other group memberships | Useful context, but requires a new query that doesn't exist yet. | |
| Let Claude decide | | |

**User's choice:** Name + Email only
**Notes:** No new repository query needed for cross-group membership display.

---

## Add-to-group interaction

| Option | Description | Selected |
|--------|-------------|----------|
| Inline per-row role + Add button | Each row gets a role dropdown (default Player) and an Add button — one click per add. | ✓ |
| Click row → fills existing form below | Clicking a row populates today's Add Member form below the table. | |
| Let Claude decide | | |

**User's choice:** Inline per-row role + Add button
**Notes:** Standalone AddMember form section is removed from the view entirely.

| Option | Description | Selected |
|--------|-------------|----------|
| Preserve search term after Add | Redirect back to Members with the same ?search= query string. | ✓ |
| Reset to unfiltered list after Add | Simpler — redirect to the plain Members page after every Add. | |
| Let Claude decide | | |

**User's choice:** Preserve search term after Add
**Notes:** Follows from choosing server round-trip search — the filter shouldn't be lost after an action.

---

## Create New User entry point

| Option | Description | Selected |
|--------|-------------|----------|
| Bootstrap modal | Mirrors ShopManagement/Index.cshtml's existing modal-with-form-post pattern. | ✓ |
| Inline expandable panel | No modal JS, but no precedent in this codebase. | |
| Let Claude decide | | |

**User's choice:** Bootstrap modal
**Notes:** Satisfies the Phase 40 goal's "without leaving the page" requirement using an established codebase pattern.

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse Phase 39 wording exactly | Same flash strings verbatim regardless of which screen triggered creation. | ✓ |
| Let Claude decide | | |

**User's choice:** Reuse Phase 39 wording exactly
**Notes:** Consistent with Phase 39's original design intent — applied identically across both callers.

---

## Mobile layout

| Option | Description | Selected |
|--------|-------------|----------|
| Stacked sections (Members, then Add Users) | Members list on top, then search + non-member cards, then Create New User below. | ✓ |
| Tab/toggle switch between the two lists | Saves scrolling, but no precedent for tabs in this codebase's mobile views. | |
| Let Claude decide | | |

**User's choice:** Stacked sections (Members, then Add Users)
**Notes:** No new mobile interaction pattern introduced.

| Option | Description | Selected |
|--------|-------------|----------|
| Same modal on mobile | Bootstrap modals already render responsively elsewhere (ShopManagement.Mobile). | |
| Separate full-page form on mobile | Mirrors the existing desktop/mobile split pattern (e.g. Manage.cshtml vs Manage.Mobile.cshtml). | |
| Let Claude decide | ✓ | ✓ |

**User's choice:** Let Claude decide
**Notes:** Deferred to planning/implementation — recorded as D-09 in CONTEXT.md.

---

## Claude's Discretion

- Exact non-member repository query/method name for the group-scoped, search-filtered "users not in this group" list.
- Exact new Platform action name/route for the create-user entry point (must accept `groupId` from the route, never from `IActiveGroupContext`).
- Whether the per-row inline "Add" control needs its own antiforgery-protected mini-form per row, or a single delegated form/JS submit.
- Whether the Create New User modal stays a modal on mobile or becomes a separate full-page form.

## Deferred Ideas

None — discussion stayed within phase scope.
