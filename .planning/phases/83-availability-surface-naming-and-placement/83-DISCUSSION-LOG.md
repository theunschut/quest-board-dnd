# Phase 83: Availability Surface Naming and Placement - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-30
**Phase:** 83-availability-surface-naming-and-placement
**Areas discussed:** Rename depth, Nav placement mechanics, Cross-link survival, Nav test set

---

## Area selection

| Option | Description | Selected |
|--------|-------------|----------|
| Rename depth | How far "Board Availability" propagates: label only, or route and action naming too | ✓ |
| Nav placement mechanics | The one-item Calendar dropdown, and which gate the moved entry takes | ✓ |
| Cross-link survival | Calendar → overview link visibility, and whether My Agenda links back | ✓ |
| Nav test set | Which assertions prove "moved, not broken" | ✓ |

**User's choice:** All four areas.

---

## Rename depth

### Q1 — How far should the rename reach into the code?

| Option | Description | Selected |
|--------|-------------|----------|
| User-visible strings only | Page title, nav labels, cross-link copy, test assertions — 14 sites. Route, controller, view folder and domain types untouched | ✓ |
| Strings + dedicated route | Adds a route so the URL reads the new name; breaks bookmarks unless a redirect is kept | |
| Full rename incl. domain types | Also renames domain models, service method, options class and its config section | |

**User's choice:** User-visible strings only.
**Notes:** The config-section hazard was decisive — `EventsOverviewOptions.SectionName` has code defaults and no `appsettings*.json` entry, so a rename could silently drop a server-environment-file override the repo cannot see.

### Q2 — Should the page name the active board?

| Option | Description | Selected |
|--------|-------------|----------|
| Board name as subtitle | Heading stays fixed; active board name renders beneath as muted text | ✓ |
| Literal "Board Availability" only | Nothing interpolated; "which board" answered by the board switcher in nav | |
| Interpolate into the heading | Heading reads e.g. "Sunless Citadel — Availability"; page loses one fixed name | |

**User's choice:** Board name as subtitle — with a follow-up question about the name itself.
**Notes:** User replied *"I think your recommended option, but what about Event Availability? or Board Event Availability? as this page only shows events and not quests?"* — reopening the naming question, which the roadmap had logged as a real but secondary ambiguity. Triggered Q3.

### Q3 — Which naming shape for the board-scoped surface?

| Option | Description | Selected |
|--------|-------------|----------|
| "Board Availability" + events subtitle | Locked two-word name kept; subtitle carries both which-board and events-not-quests | ✓ |
| "Board Event Availability" + board subtitle | Both fixes in the name; three words, breaks two-word symmetry with My Agenda | |
| "Event Availability" + board subtitle | Reads well beside Create Event, but drops the whose-axis — the primary defect | |

**User's choice:** "Board Availability" + events subtitle.
**Notes:** The subtitle slot was already committed by Q2, so it could absorb the events qualifier at no cost to the name or to test assertions. Roadmap wording stands unamended on this point.

### Q4 — Should My Agenda get the same subtitle treatment?

| Option | Description | Selected |
|--------|-------------|----------|
| Matching subtitle | "Upcoming events across all your boards"; the pair reads symmetrically | ✓ |
| Leave My Agenda untouched | Page already names the board per row and carries a board filter | |
| Subtitle only when multi-board | Conditional copy; a second rendering path to test on both layouts | |

**User's choice:** Matching subtitle.

### Q5 — What structurally prevents a stale label surviving in rendered output?

| Option | Description | Selected |
|--------|-------------|----------|
| Rendered-output guard test | Fetches affected pages on both user agents; asserts the old string appears in none | ✓ |
| Update the 8 assertions in place | Cheapest; nothing fails if an unvisited view keeps the old string | |
| Source-level string sweep test | Reads `.cshtml` files directly; catches unrendered views but couples tests to file paths | |

**User's choice:** Rendered-output guard test.
**Notes:** Stays valid alongside Q1's decision to keep the internal `Overview` vocabulary, because C# type names never reach rendered HTML.

---

## Nav placement mechanics

### Q1 — What happens to the desktop Calendar dropdown once it holds one item?

| Option | Description | Selected |
|--------|-------------|----------|
| Collapse to a plain nav link | Reverts Phase 77 D-19; its stated reason leaves with the overview | ✓ |
| Keep the one-item dropdown | Smallest diff to a nav block with a regression history; costs every reader a click | |
| Keep dropdown, add Board Availability for DMs | Contradicts the roadmap's locked placement; mixes two gate kinds in one entry | |

**User's choice:** Collapse to a plain nav link.
**Notes:** Verified safe — the four existing `Nav_*_CalendarLinkPresent` cases assert on the string "Calendar", not markup structure.

### Q2 — Which gate does the moved entry take?

| Option | Description | Selected |
|--------|-------------|----------|
| Both — DM policy AND board-type | Strictly narrows; preserves Phase 77 D-22's unresolved-board-type exclusion | ✓ |
| DM policy only | Matches sibling Create Event and flattens the markup, but silently widens visibility | |

**User's choice:** Both — DM policy AND board-type.

### Q3 — Where inside the Dungeon Master menu does the entry land?

| Option | Description | Selected |
|--------|-------------|----------|
| Directly after Create Event | Groups the two event surfaces; same index on both layouts | ✓ |
| Last, below a divider | Separates reading from doing, but mobile has no divider idiom | |
| First, above Create Quest | Most discoverable, but pushes the app's most-used entry down a slot | |

**User's choice:** Directly after Create Event.

---

## Cross-link survival

### Q1 — Does the Calendar page's cross-link stay for everyone?

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it for everyone | Exactly the roadmap's wording; zero conditional logic on either calendar view | |
| DM-only, matching the nav | One rule wherever the reader stands; contradicts the roadmap's explicit carve-out | ✓ |
| Keep for everyone, de-emphasise | Quieter treatment without hiding; bespoke button style | |

**User's choice:** DM-only, matching the nav.
**Notes:** **A deliberate amendment to the roadmap**, taken with the contradiction stated in the option description before selection. `.planning/ROADMAP.md`'s Phase 83 scope note must be updated to match, or downstream agents read a conflict.

### Q2 — Is a player's only route being the raw URL the intended end state?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — no player-facing link anywhere | Soft hide, not a permission change; page stays authorization-gate-free | ✓ |
| Yes, but keep the Details-page path | Preserves an in-context route from the relevant event | |
| Reconsider — keep the calendar link for all | Reverts to the roadmap wording | |

**User's choice:** Yes — no player-facing link anywhere.
**Notes:** Asked explicitly because the combined effect moves the phase close to the DM-only gate the roadmap rejected on 2026-08-30. Reaffirmed; the page itself keeps no gate.

### Q3 — Should My Agenda carry a return link to Board Availability?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, DM-only | Mirrors the button already pointing the other way; completes the pair for DMs | ✓ |
| No link | Keeps the diff off a page that shipped the day before | |
| Yes, for everyone | Hands players back the route the previous decisions just removed | |

**User's choice:** Yes, DM-only.

### Q4 — What happens to Board Availability's existing My Agenda header button?

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it for everyone | Signposts a player who arrives by bookmark toward their own view | |
| Keep it, DM-only | One rule in both directions | ✓ |

**User's choice:** Keep it, DM-only.
**Notes:** Verified this strands nobody — Phase 82 D-08 put My Agenda in the user dropdown unconditionally for every authenticated user, so a player on Board Availability still reaches it in one click from their own menu.

---

## Nav test set

### Q1 — How strong should the assertions on the moved entry be?

| Option | Description | Selected |
|--------|-------------|----------|
| Role-flip strings, both layouts | Existing Contain/NotContain idiom; the flip is itself structural proof | ✓ |
| Parse markup and assert ancestry | Proves placement exactly; adds a parser dependency and a new idiom | |
| Strings plus an href-window check | Cheaper than parsing; brittle against unrelated edits in the block | |

**User's choice:** Role-flip strings, both layouts.

### Q2 — Which cases make up the nav suite? (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| DM sees it — Campaign + OneShot | Replaces the existing DM-present case | ✓ |
| Player does not — Campaign + OneShot | Inverts two existing cases; the load-bearing half of the role-flip argument | ✓ |
| DM with unresolved board type does not | Proves the board-type gate survived the move | ✓ |
| Player GET /Events returns 200 | Proves no authorization gate crept in | ✓ |

**User's choice:** "you decide" — delegated to Claude.
**Notes:** All four taken. Each proves something the others cannot, and none is redundant; recorded in CONTEXT.md as decided rather than left to planner discretion.

### Q3 — Where does the calendar link's new role split get tested?

| Option | Description | Selected |
|--------|-------------|----------|
| Re-seed as DM, add player case in place | Everything about that one link stays in one file | ✓ |
| Split style from visibility | Cleaner separation; two files to touch for one link | |
| New dedicated class | Most discoverable name; a third file for a single button | |

**User's choice:** Re-seed as DM, add player case in place.
**Notes:** Not optional cleanup — both existing `CalendarButtonStyleTests` cases authenticate as a Player and assert presence, so the cross-link decision breaks them outright.

### Q4 — What does the rendered-output guard sweep, and where does it live?

| Option | Description | Selected |
|--------|-------------|----------|
| Both renamed pages + calendar, new class | One DM role sees every affected surface and every cross-link | ✓ |
| Fold into EventsOverviewControllerIntegrationTests | No new file, but sees one page — the exact gap the guard exists to close | |
| Sweep every authenticated page | Broadest coverage; slow, brittle, needs maintenance on every new route | |

**User's choice:** Both renamed pages + calendar, new class.

---

## Claude's Discretion

- The nav test case set (Q2 above) — delegated with "you decide"; all four groups taken.
- Exact subtitle wording on both pages, and the fallback when `SessionKeys.ActiveGroupName` is empty.
- Icon treatment for the moved entry inside the Dungeon Master menu.
- Whether the four DM-only view conditions share a helper or are inlined per view.
- Test class and method naming, and the new guard class's folder placement.
- Whether internal code comments referring to "the availability overview" are reworded.

## Deferred Ideas

None — the discussion stayed inside the phase boundary. Naming and placement alternatives were evaluated and rejected in place rather than deferred; the reasoning is preserved in the tables above and in CONTEXT.md's decision entries.

The roadmap's existing reopen condition on the rejected DM-only authorization gate is carried forward unchanged and is not consumed by any decision here.
