# Architecture Patterns — Omphalos SSO Integration

**Domain:** Cross-app signed-token SSO bridge between two independently-deployed .NET apps
**Researched:** 2026-07-02
**Confidence:** HIGH (all findings from direct source inspection of both repos, no speculation)

## Recommended Architecture

Two apps, no compile-time coupling, one documented contract: a short-lived signed token
Quest Board mints and Omphalos verifies. Everything else is each app extending its own
existing layering.

```
Quest Board (issuer)                          Omphalos (verifier)
─────────────────────                         ─────────────────────
DM clicks "Open DM Tool" / "Open Session Notes"
        │
        ▼
QuestController / a new nav partial
        │  calls
        ▼
ISsoTokenService.GenerateToken(user, questId?)   [QuestBoard.Domain — NEW]
        │  reads secret via
        ▼
IIntegrationSettingsService (Platform-scoped)     [QuestBoard.Domain — NEW]
        │  persisted by
        ▼
IntegrationSettingRepository → IntegrationSettingEntity (EF Core, SQL Server)  [NEW]
        │
        ▼
302 redirect to  {OmphalosBaseUrl}/sso?token={signedToken}&questId={id?}
                                                        │
                                                        ▼
                                          SsoEndpoints.cs — MapSsoEndpoints()  [Omphalos.Web — NEW]
                                                        │  AllowAnonymous, validates HMAC + exp
                                                        ▼
                                          SsoService.HandleLoginAsync(...)     [Omphalos.Services — NEW]
                                                        │  find-or-create User by external key
                                                        ▼
                                          IUserRepository (extended)          [Omphalos — MODIFIED]
                                                        │  find-or-create GameSession by QuestId link
                                                        ▼
                                          ISessionRepository (extended)       [Omphalos — MODIFIED]
                                                        │
                                                        ▼
                                          Sets omphalos_token cookie, 302 → SPA root (?session=id)
                                                        │
                                                        ▼
                                          React AppContext reads ?session= on INIT, sets activeSessionId
```

### Component Boundaries

| Component | Repo / Layer | Responsibility | Talks To |
|-----------|--------------|-----------------|----------|
| `IIntegrationSettingsService` / `IntegrationSettingsService` | Quest Board `Domain` | Read/write instance-wide Omphalos URL + shared secret | `IIntegrationSettingRepository` |
| `IIntegrationSettingRepository` / `IntegrationSettingRepository` | Quest Board `Repository` | EF Core persistence for the single settings row | `QuestBoardContext` |
| `IntegrationSettingEntity` | Quest Board `Repository` | EF entity, one row holding `OmphalosBaseUrl`, `OmphalosSharedSecret` | — |
| `ISsoTokenService` / `SsoTokenService` | Quest Board `Domain` | Builds the signed payload (user identity claims + optional questId + exp), HMAC-signs it, base64url-encodes | `IIntegrationSettingsService` |
| `Areas/Platform/Controllers/SettingsController` | Quest Board `Service` (Platform Area) | SuperAdmin CRUD for the Omphalos URL/secret | `IIntegrationSettingsService` |
| `SsoController` (new, standalone) | Quest Board `Service` | Builds redirect URL, issues `302` | `ISsoTokenService`, `IIntegrationSettingsService` |
| `_Layout.cshtml` DM dropdown + `Details.cshtml` button | Quest Board `Service` Views | Entry points into the redirect action | `SsoController` action |
| `SsoEndpoints.cs` (`MapSsoEndpoints`) | Omphalos `Web/Endpoints` | Anonymous HTTP entry point, validates token, delegates | `ISsoService` |
| `ISsoService` / `SsoService` | Omphalos `Services/Implementations` | Validates signature/expiry, find-or-create user, find-or-create session, issues Omphalos's own JWT cookie | `IUserRepository`, `ISessionRepository`, reuses `AuthService`'s token-issuing logic |
| `IUserRepository` (extended) | Omphalos `Domain`/`Repository` | Add lookup by external (Quest Board) user id | `OmphalosDbContext` |
| `GameSession` entity (extended) | Omphalos `Domain`/`Repository` | New nullable `QuestBoardQuestId` column for session linking | `OmphalosDbContext` |
| React `AppContext.jsx` (extended) | Omphalos `client` | Read `?session=` query param on load, override `activeSessionId` after `INIT` | `db.getAllSessions()` |

### Data Flow

1. DM is authenticated in Quest Board, viewing a quest (or just wants the general DM tool).
2. DM clicks "Open DM Tool" (navbar) or "Open Session Notes" (quest `Details.cshtml`, only rendered when `ViewBag.CanManage` is true — the same flag already gating the existing Manage button).
3. Quest Board action (`GET`) resolves the SuperAdmin-configured Omphalos base URL + shared secret via `IIntegrationSettingsService`, builds a signed token via `ISsoTokenService.GenerateToken(currentUser, questId: id?)`, and issues an HTTP **302 redirect** — there is no shared cookie domain between the two apps (different hosts/ports), so a redirect with the token in the URL is the only viable transport. No form POST, no iframe: a straight anchor link works because the action itself performs the redirect server-side.
4. Browser follows the redirect to `{OmphalosBaseUrl}/api/sso/login?token=...&questId=...`.
5. Omphalos's `SsoEndpoints.MapSsoEndpoints()` group (registered `AllowAnonymous()`, same pattern as `/api/auth/login`) receives the request, hands off to `ISsoService`.
6. `SsoService` validates the HMAC signature and expiry (short-lived — minutes, not the 30-day `omphalos_token` lifetime) against the same shared secret configured on both sides.
7. Find-or-create the `User`: Omphalos's `User` entity has a **unique index on `Username`**, no `Email` field at all. The token payload must carry a stable external identifier from Quest Board (its `UserEntity.Id` int) that `SsoService` maps to/creates an Omphalos `Username`. This needs a decision recorded before implementation (see Gaps below) — a new nullable `ExternalId` (Quest Board's numeric user Id) column on Omphalos's `User` entity so re-logins don't collide on display-name changes.
8. Find-or-create the `GameSession`: if the token carries a `questId`, `ISsoService` looks up (or creates) a `GameSession` row linked to that Quest Board quest ID via a new nullable column, scoped to the just-resolved `UserId` (Omphalos sessions are `UserId`-scoped, not shared across users — so "the DM's session for quest N" is naturally per-DM already).
9. `SsoService` calls the same token-generation path `AuthService` already uses (currently a **private** `GenerateToken(User)` method) to mint Omphalos's own 30-day JWT, sets it as the `omphalos_token` httpOnly cookie exactly as `/api/auth/login` does.
10. Final redirect to the SPA root, `?session={gameSessionId}` in the query string.
11. React `AppContext.jsx`'s existing `INIT` effect (currently defaults `activeSessionId: sessions?.[0]?.id ?? null`) needs a small change to prefer a session ID read from `window.location.search` if present and found in the loaded list, overriding the "first session" default. This is the one required **client-side** code change, small and additive.

## Patterns to Follow

### Pattern 1: Quest Board — instance-wide settings need a new DB-backed entity, not `appsettings.json`

**What:** `EmailSettings` (`QuestBoard.Domain/Models/EmailSettings.cs`) is the only existing
"settings" precedent, and it is 100% `appsettings.json` + env-var driven via
`services.AddOptions<EmailSettings>().BindConfiguration("EmailSettings")` in
`QuestBoard.Domain/Extensions/ServiceExtensions.cs`. **This pattern does not fit the requirement.**
The milestone spec explicitly requires a SuperAdmin-editable **Platform Settings page** — i.e.
runtime-editable, not deploy-time config. There is no existing key-value settings entity anywhere
in `QuestBoard.Repository/Entities/` (verified: 18 entity files, none settings-shaped). This is
genuinely new: a small `IntegrationSettingEntity` (single row) plus migration, following the exact
same `Entity → Repository → Domain Service → Controller` chain as
`GroupEntity → GroupRepository → GroupService → GroupController`.

**When:** Any config a SuperAdmin needs to change without a redeploy.

**Example (entity, following `GroupEntity`'s shape):**
```csharp
namespace QuestBoard.Repository.Entities;

[Table("IntegrationSettings")]
public class IntegrationSettingEntity : IEntity
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(500)]
    public string OmphalosBaseUrl { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string OmphalosSharedSecret { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```
A single-row table (`Id = 1` always) is simplest given "instance-wide, not per-group" was already
decided — no need for a generic key-value schema unless a second unrelated integration setting
shows up later.

### Pattern 2: Quest Board — Repository classes are flat, `internal`, and extend `BaseRepository<TDomain, TEntity>`

**What:** `QuestBoard.Repository/GroupRepository.cs` (confirmed via direct read) lives at the
project **root**, not in a `Repositories/` subfolder — despite `IGroupRepository` implying that
folder name. Every repository is `internal class XRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<TDomain, TEntity>(dbContext, mapper), IXRepository`.

**When:** Any new Quest Board repository — `IntegrationSettingRepository` should follow this exact
shape and location (flat file at `QuestBoard.Repository/IntegrationSettingRepository.cs`, not a
new subfolder — don't introduce a folder convention the rest of the codebase doesn't use).

### Pattern 3: Quest Board — DI registration is two parallel extension methods, one per layer

**What:** `QuestBoard.Repository/Extensions/ServiceExtensions.cs` → `AddRepositoryServices(IConfiguration)`
registers `DbContext` + all `IXRepository → XRepository`. `QuestBoard.Domain/Extensions/ServiceExtensions.cs`
→ `AddDomainServices(IConfiguration)` registers all `IXService → XService` plus any `AddOptions<T>()`
bindings. `Program.cs` calls both in sequence: `builder.Services.AddRepositoryServices(...).AddDomainServices(...)`.

**When:** Registering `IIntegrationSettingRepository`/`IIntegrationSettingService`/`ISsoTokenService` —
add one line to each extension method, no new registration pattern needed.

### Pattern 4: Quest Board — Platform Area controllers are `[Area("Platform")]` + `[Authorize(Policy = "SuperAdminOnly")]`, constructor-injected services only

**What:** `GroupController` (the only existing Platform controller) is a thin MVC controller:
validate → call `IGroupService` → `TempData` message → `RedirectToAction`. No business logic in
the controller itself. Views live under `Areas/Platform/Views/{ControllerName}/`, sharing
`Areas/Platform/Views/Shared/_Layout.Platform.cshtml`.

**When:** The new Settings page — `Areas/Platform/Controllers/SettingsController.cs`, same
`[Area("Platform")] [Authorize(Policy = "SuperAdminOnly")]` header, `Index`/`Edit` GET+POST pair
(this is a single-row settings form, closer to `AccountController.Edit` shape than `GroupController`'s
full CRUD — no Create/Delete needed since the row always exists after first save).

### Pattern 5: Quest Board — nav links are gated by `AuthorizationService.AuthorizeAsync(User, "PolicyName")` inline in `_Layout.cshtml`

**What:** The DM dropdown in `_Layout.cshtml` (lines 74–100) is rendered only if
`(await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded`. "Open DM Tool"
belongs as a new `<li>` inside that same existing dropdown block — not a new top-level nav item —
matching "Create Quest" / "Manage Shop" / "Edit My Profile" siblings already there.

**When:** Adding the navbar entry point. No new authorization policy needed; `DungeonMasterOnly`
already covers DM+Admin.

### Pattern 6: Quest Board — quest-page action buttons are conditionally rendered via `ViewBag.CanManage`

**What:** `QuestController.Details` (line 288–290) computes
`ViewBag.CanManage = isQuestDm || isAdmin` and `Details.cshtml` (line 611) gates the entire
"Manage Quest" card on `@if ((bool)ViewBag.CanManage)`. "Open Session Notes" belongs inside or
adjacent to that same existing card, reusing the same `CanManage` computation — don't duplicate
the DM/Admin check.

### Pattern 7: Omphalos — every feature is `IXRepository` (Domain interface) → `XRepository` (Repository impl) → `IXService` (Domain interface) → `XService` (Services impl) → `MapXEndpoints()` (Web static extension), registered flat in `Program.cs`

**What:** Confirmed identical 4-file shape for every existing Omphalos feature (`Session`, `User`,
`GlobalLocation`, `GlobalCharacter`, `Auth`). `Program.cs` registers repositories first, then
services, in two flat blocks — no per-feature extension-method grouping like Quest Board has (all
`AddScoped<>` calls sit directly in `Program.cs`, not in a `ServiceExtensions.cs`).

**When:** The new SSO feature — create `ISsoService`/`SsoService` (no new repository needed; it
composes `IUserRepository` + `ISessionRepository`), and `SsoEndpoints.cs` with `MapSsoEndpoints()`,
registered the same flat way: `builder.Services.AddScoped<ISsoService, SsoService>();` next to the
other service registrations, `app.MapSsoEndpoints();` next to the other endpoint maps.

### Pattern 8: Omphalos — endpoints are `MapGroup("/api/x")`, auth via `.RequireAuthorization()` or `.AllowAnonymous()` at the group or per-route level

**What:** `AuthEndpoints` mixes both on one group: `/login` and `/logout` are `.AllowAnonymous()`,
`/me` is `.RequireAuthorization()` — set per-route, not group-wide, because the group itself
carries no blanket policy. `AdminEndpoints`/`SessionEndpoints`/`SettingsEndpoints` set
`.RequireAuthorization()` (or `.RequireAuthorization("AdminOnly")`) at the **group** level since
every route inside needs it uniformly.

**When:** `SsoEndpoints` — the token-validation route must be `.AllowAnonymous()` (the whole point
is the caller isn't yet authenticated to Omphalos), following the exact `AuthEndpoints` shape:
```csharp
public static class SsoEndpoints
{
    public static IEndpointRouteBuilder MapSsoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sso");

        group.MapGet("/login", async (string token, string? questId, ISsoService sso, HttpContext http, CancellationToken ct) =>
        {
            var result = await sso.HandleLoginAsync(token, questId, ct);
            if (result is null) return Results.Unauthorized();

            http.Response.Cookies.Append("omphalos_token", result.Token, TokenCookieOptions);
            return Results.Redirect($"/?session={result.SessionId}");
        }).AllowAnonymous();

        return app;
    }
}
```

### Pattern 9: Omphalos — `AuthService.GenerateToken` needs to move onto `IAuthService` (or a shared internal helper) rather than being duplicated

**What:** `AuthService.GenerateToken(User)` is currently `private`. `SsoService` needs to issue the
exact same Omphalos-native JWT (same claims shape: `sub`=userId, `unique_name`=username,
`role`=role) so the resulting cookie behaves identically to a normal login. PROJECT.md already
flags this ("not yet on IAuthService") as a known gap.

**When:** Implementing `SsoService`. Two viable approaches, in order of preference:
1. **Add `Task<string> GenerateTokenAsync(User user)` (or keep sync) to `IAuthService`**, make the
   existing `AuthService.GenerateToken` public/interface-exposed, inject `IAuthService` into
   `SsoService` and call it directly. Minimal surface change, single source of truth for token
   shape. (Preferred — smallest diff, no duplicated JWT-building code.)
2. Extract a small internal `ITokenIssuer`/`JwtTokenFactory` used by both `AuthService` and
   `SsoService`. Only worth it if `AuthService` picks up more responsibilities later; over-engineered
   for this milestone.

### Pattern 10: Omphalos — `GameSession.Id` is a client-generated string, not a DB identity column

**What:** `GameSession.Id` is `string`, populated client-side as `` `session-${Date.now()}` `` (see
`App.jsx` `createSession()`), not DB-generated. `SessionRepository.UpsertAsync` keys off
`(Id, UserId)` composite match. This matters for "find or create the matching quest session":
the **server-side** SSO flow is the first place in Omphalos that will ever create a `GameSession`
row without a client-generated ID — a new server-side ID scheme is needed (e.g.
`$"qb-quest-{questId}-{userId}"` deterministic string, so repeat SSO hits for the same quest+DM
resolve to the same row instead of creating duplicates every click).

**When:** `SsoService`'s session find-or-create step — must NOT reuse the client's
`session-${timestamp}` convention (collides with nothing, but is non-deterministic and defeats
"find" semantics). Use a deterministic, prefixed ID derived from the Quest Board quest ID so a
second click on "Open Session Notes" for the same quest reopens the same session instead of
spawning a new one.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Reusing Omphalos's `Jwt:Secret` as the SSO shared secret

**What:** It's tempting to make "the shared secret" the same value as Omphalos's existing
`Jwt:Secret` config key, since both are HMAC-signing secrets living in Omphalos's config.

**Why bad:** Conflates two different trust boundaries. `Jwt:Secret` signs Omphalos's own
long-lived (30-day) session tokens — if that leaks, every active Omphalos session is
forgeable. The SSO shared secret only needs to be valid for the handshake (short-lived, single
use for login), and is also configured on the **Quest Board** side (in the SuperAdmin Platform
Settings UI) — a genuinely different value with a different blast radius and different owner.
Reusing `Jwt:Secret` means Quest Board's SuperAdmin settings page now holds a credential that,
if compromised, lets an attacker forge arbitrary Omphalos session cookies directly, not just
SSO handshakes.

**Instead:** A distinct `Sso:SharedSecret` config key on the Omphalos side, matched by the value
SuperAdmin enters in Quest Board's Platform Settings page. Keep `Jwt:Secret` scoped to what it
already does.

### Anti-Pattern 2: Making the SSO token long-lived like `omphalos_token`

**What:** Reusing the 30-day expiry pattern from `AuthEndpoints.TokenCookieOptions` for the
SSO handshake token itself (the one in the redirect URL).

**Why bad:** The handshake token travels in a URL (browser history, server access logs, referrer
headers if any downstream redirect leaks it) — a 30-day-valid credential sitting in a log file is
a real exposure. It only needs to survive the few seconds between Quest Board issuing the
redirect and Omphalos consuming it.

**Instead:** 1–5 minute expiry on the handshake token specifically (separate from the 30-day
`omphalos_token` cookie minted *after* validation, which is fine to keep at its current lifetime).

### Anti-Pattern 3: Matching Omphalos users by `Username` collision with Quest Board's `Name`

**What:** Naively creating/matching an Omphalos `User` by `Username == questBoardUser.Name`.

**Why bad:** Quest Board's own `AccountController.Edit` (per PROJECT.md's Key Decisions table,
Phase 34.3) already had a real security bug from assuming `User.Name`/`Username` is a stable
unique identity — display names have no cross-system uniqueness guarantee and can be freely
renamed. Two different Quest Board users could plausibly end up mapped to the same Omphalos
account, or a renamed user could silently "become" a different Omphalos identity.

**Instead:** Carry Quest Board's stable numeric `UserEntity.Id` in the signed token payload and
match/store it against a new nullable `ExternalId` (or similar) column on Omphalos's `User`
entity — exactly the same "use the stable ID, not the display name" lesson Quest Board's own
Phase 34.3 fix already encoded elsewhere in this codebase.

### Anti-Pattern 4: Building a client-side React route (`react-router`) for `/sso` landing

**What:** Adding `react-router-dom` (or similar) to handle a distinct `/sso-landing` client route
that then dispatches session selection.

**Why bad:** Omphalos's SPA has **no router today** — `App.jsx` is a single view switched by
reducer state (`view: 'sessions' | 'library'`), not URL-driven. Introducing a router just for this
one landing case is a disproportionate dependency/architecture change for a milestone whose own
scope note says "foundation for a future API, not built now" — i.e. minimal footprint is the
explicit intent.

**Instead:** The whole SSO exchange is server-side (Quest Board → Omphalos `/api/sso/login` →
redirect to SPA root with `?session=<id>` in the query string). The only client change is
`AppContext.jsx`'s `INIT` effect reading that one query param once, on mount — no routing library
needed.

### Anti-Pattern 5: Putting Omphalos integration settings on `GroupEntity`

**What:** Adding `OmphalosUrl`/`OmphalosSecret` columns to `GroupEntity` since that's where
group-scoped config would normally live in this now-multi-tenant codebase.

**Why bad:** Already explicitly decided against in PROJECT.md's Key Decisions table — Omphalos
has no tenant concept on its own side, so a per-group value would have nothing to map onto when
Omphalos validates the token. Confirmed independently during this research: Omphalos's `User`
entity has zero group/org/tenant fields anywhere in `Omphalos.Domain/Entities/`.

**Instead:** The new `IntegrationSettingEntity` (single instance-wide row) as described in
Pattern 1, gated by `SuperAdminOnly`, not `AdminOnly`.

## Scalability Considerations

Not a meaningful axis for this milestone — both apps are small self-hosted single-instance
deployments (Quest Board: 17 members on an LXC container; Omphalos: single-maintainer scale).
No connection pooling, caching, or horizontal-scaling concerns apply. The one real constraint is
**clock skew** between the two hosts for the short-lived handshake token's expiry check — worth a
generous-but-bounded clock-skew allowance (e.g. ±30s) in Omphalos's validation rather than an
exact-match window, since both apps deploy to different hosts with no guaranteed NTP sync
verification.

## New vs Modified Components — Quest Board (`C:\Repos\quest-board`)

| Component | File (new path) | Status | Layer |
|-----------|------------------|--------|-------|
| `IntegrationSettingEntity` | `QuestBoard.Repository/Entities/IntegrationSettingEntity.cs` | NEW | Repository |
| EF migration (`AddIntegrationSettings`) | `QuestBoard.Repository/Migrations/<timestamp>_AddIntegrationSettings.cs` | NEW | Repository |
| `IntegrationSetting` domain model | `QuestBoard.Domain/Models/IntegrationSetting.cs` | NEW | Domain |
| `IIntegrationSettingRepository` | `QuestBoard.Domain/Interfaces/IIntegrationSettingRepository.cs` | NEW | Domain (interface) |
| `IntegrationSettingRepository` | `QuestBoard.Repository/IntegrationSettingRepository.cs` (flat, per Pattern 2) | NEW | Repository (impl) |
| `IIntegrationSettingsService` | `QuestBoard.Domain/Interfaces/IIntegrationSettingsService.cs` | NEW | Domain (interface) |
| `IntegrationSettingsService` | `QuestBoard.Domain/Services/IntegrationSettingsService.cs` | NEW | Domain (impl) |
| `ISsoTokenService` | `QuestBoard.Domain/Interfaces/ISsoTokenService.cs` | NEW | Domain (interface) |
| `SsoTokenService` (HMAC-SHA256 sign, short expiry, base64url payload) | `QuestBoard.Domain/Services/SsoTokenService.cs` | NEW | Domain (impl) |
| `EntityProfile` AutoMapper additions (Entity ↔ Domain for the new model) | `QuestBoard.Domain/Automapper/EntityProfile.cs` | MODIFIED | Domain |
| `AddRepositoryServices` registration | `QuestBoard.Repository/Extensions/ServiceExtensions.cs` | MODIFIED | Repository |
| `AddDomainServices` registration | `QuestBoard.Domain/Extensions/ServiceExtensions.cs` | MODIFIED | Domain |
| `Areas/Platform/Controllers/SettingsController.cs` | new file | NEW | Service (Platform Area) |
| `Areas/Platform/Views/Settings/Index.cshtml` (edit form) | new file | NEW | Service (Platform Area views) |
| `ViewModelProfile` additions (Domain ↔ ViewModel for the settings form) | `QuestBoard.Service/Automapper/ViewModelProfile.cs` | MODIFIED | Service |
| `QuestBoard.Service.ViewModels.PlatformViewModels` — `IntegrationSettingsViewModel` | new file, same folder as `GroupCreateViewModel` etc. | NEW | Service |
| SSO redirect action (`SsoController.OpenDmTool` / `.OpenSessionNotes`) | `QuestBoard.Service/Controllers/SsoController.cs` (its own controller — the redirect concern is orthogonal to quest CRUD, not added onto `QuestController`) | NEW | Service |
| `_Layout.cshtml` DM dropdown — "Open DM Tool" link | `QuestBoard.Service/Views/Shared/_Layout.cshtml` (inside existing DM dropdown, ~line 96) | MODIFIED | Service (view) |
| `Details.cshtml` — "Open Session Notes" button | `QuestBoard.Service/Views/Quest/Details.cshtml` (inside/near existing `CanManage` card, ~line 611+) | MODIFIED | Service (view) |
| `Details.Mobile.cshtml` — same button, mobile view | `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` | MODIFIED | Service (view) |

**Not needed:** no changes to `QuestBoardContext.cs`'s Global Query Filters — `IntegrationSettingEntity`
is instance-wide and must NOT be tenant-filtered; register it as a `DbSet` without the group query
filter applied to other entities (verify against however `QuestBoardContext.cs` currently scopes
filters — `GroupEntity` itself and any instance-wide entity are presumably already exempt, follow
that same exemption).

## New vs Modified Components — Omphalos (`C:\Repos\omphalos`)

| Component | File (new path) | Status | Layer |
|-----------|------------------|--------|-------|
| `SsoLoginRequest`/`SsoLoginResult` DTOs | `Omphalos.Domain/DTOs/SsoDtos.cs` (or added to an existing DTO file) | NEW | Domain |
| `ISsoService` | `Omphalos.Domain/Interfaces/ISsoService.cs` | NEW | Domain (interface) |
| `IAuthService.GenerateTokenAsync` (promote from `AuthService.GenerateToken` private method) | `Omphalos.Domain/Interfaces/IAuthService.cs` | MODIFIED | Domain (interface) |
| `AuthService.GenerateToken` → public/interface-exposed | `Omphalos.Services/Implementations/AuthService.cs` | MODIFIED | Services |
| `SsoService` (validates HMAC token, find-or-create user, find-or-create session, calls `IAuthService` for the Omphalos JWT) | `Omphalos.Services/Implementations/SsoService.cs` | NEW | Services |
| `User.ExternalId` (nullable, stores Quest Board's numeric user Id) | `Omphalos.Domain/Entities/User.cs` | MODIFIED | Domain |
| `UserConfiguration` — index on `ExternalId` | `Omphalos.Repository/Configurations/UserConfiguration.cs` | MODIFIED | Repository |
| `IUserRepository.GetByExternalIdAsync` | `Omphalos.Domain/Interfaces/IUserRepository.cs` | MODIFIED | Domain (interface) |
| `UserRepository.GetByExternalIdAsync` impl | `Omphalos.Repository/Repositories/UserRepository.cs` | MODIFIED | Repository |
| `GameSession.QuestBoardQuestId` (nullable int/string, links back to the originating quest) | `Omphalos.Domain/Entities/GameSession.cs` | MODIFIED | Domain |
| `GameSessionConfiguration` — index on `(UserId, QuestBoardQuestId)` | `Omphalos.Repository/Configurations/GameSessionConfiguration.cs` | MODIFIED | Repository |
| `ISessionRepository.GetByQuestBoardQuestIdAsync` (or extend `GetByIdAsync` semantics) | `Omphalos.Domain/Interfaces/ISessionRepository.cs` | MODIFIED | Domain (interface) |
| `SessionRepository` impl of the above | `Omphalos.Repository/Repositories/SessionRepository.cs` | MODIFIED | Repository |
| EF migration (`AddSsoLinking`) | `Omphalos.Repository/Migrations/<timestamp>_AddSsoLinking.cs` | NEW | Repository |
| `SsoEndpoints.cs` (`MapSsoEndpoints`, `AllowAnonymous`) | `Omphalos.Web/Endpoints/SsoEndpoints.cs` | NEW | Web |
| `Program.cs` — register `ISsoService`, `app.MapSsoEndpoints()` | `Omphalos.Web/Program.cs` | MODIFIED | Web |
| `appsettings.json` — `Sso:SharedSecret` config key | `Omphalos.Web/appsettings.json` | MODIFIED | Web (config) |
| `appsettings.json` — `AllowedOrigins` populated with Quest Board's origin | `Omphalos.Web/appsettings.json` | MODIFIED | Web (config) — only strictly needed if any XHR/fetch call crosses origins; the redirect-based flow itself is a top-level navigation, not a fetch, so CORS isn't a hard blocker, but populate it anyway since it's currently inert and this is the first real cross-origin consumer |
| `AppContext.jsx` — read `?session=` on `INIT`, override `activeSessionId` | `client/context/AppContext.jsx` (the `INIT` case and/or the "load data" `useEffect`) | MODIFIED | client (React) |

**Not needed:** no new repository file for SSO — `SsoService` composes the existing
`IUserRepository` + `ISessionRepository`, consistent with how `SessionService`/`UserService`
never introduced dedicated repositories beyond the 1:1 pattern already established.

## Build Order

These are two separately-deployed repos with **no compile-time coupling** — the only shared
contract is the token format (claims/fields, HMAC algorithm, shared-secret config key names) and
the redirect URL shape. That contract must be pinned down and documented (e.g. in each repo's
`.planning/` or a short design note) before either side writes code, since nothing will catch a
mismatch except a live end-to-end test.

**Recommended order:**

1. **Pin the token contract first** (no code, just a written spec both sides implement against):
   payload fields (Quest Board numeric UserId, Name/Email for display, optional QuestId, issued-at,
   expiry), signing algorithm (HMAC-SHA256 over a compact string or JSON payload — NOT a full JWT
   library dependency on the Quest Board side, since `System.IdentityModel.Tokens.Jwt` is not
   currently a direct `PackageReference` in `QuestBoard.Domain`/`QuestBoard.Service` and pulling
   it in only to mirror what a simple keyed-HMAC already achieves is unnecessary weight — Omphalos
   already has the JWT library on its side for its own unrelated login tokens, no need to
   standardize the SSO handshake token on the same format), shared-secret config key names on each
   side, redirect URL query-param names (`token`, `questId`).

2. **Quest Board side can start immediately and independently** — the settings entity, migration,
   `IIntegrationSettingsService`, `ISsoTokenService`, Platform Settings page, and nav/button entry
   points are all self-contained; they only need the *shape* of the token contract from step 1, not
   a running Omphalos instance. This is genuinely the easier, lower-risk half and has no external
   dependency — build and test it (unit tests on `SsoTokenService` covering signature generation and
   expiry) fully before touching Omphalos.

3. **Omphalos side depends on step 1's contract but not on step 2's code** — `ISsoService`,
   `SsoEndpoints`, the `User.ExternalId`/`GameSession.QuestBoardQuestId` schema changes, and the
   `IAuthService.GenerateTokenAsync` promotion can all be built and unit-tested against a
   hand-crafted valid token matching the contract, without Quest Board running. This is the
   **hard dependency point**: Omphalos is owned/maintained by someone else and changes there go
   through normal PR review on that separate repo (per `PROJECT.md`'s constraints) — this is the
   long pole for scheduling, not the technical complexity. Start this side's PR early given
   external review latency is unpredictable.

4. **Integration/end-to-end verification requires both sides deployed** — this cannot be
   meaningfully tested with mocks alone for the final acceptance check, since the whole point is
   two independently-versioned live services agreeing on a wire format. Plan a manual (or scripted)
   end-to-end smoke test as the last step: real SuperAdmin-configured secret in Quest Board's
   Platform Settings, real click-through, confirm landing in the correct Omphalos session with a
   newly-provisioned or matched user.

5. **The client-side `AppContext.jsx` query-param read** can be built and tested independently of
   both the token contract and the backend SSO endpoint — it only needs *some* value in
   `?session=` and a matching session in the loaded list. Low risk, can be done in parallel with
   step 3, or even before it.

## Gaps to Address

- **Quest Board user identity → Omphalos `Username` mapping is not yet decided.** Omphalos's
  `User` entity has a unique `Username` with no `Email` field. The token contract needs to settle
  whether Omphalos derives/stores a `Username` from Quest Board's `Name` at first provisioning
  (display-only after that, keyed by the new `ExternalId`) or from `Email`. Recommend deciding
  this as part of pinning the token contract (build-order step 1), not left to whoever implements
  the Omphalos side first.
- **Exact redirect URL path on the Omphalos side** (`/api/sso/login` used throughout this document
  as the working assumption, matching the `/api/auth/*` convention) is Omphalos's call to make
  since it owns that route — Quest Board's `SsoController` only needs the base URL + path
  documented in the settings/config, not hardcoded.
- **Whether CORS (`AllowedOrigins`) is strictly required** for this flow is unresolved — the
  primary SSO handshake is a top-level browser navigation (redirect), not a `fetch()`, so CORS may
  not gate it at all. Worth confirming during implementation rather than assuming population is
  required, though populating it costs nothing and unblocks any future in-page fetch-based
  integration (the "foundation for bidirectional API calls" goal already named in PROJECT.md).

## Sources

All findings are HIGH confidence — derived from direct inspection of source files in both repos, not
training-data assumptions or web search. Key files read:

- `C:\Repos\quest-board\.planning\PROJECT.md` — milestone scope, decided constraints, prior
  abandoned-branch context
- `C:\Repos\quest-board\QuestBoard.Service\Areas\Platform\Controllers\GroupController.cs` — Platform
  Area controller pattern
- `C:\Repos\quest-board\QuestBoard.Repository\Entities\GroupEntity.cs`,
  `QuestEntity.cs`, `UserEntity.cs`, `UserGroupEntity.cs` — entity conventions, confirmed no
  existing settings entity
- `C:\Repos\quest-board\QuestBoard.Domain\Interfaces\IGroupService.cs`,
  `IGroupRepository.cs`, `IUserService.cs`, `IActiveGroupContext.cs` — Domain interface conventions
- `C:\Repos\quest-board\QuestBoard.Repository\GroupRepository.cs` — confirmed flat repository file
  location (not in a `Repositories/` subfolder) and `internal class : BaseRepository<T,E>` shape
- `C:\Repos\quest-board\QuestBoard.Domain\Extensions\ServiceExtensions.cs`,
  `QuestBoard.Repository\Extensions\ServiceExtensions.cs` — DI registration pattern
  (`AddDomainServices`/`AddRepositoryServices`)
- `C:\Repos\quest-board\QuestBoard.Domain\Models\EmailSettings.cs` — confirmed existing "settings"
  precedent is `appsettings.json`-bound, not DB-backed (doesn't fit this requirement)
- `C:\Repos\quest-board\QuestBoard.Service\Program.cs` — full DI/middleware pipeline, confirms
  `IActiveGroupContext` dual-registration pattern, Hangfire/Testing branching
- `C:\Repos\quest-board\QuestBoard.Service\Views\Shared\_Layout.cshtml` — navbar DM dropdown
  structure, confirms where "Open DM Tool" belongs
- `C:\Repos\quest-board\QuestBoard.Service\Controllers\QuestBoard\QuestController.cs` — `Details`
  action, `ViewBag.CanManage` computation, confirms `User.Email` availability
- `C:\Repos\quest-board\QuestBoard.Domain\QuestBoard.Domain.csproj` — confirmed no direct JWT
  package reference (only `AutoMapper` + `FrameworkReference`)
- `C:\Repos\omphalos\src\Omphalos.Web\Program.cs` — full Omphalos DI/pipeline, JWT bearer config,
  cookie-from-header extraction, CORS config, endpoint map registrations
- `C:\Repos\omphalos\src\Omphalos.Web\Endpoints\AuthEndpoints.cs`, `SessionEndpoints.cs`,
  `AdminEndpoints.cs`, `SettingsEndpoints.cs` — endpoint-group conventions, `AllowAnonymous` vs
  `RequireAuthorization` placement
- `C:\Repos\omphalos\src\Omphalos.Services\Implementations\AuthService.cs`,
  `UserService.cs` — confirmed `GenerateToken` is private, JWT claims shape, BCrypt usage
- `C:\Repos\omphalos\src\Omphalos.Domain\Interfaces\IAuthService.cs`,
  `IUserRepository.cs`, `ISessionService.cs`, `ISessionRepository.cs` — Domain interface shapes
- `C:\Repos\omphalos\src\Omphalos.Domain\Entities\GameSession.cs`, `User.cs` — confirmed
  `GameSession.Id` is client-generated string, `User` has `Username` (unique) but no `Email` field
- `C:\Repos\omphalos\src\Omphalos.Repository\Repositories\SessionRepository.cs`,
  `UserRepository.cs` — upsert/find patterns
- `C:\Repos\omphalos\src\Omphalos.Repository\OmphalosDbContext.cs`,
  `Configurations\GameSessionConfiguration.cs`, `UserConfiguration.cs` — EF Core configuration
  style (Fluent API `IEntityTypeConfiguration<T>`, PostgreSQL `jsonb`, unique index on `Username`)
- `C:\Repos\omphalos\src\Omphalos.Web\appsettings.json` — confirmed `Jwt`/`AllowedOrigins`/`Admin`
  config shape
- `C:\Repos\omphalos\src\client\App.jsx`, `context\AppContext.jsx`, `db\index.js` — confirmed
  **no client-side router**, single-view SPA gated by reducer state, `activeSessionId` defaults to
  first session, `fetch`-based API client with `credentials: 'include'`
- `C:\Repos\quest-board\QuestBoard.Service\QuestBoard.Service.csproj`,
  `C:\Repos\omphalos\src\Omphalos.Web\Omphalos.Web.csproj`,
  `Omphalos.Services\Omphalos.Services.csproj` — package reference inventories confirming JWT
  library presence/absence on each side
