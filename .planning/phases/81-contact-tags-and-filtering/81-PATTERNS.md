# Phase 81: Contact Tags and Filtering - Pattern Map

**Mapped:** 2026-08-30
**Files analyzed:** 24 (new + modified)
**Analogs found:** 22 / 24 (2 explicitly have no local analog — see "No Analog Found")

**Sequencing note:** Phase 80 (Contact Categories) has a CONTEXT.md but **no plans and no code**. Today's `Index.cshtml` / `Index.Mobile.cshtml` render a flat `contact-grid` / list of `contact-member-row`s with no category headings and no `IsVisibleTo`-derived grouping helper. Everywhere below that references "Phase 80's category headings / grouping" (D-11, D-26), that grouping code **does not exist yet** in the file. The planner must either sequence Phase 80 before Phase 81, or have Phase 81's plan add the tag filter/chips against the current flat markup and note where a future category-grouping pass will need to interleave. Treat every excerpt below from `Index.cshtml`/`Index.Mobile.cshtml` as "current flat-list shape," not "Phase-80-augmented shape."

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/ContactTagEntity.cs` | model (entity) | CRUD | `QuestBoard.Repository/Entities/ContactEntity.cs` | role-match (single-FK, not M2M — see below) |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (additions) | config (EF model building) | CRUD | same file, `ContactEntity`/`ContactNoteEntity` filters at lines ~402-419 | exact (filter shape) / no-analog (M2M `UsingEntity` + `.UseCollation()`) |
| `QuestBoard.Domain/Models/Contact.cs` (+ `ContactTag` model) | model (domain) | CRUD | `ContactNote` class in the same file | role-match |
| `QuestBoard.Domain/Interfaces/IContactRepository.cs` / `IContactService.cs` (extended, no new interfaces per RESEARCH open-question recommendation) | service/repository interface | CRUD | existing Contact interfaces | exact |
| `QuestBoard.Repository/ContactRepository.cs` | repository | CRUD | same file's existing `GetAllContactsWithDetailsAsync` / `GetContactWithDetailsAsync` | exact |
| `QuestBoard.Domain/Services/ContactService.cs` | service | CRUD (upsert-by-name + orphan prune) | same file's existing create/update/delete methods | exact (CRUD) / no-analog (upsert-by-name + prune-on-orphan is new) |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` | config (mapping) | transform | existing `Contact`↔`ContactEntity` / `ContactNote` map | exact |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` | config (mapping) | transform | existing `Contact`↔`ContactViewModel` map | exact |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (`Index`, `Create`/`Edit` POST, `Details`, `ToggleShowHidden`) | controller | request-response | same file, existing `Index`/`ToggleShowHidden`/`IsVisibleTo` | exact (visibility gate) / role-match (query-string filter binding → copy from `ShopController`) |
| `QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs` | model (ViewModel) | transform | `QuestBoard.Service/ViewModels/ShopViewModels/ShopIndexViewModel.cs` | exact |
| `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` | model (ViewModel) | transform | same file, existing fields | exact |
| `QuestBoard.Service/Views/Contacts/Index.cshtml` (filter row + chips) | component (Razor view) | request-response | `QuestBoard.Service/Views/Shop/Index.cshtml` (`shop-filter-row`, empty-state branches) | exact (filter UI) |
| `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml` (offcanvas + chips) | component (Razor view) | request-response | `QuestBoard.Service/Views/Shop/Index.Mobile.cshtml` (`shopFilterOffcanvas`) | exact |
| `QuestBoard.Service/Views/Contacts/Create.cshtml` / `Edit.cshtml` (tag input) | component (Razor view) | request-response | same files' existing `ContactImageFile` crop field (`@section Scripts`, `image-crop.js` call site) | exact (CDN-wrap shape) |
| `QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml` / `Edit.Mobile.cshtml` (tag input) | component (Razor view) | request-response | desktop counterparts, same pattern | exact |
| `QuestBoard.Service/Views/Contacts/Details.cshtml` / `Details.Mobile.cshtml` (tag line) | component (Razor view) | request-response | same files' existing `SubLocation` paragraph | exact |
| `QuestBoard.Service/wwwroot/js/contact-tags.js` | utility (client init module) | event-driven | `QuestBoard.Service/wwwroot/js/image-crop.js` (explicit-init) or `markdown-editor.js` (self-init) | exact |
| `QuestBoard.Service/wwwroot/css/contacts.css` / `contacts.mobile.css` (chip/filter styles) | config (styles) | n/a | same files' existing `.hidden-badge`, `.contact-placeholder`, `.contact-card` rules; `Shop/shop.css`'s `.shop-filter-row`/`.filter-apply-btn` | exact |
| `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` (new tests) | test | CRUD/request-response | same file, `Details_ContactInDifferentGroup_ReturnsNotFound` (line 489) | exact |
| `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` (+ `CreateTestContactTagAsync`) | test utility | CRUD | same file, `CreateTestContactAsync` / `CreateTestContactNoteAsync` (lines 166-219) | exact |
| `QuestBoard.IntegrationTests/Mobile/*ContactTagsMobileTests.cs` (new file) | test | request-response | `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs` (lines 10-60) | exact |
| `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` (new orphan-prune tests) | test | CRUD | same file, existing tests | exact |
| `QuestBoard.UnitTests/Services/ContactServiceTests.cs` (new upsert/prune tests) | test | CRUD | same file, existing tests | exact |
| `QuestBoard.Repository/Migrations/*_AddContactTags.cs` | migration | CRUD | any recent migration adding an entity + unique index (e.g. Phase 80's category migration if it lands first, else any `AddXxx` migration) | role-match |

---

## Pattern Assignments

### `QuestBoard.Repository/Entities/ContactTagEntity.cs` (model, CRUD)

**Analog:** `QuestBoard.Repository/Entities/ContactEntity.cs` (full file read above)

**Full analog for structural shape:**
```csharp
// QuestBoard.Repository/Entities/ContactEntity.cs
[Table("Contacts")]
public class ContactEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    // ...
    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
```

**Copy this shape** (`[Table]`, `[Key]`/`DatabaseGeneratedOption.Identity`, `[Required][StringLength]`, `GroupId` + `[ForeignKey(nameof(GroupId))] Group` navigation) for `ContactTagEntity`, per RESEARCH.md's Code Examples section (`Id`, `Name` capped at 30 chars, `GroupId`, `Group` nav, `Contacts` skip-navigation collection). No structural changes needed beyond swapping field names.

---

### `QuestBoard.Repository/Entities/QuestBoardContext.cs` (config, CRUD)

**Analog:** same file, `ContactEntity` filter block (lines ~402-419) — read in full above.

**Fail-closed filter pattern to copy verbatim, substituting `ContactTagEntity`:**
```csharp
// ContactEntity deliberately does NOT offer a SuperAdmin cross-group view like Quest/ShopItem
// do above — same "per-group roster" shape as CharacterEntity. An empty Contact list when no
// group is selected is the intended behavior here, not an oversight.
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

**The general warning comment this pattern is built around** (appears once, above the whole `QuestEntity`/`ShopItemEntity` block, lines ~344-352):
```csharp
// Global query filters for group isolation.
// ...
// A null ActiveGroupId (no group selected yet, or a session that never picked one) must
// return zero rows, never every group's rows merged together — the caller has to pick a
// group before any group-scoped data is servable, full stop.
// Lambda closes over activeGroupContext instance — re-evaluated per query, not at startup
// CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here.
//           That captures the value once (null at model-build time). Always reference the service.
```

`ContactTagEntity`'s `HasQueryFilter` must dereference `activeGroupContext.ActiveGroupId` inline exactly like these — do not assign it to a local first.

**No SuperAdmin escape hatch** — the exact comment pattern to reuse for `ContactTagEntity`'s own comment (paraphrased, not copied verbatim per CLAUDE.md's "no phase/decision IDs in comments" rule):
> "ContactEntity deliberately does NOT offer a SuperAdmin cross-group view... An empty [X] list when no group is selected is the intended behavior here, not an oversight."

**No local many-to-many analog exists.** This app has zero `UsingEntity`/skip-navigation relationships today (confirmed by RESEARCH.md's grep of all 18 existing `HasQueryFilter` calls — every one scopes a direct `GroupId` column or a required one-to-many/one-to-one navigation, never a M2M). The closest structural precedent for "add a fail-closed filter to a brand-new entity" is the pattern above; the M2M wiring itself (`HasMany().WithMany().UsingEntity(j => j.ToTable(...))`) and the `.UseCollation(...)` unique-index call have no in-repo precedent — copy them from RESEARCH.md's Pattern 2/Pattern 3 code examples (sourced from official EF Core docs), not from any local file.

---

### `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (controller, request-response)

**Analog for query-string filter binding:** `QuestBoard.Service/Controllers/Shop/ShopController.cs` `Index` (lines 1-58, read in full above).

```csharp
// QuestBoard.Service/Controllers/Shop/ShopController.cs:13-20
[HttpGet]
public async Task<IActionResult> Index(
    ItemType? type = null,
    IList<ItemRarity>? rarity = null,
    string? sort = null,
    string? search = null,
    int page = 1,
    CancellationToken token = default)
```

Copy this exact shape for the new `tag` parameter — `IList<int>? tag = null` — no `[FromQuery]`, no manual `Request.Query` parsing anywhere in this codebase (confirmed by RESEARCH.md).

**Analog for the visibility gate, already in `ContactsController.cs` today (unchanged, reuse as-is):**
```csharp
// ContactsController.cs:30-34 (current Index action)
var viewerIsDmTier = await IsDmTierAsync();
var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

var allContacts = await contactService.GetAllContactsWithDetailsAsync(token);
var visibleContacts = allContacts.Where(c => IsVisibleTo(c, currentUser.Id, includeHidden));
```
Insert the D-12 vocabulary derivation and D-08 OR-filter application **after** this line and **before** any Phase-80 grouping — see RESEARCH.md's Pitfall 3 for the exact ordering trap (deriving `AvailableTags` from the post-filter set instead of `visibleContacts`).

**`ToggleShowHidden` — the method that must change (current shape, `ContactsController.cs:301` area):**
```csharp
[HttpPost]
[Authorize(Policy = "DungeonMasterOnly")]
[ValidateAntiForgeryToken]
public IActionResult ToggleShowHidden()
{
    // ... flips session key ...
    return RedirectToAction(nameof(Index));   // <-- drops query string today; must carry `tag` (D-13)
}
```
RESEARCH.md's recommended replacement (`IList<int>? tag = null` parameter, `RedirectToAction(nameof(Index), tag != null && tag.Count > 0 ? new { tag } : null)`) is the exact fix — apply it here.

**Never use `Find()`/`FindAsync()` to resolve a submitted tag id** (write-path safety, RESEARCH.md Pitfall 1) — always `Where(t => ids.Contains(t.Id)).ToListAsync()`, so a foreign-group id silently comes back missing (D-06's "match nothing" contract) rather than bypassing the query filter via the change tracker.

---

### `QuestBoard.Domain/Services/ContactService.cs` / `QuestBoard.Repository/ContactRepository.cs` (service/repository, CRUD)

**Split-query analog:** `QuestBoard.Repository/QuestRepository.cs:88-99` (read in full above):
```csharp
// QuestRepository.cs:90-96
// Two independent collection Includes (ProposedDates and PlayerSignups) in a single
// query force EF to cross-join both collections, multiplying row count combinatorially
// and triggering the MultipleCollectionIncludeWarning. AsSplitQuery() issues one query
// per collection instead, avoiding the row-count blowup without changing the loaded shape.
var entity = await DbContext.Quests
    .AsSplitQuery()
    .Include(q => q.ProposedDates)
        .ThenInclude(pd => pd.PlayerVotes)
            .ThenInclude(pv => pv.PlayerSignup)
                .ThenInclude(ps => ps!.Player)
    .Include(q => q.PlayerSignups)
```
`ContactRepository.GetAllContactsWithDetailsAsync` / `GetContactWithDetailsAsync` currently `.Include(c => c.Notes)` only; adding `.Include(c => c.Tags)` beside it is exactly this shape — add `.AsSplitQuery()` to both methods, same rationale comment style, when the second collection Include is introduced.

**No local analog for "upsert-by-name + orphan-prune-on-save/delete."** This is new logic; RESEARCH.md's Open Questions section (items 1 and 2) already worked through the recommended shape — load the contact's existing `Tags` navigation before mutating it, diff old-vs-new tag id sets, upsert unmatched names via a filtered query (never `Find()`), and after applying the new set, check every removed tag for `Contacts.Count == 0` and delete it in the same `SaveChangesAsync` call.

---

### `QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs` (ViewModel, transform)

**Analog:** `ShopIndexViewModel`'s filter-state fields (`SelectedRarities`, `SelectedSort`, `SearchQuery`, `HasActiveFilters`, confirmed present via `ShopController.Index`'s assignment block above). Grow `ContactsIndexViewModel` with `SelectedTagIds` (`IList<int>`), `AvailableTags` (`IList<ContactTagViewModel>` or similar), and a computed `HasActiveFilters => SelectedTagIds.Count > 0` in the same shape.

---

### `QuestBoard.Service/Views/Contacts/Index.cshtml` (component, request-response)

**Analog:** `QuestBoard.Service/Views/Shop/Index.cshtml` — filter form (line 206), checkbox labels (line 218), Apply/Clear buttons (lines 260, 268, 280), two-branch empty state (lines 367-378).

```html
<!-- Shop/Index.cshtml:206 -->
<form method="get" action="@Url.Action("Index", "Shop")" class="shop-filter-row">
```
```html
<!-- Shop/Index.cshtml:218 -->
<label class="filter-check-label">
```
```html
<!-- Shop/Index.cshtml:260 -->
<button type="submit" class="btn btn-sm filter-apply-btn">
```
```html
<!-- Shop/Index.cshtml:268 -->
<button ... class="btn btn-sm filter-clear-btn">
```
```html
<!-- Shop/Index.cshtml two-branch empty state, ~367-378 -->
<h3>No items match your search</h3>
<a href="@BuildTabUrl(Model.SelectedType, Model.SelectedRarities, Model.Selec...">...</a>
...
else if (Model.HasActiveFilters)
{
    <h3>No items match your filters</h3>
    <a href="@Url.Action("Index", "Shop")" class="btn filter-apply-btn">...
}
```
Per `81-UI-SPEC.md`, name the new equivalents `.contact-filter-row`, `.contact-filter-check-label`, `.contact-filter-apply-btn`, `.contact-filter-clear-btn` (own CSS classes in `contacts.css`, not reused Shop classes) — copy the structural pattern, not the class names. Wrap the whole row in the existing `ViewerIsDmTier` conditional (`ContactsController` already exposes this flag on the ViewModel).

**D-19 disabled state** (no local Contacts precedent — this exact conditional-empty-state-container idea is new to Contacts, but the concept ("stays visible/discoverable, disabled with hint text") is Phase 80 D-07's logic per CONTEXT.md — Phase 80 has no code yet, so there is no in-repo view to copy for this specific branch; build it directly from the `81-UI-SPEC.md` Component Inventory §2 description (same outer `.contact-filter-row` container, contents swapped for a single disabled-hint line, `opacity: 0.6`, `cursor: not-allowed`).

---

### `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml` (component, request-response)

**Analog:** `QuestBoard.Service/Views/Shop/Index.Mobile.cshtml`, lines 60-90 (read above).

```csharp
// Shop/Index.Mobile.cshtml:60
bool hasActiveFilters = Model.SelectedRarities.Any() || Model.SelectedSort != ...
```
```html
<!-- Shop/Index.Mobile.cshtml:70-84 -->
<button class="btn btn-warning w-100 mb-3" type="button" data-bs-toggle="offcanvas" data-bs-target="#shopFilterOffcanvas">
    <i class="fas fa-filter me-2"></i>Filter &amp; Sort
    @if (hasActiveFilters)
    {
        <span class="badge bg-dark ms-1">Active</span>
    }
</button>

<!-- Filter & Sort offcanvas drawer -->
<div class="offcanvas offcanvas-bottom" id="shopFilterOffcanvas" tabindex="-1">
    <div class="offcanvas-header">
        <h5 class="offcanvas-title">Filter &amp; Sort</h5>
        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="offcanvas"></button>
    </div>
    <div class="offcanvas-body">
```
Copy this exactly for `#contactFilterOffcanvas` — trigger button label "Filter Tags" (per UI-SPEC copy contract, not "Filter & Sort"), same `badge bg-dark` "Active" treatment, same `offcanvas offcanvas-bottom` structure. Per UI-SPEC §3, the trigger sits in its own full-width row below `.contact-toggle-row`, not inside it.

---

### `QuestBoard.Service/Views/Contacts/Create.cshtml` / `Edit.cshtml` (+ Mobile variants) — tag input (component, request-response)

**Analog:** `image-crop.js` and its call site at `Create.cshtml:125-133` (per CONTEXT.md's canonical refs — file not re-read in full here since the exact excerpt is already pinned by RESEARCH.md's Pattern 5, reproduced below for the executor's direct use):

```html
<!-- RESEARCH.md Pattern 5 — exact shape to reproduce, modeled on Create.cshtml:125-133's cropperjs precedent -->
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

**IMPORTANT — re-verify before use:** RESEARCH.md flags this hash/version pinning as valid for only 7 days from 2026-08-30 ("re-run `npm view @yaireo/tagify version` before implementation if this research is more than a couple of weeks old"). The executor must re-verify the version/SRI hash if this phase is implemented outside that window.

**`markdown-editor.js`'s no-bundler header comment** (the alternative self-init convention, for reference if the planner picks self-init over explicit-init):
Not independently re-read this session; `image-crop.js`'s header (already captured above) documents the same "no module, no bundler, per-view `<script>` include" convention and is the house style to follow either way.

---

### `QuestBoard.Service/wwwroot/js/contact-tags.js` (utility, event-driven)

**Analog:** `QuestBoard.Service/wwwroot/js/image-crop.js` header comment (read in full above):
```javascript
// Shared client-side crop pipeline for every photo-upload form (character, contact, DM profile).
// Loaded per-view via a plain <script> include (matching site.js's no-module, no-bundler
// convention) and initialized per-view by calling initImageCrop({...}) with that view's element IDs.
```
Copy this header-comment convention (no-module, no-bundler, plain `<script>` include, explicit-init function) for `contact-tags.js`'s own header, substituting the tag-input purpose. RESEARCH.md's Pattern 5 already supplies the full body:
```javascript
function initContactTags(config) {
    const input = document.getElementById(config.inputId);
    if (!input) {
        return; // safe no-op, matches initImageCrop's defensive-include convention
    }
    new Tagify(input, {
        whitelist: config.whitelist || [],
        enforceWhitelist: false,
        originalInputValueFormat: values => values.map(v => v.value).join(', '),
        maxTags: undefined
    });
}
```

---

### `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` (test, CRUD/request-response)

**Analog:** same file, `Details_ContactInDifferentGroup_ReturnsNotFound` (lines 489-505, read in full above):
```csharp
// (9) Cross-tenant IDOR — a Details/{id} GET for a Contact belonging to another group
// returns 404.
[Fact]
public async Task Details_ContactInDifferentGroup_ReturnsNotFound()
{
    await TestDataHelper.ClearDatabaseAsync(factory.Services);
    await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

    var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
    var otherGroupOwner = await AuthenticationHelper.CreateTestUserAsync(
        factory.Services, "contact_crossgroup_owner", "contact_crossgroup_owner@example.com", "Test123!", "Other Group Owner");
    var contact = await TestDataHelper.CreateTestContactAsync(
        factory.Services, otherGroupOwner.Id, "Other Group's Contact", groupId: 2);

    var response = await adminClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```
D-23's cross-group tag isolation test copies this exact shape: `ClearDatabaseAsync` → `SeedCampaignGroupAsync(factory.Services, 2)` → create a tag/contact in group 2 via the new `CreateTestContactTagAsync` helper → assert group 1's index/filter list/vocab never surfaces it, and a POST attaching a group-1 contact to the group-2 tag id is silently dropped (not a 404/error, per D-06).

---

### `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — new `CreateTestContactTagAsync` (test utility, CRUD)

**Analog:** `CreateTestContactAsync` / `CreateTestContactNoteAsync` (lines 166-219, read in full above):
```csharp
public static async Task<ContactEntity> CreateTestContactAsync(
    IServiceProvider services,
    int createdByUserId,
    string name = "Test Contact",
    string? townCity = null,
    string? subLocation = null,
    bool isRevealed = false,
    int groupId = 1,
    byte[]? imageData = null)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

    var contact = new ContactEntity
    {
        Name = name,
        TownCity = townCity,
        SubLocation = subLocation,
        CreatedByUserId = createdByUserId,
        IsRevealed = isRevealed,
        GroupId = groupId,
        CreatedAt = DateTime.UtcNow,
        ProfileImage = imageData == null ? null : new ContactImageEntity { OriginalImageData = imageData }
    };

    context.Contacts.Add(contact);
    await context.SaveChangesAsync();

    return contact;
}

public static async Task<ContactNoteEntity> CreateTestContactNoteAsync(
    IServiceProvider services,
    int contactId,
    int authorUserId,
    string text = "Test note")
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

    var note = new ContactNoteEntity
    {
        ContactId = contactId,
        AuthorUserId = authorUserId,
        Text = text,
        CreatedAt = DateTime.UtcNow
    };

    context.Set<ContactNoteEntity>().Add(note);
    await context.SaveChangesAsync();

    return note;
}
```
`CreateTestContactTagAsync(services, groupId, name, params ContactEntity[] contacts)` should follow the same `using var scope` / direct-`DbContext`-write shape, and — because this is a M2M seed — additionally attach the tag to zero or more contacts via the skip-navigation collection before `SaveChangesAsync()`, since there is no `.Set<T>().Add()` shortcut for join rows once EF owns them implicitly.

---

### `QuestBoard.IntegrationTests/Mobile/*ContactTagsMobileTests.cs` (test, request-response)

**Analog:** `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs`, lines 10-60 (read in full above):
```csharp
private const string MobileUserAgent =
    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

private const string DesktopUserAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

private async Task<(HttpResponseMessage Response, string Html)> GetQuestDetailsAsync(
    int questId, string userAgent, AuthenticationHeaderValue? authorization = null)
{
    var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{questId}");
    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
    if (authorization != null)
    {
        request.Headers.Authorization = authorization;
    }
    var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
    var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    return (response, html);
}
```
Copy the `MobileUserAgent`/`DesktopUserAgent` constants and the `TryAddWithoutValidation("User-Agent", ...)` + manual `HttpRequestMessage`/`SendAsync` pattern verbatim — this is the only way to exercise `MobileDetectionMiddleware`'s view selection; devtools/viewport emulation never reaches it (per `project_mobile_view_ua` memory and CLAUDE.md/PROJECT.md's recorded drift bug). Point the request at `/Contacts` with the tag filter query string and assert the offcanvas/chip markup differs between the two UAs (D-22).

---

## Shared Patterns

### Fail-closed group query filter
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs` (warning comment ~344-352, `ContactEntity` filter ~405-410)
**Apply to:** `ContactTagEntity`'s `HasQueryFilter`.
```csharp
modelBuilder.Entity<ContactTagEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```
Dereference `activeGroupContext.ActiveGroupId` inline in every lambda — never capture it into a local.

### DM-tier audience gate
**Source:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`, `IsDmTierAsync()`/`ReadShowHiddenToggle()` (lines ~440-460) and the existing `ViewerIsDmTier` flag already on `ContactsIndexViewModel`.
**Apply to:** every new tag surface (filter row/offcanvas, chips on both index views, tag line on both Details views, tag-entry field on all four Create/Edit views) — reuse the *same* `ViewerIsDmTier` conditional the Show Hidden toggle and Create button already use. Do not introduce a second, parallel DM-tier check (RESEARCH.md's Anti-Pattern 3 — this class of drift is exactly what produced the `Characters/Edit.cshtml classIndex` regression PROJECT.md records).

### Query-string filter binding, no manual parsing
**Source:** `QuestBoard.Service/Controllers/Shop/ShopController.cs:14-20`
**Apply to:** `ContactsController.Index` and `ToggleShowHidden` — plain `IList<int>? tag = null` parameter binding, no `[FromQuery]`, no `Request.Query` reads.

### CDN library + thin init module, SRI-pinned
**Source:** `image-crop.js` + `Create.cshtml:125-133` (cropperjs)
**Apply to:** Tagify wiring on all four Create/Edit views — `integrity`/`crossorigin` on both CDN tags, `asp-append-version="true"` on the local module, explicit-init call (`initContactTags({...})`) in an inline `<script>` block beneath the module include.

### Split query for a second collection Include
**Source:** `QuestBoard.Repository/QuestRepository.cs:88-99` (`GetQuestWithManageDetailsAsync`)
**Apply to:** `ContactRepository`'s detail-fetch methods once `.Include(c => c.Tags)` sits beside the existing `.Include(c => c.Notes)`.

### Two-group cross-tenant integration test shape
**Source:** `ContactsControllerIntegrationTests.cs:489-505` (`Details_ContactInDifferentGroup_ReturnsNotFound`)
**Apply to:** D-23's tag isolation tests — `ClearDatabaseAsync` → `SeedCampaignGroupAsync(factory.Services, 2)` → seed cross-group data → assert isolation/silent-drop, not error.

### Real mobile User-Agent, not devtools emulation
**Source:** `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs:10-60`
**Apply to:** every D-22 mobile-markup assertion for this phase's filter offcanvas, chips, and tag-entry widget.

---

## No Analog Found

| File/Concern | Role | Data Flow | Reason |
|---|---|---|---|
| M2M relationship wiring (`HasMany().WithMany().UsingEntity(...)`) in `QuestBoardContext.cs` | config | CRUD | This is the app's first many-to-many relationship. Every existing `HasQueryFilter` (18 total) scopes a direct `GroupId` column or a required one-to-many/one-to-one navigation — confirmed by RESEARCH.md's full grep of the file. No local file demonstrates `UsingEntity`, a skip-navigation, or a join-table-scoped query. Use RESEARCH.md's Pattern 2 (sourced from official EF Core docs) instead of a codebase analog. |
| `.UseCollation("SQL_Latin1_General_CP1_CI_AS")` column-level override | config | CRUD | No existing entity in this codebase declares a column-level collation override — every other case-insensitive-ish comparison in the app relies on ambient collation. Use RESEARCH.md's Pattern 3 (sourced from official EF Core docs) instead. |
| D-19's disabled-filter-row empty state on `Index.cshtml` | component | request-response | Conceptually mirrors Phase 80 D-07's "stay visible, disabled, with hint text" logic, but Phase 80 has no code yet (CONTEXT.md only) — there is no view file to read for the actual markup. Build directly from `81-UI-SPEC.md` Component Inventory §2's description; do not wait on Phase 80 landing first unless the planner explicitly sequences it that way. |

---

## Metadata

**Analog search scope:** `QuestBoard.Repository/Entities/QuestBoardContext.cs`, `QuestBoard.Service/Controllers/Shop/ShopController.cs` + `Views/Shop/*.cshtml`, `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`, `QuestBoard.Repository/Entities/ContactEntity.cs`, `QuestBoard.Domain/Models/Contact.cs`, `QuestBoard.Repository/QuestRepository.cs`, `QuestBoard.Service/wwwroot/js/image-crop.js`, `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`, `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs`, `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs`.
**Files scanned:** ~14 direct reads/greps, all read once (no re-reads of an already-loaded range).
**Pattern extraction date:** 2026-08-30
**RIP MCP availability:** Not exposed in this session (no `mcp__rip__*` tools present); fell back to `Grep`/`Read`/`Bash`, matching RESEARCH.md's own documented fallback for this same phase.
