# Phase 64: Preserve line breaks in description text on mobile views to match desktop rendering - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Fix free-text fields that lose typed line breaks (`\n`) when rendered as HTML, because the containing element is missing `white-space: pre-wrap` (or equivalent). This phase closes every confirmed instance of that bug found during codebase investigation — not just the literal "mobile doesn't match desktop" case implied by the phase title, but every place the same underlying bug pattern (missing `white-space: pre-wrap` on a free-text display element) shows up, per user decision below.

</domain>

<decisions>
## Implementation Decisions

### Fix scope (confirmed via codebase investigation + user selection: "Characters + QuestLog + Shop" + card preview)

- **D-01:** `Characters/Details.Mobile.cshtml` — `Description` and `Backstory` fields render inside `.character-info-value` (`character-detail.mobile.css`), which has no `white-space: pre-wrap`. Desktop's `Characters/Details.cshtml` (lines 151, 159) already has it via inline `style="white-space: pre-wrap;"`. This is the literal, confirmed mobile-only bug matching the phase title. **In scope.**

- **D-02:** `QuestLog/Details.cshtml` (desktop) — the "Original Quest Description" box (lines 44-50) uses `.quest-description-box` (`quests.css:814-822`), which has no `white-space: pre-wrap`, while the "Rewards" box directly below it reuses the same class but adds an inline `style="white-space: pre-wrap;"` override (line 59). This is a **desktop-only** bug, already documented as finding IN-02 in `.planning/phases/59-add-a-rewards-field-to-quests-an-open-text-field-between-des/59-REVIEW.md` (deferred, never fixed). Mobile's equivalent (`QuestLog/Details.Mobile.cshtml`) already renders Description correctly because it reuses `.recap-display-box`, which already has `pre-wrap` — so no mobile-side change is needed for QuestLog; the fix here is purely on the desktop CSS/view. **In scope** — user chose to close this out alongside the mobile fix rather than leave a known, already-flagged bug open.

- **D-03:** Shop item `Description` — broken on **both** platforms equally (no mobile/desktop divergence): `Shared/_ShopItemDetailsContent.cshtml:136` (desktop, used by `Shop/Details.cshtml`) and `Shop/Details.Mobile.cshtml:21` (`.parchment-text-muted`) both render `@Model.Description` with no `pre-wrap` anywhere. **In scope.**

- **D-04:** Quest board list-card preview — `Quest/_QuestCard.cshtml:42` (`<p class="card-text">@Model.Description</p>`), a single partial shared identically by desktop and mobile quest lists (via `Quest/_QuestSection.cshtml`). Rendered inside a scrollable box (`.modern-card .card-text` — `max-height: 125px; overflow-y: auto`, `site.css:1313`) with no `pre-wrap`. Same bug pattern, no platform divergence (fix applies once, benefits both). **In scope** — user confirmed after this was surfaced as an additional finding mid-discussion.

### Explicitly out of scope (confirmed already correct — do not touch)

- Quest Details Description (`Quest/Details.cshtml` / `Quest/Details.Mobile.cshtml`) — both already have `pre-wrap`.
- QuestLog Session Recap (`QuestLog/Details.cshtml` / `Details.Mobile.cshtml`) — both already have `pre-wrap` via `.recap-display-box`.
- DungeonMaster Profile Bio (`Profile.cshtml` / `Profile.Mobile.cshtml`) — both already have `pre-wrap`.
- Contacts (NPC) Description and Notes (`Contacts/Details.cshtml` / `Details.Mobile.cshtml`) — both already have `pre-wrap` on Description and on note text.
- Admin `Quests.cshtml` Description column — intentionally single-line truncated (`text-truncate`) for a table cell; no mobile counterpart; not a line-break bug, this is deliberate truncation.
- `TradeItem`/`UserTransaction` `Description`/`Notes` domain fields — not rendered in any current view; nothing to fix.

### Claude's Discretion

- **Implementation mechanism per fix:** whether to use an inline `style="white-space: pre-wrap;"` (matching each page's existing local convention) or extend the relevant CSS class (`.quest-description-box`, `.character-info-value`, `.parchment-text-muted`, `.card-text`) — follow whatever convention the specific file already uses elsewhere on the same page, consistent with how every already-correct instance in the codebase does it (mix of both patterns exists; match the nearest sibling element's approach in each file).
- For D-02 (QuestLog desktop), note that `.quest-description-box` is also used by the Rewards box with a redundant inline `pre-wrap` override — adding `pre-wrap` to the CSS class itself would fix Description and make the Rewards inline override redundant (but harmless). Removing the now-redundant inline style on Rewards is a nice-to-have cleanup, not required.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Prior art / established convention
- `QuestBoard.Service/Views/Quest/Details.cshtml:31` — reference example of the correct inline `white-space: pre-wrap` pattern for Description text.
- `QuestBoard.Service/wwwroot/css/quests.css:813-834` — `.quest-description-box` / `.recap-display-box` — the established boxed-callout CSS pattern; `.recap-display-box` is the "already correct" version (has `pre-wrap`), `.quest-description-box` is the "still broken" version (D-02).
- `QuestBoard.Service/wwwroot/css/character-detail.mobile.css:57-60` — `.character-info-value` — the mobile CSS class that needs `pre-wrap` added (D-01).
- `QuestBoard.Service/wwwroot/css/site.css:1313-1319` — `.modern-card .card-text` — the scrollable card-preview box that needs `pre-wrap` added (D-04).

### Known deferred finding this phase closes out
- `.planning/phases/59-add-a-rewards-field-to-quests-an-open-text-field-between-des/59-REVIEW.md` §IN-02 — original documentation of the QuestLog Description-vs-Rewards inconsistency (D-02).

No other external specs/ADRs apply — this is a pure CSS/markup consistency fix with no schema, service, or ViewModel changes.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- The `white-space: pre-wrap` convention is already used consistently across ~90% of free-text display fields in this codebase (Quest Details, QuestLog Recap, DM Bio, Contacts Description/Notes) — this phase is closing the remaining gaps, not introducing a new pattern.

### Established Patterns
- Desktop views tend to apply `pre-wrap` via inline `style="white-space: pre-wrap;"` directly on the element.
- Mobile views (`*.Mobile.cshtml`) tend to apply it via a dedicated CSS class in the page's `*.mobile.css` file (e.g. `.dm-profile-bio-text`, `.recap-display-box`, `.note-view-text`).
- Both conventions coexist in the codebase already — no need to standardize on one, just follow whichever convention the specific file already uses for its sibling elements.

### Integration Points
- All four fixes (D-01 through D-04) are isolated CSS/inline-style changes to existing `.cshtml` views and their paired `.css` files. No controller, service, ViewModel, or migration changes are needed anywhere in this phase.

</code_context>

<specifics>
## Specific Ideas

No specific visual/UX requirements beyond "preserve line breaks as typed" — this is a rendering-correctness fix, not a design change. The four locations (D-01 through D-04) are exhaustive per this session's investigation; no other instances were found after cross-referencing every `string? Description|Bio|Backstory|Notes` domain model property against its rendering views.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (The QuestLog and Shop and card-preview items are not new capabilities; they're the same bug pattern the phase title already targets, confirmed in-scope by the user during discussion.)

</deferred>

---

*Phase: 64-Preserve line breaks in description text on mobile views to match desktop rendering*
*Context gathered: 2026-07-07*
