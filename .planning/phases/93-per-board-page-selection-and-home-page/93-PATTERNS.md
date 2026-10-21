# Phase 93: Per-Board Page Selection and Home Page - Pattern Map

**Mapped:** 2026-10-21
**Files analyzed:** 32 (new and modified, grouped where the pattern is identical)
**Analogs found:** 30 / 32 (all analog paths are git-tracked source)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Domain/Enums/BoardPage.cs` (+ `BoardPageSet`) | model (enum) | transform | `QuestBoard.Domain/Enums/MoveStatus.cs` | role-match |
| `QuestBoard.Domain/Extensions/BoardPagePolicy.cs` | utility | transform | `QuestBoard.Domain/Extensions/MoveEligibility.cs` | role-match |
| `QuestBoard.Domain/Interfaces/IBoardPageResolver.cs` (+ `BoardPageAvailability`) | interface | request-response | `QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs` | exact |
| `QuestBoard.Service/Services/BoardPageResolver.cs` | service (scoped) | request-response | `QuestBoard.Service/Services/BoardTypeResolver.cs` | exact |
| `QuestBoard.Domain/Services/BoardPageSettingsService.cs` + interface | service | CRUD/validate | `QuestBoard.Domain/Services/MoveControlService.cs` (uses `IBoardClock`) | role-match |
| `QuestBoard.Service/Filters/BoardPageGateFilter.cs` (+ `BoardPageAttribute`, `BoardPageExemptAttribute` in the same file) | middleware (MVC filter) | request-response | `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` (TempData + redirect from outside a controller) | partial (no existing global filter) |
| `QuestBoard.Service/Helpers/BoardPageRoutes.cs` | utility | transform | `QuestBoard.Service/Helpers/MoveCopy.cs` | role-match |
| `QuestBoard.Service/Program.cs` (filter registration, DI) | config | - | itself, lines 37 and 255-258 | exact |
| `QuestBoard.Repository/Entities/GroupEntity.cs` (+2 int columns) | model | CRUD | same file (`BoardType` int) | exact |
| `QuestBoard.Repository/Migrations/<ts>_AddBoardPageSettings.*` | migration | batch | `20260930161610_AddFeedEntryRevisions.cs` (non-null int + default) | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` | config | transform | same file, lines 210-219 | exact |
| `QuestBoard.Repository/EventRepository.cs`, `QuestRepository.cs` (count methods) | repository | CRUD | same files (`IgnoreQueryFilters` allowlisted) | exact |
| `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` | controller | request-response | itself | exact |
| `QuestBoard.Service/Controllers/GroupPickerController.cs` (`RedirectToLocal`) | controller | request-response | itself, lines 94-103 | exact |
| ~15 controllers gaining `[BoardPage(...)]` | controller | request-response | `[Authorize(Policy=...)]` placement in `GroupController.cs` | role-match |
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (Edit GET/POST) | controller | CRUD | itself, lines 58-85 | exact |
| `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs` | model (viewmodel) | request-response | itself | exact |
| `Areas/Platform/Views/Group/Edit.cshtml` + `.Mobile` | view | request-response | `Edit.cshtml` itself | exact |
| `Areas/Platform/Views/Group/Index.cshtml` + `.Mobile` (badge) | view | request-response | existing board-type badge in the same file | exact |
| `Views/_ViewImports.cshtml` (+ `@inject IBoardPageResolver BoardPages`) | config | - | same file, lines 15-17 | exact |
| `Views/Shared/_Layout.cshtml` + `_Layout.Mobile.cshtml` | view | request-response | same file, `activeBoardType` blocks (lines 52, 108-190) | exact |
| `Views/Shared/_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml` | view | request-response | same files, `ViewerIsMember` branch | exact |
| Calendar/Agenda/Players/Quest Details/DM Profile/Account Profile views + twins | view | request-response | each file itself | exact |
| `MoveController`, `QuestMoveService`, `EventMoveService`, `MoveStatus`, `LinkedBoard` | controller/service | request-response | themselves | exact |
| `QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs`, `BoardPageSettingsServiceTests.cs` | test | transform | `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` | role-match |
| `QuestBoard.IntegrationTests/Controllers/BoardPage*Tests.cs` | test | request-response | `Controllers/LayoutNavigationTests.cs` | exact |
| `QuestBoard.IntegrationTests/Helpers/BoardPageSeed.cs` | test helper | CRUD | `Helpers/BoardMoveSeed.cs` | exact |
| `QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs` | test (reflection) | transform | `Security/AntiForgeryTokenCoverageTests.cs` | exact |
| `QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs` | test | file-I/O | `Tests/BoardMoveStaticGuardTests.cs` | exact |
| `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` (modify list) | test | file-I/O | itself, `BoardClockConsumerPaths` line 75 | exact |

## Pattern Assignments

### `BoardPageResolver.cs` + `IBoardPageResolver.cs` (service, request-response)

**Analog:** `QuestBoard.Service/Services/BoardTypeResolver.cs` (lines 1-29) and `QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs`

```csharp
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Services;

public class BoardTypeResolver(IActiveGroupContext activeGroupContext, IGroupService groupService) : IBoardTypeResolver
{
    public async Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default)
    {
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            return null;
        }
        var group = await groupService.GetByIdAsync(groupId, token);
        return group?.BoardType;
    }
}
```

New resolver: same primary-constructor style, same null-on-no-board contract, XML doc explaining the DI-cycle reason (copy the summary's reasoning in plain language). Take board type from `IBoardTypeResolver` (no fourth lookup), read the group once, memoise keyed on `ActiveGroupId`.

**DI registration** (`Program.cs` lines 255-258): add `builder.Services.AddScoped<IBoardPageResolver, BoardPageResolver>();` directly after
```csharp
builder.Services.AddScoped<IBoardTypeResolver, BoardTypeResolver>();
```
**Filter registration** (`Program.cs` line 37): change `builder.Services.AddControllersWithViews();` to `AddControllersWithViews(options => options.Filters.Add<BoardPageGateFilter>());`.

---

### `BoardPageGateFilter.cs` (filter, request-response)

**Analog:** `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` for writing `TempData` outside a controller via `ITempDataDictionaryFactory`; the filter skeleton is in RESEARCH "Filter body" (no existing global filter exists, so RESEARCH.md is the pattern). Toast key: `TempData["Info"]`, rendered by `_Toasts.cshtml` (`Success`/`Error`/`Warning`/`Info` are plain string literals by design; do not add them to `TempDataKeys.cs`).

---

### `GroupEntity.cs` + migration + `EntityProfile.cs`

**Analog entity:** `GroupEntity.cs` `public int BoardType { get; set; }`. Add `public int DisabledPages { get; set; }` and `public int HomePage { get; set; }` with no `HasDefaultValue`.

**Migration analog:** `QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions.cs` lines 12-40:
```csharp
migrationBuilder.AddColumn<int>(
    name: "FeedRevision",
    table: "Quests",
    type: "int",
    nullable: false,
    defaultValue: 1);
```
Use `defaultValue: 0` for both columns on `Groups`; generate with `dotnet ef migrations add AddBoardPageSettings --project ../QuestBoard.Repository` from `QuestBoard.Service/`. Files begin with a BOM and `#nullable disable`; never hand-write the Designer/snapshot.

**Mapping analog:** `EntityProfile.cs` lines 211-219:
```csharp
CreateMap<GroupEntity, Group>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (BoardType)src.BoardType));
CreateMap<Group, GroupEntity>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (int)src.BoardType))
    .ForMember(dest => dest.LinkedBoardSetId, opt => opt.Ignore())
    .ForMember(dest => dest.LinkedBoardSet, opt => opt.Ignore());
```
Add `HomePage` cast (`(BoardPage)src.HomePage` / `(int)src.HomePage`) and the mask mapping; keep both `Ignore()` lines (they protect the `LinkedBoardSetId` concurrency token). Add `BoardPage` to `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` using its `[Theory][MemberData(nameof(XxxValues))] ... AssertRoundTrips(value)` shape (lines 15-40).

---

### `GroupController.cs` Edit (controller, CRUD)

**Analog:** itself, lines 58-85.
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(GroupEditViewModel model)
{
    if (!ModelState.IsValid) return View(model);
    var group = await groupService.GetByIdAsync(model.Id);
    if (group == null) return RedirectToAction(nameof(Index));
    try
    {
        group.Name = model.Name;
        await groupService.UpdateAsync(group);
        TempData["Success"] = "Group name updated.";
        return RedirectToAction(nameof(Index));
    }
    catch (DbUpdateException ex) when (
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true || ...)
    {
        ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. ...");
        return View(model);
    }
}
```
Changes: `BoardType` is `[BindNever]`, so every `return View(model)` (invalid state, duplicate name, new refusal, new `DbUpdateConcurrencyException` catch) must first re-populate `model.BoardType` and the row metadata from the loaded group. Refusals go through `ModelState.AddModelError(string.Empty, ...)` (the view already has `asp-validation-summary="ModelOnly"`). Keep `[ValidateAntiForgeryToken]`. Controller constructor already takes `IGroupService`; inject `IBoardPageSettingsService`.

**View model analog:** `GroupEditViewModel.cs` (`[Required]`, `[Display]`, `[BindNever]` BoardType). Add `List<BoardPageRowViewModel> Pages` and `BoardPage? HomePage`; absent rows or null home mean unchanged.

---

### `Edit.cshtml` / `Edit.Mobile.cshtml` (view)

**Analog:** `Areas/Platform/Views/Group/Edit.cshtml` lines 7-40. Insert the "Pages" section between the board-type `div.mb-3` and `<hr>`; keep the `<hr>` + `d-flex justify-content-between` button row untouched.
```html
<div class="mb-3">
    <label asp-for="BoardType" class="form-label">Board Type</label>
    <select asp-for="BoardType" class="form-select" asp-items="Html.GetEnumSelectList<BoardType>()" disabled></select>
</div>
<hr>
```
Rows use Bootstrap `form-check form-switch`; locked rows get `disabled` plus `small.text-muted` reason. Read `.claude/ui-guidelines.md` and the `.Mobile.cshtml` twin first (twins are not always content-equivalent; Platform mobile views render inside the desktop Platform layout).

---

### Layouts (`_Layout.cshtml`, `_Layout.Mobile.cshtml`)

**Analog:** the same files. Existing allowlist gating (lines 5, 52, 108-190):
```razor
@inject IBoardTypeResolver BoardTypeResolver
...
var activeBoardType = await BoardTypeResolver.GetBoardTypeAsync();
...
@if (activeBoardType == BoardType.OneShot)
{
<li><a class="dropdown-item" asp-controller="ShopManagement" asp-action="Index">...Manage Shop</a></li>
}
```
Replace each `activeBoardType` check with `pages.Allows(BoardPage.X)` (`var pages = await BoardPages.GetAsync();`). Brand link (line 41) currently `asp-controller="Quest" asp-action="Index"` for authenticated users; render the `EffectiveHome` route instead. Anonymous branch (`Home/Index`) unchanged. Do not render an empty DM dropdown when all items are hidden.

**Razor injection:** `Views/_ViewImports.cshtml` lines 15-17 hold the existing `@inject` lines; add `@inject IBoardPageResolver BoardPages` there (do not name it `Pages`; `_ViewImports` declares namespace `QuestBoard.Service.Pages`).

---

### Linked calendar inert links (`_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml`, Calendar mobile 131-166)

**Analog:** `_LinkedDayEntries.cshtml` lines 26-56: the `if (entry.ViewerIsMember) { <a ... href=...> } else { inert }` branch. Extend the condition (member AND page available on owning board, carried as a flag from `LinkedCalendarService`); reuse the existing inert markup unchanged.

---

### `HomeController.cs` / `GroupPickerController.cs`

`Controllers/QuestBoard/HomeController.cs` (whole file):
```csharp
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Quest")
            : View();
}
```
Inject `IBoardPageResolver`, make `Index` async; authenticated branch redirects to `EffectiveHome` route, resolver null keeps `Quest/Index`. `GroupPickerController.RedirectToLocal` (lines 94-103): keep `Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl)`; replace the fallback `RedirectToAction("Index", "Quest")` with `RedirectToAction("Index", "Home")`.

---

### Count methods (`EventRepository.cs`, `QuestRepository.cs`)

Put cross-board counts inside these two already-allowlisted files (`CrossBoardIgnoreQueryFiltersSeamTests` allows exactly five files; do not add a sixth). Take `today` as a parameter from `IBoardClock` (never read the clock in the repository; `QuestRepository.cs` is in `AmbientClockSeamTests` guarded paths). Quest rule: `!IsClosed && !HasBeenPlayed(today)` via `QuestExtensions.HasBeenPlayed`.

---

### `BoardPageSettingsService.cs` (service, clock-dependent)

**Analog:** `QuestBoard.Domain/Services/MoveControlService.cs` / `QuestMoveService.cs` (take `IBoardClock`). Register the new file in `AmbientClockSeamTests.BoardClockConsumerPaths` (`QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` line 75-87; entries are repo-relative strings, alphabetised):
```csharp
"QuestBoard.Domain/Services/MoveControlService.cs",
"QuestBoard.Domain/Services/QuestMoveService.cs",
```

---

### `[BoardPage]` attribute placement

Class-level attribute stack follows `GroupController.cs` lines 14-16 (`[Area("Platform")]` / `[Authorize(...)]` above the class). Action-level overrides for `UpdateSignupCharacter` (also Characters), `DungeonMasterController.Profile/EditProfile`, `AdminController.Quests/DeleteQuest`, `MoveController.Quest/ConfirmQuest/Event/ConfirmEvent`. `[BoardPageExempt]` on the two Characters and two DM picture endpoints. Full map is in RESEARCH "Page Map".

---

### Tests

- **Integration (gate, navigation, home, platform, quests-off):** analog `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs`: `IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime`; both user agents as constants; `GetWithUserAgentAsync(url, ua, authorization)` helper; set `_factory.TestGroupContext.BoardType` per test and restore in `DisposeAsync` (lines 11-46):
  ```csharp
  private const string MobileUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) ... Mobile/15E148 Safari/604.1";
  public ValueTask DisposeAsync() { _factory.TestGroupContext.BoardType = BoardType.OneShot; _factory.TestGroupContext.ActiveGroupId = 1; ... }
  ```
  Use `TestContext.Current.CancellationToken` on every async call (xunit.v3).
- **Seed helper:** `Helpers/BoardMoveSeed.cs`: `await using var ctx = factory.Database.CreateContext(); ... ctx.Groups.Add(new GroupEntity { Id = id, Name = name, CreatedAt = DateTime.UtcNow, BoardType = (int)type }); await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);`. `BoardPageSeed.SetAsync(factory, groupId, disabled, home)` loads the group and sets the two ints; restore in `DisposeAsync`.
- **Reflection coverage:** `Security/AntiForgeryTokenCoverageTests.cs` lines 15-30: enumerate `typeof(Program).Assembly.GetTypes()` filtered to non-abstract `Controller` subclasses, `GetMethods(BindingFlags.Public | Instance | DeclaredOnly)`, collect `violations` list, assert empty. Add an explicit always-on controller list.
- **Planning-reference guard:** `Tests/BoardMoveStaticGuardTests.cs`: copy `ResolveRepoFile`/`FindRepositoryRoot` (lines 15-34), fragment-built regex array (lines 36-46, e.g. decision ids `D-(0[1-9]|1[0-9]|2[01])`, phase reference built as `"Pha" + "se\\s+9" + "3\\b"`, planning doc names), a `FeatureSourceFiles` list (missing files skipped), `FeatureSourceFiles_Reach...` sanity fact, and a `[Theory][MemberData]` per file asserting `violations.Should().BeEmpty(because: ...)`. Note the pattern literals must never appear verbatim in the guard file itself.
- **Unit tests:** xunit.v3 + FluentAssertions + NSubstitute; Arrange/Act/Assert; fake `IBoardClock` via NSubstitute.

## Shared Patterns

### One availability source of truth
**Source:** new `BoardPagePolicy` (Domain) via `IBoardPageResolver`. **Apply to:** layouts, in-page links, gate filter, home computation, Platform form/service, move services, linked calendar flags. Never inline `BoardType` checks for page availability.

### Toast message
**Source:** `Views/Shared/_Toasts.cshtml` (`TempData["Info"]`, auto-hiding). **Apply to:** gate redirect, move refusals.

### Antiforgery
**Source:** `[HttpPost] [ValidateAntiForgeryToken]` pair as in `GroupController.Edit`. **Apply to:** any new POST; `AntiForgeryTokenCoverageTests` enforces it.

### Allowlist rendering
Show only when confirmed available; unresolved board renders nothing (PROJECT.md Validated entry). Update every `.Mobile.cshtml` twin for each view touched.

### No planning references in source
Comments explain the rule in plain language, never `D-xx`, phase, plan, or planning document names (CLAUDE.md). Backed by the new static guard.

### Guarded files
`CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` must not change.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `Filters/BoardPageGateFilter.cs` (+ attributes) | filter | request-response | No MVC filter or filter attribute exists in the repo (searched `Service/Middleware`, `Service/Authorization`); use RESEARCH "Filter body" and the TempData-from-outside-controller idiom in `CrossBoardDeepLinkMiddleware.cs` |
| DI-graph factory test (real `BoardTypeResolver`) | test | request-response | `WebApplicationFactoryBase` registers `MutableGroupContext` for both seams; no production-graph factory exists. Subclass the factory and re-register `AddScoped<IBoardTypeResolver, BoardTypeResolver>()` |

## Metadata

**Analog search scope:** `QuestBoard.Service` (Program.cs, Controllers, Middleware, Services, Constants, Areas/Platform, Views/Shared), `QuestBoard.Repository` (Entities, Automapper, Migrations), `QuestBoard.UnitTests` (Architecture, Services, Migrator), `QuestBoard.IntegrationTests` (Controllers, Helpers, Security, Tests)
**Files scanned:** ~25 read directly, plus listing searches
**Pattern extraction date:** 2026-10-21
