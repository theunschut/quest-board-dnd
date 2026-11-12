# UI/UX Design Guidelines

Read this before creating or editing any Razor view.

All new views must use the modern card pattern with these CSS classes: `modern-card`,
`modern-card-header`, `modern-card-body`, and `modern-card-footer` when the card has an action row.

```html
<div class="card modern-card">
    <div class="card-header modern-card-header">
        <h2 class="mb-0">
            <i class="fas fa-scroll me-2" aria-hidden="true"></i>
            Page Title
        </h2>
        <span class="header-subtitle">One line that says what this page is for</span>
    </div>
    <div class="card-body modern-card-body">
        ...
    </div>
</div>
```

- The card header holds the page title: one `h1` or `h2`, shown in the display face (Cinzel, 28px on
  desktop, 20px on the phone layout). Nothing else on a screen uses the display step.
- `.header-subtitle` is the optional line under the title, in muted text at 14px.
- `modern-card-footer` is a bordered strip at the bottom of a card for its action row.
- A static card never moves. Add `modern-card--interactive` only to a card that is itself a link or a
  button: it lifts on hover (not for readers who prefer reduced motion).
- The card sets its own text colour and everything inside inherits it. Do not set a text colour on
  paragraphs, spans or headings inside a card; only name a colour when the element genuinely differs
  (a status, a link, a control).
- Always include `<hr>` before the button section
- Use filled colored buttons (not outline), FontAwesome icons with `me-2` spacing
- Button layout: `d-flex justify-content-between` — secondary (cancel) left, primary (submit) right

## Tokens and layers

Every colour, size, space, shadow and duration is a design token: a `--qb-*` custom property declared in
`wwwroot/css/tokens.css`, and nowhere else. A stylesheet other than `tokens.css` never contains a colour
literal.

- **Use a token, never a literal.** Write `color: var(--qb-fg)`, not `color: #f4e4bc`. Tokens name a role
  (`--qb-fg`, `--qb-surface-glass`, `--qb-action`, `--qb-danger-text`), so changing a look is a one-value
  edit.
- **Derive a tint with `color-mix()`** from a token instead of adding a new colour.
- **No `!important`** in any layered stylesheet. The only allowed uses are the reduced-motion block in
  `base.css` and the visually-hidden helper in `components.css`.
- **Layer order** is declared once, in `app.css`, which stays the first stylesheet on every layout:
  `vendor, tokens, base, components, pages, utilities`. A later layer beats an earlier one regardless of
  selector specificity.
  - `vendor`: Bootstrap and Font Awesome, imported by `app.css`.
  - `tokens`: `tokens.css`.
  - `base`: `base.css`; the document, headings, the focus ring, reduced motion and the board backdrop.
  - `components`: `components.css` (cards, buttons, forms, alerts, tables, badges, modal, toast, empty
    state), `shell.css` (navigation bar, footer, skip link) and `shell.mobile.css` (the phone shell).
  - `pages`: a stylesheet that belongs to one page or area, such as `group-picker.css`. Wrap its rules in
    `@layer pages { ... }`.
  - `utilities`: small helpers that must win over components (`visually-hidden-focusable`, `text-halo`,
    `text-purple`).
- **Re-theme Bootstrap through its own variables** (`--bs-*`), never by out-specifying it. Bootstrap sits
  in the lowest layer, so a variable set in `base.css` or `components.css` wins without a fight.
- **Legacy page sheets are unlayered.** `legacy-pages.css` (the desktop remainder), `quests.css`,
  `shop.css`, `calendar.css` and the other per-area sheets are not in a layer yet, so on their own
  selectors they beat every layered rule. A page converts to the layered system by moving its rules into
  `@layer pages` and its look onto tokens; until then, do not fight an unlayered rule from a layered one.
- Text on the bare board takes its colour from `body`; a surface that is not the board sets its own colour
  and what sits on it inherits.

## Components

Use these shared classes; do not restyle them per page.

**Page header.** The card header from the pattern above: one title, an optional `.header-subtitle`.

**Type.** Four sizes (14, 16, 20, 28px) and two weights (400, 600). Headings are mapped onto them in
`base.css`: `h1` is the display step (Cinzel; 28px on desktop, 20px on the phone layout), `h2` and `h3`
are 20px, `h4` to `h6` are 16px at 600. Do not use `fw-bold` or `fs-*` to invent another size. Cinzel is
for page titles and the brand only, never for body text, labels, dates, numbers or buttons.

**Buttons by role.** Filled, never outline; FontAwesome icon with `me-2`.

| Class | Use |
|-------|-----|
| `btn-primary` | the one commit action of a view (wood fill, white label) |
| `btn-secondary` | cancel, back, neutral |
| `btn-success` | positive confirmation: approve, accept, join |
| `btn-warning` | edit or caution; the label is dark, never white |
| `btn-info` | neutral informational action |
| `btn-danger` | destructive |

```html
<hr>
<div class="d-flex justify-content-between">
    <a class="btn btn-secondary" asp-action="Index">
        <i class="fas fa-arrow-left me-2" aria-hidden="true"></i>Cancel
    </a>
    <button type="submit" class="btn btn-primary">
        <i class="fas fa-save me-2" aria-hidden="true"></i>Save quest
    </button>
</div>
```

**Form group.** A label, a control, optional help text, and the validation message.

```html
<div class="mb-3">
    <label asp-for="Title" class="form-label"></label>
    <input asp-for="Title" class="form-control" aria-describedby="title-help" />
    <div id="title-help" class="form-text">Shown on the quest board.</div>
    <span asp-validation-for="Title" class="field-validation-error"></span>
</div>
```

A field in error gets a 2px danger border and its message is the light danger text; keep the icon and the
sentence, never colour alone.

**Refusal line.** The sentence that says why something was refused: a `role="alert"` block with an icon
and the reason.

```html
<div role="alert" class="refusal-line">
    <i class="fas fa-circle-exclamation" aria-hidden="true"></i>
    <span>Only the Dungeon Master of this quest can close signups.</span>
</div>
```

**Empty state.** Use `.empty-state` when a list has nothing to show. The heading names what is missing
("No quests on this board yet"), one sentence gives the next step, and the optional action is a verb plus
a noun ("Create quest"). Never write "No data found" or "Nothing here".

```html
<div class="empty-state">
    <i class="fas fa-scroll empty-state-icon" aria-hidden="true"></i>
    <h2 class="empty-state-heading">No quests on this board yet</h2>
    <p class="empty-state-text">Post the first quest so players can sign up.</p>
    <a class="btn btn-primary" asp-action="Create">
        <i class="fas fa-plus me-2" aria-hidden="true"></i>Create quest
    </a>
</div>
```

An older per-page empty-state class stays on its page until that page is converted; do not remove it
on the side.

**Alerts.** `.alert` with `alert-success`, `alert-danger`, `alert-warning`, `alert-info`. An alert is a
dark panel with light status text and a status stripe. Use it for a message that belongs to the page;
a transient result of an action is a toast.

**Toasts.** Do not write toast markup. Set a `TempData` key in the controller and the shared toast partial
(rendered by every layout) shows it: `TempData["Success"]`, `["Error"]`, `["Warning"]`, `["Info"]`, and
`["GoldReceived"]` for a shop payout. Success and info announce politely (`role="status"`) and hide after
5 seconds. Errors and warnings persist until dismissed (`role="alert"`). Every toast has its own named
Close button, so keep each message to one sentence.

**Tables, badges, modals.** `.table` is parchment cells with dark ink and a wood header. `.badge` is 14px
at 600 with a label that holds 4.5:1 on its fill. `.modal` and `.offcanvas` sit on the opaque overlay
surface.

## Colour

Text colour comes from the surface it sits on: a card, the bare board and the navigation bar set a light
parchment colour once and descendants inherit it. A status colour used as text is mapped for that
surface: on the board and on glass it is the light variant that holds 4.5:1; on a paper surface (tables,
rows) it renders as dark ink, because the fills do not hold 4.5:1 there; inside an `.alert` it is the
light variant on the dark panel. Headings on a card that is not yet converted take `--qb-fg-heading`, which
is lighter than body text because those cards sit on plain glass without the scrim.

| Colour | Use it for | Never for |
|--------|------------|-----------|
| Parchment (`--qb-fg`, `--qb-surface-paper`) | body and label text on glass, chrome and the board; a paper surface with `--qb-ink` text | text on a surface where it does not reach 4.5:1; a 70%-alpha "muted" (use `--qb-fg-muted`) |
| Gold (`--qb-accent` family) | the focus ring's inner stroke, borders and ornaments, large headings at the 28px display step on desktop, checked and selected fills, decorative icons beside text | body-size text on glass (use `--qb-accent-text`), any text or border on a paper surface |
| Wood (`--qb-wood`, `--qb-action`) | chrome fills, the commit-action button fill, table header fills, text on paper | brown text on glass |
| Dark ink (`--qb-ink`, `--qb-accent-ink`) | text on paper and on gold fills | any text on glass or on the bare board |
| Event purple (`--qb-event`) | event and series identity fills, the `text-purple` helper | a generic button, a focus colour |
| Shop theme | only inside the Shop theme block | anywhere else |

**Gold is reserved.** It is allowed for exactly these: the focus ring's inner stroke; the checked or
selected state of switches, checkboxes, radios, tabs, filter and category pills and the active nav item;
navbar link and brand hover; currency (gold-piece amounts, prices, coin icons); an optional page-title
ornament; and large display headings on desktop. It is not for generic links, any button fill (the commit
action is wood, not gold), body text, ordinary card borders or status badges.

**Never put dark text on glass or on the bare board.** Never rely on colour alone: a status carries an icon
and words, a selected state carries a check or a filled marker, an error carries an icon and a sentence.

## Accessibility checklist

Check each item before merging a view.

- [ ] Text contrast is at least 4.5:1; large text (the 28px display step) and non-text (control borders,
      focus indicators, meaningful icons) at least 3:1. Translucent surfaces are measured over the lightest
      board pixel.
- [ ] Every interactive element shows the two-tone focus ring (gold inner stroke, dark outer stroke), and no
      ancestor with `overflow: hidden` clips it.
- [ ] A focused element is never hidden under the fixed navbar (the page scroll padding keeps it clear).
- [ ] Targets are at least 44 by 44px on the phone layout and at least 24 by 24px on desktop.
- [ ] No horizontal scroll at 320px wide on desktop and 390px wide on the phone layout.
- [ ] Motion respects `prefers-reduced-motion`, and nothing new animates forever.
- [ ] Landmarks and names: skip link first, `header`, `nav` with an `aria-label`, `main#main-content`,
      `footer`; every icon-only control has an `aria-label`.
- [ ] Every `<img>` that carries content has `alt`; decorative ones have `alt=""`.
- [ ] Success and info toasts use `role="status"`; error and warning toasts use `role="alert"` and persist.
- [ ] Status is never colour alone: icon and words.
- [ ] Forms keep `autocomplete` and allow paste; a dragging gesture has a non-dragging alternative.

## Mobile

The layout is chosen by User-Agent, not by viewport width: a phone gets `_Layout.Mobile` (and the Platform
area gets `_Layout.Platform.Mobile`), everything else gets the desktop layout, including a narrow desktop
window. Narrowing a desktop window never turns it into the phone layout.

- The phone shell is a static 56px bar with an offcanvas menu; the desktop main bar collapses to a
  hamburger below 1200px.
- Every control on the phone layout is at least 44px tall and wide (one zero-specificity rule in
  `shell.mobile.css`); checkbox and radio rows are tap-sized with the label as the whole target. A page
  sheet may size a control it knows better, but not below 44px.
- The phone has three text sizes (14, 16, 20px); a card title there is the 20px step.
- A `.Mobile.cshtml` twin carries no `@media` query: its styles live in its own `*.mobile.css`, loaded
  through `@section Styles`. A view with no twin that has to adapt may use
  `@media (max-width: 767.98px)` inside `@layer pages`.
- Verify a phone view under a mobile User-Agent with touch enabled. Resizing the browser window alone does
  not count: it never serves the phone layout.

### Mobile twins

Many views have a `.Mobile.cshtml` sibling resolved by a view-location expander. When you change a
view, check whether its mobile twin exists and needs the same change — they drift easily.

Note that the twins are not always content-equivalent: some mobile views deliberately omit fields
the desktop view shows. Do not "fix" a missing field by adding it without checking whether the
omission was intentional.

## No inline code

New code carries no `style` attribute, no `<style>` block, no inline `<script>` and no inline event handler
(`onclick=` and the like). The content security policy is strict and a stylesheet or script file is
cacheable and reviewable.

- Style with a class. If no shared class fits, add one to a page stylesheet.
- Put scripts in files under `wwwroot/js`, as ES modules where they import anything.
- Load a page stylesheet (and a page script) from the view, so it loads only where it is used:

```cshtml
@section Styles {
    <link rel="stylesheet" href="~/css/my-page.css" asp-append-version="true" />
}

@section Scripts {
    <script type="module" src="~/js/my-page.js" asp-append-version="true"></script>
}
```

## Themes and tokens

**Add a token** in `tokens.css`, inside the tokens layer, in the group it belongs to. Name the role, not
the colour (`--qb-surface-overlay`, not `--qb-dark-brown`). If the token is a colour that carries text, add
its pair to the token contrast test in `UiFoundationLayerGuardTests` so a later edit cannot drop it below
4.5:1. A colour that Bootstrap reads as a bare channel triple also gets its `-rgb` companion. The number of
distinct opaque hex colours is capped; reuse a token before adding one.

**Add a theme block** as `[data-theme='name'] { ... }` inside the tokens layer of `tokens.css`. A theme
overrides semantic tokens only (board, surfaces, chrome, secondary text, action). Component rules never name
a theme. A page opts in with the attribute on `<html>`, not on `<body>`: on the html element it also
recolours the page's fallback background.

The board photo is a token seam. The shell stylesheet sets `--qb-board-photo` on `:root` and `tokens.css`
reads it through `--qb-board-image`; a theme replaces `--qb-board-image` as a whole. Never set
`--qb-board-image` on `body`, because that would stop a theme on `<html>` from replacing it.

## Vendor files

Third-party CSS, JavaScript and fonts are self-hosted and committed under
`wwwroot/lib/<name>-<version>/`, each with its licence file. Never link a CDN in a view or a stylesheet.
To bump a library, add the new version directory, repoint the references (the `app.css` imports, the layout
preloads and scripts) and remove the old directory; a new path is also a new cache key. Fonts live in
`wwwroot/lib/cinzel/` with their licence.

## Rendering dates and times

Never call `.ToString(...)` directly on a `DateTime` in a view. Use the helpers:

- `Html.LocalTime(instant, style)` for a **real instant** (UTC-stored; should follow the viewer)
- `Html.WallClock(value, style)` for a **wall-clock value** (a game night or proposed date; must
  never shift)

Styles are `date`, `date-time`, `date-compact`, `date-time-compact` — the same four names on both
the server (`HtmlHelperExtensions`) and the client (`TIMESTAMP_FORMATS` in `site.js`). Both helpers
share one format table per side; keep the two sides in agreement.

The server table may also carry server-only wall-clock styles (`time-24h`, `weekday-short`,
`weekday-long`, `weekday-long-year`) that have no client entry, so the client leaves their text exactly
as the server rendered it; add a client entry only when a style should follow the viewer's locale.
