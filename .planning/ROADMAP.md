# Roadmap: D&D Quest Board

## Milestones

- ✅ **v1.0 Architecture & Features** — Phases 1–7, 9 (shipped prior to 2026-06)
- 🚧 **v2.0 Omphalos Integration** — Phases 10–11 (in progress — branch: `milestone/3-omphalos-integration`)
- ✅ **v3.0 Mobile Version** — Phases 12–19 (shipped 2026-06-25)
- ✅ **v4.0 Email Notifications** — Phases 20–25 (shipped 2026-06-28)
- ✅ **v5.0 Multi-Tenancy** — Phases 26–34.3 (shipped 2026-07-02)
- ✅ **v6.0 Board Types (Campaign Mode)** — Phases 35–37 (shipped 2026-07-03)
- ✅ **v6.1 Bugfixes** — Phases 38–42 (shipped 2026-07-04)
- ✅ **v7.0 Backlog Cleanup** — Phases 43–64 (shipped 2026-07-08)
- 🚧 **v8.0 Markdown Support** — Phases 65–71 (in progress)

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

<details open>
<summary>🚧 v8.0 Markdown Support (Phases 65–71) — IN PROGRESS</summary>

**Overview:** Let users write and view formatted text (bold, lists, headings, links, blockquotes) in all 9 free-text fields across the app — Quest Description/Rewards/Recap, Character Description/Backstory, Contact Description/Notes, DM Profile Bio, Shop Item Description — via a shared Markdown editor + toolbar + Preview toggle, replacing today's line-break-preserving plain-text display with strict CommonMark paragraph rules. A single, unit-tested sanitizing rendering pipeline (Markdig + HtmlSanitizer) is built once and reused identically by every page view and by the 3 HTML email templates that quote Quest Description.

- [ ] Phase 65: Markdown Rendering Foundation — Build and unit-test the shared Markdig + HtmlSanitizer rendering service; no user-visible changes yet
- [x] Phase 66: Quest Description Editor & Rendering (Proof-of-Concept) — Wire the shared Markdown editor into Quest Description end-to-end, including the Quest Finalized email, proving the full write→read→email loop (completed 2026-07-09)
- [ ] Phase 67: Remaining Quest Fields & Email Templates — Apply the proven pattern to Quest Rewards and Quest Recap, and wire the remaining 2 email templates
- [ ] Phase 68: Character Fields — Character Description and Backstory get the Markdown editor and rendering
- [ ] Phase 69: Contact Fields — Contact Description and per-note Contact Notes get the Markdown editor and rendering
- [ ] Phase 70: DM Profile & Shop Fields — DM Profile Bio and Shop Item Description get the Markdown editor and rendering
- [ ] Phase 71: Email-Safety Hardening — Inline styling/layout fixes so Markdown-structured content displays correctly, unclipped, in real Outlook and Gmail

</details>

## Phase Details

### Phase 65: Markdown Rendering Foundation

**Goal**: A single, secure, well-tested Markdown-to-HTML rendering pipeline exists that every later phase reuses for both page views and HTML emails — no live user-facing views change yet.
**Depends on**: Nothing (first phase of v8.0; builds on the shipped v7.0 codebase)
**Requirements**: RENDER-01, RENDER-02, RENDER-03
**Success Criteria** (what must be TRUE):

  1. Markdown input is converted to sanitized HTML that blocks script execution and `javascript:`-scheme links, proven by unit tests covering `<script>` tags, `javascript:` URLs inside `[text](url)` syntax, and generic-attribute injection payloads
  2. A single rendering service is the only place Markdown-to-HTML conversion happens — the service is designed to be called identically by MVC page views and by Razor email components, with no separate or duplicated parsing logic anywhere in the codebase
  3. A blank line (not a single Enter) is required to start a new paragraph, proven by unit tests asserting strict CommonMark paragraph behavior on multi-line input

**Plans**: 1 plan

- [x] 65-01-PLAN.md — Build and unit-test the shared Markdig + HtmlSanitizer rendering service (IMarkdownService, single pipeline, dual web/email sanitizer profiles, AddSingleton registration)

### Phase 66: Quest Description Editor & Rendering (Proof-of-Concept)

**Goal**: A user can write formatted Quest Description text with a Markdown toolbar and preview, and see it rendered as formatted HTML everywhere it's displayed — including the Quest Finalized email — proving the full write-to-read-to-email loop for the milestone's riskiest cross-cutting field before mechanically repeating the pattern elsewhere.
**Depends on**: Phase 65
**Requirements**: EDITOR-01, EDITOR-02, EDITOR-03, EDITOR-04, EDITOR-05, EDITOR-06, QUESTMD-01
**Success Criteria** (what must be TRUE):

  1. A user editing Quest Description (Create/Edit/Follow-Up forms, desktop or mobile) sees a formatting toolbar (Bold, Italic, Heading, List, Link, Blockquote) that inserts Markdown syntax around the current selection or at the cursor
  2. A user can toggle Preview to see a rendered-HTML view of their Quest Description that exactly matches how it displays once saved, with the rest of the toolbar disabled (not just visually greyed out) while Preview is active
  3. An inline hint next to the editor explains that a blank line starts a new paragraph
  4. The toolbar and editor behave identically on desktop and mobile, with icon-only 44px+ touch targets fitting in one row with no overflow or scroll mechanism
  5. Quest Description renders as formatted HTML on the quest board card, Quest Details, Quest Manage, and in the Quest Finalized email

**Plans**: 7/7 plans complete

- [x] 66-01-PLAN.md — Domain plain-text extraction (AngleSharp) + `Html.Markdown()` read helper
- [x] 66-02-PLAN.md — `POST /markdown/preview` endpoint + header-antiforgery integration test
- [x] 66-03-PLAN.md — Quest Finalized email renders Description as HTML (images stripped)
- [x] 66-04-PLAN.md — Shared editor assets: EasyMDE init, CSS, partial, FA v4-shim, tooltip init
- [x] 66-05-PLAN.md — Wire the EasyMDE editor into all 6 Quest Description write forms
- [x] 66-06-PLAN.md — Render Description on Details/Manage (HTML) + board card (plain text)
- [x] 66-07-PLAN.md — Manual UI verification checkpoint (desktop + 320px mobile + email)

**UI hint**: yes

### Phase 67: Remaining Quest Fields & Email Templates

**Goal**: The Markdown editor and rendering pattern proven on Quest Description is mechanically applied to Quest Rewards and Quest Recap, and all 3 quest-related email templates render Quest Description as formatted HTML.
**Depends on**: Phase 66
**Requirements**: QUESTMD-02, QUESTMD-03, EMAILMD-01
**Success Criteria** (what must be TRUE):

  1. A user editing Quest Rewards sees the same Markdown editor/toolbar/preview as Quest Description, and Rewards renders as formatted HTML on Quest Details and the QuestLog
  2. A user editing a Quest Recap (via the EditRecap form) sees the same Markdown editor, and the Recap renders as formatted HTML on Quest Details and the QuestLog
  3. The Quest Finalized, Session Reminder, and Waitlist Promoted emails all render Quest Description as formatted HTML, not raw Markdown syntax

**Plans**: 5 plans

- [x] 67-01-PLAN.md — Wire the Markdown editor into Quest Rewards on all 6 write forms (incl. Follow-Up mobile backfill, D-02)
- [x] 67-02-PLAN.md — Render Rewards on Quest Details + new Manage collapsible section (D-01) + pre-wrap CSS override
- [x] 67-03-PLAN.md — Wire Recap editor + render Rewards/Recap on QuestLog Details (incl. mobile Rewards backfill, D-03)
- [x] 67-04-PLAN.md — Session Reminder + Waitlist Promoted emails render Quest Description as HTML
- [x] 67-05-PLAN.md — Operator human-verification checkpoint (desktop + 320px mobile)

**UI hint**: yes

### Phase 68: Character Fields

**Goal**: A user can write and view formatted Character Description and Backstory text.
**Depends on**: Phase 66
**Requirements**: CHARMD-01, CHARMD-02
**Success Criteria** (what must be TRUE):

  1. A user editing Character Description or Backstory sees the same Markdown editor/toolbar/preview established in Phase 66
  2. Character Description and Backstory render as formatted HTML on Character Details (desktop and mobile)
  3. Existing multi-line Character Description/Backstory text displays without doubled spacing — the old line-break-preserving CSS is removed from the rendered-output containers as a companion edit

**Plans**: TBD
**UI hint**: yes

### Phase 69: Contact Fields

**Goal**: A user can write and view formatted Contact Description and Notes, with each note's formatting staying independent of every other note.
**Depends on**: Phase 66
**Requirements**: CONTACTMD-01, CONTACTMD-02
**Success Criteria** (what must be TRUE):

  1. A user editing Contact Description sees the Markdown editor, and Description renders as formatted HTML on Contact Details and Index
  2. A user editing a Contact Note sees the Markdown editor, and each note renders independently as formatted HTML — one author's unclosed formatting never bleeds into another author's note
  3. Existing multi-line Contact text displays without doubled spacing — the old line-break-preserving CSS is removed from the rendered-output containers as a companion edit

**Plans**: TBD
**UI hint**: yes

### Phase 70: DM Profile & Shop Fields

**Goal**: A user can write and view formatted DM Profile Bio and Shop Item Description text.
**Depends on**: Phase 66
**Requirements**: PROFILEMD-01, PROFILEMD-02
**Success Criteria** (what must be TRUE):

  1. A user editing DM Profile Bio sees the Markdown editor, and Bio renders as formatted HTML on the DM Profile page
  2. A user editing Shop Item Description sees the Markdown editor, and Description renders as formatted HTML on Shop Index, Details, and Manage
  3. Existing multi-line Bio/Description text displays without doubled spacing — the old line-break-preserving CSS is removed from the rendered-output containers as a companion edit

**Plans**: TBD
**UI hint**: yes

### Phase 71: Email-Safety Hardening

**Goal**: Markdown-formatted Quest Description content displays correctly and completely — not broken, missing, or clipped — when the 3 quest email templates are opened in real email clients.
**Depends on**: Phase 67
**Requirements**: EMAILMD-02, EMAILMD-03
**Success Criteria** (what must be TRUE):

  1. A recipient opening the Quest Finalized, Session Reminder, or Waitlist Promoted email in real Outlook desktop sees visible bullets and intact heading/blockquote styling for Markdown-structured Quest Description content
  2. A recipient opening the same emails in real Gmail webmail sees the same correctly formatted content
  3. A recipient can read the full quest description even when formatted with headings, lists, or blockquotes — content is not silently clipped by a fixed-height card, resolved via an explicit layout decision (truncate-with-link or remove the fixed height)

**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 35 → 36 → 37 → 38 → 39 → 40 → 41 → 42 → 43 → 44 → 45 → 46 → 47 → 48 → 49 → 50 → 51 → 52 → 53 → 54 → 55 → 56 → 57 → 58 → 59 → 60 → 61 → 62 → 63 → 64 → 65 → 66 → 67 → 68 → 69 → 70 → 71

Phases 43 and 44 have no dependency on each other or on 45/46 and may be sequenced in either order. Phase 46 depends on Phase 45. Phases 47–64 are ad-hoc additions folded in after the original v7.0 roadmap was created, each depending on the previous phase (Phase 62 depends on Phase 46 specifically, not Phase 61). Phases 57, 58, and 59 executed out of numeric order — Phase 58 (Characters rename) was planned and executed before Phase 57 (NPC directory); Phase 59 (Rewards field) was then planned and executed concurrently with Phase 57's still-in-progress execution.

For v8.0 (Phases 65–71): Phase 65 (Foundation) has no dependency and must land before any view is touched. Phase 66 (Quest Description proof-of-concept) depends on Phase 65 and is itself the dependency every later field-migration phase (67, 68, 69, 70) builds on. Phases 68, 69, and 70 have no dependency on each other or on Phase 67 beyond both requiring Phase 66's proven editor pattern, but this project's convention executes phases in numeric sequence regardless of that parallelism. Phase 71 (Email-Safety Hardening) depends specifically on Phase 67, since all 3 quest email templates must be wired to render Markdown before their visual styling/layout is hardened for real email clients.

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
| 65. Markdown Rendering Foundation | v8.0 | 1/1 | Complete    | 2026-07-09 |
| 66. Quest Description Editor & Rendering (Proof-of-Concept) | v8.0 | 7/7 | Complete    | 2026-07-09 |
| 67. Remaining Quest Fields & Email Templates | v8.0 | 5/5 | Complete   | 2026-07-10 |
| 68. Character Fields | v8.0 | 0/? | Not started | - |
| 69. Contact Fields | v8.0 | 0/? | Not started | - |
| 70. DM Profile & Shop Fields | v8.0 | 0/? | Not started | - |
| 71. Email-Safety Hardening | v8.0 | 0/? | Not started | - |
