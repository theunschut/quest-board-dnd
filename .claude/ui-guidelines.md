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

## Mobile twins

Many views have a `.Mobile.cshtml` sibling resolved by a view-location expander. When you change a
view, check whether its mobile twin exists and needs the same change — they drift easily.

Note that the twins are not always content-equivalent: some mobile views deliberately omit fields
the desktop view shows. Do not "fix" a missing field by adding it without checking whether the
omission was intentional.

## Rendering dates and times

Never call `.ToString(...)` directly on a `DateTime` in a view. Use the helpers:

- `Html.LocalTime(instant, style)` for a **real instant** (UTC-stored; should follow the viewer)
- `Html.WallClock(value, style)` for a **wall-clock value** (a game night or proposed date; must
  never shift)

Styles are `date`, `date-time`, `date-compact`, `date-time-compact` — the same four names on both
the server (`HtmlHelperExtensions`) and the client (`TIMESTAMP_FORMATS` in `site.js`). Both helpers
share one format table per side; keep the two sides in agreement.
