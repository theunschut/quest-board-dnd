# Phase 43: Mobile Parity Fixes - Pattern Map

**Mapped:** 2026-07-04
**Files analyzed:** 4 (all existing, modified in place — no new files)
**Analogs found:** 4 / 4 (2 are self-analogs — the fix is applied identically to a twin file; 2 are cross-file ports from an already-shipped desktop component)

## File Classification

| Modified File | Role | Data Flow | Closest Analog | Match Quality |
|----------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/wwwroot/css/mobile.css` | config (global stylesheet) | transform (CSS technique swap) | `QuestBoard.Service/wwwroot/css/site.css` (its own desktop twin, identical broken rule) | exact — same bug, same fix pattern, twin file |
| `QuestBoard.Service/wwwroot/css/site.css` | config (global stylesheet) | transform (CSS technique swap) | `QuestBoard.Service/wwwroot/css/mobile.css` (its own mobile twin, identical broken rule) | exact — same bug, same fix pattern, twin file |
| `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` | component (Razor view partial-page) | request-response (server-rendered list) | `QuestBoard.Service/Views/QuestLog/Index.cshtml` | exact — same model, same list, same badge concept, desktop already ships it |
| `QuestBoard.Service/wwwroot/css/quest-log.mobile.css` | config (component-scoped stylesheet) | transform (styling port) | `QuestBoard.Service/wwwroot/css/quests.css` (`.quest-log-card-footer .recap-badge` block) | exact — pixel-for-pixel port, values fully specified |

## Pattern Assignments

### `QuestBoard.Service/wwwroot/css/mobile.css` (config, transform)

**Analog:** `QuestBoard.Service/wwwroot/css/site.css` (both files carry the identical broken rule; fix each independently but identically)

**Current broken rule** (`mobile.css` lines 12-22):
```css
/* Mobile typography scale */
body {
    font-size: 16px; /* prevents iOS auto-zoom on input focus */
    line-height: 1.5;
    /* Notice board background — same image as desktop site.css */
    background-image: url('/images/Notice Board-blank2.jpg');
    background-size: cover;
    background-position: center;
    background-repeat: no-repeat;
    background-attachment: fixed;
}
```

**Fix pattern (D-01):** Remove `background-image`/`background-size`/`background-position`/`background-repeat`/`background-attachment` from the `body` rule (keep `font-size`/`line-height`) and move the background declarations onto a `body::before` pseudo-element, using `position: fixed`, full-viewport sizing, and negative stacking so it sits behind all content without needing any markup change (no wrapper `<div>` exists in `_Layout.Mobile.cshtml`'s `<body>` — confirmed by direct read, lines 1-40 — so the pseudo-element route requires zero `.cshtml` edits):

```css
body {
    font-size: 16px; /* prevents iOS auto-zoom on input focus */
    line-height: 1.5;
    position: relative; /* establishes containing block for ::before, if not already inherited */
}

body::before {
    content: "";
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100vh;
    height: 100dvh; /* dynamic viewport unit override for iOS Safari's toolbar-driven viewport changes; falls back to 100vh on browsers without dvh support since the declaration above sets it first */
    background-image: url('/images/Notice Board-blank2.jpg');
    background-size: cover;
    background-position: center;
    background-repeat: no-repeat;
    z-index: -1;
}
```

**Stacking safety:** confirmed no existing z-index in either file uses a negative value today (existing positive values in `site.css`: 10, 297, 499, 1025, 1030, 1493, 1645) — `z-index: -1` is safe and won't collide with any existing stacking context, per CONTEXT.md D-01/UI-SPEC.

**Explicitly rejected:** `-webkit-` prefix on `background-attachment` (no such prefix exists for this property) and any JS scroll-listener workaround.

---

### `QuestBoard.Service/wwwroot/css/site.css` (config, transform)

**Analog:** `QuestBoard.Service/wwwroot/css/mobile.css` (apply the identical fix here too, per D-02 — unreachable by real iOS Safari today since `MobileDetectionMiddleware` routes iPhone/iPad UAs to `mobile.css`, but fixed anyway for consistency at no extra cost)

**Current broken rule** (`site.css` lines 377-383):
```css
body {
    background-image: url('/images/Notice Board-blank2.jpg');
    background-size: cover;
    background-position: center;
    background-repeat: no-repeat;
    background-attachment: fixed;
}
```

**Fix pattern:** Same technique as `mobile.css` above — strip the background declarations off `body`, add a `body::before` pseudo-element carrying them with `position: fixed`, `100dvh`/`100vh`, `z-index: -1`:

```css
body {
    position: relative;
}

body::before {
    content: "";
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100vh;
    height: 100dvh;
    background-image: url('/images/Notice Board-blank2.jpg');
    background-size: cover;
    background-position: center;
    background-repeat: no-repeat;
    z-index: -1;
}
```

**Note:** `_Layout.cshtml`'s `<body>` tag already carries classes (`d-flex flex-column min-vh-100 @ViewData["BodyClass"]`, confirmed line 22) — verify the pseudo-element doesn't interact oddly with `d-flex` on `body` (it shouldn't, since `::before` on a flex container becomes a flex item unless given `position: fixed`, which removes it from flow — this is exactly why `position: fixed` is used, not `position: absolute`).

---

### `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` (component, request-response)

**Analog:** `QuestBoard.Service/Views/QuestLog/Index.cshtml` (desktop, lines 70-81)

**Desktop source pattern being ported** (`Index.cshtml` lines 70-81):
```html
<!-- Quest Footer -->
<div class="quest-log-card-footer">
    @if (!string.IsNullOrWhiteSpace(quest.Recap))
    {
        <span class="recap-badge">
            <i class="fas fa-scroll me-1"></i>Session Recap Available
        </span>
    }
    <span class="view-details-text">
        Click to view details <i class="fas fa-arrow-right ms-1"></i>
    </span>
</div>
```

**Current mobile card structure** (`Index.Mobile.cshtml` lines 39-56, confirmed via direct read — exact insertion point):
```html
<div class="quest-log-item"
     onclick="window.location.href='@Url.Action("Details", "QuestLog", new { id = quest.Id })'">
    <div class="d-flex justify-content-between align-items-start mb-1">
        <h6 class="quest-log-item-title mb-0 me-2">@quest.Title</h6>
        @if (boardType != BoardType.Campaign)
        {
            <span class="badge bg-secondary flex-shrink-0">CR @quest.ChallengeRating</span>
        }
    </div>
    <small class="text-muted d-block">
        <i class="fas fa-calendar-check me-1"></i>
        @((quest.FinalizedDate ?? quest.ClosedDate)?.ToString("MMM dd, yyyy") ?? "Unknown Date")
    </small>
    <small class="text-muted">
        <i class="fas fa-crown me-1"></i>
        @(quest.DungeonMaster?.Name ?? "Unknown DM")
    </small>
</div>
```

**Insertion pattern (D-05/D-07):** Add the badge block as a new row immediately after the DM-name `<small>` (after line 55), before the closing `</div>` (line 56) — copy the desktop guard/markup verbatim, no class changes (do NOT wrap in `quest-log-card-footer` — that class doesn't exist on mobile and isn't needed; the new `.recap-badge` rule in `quest-log.mobile.css` will be scoped directly, matching how `.quest-log-item .badge` is already scoped in that file):

```html
    <small class="text-muted">
        <i class="fas fa-crown me-1"></i>
        @(quest.DungeonMaster?.Name ?? "Unknown DM")
    </small>
    @if (!string.IsNullOrWhiteSpace(quest.Recap))
    {
        <span class="recap-badge d-block">
            <i class="fas fa-scroll me-1"></i>Session Recap Available
        </span>
    }
</div>
```

Note: adding `d-block` (or equivalent) on the `<span>` matches the UI-SPEC's "block-level row (own line), not inline with DM-name text" requirement (D-07/UI-SPEC "Row wrapping"), since desktop achieves the same block placement via its parent `.quest-log-card-footer` flex-column context rather than an inline modifier — mobile has no equivalent parent wrapper here, so `d-block` on the span itself is the simplest way to force it onto its own line without introducing a new wrapper div.

**Guard condition:** identical to desktop — `!string.IsNullOrWhiteSpace(quest.Recap)`, no additional condition.

---

### `QuestBoard.Service/wwwroot/css/quest-log.mobile.css` (config, transform)

**Analog:** `QuestBoard.Service/wwwroot/css/quests.css` lines 884-899 (`.quest-log-card-footer .recap-badge` + shared text-color rule)

**Desktop source values being ported** (`quests.css` lines 884-899):
```css
.quest-log-card-footer .recap-badge,
.quest-log-card-footer .view-details-text,
.quest-log-card-footer span {
    color: #F4E4BC !important;
    text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.9);
}

.quest-log-card-footer .recap-badge {
    background: rgba(255, 193, 7, 0.2);
    padding: 0.25rem 0.75rem;
    border-radius: 12px;
    border: 1px solid rgba(255, 193, 7, 0.4);
    font-size: 0.85rem;
    display: inline-block;
    margin-bottom: 0.5rem;
}
```

**Existing mobile-file conventions to coexist with** (`quest-log.mobile.css` lines 1-49 — glass-card `.quest-log-item`, parchment text on `small`/`.text-muted`, `.quest-log-item .badge { text-shadow: none !important; }`):
```css
.quest-log-item {
    background: rgba(255, 255, 255, 0.15);
    backdrop-filter: blur(15px);
    border: 1px solid rgba(255, 255, 255, 0.3);
    border-radius: 12px;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
    padding: 12px;
    margin-bottom: 8px;
    cursor: pointer;
    transition: all 0.3s ease;
}
```

**New rule to append to `quest-log.mobile.css`** — scope directly under `.quest-log-item` (no `.quest-log-card-footer` ancestor exists on mobile, so drop that prefix and scope one level shallower than desktop):

```css
/* Session Recap badge — ported pixel-for-pixel from desktop quests.css .recap-badge */
.quest-log-item .recap-badge {
    background: rgba(255, 193, 7, 0.2);
    padding: 0.25rem 0.75rem;
    border-radius: 12px;
    border: 1px solid rgba(255, 193, 7, 0.4);
    font-size: 0.85rem;
    display: inline-block;
    margin-bottom: 0.5rem;
    color: #F4E4BC !important;
    text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.9) !important;
}
```

Note: the existing `.quest-log-item small, .quest-log-item .text-muted` rule (lines 33-37) uses a *faded* parchment (`rgba(244, 228, 188, 0.7)`) for date/DM meta text — the recap badge intentionally uses the *full-opacity* `#F4E4BC` (per D-05, matching desktop exactly), so scope the new rule to `.recap-badge` specifically, not to a shared `small`/`.text-muted` selector, to avoid inheriting the faded variant.

---

## Shared Patterns

### iOS Safari fixed-background fix (applies to both `site.css` and `mobile.css`)
**Source:** `.planning/research/SUMMARY.md` §"Phase 5" and `.planning/research/PITFALLS.md` Pitfall 5 — cross-verified pattern, not derived from an in-repo analog since this bug class has no prior fix attempt in this codebase (both files currently carry the *same unfixed* rule).
**Apply to:** `mobile.css` (body rule, lines 13-22) and `site.css` (body rule, lines 377-383) — identical `::before` pseudo-element technique in both, since both currently have the identical broken declaration.
**Verification requirement (non-code, but load-bearing):** must be verified on a real iOS Safari session (physical iPhone), not devtools emulation — record exact device/iOS version/method per D-03/D-04 and Pitfall 5.

### Recap badge visual identity (applies to markup + CSS together)
**Source:** `QuestBoard.Service/wwwroot/css/quests.css:884-899` + `QuestBoard.Service/Views/QuestLog/Index.cshtml:72-77`
**Apply to:** `Index.Mobile.cshtml` (markup) and `quest-log.mobile.css` (styling) — both files must change together for the port to render correctly; markup alone (without the CSS rule) would render an unstyled `<span>` with no visual distinction.

## No Analog Found

None. All 4 files have a clear analog: 2 are self-referential twin-file fixes (site.css/mobile.css share the identical bug and identical fix pattern), 2 are direct desktop-to-mobile ports of an already-shipped component (Index.cshtml → Index.Mobile.cshtml, quests.css → quest-log.mobile.css).

## Metadata

**Analog search scope:** `QuestBoard.Service/wwwroot/css/`, `QuestBoard.Service/Views/QuestLog/`, `QuestBoard.Service/Views/Shared/_Layout*.cshtml`, `QuestBoard.Service/Middleware/MobileDetectionMiddleware.cs` (referenced, not re-read — already confirmed in CONTEXT.md)
**Files scanned:** `site.css`, `mobile.css`, `quests.css`, `quest-log.mobile.css`, `Index.cshtml`, `Index.Mobile.cshtml`, `_Layout.cshtml`, `_Layout.Mobile.cshtml`
**Pattern extraction date:** 2026-07-04
