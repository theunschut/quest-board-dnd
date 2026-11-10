# Phase 95: Quests and Quest Log UI conversion - Pattern Map

**Mapped:** 2026-11-10
**Files analyzed:** 24 new/modified file groups (25 views, 13 sheets, 6 scripts, 1 helper pair, 4 test files)
**Analogs found:** 20 / 24 (all analog paths verified git-tracked source; no `.gsd` mirror paths)

## Important finding about "already converted" analogs

Phase 94 landed the foundation (layouts, `tokens/base/components/shell` sheets) and converted **only the group picker page** into the `@layer pages` style. No view in `Views/` yet uses `<script type="module">`, and no ES module exists under `wwwroot/js` (only `site.js`, `markdown-editor.js`, `contact-tags.js`, `image-crop.js`, all classic scripts). Phase 95 therefore sets the module precedent; the planner should treat module structure as "no analog, follow ui-guidelines". Every analog below is the closest existing code, not a finished conversion.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `Views/Quest/Index.cshtml` (poster board) | view | request-response | itself (current) + `Views/Quest/Index.Mobile.cshtml` | self / role-match |
| `Views/Quest/Index.Mobile.cshtml` | view | request-response | itself | self |
| `Helpers/QuestPosterCatalog.cs`, `Helpers/QuestPosterPicker.cs` | utility | transform | `Helpers/MoveCopy.cs` (static switch helper), `Helpers/CrossBoardRouteTarget.cs` | role-match |
| `QuestBoard.UnitTests/Helpers/QuestPosterPickerTests.cs` | test | transform | `QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs` | exact |
| `wwwroot/css/quests.css`, `quests.mobile.css` (rewrite into `@layer pages`) | config (stylesheet) | n/a | `wwwroot/css/group-picker.css` | role-match (only converted page sheet) |
| `wwwroot/css/components.css` (+`.move-control-reason`, `.avatar-32`) | config | n/a | `components.css` `.row-nav-link` block (lines 596-610) | exact |
| `wwwroot/js/quest-board.js` (masonry) | utility | event-driven | `wwwroot/js/site.js` lines 88-144 (current masonry) | exact (moved code) |
| `wwwroot/js/quest-form.js`, `quest-details.js`, `quest-manage.js`, `avatar-fallback.js` | utility | event-driven | inline scripts: `Quest/_QuestFormScripts.cshtml`, `Quest/Details.cshtml` 1035-1062 | exact (moved code) |
| `wwwroot/js/markdown-editor.js` | utility | event-driven | itself (keep classic script and globals) | self |
| `Views/Shared/_MarkdownEditor.cshtml` | view (partial) | request-response | itself | self |
| `Views/Quest/_QuestFormScripts.cshtml` | view (partial) | event-driven | itself; consumer pattern `Views/Contacts/Create.cshtml` 154-160 | self |
| `Views/Quest/Details*.cshtml`, `Create*`, `Edit*`, `CreateFollowUp*`, `Manage*` | view | CRUD / request-response | `Views/GroupPicker/Index.cshtml` (page markup), `Views/Account/Login.Mobile.cshtml` (`@section Styles`) | partial |
| `Views/QuestLog/*` (merge Details, EditRecap) | view | request-response | `Views/Move/Index.cshtml` (layout-only, dates via helper) | partial |
| `Views/Move/Index*.cshtml` | view | request-response | itself (already uses `Html.WallClock`) | self |
| `Views/Shared/_Layout.cshtml`, `_Layout.Mobile.cshtml` (global sheet list trims) | config | n/a | `_Layout.GroupPicker.cshtml` line 16 (per-page sheet link) | role-match |
| `QuestsConversionGuardTests.cs` (new) | test | transform (file scan) | `Tests/UiFoundationViewLibraryGuardTests.cs`, `Tests/UiFoundationLayerGuardTests.cs`, `Tests/BoardPageStaticGuardTests.cs` | exact |
| `QuestDetailsSignupFormTests.cs`, `MarkdownEditorMarkupTests.cs`, `QuestBoardEmptyStateTests.cs` (new) | test | request-response | `Mobile/MobileViewsTests.cs` lines 14-75 | exact |
| `wwwroot/images/**/*.webp` | static asset | n/a | none | no analog |
| Scratch capture/mockup tooling (outside repo) | scratch | n/a | none committed | no analog |

## Pattern Assignments

### `Views/Quest/Index.cshtml` desktop poster board (view, request-response)

**Analog:** current `Views/Quest/Index.cshtml` (what to replace) and `Index.Mobile.cshtml` (the already-correct pieces to copy).

**Keep: link target logic** (Index.cshtml line 115, same on Index.Mobile line 34-36). Anchor must stay exactly `<a class="row-nav-link" href="...">` (`Controllers/RowNavigationAccessibilityTests.cs` pins it):
```csharp
var navUrl = currentUserId.HasValue && currentUserId.Value == quest.DungeonMaster?.Id
    ? Url.Action("Manage", "Quest", new { id = quest.Id })
    : Url.Action("Details", "Quest", new { id = quest.Id });
```

**Keep: signed-up and seal alt logic** (Index.cshtml lines 71-73, 86-98):
```csharp
var currentUserId = ViewBag.CurrentUserId as int?;
var isUserSignedUp = currentUserId.HasValue && quest.PlayerSignups.Any(ps => ps.Player.Id == currentUserId.Value);
...
alt="@((boardType == BoardType.Campaign ? (quest.IsClosed ? "Closed" : "Open") : (quest.IsFinalized ? "Finalized" : "Open"))) Quest"
```

**Remove (do not copy):** `style="background-image..."` (line 80), `style="bottom:..%"` (86, 95), `onclick="window.location.href=..."` (83), `data-image-width/height` (81-82), `.GetHashCode()` poster/seal picks (50, 61; not stable per process), `SelectPosterByContent` (33-54), `.ToString("MMM dd, yyyy 'at' h:mm tt")` (135), the description line (120).

**Date pattern to copy** (`Views/Move/Index.cshtml` line 35):
```cshtml
Session @Html.WallClock(quest.FinalizedDate.Value, "date-time")
```

**Phone-side conventions to copy to the desktop poster text** (`Index.Mobile.cshtml` 95): `quest.DungeonMaster?.Name ?? "Unknown DM"`; CR hidden on `BoardType.Campaign` (97-102); `@section Styles { <link href="~/css/home.mobile.css" asp-append-version="true" rel="stylesheet" /> }` (13-15).

**Phone card to convert:** lines 75-76 `<div class="quest-card-mobile" onclick="window.location.href='@navUrl'">` becomes a card with the stretched `row-nav-link` (see CSS below). Lines 18-25 empty state moves onto `.empty-state` with the same copy.

### `Helpers/QuestPosterCatalog.cs` + `QuestPosterPicker.cs` (utility, transform)

**Analog:** `QuestBoard.Service/Helpers/MoveCopy.cs` (namespace and shape: file-scoped `namespace QuestBoard.Service.Helpers;`, XML doc, `public static class`, expression-bodied `switch`).
```csharp
namespace QuestBoard.Service.Helpers;

/// <summary> ... </summary>
public static class MoveCopy
{
    public static string ControlReason(MoveBlockReason reason) => reason switch
    {
        MoveBlockReason.Closed => "Closed in the Quest Log.",
        ...
        _ => string.Empty
    };
}
```
Use `RESEARCH.md` Pattern 1 skeleton for the body (`unchecked((uint)questId * 2654435761u)`, never `string.GetHashCode()`). Doc comments carry no planning IDs.

### `QuestBoard.UnitTests/Helpers/QuestPosterPickerTests.cs` (test)

**Analog:** `QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs` lines 1-30: no `using Xunit/FluentAssertions` (global usings), `namespace QuestBoard.UnitTests.Helpers;`, `[Fact]` methods named `Method_Condition_Result`, FluentAssertions `.Should()`.
```csharp
public class CrossBoardRouteTargetTests
{
    [Fact]
    public void TryFromLocalUrl_CanonicalShape_ResolvesToExpectedKindAndId()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest/Details/42", out var target);
        found.Should().BeTrue();
        target!.Id.Should().Be(42);
    }
```
Cases to mirror: same id gives same poster across calls; sequential ids spread over the pool; 200-char title never yields a poster that cannot hold it (falls to `p1`); Poster3 never returned.

### `wwwroot/css/quests.css` / `quests.mobile.css` / new page sheets (stylesheet, `@layer pages`)

**Analog:** `wwwroot/css/group-picker.css` (the only converted page sheet). Whole file is one `@layer pages { ... }` block, tokens only, a header comment saying why, no `!important`, no literals:
```css
@layer pages {
  @media (max-width: 767.98px) {
    .group-picker .btn {
      min-height: var(--qb-hit-min);
      min-width: var(--qb-hit-min);
    }
    .group-picker .card-title,
    .group-picker .card-text { overflow-wrap: anywhere; }
  }
}
```
Note: the phone sheet here carries an `@media` because the page has no twin; `.Mobile` sheets use `shell.mobile.css` layer `components` per `UiFoundationLayerGuardTests.LayeredFiles`. **Register each new sheet in that guard's `LayeredFiles` and `NoLiteralFiles` arrays** (lines 53-64) and keep `LinkedBoardCalendarStyleTests` selectors flat rules (see Shared Patterns).

**Stretched card link** (RESEARCH Pattern 2; base of the existing class is `components.css` 601-610):
```css
.row-nav-link { color: inherit; text-decoration: none; }
.row-nav-link:hover, .row-nav-link:focus { color: inherit; text-decoration: underline; }
```
The focus ring rule being supplemented is `base.css` 135-144 (outline `--qb-focus-outer`, `outline-offset: var(--qb-focus-offset)`, `box-shadow: 0 0 0 var(--qb-focus-offset) var(--qb-focus-inner)`); reuse those exact tokens in the `.quest-poster:has(.row-nav-link:focus-visible)` rule.

### `wwwroot/css/components.css` additions (`.move-control-reason`, `.avatar-32`)

**Analog:** the `.row-nav-link` rule in the same file (lines 596-610): sits inside the existing `@layer components { ... }` block, a "why" comment before the rule, token values only. Remove `.move-control-reason` from `move-entry.css` and `mobile.css` in the same commit.

### `wwwroot/js/quest-board.js`, `quest-form.js`, `quest-details.js`, `quest-manage.js` (utility, event-driven)

**No existing module analog.** The code to move, with locations:
- Masonry: `site.js` 88-144 (`setCardHeights` reads `data-image-*` and writes `card.style.height`; `layoutMasonry` positions `container.children`); callers at 187, 392-400. Replace height source with CSS `aspect-ratio`; keep `getComputedStyle(document.documentElement).getPropertyValue('--card-width')`.
- Proposed dates: `site.js` 2-56 (`addProposedDate` emits `onclick="removeProposedDate(this)"` at line 21; convert to `data-action`).
- Role select / Spectator: `Quest/Details.cshtml` 1035-1062. Bug to fix: line 1039 `document.querySelectorAll('.card.modern-card.mb-3')` also matches DM Controls; target a `[data-vote-section]` wrapper and toggle `hidden`, not `style.display`.
- Submit guard: `Quest/_QuestFormScripts.cshtml` 10-51. **Keep exact selector** `document.querySelector('form[action*="/Quest/"]')` (line 16), because the partial has 26 consumers and the navbar Logout form precedes the page form.

**Delegation pattern** (RESEARCH Pattern 3): one `document.addEventListener('click', e => { const el = e.target.closest('[data-action]'); ... })`. Loaded as `<script type="module" src="~/js/quest-details.js" asp-append-version="true"></script>` inside `@section Scripts`.

**Page script include precedent** (`Views/Contacts/Create.cshtml` 154-157):
```cshtml
@section Scripts {
    @{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }
    <script src="~/lib/cropperjs-2.1.1/cropper.min.js"></script>
    <script src="~/js/image-crop.js" asp-append-version="true"></script>
```
Layouts render it after `site.js` (`_Layout.cshtml` 299-301; `_Layout.Mobile.cshtml` 249-251).

### `wwwroot/js/markdown-editor.js` + `Views/Shared/_MarkdownEditor.cshtml` (26 consumers)

**Analog:** themselves. Constraints (RESEARCH Pitfall 1, UI-SPEC "Compatibility"): stay a classic script; keep `function initMarkdownEditor(textarea, antiforgeryToken)` (line 9), `textarea.easyMDE = initMarkdownEditor(...)` (130), and `window.markdownAntiforgeryToken` (read at 130; set today at `_QuestFormScripts.cshtml` 56, move to a `<meta name="request-verification-token">` read by the file).

Insert after construction (line 62, before the maxlength block at 70):
```js
const cm = easyMDE.codemirror;
cm.setOption('extraKeys', Object.assign({}, cm.getOption('extraKeys'), { 'Tab': false, 'Shift-Tab': false }));
```
Partial change (`_MarkdownEditor.cshtml` line 14): delete the tooltip `<i ... data-bs-toggle="tooltip" ...>`; add under the textarea (before the char-count `<small>` at 24):
```cshtml
<div id="@(elementId)-help" class="form-text">Supports Markdown formatting. Leave a blank line between paragraphs.</div>
```
Keep `closest('.mb-3')` (js line 72) and `.markdown-editor-char-count-current` markup. Make the required marker `aria-hidden="true"` (line 17). Add `data-editor-label="@Model.Label"` on the textarea (line 20).

`Views/Quest/_QuestFormScripts.cshtml` must keep lines 53-54 (`~/lib/easymde-2.21.0/easymde.min.css` and `.js`); `UiFoundationViewLibraryGuardTests.QuestFormPartial_LoadsEditorFromLib` (lines 67-79) pins them.

### `Views/Quest/Details*.cshtml` signup form (view, CRUD)

**Analog:** `Details.cshtml` lines ~553-590 (desktop selects) for the phone selects: names must stay `CharacterId` (558) and `selectedRole` (575, `id="selectedRole"`). Copy that markup verbatim into `Details.Mobile.cshtml` above the voting section. Existing pinned strings in `_Calendar*` stay (see Shared Patterns). `Details.Mobile.cshtml` has no selects today.

### `Views/Quest/Create|Edit|CreateFollowUp(.Mobile).cshtml` (view, CRUD)

**Analog:** `Views/GroupPicker/Index.cshtml` lines 1-30 for the foundation card shape:
```cshtml
<div class="card modern-card">
    <div class="card-header modern-card-header"><h2 class="mb-0">...</h2></div>
    <div class="card-body modern-card-body">
```
Action row pattern is in `.claude/ui-guidelines.md` (Cancel left, commit right after `<hr>`). Edit/CreateFollowUp merge candidates per RESEARCH Q6 (tests to change: `MobileViewsTests` Edit ~887-888, CreateFollowUp ~922-923). Nothing merges without operator yes.

### `Views/QuestLog/Details` and `EditRecap` merged views

**Analog:** `Views/Move/Index.cshtml` (single view serving both layouts, dates via helper, no `@media`). When a `.Mobile.cshtml` is deleted, remove it from `AmbientClockSeamTests` file lists (it throws `FileNotFoundException`; `BoardPageStaticGuardTests` skips missing files via `File.Exists`).

### `QuestsConversionGuardTests.cs` (new, test, file scan)

**Analog:** `Tests/UiFoundationViewLibraryGuardTests.cs`. Copy its header verbatim: `ResolveRepoFile`/`FindRepositoryRoot` (lines 16-35, finds `*.slnx` or `.git`), and the view enumerator:
```csharp
private static IEnumerable<string> NonLayoutViews()
{
    string[][] roots = [["QuestBoard.Service", "Views"], ["QuestBoard.Service", "Areas"]];
    foreach (var root in roots)
    {
        var folder = ResolveRepoFile(root);
        if (!Directory.Exists(folder)) continue;
        foreach (var file in Directory.EnumerateFiles(folder, "*.cshtml", SearchOption.AllDirectories))
            if (!Path.GetFileName(file).StartsWith("_Layout", StringComparison.Ordinal))
                yield return file;
    }
}
```
Assertion style (lines 73-79): `File.ReadAllText(...).Should().Contain(x, because: "...")`.
CSS checks reuse `UiFoundationLayerGuardTests` helpers: `StripComments` (76), `StripUrls` (78-79), `ImportantOutsideAllowedBlocks` (119+), `EnclosingPreludes` (84-108), reduced-motion prelude regex (110-111), `NoLiteralFiles` (64). Planning-reference scan regex pattern from `BoardPageStaticGuardTests` 37-44 (build forbidden literals from fragments so the test file does not contain them). Each test class is in `namespace QuestBoard.IntegrationTests.Tests;` and has no collection fixture (pure file scan).

### UA-selection / rendered-markup tests (`QuestDetailsSignupFormTests`, `QuestBoardEmptyStateTests`, `MarkdownEditorMarkupTests`)

**Analog:** `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` lines 14-75:
```csharp
public class MobileViewsTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) ... Mobile/15E148 Safari/604.1";
    private const string DesktopUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ... Chrome/120.0.0.0 Safari/537.36";
    ...
    var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_home01", "dm_home01@test.com", name: "DM Home01");
    await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Open Quest Home01");
    var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_home01", "player_home01@test.com");
    var (response, html) = await GetWithUserAgentAsync("/quests", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    html.Should().Contain("quest-card-mobile");
    html.Should().NotContain("fantasy-quest-card");
```
Use unique usernames per test (`dm_home01` style) because the factory is shared. Existing phone marker assertions at 62-166 stay true after conversion; add the `Selected Adventurers` / `Adventurers signed up` assertion for the phone board.

### Layout sheet list (`_Layout.cshtml` 28, 34-36; `_Layout.Mobile.cshtml` 22-23)

**Analog:** `_Layout.GroupPicker.cshtml` line 16, per-page link: `<link rel="stylesheet" href="~/css/group-picker.css" asp-append-version="true" />`, and `Views/Account/Login.Mobile.cshtml` 5-7:
```cshtml
@section Styles {
    <link href="~/css/account.mobile.css" asp-append-version="true" rel="stylesheet" />
}
```
`LocalStylesheetHref` regex in `UiFoundationLayerGuardTests` (66-68) reads the layouts; update the order assertions if sheets leave the global list. Removal order: every consumer links its own copy first, then the layout entry goes in the last source plan (RESEARCH Pitfall 3).

## Shared Patterns

### Foundation focus ring
**Source:** `wwwroot/css/base.css` 135-144. **Apply to:** the poster and log card `:has()` ring, the markdown editor container `:focus-within`. Reuse `--qb-focus-width`, `--qb-focus-outer`, `--qb-focus-inner`, `--qb-focus-offset`.

### One link per card
**Source:** `components.css` 596-610 (`.row-nav-link`) plus the RESEARCH Pattern 2 `::after { inset: 0 }` stretch. **Apply to:** desktop poster, phone quest card, Quest Log cards. Anchor class attribute stays exactly `row-nav-link` (`Controllers/RowNavigationAccessibilityTests.cs` 111-243).

### Dates
**Source:** `Views/Move/Index.cshtml` 24, 35 (`Html.WallClock(value, "date" | "date-time")`); instants use `Html.LocalTime`. **Apply to:** poster date, phone board date, Manage, Quest Log. `ToString("yyyy-MM-ddTHH:mm")` for form values stays. Helpers themselves unchanged.

### Pinned markup that must survive
- `_Calendar.cshtml`: string `isDetailsPage && questOnDay.Quest.Id == currentQuestId` with one `_LinkedDayEntries` after it (`LinkedBoardStaticGuardTests` 119-128).
- `_Calendar.Mobile.cshtml`: order `calendar-date-label-mobile` < `_LinkedAlsoThatDay` < `calendar-vote-row`; `btn-check`; `calendar-date-entry-mobile` (`MobileViewsTests` 343-362).
- `<p class="move-control-reason" id="move-reason">` and disabled Move button classes (`BoardMoveControlTests` ~105, 112); `BoardMoveViewsTests` 393-410 forbid `style="`, `<script`, `text-muted`, `form-text`, `fw-bold`, `Html.Raw`, `<span` in the Move desktop view, and `@media` in both move sheets.
- `.also-that-day--cancelled` in `quests.mobile.css` must remain a flat rule with `text-decoration: line-through` (`LinkedBoardCalendarStyleTests` 121-133).
- DM Controls button class strings on Manage (`QuestManageDmControlsTests`).

### No planning IDs in source
**Source:** `BoardPageStaticGuardTests` 37-44 patterns. **Apply to:** every new/edited view, sheet, script, helper (D-xx, Q-xx, phase/plan numbers, planning doc names, in CSS comments too). Comments explain why in plain language (see `group-picker.css` header comment).

### Comments style
`group-picker.css` lines 1-4 and `components.css` 596-600: multi-line `/* ... */` explaining why, no IDs.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `wwwroot/js/quest-*.js` ES modules (`type="module"`) | utility | event-driven | No module exists in the repo yet; follow `.claude/ui-guidelines.md` "No inline code" and the delegation pattern above |
| `wwwroot/js/avatar-fallback.js` | utility | event-driven | New; capture-phase `error` listener plus `img.complete && img.naturalWidth === 0` sweep (RESEARCH Pitfall 6) |
| `.webp` derivatives | static asset | n/a | Pillow one-off script run in scratch; commit outputs beside the PNGs; seals via `<picture>` |
| Poster text-box CSS modifiers (`quest-poster--p1..r1`) | stylesheet | n/a | New; values from UI-SPEC "Pool and measured text areas" table |
| Scratch capture tooling, Gate 1 mockups | scratch | n/a | Phase 94 tooling not committed; rebuild per RESEARCH "Scratch tooling" |
| `QuestPosterCatalog` measured table | utility | transform | Data comes from UI-SPEC; shape follows `MoveCopy` only loosely |

## Metadata

**Analog search scope:** `QuestBoard.Service/Views`, `QuestBoard.Service/Helpers`, `QuestBoard.Service/wwwroot/css`, `wwwroot/js`, `QuestBoard.IntegrationTests/{Tests,Mobile,Controllers}`, `QuestBoard.UnitTests/Helpers`
**Files scanned/read:** about 25
**Pattern extraction date:** 2026-11-10
