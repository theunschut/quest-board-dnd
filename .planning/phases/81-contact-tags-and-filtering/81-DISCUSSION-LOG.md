# Phase 81: Contact Tags and Filtering - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-30
**Phase:** 81-contact-tags-and-filtering
**Areas discussed:** Filter semantics & state, Tag authoring & permissions, Tag entry & display, Filter UI surface

---

## Filter semantics & state

### Multi-tag selection: ANY or ALL?

| Option | Description | Selected |
|--------|-------------|----------|
| OR — any selected tag *(recommended)* | Merges the groups. Matches ShopController's rarity checkboxes, so one filter idiom in the codebase. Ticking more boxes widens the result. | ✓ |
| AND — must carry every selected tag | Shows the intersection. Better for "which shopkeeper also gives quests?", but ticking more boxes narrowing the result reads as broken, and with a small vocabulary the intersection is often empty. | |
| OR by default, with an AND toggle | Most expressive; a third piece of filter state to carry, render on two platforms, and test. | |

**User's choice:** OR — any selected tag
**Notes:** Settles the ROADMAP's explicitly deferred question. With one tag ticked the two semantics are indistinguishable, so this only diverges in the multi-tick case.

### What does the query string carry — ids or names?

| Option | Description | Selected |
|--------|-------------|----------|
| Repeated tag ids — `?tag=3&tag=7` *(recommended)* | ShopController's `IList<ItemRarity>` binding exactly; no manual parsing. Foreign/deleted ids match nothing — fail-closed. URL is opaque. | ✓ |
| Repeated tag names | Readable and hand-editable, but names are renameable and per-group, so bookmarks break and every request needs case-insensitive lookup. | |
| Comma-joined ids — `?tags=3,7` | AgendaController's Phase 82 shape. It needed raw `Request.Query` reading only to express "none selected" — a problem this phase doesn't have. | |

**User's choice:** Repeated tag ids
**Notes:** The ROADMAP had already ruled out session state; this settled the shape within the query string.

### What happens to Phase 80's category headings under an active filter?

| Option | Description | Selected |
|--------|-------------|----------|
| Headings stay, empty ones drop out *(recommended)* | Filter narrows, then Phase 80 D-13's suppression runs unchanged. Shows which categories the matches live in. One rendering mode. | ✓ |
| Filtering flattens the list | A filtered view reads as a search result. Simpler visually, but two rendering modes per view and D-13 gets bypassed rather than reused. | |
| Headings stay, empty categories still show | Named only to rule out — Phase 80 D-13 makes empty-heading suppression the sharpest rule in that phase. | |

**User's choice:** Headings stay, empty ones drop out

### Which tags appear in the filter control?

| Option | Description | Selected |
|--------|-------------|----------|
| Only tags on contacts this viewer can see *(recommended)* | Derived from the visible-but-unfiltered set. Mirrors Phase 80 D-13; needs no separate vocabulary query, so it is fail-closed by construction. | ✓ |
| The whole group's tag vocabulary | Simpler and cacheable, but the ROADMAP flags the tag list as a leak surface and a campaign-revealing name would reach players before any NPC does. | |
| Full vocabulary for DM-tier, viewer-scoped for players | Removes a minor oddity for DMs, at the cost of two vocabulary paths to build and test. | |

**User's choice:** Only tags on contacts this viewer can see

### Who sees tags and the tag filter?

*(Asked out of band — the user requested advice on restricting tags to DMs during the continue-check.)*

| Option | Description | Selected |
|--------|-------------|----------|
| DM-tier only — players see nothing *(recommended)* | Everything sits inside the existing `ViewerIsDmTier` block. The reversible direction: a later phase can open it up by flipping one conditional. Asymmetric with Phase 80's player-visible categories. | ✓ |
| Everyone — players see tags and can filter | The original assumption; safe, because viewer-scoped vocabulary already prevents leaks. But it is the non-reversible direction. | |
| Filter for everyone, chips for DM-tier only | Middle ground, but players would filter by something invisible on the results — two visibility rules instead of one. | |

**User's choice:** DM-tier only
**Notes:** Advice given was that the *safety* argument had already been handled by the viewer-scoped vocabulary rule; the deciding argument was asymmetric risk — shipping player-visible cannot be undone once a tag name has spoiled something at the table. The cost was named explicitly: two organisational systems on one page with different audiences, and a real player question left unanswered.

---

## Tag authoring & permissions

**Not asked:** the ROADMAP's "may players create tags?" question was answered as a consequence of the DM-only audience decision — players cannot see tags, so `DungeonMasterOnly` gates every write.

### How does a tag come into existence, and how is it fixed later?

| Option | Description | Selected |
|--------|-------------|----------|
| Free-typed + a Manage Tags page *(recommended)* | Mirrors Phase 80's Manage Categories; solves the folksonomy failure mode by giving a rename path. Two more views to build and verify on two platforms. | |
| Free-typed only, orphans auto-pruned — no management page | Smallest possible phase: contact form plus filter, no new views. No rename — fixing a typo means editing every contact carrying it. | ✓ |
| Curated only — create on the Manage page, pick on the form | Tidy by construction, but contradicts the ROADMAP's "free-form tags" wording and adds a detour mid-edit. | |

**User's choice:** Free-typed only, orphans auto-pruned
**Notes:** The lean cut. Two compensating decisions followed directly from it — the case-insensitive unique index (below) and rendering chips on the index as the vocabulary audit surface.

### What prevents "Shopkeeper" vs "shopkeeper" duplicates with no rename path?

| Option | Description | Selected |
|--------|-------------|----------|
| A real tag table with a case-insensitive unique index *(recommended)* | `ContactTag {Id, Name, GroupId}` + join table, unique on `(GroupId, Name)` as Phase 80 D-04 does. Typing an existing name reuses the row — a database guarantee. Needs orphan pruning. | ✓ |
| No tag table — a tag is a labelled row on a contact | Smallest schema, orphans impossible. Duplicate prevention becomes a write-time convention, and with no vocabulary table there are no stable ids, reopening the URL decision. | |
| Allow duplicates | Named only to rule out — with no rename, a duplicate is permanent and splits a tag's contacts across two filter entries. | |

**User's choice:** A real tag table with a case-insensitive unique index

### Is the orphan prune still worth it?

*(Follow-up: with a real tag table and viewer-derived filter lists, an orphaned row renders nowhere anyway.)*

| Option | Description | Selected |
|--------|-------------|----------|
| Prune on save — keep it as chosen | Keeps the table honest. Cost: re-adding a removed tag mints a new id, so bookmarked filter URLs can go stale. | ✓ |
| Never prune — orphans are invisible and ids stay stable *(recommended)* | No prune logic to write or test; the unique index reuses the same id when a tag returns, so bookmarks keep working. Table accumulates dead rows. | |
| Prune, and document it as the delete mechanism | Same as the first, stated as a user-facing contract. | |

**User's choice:** Prune on save
**Notes:** Recommendation was declined; the id-instability cost is recorded in CONTEXT.md D-06 along with the requirement that an unknown or foreign tag id must silently match nothing rather than error.

### Cap on tags per contact and tag name length?

| Option | Description | Selected |
|--------|-------------|----------|
| Short names (~30 chars), soft cap on count *(recommended)* | Shorter than Phase 80's ~60 for categories because a tag is an inline chip. No hard count limit; chips must wrap. | |
| Short names, hard cap of ~8 per contact | Predictable mobile row height, at the cost of a validation message and a number someone will want raised. | |
| You decide | Hand both limits to the planner. | ✓ |

**User's choice:** You decide
**Notes:** Locked in CONTEXT.md with rationale (≈30 chars, no hard count cap, chips wrap) so the planner inherits a default rather than an open question.

---

## Tag entry & display

### How does a DM attach tags on the four Create/Edit forms?

| Option | Description | Selected |
|--------|-------------|----------|
| Checkbox list of existing tags + text field for new ones *(recommended)* | Zero JS, same on both platforms; reusing a tag becomes a click, directly countering the missing rename path. List grows long on mobile. | |
| One comma-separated text field with a hint line | Smallest markup, most free-form. Reusing a tag means retyping it, so misspellings mint unfixable tags. | |
| A chips / typeahead widget | Nicest experience. Flagged during the question as new JS with no precedent. | ✓ |

**User's choice:** A chips / typeahead widget
**Notes:** The concern raised in the question was withdrawn after checking the codebase — `wwwroot/js/markdown-editor.js` and `image-crop.js` are already bespoke form-field modules loaded on these exact four views. Phase 80's rejection was of a new interaction model on the *index*, not of form-field enhancement. The user's choice was better-supported than the recommendation.

### Bespoke widget or wrap a CDN library?

| Option | Description | Selected |
|--------|-------------|----------|
| Bespoke `contact-tags.js`, no new dependency *(recommended)* | House style, no SRI pinning, no third-party CSS to fight the theme. Risk: hand-rolling keyboard and screen-reader behaviour. | |
| Wrap a CDN library (Tagify or similar) | The `image-crop.js` / cropperjs shape. Gets paste handling, keyboard nav, dedupe, and accessibility for free. Costs a pinned dependency and theme overrides. | ✓ |
| You decide | Hand the choice to research and planning. | |

**User's choice:** Wrap a CDN library

### What if the CDN script fails to load?

| Option | Description | Selected |
|--------|-------------|----------|
| Degrade to a plain comma-separated text input *(recommended)* | The library binds to a real input anyway; configure it to write the comma format back, so the server parses one shape whether or not JS ran. | ✓ |
| No fallback | One code path to test; a silent dead control when the CDN is unreachable. | |
| Self-host the library in `wwwroot/lib/` | Cannot be blocked, and fits "deployable via docker-compose with no setup" — but would be the first self-hosted library here. | |

**User's choice:** Degrade to a plain comma-separated text input
**Notes:** Noted that cropperjs and EasyMDE are already CDN-loaded, so the dependency shape is an accepted existing risk in this app.

### Where do tags render for a DM?

| Option | Description | Selected |
|--------|-------------|----------|
| Chips on index cards + a line on Details *(recommended)* | The index becomes the vocabulary audit surface given up with the management page. Mirrors Phase 80 D-16. Adds visual weight to cards and mobile rows. | ✓ |
| Details page only | Index stays visually identical to what Phase 80 leaves behind; auditing means opening contacts one at a time. | |
| Neither — filter-only metadata | Named to rule out: nothing would ever show a DM their own vocabulary. | |

**User's choice:** Chips on index cards + a line on Details

---

## Filter UI surface

### Where does the filter control live?

| Option | Description | Selected |
|--------|-------------|----------|
| The Shop pattern — desktop filter row, mobile offcanvas drawer *(recommended)* | Proven on both platforms here; the only shape that makes OR multi-select usable. Most markup of the three. | ✓ |
| Clickable chips only | Zero new filter UI, reuses the chips. But you can only filter by what is on screen, and multi-tag OR barely works. | |
| Both — control plus clickable chips | Best experience; two entry points into the same state to keep consistent. | |

**User's choice:** The Shop pattern
**Notes:** Clickable chips were captured as a deferred idea — they compose cleanly with the id-based query string.

### Before a board has any tags, does the filter render?

| Option | Description | Selected |
|--------|-------------|----------|
| Render it disabled, with a hint pointing at the contact form *(recommended)* | Applies Phase 80 **D-07's** logic rather than D-10's: D-10 hides categories because *players* read the index, and this filter is DM-only. Discovery is cheap when no player sees it. | ✓ |
| Hide it entirely until the first tag exists | Follows D-10's letter; cleanest index, but no way to discover the feature except from a contact's Edit form. | |
| Always render it, enabled and empty | Simplest code; an enabled control that does nothing reads as broken. | |

**User's choice:** Render it disabled, with a hint

### When an active filter matches nothing?

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror the Shop's two-branch empty state *(recommended)* | "No contacts match your filters" + Clear action, distinct from a genuinely empty list. No extra query. | ✓ |
| Same, plus a "Show Hidden would reveal matches" nudge | Useful and safe given the DM-only audience, but needs a second evaluation against the pre-visibility set — a new query path and test. | |
| One generic empty message | Least markup; a DM who forgot a filter was active gets no clue why their NPCs vanished. | |

**User's choice:** Mirror the Shop's two-branch empty state

### Should the filter survive a Show Hidden toggle?

*(Raised from a concrete finding: `ContactsController.ToggleShowHidden` ends `RedirectToAction(nameof(Index))` with no route values, so as written it would drop the query string.)*

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — carry the filter through the toggle *(recommended)* | Hidden fields carry the tag ids; the redirect re-attaches them. Filtering then flipping Show Hidden is the most likely moment to use both controls together. | ✓ |
| No — toggling clears the filter | One less thing to thread through a POST, but the two controls fight each other with no explanation on screen. | |
| You decide | Hand it to the planner, noting the interaction must be decided rather than inherited by accident. | |

**User's choice:** Yes — carry the filter through the toggle

---

## Claude's Discretion

- **Tag name length and per-contact count cap** — user answered "you decide". Locked in CONTEXT.md as ≈30 characters and no hard count cap, with rationale; planner may revisit.
- **Which library exactly** — Tagify assumed; planner may substitute an equivalent meeting the same constraints.
- Join-table naming; explicit entity vs skip-navigation.
- Whether to mint a `CONTACTTAG-*` requirement family into REQUIREMENTS.md as plan 01.
- CSS class naming for chips, filter row, and offcanvas.
- Exact wording of the disabled-filter hint and the no-results message.

## Deferred Ideas

- Opening tags to players by flipping the `ViewerIsDmTier` conditional.
- Renaming and merging tags, and a Manage Tags page.
- Clickable tag chips as a one-click filter shortcut.
- An AND / "match all" toggle alongside OR.
- A "some hidden contacts match — turn on Show Hidden" nudge on the empty state.
- Bulk tagging (shares a surface with Phase 80's deferred bulk assignment).
- Free-text search over contacts.
- Tags on Characters or Quests.
