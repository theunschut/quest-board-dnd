# Phase 78: Link Preview Foundation and Quest Cards - Pattern Map

**Mapped:** 2026-08-26
**Files analyzed:** 14
**Analogs found:** 12 / 14

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (modify) | model/config | CRUD | itself (existing DbSets + query filters, lines 8-51, 326-386) | exact (self-modify) |
| `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (modify) | config | request-response | itself, `AddRepositoryServices` (lines 11-35) | exact (self-modify) |
| `QuestBoard.Repository/Migrations/<ts>_AddDataProtectionKeys.cs` (new) | migration | batch | `20260706193921_AddContactsFeature.cs` | exact |
| `QuestBoard.Domain/Interfaces/ILinkSigningService.cs` (new) | service (interface) | transform | `QuestBoard.Domain/Interfaces/IMarkdownService.cs` | role-match |
| `QuestBoard.Domain/Services/LinkSigningService.cs` (new) | service | transform | `QuestBoard.Domain/Services/MarkdownService.cs` | role-match |
| `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` (modify — widen) | interface | request-response | itself | exact (self-modify) |
| `QuestBoard.Service/Services/ActiveGroupContextService.cs` (modify — implement new member) | service | request-response | itself (`SetGroupId` already at lines 31-35) | exact (self-modify, trivial) |
| `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` (modify — implement new member) | test double | request-response | itself | exact (self-modify, trivial) |
| `QuestBoard.Service/Controllers/LinkPreview/QuestPreviewController.cs` (new) | controller | request-response | `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (`Details` GET, lines 306-335+) | role-match, anonymous-allow is net-new |
| `QuestBoard.Service/Program.cs` (modify — ForwardedHeadersOptions) | config | request-response | itself, lines 97-111 | exact (self-modify) |
| `QuestBoard.Service/Views/LinkPreview/QuestCard.cshtml` (new) | view | request-response | none found — no existing view sets `Layout = null` (see below) | no analog |
| `QuestBoard.Service/Views/Shared/_ShareLinkButton.cshtml` (new) | component (partial) | event-driven (client JS) | `QuestBoard.Service/Views/Shared/_Toasts.cshtml` | role-match (structurally), explicitly NOT its mechanism (see Shared Patterns) |
| `Views/Quest/Details.cshtml` + `Details.Mobile.cshtml` (modify — host the partial) | view | request-response | themselves (existing summary/header card sections) | exact (self-modify) |
| `wwwroot/images/link-preview-card.png` (new) | static asset | file-I/O | `wwwroot/images/Blanks/Poster1.png` / `Ruined Posters/*` (source art only, not a code analog) | asset, not code |

## Pattern Assignments

### `QuestBoard.Repository/Entities/QuestBoardContext.cs` (model/config)

**Analog:** the file itself — existing DbSet declarations (lines 13-51) and the fail-closed query-filter block (lines 326-386).

**DbSet declaration pattern** (lines 13-51):
```csharp
public DbSet<EventEntity> Events { get; set; }
```
Add: `public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;` (per research, `DataProtectionKey` requires the `= default!` initializer shape shown in Pattern 1 of RESEARCH.md — EF's own type from the `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` package).

**Critical: do NOT add a query filter for `DataProtectionKeys`.** Every other DbSet added recently (`EventEntity`, `EventSeriesEntity`, `ContactEntity`) gets a `HasQueryFilter` entry in the same style as lines 335-386 keyed off `activeGroupContext.ActiveGroupId`. `DataProtectionKey` is global infrastructure with no `GroupId` column and must be excluded from this pattern entirely — mirror how `UserEntity` is deliberately excluded (comment near the Identity DbSets), not how `QuestEntity`/`EventEntity` are filtered.

**Constructor pattern already in place** (lines 8-11) — no change needed, `IActiveGroupContext` is already injected:
```csharp
public class QuestBoardContext(
    DbContextOptions<QuestBoardContext> options,
    IActiveGroupContext activeGroupContext)
    : IdentityDbContext<UserEntity, IdentityRole<int>, int>(options)
```
Add `, IDataProtectionKeyContext` to the base-type list.

---

### `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (config)

**Analog:** the file itself, `AddRepositoryServices` (lines 11-35).

**Registration pattern** (lines 15-16, immediately after `AddDbContext`):
```csharp
services.AddDbContext<QuestBoardContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

services.AddDataProtection().PersistKeysToDbContext<QuestBoardContext>();
```
Follow the existing `services.AddScoped<IXRepository, XRepository>();` one-line-per-service style (lines 18-29) if `LinkSigningService` is registered here instead of in `AddDomainServices` — but per Domain-layer placement (`QuestBoard.Domain/Services/`), register it in `QuestBoard.Domain/Extensions/ServiceExtensions.cs` `AddDomainServices` instead (see next section), matching how `IMarkdownService`/`MarkdownService` and every other Domain service is registered there, not in Repository.

---

### `QuestBoard.Repository/Migrations/<ts>_AddDataProtectionKeys.cs` (migration)

**Analog:** `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs` — named directly in CONTEXT.md as the multi-table precedent. Generate via `dotnet ef migrations add AddDataProtectionKeys --project ../QuestBoard.Repository` from `QuestBoard.Service/`, per `CLAUDE.md`. Do not hand-write the migration — let EF Core generate the `Xml`/`FriendlyName`/`Id` columns for `DataProtectionKeys` from the `IDataProtectionKeyContext` model contribution.

---

### `QuestBoard.Domain/Interfaces/ILinkSigningService.cs` + `QuestBoard.Domain/Services/LinkSigningService.cs` (service)

**Analog:** `QuestBoard.Domain/Interfaces/IMarkdownService.cs` + `QuestBoard.Domain/Services/MarkdownService.cs`.

**Imports pattern** (`MarkdownService.cs` lines 1-8):
```csharp
using AngleSharp.Dom;
using Ganss.Xss;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using QuestBoard.Domain.Interfaces;
using System.Text.RegularExpressions;

namespace QuestBoard.Domain.Services;
```
`LinkSigningService.cs` follows the same shape:
```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using QuestBoard.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace QuestBoard.Domain.Services;
```

**Interface doc-comment style** (`IMarkdownService.cs` lines 10-27) — every method carries a `<summary>` describing null/empty/malformed input handling explicitly, not just the happy path. Mirror this for `Protect`/`TryUnprotect` — document the tamper/malformed-input contract (returns `false`, never throws) the same way `ExtractPlainText` documents its empty-input contract.

**Core transform pattern** — see RESEARCH.md § Code Examples "Signing and verifying the token" (lines 430-471) for the concrete `Protect`/`TryUnprotect` body using `IDataProtectionProvider.CreateProtector(...)`, `WebEncoders.Base64UrlEncode/Decode`, and a `catch (CryptographicException) { return false; }` / `catch (FormatException) { return false; }` reject-don't-degrade error pattern (LINKPREV-05).

**Registration** — add to `QuestBoard.Domain/Extensions/ServiceExtensions.cs`, `AddDomainServices` (lines 15-25), following the existing one-line-per-service style:
```csharp
services.AddScoped<IUserService, UserService>();
services.AddScoped<IEmailService, EmailService>();
```
Add: `services.AddScoped<ILinkSigningService, LinkSigningService>();`

---

### `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` (widen)

**Current file (full contents, 11 lines):**
```csharp
namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Provides the active group ID for the current request or execution context.
/// Null means "see all records".
/// </summary>
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }
}
```
Add the member exactly as RESEARCH.md Pattern 2 specifies:
```csharp
    // Sets a scoped/singleton-local override for the current execution context.
    // Never persists to Session — see 78-CONTEXT.md D-12 (Session write == privilege escalation).
    void SetGroupId(int? groupId);
```

**Both implementors must be updated in the same change:**
1. `QuestBoard.Service/Services/ActiveGroupContextService.cs` — `SetGroupId` already exists at lines 31-35 verbatim; only the interface declaration needs to change to make it satisfy `IActiveGroupContext` explicitly (already a public method with matching signature, so this is a no-op body change — just confirm the `: IActiveGroupContext` declaration at line 11 now compiles against the widened interface).
2. `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — currently has no `SetGroupId` at all (only the settable `ActiveGroupId` property at line 14). Add:
```csharp
public void SetGroupId(int? groupId) => ActiveGroupId = groupId;
```

**Landmine — do not copy this anti-pattern:** `QuestBoard.Service/Jobs/HangfireJobHelper.cs` lines 26-30 resolves the **concrete type** `ActiveGroupContextService` from DI to call `SetGroupId`, not the interface:
```csharp
var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
groupContext.SetGroupId(groupId);
```
This works today only because Hangfire jobs never run inside the test `WebApplicationFactoryBase` DI graph. The new preview controller MUST resolve `IActiveGroupContext` (constructor-injected) and call `.SetGroupId(...)` on that, never the concrete type — see RESEARCH.md Pattern 2 for why the test harness silently diverges.

---

### `QuestBoard.Service/Controllers/LinkPreview/QuestPreviewController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `Details` GET (lines 306-335+) for the NotFound-on-null shape, plus `HangfireJobHelper.cs` for the "set group context before any repository call" sequencing (but resolve via `IActiveGroupContext`, not the concrete type — see landmine above).

**Imports pattern** (`QuestController.cs` lines 1-10):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.ViewModels.CalendarViewModels;
using QuestBoard.Service.ViewModels.QuestViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard;
```

**Anonymous-allow pattern — no existing analog in this codebase.** Every controller found (`QuestController`, `GroupPickerController`, `MarkdownController`, `Admin/AccountController`) uses only `[Authorize]` / `[Authorize(Policy = "...")]`; a `grep` for `AllowAnonymous` across `QuestBoard.Service/Controllers` returns zero matches. `QuestController` itself has **no class-level `[Authorize]`** (each action is individually attributed, e.g. line 27, 67, 83), which is the shape to reuse — omit any class-level `[Authorize]` on `QuestPreviewController` entirely (do not add `[AllowAnonymous]` redundantly on top of an absent class attribute; only add it if a global authorization filter is later discovered to apply by default — verify during planning whether `Program.cs` registers a fallback policy).

**Core request-response pattern** — see RESEARCH.md § Code Examples Pattern 2 (lines 342-360) for the full `TryUnprotect` → `SetGroupId` → repository read → `NotFound()`-on-null shape; reuse `QuestController.Details`'s null-check idiom verbatim:
```csharp
// QuestController.cs:309-314
var quest = await questService.GetQuestWithDetailsAsync(id, token);

if (quest == null)
{
    return NotFound();
}
```

---

### `QuestBoard.Service/Program.cs` (config, modify)

**Analog:** the file itself, lines 97-111.

**Current block:**
```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

    var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in knownProxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
            options.KnownProxies.Add(ip);
    }
});
```
Change only the `ForwardedHeaders` line per D-05 / RESEARCH.md lines 473-490:
```csharp
options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
    | ForwardedHeaders.XForwardedProto
    | ForwardedHeaders.XForwardedHost;
```
The `KnownProxies` loop and its surrounding comment are unchanged — do not duplicate this block elsewhere.

---

### `QuestBoard.Service/Views/LinkPreview/QuestCard.cshtml` (view, new — no analog)

**No existing view in this project sets `Layout = null;`.** A scan of `Views/**/*.cshtml` `@{ Layout` directives shows every view either omits `Layout` (inherits from `_ViewStart.cshtml`) or explicitly sets it to `_Layout.cshtml` / `_Layout.Mobile.cshtml`. This file is genuinely new markup with no in-repo precedent — build it directly from the UI-SPEC.md "Deliverable 2" section (inline `<style>` block, OG/Twitter meta tags, meta-refresh, minimal visible body) rather than adapting an existing view. Confirm `_ViewStart.cshtml`'s default `Layout = "_Layout";` assignment is what this file must override.

---

### `QuestBoard.Service/Views/Shared/_ShareLinkButton.cshtml` (partial, new)

**Analog:** `QuestBoard.Service/Views/Shared/_Toasts.cshtml` for partial-file conventions (top-of-file `@* comment *@` documenting purpose/rendering context, Bootstrap component classes) — but explicitly **not** its `TempData`-driven mechanism. RESEARCH.md's Anti-Patterns section (lines 382) states directly: `_Toasts.cshtml` is entirely `@if (TempData[...] != null)` blocks with no JS-callable entry point, so a `navigator.clipboard.writeText()` confirmation cannot route through it. Build a self-contained button + inline/attached JS (`data-share-url` attribute + a small script handling click → clipboard write → 2s button-state swap) per UI-SPEC.md Deliverable 3, independent of the toast plumbing.

**Comment-header convention to copy** (`_Toasts.cshtml` lines 1-3):
```cshtml
@* Shared toast-notification partial. Renders Success/Error/Warning/Info flash messages
   plus the bespoke Shop GoldReceived toast from TempData. Rendered from every layout
   so any view can surface a flash message without its own local toast markup. *@
```

---

### `Views/Quest/Details.cshtml` + `Details.Mobile.cshtml` (modify, host the partial)

**Analog:** the files themselves — locate the existing "Quest Summary" sidebar `modern-card` (desktop) and `quest-header-card-mobile` block (mobile) per UI-SPEC.md Deliverable 3 placement notes; render the partial with the server-minted signed URL:
```cshtml
@await Html.PartialAsync("_ShareLinkButton", signedUrl)
```
Mint the URL in the controller action (`QuestController.Details` GET, D-15) and pass it through the existing ViewModel, following whatever pattern that action currently uses to populate other render-time-only fields on the Details ViewModel (read the ViewModel class before implementing — not covered by this pattern map since it is a data field addition, not a new file).

## Shared Patterns

### Fail-closed group query filter (do not touch, do not `IgnoreQueryFilters()`)
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:326-386`
**Apply to:** the new preview controller's repository read — after `SetGroupId(groupId)`, the existing `QuestEntity` filter (lines 335-338) re-evaluates automatically. No new filter code needed; just don't bypass it.
```csharp
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```

### Absolute URL construction — `EmailSettings:AppUrl`
**Source:** `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs:43`
**Apply to:** every absolute URL built for `og:url`, `og:image`, and the copied share link (D-07).
```csharp
var questUrl = $"{emailSettings.AppUrl}/Quest/Details/{questId}";
```
Extend with the request-derived fallback shown in RESEARCH.md lines 366-377 (`ResolveBaseUrl`) only when `AppUrl` is unset.

### Group-scoped execution outside a normal request (SetGroupId sequencing)
**Source:** `QuestBoard.Service/Jobs/HangfireJobHelper.cs:18-33`
**Apply to:** the preview controller's "set group override, then read" sequencing — same shape, but via the widened `IActiveGroupContext` interface (constructor-injected), never `scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>()` as this helper does. Treat this file as a sequencing analog only, not a resolution-strategy analog.

### Plain-text extraction for card descriptions
**Source:** `QuestBoard.Domain/Services/MarkdownService.cs:159-194` (`ExtractPlainText`)
**Apply to:** the card-description helper (D-14) — call `IMarkdownService.ExtractPlainText(quest.Description)` first, then apply the new ~200-char word-boundary truncator on the returned plain text (never on the Markdown source). No existing truncation helper matches this shape (RESEARCH.md confirms `TruncateAtBlockBoundary` at `MarkdownService.cs:262-310` operates on parsed HTML blocks + appends a link — different mechanism, not reusable here).

### Per-action `[Authorize]`, no class-level attribute
**Source:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (class declaration line 14, first `[Authorize]` at line 27)
**Apply to:** adding `[Authorize]` to `Details` GET (D-09) — follow the same bare `[Authorize]` (no policy) used on sibling actions like line 380, not `[Authorize(Policy = "DungeonMasterOnly")]` used on DM-restricted actions (lines 67/83/139/184/278).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `QuestBoard.Service/Views/LinkPreview/QuestCard.cshtml` | view | request-response | No existing view in the project sets `Layout = null;` — every view inherits or explicitly assigns `_Layout.cshtml`/`_Layout.Mobile.cshtml`. Build from UI-SPEC.md Deliverable 2 directly. |
| `wwwroot/images/link-preview-card.png` | static asset | file-I/O | Asset-production task (design tool / scripted composition), not a code pattern — source textures identified in UI-SPEC.md Deliverable 1, no code analog applicable. |

## Metadata

**Analog search scope:** `QuestBoard.Repository/Entities`, `QuestBoard.Repository/Extensions`, `QuestBoard.Repository/Migrations`, `QuestBoard.Domain/Interfaces`, `QuestBoard.Domain/Services`, `QuestBoard.Domain/Extensions`, `QuestBoard.Service/Controllers` (all), `QuestBoard.Service/Services`, `QuestBoard.Service/Jobs`, `QuestBoard.Service/Views/Shared`, `QuestBoard.Service/Views/Quest`, `QuestBoard.Service/Program.cs`, `QuestBoard.IntegrationTests/Helpers`, `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`
**Files scanned:** ~20 read/grepped directly, plus glob sweeps across `Controllers/**` and `Views/**`
**Pattern extraction date:** 2026-08-26
