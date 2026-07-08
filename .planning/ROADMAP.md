# Roadmap: D&D Quest Board

## Milestones

- ✅ **v1.0 Architecture & Features** — Phases 1–7, 9 (shipped prior to 2026-06)
- ✗ **v2.0 Omphalos Integration (abandoned attempt)** — Phases 10–11 (superseded — see redo below; branch `milestone/3-omphalos-integration` diverged too far from `main` to merge)
- ✅ **v3.0 Mobile Version** — Phases 12–19 (shipped 2026-06-25)
- ✅ **v4.0 Email Notifications** — Phases 20–25 (shipped 2026-06-28)
- ✅ **v5.0 Multi-Tenancy** — Phases 26–34.3 (shipped 2026-07-02)
- ✅ **v6.0 Board Types (Campaign Mode)** — Phases 35–37 (shipped 2026-07-03)
- ✅ **v6.1 Bugfixes** — Phases 38–42 (shipped 2026-07-04)
- ✅ **v7.0 Backlog Cleanup** — Phases 43–64 (shipped 2026-07-08)
- 🚧 **v2.0 Omphalos Integration (redo)** — Phases 65–67 (in progress — branch: `milestone/v2-omphalos-integration`; renumbered from 35–37 after v6.0/v6.1/v7.0 claimed those numbers while this milestone was deprioritized)

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
<summary>✗ v2.0 Omphalos Integration (Phases 10–11) — SUPERSEDED, abandoned attempt</summary>

**Overview:** First attempt at Omphalos SSO integration, on branch `milestone/3-omphalos-integration`. Forked before v3.0/v4.0/v5.0 landed on `main`; by the time work resumed, the branch's code had diverged too far to merge. The milestone is being redone from scratch — see "v2.0 Omphalos Integration (redo)" below, Phases 35+. The old branch's planning docs (research, requirements, HMAC token-format design) were reviewed as historical reference during the redo's research phase but the phases below were never completed against `main`.

- [ ] ~~Phase 10: Admin Settings~~ (abandoned — details on branch `milestone/3-omphalos-integration`)
- [ ] ~~Phase 11: Navigation Token Generation~~ (abandoned — details on branch `milestone/3-omphalos-integration`)

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

<details>
<summary>✅ v7.0 Backlog Cleanup (Phases 43–64) — SHIPPED 2026-07-08</summary>

**Overview:** Close out four standing backlog items — two mobile UI bugs (#115, #116), post-finalization vote flexibility with waitlist auto-promotion for One-Shot quests (#104), and client-side crop-before-save for character/DM profile photos with dual original+cropped storage (#78, deferred since v1.0) — plus 17 ad-hoc fixes and small features folded in along the way (Phases 47–64), including two real cross-tenant security leaks found and closed mid-milestone (Phases 49, 55). Full phase-level detail archived to `.planning/milestones/v7.0-ROADMAP.md`.

- [x] Phase 43: Mobile Parity Fixes — Fix the iOS Safari fixed-background scroll bug and add the missing Session Recap badge to the mobile Quest Log (completed 2026-07-04)
- [x] Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion — Players can vote after finalization, join a waitlist, and get auto-promoted with a targeted email (completed 2026-07-04)
- [x] Phase 45: Dual-Image Storage Backend — Server stores both an original and a cropped image per upload, with zero server-side image processing (completed 2026-07-07)
- [x] Phase 46: Client-Side Crop UI — Users crop character/DM profile photos in-browser before saving, with the crop applied everywhere a photo can be uploaded (completed 2026-07-07)
- [x] Phase 47: Group Membership Email Notification Fix — Reroute `GroupController.AddMember` through the shared `CreateOrAddToGroupAsync` method so adding an existing user to a group sends the same notification email `CreateMember`/`CreateUser` already send (completed 2026-07-04)
- [x] Phase 48: Open Board Action on Platform Group Index — Add an "Open Board" button to the `/platform/group` index table reusing `GroupPickerController.SelectGroup`, so a SuperAdmin can jump straight to a group's quest board (completed 2026-07-04)
- [x] Phase 49: Fix Guild Members page missing group/tenant filtering — Close cross-group leaks on GuildMembersController (Character list/details/picture), DungeonMasterController (DM profile view/edit/picture), and QuestController.RemovePlayerSignup; CharacterEntity gets a real GroupId column + query filter; UserTransaction and PlayerSignup incidental scoping hardened (completed 2026-07-05)
- [x] Phase 50: Fix quest edit page: show edit button for campaign quests and align field visibility with create page (completed 2026-07-05)
- [x] Phase 51: Change Guild Members page layout from two columns to two stacked rows so the growing Guild Roster section isn't width-constrained (completed 2026-07-05)
- [x] Phase 52: Add Dead status to CharacterStatus enum (completed 2026-07-06)
- [x] Phase 53: Add dedicated Edit view for Quest recap so Details page is view-only (completed 2026-07-06)
- [x] Phase 54: Fix mobile signup for finalized quests (inconsistent with desktop) (completed 2026-07-06)
- [x] Phase 55: Fix cross-tenant quest leak on quest board — Closed a SuperAdmin null-ActiveGroupId escape hatch (root cause), hardened 7 EF Core query filters to fail-closed, fixed a SelectGroup IDOR gap, and added interval-gated stale-membership re-validation (completed 2026-07-06)
- [x] Phase 56: Allow admins to edit characters owned by other players — Widened GuildMembersController's owner-only guard to owner-OR-admin across Edit/Delete/ToggleRetirement via a shared CanManageCharacterAsync helper (completed 2026-07-06)
- [x] Phase 57: Add an NPC directory — DM-only creation of group-bound NPCs (name, image, description, town/city, optional sub-location) with a player-and-DM-editable notes list, plus dedicated Index/Details/Edit views mirroring the Characters pattern (completed 2026-07-06)
- [x] Phase 58: Rename the Guild Members feature to Characters everywhere — Controller, routes, views, ViewModels, CSS, integration tests, and nav copy renamed so the terminology matches the Domain/Repository layers, with zero behavior change (completed 2026-07-06)
- [x] Phase 59: Add a rewards field to quests — Optional freeform Rewards textarea between Description and Challenge Rating on Create/Edit/Follow-Up (desktop + mobile), shown as a gold `fa-coins` boxed callout below Description on Quest Details and QuestLog Details, hidden when empty (completed 2026-07-06)
- [x] Phase 60: Stop creating AspNetUserRoles entries for new users — Remove the stale Player-role write in `CreateUserAsync`, delete the dead per-group Identity-role API on both service layers, and align the test auth helper to seed AspNetUserRoles only for SuperAdmin; per-group roles stay in `UserGroups.GroupRole`, SuperAdmin untouched, no migration (completed 2026-07-06)
- [x] Phase 61: Allow DMs to edit finalized quest details — Edit Title/Description/Rewards/Challenge Rating/Total Player Count/DM-Session-Only on a finalized OneShot quest via a new Edit Quest button on Manage, without touching Proposed Dates or the player roster (completed 2026-07-07)
- [x] Phase 62: Stop eagerly loading image bytes in list/entity queries — Character/Contact/DM-profile list and detail reads project a lightweight HasProfilePicture/HasContactImage boolean instead of `.Include`-ing the image byte[], matching QuestRepository's existing pattern (completed 2026-07-07)
- [x] Phase 63: Allow any player to edit quest recaps, not just the assigned DM or admin — Removed the DM/Admin gate from QuestLogController.EditRecap, splitting the shared CanEditRecap flag so the unrelated Manage Quest link stays DM/Admin-only via a new CanManageQuest flag (completed 2026-07-07)
- [x] Phase 64: Preserve line breaks in description text on mobile views to match desktop rendering — Added `white-space: pre-wrap` to 4 confirmed instances: mobile Characters Description/Backstory, desktop QuestLog Original Quest Description, Shop item Description (both platforms), and the shared quest-board list-card preview (completed 2026-07-07)

</details>

### 🚧 v2.0 Omphalos Integration (redo) (Phases 65–67) — IN PROGRESS

**Milestone Goal:** DMs can open Omphalos session notes for any quest with one click — navigated automatically into the correct session, authenticated via a short-lived signed token. Redo of the abandoned attempt (old Phase 10–11, superseded above); redone from scratch against current `main` since that branch diverged too far to merge cleanly. Originally slotted as Phases 35–37 when planned on 2026-07-02, then deprioritized while v6.0/v6.1/v7.0 shipped and claimed those numbers on `main`; renumbered to 65–67 to follow v7.0's Phase 64 when this milestone resumed.

- [ ] **Phase 65: Platform Settings + Token Contract** - SuperAdmin-configurable Omphalos URL/secret and the written HMAC token-format contract
- [ ] **Phase 66: Navigation + Token Generation** - Signed-URL token service and every Omphalos entry point (navbar link, quest-page buttons)
- [ ] **Phase 67: Omphalos SSO Endpoint + Session Linking** - Omphalos-side SSO validation, auto-provisioning, and quest↔session linking

## Phase Details

### Phase 65: Platform Settings + Token Contract
**Repo**: Quest Board (`C:\Repos\quest-board`)
**Goal**: SuperAdmin can configure the Omphalos integration (URL, shared secret, enabled toggle) instance-wide from the Platform area, and the HMAC token-format contract that Phases 66/67 depend on is written down and agreed before either side implements against it.
**Depends on**: Nothing (first phase of this milestone — no external dependency, lowest risk, built first per research)
**Requirements**: SETT-01, SETT-02, SETT-03, SETT-04, SETT-05, SETT-06, SETT-07, SETT-08
**Success Criteria** (what must be TRUE):
  1. SuperAdmin can navigate to an Omphalos Settings page under `/platform` and save a URL, a masked shared secret, and an enabled toggle
  2. Saving the settings form with the secret field left blank preserves the previously-saved secret rather than wiping it
  3. A non-SuperAdmin (Admin, DungeonMaster, Player) cannot reach the settings page
  4. Settings persist across app restarts in a single-row `IntegrationSetting` table created by an EF Core migration
  5. The HMAC canonical token-message contract (field order, encoding, delimiter, expiry inclusion, identity claim) exists as a written document copied into both repos' planning docs
**Plans**: 5 plans
Plans:
- [ ] 65-01-PLAN.md — Persistence foundation: IntegrationSettingEntity, DbContext config, domain model + interfaces, AddIntegrationSettings migration (SETT-06, SETT-08)
- [ ] 65-02-PLAN.md — Repository + domain service (blank-preserve secret guard, singleton bootstrap) + DI + unit tests (SETT-04, SETT-05, SETT-06)
- [ ] 65-03-PLAN.md — IntegrationsController + ViewModel + desktop/mobile Integrations views + mobile CSS (SETT-01, SETT-02, SETT-03, SETT-04, SETT-05, SETT-07)
- [ ] 65-04-PLAN.md — Group/Index nav button (desktop+mobile) + integration-test authorization matrix (SETT-01, SETT-07)
- [ ] 65-05-PLAN.md — Written .planning/TOKEN-CONTRACT.md HMAC token-format contract (TOKEN-02, written portion)
**UI hint**: yes

### Phase 66: Navigation + Token Generation
**Repo**: Quest Board (`C:\Repos\quest-board`)
**Goal**: Every Omphalos entry point in the UI (DM navbar link, quest-page buttons) generates a signed, time-limited redirect token through one shared service and lands the user in Omphalos — with no surface ever linking to Omphalos's raw base URL unsigned.
**Depends on**: Phase 65 (settings service must exist at compile time; token contract must be written)
**Requirements**: NAV-01, NAV-02, NAV-03, NAV-04, NAV-05, NAV-06, TOKEN-01, TOKEN-02, TOKEN-03, TOKEN-04, TOKEN-05, TOKEN-06
**Success Criteria** (what must be TRUE):
  1. DM/Admin sees an "Open DM Tool" link in the navbar dropdown and an "Open Session Notes" button on both the Quest Detail and Quest Manage pages, only when integration is enabled and configured
  2. Clicking any of these entry points shows a brief "Opening Omphalos…" transition, then redirects with a signed URL — never the raw configured base URL
  3. The generated token carries Quest Board's `UserEntity.Id` (never `Name`/`UserName`/email), the quest ID, title, and date, and expires 5 minutes after generation
  4. When integration is disabled or unconfigured, none of these entry points render, and directly hitting the `LaunchOmphalos` action returns a graceful response instead of a raw error
  5. A Player (non-DM/Admin) cannot trigger `LaunchOmphalos` even by direct URL — the action enforces `DungeonMasterOnly` independent of button visibility
**Plans**: TBD
**UI hint**: yes

### Phase 67: Omphalos SSO Endpoint + Session Linking
**Repo**: Omphalos (`C:\Repos\omphalos`) — owned/maintained by another GitHub identity; changes here go through that repo's normal PR review, not Quest Board's branch protection. Budget for review-latency slack: this is the actual critical path (external maintainer availability, not implementation effort), and the same "branch diverged before merge" failure mode killed the original attempt. Open the PR early and keep it small/reviewable. Confirm the exact SSO route path and live Postgres collation/unique-index state with the maintainer at the start of this phase — both are flagged LOW/unverified in research.
**Goal**: Omphalos validates an incoming Quest Board token, auto-provisions or matches the user deterministically by stable ID, finds-or-creates the linked game session, and signs the user into its own session — independently enforcing its own authorization rather than trusting that Quest Board's button was hidden.
**Depends on**: Phase 65's written token contract (no compile-time coupling to Phase 65/66 code — can run in parallel with Phase 66 once the contract exists)
**Requirements**: SSO-01, SSO-02, SSO-03, SSO-04, SSO-05, SSO-06, SSO-07, SSO-08, SSO-09, SSO-10, SSO-11, LINK-01, LINK-02, LINK-03, LINK-04
**Success Criteria** (what must be TRUE):
  1. Visiting the SSO endpoint with a valid, unexpired, unused signed token lands the user in Omphalos, authenticated via the existing `omphalos_token` JWT cookie, inside the correct session
  2. An invalid/missing signature returns HTTP 400; an expired token shows a friendly "link expired" message (not a bare 401/500); a previously-used token is rejected by replay protection
  3. A first-time Quest Board identity is auto-provisioned in Omphalos with a role mapped from their Quest Board role (DM/Admin → Omphalos Admin, Player → Omphalos Player); a repeat visit from the same identity matches the existing account via `QuestBoardUserId` and never creates a duplicate
  4. Re-opening the same quest's session notes lands in the same Omphalos `GameSession` every time (found via `ExternalQuestId`, not recreated); a first visit creates it titled/dated from the token payload
  5. If the Quest Board shared secret is absent from Omphalos's environment, the SSO endpoint alone returns a clear error while the rest of Omphalos continues operating normally for its own users
  6. The `omphalos_token` cookie is issued with `Secure=true`
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 35 → 36 → 37 → 38 → 39 → 40 → 41 → 42 → 43 → 44 → 45 → 46 → 47 → 48 → 49 → 50 → 51 → 52 → 53 → 54 → 55 → 56 → 57 → 58 → 59 → 60 → 61 → 62 → 63 → 64 → 65 → 66 → 67

Phases 43 and 44 have no dependency on each other or on 45/46 and may be sequenced in either order. Phase 46 depends on Phase 45. Phases 47–64 are ad-hoc additions folded in after the original v7.0 roadmap was created, each depending on the previous phase (Phase 62 depends on Phase 46 specifically, not Phase 61). Phases 57, 58, and 59 executed out of numeric order — Phase 58 (Characters rename) was planned and executed before Phase 57 (NPC directory); Phase 59 (Rewards field) was then planned and executed concurrently with Phase 57's still-in-progress execution. Phase 65 has no dependency on 64 (v7.0 is a separate, already-shipped milestone); Phase 66 depends on 65; Phase 67 depends only on 65's written token contract and may run in parallel with 66.

| Phase | Milestone | Plans Complete | Status | Completed |
| ------- | ----------- | ---------------- | -------- | ----------- |
| 1. Layer Dependency Fix | v1.0 | 2/2 | Complete | — |
| 2. Email & Service Consolidation | v1.0 | 3/3 | Complete | — |
| 3. Code Quality & Dead Code | v1.0 | 2/2 | Complete | — |
| 4. Security Hardening | v1.0 | 4/4 | Complete | — |
| 5. Shop Filter & Sort | v1.0 | 2/2 | Complete | 2026-04-21 |
| 6. Follow-Up Quest | v1.0 | 2/2 | Complete | 2026-06-16 |
| 7. DM Profile Page | v1.0 | 2/2 | Complete | 2026-06-17 |
| 8. Profile Picture Avatar Crop | v1.0 | 0/? | Deferred | — |
| 9. Shop Pagination | v1.0 | 2/2 | Complete | — |
| 10. Admin Settings | v2.0 (abandoned attempt) | — | Superseded — see Phase 65+ | — |
| 11. Navigation Token Generation | v2.0 (abandoned attempt) | — | Superseded — see Phase 65+ | — |
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
| 35. Board Type Configuration | v6.0 | 3/3 | Complete | 2026-07-03 |
| 36. Campaign Quest Posting & Closing | v6.0 | 5/5 | Complete | 2026-07-03 |
| 37. Navigation & Access Control | v6.0 | 3/3 | Complete | 2026-07-03 |
| 38. Group-Scoped User List | v6.1 | 1/1 | Complete | 2026-07-03 |
| 39. Shared Collision-Aware User Creation & Email | v6.1 | 3/3 | Complete | 2026-07-03 |
| 40. Platform Members Page Redesign | v6.1 | 3/3 | Complete | 2026-07-04 |
| 41. Safe User Removal & Account Disable | v6.1 | 4/4 | Complete | 2026-07-04 |
| 42. Site-Wide Toast Notification Redesign | v6.1 | 5/5 | Complete | 2026-07-04 |
| 43. Mobile Parity Fixes | v7.0 | 2/2 | Complete | 2026-07-04 |
| 44. Post-Finalization Voting & Waitlist Auto-Promotion | v7.0 | 3/3 | Complete | 2026-07-04 |
| 45. Dual-Image Storage Backend | v7.0 | 3/3 | Complete    | 2026-07-07 |
| 46. Client-Side Crop UI | v7.0 | 8/8 | Complete    | 2026-07-07 |
| 47. Group Membership Email Notification Fix | v7.0 | 1/1 | Complete | 2026-07-04 |
| 48. Open Board Action on Platform Group Index | v7.0 | 1/1 | Complete | 2026-07-04 |
| 49. Fix Guild Members page missing group/tenant filtering | v7.0 | 4/4 | Complete | 2026-07-05 |
| 50. Fix quest edit page: show edit button for campaign quests and align field visibility with create page | v7.0 | 3/3 | Complete | 2026-07-05 |
| 51. Change Guild Members page layout from two columns to two stacked rows | v7.0 | 1/1 | Complete | 2026-07-05 |
| 52. Add Dead status to CharacterStatus enum | v7.0 | 1/1 | Complete | 2026-07-06 |
| 53. Add dedicated Edit view for Quest recap so Details page is view-only | v7.0 | 2/2 | Complete | 2026-07-06 |
| 54. Fix mobile signup for finalized quests (inconsistent with desktop) | v7.0 | 2/2 | Complete | 2026-07-06 |
| 55. Fix cross-tenant quest leak on quest board | v7.0 | 4/4 | Complete | 2026-07-06 |
| 56. Allow admins to edit characters owned by other players | v7.0 | 1/1 | Complete | 2026-07-06 |
| 57. Add an NPC directory | v7.0 | 6/6 | Complete | 2026-07-06 |
| 58. Rename the Guild Members feature to Characters everywhere | v7.0 | 6/6 | Complete | 2026-07-06 |
| 59. Add a rewards field to quests | v7.0 | 2/2 | Complete | 2026-07-06 |
| 60. Stop creating AspNetUserRoles entries for new users | v7.0 | 1/1 | Complete    | 2026-07-06 |
| 61. Allow DMs to edit finalized quest details | v7.0 | 2/2 | Complete    | 2026-07-07 |
| 62. Stop eagerly loading image bytes in list/entity queries | v7.0 | 3/3 | Complete    | 2026-07-07 |
| 63. Allow any player to edit quest recaps | v7.0 | 1/1 | Complete    | 2026-07-07 |
| 64. Preserve line breaks in description text on mobile views | v7.0 | 2/2 | Complete    | 2026-07-07 |
| 65. Platform Settings + Token Contract | v2.0 (redo) | 0/5 | Not started | - |
| 66. Navigation + Token Generation | v2.0 (redo) | 0/? | Not started | - |
| 67. Omphalos SSO Endpoint + Session Linking | v2.0 (redo) | 0/? | Not started | - |
