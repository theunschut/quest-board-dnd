# Phase 83: Availability Surface Naming and Placement - Pattern Map

**Mapped:** 2026-08-30
**Files analyzed:** 12 (11 modified + 1 new test class)
**Analogs found:** 12 / 12 (11 in-file / sibling-file analogs, 1 new-file analog)

**Note on method:** This phase touches almost no new files. For a modified file, the
correct analog is usually an adjacent block *in the same file* that already has the
shape the new markup needs — not a different file. Every entry below says explicitly
whether its analog is in-file, a sibling view (desktop/mobile twin), or a genuinely
external file. The UI-SPEC's own Modification Contract already quotes exact current
markup and the exact edit for 10 of the 12 sites, so those excerpts are reused directly
rather than re-derived — re-reading and re-quoting the same lines a second time would
add nothing.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `Views/Shared/_Layout.cshtml` (Mods 1, 3) | component (nav partial) | request-response | in-file: existing `Create Event` `<li>` (Mod 1 site) and existing `Shop`/`Quest Log` plain `nav-item` (Mod 3 site) | exact |
| `Views/Shared/_Layout.Mobile.cshtml` (Mods 2, 4) | component (nav partial) | request-response | in-file: existing `Create Event` flat `<li>` (Mod 2) and the desktop `_Layout.cshtml` twin edit (Mod 4) | exact |
| `Views/Events/Index.cshtml` (Mod 5) | component (page header) | request-response | in-file: current header markup itself, pattern borrowed from `Views/Agenda/Index.cshtml`'s `hasOtherBoardRow` hoisting idiom | exact |
| `Views/Events/Index.Mobile.cshtml` (Mod 6) | component (page header) | request-response | sibling: `Views/Events/Index.cshtml` (Mod 5, same edit, mobile shape) | exact |
| `Views/Agenda/Index.cshtml` (Mod 7) | component (page header) | request-response | in-file: existing `@{ }` block computing `hasOtherBoardRow`; cross-file: `Events/Index.cshtml`'s new `isDm` conditional button (mirror direction) | exact |
| `Views/Agenda/Index.Mobile.cshtml` (Mod 8) | component (page header) | request-response | sibling: `Views/Agenda/Index.cshtml` (Mod 7) | exact |
| `Views/Calendar/Index.cshtml` (Mod 9) | component (cross-link buttons) | request-response | in-file: adjacent unconditional `My Agenda` button in the same flex row | exact |
| `Views/Calendar/Index.Mobile.cshtml` (Mod 10) | component (cross-link buttons) | request-response | sibling: `Views/Calendar/Index.cshtml` (Mod 9); in-file adjacent `My Agenda` button in the same `d-grid` | exact |
| `Controllers/Events/EventsController.cs` (Mod 11) | controller | request-response | in-file: the existing comment block above `Index` (`:25-32`), extend in place | exact (comment-only) |
| `wwwroot/css/modern-card.css` (new `.header-subtitle` rule) | config/style | transform (CSS) | in-file: `.modern-card-header h1, h2, ...` block ending at line 40 | exact |
| `IntegrationTests/Controllers/LayoutNavigationTests.cs` (4→8 cases) | test | request-response | in-file: My Agenda `[Theory]` cases at `:384-452`, and the four Availability Overview cases they replace at `:319-382` | exact |
| `IntegrationTests/Controllers/CalendarButtonStyleTests.cs` (2 re-seeded + 1 added) | test | request-response | in-file: its own two existing `[Fact]` cases | exact |
| `IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` (+1 case) | test | request-response | in-file: existing class fixture/setup conventions | exact |
| **NEW** `IntegrationTests/Controllers/StaleAvailabilityOverviewLabelGuardTests.cs` (name is discretion — see below) (D-15) | test | request-response | `IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs` — a small cross-page assertion class that already fetches multiple representative surfaces with both user agents | role-match, no exact precedent |

## Pattern Assignments

### `QuestBoard.Service/Views/Shared/_Layout.cshtml` (Mods 1, 3)

**Analog:** in-file, immediately preceding/surrounding markup. UI-SPEC Modification 1
already quotes the exact current block (`:103-107`, the `Create Event` `<li>`) and the
exact inserted sibling — copy verbatim:

```cshtml
<li>
    <a class="dropdown-item" asp-controller="Events" asp-action="Create">
        <i class="fas fa-calendar-plus me-2"></i>Create Event
    </a>
</li>
@if (activeBoardType is BoardType.OneShot or BoardType.Campaign)
{
<li>
    <a class="dropdown-item" asp-controller="Events" asp-action="Index">
        <i class="fas fa-calendar-check me-2"></i>Board Availability
    </a>
</li>
}
```

Icon/spacing rule (from UI-SPEC): `me-2` matches every other item in this dropdown; no
color class on the icon (this menu's other items — `fa-scroll`, `fa-calendar-plus`,
`fa-coins`, `fa-user-edit` — are all plain; only the DM toggle itself carries color,
`text-danger`). The DM-policy gate is inherited from the enclosing `@if` already wrapping
this whole dropdown — only the board-type condition needs its own nested `@if`.

For Mod 3 (Calendar dropdown collapse), copy the plain `nav-item` shape from its
now-neighbours (`Shop`, `Quest Log`, `Characters`, `Contacts` — all `<li class="nav-item">`
+ `<a class="nav-link">` + `me-1` icon spacing):

```cshtml
<li class="nav-item">
    <a class="nav-link" asp-controller="Calendar" asp-action="Index">
        <i class="fas fa-calendar-alt me-1"></i>Calendar
    </a>
</li>
```

Note the icon spacing flips `me-2` → `me-1` here — that is what makes the collapsed item
match its new top-level-nav siblings exactly, not optional polish.

### `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` (Mods 2, 4)

**Analog:** the desktop `_Layout.cshtml` edit above, translated to this file's flat
(no-dropdown) idiom — this file has no dropdown anywhere (established pattern, Phase 77
D-20). Insert as a flat `<li class="nav-item">` sibling after `Create Event`
(`:84-88`), no divider:

```cshtml
<li class="nav-item">
    <a class="nav-link" asp-controller="Events" asp-action="Create">
        <i class="fas fa-calendar-plus me-2"></i>Create Event
    </a>
</li>
@if (activeBoardType is BoardType.OneShot or BoardType.Campaign)
{
<li class="nav-item">
    <a class="nav-link" asp-controller="Events" asp-action="Index">
        <i class="fas fa-calendar-check me-2"></i>Board Availability
    </a>
</li>
}
```

This file's DM section already uses `me-2` throughout (unlike its own top-level-nav
`me-1` convention further down) — keep `me-2` here, matching the local block, not the
desktop file's Mod-3 flip.

For Mod 4 (Calendar flat sibling removal, `:146-155`): this is a pure deletion, not a
restructure — the mobile layout never had a dropdown to collapse. Delete the
`Availability Overview` `<li>`; leave the `Calendar` `<li>` (`me-2`) untouched.

### `QuestBoard.Service/Views/Events/Index.cshtml` (Mod 5)

**Analog:** in-file (its own current header) for structure; `Views/Agenda/Index.cshtml`'s
existing `@{ }` block for the "hoist an awaited bool once at the top" idiom UI-SPEC
explicitly calls for.

**Core pattern — hoisted `isDm` + subtitle wrapper** (UI-SPEC Modification 5, copy
verbatim):

```cshtml
@using QuestBoard.Service.Constants
@{
    ViewData["Title"] = "Board Availability";
    var activeGroupName = Context.Session?.GetString(SessionKeys.ActiveGroupName);
    var subtitleBoardName = string.IsNullOrEmpty(activeGroupName) ? "this board" : activeGroupName;
    var isDm = (await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded;
}
...
<div class="card-header modern-card-header d-flex justify-content-between align-items-start">
    <div>
        <h2 class="mb-0">
            <i class="fas fa-calendar-check text-purple me-2"></i>@ViewData["Title"]
        </h2>
        <p class="header-subtitle mb-0">Events on @subtitleBoardName</p>
    </div>
    @if (isDm)
    {
        <a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm">
            <i class="fas fa-calendar-days me-2"></i>My Agenda
        </a>
    }
</div>
```

**Auth pattern:** `(await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded`
— `AuthorizationService` is already available via `_ViewImports.cshtml:14`, no new
`@inject` needed. `Context.Session` is directly available on every Razor view (inherited
from `RazorPage`) — no `@inject IHttpContextAccessor` needed here (that injection exists
in `_Layout.cshtml` for a different, layout-specific reason).

**Structural note (load-bearing):** `align-items-center` → `align-items-start`. With the
subtitle making the left flex child two lines tall, `center` would visually detach the
button from the heading baseline.

### `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` (Mod 6)

**Analog:** sibling of Mod 5, same file pair pattern as everywhere else in this app
(desktop/mobile twins). Copy Mod 5's shape with mobile's existing `h4`/`me-1`
conventions (UI-SPEC Modification 6, verbatim):

```cshtml
@using QuestBoard.Service.Constants
@{
    ViewData["Title"] = "Board Availability";
    var activeGroupName = Context.Session?.GetString(SessionKeys.ActiveGroupName);
    var subtitleBoardName = string.IsNullOrEmpty(activeGroupName) ? "this board" : activeGroupName;
    var isDm = (await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded;
}
...
<div class="d-flex justify-content-between align-items-start mb-2">
    <div>
        <h4 class="mb-0">
            <i class="fas fa-calendar-check text-purple me-2"></i>@ViewData["Title"]
        </h4>
        <p class="header-subtitle mb-0">Events on @subtitleBoardName</p>
    </div>
    @if (isDm)
    {
        <a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm">
            <i class="fas fa-calendar-days me-1"></i>My Agenda
        </a>
    }
</div>
```

Icon spacing on the button stays `me-1` — this file's existing convention for this
specific button; do not "correct" it to `me-2` to match desktop.

### `QuestBoard.Service/Views/Agenda/Index.cshtml` (Mod 7)

**Analog:** in-file. This file already hoists an awaited/computed value at the top of
its `@{ }` block:

```cshtml
@{
    ViewData["Title"] = "My Agenda";
    var hasOtherBoardRow = Model.Rows.Any(r => !r.IsActiveBoard);
}
```

Add `isDm` to this same block rather than opening a second one — exactly the reasoning
UI-SPEC states for why Agenda already hoists `hasOtherBoardRow`:

```cshtml
@{
    ViewData["Title"] = "My Agenda";
    var hasOtherBoardRow = Model.Rows.Any(r => !r.IsActiveBoard);
    var isDm = (await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded;
}
```

Header becomes (mirrors `Events/Index.cshtml`'s Mod 5 shape, direction reversed —
the return button here points at `Events`/`Index` instead of `Agenda`/`Index`):

```cshtml
<div class="card-header modern-card-header d-flex justify-content-between align-items-start">
    <div>
        <h2 class="mb-0">
            <i class="fas fa-calendar-days text-purple me-2"></i>@ViewData["Title"]
        </h2>
        <p class="header-subtitle mb-0">Upcoming events across all your boards</p>
    </div>
    @if (isDm)
    {
        <a asp-controller="Events" asp-action="Index" class="btn btn-secondary btn-sm">
            <i class="fas fa-calendar-check me-2"></i>Board Availability
        </a>
    }
</div>
```

**Do not touch** the `:9-15` comment block explaining why this page's row-click pattern
diverges from the overview's — out of scope per CONTEXT.md.

### `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml` (Mod 8)

**Analog:** sibling of Mod 7. This file's header currently has no flex wrapper at all
(`<h4 class="mb-3">` directly inside `container-fluid`), so the edit introduces the
wrapper rather than adjusting one. UI-SPEC Modification 8, verbatim:

```cshtml
<div class="container-fluid px-2 mt-2">
    <div class="d-flex justify-content-between align-items-start mb-3">
        <div>
            <h4 class="mb-0">
                <i class="fas fa-calendar-days text-purple me-2"></i>@ViewData["Title"]
            </h4>
            <p class="header-subtitle mb-0">Upcoming events across all your boards</p>
        </div>
        @if (isDm)
        {
            <a asp-controller="Events" asp-action="Index" class="btn btn-secondary btn-sm">
                <i class="fas fa-calendar-check me-1"></i>Board Availability
            </a>
        }
    </div>
```

Note: `mb-3` moves from the `<h4>` to the new wrapping `<div>` so the spacing below the
whole header block is preserved rather than duplicated or lost. Button icon spacing is
`me-1`, matching this file's other compact buttons (roster-toggle, filter buttons).

### `QuestBoard.Service/Views/Calendar/Index.cshtml` (Mod 9)

**Analog:** in-file — the adjacent `My Agenda` button in the same flex row is the shape
to wrap the first button in. UI-SPEC Modification 9, verbatim:

```cshtml
@if ((await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded)
{
    <a asp-controller="Events" asp-action="Index" class="btn btn-secondary btn-sm me-2">
        <i class="fas fa-calendar-check me-2"></i>Board Availability
    </a>
}
<a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm me-2">
    <i class="fas fa-calendar-days me-2"></i>My Agenda
</a>
```

No layout compensation needed: the header is `d-flex justify-content-between
align-items-center` and `justify-content-between` redistributes spacing across however
many children are present — do not add a placeholder or fixed-width wrapper.

### `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` (Mod 10)

**Analog:** sibling of Mod 9; in-file the adjacent `My Agenda` button in the same
`d-grid gap-2` stack. UI-SPEC Modification 10, verbatim:

```cshtml
<div class="d-grid gap-2 mb-3">
    @if ((await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded)
    {
        <a asp-controller="Events" asp-action="Index" class="btn btn-secondary btn-sm">
            <i class="fas fa-calendar-check me-2"></i>Board Availability
        </a>
    }
    <a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm">
        <i class="fas fa-calendar-days me-2"></i>My Agenda
    </a>
</div>
```

Simpler than Mod 9: `d-grid` full-width stacked buttons have no horizontal-alignment
relationship to preserve when one is removed.

### `QuestBoard.Service/Controllers/Events/EventsController.cs` (Mod 11 — comment only)

**Analog:** in-file, the existing comment block above `Index` (`:25-32`), currently
read from this session:

```csharp
    // Read-only and available to every board member: the same per-event availability is
    // already visible one event at a time on Details, so gating the aggregate would make
    // public information restricted only because it is shown together. The page size is
    // clamped server-side so a client-supplied value can never turn into an unbounded
    // query, and there is deliberately no active-group check here -- an authenticated
    // request with no active group is already redirected to the group picker upstream.
    // The configured ceiling is validated at application start; the floor below is a second,
    // defensive layer so this clamp still cannot throw even if a host somehow bypasses that.
```

Extend this comment in place to add the open-page/gated-links distinction, in plain
language, no phase number, no decision ID (per CLAUDE.md and UI-SPEC Modification 11).
No code change — `[Authorize]` on the class stays exactly as-is; do not add a policy to
`Index`.

### `QuestBoard.Service/wwwroot/css/modern-card.css` (new `.header-subtitle` rule)

**Analog:** in-file, the existing `.modern-card-header h1, h2, ...` rule ending at line
40 — insert the new rule directly after it. UI-SPEC's exact rule (Color section):

```css
.modern-card-header .header-subtitle {
    display: block;
    margin-top: 0.25rem;
    font-size: 0.8125rem;
    font-weight: 400;
    opacity: 0.75;
}
```

This is one shared, generic rule — not two page-specific duplicates. Do not use
`.text-muted` (untuned against this header's glass-surface + dark-shadow treatment; see
UI-SPEC's Color section for the full rationale referencing the Phase 77 mobile-contrast
fix).

## Shared Patterns

### DM-only conditional (applies to Mods 1, 2, 5, 6, 7, 8, 9, 10 — 8 sites)

**Source (existing idiom, already used verbatim in both layouts today):**
```csharp
(await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded
```

**Apply to:** every DM-gated `@if` in this phase. **Decision (UI-SPEC, explicit):**
inline this check at each of the 8 call sites — no shared partial, no tag helper. Where
a view already computes other `@{ }`-block variables at the top (Agenda's
`hasOtherBoardRow`, and now Events'/Agenda's own `isDm`), hoist the awaited bool into
that block rather than inlining it in markup — one `await` per render, matching the
existing `hasOtherBoardRow` precedent in `Views/Agenda/Index.cshtml`.

### Desktop/mobile twin editing

**Source:** every touched pair in this phase (`_Layout.cshtml`/`_Layout.Mobile.cshtml`,
`Events/Index.cshtml`/`Index.Mobile.cshtml`, `Agenda/Index.cshtml`/`Index.Mobile.cshtml`,
`Calendar/Index.cshtml`/`Index.Mobile.cshtml`).
**Apply to:** all 8 view files. Mobile views are selected by user agent, not breakpoint
(established pattern) — every nav/markup change in this phase needs its mobile twin in
the same commit; Phase 76 plan `76-14` previously fought a regression from exactly this
kind of desktop/mobile drift in the Calendar nav block.

### Test theory/user-agent shape

**Source:** `LayoutNavigationTests.cs:384-452` (My Agenda cases), the shape the 8 new
role-flip cases follow:
```csharp
[Theory]
[InlineData(DesktopUserAgent)]
[InlineData(MobileUserAgent)]
public async Task Nav_CampaignAuthenticated_MyAgendaLinkPresent(string userAgent)
{
    _factory.TestGroupContext.BoardType = BoardType.Campaign;
    var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
        _factory, "navagenda_campaign", "navagenda_campaign@test.com");

    var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    html.Should().Contain("My Agenda");
}
```
**Apply to:** all 8 new `LayoutNavigationTests` cases (D-13 groups 1-3) and the new
guard class. Note the request target in existing cases is `/quests` (the home page nav
renders on any authenticated route) — new cases can keep this pattern for presence/absence
assertions, but D-13 case 4 specifically requires `GET /Events` (see below).

**Client-pair source** (`AuthenticationHelper`, used across the whole nav suite):
```csharp
var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
    _factory, "navevent_campaign_dm", "navevent_campaign_dm@test.com");
// versus
var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
    _factory, "navagenda_player", "navagenda_player@test.com");
```
`CreateAuthenticatedDMClientAsync` for every "DM sees it" case; `CreateAuthenticatedClientWithUserAsync`
(default Player role, or explicit `roles: ["Player"]` as `CalendarButtonStyleTests` does)
for every "player does not" case.

**Board-type-driven case source** (`LayoutNavigationTests.cs` class-level pattern, seen
in the existing `Nav_CampaignDm_...` cases and the class's shared `DisposeAsync` reset):
```csharp
_factory.TestGroupContext.BoardType = BoardType.Campaign; // or .OneShot, or null
```
followed at the end of the test method by the class-wide `DisposeAsync` reset
(`BoardType = BoardType.OneShot; ActiveGroupId = 1;`) rather than a per-test
`try/finally` — this class resets in `DisposeAsync`, not inline. **No existing case in
this class drives an unresolved (`null`) board type for a DM-gated entry** — the closest
precedent is `Nav_UnresolvedBoardTypeAuthenticated_MyAgendaLinkPresentAndPageReachable`,
but that entry is unconditional by design (My Agenda has no board-type gate), so it
proves the opposite property. D-13 case 3 (DM, unresolved board type, entry absent) has
no direct precedent in this file and must be written from the board-type-setting idiom
above plus a `NotContain` assertion — flagged explicitly per the task instructions.

### `CalendarButtonStyleTests.cs` re-seed pattern

**Source:** its own two existing `[Fact]` cases (full file read above). Both currently
seed `roles: ["Player"]` and assert presence — D-08 breaks both, so both become DM-seeded
(`CreateAuthenticatedClientWithUserAsync(..., roles: ["DungeonMaster"])` or
`CreateAuthenticatedDMClientAsync`, matching whichever helper the rest of the suite
prefers for a DM-only client) and a third `[Fact]` is added asserting a Player does
**not** see the button, reusing the class's private `GetWithUserAgentAsync` helper and
its `btn-secondary`/`NotContain btn-outline-*` assertion shape unchanged. Update the
class doc comment from "styling" to "styling and cross-link visibility."

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| **NEW** D-15 guard test class | test | request-response | No existing test class fetches multiple *different* pages (Board Availability + My Agenda + Calendar) in one class to assert a single retired string is absent from all of them. The closest structural relative is `IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs`, which already fetches more than one representative surface (an events row and a quest row) across both user agents using the same `GetWithUserAgentAsync` + `AuthenticationHelper.CreateAuthenticatedDMClientAsync`-style setup — copy its class shape (constructor injection of `WebApplicationFactoryBase`, `IAsyncLifetime` with `BoardType`/`ActiveGroupId` reset in `DisposeAsync`, private `GetWithUserAgentAsync` helper) but write fresh `[Theory]`/`[InlineData(Desktop/MobileUserAgent)]` methods, one per surface (`/Events`, `/Agenda`, `/Calendar`), each asserting `html.Should().NotContain("Availability Overview")` as a DM. Per CONTEXT.md D-15's own reasoning, do not fold this into `EventsOverviewControllerIntegrationTests` (sees only one page) — a dedicated class is correct. Name and folder placement are explicitly left to planner/executor discretion per CONTEXT.md. |

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/Shared/`, `QuestBoard.Service/Views/Events/`,
`QuestBoard.Service/Views/Agenda/`, `QuestBoard.Service/Views/Calendar/`,
`QuestBoard.Service/Controllers/Events/`, `QuestBoard.Service/wwwroot/css/`,
`QuestBoard.IntegrationTests/Controllers/`
**Files scanned/read:** `83-CONTEXT.md`, `83-UI-SPEC.md`, `LayoutNavigationTests.cs`
(full test-name sections `:1-40`, `:300-460`), `CalendarButtonStyleTests.cs` (full),
`Agenda/Index.cshtml` (`:1-30`), `EventsController.cs` (`:1-55`),
`EventsOverviewControllerIntegrationTests.cs` (`:1-40`),
`RowNavigationAccessibilityTests.cs` (`:1-60`), directory listing of
`QuestBoard.IntegrationTests/Controllers/`.
**Pattern extraction date:** 2026-08-30
