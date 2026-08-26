# Phase 72: Change Character on an Existing Signup - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-25
**Phase:** 72-change-character-on-an-existing-signup
**Areas discussed:** Change affordance, Clearing to none, Retired/Dead pick, Save feedback

---

## Change affordance

### Q1 — Desktop trigger when a character is already set

| Option | Description | Selected |
|--------|-------------|----------|
| Pencil button beside name | Small `btn-sm` pencil right after name+avatar, mirroring where the green `+` sits today. Lowest-risk; same cell, same size, both tables identical. Cost: a second small button in a busy row. | ✓ |
| Whole cell is clickable | Avatar+name block becomes the trigger for your own row. Zero extra chrome. Cost: no affordance until hover; new interaction pattern here. | |
| Text 'Change' button | Explicit `btn-sm btn-outline` labelled "Change". Most discoverable. Cost: widest; breaks the icon-only convention the `+` established. | |

**User's choice:** Pencil button beside name

### Q2 — Mobile placement in the tight participant flex row

| Option | Description | Selected |
|--------|-------------|----------|
| Inline, next to character name | Second line of the left stack, same structural relationship as desktop. One mental model across platforms. Cost: the line is `<small>`; the button must stay subordinate so the row doesn't grow. | ✓ |
| Third column, left of the badge | Own element between the left stack and the right-pinned badge. Bigger tap target, never crowds the small text. Cost: three zones on a narrow screen; badge shifts only on your row. | |
| Tap anywhere on your row | Your row (already `bg-dark rounded px-2`) becomes the trigger. Largest tap target, mobile-native. Cost: invisible affordance; new pattern here. | |

**User's choice:** Inline, next to character name

### Q3 — Visibility rule

Context given: the control is gated on `ViewBag.UserCharacters.Any()` (Active-only), so a player with zero Active characters but a Retired one on their signup sees no control and can never clear it.

| Option | Description | Selected |
|--------|-------------|----------|
| Show if character set OR any Active exists | Renders whenever there's something to do; hides only the genuinely dead case. Closes the Retired-lockout hole SIGNCHAR-03+04 create together. | ✓ |
| Always show on your own row | Simplest rule — one condition. Cost: a player with no characters gets a pencil opening a dead-end modal. | |
| Keep the current gate | Smallest diff. Cost: leaves the Retired lockout live. | |

**User's choice:** Show if character set OR any Active exists *(recommended option)*

### Q4 — Icons for empty vs filled state

| Option | Description | Selected |
|--------|-------------|----------|
| Keep `+` for empty, pencil for filled | Icon matches the verb; existing add flow untouched. Cost: branches stay separate, so four trigger blocks across desktop's two tables. | ✓ |
| One pencil for every state | Collapses each cell to one block — addresses the near-duplicate drift risk. Cost: a pencil reads oddly when nothing is there. | |
| You decide | Let the planner pick from how the trigger factors out alongside the partial. | |

**User's choice:** Keep `+` for empty, pencil for filled

---

## Clearing to none

### Q1 — How clearing is expressed

Context given: the modal's select carries `required`, blocking the empty option at browser level.

| Option | Description | Selected |
|--------|-------------|----------|
| Drop `required`, empty option clears | Placeholder becomes a real `-- No character --` choice; one control, one submit path, matches the `int?` the action accepts. Cost: two-step and reads passive. | |
| Separate 'Remove character' button | Keeps `required` for add/change; a distinct button posts null. Clearing becomes explicit and unmistakable. Cost: two submit paths, third footer button, must hide when nothing to remove. | ✓ |
| Both | Blank option *and* Remove button. Max discoverability. Cost: two ways to do one thing; more surface to keep in sync. | |

**User's choice:** Separate 'Remove character' button
**Notes:** Operator wanted clearing to be an explicit act, not a passive selection.

### Q2 — Where the destructive button lives

Context given: CLAUDE.md's modal convention is `d-flex justify-content-between` — secondary left, primary right.

| Option | Description | Selected |
|--------|-------------|----------|
| Footer far left, opposite Cancel/Save | `btn-outline-danger` pinned left; Cancel+Save group right. Standard destructive-isolated pattern. Cost: three buttons is tight on a narrow mobile modal. | ✓ |
| In the modal body, under the dropdown | Sits with the field it acts on; footer keeps its clean two-button shape. Cost: a submit-style action outside the footer is unusual here. | |
| Text link under the dropdown | Lowest visual weight, footer untouched. Cost: least discoverable; a link performing a POST is a weak affordance. | |

**User's choice:** Footer far left, opposite Cancel/Save

### Q3 — Confirmation before clearing

Context given: a confirm dialog *before a swap* is already ruled out of scope (reversible, affects no one else, fires no email). Remove is a new, explicitly destructive-looking control, so it was decided on its own rather than assumed covered.

| Option | Description | Selected |
|--------|-------------|----------|
| No confirm — same as swap | Clearing is as reversible as swapping; consistent with the locked swap decision and every other edit surface. Cost: a red button firing instantly may feel abrupt. | |
| Native `confirm()` dialog | Matches the existing `revokeSignup()` pattern in `Details.cshtml`. Cost: revoke is irreversible and this isn't, so it borrows unearned friction; adds JS to the partial. | ✓ |
| Two-step in-modal | Button swaps to a "Confirm remove?" state in place. Cheap safety, no browser dialog. Cost: most JS; new pattern here. | |

**User's choice:** Native `confirm()` dialog

### Q4 — Does Remove render when there's nothing to remove?

| Option | Description | Selected |
|--------|-------------|----------|
| Hidden when no character set | Add flow stays visually identical to today. Cost: footer button count changes between opens of one shared modal, so show/hide rides the same wiring that pre-selects the dropdown. | ✓ |
| Always rendered, disabled when empty | Fixed three-button footer; nothing moves. Cost: a permanently-dead control, and Phase 35 recorded that disabled controls here are hard to distinguish from live ones. | |
| Always rendered and enabled | Simplest markup; clearing an empty signup is a harmless no-op. Cost: destructive-looking action that does nothing, and still fires `confirm()`. | |

**User's choice:** Hidden when no character set

---

## Retired/Dead pick

### Q1 — How an inactive current character is shown (SIGNCHAR-04)

Context given: `ViewBag.UserCharacters` is Active-only, so a Retired/Dead character has no matching `<option>`.

**First response:** *"What's your advise here?"* — Claude recommended injecting it as a selected, labelled option, on the grounds that it preserves the invariant that the dropdown's value *is* the signup's `CharacterId`; that the read-only + "Keep current" alternative would need a third state a nullable `int` can't express, colliding with the just-locked `null`-means-clear; that `<optgroup>` has no precedent in this codebase; and that injecting the option kills the roadmap's named silent-wipe risk at the source. Claude also flagged that this display fix alone is insufficient — `QuestController.cs:545` would still `BadRequest` the save.

| Option | Description | Selected |
|--------|-------------|----------|
| Inject as selected, labelled option | Prepend the signup's character as `<Name> — Level N (Retired)`, marked selected. No sentinel needed; `null` keeps meaning clear. | ✓ |
| Grouped under an `<optgroup>` | Active in one group, inactive under "No longer active". Cleaner semantics, scales past one entry. Cost: no precedent here; heavy for usually one entry. | |
| Read-only line + 'Keep current' | Static "Currently: X (Retired)" with a sentinel default. Most explicit about not-selectable. Cost: needs a third state alongside `null`-means-clear. | |

**User's choice:** Inject as selected, labelled option *(recommended option, confirmed after advice)*

### Q2 — How the server-side non-Active rejection changes

| Option | Description | Selected |
|--------|-------------|----------|
| Allow only if unchanged *(Claude's recommendation)* | Accept a non-Active id only when it equals the signup's existing `CharacterId`. Narrowest relaxation — "you may keep what you already have". Ownership check untouched. | |
| Allow any character you own | Drop the `Status` check entirely; ownership and group scope remain the only gates. Simplest rule. Cost: silently widens the feature — Retired and Dead become newly assignable, which no requirement asked for. | ✓ |
| Keep Active-only, treat unchanged as a no-op | Client omits `characterId` when untouched. Zero contract change. Cost: correctness depends on client-side dirty-tracking; any post carrying the Retired id still 400s. | |

**User's choice:** Allow any character you own
**Notes:** Chosen against Claude's recommendation, with the widening trade-off stated in the option description. Recorded in CONTEXT.md as a knowing operator decision.

### Q3 — Should the dropdown then list Retired/Dead as freely selectable?

Follow-up raised by Claude: dropping the `Status` check makes the server accept more than the dropdown offers.

| Option | Description | Selected |
|--------|-------------|----------|
| List all owned characters, status-labelled | UI and server permit the same set — no gap. Cost: changes the add flow too; the list grows with every dead character. | ✓ |
| Active-only + injected current pick | Server permissive, UI opinionated — keep a Retired character but don't go shopping for one. Cost: deliberate disagreement needing a comment. | |
| You decide | Let the planner settle it from how `ViewBag.UserCharacters` is populated. | |

**User's choice:** List all owned characters, status-labelled

### Q4 — How far the `ViewBag.UserCharacters` change reaches

Context given: one writer (`QuestController.cs:337`), five readers — the modal, two visibility gates, and three signup-time selects that are outside this phase's stated scope.

| Option | Description | Selected |
|--------|-------------|----------|
| Widen the shared list — all six sites | One list, one rule everywhere on Details. No divergence to explain; gates get simpler. Cost: changes signup-time behaviour no requirement asked for — the exact spillover the roadmap's no-scope-creep line points at. | ✓ |
| Second list for the modal only | Blast radius stays inside the named feature. Cost: two near-identical lists on one ViewBag; the split must be written down. | |
| Widen the list, but keep signup-time filtered in the view | Single source of truth in the controller, presentation choice in the view. Cost: filtering logic in three Razor blocks — the near-duplicate drift the roadmap warns about. | |

**User's choice:** Widen the shared list — all six sites

### Q5 — Group scoping (gap surfaced by Claude mid-area)

Claude reported: `QuestController.cs:328` populates the list with `GetCharactersByOwnerIdAsync(currentUser.Id)` — owner-filtered only, never group-filtered — and the validation checks `OwnerId` and `Status` but never `GroupId` (present on `Character.cs:44`). A user in two groups is already offered their other board's characters and the server would accept one. Dropping the `Status` filter widens the offered set.

| Option | Description | Selected |
|--------|-------------|----------|
| Filter the list AND check on save | Scope `ViewBag.UserCharacters` to the active group *and* add a `GroupId` check to the action. Defence in both layers. This is what SIGNCHAR-07's test actually proves. | ✓ |
| Server check only | Closes the hole with the smallest diff. Cost: dropdown still offers characters that 400 on save. | |
| Filter the list only | Fixes what users can reach through the UI. Cost: leaves the server accepting a hand-crafted cross-group post — the class of leak this project has shipped twice. | |

**User's choice:** Filter the list AND check on save *(recommended option)*

---

## Save feedback

### Q1 — What the player sees after saving

Context given: `UpdateSignupCharacter` does a bare `RedirectToAction("Details")` with no `TempData`; the Phase 42 toast system is already wired into both layouts.

| Option | Description | Selected |
|--------|-------------|----------|
| Success toast on both swap and clear | `TempData["Success"]` before redirect; `_Toasts.cshtml` picks it up with no view changes. Matters most on mobile where the changed row may be off-screen. | ✓ |
| Stay silent — redirect only | The reloaded page is its own confirmation. Smallest diff, no toast fatigue. Cost: on mobile a successful save can look like nothing happened. | |
| Toast on clear only | Clearing is the one outcome mistakable for a failed save. Cost: asymmetric behaviour from one action. | |

**User's choice:** Success toast on both swap and clear

### Q2 — Whether the failure paths change too

**First response:** *"what's your advise?"* — Claude recommended the split, on the grounds that "not signed up" is reachable without tampering (stale modal after revoking in another tab, or being dropped from a finalized quest) and a bare 400 is a dead end for a legitimate user; whereas once the dropdown is group-scoped, nothing legitimate can produce a cross-group post, so a hard rejection is honest and keeps SIGNCHAR-07's test asserting on a rejection rather than the weaker "redirected and happened not to mutate".

| Option | Description | Selected |
|--------|-------------|----------|
| Leave both as `BadRequest` | Smallest diff, symmetric, crisp for testing. Cost: a legitimate stale-page save lands on a bare 400. | |
| Error toast + redirect for both | Every outcome lands on a working page. Cost: launders a tampering attempt into a friendly message; weakens the security assertion. | |
| Toast for 'not signed up', 400 for cross-group | Matches each response to how the state is actually reached. Cost: two failure styles in one action, needing a comment to survive review. | ✓ |

**User's choice:** Toast for 'not signed up', 400 for cross-group *(recommended option, confirmed after advice)*

---

## Claude's Discretion

- Whether the trigger button lives inside `_CharacterSelectModal.cshtml` or stays in each host view
- How the modal learns the current pick per invocation (`show.bs.modal` + `event.relatedTarget` is the named precedent)
- How Remove posts `null` without colliding with the `characterId` select or tripping `required`
- Exact status-suffix wording and Active-vs-inactive ordering in the list
- Toast message wording for swap vs clear
- Test structure for SIGNCHAR-07 beyond "two distinct groups, asserts rejection"

## Deferred Ideas

- **SIGNCHAR-08** — "recently changed" indicator on the DM's Manage page. Already logged in `.planning/REQUIREMENTS.md` → Future Requirements.
- **The shared partial's exact boundary** — offered as a further gray area; user chose to proceed to context instead.
- **What the mobile User-Agent verification must prove** — offered as a further gray area; left to the planner.

*No scope creep was raised during this discussion. The two scope-widening decisions (D-10/D-11 dropping the `Status` check, D-12 widening the shared list to the signup-time selects) were deliberate operator choices made with the trade-off stated, not creep — both are recorded as such in CONTEXT.md.*
