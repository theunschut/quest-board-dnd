---
phase: 83
slug: availability-surface-naming-and-placement
status: draft
shadcn_initialized: false
preset: none
created: 2026-08-30
---

# Phase 83 — UI Design Contract

> Visual and interaction contract for a **modification-only** phase over two already-shipped
> pages and three already-shipped navigation surfaces. Nothing here is greenfield. Every
> markup block below is quoted from the current file and followed by the exact edit — the
> executor's job is to apply the diff, not to design from a blank page.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none — Bootstrap 5 + FontAwesome, no shadcn, no component library |
| Preset | not applicable |
| Component library | Bootstrap 5.3 (bundled), no Radix/Base UI |
| Icon library | FontAwesome (`fas fa-*`) |
| Font | project default (unchanged by this phase) |
| CSS files touched | `wwwroot/css/modern-card.css` (one new rule, shared by both affected pages) |

This phase introduces **zero new dependencies** and **zero new CSS frameworks**, per
`CLAUDE.md`. All new markup reuses existing Bootstrap utility classes
(`text-muted`... — see the explicit rejection of this class below — `d-flex`, `btn-sm`,
`me-1`/`me-2`) and the existing `modern-card` / `modern-card-header` / `modern-card-body`
convention.

---

## Scope Boundary (read before editing anything)

**In scope — 10 files, all already exist and already render:**

1. `QuestBoard.Service/Views/Shared/_Layout.cshtml` — DM dropdown (:90-127), Calendar dropdown (:168-189)
2. `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` — DM flat block (:76-105), Calendar flat siblings (:141-158)
3. `QuestBoard.Service/Views/Events/Index.cshtml` — title, card header, `My Agenda` button
4. `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` — same, mobile
5. `QuestBoard.Service/Views/Agenda/Index.cshtml` — card header
6. `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml` — card header, mobile
7. `QuestBoard.Service/Views/Calendar/Index.cshtml` — cross-link button pair (:14-21)
8. `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` — cross-link button pair (:32-39)
9. `QuestBoard.Service/wwwroot/css/modern-card.css` — one new shared rule for the subtitle
10. `QuestBoard.Service/Controllers/Events/EventsController.cs` — comment only, no markup, no behavior change

**Out of scope — do not touch, do not redesign:**

- The availability grid, its five-state cells, `_AvailabilityCell.cshtml`, `_AvailabilityCounts.cshtml` — unchanged pixel-for-pixel.
- The agenda's row layout, roster rendering, filter dropdown, paging, switch-board modal — unchanged.
- Any Details-page treatment (`Events/Details.cshtml`).
- `EventsController.Index`'s `[Authorize]`-only attribute set — do not add a policy here.
- The route `/Events`, the controller name, the view folder, or any C# type/method name (`EventOverviewViewModel`, `EventAvailabilityRow`, `GetAvailabilityOverviewAsync`, `EventsOverviewOptions`, etc.). Rename touches **rendered strings only**.

---

## Spacing Scale

No new spacing tokens. Every edit in this phase reuses spacing already in the codebase:

| Token | Value | Usage in this phase |
|-------|-------|----------------------|
| xs | 4px (`me-1`, `mt-1`) | Icon-to-label gap on compact mobile buttons; subtitle-to-heading gap |
| sm | 8px (`me-2`, `gap-2`) | Icon-to-label gap on desktop buttons; button gaps |
| — | `btn-sm` padding (Bootstrap default, unchanged) | All buttons touched in this phase are already `btn-sm`; do not change size |

Exceptions: none.

---

## Typography

No new type sizes and no new weights. This phase adds exactly one new typographic role — the
subtitle — built entirely from existing Bootstrap primitives, not a new declared size.

| Role | Size | Weight | Line Height | Source |
|------|------|--------|-------------|--------|
| Page heading (`h2`, desktop) | Bootstrap `h2` default (~2rem) | inherited `modern-card-header` bold (600) | Bootstrap default | unchanged |
| Page heading (`h4`, mobile) | Bootstrap `h4` default (~1.5rem) | inherited `modern-card-header` bold (600) | Bootstrap default | unchanged |
| **New: header subtitle** | Bootstrap `small` (0.875em ≈ 14px against the h2, ≈10.5px against nothing — always sized relative to its parent) | 400 (normal — explicitly lighter than the 600 the header imposes by default) | Bootstrap default (~1.5) | new `.header-subtitle` rule, see Color section |
| Nav item / dropdown item | Bootstrap default (`nav-link`, `dropdown-item`) | unchanged | unchanged | unchanged |

The subtitle is **always the second line under the h2/h4, never beside it**, and never larger
than `small`. It must never compete with the heading for primary read weight.

---

## Color

No changes to the app's 60/30/10 palette. This phase adds **one new color decision**: how the
subtitle reads against `.modern-card-header`'s existing background treatment.

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | unchanged (page background) | unchanged |
| Secondary (30%) | unchanged (`modern-card` / `modern-card-header` glass surfaces) | unchanged |
| Accent (10%) | `text-purple` (existing custom class) | Reserved for: the `fa-calendar-days` icon on "My Agenda" (existing, unchanged), the `fa-calendar-check` icon on the Board Availability page heading (existing, unchanged). **Not** applied to the new subtitle text, and **not** applied to the moved DM-menu icon (see Modification 3 below — it stays plain, matching its new siblings). |
| Destructive | not applicable — no destructive action in this phase | — |
| **New: subtitle text** | inherits `.modern-card-header`'s own `color: #1a1a1a` and light text-shadow, dimmed via `opacity: 0.75` | The header subtitle only |

**Why not Bootstrap's `.text-muted`:** `.modern-card-header` in `modern-card.css:22-30` sets its
own `color: #1a1a1a !important` plus a light drop-shadow, specifically because this header sits
on a semi-transparent glass surface, not a plain white background — headings then override to a
warm gold (`#F4E4BC`) with a dark shadow for contrast against the page's dark hero background.
Bootstrap's `.text-muted` (`color: var(--bs-secondary-color) !important`, a mid-grey) was never
tuned against this glass surface and this app has already had to fix a real contrast defect on
a sibling page (Phase 77 plan `77-11`, mobile availability cards). Rather than gamble on an
untested grey, the subtitle **dims the header's own already-legible color** via `opacity`,
guaranteeing at least the same contrast ratio as any other non-heading text already rendered in
that header, just visually lighter.

**New CSS rule — add to `QuestBoard.Service/wwwroot/css/modern-card.css`, directly after the
existing `.modern-card-header h1, h2, ...` block (after line 40):**

```css
.modern-card-header .header-subtitle {
    display: block;
    margin-top: 0.25rem;
    font-size: 0.8125rem;
    font-weight: 400;
    opacity: 0.75;
}
```

This is a **shared, generic rule** — not scoped to Events or Agenda specifically — because both
pages need the identical treatment and a future page with the same "heading + secondary line"
shape should be able to reuse it without duplicating the rule. Do not create two near-identical
page-specific rules; that is the drift pattern `CLAUDE.md`/`PROJECT.md` already flags elsewhere
in this codebase.

Applied as `<p class="header-subtitle mb-0">…</p>` (Bootstrap's `mb-0` kills the default `<p>`
bottom margin; `margin-top` comes from the new rule, not a utility class, so it is not
accidentally droppable by an editor who only sees `mb-0` and assumes that's the whole spacing
story).

---

## Copywriting Contract

| Element | Copy | Site(s) |
|---------|------|---------|
| Page name (title, nav label, card heading — one string, D-02) | **Board Availability** | `Events/Index.cshtml` `ViewData["Title"]`; `Events/Index.Mobile.cshtml` `ViewData["Title"]`; desktop DM-dropdown item; mobile DM flat item; Calendar cross-link (both layouts) |
| Board Availability subtitle (D-03) | **"Events on {ActiveGroupName}"** — e.g. "Events on Last Bastion" | `Events/Index.cshtml`, `Events/Index.Mobile.cshtml`, directly under the `<h2>`/`<h4>` |
| Board Availability subtitle — empty-name fallback (D-03) | **"Events on this board"** (used only when `SessionKeys.ActiveGroupName` is null or empty; matches the layout's own established fallback pattern of substituting a generic phrase rather than rendering "Events on ") | same two files |
| My Agenda subtitle (D-04) | **"Upcoming events across all your boards"** — static, no interpolation, no conditional | `Agenda/Index.cshtml`, `Agenda/Index.Mobile.cshtml`, directly under the `<h2>`/`<h4>` |
| Moved DM-menu entry label (D-07) | **"Board Availability"** (identical string to the page name — D-02's "one string, used identically" extends here too) | desktop DM dropdown, mobile DM flat block |
| Calendar cross-link label (D-08, renamed + gated) | **"Board Availability"** | `Calendar/Index.cshtml`, `Calendar/Index.Mobile.cshtml` |
| New return button on My Agenda (D-10) | **"Board Availability"** (mirrors the existing button text convention: buttons carry the destination's page name, not a verb) | `Agenda/Index.cshtml`, `Agenda/Index.Mobile.cshtml` card header |
| Existing "My Agenda" button on Board Availability (D-11, now DM-gated, text unchanged) | **"My Agenda"** | `Events/Index.cshtml`, `Events/Index.Mobile.cshtml` |
| Empty states | unchanged — this phase does not touch empty-state copy on either page | — |
| Destructive confirmation | not applicable — no destructive action in this phase | — |

**Verbatim string that must disappear everywhere (D-15's guard target):** `"Availability
Overview"`. It must not survive as a title, a nav label, a button label, or a cross-link label
on any of the 8 touched view files, on either desktop or mobile.

---

## Modification Contract

Each block quotes the current markup, then states the exact change. Line numbers are current as
of this research pass and will drift as edits land — treat them as a locator, not a promise.

### 1. Desktop DM dropdown — insert the moved entry (D-06, D-07)

`QuestBoard.Service/Views/Shared/_Layout.cshtml:103-107` currently:

```cshtml
<li>
    <a class="dropdown-item" asp-controller="Events" asp-action="Create">
        <i class="fas fa-calendar-plus me-2"></i>Create Event
    </a>
</li>
```

Insert immediately after, as a new `<li>` sibling, gated on **both** the board-type condition
and the fact that this whole block already sits inside the `DungeonMasterOnly` check (D-06 —
DM-policy is inherited from the enclosing `@if`, only the board-type half needs its own nested
condition):

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

**Icon decision:** plain `fas fa-calendar-check`, **no color class.** Its new siblings in this
menu (`fa-scroll`, `fa-calendar-plus`, `fa-coins`, `fa-user-edit`) are all uncolored; the DM
toggle itself already carries the only color in this menu (`text-danger`). Carrying
`text-purple` in here — even though the page heading uses it — would make this one entry look
like a second accent inside a menu that currently has exactly one, which reads as "special"
rather than "moved." Consistency with the immediate menu context wins over consistency with the
page's own heading.

**Spacing/icon convention check:** `me-2`, matching every other item in this dropdown (not
`me-1`, which is the top-level-nav-link convention used one section down).

### 2. Mobile DM flat block — mirror insertion (D-06, D-07)

`QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:84-88` currently:

```cshtml
<li class="nav-item">
    <a class="nav-link" asp-controller="Events" asp-action="Create">
        <i class="fas fa-calendar-plus me-2"></i>Create Event
    </a>
</li>
```

Insert immediately after, as a flat `nav-item` sibling — **no dropdown, no divider**, this
layout has neither idiom:

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

Same icon decision as Modification 1: plain `fa-calendar-check`, no color class, `me-2` (this
mobile block already uses `me-2` throughout its DM section, unlike the top-level `me-1`
convention further down the same file).

### 3. Desktop Calendar dropdown — collapse to plain nav-item (D-05)

`QuestBoard.Service/Views/Shared/_Layout.cshtml:172-189` currently:

```cshtml
<li class="nav-item dropdown">
    <a class="nav-link dropdown-toggle" href="#" id="calendarDropdown" role="button" data-bs-toggle="dropdown">
        <i class="fas fa-calendar-alt me-1"></i>Calendar
    </a>
    <ul class="dropdown-menu" aria-labelledby="calendarDropdown">
        <li>
            <a class="dropdown-item" asp-controller="Calendar" asp-action="Index">
                <i class="fas fa-calendar-alt me-2"></i>Calendar
            </a>
        </li>
        <li>
            <a class="dropdown-item" asp-controller="Events" asp-action="Index">
                <i class="fas fa-calendar-check me-2"></i>Availability Overview
            </a>
        </li>
    </ul>
</li>
```

Replace with a plain `nav-item`, styled identically to its now-neighbours (`Shop`, `Quest Log`,
`Characters`, `Contacts`):

```cshtml
<li class="nav-item">
    <a class="nav-link" asp-controller="Calendar" asp-action="Index">
        <i class="fas fa-calendar-alt me-1"></i>Calendar
    </a>
</li>
```

Note the icon spacing flips from `me-2` (dropdown-item convention) to `me-1` (top-level
`nav-link` convention) — this is not optional polish, it is what makes the collapsed item match
its new siblings exactly, which is the explicit ask in the roadmap risk about this block.

The `Board Availability` (formerly "Availability Overview") item does not move here — it is
deleted from this menu entirely; its only remaining desktop nav home is the DM dropdown
(Modification 1).

### 4. Mobile Calendar flat siblings — remove the moved item (D-05, mobile has no dropdown to collapse)

`QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:146-155` currently:

```cshtml
<li class="nav-item">
    <a class="nav-link" asp-controller="Calendar" asp-action="Index">
        <i class="fas fa-calendar-alt me-2"></i>Calendar
    </a>
</li>
<li class="nav-item">
    <a class="nav-link" asp-controller="Events" asp-action="Index">
        <i class="fas fa-calendar-check me-2"></i>Availability Overview
    </a>
</li>
```

Becomes:

```cshtml
<li class="nav-item">
    <a class="nav-link" asp-controller="Calendar" asp-action="Index">
        <i class="fas fa-calendar-alt me-2"></i>Calendar
    </a>
</li>
```

Nothing else in this block changes — this layout never had a dropdown to collapse, so this is a
deletion, not a restructure. `Calendar`'s own icon spacing (`me-2`) is already correct here and
stays as-is (this file's top-level items use `me-2` throughout, unlike desktop's `me-1`).

### 5. `Events/Index.cshtml` header — rename, subtitle, DM-gate the button (D-02, D-03, D-11)

Current (`:1-18`):

```cshtml
@{
    ViewData["Title"] = "Availability Overview";
}
...
<div class="card-header modern-card-header d-flex justify-content-between align-items-center">
    <h2 class="mb-0">
        <i class="fas fa-calendar-check text-purple me-2"></i>@ViewData["Title"]
    </h2>
    <a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm">
        <i class="fas fa-calendar-days me-2"></i>My Agenda
    </a>
</div>
```

New:

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

**Structural notes, both load-bearing:**

- `align-items-center` → `align-items-start`. With the subtitle added, the left flex child is
  now two lines tall; `center` would vertically centre the button against that two-line block,
  visually detaching it from the heading baseline. `start` pins the button to the top, level
  with the `<h2>`, which is the correct reading for "this button belongs to the page, not to a
  vertically-centred midpoint."
- The `<h2>` and the new `<p>` are wrapped in a bare `<div>` (no class needed) so the flex
  container still has exactly two children when the button is present: the title block and the
  button. When `isDm` is false, the title block is the only child, `justify-content-between`
  degrades gracefully to left-aligned — no compensation needed, this is standard flexbox
  behavior with a single item.
- `AuthorizationService` is already available in every view via `_ViewImports.cshtml:14` — no
  new `@inject` needed.
- `Context.Session` is available directly on every Razor view (inherited from `RazorPage`) — no
  new `@inject IHttpContextAccessor` needed here, unlike `_Layout.cshtml` which injects it for a
  different reason (it needs it inside a `@functions`-free plain code block at the top before
  any view-specific base class guarantees are relied on; the view itself does not need to match
  that pattern).

### 6. `Events/Index.Mobile.cshtml` header — same changes, mobile shape (D-02, D-03, D-11)

Current (`:1-19`):

```cshtml
@{
    ViewData["Title"] = "Availability Overview";
}
...
<div class="d-flex justify-content-between align-items-center mb-2">
    <h4 class="mb-0">
        <i class="fas fa-calendar-check text-purple me-2"></i>@ViewData["Title"]
    </h4>
    <a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm">
        <i class="fas fa-calendar-days me-1"></i>My Agenda
    </a>
</div>
```

New:

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

Same `align-items-center` → `align-items-start` reasoning as Modification 5. Icon spacing on
the button stays `me-1` (this file's existing convention for this specific button — do not
"correct" it to `me-2` to match desktop; the two layouts already differ here and that is not
this phase's concern to fix).

### 7. `Agenda/Index.cshtml` header — subtitle, new DM-only return button (D-04, D-10)

Current (`:17-24`):

```cshtml
<div class="card-header modern-card-header">
    <h2 class="mb-0">
        <i class="fas fa-calendar-days text-purple me-2"></i>@ViewData["Title"]
    </h2>
</div>
```

New — add the `isDm` computation to the existing `@{ }` block at the top of the file (it
already computes `hasOtherBoardRow` there, so this joins it rather than opening a second block):

```cshtml
@{
    ViewData["Title"] = "My Agenda";
    var hasOtherBoardRow = Model.Rows.Any(r => !r.IsActiveBoard);
    var isDm = (await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded;
}
```

Header becomes:

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

This page's header had no flex layout and no button before — this is new structure, not a
restructure of something that broke. `ViewData["Title"]` itself does **not** change (My Agenda
keeps its name, D-04); only the header markup around it changes.

**Do not touch** the `:9-15` comment block explaining why this page's row action diverges from
the overview's whole-row click pattern — that is unrelated to the header and the CONTEXT
explicitly says not to "restore consistency" there.

### 8. `Agenda/Index.Mobile.cshtml` header — same changes, mobile shape (D-04, D-10)

Current (`:18-21`):

```cshtml
<div class="container-fluid px-2 mt-2">
    <h4 class="mb-3">
        <i class="fas fa-calendar-days text-purple me-2"></i>@ViewData["Title"]
    </h4>
```

New — add `isDm` to the existing top-of-file `@{ }` block, same as Modification 7. Header
becomes:

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

Note the `<h4 class="mb-3">` becomes `<h4 class="mb-0">` — the `mb-3` bottom margin moves to the
new wrapping `<div class="... mb-3">` so the spacing below the whole header block is preserved
exactly, rather than being duplicated or lost. Icon spacing on the new button is `me-1`,
matching this file's other compact buttons (the roster-toggle and filter buttons in this same
file all use `me-1`).

### 9. `Calendar/Index.cshtml` — DM-gate and rename the first cross-link (D-08)

Current (`:16-21`):

```cshtml
<a asp-controller="Events" asp-action="Index" class="btn btn-secondary btn-sm me-2">
    <i class="fas fa-calendar-check me-2"></i>Availability Overview
</a>
<a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm me-2">
    <i class="fas fa-calendar-days me-2"></i>My Agenda
</a>
```

New:

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

**Layout compensation: none needed, and specifically why.** This header is
`d-flex justify-content-between align-items-center` with the `<h2>`, the (now conditional)
Board Availability button, the My Agenda button, and the `.calendar-navigation` month-picker
block as four flex children. `justify-content-between` recalculates spacing across however many
children are actually present at render time — removing one button for a player leaves three
children (`h2`, My Agenda, month-picker) and the browser redistributes the gaps automatically.
The `me-2` on the removed button disappears with it, so there is no orphaned margin to clean up.
**Do not** wrap the buttons in a fixed-width container or add a placeholder — that would be
solving a problem flexbox already does not have.

### 10. `Calendar/Index.Mobile.cshtml` — DM-gate and rename (D-08)

Current (`:33-40`):

```cshtml
<div class="d-grid gap-2 mb-3">
    <a asp-controller="Events" asp-action="Index" class="btn btn-secondary btn-sm">
        <i class="fas fa-calendar-check me-2"></i>Availability Overview
    </a>
    <a asp-controller="Agenda" asp-action="Index" class="btn btn-secondary btn-sm">
        <i class="fas fa-calendar-days me-2"></i>My Agenda
    </a>
</div>
```

New:

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

**No compensation needed here either, for a simpler reason than Modification 9:** this
container is `d-grid` with full-width stacked buttons, not a side-by-side row. Removing one
button just leaves one full-width button in the grid; there was never a horizontal-alignment
relationship between the two to preserve.

### 11. `EventsController.cs` — comment update only, no markup, no behavior (D-09)

`QuestBoard.Service/Controllers/Events/EventsController.cs:25-32`'s comment above `Index`
currently explains why the action is unrestricted. Extend it — in plain language, with **no
phase number, no decision ID** — to note that the page stays open while its discoverable links
are now DM-only, so a future reader does not "finish the job" by adding an authorization policy
here. This is prose guidance for the planner/executor, not a markup spec; exact wording is
implementation discretion as long as it states the open-page / gated-links distinction and does
not reference this phase by number.

---

## DM-Only Conditional Pattern (applies to Modifications 1, 2, 5, 6, 7, 8, 9, 10)

**Decision: inline the check at each of the eight call sites, no shared partial or tag
helper.** The idiom is already one line
(`(await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded`), it is
already used verbatim in both layouts today, and `AuthorizationService` is globally available
in every view via `_ViewImports.cshtml:14`. Eight call sites of a one-line check do not justify
a new partial view or view component — that would add an indirection layer to trace through for
a check that is already this cheap to read in place. Where a view already computes other
`@{ }`-block variables at the top (Agenda's `hasOtherBoardRow`), add `isDm` there rather than
inline in the markup, for the same reason Agenda already hoists `hasOtherBoardRow` — one
`await` per render, not one per usage if a future edit needs the value twice.

---

## Accessibility Note (no color-only signal introduced)

This phase introduces no new color-only meaning. The subtitle's dimming is opacity-only
decoration, not a status signal — it carries no information a screen reader needs distinguished
from the heading beyond normal DOM order (`<h2>` then `<p>`, read in sequence). The five-state
availability cells' non-color companion signals are untouched (out of scope, see Scope
Boundary). DM-only buttons and nav entries are removed from the DOM entirely when the user is
not a DM (`@if`, not `display:none` / `visually-hidden`), so they are never focusable and never
announced to a player — this is the correct treatment for a genuinely absent action, not a
merely-hidden one.

---

## Registry Safety

Not applicable. No shadcn, no component registry, no third-party blocks. `Tool: none`.

| Registry | Blocks Used | Safety Gate |
|----------|-------------|--------------|
| — | — | not applicable — no registry in this project |

---

## Test Surface Implications (for the planner, not a UI concern but load-bearing on the same files)

The UI changes above directly determine what `LayoutNavigationTests`, `CalendarButtonStyleTests`,
and the new D-15 guard class must assert against. Summarized from `83-CONTEXT.md` D-12–D-15 so
the planner does not have to cross-reference two documents while writing test tasks:

- Every string assertion currently checking for `"Availability Overview"` must be updated to
  `"Board Availability"`, and a new guard class must assert `"Availability Overview"` appears in
  **none** of: Board Availability (both UAs), My Agenda (both UAs), Calendar (both UAs) — as a
  DM, the one role that sees every affected surface.
- The nav test set must prove the *flip*, not just presence: a DM sees the moved entry inside
  the DM dropdown/block; a player does not see it anywhere; a DM with an unresolved board type
  does not see it either (proves D-06's board-type gate survived being nested one level deeper
  than the old Calendar-dropdown location).
- `CalendarButtonStyleTests`'s two existing cases authenticate as a Player and assert the
  Board Availability link is present — both break under D-08 and must be re-seeded as a DM,
  plus a new player-absent case added in the same file.
- A player `GET /Events` must still return 200 — this is the test that would catch a future
  "finish the job" regression where someone adds `[Authorize(Policy = "DungeonMasterOnly")]` to
  `EventsController.Index`, which D-09 explicitly forbids.

---

## Checker Sign-Off

- [ ] Dimension 1 Copywriting: PASS — verify the exact strings in the Copywriting Contract table land verbatim, and `"Availability Overview"` is fully retired from rendered output
- [ ] Dimension 2 Visuals: PASS — verify `align-items-start` (not `center`) on both Events header variants once the subtitle is present, and that DM-only elements are absent from the DOM (not merely hidden) for a player
- [ ] Dimension 3 Color: PASS — verify the subtitle does not use `.text-muted`, uses the new `.header-subtitle` rule, and that no accent color (`text-purple`) leaked onto the moved DM-menu icon
- [ ] Dimension 4 Typography: PASS — verify the subtitle renders at `small`-equivalent size, weight 400, never larger than or competing with the heading
- [ ] Dimension 5 Spacing: PASS — verify `me-1` vs `me-2` icon spacing matches each file's existing local convention (not a blanket global choice) at every site in the Modification Contract
- [ ] Dimension 6 Registry Safety: PASS — not applicable, no registry in this project

**Approval:** pending
