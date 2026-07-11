# Phase 72: Platform Settings + Token Contract - Research

**Researched:** 2026-07-08
**Domain:** ASP.NET Core MVC SuperAdmin settings page (EF Core-backed singleton config row) + a written cross-repo HMAC token-format design contract (no token code lands in this phase)
**Confidence:** HIGH

## Summary

Phase 72 is the lowest-risk phase of the v2.0 Omphalos Integration milestone: no external repo dependency, no new NuGet packages, and a well-precedented codebase pattern (`GroupController`/`UsersController` under `Areas/Platform`) to follow almost verbatim. The only genuinely novel piece is the `IntegrationSettingEntity` itself — this codebase has zero prior "admin-editable, DB-backed singleton settings row" entities (the one existing "settings" precedent, `EmailSettings`, is `appsettings.json`/`IOptions`-bound and deploy-time only, which does not satisfy SETT-06's runtime-editable requirement). Everything else — controller shape, `SuperAdminOnly` policy, migration mechanics, modern-card UI, header-button nav wiring — has a direct, current, verified precedent in this exact codebase.

This phase also produces a design artifact with no runtime code: the written HMAC canonical-message contract that Phase 73 (Quest Board token minting) and Phase 74 (Omphalos token verification) both implement against. CONTEXT.md's D-01/D-02 already lock the two highest-risk decisions from the milestone-level research (explicit nonce field; JSON+Base64URL encoding over the pipe-delimited format the original milestone research recommended) — this phase's job is to specify the encoding down to the byte level so two independently-implemented .NET codebases produce identical HMAC input, not just to restate the field list. The single most important technical nuance this research surfaces beyond what CONTEXT.md already decided: the contract must specify that verification recomputes the HMAC over the **exact transmitted payload bytes** (decode-then-verify-then-parse), never over a byte sequence independently re-serialized from parsed fields — this is what makes "JSON" safe against the same canonicalization-ambiguity risk that sank the pipe-delimited approach, and it is not automatic just because the format is JSON.

**Primary recommendation:** Build `IntegrationSettingEntity`/`IntegrationSettingsController` as a new, narrower variant of the `GroupEntity`→`GroupController` chain (singleton row, not full CRUD — a custom 2-method service interface, not `IBaseService<T>`), reuse `WebEncoders`/`RandomNumberGenerator` (BCL, zero new packages) for the "Generate Secret" button, and write the token contract as a new top-level `.planning/TOKEN-CONTRACT.md` (not duplicated into the Omphalos repo per D-06) specifying byte-exact HMAC verification semantics explicitly.

## Architectural Responsibility Map

Quest Board is a server-rendered ASP.NET Core MVC monolith (no SPA, no separate API tier) — "Frontend Server" and "API/Backend" collapse into the same process/tier here. The table below reflects that reality rather than forcing a distinction the codebase doesn't have.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Settings persistence (URL, secret, enabled flag) | Database/Storage (SQL Server via EF Core) | — | Single-row table, no caching layer needed at this scale (17-member deployment) |
| Settings CRUD UI + validation | Backend (MVC Server — Controller + Razor SSR) | Browser/Client (form rendering only, no client JS needed) | `[Area("Platform")]` controller pattern; validation is server-side `ModelState`/DataAnnotations, matching every other Platform controller |
| Authorization gating (SuperAdmin-only) | Backend (ASP.NET Core `[Authorize(Policy = "SuperAdminOnly")]` middleware) | — | Declarative policy already registered app-wide; no new policy needed |
| "Generate Secret" cryptographic randomness | Backend (`RandomNumberGenerator`, server-side) | — | D-03 explicitly requires server-side generation, never client-side JS — the browser must not be trusted to produce the value that becomes a shared HMAC key |
| Token contract (design artifact — no runtime code this phase) | Cross-cutting / design-time | — | Consumed by Phase 73 (Backend, Quest Board) and Phase 74 (Backend, Omphalos) as an API-layer signing/verification concern in each repo; this phase only produces the written spec |

## Project Constraints (from CLAUDE.md)

- **No EF packages outside `QuestBoard.Repository`** — `IntegrationSettingEntity`, its EF Core configuration, and the migration all belong in `QuestBoard.Repository`; the repository/service/controller chain must not leak EF types into `QuestBoard.Service`.
- **Windows dev environment, CRLF line endings** — new files should match existing line-ending convention; SQL Server via `localhost` connection string in dev (already configured, confirmed in `appsettings.json`).
- **Never commit directly to `main`** — this phase's work lands on the `milestone/v2-omphalos-integration` branch (already checked out).
- **Migrations auto-apply on startup** (`context.Database.Migrate()`) — no manual `dotnet ef database update` needed in dev; confirmed `dotnet-ef 10.0.5` is installed globally.
- **No GSD requirement IDs in source code comments** — do not write `// SETT-04: blank preserves secret` in the controller; write the plain-language reason instead (e.g., `// Blank secret field means "don't change it" — never overwrite with empty string`).
- **UI/UX guidelines** — new views must use `modern-card`/`modern-card-header`/`modern-card-body`, filled (not outline) colored buttons, FontAwesome icons with `me-2`, `<hr>` before the button section, `d-flex justify-content-between` button layout (secondary/cancel left, primary/submit right). Directly verified against `Group/Index.cshtml` (see Code Examples).
- **Authorization policies** — `"SuperAdminOnly"` (`policy.RequireRole("SuperAdmin")`) is the only policy this phase needs; confirmed registered in `Program.cs:88-89`.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SETT-01 | SuperAdmin can navigate to an Omphalos Settings page from `/platform` | `GroupController`/`UsersController` `[Area("Platform")]` pattern; header-button nav wiring pattern (no shared Platform nav list exists — see Architecture Patterns) |
| SETT-02 | Settings page has input fields for Omphalos URL and shared secret | `EditShopItemViewModel.ReferenceUrl`'s `[Url]` DataAnnotation precedent for the URL field; new `type="password"` input for the secret |
| SETT-03 | Shared secret field renders as `type="password"` (masked) | Standard `<input asp-for="..." type="password">` — no library needed |
| SETT-04 | Blank secret on submit preserves existing value | `ContactsController`/`CharactersController`'s file-upload blank-preserve pattern (`// Otherwise, the [x] remains unchanged`) adapted from file-input to string-input; see Pitfalls and Code Examples |
| SETT-05 | "Integration Enabled" toggle gates all Omphalos UI + redirect | `IsEnabled bool` column on the same entity; consumed by Phase 73, not built this phase — this phase only persists the flag |
| SETT-06 | Persisted in single-row `IntegrationSettingEntity` (`OmphalosUrl`, `OmphalosSharedSecret`, `IsEnabled`) | `GroupEntity`-shape precedent, adapted to singleton-row semantics (see Architecture Patterns, Pattern 1) |
| SETT-07 | Protected by `SuperAdminOnly` policy | Confirmed registered `Program.cs:88-89`; applied identically on `UsersController`/`GroupController` |
| SETT-08 | EF Core migration creates the `IntegrationSetting` table | `dotnet ef migrations add` from `QuestBoard.Service/` targeting `QuestBoard.Repository`; naming convention confirmed against 10 most recent migrations |
| TOKEN-02 (partial — written contract only, no code this phase) | HMAC-SHA256 canonical message format written down before either side implements | See "Token Contract Design" below — this phase's actual second deliverable per the phase goal |

</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| EF Core (SQL Server provider) | 10.0.9 (already referenced, confirmed via `QuestBoard.Repository.csproj`) [VERIFIED: codebase `.csproj` inspection] | New `IntegrationSettingEntity` + migration | Existing, in-use ORM; no version change needed |
| `Microsoft.AspNetCore.WebUtilities.WebEncoders` (framework-provided, ships with `Microsoft.AspNetCore.App`) | Ships with ASP.NET Core 10.0.9 | `Base64UrlEncode`/`Decode` for the token contract's encoding step (used by Phase 73, but the contract document written in Phase 72 must name this exact API) | Already the established convention in `AdminController.cs`/`GroupController.cs` for password-reset and welcome-email tokens (4+ call sites, confirmed via direct grep) — reuse verbatim, do not invent a second encoding technique |
| `System.Security.Cryptography.RandomNumberGenerator` (BCL) | Built into .NET 10 | Server-side "Generate Secret" button (D-03) | `RandomNumberGenerator.GetString(ReadOnlySpan<char> choices, int length)` and `GetHexString(int length)` are static, CSPRNG-backed, and available since .NET 8 — confirmed current for net-10.0 via Microsoft Learn [CITED: learn.microsoft.com/dotnet/api/system.security.cryptography.randomnumbergenerator.getstring, moniker net-10.0, updated 2026-07-01] |
| `System.Text.Json` (BCL) | Built into .NET 10 | Serializing the token contract's canonical JSON payload (Phase 73/74 consumer, but this phase's written contract must specify exact serialization behavior) | Default serializer throughout ASP.NET Core; zero new dependency |

### Supporting

No supporting/third-party libraries — this phase, like the rest of the milestone, deliberately needs **zero new NuGet packages**. This was independently confirmed by the original milestone-level stack research [CITED: `.planning/research/STACK.md` @ commit `7e99ca0`, "Zero new NuGet packages needed in either repo"] and re-confirmed here by inspecting the current `QuestBoard.Repository.csproj`/`QuestBoard.Domain.csproj` — no crypto/JWT package exists in either project today.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New DB-backed `IntegrationSettingEntity` | `IOptions<OmphalosSettings>` bound from `appsettings.json`, mirroring `EmailSettings` | Rejected — SETT-01/SETT-06 explicitly require a SuperAdmin-editable-at-runtime page; `IOptions<T>` is startup-bound only and would need `IOptionsMonitor` + custom reload plumbing that doesn't exist anywhere in this codebase today |
| `IBaseService<IntegrationSetting>`/`IBaseRepository<IntegrationSetting>` (full generic CRUD, matching `GroupService`) | A narrow, custom `IIntegrationSettingService` with just `GetAsync()`/`SaveAsync()` | The generic `IBaseService<T>` contract includes `AddAsync`/`RemoveAsync`/`GetAllAsync`/`ExistsAsync(int id)` — none of which are meaningful for a singleton row that always exists at `Id = 1`. Recommend a purpose-built 2-method interface instead of forcing this entity through a CRUD abstraction built for multi-row collections (see Architecture Patterns) |
| `RandomNumberGenerator.GetString`/`GetHexString` for the shared secret | `Guid.NewGuid().ToString("N")` x2 concatenated | `Guid` is not a CSPRNG-audited API for secret generation (it's a `System.Random`-derived collision-avoidance identifier by original design intent, even though modern implementations are also CSPRNG-backed) — `RandomNumberGenerator` is the API explicitly designed and documented for security-sensitive random values; use it, not `Guid`, for the secret itself. (`Guid.NewGuid()` remains the right choice for the **nonce** field in the token contract — see Token Contract Design — since a nonce's requirement is uniqueness, not unpredictability, and `Guid` is the idiomatic .NET uniqueness primitive already used for token-ID-shaped values elsewhere in ASP.NET Core internals.) |

**Installation:**
```bash
# No packages to install — everything is BCL or already-referenced framework libs.
```

**Version verification:** `QuestBoard.Repository.csproj`/`QuestBoard.Service.csproj` inspected directly; EF Core 10.0.9 and ASP.NET Core 10.0.9 confirmed current and already in use — no `npm view`/`pip index` equivalent needed since no new packages are proposed.

## Package Legitimacy Audit

**Not applicable — this phase installs zero external packages.** All APIs used (`RandomNumberGenerator`, `WebEncoders`, `System.Text.Json`, EF Core) are either BCL or already-referenced framework/NuGet dependencies confirmed present in the current `.csproj` files. The Package Legitimacy Gate protocol (slopcheck, registry verification) was not run because there is nothing to verify — no `npm install`/`dotnet add package` command appears anywhere in this phase's design. If a future planning pass introduces any new package for this phase, re-run the gate before finalizing the plan.

## Architecture Patterns

### System Architecture Diagram

```
SuperAdmin (browser)
       │  GET /platform/Integrations
       ▼
IntegrationsController                    [Service — Areas/Platform, NEW]
[Area("Platform")] [Authorize("SuperAdminOnly")]
       │  Index() → GetAsync()
       ▼
IIntegrationSettingService                [Domain, NEW]
       │  GetAsync() / SaveAsync(model)
       ▼
IIntegrationSettingRepository             [Domain interface, NEW]
       │  implemented by
       ▼
IntegrationSettingRepository              [Repository, NEW — flat file, internal class]
       │  EF Core DbSet<IntegrationSettingEntity>
       ▼
QuestBoardContext → SQL Server            [single row, Id = 1 always]

SuperAdmin submits form (POST /platform/Integrations)
       │
       ▼
IntegrationsController.Index(POST)
       │  if (!string.IsNullOrWhiteSpace(model.SharedSecret)) → overwrite
       │  else → preserve existing OmphalosSharedSecret unchanged   (SETT-04)
       ▼
IIntegrationSettingService.SaveAsync(...)  →  persists  →  TempData["Success"]  →  redirect

SuperAdmin clicks "Generate Secret" (POST /platform/Integrations/GenerateSecret)
       │
       ▼
IntegrationsController.GenerateSecret()
       │  RandomNumberGenerator.GetString(...) → immediately SaveAsync (persist now, not
       │  deferred to a later "Save" click — see Pitfall 3 below for why)
       ▼
redirect back to Index → secret shown once in the (now populated) masked field
```

Non-SuperAdmin request path (SETT-03 verification): `[Authorize(Policy = "SuperAdminOnly")]` short-circuits before the action body runs — ASP.NET Core's authorization middleware returns 403/redirect-to-Access-Denied before `IntegrationsController` is even constructed, matching the existing app-wide `ConfigureApplicationCookie` redirect-on-policy-failure behavior confirmed in `Program.cs:91-93`.

### Recommended Project Structure

```
QuestBoard.Repository/
├── Entities/
│   └── IntegrationSettingEntity.cs          # NEW — [Table("IntegrationSettings")], Id/OmphalosUrl/OmphalosSharedSecret/IsEnabled
├── IntegrationSettingRepository.cs          # NEW — flat file (matches GroupRepository.cs location, NOT a Repositories/ subfolder)
├── Migrations/
│   └── <timestamp>_AddIntegrationSettings.cs # NEW
└── Automapper/EntityProfile.cs              # MODIFIED — add Entity<->Domain map (only if a Domain model is introduced; see Pattern 1 note)

QuestBoard.Domain/
├── Interfaces/
│   ├── IIntegrationSettingRepository.cs     # NEW
│   └── IIntegrationSettingService.cs        # NEW — narrow, NOT IBaseService<T>
├── Models/
│   └── IntegrationSetting.cs                # NEW — plain domain model
├── Services/
│   └── IntegrationSettingService.cs         # NEW
└── Extensions/ServiceExtensions.cs          # MODIFIED — register repo + service

QuestBoard.Service/
├── Areas/Platform/
│   ├── Controllers/
│   │   └── IntegrationsController.cs        # NEW
│   └── Views/Integrations/
│       ├── Index.cshtml                     # NEW — desktop
│       └── Index.Mobile.cshtml              # NEW — mobile (parity required, see Pitfalls)
├── ViewModels/PlatformViewModels/
│   └── IntegrationSettingsViewModel.cs      # NEW
└── Areas/Platform/Views/Group/
    ├── Index.cshtml                         # MODIFIED — add header button to Integrations
    └── Index.Mobile.cshtml                  # MODIFIED — same, mobile

.planning/
└── TOKEN-CONTRACT.md                        # NEW — top-level, not nested in phase 72's dir (see Token Contract Design)
```

### Pattern 1: Singleton settings row — deviate from `IBaseService<T>`/`IBaseRepository<T>`

**What:** Every existing Domain service (`GroupService`, `UserService`, etc.) implements `IBaseService<T>` (`AddAsync`/`GetAllAsync`/`GetByIdAsync`/`RemoveAsync`/`UpdateAsync`) because every existing entity is a genuine multi-row collection. `IntegrationSettingEntity` is a **singleton** — exactly one row, always `Id = 1`, no create/delete/list semantics. Forcing it through `IBaseService<T>` produces meaningless methods (`GetAllAsync()` returning a 1-item list, `ExistsAsync(int id)` requiring a redundant `id` parameter that's always `1`).

**When:** Any config entity that is architecturally a singleton, not a collection.

**Example:**
```csharp
// QuestBoard.Domain/Interfaces/IIntegrationSettingService.cs
namespace QuestBoard.Domain.Interfaces;

public interface IIntegrationSettingService
{
    /// <summary>Returns the current settings row, creating a default (disabled, empty) row on first access.</summary>
    Task<IntegrationSetting> GetAsync(CancellationToken token = default);

    /// <summary>
    /// Persists the settings. If newSecret is null/whitespace, the existing OmphalosSharedSecret
    /// is preserved unchanged — callers must not pass an empty string to mean "clear it."
    /// </summary>
    Task SaveAsync(string omphalosUrl, string? newSecret, bool isEnabled, CancellationToken token = default);
}
```
This still composes a `BaseRepository<TModel, TEntity>`-derived repository underneath for the raw `Get`/`Update` mechanics (reuse, don't reinvent EF plumbing) — only the **service-layer** contract deviates from the generic shape, matching how `GroupService` already adds bespoke methods (`GetAllWithMemberCountAsync`, `HasMembersAsync`) alongside its base CRUD.

### Pattern 2: `IntegrationSettingEntity` follows `GroupEntity`'s shape, adapted for a fixed single row

**What:** `GroupEntity` (`QuestBoard.Repository/Entities/GroupEntity.cs`) is the closest existing shape: flat DataAnnotations, `[Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id`. For a genuinely singleton table, consider following the `DungeonMasterProfileEntity` precedent instead (`.Property(p => p.Id).ValueGeneratedNever()`, confirmed in `QuestBoardContext.cs:159-162`) so `Id` is always exactly `1` rather than auto-incrementing — this makes "does a row already exist" trivially `FindAsync(1)` with no query needed. Either approach works; `ValueGeneratedNever()` with a hardcoded `Id = 1` is marginally cleaner for a true singleton and avoids ever having row `Id = 2` exist by accident.

**Example (entity, using REQUIREMENTS.md's exact column names — see Pitfall 4 below for why this matters):**
```csharp
namespace QuestBoard.Repository.Entities;

[Table("IntegrationSettings")]
public class IntegrationSettingEntity : IEntity
{
    [Key]
    public int Id { get; set; } = 1;   // ValueGeneratedNever() in OnModelCreating — singleton row

    [Required, StringLength(500)]
    public string OmphalosUrl { get; set; } = string.Empty;

    [StringLength(200)]
    public string OmphalosSharedSecret { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
}
```
**No `HasQueryFilter` call for this entity** — confirmed by direct inspection of `QuestBoardContext.OnModelCreating`: `GroupEntity` itself and `UserEntity` are the only entities with no tenant query filter registered (Groups/Users are the tenant boundary, not tenant-scoped data). `IntegrationSettingEntity` is instance-wide by design (PROJECT.md Key Decision, confirmed) — it must join that same "not filtered" set, not the `QuestEntity`/`ShopItemEntity`/`CharacterEntity`-style filtered set. Getting this wrong (accidentally adding a query filter) would make the settings row invisible whenever `ActiveGroupContext.ActiveGroupId` is null — a subtle, hard-to-spot bug that would surface as "SuperAdmin configured Omphalos but nothing works" with no obvious cause.

### Pattern 3: Blank-preserves-existing-value guard (SETT-04) — adapt the file-upload pattern to a string field

**What:** The established pattern in this codebase for "blank input = don't change the stored value" is currently only implemented for file uploads (`ContactsController.cs`, `CharactersController.cs` — "Otherwise, the [image] remains unchanged"). No existing precedent handles a **masked string** field this way (password-change forms in this codebase, e.g. `AdminController.EditUser`, handle email-change guards, not masked-secret guards — CONTEXT.md's `code_context` already flags this distinction explicitly). The adaptation is straightforward: same "only touch it if a new value was actually submitted" logic, applied to `string?` instead of `IFormFile?`.

**When:** SETT-04's exact requirement — implement in the controller's POST action, not buried in the service (keep the "was a new value submitted" decision visible at the boundary where the raw posted value is available).

**Example:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Index(IntegrationSettingsViewModel model, CancellationToken token)
{
    if (!ModelState.IsValid) return View(model);

    // Blank secret field means "don't change it" — never overwrite the stored secret with
    // an empty string just because the field was left masked/blank on this edit.
    var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;

    await integrationSettingService.SaveAsync(model.OmphalosUrl, newSecret, model.IsEnabled, token);
    TempData["Success"] = "Integration settings saved.";
    return RedirectToAction(nameof(Index));
}
```
**Critically, the GET action must never populate `model.SharedSecret` with the actual stored value** — this is the flip side of the same guard. Pre-filling a password-type input with the real secret (a) defeats the point of masking it and (b) makes "leave it blank" indistinguishable from "I copy-pasted the exact same value back." Expose a separate `bool HasSecretConfigured` on the ViewModel for the GET view to render a "•••• configured" placeholder/label instead.

### Pattern 4: Nav wiring — header button, not a shared Platform nav list

**What:** Confirmed via direct inspection of both `_Layout.Platform.cshtml` and `_Layout.Platform.Mobile.cshtml`: there is no shared per-page nav list in the Platform area layout. Cross-linking between Platform pages is done via a header button on the *source* page. `UsersController`'s addition (merged 2026-07-08) wired `<a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">Manage Users</a>` directly into `Group/Index.cshtml`'s header, both desktop and `.Mobile.cshtml`.

**When:** Add the new Integrations page's entry point as a third header button on `Group/Index.cshtml`/`Index.Mobile.cshtml`, matching the existing two exactly (button color/icon choice at planner's discretion — `btn-secondary` with an icon like `fa-plug` would be visually consistent with the existing `fa-users-cog` "Manage Users" button, though this is a cosmetic detail CONTEXT.md leaves to planning).

**Example (verified current markup, `Group/Index.cshtml:12-19`):**
```html
<div class="d-flex gap-2">
    <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success">
        <i class="fas fa-plus me-2"></i>Create Group
    </a>
    <a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">
        <i class="fas fa-users-cog me-2"></i>Manage Users
    </a>
    <!-- NEW: -->
    <a asp-controller="Integrations" asp-action="Index" asp-area="Platform" class="btn btn-secondary">
        <i class="fas fa-plug me-2"></i>Integrations
    </a>
</div>
```
Mobile parity is not optional — PROJECT.md's own Key Decisions log records two separate follow-up phases (43, 54) that had to backfill desktop-only fixes onto mobile, and explicitly calls out "pairing desktop+mobile edits into the SAME task" as the fix. Any task touching `Group/Index.cshtml` must touch `Group/Index.Mobile.cshtml` in the same task, and the new `Integrations/Index.cshtml` must ship with `Integrations/Index.Mobile.cshtml` alongside it, not as a follow-up.

### Pattern 5: "Enabled" toggle — plain checkbox, not a switch

**What:** Grepped every `form-check`/`form-switch` usage in the Service project; every boolean toggle in this codebase (`AdminController.EditUser`'s `HasKey`, confirmed at `EditUser.cshtml:41-42`) uses a plain Bootstrap `form-check` checkbox, never `form-switch`. There is no toggle-switch precedent anywhere in the app.

**When:** The "Integration Enabled" checkbox (SETT-05) should match this exactly — do not introduce a `form-switch` as a new, one-off visual pattern.

**Example (verified current markup, `EditUser.cshtml:40-47`, directly adaptable):**
```html
<div class="form-check">
    <input asp-for="IsEnabled" class="form-check-input" type="checkbox" />
    <label asp-for="IsEnabled" class="form-check-label">
        <i class="fas fa-toggle-on text-success me-2"></i>
        Integration Enabled
    </label>
</div>
```

### Anti-Patterns to Avoid

- **Adding `OmphalosUrl`/`OmphalosSharedSecret` columns to `GroupEntity`:** Already explicitly decided against in PROJECT.md's Key Decisions table (confirmed, line 227) — Omphalos has no tenant concept, so a per-group value has nothing to bind to on the Omphalos side. Use the new instance-wide `IntegrationSettingEntity` (not tenant-filtered) instead.
- **Pre-populating the masked secret input with the real stored value on GET:** Defeats masking and makes the blank-preserve guard ambiguous. Show a "configured" indicator instead (see Pattern 3).
- **Forcing `IntegrationSettingEntity` through `IBaseService<T>`:** See Pattern 1 — produces meaningless `GetAllAsync`/`ExistsAsync(int id)` methods for a table that only ever has one row.
- **Generating the secret client-side (JavaScript `crypto.getRandomValues`):** D-03 explicitly requires server-side generation. Even though browser CSPRNG is itself sound, routing the "Generate Secret" action through a server POST keeps the generation auditable/loggable and consistent with this app's "no AJAX/fetch, full-page POST" convention (confirmed convention, per the recent crop-feature research and every controller inspected this session — no `fetch()`/AJAX pattern exists anywhere in the Platform area).
- **Treating "Generate Secret" as a client-side-only fill that requires a separate "Save" click to persist:** See Pitfall 3 below — recommend the Generate action persists immediately.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Cryptographically random secret generation | A custom random-string generator (`Guid`-concatenation, `System.Random`, manual char-array shuffling) | `RandomNumberGenerator.GetString(choices, length)` or `.GetHexString(length)` (BCL, .NET 8+) | Purpose-built CSPRNG API; hand-rolling risks a non-uniform or predictable distribution, and there is zero benefit to not using the framework-provided, audited method |
| URL-safe token encoding (for Phase 73/74, but the contract this phase writes must name it) | Hand-rolled `Replace('+','-').Replace('/','_').TrimEnd('=')` | `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode/Decode` | Already the established convention in this exact codebase (4+ call sites in `AdminController.cs`/`GroupController.cs`); a hand-rolled version risks subtly different padding/alphabet behavior between the two independently-implemented repos, exactly the class of bug the milestone's own Pitfalls research flagged as the top risk |
| Constant-time signature comparison (Phase 73/74 concern, but worth naming in the contract) | `==`/`.Equals()`/`SequenceEqual` on the HMAC bytes | `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals` | BCL-provided, timing-attack-safe; a plain equality check leaks signature bytes one at a time via response-timing side channels |
| Settings row "does it exist yet" check | A raw SQL `SELECT COUNT(*)` or manual first-run bootstrapping script | EF Core `FindAsync(1)` returning null on first run, service layer creates-or-returns-default | Matches the existing `BaseRepository.GetByIdAsync` pattern; no new infrastructure needed |

**Key insight:** Every piece of cryptographic or security-relevant plumbing this phase touches (secret generation, and by extension the encoding conventions the Phase 72-written contract locks in for Phase 73/74) already has a zero-new-dependency BCL answer that's also already precedented in this exact codebase. There is no legitimate reason for this phase — or the ones that follow it against the contract it writes — to hand-roll any of it.

## Common Pitfalls

### Pitfall 1: The written token contract is under-specified in exactly the way that lets JSON "feel safe" without actually being safe

**What goes wrong:** D-02 already resolved the field-shifting collision risk by choosing JSON over pipe-delimited concatenation — but JSON alone does not guarantee two independently-implemented codebases produce byte-identical serialization. If the contract document only says "serialize as JSON, HMAC-sign it," Phase 73 and Phase 74 could each build the JSON via slightly different means (a C# record vs. a manually-built `JsonObject`, different property ordering, different whitespace/formatting settings) and, if either side **re-serializes** the payload before verifying the signature (rather than verifying over the exact bytes that were originally signed), a byte-for-byte mismatch causes every token to fail verification — or worse, if the mismatch is coincidentally masked by an accommodating deserializer, it could reopen a subtler version of the same canonicalization risk pipe-delimiting had.

**Why it happens:** "We used JSON instead of a delimited string" reads as a complete fix for the canonicalization problem the original research flagged, but it only fixes the *delimiter-collision* half of that problem. The *serializer-determinism* half is still live unless the contract explicitly forecloses it.

**How to avoid:** The written contract (this phase's actual deliverable) must specify that verification works by decoding the **transmitted payload segment back to its original raw bytes** and computing `HMACSHA256.HashData(secret, thoseExactBytes)` — never by re-serializing the parsed/deserialized fields and hoping the result matches. Concretely:
1. Signer: `payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))`; `signature = HMACSHA256.HashData(secretBytes, payloadBytes)`; `token = WebEncoders.Base64UrlEncode(payloadBytes) + "." + WebEncoders.Base64UrlEncode(signature)`.
2. Verifier: split on `.`, `WebEncoders.Base64UrlDecode` **both segments back to raw bytes**, recompute `HMACSHA256.HashData(secretBytes, decodedPayloadBytes)`, compare via `CryptographicOperations.FixedTimeEquals` against the decoded signature bytes — **only after** the signature check passes, `JsonSerializer.Deserialize` the same `decodedPayloadBytes` to get usable fields.
This ordering means the two sides never need to agree on serializer property-ordering behavior at all, because the verifier never independently re-serializes anything — it only ever operates on the exact bytes the signer produced. This is the same structural pattern JWT itself uses (sign over `base64url(header).base64url(payload)` as a literal string, never re-derived) and should be named explicitly as the model to follow, even though this design deliberately isn't using an actual JWT library (per the original milestone stack research's "what NOT to add" guidance, reconfirmed still valid — no JWT package exists in either repo today).

**Warning signs:** A code review or draft PR for Phase 73/74 where the verifier path calls `JsonSerializer.Deserialize` *before* the signature check, or re-serializes fields to recompute the HMAC input instead of operating on the original transmitted bytes.

**Phase to address:** This phase (72) — the written contract must state this explicitly, in prose, with the exact function-call sequence above, not just "sign the JSON."

---

### Pitfall 2: Secret storage posture — a DB-backed plaintext secret is a deliberate tradeoff, not a free byproduct of "it was easy to add a column"

**What goes wrong:** SETT-06 requires the shared secret to live in the new `IntegrationSettingEntity` row — i.e., in SQL Server, in plaintext, inside regular DB backups. This is a materially different security posture than every other secret this codebase currently handles (e.g., `EmailSettings__SmtpPassword`, confirmed to live in an env-var/`appsettings`-outside-source-control pattern, never in the application database). Shipping this without acknowledging the shift means DB backups now contain a credential that, if leaked, is directly usable to forge SSO tokens.

**Why it happens:** The requirement explicitly asks for a DB-backed, SuperAdmin-editable settings row (correctly — that's the only way to satisfy "runtime editable without redeploy"), and it's easy to treat that as settled without a second thought about the storage-at-rest implication.

**How to avoid:** This is not a reason to change the design (SETT-06 is a locked requirement) — it is a reason to (a) document the tradeoff explicitly in the phase's plan/PR description so it's a visible decision, not an accident, and (b) confirm this database's existing backup security posture is one the team is comfortable extending to cover a live credential (out of scope to change in this phase, but worth a one-line note rather than silence). No code changes are implied by this pitfall — it's a documentation/awareness item for whoever reviews the PR.

**Phase to address:** This phase, as a one-line acknowledgment in the plan or PR description — not a blocking implementation task.

---

### Pitfall 3: Conflating "Generate Secret" with "Save" produces a real chance of losing a freshly generated secret

**What goes wrong:** If "Generate Secret" only fills the visible field client-side (or server-renders it into the response) without persisting, and the SuperAdmin navigates away, refreshes, or the session expires before clicking the separate "Save" button, the freshly generated value is lost — and because the field is masked/blank on next load either way, there would be no visible sign anything went wrong until the SuperAdmin tries to paste the (now-nonexistent) secret into Omphalos and SSO fails.

**How to avoid:** Make the `GenerateSecret` POST action persist the new secret immediately (via the same `SaveAsync` path, updating only `OmphalosSharedSecret` and leaving `OmphalosUrl`/`IsEnabled` untouched), then redirect back to the Index GET with `TempData` carrying the plaintext value for exactly one render — this is the standard "shown once, then gone" pattern used by API-key-generation UIs generally (GitHub PATs, AWS access keys) and matches D-03's own wording ("shown once in the masked field"). Do not require a second, separate "Save" click for the generated value to actually persist — that's an easy way to lose it.

**Warning signs:** A `GenerateSecret` action that only returns a partial view / sets a hidden field without calling into `IIntegrationSettingService.SaveAsync`.

**Phase to address:** This phase — decide and implement this explicitly, don't leave the persist-timing question implicit.

---

### Pitfall 4: Column-naming drift between the milestone's earlier architecture research and the now-locked REQUIREMENTS.md

**What goes wrong:** The original milestone-level `ARCHITECTURE.md` (written 2026-07-02, before `REQUIREMENTS.md` was finalized the same day) used `OmphalosBaseUrl` as the column name. The now-authoritative `REQUIREMENTS.md` (SETT-06, locked) uses `OmphalosUrl`. If a planner or implementer references the older research doc without cross-checking, the entity could be built with the wrong column name, which would then need a follow-up migration to fix — small, but avoidable.

**How to avoid:** Use `OmphalosUrl`, `OmphalosSharedSecret`, `IsEnabled` — copied verbatim from `REQUIREMENTS.md` SETT-06 — not the older research artifact's naming. This RESEARCH.md's Code Examples above already use the corrected names.

**Phase to address:** This phase, at entity-definition time.

---

### Pitfall 5: SETT-08's literal wording ("`IntegrationSetting` table," singular) conflicts with this codebase's established plural table-naming convention

**What goes wrong:** SETT-08 says "creates the `IntegrationSetting` table" (singular). Every existing entity in this codebase uses a plural `[Table("...")]` name (`Groups`, not `Group`; presumably `Contacts`, `Users`, etc., following standard EF Core/relational convention). Taking SETT-08's prose completely literally would produce a naming inconsistency with the rest of the schema.

**How to avoid:** Treat SETT-08's wording as describing the *migration's effect* ("a table for integration settings gets created"), not as a literal mandate for the exact `[Table(...)]` attribute string. Recommend `[Table("IntegrationSettings")]` (plural) for schema consistency with `Groups`/every other entity — this is a minor, low-risk naming call, but flag it explicitly for the planner/discuss step rather than silently picking one, since it's the kind of detail a careful reviewer might reasonably expect to match the requirement text exactly.

**Phase to address:** This phase, at entity-definition time — low stakes either way, but should be a deliberate choice, not an oversight.

---

### Pitfall 6: TOKEN-02's "copied verbatim into both repos' planning docs" wording is superseded by D-06 — don't literally duplicate the file into the Omphalos repo

**What goes wrong:** `REQUIREMENTS.md`'s TOKEN-02 text says the contract must be "copied verbatim into both repos' planning docs." Read literally, this implies committing a duplicate file into `C:\Repos\omphalos`. But CONTEXT.md's D-06 (a later, more specific, explicitly-discussed decision — see `72-DISCUSSION-LOG.md`) locks in the opposite: the document stays only in Quest Board's `.planning/`, and Phase 74's PR description references it by pointing back, because Omphalos has no `.planning/`/docs convention today (confirmed: only `README.md`/`CLAUDE.md` at its repo root).

**How to avoid:** Follow D-06, not TOKEN-02's literal phrasing — D-06 is the locked decision (from `## Decisions` in CONTEXT.md, which downstream agents must treat as authoritative over an earlier, less-specific requirement phrasing). Do not create any new file in `C:\Repos\omphalos` as part of this phase or a future one for this purpose.

**Phase to address:** This phase, when producing the written contract — write it once, in Quest Board's `.planning/`, and stop there.

## Token Contract Design

This is the concrete content this phase's written contract document should specify, synthesizing CONTEXT.md's locked decisions (D-01, D-02) with the byte-exact verification guidance from Pitfall 1 above. This phase does not implement any of this — Phase 73 signs, Phase 74 verifies — but the contract must be unambiguous enough that both phases can be implemented independently without a live cross-repo test being the only way to catch a mismatch.

**Recommended field list** (per D-02, verbatim): `nonce`, `userId`, `questId`, `questTitle`, `questDate`, `expiry`.

**Recommended field types/encoding for the contract to lock in explicitly:**

| Field | Type | Notes |
|-------|------|-------|
| `nonce` | `string` (GUID, e.g. `Guid.NewGuid().ToString()`) | Uniqueness is the only requirement (Phase 74 keys its used-token table on this) — cryptographic unpredictability is not required for a nonce, unlike the shared secret itself. `Guid` is the idiomatic .NET choice and needs no new API. |
| `userId` | `int` | Quest Board's `UserEntity.Id` — confirmed `int` via `UserEntity : IdentityUser<int>`. **Never** `Name`/`UserName`/`Email` (TOKEN-04, and the milestone's own Phase 34.3 precedent for why not) |
| `questId` | `int` | `QuestEntity.Id` |
| `questTitle` | `string` | Free text — safe inside JSON regardless of content, which is the entire point of moving off pipe-delimiting |
| `questDate` | `string`, ISO-8601/round-trip (`"o"` format), UTC | Human-readable pass-through for Omphalos session naming (TOKEN-06) — not used for any expiry/security logic, so format ambiguity here is a cosmetic risk only |
| `expiry` | `long` (Unix seconds, UTC) | Not an ISO-8601 string — avoids timezone-parsing ambiguity between two independently-deployed hosts for the one field that **is** security-relevant (TOKEN-03's 300-second TTL check). This specific recommendation carries forward unchanged from the original milestone stack research, which is still sound regardless of the JSON-vs-pipe-delimited format change. |

**Signing/verification sequence to specify verbatim in the contract document** (see Pitfall 1 for full rationale):
1. Signer serializes the payload object to JSON, UTF-8 bytes.
2. Signer computes `HMACSHA256.HashData(UTF8(secret), payloadBytes)`.
3. Signer Base64URL-encodes (`WebEncoders.Base64UrlEncode`) both the payload bytes and the signature bytes, joins with a literal `.`.
4. Verifier splits on `.`, Base64URL-decodes **both segments back to raw bytes** (never re-serializes).
5. Verifier recomputes the HMAC over the decoded payload bytes and compares to the decoded signature bytes using `CryptographicOperations.FixedTimeEquals`.
6. Only after step 5 passes does the verifier `JsonSerializer.Deserialize` the payload bytes into usable fields, then separately checks `expiry` against current time (with a small clock-skew allowance, e.g. ±30s, per the original milestone research's still-valid guidance on this point) and checks `nonce` against Phase 74's used-token table for replay.

**Where the document should live:** `.planning/TOKEN-CONTRACT.md` (new, top-level — not nested under `phases/72-.../`, since Phase 73 and Phase 74 both need to reference it long after Phase 72's directory is no longer the active phase; this mirrors how `PROJECT.md`/`REQUIREMENTS.md` are already top-level, cross-phase artifacts). Per D-06, this file is **not** duplicated into `C:\Repos\omphalos` — Phase 74's PR description links back to it.

## Code Examples

### Existing WebEncoders convention (verified, `AdminController.cs`)
```csharp
// Source: QuestBoard.Service/Controllers/Admin/AdminController.cs:134 (and 3 other call sites)
var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
```

### RandomNumberGenerator secret generation (BCL, .NET 8+, confirmed current for net-10.0)
```csharp
// Source: learn.microsoft.com/dotnet/api/system.security.cryptography.randomnumbergenerator.getstring (net-10.0)
private const string SecretChars =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

var newSecret = RandomNumberGenerator.GetString(SecretChars, length: 48);
// Or, if a hex-only value is preferred (simpler to eyeball/paste, still CSPRNG):
var newSecretHex = RandomNumberGenerator.GetHexString(64); // 64 hex chars = 256 bits
```

### Blank-preserve pattern, adapted from file-upload to string (see Architecture Patterns, Pattern 3)
```csharp
// Source: adapted from QuestBoard.Service/Controllers/Contacts/ContactsController.cs:238
// ("Otherwise, the contact image remains unchanged.") — same guard, applied to a string field.
var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;
await integrationSettingService.SaveAsync(model.OmphalosUrl, newSecret, model.IsEnabled, token);
```

### EF Core migration command (following the codebase's documented workflow)
```bash
# Run from QuestBoard.Service/, per CLAUDE.md
dotnet ef migrations add AddIntegrationSettings --project ../QuestBoard.Repository
# No manual `database update` needed — auto-applied on startup via context.Database.Migrate()
```

## State of the Art

| Old Approach (milestone research, 2026-07-02, pre-CONTEXT.md) | Current Approach (this phase, post-discussion) | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Pipe-delimited payload (`"{email}\|{issuedAt}\|{expiresAt}\|{questId}"`), signed as a raw string | JSON payload (6 named fields incl. `nonce`), HMAC-signed over raw JSON bytes, Base64URL-encoded | D-02, discussed and locked 2026-07-02 (same day as the original research, superseding it before it was ever implemented) | Eliminates the delimiter-collision risk a free-text `questTitle` field would have introduced into a pipe-delimited scheme; the original design deliberately *excluded* `questTitle` from the signed payload specifically to avoid this — the new JSON design can safely include it (TOKEN-06 requires it) |
| Cross-system identity key: `email` (Quest Board `IdentityUser.Email`) | Cross-system identity key: `UserEntity.Id` (int) | D-01, locked 2026-07-02; independently reconfirmed in PROJECT.md's Key Decisions table (line 228) | Avoids requiring a schema change on Omphalos's side to add an `Email` column just for matching, and sidesteps any case-normalization/uniqueness assumptions about email — direct numeric ID match is simpler and was the milestone's own Architecture/Pitfalls research's converged recommendation even before CONTEXT.md formalized it |
| No explicit nonce/jti field (5-min token TTL alone was considered sufficient residual risk) | Explicit `nonce` field, Phase 74 replay-protection keys off it | D-01, locked 2026-07-02; PROJECT.md Key Decision (line 231) — user pushed back on an initial "small trusted group, skip replay protection" scope-cut | Replay protection is now in-scope for the milestone; this phase's contract must include the nonce field for Phase 74 to have something to track |

**Deprecated/outdated:** The original `.planning/research/SUMMARY.md`/`STACK.md`/`ARCHITECTURE.md`/`PITFALLS.md` files at their *current* HEAD content are **not** about this milestone at all — they were overwritten by the unrelated v7.0 (image-cropping/backlog-cleanup) milestone's research on 2026-07-04 and never restored. This RESEARCH.md recovers the original Omphalos-milestone research content via `git show 7e99ca0:.planning/research/{SUMMARY,STACK,ARCHITECTURE,PITFALLS}.md` (commit `7e99ca0`, "docs: research summary for milestone v2.0 Omphalos Integration"). **If a future phase needs to re-consult that research, it must retrieve it from that commit, not from the current working tree** — the working-tree copies at those paths belong to a different milestone.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `[Table("IntegrationSettings")]` (plural) is the better table name than SETT-08's literal singular wording | Pitfall 5 / Pattern 2 | Low — purely cosmetic schema-naming choice, trivially fixed with a follow-up migration if the team prefers literal-singular |
| A2 | `Guid.NewGuid()` is an acceptable nonce-generation method (vs. `RandomNumberGenerator.GetHexString`) | Token Contract Design | Low — nonce only needs uniqueness, not unpredictability; either works, this is a style preference the contract document should state explicitly either way |
| A3 | FontAwesome `fa-plug` is a reasonable icon choice for the "Integrations" nav button | Pattern 4 | None — CONTEXT.md explicitly leaves icon choice to planning; this is a suggestion, not a locked recommendation |
| A4 | The "Generate Secret" action should persist immediately rather than deferring to a separate Save click | Pitfall 3 | Medium — if planning chooses the opposite (defer-to-Save) design instead, that's a legitimate alternative, but it must be a deliberate choice with the loss-on-navigate-away risk acknowledged, not an accidental default |
| A5 | `System.Text.Json`'s default serializer settings (no special `JsonSerializerOptions`) are sufficient for the token contract's payload — no custom naming policy needed | Token Contract Design | Low — since verification never re-serializes (Pitfall 1's core guidance), the specific `JsonSerializerOptions` used by the signer don't need to match anything on the verifier side at all; this assumption only affects readability/convention, not correctness |

**If this table is empty:** N/A — see entries above. All are low-to-medium risk, cosmetic-or-style-level items; none affect the phase's core correctness (persistence, authorization, blank-preserve guard, or the byte-exact-verification contract design).

## Open Questions (RESOLVED)

1. **Should the "Test Connection" idea from the original Pitfalls research (a button that round-trips a validation call to Omphalos before the SuperAdmin considers the feature live) be in scope for this phase?**
   - What we know: The original milestone-level `PITFALLS.md` flagged "SuperAdmin configures URL/secret but never tests the connection before DMs try it live" as a UX pitfall, recommending a test-connection action.
   - What's unclear: This would require Quest Board to make an outbound HTTP call to Omphalos, which doesn't exist until Phase 74 ships the SSO endpoint — so a real "test connection" can't functionally work until all three phases are done, making it a poor fit for Phase 72 specifically.
   - RESOLVED: Out of scope for Phase 72 (no SETT-* requirement asks for it, and it can't be meaningfully implemented before Phase 74 exists); flag as a candidate follow-up once all three phases ship, if desired.

2. **Exact route path and page title for the "Integrations" page.**
   - What we know: CONTEXT.md D-05 locks the generic "Integrations" naming/framing; exact route, icon, copy explicitly left to planning.
   - What's unclear: Nothing blocking — this is a pure implementation-detail choice with no research-level ambiguity.
   - RESOLVED: `Areas/Platform/Controllers/IntegrationsController.cs`, default `Index` action at `/platform/Integrations`, matching `GroupController`/`UsersController`'s own `{Controller}/{action}` convention (no custom route needed).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/run | ✓ | 10.0.301 | — |
| `dotnet-ef` global tool | Migration authoring | ✓ | 10.0.5 | — |
| SQL Server (dev, `localhost`) | EF Core migration apply, runtime persistence | Not directly probed this session (no live DB connection attempted) | — | Migrations auto-apply on startup per CLAUDE.md; if SQL Server isn't running locally, `dotnet run` will fail at `context.Database.Migrate()` with a clear connection error — not a silent failure mode |

**Missing dependencies with no fallback:** None identified — this phase has no new external dependency beyond what the project already requires to build/run at all.

**Missing dependencies with fallback:** None applicable.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 (confirmed via `TestContext.Current.CancellationToken` usage pattern in existing tests), NSubstitute for mocking, FluentAssertions-style `.Should()` assertions |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSetting"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SETT-01 | SuperAdmin can navigate to Integrations page | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Integrations"` | ❌ Wave 0 — new `IntegrationsAreaIntegrationTests.cs`, modeled on `PlatformAreaIntegrationTests.cs` |
| SETT-02/03 | URL + masked secret fields present | unit (ViewModel validation) / integration (rendered HTML contains `type="password"`) | same as above | ❌ Wave 0 |
| SETT-04 | Blank secret preserves existing value | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSettingService"` | ❌ Wave 0 — new `IntegrationSettingServiceTests.cs`, modeled on `GroupServiceTests.cs` |
| SETT-05 | `IsEnabled` persists correctly | unit | same as SETT-04's file | ❌ Wave 0 |
| SETT-06 | Single-row entity, correct columns | unit (repository) or a migration-applies-cleanly smoke test | `dotnet ef migrations add --dry-run`-style manual check, or a repository test | ❌ Wave 0 |
| SETT-07 | Non-SuperAdmin denied | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Integrations"` | ❌ Wave 0 — mirror `PlatformIndex_WhenNotSuperAdmin_ShouldDeny`/`_WhenAdmin_ShouldDeny`/`_WhenNotAuthenticated_ShouldRedirect` from `PlatformAreaIntegrationTests.cs` exactly, retargeted at the new controller |
| SETT-08 | Migration creates the table | build-time (migration applies during `dotnet ef database update` / integration test DB setup) | Covered implicitly by any integration test that hits the new controller against a real (test) DB | ✓ — existing `WebApplicationFactoryBase` test infrastructure already runs migrations against a test database |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSetting"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Services/IntegrationSettingServiceTests.cs` — covers SETT-04, SETT-05 (blank-preserve guard, enabled-flag persistence), modeled directly on `GroupServiceTests.cs`'s NSubstitute-based pattern
- [ ] `QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs` — covers SETT-01, SETT-07 (authorization matrix: SuperAdmin/Admin/Player/unauthenticated), modeled directly on `PlatformAreaIntegrationTests.cs`'s existing 4-test shape (same `AuthenticationHelper` calls, same assertions, retargeted URL)
- No new test framework install needed — xUnit v3/NSubstitute/`WebApplicationFactoryBase` are already fully set up and proven for this exact `Areas/Platform` + `SuperAdminOnly` shape.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | This phase adds no new authentication surface — relies entirely on existing ASP.NET Core Identity cookie auth |
| V3 Session Management | No | No session-management change |
| V4 Access Control | Yes | `[Authorize(Policy = "SuperAdminOnly")]`, identical to `GroupController`/`UsersController` — no new policy, reuses the existing `policy.RequireRole("SuperAdmin")` registered in `Program.cs:88-89` |
| V5 Input Validation | Yes | `[Url]` DataAnnotation on `OmphalosUrl` (precedented, `EditShopItemViewModel.ReferenceUrl`); `[StringLength]` bounds on the secret field; standard ASP.NET Core `ModelState` validation, no custom parser |
| V6 Cryptography | Yes | `RandomNumberGenerator` (BCL, CSPRNG) for secret generation — never hand-rolled; the secret itself is stored, not hashed (it must be recoverable/copyable by the SuperAdmin, unlike a password), which is an inherent property of a shared-secret bridge credential, not a gap — see Pitfall 2 for the storage-posture acknowledgment this implies |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on the settings-save/generate-secret POST actions | Tampering | `[ValidateAntiForgeryToken]` — already the established convention on every mutating Platform-area action (`GroupController`, `UsersController`); apply identically |
| Privilege escalation (non-SuperAdmin reaching the settings page) | Elevation of Privilege | `[Authorize(Policy = "SuperAdminOnly")]` at the controller level (not per-action) — matches existing convention, fails closed by ASP.NET Core's authorization middleware before the action body runs |
| Secret disclosure via GET-populated masked field | Information Disclosure | Never populate `SharedSecret` on GET with the real value (Pattern 3) — render a "configured" indicator instead |
| Secret disclosure via server/access logs (the URL/query string, not applicable to this phase's own settings page, but relevant to the contract this phase writes for Phase 73/74) | Information Disclosure | Out of this phase's direct scope (no token is generated or transmitted yet) — but the written contract should note the short (300s) TTL and single-use nonce as the mitigations Phase 73/74 rely on, consistent with the original milestone Pitfalls research |

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection (this session): `QuestBoard.Service/Areas/Platform/Controllers/{UsersController,GroupController}.cs`, `QuestBoard.Repository/Entities/{GroupEntity,UserEntity,IEntity}.cs`, `QuestBoard.Repository/{BaseRepository,GroupRepository}.cs`, `QuestBoard.Repository/Entities/QuestBoardContext.cs` (full `OnModelCreating`, query-filter registration), `QuestBoard.Repository/Extensions/ServiceExtensions.cs`, `QuestBoard.Domain/Extensions/ServiceExtensions.cs`, `QuestBoard.Domain/{Interfaces/IGroupService.cs,Interfaces/IBaseService.cs,Models/Group.cs}`, `QuestBoard.Service/Program.cs` (policy registration, lines 83-93), `QuestBoard.Service/Controllers/{Contacts/ContactsController.cs,Admin/AdminController.cs}`, `QuestBoard.Service/Views/Admin/EditUser.cshtml`, `QuestBoard.Service/ViewModels/ShopViewModels/EditShopItemViewModel.cs`, `QuestBoard.Service/Areas/Platform/Views/Group/{Index.cshtml,Index.Mobile.cshtml}`, `QuestBoard.UnitTests/Services/GroupServiceTests.cs`, `QuestBoard.IntegrationTests/Controllers/PlatformAreaIntegrationTests.cs`, `.github/workflows/dotnet.yml`
- `git show 7e99ca0:.planning/research/{SUMMARY,STACK,ARCHITECTURE,PITFALLS}.md` — the original, milestone-specific v2.0 Omphalos Integration research (2026-07-02), recovered from git history since the working-tree copies at those paths were overwritten by an unrelated later milestone (see State of the Art)
- `.planning/PROJECT.md` Key Decisions table (lines 226-233) — confirms D-01/D-02-equivalent decisions independently logged at the milestone level
- `.planning/phases/72-platform-settings-token-contract/{72-CONTEXT.md,72-DISCUSSION-LOG.md}` — locked decisions D-01 through D-06 and their discussion rationale
- [RandomNumberGenerator.GetString Method — Microsoft Learn (net-10.0, updated 2026-07-01)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getstring?view=net-10.0) — confirmed static signature, current for .NET 10/11

### Secondary (MEDIUM confidence)
- None — all findings this session were either direct codebase reads or a single official Microsoft Learn doc fetch; no web-search-only claims were needed for this phase's scope.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages, every API confirmed either via direct `.csproj`/source inspection or a current Microsoft Learn doc fetch
- Architecture: HIGH — every pattern cited is a verified, current (post-v7.0-merge) codebase precedent, not an inference from the (partially stale) original milestone research
- Pitfalls: HIGH — grounded in direct inspection of this codebase's actual established patterns (or their explicit absence, e.g. no masked-string blank-preserve precedent existed before this phase) plus the original milestone Pitfalls research (recovered from git history), cross-checked against the current CONTEXT.md decisions for anything superseded

**Research date:** 2026-07-08
**Valid until:** 30 days (stable domain — no fast-moving external dependency; re-verify only if EF Core/ASP.NET Core version changes or if CONTEXT.md decisions are revisited)
