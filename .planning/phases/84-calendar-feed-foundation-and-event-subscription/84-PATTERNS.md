# Phase 84: Calendar Feed Foundation and Event Subscription - Pattern Map

**Mapped:** 2026-09-17
**Files analyzed:** 20
**Analogs found:** 17 / 20

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/CalendarSubscriptionEntity.cs` | model (EF entity) | CRUD | No close analog — see "No Analog Found" | none |
| `QuestBoard.Repository/CalendarSubscriptionRepository.cs` | service (repository) | CRUD | `QuestBoard.Repository/EventSignupRepository.cs` | role-match |
| `QuestBoard.Domain/Interfaces/ICalendarSubscriptionRepository.cs` | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` | exact |
| `QuestBoard.Repository/Migrations/<ts>_AddCalendarSubscriptions.cs` | migration | CRUD | `QuestBoard.Repository/Migrations/20260830094351_AddContactCategories.cs` | exact |
| `QuestBoard.Repository/EventSignupRepository.cs` (new method, D-17 feed query) | service (repository) | CRUD / cross-tenant read | `QuestBoard.Repository/EventRepository.cs` `GetUpcomingAcrossGroupsWithSignupsAsync` (lines 160-190) | role-match (shape must diverge — see below) |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` (mint/rename/revoke + D-17 read + second-layer re-check) | service | CRUD + event-driven read | `QuestBoard.Domain/Services/EventService.cs` `GetCrossBoardAgendaAsync` (lines 82-126) | exact (safety pattern) |
| `QuestBoard.Domain/Models/CalendarFeedOptions.cs` | config | — | `QuestBoard.Domain/Models/AgendaOptions.cs` | exact |
| `QuestBoard.Domain/Models/CalendarSubscription.cs` | model (domain) | CRUD | `QuestBoard.Domain/Models/EventSignup.cs` | role-match |
| `QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs` + implementation (ICS writer) | utility (pure formatting) | transform | No close analog in-repo — nearest sibling is `IMarkdownService`/`MarkdownService`, but only as a "pure text-in/text-out service" shape, not a content match | partial |
| `QuestBoard.Service/Controllers/CalendarFeedController.cs` (anonymous `.ics` GET) | controller | request-response (streaming text response, no view) | `QuestBoard.Service/Controllers/Admin/AccountController.cs` (`ForgotPassword`/`SetPassword` actions — `[AllowAnonymous]`, `[EnableRateLimiting(...)]`) | role-match |
| `QuestBoard.Service/Program.cs` (new rate-limit policy) | config | — | existing `forgot-password`/`set-password` policies (`Program.cs:118-136`) | exact |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` (extended: Add/Rename/Revoke POSTs) | controller | request-response, CRUD | `QuestBoard.Service/Controllers/ContactCategoryManagementController.cs` (Add/Edit/Delete actions) — not read this session but its view pair below is the direct precedent for the POST shapes it must produce | role-match |
| `QuestBoard.Service/ViewModels/AccountViewModels/ProfileViewModel.cs` (extended) | model (ViewModel) | — | itself (existing file, extended) | exact |
| `QuestBoard.Service/Views/Account/Profile.cshtml` (extended, D-15) | component (Razor view) | request-response | itself (existing file) — table/row shape borrowed from `Views/ContactCategoryManagement/Manage.cshtml` | exact (host) / role-match (row-list shape) |
| `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml` (extended, D-15) | component (Razor view) | request-response | itself (existing file) — card-row shape borrowed from `Views/ContactCategoryManagement/Manage.Mobile.cshtml` | exact (host) / role-match (row-list shape) |
| QR rendering helper/service (D-14) | utility | transform | No analog — first server-rendered image/SVG helper in this codebase | none |
| `QuestBoard.UnitTests/CalendarFeedWriterTests.cs` | test | transform (pure-function) | `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` | role-match |
| `QuestBoard.IntegrationTests/CalendarSubscriptionFeedTests.cs` | test | request-response, tenant-isolation | `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs` | exact |
| `QuestBoard.IntegrationTests/ProfileCalendarSubscriptionTests.cs` (mobile markup) | test | request-response, markup | `QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs` | exact |
| Token generation helper (D-06, `GenerateSubscriptionToken`) | utility | transform | `QuestBoard.Service/Controllers/Admin/AccountController.cs` (`WebEncoders.Base64UrlEncode` idiom, lines 48-51) | exact |
| `EmailSettings:AppUrl` read for the feed's absolute URL | config consumer | — | `QuestBoard.Service/Jobs/ForgotPasswordEmailJob.cs` (`IOptions<EmailSettings>` → `emailSettings.AppUrl`, line 19-24) | exact |

## Pattern Assignments

### `QuestBoard.Repository/Entities/CalendarSubscriptionEntity.cs` (model, CRUD)

**No true analog exists** — the codebase has no other per-user, independently-revocable, token-indexed child entity. The closest structural pieces to borrow from separately:

**Tombstone-column idiom** — `QuestBoard.Repository/Entities/EventEntity.cs`:
```csharp
// A cancelled occurrence is a tombstone: its row and its availability answers survive,
// but every read surface that renders events must account for it. Null means the
// occurrence is live; un-cancelling is a single write of null with no data loss.
public DateTime? CancelledAt { get; set; }
```
Reuse this exact shape for `RevokedAt` (D-08): `public DateTime? RevokedAt { get; set; }`.

**Unique-indexed column idiom** — `QuestBoard.Repository/Entities/QuestBoardContext.cs:230-232`:
```csharp
// Groups.Name must be unique across the tenant
modelBuilder.Entity<GroupEntity>()
    .HasIndex(g => g.Name)
    .IsUnique();
```
Apply the same `HasIndex(...).IsUnique()` call to the `Token` column so `GetByTokenAsync` is a direct indexed lookup (D-06).

**CRITICAL — this table must NOT carry a `GroupId` and must NOT be registered with a `HasQueryFilter` call.** `QuestBoard.Repository/Entities/QuestBoardContext.cs:392-533` shows every group-scoped entity's filter fails closed (`activeGroupContext.ActiveGroupId != null && e.GroupId == ...`) — the feed endpoint has no active group, so any filter on this table would make every lookup return nothing, indistinguishable from "token not found." This entity is scoped by `UserId`, not `GroupId`.

### `QuestBoard.Domain/Interfaces/ICalendarSubscriptionRepository.cs` + `CalendarSubscriptionRepository.cs` (service, CRUD)

**Analog:** `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` + `QuestBoard.Repository/EventSignupRepository.cs` — a small, per-user, per-row CRUD repository with a create-or-update write and a hard-delete write, same shape this table needs (mint = create, rename = update, revoke = update-not-delete).

**Interface doc-comment pattern** (`IEventSignupRepository.cs:6-23`):
```csharp
public interface IEventSignupRepository : IBaseRepository<EventSignup>
{
    /// <summary>
    /// Creates the caller's signup row for the event when none exists yet, or updates it when
    /// one does, stamping the answered timestamp in both cases. The caller must supply
    /// <paramref name="userId"/> from the authenticated principal and never from request input,
    /// ...
    /// </summary>
    Task SetAvailabilityAsync(int eventId, int userId, VoteType availability, CancellationToken token = default);
    ...
}
```
Mirror this for `MintAsync(int userId, CancellationToken)`, `RenameAsync(int id, int userId, string name, CancellationToken)`, `RevokeAsync(int id, int userId, CancellationToken)`, `GetByTokenAsync(string token, CancellationToken)`, `GetForUserAsync(int userId, CancellationToken)`, and a throttled `TouchLastFetchedAsync(int id, DateTime now, CancellationToken)`.

**Create-or-update write pattern** (`EventSignupRepository.cs:12-42`):
```csharp
var entity = await DbSet.FirstOrDefaultAsync(es => es.EventId == eventId && es.UserId == userId, token);
if (entity != null)
{
    entity.Availability = (int)availability;
    entity.UpdatedAt = DateTime.UtcNow;
}
else
{
    await DbContext.EventSignups.AddAsync(new EventSignupEntity { ... }, token);
}
await DbContext.SaveChangesAsync(token);
```

### D-17 feed query — new method on `IEventSignupRepository`/`EventSignupRepository`

**Analog for the SAFETY SHAPE only (not the query root):** `QuestBoard.Repository/EventRepository.cs:160-190`, `GetUpcomingAcrossGroupsWithSignupsAsync`:
```csharp
// QuestBoard.Repository/EventRepository.cs:160-190
public async Task<IList<EventWithSignups>> GetUpcomingAcrossGroupsWithSignupsAsync(
    IReadOnlyCollection<int> memberGroupIds, DateOnly today, int take, CancellationToken token = default)
{
    // Scope is re-imposed immediately by memberGroupIds, supplied by the caller from a
    // fresh membership read taken this same request -- this bypass is therefore strictly
    // narrower than the ambient filter for any single board, never broader.
    var entities = await DbContext.Events
        .IgnoreQueryFilters()
        .Where(e => memberGroupIds.Contains(e.GroupId) && e.Date >= today && e.CancelledAt == null)
        .OrderBy(e => e.Date).ThenBy(e => e.StartTime).ThenBy(e => e.Id)
        .Take(take)
        .Include(e => e.Signups).ThenInclude(s => s.User)
        .AsNoTracking()
        .ToListAsync(token);
    ...
}
```
**Do not widen this method.** Per RESEARCH.md's Pitfall 5, D-17 requires a **new** method rooted at `DbContext.EventSignups`, e.g.:
```csharp
var entities = await DbContext.EventSignups
    .IgnoreQueryFilters()
    .Where(es => es.UserId == userId
        && memberGroupIds.Contains(es.Event.GroupId)
        && es.Event.CancelledAt == null
        && es.Event.Date >= windowStart && es.Event.Date <= windowEnd)
    .Include(es => es.Event)
    .AsNoTracking()
    .ToListAsync(token);
```
No `.Take(...)` (D-13 is a date window, not a row count) and no `.Include(Signups).ThenInclude(User)` roster join (D-10/D-11 need no roster).

### `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` — feed read + second-layer re-check

**Analog:** `QuestBoard.Domain/Services/EventService.cs:82-126`, `GetCrossBoardAgendaAsync`:
```csharp
var fetched = await repository.GetUpcomingAcrossGroupsWithSignupsAsync(memberGroupIds, today, take + 1, token);

var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList();

if (checkedRows.Count != fetched.Count)
{
    logger.LogError(
        "Cross-board agenda dropped {DroppedCount} of {FetchedCount} row(s) falling outside the caller's board set. " +
        "The query is built from the same set, so this indicates a lost or mistranslated board predicate.",
        fetched.Count - checkedRows.Count,
        fetched.Count);
}
```
Reuse verbatim for the feed read: fetch via the new `EventSignups`-rooted method, re-check every row's `Event.GroupId` against a freshly-read `memberGroupIds` set, `LogError` on any mismatch — this is mandatory per CONTEXT.md's own framing ("a feed is read by a machine, so a leak has no reader to notice it"). No `Take`/`hasMore` windowing is needed here (D-13 has no row cap), so the post-recheck logic is simpler than the agenda's.

**CRITICAL anti-pattern (RESEARCH.md, Anti-Patterns section):** never resolve `IActiveGroupContext` anywhere in this read path — it is `null` on every request to this endpoint and every entity filtered through it returns **zero rows**, silently, not an error. Membership must come from `IGroupService.GetGroupsForUserAsync(subscription.UserId)`, not from active-group state.

### `QuestBoard.Domain/Models/CalendarFeedOptions.cs` (config)

**Analog:** `QuestBoard.Domain/Models/AgendaOptions.cs` (full file, cited per CONTEXT.md D-13: "configurable in the manner of `AgendaOptions`"):
```csharp
namespace QuestBoard.Domain.Models;

// Code defaults, overridable through configuration, so no deployment environment file has
// to change for the feature to work.
public class AgendaOptions
{
    public const string SectionName = "Agenda";
    public int DefaultTake { get; set; } = 5;
    public int MaxTake { get; set; } = 50;
    public int PageIncrement { get; set; } = 5;
    public bool IsValid() => DefaultTake >= 1 && MaxTake >= 1 && PageIncrement >= 1 && DefaultTake <= MaxTake;
}
```
**Registration** — `QuestBoard.Domain/Extensions/ServiceExtensions.cs:31-32`:
```csharp
services.AddOptions<AgendaOptions>()
    .BindConfiguration(AgendaOptions.SectionName);
```
Mirror with `CalendarFeedOptions { SectionName = "CalendarFeed"; MonthsBack = 3; MonthsAhead = 12; IsValid() }` and the matching `AddOptions<CalendarFeedOptions>().BindConfiguration(...)` call.

### ICS writer (`ICalendarFeedWriter` + implementation) — pure formatting, no analog content-wise

**No structural analog exists for RFC 5545 generation.** The nearest sibling by *shape* (pure text-in/text-out service, no I/O, no DB) is `IMarkdownService`/`MarkdownService`, useful only for the "how this codebase organizes a pure formatting service + its unit test" convention, not for any ICS-specific content:
- Interface lives in `QuestBoard.Domain/Interfaces/`, implementation in `QuestBoard.Domain/Services/`, registered alongside other domain services in `ServiceExtensions.cs`.
- Its unit test (`QuestBoard.UnitTests/Services/MarkdownServiceTests.cs`) asserts exact output via string/regex assertions with no mocking — same shape the planner should use for `CalendarFeedWriterTests.cs` (assert exact byte/line output for folding, CRLF, escaping — a golden-file/exact-string style test, not a snapshot framework this codebase doesn't have).

Write the ICS body per RESEARCH.md's `## Code Examples` section directly (already line-cited RFC 5545 folding/CRLF/escaping rules and the exact all-day vs. timed VEVENT shapes) — there is no existing code to copy the wire format from.

### `QuestBoard.Service/Controllers/CalendarFeedController.cs` (anonymous, token-authenticated, `text/calendar`)

**Analog:** `QuestBoard.Service/Controllers/Admin/AccountController.cs`, `ForgotPassword`/`SetPassword` actions (lines 26-115):
```csharp
[HttpGet]
[AllowAnonymous]
public IActionResult AccessDenied()
{
    return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
[EnableRateLimiting("forgot-password")]
public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
{
    ...
}
```
For the feed GET: `[AllowAnonymous]`, `[EnableRateLimiting("calendar-feed")]` (new policy), no `[ValidateAntiForgeryToken]` (no form, GET-only, token-in-URL is the auth). Return `410 Gone` / `404 Not Found` / `200` with `Content-Type: text/calendar; charset=utf-8` directly — this codebase's controllers already return typed status results (`NotFound()`, etc.) elsewhere; use `StatusCode(410)` for the tombstone case since MVC has no built-in `Gone()` helper.

**Route placement (Pattern 3 in RESEARCH.md):** put this on its own new controller, NOT under `/Account` — deliberately excluded from `GroupSessionMiddleware.ExemptPathPrefixes` (`QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:59-84`) unless the plan makes an explicit, recorded decision to add it. Anonymous requests already pass the middleware untouched (`GroupSessionMiddleware.cs:99-105`, `if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }` — this check runs first, before `ExemptPathPrefixes`).

### Rate-limit policy (Program.cs)

**Analog:** `QuestBoard.Service/Program.cs:118-141`:
```csharp
builder.Services.AddRateLimiter(options =>
{
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
    ...
});
```
RESEARCH.md's Security Domain section recommends partitioning by the token itself rather than IP for this endpoint (ties the budget to the specific subscription, not a shared NAT/proxy IP pool) — same `AddPolicy`/`GetFixedWindowLimiter` shape, different `partitionKey` expression (`context.Request.RouteValues["token"]?.ToString() ?? "unknown"`).

### Token generation (D-06)

**Analog:** `QuestBoard.Service/Controllers/Admin/AccountController.cs:48-51` (existing password-reset token idiom):
```csharp
var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId.Value);
var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
```
Per RESEARCH.md's "Don't Hand-Roll" table, use the BCL crypto primitive directly rather than ASP.NET Identity's token provider (this is not an identity-flow token):
```csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

var bytes = RandomNumberGenerator.GetBytes(32); // 256 bits
var token = WebEncoders.Base64UrlEncode(bytes); // URL-safe, no padding
```

### `EmailSettings:AppUrl` for the feed's absolute base URL

**Analog:** `QuestBoard.Service/Jobs/ForgotPasswordEmailJob.cs:19-24`:
```csharp
var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;
...
{ nameof(ForgotPassword.AppUrl), emailSettings.AppUrl }
```
Inject `IOptions<EmailSettings>` wherever the Profile page or controller builds the `https://.../calendar/{token}.ics` and `webcal://.../calendar/{token}.ics` links — do not derive the host/scheme from the current `HttpRequest` (Phase 78's forwarded-header fix is unshipped; `Program.cs:103` currently forwards only `XForwardedFor`).

### Profile views (D-15) — row-list shape

**Host analog:** `QuestBoard.Service/Views/Account/Profile.cshtml` / `.Mobile.cshtml` (both read in full above) — extend the existing single `.modern-card`/`.account-card-mobile` with a new sub-section below the existing fields and above the final `<hr>`+button row, per the UI-SPEC's card-not-page framing.

**Row-list shape analog:** `QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml` (desktop table) and `Manage.Mobile.cshtml` (`.category-mgmt-row` cards) — both read in full above. Concretely reusable:
- Desktop table structure: `<table class="table table-striped table-hover align-middle">` with an Actions column right-aligned, Rename as `btn-warning btn-sm` and Delete as `btn-danger btn-sm` inside a `<form method="post">` with `@Html.AntiForgeryToken()`.
- Mobile card structure: `.category-mgmt-row` div with a flex header row (name + meta), then a `d-flex gap-2 mt-2` action row with `flex-grow-1` buttons — the same 44×44px icon-button floor from `contacts.mobile.css:194-197` applies to the Copy/Show-QR icon buttons per the UI-SPEC's Spacing Scale exception.
- Native `confirm()` delete pattern (`Manage.cshtml:105-116` and `Manage.Mobile.cshtml:77-88`, identical script in both layouts):
```javascript
document.querySelectorAll('.category-delete-form').forEach(function (form) {
    form.addEventListener('submit', function (e) {
        var name = form.dataset.categoryName;
        ...
        if (!confirm(message)) { e.preventDefault(); }
    });
});
```
Reuse this exact structure for the Delete confirm string mandated by UI-SPEC's Copywriting Contract (`confirm('Delete "{name}"? Any device using this address will silently stop receiving updates...')`).
- Empty-state block (`Manage.cshtml:84-91`):
```html
<div class="text-center py-5">
    <i class="fas fa-tags fa-3x text-muted mb-3"></i>
    <h4>No Categories Yet</h4>
    <p class="text-muted">Add your first category above to start grouping your contacts.</p>
</div>
```

**Rename-modal idiom (`show.bs.modal` + `event.relatedTarget`)** — `QuestBoard.Service/Views/Shop/Index.cshtml:452-465` (the only place this session read the idiom directly; UI-SPEC also cites `ShopManagement/Index.cshtml` and `_CharacterSelectModal.cshtml` as siblings using the same convention):
```javascript
document.getElementById('itemDetailsModal').addEventListener('show.bs.modal', function (event) {
    const button = event.relatedTarget;
    if (!button) return;
    const itemUrl = button.getAttribute('data-item-url');
    ...
});
```
For the Rename modal, the triggering button's `data-*` attributes should carry the subscription id and current name so the single reused modal instance can be pre-filled per the UI-SPEC's E6 "zero-one-many" resolution — no per-row modal markup.

### `AccountController.cs` — Add/Rename/Revoke POST actions

**Analog:** `AccountController.Profile()` GET (lines 171-186) for the membership/role read shape, and `ContactCategoryManagementController`'s Add/Edit/Delete action shape (not read this session; infer from `Manage.cshtml`'s `asp-action="Add"/"MoveUp"/"MoveDown"/"Delete"` forms — each posts to a same-named controller action, `[ValidateAntiForgeryToken]`, redirects back to the management view on completion). Apply the same shape: `AddCalendarSubscription`, `RenameCalendarSubscription`, `RevokeCalendarSubscription`, each `[HttpPost][ValidateAntiForgeryToken]`, resolving the authenticated user via `userService.GetUserAsync(User)` (as `Profile()` already does), never trusting a client-supplied user id — mirroring `IEventSignupRepository`'s doc-comment discipline ("caller must supply `userId` from the authenticated principal and never from request input").

## Shared Patterns

### Tenant-safety: pinned predicate + second-layer re-check
**Source:** `QuestBoard.Domain/Services/EventService.cs:82-126` (`GetCrossBoardAgendaAsync`) and `QuestBoard.Repository/EventRepository.cs:160-190` (`GetUpcomingAcrossGroupsWithSignupsAsync`)
**Apply to:** `CalendarSubscriptionService`'s feed-read method and the new `EventSignups`-rooted repository method — this is the third instance of this pattern in the codebase (fresh membership read → `IgnoreQueryFilters()` + pinned `memberGroupIds.Contains(...)` predicate → in-memory re-check → `LogError` on any mismatch).

### Fail-closed query filters — never resolve `IActiveGroupContext` in this feed's path
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:392-533` — every group-scoped `HasQueryFilter` call is `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`.
**Apply to:** every new repository/service method touching `Events`/`EventSignups` on this endpoint's path — they must all use `IgnoreQueryFilters()` + explicit `memberGroupIds` re-pinning, never the ambient filter, and must never resolve `IActiveGroupContext` (it is null here).

### Anonymous route placement relative to `GroupSessionMiddleware`
**Source:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:99-105`, `:59-84`
**Apply to:** `CalendarFeedController` — anonymous requests pass through untouched regardless of `ExemptPathPrefixes`; the only open decision is whether to add the new controller's prefix to that list for the authenticated-browser case (RESEARCH.md recommends not adding it by default).

### Rate limiting on an anonymous endpoint
**Source:** `QuestBoard.Service/Program.cs:118-141` (`forgot-password`/`set-password` policies)
**Apply to:** the new `.ics` GET action via a new named policy, `[EnableRateLimiting("calendar-feed")]`, partitioned by token rather than IP per RESEARCH.md's Security Domain guidance.

### Options-class + registration for a configurable window/page-size
**Source:** `QuestBoard.Domain/Models/AgendaOptions.cs` + `QuestBoard.Domain/Extensions/ServiceExtensions.cs:31-32`
**Apply to:** `CalendarFeedOptions` (D-13's rolling window bounds).

### Token idiom (`WebEncoders.Base64UrlEncode`)
**Source:** `QuestBoard.Service/Controllers/Admin/AccountController.cs:48-51`
**Apply to:** `GenerateSubscriptionToken()`, substituting `RandomNumberGenerator.GetBytes(32)` for the password-reset raw token per RESEARCH.md's "Don't Hand-Roll" guidance (this is not an Identity-flow token, so skip `identityService.GeneratePasswordResetTokenForUserAsync`).

### `EmailSettings:AppUrl` for absolute links
**Source:** `QuestBoard.Service/Jobs/ForgotPasswordEmailJob.cs:19-24`, `QuestBoard.Domain/Models/EmailSettings.cs`
**Apply to:** every place the feed's `https://` and `webcal://` URLs are built (Profile view/controller and any place the address is echoed back).

### Two-group tenant-isolation integration test shape
**Source:** `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs` (full seeding helper set: `SeedBoardAsync`, `SeedEventAsync`, `SeedMembershipAsync`, `SeedMemberAsync`, `SeedSignupAsync`, `RemoveAllMembershipsAsync`, `LeaveBoardAsync`)
**Apply to:** `CalendarSubscriptionFeedTests.cs` — reuse the same seeding helpers verbatim (they are private to that test class today; either duplicate them or, if the planner prefers, extract to a shared test helper — flag this as a planner decision, not pre-decided here). Note the class's own caveat: the shared harness uses EF Core InMemory, so relational-only translation risks (empty-collection `Contains`, filter-bypass-through-navigation) are NOT covered by this test shape alone.

### Mobile markup test shape (real User-Agent, not devtools emulation)
**Source:** `QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs` — `MobileUserAgent` constant + `GetMobileAsync` helper attaching the header via `request.Headers.TryAddWithoutValidation("User-Agent", ...)`.
**Apply to:** `ProfileCalendarSubscriptionTests.cs` for asserting the D-15 section renders correctly under `Profile.Mobile.cshtml` — copy the `MobileUserAgent` constant and `GetMobileAsync` helper.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `CalendarSubscriptionEntity.cs` | model | CRUD | No existing entity in this codebase is a per-user, `GroupId`-free, token-indexed, tombstoned child table. Compose from three separate idioms (tombstone column from `EventEntity.CancelledAt`, unique index from `GroupEntity.Name`, per-user FK from `EventSignupEntity.UserId`) rather than one analog. |
| `ICalendarFeedWriter` / ICS writer implementation | utility | transform | No RFC 5545 or any other calendar/date-serialization writer exists in this codebase. Build directly from RESEARCH.md's `## Code Examples` section (already line-cited RFC text and exact VEVENT output for both branches). `IMarkdownService` is a shape-only sibling (pure formatting service + exact-string unit test), not a content analog. |
| QR rendering helper (QRCoder `SvgQRCode`) | utility | transform | First server-rendered image/SVG generation in this codebase — no analog. Follow RESEARCH.md's package guidance directly (`QRCoder` 1.8.0, `SvgQRCode` renderer, registered/consumed from `QuestBoard.Service` since it is a presentation package). |

## Metadata

**Analog search scope:** `QuestBoard.Repository/` (entities, repositories, `QuestBoardContext.cs`, migrations), `QuestBoard.Domain/` (services, interfaces, models, `ServiceExtensions.cs`), `QuestBoard.Service/` (controllers, `Program.cs`, middleware, Views/Account, Views/ContactCategoryManagement, Views/Shop), `QuestBoard.UnitTests/Services/`, `QuestBoard.IntegrationTests/Tests/`
**Files scanned:** ~30 (read fully or in targeted ranges this session)
**Pattern extraction date:** 2026-09-17
