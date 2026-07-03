# Roadmap: D&D Quest Board

## Milestones

- ✅ **v1.0 Architecture & Features** — Phases 1–7, 9 (shipped prior to 2026-06)
- 🚧 **v2.0 Omphalos Integration** — Phases 10–11 (in progress — branch: `milestone/3-omphalos-integration`)
- ✅ **v3.0 Mobile Version** — Phases 12–19 (shipped 2026-06-25)
- ✅ **v4.0 Email Notifications** — Phases 20–25 (shipped 2026-06-28)
- ✅ **v5.0 Multi-Tenancy** — Phases 26–34.3 (shipped 2026-07-02)
- ✅ **v6.0 Board Types (Campaign Mode)** — Phases 35–37 (shipped 2026-07-03)
- 🚧 **v6.1 Bugfixes** — Phases 38–40 (in progress — started 2026-07-03)

_Note: Phase 8 (profile picture avatar crop) was scoped in v1.0 but deferred; it is not assigned to any active milestone._

## Phases

<details>
<summary>✅ v1.0 Architecture & Features (Phases 1–7, 9) — SHIPPED prior to 2026-06</summary>

**Overview:** Restored correct layer boundaries (Domain ← Repository), consolidated business logic into services, removed dead code and security gaps, then added four backlog features on the clean architecture. Phase 8 (avatar crop) was deferred.

- [x] Phase 1: Layer Dependency Fix — 2/2 plans — complete
- [x] Phase 2: Email & Service Consolidation — 3/3 plans — complete
- [x] Phase 3: Code Quality & Dead Code — 2/2 plans — complete
- [x] Phase 4: Security Hardening — 4/4 plans — complete
- [x] Phase 5: Shop Filter & Sort — 2/2 plans — completed 2026-04-21
- [x] Phase 6: Follow-Up Quest — 2/2 plans — completed 2026-06-16
- [x] Phase 7: DM Profile Page — 2/2 plans — completed 2026-06-17
- [ ] Phase 8: Profile Picture Avatar Crop — deferred (SkiaSharp native lib unverified on host)
- [x] Phase 9: Shop Pagination — 2/2 plans — complete

</details>

<details>
<summary>🚧 v2.0 Omphalos Integration (Phases 10–11) — IN PROGRESS (branch: milestone/3-omphalos-integration)</summary>

**Overview:** Integrates the Omphalos SSO system for guest navigation token generation. Work is on a separate branch and will be merged after v4.0 lands on main.

- [ ] Phase 10: Omphalos Integration (details on branch `milestone/3-omphalos-integration`)
- [ ] Phase 11: Navigation Token Generation (details on branch `milestone/3-omphalos-integration`)

</details>

<details>
<summary>✅ v3.0 Mobile Version (Phases 12–19) — SHIPPED 2026-06-25</summary>

**Overview:** Added purpose-built `.Mobile.cshtml` view variants alongside all desktop views via a mobile detection middleware + view-location expander. No controllers, ViewModels, repositories, or domain services were modified.

- [x] Phase 12: Mobile Infrastructure — 3/3 plans — completed 2026-06-24
- [x] Phase 13: Core Player Views — 4/4 plans — completed 2026-06-24
- [x] Phase 14: Calendar — 3/3 plans — completed 2026-06-24
- [x] Phase 15: DM Views — 4/4 plans — completed 2026-06-24
- [x] Phase 16: Account & Browse — 4/4 plans — completed 2026-06-25
- [x] Phase 17: Character & Player Views — 4/4 plans — completed 2026-06-25
- [x] Phase 18: DM Editing & Secondary Quest Views — 5/5 plans — completed 2026-06-25
- [x] Phase 19: Admin & Shop Management Views — 7/7 plans — completed 2026-06-25

</details>

<details>
<summary>✅ v4.0 Email Notifications (Phases 20–25) — SHIPPED 2026-06-28</summary>

**Overview:** Styled HTML email templates (Razor + HtmlRenderer), Hangfire background jobs for automated session reminders, admin email stats dashboard backed by Resend REST API, and email confirmation flow with admin resend button.

- [x] Phase 20: Hangfire Infrastructure — 4/4 plans — completed 2026-06-25
- [x] Phase 21: HTML Email Templates — 4/4 plans — completed 2026-06-26
- [x] Phase 22: Session Reminders — 5/5 plans — completed 2026-06-26
- [x] Phase 23: Admin Email Stats — 2/2 plans — completed 2026-06-27
- [x] Phase 24: Email Confirmation Flow — 5/5 plans — completed 2026-06-26
- [x] Phase 25: Confirmation Email Razor Template — 2/2 plans — completed 2026-06-27

</details>

<details>
<summary>✅ v5.0 Multi-Tenancy (Phases 26–34.3) — SHIPPED 2026-07-02</summary>

**Overview:** Transform the Quest Board from a single-tenant EuphoriaInn app into a generic, rebrandable multi-group platform. Namespace rename, group schema with EF Core Global Query Filters, SuperAdmin role and management area, group-picker UX, admin-only user creation, auth lockdown with public landing page, first-login password flow, session persistence across app restarts, and a closing codebase cleanup/security pass. Full phase-level detail archived to `.planning/milestones/v5.0-ROADMAP.md`.

- [x] Phase 26: Namespace Rename — Rename all EuphoriaInn.* namespaces and project files to QuestBoard.* with zero behavior change (completed 2026-06-29)
- [x] Phase 27: Group Schema Foundation — GroupEntity + UserGroups junction table + GroupId FKs + data migration seeding EuphoriaInn group (completed 2026-06-30)
- [x] Phase 28: Tenant Isolation — IActiveGroupContext + EF Core Global Query Filters + Hangfire adaptation + test factory stub (completed 2026-06-30)
- [x] Phase 29: SuperAdmin Role & Management Area — SuperAdmin Identity role + updated authorization handlers + /platform MVC Area for group management (completed 2026-06-30)
- [x] Phase 30: Group UX & Admin User Creation — Group-picker flow + navigation + self-registration removal + admin user creation (completed 2026-06-30)
- [x] Phase 31: Unauthenticated Landing Redirect — Auth lockdown on group-scoped pages + public landing page at / + quest board moved to /quests + session-recovery middleware (completed 2026-07-01)
- [x] Phase 32: First-Login Password Flow — Admin-created users set their own password via a welcome email link; removes admin-set password from CreateUser form; adds a self-service Forgot Password flow (completed 2026-07-01)
- [x] Phase 33: Session Persistence & Admin Email Rate Limiting — ActiveGroupId survives app restarts via AddDistributedSqlServerCache; admin email resend buttons rate-limited 3/hour per target user (completed 2026-07-01)
- [x] Phase 34: Codebase Cleanup & Security Hardening (Mechanical Cleanup slice) — Remove dead code, strip GSD-ID/phase comment tags codebase-wide, backfill XML docs on all 35 interfaces, capture clean dependency scan — 5 plans (Wave 1, parallel) (completed 2026-07-01)
- [x] Phase 34.1: Security & Bugs (INSERTED) — fix Known Bugs and Security Considerations items plus related Test Coverage Gaps: verify-and-close the stale SessionReminderJob null-dereference claim; Resend 429 retry-backoff; CSRF regression test; secret-logging verification (completed 2026-07-02)
- [x] Phase 34.2: Performance & Architecture (INSERTED) — QuestController/AdminController cleanup via selective service-layer extraction + net-new MVC-boilerplate helpers, composite index + shop-query projection + Hangfire AutomaticRetryAttribute, HangfireJobHelper scope helper, ActiveGroupId null-guard — 5 plans (2 waves), depends on Phase 34 and 34.1 (completed 2026-07-02)
- [x] Phase 34.3: Group Role Authorization Regression Fix (INSERTED, URGENT) — Inline IsInRole checks across QuestController, QuestLogController, DungeonMasterController, and Admin/AccountController were never migrated to GroupRole after Phase 27 deleted Player/DungeonMaster/Admin rows from AspNetUserRoles; regression discovered post-milestone, pre-ship — 6 plans (3 waves) (completed 2026-07-02)

</details>

<details>
<summary>✅ v6.0 Board Types (Campaign Mode) (Phases 35–37) — SHIPPED 2026-07-03</summary>

**Overview:** Let a DM choose a group's quest-board type at creation — the existing One-Shot board (date voting + finalization) or a new Campaign board for ongoing games with a fixed party — without forking the controller/service/view stack. Full phase-level detail archived to `.planning/milestones/v6.0-ROADMAP.md`.

- [x] Phase 35: Board Type Configuration — SuperAdmin sets a group's board type at creation; locked afterward (completed 2026-07-03)
- [x] Phase 36: Campaign Quest Posting & Closing — DM can post, close, and reopen campaign quests with no date voting or per-quest signup, and no quest-related emails are sent (completed 2026-07-03)
- [x] Phase 37: Navigation & Access Control — Board-type-aware nav visibility, plus Email Stats locked to SuperAdmin only (completed 2026-07-03)

</details>

<details open>
<summary>🚧 v6.1 Bugfixes (Phases 38–40) — IN PROGRESS (started 2026-07-03)</summary>

**Overview:** Closes three admin-facing user-management gaps found after v6.0 shipped: a group admin user-list PII leak (`AdminController.Users()` shows every platform user, not just the active group's), a raw `<select>` dropdown on the Platform Members page with no create-user entry point, and a hard-fail on email collision during user creation instead of a friendlier auto-add-to-group flow. All three are additive extensions of existing seams — zero new packages, zero new migrations.

**Requirements:** USERS-01, CREATE-01 through CREATE-04, MEMBERS-01 through MEMBERS-03 (8 total, see `.planning/REQUIREMENTS.md`)

- [ ] Phase 38: Group-Scoped User List — Group admin's Users page shows only members of the active group, closing a cross-tenant PII leak
- [ ] Phase 39: Shared Collision-Aware User Creation & Email — Creating a user with an existing email adds them to the group instead of failing, with a distinct notification email, applied identically everywhere user creation happens
- [ ] Phase 40: Platform Members Page Redesign — Two-column Members page with a searchable available-users table and a group-scoped Create New User entry point

### Phase 38: Group-Scoped User List
**Goal**: A group admin viewing the Users management page sees only members of their currently active group — no other group's users are visible.
**Depends on**: Nothing (first phase; fully independent read-path fix)
**Requirements**: USERS-01
**Success Criteria** (what must be TRUE):
  1. A group admin opening the Users page sees only users who belong to the currently active group
  2. A group admin never sees a user from a different group on the Users page, even when multiple groups share the platform
  3. Each listed user still shows their correct role within the active group (no regression from the existing per-user role display)
**Plans**: TBD

### Phase 39: Shared Collision-Aware User Creation & Email
**Goal**: Creating a user whose email already exists on the platform adds that person to the target group instead of failing, and everyone affected is notified appropriately — with identical behavior regardless of which screen triggered the creation.
**Depends on**: Nothing structurally, but must complete before Phase 40 (Platform's create-user entry point reuses this phase's shared method)
**Requirements**: CREATE-01, CREATE-02, CREATE-03, CREATE-04
**Success Criteria** (what must be TRUE):
  1. An admin submitting the Create User form with an email that belongs to an existing user (not yet in this group) adds that user to the group with the selected role, instead of showing a duplicate-account error
  2. A user added to a group via the email-collision path receives a "you've been added to a group" email that is visibly distinct from the new-account welcome email and contains no set-password link
  3. An admin submitting the Create User form with an email that already belongs to a member of the current group sees a friendly "already a member" message, not a duplicate-membership error
  4. A brand-new email address still creates a new account and sends the existing welcome email, unchanged
  5. The group-admin Create User form and the (not-yet-built) platform-level create-user entry point both exhibit identical collision behavior once Phase 40 wires the platform entry point onto this phase's shared method
**Plans**: TBD

### Phase 40: Platform Members Page Redesign
**Goal**: A SuperAdmin managing a group's membership sees current members and searchable non-member users side by side, and can create a new user scoped to that group without leaving the page.
**Depends on**: Phase 39 (reuses the shared collision-aware creation method for its Create New User entry point)
**Requirements**: MEMBERS-01, MEMBERS-02, MEMBERS-03
**Success Criteria** (what must be TRUE):
  1. The Platform group Members page shows a two-column layout — current group members on the left, other platform users on the right
  2. The right-hand column lets a SuperAdmin filter the non-member user list by typing part of a name or email, replacing the plain dropdown select
  3. A SuperAdmin can add a listed non-member user to the group directly from the right-hand column
  4. The right-hand column has a "Create New User" entry point that creates or adds (per the Phase 39 collision behavior) a user scoped to the group being managed, without requiring a session-level active group
**Plans**: TBD
**UI hint**: yes

</details>

## Progress

**Execution Order:**
Phases execute in numeric order: 35 → 36 → 37 → 38 → 39 → 40

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Layer Dependency Fix | v1.0 | 2/2 | Complete | — |
| 2. Email & Service Consolidation | v1.0 | 3/3 | Complete | — |
| 3. Code Quality & Dead Code | v1.0 | 2/2 | Complete | — |
| 4. Security Hardening | v1.0 | 4/4 | Complete | — |
| 5. Shop Filter & Sort | v1.0 | 2/2 | Complete | 2026-04-21 |
| 6. Follow-Up Quest | v1.0 | 2/2 | Complete | 2026-06-16 |
| 7. DM Profile Page | v1.0 | 2/2 | Complete | 2026-06-17 |
| 8. Profile Picture Avatar Crop | v1.0 | 0/? | Deferred | — |
| 9. Shop Pagination | v1.0 | 2/2 | Complete | — |
| 10. Omphalos Integration | v2.0 | — | In progress (other branch) | — |
| 11. Navigation Token Generation | v2.0 | — | In progress (other branch) | — |
| 12. Mobile Infrastructure | v3.0 | 3/3 | Complete | 2026-06-24 |
| 13. Core Player Views | v3.0 | 4/4 | Complete | 2026-06-24 |
| 14. Calendar | v3.0 | 3/3 | Complete | 2026-06-24 |
| 15. DM Views | v3.0 | 4/4 | Complete | 2026-06-24 |
| 16. Account & Browse | v3.0 | 4/4 | Complete | 2026-06-25 |
| 17. Character & Player Views | v3.0 | 4/4 | Complete | 2026-06-25 |
| 18. DM Editing & Secondary Quest Views | v3.0 | 5/5 | Complete | 2026-06-25 |
| 19. Admin & Shop Management Views | v3.0 | 7/7 | Complete | 2026-06-25 |
| 20. Hangfire Infrastructure | v4.0 | 4/4 | Complete | 2026-06-25 |
| 21. HTML Email Templates | v4.0 | 4/4 | Complete | 2026-06-26 |
| 22. Session Reminders | v4.0 | 5/5 | Complete | 2026-06-26 |
| 23. Admin Email Stats | v4.0 | 2/2 | Complete | 2026-06-27 |
| 24. Email Confirmation Flow | v4.0 | 5/5 | Complete | 2026-06-26 |
| 25. Confirmation Email Razor Template | v4.0 | 2/2 | Complete | 2026-06-27 |
| 26. Namespace Rename | v5.0 | 2/2 | Complete | 2026-06-29 |
| 27. Group Schema Foundation | v5.0 | 3/3 | Complete | 2026-06-30 |
| 28. Tenant Isolation | v5.0 | 3/3 | Complete | 2026-06-30 |
| 29. SuperAdmin Role & Management Area | v5.0 | 5/5 | Complete | 2026-06-30 |
| 30. Group UX & Admin User Creation | v5.0 | 5/5 | Complete | 2026-06-30 |
| 31. Unauthenticated Landing Redirect | v5.0 | 4/4 | Complete | 2026-07-01 |
| 32. First-Login Password Flow | v5.0 | 5/5 | Complete | 2026-07-01 |
| 33. Session Persistence & Admin Email Rate Limiting | v5.0 | 3/3 | Complete | 2026-07-01 |
| 34. Codebase Cleanup & Security Hardening | v5.0 | 5/5 | Complete | 2026-07-01 |
| 34.1. Security & Bugs | v5.0 | 2/2 | Complete | 2026-07-02 |
| 34.2. Performance & Architecture | v5.0 | 5/5 | Complete | 2026-07-02 |
| 34.3. Group Role Authorization Regression Fix | v5.0 | 6/6 | Complete | 2026-07-02 |
| 35. Board Type Configuration | v6.0 | 3/3 | Complete    | 2026-07-03 |
| 36. Campaign Quest Posting & Closing | v6.0 | 5/5 | Complete    | 2026-07-03 |
| 37. Navigation & Access Control | v6.0 | 3/3 | Complete    | 2026-07-03 |
| 38. Group-Scoped User List | v6.1 | 0/TBD | Not started | — |
| 39. Shared Collision-Aware User Creation & Email | v6.1 | 0/TBD | Not started | — |
| 40. Platform Members Page Redesign | v6.1 | 0/TBD | Not started | — |
