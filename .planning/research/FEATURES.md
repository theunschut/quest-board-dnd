# Feature Research

**Domain:** Multi-tenancy for an ASP.NET Core 10 MVC D&D group management app
**Researched:** 2026-06-29
**Confidence:** MEDIUM (EF Core multi-tenancy via official MS docs and context7; UX patterns from industry observation; implementation details LOW due to websearch-only sourcing for some items)

---

## Feature Landscape

### Table Stakes (Users Expect These)

These are the behaviors users assume exist the moment "multi-group" is mentioned. Missing any of these makes the feature feel incomplete or broken.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Group entity with display name | Any multi-tenant system needs a named container | LOW | Single `GroupEntity { Id, Name, CreatedAt }` table. No complex config needed at this scale. |
| Per-group data isolation (quests, shop, characters) | Users expect they cannot see another group's quests | MEDIUM | EF Core Global Query Filters on every tenant-scoped entity: `.HasQueryFilter(e => e.GroupId == _currentGroupId)`. Requires adding `GroupId` FK to `QuestEntity`, `ShopItemEntity`, `CharacterEntity`, `TradeItemEntity`, `UserTransactionEntity`, `DungeonMasterProfileEntity`. |
| User↔group membership (many-to-many) | A user can belong to multiple D&D groups | MEDIUM | `UserGroupEntity { UserId, GroupId, Role }` junction table. Composite PK `(UserId, GroupId)`. Role field allows per-group role assignment (Admin/DM/Player scoped to that group). All existing users seeded into the "EuphoriaInn" group on migration. |
| Active-group context persisted across requests | After switching groups, every page should reflect the chosen group | MEDIUM | Session-based: `HttpContext.Session.SetInt32("ActiveGroupId", groupId)`. Resolved per-request by `ITenantService` injected into `QuestBoardContext`. Session already exists (IdleTimeout = 24h). No auth cookie re-issue required — simpler than claims for cookie-auth apps. |
| Group picker at login (for multi-group users) | A user in two groups must be able to choose which one to enter | LOW | After `PasswordSignInAsync` succeeds, check membership count. If count == 1, set session and redirect to Home. If count > 1, redirect to a dedicated `GET /Groups/Pick` page listing their groups. Single-group users (most of the 17 members) never see this page. |
| Group switcher in navigation | Users who belong to multiple groups must be able to change context without logging out | LOW | Navbar dropdown showing current group name + list of other memberships. POST to `/Groups/Switch/{id}` updates session and redirects to Home. Standard UX pattern (Slack, GitHub, Linear). Invisible to single-group users — navbar element only renders if membership count > 1. |
| SuperAdmin role with cross-group access | A platform owner must be able to manage all groups | MEDIUM | Add `"SuperAdmin"` role to Identity. `SuperAdminOnly` authorization policy. `ITenantService` returns `null` for SuperAdmin, and `QuestBoardContext` filter uses: `.HasQueryFilter(e => _currentGroupId == null \|\| e.GroupId == _currentGroupId)`. SuperAdmin sees all data unfiltered. |
| Dedicated management area for SuperAdmin | SuperAdmin needs a place to create/view/delete groups and manage cross-group users | MEDIUM | MVC Area at `/Groups` (or `/Platform`) with `[Authorize(Policy = "SuperAdminOnly")]`. Separate from `/Admin` (which remains per-group). Contains: group list, create group, assign group admin. |
| Admin-only user creation (no self-registration) | In a closed trusted group, admins create accounts — open registration is a security hole | LOW | Remove the public `Register` GET/POST actions from `AccountController` (or gate them with `[Authorize(Policy = "AdminOnly")]`). Move user creation to `AdminController.CreateUser` (form: Name, Email, Password, Role, Group). Reuses existing `ConfirmationEmailJob` to send welcome/confirm email. |
| Group-scoped admin panel | Group admins should only see and manage users in their own group | LOW | Existing `AdminController.Users()` filters by `ActiveGroupId` from session — returns only users whose `UserGroupEntity` has the current group. SuperAdmin bypasses this filter. |
| Existing data migrated into default group | All current quests, users, shop items stay intact after migration | MEDIUM | EF Core migration sets `GroupId = 1` (EuphoriaInn) for all existing rows via `migrationBuilder.Sql(...)`. Data seeder ensures EuphoriaInn group exists before migration runs. |

### Differentiators (Competitive Advantage)

Features that add value beyond the minimum viable multi-group experience. Not required for v5.0 launch, but worth knowing about.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Per-group role assignment (role is group-scoped, not global) | A user can be DM in one group and Player in another — this is realistic for the D&D hobby | MEDIUM | Requires storing `Role` in `UserGroupEntity` rather than relying solely on global Identity roles. When entering a group context, load the group-scoped role and use it for policy checks. Requires custom authorization handler reading group context. **Significant complexity increase** — only worthwhile if users actually DM in multiple groups. |
| Group invite link (time-limited URL) | Admin generates a signup link for a new member rather than manually creating their account | MEDIUM | Generates a signed token stored in DB with expiry. Invitee clicks link, sets their own password. Avoids admin knowing the new member's password. Not needed at 17 members where admin creating accounts is fine. |
| Group branding (name, description, avatar) | Each D&D group has a custom identity in the platform | LOW | Add `Description` and `AvatarUrl` to `GroupEntity`. Display on group picker page. Very low implementation cost but low value until multiple groups actually exist. |
| Group-scoped shop catalog | Each group has its own shop items independent of other groups | Already in table stakes | This is included in the base isolation requirement — not a differentiator. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Per-user email opt-out per group | "A user in two groups might want reminders from one but not the other" | Cross-group preference state adds a third dimension (user × group × preference) to what is currently a flat `HasKey` field. At 17 members and 1-3 groups this never occurs. | Defer; the existing `HasKey` field already handles the single-group opt-out case. |
| Global roles that carry across groups (one role for all groups) | Simpler to assign once | Breaks the logical model — a DM in Group A should not automatically be a DM in Group B. Would require role re-architecture with no user benefit at current scale. | Scope roles to the group via `UserGroupEntity.Role`. For v5.0, if only one group exists, global roles work fine — migrate to per-group later when a second group is created with different membership. |
| Self-service tenant creation by any admin | Group admins request the ability to spawn new groups | A rogue group creation gives them data isolation but no platform visibility. SuperAdmin must be the gatekeeper to prevent proliferation at small scale. | SuperAdmin creates groups; group admin manages membership within their group. |
| Real-time group presence / activity indicators | "Show which group members are online" | Requires WebSocket/SignalR infrastructure, not present in the stack. Enormous complexity for a hobby app. | Not needed — session-based auth already exists, and D&D groups coordinate via Discord/WhatsApp anyway. |
| Separate database per group | Maximum isolation, no accidental leakage | At 1-3 groups and 17 users, managing multiple connection strings, separate migrations, and separate Hangfire instances is over-engineering by an order of magnitude. | Single shared database with EF Core Global Query Filters. Proven pattern, zero ops overhead. |
| JWT-based active tenant claims | "Store the active group in the auth token for statelessness" | This app uses cookie auth + sessions — already in place from v1.x. Re-issuing the auth cookie on every group switch (via `RefreshSignInAsync`) introduces latency and complexity with no benefit for an MVC app. | Store active group in `HttpContext.Session` (already configured with 24h idle timeout). Resolved per-request by `ITenantService`. |
| Schema-per-group database partitioning | Schema isolation without separate databases | EF Core does not support migrations across multiple schemas for the same DbContext. Would require manual migration scripting. No benefit over query filters at this scale. | Global Query Filters with `GroupId` discriminator column. |

---

## Feature Dependencies

```
[Group entity (GroupEntity table)]
    └──required by──> [User↔group membership (UserGroupEntity)]
    └──required by──> [GroupId FK on all tenant-scoped entities]
    └──required by──> [SuperAdmin management area]
    └──required by──> [Group picker page]
    └──required by──> [Group switcher in nav]

[GroupId FK on all tenant-scoped entities]
    └──required by──> [EF Core Global Query Filters (per-entity HasQueryFilter)]
    └──required by──> [Data migration seeding GroupId = 1 for existing rows]

[EF Core Global Query Filters]
    └──required by──> [Per-group data isolation (quests, shop, characters, etc.)]
    └──requires bypass for──> [SuperAdmin cross-group access (ITenantService returns null)]

[User↔group membership (UserGroupEntity)]
    └──required by──> [Group picker at login]
    └──required by──> [Group switcher in nav]
    └──required by──> [Group-scoped admin panel (filter users by group)]
    └──required by──> [Admin-only user creation (assign new user to group)]

[ITenantService (resolves active GroupId from session)]
    └──required by──> [QuestBoardContext (injected into DbContext constructor)]
    └──required by──> [Group switcher (writes to session)]
    └──required by──> [Group picker (writes to session after selection)]

[Active-group context (session)]
    └──required by──> [ITenantService]
    └──set by──> [Group picker at login]
    └──updated by──> [Group switcher in nav]

[SuperAdmin role + policy]
    └──required by──> [SuperAdmin management area]
    └──required by──> [ITenantService null-group bypass]
    └──required by──> [Cross-group user visibility in platform area]

[Admin-only user creation]
    └──requires removal of──> [Public Register action in AccountController]
    └──reuses──> [ConfirmationEmailJob (existing Hangfire job)]
    └──requires──> [UserGroupEntity assignment at creation time]
```

### Dependency Notes

- **Group entity must be first:** Every other feature depends on `GroupEntity` existing. It is the foundation migration.
- **Global Query Filters depend on GroupId FKs:** You cannot add the filters before adding the FK columns with a migration. Data migration (setting `GroupId = 1` for existing rows) must run as part of the same migration step.
- **ITenantService is the central seam:** It is read by both the DbContext (for filtering) and controllers (for authorization checks). Design it as a `Scoped` service so it resolves once per request.
- **Session already exists:** The app already configures `IdleTimeout = 24h` sessions. No new session infrastructure needed — just write `ActiveGroupId` to it.
- **Admin-only user creation can be a late phase:** Removing self-registration does not block any other feature. Do it early to close the security gap, but it has no downstream dependencies.
- **Per-group role scoping (differentiator) conflicts with current global Identity role model:** If implemented, it changes how authorization handlers read roles. Keep it out of v5.0 scope unless explicitly required.

---

## UX Flow: Expected Behaviors

### (a) Switching Between Groups (member of 2+ groups)

1. User is logged in, sees "EuphoriaInn" in navbar group indicator.
2. Clicks the group name dropdown → sees list of their other group memberships.
3. Clicks another group name → browser POSTs to `/Groups/Switch/{newGroupId}`.
4. Server validates user is a member of that group, writes `ActiveGroupId` to session.
5. Redirects to `/` (Home). All pages now show that group's quests, shop, calendar.
6. No logout. No re-authentication. Session cookie unchanged.
7. **Single-group users:** group indicator in navbar is non-interactive (plain text, no dropdown). They never interact with the switcher.

### (b) SuperAdmin Managing Groups

1. SuperAdmin logs in → lands on normal Home page (scoped to their own active group, or a "platform" default group).
2. Platform management link appears in navbar (e.g. "Platform" or gear icon), not visible to regular users.
3. `/Groups` area lists all groups with member counts, creation dates.
4. SuperAdmin can: create group (name → saves GroupEntity), view group members, remove group (with confirmation — destructive).
5. SuperAdmin can enter any group's `/Admin` area by switching active group context (same switcher, but accessible to all groups not just own memberships).
6. SuperAdmin does NOT see cross-group data in the normal views (Home, Quest Board, Shop) unless they explicitly switch to that group — isolation UX is maintained even for SuperAdmin.

### (c) Group Admin Creating a User

1. Admin navigates to `/Admin/Users` → sees only users in their active group.
2. Clicks "Create User" button → form: Display Name, Email, Password, Role (Player/DM/Admin).
3. Admin submits → system creates `UserEntity` via `UserManager.CreateAsync`, assigns to `UserGroupEntity` with current group + selected role, enqueues `ConfirmationEmailJob`.
4. User receives email with confirmation link. Until confirmed, `EmailConfirmed = false` (existing behavior).
5. New user appears in Admin's user list immediately.
6. **No public registration page.** The `GET /Account/Register` route returns 403 (or is removed entirely). The Login page has no "Register" link.

---

## MVP Definition

### Launch With (v5.0 — these are the required features)

- [ ] Group entity + EF Core migration — foundation for everything
- [ ] GroupId FK + data migration on all tenant-scoped entities (Quest, ShopItem, Character, TradeItem, UserTransaction, DungeonMasterProfile) — isolation foundation
- [ ] UserGroupEntity junction table with composite PK + seed existing users into EuphoriaInn group — membership model
- [ ] ITenantService (Scoped, reads session) + EF Core Global Query Filters wired into QuestBoardContext — isolation enforcement
- [ ] Group picker page (login redirect for multi-group users) + group switcher in navbar — active-group UX
- [ ] SuperAdmin role + policy + platform management area at `/Groups` — system administration
- [ ] Admin-only user creation in AdminController + self-registration removed from AccountController — closed-group security

### Defer to v5.x (after v5.0 ships and the second group is actually created)

- [ ] Per-group role scoping (role stored in UserGroupEntity, not global Identity roles) — trigger: a user is DM in one group and Player in another
- [ ] Group invite link (time-limited signup URL) — trigger: admin reports that password creation on behalf of new members is inconvenient
- [ ] Group branding (description, avatar) — trigger: second group is created and wants a distinct identity

### Out of Scope (anti-features, confirmed above)

- Separate database per group
- JWT-based active tenant claims
- Schema-per-group isolation
- Real-time presence indicators
- Self-service group creation by non-SuperAdmin

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Group entity + migration | LOW (invisible) | LOW | P1 (unblocks everything) |
| GroupId FK + data migration on all entities | LOW (invisible) | MEDIUM | P1 (isolation foundation) |
| EF Core Global Query Filters | HIGH (correctness guarantee) | MEDIUM | P1 |
| UserGroupEntity junction table | HIGH | MEDIUM | P1 |
| ITenantService (session-based) | HIGH (wires filters to requests) | LOW | P1 |
| Group picker at login | MEDIUM (only needed for multi-group users) | LOW | P1 |
| Group switcher in navbar | MEDIUM (only needed for multi-group users) | LOW | P1 |
| Admin-only user creation | HIGH (security) | LOW | P1 |
| Remove self-registration | HIGH (security) | LOW | P1 |
| SuperAdmin role + policy | MEDIUM | LOW | P1 |
| SuperAdmin management area (/Groups) | MEDIUM | MEDIUM | P1 |
| Data migration seeding GroupId=1 | HIGH (data integrity) | LOW | P1 (part of migration) |
| Per-group role scoping | LOW at 1-2 groups | HIGH | P3 |
| Group invite links | LOW at current scale | MEDIUM | P3 |

---

## Dependencies on Existing Systems

| Existing System | Impact | Required Change |
|----------------|--------|-----------------|
| `QuestBoardContext` (EF Core DbContext) | Must inject `ITenantService`, apply filters in `OnModelCreating` | Constructor change + `HasQueryFilter` calls per entity |
| `UserEntity` (ASP.NET Identity) | Needs navigation to `ICollection<UserGroupEntity>` | EF config only, no identity table schema change |
| `AccountController.Register` | Must be locked down (admin-only or removed) | Authorization attribute or route removal |
| `AdminController.Users()` | Must filter by active group via `UserGroupEntity` | Query change + GroupId context |
| `QuestService`, `ShopService`, `CharacterService` | Data already group-filtered by Global Query Filter — no service code changes needed | None (filter is transparent to services) |
| `Hangfire reminder job` | Queries quests — these are already group-scoped via filter | ReminderLog also needs GroupId if reminders must be group-isolated |
| Session (IdleTimeout = 24h) | ActiveGroupId stored here — already configured | Just write/read int from session |
| Authorization policies (AdminOnly, DungeonMasterOnly) | Must continue to work within group context | Add SuperAdminOnly policy; existing policies unchanged |
| AutoMapper profiles (EntityProfile, ViewModelProfile) | Group model and ViewModel needed | New `GroupEntity → Group → GroupViewModel` mapping pair |

---

## Sources

- EF Core Global Query Filters + multi-tenancy (official): [Microsoft Learn — Multi-tenancy EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) — MEDIUM confidence (official docs, sound patterns)
- EF Core 10 named filters + selective IgnoreQueryFilters: [codewithmukesh.com — Global Query Filters EFCore](https://codewithmukesh.com/blog/global-query-filters-efcore/) — MEDIUM confidence
- Multi-tenant admin hierarchy (SuperAdmin vs tenant Admin): [The Reformed Programmer — Building ASP.NET Core multi-tenant apps Part 2](https://www.thereformedprogrammer.net/building-asp-net-core-and-ef-core-multi-tenant-apps-part2-administration/) — MEDIUM confidence
- Tenant context resolution (ITenantProvider, session vs claims): [Anton Dev Tips — How to implement multitenancy in ASP.NET Core](https://antondevtips.com/blog/how-to-implement-multitenancy-in-asp-net-core-with-ef-core) — LOW confidence (websearch)
- Group switcher UX patterns: [WorkOS — Multi-tenant session management](https://workos.com/blog/multi-tenant-session-management) + [Auth0 multi-tenant best practices](https://auth0.com/docs/get-started/auth0-overview/create-tenants/multi-tenant-apps-best-practices) — LOW confidence (industry-observed patterns)
- Disabling ASP.NET Core Identity self-registration: [andyp.dev — Disable user registrations](https://andyp.dev/posts/disable-user-registrations-in-asp-net-core-3-identity) + [damienbod.com — Disabling parts of Identity](https://damienbod.com/2018/08/07/disabling-parts-of-asp-net-core-identity/) — LOW confidence (websearch, but matches this app's custom controller pattern)

---

*Feature research for: Milestone 5 Multi-Tenancy — D&D Quest Board*
*Researched: 2026-06-29*
