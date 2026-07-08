---
phase: 57
slug: add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
status: draft
shadcn_initialized: false
preset: none
created: 2026-07-06
---

# Phase 57 — UI Design Contract (Contacts / NPC Directory)

> Visual and interaction contract for the Contacts feature. Generated retroactively (6 PLAN.md files already exist) at user request, to add design rigor beyond 57-CONTEXT.md's D-20 prose layout. Consumed by gsd-ui-checker, gsd-planner (if replanned), gsd-executor, and gsd-ui-auditor.

This is a server-rendered ASP.NET Core MVC (Razor) application — not a React/Vite/Next.js frontend. **The shadcn initialization gate does not apply** (no `package.json`, no `components.json`, no Node-based component tooling in this repo). The project's own design system is the CLAUDE.md "modern card" convention plus Bootstrap 5 + FontAwesome, already fully established across the Characters feature this phase mirrors. All design tokens below are extracted directly from that existing system — nothing here is newly invented except the 4 genuinely new UI patterns this phase introduces (Section "New Pattern Specs").

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none (no component registry — server-rendered Razor views) |
| Preset | not applicable |
| Component library | Bootstrap 5 (existing project dependency) + custom `modern-card` convention (CLAUDE.md) |
| Icon library | Font Awesome 6 (`fas fa-*`, confirmed via existing `Views/Characters/*.cshtml` usage) |
| Font | Bootstrap default body font; `'Cinzel', serif` reserved for decorative page-title headings only (e.g. `.characters-page h1`) — Contacts should follow the same split: page `<h1>` may use Cinzel treatment if the Index page wants visual parity with Characters, but this is optional flourish, not required |

Registry safety gate: **not applicable** (no shadcn, no component registry of any kind).

---

## Spacing Scale

This app does not declare a formal 4px-multiple spacing scale — it uses Bootstrap 5's spacing utility classes (`mb-1`/`mb-2`/`mb-3`/`mb-4`, `p-*`, `gap-2`) directly in markup, which already resolve to a 4px-based scale (`0.25rem`/`0.5rem`/`1rem`/`1.5rem` = 4/8/16/24px at the default 16px root). Mirror the exact spacing classes already used in `Views/Characters/*.cshtml` — do not introduce new raw-pixel spacing.

| Token | Bootstrap class | Value | Usage in this phase |
|-------|-------|-------|-------|
| xs | `me-1` / `mb-1` | 4px | Icon-to-text gaps inside badges and buttons |
| sm | `me-2` / `mb-2` | 8px | Icon-to-label gaps in headers/buttons (CLAUDE.md's `me-2` convention) |
| md | `mb-3` | 16px | Default field/section spacing on Create/Edit forms |
| lg | `mb-4` | 24px | Card-to-card spacing, header row bottom margin |
| xl | `py-5` (empty states) | 48px | Empty-state vertical padding (matches Characters' `text-center py-5`) |

Exceptions: none. Notes-list item spacing uses `mb-3` between note cards (see New Pattern Specs).

---

## Typography

This app has no declared type scale document — it inherits Bootstrap 5 defaults. The wider app's Bootstrap heading scale (h1–h5) spans more sizes than a single feature should use; **for Contacts views specifically**, the active type scale used across Index/Details/Edit/Create (desktop + mobile) is capped at exactly 4 sizes:

| Role | Size | Weight | Line Height | Source |
|------|------|--------|-------------|--------|
| Card heading | 24px (Bootstrap h4, `1.5rem`) | 500 (Bootstrap default heading weight) | 1.2 | `modern-card-header` titles — every Contacts card header (Index section header, Details Description/Notes/Actions cards, Edit/Create form header) uses **h4/24px specifically**, not a range. Do not vary this per screen. |
| Body / form text / label / note text | 16px (Bootstrap `1rem` default) | 400 (body/note text), 700 `fw-bold` (labels) | 1.5 (Bootstrap default) | `.form-control`, `.form-control-plaintext`, `Views/Characters/Details.cshtml` labels, Notes Card body text |
| Note metadata (author + timestamp) | 14px (`small`/`text-muted`) | 400 | 1.5 | New — matches `character-owner`/`small.text-muted` precedent |
| Badge text | 12px (`.badge` default ~0.75rem) | 600–700 | 1 | Matches `.retired-badge`/`.dead-badge`/`.main-badge` (0.75rem, font-weight 600–700). Badges are a UI-convention element (status chips), not body/heading copy — counted here as the 4th size because Contacts views render it directly (Hidden badge), not omitted. |

That is the complete set: **24 / 16 / 14 / 12px — 4 sizes, no more.** Do not introduce a 5th distinct size anywhere in Contacts views.

**Excluded from this phase's active scale (footnote, not a 5th size):** the wider app's `.characters-page h1` decorative treatment (Bootstrap h1, 40px, `'Cinzel', serif`, weight 700) and the h3/h5 variants of the Bootstrap heading scale (28px/20px) exist elsewhere in the app but are **not used by any Contacts view**. If a future Contacts screen wants a decorative page-level `<h1>` for visual parity with Characters, that would be an explicit, separately-reviewed exception to this contract — not an implicit 5th size. As specified, no Contacts view in this phase renders an h1, h3, or h5.

---

## Color

This app's palette is Bootstrap 5 semantic colors + a dark "fantasy/D&D" accent treatment on card-grid pages (Characters, Shop). Contacts inherits the same palette — no new colors.

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | Bootstrap default light surface (`#fff` / default body bg) on form/details pages; dark gradient `linear-gradient(145deg, rgba(20,20,30,.95), rgba(30,30,40,.95))` on the Characters-style card-grid Index | Page background, card bodies |
| Secondary (30%) | `modern-card` / `modern-card-header` surfaces (Bootstrap `.card` white/light gray), nav bar | Cards, headers, containers |
| Accent (10%) | `#ffc107` (Bootstrap `warning`/amber-gold — this app's signature "quest board gold" accent, used for primary CTAs, hover glows, and the "+ Character"-style create button) | Reserved for: the "+ Contact" primary button, Edit button (`btn-warning`), card-hover glow border on Index cards, focus-ring color on form inputs (already global via `site.css` `.form-control:focus`) |
| Destructive | `#dc3545` (Bootstrap `danger` red, `btn-danger`) | Reserved for: Delete Contact button, Delete Note icon button, remove-class-style destructive icon buttons |

Additional semantic colors already established and reused as-is (not new):
- `bg-success` (`#198754`) — "Active"/positive state badges, Reactivate-style buttons
- `bg-secondary` (`#6c757d`) — Retired-style muted badges, Cancel buttons, secondary/neutral actions
- `bg-dark` (`#212529`) — Dead-style badges (not applicable to Contacts, but same dark-badge token reused for "Hidden")
- `bg-warning` (`#ffc107`) — Main-style highlighted badges

Accent reserved for: "+ Contact" button, "Edit Contact" button, Index card hover-glow border, form focus rings. Never used for informational badges (those use success/secondary/dark per element, see below) or for the Show Hidden toggle's OFF state.

---

## Copywriting Contract

| Element | Copy |
|---------|------|
| Primary CTA (Index, DM-tier) | `+ Contact` (button label — matches D-20's exact phrasing; icon `fa-plus`) |
| Create page title | `Create New Contact` |
| Edit page title | `Edit {Contact Name}` |
| Empty state heading (Index, no Contacts at all) | `No Contacts Yet` |
| Empty state body (Index) | `No one has added a contact yet. DMs can create the first one to start building out the world.` (Players see this too, per D-19 visibility — copy must not assume the viewer is a DM) |
| Empty state heading (Notes list, no notes yet) | `No Notes Yet` |
| Empty state body (Notes list) | `Be the first to jot something down about this contact.` |
| Add-note button/placeholder | Textarea placeholder: `Add a note about this contact...` — submit button: `Add Note` (icon `fa-plus`) |
| Error state (generic form validation) | Existing `asp-validation-summary="All"` pattern — no custom copy needed, reuse ASP.NET Core's field-level messages exactly as Characters does |
| Error state (hidden Contact, unauthorized) | Standard app 404 page (no bespoke "this is hidden" messaging — D-13 requires the same not-found convention as cross-tenant access, which must NOT leak that a hidden record exists) |
| Destructive confirmation (Delete Contact) | `Are you sure you want to delete this contact? This action cannot be undone.` (mirrors Characters' exact wording, swapping "character" → "contact") |
| Destructive confirmation (Delete Note) | `Delete this note? This action cannot be undone.` (new — shorter, since it's a per-row inline action, not a full-page destructive action) |
| Show Hidden toggle, OFF state label | `Show Hidden` (icon `fa-eye-slash`) |
| Show Hidden toggle, ON state label | `Hide Hidden` (icon `fa-eye`) — alternatively `Showing Hidden` as a pressed/active-state label; see New Pattern Spec 2 for exact recommendation |
| Hidden badge (Index list + Details) | `Hidden` (icon `fa-eye-slash`) |
| Reveal action (Details Actions card, currently hidden) | `Reveal Contact` (icon `fa-eye`, button style `btn-success` — a positive, forward action) |
| Hide action (Details Actions card, currently revealed) | `Hide Contact` (icon `fa-eye-slash`, button style `btn-secondary` — a neutral/retracting action, NOT destructive-red since it's reversible and non-destructive) |

---

## New Pattern Specs

These are the 4 genuinely new interaction patterns this phase introduces. Everything else (Index card grid, Details portrait card, Edit/Create form layout) is a near-exact mirror of `Views/Characters/*.cshtml` — see "Mirrored Patterns" section below for the brief pointer.

### 1. Notes List (Details page, right column, below Description card)

**Structure:** A new `modern-card` titled "Notes", placed directly below the existing Description card in the right (`col-lg-8`) column. Contains, top to bottom:

1. **Add-note form** (visible to all group members, not just DM-tier — per D-16):
   - A `<textarea>` (3 rows, `class="form-control"`, `placeholder="Add a note about this contact..."`, `maxlength="2000"`), full width.
   - A `Add Note` submit button directly below, right-aligned (`d-flex justify-content-end`), `btn-warning` (accent color — this is the phase's one primary "contributory" action on an otherwise view-only page), icon `fa-plus me-2`.
   - `<hr>` separates the form from the list below it (per CLAUDE.md's "always include `<hr>` before the button section" — here inverted: the hr divides the input area from the read area, marking a clear visual break).

2. **Notes list** (newest first, per D-10):
   - Each note renders as its own lightweight bordered block, NOT a full `modern-card` (that would be too heavy repeated N times) — use a simple `<div class="note-item border rounded p-3 mb-3">`.
   - **Header row** inside each note item: `d-flex justify-content-between align-items-start`.
     - Left: author name + timestamp, styled exactly like Characters' existing `character-owner` convention — `<small class="text-muted"><i class="fas fa-user me-1"></i>{AuthorName} · <i class="fas fa-clock me-1"></i>{timestamp, e.g. "Jul 6, 2026 3:42 PM"}</small>`. If the note was edited, append ` (edited)` in the same muted small text — no separate audit trail needed (matches RESEARCH.md's `UpdatedAt` nullable field).
     - Right: two icon-only buttons, `btn-sm btn-outline-secondary` for Edit (icon `fa-pen`) and `btn-sm btn-outline-danger` for Delete (icon `fa-trash`), both with `title` tooltips ("Edit note" / "Delete note") since they carry no text label — this is the one sanctioned exception to CLAUDE.md's "filled colored buttons, not outline" rule, justified because these are dense per-row inline icon actions where a filled button would visually overwhelm a repeated list (same reasoning Bootstrap-based dense list UIs commonly use outline/ghost buttons for row-level actions). Any group member sees both (D-09 — no ownership restriction).
   - **Body**: the note text itself, `<p class="mb-0 mt-2" style="white-space: pre-wrap;">{Text}</p>` — matches Characters' Description/Backstory `white-space: pre-wrap` convention for preserving line breaks in freeform text.
   - **Edit-in-place interaction**: clicking the pencil icon swaps the note body `<p>` for an inline `<textarea>` (same styling as the add-note form) with `Save`/`Cancel` buttons (`btn-sm btn-warning` / `btn-sm btn-secondary`) replacing the two icon buttons — no navigation to a separate page, no modal. This is a small vanilla-JS toggle (show/hide two sibling elements), consistent with this app's existing pattern of light inline JS (e.g. the Classes add/remove list on Character's Create/Edit forms) rather than introducing a new JS framework or a modal library.
   - **Delete interaction**: clicking the trash icon shows a native `confirm()` dialog (`Delete this note? This action cannot be undone.`) before submitting — same mechanism as Characters' Delete Contact button, just scoped to a single note row via a small inline `<form>` per note (POST to a `DeleteNote` action with the note's Id as hidden input).

3. **Empty state** (no notes yet): centered, matching Characters' empty-state visual convention — `<div class="text-center py-4"><i class="fas fa-comment-slash fa-2x text-muted mb-2"></i><p class="text-muted mb-0">No Notes Yet</p><p class="text-muted small">Be the first to jot something down about this contact.</p></div>`. Note: use `py-4`/`fa-2x` (slightly smaller than Characters' `py-5`/`fa-3x` Index-level empty states) since this is a sub-section within a page, not a full-page empty state.

**Ordering:** newest-first via `OrderByDescending(CreatedAt)` at the query layer (per RESEARCH.md) — visually this means the add-note form sits at the top, immediately followed by the most recent note, reading top-to-bottom as a reverse-chronological feed (like a comment thread, not a chat log).

### 2. "Show Hidden" Toggle Button (Index page header, DM-tier only)

**Placement:** In the same header row as "+ Contact" (`d-flex justify-content-between align-items-center mb-4`), positioned immediately to the left of "+ Contact" (i.e. `... [Show Hidden toggle] [+ Contact]`, right-aligned as a button group) — only DM-tier viewers (DungeonMaster/Admin/SuperAdmin, resolved via `GetEffectiveGroupRoleAsync`, not raw `IsInRole`, per RESEARCH.md Pitfall 1) see the toggle at all; Players see only the page title (no button on their side since "+ Contact" is also DM-tier-only per D-09b, meaning Players may see zero buttons in that header row).

**Visual states** — this is a two-state pressed/toggle button, not two different buttons:
- **OFF (default)**: `btn btn-outline-secondary`, icon `fa-eye-slash me-2`, label `Show Hidden`. This is the one other sanctioned outline-button exception (alongside note row-actions) — justified because a toggle's OFF state should read as visually recessive/neutral (Bootstrap's own pattern for toggle/filter buttons), while the "+ Contact" primary action stays filled/accent to keep the create action visually dominant.
- **ON (active)**: `btn btn-secondary` (filled, same neutral gray — NOT accent gold, since this is a visibility filter, not a primary action), icon `fa-eye me-2`, label `Hide Hidden`. The filled-vs-outline swap communicates "currently pressed" the same way Bootstrap's `.active` toggle-button convention works, without needing extra ARIA-only state (though `aria-pressed="true"/"false"` should still be set for accessibility).
- Implemented as a simple POST form (`asp-action="ToggleShowHidden"`) that flips the session value and redirects back to Index — no client-side JS/AJAX needed, consistent with this app's existing `ToggleRetirement` pattern on Characters.

**Hidden Contact row treatment when toggle is ON (or viewer is the creator):** each hidden Contact's card in the grid gets a `Hidden` badge — reuse the exact positioning/style convention of `.retired-badge` (top-right corner, `rgba(108,117,125,.95)` background, white text, `border-radius: 20px`, `0.75rem` font, `backdrop-filter: blur(5px)`) but with icon `fa-eye-slash` and label `Hidden` instead of `fa-moon`/`Retired`. Do NOT reuse the dark `.dead-badge` styling (`rgba(33,37,41,.95)`) — that's reserved for the Dead character status elsewhere in the app; Hidden gets the secondary-gray treatment to signal "administratively withheld," not "deceased."

### 3. "Hidden" Badge (Index list rows + Details page portrait card)

**Index (card grid):** positioned exactly like Characters' `.retired-badge` — top-right of the card thumbnail, `rgba(108, 117, 125, 0.95)` background, white text, `fa-eye-slash me-1` icon, label `Hidden`. If a Contact is both hidden AND the badge system needs to coexist with any future status badges, Hidden takes top-right precedence (Contacts have no other badge type today, so no collision to resolve).

**Details page (portrait card, left column):** same badge treatment as Characters' status badges — a `<span class="badge bg-secondary fs-6"><i class="fas fa-eye-slash me-1"></i>Hidden</span>` placed in the same `<div class="mb-2">` block below the name/town-city, matching exactly where Characters renders its Dead/Retired/Active status badge. Shown only when the record is hidden AND the current viewer is permitted to see it at all (creator or toggle-ON DM-tier viewer) — a Player literally cannot reach this page for a hidden Contact (D-13's 404), so the badge never needs a Player-facing empty/neutral state.

### 4. Reveal/Hide Toggle Action (Details page, Actions card)

**Placement:** Inside the existing Actions card (left column, below the portrait card) — DM-tier only (`canEdit`-equivalent gate, same card visibility gate Characters uses), positioned between Edit and Delete, mirroring where Characters places its Retire/Reactivate toggle form:

```
[ Edit Contact ]       btn-warning, full width, mb-2
[ Reveal Contact ] or [ Hide Contact ]   -- toggle form, mb-2
[ Delete Contact ]      btn-danger, full width
```

- **Currently hidden → show "Reveal Contact"**: `btn btn-success w-100` (icon `fa-eye me-2`), a positive/forward action — mirrors Characters' `Reactivate Character` success-green treatment for "bringing something back into visibility."
- **Currently revealed → show "Hide Contact"**: `btn btn-secondary w-100` (icon `fa-eye-slash me-2`), a neutral/reversible action — mirrors Characters' `Retire Character` secondary-gray treatment for "stepping something back without destroying it."
- Both implemented as a single POST form (`asp-action="ToggleReveal"`) with a hidden `id` input, matching `ToggleRetirement`'s exact form shape — no confirmation dialog needed (this is non-destructive and instantly reversible, unlike Delete).

---

## Mirrored Patterns (brief — see `Views/Characters/*.cshtml` for full reference)

**Visual focal point:** Index — the card grid is the sole focal point; no competing hero element or banner above it. Details — the portrait card (left column) draws the eye first via its dark-gradient/hover-glow treatment, with the Notes card as the secondary reading focus below the Description card in the right column.

These carry over near-verbatim from the Characters feature with only label/field substitutions — no new design decisions needed:

- **Index page layout**: single flat `modern-card` section (not two sections like Characters' My/Other split, per D-17) containing a `character-grid`-equivalent `contact-grid` (reuse the identical CSS grid: `repeat(auto-fill, minmax(250px, 1fr))`, `gap: 1.5rem`). Card content: thumbnail/placeholder, name, town/city (in place of class/level), sub-location as a secondary line if present. Same hover/glow/dark-gradient card treatment as `.character-card`.
- **Details page layout**: two-column (`col-lg-4` portrait + Actions card / `col-lg-8` Description + Notes card), exactly per D-20.
- **Edit/Create form layout**: single `modern-card` with `card-header modern-card-header`, image upload field (reuse `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` client-side JS validation script verbatim from Character's Create/Edit), Name/TownCity/SubLocation/Description text fields, `d-flex gap-2` Save+Cancel button row (Save = `btn-warning`, Cancel = `btn-secondary`, per CLAUDE.md's button-layout convention).
- **Placeholder image treatment**: `fas fa-user fa-3x`/`fa-5x` on Character; for Contacts consider `fas fa-user-secret` or `fas fa-user` (same generic-person icon is acceptable — no strong reason to differentiate; Claude's Discretion, default to `fa-user` for consistency).
- **Mobile views**: full parity per D-18, same responsive breakpoint (`@media (max-width: 768px)`) and single-column grid collapse Characters already uses.

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|--------------|
| shadcn official | none | not applicable (no shadcn in this project) |
| third-party | none | not applicable |

No component registry of any kind is used in this project. This section is intentionally empty beyond the template's required structure.

---

## Checker Sign-Off

- [ ] Dimension 1 Copywriting: PASS
- [ ] Dimension 2 Visuals: PASS
- [ ] Dimension 3 Color: PASS
- [ ] Dimension 4 Typography: PASS
- [ ] Dimension 5 Spacing: PASS
- [ ] Dimension 6 Registry Safety: PASS

**Approval:** pending
