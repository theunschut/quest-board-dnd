# Roadmap: D&D Quest Board

## Milestones

- ✅ **v1.0 Architecture & Features** — Phases 1–7, 9 (shipped prior to 2026-06)
- 🚧 **v2.0 Omphalos Integration** — Phases 10–11 (in progress — branch: `milestone/3-omphalos-integration`)
- ✅ **v3.0 Mobile Version** — Phases 12–19 (shipped 2026-06-25)
- ✅ **v4.0 Email Notifications** — Phases 20–25 (shipped 2026-06-28)
- ✅ **v5.0 Multi-Tenancy** — Phases 26–34.3 (shipped 2026-07-02)
- ✅ **v6.0 Board Types (Campaign Mode)** — Phases 35–37 (shipped 2026-07-03)
- ✅ **v6.1 Bugfixes** — Phases 38–42 (shipped 2026-07-04)
- 🚧 **v7.0 Backlog Cleanup** — Phases 43–46 (in progress)

_Note: Phase 8 (profile picture avatar crop) was scoped in v1.0 but deferred; issue #78 is now delivered by v7.0 Phases 45–46._

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
- [ ] Phase 8: Profile Picture Avatar Crop — deferred (SkiaSharp native lib unverified on host); delivered by v7.0 Phases 45–46
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

<details>
<summary>✅ v6.1 Bugfixes (Phases 38–42) — SHIPPED 2026-07-04</summary>

- [x] Phase 38: Group-Scoped User List — Group admin's Users page shows only members of the active group, closing a cross-tenant PII leak (completed 2026-07-03)
- [x] Phase 39: Shared Collision-Aware User Creation & Email — Creating a user with an existing email adds them to the group instead of failing, with a distinct notification email, applied identically everywhere user creation happens (completed 2026-07-04)
- [x] Phase 40: Platform Members Page Redesign — Two-column Members page with a searchable available-users table and a group-scoped Create New User entry point (completed 2026-07-04)
- [x] Phase 41: Safe User Removal & Account Disable — Group-admin Delete removes from the active group only (not a hard account delete), and SuperAdmin can disable/re-enable an account via Identity's lockout mechanism (completed 2026-07-04)
- [x] Phase 42: Site-Wide Toast Notification Redesign — Convert all flash messages app-wide from static alert banners to Bootstrap toast notifications, matching the Shop view's existing local toast pattern (deferred from Phase 39) (completed 2026-07-04)

</details>

<details open>
<summary>🚧 v7.0 Backlog Cleanup (Phases 43–46) — IN PROGRESS</summary>

**Overview:** Close out four standing backlog items — two mobile UI bugs (#115, #116), post-finalization vote flexibility with waitlist auto-promotion for One-Shot quests (#104), and client-side crop-before-save for character/DM profile photos with dual original+cropped storage (#78, deferred since v1.0).

- [x] Phase 43: Mobile Parity Fixes — Fix the iOS Safari fixed-background scroll bug and add the missing Session Recap badge to the mobile Quest Log (completed 2026-07-04)
- [ ] Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion — Players can vote after finalization, join a waitlist, and get auto-promoted with a targeted email
- [ ] Phase 45: Dual-Image Storage Backend — Server stores both an original and a cropped image per upload, with zero server-side image processing
- [ ] Phase 46: Client-Side Crop UI — Users crop character/DM profile photos in-browser before saving, with the crop applied everywhere a photo can be uploaded

</details>

## Phase Details

### Phase 43: Mobile Parity Fixes

**Goal**: Mobile users get the same visual behavior and information as desktop users on the two screens where they currently don't
**Depends on**: Nothing (independent of all other v7.0 work)
**Requirements**: MOBILE-01, MOBILE-02
**Success Criteria** (what must be TRUE):

  1. On an iOS Safari session (real device or real-device cloud — devtools emulation does not count), the page background stays visually fixed in place while content scrolls over it, instead of scrolling with the content
  2. The mobile Quest Log list view shows a "Session Recap Available" badge on any quest that has a recap, matching what desktop already shows
  3. Both fixes are verified against actual mobile browser behavior (not just responsive-mode screenshots), since this exact bug class has escaped desktop/emulation testing before in this codebase

**Plans**: 2/2 plans complete

- [x] 43-01-PLAN.md — Fix iOS Safari fixed-background scroll bug via body::before layer in site.css + mobile.css (MOBILE-01)
- [x] 43-02-PLAN.md — Add "Session Recap Available" amber/gold badge to mobile Quest Log (Index.Mobile.cshtml + quest-log.mobile.css) (MOBILE-02)

**UI hint**: yes

### Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion

**Goal**: Players can still respond after a One-Shot quest is finalized, capacity is never a hard wall, and the right — and only the right — player is notified when a seat opens up
**Depends on**: Nothing (independent of mobile fixes and image work — different tables entirely)
**Requirements**: VOTE-01, VOTE-02, VOTE-03, VOTE-04, VOTE-05, VOTE-06, VOTE-07
**Success Criteria** (what must be TRUE):

  1. A player can vote Yes on a finalized, fully-seated One-Shot quest and lands on a waitlist instead of getting rejected
  2. The waitlist visibly orders candidates by vote (Yes above Maybe above No) and then by how recently they signed up or changed their vote
  3. When a selected player votes No or fully revokes their signup, their seat frees up and the top waitlisted candidate is automatically promoted into it
  4. A selected player who changes their vote to Maybe keeps their seat with no promotion triggered, and a waitlisted player who votes No stays on the waitlist (not removed), sorted to the bottom
  5. Only the player who was passively auto-promoted receives a notification email — never the player whose action freed the seat, and never broadcast to the rest of the waitlist

**Plans**: 1/3 plans executed
**Wave 1**

- [x] 44-01-PLAN.md — Data foundation: LastVoteChangeTime migration, generalized ChangeVoteAsync (fixes VoteType bug) + GetTopWaitlistedCandidateAsync, centralized WaitlistOrdering (VOTE-01/02/03)

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 44-02-PLAN.md — Promotion orchestration in QuestService + single-recipient WaitlistPromoted email pipeline (VOTE-04/05/07)

**Wave 3** *(blocked on Wave 2 completion)*

- [ ] 44-03-PLAN.md — ChangeVote controller action + desktop & mobile 3-button vote UI with shared ordering (VOTE-01/02/04/05/06/07, D-01/02/03/04/05)

### Phase 45: Dual-Image Storage Backend

**Goal**: The application can accept, store, and serve two versions (original and cropped) of any uploaded character or DM profile photo, entirely without server-side image processing
**Depends on**: Nothing (independent of Phases 43–44; must land before Phase 46)
**Requirements**: IMAGE-02, IMAGE-03
**Success Criteria** (what must be TRUE):

  1. Uploading a character photo or DM profile photo persists both the original file bytes and a second image (posted from the client) as two distinct stored values, with no data loss on either
  2. Re-uploading a new photo atomically replaces both the original and cropped values together — there is never a state where one is updated and the other still reflects a prior upload
  3. No server-side image-decoding or image-processing library (SkiaSharp, ImageSharp, Magick.NET, etc.) is added to the project — the server only validates and stores the byte arrays it receives
  4. The two stored images are independently retrievable (e.g. via distinct repository/service calls), ready for Phase 46 to wire into the character-details ("show original") and guild-member-list ("show cropped") pages

**Plans**: TBD

### Phase 46: Client-Side Crop UI

**Goal**: Users see and control exactly how their character and DM profile photos are framed before saving, and the rest of the app shows the right version (cropped vs. original) in the right place
**Depends on**: Phase 45 (needs the dual-image storage path already working)
**Requirements**: IMAGE-01, IMAGE-04, IMAGE-05
**Success Criteria** (what must be TRUE):

  1. On every image-upload field in the app (character photo, DM profile photo — create and edit, desktop and mobile), the user sees an interactive crop frame (Cropper.js v2.1.1) they can drag, resize, and zoom over their photo before saving
  2. Saving a photo submits both the original and the cropped result in one ordinary form submission, with no separate upload step or page reload
  3. The guild-member list page displays the cropped image for each character; the character details page and DM profile details page display the original, unmodified image
  4. On a real touchscreen device, the crop frame responds correctly to drag and pinch gestures, a real phone-camera photo crops with correct orientation (not sideways/upside-down), and a full-resolution camera photo does not crash or blank the crop canvas on iOS Safari — each verified on a real device, not devtools emulation

**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 35 → 36 → 37 → 38 → 39 → 40 → 41 → 42 → 43 → 44 → 45 → 46

Phases 43 and 44 have no dependency on each other or on 45/46 and may be sequenced in either order. Phase 46 depends on Phase 45.

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
| 38. Group-Scoped User List | v6.1 | 1/1 | Complete    | 2026-07-03 |
| 39. Shared Collision-Aware User Creation & Email | v6.1 | 3/3 | Complete    | 2026-07-03 |
| 40. Platform Members Page Redesign | v6.1 | 3/3 | Complete    | 2026-07-04 |
| 41. Safe User Removal & Account Disable | v6.1 | 4/4 | Complete    | 2026-07-04 |
| 42. Site-Wide Toast Notification Redesign | v6.1 | 5/5 | Complete    | 2026-07-04 |
| 43. Mobile Parity Fixes | v7.0 | 2/2 | Complete    | 2026-07-04 |
| 44. Post-Finalization Voting & Waitlist Auto-Promotion | v7.0 | 1/3 | In Progress|  |
| 45. Dual-Image Storage Backend | v7.0 | 0/? | Not started | — |
| 46. Client-Side Crop UI | v7.0 | 0/? | Not started | — |

### Phase 47: Group Membership Email Notification Fix: adding an existing user to a group via the Platform area's GroupController.AddMember action sends no email notification, unlike the CreateMember action in the same controller and AdminController.CreateUser, which both already enqueue GroupMembershipAddedEmailJob

**Goal:** A SuperAdmin who adds an existing user to a group via the Platform Members-page available-users panel (`GroupController.AddMember`) receives a notification email — the confirmed-account "you've been added to {group}" email or, for a stranded/unconfirmed account, the welcome email with a set-password link — by rerouting `AddMember` through the same `UserService.CreateOrAddToGroupAsync` shared method that `CreateMember` and `AdminController.CreateUser` already use, so all three add-to-group entry points behave identically.
**Requirements**: None (ad-hoc bug-fix phase — no REQ-IDs; source of truth is 47-CONTEXT.md decisions D-01–D-06)
**Depends on:** Phase 46
**Plans:** 1/1 plans complete

Plans:

- [x] 47-01-PLAN.md — Reroute AddMember through CreateOrAddToGroupAsync with per-outcome email dispatch, plus regression tests asserting the correct email job is enqueued

### Phase 48: Add an Open Board action to the /platform group index table, reusing GroupPicker functionality so DMs can jump straight to a group's quest board without navigating through Members/Edit first

**Goal:** A SuperAdmin can click an "Open Board" button on any group row at `/platform/group` (desktop and mobile) to set that group active and land directly on its quest board, reusing `GroupPickerController.SelectGroup` verbatim — no `/groups/pick` detour and no new backend code.
**Requirements**: TBD (ad-hoc phase — no REQ-IDs; source of truth is 48-CONTEXT.md decisions D-01–D-04)
**Depends on:** Phase 47
**Plans:** 1/1 plans complete

Plans:

- [x] 48-01-PLAN.md — Add "Open Board" POST-form button (left of Members) to the desktop and mobile Platform Group index views, plus human verification
