# Phase 98: Characters, Contacts, DM profile and Players UI conversion - Pattern Map

**Mapped:** 2026-11-13
**Files analyzed:** about 60 (26 views, 13 sheets, 4 scripts, 2 partials, 1 repository, 11+ test files)
**Analogs found:** all but two (no-analog items at the end)

Paths are repository-relative. Phase 97's map (`.planning/phases/97-.../97-PATTERNS.md`) still applies for: `@section Styles` linking, no-inline-code regexes, layered sheet rules, `UiFoundationLayerGuardTests` appends, the `RetiredFiles` idea, and the scratch tooling (copy from `~/qb-ui-review/phase-95/tools`). Only what is new or different is below. Re-read the shared arrays before appending (Phases 96/97 already grew them).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `Views/Shared/_CharacterAvatar.cshtml` (new) | partial | request-response | `Views/QuestLog/Details.cshtml:94-100` (img plus hidden placeholder), `components.css:752-766` | exact |
| `QuestBoard.Repository/QuestRepository.cs` (picture flag after map, lines 84-88 and 114-118) | repository | CRUD read | `QuestBoard.Repository/CharacterRepository.cs:15-32` | exact |
| `Views/Quest/Details.cshtml` (160, 295, 400), `Quest/Manage.cshtml` (435, 552), `QuestLog/Details.cshtml` (94) | view (tag swap only) | request-response | themselves, to `_CharacterAvatar` | exact |
| `Views/Shared/_ImageCropDialog.cshtml` (new, replaces 10 copies) | partial | event-driven | dialog copies in `Characters/Create.cshtml:151-199`; Bootstrap modal in `Series/Details.cshtml` (Phase 97) | role-match |
| `wwwroot/js/image-crop.js` (classic script to ES module) | script | event-driven | `wwwroot/js/event-series-form.js`, `quest-manage.js`, `avatar-fallback.js` | role-match |
| `wwwroot/js/contact-notes.js` (new, from duplicated Contacts/Details scripts) | script | event-driven | `wwwroot/js/event-details.js` (data attributes, confirm) | role-match |
| `wwwroot/js/contact-tags.js` (global init to self-initialising, data attributes) | script | event-driven | `event-series-form.js` header | role-match |
| row navigation on phone Characters/Contacts rows | script/markup | event-driven | `wwwroot/js/row-link.js` (tables), `home.mobile.css` stretched link (cards) | role-match |
| `wwwroot/css/member-cards.css`, `contact-form.css` (new, merged from characters/contacts and mobile sheets) | stylesheet | n/a | `quest-form.css`, `events-overview.css`, `group-picker.css` | exact |
| `characters.css`, `contacts.css`, `dm-profile.css`, `image-crop.css` (leave `_Layout.cshtml:26-29`) | stylesheet | n/a | Phase 97 removal of `calendar.css` etc. from layout | exact |
| Six merged views (Characters/Details, Contacts/Details, Contacts/Create, Contacts/Edit, ContactCategoryManagement/Edit, DungeonMaster/EditProfile) | view | CRUD | `Views/Events/Create.cshtml` (single view, `@section Styles/Scripts`); Phase 96 merges (`Account/Login`) | exact |
| Remaining twins (Characters/Index, Contacts/Index, Manage, Players/Index, DM Profile, Characters Create/Edit unless D-09 merges) | view | CRUD | `Views/Events/Index(.Mobile).cshtml`, `Agenda/Index(.Mobile)` | role-match |
| `Tests/CharactersConversionGuardTests.cs` (new) | test | file scan | `Tests/EventsConversionGuardTests.cs`, `Tests/AccountConversionGuardTests.cs` (RetiredFiles) | exact |
| `CharacterAvatarRenderTests`, `ImageCropDialogRenderTests` (new) | test | request-response render | `Controllers/CharactersControllerIntegrationTests.cs:744-792`, `Tests/AgendaConversionTests.cs` (AgentFor) | exact |
| `CharactersTwinAnswersTests` (new) | test | render | `Tests/AgendaConversionTests.cs` (Theory on mobile UA) | role-match |
| Rewritten guards (`MobileViewsTests` Characters/Contacts/DM facts, `ContactCategoryContrastGuardTests`, `ContactsTags*MarkupTests`, `ContactDetailsCategoryTests`, `ContactCategoryManagementControllerIntegrationTests`) | test | render / file scan | themselves | exact |
| `UiFoundationLayerGuardTests.cs` arrays | test | file scan | itself (`LayeredFiles` at lines 54+) | exact |

## Pattern Assignments

### 1. `_CharacterAvatar.cshtml` and the picture flag (D-04)

**Existing markup to replace** (`Views/QuestLog/Details.cshtml:94-99`; Quest/Details and Manage are the same shape):
```cshtml
<img src="@Url.Action("GetCroppedPicture", "Characters", new { id = player.Character.Id })" alt="" class="avatar-32" />
<span class="avatar-32 avatar-32--placeholder" hidden aria-hidden="true">
    <i class="fas fa-user" aria-hidden="true"></i>
</span>
```
Styles already in `components.css:752-766` (`.avatar-32`, `.avatar-32--placeholder`). `wwwroot/js/avatar-fallback.js` reveals the hidden sibling on an image error; keep it loaded (safety net). Partial shape (RESEARCH Code Examples): picture present renders the img plus hidden sibling; absent renders the placeholder not hidden, no img, no URL. `QuestLog/Details` passes decorative (empty alt).

**Repository analog** (`QuestBoard.Repository/CharacterRepository.cs:21-32`):
```csharp
var imageFlags = await DbContext.Characters
    .Select(c => new { c.Id, HasImage = c.ProfileImage != null })
    .ToDictionaryAsync(x => x.Id, x => x.HasImage, token);
foreach (var character in characters)
    character.HasProfilePicture = imageFlags.GetValueOrDefault(character.Id);
```
Apply in `QuestRepository.GetQuestWithDetailsAsync` (line 84-88) and `GetQuestWithManageViewDetailsAsync` (114-118) after `Mapper.Map<Quest>(entity)`, restricted to `ids.Contains(c.Id)` of the signups. Read-only scalar query, so the "writes to Events/Quests through the change tracker" rule is not engaged. `Domain.Models.Character.HasProfilePicture` already exists (`Character.cs:16-17`).

**Test analog:** `CharactersControllerIntegrationTests.cs:744-792` (seed `CharacterImageEntity`, GET, `Contain($"GetCroppedPicture/{id}")` vs `NotContain`). Reuse `BoardPageCharacterLinksTests.SeedFinalizedQuestAsync` for the quest pages. Add a guard that no view under `Views/Quest` or `Views/QuestLog` contains `Url.Action("GetCroppedPicture"`.

### 2. `image-crop.js` as ES module plus `_ImageCropDialog.cshtml` (D-01 to D-03)

**Module shape analog:** `wwwroot/js/quest-manage.js:1-10` and `event-series-form.js` header: a header comment, "nothing is exposed globally and no markup is built from strings", wiring from data attributes, token from meta:
```js
function antiforgeryToken() {
    const meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.content : '';
}
```
Loaded as `<script type="module" src="~/js/x.js" asp-append-version="true"></script>` in `@section Scripts` (see `Events/Create.cshtml` bottom). Cropper vendor script stays a classic `<script src="~/lib/cropperjs-2.1.1/cropper.min.js">` before the module (today `Contacts/Create.cshtml:156-158`). The ten `initImageCrop({ fileInputId: ..., hiddenCroppedInputName: 'CroppedPictureFile', aspectRatio: 1 })` inline calls go; the module self-initialises from `data-crop-*` attributes on the file input (RESEARCH 3c). Control logic (`nudge`, `resizeBy`, `reset`, STEP 8, clamp to image rect, `keydown` on `#cropperSelectionEl` only, `role="status"` via `textContent`) is in RESEARCH "Crop controls on the public API"; copy it. Do not set the `keyboard` attribute (document-level, Delete removes the selection). Keep the existing `$center('contain'); $initSelection(true, true)` Reset sequence (`image-crop.js:197-198`).

**Dialog partial:** one opaque modal replacing ten copies (starts: `Characters/Create.cshtml:151`, `Edit:153`, `Contacts/Create:106`, `Edit:119`, DM EditProfile, plus phone twins). Drop `bg-dark text-light` and `btn-close-white`. Keep ids `cropPhotoModal`, `cropperImageEl`, `cropperSelectionEl`, `cropConfirmBtn`, `cropCancelBtn`, `fileSizeError` (toggle `hidden`, not `style.display`). `image-crop.css` opens `@layer pages {`, no `!important` (the current file has `height: 320px !important` at the phone breakpoint to be handled with a layered rule or class), hosts link it from `@section Styles`.

**Static guards to add** (copy `CalendarMonthModule_WritesNoMarkupFromStrings` in `EventsConversionGuardTests.cs:563`): no `innerHTML/outerHTML/insertAdjacentHTML`, no `document.addEventListener('keydown'`, calls `$moveTo` and `$change`, partial has no `bg-dark`/`text-light`/`style=`.

### 3. Merged views: Styles/Scripts sections, no inline code

**Analog:** `Views/Events/Create.cshtml` lines 9-11 and tail:
```cshtml
@section Styles {
    <link rel="stylesheet" href="~/css/event-forms.css" asp-append-version="true" />
}
...
@section Scripts {
    @{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }
    <script type="module" src="~/js/event-series-form.js" asp-append-version="true"></script>
}
```
`_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml` all render `Styles` and `Scripts`, so one merged view serves both layouts. Phone differences become CSS-only (`.mobile-layout` scope, chosen by UA not width: RESEARCH Pitfall 11). Delete the `.Mobile.cshtml` and its sheet; the layout block `_Layout.cshtml:26-29` loses `characters.css`, `contacts.css`, `dm-profile.css`, `image-crop.css` as each page links its own.

**Merged-twin retirement test** (`AccountConversionGuardTests.cs:30-40`, `:148-155`):
```csharp
private static readonly string[] RetiredFiles = [ "QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml", ... ];
[Fact] public void RetiredFiles_AreGone() { foreach (var path in RetiredFiles)
    File.Exists(ResolveRepoFile(path)).Should().BeFalse($"{path} was merged into its desktop view and must stay deleted"); }
```
Put the six `.Mobile.cshtml` files and `character-detail.mobile.css`, `contact-detail.mobile.css`, `contact-form.mobile.css`, `dm-editprofile.mobile.css` etc. (as actually deleted) in `CharactersConversionGuardTests.RetiredFiles`.

**Merged views need a phone-UA test** (Contacts/Details and ContactCategoryManagement/Edit had none): copy the Theory shape from `Tests/AgendaConversionTests.cs:22-34,120-122` (`MobileUserAgent` const, `AgentFor(bool mobile)`, `[Theory][InlineData(false)][InlineData(true)]`); assert the single view renders under the phone UA and no longer asserts `*.mobile.css` for merged pages. Existing mobile fact shape to rewrite to the same intent: `MobileViewsTests.cs:1096-1110` (`html.Should().Contain("characters.mobile.css")` becomes the merged sheet name plus the pinned hook classes).

### 4. Row navigation on the phone lists (replaces inline `onclick="window.location.href=..."`)

Phone rows are cards (`Characters/Index.Mobile.cshtml:21,70`, `Contacts/Index.Mobile.cshtml:11`), so use the stretched-link pattern, not `row-link.js` (tables only):
```css
/* home.mobile.css:57-66 */
.quest-card-mobile .row-nav-link::after { content: ""; position: absolute; inset: 0; }
.quest-card-mobile:has(.row-nav-link:focus-visible) { outline: var(--qb-focus-width) solid var(--qb-focus-outer); ... }
```
Anchor `min-height: var(--qb-hit-min)`. Players/Index table rows, if whole-row click is wanted: `row-link.js` with `<tr data-row-href="@Url.Action(...)">` (`Events/Index.cshtml:65`, module loaded at `:146`).

### 5. Confirm on submit and notes script (`contact-notes.js`)

**Analog:** `wwwroot/js/event-details.js:70-78`:
```js
document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        if (!confirm(form.dataset.confirmMessage)) { event.preventDefault(); }
    });
});
```
View side: build the sentence in the view's C# block and write `data-confirm-message="@msg"` (`Events/Details.cshtml:78,87`). Replaces `onsubmit confirm(` on Characters/Details (`:93`), Contacts/Details (two). Wording byte-identical. Notes edit toggle: the `style="display: none;"` on `.note-edit-form` becomes the `hidden` attribute; the module toggles `hidden`, builds nothing from strings.

### 6. Tagify data hand-off (`contact-tags.js`)

Today: `initContactTags({ inputId: 'TagsInput', whitelist: @Html.Raw(Json.Serialize(Model.AvailableTagNames)) })` inline in four views (`Contacts/Create.cshtml:165`, `Create.Mobile:157`, `Edit:178`, `Edit.Mobile:169`). The module self-initialises from the input: put the whitelist in a `data-whitelist` attribute (encoded by Razor, parsed with `JSON.parse` in the module). Source of the data-attribute convention: `event-series-form.js` header (`data-collision-url`, `data-event-id`). No `@Html.Raw` in views (the converted-views guard scans for it in linked partials; keep it out here too).

### 7. Sheets: `member-cards.css`, `contact-form.css`, the Main badge

Open with `@layer pages {` (analog `quest-vote.css`, `home.mobile.css`); tokens only; no hex/rgb, no `!important`, no `transition: all` (`characters.css` has six), no planning IDs. Static Main badge replaces `characters.css:191-213` (pulse and `@keyframes main-badge-pulse`); see RESEARCH "Static Main badge". `.mobile-layout`-scope phone rules. Parchment tables allowed only where they match the Direction A renders (D-07).

Append to `UiFoundationLayerGuardTests.cs` `LayeredFiles` (`("member-cards.css","pages")`, `("contact-form.css","pages")`, `("image-crop.css","pages")`, surviving mobile sheets) and `NoLiteralFiles`; remove names of deleted sheets in the same commit. Correct the stale `!important` guard comment per WINDOWS #28 if this phase is first to touch it.

### 8. `CharactersConversionGuardTests.cs` (new)

**Analog:** `Tests/EventsConversionGuardTests.cs`: copy the lists `AreaFiles`, `ConvertedViews`, `RetiredFiles` (line ~85), `ConvertedSheets`, `ViewSheetLinks`; the facts `ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `ConvertedSheets_OpenThePagesLayer/HaveNoImportantFlag/HaveNoColourLiteral`, `ConvertedViews_LinkTheirSheets`, `PhaseScope_IsFullyConverted` (`:631`, fails if an area view is not listed), `Layouts_LinkNoAreaSheet` (`:664`), plus the module "WritesNoMarkupFromStrings" facts. Inline-code regexes (`UiFoundationMobileShellGuardTests.cs:105-108`): `\sstyle\s*=`, `<style\b`, `<script\b(?![^>]*\bsrc=)[^>]*>`, `\son[a-z]+\s*=`. Write planning-reference regexes as fragments so the test never contains the forbidden strings. Repo-root helper `ResolveRepoFile`/`FindRepositoryRoot` copied as in `QuestsConversionGuardTests.cs:22-37`. Missing files are skipped, never removed from guards (DoC-8).

### 9. D-09 Characters Create/Edit merge (decision pending)

No code analog needed beyond section 3. Candidate only; merges on the operator's explicit yes at Gate 1. If it merges, tests that change: `MobileViewsTests` `GetMobilePage_CharacterCreate_...` (`:1142`) and `..._CharacterEdit_...` (`:1167`) (`character-form-card`, `character-form.mobile.css`). Phone Classes required marker (Q-51) becomes the single markup.

## Shared Patterns

- **Foundation layers and tokens:** `tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css`; area sheets `@layer pages`. Focus ring for `[tabindex]` and `.row-nav-link` lives in `base.css:135`, no per-page `:focus-visible`.
- **No inline code:** enforced by `CharactersConversionGuardTests`; modules loaded `type="module"` in `@section Scripts`.
- **Dates:** DM profile undated quest, `@if (quest.Date.HasValue) { @Html.WallClock(quest.Date.Value, "date") } else { <text>Date TBD</text> }` (wall-clock, never `LocalTime`; `HtmlHelperExtensions.cs:33`). A-characters-043 quest-history raw date goes to the helper matching its meaning.
- **Markdown editor:** `_MarkdownEditor.cshtml:20` already renders `.markdown-editor-hint`; verify on the six editor pages, do not edit.
- **Comments:** plain-language why, no D-nn/Q-nn/A-characters-nnn/phase IDs in views, CSS, JS or C#.
- **Test userdata:** unique usernames per class (`AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "name", "name@test.com")`), `TestDataHelper.ClearDatabaseAsync`, `TestDataHelper.CreateTestCharacterAsync(...)`.
- **Scratch tooling:** copy from `~/qb-ui-review/phase-95/tools` into `~/qb-ui-review/phase-98/tools` (crop probe: `~/qb-ui-review/phase-94/audit/characters/probe1.mjs`); no review branch is created (D-07).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| Crop control row (nudge/resize/Reset buttons, status line, Tab order) | script + partial | event-driven | No in-repo control driving a Cropper selection; use RESEARCH 3b/3c and the vendored `cropper.min.js` API |
| Manage Categories table in a responsive container at 320px (Q-117) | view + css | n/a | Closest is the Phase 96 `AccessDenied` overflow fix and Phase 97 table containers; shape decided at Gate 1 |

## Metadata

**Analog search scope:** `QuestBoard.Service/{Views,wwwroot/css,wwwroot/js}`, `QuestBoard.Repository`, `QuestBoard.IntegrationTests/{Tests,Controllers,Mobile}`, Phase 97 PATTERNS.
**Files scanned:** about 25 read or grepped; RESEARCH sections 1c, 3, 4 and Code Examples used for the full inventory (line numbers there were verified 2026-11-13).
**Pattern extraction date:** 2026-11-13
