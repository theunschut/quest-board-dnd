# Requirements: D&D Quest Board

**Defined:** 2026-07-03
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

## v6.1 Requirements

Requirements for the Bugfixes milestone. Each maps to roadmap phases.

### Group-Scoped User List

- [x] **USERS-01**: Group admin's Users management page shows only members of the currently active group, not all platform users

### Shared User Creation & Email-Collision Handling

- [x] **CREATE-01**: Creating a user with an email that already belongs to an existing platform user adds that user to the active group with the selected role, instead of failing with a duplicate-account error
- [x] **CREATE-02**: A user added to a group via the email-collision path receives a "you've been added to a group" notification email, distinct from the new-account welcome email (no set-password link)
- [x] **CREATE-03**: Creating a user with an email that already belongs to a member of the current group shows a friendly "already a member" message instead of a duplicate-membership error
- [x] **CREATE-04**: Email-collision handling behaves identically whether triggered from the group-admin Create User form or the new platform-level Create User entry point

### Platform Members Page Redesign

- [x] **MEMBERS-01**: The Platform group Members page uses a two-column layout — left column shows current group members, right column shows other (non-member) users with an "Add User" action
- [x] **MEMBERS-02**: The right-hand "other users" list is a searchable/filterable table (filter by name or email) instead of a plain dropdown select
- [x] **MEMBERS-03**: The Platform group Members page has a "Create New User" entry point (in the right column) that creates (or, per CREATE-01, adds) a user scoped to the group being managed

### Safe User Removal & Account Disable

- [ ] **SAFE-01**: Group admin's "Delete" button on the Users page removes the user from the active group only — their account and any other group memberships stay intact
- [ ] **SAFE-02**: SuperAdmin can disable a user account so it can no longer log in, without deleting any account data
- [ ] **SAFE-03**: SuperAdmin can re-enable a previously disabled account
- [ ] **SAFE-04**: A disabled user attempting to log in sees an accurate message, not the existing "try again in 15 minutes" failed-attempts copy

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
| USERS-01 | Phase 38 | Complete |
| CREATE-01 | Phase 39 | Complete |
| CREATE-02 | Phase 39 | Complete |
| CREATE-03 | Phase 39 | Complete |
| CREATE-04 | Phase 39 | Complete |
| MEMBERS-01 | Phase 40 | Complete |
| MEMBERS-02 | Phase 40 | Complete |
| MEMBERS-03 | Phase 40 | Complete |
| SAFE-01 | Phase 41 | Pending |
| SAFE-02 | Phase 41 | Pending |
| SAFE-03 | Phase 41 | Pending |
| SAFE-04 | Phase 41 | Pending |

**Coverage:**

- v6.1 requirements: 12 total
- Mapped to phases: 12
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-03*
*Last updated: 2026-07-03 after adding Phase 41 (Safe User Removal & Account Disable)*
