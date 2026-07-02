# Milestones — D&D Quest Board

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
