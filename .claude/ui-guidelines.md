# UI/UX Design Guidelines

Read this before creating or editing any Razor view.

All new views must use the modern card pattern with these CSS classes: `modern-card`,
`modern-card-header`, `modern-card-body`.

```html
<div class="card-header modern-card-header">
    <h2 class="mb-0">
        <i class="fas fa-icon-name text-color me-2"></i>
        Page Title
    </h2>
</div>
```

- Always include `<hr>` before the button section
- Use filled colored buttons (not outline), FontAwesome icons with `me-2` spacing
- Button layout: `d-flex justify-content-between` — secondary (cancel) left, primary (submit) right

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
