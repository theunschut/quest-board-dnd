# Requirements: D&D Quest Board v5.0 Multi-Tenancy

**Defined:** 2026-06-29
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

## v5.0 Requirements

### Rename

- [x] **RENAME-01**: All `EuphoriaInn.*` namespaces renamed to `QuestBoard.*` across every C# file
- [x] **RENAME-02**: Project files (`.csproj`), solution file (`.slnx`), and directory names renamed to match `QuestBoard.*`
- [x] **RENAME-03**: All config files (`appsettings*.json`), GitHub Actions workflows, and any deployment references updated
- [x] **RENAME-04**: All EF Core migration `*.Designer.cs` files updated with new namespace; `dotnet build` + all 191 tests pass with zero behavior change

### Group Schema

- [x] **GROUP-01**: `GroupEntity` table exists with `Id`, `Name`, `CreatedAt` columns *(model layer complete — plan 27-01; migration in plan 27-02)*
- [x] **GROUP-02**: `UserGroups` junction table exists with `UserId`, `GroupId`, `GroupRole` (enum: Player / DungeonMaster / Admin) *(model layer complete — plan 27-01; migration in plan 27-02)*
- [x] **GROUP-03**: `GroupId` FK added to `QuestEntity` and `ShopItemEntity` (the two shared-resource entities not naturally scoped through user ownership) *(model layer complete — plan 27-01; migration in plan 27-02)*
- [ ] **GROUP-04**: Data migration seeds the `"EuphoriaInn"` group as `GroupId = 1`
- [ ] **GROUP-05**: All existing users are assigned to EuphoriaInn in `UserGroups`; each user's `GroupRole` is seeded from their current `AspNetUserRoles` entry
- [ ] **GROUP-06**: `AspNetUserRoles` entries for Player / DungeonMaster / Admin are removed after migration; only SuperAdmin assignments remain in `AspNetUserRoles`

### Tenant Isolation

- [ ] **TENANT-01**: `IActiveGroupContext` interface defined in the Domain layer with an `ActiveGroupId` property (`int?` — `null` means SuperAdmin, see all)
- [ ] **TENANT-02**: `ActiveGroupContextService` in the Service layer implements `IActiveGroupContext`; reads `ActiveGroupId` from ASP.NET Core Session; returns `null` when the user holds the SuperAdmin role
- [ ] **TENANT-03**: EF Core Global Query Filters applied to `QuestEntity` and `ShopItemEntity` using `IActiveGroupContext.ActiveGroupId`; `UserEntity` does NOT receive a query filter
- [x] **TENANT-04**: All four Hangfire email jobs bypass the group filter (cross-tenant by design) or receive an explicit `groupId` parameter where needed
- [ ] **TENANT-05**: Integration test factory registers a stub `IActiveGroupContext` returning `GroupId = 1` by default; all 191 existing tests pass after filter addition

### Authorization

- [x] **AUTH-01**: `SuperAdmin` role added to `AspNetRoles` and seedable at startup
- [x] **AUTH-02**: `AdminHandler` updated to check `UserGroups.GroupRole == Admin` for the active group (instead of `AspNetUserRoles`)
- [x] **AUTH-03**: `DungeonMasterHandler` updated to check `UserGroups.GroupRole` is DungeonMaster or Admin for the active group
- [x] **AUTH-04**: Both handlers grant access when the user holds the SuperAdmin Identity role, regardless of active group
- [x] **AUTH-05**: A `SuperAdminOnly` authorization policy exists, used to protect the management area

### Group UX

- [x] **UX-01**: User belonging to exactly one group is automatically redirected to that group's content after login (no picker shown)
- [x] **UX-02**: User belonging to multiple groups sees a group-picker page after login and selects which group to enter
- [x] **UX-03**: SuperAdmin always lands on the group-picker page after login and can enter any group or go to the management area
- [x] **UX-04**: Active group is stored in ASP.NET Core Session per request; selected group persists across requests until session expires or user exits
- [x] **UX-05**: Navigation displays the current group name and a "Switch group" link; clicking it returns to the group-picker

### Management Area

- [x] **MGMT-01**: A dedicated MVC Area exists for SuperAdmin group management; route is not `/superadmin` (planner recommends a name, e.g. `/groups` or `/platform`)
- [x] **MGMT-02**: SuperAdmin can view a list of all groups with member counts
- [x] **MGMT-03**: SuperAdmin can create a new group (name required)
- [x] **MGMT-04**: SuperAdmin can edit a group's name or delete an empty group
- [x] **MGMT-05**: SuperAdmin can add any existing user to any group and assign their `GroupRole`
- [x] **MGMT-06**: SuperAdmin can remove a user from a group
- [x] **MGMT-07**: Group admin (Admin `GroupRole` in a group) can create new user accounts within their group
- [x] **MGMT-08**: Group admin can promote or demote users within their group (Player ↔ DungeonMaster ↔ Admin)

### User Creation

- [x] **REG-01**: Public self-registration (`AccountController.Register`) is removed or restricted to SuperAdmin / Admin users only
- [x] **REG-02**: Newly created user accounts are automatically assigned to the creating admin's active group with the specified `GroupRole`
- [x] **REG-03**: The existing email confirmation flow is triggered when a group admin or SuperAdmin creates a new user account

### Session Persistence (Phase 33)

- [x] **SESSION-01**: ASP.NET Core Session is backed by `AddDistributedSqlServerCache` (package `Microsoft.Extensions.Caching.SqlServer` 10.0.9) registered BEFORE `AddSession` in `Program.cs`, so `ActiveGroupId` survives an app restart instead of being wiped from the in-memory fallback store. Registration is guarded by `if (!builder.Environment.IsEnvironment("Testing"))` with an `AddDistributedMemoryCache()` fallback in the Testing branch (mirrors the existing Hangfire branch).
- [x] **SESSION-02**: The `dbo.AspNetSessionState` cache table is provisioned by an EF Core migration using `migrationBuilder.Sql(...)` with an `IF NOT EXISTS` guard; the `Id` column uses `COLLATE SQL_Latin1_General_CP1_CS_AS` (case-sensitive) per the official `dotnet/aspnetcore` DDL. No `DbSet`/entity is added for this table.

### Admin Email Rate Limiting (Phase 33)

- [x] **EMAIL-RATE-01**: `AdminController.SendConfirmationEmail` (POST) rejects the 4th request within 1 hour for the same target `userId` with HTTP 429 and the body `"Too many requests. Please try again later."`, enforced via a programmatic `PartitionedRateLimiter<int>` `AttemptAcquire(userId)` check inside the action body (not `[EnableRateLimiting]`, because `userId` is a POST form field unavailable to policy factories that run before model binding).
- [x] **EMAIL-RATE-02**: The admin email resend rate limit is partitioned per target user (`$"email-resend:{userId}"`); two different target users each get an independent 3/hour budget.
- [x] **EMAIL-RATE-03**: `AdminController.EditUser` (POST) applies the same `AttemptAcquire(model.Id)` check only inside the `emailChanged && !string.IsNullOrEmpty(model.Email)` branch (the email-dispatch sub-path), so non-email-changing saves are not counted.
- [x] **EMAIL-RATE-04**: `AdminController.CreateUser` (POST, welcome email) is explicitly NOT rate-limited — it is a one-shot automated send, not a repeatable manual button.

### Password Flow (Phase 32)

- [x] **PWFLOW-01**: Admin-created accounts have no password at creation (`UserManager.CreateAsync(user)` no-password overload; `PasswordHash` stays null); `CreateUserViewModel.Password` and the password input in `CreateUser.cshtml`/`CreateUser.Mobile.cshtml` are removed
- [x] **PWFLOW-02**: A newly created user receives one combined "Welcome — set your password" email; clicking the link and submitting a new password sets `PasswordHash` AND marks `EmailConfirmed = true` in a single `SetPassword` action (no separate confirm-email step)
- [x] **PWFLOW-03**: A passwordless account cannot sign in until `SetPassword` is completed — `PasswordSignInAsync` fails gracefully (returns `Failed`, never throws) against a null `PasswordHash`
- [x] **PWFLOW-04**: Self-service "Forgot password?" flow (`GET/POST /Account/ForgotPassword`) sends a reset link that lands on `SetPassword`; the POST is enumeration-safe (identical generic response whether or not the email matches) and rate-limited to 3 requests / 15 minutes per client IP via built-in `Microsoft.AspNetCore.RateLimiting`
- [x] **PWFLOW-05**: The admin "Resend welcome email" button on `Views/Admin/Users.cshtml` (shown when `EmailConfirmed == false`) sends the new Welcome email; the old `ConfirmEmail.razor` + `ConfirmationEmailJob` flow is retired
- [x] **PWFLOW-06**: `DataProtectionTokenProviderOptions.TokenLifespan` is configured to 7 days (net-new config; uniformly affects password-reset, email-confirmation, and change-email tokens)

## Future Requirements

Deferred to v5.x or later — tracked but not in current roadmap.

- **"Password changed" notification email** — for `AccountController.ChangePassword` (self-service) and `AdminController.ResetPassword` (admin-triggered); deferred from Phase 32 to a future phase once `Welcome.razor` establishes the pattern
- Per-group email configuration (custom sender address per group)
- Group invitation flow (invite by email link rather than admin-created account)
- Cross-group quest browsing or character directory
- Digest batching for session reminders (EMAIL-04/REMIND-02 — deferred since v4.0)
- Profile picture crop / avatar selection (issue #78 — deferred since v2.x)

## Out of Scope

- **Per-group Identity roles in `AspNetUserRoles`** — per-group roles live in `UserGroups.GroupRole`; `AspNetUserRoles` is used only for SuperAdmin
- **Database-per-tenant or schema-per-tenant** — shared-database shared-schema with EF Query Filters is sufficient at current scale
- **Third-party multi-tenancy frameworks** (Finbuckle, SaasKit, Abp.io) — existing EF Core 10 capabilities cover all requirements
- **Group billing or subscription management** — out of scope for this app
- **Email non-uniqueness / separate account per group** — one global user identity with per-group roles (see design decision in session 2026-06-29)

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| RENAME-01 | Phase 26 | Complete |
| RENAME-02 | Phase 26 | Complete |
| RENAME-03 | Phase 26 | Complete |
| RENAME-04 | Phase 26 | Complete |
| GROUP-01 | Phase 27 | Complete (model) |
| GROUP-02 | Phase 27 | Complete (model) |
| GROUP-03 | Phase 27 | Complete (model) |
| GROUP-04 | Phase 27 | Complete |
| GROUP-05 | Phase 27 | Complete |
| GROUP-06 | Phase 27 | Complete |
| TENANT-01 | Phase 28 | Complete — 28-01 |
| TENANT-02 | Phase 28 | Complete — 28-01 |
| TENANT-03 | Phase 28 | Complete — 28-01 + 28-03 |
| TENANT-04 | Phase 28 | Complete — 28-02 |
| TENANT-05 | Phase 28 | Complete — 28-01 + 28-03 |
| AUTH-01 | Phase 29 | Complete — 29-02 |
| AUTH-02 | Phase 29 | Complete — 29-01 |
| AUTH-03 | Phase 29 | Complete — 29-01 |
| AUTH-04 | Phase 29 | Complete — 29-01 |
| AUTH-05 | Phase 29 | Complete — 29-01 |
| MGMT-01 | Phase 29 | Complete — 29-04 |
| MGMT-02 | Phase 29 | Complete — 29-04 |
| MGMT-03 | Phase 29 | Complete — 29-04 |
| MGMT-04 | Phase 29 | Complete — 29-04 |
| MGMT-05 | Phase 29 | Complete — 29-04 |
| MGMT-06 | Phase 29 | Complete — 29-04 |
| MGMT-07 | Phase 30 | Complete |
| MGMT-08 | Phase 30 | Complete |
| UX-01 | Phase 30 | Complete |
| UX-02 | Phase 30 | Complete |
| UX-03 | Phase 30 | Complete |
| UX-04 | Phase 30 | Complete |
| UX-05 | Phase 30 | Complete |
| REG-01 | Phase 30 | Complete |
| REG-02 | Phase 30 | Complete |
| REG-03 | Phase 30 | Complete |
| PWFLOW-01 | Phase 32 | Complete — 32-01 + 32-04 |
| PWFLOW-02 | Phase 32 | Complete — 32-01 + 32-02 + 32-03 |
| PWFLOW-03 | Phase 32 | Complete — 32-01 + 32-03 + 32-05 |
| PWFLOW-04 | Phase 32 | Complete — 32-02 + 32-03 + 32-05 |
| PWFLOW-05 | Phase 32 | Complete — 32-02 + 32-04 + 32-05 |
| PWFLOW-06 | Phase 32 | Complete — 32-02 |
| SESSION-01 | Phase 33 | Complete — 33-01 + 33-03 (human-verified) |
| SESSION-02 | Phase 33 | Complete — 33-01 + 33-03 (human-verified) |
| EMAIL-RATE-01 | Phase 33 | Complete — 33-02 + 33-03 |
| EMAIL-RATE-02 | Phase 33 | Complete — 33-02 + 33-03 |
| EMAIL-RATE-03 | Phase 33 | Complete — 33-02 + 33-03 |
| EMAIL-RATE-04 | Phase 33 | Complete — 33-02 + 33-03 |
