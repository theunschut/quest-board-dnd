# Milestones — D&D Quest Board

## v6.1 Bugfixes (Shipped: 2026-07-04)

**Phases completed:** 5 phases, 16 plans, 37 tasks

**Key accomplishments:**

- Closed a live cross-tenant PII leak by scoping `AdminController.Users()` to the active group and hardening the four role-change POST actions against out-of-group `userId` tampering, proven by a passing regression test.
- Domain-layer `IUserService.CreateOrAddToGroupAsync` resolving four collision outcomes (new account, added-to-group, added-to-group-stranded-account, already-member) via throw-on-collision membership add, fully unit-tested with NSubstitute.
- New AddedToGroup.razor email component (Welcome.razor's visual shell, token-free Log In CTA) plus its GroupMembershipAddedEmailJob Hangfire dispatcher, and a RedirectWithWarning/alert-warning flash pair for the Admin Users page
- AdminController.CreateUser now branches on CreateOrAddToGroupAsync's five-outcome result to drive new-account creation, silent collision auto-add with a token-free notification email, stranded-account SetPassword resend, and an already-member warning flash — closing CREATE-01 through CREATE-04.
- Two-column Platform Members page (current members left, searchable non-members right) with a Create New User modal, later extended mid-checkpoint with a matching server-side search on the Current Members list, dual-search-preservation across every Add/Remove/Create action, and header restructures on both Members and Group Management pages.
- Repurposed the group-admin "Delete" user action to a reversible group-scoped removal via `IGroupService.RemoveMemberAsync`, replacing the hard-delete that previously cascaded a user out of every group and threw an unhandled `DbUpdateException` for users with quest/shop/transaction/reminder history.
- Three new `IIdentityService` methods (`DisableUserAsync`, `EnableUserAsync`, `GetLockoutEndAsync`) built on Identity's existing `LockoutEnd` field, plus an app-wide `SecurityStampValidatorOptions.ValidationInterval` shortened to 5 minutes to bound how fast a disabled account's active session is force-expired.
- New SuperAdmin-only `Areas/Platform/Controllers/UsersController` (Index/Disable/Enable) with desktop + mobile cross-group user list views, a "Manage Users" entry point on the Group Index header, and integration tests proving disable sets the lockout sentinel without deleting data while self-disable is blocked and peer-SuperAdmin disable is allowed.
- `AccountController.Login`'s `IsLockedOut` branch now composes `GetIdByEmailAsync` + `GetLockoutEndAsync` and branches on an exact `== DateTimeOffset.MaxValue` comparison to show "This account has been disabled. Contact an administrator." for a disabled account while leaving the "...Try again in 15 minutes." copy unchanged for an ordinary temporary lockout.
- Built a single shared `_Toasts.cshtml` partial (4 generic types + bespoke GoldReceived) wired into all 5 layouts, added a `RedirectWithInfo` controller helper, and standardized AccountController's TempData keys — the foundation Wave 2 migration plans depend on.
- Removed local Bootstrap toast markup and duplicate init scripts from all 4 Shop views (Index/Details, desktop + mobile), delegating Success/Error/GoldReceived rendering to the shared `_Toasts.cshtml` partial while leaving the Mystical Merchant novelty toast completely untouched.
- Removed local `alert alert-dismissible` flash banners from all 6 Platform-area views (Group Index/Members, Users Index — desktop + mobile), relying on the shared `_Toasts.cshtml` partial wired into the Platform layout pair by Plan 01.
- Removed local flash-banner markup from all 6 Account views (Login, ForgotPassword, Profile — desktop + mobile), completing the Account-area migration onto the shared toast partial, including Profile's outlier `SuccessMessage`-keyed block.
- Removed the last 3 root-layout views' local TempData `alert-dismissible` flash markup (Admin Users, Quest Manage desktop + mobile), completing the app-wide migration to the shared toast partial from Plan 01.

---

## v6.0 Board Types (Campaign Mode) (Shipped: 2026-07-03)

**Phases completed:** 3 phases, 11 plans, 28 tasks

**Key accomplishments:**

- Board Type UI shipped across all six Platform Group views (Create/Edit/Index, desktop + mobile) plus a global CSS fix so disabled form fields actually look disabled.
- Campaign quest board, Manage, Details, and Create views (desktop + mobile) render conditionally on `ViewBag.BoardType` — no CR badge, no signup/date-voting, Close/Reopen buttons on Manage, stripped Create form — with one-shot rendering unchanged.
- Quest Log Index and Details (desktop + mobile) drop the CR badge and Adventurers count for campaign-closed entries, show the completed date from `ClosedDate` (falling back to `FinalizedDate` for one-shot), and keep the Session Recap flow working unchanged.
- OneShot allowlist nav gating shipped in both desktop and mobile layouts (LayoutNavigationTests 16/16 green), plus a circular DI dependency fix (new IBoardTypeResolver service) discovered and resolved during the human-verify checkpoint that had left the app unable to start.

---

## v1.0 — Architecture & Features

**Shipped:** prior to 2026-06
**Phases:** 1–7, 9 (Phase 8 deferred) | **Plans:** 19

### Delivered

Restored correct layer boundaries (Domain compiles without Repository reference), moved business logic from controllers into services, removed dead code and security gaps, then added four features on the clean architecture: shop filter/sort, follow-up quest creation, DM profile page, and server-side shop pagination. Phase 8 (avatar crop) was deferred pending SkiaSharp native lib verification.

### Key Accomplishments

1. Domain layer no longer references Repository — `EntityProfile.cs` moved, all repository interfaces relocated to Domain
2. `QuestController.Finalize` reduced to <20 lines — all email and finalization logic moved to QuestService
3. Dead code removed: `SecurityConfiguration`, `UpdateQuestPropertiesAsync`, magic number in `SignupRole == 1`
4. Account lockout (5 attempts, 15-min), 8-character minimum password, `.env` removed from git
5. Shop filter/sort by rarity and price with URL-persisted state
6. Follow-up quest creation pre-filling finalized player list
7. DM profile page with bio and photo, editable by Admin

### Archive

- `.planning/milestones/v1.0-ROADMAP.md`
- `.planning/milestones/v1.0-phases/` (phases 01–07, 09)

---

## v3.0 — Mobile Version

**Shipped:** 2026-06-25
**Phases:** 12–19 | **Plans:** 34 | **Tests:** 139 integration tests

### Delivered

Added purpose-built `.Mobile.cshtml` view variants for all player, DM, admin, and shop pages via a `MobileDetectionMiddleware` + `MobileViewLocationExpander` pipeline. Zero changes to controllers, ViewModels, repositories, or domain services — the entire feature is additive to the Service layer's Views directory and static assets.

### Key Accomplishments

1. Mobile detection + view-expander infrastructure — one-time middleware enabling all subsequent phases
2. `mobile.css` baseline with 44px touch targets enforced site-wide
3. Agenda-style mobile calendar replacing 7-column grid — fully readable on small screens
4. 19 `.Mobile.cshtml` view files + 14 dedicated mobile CSS files across all app areas
5. 139 integration tests covering all mobile view routes with mobile User-Agent header

### Archive

- `.planning/milestones/v3.0-ROADMAP.md`
- `.planning/milestones/v3.0-phases/` (phases 12–19)

---

## v4.0.0 — Email Notifications

**Shipped:** 2026-06-28
**Phases:** 20–25 | **Plans:** 22 | **Tests:** 191 (52 unit + 139 integration)
**Timeline:** 4 days (2026-06-25 → 2026-06-28)
**Files changed:** ~211 | **Lines added:** ~31 000

### Delivered

Upgraded all outbound emails from plain text to styled HTML (Razor components + HtmlRenderer), added automated 24h session reminders via a Hangfire recurring job, a DM manual reminder trigger from the quest manage page, idempotent dedup via a ReminderLog table, an admin email stats dashboard (sent/delivered/bounced/failed) backed by the Resend REST API, and an email confirmation flow with admin resend button and job-level guards for unconfirmed users.

### Key Accomplishments

1. Hangfire background job infrastructure — SQL Server storage, admin-only dashboard at `/hangfire`, `IServiceScopeFactory` pattern established for all jobs
2. All outbound emails upgraded to styled HTML — `_EmailLayout`, `QuestFinalized`, `QuestDateChanged`, `SessionReminder`, `ConfirmEmail` Razor components
3. Quest-finalization dedup guard via `FinalizedEmailSentForDate` column — no duplicate emails on re-finalization
4. 24h automated session reminders — daily CRON job + DM manual trigger, idempotent via ReminderLog table
5. Admin email stats dashboard — live Resend API pull with 5-minute cache, graceful degraded states for missing key / API error
6. Email confirmation flow — admin resend button, `EmailConfirmed` guard on all four email jobs, ASP.NET Identity token callback endpoint

### Known Deferred Items at Close: 2 (see STATE.md Deferred Items)

EMAIL-04 / REMIND-02 — digest batching (single combined email for multiple same-day quests). Explicitly dropped from scope; same-day quests have never occurred in one year of operation.

### Archive

- `.planning/milestones/v4.0-ROADMAP.md`
- `.planning/milestones/v4.0-REQUIREMENTS.md`
- `.planning/milestones/v4.0-phases/` (phases 20–25)

---

## v5.0 — Multi-Tenancy

**Shipped:** 2026-07-02
**Phases:** 26–34.3 (12 phases, incl. 3 inserted) | **Plans:** 48
**Timeline:** 4 days (2026-06-29 → 2026-07-02)
**Files changed:** 754 | **Lines:** +56,450 / -6,374

### Delivered

Transformed the Quest Board from a single-tenant EuphoriaInn app into a generic, rebrandable multi-group platform: full `EuphoriaInn.*` → `QuestBoard.*` namespace rename, a `GroupEntity`/`UserGroups` schema with EF Core Global Query Filters for tenant isolation, a `SuperAdmin` role with a dedicated `/platform` management area, group-picker UX with admin-only user creation, unauthenticated-visitor lockdown with a public landing page, a passwordless first-login flow driven by welcome/forgot-password emails, session persistence across app restarts via `AddDistributedSqlServerCache`, admin email rate limiting, and a closing codebase cleanup/security-hardening pass that also caught and fixed a pre-ship group-role authorization regression.

### Key Accomplishments

1. Renamed the entire codebase from `EuphoriaInn.*` to `QuestBoard.*` with zero behavior change, verified against the full 191-test suite
2. Built the multi-group data model — `GroupEntity`, `UserGroups` junction, `GroupId` FKs — with EF Core Global Query Filters enforcing tenant isolation on quests and shop items
3. Added a `SuperAdmin` role, a dedicated `/platform` management area, and group-picker UX with admin-only user creation (self-registration removed)
4. Locked down authentication — public landing page at `/`, quest board moved to `/quests`, first-login password-set flow via welcome email, self-service enumeration-safe forgot-password flow
5. Made session state durable across app restarts (`AddDistributedSqlServerCache`) and rate-limited repeatable admin email-send actions to protect the mail relay's quota
6. Closed with a dedicated security/performance hardening pass (Phases 34/34.1/34.2) and caught + fixed a pre-ship group-role authorization regression (Phase 34.3) before any production deploy

### Known Deferred Items at Close

- `GroupSessionMiddleware` POST-body data-loss risk on session expiry mid-submission — flagged by code review, not yet fixed
- Digest batching for session reminders (EMAIL-04/REMIND-02) — still deferred, same-day quests have never occurred in a year of operation
- Profile picture crop/avatar selection (issue #78) — still paused pending SkiaSharp native-lib verification on the deployment host

### Archive

- `.planning/milestones/v5.0-ROADMAP.md`
- `.planning/milestones/v5.0-REQUIREMENTS.md`
