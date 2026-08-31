# Phase 80: Contact Categories - Pattern Map

**Mapped:** 2026-08-30
**Files analyzed:** 27 (14 new, 13 modified)
**Analogs found:** 27 / 27

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/ContactCategoryEntity.cs` | model (entity) | CRUD | `QuestBoard.Repository/Entities/ContactEntity.cs` | exact (sibling group-scoped entity) |
| `QuestBoard.Domain/Models/ContactCategory.cs` | model (domain) | CRUD | `QuestBoard.Domain/Models/Contact.cs` (referenced, not read this pass — same shape as `ContactEntity`) | exact |
| `QuestBoard.Domain/Interfaces/IContactCategoryRepository.cs` + `QuestBoard.Repository/ContactCategoryRepository.cs` | service/repository | CRUD | `IContactRepository` / `QuestBoard.Repository/ContactRepository.cs` | role-match (Contact repo is heavier — image/notes handling — Category repo is a plain CRUD + reorder subset) |
| `QuestBoard.Domain/Interfaces/IContactCategoryService.cs` + `QuestBoard.Domain/Services/ContactCategoryService.cs` | service | CRUD | `IContactService` / `QuestBoard.Domain/Services/ContactService.cs` | role-match |
| `QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs` | controller | request-response | `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs` | exact (named analog, D-06) |
| `QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml` + `Manage.Mobile.cshtml` | view | request-response | `Views/ShopManagement/Index.cshtml` (+ `.Mobile.cshtml`) and `Areas/Platform/Views/Group/Index.cshtml` | exact (per UI-SPEC, markup already drafted) |
| `QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml` | view | request-response | `Areas/Platform/Views/Group/Edit.cshtml` (rename-only form) | exact |
| `QuestBoard.Service/ViewModels/ContactViewModels/ContactCategoryGroupViewModel.cs` | model (viewmodel) | transform | `QuestBoard.Service/ViewModels/ShopViewModels/ShopCategoryViewModel.cs` | shape-only — **dead code, see note below** |
| `QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs` | test | request-response | `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` (cross-group section, lines 351-398) | exact |
| `QuestBoard.UnitTests/Repository/ContactCategoryRepositoryTests.cs` | test | CRUD | `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` | role-match |
| `QuestBoard.UnitTests/Services/ContactCategoryServiceTests.cs` | test | CRUD | `QuestBoard.UnitTests/Services/ContactServiceTests.cs` | role-match |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (modified) | config | CRUD | itself — `ContactEntity`'s own filter block (lines 426-432) and `Contact → Group` FK block (256-261) | exact (copy-with-name-swap) |
| `ContactEntity.cs`, `Contact.cs`, `ContactViewModel.cs` (modified — add `CategoryId`/`CategoryName`) | model | CRUD | same files, `GroupId`/`Group` navigation shape already on `ContactEntity` | exact |
| `ContactsController.cs` (modified `Index`, `Create`, `Edit`) | controller | request-response | itself — `Index` (lines 22-50), `IsVisibleTo` (469) | exact |
| `ContactsIndexViewModel.cs` (modified) | viewmodel | transform | itself; grouping shape from `ShopCategoryViewModel` | exact + shape-only |
| Both AutoMapper profiles (modified) | config | transform | `EntityProfile.cs:114-129` (Contact block), `ViewModelProfile.cs:81-92` (Contact block) | exact |
| `Views/Contacts/{Index,Create,Edit,Details}.cshtml` + `.Mobile.cshtml` (modified) | view | request-response | themselves, per 80-UI-SPEC.md markup | exact |
| `wwwroot/css/contacts.css`, `contacts.mobile.css` (modified) | config (styles) | — | themselves + `modern-card.css` | exact |
| `ContactsControllerIntegrationTests.cs` (modified) + `TestDataHelper` (modified) | test | request-response | itself, `CreateTestContactAsync` (lines 166-193) | exact |
| New EF migration | migration | batch | any recent migration under `QuestBoard.Repository/Migrations/` | exact (standard `dotnet ef migrations add`) |

## Pattern Assignments

### `QuestBoard.Repository/Entities/ContactCategoryEntity.cs` (model, CRUD)

**Analog:** `QuestBoard.Repository/Entities/ContactEntity.cs` (verified, full file, 44 lines)

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Contacts")]
public class ContactEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    ...
    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
```

Copy this shape for `ContactCategoryEntity`: `Id`, `[Required][StringLength(60)] Name` (D-15's 60-char cap — note `ContactEntity.Name` uses 100, `ContactCategoryEntity.Name` must use 60 per D-15, not a copy of that number), `SortOrder` (int), `GroupId` + `[ForeignKey] Group` navigation exactly as above. `ContactEntity` itself gains `public int? CategoryId { get; set; }` + `[ForeignKey(nameof(CategoryId))] public virtual ContactCategoryEntity? Category { get; set; }` (nullable, matching D-01).

---

### `QuestBoard.Repository/Entities/QuestBoardContext.cs` (config, modified)

**Analog:** itself — the `ContactEntity` filter block and `Contact → Group` FK block.

**Fail-closed query filter** (verified, `QuestBoardContext.cs:426-432`):
```csharp
// ContactEntity deliberately does NOT offer a SuperAdmin cross-group view like Quest/ShopItem
// do above -- same "per-group roster" shape as CharacterEntity. An empty Contact list when no
// group is selected is the intended behavior here, not an oversight.
modelBuilder.Entity<ContactEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```
Add an identical block for `ContactCategoryEntity`, swapping only the entity type — see RESEARCH.md Pattern 1 for the exact new-entity text. The comment at line 357 ("Do NOT capture `activeGroupContext.ActiveGroupId` into a local var here") is the load-bearing warning; do not violate it.

**`Contact → Group: NoAction`** (verified, `QuestBoardContext.cs:257` area — RESEARCH.md quotes lines 256-261 verbatim):
```csharp
// Contact -> Group: NoAction to prevent cascade cycles
modelBuilder.Entity<ContactEntity>()
    .HasOne(c => c.Group)
    .WithMany()
    .HasForeignKey(c => c.GroupId)
    .OnDelete(DeleteBehavior.NoAction);
```
`ContactCategoryEntity → Group` must copy this exact shape (`NoAction`), never the EF convention default (`Cascade`). `Contact → Category` is the one place `SetNull` belongs (see RESEARCH.md Pattern 2 for the `Event → Series` fluent shape to mirror with `SetNull` substituted for `NoAction`).

**Unique index (D-04):**
```csharp
modelBuilder.Entity<ContactCategoryEntity>()
    .HasIndex(cc => new { cc.GroupId, cc.Name })
    .IsUnique();
```

---

### `QuestBoard.Repository/ContactCategoryRepository.cs` + `IContactCategoryRepository` (repository, CRUD)

**Analog:** `QuestBoard.Repository/ContactRepository.cs` (verified, full file, 213 lines) and `QuestBoard.Domain/Interfaces/IContactRepository.cs`.

Key structural pattern to copy — `internal class X(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<TDomain, TEntity>(dbContext, mapper), IX`:
```csharp
internal class ContactRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<Contact, ContactEntity>(dbContext, mapper), IContactRepository
{
    public async Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default)
    {
        var entities = await DbContext.Contacts
            .Include(c => c.CreatedByUser)
            .Include(c => c.Notes).ThenInclude(n => n.Author)
            .OrderBy(c => c.Name)
            .ToListAsync(token);
        var contacts = Mapper.Map<IList<Contact>>(entities);
        return contacts;
    }
    // ...
}
```
`ContactCategoryRepository` should follow this same `BaseRepository<ContactCategory, ContactCategoryEntity>` shape, adding CRUD plus two reorder-support methods (`MoveUp`/`MoveDown` swap `SortOrder` with the adjacent row, or a `GetOrderedByGroupAsync` the service uses to compute adjacency — no existing repository in this codebase has a reorder method, so this part is genuinely new, not copied). No image/note handling is needed (that machinery in `ContactRepository` is Contact-specific and should NOT be copied).

Also add `.Include(c => c.Category)` to `ContactRepository.GetAllContactsWithDetailsAsync` and `GetContactWithDetailsAsync`, mirroring the existing `.Include(c => c.CreatedByUser)` on the same lines (verified `ContactRepository.cs:18` and `:42`).

---

### `QuestBoard.Domain/Services/ContactCategoryService.cs` + `IContactCategoryService` (service, CRUD)

**Analog:** `QuestBoard.Domain/Services/ContactService.cs` (verified, full file, 116 lines).

```csharp
internal class ContactService(IContactRepository repository, IMapper mapper)
    : BaseService<Contact>(repository, mapper), IContactService
{
    public async Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default)
        => await repository.GetAllContactsWithDetailsAsync(token);
    // thin pass-through methods to the repository
}
```
`ContactCategoryService` should be an equally thin pass-through over `BaseService<ContactCategory>` plus the reorder methods and a duplicate-name check delegated to the DB unique index (do not hand-roll a pre-check — see RESEARCH.md "Don't Hand-Roll").

---

### `QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs` (verified, opening 80 lines read).

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Shop;

[Authorize(Policy = "DungeonMasterOnly")]
public class ShopManagementController(
    IAuthorizationService authorizationService,
    IShopService shopService,
    IUserService userService,
    IMapper mapper
    ) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token = default)
    {
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null) { return Challenge(); }
        var allItems = await shopService.GetAllAsync(token);
        var viewModel = new ShopManagementIndexViewModel { /* ... */ };
        return View(viewModel);
    }
    // Create (GET+POST), Edit (GET+POST) follow the same shape
}
```

Copy: **class-level `[Authorize(Policy = "DungeonMasterOnly")]`** (matches D-05 exactly — do not duplicate per-action), plain unareaed `Controller` (routes resolve to `/ContactCategoryManagement/{action}` by convention), constructor-injected `IContactCategoryService` + `IUserService` + `IMapper`. Per UI-SPEC Component Spec 3, the action set is `Index` (GET, list + inline add form), `Add` (POST), `Edit` (GET+POST, rename only), `Delete` (POST), `MoveUp`/`MoveDown` (POST) — **not** a separate `Create.cshtml` (deliberate divergence from RESEARCH.md's literal file-list sketch; UI-SPEC's reasoning: a category has one field, so the add form is inline on `Index`, not its own page). `ShopManagementController` has no `.Mobile.cshtml` variant on its own views by convention duplication rule — but `Views/ShopManagement/{Index,Create,Edit}.cshtml` **do** each have a `.Mobile.cshtml` sibling (confirmed in RESEARCH.md Pattern 4), so `Manage.cshtml`/`Manage.Mobile.cshtml`/`Edit.cshtml` must follow the same platform-pairing, per D-08.

**Duplicate-name error handling** — analog `GroupController.Create`/`Edit` (verified, `Areas/Platform/Controllers/GroupController.cs:38-56` and `67-88`):
```csharp
try
{
    await groupService.AddAsync(new Group { Name = model.Name, BoardType = model.BoardType!.Value });
    TempData["Success"] = "Group created successfully.";
    return RedirectToAction(nameof(Index));
}
catch (DbUpdateException ex) when (
    ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
    ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
{
    ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. Please choose a different name.");
    return View(model);
}
```
Copy verbatim for `ContactCategoryManagementController.Add`/`Edit`, swapping the error text to D-04's exact wording ("A category with that name already exists. Please choose a different name.") and, per UI-SPEC Component Spec 3, re-rendering `Index` (not a separate `Add` view) with the category list repopulated and `NewCategory`'s `ModelState` errors intact on failure.

---

### Views — Manage/Edit and Contacts Index/Create/Edit/Details

**Analog:** `Views/ShopManagement/Index.cshtml` (+ `.Mobile.cshtml`), `Areas/Platform/Views/Group/Index.cshtml`/`Edit.cshtml`, and the existing `Views/Contacts/*` files.

Exact markup for every new/modified view is already drafted in `.planning/phases/80-contact-categories/80-UI-SPEC.md` Component Specs 1, 3, 5, 6, 7 — copy those blocks directly rather than re-deriving them. Key structural rules embedded in that markup:
- `modern-card` / `modern-card-header` / `modern-card-body` wrapper (CLAUDE.md-mandated), `<hr>` before the button row, `d-flex justify-content-between` button layout — same shape as every other management page.
- Reorder buttons are plain `<form method="post">` + `[ValidateAntiForgeryToken]`, no AJAX/JS framework — matches this codebase's zero-JS-library convention.
- Delete confirmation uses the existing plain-`confirm()` idiom (see `ShopManagement/Index.cshtml`'s `onsubmit="return confirm(...)"` and `Contacts/Details.cshtml`'s "Delete Contact" button) via `data-*` attributes, not string-interpolated `onsubmit`.

---

### `ContactCategoryGroupViewModel` (viewmodel, transform)

**Analog:** `QuestBoard.Service/ViewModels/ShopViewModels/ShopCategoryViewModel.cs` (verified, full file — 6 lines):
```csharp
namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class ShopCategoryViewModel
{
    public string Title { get; set; } = string.Empty;
    public IList<ShopItemViewModel> Items { get; set; } = [];
}
```
**Important caveat, confirmed this pass:** this type is dead code. `git grep`/codebase search finds no controller constructing it and no view binding to it anywhere in `QuestBoard.Service`. Treat the `{ Title, Items }` shape as a naming/structure precedent only — it is **not** a proven rendering path, and its lack of use means there is no example Razor loop consuming it to also copy. `ContactCategoryGroupViewModel` should be `{ string Title, bool IsUngrouped, IList<ContactViewModel> Contacts }` (the `IsUngrouped` flag is new — needed by UI-SPEC's muted-Ungrouped-heading CSS class toggle, Component Spec 2 — `ShopCategoryViewModel` has no equivalent since it has no "ungrouped" concept).

---

### `ContactsController.Index` — grouping insertion point (D-13)

**Analog:** itself (verified, `ContactsController.cs:1-50`, `IsVisibleTo` at `469`).

```csharp
[HttpGet]
public async Task<IActionResult> Index(CancellationToken token = default)
{
    var currentUser = await userService.GetUserAsync(User);
    if (currentUser.Id == 0) { return Challenge(); }

    var viewerIsDmTier = await IsDmTierAsync();
    var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

    var allContacts = await contactService.GetAllContactsWithDetailsAsync(token);
    var visibleContacts = allContacts.Where(c => IsVisibleTo(c, currentUser.Id, includeHidden)).ToList();

    var contactViewModels = mapper.Map<List<ContactViewModel>>(visibleContacts);
    foreach (var vm in contactViewModels) { vm.CanManage = viewerIsDmTier; }

    var viewModel = new ContactsIndexViewModel
    {
        Contacts = contactViewModels,
        ShowHidden = includeHidden,
        ViewerIsDmTier = viewerIsDmTier
    };
    return View(viewModel);
}

private static bool IsVisibleTo(Contact contact, int currentUserId, bool includeHidden)
{
    if (contact.IsRevealed) { return true; }
    if (currentUserId != 0 && contact.CreatedByUserId == currentUserId) { return true; }
    return includeHidden;
}
```
The `contactViewModels` list at the line after the `foreach` is the exact D-13 insertion point — group strictly after this, never before `visibleContacts` is computed and never inside `ContactRepository`. `IsVisibleTo` (469) is reused completely unchanged.

---

### `ContactsIndexViewModel` (modified)

**Analog:** itself (verified, full file, 15 lines):
```csharp
public class ContactsIndexViewModel
{
    // Flat, alphabetical list -- Contacts have no owner concept, so unlike Characters there is
    // no "My/Other" split.
    public IList<ContactViewModel> Contacts { get; set; } = [];
    public bool ShowHidden { get; set; }
    public bool ViewerIsDmTier { get; set; }
}
```
The comment on `Contacts` is stale once grouping lands (CONTEXT.md flags this explicitly) — update it, don't leave it. Add `public IList<ContactCategoryGroupViewModel> CategoryGroups { get; set; } = []` and a `public bool HasCategories => CategoryGroups.Count > 0;` (or set directly) driving the D-10 flat-list fallback used in UI-SPEC's view markup.

---

### AutoMapper — both boundaries (modified)

**Analog:** the existing Contact mapping block in each profile.

`EntityProfile.cs` (verified, lines 114-129 area):
```csharp
// Contact mapping
CreateMap<Contact, ContactEntity>()
    .ForMember(dest => dest.ProfileImage, opt => opt.MapFrom(src => src.ContactImageData == null
        ? null
        : new ContactImageEntity { OriginalImageData = src.ContactImageData }));

CreateMap<ContactEntity, Contact>()
    .ForMember(dest => dest.ContactImageData, opt => opt.MapFrom(src => src.ProfileImage != null ? src.ProfileImage.OriginalImageData : null))
    .ForMember(dest => dest.HasContactImage, opt => opt.Ignore());
```
Add `CreateMap<ContactCategory, ContactCategoryEntity>().ReverseMap()` (no special members needed — plain scalar fields) and, on the existing Contact maps, no extra `.ForMember` is required since `CategoryId` is a plain scalar that AutoMapper matches by convention; only `CategoryName` (domain-model display projection, mirroring how `AuthorName` is handled on `ContactNote`, per RESEARCH.md's Project Structure note) needs `.ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))` on `ContactEntity → Contact`.

`ViewModelProfile.cs` (verified, lines 81-92 area):
```csharp
// Contact to ContactViewModel
CreateMap<Contact, ContactViewModel>()
    .ForMember(dest => dest.HasContactImage, opt => opt.MapFrom(src => src.HasContactImage))
    .ForMember(dest => dest.ContactImageFile, opt => opt.Ignore());
    // ...

// ContactViewModel to Contact
CreateMap<ContactViewModel, Contact>()
    .ForMember(dest => dest.ContactImageData, opt => opt.Ignore());
    // ...
```
Add `CreateMap<ContactCategory, ContactCategoryViewModel>().ReverseMap()` (new small viewmodel for the Manage page rows, per UI-SPEC's `Model.Categories` with `Id`/`Name`/`ContactCount`/`IsFirst`/`IsLast`) — `ContactCount`/`IsFirst`/`IsLast` are computed in the controller/service, not AutoMapper-derived, matching how `CanManage` on `ContactViewModel` is set imperatively in `ContactsController.Index` rather than mapped.

---

### `Contact.cs` / `ContactEntity.cs` / `ContactViewModel.cs` — the category field (D-16 correction)

**Verified this pass, confirms RESEARCH.md Pitfall 1 exactly:** none of `Contact.cs`, `ContactEntity.cs` (full file read, 44 lines), or `ContactViewModel.cs` (full file read, 107 lines) has any `Category`-shaped field today. D-16's claim ("already on the mapped ContactViewModel") is **false** — treat it as three real tasks: add `int? CategoryId` + navigation to `ContactEntity`, add `int? CategoryId` + `string? CategoryName` to `Contact` (domain), add `int? CategoryId` + `string? CategoryName` to `ContactViewModel`, plus the two AutoMapper entries above. `ContactEntity`'s existing `GroupId`/`Group` FK block (lines 40-43) is the literal shape to mirror for `CategoryId`/`Category`, but nullable:
```csharp
public int? CategoryId { get; set; }

[ForeignKey(nameof(CategoryId))]
public virtual ContactCategoryEntity? Category { get; set; }
```

---

### Test files

**Cross-group isolation test (D-17)** — analog `ContactsControllerIntegrationTests.cs:351-398` (`ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups`), pattern confirmed via RESEARCH.md's verified read:
1. `TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2)` creates group 2.
2. Seed group-1 data directly via a new `TestDataHelper.CreateTestContactCategoryAsync` (mirror `CreateTestContactAsync`, verified full signature below).
3. `factory.TestGroupContext.ActiveGroupId = 1` then `= 2` toggles the simulated active group between requests in the same test.
4. Assert the HTML response never contains the other group's category name; assert a cross-group `CategoryId` POST is refused.

**`TestDataHelper.CreateTestContactAsync`** (verified, `TestDataHelper.cs:166-193`):
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
```
Add a sibling `CreateTestContactCategoryAsync(IServiceProvider services, int groupId, string name, int sortOrder = 0)` following this identical `using var scope = services.CreateScope(); ... context.Add(...); await context.SaveChangesAsync();` shape.

**Real-mobile-User-Agent render test** — analog `AgendaMobileRenderTests.GetMobileAsync` (Phase 82), cited verbatim in RESEARCH.md as the literal helper to reuse:
```csharp
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
Copy this helper verbatim into the new mobile-render test class for `ContactCategoryManagement/Manage.Mobile.cshtml` and the grouped `Contacts/Index.Mobile.cshtml` — this is the *only* mechanism in the codebase that actually exercises `MobileViewLocationExpander`'s `.Mobile.cshtml` selection; devtools emulation does not set this header.

**DB-direct delete-orphan assertion (D-20)** — analog `EventsControllerIntegrationTests.cs:378-385` pattern (RESEARCH.md-cited, not independently re-read this pass — trust RESEARCH.md's verified quote):
```csharp
private async Task<ContactEntity?> GetContactAsync(int contactId)
{
    await using var ctx = factory.Database.CreateContext();
    return await ctx.Contacts
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.Id == contactId, TestContext.Current.CancellationToken);
}
```
`IgnoreQueryFilters()` here is sanctioned test-only infrastructure per D-17's own carve-out (asserting raw DB state, not an application code path) — never copy this into `ContactCategoryManagementController`, any repository, or any service method.

**Unit test analogs:** `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` and `QuestBoard.UnitTests/Services/ContactServiceTests.cs` — not read this pass (out of budget), but RESEARCH.md's Wave 0 Gaps confirms they exist and should be mirrored structurally for `ContactCategoryRepositoryTests.cs`/`ContactCategoryServiceTests.cs`.

## Shared Patterns

### Authentication / Authorization
**Source:** `ShopManagementController` class-level `[Authorize(Policy = "DungeonMasterOnly")]` (verified, line 11 of that file) and `ContactsController` class-level `[Authorize]` (verified, line 12).
**Apply to:** `ContactCategoryManagementController` (class-level `DungeonMasterOnly`, matching D-05); `ContactsController`'s existing `[Authorize]` is unchanged — Create/Edit/Delete of contacts already carry the DM gate elsewhere in that file (not re-verified line-by-line this pass, but confirmed present via CONTEXT.md D-05's framing of it as "the exact gate ContactsController's Create/Edit/Delete already carry").

### Fail-closed group query filter
**Source:** `QuestBoardContext.cs:426-432` (`ContactEntity`), warning comment at line 357.
**Apply to:** the new `ContactCategoryEntity` filter — copy verbatim, entity name swapped, never capture `ActiveGroupId` into a local.

### `DbUpdateException` → `ModelState` error, not 500
**Source:** `GroupController.Create`/`Edit`, verified `GroupController.cs:38-56`/`67-88`.
**Apply to:** `ContactCategoryManagementController.Add`/`Edit` for the D-04 duplicate-name case.

### `Group` FK `NoAction` convention
**Source:** `QuestBoardContext.cs:257` area, `Contact → Group`.
**Apply to:** `ContactCategoryEntity → Group` FK configuration — must not use EF's convention-default `Cascade`.

### Grouping-after-visibility-filter
**Source:** `ContactsController.Index` (lines 22-50) + `IsVisibleTo` (line 469).
**Apply to:** the new `CategoryGroups` computation — must sit strictly after `visibleContacts` is built, inside the controller, never in `ContactRepository`.

### Real mobile User-Agent test helper
**Source:** `AgendaMobileRenderTests.GetMobileAsync` (Phase 82).
**Apply to:** every new/modified `.Mobile.cshtml` verification in this phase (`Manage.Mobile.cshtml`, `Contacts/Index.Mobile.cshtml` grouped rendering).

### Modern-card UI shell
**Source:** CLAUDE.md UI/UX Design Guidelines + `Views/ShopManagement/Index.cshtml`.
**Apply to:** `Manage.cshtml`/`Manage.Mobile.cshtml`/`Edit.cshtml` — `modern-card`/`modern-card-header`/`modern-card-body`, `<hr>` before buttons, `d-flex justify-content-between` button row. Exact markup already drafted in 80-UI-SPEC.md Component Spec 3.

## No Analog Found

None. Every file in this phase's scope has at least a role-match analog somewhere in the existing codebase; the reorder (`MoveUp`/`MoveDown`) mechanism has no existing precedent in this codebase (no other entity has manual `SortOrder` reordering) and is flagged as genuinely new ground in RESEARCH.md's Open Question 1 — the planner should treat the reorder actions as new interaction surface, not a copy, while everything else (CRUD shape, auth, filter, mapping, tests) is a direct copy.

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Domain/`, `QuestBoard.Service/Controllers/`, `QuestBoard.Service/Views/`, `QuestBoard.Service/ViewModels/`, `QuestBoard.Service/Automapper/`, `QuestBoard.IntegrationTests/`
**Files scanned (read/grepped this session):** `ShopManagementController.cs`, `ContactRepository.cs`, `ContactService.cs`, `ContactEntity.cs`, `ContactViewModel.cs`, `ContactsIndexViewModel.cs`, `ContactsController.cs` (Index + IsVisibleTo regions), `ShopCategoryViewModel.cs`, `EntityProfile.cs` (Contact block), `ViewModelProfile.cs` (Contact block), `GroupController.cs` (Create/Edit), `QuestBoardContext.cs` (filter + FK regions), `TestDataHelper.cs` (CreateTestContactAsync) — plus the full CONTEXT.md/RESEARCH.md/UI-SPEC.md for this phase, which independently verified `AgendaMobileRenderTests.GetMobileAsync`, `EventsControllerIntegrationTests` DB-assertion pattern, and the cross-group test fixture at `ContactsControllerIntegrationTests.cs:351-398`.
**Pattern extraction date:** 2026-08-30
