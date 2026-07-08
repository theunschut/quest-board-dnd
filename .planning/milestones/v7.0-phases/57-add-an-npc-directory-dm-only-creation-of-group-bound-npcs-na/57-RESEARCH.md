# Phase 57: Add an NPC directory (Contacts) - Research

**Researched:** 2026-07-06
**Domain:** ASP.NET Core 10 MVC feature addition, mirroring an existing in-house pattern (no external libraries)
**Confidence:** HIGH

## Summary

This phase adds a new "Contacts" feature by mirroring the existing Characters feature almost file-for-file: three new entities (`ContactEntity`, `ContactImageEntity`, `ContactNoteEntity`), a controller, ViewModels, 8 Razor views (4 desktop + 4 mobile), one EF Core migration, AutoMapper wiring in two profiles, two DI registrations, and two nav-link insertions. No new third-party packages are introduced — this is 100% in-house code that reuses validation attributes, image-upload logic, and query-filter conventions already present in the codebase.

**Critical correction to phase framing:** The phase description and 57-CONTEXT.md both state Phase 57 executes "before Phase 58" and that the Characters feature "still" uses `GuildMembersController`/`Views/GuildMembers/` names. **This is stale.** Git history and the live codebase confirm Phase 58 (the Guild Members -> Characters rename) has already executed and merged (commits `4416870`..`088f65f`, `.planning/STATE.md` shows `current_phase: 58`, `status: executing`... `last_activity_desc: Phase 58 complete`). `.planning/ROADMAP.md` line 221 explicitly says: *"Phases 57 and 58 executed out of numeric order — Phase 58 (Characters rename) was planned and executed before Phase 57 (NPC directory), which remains unplanned."* The controller today is `QuestBoard.Service/Controllers/Characters/CharactersController.cs`, views live in `QuestBoard.Service/Views/Characters/`, and ViewModel is `CharacterViewModel`/`CharactersIndexViewModel` — **the post-rename names already exist on disk.** The planner MUST target these current (already-renamed) names as the pattern to mirror, not `GuildMembersController`/`Views/GuildMembers/`. See `## Assumptions Log` and `## Open Questions` for how to handle this discrepancy — it does not change any of CONTEXT.md's D-01 through D-20 decisions (Contact naming, fields, visibility rules), only which file path is the mirror target.

**Primary recommendation:** Build `ContactsController` mirroring `CharactersController` exactly for CRUD/image-upload mechanics, add a session-backed per-group "Show Hidden" toggle using the exact same `HttpContext.Session.GetInt32`/`SetInt32` pattern `ActiveGroupContextService` already uses (new key `ShowHiddenContacts_{groupId}`), and give `ContactEntity`/`ContactImageEntity`/`ContactNoteEntity` the same fail-closed `HasQueryFilter` shape `CharacterEntity` uses today (no SuperAdmin cross-group escape hatch).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Contact CRUD (name/image/description/town/sub-location) | API/Backend (`ContactsController`, `ContactService`, `ContactRepository`) | Database (EF Core entities + migration) | Mirrors `CharactersController`/`CharacterService`/`CharacterRepository` three-layer split |
| Image upload validation & storage | API/Backend (`ContactViewModel` validators, controller upload handling) | Database (`ContactImageEntity` 1:1 table) | Same shape as `CharacterImageEntity` — validation attributes run server-side on the ViewModel |
| Notes list (add/edit/delete) | API/Backend (`ContactsController` note actions or nested actions) | Database (`ContactNoteEntity` child table) | Collaborative, no per-note auth check needed (D-09) — pure CRUD against a child table |
| Visibility/hide-reveal toggle button state (Show Hidden) | Frontend Server / Session (ASP.NET Core Session, SQL-backed distributed cache) | API/Backend (read in controller to filter Index query) | Reuses Phase 33's `ActiveGroupId`-style session mechanism — session state lives server-side, not client-side, because this app uses a SQL-backed distributed session store |
| Group-scoped data isolation (multi-tenancy) | Database (EF Core `HasQueryFilter`) | API/Backend (`IActiveGroupContext.ActiveGroupId`) | Same global-query-filter mechanism already governing `CharacterEntity` |
| Nav link visibility (board-type allowlist) | Frontend Server (Razor `_Layout.cshtml`/`_Layout.Mobile.cshtml`) | — | Pure Razor conditional, no new backend logic needed — same allowlist Characters/Quest Log already use |

## Standard Stack

### Core
No new external libraries. This phase is implemented entirely with the existing stack already present in the repo:

| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|---------------|
| ASP.NET Core MVC | 10 (net10.0, confirmed via `dotnet --version` 10.0.301) | Controllers/Views | Existing project target framework |
| EF Core | matches `Microsoft.EntityFrameworkCore.Tools` 9.0.6 (dotnet-ef global/local tool) `[VERIFIED: dotnet ef --version output]` | Migrations, `HasQueryFilter` | Already the project's ORM |
| AutoMapper | existing `EntityProfile`/`ViewModelProfile` pattern | Entity<->Domain<->ViewModel mapping | Already wired at both boundaries |

### Supporting
None — no new NuGet packages required.

### Alternatives Considered
Not applicable — CONTEXT.md locks the pattern-mirroring approach (D-02); no alternative libraries or architectures were evaluated because none are needed.

**Installation:** None. No `npm install` / `dotnet add package` step needed for this phase.

## Package Legitimacy Audit

**Not applicable.** This phase installs zero external packages (npm, NuGet, or otherwise) — it is a pure in-house feature addition reusing existing project dependencies. The Package Legitimacy Gate protocol is skipped; the planner does not need a `checkpoint:human-verify` task for package installation in this phase.

## Architecture Patterns

### System Architecture Diagram

```
Browser (Index/Details/Edit/Create views, + Mobile counterparts)
        |
        |  GET/POST /Contacts/*
        v
ContactsController  (QuestBoard.Service/Controllers/Contacts/)
   - [Authorize(Policy="DungeonMasterOnly")] on Create/Edit/Delete/Reveal-Hide-toggle actions
   - [Authorize] (any authenticated group member) on Index/Details/AddNote/EditNote/DeleteNote
   - reads "Show Hidden" toggle from Session (HttpContext.Session.GetInt32("ShowHiddenContacts_{groupId}"))
        |
        v
IContactService / ContactService  (QuestBoard.Domain)
   - GetAllContactsWithDetailsAsync(includeHidden, currentUserId)
   - GetContactWithDetailsAsync(id)
   - Add/Update/RemoveAsync (from IBaseService<Contact>)
   - Note-specific: AddNoteAsync / UpdateNoteAsync / DeleteNoteAsync (or expose Notes via Update path,
     mirroring CharacterRepository.UpdateAsync's child-collection reconciliation)
        |
        v
IContactRepository / ContactRepository  (QuestBoard.Repository)
   - EF Core queries against QuestBoardContext.Contacts / ContactImages / ContactNotes
   - Global query filter auto-applies GroupId scoping (fail-closed, no SuperAdmin bypass)
        |
        v
QuestBoardContext (EF Core, SQL Server)
   - Contacts table (ContactEntity)
   - ContactImages table (ContactImageEntity, 1:1 FK-as-PK)
   - ContactNotes table (ContactNoteEntity, many-to-one FK to Contact, FK to authoring UserEntity)
```

Session state for the "Show Hidden" toggle flows entirely through ASP.NET Core's SQL-backed distributed session (same infrastructure `ActiveGroupId` already uses) — it never touches the database's Contact tables directly; it is read by the controller at request time to decide whether to include hidden Contacts in the Index query result.

### Recommended Project Structure
```
QuestBoard.Repository/
├── Entities/
│   ├── ContactEntity.cs            # mirrors CharacterEntity
│   ├── ContactImageEntity.cs       # mirrors CharacterImageEntity (1:1 FK-as-PK)
│   └── ContactNoteEntity.cs        # new shape — see "Notes entity design" below
├── ContactRepository.cs            # mirrors CharacterRepository.cs
QuestBoard.Domain/
├── Models/
│   └── Contact.cs                  # domain model + ContactNote nested class, mirrors Character.cs
├── Interfaces/
│   ├── IContactRepository.cs
│   └── IContactService.cs
├── Services/
│   └── ContactService.cs
QuestBoard.Service/
├── Controllers/Contacts/
│   └── ContactsController.cs
├── ViewModels/ContactViewModels/
│   └── ContactViewModel.cs         # + ContactsIndexViewModel, ContactNoteViewModel
├── Views/Contacts/
│   ├── Index.cshtml / Index.Mobile.cshtml
│   ├── Details.cshtml / Details.Mobile.cshtml
│   ├── Edit.cshtml / Edit.Mobile.cshtml
│   └── Create.cshtml / Create.Mobile.cshtml
```

### Pattern 1: Group-scoped fail-closed query filter (mandatory, no shortcuts)
**What:** Every new entity gets a `HasQueryFilter` in `QuestBoardContext.OnModelCreating()` that returns zero rows when `ActiveGroupId` is null — never merges cross-group data.
**When to use:** All three new entities (`ContactEntity` directly on `GroupId`; `ContactImageEntity`/`ContactNoteEntity` scoped through the required `Contact` navigation, exactly like `CharacterImageEntity`/`CharacterClassEntity` today).
**Example:**
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs (lines 301-318, existing CharacterEntity/CharacterImageEntity/CharacterClassEntity filters)
modelBuilder.Entity<ContactEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

modelBuilder.Entity<ContactImageEntity>()
    .HasQueryFilter(ci =>
        activeGroupContext.ActiveGroupId != null &&
        ci.Contact.GroupId == activeGroupContext.ActiveGroupId);

modelBuilder.Entity<ContactNoteEntity>()
    .HasQueryFilter(cn =>
        activeGroupContext.ActiveGroupId != null &&
        cn.Contact.GroupId == activeGroupContext.ActiveGroupId);
```
**CRITICAL:** Do NOT capture `activeGroupContext.ActiveGroupId` into a local variable before the lambda — the existing code comment (`QuestBoardContext.cs` lines 251-252) explicitly warns this bakes in a stale null at model-build time. Always reference the service instance inside the lambda, exactly as every existing filter does. Also do NOT give Contacts a SuperAdmin cross-group bypass like `QuestEntity`/`ShopItemEntity` have — `CharacterEntity`'s comment (lines 297-300) explicitly says this omission is intentional for roster-style, per-group content, and Contacts is the same shape.

### Pattern 2: Session-based per-group toggle (D-15b)
**What:** A boolean stored in ASP.NET Core Session, keyed per group, reusing the exact mechanism `ActiveGroupContextService` uses for `ActiveGroupId`.
**When to use:** The "Show Hidden" toggle button on the Contacts Index page (DM-tier viewers only).
**Example:**
```csharp
// Source: QuestBoard.Service/Constants/SessionKeys.cs + QuestBoard.Service/Services/ActiveGroupContextService.cs (existing ActiveGroupId pattern)
// New constant to add to SessionKeys.cs:
public static string ShowHiddenContactsKey(int groupId) => $"ShowHiddenContacts_{groupId}";

// Reading in the controller (mirrors HttpContext.Session.GetInt32 usage in ActiveGroupContextService):
var groupId = activeGroupContext.RequireActiveGroupId();
var showHidden = HttpContext.Session.GetInt32(SessionKeys.ShowHiddenContactsKey(groupId)) == 1;

// Writing (toggle POST action):
HttpContext.Session.SetInt32(SessionKeys.ShowHiddenContactsKey(groupId), showHidden ? 1 : 0);
```
`Session.GetInt32`/`SetInt32` is the correct API — ASP.NET Core's `ISession` has no native `GetBoolean`/`SetBoolean`; the existing codebase already stores `ActiveGroupId` as an `int?` via this exact same extension method pair, so storing the toggle as `0`/`1` int keeps the pattern 1:1 rather than introducing a new session-serialization convention (`Session.GetString`/manual bool-parsing) that nothing else in the app uses.

This project's session store is SQL-backed and distributed (confirmed by `ActiveGroupContextService`'s own XML doc comment: "In Hangfire background threads (no HttpContext), returns null" — implying a distributed session provider is configured, consistent with CONTEXT.md's description of "Phase 33's SQL-backed distributed-session mechanism"). Session expiry naturally resets ALL group keys to unset (== null, read as `false`/hidden), satisfying D-15b's "resets to safe default on session expiry or fresh login" requirement with zero extra code — this falls out of using Session directly rather than a custom store.

### Pattern 3: Notes entity design (authored + timestamped child collection)
**What:** No existing "freeform authored note list" entity exists in this codebase to mirror directly. `PlayerDateVoteEntity` was suggested in the phase brief as an analog but is a poor fit — it has no author FK, no timestamp, no ordering, and represents a single-value vote, not free text. The best structural analog is actually **`CharacterClassEntity`** (a simple `HasMany`/`WithOne` child collection scoped by parent FK, reconciled by Id on update) combined with **`QuestEntity`'s `DungeonMaster` FK-to-`UserEntity` pattern** for the author reference.
**When to use:** `ContactNoteEntity` design.
**Example:**
```csharp
// New entity — no direct precedent, composed from two existing patterns:
// (1) CharacterClassEntity's parent-FK child-collection shape
// (2) QuestEntity.DungeonMasterId's FK-to-UserEntity shape
[Table("ContactNotes")]
public class ContactNoteEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int ContactId { get; set; }

    [ForeignKey(nameof(ContactId))]
    public virtual ContactEntity Contact { get; set; } = null!;

    [Required]
    [StringLength(2000)]
    public string Text { get; set; } = string.Empty;

    [Required]
    public int AuthorUserId { get; set; }

    [ForeignKey(nameof(AuthorUserId))]
    public virtual UserEntity Author { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Only set if the note has been edited since creation — lets the Details view
    // optionally show "edited" without a full audit trail (Claude's Discretion per CONTEXT.md).
    public DateTime? UpdatedAt { get; set; }
}
```
`OnModelCreating` relationship registration (mirrors `CharacterEntity.Classes` at `QuestBoardContext.cs:121-125`, and the `NoAction` delete behavior QuestEntity uses for its `DungeonMasterId` FK to avoid cascade-cycle errors on `UserEntity`):
```csharp
modelBuilder.Entity<ContactEntity>()
    .HasMany(c => c.Notes)
    .WithOne(n => n.Contact)
    .HasForeignKey(n => n.ContactId)
    .OnDelete(DeleteBehavior.Cascade); // deleting a Contact deletes its notes — same as Classes

modelBuilder.Entity<ContactNoteEntity>()
    .HasOne(n => n.Author)
    .WithMany()
    .HasForeignKey(n => n.AuthorUserId)
    .OnDelete(DeleteBehavior.NoAction); // avoid cascade cycle through UserEntity, same as QuestEntity.DungeonMaster
```
**Note ordering (D-10, newest first):** implement via `OrderByDescending(n => n.CreatedAt)` in the repository query — do not rely on Id ordering, since `CreatedAt` is the semantically correct sort key even though in practice Id and CreatedAt will almost always agree for an append-only table.

**Update/delete-by-anyone (D-09):** Since any group member can edit/delete any note with no ownership check, the note edit/delete actions need only `[Authorize]` (any authenticated user in the group) — no `CanManageX`-style guard like Characters' Edit/Delete/ToggleRetirement have. This is materially simpler than the Character mirror and the planner should not accidentally copy `CanManageCharacterAsync`'s owner-or-admin check onto note actions.

### Anti-Patterns to Avoid
- **Copying `CanManageCharacterAsync`'s owner-or-admin guard onto Contact or Note actions:** Contact's own CRUD is a flat `DungeonMasterOnly` policy gate (D-09b) — there is no "owner" concept for a Contact (D-07 explicitly says `CreatedByUserId` carries no edit-restriction meaning). Do not build an ownership check that doesn't exist in the requirements.
- **Persisting the "Show Hidden" toggle to a database table:** D-15b explicitly rejects this ("A durable (never-expiring) DB-persisted preference was explicitly considered and rejected for reintroducing that exact spoiler risk"). Session-only, full stop.
- **Capturing `activeGroupContext.ActiveGroupId` in a local variable inside `OnModelCreating`:** breaks the lambda's re-evaluation per query (see Pattern 1 above); this is an existing documented pitfall in the codebase itself, not a hypothetical.
- **Giving `ContactEntity` a SuperAdmin cross-group query-filter bypass:** `CharacterEntity`'s own filter deliberately omits this (unlike `QuestEntity`/`ShopItemEntity`), and Contacts is the same "per-group roster" shape, not the "cross-group visibility for platform admins" shape.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Image upload MIME/size validation | New custom validation logic | `MaxFileSizeAttribute` / `AllowedExtensionsAttribute` from `CharacterViewModel.cs` — reuse verbatim (or move to a shared location if the planner wants to avoid duplication, but functionally identical) | Already handles the exact JPG/PNG/GIF, 5MB constraints D-06 specifies |
| Image MIME sniffing on read | New byte-sniffing logic | `CharactersController.DetectImageMimeType` (private static, byte-signature check for PNG/GIF/else-JPEG) — copy into `ContactsController` | Proven, minimal, exactly matches D-06's mirrored image behavior |
| Per-group session state | Custom cookie or claims-based storage | `HttpContext.Session.GetInt32`/`SetInt32`, same as `ActiveGroupContextService` | This project already has a working SQL-backed distributed session; introducing a second mechanism (e.g. a cookie) would fragment session semantics and break the "resets on session expiry" guarantee D-15b relies on |
| Group-scoped multi-tenancy filtering | Manual `.Where(x => x.GroupId == ...)` sprinkled through repository methods | EF Core global `HasQueryFilter` in `OnModelCreating` | Applies automatically to every LINQ query without per-method boilerplate, and is the established, hardened (Phase 55) pattern in this codebase |

**Key insight:** Every piece of this phase has a working precedent already in the codebase. The only genuinely novel piece is the Notes child-collection shape (no exact analog exists) and the per-group session-keyed toggle (analog exists for a single global key, `ActiveGroupId`, but not yet for a per-group-keyed one) — everything else is direct duplication of the Character pattern with renamed identifiers.

## Runtime State Inventory

**Not applicable — this is a greenfield feature addition, not a rename/refactor/migration phase.** No existing runtime state (stored data, live service config, OS-registered state, secrets, build artifacts) references "Contact"/"NPC" anywhere in this codebase today, since the feature does not yet exist. Skipping this section per the greenfield exemption.

## Common Pitfalls

### Pitfall 1: Treating `DungeonMasterOnly` policy resolution as identical to `GetEffectiveGroupRoleAsync`
**What goes wrong:** `DungeonMasterHandler` (used by `[Authorize(Policy = "DungeonMasterOnly")]`) checks `context.User.IsInRole("SuperAdmin")` directly and succeeds even with **no active group selected** (`ActiveGroupId is not { } groupId` -> fails only for non-SuperAdmin, non-DM/Admin roles). This is a *different* code path from `GetEffectiveGroupRoleAsync(User, groupId)`, which is used in `CharactersController.Details`/`Edit`/etc. to compute `CanEdit`/decide button visibility, and which requires an active group to resolve any role at all (SuperAdmin included, mapped to `GroupRole.Admin`).
**Why it happens:** Two independent "is this user DM-tier" checks exist in the codebase for different purposes (attribute-based route gating vs. view-model flag computation), and they diverge slightly in the null-group edge case.
**How to avoid:** For the Contact controller's create/edit/delete actions, `[Authorize(Policy = "DungeonMasterOnly")]` is sufficient and matches D-09b exactly — no additional in-action role check needed. For the "Show Hidden" toggle visibility (D-15.2 — only DM-tier viewers see the toggle) and the "creator always sees own hidden" exception (D-15.1), the controller/view needs `GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId())` (same call `CharactersController.Details` already makes) to know the *viewing* user's role, since the policy attribute only gates the action, it doesn't expose the resolved role to the view.
**Warning signs:** A view that renders differently for DM vs Player but only checks `User.IsInRole(...)` directly, ignoring the per-group role resolution SuperAdmin-as-Admin mapping.

### Pitfall 2: The D-15 "creator + toggle" visibility rule is NOT a single filter expression
**What goes wrong:** Attempting to express all of D-15's precedence rules (creator always sees own hidden; DM-tier sees hidden only with toggle ON; Players never see hidden) as one `HasQueryFilter` lambda. EF Core global query filters cannot reference request-scoped mutable state like "is the Show Hidden toggle currently on" cleanly alongside "is this the creator" without either (a) injecting far more context into the DbContext constructor than `IActiveGroupContext` currently provides, or (b) making the query filter so complex it becomes untestable and couples an EF filter to a Session read.
**Why it happens:** The temptation is to treat this like the existing `ActiveGroupId` query filter (which IS injected via DI into `OnModelCreating` and works well for a single, simple boolean condition). But this visibility rule has 3 independent branches with different scopes (per-request current-user-is-creator; per-request current-user-role; per-group session toggle), not one global constant.
**How to avoid:** Do NOT add `IsRevealed`-aware logic to a `HasQueryFilter`. Instead:
1. Keep the query filter scoped to `GroupId` only (Pattern 1 above) — that's the multi-tenancy boundary, always enforced.
2. Apply the visibility rule as an explicit `.Where(...)` clause (or post-fetch filter) in `ContactRepository.GetAllContactsWithDetailsAsync(currentUserId, viewerRole, showHiddenToggle)`, passed in as parameters from the controller/service layer — NOT derived inside the repository from ambient session state.
3. For `Details/{id}` (D-13's 404 requirement), apply the same three-branch check explicitly in the controller after fetching the Contact, returning `NotFound()` if the viewer fails all three visibility branches — mirroring the existing `Edit`/`Delete` `CanManageCharacterAsync`-style guard shape (a small private helper method), not a query filter.
**Warning signs:** A repository method signature that doesn't take role/toggle/currentUserId as explicit parameters, but instead reads `IActiveGroupContext` or `HttpContext.Session` directly inside `QuestBoard.Repository` — this would break the project's Service -> Domain -> Repository dependency direction (Repository has no business reading Session directly; only `QuestBoard.Service` should touch `HttpContext.Session`).

### Pitfall 3: `RequireActiveGroupId()` vs nullable `ActiveGroupId` in the toggle read/write path
**What goes wrong:** The toggle's session key is scoped by `groupId` (`ShowHiddenContacts_{groupId}`). If the controller action reads/writes this key using a nullable `activeGroupContext.ActiveGroupId` without a null guard, a session with no active group selected either throws or silently uses a `null`-derived key string (`ShowHiddenContacts_`), which would incorrectly collide across all "no active group" states.
**How to avoid:** Use `activeGroupContext.RequireActiveGroupId()` (already used elsewhere in `CharactersController`, e.g. line 154) for the toggle key, exactly as done for tagging a new Contact's `GroupId` on Create.
**Warning signs:** Any code path where the toggle key is built before confirming an active group exists.

### Pitfall 4: AutoMapper reverse-mapping the Notes collection incorrectly on Update
**What goes wrong:** `CharacterRepository.UpdateAsync` (lines 73-127) contains a deliberately hand-rolled reconciliation loop for `Classes` because "AutoMapper's default mapping replaces child navigations... with brand-new CLR instances rather than mutating the entities EF is already tracking," which the change tracker then treats as detached and can throw concurrency exceptions on save (especially under the InMemory test provider). If `ContactRepository.UpdateAsync` naively does `Mapper.Map(model, entity)` without the same by-Id reconciliation for `Notes`, adding/editing/deleting notes via a straight AutoMapper pass risks the exact same failure class this comment documents.
**Why it happens:** It's the natural, simpler-looking approach, and it works for scalar fields — it just silently breaks for child collections.
**How to avoid:** Either (a) don't route note add/edit/delete through the generic `UpdateAsync` path at all — give `IContactRepository`/`IContactService` dedicated `AddNoteAsync`/`UpdateNoteAsync`/`DeleteNoteAsync` methods that directly manipulate the `ContactNotes` DbSet (simpler, and matches D-16's "notes UI lives on Details page" as a logically separate action from editing the Contact's core fields on the Edit page), or (b) if routed through `UpdateAsync`, copy `CharacterRepository`'s exact by-Id reconciliation pattern for `Notes`. **Option (a) is recommended** — since Edit (D-20) explicitly does not include notes editing, there's no reason to fold Notes into the same `UpdateAsync` call that handles Name/Image/Description/TownCity/SubLocation.
**Warning signs:** A single `ContactViewModel` with both core-field properties and a `Notes` collection submitted together from one form — this doesn't match D-20's UI split (Edit = core fields only; Details = notes UI) and risks exactly the AutoMapper collection-replacement issue.

## Code Examples

### EF Core migration invocation (verified against actual recent migration history, not the stale `create-migration.sh`)
```bash
# Source: recent migration file timestamps (20260705183646_AddGroupIdToCharacters.cs, etc.)
# and CLAUDE.md's documented convention — run from QuestBoard.Service/ (not the stale
# create-migration.sh, which still references a WSL path /mnt/c/Repos/quest-board that
# predates this project's Windows-native dev setup and only creates the *initial* migration).
cd QuestBoard.Service
dotnet ef migrations add AddContactsFeature --project ../QuestBoard.Repository --context QuestBoardContext
```
No manual `dotnet ef database update` is needed — `ConfigureDatabase()` in `QuestBoard.Repository/Extensions/ServiceExtensions.cs` calls `context.Database.Migrate()` on startup (confirmed, matches CLAUDE.md).

### DI registration (exact insertion points)
```csharp
// Source: QuestBoard.Domain/Extensions/ServiceExtensions.cs (line 20, existing Character line)
services.AddScoped<IContactService, ContactService>();

// Source: QuestBoard.Repository/Extensions/ServiceExtensions.cs (line 24, existing Character line)
services.AddScoped<IContactRepository, ContactRepository>();
```

### Nav integration (exact insertion points, both files)
```razor
@* Source: QuestBoard.Service/Views/Shared/_Layout.cshtml, lines 125-134 *@
@* Desktop nav — insert immediately after the existing Characters <li>, before the OneShot-only Players block *@
<li class="nav-item">
    <a class="nav-link" asp-controller="Contacts" asp-action="Index">
        <i class="fas fa-address-book me-1"></i>Contacts
    </a>
</li>

@* Source: QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml, lines 112-116 *@
@* Mobile nav — insert immediately after the existing Characters <li>, before the OneShot-only block *@
<li class="nav-item">
    <a class="nav-link" asp-controller="Contacts" asp-action="Index">
        <i class="fas fa-address-book me-2"></i>Contacts
    </a>
</li>
```
Both insertion points sit inside the unconditional "available to all authenticated users" block (not wrapped in `@if (activeBoardType == BoardType.OneShot)`), matching D-19's "visible for both board types" requirement — the exact same placement Characters/Quest Log already use.

### Mobile view file naming (confirmed mechanism)
```csharp
// Source: QuestBoard.Service/ViewExpanders/MobileViewLocationExpander.cs
// When context.Items["IsMobile"] is true, the expander tries "{ViewName}.Mobile.cshtml" FIRST,
// falling back to "{ViewName}.cshtml" if the mobile variant doesn't exist.
// No controller-side branching needed — just create the 4 files with the ".Mobile.cshtml" suffix
// in the same Views/Contacts/ folder as the desktop views:
//   Views/Contacts/Index.Mobile.cshtml
//   Views/Contacts/Details.Mobile.cshtml
//   Views/Contacts/Edit.Mobile.cshtml
//   Views/Contacts/Create.Mobile.cshtml
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `GuildMembersController` / `Views/GuildMembers/` (what CONTEXT.md and the phase description assume is still current) | `CharactersController` / `Views/Characters/` | Phase 58, completed 2026-07-06 (same day as this research, before it) | The planner must target the ALREADY-RENAMED files as the mirror source. There is no `GuildMembersController.cs` on disk to read. |

**Deprecated/outdated:** `create-migration.sh` — references a stale WSL path and only creates the very first migration; not a template for adding a new migration today. Use the direct `dotnet ef migrations add <Name> --project ../QuestBoard.Repository --context QuestBoardContext` invocation shown above instead.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | `ContactNoteEntity`'s exact property names (`Text`, `AuthorUserId`, `CreatedAt`, `UpdatedAt`) are proposed, not mandated by CONTEXT.md (which explicitly leaves this to Claude's Discretion) | Pattern 3 / Notes entity design | Low — CONTEXT.md explicitly defers exact naming; planner/executor can rename freely as long as the shape (author FK, timestamp, freeform text, `StringLength(2000)`) matches D-08/D-09/D-10/D-11 |
| A2 | The distributed session store is confirmed SQL-backed via inference from `ActiveGroupContextService`'s doc comment and CONTEXT.md's own description of "Phase 33's SQL-backed distributed-session mechanism" — the actual `AddDistributedSqlServerCache`/`AddSession` registration in `Program.cs` was not directly read in this research pass | Pattern 2 / Session toggle | Low — even if the session provider details differ, `HttpContext.Session.GetInt32`/`SetInt32` is provider-agnostic; the planner does not need to touch session provider configuration, only use the existing `ISession` API |
| A3 | Recommending dedicated `AddNoteAsync`/`UpdateNoteAsync`/`DeleteNoteAsync` repository/service methods rather than folding Notes into the generic `UpdateAsync` — this is a design recommendation based on avoiding Pitfall 4, not a decision CONTEXT.md locked in | Pitfall 4 / Code Examples | Low-medium — if the planner instead chooses to fold Notes into `UpdateAsync`, they MUST replicate `CharacterRepository`'s by-Id reconciliation loop for `Classes` (lines 90-124) applied to `Notes`, or risk EF change-tracker exceptions under the InMemory test provider used by integration tests |

## Open Questions

1. **Should 57-CONTEXT.md itself be corrected/annotated before planning proceeds?**
   - What we know: CONTEXT.md's `<domain>` section explicitly states "This phase executes before Phase 58... should be built against the Characters feature's *current* file/class names (`GuildMembersController`, `Views/GuildMembers/`, `CharacterEntity`, etc.)" — this is factually false as of this research session; Phase 58 has already shipped.
   - What's unclear: Whether the planner should silently substitute the correct (already-renamed) file paths (as this research does) or whether the discrepancy should be flagged back to the user before planning proceeds, since it reflects a stale assumption baked into a "locked" document.
   - Recommendation: None of CONTEXT.md's D-01 through D-20 decisions are actually affected by this — they're all about the Contact feature's own shape, not about which files to read. The planner should proceed using this RESEARCH.md's corrected file pointers (`CharactersController.cs`, `Views/Characters/`, etc.) and note the correction in the plan's assumptions rather than blocking on it. `CharacterEntity`/`CharacterImageEntity` names were correctly identified in CONTEXT.md's `<code_context>` section regardless (those were never renamed — only the Service-layer controller/views/ViewModels were, per Phase 58's scope) and remain accurate.

2. **Exact `Program.cs` session/distributed-cache configuration**
   - What we know: `ActiveGroupContextService` and CONTEXT.md both describe the session mechanism as SQL-backed and distributed (Phase 33 precedent).
   - What's unclear: This research pass did not directly open `Program.cs` to confirm the exact `AddDistributedSqlServerCache`/`AddSession` call and options (e.g. `IdleTimeout`), since it is not required to implement the toggle (the `ISession` API surface used is identical regardless of the underlying store).
   - Recommendation: Not blocking — the planner can proceed with `HttpContext.Session.GetInt32`/`SetInt32` calls as shown; if session `IdleTimeout` behavior needs to be verified end-to-end (e.g. confirming the toggle truly resets on the same schedule as `ActiveGroupId`), that's a verification-phase concern, not a planning blocker.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|--------------|-----------|---------|----------|
| .NET SDK | Build/run | Yes | 10.0.301 `[VERIFIED: dotnet --version]` | — |
| dotnet-ef tool | Migration generation | Yes | 9.0.6 `[VERIFIED: dotnet ef --version]` | — |
| SQL Server | Runtime persistence, session store | Assumed available per CLAUDE.md ("SQL Server runs on the Windows host... use localhost") | — | Not verified this session (no live DB connection check performed); not a phase blocker since migrations apply on next app startup regardless |

**Missing dependencies with no fallback:** None identified.
**Missing dependencies with fallback:** None — all required tooling confirmed present.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (`QuestBoard.UnitTests`, `QuestBoard.IntegrationTests` projects) |
| Config file | Standard `.csproj`-based xUnit setup — no separate config file |
| Quick run command | `dotnet test QuestBoard.UnitTests` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

This phase has no `REQUIREMENTS.md` mapping (ad-hoc backlog phase); the table below maps CONTEXT.md decision IDs to test coverage instead.

| Decision ID | Behavior | Test Type | Automated Command | File Exists? |
|-------------|----------|-----------|---------------------|--------------|
| D-09b | Create/Edit/Delete gated by `DungeonMasterOnly` policy | integration | `dotnet test --filter FullyQualifiedName~ContactsControllerIntegrationTests` | Wave 0 |
| D-12/D-13 | Hidden Contact 404s for unauthorized viewers | integration | same as above | Wave 0 |
| D-14 | New Contact defaults `IsRevealed = false` | unit | `dotnet test --filter FullyQualifiedName~ContactServiceTests` or `ContactRepositoryTests` | Wave 0 |
| D-15 | Creator-always-sees-own-hidden + toggle precedence (3 branches) | integration | same integration suite, multiple `[Fact]`s per branch (mirrors `Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed`-style role-matrix tests already in `CharactersControllerIntegrationTests.cs`) | Wave 0 |
| D-15b | Toggle is per-group, session-scoped, resets on session expiry | integration (may require a session-reset simulation, similar in spirit to Phase 55's session-expiry regression test) | same integration suite | Wave 0 |
| D-09 | Any group member can edit/delete any note | integration | same integration suite | Wave 0 |
| D-10 | Notes display newest first | unit or integration | `ContactRepositoryTests` (mirrors `CharacterRepositoryTests.cs` ordering assertions) | Wave 0 |
| D-06 | Image upload validation (type/size) | unit | reuse existing `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` — no new test needed if attributes are reused verbatim, but a controller-level integration test for reject-on-bad-type/size mirrors Character's existing coverage | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests`
- **Per wave merge:** `dotnet test` (full suite, including `QuestBoard.IntegrationTests`)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` — mirrors `CharacterRepositoryTests.cs`; covers ordering, group-scoping, image-profile round-trip
- [ ] `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` — mirrors `CharactersControllerIntegrationTests.cs`; covers the D-09b/D-12/D-13/D-15/D-15b role-and-visibility matrix (this is the highest-value test file in the phase, given the 3-branch visibility logic in Pitfall 2)
- No new test framework or fixture install needed — `WebApplicationFactoryBase` (used by all existing controller integration tests) already provides the harness this phase's controller tests need.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | No | Unchanged — relies on existing ASP.NET Core Identity, no new auth surface |
| V3 Session Management | Yes | New session key (`ShowHiddenContacts_{groupId}`) added to existing SQL-backed distributed session — must follow the same key-scoping convention as `ActiveGroupId` (`SessionKeys.cs` constants class), not a raw string literal scattered across the controller |
| V4 Access Control | Yes | `[Authorize(Policy = "DungeonMasterOnly")]` on Create/Edit/Delete/Reveal-toggle actions (existing policy, existing handler — no new authorization code beyond attribute placement); explicit controller-level visibility check (not a query filter — see Pitfall 2) for the 3-branch hidden-Contact rule |
| V5 Input Validation | Yes | `[StringLength]` on `Description`/`SubLocation`/`TownCity`/note `Text`; `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` reused verbatim for image upload |
| V6 Cryptography | No | No new cryptographic surface — image bytes stored as `byte[]` exactly as `CharacterImageEntity` already does, no encryption at this layer (matches existing precedent, not a new risk) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|------------------------|
| Cross-tenant data leak (viewing another group's Contacts) | Information Disclosure | Fail-closed `HasQueryFilter` on `GroupId` (Pattern 1) — same mitigation that closed Phase 55's cross-tenant quest leak |
| IDOR on `Details/{id}` (guessing another group's Contact id) | Information Disclosure | Query filter returns null for cross-group ids -> controller returns `NotFound()`, same convention Phase 49/55 established for Characters |
| Hidden-Contact spoiler leak via role-based access alone (the exact scenario D-15 was designed to prevent) | Information Disclosure | Explicit 3-branch visibility check in the controller (creator exception, DM-tier toggle, Player never) rather than relying solely on `DungeonMasterOnly` policy, which would otherwise let a DM-tier user who is "supposed to be surprised" see spoilers just by virtue of role |
| Unrestricted file upload (arbitrary file type/size as Contact image) | Tampering / Denial of Service | `AllowedExtensionsAttribute` (JPG/PNG/GIF) + `MaxFileSizeAttribute` (5MB) + `DetectImageMimeType` byte-sniffing on read — all three layers reused verbatim from Character |
| CSRF on note add/edit/delete or toggle actions | Tampering | `[ValidateAntiForgeryToken]` on all `[HttpPost]` actions, exactly as every existing `CharactersController` POST action already does |

## Sources

### Primary (HIGH confidence)
- `QuestBoard.Service/Controllers/Characters/CharactersController.cs` — full controller pattern, read in full
- `QuestBoard.Repository/Entities/CharacterEntity.cs`, `CharacterImageEntity.cs`, `PlayerDateVoteEntity.cs` — entity shapes, read in full
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — all `HasQueryFilter` registrations and their surrounding rationale comments, read in full
- `QuestBoard.Service/Constants/SessionKeys.cs`, `QuestBoard.Service/Services/ActiveGroupContextService.cs` — session mechanism, read in full
- `QuestBoard.Repository/Automapper/EntityProfile.cs`, `QuestBoard.Service/Automapper/ViewModelProfile.cs` — AutoMapper wiring, read in full
- `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` — policy resolution mechanics, read in full
- `QuestBoard.Domain/Services/UserService.cs` (`GetEffectiveGroupRoleAsync`) — role resolution used by Details/Edit
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` — validation attributes, read in full
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs`, `QuestBoard.Repository/Extensions/ServiceExtensions.cs` — DI registration, read in full
- `QuestBoard.Service/Views/Shared/_Layout.cshtml`, `_Layout.Mobile.cshtml` — exact nav line numbers, read in full
- `QuestBoard.Service/ViewExpanders/MobileViewLocationExpander.cs` — mobile view resolution mechanism, read in full
- `QuestBoard.Repository/CharacterRepository.cs` — including the `UpdateAsync` child-collection reconciliation pattern (Pitfall 4 source), read in full
- `.planning/ROADMAP.md` line 221 — authoritative confirmation that Phase 58 executed before Phase 57
- `.planning/STATE.md` — confirms `current_phase: 58`, `status: executing`, Phase 58 complete
- `git log` on the working tree — confirms Phase 58 commits already merged
- `dotnet --version` / `dotnet ef --version` — tool versions confirmed via direct execution

### Secondary (MEDIUM confidence)
- None used — all findings this session came from direct codebase reads or verified tool output, no external web search was needed since this phase introduces no new external technology.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new libraries; existing stack directly confirmed via `dotnet --version`/`dotnet ef --version`
- Architecture: HIGH - every pattern cited is read directly from the live codebase, not inferred
- Pitfalls: HIGH - Pitfall 1/2/3 derived from actual divergences observed between `DungeonMasterHandler` and `GetEffectiveGroupRoleAsync`; Pitfall 4 is a documented comment already in `CharacterRepository.cs`

**Research date:** 2026-07-06
**Valid until:** 30 days (stable in-house pattern, no external dependency drift risk) — but re-verify file paths immediately if any further renames land before this phase is planned/executed, since this research already had to correct one stale assumption from CONTEXT.md
