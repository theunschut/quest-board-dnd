# Phase 81: Contact Tags and Filtering - Research

**Researched:** 2026-08-30
**Domain:** ASP.NET Core MVC (EF Core many-to-many + global query filters), CDN-wrapped vanilla-JS tag input, query-string filter UI
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

D-01 through D-30, verbatim from `.planning/phases/81-contact-tags-and-filtering/81-CONTEXT.md`:

- **D-01:** Tags are DM-tier only. Players see no tags, no chips, and no filter control. Every tag surface sits inside the `ViewerIsDmTier` conditional the index views already use.
- **D-02:** `[Authorize(Policy = "DungeonMasterOnly")]` on every tag write.
- **D-03:** A real `ContactTag` entity — `Id`, `Name`, `GroupId` — joined many-to-many to `ContactEntity`. Not a second category column, not a denormalised name-per-contact row.
- **D-04:** Tag names are unique per group, case-insensitive — a unique index on `(GroupId, Name)`, same shape as Phase 80 D-04. Typing "Shopkeeper" when "shopkeeper" exists reuses the existing row.
- **D-05:** `ContactTag` needs its own fail-closed `HasQueryFilter` in `QuestBoardContext.cs`, same shape as `ContactEntity` at line 405. The lambda must dereference `activeGroupContext` inline. No SuperAdmin cross-group view.
- **D-06:** Orphaned tag rows are pruned when the last contact drops them — on contact save and on contact delete. An unknown, deleted, or foreign tag id in the query string must silently match nothing, never 404 and never error.
- **D-07:** No management page. Tag creation is free-typed on the contact form; removing a tag from its last contact is how a DM deletes it. No rename path.
- **D-08:** OR semantics — a contact matches if it carries *any* selected tag.
- **D-09:** Filter state lives in the query string as repeated tag ids — `?tag=3&tag=7` — bound exactly the way `ShopController.Index` binds `IList<ItemRarity>? rarity`. Not session.
- **D-10:** The filter narrows what the viewer could already see and can never widen it. Applied in memory, after `ContactsController.IsVisibleTo`. `IgnoreQueryFilters()` is forbidden on every path in this phase.
- **D-11:** Under an active filter, Phase 80's category headings stay, and empty ones drop out.
- **D-12:** The filter lists only tags carried by contacts this viewer can see — derived from the viewer's visible-but-unfiltered contact set. No separate vocabulary query.
- **D-13:** The tag filter survives a Show Hidden toggle. `ToggleShowHidden` must carry the selected tag ids through its redirect.
- **D-14:** A chips/typeahead widget on all four Create/Edit views.
- **D-15:** Wrap a CDN library (Tagify or similar) rather than hand-rolling — the exact shape `image-crop.js` uses for `cropperjs`. Research must confirm the current version and hash rather than assuming.
- **D-16:** The widget degrades to a plain comma-separated text input. The server parses one value shape regardless of whether JS ran — split, trim, drop empties, dedupe case-insensitively, upsert against D-04's index.
- **D-17:** Tag chips on the index cards, and a muted tag line on Details — both DM-tier only, mirroring Phase 80 D-16.
- **D-18:** Tag names render as plain Razor-escaped text — never through `IMarkdownService`.
- **D-19:** Before a board has any tags, the filter control renders disabled with helper text pointing at the contact form (applies Phase 80's D-07 logic, not D-10).
- **D-20:** The filter control follows the Shop pattern on both platforms — `method="get"` form with checkboxes + Apply/Clear on desktop, bottom offcanvas drawer on mobile.
- **D-21:** Two-branch empty state, mirroring the Shop.
- **D-22:** Desktop and mobile ship together in this phase; mobile markup is verified with a real mobile User-Agent, not devtools emulation.
- **D-23:** Cross-group tag isolation, proved by a two-group integration test. `IgnoreQueryFilters()` forbidden on every path.
- **D-24:** The audience gate, both directions (player gets nothing, DM-tier gets everything).
- **D-25:** The filter narrows and never widens (including the unrevealed + Show Hidden off case).
- **D-26:** OR semantics and heading composition (union, not intersection; headings survive filter, empty ones suppressed).
- **D-27:** Vocabulary scoping (a tag borne only by unrevealed contacts doesn't leak into the filter list).
- **D-28:** Orphan pruning asserted against the database.
- **D-29:** The no-JS path (comma-separated POST tags correctly, dedupes case-insensitively, reuses existing rows).
- **D-30:** The Show Hidden round trip preserves the filter.

### Claude's Discretion

- **Tag name length and count cap.** Locked as: names capped at ~30 characters, no hard cap on tags per contact (chip markup must wrap gracefully).
- **Which library exactly.** Tagify is the assumed choice under D-15; planner may substitute an equivalent meeting the same constraints.
- Join-table naming and whether it is an explicit entity or a skip-navigation.
- Whether to mint a `CONTACTTAG-*` requirement family into REQUIREMENTS.md as plan 01.
- CSS class naming for chips, the filter row, and the offcanvas.
- Exact wording of the disabled-filter hint text (D-19) and the no-results message (D-21).

### Deferred Ideas (OUT OF SCOPE)

- Opening tags to players (flip `ViewerIsDmTier`).
- Renaming and merging tags, and a Manage Tags page.
- Clickable tag chips as a filter shortcut.
- An AND/"match all" toggle.
- A "some hidden contacts match — turn on Show Hidden" nudge on the empty state.
- Bulk tagging.
- Free-text search over contacts.
- Tags on Characters or Quests.

</user_constraints>

<phase_requirements>
## Phase Requirements

**No `CONTACTTAG-*` requirement IDs exist yet.** `.planning/ROADMAP.md` lists `Requirements: TBD` for Phase 81, and `.planning/REQUIREMENTS.md`'s v1 table has no `CONTACTTAG-*` family — confirmed by direct read of both files during this research session `[VERIFIED: .planning/REQUIREMENTS.md, .planning/ROADMAP.md]`. Phase 82 set the precedent for this situation: it minted its own `EVTAGENDA-*` family as its first plan rather than waiting for a separate requirements-authoring step. CONTEXT.md's Claude's-Discretion list explicitly leaves "whether to mint a `CONTACTTAG-*` requirement family" to the planner.

Per this agent's instructions, requirement IDs are not minted during research. The 30 locked decisions (D-01–D-30) in CONTEXT.md are the requirement source for planning, exactly as Phase 80's CONTEXT.md was for Phase 80. If the planner mints `CONTACTTAG-*` IDs, D-01 through D-22 (feature behavior) and D-23 through D-30 (test coverage) map roughly one-to-one to individual requirement IDs — a natural split.

</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Windows dev environment** — CRLF line endings, Windows-style paths, no Unix-only shell syntax in anything checked in.
- **Never commit to `main`** — this work lands on `milestone/v9-rolling-improvements` (current branch), consistent with existing history.
- **EF packages belong only in `QuestBoard.Repository`** — never add an EF Core package reference to `QuestBoard.Service`.
- **Migrations auto-apply on startup** via `context.Database.Migrate()` — no manual `dotnet ef database update` step needed in dev; `docker-compose up` alone deploys new migrations.
- **Three-layer architecture, one-way dependency**: Service → Domain → Repository. `ContactTag` needs an entity in Repository, a domain model in Domain, and mapping at both AutoMapper boundaries (`QuestBoard.Repository/Automapper/EntityProfile.cs` and `QuestBoard.Service/Automapper/ViewModelProfile.cs`).
- **No GSD planning/tracking IDs in source comments** — do not write `D-05`, `Phase 81`, `CONTACTTAG-03`, etc. into code comments, XML docs, or string literals. Comments must explain *why* in plain language that stays true independent of which phase touched the code (see the existing `QuestBoardContext.cs` filter comments for the house style to match).
- **UI/UX Design Guidelines** — new views/partials use `modern-card` / `modern-card-header` / `modern-card-body`, `<hr>` before the button section, filled colored buttons with FontAwesome + `me-2`, `d-flex justify-content-between` button layout. This phase touches existing Contacts views rather than adding new pages, so apply these conventions only where new markup (the filter form, the tag-entry widget) is introduced — match the surrounding view's existing card structure otherwise.
- **RIP MCP navigation protocol** — not available in this session (no `mcp__rip__*` tools were exposed); this research fell back to `Grep`/`Read`/`Bash` per the documented fallback path. If RIP is available at plan/execute time, the planner and executor should prefer it per CLAUDE.md.

## Summary

This phase adds one genuinely new architectural shape to the codebase: **the app's first many-to-many relationship**. Every existing `HasQueryFilter` in `QuestBoardContext.cs` (18 of them) scopes either a direct `GroupId` column or a required one-to-many navigation (`Quest.GroupId`, `pd.Quest.GroupId`, etc.) — there is no `UsingEntity`/skip-navigation anywhere in the codebase today `[VERIFIED: grep of QuestBoardContext.cs]`. That means this phase cannot copy-paste an existing local pattern for the join table; it can only copy the *entity-level* fail-closed filter pattern (which does transfer directly) and must reason from first principles (backed by official EF Core docs, gathered below) about how that filter interacts with a many-to-many collection navigation.

The good news: EF Core's documented behavior gives a low-risk path if the plan follows it. A `HasQueryFilter` on `ContactTagEntity` (the "many" side reached via `Contact.Tags`) is a **collection** navigation, not the required-reference-navigation case Microsoft's own docs warn about (where a filtered parent silently drops rows via `INNER JOIN`). For a collection navigation, a foreign-group `ContactTag` row simply fails to appear in the loaded `Tags` collection — it does not cause the parent `Contact` to disappear from results. The actual leak vector in this feature is not query-filter propagation; it is the **write path** — a POST that attaches a contact to a submitted tag id must resolve that id through a query that respects `ContactTagEntity`'s filter (a plain `Where(...).ToListAsync()`), never `Find()`/`FindAsync()` (which can return a tracked entity from memory without re-querying, sidestepping the filter) and never a raw SQL/`IgnoreQueryFilters()` shortcut. D-23's cross-group POST-refusal test is exactly the test that catches a regression here.

The join table itself needs no separate `HasQueryFilter` of its own: since every read path joins through both already-filtered ends (`Contact` and `ContactTag`), a join-table row referencing an out-of-group entity on either side structurally cannot appear in a query result. The **explicit CLR entity vs. implicit skip-navigation** question (left as Claude's discretion) has a clean answer for this phase: no payload data is needed on the join row, so use an **implicit, unmapped skip-navigation join** (`HasMany().WithMany()` + `UsingEntity(j => j.ToTable(...))` for a readable table name only) rather than a dedicated `ContactContactTag` entity class — this matches the codebase's "don't build what you don't need" posture and the EF Core docs' own advice against over-configuring a plain junction table.

The unique-index requirement (D-04) is very likely **already free**: the app's `docker-compose.yml` sets no `MSSQL_COLLATION` on the `sqlserver` service, and the Microsoft SQL Server Docker image defaults to `SQL_Latin1_General_CP1_CI_AS` (case-insensitive) when that variable is unset `[CITED: microsoft/mssql-docker GitHub issues + docs]`. A plain `HasIndex(...).IsUnique()` on `(GroupId, Name)` will therefore already treat "Shopkeeper" and "shopkeeper" as a collision under the database's ambient collation. The dev connection string points at `localhost` (a developer's own SQL Server install, not this Docker image), so the ambient-collation assumption is not verified for every dev machine — the safe, portable fix that removes the dependency on ambient server/database collation entirely is to declare the column's collation explicitly with `.UseCollation("SQL_Latin1_General_CP1_CI_AS")`, a one-line, well-supported EF Core 10 API. This makes the case-insensitive guarantee travel with the migration instead of depending on how a given SQL Server instance was provisioned.

Tagify (`@yaireo/tagify`, current version **4.38.0**, published 2026-06-27, 152k weekly downloads, `OK` verdict from the package-legitimacy gate) satisfies every constraint D-15/D-16 name: it is a UMD global loadable via a plain `<script>` tag with no bundler, it binds directly to a real `<input>`, its `originalInputValueFormat` option writes an arbitrary string (including a plain comma-joined list) back to that input on every change, its `whitelist` option accepts an array of suggestion strings/objects supplied at init time, and `enforceWhitelist: false` (the default) allows free typing of new tags outside the whitelist — exactly D-16's and D-12's requirements. Both the JS and CSS were downloaded directly from jsDelivr and SHA-384-hashed in this session (below) rather than copied from a third-party listing.

**Primary recommendation:** Model `ContactTag` as an implicit skip-navigation many-to-many (no join-entity class), give it its own fail-closed `HasQueryFilter` plus a `.UseCollation(...)`-backed unique index on `(GroupId, Name)`, validate every submitted tag id through a query (never `Find()`) before attaching it to a contact, and wrap Tagify 4.38.0 exactly the way `image-crop.js` wraps `cropperjs` — a pinned CDN `<script>`/`<link>` with the SRI hashes computed below, plus a thin `wwwroot/js/contact-tags.js` init module.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Tag CRUD (upsert-by-name, orphan pruning) | API/Backend (`ContactsController` + `ContactService`/`ContactRepository`) | Database (unique index enforces the dedup guarantee the app-layer upsert relies on) | Tag identity (D-04's case-insensitive uniqueness) must be a DB-level guarantee, not a convention, since there's no rename UI (D-07) to fix a drifted duplicate later. |
| Tag vocabulary read (filter checkbox list) | API/Backend (`ContactsController.Index`, derived in memory from the already-loaded visible contact set) | — | D-12 explicitly forbids a separate query; the vocabulary is a projection of data the controller already fetched for the index itself. No new DB round trip, no new tenancy surface to get wrong. |
| Tag filter application (narrowing the contact list) | API/Backend (`ContactsController.Index`, in-memory, after `IsVisibleTo`) | — | D-10 mandates in-memory application after the visibility gate — never in the SQL query, never before. This differs from `ShopController`'s rarity filter, which *is* applied in the query (see Common Pitfalls). |
| Tag-input UX (chips, typeahead, dedup-on-type) | Browser/Client (Tagify, vanilla JS init module) | Frontend Server (Razor renders the plain `<input>` Tagify progressively enhances) | D-16 requires the feature to work with JS entirely absent — the server-rendered `<input>` is the real control; Tagify is a client-side enhancement layer only. |
| Tag chip/line rendering (index, Details) | Frontend Server (Razor, plain escaped text) | — | D-18 forbids Markdown rendering for tag names; this is server-side HTML-escaped text output, no client logic needed. |
| Cross-group tenancy enforcement | Database (`HasQueryFilter` on `ContactTagEntity`) + API/Backend (id-lookup-before-attach) | — | The `HasQueryFilter` is necessary but not sufficient — the write path must also route every submitted tag id through a filtered query rather than an unfiltered `Find()`/raw lookup. See Common Pitfalls. |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `@yaireo/tagify` (CDN, not npm-installed) | 4.38.0 (published 2026-06-27) `[VERIFIED: npm registry — npm view, and gsd-tools package-legitimacy check: verdict OK]` | Tag chips/typeahead input, wraps a plain `<input>` | UMD global, no bundler required (matches this codebase's no-module convention), binds to a real form input, has a documented comma-string output mode and a whitelist/suggestions API — the only library found that satisfies all of D-15/D-16/D-12 without a build step. |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 (already in use) `[VERIFIED: QuestBoard.Repository.csproj]` | Data access, many-to-many mapping, `HasQueryFilter`, `.UseCollation()` | Already the project's ORM; no new package needed — `UsingEntity`, skip navigations, and `.UseCollation()` have all been stable EF Core APIs since v5/v7, well within 10.0.9. |

### Supporting

None. No new NuGet packages are required — the many-to-many mapping, the query filter, and the collation override are all built into the already-referenced `Microsoft.EntityFrameworkCore.SqlServer` package.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Tagify | `select2` / `Choices.js` / plain `<datalist>` | Select2/Choices.js are select-oriented widgets (closed vocabulary by default) and need more configuration to support free-typed new tags; `<datalist>` has no chip UI, no dedup, and inconsistent mobile browser support for a multi-value use case. Tagify is purpose-built for exactly this "chips typed into an input, optionally from a whitelist" shape. |
| Implicit skip-navigation join | Explicit `ContactContactTag` join entity class | Only worth it if the join row needs a payload (e.g., `CreatedAt`, `CreatedByUserId` on the association itself) or if tests need to query the join table directly by a strongly-typed navigation. Neither is required by any locked decision; add it later if a future phase needs join-row metadata. |
| `.UseCollation()` explicit override | Rely on ambient server/database collation | Relying on ambient collation is likely already correct in Docker/production (verified: no `MSSQL_COLLATION` override, image defaults to CI_AS) but is **not verified** for whatever SQL Server a given developer has installed at `localhost`. An explicit `.UseCollation()` removes that per-machine variable entirely, at zero cost. |

**Installation:**

No `dotnet add package` step. Tagify is added as two `<script>`/`<link>` tags (CDN), exactly like `cropperjs` — see Code Examples.

**Version verification:** `@yaireo/tagify` 4.38.0 confirmed current via `npm view @yaireo/tagify version` (returned `4.38.0`, matching jsDelivr's `X-JSD-Version: 4.38.0` response header for `@yaireo/tagify@4.38.0`) on 2026-08-30. `Microsoft.EntityFrameworkCore.SqlServer` 10.0.9 confirmed via direct read of `QuestBoard.Repository.csproj` — already the pinned version in this project, no bump needed.

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| `@yaireo/tagify` | npm (consumed via jsDelivr CDN, not `npm install`ed into this .NET project) | First published 2017-05-30, latest release 2026-06-27 (~9 years active) | 152,240/week `[VERIFIED: api.npmjs.org/downloads]` | `github.com/yairEO/tagify` `[VERIFIED: npm registry repository field]` | **OK** `[VERIFIED: gsd-tools query package-legitimacy check --ecosystem npm]` — no postinstall script, not deprecated | Approved |

**Packages removed due to [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** none.

`@yaireo/tagify`'s package name was confirmed via the package's own official GitHub README (fetched directly in this session) and the npm registry (`npm view`), and it returned `OK` from `gsd-tools query package-legitimacy check`, satisfying both prongs of the `[VERIFIED: npm registry]` bar. Because this library is consumed via CDN `<script>` tag rather than `npm install`, there is no `package.json`/lockfile entry and therefore no npm supply-chain surface in this repo at all — the only trust decision is "does the pinned SRI hash match the file jsDelivr serves," which is addressed by pinning the exact hashes computed below.

## Architecture Patterns

### System Architecture Diagram

```
Browser (DM-tier viewer)
  │
  │ GET /Contacts?tag=3&tag=7           (D-09: repeated query-string ids)
  ▼
ContactsController.Index
  │
  ├─► IsDmTierAsync() / ReadShowHiddenToggle()          (existing, unchanged)
  │
  ├─► contactService.GetAllContactsWithDetailsAsync()   ── EF Core query ──►  SQL Server
  │        (adds .Include(c => c.Tags), .AsSplitQuery())                     │
  │        ContactEntity.HasQueryFilter  ──┐                                 │  ContactTagEntity.HasQueryFilter
  │                                        │  both scope to activeGroupContext.ActiveGroupId
  │                                        └────────────────────────────────►│  (D-05, new)
  │
  ├─► visibleContacts = allContacts.Where(IsVisibleTo)   (D-10: unchanged gate, runs FIRST)
  │
  ├─► availableTags = visibleContacts.SelectMany(c => c.Tags).Distinct()   (D-12: derived in memory, no new query)
  │
  ├─► filteredContacts = selectedTagIds.Any()
  │        ? visibleContacts.Where(c => c.Tags.Any(t => selectedTagIds.Contains(t.Id)))   (D-08: OR / union)
  │        : visibleContacts                                                              (D-10: narrows only)
  │
  ├─► group by Phase 80 category, drop empty groups        (D-11: composes with Phase 80's suppression rule)
  │
  ▼
ContactsIndexViewModel { Contacts (grouped), SelectedTagIds, AvailableTags, HasActiveFilters, ... }
  │
  ▼
Views/Contacts/Index.cshtml + Index.Mobile.cshtml           (D-20: shop-filter-row form / offcanvas drawer)
  │
  ▼
Browser renders chips (D-17), filter checkboxes pre-checked from SelectedTagIds, Apply/Clear buttons

─────────────────────────── Tag entry (Create/Edit) ───────────────────────────

Browser
  │  <input name="TagsInput" value="shopkeeper, quest giver">     (D-16: real input, plain value)
  │  Tagify enhances it: whitelist = AvailableTags (JSON, D-12's vocabulary reused here too)
  │  On every change, Tagify writes the comma string back via originalInputValueFormat
  ▼
POST /Contacts/Create or /Contacts/Edit/{id}
  │
  ▼
ContactsController.Create/Edit (POST)
  │
  ├─► parse TagsInput: split(',') → trim → drop empty → dedupe case-insensitively   (D-16, JS-independent)
  │
  ├─► look up each name against ContactTagEntity via a FILTERED query               (never Find()/FindAsync())
  │        — a match reuses the existing row (D-04); no match inserts a new ContactTagEntity
  │
  ├─► [Authorize(Policy = "DungeonMasterOnly")]                                     (D-02)
  │
  ▼
SaveChanges → prune any ContactTag now orphaned (0 contacts) as part of the same save   (D-06)
```

### Recommended Project Structure

```
QuestBoard.Repository/Entities/
├── ContactTagEntity.cs           # new — Id, Name, GroupId, Contacts (skip nav)
└── QuestBoardContext.cs          # + DbSet<ContactTagEntity>, + HasQueryFilter, + UsingEntity/.UseCollation()

QuestBoard.Domain/
├── Models/Contact.cs             # ContactTag domain model + Contact.Tags collection added alongside existing ContactNote
├── Interfaces/IContactTagRepository.cs / IContactTagService.cs   # only if a dedicated seam is needed (see Open Questions)
└── Services/ContactService.cs    # upsert-by-name + orphan-prune logic

QuestBoard.Service/
├── Controllers/Contacts/ContactsController.cs   # Index gains tag param + filter/vocab logic; Create/Edit parse TagsInput; ToggleShowHidden carries tag ids
├── ViewModels/ContactViewModels/
│   ├── ContactViewModel.cs        # + TagsInput (bound), TagNames (display)
│   └── ContactsIndexViewModel.cs  # + SelectedTagIds, AvailableTags, HasActiveFilters
├── Views/Contacts/
│   ├── Index.cshtml / Index.Mobile.cshtml     # + filter form/offcanvas (D-20), + chips (D-17)
│   ├── Create.cshtml / Create.Mobile.cshtml   # + tag input + Tagify CDN scripts
│   ├── Edit.cshtml / Edit.Mobile.cshtml       # + tag input + Tagify CDN scripts
│   └── Details.cshtml / Details.Mobile.cshtml # + muted tag line (D-17)
└── wwwroot/
    ├── js/contact-tags.js          # new — thin Tagify init module, mirrors image-crop.js's initImageCrop() convention
    └── css/contacts.css / contacts.mobile.css # + chip styles, filter row/offcanvas styles, Tagify CSS-var overrides
```

### Pattern 1: Fail-closed group filter on the new entity (direct copy of the house pattern)

**What:** Every group-scoped entity gets a `HasQueryFilter` returning zero rows when no group is active, dereferencing `activeGroupContext` inline (never captured into a local).
**When to use:** `ContactTagEntity`, exactly like every other entity in `QuestBoardContext.cs`.
**Example:**
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs:405-410 (existing ContactEntity filter, pattern to copy verbatim for ContactTagEntity)
modelBuilder.Entity<ContactTagEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

// ContactTagEntity gets no SuperAdmin cross-group view, same as ContactEntity
// (comment at QuestBoardContext.cs:402) — per-group roster data, not shared.
```

### Pattern 2: Implicit skip-navigation many-to-many, named join table, no payload

**What:** `Contact.Tags` / `ContactTag.Contacts` skip navigations, backed by an unmapped join table given an explicit name (for readable migrations) but no dedicated CLR class.
**When to use:** This phase — no data needs to live on the association row itself.
**Example:**
```csharp
// Source: EF Core official docs, "Many-to-many with named join table"
// https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many
modelBuilder.Entity<ContactEntity>()
    .HasMany(c => c.Tags)
    .WithMany(t => t.Contacts)
    .UsingEntity(j => j.ToTable("ContactContactTags"));
```
```csharp
// ContactEntity.cs
public virtual ICollection<ContactTagEntity> Tags { get; set; } = [];

// ContactTagEntity.cs
public virtual ICollection<ContactEntity> Contacts { get; set; } = [];
```

### Pattern 3: Case-insensitive uniqueness that doesn't depend on ambient server collation

**What:** Declare the column's own collation explicitly instead of relying on whatever collation the target SQL Server instance happens to have.
**When to use:** `ContactTagEntity.Name`, for D-04.
**Example:**
```csharp
// Source: EF Core official docs — column-level UseCollation has been stable since EF Core 5
modelBuilder.Entity<ContactTagEntity>()
    .Property(t => t.Name)
    .UseCollation("SQL_Latin1_General_CP1_CI_AS");

modelBuilder.Entity<ContactTagEntity>()
    .HasIndex(t => new { t.GroupId, t.Name })
    .IsUnique();
```
With this collation on the column, a plain `context.ContactTags.FirstOrDefaultAsync(t => t.Name == submittedName)` already matches case-insensitively at the SQL level — no `.ToLower()` needed on either side, and it stays index-friendly (a `.ToLower()` comparison would not be sargable against the unique index).

### Pattern 4: Split query when adding a second collection Include

**What:** `AsSplitQuery()` on any query that `.Include()`s two independent collections off the same root, to avoid EF's `MultipleCollectionIncludeWarning` and the row-count cartesian blowup.
**When to use:** `ContactRepository.GetAllContactsWithDetailsAsync` / `GetContactWithDetailsAsync` currently `.Include(c => c.Notes)`; adding `.Include(c => c.Tags)` introduces exactly the two-independent-collections shape this pattern exists for.
**Example:**
```csharp
// Source: QuestBoard.Repository/QuestRepository.cs:90-96 (existing precedent, same rationale applies)
// "Two independent collection Includes (ProposedDates and PlayerSignups) in a single
//  query force EF to cross-join both collections, multiplying row count combinatorially
//  and triggering the MultipleCollectionIncludeWarning. AsSplitQuery() issues one query
//  per collection instead, avoiding the row-count blowup without changing the loaded shape."
var entities = await DbContext.Contacts
    .AsSplitQuery()
    .Include(c => c.CreatedByUser)
    .Include(c => c.Notes).ThenInclude(n => n.Author)
    .Include(c => c.Tags)
    .OrderBy(c => c.Name)
    .ToListAsync(token);
```

### Pattern 5: CDN library wrap, thin init module (D-15's required shape)

**What:** Pinned CDN `<script>`/`<link>` with SRI, plus a small hand-written `wwwroot/js/*.js` module that wires it to the page — no bundler, no npm install into the .NET project.
**When to use:** Loading Tagify on the four Create/Edit views.
**Example:**
```html
<!-- Source: QuestBoard.Service/Views/Contacts/Create.cshtml:125-133 (existing cropperjs precedent, exact shape to copy) -->
@section Scripts {
    <link href="https://cdn.jsdelivr.net/npm/@yaireo/tagify@4.38.0/dist/tagify.css"
          integrity="sha384-C4PNucE7dGKU8Ad5d3yFKR13AcpKTe+MbcGsu83yTrSmvAMWiPj7Gb2vUJth6uLu"
          crossorigin="anonymous" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/@yaireo/tagify@4.38.0/dist/tagify.min.js"
            integrity="sha384-YtX1Y58YfRTMkRpmTDmpcMyzLXjMKMypzK5BNd5PRLaoZfVSFefqUZ855u0XJn0E"
            crossorigin="anonymous"></script>
    <script src="~/js/contact-tags.js" asp-append-version="true"></script>
    <script>
        initContactTags({ inputId: 'TagsInput', whitelist: @Html.Raw(Json.Serialize(Model.AvailableTagNames)) });
    </script>
}
```
```javascript
// wwwroot/js/contact-tags.js — mirrors image-crop.js's initImageCrop() convention: one
// reusable initializer, view supplies its own element id and whitelist.
function initContactTags(config) {
    const input = document.getElementById(config.inputId);
    if (!input) {
        return; // safe no-op, matches initImageCrop's defensive-include convention
    }
    new Tagify(input, {
        whitelist: config.whitelist || [],
        enforceWhitelist: false,          // D-16: free typing beyond the whitelist is allowed
        originalInputValueFormat: values => values.map(v => v.value).join(', '),
        maxTags: undefined                // D-14 discretion: no hard cap on tags per contact
    });
}
```

### Anti-Patterns to Avoid

- **Applying the tag filter in the SQL query (like `ShopController` applies rarity):** D-10 explicitly requires the filter to run in memory, after `IsVisibleTo`. `ShopController.Index` is the UI/markup model (D-20), not the filter-execution model — don't copy that half of the precedent. Applying it in the query would put the filter *before* the visibility gate, which is exactly the "widen what's visible" failure D-10 forbids.
- **`context.ContactTags.Find(id)` to resolve a submitted tag id:** `Find()`/`FindAsync()` can return an already-tracked entity straight from the change tracker without re-running the query (and therefore without re-evaluating the global query filter) `[ASSUMED — EF Core's documented Find/FindAsync tracked-entity short-circuit behavior; not independently re-verified against the EF Core source in this session, but well-established EF Core semantics]`. Use `Where(t => ids.Contains(t.Id)).ToListAsync()` instead — it always executes a real, filtered query, and a foreign-group id simply comes back missing from the result set (D-06's "silently match nothing" contract).
- **A second requirement family diverging from D-01's audience gate:** every new tag surface (filter, chips, muted line, tag-entry field) must sit inside the *same* `ViewerIsDmTier` check the Show Hidden toggle and Create button already use — don't introduce a second, parallel DM-tier check that could drift from the first (this is exactly the class of bug PROJECT.md blames for the `Characters/Edit.cshtml` `classIndex` regression).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Chip/typeahead tag input, keyboard nav, paste handling, dedup-on-type | A custom `contenteditable` or JS array-diffing widget | Tagify 4.38.0 (CDN) | D-15 already settles this; a hand-rolled widget would also need its own accessibility work (ARIA roles, focus management) that Tagify ships with. |
| Case-insensitive tag-name dedup at the database | App-layer `.Where(t => t.Name.ToLower() == name.ToLower())` scans, or a "canonical lowercase name" shadow column | Column-level `.UseCollation("SQL_Latin1_General_CP1_CI_AS")` + a plain unique index | The collation approach makes the DB itself the single source of truth for "same tag," matching D-04's requirement that this be a database guarantee, not an app convention — and it stays sargable against the unique index (a `.ToLower()` predicate on both sides is not). |
| Many-to-many join-row bookkeeping (insert/delete pairs, avoid duplicate pairs) | Manual `INSERT`/`DELETE` against a hand-modeled join table | EF Core skip navigations (`contact.Tags.Add(tag)` / `.Remove(tag)`) | EF Core already tracks the association and generates the correct join-table SQL; hand-rolling this reintroduces exactly the kind of bug class (duplicate rows, missed cascade) EF's join-entity change-tracking exists to prevent. |

**Key insight:** this phase's two genuinely new pieces of infrastructure — the many-to-many relationship and the CDN-wrapped chips widget — both already have a first-class, well-documented EF Core / Tagify feature that does exactly what's needed. The risk in this phase is not "we have to build something hard," it's "wire the two pieces (filter-then-attach on write; in-memory-filter-after-visibility on read) in the right order," which is a controller-logic concern, not a hand-rolling concern.

## Common Pitfalls

### Pitfall 1: Trusting `HasQueryFilter` alone to make tag-attachment safe

**What goes wrong:** A developer adds `HasQueryFilter` to `ContactTagEntity` (D-05) and assumes that alone makes `contact.Tags.Add(someContactTag)` safe for any `someContactTag` obtained however — including via `Find()`, via a raw id passed straight from a POST body into an EF `Attach`/stub entity, or via a query issued with `IgnoreQueryFilters()` for an unrelated reason elsewhere in the same request.
**Why it happens:** The filter *reads* correctly, so it's easy to assume it also constrains writes. It doesn't — EF Core's global query filter only affects entities that come from an actual filtered query. An entity manually constructed as `new ContactTagEntity { Id = submittedId }` and attached directly (a common shortcut for "I already know the id, skip the round trip") never passes through the filter at all.
**How to avoid:** Always resolve a submitted tag id through a real query (`Where(t => ids.Contains(t.Id)).ToListAsync()`) before attaching it to `contact.Tags`; treat any id that comes back missing as silently dropped (D-06), not as an error.
**Warning signs:** Any code path that builds a `ContactTagEntity` (or its domain-model equivalent) from a raw `int` id without a database round trip in between.

### Pitfall 2: Two independent collection Includes without `AsSplitQuery()`

**What goes wrong:** Adding `.Include(c => c.Tags)` next to the existing `.Include(c => c.Notes)` on `ContactRepository`'s two detail-fetch methods produces a cartesian-product row explosion (every note × every tag per contact) and EF Core's `MultipleCollectionIncludeWarning`.
**Why it happens:** EF Core translates two sibling collection `Include`s into a single SQL query with two joins by default, which multiplies row counts rather than keeping the collections independent.
**How to avoid:** Add `.AsSplitQuery()` — this codebase already has the identical fix in `QuestRepository.GetQuestWithManageDetailsAsync` for the same reason (`ProposedDates` + `PlayerSignups`); copy that precedent rather than rediscovering it.
**Warning signs:** A build/test run emitting `MultipleCollectionIncludeWarning` in logs, or contact note counts/tag counts looking multiplied in a manual smoke test.

### Pitfall 3: Deriving the filter vocabulary from the *filtered* set instead of the *visible-but-unfiltered* set

**What goes wrong:** Computing `AvailableTags` from `filteredContacts` (post-tag-filter) instead of `visibleContacts` (post-visibility-gate, pre-tag-filter) makes every unchecked tag checkbox disappear the moment one tag is selected, since only contacts matching the current filter remain to supply tag names.
**Why it happens:** It's natural to compute the vocabulary from "the contacts I'm about to render," which after filtering is the wrong set — CONTEXT.md's D-12 calls this out explicitly by name ("Note the 'unfiltered' half").
**How to avoid:** Compute `AvailableTags` once, from `visibleContacts`, before applying `selectedTagIds`. Do this in the controller in a fixed order: `IsVisibleTo` → derive vocabulary → apply tag filter → group by category.
**Warning signs:** A UI test that selects one tag checkbox and finds every other checkbox has vanished from the rendered form.

### Pitfall 4: `ToggleShowHidden`'s existing `RedirectToAction(nameof(Index))` silently drops the filter

**What goes wrong:** D-13 requires the Show Hidden toggle to preserve the active tag filter across its POST-redirect round trip. The current implementation (`ContactsController.cs:301-316`) redirects with no route values at all — adding tag filtering without touching this method leaves a real, easily-missed regression: flip Show Hidden while a tag filter is active, and the filter silently clears.
**Why it happens:** `ToggleShowHidden` predates tag filtering and was never written to carry query-string state through its redirect.
**How to avoid:** The Show Hidden `<form>` must carry the currently-selected tag ids as hidden `<input>` fields (as CONTEXT.md's own canonical-refs section specifies), and `ToggleShowHidden` must read them and include them as route values on its `RedirectToAction`.
**Warning signs:** A manual test: apply a tag filter, click Show Hidden, observe the tag filter checkboxes are now all unchecked.

### Pitfall 5: SQL Server collation is a per-machine variable the plan must not silently assume

**What goes wrong:** Assuming every environment (Docker prod, every developer's local SQL Server) has `SQL_Latin1_General_CP1_CI_AS` and shipping a plain unique index with no explicit collation. Docker prod is verified safe (no `MSSQL_COLLATION` override); a developer's own `localhost` SQL Server install (used per `appsettings.json`'s `DefaultConnection`) was **not** independently verified in this session and could theoretically have a different collation depending on how it was originally installed.
**Why it happens:** It's easy to test only against the Docker environment (or an already-provisioned dev database) and never notice the assumption.
**How to avoid:** Use `.UseCollation("SQL_Latin1_General_CP1_CI_AS")` explicitly on the `Name` column (Pattern 3, above) so the guarantee doesn't depend on ambient server/database collation in any environment.
**Warning signs:** A migration that adds the unique index with no `.UseCollation()` call and no test asserting case-insensitive dedup end-to-end against a real (not in-memory) database.

### Pitfall 6: EF Core's in-memory test provider doesn't enforce a real unique index or collation

**What goes wrong:** `QuestBoard.IntegrationTests` runs against `Microsoft.EntityFrameworkCore.InMemory`, which does not enforce database-level unique constraints or SQL Server collation behavior the way a real SQL Server would. A test that types "Shopkeeper" then "shopkeeper" and asserts a single row could pass against the in-memory provider due to app-layer upsert logic alone, while silently masking a broken or missing database-level index that would only surface against real SQL Server.
**Why it happens:** The in-memory provider is deliberately lenient about constraints it doesn't model (this is a documented EF Core InMemory limitation, not specific to this codebase).
**How to avoid:** Write the dedup/upsert logic so it works correctly by construction (case-insensitive lookup-before-insert), don't rely on the unique index throwing to catch the app failing to upsert. Treat the DB-level index as defense-in-depth against races/direct-DB edits, not as the primary mechanism the integration test suite exercises.
**Warning signs:** A passing integration test suite that never actually causes two near-simultaneous writes to collide against the real database's unique index.

## Code Examples

### `ContactTagEntity` (Repository layer)

```csharp
// QuestBoard.Repository/Entities/ContactTagEntity.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("ContactTags")]
public class ContactTagEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(30)]
    public string Name { get; set; } = string.Empty;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;

    public virtual ICollection<ContactEntity> Contacts { get; set; } = [];
}
```

### `QuestBoardContext.cs` additions (put in the same block as the existing Contact filters, `QuestBoardContext.cs:405-436`)

```csharp
// Source: pattern from QuestBoardContext.cs:405-410, extended per D-04/D-05
modelBuilder.Entity<ContactTagEntity>()
    .Property(t => t.Name)
    .UseCollation("SQL_Latin1_General_CP1_CI_AS");

modelBuilder.Entity<ContactTagEntity>()
    .HasIndex(t => new { t.GroupId, t.Name })
    .IsUnique();

// ContactTagEntity deliberately does NOT offer a SuperAdmin cross-group view, same
// "per-group roster" shape as ContactEntity and CharacterEntity above.
modelBuilder.Entity<ContactTagEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

modelBuilder.Entity<ContactEntity>()
    .HasMany(c => c.Tags)
    .WithMany(t => t.Contacts)
    .UsingEntity(j => j.ToTable("ContactContactTags"));
```

### Query-string tag filter binding (controller signature, mirrors `ShopController.Index`)

```csharp
// Source: pattern from ShopController.cs:14-20 (IList<ItemRarity>? rarity binding)
[HttpGet]
public async Task<IActionResult> Index(IList<int>? tag = null, CancellationToken token = default)
{
    var selectedTagIds = tag ?? [];
    // ... existing IsDmTierAsync / ReadShowHiddenToggle / GetAllContactsWithDetailsAsync / IsVisibleTo ...

    var availableTags = visibleContacts
        .SelectMany(c => c.Tags)
        .DistinctBy(t => t.Id)
        .OrderBy(t => t.Name)
        .ToList();

    var filteredContacts = selectedTagIds.Count > 0
        ? visibleContacts.Where(c => c.Tags.Any(t => selectedTagIds.Contains(t.Id))).ToList()
        : visibleContacts;

    // ... group filteredContacts by Phase 80 category, drop empty groups (D-11) ...
}
```

### `ToggleShowHidden` carrying the filter through its redirect (D-13)

```csharp
// Source: pattern extends ContactsController.cs:298-316
[HttpPost]
[Authorize(Policy = "DungeonMasterOnly")]
[ValidateAntiForgeryToken]
public IActionResult ToggleShowHidden(IList<int>? tag = null)
{
    if (activeGroupContext.ActiveGroupId is not { } groupId)
    {
        return RedirectToAction("Index", "GroupPicker");
    }

    var key = SessionKeys.ShowHiddenContactsKey(groupId);
    var current = HttpContext.Session.GetInt32(key) == 1;
    HttpContext.Session.SetInt32(key, current ? 0 : 1);

    return RedirectToAction(nameof(Index), tag != null && tag.Count > 0 ? new { tag } : null);
}
```
The Show Hidden `<form>` must POST the currently-selected tag ids as hidden inputs for this binding to receive them — e.g. `@foreach (var id in Model.SelectedTagIds) { <input type="hidden" name="tag" value="@id" /> }` inside that form.

## State of the Art

Not applicable in the "old approach superseded" sense — this is genuinely new ground for the codebase (first many-to-many relationship, first CDN-wrapped tag-input widget). The one relevant "state of the art" note: EF Core 10 (the version already in this project) added **named multiple query filters** (`HasQueryFilter("name", ...)`), which is not needed here since `ContactTagEntity` only needs one filter — noted only so the planner doesn't reach for it unnecessarily.

**Deprecated/outdated:** none relevant to this phase.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `DbSet.Find()`/`FindAsync()` can return a tracked entity from the change tracker without re-running the query, bypassing a global query filter for entities already tracked earlier in the same `DbContext` scope. | Common Pitfalls, Pitfall 1 | If wrong (i.e., `Find()` always re-applies filters), the "never use `Find()`" guidance is overly cautious but harmless — `Where(...).ToListAsync()` is a safe, filter-respecting choice either way, so this assumption changes a recommendation's *strength*, not its correctness. Low risk either way; recommend the planner still avoid `Find()` for this lookup since the safe alternative costs nothing. |
| A2 | The developer's local SQL Server (used via `appsettings.json`'s `localhost` connection string) has the same default collation (`SQL_Latin1_General_CP1_CI_AS`) as the Docker `mssql/server` image. Not independently confirmed for any specific developer machine in this session. | Summary, Pitfall 5 | If a local dev SQL Server has a different (e.g., case-sensitive) collation, a plain unique index without `.UseCollation()` would behave inconsistently between dev and prod — dev would allow "Shopkeeper" and "shopkeeper" as two rows, prod would reject the second as a duplicate. The recommended `.UseCollation()` override in Pattern 3 makes this assumption moot regardless of its truth. |
| A3 | Tagify's `originalInputValueFormat` callback fires automatically on every add/remove/edit of a tag (not just once at init), keeping the underlying `<input>`'s value continuously in sync with the comma-joined string. Confirmed via the library's documented API surface (GitHub README) but not exercised in a running browser during this research session. | Architecture Patterns, Pattern 5 | If the sync only happens on blur or form submit rather than on every change, the no-JS fallback contract (D-16) is still satisfied at submit time (which is what matters for the POST), so the risk is limited to a possible UX detail (e.g., a live preview elsewhere on the page reading a stale value) rather than a correctness break. |

## Open Questions

1. **Does `ContactTag` need its own `IContactTagRepository`/`IContactTagService`, or does the upsert/prune logic live inside `IContactRepository`/`IContactService`?**
   - What we know: Phase 80's `ContactCategory` (a simpler one-to-many, not many-to-many) got its own `IContactCategoryService`/`IContactCategoryRepository` per that phase's Integration Points list. Tags are more tightly coupled to the contact write path (upsert-on-save, prune-on-save/delete happen *as part of* saving a contact), which argues for keeping the logic inside `ContactService`/`ContactRepository` rather than a parallel service.
   - What's unclear: whether the filter-vocabulary read (`AvailableTags` for the index) is naturally a `ContactService` concern (it's derived from contacts already loaded) or deserves its own thin repository method for testability.
   - Recommendation: keep tag upsert/prune inside `ContactService`/`ContactRepository` (it's inseparable from the contact save/delete transaction boundary per D-06), and treat "does the vocabulary read need a separate seam" as a planner call once the exact `Index` action shape is drafted.

2. **Exact orphan-prune transaction shape.** D-06 requires pruning on both contact save and contact delete. For save, this means: after updating `contact.Tags` to the submitted set, check every tag the contact *used to have but no longer has* for `Contacts.Count == 0` and delete those rows in the same `SaveChangesAsync` call.
   - What we know: this must happen in the same save as the contact update/delete (not a background job), since D-28 requires asserting it against the database, implying synchronous, deterministic behavior.
   - What's unclear: whether checking `tag.Contacts.Count == 0` requires the tag's `Contacts` collection to be explicitly loaded/tracked (it will be, if the code loads the contact's *previous* tag set before applying the new one — which it must do anyway to compute the diff).
   - Recommendation: load the contact's existing `Tags` navigation before mutating it, diff old-vs-new tag id sets, and after `SaveChangesAsync`, re-check (or check pre-save via a tracked-collection count) any removed tag for zero remaining contacts.

## Environment Availability

No new external service dependency is introduced by this phase — Tagify is fetched from jsDelivr at request time by the browser, exactly like the already-shipped `cropperjs` CDN dependency, so no new environment probe is needed beyond confirming jsDelivr itself is reachable (it is — verified directly in this session via `curl`, HTTP 200 for both the JS and CSS files). `Microsoft.EntityFrameworkCore.SqlServer` 10.0.9, `net10.0`, and SQL Server are all already-provisioned dependencies of the existing app; no version bump or new install step is required.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2, FluentAssertions v8.10.0, NSubstitute v5.3.0 `[VERIFIED: .planning/codebase/TESTING.md]` |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution — required, since tests share one in-memory database per factory) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~ContactsControllerIntegrationTests"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

No `CONTACTTAG-*` IDs exist yet (see Phase Requirements above); this maps D-23–D-30 (the locked test-coverage decisions) to concrete tests instead.

| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-23 | Group A's tags never appear in group B's index/filter/suggestions; cross-group POST attach refused | integration | `dotnet test --filter "FullyQualifiedName~ContactsControllerIntegrationTests"` | ❌ Wave 0 — new test methods, precedent exists at `ContactsControllerIntegrationTests.cs:488` (`Details_ContactInDifferentGroup_ReturnsNotFound`, using `TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2)`) |
| D-24 | Player sees zero tag surfaces; DM-tier sees all of them, on both index views and both Details views | integration | same filter as above | ❌ Wave 0 |
| D-25 | Tag filter narrows only — unrevealed+filtered+Show-Hidden-off contact never appears | integration | same filter as above | ❌ Wave 0 |
| D-26 | Two selected tags return the union (OR); headings survive filter, empty ones suppressed | integration | same filter as above | ❌ Wave 0 — depends on Phase 80's category grouping shape landing first or being stubbed |
| D-27 | A tag borne only by unrevealed contacts is absent from the filter list for a non-DM/Show-Hidden-off viewer, present otherwise | integration | same filter as above | ❌ Wave 0 |
| D-28 | Removing a tag from its last contact deletes the row (asserted via `QuestBoardContext`, not the UI); re-adding mints a fresh id | integration or unit (repository-level) | `dotnet test --filter "FullyQualifiedName~ContactRepositoryTests"` | ❌ Wave 0 — mirrors the existing `ContactRepositoryTests.cs` pattern |
| D-29 | POSTing the plain comma-separated `TagsInput` value (no JS) tags correctly, dedupes case-insensitively, reuses existing rows | integration | `dotnet test --filter "FullyQualifiedName~ContactsControllerIntegrationTests"` | ❌ Wave 0 |
| D-30 | `ToggleShowHidden`'s redirect preserves the selected tag ids | integration | same filter as above | ❌ Wave 0 |
| D-22 (mobile UA) | Mobile markup (chips, filter offcanvas, tag input) only renders for a real mobile User-Agent | integration | `dotnet test --filter "FullyQualifiedName~QuestDetailsMobileCharacterControlTests"` (pattern precedent, not the actual new test) | ❌ Wave 0 — copy the exact `request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent)` pattern from `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs:10-60` |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~ContactsControllerIntegrationTests|FullyQualifiedName~ContactRepositoryTests|FullyQualifiedName~ContactServiceTests"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] New cross-group tag isolation tests in `ContactsControllerIntegrationTests.cs` — pattern precedent already exists at line 488 (`Details_ContactInDifferentGroup_ReturnsNotFound` using `TestDataHelper.SeedCampaignGroupAsync`), no new fixture needed.
- [ ] A real-mobile-User-Agent test for the tag filter offcanvas and chip rendering — pattern precedent exists in `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs` (`MobileUserAgent`/`DesktopUserAgent` constants + `TryAddWithoutValidation`), no new test infrastructure needed, just a new test file/class following that shape.
- [ ] Repository-level orphan-prune assertion (D-28) — mirrors existing `ContactRepositoryTests.cs`; no new fixture needed.
- [ ] `TestDataHelper` likely needs a `CreateTestContactTagAsync(...)` helper analogous to the existing `CreateTestContactAsync`/`CreateTestContactNoteAsync` (`TestDataHelper.cs:166-219`) to seed tags directly in tests without going through the controller POST path every time.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Unchanged — this phase adds no new authentication surface. |
| V3 Session Management | No | Filter state is explicitly query-string, not session (D-09) — no new session key. |
| V4 Access Control | Yes | `[Authorize(Policy = "DungeonMasterOnly")]` on every tag write (D-02), `ViewerIsDmTier` gate on every tag read/render surface (D-01/D-24) — both are existing, proven policy/flag patterns, reused unchanged. |
| V5 Input Validation | Yes | Tag name length cap (~30 chars, Claude's Discretion), server-side parse of the comma-separated fallback (split/trim/drop-empty/dedupe, D-16) regardless of client JS, `[StringLength(30)]` data annotation mirroring the existing `ContactViewModel` pattern. |
| V6 Cryptography | No | Not applicable to this phase. |
| V9 (custom: Multi-Tenancy Data Isolation — this app's dominant threat class per PROJECT.md) | Yes | Fail-closed `HasQueryFilter` on `ContactTagEntity` (D-05) + filtered-query-not-`Find()` lookup on every write path (see Common Pitfalls, Pitfall 1) + `IgnoreQueryFilters()` forbidden everywhere in this phase (D-10/D-23). |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cross-tenant tag attachment — a DM in group A submits a tag id belonging to group B on a Contact POST, hoping to attach or discover it | Elevation of Privilege / Information Disclosure | Resolve every submitted tag id through a query that respects `ContactTagEntity`'s `HasQueryFilter` (never `Find()`, never `IgnoreQueryFilters()`); a foreign-group id must silently fail to resolve (D-06's "match nothing" contract), never 404/error (which would itself leak "an id in this range exists but isn't yours"). |
| Tag-name category-heading-style disclosure via the filter's suggestion list | Information Disclosure | D-12's "derive vocabulary from visible-but-unfiltered contacts, no separate query" design is itself the mitigation — a tag with zero visible-to-this-viewer contacts structurally cannot appear in the suggestion/whitelist JSON sent to the browser, since that JSON is built server-side from the same already-filtered set. |
| XSS via a tag name rendered unsanitized | Tampering | D-18 mandates plain Razor `@`-escaped output, never through `IMarkdownService` — standard ASP.NET Core auto-encoding is sufficient as long as `Html.Raw`/`IMarkdownService` are never used for tag name display. The one place raw output *is* intentionally used (the Tagify whitelist JSON passed to `Html.Raw(Json.Serialize(...))` in Pattern 5's example) is safe because `Json.Serialize` HTML/JS-escapes its output for embedding in a `<script>` block — this is standard ASP.NET Core `System.Text.Json` behavior, not a hand-rolled escape. |

## Sources

### Primary (HIGH confidence)

- Direct codebase reads: `QuestBoardContext.cs` (all 18 existing `HasQueryFilter` calls, lines 326-472), `ContactsController.cs` (full file), `ShopController.cs` + `ShopService.cs` + `Shop/Index.cshtml` + `Shop/Index.Mobile.cshtml`, `ContactRepository.cs`, `IContactRepository.cs`/`IContactService.cs`, `ContactEntity.cs`/`Contact.cs`, `image-crop.js` + its `Create.cshtml` call site, `EntityProfile.cs`/`ViewModelProfile.cs` (Contact mappings), `QuestRepository.cs` (`AsSplitQuery` precedent), `AgendaController.cs` (intersect-narrows-never-widens precedent), `docker-compose.yml`, `appsettings.json`, `QuestBoard.Repository.csproj`, `AuthenticationHelper.cs`, `ContactsControllerIntegrationTests.cs`, `QuestDetailsMobileCharacterControlTests.cs`, `TestDataHelper.cs`.
- `npm view @yaireo/tagify version` / `.time.created` / `.repository.url` — direct registry query, executed in this session.
- `gsd-tools query package-legitimacy check --ecosystem npm "@yaireo/tagify"` — verdict `OK`, executed in this session.
- `curl` download + `openssl dgst -sha384` of `https://cdn.jsdelivr.net/npm/@yaireo/tagify@4.38.0/dist/tagify.min.js` and `.../tagify.css` — SRI hashes computed directly from the served bytes in this session, not copied from a third party.
- Microsoft Learn — [Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters) — fetched in full this session; the "Query filters and required navigations" section is the authoritative source for the required-vs-collection-navigation distinction this research's core recommendation rests on.

### Secondary (MEDIUM confidence)

- Microsoft Learn — [Many-to-many relationships - EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many) — fetched in full this session; source for `UsingEntity`/named-join-table syntax.
- `github.com/yairEO/tagify` README — fetched via WebFetch this session; source for `originalInputValueFormat`, `whitelist`, `enforceWhitelist`, and the CSS custom-property override surface.
- `github.com/microsoft/mssql-docker` issues (#629, #338) and related Microsoft Learn/community pages — cross-referenced for the Docker image's default collation being `SQL_Latin1_General_CP1_CI_AS` when `MSSQL_COLLATION` is unset.

### Tertiary (LOW confidence)

- The `Find()`/`FindAsync()` tracked-entity-bypasses-filter claim (Assumptions Log A1) — based on general EF Core training knowledge, not independently re-confirmed against EF Core source or an authoritative doc page in this session; the recommended mitigation (avoid `Find()` for this specific lookup) is safe regardless of whether the underlying claim is exactly right.

## Metadata

**Confidence breakdown:**
- Standard stack (Tagify version/hash/features, EF Core version): HIGH — every version number, SRI hash, and API surface claim was independently confirmed via `npm view`, `curl`+`openssl`, the package-legitimacy gate, or the library's own official README, in this session.
- Architecture (many-to-many + query filter interaction, collation, split-query): HIGH for the parts grounded directly in Microsoft's official EF Core docs (required-vs-collection navigation distinction, `UsingEntity` syntax, `.UseCollation()`); MEDIUM for the join-table-needs-no-filter-of-its-own conclusion, which is a sound extrapolation from those docs plus this codebase's own multi-tenancy pattern but was not tested against a running database in this session — this is exactly why D-23's integration test exists, and the planner should treat that test as load-bearing, not a formality.
- Pitfalls: HIGH for the ones with direct codebase precedent (`AsSplitQuery`, `ToggleShowHidden`'s redirect, D-12's vocabulary-ordering trap — all three point at concrete existing code); LOW-MEDIUM for the `Find()` bypass claim specifically (flagged in the Assumptions Log).

**Research date:** 2026-08-30
**Valid until:** 30 days for the EF Core/architecture guidance (stable API surface, unlikely to change); 7 days for the pinned Tagify version/SRI hash specifically — re-run `npm view @yaireo/tagify version` before implementation if this research is more than a couple of weeks old, since a version bump changes both the CDN URL and the SRI hash.
