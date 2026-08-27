# Phase 80: Contact Categories - Context

**Gathered:** 2026-08-27
**Status:** Ready for planning

<domain>
## Phase Boundary

A DM can create named, per-group categories — "Corridor", "Guild Members", "Last Bastion" — assign the board's NPCs to them, and the Contacts index renders contacts under those headings instead of one flat alphabetical list, on both desktop and mobile.

The phase delivers: a `ContactCategory` entity scoped to a group, a DM-tier management page (desktop + mobile) to create/rename/delete/reorder categories, a category dropdown on the four Contacts Create/Edit views, grouped rendering on both Contacts index views, and the category shown on both Contacts Details views.

Out of scope: tags, filtering, and search (Phase 81); bulk assignment of contacts to a category; any change to Characters, Quests, or the Shop; per-contact ordering within a category; category icons, colours, or descriptions.

**Note:** ROADMAP.md lists `Requirements: TBD` for this phase — no `CONTACTCAT-*` IDs exist in REQUIREMENTS.md yet. The decisions below are the requirement source for planning.

</domain>

<decisions>
## Implementation Decisions

### Data model and cardinality

- **D-01: A contact belongs to exactly one category, or to none.** A nullable FK on `ContactEntity`, not a join table. This settles the ROADMAP's stated open question in favour of how the requester described the feature — "kopjes" (headings) that partition a long list, so every contact renders exactly once on the index.

  Rejected: multi-category membership. Structurally it is the same many-to-many shape as Phase 81's tags, and the ROADMAP explicitly forbids modelling tags as a second category column — building both would mean two near-identical many-to-many features on the same entity.

- **D-02: A real per-group `ContactCategory` entity — `Id`, `Name`, `GroupId`, `SortOrder` — not a free-text column on `ContactEntity`.** Renaming a category updates every contact at once because the name lives in exactly one place, and `SortOrder` (D-08) has nowhere to live without a table.

  **The new entity needs its own `HasQueryFilter` in `QuestBoardContext.cs`, in the same fail-closed shape as `ContactEntity` at line 405** — `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`. A null active group must return zero categories, never every board's categories merged. The lambda must dereference `activeGroupContext` inline; capturing `ActiveGroupId` into a local reads it once at model-build time (null) and breaks the filter — the file's own comment warns about exactly this.

  `ContactEntity` gains a nullable `CategoryId` + navigation. Category is *not* an enum: unlike `ItemType`, the vocabulary is board-authored.

- **D-03: Deleting a non-empty category orphans its contacts rather than blocking or cascading.** `OnDelete(DeleteBehavior.SetNull)` on the FK — every contact drops to `CategoryId = null` and falls into the Ungrouped heading (D-07). The delete confirmation names the count: "This will move 7 contacts to Ungrouped."

  **A mis-set delete behaviour here silently deletes NPCs**, which is why D-15 requires a database-level assertion rather than a UI check.

  Rejected: blocking while non-empty — with no bulk-assign in this phase (D-06), emptying a category means editing every contact individually. Rejected: a reassign-on-delete form — a whole extra flow for a rare operation.

- **D-04: Category names are unique per group, case-insensitive.** A unique index on `(GroupId, Name)` plus form validation, so a board cannot end up with "Guild Members" twice or with "guild members" rendering beside it as a separate heading. The user must see a validation message, not a raw DB exception.

### Who manages categories and how contacts are assigned

- **D-05: `[Authorize(Policy = "DungeonMasterOnly")]` on every category write.** The exact gate `ContactsController`'s Create/Edit/Delete already carry. Anyone who can create an NPC can organise NPCs; no new authorization tier. Players see the headings on the index but cannot change them.

- **D-06: A dedicated "Manage Categories" page, reached from a DM-tier button on the Contacts index — not inline-only creation.** Lists the board's categories with rename, delete, reorder, and an add form. Mirrors how `ShopManagement` sits alongside `Shop`. It is also the only surface where `SortOrder` (D-08) can be edited.

  Assignment itself is **a single `<select>` on the contact form**, added to `Views/Contacts/Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, and `Edit.Mobile.cshtml`, with a blank "— None —" first option. Uses the existing form POST and validation; nothing new to wire.

  **Accepted cost: no bulk assignment in this phase.** Categorising an existing board of ~30 NPCs for the first time is 30 edit-save round trips. Deferred, not forgotten — see Deferred Ideas.

  Rejected: drag-and-drop on the index — new JS with no precedent here, no translation to the mobile stacked-row layout, and a new AJAX endpoint with antiforgery handling.

- **D-07 (assignment empty state): When a board has no categories yet, the contact form renders the category select **disabled, with helper text linking to Manage Categories**.** The operator chose discoverability over hiding the field: a DM learns the feature exists from the form they already use every time they add an NPC, rather than having to find the management page first.

  **Note the deliberate asymmetry with D-10:** the *index* hides all trace of categories until one exists, because players read that page and a lone empty heading over the whole address book is noise. The *contact form* is DM-only, so a disabled hint there costs nothing and does the discovery work.

- **D-08 (management page platform parity): The management page ships desktop and mobile — `Manage.cshtml` and `Manage.Mobile.cshtml`.** The both-platforms-in-one-phase rule from Phase 72, held through 74 and 78. `MobileDetectionMiddleware` picks the layout from the User-Agent, so a desktop-only page means a DM at the table gets an unstyled page from a button the mobile index renders.

  **Mobile markup must be verified with a real mobile User-Agent, not devtools emulation** (Phase 74 D-16). The "mobile markup that was never selected" failure is already on PROJECT.md's record.

  Reordering uses up/down buttons, not drag — no new JS library, and it works identically on both platforms.

### Uncategorised contacts and ordering

- **D-09: Uncategorised contacts render under a synthetic "Ungrouped" heading, pinned last, and only when it holds at least one contact visible to this viewer.** This settles the ROADMAP's open question. Every contact lives under exactly one heading, so the index has one consistent shape, and a DM can see at a glance what still needs filing.

  "Ungrouped" is synthetic — not a row in `ContactCategory`, not renameable, and not orderable. It always sorts after every real category regardless of `SortOrder`.

- **D-10: A board with zero categories renders exactly the flat list it renders today — no headings at all, not even "Ungrouped".** One conditional in each index view. Boards that never adopt the feature see no change, and no board gains a meaningless heading labelling its entire contact list. A DM's first category is what switches headings on.

- **D-11: Category headings are ordered by a DM-set `SortOrder`, edited on the management page.** A world's important places are not alphabetical — a DM will want "Last Bastion" first, not filed under L. Alphabetical ordering leaves prefixing names with numbers as the only workaround, and those numbers then show on the index.

- **D-12: Contacts within a category stay alphabetical by name — unchanged from today.** The index is already a flat alphabetical list and `ContactsIndexViewModel`'s own comment says so. Grouping changes where a contact sits, not how it sorts within its group. No per-contact sort position.

### Headings, the visibility gates, and rendering

- **D-13: A heading renders only when at least one contact under it survives `IsVisibleTo` for this viewer. Empty headings are suppressed.**

  This is the sharpest rule in the phase. The ROADMAP is explicit that a heading must never disclose the existence of a contact the viewer cannot see — "Corridor" appearing to a player is itself a campaign spoiler even with nothing under it.

  **`IsVisibleTo` (`ContactsController.cs:469`) runs in memory, after `contactService.GetAllContactsWithDetailsAsync`, and covers both gates — `IsRevealed` and the per-group Show Hidden session toggle.** Grouping therefore happens *after* that filter, never before, and never in the query. A DM flipping Show Hidden on makes previously-suppressed headings appear; that is correct, not a leak.

- **D-14: Headings carry the category name alone — no contact count.** A true count leaks how many hidden NPCs a category holds; a viewer-scoped count is redundant with the cards rendered directly beneath it and would visibly change when a DM flips Show Hidden, which reads as a bug. The ROADMAP names a count-bearing heading as one of the disclosure risks to avoid.

- **D-15: Category names render as plain Razor-escaped text — never through `IMarkdownService`.** A category name is a label, not content; `Contact.Name` and `TownCity` are already handled this way on the same views. Length-capped (~60 chars) so a long name cannot break the heading layout on mobile. Markdown belongs on long-form fields in this codebase (the Phase 66–71 rollout), and a Markdown heading could contain a link or an image.

- **D-16: The category is also shown on both Contacts Details views** — a muted line near `TownCity` / `SubLocation` on `Details.cshtml` and `Details.Mobile.cshtml`. The field is already on the mapped `ContactViewModel`; no new query, no new gate. Without it, a player landing on an NPC directly has no way to learn its category short of going back and scanning the list.

### Test coverage this phase must deliver

- **D-17: Cross-group category isolation, proved by a two-group integration test.** Group A's categories never appear on group B's index or in group B's assignment dropdown, and a POST assigning a contact to a category id owned by another group is refused rather than silently accepted. This app has shipped two real cross-tenant leaks (Phases 49/55), and the ROADMAP calls a category name itself campaign-revealing. `IgnoreQueryFilters()` is forbidden on every path in this phase.

- **D-18: Empty-heading suppression, both directions.** A category whose contacts are all unrevealed renders no heading for a player; the same heading *does* appear for a DM with Show Hidden on. This is the rule most likely to regress the next time either index view is touched.

- **D-19: Ordering and Ungrouped placement pinned by test.** Categories render in `SortOrder`, contacts sort alphabetically within, Ungrouped is last, and a board with zero categories renders the flat list unchanged (D-10).

- **D-20: Delete orphans rather than cascades — asserted against the database, not inferred from the UI.** Guards against a mis-set `OnDelete` silently deleting NPCs.

### Claude's Discretion

- **Grouping shape on the ViewModel.** The operator declined to choose and handed this to the planner. **Locked as: nested groups — `ContactsIndexViewModel` carries `IList<ContactCategoryGroupViewModel> { Title, IList<ContactViewModel> Contacts }`,** the exact shape `ViewModels/ShopViewModels/ShopCategoryViewModel.cs` already uses. The controller filters by `IsVisibleTo`, then groups, then drops empty groups — so D-13's suppression rule lives in one place instead of being written twice, once per view.

  Each index view keeps its own markup (desktop card grid vs mobile stacked rows) and only loops the headings. **A single shared `_ContactList` partial was rejected**: the two layouts are genuinely different markup, so the partial would need platform branching inside it — reintroducing exactly the coupling Phase 78 D-10 removed.

  The planner may revisit this if research surfaces a reason, but the two-views-duplicate-the-suppression-rule alternative is the drift class PROJECT.md blames for the `Characters/Edit.cshtml` `classIndex` bug and should not be chosen without one.

- Exact wording of the delete-confirmation and the disabled-dropdown hint text.
- Whether `SortOrder` is dense (0,1,2…) or sparse (10,20,30…) and how ties break.
- CSS class naming for the heading, following the existing `contacts.css` / `contacts.mobile.css` conventions.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase definition
- `.planning/ROADMAP.md` § "Phase 80: Contact Categories" — goal, origin (the operator-relayed board-user request of 2026-08-27), scope notes, and the two questions this discussion answered (cardinality → D-01, who manages → D-05; uncategorised home → D-09).
- `.planning/ROADMAP.md` § "Phase 81: Contact Tags and Filtering" — the next phase's constraints. Read it to avoid foreclosing them: tags are many-to-many and must **not** be modelled as a second category column, and tag filter state belongs in the query string.
- `.planning/REQUIREMENTS.md` — contains no `CONTACTCAT-*` requirements. The decisions in this file are the requirement source for Phase 80.

### Prior decisions that bind this phase
- `.planning/phases/78-link-preview-foundation-and-quest-cards/78-CONTEXT.md` § D-12 — `IgnoreQueryFilters()` is forbidden on tenant-scoped reads; the fail-closed filter is the remedy for the Phase 49/55 leaks.
- `.planning/phases/78-link-preview-foundation-and-quest-cards/78-CONTEXT.md` § D-10 — why platform-branching inside one shared markup surface was rejected; the basis for the Claude's-Discretion note above.
- `.planning/PROJECT.md` — the recorded drift bugs (`Characters/Edit.cshtml` `classIndex`, mobile markup that never renders) that D-08 and the ViewModel shape are guarding against.

### Codebase conventions
- `CLAUDE.md` § "UI/UX Design Guidelines" — `modern-card` / `modern-card-header` / `modern-card-body` are mandatory on the new management views, with `<hr>` before the button section and `d-flex justify-content-between` button layout.
- `CLAUDE.md` § "Code Comments" — no phase or requirement IDs (`D-01`, `Phase 80`) in source comments, XML docs, or string literals.
- `.planning/codebase/CONVENTIONS.md` — naming and AutoMapper patterns for the two mapping boundaries the new entity crosses.
- `.planning/codebase/ARCHITECTURE.md` — Service → Domain → Repository dependency direction; EF packages belong only in `QuestBoard.Repository`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/ViewModels/ShopViewModels/ShopCategoryViewModel.cs` — `{ Title, Items }`. The precedent for the grouped ViewModel shape, though it is backed by the fixed `ItemType` enum rather than user-created rows.
- `ContactsController.IsVisibleTo` (`ContactsController.cs:469`) — the single existing visibility predicate covering `IsRevealed`, the creator exemption, and the Show Hidden toggle. Reuse it unchanged; group *after* it.
- `ContactsController.IsDmTierAsync` / `ReadShowHiddenToggle` — already drive `ViewerIsDmTier` and `ShowHidden` on the index ViewModel; the management-page button reuses `ViewerIsDmTier`.
- `wwwroot/css/contacts.css` and `contacts.mobile.css` — the heading styles belong here, not inline.

### Established Patterns
- **Fail-closed group filter** (`QuestBoardContext.cs:326–345`, Contacts at line 405): every group-scoped entity gets a `HasQueryFilter` that returns zero rows when `ActiveGroupId` is null. The comment at line 333 warns against capturing `ActiveGroupId` into a local — the new filter must dereference the service inline.
- **`ContactEntity` deliberately has no SuperAdmin cross-group view** (comment at `QuestBoardContext.cs:402`) — it is a per-group roster like `CharacterEntity`. `ContactCategory` follows the same rule.
- **Two index views, no shared markup**: `Views/Contacts/Index.cshtml` renders a `contact-grid` of `contact-card`s; `Index.Mobile.cshtml` renders `contact-member-row`s inside a `contact-section-card`. Deliberately different, not drift.
- **Migrations auto-apply on startup** via `context.Database.Migrate()` — no manual `database update` step in dev.
- **`ContactsIndexViewModel`** currently carries a flat `IList<ContactViewModel>` with a comment stating Contacts have no owner concept and so no My/Other split. That comment becomes stale once grouping lands and should be updated, not left.

### Integration Points
- `QuestBoard.Repository/Entities/ContactEntity.cs` — nullable `CategoryId` + navigation; new `ContactCategoryEntity`.
- `QuestBoard.Domain/Models/Contact.cs` — nullable category on the domain model; new `ContactCategory` domain model, `IContactCategoryService` / `IContactCategoryRepository`.
- `QuestBoard.Repository/Automapper/EntityProfile.cs` and `QuestBoard.Service/Automapper/ViewModelProfile.cs` — both mapping boundaries.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — new `DbSet`, new `HasQueryFilter`, `OnDelete(SetNull)`, unique index on `(GroupId, Name)`.
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` — `Index` groups after `IsVisibleTo`; `Create`/`Edit` GET populate the dropdown, POST persist `CategoryId`; `Details` exposes the category name.
- Views touched: `Contacts/Index.cshtml`, `Index.Mobile.cshtml`, `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`, plus new `Manage.cshtml` / `Manage.Mobile.cshtml`.
- Tests: `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`, `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs`, `QuestBoard.UnitTests/Services/ContactServiceTests.cs`.

</code_context>

<specifics>
## Specific Ideas

- The requester's own words, relayed by the operator on 2026-08-27: *"Misschien leuk … om NPC's in categorieën te kunnen onderverdelen? Dat ik verschillende kopjes/categorieën kan maken om de boel wat overzichtelijk te houden."* — "kopjes" (headings) is the mental model: a long flat list broken into named sections, not a taxonomy system.
- Example category names given in the ROADMAP: "Corridor", "Guild Members", "Last Bastion". Note that "Last Bastion" is a place name that alphabetises badly — the direct motivation for D-11's manual ordering.
- The operator explicitly chose the discoverable disabled-dropdown hint (D-07) over hiding the field, while keeping the index invisible until a category exists (D-10). The asymmetry is deliberate: the form is DM-only, the index is read by players.
- On the ViewModel grouping shape the operator said, in substance, "whatever you think is best — or let the planner work this out." Recorded as discretion, decided with rationale, and left open to the planner.

</specifics>

<deferred>
## Deferred Ideas

- **Bulk assignment** — tick several contacts on the management page and file them under a category in one action. Real value for the first-time categorisation of an existing board (~30 NPCs today is 30 edit-save round trips). Deliberately cut from Phase 80 to keep the assignment path to the existing form POST. Strongest candidate for a follow-up phase.
- **Drag-and-drop refiling on the index** — considered and rejected for this phase: new JS with no precedent, no translation to the mobile stacked-row layout, and a new AJAX endpoint with antiforgery handling.
- **Per-contact manual ordering within a category** — pinning a guild leader above the rank and file. Rejected for D-12; would need a second reorder UI on a surface that has none.
- **Category icons, colours, or descriptions** — never raised as a need; the heading is a label.
- **Collapsible category sections on the index** — a natural follow-on once a board has many categories, but pure UI polish and not requested.
- **Tags and filtering** — Phase 81, already scoped in ROADMAP.md. Nothing in Phase 80 may model tags as a second category column.

</deferred>

---

*Phase: 80-contact-categories*
*Context gathered: 2026-08-27*
