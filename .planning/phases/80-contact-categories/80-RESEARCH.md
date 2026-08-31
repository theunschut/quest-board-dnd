# Phase 80: Contact Categories - Research

**Researched:** 2026-08-30
**Domain:** EF Core 10 / SQL Server tenant-scoped grouping feature on an existing ASP.NET Core 10 MVC entity
**Confidence:** HIGH

## Summary

Phase 80 adds one new entity (`ContactCategory`), one nullable FK on the existing `ContactEntity`,
a DM-only management page, and a grouped rendering pass on two already-existing index views. Every
piece of this phase has a direct, literal precedent already living in this codebase — there is no
unfamiliar EF Core or SQL Server mechanism here. The four riskiest mechanics (fail-closed query
filter shape, `OnDelete(SetNull)` on a nullable FK, case-insensitive unique index, and mobile view
selection) are all things this codebase already does elsewhere for a different entity, so this
research is almost entirely "here is the exact block to copy," not "here is a new pattern to learn."

The single most load-bearing finding is a **conflict between CONTEXT.md and the actual code**:
D-16 states "the field is already on the mapped `ContactViewModel`" — it is not. Neither
`Contact` (domain), `ContactEntity`, nor `ContactViewModel` has any category-shaped field today;
`Categor` does not appear anywhere in the Contacts codebase (`git grep` returned zero hits). This
does not change what D-16 asks for (add the category to Details), but the planner must not skip a
task believing the field already exists.

A second, lower-stakes correction: D-02/Discretion cites `ShopManagement` and
`ShopViewModels/ShopCategoryViewModel.cs` as an "already uses" precedent. `ShopManagementController`
and its Mobile-paired views are real and in active use — good precedent. `ShopCategoryViewModel`
itself, however, is dead code: it is declared but referenced nowhere in `QuestBoard.Service` (no
controller constructs it, no view binds to it). The `{ Title, Items }` shape is still the right one
to copy, but it is a shape precedent only, not a battle-tested rendering pattern already proven in
production.

**Primary recommendation:** Copy the `ContactEntity` query filter, the `Contact → Group: NoAction`
FK configuration, and the `GroupController` `DbUpdateException` catch pattern verbatim (see Code
Examples). Do not invent new shapes for any of these three — this codebase already answers exactly
what D-02/D-03/D-04 ask for, several times over, for adjacent entities.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Category CRUD (create/rename/delete/reorder) | API / Backend (`ShopManagementController`-style MVC controller) | Database (unique index, FK constraint) | DM-only write; SQL Server enforces the uniqueness and orphan-on-delete guarantees the app must not re-implement in code |
| Category → Contact assignment | API / Backend (existing Contacts Create/Edit POST) | — | Reuses the existing form POST + ModelState validation path; no new endpoint |
| Grouped index rendering | Frontend Server (SSR Razor views) | API / Backend (`ContactsController.Index` groups after filtering) | Grouping is a display concern computed once in the controller and consumed by two Razor views; the visibility gate (`IsVisibleTo`) is the security boundary and MUST run in the API/backend tier, never in the view |
| Cross-group isolation | Database (EF Core global query filter) | API / Backend (never calls `IgnoreQueryFilters()`) | The query filter is the single point of enforcement; two prior real leaks (Phases 49/55) both trace back to a path that bypassed or never reached this layer |
| Mobile vs desktop view selection | Frontend Server (`MobileViewLocationExpander` + `MobileDetectionMiddleware`) | — | Server-side, request-scoped, User-Agent-driven — no client JS or CDN involvement |

## Standard Stack

### Core

No new libraries. This phase is pure additive EF Core modeling + MVC on the existing stack.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 `[VERIFIED: QuestBoard.Repository.csproj]` | ORM, migrations, query filters, FK behavior | Already the project's ORM; confirmed via direct file read, not assumed |
| ASP.NET Core MVC | net10.0 `[VERIFIED: QuestBoard.Repository.csproj TargetFramework]` | Controllers, Razor views, `IViewLocationExpander` | Existing framework; no version-specific behavior change affects this phase |

### Supporting

Nothing new. AutoMapper (already a dependency) needs two new `CreateMap` entries; no package change.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Plain unique index for case-insensitive uniqueness (D-04) | A computed lower-cased shadow column + unique index on that | Unnecessary here — see "Case-insensitive unique index" finding below. Would add a migration-time backfill step and a second column with zero benefit given the DB's default collation |
| `OnDelete(DeleteBehavior.SetNull)` (D-03) | Application-level "load every child, null the FK, save" before delete | Redundant — SQL Server enforces `ON DELETE SET NULL` at the constraint level once the FK is configured correctly; no dependents need to be loaded into the change tracker first |

**Installation:** None. No `npm install` / `dotnet add package` needed for this phase.

**Version verification:** EF Core version confirmed directly from `QuestBoard.Repository/QuestBoard.Repository.csproj` (`Microsoft.EntityFrameworkCore` / `Microsoft.EntityFrameworkCore.SqlServer` both `10.0.9`) — read directly, not queried from a registry, so this is `[VERIFIED: QuestBoard.Repository.csproj]` rather than a registry check.

## Package Legitimacy Audit

**Not applicable.** This phase introduces zero new NuGet packages, zero new npm packages, and zero
new external dependencies of any kind. `ContactCategoryEntity`, its repository/service, its
controller, and its views are all built from types and packages already present in the project.
No legitimacy check is required.

## Architecture Patterns

### System Architecture Diagram

```
Request (GET /Contacts/Index)
        |
        v
MobileDetectionMiddleware --sets context.Items["IsMobile"]--> MobileViewLocationExpander
        |                                                          (selects *.Mobile.cshtml
        |                                                           vs *.cshtml at view-resolve time)
        v
ContactsController.Index
        |
        |-- contactService.GetAllContactsWithDetailsAsync()
        |         |
        |         v
        |   ContactRepository --Include(Category)--> QuestBoardContext
        |                                                  |
        |                                                  |-- ContactEntity.HasQueryFilter (GroupId == ActiveGroupId)
        |                                                  '-- ContactCategoryEntity.HasQueryFilter (GroupId == ActiveGroupId)
        |                                                       [both fail-closed: null ActiveGroupId => zero rows]
        |
        |-- IsVisibleTo(contact, currentUserId, includeHidden)   <-- visibility gate runs HERE, in memory,
        |         (unchanged; still the sole security/spoiler       AFTER the DB round-trip, BEFORE grouping
        |          boundary for IsRevealed + Show Hidden)
        |
        |-- map to ContactViewModel (now carries CategoryId/CategoryName)
        |
        |-- GROUP BY category, ORDER BY SortOrder, Ungrouped pinned last
        |         (empty groups need no explicit suppression step --
        |          GroupBy over the already-filtered list can never
        |          produce a group with zero visible contacts)
        |
        v
ContactsIndexViewModel { IList<ContactCategoryGroupViewModel> CategoryGroups, ... }
        |
        v
Index.cshtml  OR  Index.Mobile.cshtml   (desktop card grid vs mobile stacked rows,
                                          each loops CategoryGroups, own markup, no shared partial)
```

### Recommended Project Structure

No new folders. New files land in the existing per-layer locations:

```
QuestBoard.Repository/
├── Entities/ContactEntity.cs           # + nullable CategoryId, Category navigation
├── Entities/ContactCategoryEntity.cs   # new
├── Entities/QuestBoardContext.cs       # + DbSet, HasQueryFilter, OnDelete(SetNull), unique index
├── ContactCategoryRepository.cs        # new (mirrors ContactRepository.cs shape)
└── Automapper/EntityProfile.cs         # + 2 CreateMap entries

QuestBoard.Domain/
├── Models/Contact.cs                   # + nullable CategoryId, CategoryName (display-only, like AuthorName)
├── Models/ContactCategory.cs           # new
└── Interfaces/IContactCategoryService.cs, IContactCategoryRepository.cs  # new

QuestBoard.Service/
├── Controllers/Contacts/ContactsController.cs      # Index groups after IsVisibleTo; Create/Edit dropdown
├── Controllers/Contacts/ContactCategoryManagementController.cs  # new, mirrors ShopManagementController
├── ViewModels/ContactViewModels/ContactsIndexViewModel.cs        # + CategoryGroups
├── ViewModels/ContactViewModels/ContactCategoryGroupViewModel.cs # new: { Title, IList<ContactViewModel> Contacts }
├── ViewModels/ContactViewModels/ContactViewModel.cs               # + CategoryId, CategoryName
├── Automapper/ViewModelProfile.cs                                 # + category mapping members
├── Views/Contacts/{Index,Create,Edit,Details}.cshtml + .Mobile.cshtml   # touched
└── Views/ContactCategoryManagement/{Index,Create,Edit}.cshtml + .Mobile.cshtml  # new, mirrors Views/ShopManagement
```

### Pattern 1: Fail-closed group query filter (D-02)

**What:** Every group-scoped entity gets a `HasQueryFilter` that returns zero rows when
`ActiveGroupId` is null, and dereferences the group-context service inline in the lambda —
never captures `ActiveGroupId` into a local first.

**When to use:** Any new entity that carries its own `GroupId` column directly (as opposed to
reaching group scope only through a required navigation, like `ContactImageEntity` does through
`Contact`). `ContactCategoryEntity` is the direct-`GroupId` case, exactly like `ContactEntity`
itself.

**Example — the literal block to mirror, from `QuestBoard.Repository/Entities/QuestBoardContext.cs:426-432`:**
```csharp
// ContactEntity deliberately does NOT offer a SuperAdmin cross-group view like Quest/ShopItem
// do above — same "per-group roster" shape as CharacterEntity. An empty Contact list when no
// group is selected is the intended behavior here, not an oversight.
modelBuilder.Entity<ContactEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```
The new filter for `ContactCategoryEntity` is the same shape with the entity type swapped:
```csharp
modelBuilder.Entity<ContactCategoryEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```
The file's own warning comment at lines 356-358 applies verbatim: **do not** write
`var activeGroupId = activeGroupContext.ActiveGroupId;` and close over `activeGroupId` — that
reads the value once at model-build time (null) and permanently breaks the filter. Reference
`activeGroupContext.ActiveGroupId` directly inside the lambda, exactly as every existing filter
does.

**Query filter composition with `Include` — the real footgun, and why it does NOT bite this
phase:** EF Core's documented warning (`PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`,
`[CITED: learn.microsoft.com/ef/core/querying/filters]`) fires when a **required** navigation
points at an entity with its own query filter — EF Core may use an inner join, and a filtered-out
related row can silently drop the parent row too. `Contact.CategoryId` is **nullable** and D-01
locks the relationship as optional ("a contact belongs to exactly one category, or to none"), so
`Contact → Category` is an optional navigation and EF Core uses a left join: a Contact whose
Category got filtered out (impossible here anyway, since both entities share the identical
`GroupId == ActiveGroupId` filter) would simply show a null `Category` navigation, not vanish.
**Action for the planner:** configure the relationship explicitly with `.IsRequired(false)`
(matching the `Event → Series` precedent below) so this stays true by contract, not by
convention-inferred accident.

### Pattern 2: `OnDelete(DeleteBehavior.SetNull)` on a nullable FK, with the `Group` NoAction
precedent that avoids a real SQL Server cascade-path conflict (D-03)

**What:** SQL Server refuses to create a schema with multiple cascade paths converging on the same
table from the same root delete ("may cause cycles or multiple cascade paths" DDL error). This
codebase already hit this class of problem and standardized on `NoAction` for every entity's direct
FK to `Group` — **including `Contact → Group`, which is `NoAction`, not `Cascade`** (verified
below). This matters for Phase 80 because `ContactCategoryEntity` will also have a direct `GroupId`
FK, and it must follow the same `NoAction` convention, or a `Group` delete could create exactly
the multi-path conflict this pattern exists to prevent (`Group` cascades to `ContactCategory`,
which SET NULLs `Contact`, while `Group` also has its own path to `Contact` directly).

**Verified literal precedent, `QuestBoardContext.cs:256-261`:**
```csharp
// Contact → Group: NoAction to prevent cascade cycles
modelBuilder.Entity<ContactEntity>()
    .HasOne(c => c.Group)
    .WithMany()
    .HasForeignKey(c => c.GroupId)
    .OnDelete(DeleteBehavior.NoAction);
```
This is a correction to an implicit assumption CONTEXT.md does not make explicit but the planner
might: `Contact → Group` is **not** using EF's convention-default cascade behavior. Every
`GroupId`-carrying entity in this codebase (`QuestEntity`, `ShopItemEntity`, `CharacterEntity`,
`ContactEntity`, `EventEntity`, `EventSeriesEntity`) explicitly configures `NoAction` on its
`Group` FK. **`ContactCategoryEntity → Group` must do the same** — copy this exact five-line block
with the entity/nav names swapped.

**The `Contact → Category` FK itself is the one place `SetNull` belongs**, and it is a single,
isolated cascade path (Category deleted → its Contacts SET NULL), so it does not collide with the
`NoAction`-everywhere `Group` convention above. Precedent for the fluent shape (optional FK,
explicit `.IsRequired(false)`, `.OnDelete(...)`), from `EventEntity → EventSeries`,
`QuestBoardContext.cs:277-284`:
```csharp
// Event → Series: nullable, NoAction — a one-off event has no series and a
...
modelBuilder.Entity<EventEntity>()
    .HasOne(e => e.Series)
    .WithMany()
    .HasForeignKey(e => e.SeriesId)
    .OnDelete(DeleteBehavior.NoAction)
    .IsRequired(false);
```
For `Contact → Category`, swap `NoAction` for `SetNull`:
```csharp
modelBuilder.Entity<ContactEntity>()
    .HasOne(c => c.Category)
    .WithMany()
    .HasForeignKey(c => c.CategoryId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);
```
`[CITED: learn.microsoft.com/ef/core/saving/cascade-delete]` confirms `SetNull`/`ClientSetNull`
require the FK property to be nullable (`int? CategoryId`) and the relationship optional — both
already true under D-01/D-02. Because `SetNull` (not `ClientSetNull`) emits an actual
`ON DELETE SET NULL` constraint at the database level, SQL Server nulls the FK on every dependent
row directly — **the delete action in the controller does not need to load or touch any Contact
rows itself** to make D-03's orphaning happen; it only needs a `COUNT` query beforehand to populate
the "This will move 7 contacts to Ungrouped" confirmation copy.

### Pattern 3: Case-insensitive unique index needs no computed column (D-04)

**What:** SQL Server's default column collation governs index comparison. This project's Docker
Compose service (`mcr.microsoft.com/mssql/server:2022-latest`) sets no `MSSQL_COLLATION`
environment variable, so the database uses the image's default collation,
`SQL_Latin1_General_CP1_CI_AS` — **case-insensitive**, accent-sensitive `[CITED: Microsoft SQL
Server container image documentation — default collation when MSSQL_COLLATION is unset]`. This is
corroborated inside the codebase itself: one migration
(`20260701163850_AddSessionStateTable.cs`) explicitly overrides a column to
`COLLATE SQL_Latin1_General_CP1_CS_AS` — **case-sensitive** — specifically because ASP.NET Core
session-state keys must compare case-sensitively. That override would be pointless if the
database's ambient collation were already case-sensitive; its presence is itself evidence the
default is case-insensitive.

`GroupEntity.Name` already has a plain unique index with no collation override
(`QuestBoardContext.cs:226-228`, `IX_Groups_Name`), and it is treated as case-insensitively unique
in practice — `GroupController.Create`/`Edit` catch the resulting `DbUpdateException` and surface
it as a friendly duplicate-name error (see Code Examples). **A plain `HasIndex(cc => new {
cc.GroupId, cc.Name }).IsUnique()` on `ContactCategoryEntity` gives D-04's case-insensitive
uniqueness for free — no computed lower-cased shadow column, no explicit `COLLATE` clause needed.**

### Pattern 4: `ShopManagement` as the D-06 management-page precedent

`ShopManagementController` (`QuestBoard.Service/Controllers/Shop/ShopManagementController.cs`):
- Class-level `[Authorize(Policy = "DungeonMasterOnly")]` — matches D-05 exactly; no per-action
  attribute duplication needed if the new controller follows the same class-level pattern.
- Plain `Controller`, no `[Area]` — route resolves to `/ShopManagement/{action}` by convention.
- `Index`, `Create` (GET+POST), `Edit` (GET+POST), plus item-specific workflow actions
  (`Publish`/`Archive`/`Deny`/`Reopen`/`Delete`) that Contact Categories won't need — the relevant
  subset to copy is `Index`/`Create`/`Edit`/`Delete`, plus new `MoveUp`/`MoveDown` actions for
  D-08's up/down reordering (no drag JS, no library).
- Full desktop+mobile pairing exists today: `Views/ShopManagement/{Index,Create,Edit}.cshtml` each
  have a `.Mobile.cshtml` sibling — directly confirms the both-platforms convention D-08 invokes.

### Anti-Patterns to Avoid

- **Capturing `activeGroupContext.ActiveGroupId` into a local before the `HasQueryFilter` lambda.**
  The file's own comment calls this out by name; it silently reads null at model-build time and
  the filter stops working for every request thereafter.
- **`IgnoreQueryFilters()` anywhere in application code.** D-17 forbids it on every path in this
  phase. It remains legitimate **only** inside test-assertion helpers that read raw DB state
  directly (see Test Coverage below) — that is test infrastructure verifying the DB, not an
  application code path, and the existing `EventsControllerIntegrationTests.GetSignupAsync` helper
  already uses it that way as precedent.
- **Loading and manually nulling every Contact's `CategoryId` before deleting a Category.**
  Redundant work — `OnDelete(DeleteBehavior.SetNull)` makes SQL Server do this at the constraint
  level. Manual nulling only matters if the codebase needed `ClientSetNull` (EF Core does the
  nulling in memory) instead — it does not; `SetNull` is a real DB-level `ON DELETE SET NULL`.
- **A shared `_ContactList` partial across desktop/mobile.** Already rejected in CONTEXT.md
  Discretion — the markup is genuinely different, and Phase 78 D-10 already established why
  platform-branching inside one shared surface is the wrong direction for this codebase.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Orphaning contacts on category delete | A repository method that loads every child, sets `CategoryId = null`, and saves each row | `OnDelete(DeleteBehavior.SetNull)` FK constraint | SQL Server does this atomically and correctly at the constraint level; a hand-rolled loop is slower, non-atomic, and re-implements what the FK already guarantees |
| Case-insensitive duplicate name detection | An `ToLowerInvariant()` comparison in a repository "does this name already exist" pre-check, or a computed lower-cased shadow column | A plain unique index on `(GroupId, Name)` + catching `DbUpdateException` | The DB's own collation already does case-insensitive comparison; a pre-check has a TOCTOU race the unique index closes for free, and `GroupController` already has the exact catch-and-surface pattern |
| Mobile page detection | Feature-detecting via viewport width, cookies, or client JS | The existing `MobileDetectionMiddleware` (User-Agent substring match) + `MobileViewLocationExpander` (`*.Mobile.cshtml` selection) | Already built, already tested, already wired into the pipeline — a second detection mechanism would only create drift |

**Key insight:** Every mechanism this phase needs — fail-closed filtering, orphan-on-delete,
case-insensitive uniqueness, mobile view selection — already exists in this codebase for a sibling
entity. The work is disciplined copying with the entity name swapped, not invention.

## Common Pitfalls

### Pitfall 1: Believing the category field already exists on `ContactViewModel` (per D-16's text)
**What goes wrong:** A task gets scoped as "wire the already-mapped category field onto
Details.cshtml" and skips adding the field to `Contact`, `ContactEntity`, `ContactViewModel`, and
both AutoMapper profiles.
**Why it happens:** CONTEXT.md D-16 states "The field is already on the mapped
`ContactViewModel`; no new query, no new gate" — this is inaccurate. `git grep -i categor` across
`QuestBoard.Domain/Models/Contact.cs`, `QuestBoard.Repository/Entities/ContactEntity.cs`,
`QuestBoard.Service/ViewModels/ContactViewModels/*.cs` returns zero matches.
**How to avoid:** Treat D-16 as "add the category to the existing mapped-and-displayed field set,"
not "surface a field that already exists." The category *query* is genuinely free once
`.Include(c => c.Category)` is added to `ContactRepository`'s two `GetContactWithDetailsAsync*`
methods (mirroring the existing `.Include(c => c.CreatedByUser)` pattern already there) — that part
of D-16 is correct. Only the "already on the ViewModel" claim is wrong.
**Warning signs:** A plan task for Details.cshtml/Details.Mobile.cshtml with no corresponding task
touching `Contact.cs`, `ContactEntity.cs`, `ContactViewModel.cs`, or the two AutoMapper profiles.

### Pitfall 2: Configuring `ContactCategoryEntity → Group` with EF's convention default instead of `NoAction`
**What goes wrong:** Migration generation either succeeds with an unintended `Cascade` delete on a
required FK, or fails outright at `dotnet ef migrations add` / SQL Server DDL apply time with a
"may cause cycles or multiple cascade paths" error, because `Group → ContactCategory` (cascade) and
`Group → Contact` (also on the delete graph) both eventually touch `Contact`.
**Why it happens:** Every other `GroupId`-carrying entity in this codebase explicitly overrides
the convention default to `NoAction`; a new entity added without copying that override risks
silently picking up `Cascade` instead. EF Core's convention-based default for a required
(non-nullable) FK is `Cascade`, not `NoAction`.
**How to avoid:** Copy the exact five-line `HasOne(...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.NoAction)`
block from any of the six existing `Group` FK configurations (`QuestBoardContext.cs:235-275`),
substituting `ContactCategoryEntity`/`.Group`/`.GroupId`.
**Warning signs:** `dotnet ef migrations add` throws or generates unexpected SQL; a generated
migration includes `ON DELETE CASCADE` on the `ContactCategories.GroupId` FK.

### Pitfall 3: Grouping before `IsVisibleTo`, not after (D-13)
**What goes wrong:** A category heading with 3 hidden-and-unrevealed contacts renders with a
visible heading and zero visible cards under it — the exact spoiler leak D-13 is written to
prevent ("Corridor" existing is itself campaign-revealing to a player).
**Why it happens:** It is structurally tempting to `GROUP BY CategoryId` at the EF Core query
level (in `ContactRepository`) because it looks like "the database already knows the grouping" —
but the database has no idea which contacts are visible to *this* viewer; `IsVisibleTo` is a
three-branch, viewer-specific, in-memory predicate (`IsRevealed` OR creator-of-record OR DM +
Show Hidden) that cannot be pushed into SQL without duplicating that logic in two places.
**How to avoid:** Grouping happens in `ContactsController.Index`, strictly after the existing
`allContacts.Where(c => IsVisibleTo(...)).ToList()` line (currently line 34). Once grouping runs
over an already-filtered `List<ContactViewModel>`, empty-group suppression requires **no separate
code** — a `GroupBy` over a pre-filtered sequence cannot produce a group with zero elements in the
first place.
**Warning signs:** A `GROUP BY` clause, `.Include(...).ThenInclude(...)` category aggregation, or
any category-count logic living in `ContactRepository` rather than `ContactsController`.

### Pitfall 4: Devtools mobile emulation "confirming" the mobile view renders
**What goes wrong:** A developer verifies `ContactCategoryManagement/Index.Mobile.cshtml` and the
mobile Contacts index by resizing a browser window / using Chrome DevTools device emulation, sees
what looks like a responsive layout, and ships it — but the request never actually carried a
mobile User-Agent, so `MobileViewLocationExpander` never selected the `.Mobile.cshtml` file at all;
what rendered was the desktop view with the browser's own viewport shrinking it.
**Why it happens:** `MobileDetectionMiddleware` keys entirely off
`context.Request.Headers.UserAgent` (substring match against `Mobi`, `Android`, `iPhone`, `iPad`,
`Windows Phone`, `BlackBerry`) — DevTools viewport emulation does not change this header unless the
device toolbar's specific device profile is selected AND network throttling/UA override is also
explicitly enabled, which is easy to skip.
**How to avoid:** Verification must be either (a) a real phone, or (b) an integration test that
sets a literal mobile `User-Agent` header on the `HttpRequestMessage`, exactly as
`AgendaMobileRenderTests.GetMobileAsync` does (see Code Examples) — reuse that helper's shape
verbatim in the new Contact Categories mobile render tests.
**Warning signs:** A UAT or manual-verification note that says "checked in DevTools" with no
mention of a real device or a `User-Agent` header override.

## Code Examples

### Fail-closed query filter for a new group-scoped entity
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs:426-432 (ContactEntity, the entity
// ContactCategoryEntity must mirror exactly — same "no SuperAdmin cross-group view" shape)
modelBuilder.Entity<ContactCategoryEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```

### `Group` FK convention every direct-`GroupId` entity in this codebase follows
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs:256-261 (ContactEntity -> Group)
modelBuilder.Entity<ContactCategoryEntity>()
    .HasOne(cc => cc.Group)
    .WithMany()
    .HasForeignKey(cc => cc.GroupId)
    .OnDelete(DeleteBehavior.NoAction);
```

### Optional FK with `SetNull` (the one delete behavior actually new to this phase)
```csharp
// Shape source: QuestBoard.Repository/Entities/QuestBoardContext.cs:277-284 (Event -> Series,
// which uses NoAction instead of SetNull -- swap the OnDelete value only)
modelBuilder.Entity<ContactEntity>()
    .HasOne(c => c.Category)
    .WithMany()
    .HasForeignKey(c => c.CategoryId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);

modelBuilder.Entity<ContactCategoryEntity>()
    .HasIndex(cc => new { cc.GroupId, cc.Name })
    .IsUnique();
```

### Surfacing a unique-constraint violation as a ModelState error, not a 500
```csharp
// Source: QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:38-56 (Create) and
// 67-88 (Edit) -- the only existing precedent for this exact exception-to-ModelState pattern
try
{
    await categoryService.AddAsync(new ContactCategory { Name = model.Name, GroupId = activeGroupId });
    TempData["Success"] = "Category created successfully.";
    return RedirectToAction(nameof(Index));
}
catch (DbUpdateException ex) when (
    ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
    ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
{
    ModelState.AddModelError(nameof(model.Name), "A category with that name already exists. Please choose a different name.");
    return View(model);
}
```

### Grouping after the visibility filter (the D-13 insertion point)
```csharp
// Source pattern: QuestBoard.Service/Controllers/Contacts/ContactsController.cs:22-50 (Index),
// extended. IsVisibleTo (line 469) is unchanged and still runs first.
var allContacts = await contactService.GetAllContactsWithDetailsAsync(token);
var visibleContacts = allContacts.Where(c => IsVisibleTo(c, currentUser.Id, includeHidden)).ToList();

var contactViewModels = mapper.Map<List<ContactViewModel>>(visibleContacts);
foreach (var vm in contactViewModels) { vm.CanManage = viewerIsDmTier; }

// Grouping happens strictly after the line above. A GroupBy over an already-filtered list can
// never yield a group with zero visible contacts, so no separate "drop empty groups" step exists.
var categoryGroups = contactViewModels
    .GroupBy(c => (c.CategoryId, c.CategoryName, c.CategorySortOrder))
    .OrderBy(g => g.Key.CategoryId == null) // Ungrouped (null CategoryId) always sorts last
    .ThenBy(g => g.Key.CategorySortOrder)
    .Select(g => new ContactCategoryGroupViewModel
    {
        Title = g.Key.CategoryName ?? "Ungrouped",
        Contacts = [.. g.OrderBy(c => c.Name)] // D-12: alphabetical within a category, unchanged
    })
    .ToList();
```

### A real mobile User-Agent test, reused verbatim
```csharp
// Source: QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs:17-38 -- copy this shape
// for a new ContactCategoryMobileRenderTests / ContactsIndexMobileGroupingTests class.
private const string MobileUserAgent =
    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

private async Task<(HttpResponseMessage Response, string Html)> GetMobileAsync(HttpClient client, string url)
{
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
    var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
    var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    return (response, html);
}
```

### DB-direct assertion for D-20 (delete orphans, proven against the database)
```csharp
// Source pattern: QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs:378-385
// -- factory.Database.CreateContext() opens a fresh, untracked context; IgnoreQueryFilters() here
// is test infrastructure asserting raw DB state, not an application code path, so it does not
// violate D-17's "IgnoreQueryFilters() is forbidden on every path in this phase" (that rule targets
// ContactsController / ContactCategoryRepository / the service layer).
private async Task<ContactEntity?> GetContactAsync(int contactId)
{
    await using var ctx = factory.Database.CreateContext();
    return await ctx.Contacts
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.Id == contactId, TestContext.Current.CancellationToken);
}

// ... after POSTing /ContactCategoryManagement/Delete/{categoryId} for a category holding one contact:
(await GetContactAsync(contactId))!.CategoryId.Should().BeNull();
```

## State of the Art

Not applicable in the usual sense — nothing in this phase involves a deprecated approach or a
recent ecosystem shift. Every pattern used is the current, already-in-production approach this
codebase already follows for sibling entities (Quest, ShopItem, Character, Event, EventSeries).

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| — | — | — | Not applicable; no superseded approach exists for this phase |

**Deprecated/outdated:** None identified.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | SQL Server's default collation for this project's Docker container is `SQL_Latin1_General_CP1_CI_AS` (case-insensitive) because `MSSQL_COLLATION` is unset in `docker-compose.yml` and the codebase's own explicit `CS_AS` override elsewhere implies the ambient default is CI. Not confirmed by directly querying `SELECT SERVERPROPERTY('Collation')` against a live instance in this research session. | Pattern 3 / D-04 | If the running database was actually created with an explicit case-sensitive collation at some point outside this repo's tracked config (e.g., a manually provisioned production server), the plain unique index would give case-sensitive uniqueness instead, and "guild members" / "Guild Members" could coexist as two headings — exactly what D-04 forbids. The planner should add a one-line verification task (`SELECT DATABASEPROPERTYEX('QuestBoard', 'Collation')`) before relying on this, or simply keep the `DbUpdateException` catch as the actual safety net regardless (it fires on any unique violation the DB does enforce, and the visible failure mode if the assumption is wrong is "duplicate names allowed," not a crash) |

**If this table were empty:** it is not — see A1 above. All other claims in this research are
`[VERIFIED]` (direct file reads of this codebase) or `[CITED]` (official EF Core / SQL Server
documentation referenced by URL).

## Open Questions

1. **Exact `ContactCategoryGroupViewModel` grouping key shape when two categories could
   legitimately share a `SortOrder`**
   - What we know: D-11 locks `SortOrder` as DM-set; Claude's Discretion leaves dense-vs-sparse
     numbering and tie-breaking to the planner.
   - What's unclear: whether a tie should break by `Id` (creation order) or `Name` (alphabetical)
     — CONTEXT.md does not specify, and no existing `SortOrder`-bearing entity in this codebase
     exists to copy a tie-break convention from (this is genuinely new ground, not precedent-backed).
   - Recommendation: break ties by `Id` ascending (creation order) — simplest, deterministic, and
     consistent with how the up/down reorder buttons (D-08) would naturally reassign distinct
     values on first save, making true ties a transient state rather than a steady one.

2. **Whether `ContactCategoryRepository`/`Service` should live as new files or extend
   `ContactRepository`/`ContactService`**
   - What we know: CONTEXT.md's Integration Points section explicitly lists new
     `IContactCategoryService` / `IContactCategoryRepository` interfaces.
   - What's unclear: nothing structurally — this is confirmed as new files, mirroring the
     `ShopService`/`ShopManagementController` split (categories get their own service, contacts
     keep theirs). Flagging only because the alternative (bolting category CRUD onto
     `IContactService`) would be an easy shortcut a planner might otherwise reach for.
   - Recommendation: new files, per CONTEXT.md.

<phase_requirements>
## Phase Requirements

No `CONTACTCAT-*` requirement IDs exist yet. `.planning/REQUIREMENTS.md` has no Contact Categories
section, and `.planning/ROADMAP.md` lists `Requirements: TBD` for Phase 80. Per the phase
description, the planner's first plan must mint this requirement family into both files — following
the exact shape Phase 82's `82-01-PLAN.md` used to mint `EVTAGENDA-*` (read that plan for the
minting-plan structure: frontmatter `requirements:` list, a `## Requirements Coverage` table
addition to ROADMAP.md, and a Traceability row per ID in REQUIREMENTS.md).

The table below maps each locked decision to research support, using the decision IDs as the
provisional requirement anchor since no `CONTACTCAT-*` IDs exist yet for the planner to reference.

| Decision | Description | Research Support |
|----------|-------------|-------------------|
| D-01 | Contact belongs to exactly one category or none (nullable FK) | Pattern 2 — optional FK shape confirmed via `Event → Series` precedent |
| D-02 | Real `ContactCategory` entity with fail-closed `HasQueryFilter` | Pattern 1 — literal filter block to copy from `ContactEntity` |
| D-03 | Delete orphans via `OnDelete(SetNull)`, not cascade/block | Pattern 2 — `SetNull` fluent config + the `Group: NoAction` cascade-path precedent that must NOT be broken |
| D-04 | Case-insensitive unique `(GroupId, Name)`, surfaced as a validation error | Pattern 3 — DB collation finding (A1) + `GroupController` exception-catch precedent |
| D-05 | `DungeonMasterOnly` on every category write | Pattern 4 — `ShopManagementController` class-level attribute precedent |
| D-06 | Dedicated Manage Categories page mirroring `ShopManagement` | Pattern 4 — full controller/view inventory |
| D-07 | Disabled category select + helper text when zero categories exist | No code precedent needed — pure view-layer conditional; see Architecture Patterns diagram for where `CategoryGroups`/empty-state data originates |
| D-08 | Manage page ships desktop + mobile; reorder via up/down buttons | Pattern 4 (desktop/mobile pairing) + Pitfall 4 (verification must use a real mobile UA) |
| D-09 | Uncategorised contacts under synthetic "Ungrouped", pinned last | Code Example "Grouping after the visibility filter" |
| D-10 | Zero categories = today's flat list, no headings | Same code example — `categoryGroups.Any()` conditional at the view level |
| D-11 | Headings ordered by DM-set `SortOrder` | Same code example; Open Question 1 (tie-break) |
| D-12 | Contacts alphabetical within a category (unchanged) | Same code example — inner `OrderBy(c => c.Name)` |
| D-13 | Heading renders only if ≥1 contact survives `IsVisibleTo` | Pitfall 3 — exact insertion point and why no separate suppression step is needed |
| D-14 | Headings show name only, no count | View-layer only; `ContactCategoryGroupViewModel.Title` carries no count field by design |
| D-15 | Category name is plain Razor-escaped text, ~60 char cap | `[StringLength(60)]` on the `Name` property, matching the `[StringLength]` convention already used on `ContactEntity.Name`/`TownCity` |
| D-16 | Category shown on both Details views | Pitfall 1 — the CONTEXT.md/code conflict; `.Include(c => c.Category)` addition needed in `ContactRepository` |
| D-17 | Cross-group isolation, two-group integration test | Test Coverage section below — exact fixture pattern (`SeedCampaignGroupAsync` + manual `UserGroups` seed) |
| D-18 | Empty-heading suppression both directions | Pitfall 3 |
| D-19 | Ordering + Ungrouped placement + zero-category flat list, pinned by test | Validation Architecture section below |
| D-20 | Delete orphans asserted against the DB | Code Example "DB-direct assertion for D-20" |
</phase_requirements>

## Test Infrastructure (D-17, D-20)

- **Base fixture:** `WebApplicationFactoryBase` (`QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`)
  — exposes `factory.Database` (a `TestDatabase` wrapping an EF Core InMemory provider) and
  `factory.TestGroupContext.ActiveGroupId`, which test code sets directly to simulate the active
  group without going through session/cookie machinery.
- **Existing Contacts test class:** `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`
  (879 lines) — add new `[Fact]` methods here for Create/Edit/Delete/Index category behavior rather
  than a new class, matching how every other Contacts behavior (D-09/D-12/D-13/D-14/D-15/D-15b from
  the original Phase 57 CONTEXT.md, referenced in this file's header comment) already lives in one
  class.
- **Cross-group isolation pattern (D-17), verified precedent at
  `ContactsControllerIntegrationTests.cs:351-398`
  (`ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups`):**
  1. `TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2)` creates group 2.
  2. `TestDataHelper.CreateTestContactAsync(factory.Services, creatorId, "...", groupId: 1, ...)`
     seeds group-1 data directly against the DB (bypassing the query filter entirely, since this is
     direct EF Core entity creation, not a filtered read).
  3. A second user is added to group 2 via a raw `UserGroupEntity` insert through
     `scope.ServiceProvider.GetRequiredService<QuestBoardContext>()`.
  4. `factory.TestGroupContext.ActiveGroupId = 1` then `= 2` toggles the simulated active group
     between requests within the same test.
  5. Assertions check the HTML response body for absence of the other group's data
     (`content.Should().NotContain("Group One Hidden Contact")`).
  For Phase 80's D-17 test: seed a category in group 1, seed a category in group 2, assert (a) a
  GET to `/ContactCategoryManagement/Index` while `ActiveGroupId = 2` never contains group 1's
  category name, (b) a GET to `/Contacts/Create` while `ActiveGroupId = 2` never renders group 1's
  category name in the `<select>`, and (c) a POST to `/Contacts/Create` or `/Contacts/Edit` with a
  `CategoryId` belonging to group 1's category, while `ActiveGroupId = 2`, is refused (400/redirect
  with a ModelState error) rather than silently persisting a cross-group FK.
- **DB-direct assertion pattern (D-20), verified precedent at
  `EventsControllerIntegrationTests.cs:378-385` and used throughout that file** — see the Code
  Examples section above for the exact `factory.Database.CreateContext()` +
  `.IgnoreQueryFilters()` + `FirstOrDefaultAsync` shape. This is the only sanctioned use of
  `IgnoreQueryFilters()` in this phase: it lives in test-only helper methods, asserting raw DB
  state after an HTTP action, never in `ContactsController`, `ContactCategoryManagementController`,
  or any repository/service method.
- **`TestDataHelper.CreateTestContactAsync`** (`QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs:166-195`)
  will need a new sibling, e.g. `CreateTestContactCategoryAsync(services, groupId, name, sortOrder)`,
  following the identical `using var scope = services.CreateScope(); ... context.Add(...); await
  context.SaveChangesAsync();` shape already used for `CreateTestContactAsync`.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 `[VERIFIED: QuestBoard.IntegrationTests.csproj]`, with `xunit.runner.visualstudio` 3.1.5 |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run command | `dotnet test --filter "FullyQualifiedName~ContactsController\|FullyQualifiedName~ContactCategory"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|--------------------|--------------|
| D-17 | Category names/dropdown/POST never leak across groups | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_CrossGroup"` | ❌ Wave 0 |
| D-18 | Heading suppressed for a player, appears for DM+ShowHidden | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_EmptyHeadingSuppression"` | ❌ Wave 0 |
| D-19 | `SortOrder` ordering, Ungrouped last, zero-category flat list unchanged | integration | `dotnet test --filter "FullyQualifiedName~ContactsIndex_CategoryOrdering"` | ❌ Wave 0 |
| D-20 | Delete orphans, asserted via `IgnoreQueryFilters()` DB read | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_DeleteOrphans"` | ❌ Wave 0 |
| D-04 | Duplicate name (case-insensitive) surfaces as ModelState error, not 500 | integration | `dotnet test --filter "FullyQualifiedName~ContactCategory_DuplicateName"` | ❌ Wave 0 |
| D-08 | Mobile management page selected by real User-Agent, not devtools emulation | integration (render) | `dotnet test --filter "FullyQualifiedName~ContactCategoryMobileRender"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Contact"` (fast, scoped)
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] New `[Fact]` methods in `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` — covers D-13/D-14/D-16 (grouping + visibility interplay on the existing Index/Details actions)
- [ ] New file `QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs` — covers D-04/D-05/D-06/D-08 (CRUD, authorization, reorder, mobile render)
- [ ] New file or new region in the same file — covers D-17 (cross-group isolation) and D-20 (delete-orphan DB assertion)
- [ ] `TestDataHelper.CreateTestContactCategoryAsync` helper — needed by every test above
- [ ] Unit tests: `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` (extend for `.Include(Category)`) and a new `ContactCategoryRepositoryTests.cs` / `ContactCategoryServiceTests.cs` pair, mirroring `ContactServiceTests.cs`'s existing structure
- Framework install: none — xunit.v3 already present and configured

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | No | Unchanged — this phase adds no auth surface |
| V3 Session Management | No | Unchanged |
| V4 Access Control | Yes | `[Authorize(Policy = "DungeonMasterOnly")]` class-level attribute on the new management controller (D-05), mirroring `ShopManagementController` and `ContactsController`'s existing Create/Edit/Delete attributes |
| V5 Input Validation | Yes | `[StringLength(60)]` + `[Required]` on `ContactCategory.Name` (server-side `ModelState`), matching the existing `[StringLength]` pattern on `ContactEntity.Name`/`TownCity`/`SubLocation` |
| V6 Cryptography | No | Not applicable — no secrets, tokens, or crypto in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Cross-group data disclosure via a missing or bypassed query filter (this app's own documented history: Phases 49 and 55) | Information Disclosure | Fail-closed `HasQueryFilter` on `ContactCategoryEntity` (Pattern 1) + a hard project rule that `IgnoreQueryFilters()` never appears in `ContactCategoryManagementController`, `ContactsController`, or any repository/service method — D-17's two-group integration test is the automated proof, not a manual review |
| Category-name-as-spoiler disclosure (a heading itself reveals plot content to a player who cannot see anything under it) | Information Disclosure | D-13/D-18: grouping computed strictly after `IsVisibleTo`, so an empty group for this viewer can never render a heading — enforced structurally by `GroupBy` over pre-filtered data, not by an extra runtime check that could be forgotten |
| IDOR — a POST assigning a Contact to a `CategoryId` the caller's active group does not own | Tampering / Elevation of Privilege | The existing `HasQueryFilter` on `ContactCategoryEntity` means a lookup for that `CategoryId` scoped to the active group returns null for a foreign category ID; the Create/Edit POST handlers must explicitly validate the submitted `CategoryId` resolves within the active group (via a filtered lookup) before persisting, rather than trusting the posted FK value directly — this is the concrete mechanism D-17's "a POST assigning a contact to a category id owned by another group is refused" clause requires |
| Stored XSS via a category name rendered as a heading | Tampering | D-15: plain Razor `@`-interpolation (auto-HTML-escaped by default in `.cshtml`), never routed through `IMarkdownService` — identical to how `Contact.Name`/`TownCity` are already rendered on the same views |

## Sources

### Primary (HIGH confidence — direct codebase reads, this session)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — full `HasQueryFilter` block (lines 350-476), `Group` FK `NoAction` convention (lines 235-275), `Event → Series` nullable-FK shape (277-284), existing unique indexes (96-99, 216-217, 226-228, 231-233, 325-334)
- `QuestBoard.Repository/Entities/ContactEntity.cs`, `QuestBoard.Domain/Models/Contact.cs`, `QuestBoard.Service/ViewModels/ContactViewModels/{ContactViewModel,ContactsIndexViewModel}.cs` — confirmed zero existing category field (Pitfall 1)
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` — full file, `Index`/`IsVisibleTo`/`Create`/`Edit`/`Details` actions
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — `DbUpdateException` catch-and-surface precedent (lines 38-56, 67-88)
- `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs`, `QuestBoard.Service/Views/ShopManagement/*.{cshtml,Mobile.cshtml}` — D-06 precedent
- `QuestBoard.Service/ViewModels/ShopViewModels/ShopCategoryViewModel.cs` — confirmed dead/unreferenced (grep across `QuestBoard.Service`)
- `QuestBoard.Service/Middleware/MobileDetectionMiddleware.cs`, `QuestBoard.Service/ViewExpanders/MobileViewLocationExpander.cs` — mobile selection mechanism
- `QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs` — real-mobile-UA test helper (lines 17-38)
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` — cross-group test fixture pattern (lines 351-398)
- `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` — DB-direct assertion pattern (`GetSignupAsync`, lines 378-385, and its ~15 call sites)
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — `CreateTestContactAsync` (166-195), `SeedCampaignGroupAsync` (249-264)
- `QuestBoard.Repository/Automapper/EntityProfile.cs`, `QuestBoard.Service/Automapper/ViewModelProfile.cs` — existing Contact mapping shape
- `QuestBoard.Repository/QuestBoard.Repository.csproj` — EF Core 10.0.9 version confirmation
- `docker-compose.yml` — absence of `MSSQL_COLLATION`; `QuestBoard.Repository/Migrations/20260701163850_AddSessionStateTable.cs` — the contrasting explicit `CS_AS` override

### Secondary (MEDIUM confidence — official docs, WebSearch)
- [EF Core Global Query Filters — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/filters) — required-navigation + query-filter interaction warning
- [Cascade Delete — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete) — `SetNull` requires a nullable FK and optional relationship

### Tertiary (LOW confidence)
- SQL Server container default collation (`SQL_Latin1_General_CP1_CI_AS` when `MSSQL_COLLATION` unset) — not independently re-verified against a live instance in this session; see Assumptions Log A1

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; existing versions read directly from `.csproj`
- Architecture: HIGH — every mechanism has a literal, verified precedent already in this codebase
- Pitfalls: HIGH — all four pitfalls are grounded in direct code reads (Pitfall 1: grep returning zero matches; Pitfall 2/3: literal existing code; Pitfall 4: existing middleware source + existing test precedent)

**Research date:** 2026-08-30
**Valid until:** 2026-09-27 (30 days — stable internal codebase patterns, not a fast-moving external dependency)
