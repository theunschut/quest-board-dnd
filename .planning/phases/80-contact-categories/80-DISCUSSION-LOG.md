# Phase 80: Contact Categories - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-27
**Phase:** 80-contact-categories
**Areas discussed:** Data model & cardinality, Who manages categories, Uncategorised home & ordering, Headings vs the visibility gates, Empty state, Name text handling, Test coverage

---

## Area selection

| Option | Description | Selected |
|--------|-------------|----------|
| Data model & cardinality | One category per contact or several; lookup table or free-text column | ✓ |
| Who manages categories | DM-tier vs Admin-only; dedicated page vs inline creation | ✓ |
| Uncategorised home & ordering | Where uncategorised contacts go; heading order | ✓ |
| Headings vs the visibility gates | Empty headings, counts, interaction with IsRevealed / Show Hidden | ✓ |

**User's choice:** all four areas.

---

## Data model & cardinality

### Cardinality

| Option | Description | Selected |
|--------|-------------|----------|
| Exactly one | Nullable FK; matches the requester's "kopjes" framing; every contact renders once | ✓ |
| Several | Join table; contact renders under multiple headings | |

**Notes:** Multi-category was rejected as structurally identical to Phase 81's tags, which the ROADMAP forbids modelling as a second category column.

### Storage

| Option | Description | Selected |
|--------|-------------|----------|
| ContactCategory table | Per-group entity (Id, Name, GroupId, SortOrder) + nullable FK on ContactEntity | ✓ |
| Free-text column | Nullable CategoryName string; group by distinct value | |

**Notes:** Free-text leaves rename as a bulk update, allows two spellings to render as two headings, and has nowhere to hang a sort order.

### Delete behaviour

| Option | Description | Selected |
|--------|-------------|----------|
| Orphan the contacts | SetNull; contacts fall to Ungrouped; confirm names the count | ✓ |
| Block while non-empty | Refuse delete until reassigned | |
| Reassign on delete | Delete form makes DM pick a destination | |

**Notes:** Blocking is tedious without bulk-assign; reassign-on-delete is a whole extra flow for a rare operation.

### Name uniqueness

| Option | Description | Selected |
|--------|-------------|----------|
| Unique per group, case-insensitive | Index on (GroupId, Name) + form validation | ✓ |
| No constraint | Trust the DM | |

**Notes:** Duplicate headings fail silently and recur; validation must surface as a message, not a raw DB exception.

---

## Who manages categories

### Authorization

| Option | Description | Selected |
|--------|-------------|----------|
| Any DM-tier user | DungeonMasterOnly — the gate Contacts Create/Edit/Delete already carry | ✓ |
| Admins only | AdminOnly | |

**Notes:** Admin-only would let a DM create an NPC but not the heading to file it under — an asymmetry the codebase has nowhere else.

### Management UI location

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated management page | Reached from the Contacts index; hosts rename/delete/reorder | ✓ |
| Inline on the contact form only | "+ New category…" in the dropdown | |
| Both | Dedicated page plus inline create | |

**Notes:** Inline-only leaves no way to rename or delete and nowhere to put a sort order. "Both" was judged too large a surface for the value.

### Assignment mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Dropdown on Create/Edit contact | Single select added to the four contact form views | ✓ |
| Dropdown plus bulk-assign | Also tick-and-file several contacts from the management page | |
| Drag-and-drop on the index | Drag a card onto a heading | |

**Notes:** Bulk-assign's value for first-time categorisation was acknowledged and deferred rather than dismissed. Drag-and-drop has no JS precedent here and does not translate to the mobile row layout.

### Platform parity for the management page

| Option | Description | Selected |
|--------|-------------|----------|
| Desktop and mobile | Manage.cshtml + Manage.Mobile.cshtml, verified with a real mobile User-Agent | ✓ |
| Desktop only for now | Ship one view; mobile gets the desktop layout | |

**Notes:** Holds the both-platforms rule established in Phase 72 and carried through 74 and 78.

---

## Uncategorised home & ordering

### Where uncategorised contacts go

| Option | Description | Selected |
|--------|-------------|----------|
| "Ungrouped" heading, last | Synthetic heading after all real categories, only when non-empty | ✓ |
| Flat remainder block, no heading | Plain list above or below the categories | |
| Uncategorised block first | Same heading, placed above the named categories | |

**Notes:** Placing leftovers first would bury the deliberately-organised part of the list under them — backwards for players, who are the majority of readers.

### Heading order

| Option | Description | Selected |
|--------|-------------|----------|
| Manual sort order set by the DM | SortOrder int, up/down buttons on the management page | ✓ |
| Alphabetical | Order by Name | |
| Creation order | Order by Id | |

**Notes:** "Last Bastion" filed under L was the concrete case. Alphabetical leaves numeric name prefixes as the only workaround, and those show on the index.

### Order within a category

| Option | Description | Selected |
|--------|-------------|----------|
| Alphabetical by name | Unchanged from today's flat list | ✓ |
| Manual order within the category too | Per-contact sort position | |

**Notes:** Grouping changes where a contact sits, not how it sorts within its group.

### Zero-category boards

| Option | Description | Selected |
|--------|-------------|----------|
| Exactly today's flat list | Suppress all headings until a category exists | ✓ |
| A single "Ungrouped" heading | Always render headings | |

**Notes:** Boards that ignore the feature must see no change.

---

## Headings vs the visibility gates

### Empty headings

| Option | Description | Selected |
|--------|-------------|----------|
| Suppress empty headings | Heading renders only if a contact under it survives IsVisibleTo | ✓ |
| Always show all categories | Consistent page shape for everyone | |

**Notes:** The ROADMAP's explicit rule. A category name is itself a campaign spoiler. Grouping must happen after the in-memory IsVisibleTo filter, never before.

### Counts on headings

| Option | Description | Selected |
|--------|-------------|----------|
| No count | Name alone | ✓ |
| Viewer-scoped count | Number of contacts this viewer can see | |

**Notes:** A true count leaks hidden NPCs; a viewer-scoped count is redundant with the cards below and changes when a DM flips Show Hidden.

### How grouping reaches the two views

| Option | Description | Selected |
|--------|-------------|----------|
| Nested groups on the ViewModel | IList<ContactCategoryGroupViewModel> { Title, Contacts }, mirroring ShopCategoryViewModel | ✓ (Claude's call) |
| Flat list plus a category name per contact | Each view groups in Razor | |
| Shared Razor partial for the whole list | One _ContactList partial both views render | |

**User's choice:** free text — *"I really don't know? whatever you think is best! or let the planner/researcher work this out?"*
**Notes:** Recorded as Claude's discretion. Decided in favour of nested groups so the empty-heading suppression rule lives in one place; the shared partial was rejected because the two layouts are genuinely different markup and the partial would need platform branching inside it. Left open to the planner if research surfaces a reason.

### Category on the Details page

| Option | Description | Selected |
|--------|-------------|----------|
| Show it on Details too | Muted line near TownCity/SubLocation on both Details views | ✓ |
| Index only | Details unchanged | |

---

## Additional areas (second round)

### Contact form dropdown when no categories exist

| Option | Description | Selected |
|--------|-------------|----------|
| Hide the field entirely | No categories → no dropdown | |
| Show it disabled with a hint | Greyed select plus helper text linking to Manage Categories | ✓ |
| Show it enabled with only "— None —" | Always render the select | |

**Notes:** The operator chose discoverability here while keeping the *index* free of headings until a category exists. The asymmetry is deliberate — the form is DM-only, the index is read by players.

### Category name text handling

| Option | Description | Selected |
|--------|-------------|----------|
| Plain text, Razor-escaped | Length-capped label; no Markdown pipeline | ✓ |
| Markdown-rendered | Run names through the Phase 66–71 pipeline | |

**Notes:** Markdown belongs on long-form fields here; a Markdown heading could contain a link or an image.

### Required test coverage (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| Cross-group category isolation | Two-group integration test on index, dropdown, and assignment POST | ✓ |
| Empty-heading suppression | Both directions — player sees nothing, DM with Show Hidden sees it | ✓ |
| Ordering and Ungrouped placement | SortOrder, alphabetical within, Ungrouped last, zero-category fallback | ✓ |
| Delete orphans rather than cascades | Asserted against the database, not the UI | ✓ |

**User's choice:** all four.

---

## Claude's Discretion

- Grouping shape on the ViewModel — decided as nested groups with rationale; open to the planner.
- Wording of the delete-confirmation and the disabled-dropdown hint.
- Whether SortOrder is dense or sparse, and how ties break.
- CSS class naming for headings, following existing `contacts.css` conventions.

## Deferred Ideas

- Bulk assignment from the management page — strongest follow-up candidate.
- Drag-and-drop refiling on the index.
- Per-contact manual ordering within a category.
- Category icons, colours, or descriptions.
- Collapsible category sections on the index.
- Tags and filtering — already scoped as Phase 81.
