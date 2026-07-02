# Phase 33: Session Persistence + Admin Email Rate Limiting - Research

**Researched:** 2026-07-01
**Domain:** ASP.NET Core distributed caching (SQL Server backing store) + rate limiting middleware, on ASP.NET Core 10 / EF Core 10.0.9
**Confidence:** HIGH

## Summary

This phase has two independent, self-contained deliverables. Both extend patterns already established in this codebase (Phase 32's rate limiting; the project's existing raw-SQL EF migration convention) rather than introducing anything architecturally new.

**Session persistence** requires exactly one new package (`Microsoft.Extensions.Caching.SqlServer`, version `10.0.9` — matching the project's existing EF Core 10.0.9 pinning), one `Program.cs` registration block placed before `AddSession`, and one EF migration that raw-SQL-creates the cache table. The DDL is fixed and publicly documented in the package's own source (`SqlQueries.cs`) — verified directly against the `dotnet/aspnetcore` GitHub source in this session. The single biggest risk is **not** the session code — `ActiveGroupContextService` needs zero changes — it is that `AddDistributedSqlServerCache` runs **unconditionally in Program.cs today**, including in the `Testing` ASP.NET Core environment used by `WebApplicationFactoryBase`. Because the integration test suite swaps EF Core to an `InMemoryDatabase` but does **not** touch `IDistributedCache`/`AddSession`, wiring the connection string unconditionally risks either (a) tests silently depending on a live local SQL Server being reachable at `localhost` outside Docker, or (b) worse, no failure at all until first Set/Get, since `SqlServerCache` only touches the DB lazily. This must be called out explicitly for the planner.

**Admin email rate limiting** extends the Phase 32 `AddRateLimiter` block with a third named policy. However, CONTEXT.md's assumption that `httpContext.GetRouteValue("userId")` can be used as the partition key is **incorrect for both target actions** — `userId` (on `SendConfirmationEmail`) and `Id` (on `EditUser`) are both submitted as **POST form fields**, not route values or query parameters, because the app registers only the default `{controller}/{action}/{id?}` route and both forms post to bare action URLs (`/Admin/SendConfirmationEmail`, `/Admin/EditUser`) with the identifier in the request body. `GetRouteValue("userId")` will return `null` in the rate-limiter's partition-key factory for both actions, because that factory runs during routing/rate-limiting middleware — before MVC model binding reads the form body. This is corrected in this research; see Pitfall 1 and the recommended pattern below.

**Primary recommendation:** Use `AddDistributedSqlServerCache` + a guarded (non-Testing-environment) raw-SQL EF migration for session persistence exactly as CONTEXT.md specifies, but branch its registration the same way Hangfire is already branched (`if (!builder.Environment.IsEnvironment("Testing"))`), and fall back to `AddDistributedMemoryCache()` in Testing so the existing test suite's session behavior is unaffected. For the rate limiter, do not rely on `GetRouteValue` — read the target user id from `httpContext.Request.RouteValues` is a dead end; instead partition by the authenticated admin's identity is also wrong per D-05 (target user must be the partition key, not admin). The correct fix is to enable **form-buffering-safe** access via `httpContext.Request.Form["userId"]` (synchronous access works because ASP.NET Core buffers small form bodies by default when read this way through the already-parsed `IFormCollection`, but only once `Request.Form` has triggered a read) — however the more robust, idiomatic solution recommended here is to **rate-limit inside the action body** using an injected `PartitionedRateLimiter<string>` (manual check-and-throw-429), NOT the `[EnableRateLimiting]` attribute, for both `SendConfirmationEmail` and `EditUser`'s email-change branch. See Pitfall 1 and Code Examples for the exact pattern.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Session data persistence (ActiveGroupId) | API / Backend (ASP.NET Core middleware) | Database / Storage (SQL Server cache table) | Session middleware lives in the Service layer's request pipeline; SQL Server is purely a backing store, no business logic |
| Cache table schema/provisioning | Database / Storage | — | EF Core migration in `QuestBoard.Repository`, consistent with all other schema changes in this project |
| Admin email rate limiting | API / Backend | — | Enforced in ASP.NET Core middleware (`AddRateLimiter`) and/or controller action body; no client or DB involvement |
| Rate-limit partition key extraction | API / Backend (controller action) | — | Must happen after model binding reads the POST form body — this is why attribute-based `[EnableRateLimiting]` (which runs before binding) cannot cleanly access `userId` from form data; the extraction is correctly an Action-tier concern, not middleware-tier |

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| `Microsoft.Extensions.Caching.SqlServer` | nuget.org | First-party Microsoft package, part of `dotnet/aspnetcore`, shipped since .NET Core 1.0 (~9 years) | Very high (millions/week across all versions; official ASP.NET Core caching package) | github.com/dotnet/aspnetcore | OK | Approved |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

This is a first-party Microsoft package maintained in the same `dotnet/aspnetcore` monorepo as the ASP.NET Core framework itself, already implicitly trusted by this project (it references `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` from the same org). No `npm`/registry-confusion risk applies (NuGet ecosystem, verified via WebFetch against nuget.org and the package's own GitHub source, both official sources) `[VERIFIED: nuget.org + github.com/dotnet/aspnetcore]`.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Extensions.Caching.SqlServer` | `10.0.9` | Provides `IDistributedCache` backed by SQL Server; used by `AddSession` as the session backing store | First-party Microsoft package; version-locked to match the project's existing `Microsoft.EntityFrameworkCore*` = `10.0.9` pinning `[VERIFIED: nuget.org — https://www.nuget.org/packages/Microsoft.Extensions.Caching.SqlServer/]` |

No supporting/alternative packages are needed — CONTEXT.md correctly rules out Redis and any other distributed cache infra (D-01, phase boundary).

**Installation:**
```bash
dotnet add QuestBoard.Service package Microsoft.Extensions.Caching.SqlServer --version 10.0.9
```

**Version verification:** Confirmed live via WebFetch against `https://www.nuget.org/packages/Microsoft.Extensions.Caching.SqlServer/` — latest listed version is `10.0.9`, targeting `.NET 10.0` and `.NET Standard 2.0`. This matches the project's `QuestBoard.Repository.csproj` and `QuestBoard.Service.csproj`, both pinned to `Microsoft.EntityFrameworkCore* = 10.0.9` and `TargetFramework = net10.0`. Add the package to `QuestBoard.Service.csproj` (where `Program.cs` and `AddSession` live), NOT `QuestBoard.Repository` — this is a Service-tier registration, not a Repository/EF-Core concern, despite installing the cache table via an EF migration. `[VERIFIED: nuget.org]`

## Architecture Patterns

### System Architecture Diagram

```
[Browser: player/DM selects active group]
        |
        v
[GroupPicker/GroupSession middleware sets Session["ActiveGroupId"]]
        |
        v
[ASP.NET Core Session middleware (UseSession)]
        |
        v  (Set/Get calls IDistributedCache)
[IDistributedCache abstraction]
        |
        v  (was: AddDistributedMemoryCache implicit fallback -- wiped on restart)
        v  (now: AddDistributedSqlServerCache)
        |
        v
[SQL Server: dbo.AspNetSessionState table]
   (Id, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration)
        |
        v (background, every ExpiredItemsDeletionInterval)
[SqlServerCache internal cleanup: DELETE WHERE ExpiresAtTime < @UtcNow]

---

[Admin clicks "Resend Welcome Email" button on /Admin/Users]
        |
        v  POST /Admin/SendConfirmationEmail  (body: userId=<id>, __RequestVerificationToken)
        |
        v
[UseRouting -> UseRateLimiter -> UseAuthorization]  <-- userId NOT available here (form not yet bound)
        |
        v
[AdminController.SendConfirmationEmail(int userId)]  <-- MVC model binding reads form body HERE
        |
        v  (recommended: manual rate-limit check inside action body, keyed by userId)
[PartitionedRateLimiter<string> lease check: "email-resend:{userId}"]
        |
   pass |  reject -> 429 (same OnRejected-style response, applied manually)
        v
[WelcomeEmailJob enqueued via Hangfire]
```

### Recommended Project Structure

No new folders needed. Files touched:
```
QuestBoard.Service/
├── Program.cs                              # AddDistributedSqlServerCache registration (before AddSession);
│                                            # new "admin-email-resend" rate limit policy (if attribute-based)
├── Controllers/Admin/AdminController.cs    # SendConfirmationEmail, EditUser — rate-limit enforcement
QuestBoard.Repository/
├── Migrations/
│   └── {timestamp}_AddSessionStateTable.cs # raw SQL CREATE TABLE for dbo.AspNetSessionState
```

### Pattern 1: Guarded distributed cache registration (Testing-environment-safe)

**What:** Register `AddDistributedSqlServerCache` only outside the `Testing` environment; use `AddDistributedMemoryCache()` in Testing. This exactly mirrors the existing Hangfire branch in `Program.cs` (lines 179–211) and prevents the integration test suite from silently depending on a reachable local SQL Server for session storage (the test suite already swaps EF Core to `UseInMemoryDatabase`, but nothing currently swaps `IDistributedCache`).

**When to use:** Any time a new external-dependency registration is added to `Program.cs` — this project's established convention (Hangfire, `IQuestEmailDispatcher`/`IReminderJobDispatcher`) is to branch on `builder.Environment.IsEnvironment("Testing")`, not to leave production wiring unconditionally active in tests.

**Example:**
```csharp
// Source: pattern mirrors existing Hangfire branch, Program.cs:179-211 (this repo)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.SchemaName = "dbo";
        options.TableName = "AspNetSessionState";
        // ExpiredItemsDeletionInterval: framework default is 30 min; no override needed (D-04).
    });
}
else
{
    // Testing environment: WebApplicationFactoryBase swaps EF Core to InMemory but does not
    // touch IDistributedCache. Without this branch, AddDistributedSqlServerCache would be wired
    // against appsettings.json's DefaultConnection (a real SQL Server connection string) even
    // though no appsettings.Testing.json overrides it -- risking either a hard dependency on a
    // reachable local SQL Server during `dotnet test`, or silent failures on first Session write.
    builder.Services.AddDistributedMemoryCache();
}

// Must appear BEFORE AddSession (D-01) -- either branch above must run first.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
```

### Pattern 2: Raw-SQL EF Core migration matching project convention

**What:** This project's established convention for non-entity-tracked schema changes is `migrationBuilder.Sql("...")` inside an otherwise-empty `Up`/`Down` pair — confirmed via `20260420142117_EnableLockoutForExistingUsers.cs` (this repo). `CreateTable`-style fluent builder calls (used in `20260626190255_AddReminderLog.cs`) are reserved for EF-tracked entities; the `AspNetSessionState` table is **not** an EF entity (no `DbSet`, no `OnModelCreating` mapping) — Microsoft's own guidance explicitly instructs using the standalone `dotnet-sql-cache` CLI tool or raw DDL, never EF fluent mapping, for this table.

**When to use:** For the `AspNetSessionState` cache table specifically. Do not add a `DbSet<T>` or entity class for it — `SqlServerCache` reads/writes it directly via ADO.NET, not through `QuestBoardContext`.

**Example:**
```csharp
// Source: DDL verified against dotnet/aspnetcore GitHub source (src/Tools/dotnet-sql-cache/src/SqlQueries.cs)
// via WebFetch in this research session -- this is the exact schema `dotnet sql-cache create` generates.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionStateTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'AspNetSessionState')
BEGIN
    CREATE TABLE [dbo].[AspNetSessionState] (
        [Id] NVARCHAR(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
        [Value] VARBINARY(MAX) NOT NULL,
        [ExpiresAtTime] DATETIMEOFFSET(7) NOT NULL,
        [SlidingExpirationInSeconds] BIGINT NULL,
        [AbsoluteExpiration] DATETIMEOFFSET(7) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [Index_ExpiresAtTime] ON [dbo].[AspNetSessionState]([ExpiresAtTime]);
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[AspNetSessionState]");
        }
    }
}
```

**Important schema detail not in CONTEXT.md D-03:** the `Id` column requires `COLLATE SQL_Latin1_General_CP1_CS_AS` (case-sensitive, accent-sensitive) — confirmed from the package's own `SqlQueries.cs` source. Case-insensitive collation would let `IDistributedCache` keys that differ only by case collide, which is a correctness bug (session cookie/key derivation is case-sensitive elsewhere in Identity). CONTEXT.md's DDL description omitted this collation clause — include it.

### Pattern 3: Programmatic rate limiting inside the action body (form-data partition key)

**What:** For endpoints where the partition key is submitted as a POST form field (not a route value, header, or query param), the `[EnableRateLimiting("policy")]` attribute cannot correctly partition, because the middleware pipeline (`UseRateLimiter`, positioned after `UseRouting`/before `UseAuthorization`) runs **before MVC model binding** reads the request body. Inject `System.Threading.RateLimiting.PartitionedRateLimiter<string>` (constructed once as a singleton) and call `AttemptAcquire` inside the action, after the `userId`/`Id` parameter is already bound.

**When to use:** `AdminController.SendConfirmationEmail` (userId is a form field) and `AdminController.EditUser` POST email-change branch (Id is a form field). Do NOT use `[EnableRateLimiting]` here — it is the CONTEXT.md D-06/D-07 assumption that needs correcting (see Pitfall 1).

**Example:**
```csharp
// Source: System.Threading.RateLimiting API, first-party .NET runtime library
// (no external package -- already referenced transitively via Microsoft.AspNetCore.RateLimiting,
// already used in Program.cs for the forgot-password/set-password policies).

// Program.cs: register the limiter as a singleton, keyed by target userId, so it is shared
// across requests but NOT tied to the ASP.NET Core rate-limiting middleware pipeline.
builder.Services.AddSingleton(_ =>
    PartitionedRateLimiter.Create<int, string>(userId =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"email-resend:{userId}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            })));

// AdminController.cs: inject PartitionedRateLimiter<int> and check inside the action.
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SendConfirmationEmail(int userId)
{
    using var lease = _emailResendLimiter.AttemptAcquire(userId);
    if (!lease.IsAcquired)
    {
        Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return Content("Too many requests. Please try again later.");
    }

    // ... existing SendConfirmationEmail logic unchanged ...
}
```

**Alternative (attribute-based, approximate):** If the planner prefers to keep the attribute pattern for consistency with `forgot-password`/`set-password`, the practical workaround is to key the attribute-based policy on `httpContext.Connection.RemoteIpAddress` (admin's IP) instead of target userId — this is a *materially different* partition semantic than D-05 specifies (D-05 explicitly wants per-target-user limiting, not per-admin-IP, "to protect any one recipient's inbox... regardless of which admin triggers it"). Because `SendConfirmationEmail`/`EditUser` are both `[Authorize(Policy = "AdminOnly")]`, IP-based partitioning is a weaker but simpler substitute if the planner decides the userId-based precision isn't worth the extra complexity. **This research recommends the programmatic approach (Pattern 3) to honor D-05 exactly, since it is not materially more complex than the attribute approach** — it is a ~10-line change confined to the controller and one singleton registration.

### Anti-Patterns to Avoid
- **Using `[EnableRateLimiting]` with a partition-key factory that reads `httpContext.Request.Form`:** Technically possible (`IFormCollection` synchronous read triggers `ReadFormAsync().GetAwaiter().GetResult()` internally) but blocks a thread and is fragile/undocumented for this use case — no official Microsoft example uses form-body partition keys (confirmed absent from `learn.microsoft.com/aspnet/core/performance/rate-limit`). Prefer the programmatic in-action pattern instead.
- **Adding an EF entity/`DbSet` for `AspNetSessionState`:** The table is managed exclusively by `SqlServerCache`'s internal ADO.NET queries; EF Core has no awareness of it and should not model it. Mixing EF Core migrations (schema) with raw ADO.NET runtime access (data) is fine — that is the intended design — but do not add a `SessionStateEntity` domain model.
- **Registering `AddDistributedSqlServerCache` unconditionally (no Testing guard):** See Pitfall 2 below — this is the single highest-risk pitfall in this phase.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Persisting session data across restarts | Custom SQL Server-backed `ISessionStore` | `Microsoft.Extensions.Caching.SqlServer` + `AddDistributedSqlServerCache` | Handles serialization, sliding/absolute expiration, and cleanup polling correctly; a hand-rolled version would need to reimplement `IDistributedCache`'s full contract including expiration semantics session middleware depends on |
| Rate limiting requests | Custom in-memory counter dictionary | `Microsoft.AspNetCore.RateLimiting` (`PartitionedRateLimiter`, `FixedWindowRateLimiter`) | Already the established pattern in this codebase (Phase 32); handles thread-safety, auto-replenishment, and queueing correctly — a hand-rolled counter risks race conditions under concurrent admin requests |
| Expired cache row cleanup | Hangfire recurring job to `DELETE FROM AspNetSessionState WHERE ExpiresAtTime < GETUTCDATE()` | `SqlServerCacheOptions.ExpiredItemsDeletionInterval` (built-in, default 30 min) | D-04 already correctly rules this out — the package's `SqlServerCache` runs its own polling-based cleanup internally; a duplicate Hangfire job would race with it for no benefit |

**Key insight:** Both halves of this phase are extending frameworks the project already depends on (`Microsoft.Extensions.Caching.*` family, `Microsoft.AspNetCore.RateLimiting`) rather than introducing new abstractions — the temptation to hand-roll should not arise here if the planner follows the patterns above.

## Common Pitfalls

### Pitfall 1: `httpContext.GetRouteValue("userId")` returns null for both target actions [CORRECTS CONTEXT.md D-06/D-07]

**What goes wrong:** CONTEXT.md D-06 states `"the userId action parameter is already in the route/form, accessible from httpContext.GetRouteValue("userId") in the policy factory."` This is only true if `userId` were part of the URL route template (e.g., `/Admin/SendConfirmationEmail/{userId}`) or a query string parameter. In this codebase, both `SendConfirmationEmail` and `EditUser`'s `Id` are submitted as **POST form-body fields** (confirmed by reading `Views/Admin/Users.cshtml` lines 144-145: `<input type="hidden" name="userId" ...>` inside a `<form>`, and `Views/Admin/EditUser.cshtml` line 25: `<input asp-for="Id" type="hidden" />`). The only registered route is the default `{controller=Home}/{action=Index}/{id?}` (`Program.cs:281-283`) — no route template exposes `userId` as a path segment.

**Why it happens:** `GetRouteValue` reads from `HttpContext.Request.RouteValues`, which is populated by the routing middleware from the URL template match (path segments, or `{id?}` defaults) — never from the request body. Rate-limiting policy factories run during `UseRateLimiter()` (positioned after `UseRouting()`, before `UseAuthorization()`), which is **before** MVC's model-binding stage runs (`ActionMethodExecutor`/`ControllerActionInvoker`, which happens later, inside `UseEndpoints`/`MapControllerRoute` dispatch). Form parsing has not occurred yet at the point a rate-limiter policy factory executes.

**How to avoid:** Use the programmatic pattern (Research Pattern 3 above): inject a `PartitionedRateLimiter<int>` singleton and call `AttemptAcquire(userId)` inside the controller action, after `userId`/`model.Id` is already bound by MVC. This is the only reliable way to key the partition on a form-submitted value.

**Warning signs:** If implemented per CONTEXT.md's literal D-06 text, the rate limiter's partition key factory will resolve `userId` to `null` for every request, meaning `partitionKey: httpContext.GetRouteValue("userId")?.ToString() ?? "unknown"` collapses **every admin's every resend, for every different target user, into one shared "unknown" partition** — the 3/hour limit would apply globally across all users being resent emails, not per-target-user as D-05 intends. This is a silent correctness bug that would pass casual testing (rate limiting still "works", just not with the intended granularity) and only surface when two different unconfirmed users both need a resend within the same hour and the second one is unexpectedly blocked.

### Pitfall 2: `AddDistributedSqlServerCache` runs unconditionally in the Testing environment unless explicitly guarded

**What goes wrong:** `WebApplicationFactoryBase.ConfigureWebHost` (this repo, `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`) swaps `QuestBoardContext`'s EF Core provider to `UseInMemoryDatabase`, but does **not** touch `IDistributedCache`, `AddSession`, or any connection-string-based registration outside EF Core. `Program.cs`'s existing environment branches (`if (!builder.Environment.IsEnvironment("Testing"))`) are used for Hangfire and the two email dispatchers — but `AddSession` itself, at `Program.cs:138`, and the rate limiter block at `Program.cs:105`, are **not** currently inside a Testing guard; they run identically in both environments (confirmed: `ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429` integration test relies on the real rate limiter running in Testing). If `AddDistributedSqlServerCache` is added without a Testing guard, it will be wired against `appsettings.json`'s `DefaultConnection` (`Server=localhost;Database=QuestBoard;...`) in every `dotnet test` run, since no `appsettings.Testing.json` exists to override it.

**Why it happens:** `SqlServerCache`'s registration itself does not eagerly connect — the constructor only validates the options object (non-null `ConnectionString`/`SchemaName`/`TableName`), so `dotnet test` would not fail at startup. However, the **first** `Session.Set`/`Session.GetInt32` call in any integration test that exercises group selection (`GroupSessionMiddlewareIntegrationTests`, `GroupPickerControllerIntegrationTests`, etc.) will attempt a real ADO.NET connection to `localhost` SQL Server, outside the InMemory database the rest of the test relies on. On CI or any machine without a local SQL Server reachable at that connection string, this fails; on the developer's own Windows machine (where SQL Server *does* run on `localhost` per CLAUDE.md), it may silently "work" by writing real session rows into the real `QuestBoard` database's `AspNetSessionState` table — cross-contaminating dev data with test session data.

**How to avoid:** Branch the registration exactly like Hangfire (Research Pattern 1): `AddDistributedSqlServerCache` only outside Testing; `AddDistributedMemoryCache()` inside Testing. This requires zero test-project changes since `WebApplicationFactoryBase` already sets `builder.UseEnvironment("Testing")`.

**Warning signs:** Integration tests pass locally (because `localhost` SQL Server is reachable — Windows dev box) but behave inconsistently on a clean CI runner, or the local dev `QuestBoard` database accumulates unexpected `AspNetSessionState` rows after running `dotnet test`.

### Pitfall 3: `SqlServerCacheOptions.SchemaName`/`TableName` are required, not "defaults" — startup throws if omitted

**What goes wrong:** CONTEXT.md D-02 describes `dbo`/`AspNetSessionState` as "the defaults but should be explicit for clarity" — this phrasing implies the package has built-in fallback values. It does not. `SqlServerCache`'s constructor calls `ArgumentThrowHelper.ThrowIfNullOrEmpty(cacheOptions.SchemaName)` and the equivalent for `TableName` — both are validated as required, non-nullable, non-empty strings with **no default value**. Omitting either causes an `ArgumentException` at application startup (when `IDistributedCache` is first resolved / DI container validates on build, depending on hosting model).

**Why it happens:** Unlike some other options classes in the `Microsoft.Extensions.Caching.*` family, `SqlServerCacheOptions` has no implicit "table not specified, assume X" behavior — this is intentional, since the same SQL Server instance/database may host multiple distinct cache tables for different apps or purposes.

**How to avoid:** Always explicitly set both `SchemaName` and `TableName` in the `AddDistributedSqlServerCache` lambda, exactly as CONTEXT.md's D-02 code intends to do — the phrasing is the only inaccuracy, not the actual configuration approach. `[VERIFIED: source.dot.net/Microsoft.Extensions.Caching.SqlServer/SqlServerCache.cs.html]`

**Warning signs:** `ArgumentException: SchemaName cannot be null or empty` (or `TableName`) thrown at app startup if either line is accidentally omitted or set from an unconfigured `IConfiguration` key that returns null.

### Pitfall 4: `ExpiredItemsDeletionInterval` has a 5-minute minimum if explicitly set

**What goes wrong:** If a future maintainer decides to override the 30-minute default (D-04 says not to, but this is worth flagging for the planner's "Claude's Discretion" note), setting `ExpiredItemsDeletionInterval` below 5 minutes throws an `ArgumentOutOfRangeException` at startup — `SqlServerCache`'s constructor validates `ExpiredItemsDeletionInterval >= TimeSpan.FromMinutes(5)` whenever the option is not left at its default.

**How to avoid:** Leave `ExpiredItemsDeletionInterval` unset (framework default of 30 minutes applies) per D-04/Claude's Discretion note in CONTEXT.md. If ever overridden, ensure the value is `>= TimeSpan.FromMinutes(5)`.

### Pitfall 5: Migration deploy-order risk on the Windows host (single-instance app, not blue/green)

**What goes wrong:** Per CLAUDE.md, this app runs directly on a Linux host at `/opt/questboard/` in production (per user's persisted memory) and migrations auto-apply via `context.Database.Migrate()` on startup (per CLAUDE.md's stated dev workflow — same auto-migrate call applies in all non-Testing environments per `Program.cs:290`). Because there is only one running instance (not a rolling/blue-green deploy), the `AspNetSessionState` table migration is low-risk compared to the Phase 27 `AddGroupSchema` migration's documented co-deployment hazard (which involved *removing* rows read by live authorization checks). This migration only *adds* a new table — no existing table's shape or data changes, so there is no requirement to coordinate this migration with any other phase's deployment. However, the **local Windows dev environment** (SQL Server on `localhost`, not Docker per CLAUDE.md) means any manual/incomplete migration application (e.g., a developer partially running `dotnet ef database update` by hand instead of letting `Migrate()` run) could leave the table half-created; the `IF NOT EXISTS` guard in Research Pattern 2 protects against re-running the migration, but does not protect against a manually-created table with a mismatched schema (e.g., wrong collation) pre-existing from an earlier ad hoc test of the `dotnet sql-cache create` CLI tool. Recommend the planner add a note/checkpoint to verify no stray `AspNetSessionState` table already exists in the dev database from manual experimentation before running the new migration.

**Warning signs:** `There is already an object named 'AspNetSessionState' in the database` (if a stray table exists without the `IF NOT EXISTS` guard catching it due to case/schema mismatch), or session writes silently failing due to a pre-existing table with a different collation than `SQL_Latin1_General_CP1_CS_AS`.

## Code Examples

### Full `Program.cs` diff sketch (registration order)

```csharp
// Source: this repo's Program.cs, lines 100-143 (verified in this research session), extended per D-01/D-02/D-04
// and Pitfall 2's Testing-environment guard.

// ... existing PWFLOW-04 rate limiter block (forgot-password, set-password) stays unchanged ...
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("forgot-password", /* unchanged */ httpContext => /* ... */ default!);
    options.AddPolicy("set-password", /* unchanged */ httpContext => /* ... */ default!);

    // NOTE: "admin-email-resend" is NOT added here as an AddPolicy if Pattern 3 (programmatic,
    // in-action PartitionedRateLimiter<int>) is adopted -- see Pitfall 1. If the planner instead
    // chooses the IP-based attribute approximation, add a third options.AddPolicy(...) here
    // partitioned by httpContext.Connection.RemoteIpAddress, matching forgot-password's shape.

    options.OnRejected = async (context, cancellationToken) => { /* unchanged */ };
});

// D-01: AddDistributedSqlServerCache MUST be registered before AddSession.
// Guarded per Pitfall 2 -- mirrors the existing Hangfire branch pattern in this file.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.SchemaName = "dbo";
        options.TableName = "AspNetSessionState";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Pattern 3: singleton PartitionedRateLimiter for admin email resend, keyed by target userId
// (not tied to the ASP.NET Core rate-limiting middleware pipeline -- checked manually in the action).
builder.Services.AddSingleton(_ =>
    PartitionedRateLimiter.Create<int, string>(userId =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"email-resend:{userId}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            })));
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AddSession` with no `IDistributedCache` registered (implicit `AddDistributedMemoryCache` fallback via internal ASP.NET Core defaults) | Explicit `AddDistributedSqlServerCache` before `AddSession` | This phase (Phase 33) | Session data (`ActiveGroupId`) survives app restarts/deploys; previously every deploy forced all logged-in users to re-pick their group |
| Attribute-only `[EnableRateLimiting]` for all rate-limited actions (Phase 32 pattern) | Mixed: attribute-based for IP-partitioned anonymous endpoints (unchanged), programmatic in-action `PartitionedRateLimiter` for form-body-partitioned admin endpoints (this phase) | This phase (Phase 33) | Necessary because Phase 32's endpoints (`ForgotPassword`, `SetPassword`) partition by client IP (always available pre-model-binding), while this phase's endpoints need to partition by a POST-form-body value only available post-model-binding |

**Deprecated/outdated:** None — `Microsoft.Extensions.Caching.SqlServer` and `Microsoft.AspNetCore.RateLimiting` are both current, actively maintained, first-party packages with no announced replacement or deprecation.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Production deployment auto-applies this migration via `context.Database.Migrate()` on the Linux host at `/opt/questboard/` exactly as it does in local dev (per user's persisted memory of deployment model, which differs from the Docker-based description in CLAUDE.md's "Development Commands" section) | Pitfall 5 | If production actually uses a different migration-application step (e.g., manual `dotnet ef database update` in a deploy script), the `IF NOT EXISTS` guard still protects against double-creation, but any deploy-order assumptions in this research would need revisiting — low risk given the guard clause |
| A2 | No `appsettings.Testing.json` exists anywhere in the solution (confirmed via glob search in this session, only `appsettings.json` found under `QuestBoard.Service/`) | Pitfall 2 | If one is added later without the planner also removing the Testing-environment guard, the guard becomes redundant but harmless — no risk of breakage, just an unnecessary branch |

**If this table is empty:** N/A — see entries above. All package/version/schema/API claims were verified this session via WebFetch against `nuget.org`, `source.dot.net`, and `github.com/dotnet/aspnetcore`, and all codebase claims were verified by reading the actual files (`Program.cs`, `AdminController.cs`, `Views/Admin/Users.cshtml`, `Views/Admin/EditUser.cshtml`, `WebApplicationFactoryBase.cs`, migration files, `.csproj` files).

## Open Questions

1. **Should `EditUser`'s rate limit apply to the whole action or only the email-change branch?**
   - What we know: D-07 explicitly leaves this as the planner's discretion. The email-change branch is easily isolated in code (it's an `if (emailChanged && !string.IsNullOrEmpty(model.Email))` block inside the existing action, `AdminController.cs:189-201`).
   - What's unclear: Whether counting non-email-changing `EditUser` saves against the same rate-limit budget would ever practically matter (D-07's own text says "not a meaningful risk even if counted").
   - Recommendation: Since Pattern 3 (programmatic `PartitionedRateLimiter`) is already recommended for `SendConfirmationEmail`, apply the same `AttemptAcquire` call **only inside the `emailChanged` branch** of `EditUser` — this is nearly free (one extra `if` check) once the singleton limiter already exists, and it exactly matches D-07's stated intent (limit only the email-dispatch sub-path). No reason to settle for the coarser whole-action approximation given the marginal extra code is negligible.

2. **Does the production Linux deployment run migrations identically to local dev, or is there a separate deploy script?**
   - What we know: CLAUDE.md documents `docker-compose up` for local dev with SQL Server on the Windows host; the user's persisted memory (`project-deployment.md`) states production runs directly on Linux at `/opt/questboard/`, not Docker.
   - What's unclear: Whether the actual deploy script also calls `context.Database.Migrate()` on startup (same as dev) or has a separate migration step — this research assumes parity based on CLAUDE.md's stated auto-apply behavior, but the production deployment script itself was not read in this session (out of scope for phase research; not a repo file).
   - Recommendation: Low-risk given the migration is purely additive (`CREATE TABLE IF NOT EXISTS`) — no special handling needed regardless of how migrations are triggered in production.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| SQL Server (local, `localhost`) | Session persistence (dev/prod), new migration | Assumed ✓ per CLAUDE.md ("SQL Server runs on the Windows host... use `localhost` in the connection string for local development") | Not directly probed this session (no DB connectivity check tool available in this research context) | If unreachable, `AddDistributedSqlServerCache` registration itself will not fail at startup (lazy connection) — only Session Set/Get calls will throw `SqlException` at runtime |
| `dotnet ef` CLI / EF Core tooling | Creating/removing the new migration | Assumed ✓ — `Microsoft.EntityFrameworkCore.Tools` already referenced in `QuestBoard.Service.csproj` | 10.0.9 (matches other EF packages) `[VERIFIED: QuestBoard.Service.csproj]` | None needed — already a project dependency |

**Missing dependencies with no fallback:** None identified — this phase adds no genuinely new external-service dependency (SQL Server is already a hard dependency of the entire application).

**Missing dependencies with fallback:** SQL Server reachability at migration-apply time — not independently verifiable in this research session; flagged as a standard pre-execution assumption consistent with every other migration in this project's history.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3` 3.2.2), `Microsoft.AspNetCore.Mvc.Testing` 10.0.9, `FluentAssertions` 8.10.0 `[VERIFIED: QuestBoard.IntegrationTests.csproj]` |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Admin\|FullyQualifiedName~Session"` |
| Full suite command | `dotnet test` (runs `QuestBoard.UnitTests` + `QuestBoard.IntegrationTests`) |

### Phase Requirements → Test Map

No REQ-IDs exist yet for this phase (per CONTEXT.md, the planner should add them to REQUIREMENTS.md). Proposed REQ-IDs and their test mapping, for the planner to formalize:

| Proposed REQ-ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SESSION-01 | `ActiveGroupId` survives an app restart (session backed by SQL Server, not wiped from memory) | integration | New test needed — requires stopping/restarting the `WebApplicationFactory` mid-test or verifying `IDistributedCache` is `SqlServerCache`-backed via DI container inspection in non-Testing config | ❌ Wave 0 — no existing test restarts the app; likely needs a narrower unit-style check that `Program.cs` resolves `SqlServerCache` as `IDistributedCache` outside Testing, rather than a true restart simulation |
| SESSION-02 | `AspNetSessionState` table is created by migration with correct schema (columns, PK, index) | integration or manual DB check | `dotnet ef migrations script --project QuestBoard.Repository` + inspect generated SQL, or a lightweight EF Core migration-application test against a real SQL Server (not InMemory) | ❌ Wave 0 — existing integration suite uses `UseInMemoryDatabase`, which does not run raw-SQL migrations at all; this migration's DDL cannot be exercised by the existing test infra without a real SQL Server test target |
| EMAIL-RATE-01 | `SendConfirmationEmail` rejects the 4th request within 1 hour for the same target userId with 429 | integration | New test modeled on `ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429` (`AccountControllerIntegrationTests.cs`), adapted to POST `/Admin/SendConfirmationEmail` 4x with the same `userId` | ❌ Wave 0 — needs new test file/method |
| EMAIL-RATE-02 | `SendConfirmationEmail` rate limit is scoped per-target-userId (two different users' resends don't share a budget) | integration | New test: interleave resends for `userId=A` and `userId=B`, assert both succeed independently up to 3 each | ❌ Wave 0 |
| EMAIL-RATE-03 | `EditUser` POST email-change path is rate-limited the same way | integration | New test modeled on the above, POSTing `EditUser` with a changed email 4x for the same target `Id` | ❌ Wave 0 |
| EMAIL-RATE-04 | `CreateUser` POST (welcome email) is NOT rate-limited (D-08 exemption) | integration | Existing `CreateUser`-related tests should continue passing unmodified; optionally add an explicit test asserting 4 rapid `CreateUser` calls with distinct emails all succeed | Existing tests should already cover create-succeeds; explicit "not rate limited" assertion is new — ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Admin"`
- **Per wave merge:** `dotnet test` (full suite — 191+ existing tests plus new ones from this phase)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] No existing integration test exercises a raw-SQL EF migration against a real SQL Server — the entire integration suite uses EF Core `InMemoryDatabase`, which silently no-ops `migrationBuilder.Sql(...)` calls (InMemory provider does not execute migrations at all, so this specific migration cannot be verified by `dotnet test` in this project's current test architecture; verification will need to be a manual/checkpoint step against the real local SQL Server, or a narrowly-scoped new test project that targets a real SQL Server test database)
- [ ] No existing pattern for testing `IDistributedCache`/session-survives-restart behavior — recommend a lighter-weight test that only asserts DI resolves `SqlServerCache` as `IDistributedCache` in non-Testing config (via a small `WebApplicationFactory` variant that does NOT override the environment to Testing), rather than attempting a literal process-restart test
- [ ] `PartitionedRateLimiter<int>` singleton (Pattern 3) needs a test-visible reset mechanism if tests run in parallel and share static state across test classes — check whether `WebApplicationFactoryBase` creates one instance per test class (it does, per constructor) which should naturally isolate the limiter's in-memory state between test classes

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Not touched by this phase |
| V3 Session Management | Yes | ASVS V3 requires session data to be handled with defined expiration/invalidation — `AddDistributedSqlServerCache`'s `ExpiredItemsDeletionInterval` and the existing `AddSession(IdleTimeout = 24h)` already satisfy this; no change to session security posture, only to storage durability |
| V4 Access Control | Yes (rate limiting endpoints already require `[Authorize(Policy = "AdminOnly")]`) | `AdminController` class-level `[Authorize(Policy = "AdminOnly")]` (confirmed, `AdminController.cs:20`) already gates both target actions — rate limiting here is a defense-in-depth / resource-protection control, not an access-control boundary |
| V5 Input Validation | No new surface | `userId`/`Id` form fields are already validated by existing `userService.GetByIdAsync` null-checks before any email dispatch |
| V6 Cryptography | No | Not touched |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Admin button-mashing exhausting Resend's 100/day quota | Denial of Service (resource exhaustion, self-inflicted rather than external) | Per-target-userId fixed-window rate limit (3/hour), exactly as D-05 specifies |
| Rate-limit partition-key collision (Pitfall 1) causing global rather than per-user limiting | Denial of Service (a form of availability bug — legitimate resends blocked because an unrelated user's resends exhausted a shared "unknown" bucket) | Use the programmatic in-action partitioning (Pattern 3) instead of the broken `GetRouteValue` approach from CONTEXT.md's literal text |
| Stale/cross-contaminated session data in dev DB from unguarded `AddDistributedSqlServerCache` in Testing environment (Pitfall 2) | Tampering (of a sort — test runs writing into the real dev database) / Information Disclosure (test session data persisting where a developer might not expect it) | Testing-environment guard (Pattern 1), falling back to `AddDistributedMemoryCache()` |

## Sources

### Primary (HIGH confidence)
- `https://www.nuget.org/packages/Microsoft.Extensions.Caching.SqlServer/` — confirmed package name, version 10.0.9, .NET 10.0 target `[VERIFIED: nuget.org]`
- `https://source.dot.net/Microsoft.Extensions.Caching.SqlServer/SqlServerCache.cs.html` — confirmed constructor validation of `ConnectionString`/`SchemaName`/`TableName` (required, no defaults) and `ExpiredItemsDeletionInterval` 5-minute minimum `[VERIFIED: source.dot.net, official Microsoft source browser]`
- `https://github.com/dotnet/aspnetcore/blob/main/src/Tools/dotnet-sql-cache/src/SqlQueries.cs` (fetched via raw.githubusercontent.com) — confirmed exact `CREATE TABLE`/`CREATE NONCLUSTERED INDEX` DDL including `Id` column's `COLLATE SQL_Latin1_General_CP1_CS_AS` clause, which CONTEXT.md's D-03 omitted `[VERIFIED: github.com/dotnet/aspnetcore, official source repo]`
- `https://github.com/dotnet/aspnetcore/blob/main/src/Caching/SqlServer/src/SqlServerCachingServicesExtensions.cs` — confirmed `AddDistributedSqlServerCache` extension method signature `[VERIFIED: github.com/dotnet/aspnetcore]`
- `https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0` — confirmed registration example/pattern, current for aspnetcore-10.0 `[CITED: learn.microsoft.com]`
- `https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0` — confirmed no official example partitions by request-body/form data; confirmed `UseRateLimiter` must run after `UseRouting` for attribute-based policies `[CITED: learn.microsoft.com]`
- This repository: `QuestBoard.Service/Program.cs` (read in full), `QuestBoard.Service/Controllers/Admin/AdminController.cs` (read in full), `QuestBoard.Service/Views/Admin/Users.cshtml`, `QuestBoard.Service/Views/Admin/EditUser.cshtml`, `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs`, `QuestBoard.Repository/Migrations/20260626190255_AddReminderLog.cs`, `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs`, `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`, `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`, `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs`, all `.csproj` files — all `[VERIFIED: direct file read, this session]`

### Secondary (MEDIUM confidence)
- None — all findings for this phase were traceable to either an official Microsoft source/repo or a direct read of this project's own files.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — single package, version confirmed directly against nuget.org, no alternatives to weigh
- Architecture: HIGH — registration pattern confirmed against official Microsoft docs and source code; Testing-environment guard pattern directly observed in this project's existing `Program.cs`
- Pitfalls: HIGH — both major pitfalls (route-value partition key; unguarded Testing registration) were discovered by directly reading this project's own view/controller/factory files, not inferred from general knowledge

**Research date:** 2026-07-01
**Valid until:** 2026-07-31 (30 days — stable, first-party framework APIs; no fast-moving dependencies in this phase)
