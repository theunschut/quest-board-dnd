# Roadmap: D&D Quest Board

## Milestones

- ✅ **v1.0 Architecture & Features** — Phases 1–7, 9 (shipped prior to 2026-06)
- 🚧 **v2.0 Omphalos Integration** — Phases 10–11 (in progress — branch: `milestone/3-omphalos-integration`)
- ✅ **v3.0 Mobile Version** — Phases 12–19 (shipped 2026-06-25)
- ✅ **v4.0 Email Notifications** — Phases 20–25 (shipped 2026-06-28)
- ✅ **v5.0 Multi-Tenancy** — Phases 26–34.3 (shipped 2026-07-02)
- 🚧 **v6.0 Board Types (Campaign Mode)** — Phases 35–37 (in progress)

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
<summary>🚧 v6.0 Board Types (Campaign Mode) (Phases 35–37) — IN PROGRESS</summary>

**Overview:** Let a DM choose a group's quest-board type at creation — the existing One-Shot board (date voting + finalization) or a new Campaign board for ongoing games with a fixed party — without forking the controller/service/view stack.

- [x] Phase 35: Board Type Configuration — SuperAdmin sets a group's board type at creation; locked afterward (completed 2026-07-03)
- [x] Phase 36: Campaign Quest Posting & Closing — DM can post, close, and reopen campaign quests with no date voting or per-quest signup, and no quest-related emails are sent (completed 2026-07-03)
- [ ] Phase 37: Navigation & Access Control — Board-type-aware nav visibility, plus Email Stats locked to SuperAdmin only

</details>

## Phase Details

### Phase 35: Board Type Configuration

**Goal**: A SuperAdmin can set a group's board type (One-Shot or Campaign) when creating it, and that choice is permanent — the rest of the app can reliably branch on it.
**Depends on**: Nothing (first phase of v6.0; builds on existing `GroupEntity`/`Platform/GroupController` from v5.0)
**Requirements**: BOARD-01, BOARD-02
**Success Criteria** (what must be TRUE):

  1. SuperAdmin sees a One-Shot/Campaign choice on the group creation form and the selected value is saved on the group
  2. The board type is displayed (read-only) on the group edit/details view, with no control to change it after creation
  3. Attempting to submit a board-type value on the edit form has no effect — the group's stored `BoardType` is unchanged
  4. Existing groups created before this phase default to One-Shot with no behavior change

**Plans**: 3 plans

Plans:
**Wave 1**

- [x] 35-01-PLAN.md — Data foundation: BoardType enum, entity/domain/projection models, AutoMapper + repository projections, EF migration, and failing integration-test scaffolds

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 35-02-PLAN.md — ViewModels + GroupController wiring: required nullable BoardType on Create, display-only on Edit, Create POST persists, Edit POST ignores (tamper protection)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 35-03-PLAN.md — Razor views: Board Type dropdown (Create), disabled dropdown (Edit), badge column/card (Index), all desktop + mobile, plus human-verify checkpoint

**UI hint**: yes

### Phase 36: Campaign Quest Posting & Closing

**Goal**: A DM running a campaign-type group can post quests and manage their lifecycle (close/reopen) without any of the one-shot scheduling machinery, and campaign quests never trigger scheduling-related emails.
**Depends on**: Phase 35 (requires `BoardType` to exist and be readable on the group)
**Requirements**: CQUEST-01, CQUEST-02, CQUEST-03, CQUEST-04, CQUEST-05, CQUEST-06
**Success Criteria** (what must be TRUE):

  1. DM posting a quest in a campaign group sees no proposed-dates picker, and the quest saves successfully without dates
  2. A campaign quest's detail/manage page shows no signup or date-voting section — only quest content and a Close/Reopen control
  3. DM can close an open campaign quest via a single action, and it immediately disappears from the active quest board
  4. DM can reopen a closed campaign quest, and it immediately reappears on the active quest board
  5. A closed campaign quest appears in the Quest Log right away (no next-day wait), while one-shot finalized quests keep their existing next-day Quest Log behavior unchanged
  6. No email is sent (posted/reminder/finalized) for any quest action inside a campaign group — verified for post, close, and reopen

**Plans**: 5 plans

Plans:
**Wave 1**

- [x] 36-01-PLAN.md — Data foundation: IsClosed/ClosedDate on entity + domain model, EF migration, campaign-group + closed-quest test seeding

**Wave 2** *(blocked on Wave 1)*

- [x] 36-02-PLAN.md — Close/Reopen service+repository methods (no email), IsClosed-aware board filters + Quest Log OR-branch, unit tests (RED→GREEN)

**Wave 3** *(blocked on Wave 2)*

- [x] 36-03-PLAN.md — Close/Reopen controller actions + authz/CSRF, conditional Create validation, ViewBag.BoardType threading, Quest Log guard fixes, integration tests

**Wave 4** *(blocked on Wave 3; 36-04 and 36-05 run in parallel — no file overlap)*

- [x] 36-04-PLAN.md — Campaign board/Manage/Details/Create views (desktop + mobile): Open/Closed seal, CR/signup removal, Close/Reopen buttons, stripped Create form + human-verify
- [x] 36-05-PLAN.md — Campaign Quest Log views (desktop + mobile): CR/Adventurers removal, ClosedDate display, recap preserved + human-verify

**UI hint**: yes

### Phase 37: Navigation & Access Control

**Goal**: Campaign groups show only the nav items relevant to their board type, and the Email Stats page is restricted to SuperAdmin regardless of group type.
**Depends on**: Phase 35 (nav visibility branches on `BoardType`); independent of Phase 36
**Requirements**: NAV-01, NAV-02, NAV-03, NAV-04, NAV-05, NAV-06, ACCESS-01
**Success Criteria** (what must be TRUE):

  1. In a campaign group, the desktop and mobile nav both hide Calendar, Shop, "Manage Shop", "Edit My Profile", and "Players" — Guild member directory stays visible
  2. In a one-shot group, all nav items render exactly as before this phase (no regression)
  3. A user with the Admin role (not SuperAdmin) can no longer see the "Email Stats" nav link or load the Email Stats page — a direct URL request is rejected, not just hidden from nav
  4. A SuperAdmin can still see and load the Email Stats page in both one-shot and campaign groups

**Plans**: 3 plans

Plans:
**Wave 1** *(37-01 and 37-02 run in parallel — no file overlap)*

- [x] 37-01-PLAN.md — Foundation: extend IActiveGroupContext with GetBoardTypeAsync + settable BoardType test double + failing LayoutNavigationTests scaffold (RED)
- [x] 37-02-PLAN.md — Email Stats access control: SuperAdminOnly gate on EmailStats, AccessDenied action + ConfigureApplicationCookie wiring, generalized AccessDenied view, backend tests

**Wave 2** *(blocked on 37-01 and 37-02)*

- [ ] 37-03-PLAN.md — Layout nav gating (desktop + mobile): OneShot allowlist for the 5 campaign-hidden items, D-04 anonymous-Calendar fix, SuperAdmin Email Stats link gate, LayoutNavigationTests GREEN + human-verify checkpoint

**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 35 → 36 → 37

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
| 37. Navigation & Access Control | v6.0 | 2/3 | In Progress|  |
