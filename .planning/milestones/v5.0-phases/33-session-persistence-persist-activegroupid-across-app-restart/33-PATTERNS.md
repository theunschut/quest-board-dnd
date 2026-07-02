# Phase 33: Session Persistence + Admin Email Rate Limiting - Pattern Map

**Mapped:** 2026-07-01
**Files analyzed:** 6 (2 modified, 1 new migration, 3 integration test files/additions)
**Analogs found:** 6 / 6

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/Program.cs` (edit: `AddDistributedSqlServerCache` + Testing guard, before `AddSession`) | config | request-response (DI wiring) | Same file, existing Hangfire Testing-guard block (`Program.cs:179-211`) | exact |
| `QuestBoard.Service/Program.cs` (edit: `PartitionedRateLimiter<int>` singleton for admin email resend) | config | request-response | Same file, existing `AddRateLimiter` block (`Program.cs:105-135`) | role-match (different partitioning mechanism — programmatic, not attribute) |
| `QuestBoard.Repository/Migrations/{timestamp}_AddSessionStateTable.cs` | migration | batch (raw DDL) | `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs` | exact |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `SendConfirmationEmail` (edit: inject limiter, `AttemptAcquire`) | controller | request-response | Same file — no existing rate-limited action to copy from directly; use `AccountController.ForgotPassword` shape (attribute-based) as the closest home-grown 429 precedent, adapted per Research Pattern 3 | role-match (different enforcement layer) |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `EditUser` POST (edit: `AttemptAcquire` inside `emailChanged` branch) | controller | request-response | Same file, `SendConfirmationEmail` (once edited) | exact (same controller, same new pattern) |
| `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` (new tests: EMAIL-RATE-01/02/03/04) | test | request-response | `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` — `ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429` | exact (429 rate-limit test shape) |

## Pattern Assignments

### `QuestBoard.Service/Program.cs` — distributed cache registration (config)

**Analog:** same file, existing Hangfire Testing-environment guard.

**Existing Testing-guard pattern** (`Program.cs:179-211`):
```csharp
if (!builder.Environment.IsEnvironment("Testing"))
{
    // real infra: HangfireQuestEmailDispatcher, AddHangfire(...), AddHangfireServer(...)
}
else
{
    // In the Testing environment Hangfire is skipped, so use a no-op dispatcher.
    builder.Services.AddScoped<IQuestEmailDispatcher, NullQuestEmailDispatcher>();
    builder.Services.AddScoped<IReminderJobDispatcher, NullReminderJobDispatcher>();
}
```

**Apply this exact shape** for the new cache registration, inserted between the existing `AddRateLimiter` block (ends `Program.cs:135`) and the existing `AddSession` block (`Program.cs:138-143`):
```csharp
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

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
```
`AddSession` itself (`Program.cs:138-143`) is UNCHANGED — only its preceding registration changes.

**Connection string pattern to copy** (already used by Hangfire, `Program.cs:190-191`):
```csharp
builder.Configuration.GetConnectionString("DefaultConnection")
```

---

### `QuestBoard.Service/Program.cs` — admin email rate-limit singleton (config)

**Analog:** same file, existing `AddRateLimiter` block (`Program.cs:105-135`) — but do NOT add a third `options.AddPolicy(...)` here (Pitfall 1: `userId`/`Id` are POST form fields, unavailable to policy factories that run pre-model-binding). Instead register a standalone singleton limiter, following the same `RateLimitPartition.GetFixedWindowLimiter` shape already used for `forgot-password`/`set-password`:

**Existing attribute-based policy shape** (`Program.cs:107-116`, for structural reference only — do not attribute-decorate the admin actions):
```csharp
options.AddPolicy("forgot-password", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
```

**New pattern to add** (singleton, placed after the `AddRateLimiter` block, before or after the new cache block — order relative to cache/session does not matter):
```csharp
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
Uses the same `System.Threading.RateLimiting` namespace already imported at `Program.cs:20`. No new `using` needed.

**Existing `OnRejected` response pattern to replicate manually in the controller** (`Program.cs:129-134`):
```csharp
options.OnRejected = async (context, cancellationToken) =>
{
    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    await context.HttpContext.Response.WriteAsync(
        "Too many requests. Please try again later.", cancellationToken);
};
```
This exact status code + message must be replicated inline in `SendConfirmationEmail`/`EditUser` since the manual `AttemptAcquire` path bypasses `AddRateLimiter`'s middleware-level `OnRejected` hook entirely.

---

### `QuestBoard.Repository/Migrations/{timestamp}_AddSessionStateTable.cs` (migration, batch)

**Analog:** `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs` (full file, 26 lines — read in full).

**Full pattern to copy** (raw-SQL-only migration, no `CreateTable` fluent builder — this table is not an EF entity):
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EnableLockoutForExistingUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET LockoutEnabled = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET LockoutEnabled = 0");
        }
    }
}
```

**New migration body** (per RESEARCH.md Pattern 2 — includes the `IF NOT EXISTS` guard and required `COLLATE SQL_Latin1_General_CP1_CS_AS` clause that CONTEXT.md's D-03 omitted):
```csharp
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
```
Naming/registration: use `dotnet ef migrations add AddSessionStateTable --project QuestBoard.Repository` from `QuestBoard.Service/` per CLAUDE.md — do NOT hand-write the `.Designer.cs` companion file; let the tool generate it (it will be near-empty since no `DbSet`/model changes exist).

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `SendConfirmationEmail` and `EditUser` (controller, request-response)

**Analog:** same file/class — apply the new limiter directly; no attribute decoration.

**Current constructor** (`AdminController.cs:21`, to be extended with the new singleton dependency):
```csharp
public class AdminController(IUserService userService, IQuestService questService, IIdentityService identityService, IBackgroundJobClient jobClient, IHttpClientFactory httpClientFactory, IOptions<EmailSettings> emailOptions, IMemoryCache cache, IActiveGroupContext activeGroupContext, ILogger<AdminController> logger) : Controller
```
Add a `PartitionedRateLimiter<int>` parameter (matches the `AddSingleton(_ => PartitionedRateLimiter.Create<int, string>(...))` registration type).

**`SendConfirmationEmail` current shape** (`AdminController.cs:270-289`, `[HttpPost][ValidateAntiForgeryToken]`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SendConfirmationEmail(int userId)
{
    var user = await userService.GetByIdAsync(userId);
    if (user == null)
    {
        return RedirectToAction(nameof(Users));
    }

    if (user.EmailConfirmed)
    {
        TempData["Error"] = $"{user.Name} has already confirmed their account.";
        return RedirectToAction(nameof(Users));
    }

    var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId);
    if (rawToken == null || string.IsNullOrEmpty(user.Email))
    {
        TempData["Error"] = $"Failed to send confirmation email to {user.Name}. Please try again.";
        // ... (unchanged rest of method)
```
Insert the `AttemptAcquire` check as the FIRST statement in the method body (after the `[ValidateAntiForgeryToken]` attribute line, before `userService.GetByIdAsync`), per RESEARCH.md Pattern 3:
```csharp
using var lease = emailResendLimiter.AttemptAcquire(userId);
if (!lease.IsAcquired)
{
    Response.StatusCode = StatusCodes.Status429TooManyRequests;
    return Content("Too many requests. Please try again later.");
}
```

**`EditUser` POST current shape** (`AdminController.cs:166-207`, the email-change branch is lines 189-201):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditUser(EditUserViewModel model)
{
    if (ModelState.IsValid)
    {
        var user = await userService.GetByIdAsync(model.Id);
        if (user == null)
        {
            return RedirectToAction(nameof(Users));
        }

        var emailChanged = !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase);

        user.Name = model.Name;
        user.HasKey = model.HasKey;
        if (!emailChanged)
            user.Email = model.Email;

        await userService.UpdateAsync(user);

        if (emailChanged && !string.IsNullOrEmpty(model.Email))
        {
            var rawToken = await identityService.GenerateChangeEmailTokenAsync(user.Id, model.Email);
            if (rawToken != null)
            {
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                var callbackUrl = Url.Action("ConfirmEmailChange", "Account",
                    new { userId = user.Id, newEmail = model.Email, token = encodedToken }, Request.Scheme);
                jobClient.Enqueue<ChangeEmailConfirmationJob>(j => j.ExecuteAsync(model.Email, user.Name, callbackUrl!, CancellationToken.None));
                TempData["Success"] = $"A confirmation email has been sent to {model.Email} for {user.Name}. The address will update once confirmed.";
                return RedirectToAction(nameof(Users));
            }
        }

        return RedirectToAction(nameof(Users));
    }

    return View(model);
}
```
Per D-07/Research Open Question 1: apply `AttemptAcquire` ONLY inside the `if (emailChanged && !string.IsNullOrEmpty(model.Email))` block (line 189), as the first statement inside it — keyed by `model.Id`, matching the partition key used for `SendConfirmationEmail`'s `userId`:
```csharp
if (emailChanged && !string.IsNullOrEmpty(model.Email))
{
    using var lease = emailResendLimiter.AttemptAcquire(model.Id);
    if (!lease.IsAcquired)
    {
        Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return Content("Too many requests. Please try again later.");
    }

    var rawToken = await identityService.GenerateChangeEmailTokenAsync(user.Id, model.Email);
    // ... unchanged
}
```

**Imports already present that cover this change** (`AdminController.cs:1-16`) — no new `using` needed beyond `System.Threading.RateLimiting` (same namespace as `Program.cs:20`):
```csharp
using Microsoft.AspNetCore.Mvc;
```

---

### `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` — new tests (test, request-response)

**Analog:** `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs`, method `ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429`.

**Pattern to copy** (client setup, anti-forgery token extraction, repeated POST loop, 429 assertion):
```csharp
[Fact]
public async Task ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429()
{
    // Arrange — isolated client for this test.
    await TestDataHelper.ClearDatabaseAsync(factory.Services);
    var client = factory.CreateNonRedirectingClient();

    async Task<HttpResponseMessage> PostForgotPasswordAsync(string email)
    {
        var getResponse = await client.GetAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string> { ["Email"] = email },
            token);

        return await client.PostAsync("/Account/ForgotPassword", formContent, TestContext.Current.CancellationToken);
    }

    // Act — issue 4 rapid requests from the same client (same RemoteIpAddress partition).
    var responses = new List<HttpResponseMessage>();
    for (var i = 1; i <= 4; i++)
    {
        responses.Add(await PostForgotPasswordAsync($"user{i}@example.com"));
    }

    // Assert — at least one of the 4 rapid requests is rejected with 429.
    responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
}
```

**Adapt for EMAIL-RATE-01/02/03/04:**
- Need an authenticated admin client — check `AdminControllerIntegrationTests.cs` (not fully read this session, but same file the new tests should live in) for the existing admin-login helper pattern (likely `AuthenticationHelper.CreateTestUserAsync` + a login call, or a pre-seeded admin fixture) before duplicating; reuse whatever admin-auth helper other tests in that file already use.
- Because this phase's rate limit is partitioned by target `userId`/`Id` (not IP), EMAIL-RATE-02's "two different users don't share a budget" test is a NEW assertion shape not present in the `forgot-password` analog — assert both `userId=A` and `userId=B` sequences of 3 succeed (no 429) independently, then a 4th on either returns 429.
- Because the limiter here is an in-process singleton `PartitionedRateLimiter<int>` (not tied to `UseRateLimiter` middleware), it is NOT skipped in the Testing environment (no Testing-guard around this singleton registration per the Program.cs pattern above) — the test can rely on it firing exactly like `forgot-password` does today in tests.

## Shared Patterns

### Testing-environment guard for new external-dependency registrations
**Source:** `QuestBoard.Service/Program.cs:179-211` (Hangfire branch)
**Apply to:** The new `AddDistributedSqlServerCache` registration — this is THE critical shared pattern for this phase (RESEARCH.md Pitfall 2). Every external-dependency registration added to `Program.cs` in this codebase is guarded by `if (!builder.Environment.IsEnvironment("Testing"))` with a lightweight in-memory/no-op fallback in the `else` branch. Do not deviate.

### Fixed-window rate limiting shape
**Source:** `QuestBoard.Service/Program.cs:105-135` (`forgot-password`/`set-password` policies)
**Apply to:** The new `PartitionedRateLimiter<int, string>` singleton for admin email resend — same `RateLimitPartition.GetFixedWindowLimiter` factory shape, same `FixedWindowRateLimiterOptions` fields (`PermitLimit`, `Window`, `QueueLimit = 0`, `AutoReplenishment = true`), only the partition key type and window duration differ (per-userId int key vs. per-IP string key; 1 hour vs. 15 minutes).

### 429 response body/status
**Source:** `QuestBoard.Service/Program.cs:129-134` (`options.OnRejected`)
**Apply to:** Both `SendConfirmationEmail` and `EditUser`'s manual `AttemptAcquire` rejection branches — must replicate `StatusCodes.Status429TooManyRequests` + the exact string `"Too many requests. Please try again later."` for consistency with the middleware-level policies, even though this path bypasses `OnRejected` entirely.

### Raw-SQL EF migration for non-entity schema objects
**Source:** `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs`
**Apply to:** The new `AddSessionStateTable` migration — `migrationBuilder.Sql(...)` only, no `CreateTable`/fluent builder, no `DbSet`/entity class, matching the project's established convention for schema objects EF Core does not track.

## No Analog Found

None — all files in scope have a clear analog. The only structural novelty is the programmatic (non-attribute) `PartitionedRateLimiter<int>` usage inside a controller action body, which has no prior analog in this codebase (Phase 32's rate limiting is entirely attribute/middleware-based). RESEARCH.md Pattern 3 is the authoritative source for this new shape since no existing file demonstrates it.

## Metadata

**Analog search scope:** `QuestBoard.Service/Program.cs`, `QuestBoard.Service/Controllers/Admin/AdminController.cs`, `QuestBoard.Repository/Migrations/*.cs`, `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs`
**Files scanned:** 6 read directly (Program.cs, AdminController.cs sections, EnableLockoutForExistingUsers.cs, AccountControllerIntegrationTests.cs section), 2 located via Glob (AddReminderLog migration — not read, CreateTable-fluent example noted in RESEARCH.md as the contrasting non-analog)
**Pattern extraction date:** 2026-07-01
