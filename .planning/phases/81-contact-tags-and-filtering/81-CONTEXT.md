# Phase 81: Contact Tags and Filtering - Context

**Gathered:** 2026-08-30
**Status:** Ready for planning

<domain>
## Phase Boundary

A DM can attach free-form, per-group tags — "shopkeeper", "quest giver" — to the board's NPCs, and the Contacts index offers a filter that narrows the list to the selected tags, on both desktop and mobile.

The phase delivers: a `ContactTag` entity scoped to a group with a many-to-many join to `ContactEntity`, a chips/typeahead tag field on the four Contacts Create/Edit views, tag chips on both Contacts index views and a tag line on both Details views, and a tag filter control on both index views whose state lives in the query string.

**Every one of those surfaces is DM-tier only.** Players see the Contacts index exactly as Phase 80 leaves it — no chips, no filter, no tag markup at all.

Out of scope: renaming or merging tags, any tag management page, tags on Characters or Quests, bulk tagging, AND-semantics filtering, free-text search over contacts, and opening any tag surface to players.

**Note on requirements:** ROADMAP.md lists `Requirements: TBD` for this phase — no `CONTACTTAG-*` IDs exist in REQUIREMENTS.md. As with Phase 80, the decisions below are the requirement source for planning. Phase 82 minted its `EVTAGENDA-*` family as its first plan; the planner may do the same here.

**Note on sequencing:** Phase 80 (Contact Categories) has a CONTEXT.md but **no plans and no code**. Nothing in this phase depends on Phase 80 having shipped, but several decisions below are written to compose with 80's locked shape (category headings, the `IsVisibleTo` ordering rule, the two-views-no-shared-partial rule). If 80's shape changes during its own planning, re-read D-11 and D-17.

</domain>

<decisions>
## Implementation Decisions

### Audience — who sees tags at all

- **D-01: Tags are DM-tier only. Players see no tags, no chips, and no filter control.** Every tag surface sits inside the `ViewerIsDmTier` conditional the index views already use for the Show Hidden toggle and the Create button.

  The safety argument was not the deciding one — D-12's viewer-scoped vocabulary already prevents a tag from surfacing unless a contact bearing it is visible. The deciding argument is **direction of reversibility**: if tags ship player-visible and a DM's naming habits ("Betrayer", "Corridor Spy") turn out to telegraph plot, the damage has already happened at the table. If they ship DM-only and players want them, a later phase flips one conditional.

  Secondary benefit: one audience's markup to verify per platform instead of two states, which matters given D-22's real-User-Agent requirement.

  **Accepted cost, named deliberately:** this is asymmetric with Phase 80, where category headings *are* player-visible. Two organisational systems on one page with different audiences is a thing that will need explaining. And "which of these NPCs sells things?" is a real player question this phase declines to answer.

- **D-02: `[Authorize(Policy = "DungeonMasterOnly")]` on every tag write** — the exact gate `ContactsController`'s Create/Edit/Delete already carry. This settles the ROADMAP's stated open question ("whether players may create tags or only DM-tier users") as a consequence of D-01 rather than as a separate choice: players cannot see tags, so they certainly cannot author them.

### Data model

- **D-03: A real `ContactTag` entity — `Id`, `Name`, `GroupId` — joined many-to-many to `ContactEntity`.** Not a second category column (the ROADMAP explicitly forbids that), and not a denormalised name-per-contact row.

  The denormalised shape was considered and rejected: it makes orphans impossible and needs no vocabulary table, but duplicate prevention then rests on a write-time convention rather than a database guarantee — and with no rename path (D-07), a duplicate is permanent. It would also have reopened D-09, since a schema with no vocabulary table has no stable ids to put in a URL.

- **D-04: Tag names are unique per group, case-insensitive** — a unique index on `(GroupId, Name)`, the same shape Phase 80 D-04 uses for categories. Typing "Shopkeeper" when "shopkeeper" already exists **reuses the existing row** rather than minting a twin. This index is what makes shipping without a rename page survivable: it eliminates the most common duplicate class at the database.

- **D-05: `ContactTag` needs its own fail-closed `HasQueryFilter` in `QuestBoardContext.cs`**, in the same shape as `ContactEntity` at line 405 — `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`. A null active group returns zero tags, never every board's tags merged.

  **The lambda must dereference `activeGroupContext` inline.** Capturing `ActiveGroupId` into a local reads it once at model-build time (null) and silently breaks the filter — the file's own comment at line 333 warns about exactly this.

  Like `ContactEntity` (see the comment at `QuestBoardContext.cs:402`), `ContactTag` gets **no SuperAdmin cross-group view**. It is per-group roster data.

- **D-06: Orphaned tag rows are pruned when the last contact drops them** — on contact save and on contact delete. Since D-12 derives the filter list from tags on visible contacts, an orphaned row renders nowhere, so this is table hygiene rather than user-visible behaviour.

  **Accepted cost:** re-adding a removed tag mints a new id, so a bookmarked filter URL pointing at the old id silently matches nothing. That is the correct failure — **an unknown, deleted, or foreign tag id in the query string must silently match nothing, never 404 and never error.** Fail-closed, the same way `AgendaController` drops stale board ids rather than rejecting them.

- **D-07: No management page. Tag creation is free-typed on the contact form; removing a tag from its last contact is how a DM deletes it.** There is no rename path — correcting a genuine misspelling means editing every contact that carries it.

  This was chosen over mirroring Phase 80's Manage Categories page, and it is the leanest cut available: the entire feature is the contact form plus the filter, with no new management views to build or verify on two platforms. **D-04 and D-17 are the compensations** — the unique index kills the case-variant duplicate class, and rendering chips on the index gives a DM the vocabulary-audit surface the management page would have provided.

### Filter semantics and state

- **D-08: OR semantics — a contact matches if it carries any of the selected tags.** Ticking "shopkeeper" and "quest giver" merges both groups. This settles the ROADMAP's stated open question. Identical to how `ShopController`'s rarity checkboxes already behave, so the codebase keeps one filter idiom; ticking more boxes widens the result, which is what a checkbox list leads people to expect.

- **D-09: Filter state lives in the query string as repeated tag ids — `?tag=3&tag=7`** — bound exactly the way `ShopController.Index` binds `IList<ItemRarity>? rarity`, with no manual parsing. Ids, not names: a name has no stability guarantee and would need case-insensitive lookup on every request.

  **Not session.** The ROADMAP is explicit that the Show Hidden toggle's per-group session scoping is not the pattern to copy here. `AgendaController`'s comma-joined shape was also rejected — it needed raw `Request.Query` reading only because it had to express "none selected", a problem this phase does not have, since an empty tag filter simply means "show everything".

- **D-10: The filter narrows what the viewer could already see and can never widen it.** It is applied **in memory, after `ContactsController.IsVisibleTo`** — never in the query, never before the visibility gate. This carries Phase 80 D-13 forward unchanged: `IsVisibleTo` (`ContactsController.cs:469`) covers both `IsRevealed` and the per-group Show Hidden session toggle, and grouping and filtering both happen downstream of it.

  **`IgnoreQueryFilters()` is forbidden on every path in this phase** (Phase 78 D-12). This app has shipped two real cross-tenant leaks (Phases 49/55).

- **D-11: Under an active filter, Phase 80's category headings stay, and empty ones drop out.** The filter narrows the contact set, then Phase 80 D-13's existing suppression rule runs unchanged — a heading renders only if at least one contact survives beneath it.

  Filtering to "shopkeeper" therefore shows *which categories* the board's shopkeepers live in, which a flat result list throws away. Flattening was rejected because it would give each index view two rendering modes and require bypassing D-13's rule rather than reusing it — a second code path across two views on two platforms.

- **D-12: The filter lists only tags carried by contacts this viewer can see** — derived from the viewer's **visible-but-unfiltered** contact set, which the controller has already loaded.

  Note the "unfiltered" half: derive the vocabulary from the visibility-filtered set *before* the tag filter is applied, or ticking one tag makes every other tag vanish and a second one can never be added.

  This needs **no separate vocabulary query at all**, which makes it fail-closed by construction rather than by a rule someone has to remember. It mirrors Phase 80 D-13: a tag borne only by unrevealed contacts renders nowhere, and reappears for a DM who flips Show Hidden.

- **D-13: The tag filter survives a Show Hidden toggle.** The Show Hidden form carries the currently selected tag ids as hidden fields, and `ToggleShowHidden` re-attaches them to its redirect.

  **This requires a deliberate change:** `ContactsController.ToggleShowHidden` currently ends `RedirectToAction(nameof(Index))` with no route values, which would drop the query string. Filtering to "shopkeeper" and then flipping Show Hidden to check for unrevealed ones is the single most likely moment for a DM to use both controls together, and losing the filter there reads as the feature being broken.

### Tag entry on the contact form

- **D-14: A chips / typeahead widget on all four Create/Edit views** — `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`.

  A no-JS checkbox list was recommended and declined. The concern that motivated the recommendation — that Phase 80 rejected drag-and-drop for lack of JS precedent — **turned out not to apply**: `wwwroot/js/markdown-editor.js` and `wwwroot/js/image-crop.js` are both hand-written vanilla-JS enhancements of form fields, and both already load on these exact four views via `@section Scripts`. Phase 80's rejection was of a new interaction model on the *index*, not of form-field enhancement. The house pattern directly supports this choice.

- **D-15: Wrap a CDN library (Tagify or similar) rather than hand-rolling** — the exact shape `image-crop.js` uses for `cropperjs`: a CDN `<script>` pinned with `integrity` and `crossorigin`, plus a thin init module in `wwwroot/js/` loaded with `asp-append-version="true"`. Buys paste handling, keyboard navigation, deduplication, and accessibility rather than hand-rolling them.

  **Costs the planner must handle:** the library's default styling has to be overridden to match this app's theme on both platforms (belongs in `contacts.css` / `contacts.mobile.css`, not inline), and the version plus its SRI hash need pinning. Research should confirm the current version and hash rather than assuming.

- **D-16: The widget degrades to a plain comma-separated text input.** The underlying control is a real `<input>` holding `shopkeeper, quest giver`; the library enhances it in place and writes the same comma format back on change (Tagify's `originalInputValueFormat`).

  **The server therefore parses one value shape regardless of whether JS ran** — split, trim, drop empties, dedupe case-insensitively, upsert against D-04's index. A DM on a blocked network can still tag, just without chips. This is close to free given the library binds to a real input anyway.

### Display

- **D-17: Tag chips on the index cards, and a muted tag line on Details** — both DM-tier only, mirroring Phase 80 D-16's treatment of the category on `Details.cshtml` / `Details.Mobile.cshtml`.

  The index is doing double duty here: it is the **vocabulary audit surface** given up when D-07 cut the management page. A DM can see the whole in-use vocabulary at a glance and spot "Shopkeper" sitting beside "shopkeeper" — which is the only way that misspelling ever gets noticed. It also makes a filtered result self-explanatory.

  Chips must wrap gracefully in both the desktop `contact-grid` cards and the mobile `contact-member-row` layout.

- **D-18: Tag names render as plain Razor-escaped text — never through `IMarkdownService`.** Carried forward from Phase 80 D-15: a tag is a label, not content, and a Markdown tag could carry a link or an image. `Contact.Name` and `TownCity` are already handled this way on the same views.

- **D-19: Before a board has any tags, the filter control renders disabled with helper text pointing at the contact form.**

  This deliberately applies Phase 80 **D-07's** logic, not D-10's. D-10 hides categories from the index until one exists because *players* read that page and an empty heading is noise for them. The tag filter is DM-only (D-01), so the reasoning that made Phase 80's disabled category dropdown discoverable applies instead: a DM learns the feature exists from the page they already use, and no player ever sees it.

- **D-20: The filter control follows the Shop pattern on both platforms** — a `method="get"` form with tag checkboxes and Apply / Clear on desktop (`Shop/Index.cshtml`'s `shop-filter-row`), and a bottom offcanvas drawer behind a Filter button on mobile (`Shop/Index.Mobile.cshtml`'s `shopFilterOffcanvas`). Proven on both platforms in this codebase, and the only shape that makes D-08's multi-select genuinely usable — tick three tags, apply once.

  Clickable chips as a filter shortcut were considered. They compose naturally with D-09 (a chip click is just a URL with one tag id) but were left out as a second entry point into the same state; see Deferred Ideas.

- **D-21: Two-branch empty state, mirroring the Shop.** "No contacts match your filters" with a Clear filters action, kept distinct from the message a genuinely empty contact list shows — the exact pattern `Shop/Index.cshtml` already renders. The filter control stays on screen above it, so adjusting a selection does not require clearing first.

- **D-22: Desktop and mobile ship together in this phase**, and **mobile markup is verified with a real mobile User-Agent, not devtools emulation** (Phase 80 D-08, Phase 74 D-16). `MobileDetectionMiddleware` selects the layout from the User-Agent; "mobile markup that was never selected" is already on PROJECT.md's record of shipped bugs.

### Test coverage this phase must deliver

Derived from Phase 80 D-17–D-20's pattern, not separately elected.

- **D-23: Cross-group tag isolation, proved by a two-group integration test.** Group A's tags never appear on group B's index, in group B's filter list, or in group B's tag-entry suggestions; a POST attaching a contact to a tag id owned by another group is refused rather than silently accepted. `IgnoreQueryFilters()` forbidden on every path.

- **D-24: The audience gate, both directions.** A player receives no tag chips, no filter control, and no tag markup on either index view or either Details view; a DM-tier viewer receives all of it. This is the rule most likely to regress the next time either index view is touched.

- **D-25: The filter narrows and never widens.** A tag filter cannot surface a contact that `IsVisibleTo` excluded — including the case where an unrevealed contact carries the filtered tag and Show Hidden is off.

- **D-26: OR semantics and heading composition.** Two selected tags return the union, not the intersection; category headings survive an active filter and empty ones are suppressed (D-11).

- **D-27: Vocabulary scoping.** A tag borne only by unrevealed contacts does not appear in the filter list for a viewer who cannot see them, and does appear for a DM with Show Hidden on. Mirrors Phase 80 D-18.

- **D-28: Orphan pruning asserted against the database**, not inferred from the UI — removing a tag from its last contact deletes the row, and re-adding the name creates a fresh one.

- **D-29: The no-JS path.** POSTing the plain comma-separated format tags the contact correctly, deduplicates case-insensitively, and reuses existing rows per D-04.

- **D-30: The Show Hidden round trip preserves the filter** (D-13) — the redirect carries the selected tag ids.

### Claude's Discretion

- **Tag name length and count cap.** The operator declined to choose. **Locked as: names capped at ~30 characters** — shorter than Phase 80's ~60 for categories, because a tag renders as an inline chip rather than a section heading and a long one wraps badly on the mobile stacked-row layout — **and no hard cap on tags per contact**, with the chip markup required to wrap gracefully instead. A DM tagging one NPC twelve ways is their own concern, not a correctness problem. The planner may revisit if research surfaces a reason.

- **Which library exactly.** Tagify is the assumed choice under D-15; the planner may substitute an equivalent that meets the same constraints (CDN + SRI, thin init module, works on both platforms, binds to a real input for D-16's fallback).

- Join-table naming and whether it is an explicit entity or a skip-navigation.
- Whether to mint a `CONTACTTAG-*` requirement family into REQUIREMENTS.md as plan 01, the way Phase 82 minted `EVTAGENDA-*`.
- CSS class naming for chips, the filter row, and the offcanvas, following existing `contacts.css` / `contacts.mobile.css` and `shop` conventions.
- Exact wording of the disabled-filter hint text (D-19) and the no-results message (D-21).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase definition
- `.planning/ROADMAP.md` § "Phase 81: Contact Tags and Filtering" — goal, origin (the same operator-relayed board-user request as Phase 80), scope notes, and the two questions this discussion answered (AND vs OR → D-08; who may create tags → D-01/D-02).
- `.planning/ROADMAP.md` § "Phase 80: Contact Categories" — the dependency's scope notes, including the rule that tags must **not** be modelled as a second category column and that filter state belongs in the query string.
- `.planning/REQUIREMENTS.md` — contains no `CONTACTTAG-*` requirements. The decisions in this file are the requirement source for Phase 81.

### Prior decisions that bind this phase
- `.planning/phases/80-contact-categories/80-CONTEXT.md` — the whole file. Specifically: **D-02** (fail-closed `HasQueryFilter`, inline dereference) → D-05; **D-04** (case-insensitive unique index) → D-04; **D-05** (`DungeonMasterOnly` gate) → D-02; **D-07/D-10** (the deliberate discoverability asymmetry) → D-19; **D-08** (both platforms, real-UA verification) → D-22; **D-13** (`IsVisibleTo` runs in memory, group after it, empty headings suppressed) → D-10/D-11; **D-15** (plain escaped text, no Markdown, length cap) → D-18; **D-16** (category on both Details views) → D-17; **D-17–D-20** (the test-coverage pattern) → D-23–D-30; and the **Claude's-Discretion note** rejecting a shared `_ContactList` partial — the two index views keep their own markup.
- `.planning/phases/78-link-preview-foundation-and-quest-cards/78-CONTEXT.md` § D-12 — `IgnoreQueryFilters()` is forbidden on tenant-scoped reads; the fail-closed filter is the remedy for the Phase 49/55 leaks.
- `.planning/phases/78-link-preview-foundation-and-quest-cards/78-CONTEXT.md` § D-10 — why platform-branching inside one shared markup surface was rejected.
- `.planning/PROJECT.md` — the recorded drift bugs (`Characters/Edit.cshtml` `classIndex`, mobile markup that never renders) that D-22 guards against.

### Codebase precedents this phase copies
- `QuestBoard.Service/Controllers/Shop/ShopController.cs` § `Index` — the `IList<ItemRarity>? rarity` binding is the exact model for D-09's repeated-tag-id query string, alongside `sort`, `search`, and `page`.
- `QuestBoard.Service/Views/Shop/Index.cshtml` — the desktop `shop-filter-row` GET form, rarity checkboxes, Apply / Clear buttons, `HasActiveFilters`, and the two-branch empty state. The model for D-20 and D-21.
- `QuestBoard.Service/Views/Shop/Index.Mobile.cshtml` — the `shopFilterOffcanvas` bottom drawer behind a Filter button, with an active-filter badge. The model for D-20's mobile half.
- `QuestBoard.Service/wwwroot/js/image-crop.js` and its call site at `Views/Contacts/Create.cshtml:125-133` — the CDN-plus-thin-init-module shape D-15 copies, including `integrity` / `crossorigin` on the CDN script and `asp-append-version="true"` on the local module.
- `QuestBoard.Service/wwwroot/js/markdown-editor.js` — the other bespoke form-field module already loaded on these views; its header comment documents the no-module, no-bundler convention that D-15 must follow.
- `QuestBoard.Service/Controllers/AgendaController.cs` § `Index` — the intersect-with-what-the-viewer-may-see line and its comment ("the filter cannot widen the set... by construction rather than by convention") is the reasoning D-10 applies. Its comma-joined querystring shape was **rejected** for this phase; read it to understand why.

### Codebase conventions
- `CLAUDE.md` § "UI/UX Design Guidelines" — `modern-card` / `modern-card-header` / `modern-card-body`, `<hr>` before the button section, `d-flex justify-content-between` button layout.
- `CLAUDE.md` § "Code Comments" — no phase or requirement IDs (`D-01`, `Phase 81`) in source comments, XML docs, or string literals.
- `CLAUDE.md` § "Entity Framework" — EF packages belong only in `QuestBoard.Repository`.
- `.planning/codebase/CONVENTIONS.md` — naming and AutoMapper patterns for the two mapping boundaries the new entity crosses.
- `.planning/codebase/ARCHITECTURE.md` — Service → Domain → Repository dependency direction.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContactsController.IsVisibleTo` (`ContactsController.cs:469`) — the single visibility predicate covering `IsRevealed`, the creator exemption, and the Show Hidden toggle. Reuse unchanged; filter *after* it (D-10).
- `ContactsController.IsDmTierAsync` / `ReadShowHiddenToggle` — already drive `ViewerIsDmTier` and `ShowHidden` on the index ViewModel. `ViewerIsDmTier` is the flag D-01's entire audience gate hangs on; it already exists and is already on the ViewModel.
- `ShopIndexViewModel` — carries `SelectedRarities`, `SelectedSort`, `SearchQuery`, and `HasActiveFilters`. The shape `ContactsIndexViewModel` should grow for `SelectedTagIds` / `AvailableTags` / `HasActiveFilters`.
- `wwwroot/js/image-crop.js` — the `initImageCrop({...})` explicit-init convention; `markdown-editor.js` uses self-init on `DOMContentLoaded`. Either is house style; pick one and document it.
- `wwwroot/css/contacts.css` and `contacts.mobile.css` — chip and filter styles belong here, not inline, and this is also where the library's default styling gets overridden.

### Established Patterns
- **Fail-closed group filter** (`QuestBoardContext.cs:326–345`, Contacts at line 405): every group-scoped entity gets a `HasQueryFilter` returning zero rows when `ActiveGroupId` is null. The comment at line 333 warns against capturing `ActiveGroupId` into a local — the new filter must dereference the service inline.
- **`ContactEntity` deliberately has no SuperAdmin cross-group view** (comment at `QuestBoardContext.cs:402`). `ContactTag` follows the same rule.
- **Two index views, no shared markup**: `Views/Contacts/Index.cshtml` renders a `contact-grid` of `contact-card`s; `Index.Mobile.cshtml` renders `contact-member-row`s inside a `contact-section-card`. Deliberately different, not drift — Phase 80 rejected a shared `_ContactList` partial.
- **Query-string filters are already a solved problem here** — `ShopController.Index` binds `ItemType?`, `IList<ItemRarity>?`, `string? sort`, `string? search`, and `int page` straight from the query string with no manual parsing. There is no `[FromQuery]` anywhere in this codebase; plain parameter binding is the convention.
- **CDN scripts are pinned with `integrity` + `crossorigin`** (cropperjs at `Create.cshtml:127-129`); local modules use `asp-append-version="true"`. There is no `wwwroot/lib/` — nothing is self-hosted today.
- **Migrations auto-apply on startup** via `context.Database.Migrate()`.

### Integration Points
- `QuestBoard.Repository/Entities/` — new `ContactTagEntity`, the join to `ContactEntity`.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — new `DbSet`, new fail-closed `HasQueryFilter` (D-05), case-insensitive unique index on `(GroupId, Name)` (D-04), join configuration.
- `QuestBoard.Domain/Models/Contact.cs` — tags on the domain model; new `ContactTag` domain model, `IContactTagService` / repository interface.
- `QuestBoard.Repository/Automapper/EntityProfile.cs` and `QuestBoard.Service/Automapper/ViewModelProfile.cs` — both mapping boundaries.
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` — `Index` gains the tag parameter, applies the filter after `IsVisibleTo`, and derives the vocabulary (D-12); `Create`/`Edit` GET supply existing tags for suggestions and POST upsert the submitted names; `Details` exposes them; **`ToggleShowHidden` must carry the filter through its redirect (D-13)**.
- `QuestBoard.Service/ViewModels/ContactViewModels/` — `ContactsIndexViewModel` gains selected tags, available tags, and `HasActiveFilters`; `ContactViewModel` gains the tag list.
- Views touched: `Contacts/Index.cshtml`, `Index.Mobile.cshtml`, `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`.
- New assets: `wwwroot/js/contact-tags.js` (init module), chip and filter styles in `contacts.css` / `contacts.mobile.css`.
- Tests: `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`, `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs`, `QuestBoard.UnitTests/Services/ContactServiceTests.cs`.

</code_context>

<specifics>
## Specific Ideas

- The requester's words, relayed by the operator: *"Misschien later nog een filter optie, dat ik tags kan maken op bv shopkeeper en dat er dan gefilterd kan worden erop."* — note the passive "er kan gefilterd worden": it does not name who filters, which is what left D-01's audience question genuinely open.
- Example tag names from the ROADMAP: "shopkeeper", "quest giver". Both are *role* metadata rather than *place* metadata — part of why tags read as DM bookkeeping and categories read as player-facing structure.
- The operator overrode a recommendation on D-14, choosing the chips widget over a no-JS checkbox list. That override proved to be better-founded than the recommendation: the scout had missed that `markdown-editor.js` and `image-crop.js` are already bespoke form-field modules on these exact four views. Do not re-litigate this in planning.
- On tag name and count limits the operator said "you decide" — recorded as discretion, locked with rationale, left open to the planner.

</specifics>

<deferred>
## Deferred Ideas

- **Opening tags to players** — flip the `ViewerIsDmTier` conditional from D-01. Deliberately the reversible direction; the strongest candidate for a follow-up if players ask for the filter.
- **Renaming and merging tags, and a Manage Tags page** — the surface D-07 cut. Would restore the rename path and give a single place to see the board's vocabulary. Mirrors Phase 80's Manage Categories, so it is copy-shaped work when wanted.
- **Clickable tag chips as a filter shortcut** — click a chip on a card to filter by that tag. Composes cleanly with D-09 (a chip click is just a URL with one tag id) and the chips already exist per D-17. Cut only to avoid two entry points into the same filter state in one phase.
- **An AND / "match all" toggle** — D-08 chose OR. An AND mode would answer "which shopkeeper also gives quests?", at the cost of a third piece of filter state on two platforms.
- **A "some hidden contacts match — turn on Show Hidden" nudge on the empty state** — genuinely useful and safe given the DM-only audience, but needs a second evaluation of the filter against the pre-visibility set: a new query path and a new test for a narrow moment.
- **Bulk tagging** — tick several contacts and tag them in one action. Inherits Phase 80's deferred bulk-assignment idea; both would be served by the same surface.
- **Free-text search over contacts** — the Shop has `search` alongside its filters. Never requested for Contacts; explicitly out of scope here.
- **Tags on Characters or Quests** — Contacts only, per the ROADMAP.

</deferred>

---

*Phase: 81-contact-tags-and-filtering*
*Context gathered: 2026-08-30*
