# Requirements: D&D Quest Board

**Defined:** 2026-07-03
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

## v6.1 Requirements

Requirements for the Bugfixes milestone. Each maps to roadmap phases.

### Group-Scoped User List

- [ ] **USERS-01**: Group admin's Users management page shows only members of the currently active group, not all platform users

### Shared User Creation & Email-Collision Handling

- [ ] **CREATE-01**: Creating a user with an email that already belongs to an existing platform user adds that user to the active group with the selected role, instead of failing with a duplicate-account error
- [ ] **CREATE-02**: A user added to a group via the email-collision path receives a "you've been added to a group" notification email, distinct from the new-account welcome email (no set-password link)
- [ ] **CREATE-03**: Creating a user with an email that already belongs to a member of the current group shows a friendly "already a member" message instead of a duplicate-membership error
- [ ] **CREATE-04**: Email-collision handling behaves identically whether triggered from the group-admin Create User form or the new platform-level Create User entry point

### Platform Members Page Redesign

- [ ] **MEMBERS-01**: The Platform group Members page uses a two-column layout — left column shows current group members, right column shows other (non-member) users with an "Add User" action
- [ ] **MEMBERS-02**: The right-hand "other users" list is a searchable/filterable table (filter by name or email) instead of a plain dropdown select
- [ ] **MEMBERS-03**: The Platform group Members page has a "Create New User" entry point (in the right column) that creates (or, per CREATE-01, adds) a user scoped to the group being managed

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Confirming interstitial before collision auto-add | Milestone spec calls for silent auto-add + after-the-fact notification, not a consent step |
| JS grid/DataTables library or AJAX search for the Members table | App has zero client-side JS dependencies today; overkill at ~17 users |
| Pagination on the Members table | Not needed at current/near-term scale |
| Cross-group visibility banner/switcher on the group-admin Users page | Polish; revisit only if verification surfaces confusion |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| USERS-01 | Phase 38 | Pending |
| CREATE-01 | Phase 39 | Pending |
| CREATE-02 | Phase 39 | Pending |
| CREATE-03 | Phase 39 | Pending |
| CREATE-04 | Phase 39 | Pending |
| MEMBERS-01 | Phase 40 | Pending |
| MEMBERS-02 | Phase 40 | Pending |
| MEMBERS-03 | Phase 40 | Pending |

**Coverage:**
- v6.1 requirements: 8 total
- Mapped to phases: 8
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-03*
*Last updated: 2026-07-03 after roadmap creation (ROADMAP.md phases 38-40)*
