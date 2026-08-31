---
phase: 80
slug: contact-categories
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-08-31
---

# Phase 80 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Register origin: `register_authored_at_plan_time: true` — all 9 PLAN files carried a
> `<threat_model>` block. Audited in verify-mitigations mode (not retroactive-STRIDE).
> Threat IDs in this phase are globally unique (`T-80-<plan>-<n>`), so the register
> resolves by ID alone.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| active board session state → EF Core read | Every `ContactCategories` read crosses the tenant boundary; the fail-closed global query filter is the only thing holding it | Another campaign's category vocabulary |
| form post → contact write | `CategoryId` is a raw integer under the caller's control and is the one phase-80 value that crosses into a write on a different entity | A foreign board's category riding into a local contact row |
| browser → `/ContactCategoryManagement` | Seven new actions; the caller controls the action, the id and the name | Category create / rename / delete / reorder |
| viewer identity → rendered headings | The set of headings a viewer sees is derived from what that viewer may see; a heading computed on the wrong side of the filter discloses a hidden contact's existence | Existence of unrevealed contacts |
| stored category name → rendered page | Whatever a DM typed is rendered to every board member on the index, the details page and the management page | Stored markup |
| database schema → data integrity | A delete behaviour set wrongly here destroys contact rows with no application code involved | Contact rows |

---

## Threat Register

43 register entries across 9 plans. All closed.

| Threat ID | Plan | Category | Component | Severity | Disposition | Mitigation — verified evidence | Status |
|-----------|------|----------|-----------|----------|-------------|-------------------------------|--------|
| T-80-01-01 | 80-01 | Repudiation | Requirement traceability | medium | mitigate | `CONTACTCAT-01..15` present in all three places and the sets are **identical**: `.planning/REQUIREMENTS.md` (15), `.planning/ROADMAP.md` coverage table (15), and the union of the nine plans' `requirements:` frontmatter (15). Zero ids exist in only one place | closed |
| T-80-01-02 | 80-01 | Tampering | Validation contract | medium | mitigate | `80-VALIDATION.md:48` — `## Per-Task Verification Map`, 13 rows, each binding Requirement → Task ID → runnable `dotnet test --filter` command. The cross-group isolation suite is row 1–2 (`CONTACTCAT-05` → `~ContactCategory_CrossGroup`). All 13 rows marked green | closed |
| T-80-01-03 | 80-01 | Information Disclosure | — | low | accept | Verified docs-only: all five `docs(80-01)` commits touch `.planning/` paths exclusively (`REQUIREMENTS.md`, `ROADMAP.md`, `80-VALIDATION.md`, `80-01-SUMMARY.md`). Zero source files. See R-02 | closed |
| T-80-01-SC | 80-01 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-02-01 | 80-02 | Information Disclosure | `ContactCategoryEntity` global query filter | **critical** | mitigate | `QuestBoardContext.cs:467-470` — `HasQueryFilter(e => activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId)`. The service is dereferenced **inline in the lambda** (`activeGroupContext` is the primary-constructor parameter at `:10`, closed over as an instance so the property re-evaluates per query); grep for `var … = activeGroupContext.ActiveGroupId` returns **zero** captures anywhere in the file, and `:387` carries an explicit "Do NOT capture into a local var" guard comment. Null-board short circuit proven at runtime by `ContactCategory_CrossGroup_NullActiveBoard_CategoryReadResolvesToNothing` (green) | closed |
| T-80-02-02 | 80-02 | Denial of Service | `ContactEntity` rows | high | mitigate | Verified **against the generated migration text**, not inferred: `20260830094351_AddContactCategories.cs:50-56` — `AddForeignKey(FK_Contacts_ContactCategories_CategoryId, …, onDelete: ReferentialAction.SetNull)`. Fluent source at `QuestBoardContext.cs:281-284`. Corroborated live: `sys.foreign_keys` shows `SET_NULL` on the deployed SQL Server instance | closed |
| T-80-02-03 | 80-02 | Denial of Service | Schema creation | high | mitigate | Verified against the migration text: `20260830094351_AddContactCategories.cs:32-36` emits `FK_ContactCategories_Groups_GroupId` **with no `onDelete:` argument**, which is `ReferentialAction.NoAction` (the parameter default; `Cascade` would be emitted explicitly). Fluent source is explicit at `QuestBoardContext.cs:269-273` — `.OnDelete(DeleteBehavior.NoAction)`. Corroborated live: `sys.foreign_keys` shows `NO_ACTION`. Evidence is by-omission in the migration and by-explicit-statement in the model + live schema; all three agree | closed |
| T-80-02-04 | 80-02 | Tampering | Category name length | low | mitigate | `ContactCategoryEntity.cs:16-17` `[Required]` + `[StringLength(60)]`; `ContactCategory.cs:11-12` (domain model) same pair; migration emits `nvarchar(60), maxLength: 60, nullable: false`. Server-side on all three layers, independent of any client control | closed |
| T-80-02-SC | 80-02 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-03-01 | 80-03 | Information Disclosure | `ContactCategoryRepository` | **critical** | mitigate | All five methods (`GetOrderedForActiveGroupAsync`, `GetContactCountsAsync`, `GetNextSortOrderAsync`, `SwapSortOrderAsync`, `DeleteWithDependentsLoadedAsync`) read `DbContext.ContactCategories` / `DbContext.Contacts` directly. Grep for `IgnoreQueryFilters` in `ContactCategoryRepository.cs` = **0**; grep for a manual `GroupId` predicate = **0**. The three pre-existing bypass sites (`EventRepository.cs:172`, `GroupRepository.cs:135/147`, `QuestRepository.cs:268`) are untouched by this phase and belong to other entities | closed |
| T-80-03-02 | 80-03 | Tampering | Reorder swap | medium | mitigate | `ContactCategoryService.cs:64-72` — `FindIndex` walks the **ordered list by position**; no arithmetic on `SortOrder` anywhere in the swap path. Boundary guards at `:39` (`index < 0 \|\| index == 0`) and `:50` (`index < 0 \|\| index == ordered.Count - 1`) return `false` before any write. `SwapSortOrderAsync` additionally no-ops if either row fails to resolve through the filter. Four unit facts green: `MoveUpAsync_FirstCategory_ReportsNoMove`, `MoveDownAsync_LastCategory_ReportsNoMove`, `MoveUpAsync_MiddleCategory_SwapsWithPredecessor`, `MoveDownAsync_MiddleCategory_SwapsWithSuccessor` | closed |
| T-80-03-03 | 80-03 | Denial of Service | Category delete | high | mitigate | `ContactCategoryRepository.DeleteWithDependentsLoadedAsync` loads the dependent contacts into the change tracker **before** `Remove`, so the configured `SetNull` applies under both the in-memory test provider and SQL Server. Unit fact `DeleteWithDependentsLoadedAsync_ContactsSurviveWithNullCategory` + service fact `DeleteAsync_RemovesCategoryAndOrphansItsContacts`, both green | closed |
| T-80-03-04 | 80-03 | Elevation of Privilege | — | low | accept | Verified: the four `80-03` commits touch only `QuestBoard.Domain/{Interfaces,Services,Extensions}`, `QuestBoard.Repository/{ContactCategoryRepository,ContactRepository,Extensions}` and unit tests. **Zero controllers, zero views, zero routes.** The auth gate lives on `80-05`'s controller (T-80-05-01). See R-03 | closed |
| T-80-03-SC | 80-03 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-04-01 | 80-04 | Information Disclosure | `ContactCategoryGroupViewModel` | high | mitigate | The type declares exactly three members — `Title`, `IsUngrouped`, `Contacts`. **No count-shaped member exists**, so no view can render one even by accident. This is a structural control, stronger than a per-view assertion: it holds for both the desktop and the mobile index without either being tested for it | closed |
| T-80-04-02 | 80-04 | Elevation of Privilege | `ContactCategoryViewModel` reverse map | high | mitigate | Defence in depth, both layers verified. (1) `ViewModelProfile.cs:112-113` — `CreateMap<ContactCategoryViewModel, ContactCategory>().ForMember(dest => dest.GroupId, opt => opt.Ignore())`. (2) `ContactCategoryViewModel` has **no `GroupId` member at all**, so model binding has no target. (3) `ContactCategoryManagementController.cs:57` stamps `category.GroupId = activeGroupId` from `activeGroupContext`, never from the post | closed |
| T-80-04-03 | 80-04 | Tampering | `ContactCategoryViewModel.Name` | medium | mitigate | `ContactCategoryViewModel.cs:9-11` — `[Required(ErrorMessage = …)]` + `[StringLength(60, ErrorMessage = …)]`. Server-side and enforced via `ModelState.IsValid` in both `Add` (`:47`) and `Edit` POST (`:100`), independent of the `maxlength="60"` on the input | closed |
| T-80-04-04 | 80-04 | Tampering | Rendered category name | high | **transfer** → 80-06 T3 | **Transfer honored — verified, not assumed.** The receiving obligation exists and passes: `ContactsControllerIntegrationTests.cs:1086` `ContactCategory_NameRendersEscaped_AngleBracketsAreEncoded` seeds a category named `<script>alert('x')</script>`, GETs `/Contacts/Index`, and asserts `NotContain("<script>alert('x')</script>")` **and** `Contain("&lt;script&gt;")`. Green. `80-VALIDATION.md` carries the matching row (`CONTACTCAT-13`, `80-06 T3`, filter `~ContactCategory_NameRendersEscaped`, status green). See F2 for the one residual coverage note | closed |
| T-80-04-SC | 80-04 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-05-01 | 80-05 | Elevation of Privilege | Every category write action | high | mitigate | **One class-level `[Authorize(Policy = "DungeonMasterOnly")]` at `ContactCategoryManagementController.cs:11`, covering all seven actions** (`Index`, `Add`, `Edit` GET, `Edit` POST, `Delete`, `MoveUp`, `MoveDown`). Zero `[AllowAnonymous]` and zero action-level `[Authorize]` overrides in the file. Proven by refusal, not inspection: six green facts — `Index_Get_PlayerAccess_ShouldBeBlocked`, `Add_Post_…`, `Edit_Post_…`, `Delete_Post_…`, `MoveUp_Post_…`, `MoveDown_Post_PlayerAccess_ShouldBeBlocked` — i.e. **all five writes**, plus positive DM and Admin access facts. See F1 on the "six actions" wording | closed |
| T-80-05-02 | 80-05 | Information Disclosure | `Index`, `Edit` by id | **critical** | mitigate | Every read crosses the filter and no board id is read from the request. `Index`/`Add` re-render → `BuildManagementViewModelAsync` → `GetOrderedAsync` → filtered `DbContext.ContactCategories`. `Edit` GET `:78` and `Edit` POST `:95` → `contactCategoryService.GetByIdAsync(id)` → `BaseRepository.GetByIdAsync` → `DbSet.FindAsync`, which resolves through the filtered query root and returns `null` for a foreign board → `NotFound()`. The remaining three writes are filtered too: `Delete` → `FirstOrDefaultAsync` on the filtered set (no-op on miss); `MoveUp`/`MoveDown` → `GetOrderedForActiveGroupAsync` then position lookup, returning `false` for an id absent from the board's list. That `FindAsync` honours the filter is **not assumed** — the identical `GetByIdAsync` path is exercised by `ContactCategory_CrossGroup_CreatePost_ForeignCategoryId_IsRefusedAndNotStored` (green) and by the orchestrator's live forged POST against SQL Server. See F3 on test coverage | closed |
| T-80-05-03 | 80-05 | Tampering | CSRF on the five writes | high | mitigate | Counted, not sampled. Controller: exactly **5 `[HttpPost]` and 5 `[ValidateAntiForgeryToken]`**, paired 1:1 at lines `31/32`, `89/90`, `129/130`, `139/140`, `149/150` — `Add`, `Edit` POST, `Delete`, `MoveUp`, `MoveDown`. Views: `@Html.AntiForgeryToken()` present in **every** form on both platforms — `Manage.cshtml` (Add, MoveUp, MoveDown, Delete), `Edit.cshtml` (rename), `Manage.Mobile.cshtml` (Add, MoveUp, MoveDown, Delete), `Edit.Mobile.cshtml` (rename). 10/10 forms | closed |
| T-80-05-04 | 80-05 | Denial of Service | Delete of a populated category | high | mitigate | `ContactCategoryManagementController.cs:129-134` — `Delete` calls only `contactCategoryService.DeleteAsync(id)` then redirects; **zero contact writes in the controller**. `Delete_Post_DungeonMaster_ContactCategory_DeleteOrphans_ContactsSurviveWithNullCategory` reads the contact rows back through `factory.Database.CreateContext()` + `IgnoreQueryFilters()` — a fresh untracked context, not the request's tracker — and asserts both rows survive with `CategoryId == null`. Green. Corroborated live via `sys.foreign_keys` | closed |
| T-80-05-05 | 80-05 | Tampering | Stored markup in a category name | high | mitigate | `grep -rn "Html.Raw\|Markdown\|ToHtml" Views/ContactCategoryManagement/` = **0 matches**. Names render as `@category.Name` / `@Model.Name` (Razor default encoding) in all four view files; `data-category-name="@category.Name"` is attribute-encoded and is consumed only by `confirm()` (text, not HTML). Explicit fact `Index_Get_CategoryNameWithMarkup_IsHtmlEscaped` (`:423`) asserts raw markup absent and `&lt;script&gt;` present. Green | closed |
| T-80-05-06 | 80-05 | Tampering | Category name length | medium | mitigate | Same server-side chain as T-80-02-04 / T-80-04-03: view model `[StringLength(60)]` → `ModelState.IsValid` gate → domain model `[StringLength(60)]` → entity `[StringLength(60)]` → `nvarchar(60)` column. The `maxlength` attribute in the views is convenience, not the control | closed |
| T-80-05-SC | 80-05 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-06-01 | 80-06 | Information Disclosure | Category headings on the index | **critical** | mitigate | **Order of operations verified in source, not inferred from the presence of a filter call.** `ContactsController.Index`: `:35` builds `visibleContacts` via `.Where(c => IsVisibleTo(c, currentUser.Id, includeHidden))`; `:37` maps that filtered list into `contactViewModels`; `:52-66` groups **over `contactViewModels`**. Grouping therefore cannot see a contact the viewer cannot, and a category with nothing visible produces no group and no heading. Pinned in both directions by three green facts: `…_PlayerNeverSeesHeadingForUnrevealedOnlyCategory`, `…_DmWithHiddenToggleOnSeesHeading`, `…_DmWithHiddenToggleOffDoesNotSeeHeading`. Corroborated live (0-contact category sorted first renders no heading) | closed |
| T-80-06-02 | 80-06 | Information Disclosure | Heading contents | high | mitigate | Three components, all present. (1) `ContactCategoryGroupViewModel` carries no count member (T-80-04-01). (2) Both views render the title alone — `Index.cshtml:96-98` and `Index.Mobile.cshtml:82-84` are `<i class="fas fa-tag me-2"></i>@group.Title`, no badge, no parenthetical. (3) The explicit fact exists: `ContactsControllerIntegrationTests.cs:1061-1064` — `NotMatchRegex(@"Merchant Guild\s*\(\d+\)")` and `NotMatchRegex(@"Thieves Union\s*\(\d+\)")` inside `ContactsIndex_CategoryOrdering_UngroupedHeadingAppearsAfterEveryRealCategory`. Green | closed |
| T-80-06-03 | 80-06 | Tampering | Stored markup in a category name | high | mitigate | (1) Razor default encoding on **both** views — `Index.cshtml:97` and `Index.Mobile.cshtml:83`, both bare `@group.Title`. (2) No Markdown routing: `Html.Markdown` appears in the Contacts views only on `Model.Description` and `note.Text`, never on a category name. (3) Escaping fact `ContactCategory_NameRendersEscaped_AngleBracketsAreEncoded` green. See F2 | closed |
| T-80-06-04 | 80-06 | Elevation of Privilege | Manage Categories entry point | medium | mitigate | The link sits inside the pre-existing DM-tier conditional on both platforms — `Index.cshtml:54` `@if (Model.ViewerIsDmTier)` wrapping `:71-73`, and `Index.Mobile.cshtml:41` wrapping `:58-60`. The hidden link is convenience only; the real gate is `ContactCategoryManagementController.cs:11`'s class-level policy, proven by the six player-refusal facts under T-80-05-01 | closed |
| T-80-06-SC | 80-06 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-07-01 | 80-07 | Tampering | Posted category id on Create and Edit | **critical** | mitigate | **Both** post handlers resolve before persisting: `ContactsController.cs:180` (Create) and `:244` (Edit) call `IsCategoryAcceptableAsync(viewModel.CategoryId)` and, on failure, `AddModelError` + re-render — `:505-517` returns `true` only for `null` or an id that resolves through the board-filtered `contactCategoryService.GetByIdAsync`. The check runs **before** `mapper.Map<Contact>(viewModel)` (Create) and before `existingContact.CategoryId = viewModel.CategoryId` (Edit). Two green facts assert refusal **and** unchanged stored state via `IgnoreQueryFilters()` on a fresh context. Independently confirmed live: forged POST with a foreign `CategoryId` refused, 0 rows persisted (direct SQL). Ride-along vectors closed too: `CategoryName`/`CategorySortOrder` are `Ignore()`d on the reverse map (`ViewModelProfile.cs:98-99`) and do not exist on `ContactEntity` | closed |
| T-80-07-02 | 80-07 | Information Disclosure | Category dropdown contents | **critical** | mitigate | `ContactsController.cs:492-500` `PopulateCategoryOptionsAsync` projects `contactCategoryService.GetOrderedAsync()` — the board-filtered ordered read — straight into `SelectListItem`s. No bypass, no re-sort, no second source. It is the **only** producer of `CategoryOptions`, called at all six render sites (`:110, :135, :161, :183, :213, :237, :248, :292`). Two-board facts green: `ContactCategory_CrossGroup_CreateFormDropdownNeverShowsOtherBoardsCategory` and `…_IndexNeverShowsOtherBoardsCategory`, each asserting the other board's name never appears. Confirmed live | closed |
| T-80-07-03 | 80-07 | Information Disclosure | Null active board | high | mitigate | Proven by explicit fact, not inferred from the filter's shape: `ContactCategory_CrossGroup_NullActiveBoard_CategoryReadResolvesToNothing` seeds categories on boards 1 **and** 2, opens a context whose `ActiveGroupId` is fixed at `null`, and asserts `categories.Should().BeEmpty()`. Green. Filter source at `QuestBoardContext.cs:467-470` (T-80-02-01) | closed |
| T-80-07-04 | 80-07 | Elevation of Privilege | Contact form actions | medium | accept | Verified unchanged, not taken on trust: `git diff <phase-base>..HEAD -- ContactsController.cs` filtered to `Authorize\|ValidateAntiForgery\|HttpPost\|HttpGet` returns **zero lines**. `Create`/`Edit`/`Delete` retain `[Authorize(Policy = "DungeonMasterOnly")]` + `[ValidateAntiForgeryToken]` (`:106/116-117`, `:202/219-220`, `:322-323`). See R-04 | closed |
| T-80-07-SC | 80-07 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-08-01 | 80-08 | Tampering | Category name on the details views | high | mitigate | (1) Razor default encoding on both platforms — `Details.cshtml:39` and `Details.Mobile.cshtml:37`, both `<i class="fas fa-tag me-1"></i>@Model.CategoryName`. (2) No Markdown routing — `Html.Markdown` is applied to `Model.Description` and `note.Text` only; the category name never reaches it. (3) Escaping fact in the details suite: `ContactDetailsCategoryTests.cs:61` `ContactDetails_Category_NameWithAngleBracketsRendersEscaped`, asserting raw markup absent and `&lt;script&gt;` present. Green | closed |
| T-80-08-02 | 80-08 | Information Disclosure | Contact details access | medium | accept | Verified unchanged: `git diff <phase-base>..HEAD -- ContactsController.cs` filtered to `IsVisibleTo\|GetContactWithDetailsAsync\|NotFound` returns **zero lines**. `Details` (`:82-102`) still resolves the contact, then applies `IsVisibleTo` and returns `NotFound()` **before** `mapper.Map<ContactViewModel>` — the category line renders inside a page the viewer already passed that check for. Pre-existing IDOR fact `Details_ContactInDifferentGroup_ReturnsNotFound` still green. See R-05 | closed |
| T-80-08-03 | 80-08 | Repudiation | Mobile verification claim | medium | mitigate | **Every** mobile assertion is paired with a non-mobile request to the same URL asserting a mobile-only marker is absent, so a pass cannot come from a string both files emit. All three facts in `ContactCategoryMobileRenderTests.cs` follow the pattern: management page (`category-mgmt-row` present mobile / absent desktop), contacts index (`contact-member-row` present / absent), contact details (`contact-info-value` present / absent). Selection is by real `User-Agent` header via `GetMobileAsync`, never viewport emulation. Green. Corroborated live under a real Pixel 8 / Android 14 UA | closed |
| T-80-08-SC | 80-08 | Tampering | Supply chain | high | accept | Zero package changes — see R-01 | closed |
| T-80-09-01 | 80-09 | Information Disclosure | `contacts.mobile.css` / `modern-card.css` / `contact-form.mobile.css` | low | accept | Verified by diff: the 80-09 range touches exactly five files — three CSS files (+30 lines of colour declarations), one Razor view (1 line, see T-80-09-02) and one new test file. **Zero** query, filter, view-model, controller or repository changes, so no new path exists for cross-board category data. The cross-group isolation suite remains the control. See R-06 | closed |
| T-80-09-02 | 80-09 | Tampering | `Manage.Mobile.cshtml` | low | accept | Verified by diff — the entire change is `class="mb-3"` → `class="mb-3 category-mgmt-add-form"` on an existing `<form>`. One insertion, one deletion. No new input, no new binding target; `@Html.AntiForgeryToken()` on the next line and the class-level `DungeonMasterOnly` policy are untouched. See R-06 | closed |
| T-80-09-SC | 80-09 | Tampering | Supply chain | n/a | accept | Zero package changes — see R-01 | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above `workflow.security_block_on` (high) count toward `threats_open`*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party or another plan)*

---

## Verification Method

ASVS L1 (mitigation must be **present** in the cited file). The six critical cross-board
isolation threats were verified beyond L1 — data flow traced end to end and ordering of
operations checked in source — because this codebase has a documented history of getting this
class wrong (the Phase 49 and Phase 55 leaks the plans cite).

Beyond static verification, every `Automated Command` in `80-VALIDATION.md`'s Per-Task
Verification Map was **re-executed against the current tree** during this audit:

| Filter | Result |
|--------|--------|
| `QuestBoard.IntegrationTests ~ContactCategory` | 37 passed, 0 failed |
| `~ContactCategory_CrossGroup` | 6 passed, 0 failed |
| `~ContactCategory_EmptyHeadingSuppression` | 3 passed, 0 failed |
| `~ContactsIndex_Category` | 4 passed, 0 failed |
| `~ContactDetails_Category` | 3 passed, 0 failed |
| `~ContactCategoryMobileRender` | 3 passed, 0 failed |
| `~ContactCategoryManagement` | 16 passed, 0 failed |
| `QuestBoard.UnitTests ~ContactCategory` | 13 passed, 0 failed |

Run with `--no-build` — the application was running under a debugger and held the build outputs,
so a rebuild was not possible. Binaries dated `2026-08-31 01:07` post-date every phase-80 source
file (latest `2026-08-30 14:24`), so the assemblies under test match the audited source. See F4.

Orchestrator field evidence (live app + real SQL Server, 2026-08-30/31) was treated as
**corroborating** only. Each of the five field observations was independently re-derived from
source before being credited: the forged cross-board POST (T-80-07-01/02), the unique index and
its case-insensitive collation, the two FK delete rules (T-80-02-02/03), heading suppression for
a zero-contact category (T-80-06-01), and real-UA mobile view selection (T-80-08-03).

---

## Unregistered Flags

**None found.**

No `## Threat Flags` section exists in any of the nine SUMMARY files — a search for the string
`threat` across all nine returns zero matches. Absence of flags is **not** evidence of no new
attack surface, so the auditor compensated by enumerating the phase's production surface
directly rather than trusting the executor's silence (see F5):

- **New HTTP surface:** exactly one controller, `ContactCategoryManagementController`, with 7
  actions. All 7 covered by the class-level `DungeonMasterOnly` policy (T-80-05-01).
- **New persisted surface:** one entity/table, `ContactCategories`, plus one nullable FK column
  on `Contacts`. Both carry registered threats (T-80-02-01 through T-80-02-04).
- **New client-bindable members:** `ContactViewModel.CategoryId` (T-80-07-01),
  `ContactCategoryViewModel.{Id, Name}` (T-80-04-02, T-80-04-03). `CategoryName` and
  `CategorySortOrder` are bindable but unpersistable — `Ignore()`d on the reverse map and
  absent from `ContactEntity`.
- **New rendered surface:** category name on the contacts index, contact details and management
  pages, desktop and mobile (T-80-05-05, T-80-06-03, T-80-08-01).

Every item maps to a registered threat. No unregistered attack surface.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-01 | T-80-01-SC … T-80-09-SC (all 9) | The phase installs no packages at all. Verified independently of the claim: `git diff <phase-base>..HEAD -- '*.csproj' 'packages.lock.json' 'package.json' 'Directory.Packages.props' 'NuGet.config'` returns **zero changes** across the entire phase-80 commit range. With no new dependency there is no supply-chain surface to mitigate | Theun Schut | 2026-08-31 |
| R-02 | T-80-01-03 | `80-01` is a planning-documents plan. All five of its commits touch `.planning/` paths only; it writes no code and adds no runtime surface, so it cannot itself disclose data | Theun Schut | 2026-08-31 |
| R-03 | T-80-03-04 | `80-03` adds a repository, a service and their interfaces — no controller, no view, no route. There is no HTTP surface for an authorization gate to sit on; the gate belongs to `80-05`'s controller and is verified there as T-80-05-01 | Theun Schut | 2026-08-31 |
| R-04 | T-80-07-04 | The contact form actions already carried `DungeonMasterOnly` before this phase and the diff confirms not one authorization or antiforgery attribute line changed. Re-litigating a pre-existing, unmodified gate is out of scope for this phase | Theun Schut | 2026-08-31 |
| R-05 | T-80-08-02 | The `Details` action's visibility check is pre-existing, runs before mapping, and is unchanged by this phase (zero diff lines). The category line is additive markup inside a page the viewer has already been authorized for | Theun Schut | 2026-08-31 |
| R-06 | T-80-09-01, T-80-09-02 | The `80-09` gap-closure plan changes three stylesheets (colour declarations only) and adds one CSS class token to an existing form element. No data path, no binding target, no auth or antiforgery change | Theun Schut | 2026-08-31 |

---

## Findings

Raised by the security audit; none is an open threat.

| ID | Finding | Impact |
|----|---------|--------|
| F1 | **T-80-05-01's mitigation text undercounts the surface it protects.** It says "one class-level DungeonMaster policy covering all **six** actions"; `ContactCategoryManagementController` actually exposes **seven** (`Index`, `Add`, `Edit` GET, `Edit` POST, `Delete`, `MoveUp`, `MoveDown`) — the `Edit` GET read appears to have been omitted from the count. Because the attribute is class-level it covers 7/7 regardless, and `Edit` GET is separately protected by T-80-05-02 | Documentation accuracy only. No gap. A future edit that reasons from "six" could miscount |
| F2 | **Escaping facts cover the desktop render only.** T-80-04-04/T-80-06-03's fact GETs `/Contacts/Index` with a default UA, and T-80-08-01's GETs `/Contacts/Details/{id}` likewise — neither is re-run under a mobile UA, so `Index.Mobile.cshtml:83` and `Details.Mobile.cshtml:37` are verified by source inspection (bare `@group.Title` / `@Model.CategoryName`, zero `Html.Raw`) rather than at runtime. The mitigation is Razor's default encoder, which is not per-view configurable here, so the residual risk is a future edit introducing `Html.Raw` in a mobile file only | Residual regression risk. Worth adding the markup-name seed to the existing `ContactCategoryMobileRender` paired-UA tests, which already have the harness |
| F3 | **T-80-05-02 has no cross-board test of its own.** `ContactCategoryManagementControllerIntegrationTests` contains 16 facts, none of which requests `Edit`/`Delete`/`MoveUp`/`MoveDown` with a category id belonging to another board. The mechanism is nonetheless proven — the identical `GetByIdAsync` → `FindAsync` → filtered-query-root path is exercised by `ContactCategory_CrossGroup_CreatePost_ForeignCategoryId_IsRefusedAndNotStored` (green) and by the orchestrator's live forged POST — and the three remaining writes were traced to filtered reads by hand. But the closure rests on a neighbouring plan's fact, not this controller's own | Residual regression risk. A `…_Edit_Get_ForeignBoardCategory_ReturnsNotFound` fact would make this controller self-verifying |
| F4 | **The audit could not rebuild.** `dotnet test` failed with MSB3027/MSB3021 — `QuestBoard.Service (PID 22208)` was holding `QuestBoard.Repository.dll` and `QuestBoard.Domain.dll`. Tests were run `--no-build`; binary timestamps (`2026-08-31 01:07`) post-date every phase-80 source file (`2026-08-30 14:24` latest) and the working tree is clean, so the assemblies match the audited source. Not a security finding, but the evidence is one indirection removed from a clean-room build | Process. Re-run the eight filters after a clean rebuild before shipping |
| F5 | **No `## Threat Flags` section in any of the nine SUMMARY files.** The executor never recorded new attack surface, so the audit could not use the summaries as an input at all. Compensated by enumerating the production surface from the commit diff directly (see Unregistered Flags). No unregistered surface found — but the absence of flags carried zero information here | Process. The section should be emitted even when empty, so "no flags" is distinguishable from "flags not considered" |
| F6 | **T-80-02-03's evidence is by omission in the migration.** `AddContactCategories.cs:32-36` emits the Groups FK with no `onDelete:` argument. That *is* `NoAction` (the `MigrationBuilder` parameter default; `Cascade` would be emitted explicitly), but a reader checking "verify against the generated migration text" will find no literal `NoAction` token to match. The model source is explicit (`QuestBoardContext.cs:269-273`) and the live `sys.foreign_keys` row reads `NO_ACTION`, so all three agree | Documentation accuracy. Cite the model line and the live schema alongside the migration, not the migration alone |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open (blocking) | Open (non-blocking) | Run By |
|------------|---------------|--------|-----------------|---------------------|--------|
| 2026-08-31 | 43 | 43 | 0 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

Disposition split: 27 `mitigate`, 15 `accept`, 1 `transfer`.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (R-01 … R-06)
- [x] Transfer disposition verified at its receiving plan, not assumed (T-80-04-04 → 80-06 T3)
- [x] `threats_open: 0` confirmed — no open threat at or above `high`
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-31

> Evidence dependency: 23 of the 27 `mitigate` verdicts cite a green test as part of their
> evidence. Verified at 85 category-scoped facts (72 integration + 13 unit), 0 failures,
> run `--no-build` against binaries that post-date every phase-80 source file. Re-verify if
> that suite changes — and see F4 before shipping.
