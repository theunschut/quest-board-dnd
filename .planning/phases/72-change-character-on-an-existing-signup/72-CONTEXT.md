# Phase 72: Change Character on an Existing Signup - Context

**Gathered:** 2026-08-25
**Status:** Ready for planning

<domain>
## Phase Boundary

A player can change — or clear — the character on **their own** existing quest signup, from both the desktop and mobile quest Details pages, without a DM intervening. Covers the finalized-participants table and the waitlist table, all three signup roles, and works after finalization with no time cutoff.

Not in this phase: DM-side character editing on the Manage page, email notification on swap, audit trail, and any change to signup *creation* flow beyond the shared character list widening decided below.

</domain>

<decisions>
## Implementation Decisions

### Change affordance

- **D-01:** Desktop — when a character is already set, a small `btn-sm` **pencil icon button** sits immediately after the character name + avatar, in the same table cell and at the same size as the existing green `+`. Applies identically to both the finalized-participants table and the waitlist table.
- **D-02:** Mobile — the pencil renders **inline on the second line**, immediately after the character name inside the left stack of the participant row. Same structural relationship as desktop, so both platforms share one mental model. It must stay visually subordinate to the `<small>` text so the row does not grow taller.
- **D-03:** Visibility rule — the control renders when **a character is set OR the player has at least one selectable character**. It is hidden only when there is nothing to do (no character and nothing to pick). This deliberately replaces today's `ViewBag.UserCharacters.Any()` gate, which locks a player out of clearing a Retired character.
- **D-04:** The empty and filled states keep **separate icons**: green `+` ("Add character") when no character is set, pencil when one is. The existing add flow is otherwise untouched.

### Clearing to no character (SIGNCHAR-03)

- **D-05:** Clearing is a **dedicated "Remove character" button**, not a blank dropdown entry. The `required` attribute **stays** on the `<select>` for the add/change path.
- **D-06:** The Remove button is `btn-outline-danger`, pinned **far left in the modal footer**, with Cancel and Save grouped right — the destructive-action-isolated variant of CLAUDE.md's `d-flex justify-content-between` convention.
- **D-07:** Remove is guarded by a **native `confirm()` dialog**, matching the existing `revokeSignup()` idiom already in `Details.cshtml`.
- **D-08:** Remove is **hidden entirely when no character is set** — the add flow's footer stays exactly as it ships today (Cancel + Save). The show/hide rides the same per-invocation wiring that pre-selects the dropdown.

### Inactive (Retired/Dead) characters — SIGNCHAR-04

- **D-09:** The signup's current character is **injected into the dropdown as a selected, status-labelled option** (e.g. `Thorin — Level 5 (Retired)`). The dropdown's value therefore always *is* the signup's `CharacterId` — no sentinel "keep current" state is needed, and `null` keeps meaning "clear". This is what structurally kills the silent-wipe risk the roadmap flags (a naive `select.value = characterId` with no matching `<option>` falling back to `""`).
- **D-10:** **The server-side `CharacterStatus.Active` check is dropped entirely** from `UpdateSignupCharacter`. Ownership and group scope become the only gates. *Trade-off accepted knowingly:* this is wider than the minimal "allow only if unchanged" relaxation and makes Retired/Dead characters newly assignable, which no requirement asked for. The operator chose it deliberately as the simpler, more permissive rule — a player should be free to bring whoever they like.
- **D-11:** Consequently the dropdown **lists all owned characters**, each suffixed with its status where not Active — so the UI offers exactly what the server permits, with no gap between the two.
- **D-12:** `ViewBag.UserCharacters` is **widened at its single source** (`QuestController.cs:337`) and the change reaches **all six read sites**, including the three signup-time selects. One list, one rule, everywhere on the Details page. *Noted:* this changes signup-time behaviour, which no SIGNCHAR requirement asked for; it is an accepted, deliberate spillover chosen over maintaining two near-identical lists.

### Group scoping (SIGNCHAR-07) — gap found during discussion

- **D-13:** **Defence in both layers.** `ViewBag.UserCharacters` is scoped to the **active group** so the dropdown only ever offers same-board characters, *and* a `GroupId` check is added to `UpdateSignupCharacter` so a tampered post is rejected regardless.
- **Why this is here:** the scout found the population query is `GetCharactersByOwnerIdAsync(currentUser.Id)` — **owner-filtered only, never group-filtered** — while the validation checks `OwnerId` and `Status` but **never `GroupId`** (which exists on `Character.cs:44`). A user in two groups is already offered their other board's characters today and the server would accept one. Dropping the `Status` filter (D-10/D-11) widens the offered set, so this gets more exposed, not less. This is the live gap SIGNCHAR-07's cross-group test is actually there to prove closed.

### Save feedback

- **D-14:** **Success toast on both swap and clear** — set `TempData["Success"]` before redirecting. The shared `_Toasts.cshtml` in both layouts picks it up with no view changes. Matters most on mobile, where the changed row can be scrolled out of view after reload.
- **D-15:** **Failure paths split by reachability.** "You are not signed up for this quest" becomes `TempData["Error"]` + redirect, because it is reachable *without tampering* (a stale modal after revoking the signup in another tab, or being dropped from a finalized quest). The **cross-group rejection stays a hard `BadRequest`**, because once the dropdown is group-scoped nothing legitimate can produce that post — and it keeps SIGNCHAR-07's test asserting on a rejection rather than the weaker "redirected and happened not to mutate".
- **D-16:** The split needs a plain-language comment explaining *why* the two failures differ. Per CLAUDE.md's Code Comments rule: no phase/requirement IDs in source.

### Claude's Discretion

Not discussed — planner decides:
- Whether the trigger button lives inside `_CharacterSelectModal.cshtml` or stays in each host view (one modal instance per page serving multiple trigger sites means the partial probably renders the modal only).
- How the modal learns the current pick per invocation — the roadmap names `show.bs.modal` + `event.relatedTarget` as the established idiom here (`Shop/Index.cshtml`, `ShopManagement/Index.cshtml` and their `.Mobile` counterparts).
- How the Remove button posts `null` without colliding with the `characterId` select or tripping `required` (a submit button sharing the field name would post the value twice — avoid).
- Exact status-suffix wording, and ordering of Active vs inactive entries in the list.
- Toast message wording for swap vs clear.
- Test structure for SIGNCHAR-07 beyond "two distinct groups, asserts rejection".

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — Phase 72 entry: goal, 5 success criteria, scope notes (service-layer only, desktop+mobile in one phase, partial-first internal order), decisions locked before planning, and the 4 named risks this phase must avoid
- `.planning/REQUIREMENTS.md` — SIGNCHAR-01 … SIGNCHAR-07 in full, plus the **Out of Scope** section, which carries the reasoning behind six exclusions relevant to this phase (DM-side editing, email notification, audit trail, confirmation-before-swap, role restriction, server-side auto-clear)

### Project constraints and conventions
- `.planning/PROJECT.md` — Constraints (no user-facing regressions; ASP.NET Core 10 MVC + SQL Server + EF Core; 100 emails/day relay budget) and the Key Decisions table
- `CLAUDE.md` — **UI/UX Design Guidelines** (modern-card pattern, filled colored buttons, FontAwesome with `me-2`, `d-flex justify-content-between` footer layout), **Code Comments** rule (no GSD tracking IDs in source), **Branching** rule (never commit to `main`), **RIP Lookup Protocol**
- `.planning/codebase/CONVENTIONS.md` — naming patterns, AutoMapper patterns, code style
- `.planning/codebase/STRUCTURE.md` — view and controller layout

### Code touched by this phase
- `QuestBoard.Service/Views/Quest/Details.cshtml` — the two character cells (`:116`–`:145` finalized, `:232`–`:260` waitlist), the signup-time selects (`:333`, `:419`), and the existing `#addCharacterModal` (`:820`–`:862`) that becomes the shared partial
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` — participant rows (`:215`) and waitlist rows (`:243`) rendering the character as bare `<small class="text-muted">`; signup-time select at `:295`
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `UpdateSignupCharacter` (`:520`–`:555`) and the `ViewBag.UserCharacters` population in the Details action (`:326`–`:337`)
- `QuestBoard.Domain/Models/Character.cs` — `Status` (`:25`), `OwnerId` (`:35`), `GroupId` (`:44`)
- `QuestBoard.Service/Views/Shared/_Toasts.cshtml` — the site-wide toast partial, already wired into `_Layout.cshtml` and `_Layout.Mobile.cshtml`

### Precedent to follow
- `QuestBoard.Service/Views/Shop/Index.cshtml` and `QuestBoard.Service/Views/ShopManagement/Index.cshtml` (+ their `.Mobile` counterparts) — the established `show.bs.modal` + `event.relatedTarget` idiom for a single modal serving many trigger sites
- `.planning/milestones/v6.1-phases/42-site-wide-toast-notification-redesign/` — the phase that standardised `TempData["Success"|"Error"|"Warning"|"Info"]` → `_Toasts.cshtml`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`#addCharacterModal`** (`Details.cshtml:820`) — already posts to `UpdateSignupCharacter` with a `questId` hidden field and a `characterId` select. This is the block that gets extracted into `_CharacterSelectModal.cshtml` and then reused by mobile. Its submit button currently reads "Add Character" and its title "Add Character to Signup" — both need to serve add/change/clear.
- **`_Toasts.cshtml`** — rendered by `_Layout.cshtml`, `_Layout.Mobile.cshtml`, and `_Layout.GroupPicker.cshtml`. Reading `TempData["Success"]`/`["Error"]` requires zero view changes.
- **`revokeSignup()`** in `Details.cshtml` — the existing `confirm()` guard pattern D-07 mirrors.
- **`UpdateSignupCharacterAsync`** on `IPlayerSignupService` — the nullable path (controller → service → repository override → `PlayerSignupEntity.CharacterId`) already works end to end. Clearing is an existing, supported server-side code path; no Domain, Repository, or migration change is needed.

### Established Patterns
- **Platform-split views** — every Quest view has a `.cshtml` / `.Mobile.cshtml` pair selected server-side. Mobile markup that never renders is a recorded live failure in this repo (`_Layout.Platform.Mobile.cshtml`), so mobile work must be verified with a **real mobile User-Agent, not devtools emulation**.
- **`ViewBag` for the character list** — one writer (`QuestController.cs:337`), six readers. D-12 widens it at the writer.
- **`event.relatedTarget` modal priming** — established in the Shop views for exactly this "one modal, many triggers" shape.
- **Near-duplicate view blocks are this project's recorded drift class** — PROJECT.md blames it for the `Characters/Edit.cshtml` `classIndex` bug and three other instances. `Details.cshtml` already holds two structurally identical character cells; mobile would make a third. Extract the partial rather than hand-copying a fourth.

### Integration Points
- `QuestController.Details` — populates `ViewBag.UserCharacters` (widen + group-scope here, D-12/D-13)
- `QuestController.UpdateSignupCharacter` — drop the `Status` check (D-10), add the `GroupId` check (D-13), add success `TempData` (D-14), split the failure responses (D-15)
- Both Details views' character cells/rows — add the pencil trigger (D-01/D-02) behind the new visibility rule (D-03)
- New `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml` — must exist before either host view can call it (roadmap's stated internal order)

</code_context>

<specifics>
## Specific Ideas

- The pencil must sit in **exactly the position the green `+` occupies today** on desktop — same cell, same `btn-sm` size — so the two states read as one control family rather than two bolt-ons.
- On mobile the pencil goes on the **second line next to the character name**, not as a third flex column and not as a whole-row tap target. Explicitly rejected: making the row itself clickable (invisible affordance, new pattern here).
- Explicitly rejected for clearing: a blank `-- No character --` dropdown entry, and dropping `required`. The operator wanted clearing to be an **explicit, unmistakable act**, not a passive selection.
- Explicitly rejected for the inactive character: `<optgroup>` (no precedent in this codebase) and a read-only "Currently: X (Retired)" line with a `— Keep current —` sentinel (would need a third state alongside `null`-means-clear).

</specifics>

<deferred>
## Deferred Ideas

- **SIGNCHAR-08 — "recently changed" indicator on the DM's Manage page.** Already logged in `.planning/REQUIREMENTS.md` → Future Requirements. Surfaced during v9.0 research as the cheap alternative to an email notification; the operator chose no notification for v9.0.
- **The shared partial's exact boundary** — whether the trigger button is part of `_CharacterSelectModal.cshtml` or stays in each host view. Left to the planner rather than decided here.
- **What the mobile User-Agent verification must prove** — raised as an open area, not explored. The roadmap requires real-UA verification; the specific assertions are the planner's to define.

</deferred>

---

*Phase: 72-Change Character on an Existing Signup*
*Context gathered: 2026-08-25*
