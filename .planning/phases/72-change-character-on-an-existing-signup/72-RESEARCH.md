# Phase 72: Change Character on an Existing Signup - Research

**Researched:** 2026-08-25
**Domain:** ASP.NET Core 10 MVC — Razor partial extraction, Bootstrap 5 modal reuse, EF Core query-filter security
**Confidence:** HIGH

## Summary

This phase is a pure service-layer + view feature on an already-working data path. The nullable-`CharacterId` round trip (`UpdateSignupCharacter` → `PlayerSignupService.UpdateSignupCharacterAsync` → `PlayerSignupRepository.UpdateAsync` override → `PlayerSignupEntity.CharacterId`) was read end to end and works exactly as CONTEXT.md describes — no Domain/Repository/migration change is needed anywhere in this phase.

Every line-number claim in CONTEXT.md's `<canonical_refs>` and the phase's "Verification focus" list was checked against the live tree and matches exactly, with one exception (`#addCharacterModal`'s closing tag is line 863, not 862 — trivial, not worth re-litigating). All six `ViewBag.UserCharacters` read sites were enumerated. The `show.bs.modal` + `event.relatedTarget` idiom was extracted verbatim from `ShopManagement/Index.cshtml`'s `denyModal`, which is a closer structural match than `Shop/Index.cshtml`'s `itemDetailsModal` (the deny modal primes a *form* from `data-item-id`/`data-item-name` attributes, exactly the shape this phase needs for priming a character-select form from a signup's current character).

**One finding changes the risk picture for D-13/SIGNCHAR-07 and must reach the planner:** `CharacterEntity` already carries a global EF Core `HasQueryFilter` scoped to the caller's active group (`QuestBoardContext.cs:328-331`), applied automatically to every `DbContext.Characters` query — including both `GetCharactersByOwnerIdAsync` (populates `ViewBag.UserCharacters`) and `GetCharacterWithDetailsAsync` (validates `UpdateSignupCharacter`'s POST). This means a character belonging to a group other than the caller's active group is **already unreachable** through either code path today: `GetCharacterWithDetailsAsync` returns `null` for it, and the existing `character == null || ...` check in `UpdateSignupCharacter` already returns `BadRequest("Invalid character selection.")` for it. The "gap" narrative in CONTEXT.md/DISCUSSION-LOG (a scout reading only the LINQ `.Where(c => c.OwnerId == ownerId)` clause, which doesn't show model-level query filters) does not hold up against the full picture. This does **not** make D-13 unworkable — the codebase's own established convention (see `RemovePlayerSignup`'s explicit `GroupId` check at `QuestController.cs:642`, despite `Quest` also carrying a query filter) is exactly this kind of defense-in-depth belt-and-suspenders check even when a filter already covers it. But the planner needs to know the explicit check will not be exercised by a naive integration test — see the Security Domain section below for what SIGNCHAR-07's test must actually prove.

**Primary recommendation:** Extract `_CharacterSelectModal.cshtml` first (self-contained modal + `show.bs.modal` script, using the `denyModal`/`data-bs-*` priming idiom below), wire `Details.cshtml` second, wire `Details.Mobile.cshtml` third — exactly the roadmap's stated internal order — then widen `ViewBag.UserCharacters` at its single source and add the (defense-in-depth, not gap-closing) `GroupId` check to `UpdateSignupCharacter`.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SIGNCHAR-01 | Change character on desktop Details, both finalized and waitlist tables | Character cells verified at `Details.cshtml:116-145` (finalized) and `:232-260` (waitlist); pencil trigger replaces/joins the existing `+` button pattern |
| SIGNCHAR-02 | Change character on mobile Details | Mobile rows verified at `Details.Mobile.cshtml:215` (participant) and `:243` (waitlist) — both currently bare `<small>`, zero control; `_CharacterSelectModal.cshtml` partial reused here per roadmap's internal order |
| SIGNCHAR-03 | Clear to no character, both platforms | `UpdateSignupCharacterAsync` already accepts `null` end-to-end (verified: Service → Repository override → Entity); D-05's dedicated Remove button avoids the `required`-attribute collision — see "Don't Hand-Roll" / Code Examples below |
| SIGNCHAR-04 | Inactive character shown as current selection, status-labelled | `CharacterStatus` enum verified (`Active=0, Retired=1, Dead=2`); D-09's "inject current pick as selected option" pattern structurally prevents the silent-wipe bug documented in `.planning/research/PITFALLS.md` Pitfall 5 |
| SIGNCHAR-05 | Works post-finalization, no time cutoff | `UpdateSignupCharacter` (`QuestController.cs:520-555`) verified to have **no** `IsFinalized` guard, unlike its sibling `UpdateSignup` (`:496-518`) which does — the asymmetry CONTEXT.md documents is real and already in code, nothing to build |
| SIGNCHAR-06 | Works for waitlisted signups, all 3 roles | `UpdateSignupCharacter` looks up the signup by `Player.Id` only — no `IsSelected`/`Role` gate exists, so waitlist and all roles already reach it unmodified |
| SIGNCHAR-07 | Cross-user/cross-group rejection, proven by automated test | `character.OwnerId != user.Id` check already exists and is real (same-group cross-user case). Cross-group case is **already blocked by the `CharacterEntity` global query filter**, not by any code this phase writes — see Security Domain section for how to write a test that proves something real |

</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Change/clear character trigger UI (pencil, Remove button) | Browser / Client (Razor view + Bootstrap JS) | — | Pure presentational affordance; no new client-side state beyond existing `show.bs.modal` idiom |
| Modal priming (populate select + current-value option) | Browser / Client | API / Backend (data source) | `event.relatedTarget` reads `data-bs-*` attributes set server-side at render time; no AJAX round trip needed |
| Character list scoping (owner + group + status) | API / Backend (`QuestController.Details` GET) | Database (EF Core query filter) | `ViewBag.UserCharacters` population is a controller responsibility; the group scope is *already* enforced one layer down by the EF Core model-level filter |
| Character selection validation on save | API / Backend (`UpdateSignupCharacter` POST) | Database (EF Core query filter) | Ownership/status logic belongs in the controller/service; group isolation is already guaranteed at the DbContext level and the explicit check is additive defense-in-depth |
| Signup character persistence | Database / Storage | — | `PlayerSignupEntity.CharacterId` — no schema change, already nullable |
| Toast feedback | Browser / Client (`_Toasts.cshtml`) | API / Backend (`TempData`) | Existing shared partial; backend sets `TempData["Success"/"Error"]`, partial renders it — zero new view wiring |

## Standard Stack

No new packages. This phase reuses:

| Library | Version (verified in repo) | Purpose | Source |
|---------|---------|---------|--------------|
| Bootstrap | 5.3.0 (bundle incl. Popper) | Modal, `show.bs.modal` event, toast | `_Layout.cshtml:12,223` — `[VERIFIED: codebase]` |
| ASP.NET Core MVC | net10.0 | Controller actions, Razor views, TempData | `QuestBoard.IntegrationTests.csproj:4` — `[VERIFIED: codebase]` |
| xunit.v3 + Microsoft.AspNetCore.Mvc.Testing | 3.2.2 / 10.0.9 | Integration test harness | `QuestBoard.IntegrationTests.csproj` — `[VERIFIED: codebase]` |
| EF Core InMemory | 10.0.9 | Test database provider | `QuestBoard.IntegrationTests.csproj:15` — `[VERIFIED: codebase]` |

**Installation:** None required — no `npm install` / `dotnet add package` needed anywhere in this phase.

## Package Legitimacy Audit

Not applicable. This phase installs zero external packages (confirmed against the roadmap's explicit scope note: "No new packages, no new JS library"). Section omitted per the protocol's own scope condition.

## Architecture Patterns

### System Architecture Diagram

```
[Player clicks pencil/+ icon in Details.cshtml or Details.Mobile.cshtml]
        │  (data-bs-toggle="modal" data-bs-target="#characterSelectModal"
        │   data-current-character-id="..." data-quest-id="...")
        ▼
[show.bs.modal listener on #characterSelectModal]
  reads event.relatedTarget.getAttribute(...)
  sets <form action>, pre-selects <select id="characterSelect"> to current value,
  shows/hides "Remove" button based on whether a character is currently set
        │
        ▼
[Player picks a character OR clicks "Remove" (native confirm() guard) OR Cancels]
        │  POST /Quest/UpdateSignupCharacter  (characterId=<id> or omitted/null)
        ▼
QuestController.UpdateSignupCharacter(questId, characterId?)
  ├─ loads quest, finds caller's PlayerSignup by Player.Id
  │    └─ not found → BadRequest("You are not signed up...") [D-15: could become TempData+redirect]
  ├─ if characterId.HasValue:
  │    characterService.GetCharacterWithDetailsAsync(characterId)
  │      └─ EF Core CharacterEntity query filter (GroupId == ActiveGroupId) already applied here
  │    ├─ null (wrong group, already filtered out) → BadRequest [existing behavior]
  │    ├─ OwnerId != caller → BadRequest [existing behavior]
  │    └─ (D-13 addition) explicit character.GroupId != activeGroupId → BadRequest [defense-in-depth]
  ├─ playerSignupService.UpdateSignupCharacterAsync(signupId, characterId)
  │    → PlayerSignupRepository.UpdateAsync override → entity.CharacterId = model.CharacterId
  ├─ (D-14 addition) TempData["Success"] = "..."
  └─ RedirectToAction("Details")
        ▼
QuestController.Details GET — re-fetches quest, re-populates ViewBag.UserCharacters
  (D-12: widened to all statuses; D-13: group-scoped — already redundant with the query
   filter, kept as defense-in-depth per codebase convention)
        ▼
Details.cshtml / Details.Mobile.cshtml re-render (view-location expander picks by IsMobile)
  _Toasts.cshtml (already wired into both _Layout.cshtml and _Layout.Mobile.cshtml)
  reads TempData["Success"]/["Error"] with zero new view code
```

### Recommended Project Structure

No new folders. One new shared partial:

```
QuestBoard.Service/Views/
├── Shared/
│   └── _CharacterSelectModal.cshtml   # NEW — extracted from Details.cshtml's #addCharacterModal
├── Quest/
│   ├── Details.cshtml                 # MODIFIED — pencil triggers, calls the partial
│   └── Details.Mobile.cshtml          # MODIFIED — pencil triggers (new UI), calls the partial
```

### Pattern 1: `event.relatedTarget` modal priming (established idiom, verbatim source)

**What:** A single modal instance serves many trigger buttons/rows. Each trigger carries `data-bs-toggle="modal" data-bs-target="#modalId"` plus custom `data-*` attributes. A `show.bs.modal` listener on the modal reads `event.relatedTarget` (the exact element that triggered the show) to prime the modal's form.

**When to use:** Exactly this phase's shape — one `_CharacterSelectModal.cshtml` instance per Details page, opened from N different rows (finalized table rows, waitlist table rows, mobile rows), each needing to prime a different `questId`/current-`characterId`.

**Verbatim precedent** — `ShopManagement/Index.cshtml` (closest structural match: primes a *form action* and multiple fields from data attributes, not just an AJAX URL like the Shop `itemDetailsModal` variant):

Trigger button (`ShopManagement/Index.cshtml:93-97`):
```cshtml
<button type="button" class="btn btn-danger btn-sm btn-action" title="Deny"
        data-bs-toggle="modal" data-bs-target="#denyModal"
        data-item-id="@item.Id"
        data-item-name="@item.Name">
    <i class="fas fa-times"></i>
</button>
```

Modal + form (`ShopManagement/Index.cshtml:455-492`):
```cshtml
<div class="modal fade" id="denyModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content bg-dark text-light">
            <form id="denyForm" method="post">
                @Html.AntiForgeryToken()
                <div class="modal-header border-secondary">
                    <h5 class="modal-title">Deny Item</h5>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                </div>
                <div class="modal-body">
                    <p>You are about to deny: <strong id="denyItemName"></strong></p>
                    ...
                </div>
                <div class="modal-footer border-secondary">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                    <button type="submit" class="btn btn-danger">Deny Item</button>
                </div>
            </form>
        </div>
    </div>
</div>
```

Priming script (`ShopManagement/Index.cshtml:501-517`):
```javascript
document.addEventListener('DOMContentLoaded', function() {
    const denyModal = document.getElementById('denyModal');
    if (denyModal) {
        denyModal.addEventListener('show.bs.modal', function(event) {
            const button = event.relatedTarget;
            const itemId = button.getAttribute('data-item-id');
            const itemName = button.getAttribute('data-item-name');

            const form = document.getElementById('denyForm');
            form.action = '/ShopManagement/Deny/' + itemId;

            document.getElementById('denyItemName').textContent = itemName;
            document.getElementById('denialReason').value = '';
        });
    }
});
```

**For this phase:** the trigger button (pencil or `+`) carries `data-quest-id="@Model.Quest.Id"` and `data-current-character-id="@participant.Character?.Id"` (empty string when null). The `show.bs.modal` listener sets the hidden `questId` field and `characterSelect.value = currentCharacterId` (this works cleanly under D-09 because the current character is *always* rendered as an option in the select, including Retired/Dead ones — there is no missing-option fallback case to guard against). It also toggles the Remove button's visibility based on whether `data-current-character-id` is non-empty (D-08).

### Anti-Patterns to Avoid

- **Re-populating the select from a second, narrower list for the modal only.** DISCUSSION-LOG's Q4 explicitly rejected this ("Second list for the modal only") in favor of widening `ViewBag.UserCharacters` at its one source — don't reintroduce the two-list split.
- **Relying on `select.value = characterId` alone for the current-selection pre-fill without D-09's "inject as option" step.** This is Pitfall 5 from `.planning/research/PITFALLS.md` — a browser silently falls back to the first `<option>` when the target value has no matching option, which is exactly the silent-wipe bug SIGNCHAR-04 exists to prevent.
- **Adding `IgnoreQueryFilters()` anywhere in this phase's touched code.** Doing so would remove the group-scoping protection currently provided for free by the model-level filter — there is no reason to add it, and the research above confirms today's code (correctly) never does.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Modal-per-trigger data passing | A custom JS event bus / global state object to track "which row was clicked" | `event.relatedTarget` + `data-bs-*` attributes | Bootstrap 5 native mechanism, already the established idiom in this codebase (Shop, ShopManagement, both `.Mobile` variants) |
| Destructive-action confirmation | A custom modal-within-modal confirm dialog | Native `confirm()` | Matches `revokeSignup()`'s existing idiom verbatim (`Details.cshtml`) — D-07 locks this |
| Group-scoped character validation | A hand-rolled `WHERE GroupId = @activeGroupId` raw SQL check bolted onto `UpdateSignupCharacter` | The existing `IActiveGroupContext.ActiveGroupId` (already DI'd into `QuestController`) compared against `character.GroupId` | `activeGroupContext` is already a constructor parameter (`QuestController.cs:21`); no new DI wiring needed, and the same `activeGroupContext.ActiveGroupId is not { } groupId \|\| x.GroupId != groupId` idiom is already used at `QuestController.cs:642` |

**Key insight:** Nearly everything this phase needs is already an established idiom somewhere else in this exact codebase. The work is almost entirely "wire the same shapes to a new call site," not "invent a new pattern."

## Common Pitfalls

### Pitfall 1: Believing the group-scoping "gap" requires new server-side enforcement to close

**What goes wrong:** Treating D-13's `GroupId` check on `UpdateSignupCharacter` as the thing that makes cross-group assignment impossible, and therefore writing a SIGNCHAR-07 test that will trivially pass regardless of whether that check is even implemented — giving false confidence that the *new* code is what's being tested.

**Why it happens:** `CharacterEntity` carries a global EF Core `HasQueryFilter` (`QuestBoardContext.cs:328-331`) that scopes every `DbContext.Characters` query to `activeGroupContext.ActiveGroupId`. This is invisible to a code read that only looks at repository method bodies (`GetCharactersByOwnerIdAsync`, `GetCharacterWithDetailsAsync`) — both look owner-only at the LINQ level, but the filter is applied underneath by EF Core automatically. A cross-group `characterId` therefore already resolves to `null` in `GetCharacterWithDetailsAsync` today, and `UpdateSignupCharacter`'s pre-existing `character == null` check already returns `BadRequest` for it — before any Phase 72 code exists.

**How to avoid:** Add the explicit `GroupId` check anyway (matches the codebase's established belt-and-suspenders convention — see `RemovePlayerSignup`'s explicit check despite `Quest` also having a filter), but do not present it as gap-closing in code comments (per D-16, no phase-ID references anyway — but also don't claim in a plain-language comment that this check is "the" protection, since the filter is the actual first line of defense). Write the SIGNCHAR-07 test to assert on the **actually reachable** distinguishing case — same-group, different-owner — as the primary proof (this exercises real, new-to-this-phase code: the widened, all-statuses character list plus the ownership check), and treat the cross-group case as a regression test that documents current + future protection rather than a test that proves new code.

**Warning signs:** A SIGNCHAR-07 test that passes before the `GroupId` check is even added to `UpdateSignupCharacter`.

### Pitfall 2: Silent character wipe via a naive select pre-fill (Retired/Dead characters)

**What goes wrong:** `select.value = characterId` finds no matching `<option>` for a character excluded from the option list, silently falls back to the first option, and a Save the user thought was a no-op actually swaps their character.

**Why it happens:** Today's `ViewBag.UserCharacters` is `CharacterStatus.Active`-only (`QuestController.cs:330`). A signup's `Character` navigation has no such filter, so a Retired/Dead character can be the signup's current value without being in the dropdown's option list.

**How to avoid:** D-09's "inject current pick as a selected, status-labelled option" — the option list must always include the signup's actual current character, regardless of status, before any pre-selection logic runs.

**Warning signs:** Retire a character currently assigned to a signup, open the change modal, verify the dropdown's initial selection matches the row's displayed character rather than silently jumping to a different one.

### Pitfall 3: The `required` attribute blocking a null-clearing submit

**What goes wrong:** Reusing `#addCharacterModal`'s form verbatim for the Remove action fails client-side validation because `<select name="characterId" required>` blocks submission when no option is selected.

**Why it happens:** The existing modal was built for add-only, where "must pick something" is correct. D-05 keeps `required` for the add/change path but needs a distinct path for Remove.

**How to avoid:** Remove is its own `<button>` (not a submit of the same form/field) — see Code Examples below for the exact mechanism recommended.

**Warning signs:** Clicking Remove does nothing, or the browser shows the native "Please select an item in the list" validation bubble.

### Pitfall 4: Mobile markup that never renders

**What goes wrong:** New mobile UI is written but never actually served, because the platform-split mechanism silently falls through to desktop.

**Why it happens:** This project has a recorded live case (`Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` — dead code because that area's `_ViewStart.cshtml` never selects it, per `.planning/PROJECT.md` Known Issues). The mechanism here (verified, see Validation Architecture below) is `MobileDetectionMiddleware` (User-Agent keyword match → `context.Items["IsMobile"]`) + `MobileViewLocationExpander` (an `IViewLocationExpander` that tries `X.Mobile.cshtml` first when `IsMobile == true`). Both are correctly wired for the `Quest` controller/views already (mobile Details views exist and render today), so the risk here is specifically about *new* markup added inside the mobile file being verified, not the routing mechanism itself.

**How to avoid:** Verify with a real mobile User-Agent string sent via an `HttpRequestMessage` header (the existing `MobileViewsTests.cs` pattern — see Validation Architecture), not browser devtools emulation (which some environments don't consistently propagate through the test harness or a real reverse proxy the same way).

**Warning signs:** A UAT pass that only used Chrome devtools' device toolbar, not an actual `User-Agent` header check.

## Code Examples

### Extracted shared partial skeleton

```cshtml
@* Source: extracted from Details.cshtml:820-863 (#addCharacterModal), generalized for
   both add and change/clear, following the ShopManagement/Index.cshtml denyModal idiom
   for show.bs.modal + event.relatedTarget priming. *@
<div class="modal fade" id="characterSelectModal" tabindex="-1" aria-labelledby="characterSelectModalLabel" aria-hidden="true">
    <div class="modal-dialog">
        <div class="modal-content bg-dark text-light">
            <div class="modal-header border-secondary">
                <h5 class="modal-title" id="characterSelectModalLabel">
                    <i class="fas fa-user-plus me-2"></i><span id="characterSelectModalTitle">Add Character to Signup</span>
                </h5>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>
            <form asp-action="UpdateSignupCharacter" method="post" id="characterSelectForm">
                <div class="modal-body">
                    <input type="hidden" name="questId" id="characterSelectQuestId" value="" />
                    <div class="mb-3">
                        <label for="characterSelect" class="form-label">Select Character <span class="text-danger">*</span></label>
                        <select name="characterId" id="characterSelect" class="form-select" required>
                            <option value="">-- Select a character --</option>
                            @foreach (var character in ViewBag.UserCharacters as List<QuestBoard.Domain.Models.Character> ?? new List<QuestBoard.Domain.Models.Character>())
                            {
                                var classList = string.Join(", ", character.Classes.Select(c => $"{c.Class} {c.ClassLevel}"));
                                var statusSuffix = character.Status == QuestBoard.Domain.Enums.CharacterStatus.Active ? "" : $" ({character.Status})";
                                <option value="@character.Id">@character.Name - Level @character.Level (@classList)@statusSuffix</option>
                            }
                        </select>
                    </div>
                </div>
                <div class="modal-footer border-secondary d-flex justify-content-between">
                    <button type="button" class="btn btn-outline-danger d-none" id="characterRemoveBtn">
                        <i class="fas fa-times me-2"></i>Remove Character
                    </button>
                    <div>
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                        <button type="submit" class="btn btn-success">
                            <i class="fas fa-check me-2"></i>Save
                        </button>
                    </div>
                </div>
            </form>
        </div>
    </div>
</div>
```

### Removing without tripping `required` or double-posting `characterId`

The collision CONTEXT.md flags — "a submit button sharing the field name would post the value twice" — is avoided by making Remove a plain `type="button"` that clears `required`, sets the select's value to empty, disables it (so it doesn't post at all), then submits the form via JS rather than as a second named submit button:

```javascript
document.getElementById('characterRemoveBtn').addEventListener('click', function () {
    if (!confirm('Remove this character from your signup?')) return;
    const select = document.getElementById('characterSelect');
    select.required = false;
    select.value = '';
    select.disabled = true; // disabled fields are never posted — characterId arrives as absent, not empty string
    document.getElementById('characterSelectForm').submit();
});
```

A disabled `<select>` is excluded from form submission entirely by the browser, so the controller receives `characterId = null` via model binding (`int?`) — matching the already-working null path, no server change needed to support this specific mechanism.

### Priming from a trigger (desktop finalized-table cell)

```cshtml
@if (participant.Character != null)
{
    <div class="d-flex align-items-center">
        <img ... />
        <span>@participant.Character.Name</span>
        @if (isCurrentUser)
        {
            <button type="button" class="btn btn-sm btn-primary ms-2"
                    data-bs-toggle="modal" data-bs-target="#characterSelectModal"
                    data-quest-id="@Model.Quest?.Id"
                    data-current-character-id="@participant.Character.Id"
                    title="Change character">
                <i class="fas fa-pencil"></i>
            </button>
        }
    </div>
}
else
{
    <div class="d-flex align-items-center gap-2">
        <span class="text-muted fst-italic">No character</span>
        @if (isCurrentUser && ViewBag.UserCharacters != null && ((List<Character>)ViewBag.UserCharacters).Any())
        {
            <button type="button" class="btn btn-sm btn-success"
                    data-bs-toggle="modal" data-bs-target="#characterSelectModal"
                    data-quest-id="@Model.Quest?.Id"
                    data-current-character-id=""
                    title="Add character">
                <i class="fas fa-plus"></i>
            </button>
        }
    </div>
}
```

Note: D-03's visibility rule ("renders when a character is set OR the player has at least one selectable character") replaces the `.Any()` gate shown above for the empty-state `+` — the pencil for the filled state has no such gate at all (a character is already set, so there is always something to do: change it or clear it).

### The existing GroupId check idiom to mirror in `UpdateSignupCharacter`

```csharp
// From RemovePlayerSignup, QuestController.cs:642 — the established shape for an explicit,
// belt-and-suspenders group check alongside a query-filter-scoped entity.
if (activeGroupContext.ActiveGroupId is not { } groupId || signup.Quest.GroupId != groupId)
{
    return NotFound();
}
```

For `UpdateSignupCharacter`, the equivalent check is on `character.GroupId`, and per D-15 the response is `BadRequest`, not `NotFound` (this action already uses `BadRequest` for its other rejection paths, and D-15 locks `BadRequest` specifically for the cross-group case):

```csharp
if (characterId.HasValue)
{
    var character = await characterService.GetCharacterWithDetailsAsync(characterId.Value);
    if (character == null || character.OwnerId != user.Id)
    {
        return BadRequest("Invalid character selection.");
    }
    // Explicit group check kept alongside the query-filter's own scoping (CharacterEntity's
    // HasQueryFilter already excludes other groups' characters at the database layer) so this
    // action does not silently depend on that filter alone for cross-tenant safety.
    if (activeGroupContext.ActiveGroupId is not { } groupId || character.GroupId != groupId)
    {
        return BadRequest("Invalid character selection.");
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Character validation on write checked `Status == Active` | This phase drops the `Status` check entirely (D-10) | This phase | Retired/Dead characters become newly assignable to signups server-side; deliberate, locked |
| `ViewBag.UserCharacters` filtered to Active only | Widened to all owned, active-group characters, status-labelled (D-11/D-12) | This phase | Six read sites change simultaneously; signup-time selects also widen (accepted spillover) |

**Deprecated/outdated:** None — no prior version of this feature existed to deprecate; this is additive.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ShopManagement/Index.cshtml`'s `denyModal` idiom is the best precedent to copy (over `Shop/Index.cshtml`'s AJAX-URL variant) because it primes a form + hidden fields from `data-*` attributes rather than fetching remote content | Architecture Patterns | Low — both idioms were read in full; the recommendation is a judgment call on closeness of fit, not a factual claim that could be wrong |
| A2 | A `disabled` `<select>` is excluded from form submission by every browser QuestBoard needs to support, so `characterId` arrives as `null` via `int?` model binding when Remove is clicked | Code Examples | Low-Medium — this is standard HTML forms behavior (`[CITED: MDN — disabled form controls are not submitted]`), not verified against this specific codebase's client compatibility target, but is extremely well-established web platform behavior |

**All claims tagged `[VERIFIED: codebase]` were confirmed directly against files in this repository during this research session** — the two entries above are the only claims not backed by a direct read of this codebase's own code or tests.

## Open Questions

1. **Exact status-suffix wording and Active-vs-inactive ordering (D-12/D-11 left to planner's discretion).**
   - What we know: `CharacterStatus` enum values are `Active=0, Retired=1, Dead=2`; existing option text pattern is `"{Name} - Level {Level} ({classList})"`.
   - What's unclear: whether inactive entries sort to the bottom of the list or stay in the existing owner-query order (`Role == Main` first, then `Status == Active` first, then `Name` — confirmed in `CharacterRepository.GetCharactersByOwnerIdAsync`'s `.OrderByDescending(c => c.Role == 0).ThenByDescending(c => c.Status == 0).ThenBy(c => c.Name)`).
   - Recommendation: the repository's existing ordering already sorts Active before inactive, so no extra sort is needed in the view — Active characters will naturally appear first in the option list without any Razor-side ordering logic. This is a bonus discovery: D-11's "list all owned characters, status-labelled" gets its ordering for free from the existing repository query.

2. **Whether the mobile pencil belongs inside `_CharacterSelectModal.cshtml`'s trigger markup or stays entirely in the host view (left to planner per CONTEXT.md's "Claude's Discretion").**
   - What we know: `ARCHITECTURE.md`'s recommended build order treats the partial as "modal markup + `show.bs.modal` script" only (self-contained), with trigger buttons added separately in each host view.
   - Recommendation: keep the partial modal-only (per the milestone-level research's own recommended order) — trigger buttons stay in `Details.cshtml`/`Details.Mobile.cshtml` since desktop and mobile need visually different trigger placement (D-01 cell-inline vs. D-02 second-line-inline) that doesn't fit a single shared trigger component anyway.

## Environment Availability

Skipped — this phase has no external dependencies beyond the already-installed .NET 10 SDK, EF Core, and Bootstrap CDN assets already loaded by both layouts. No new tool, service, or runtime is introduced.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + Microsoft.AspNetCore.Mvc.Testing 10.0.9 |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (no separate xunit.runner.json found) |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController"` |
| Full suite command | `dotnet test` (per `.planning/config.json` `workflow.test_command`) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SIGNCHAR-01 | POST with new characterId updates finalized-table signup's character | integration | `dotnet test --filter "FullyQualifiedName~UpdateSignupCharacter"` | ❌ Wave 0 — no existing `UpdateSignupCharacter` integration tests found (only `JoinFinalizedQuest` and `UpdateSignup` are covered today) |
| SIGNCHAR-02 | Same, verified via mobile User-Agent request | integration | same filter, with `MobileUserAgent` header per `MobileViewsTests.cs` pattern | ❌ Wave 0 |
| SIGNCHAR-03 | POST with no characterId selected clears `CharacterId` to null in DB | integration | same filter, assert via `context.PlayerSignups...CharacterId.Should().BeNull()` | ❌ Wave 0 |
| SIGNCHAR-04 | Retired/Dead character shown as current selection, unchanged on no-op save | integration + manual (dropdown rendering) | `dotnet test --filter "FullyQualifiedName~RetiredCharacter"` for the persistence half; UAT for the rendered-option-selected half | ❌ Wave 0 |
| SIGNCHAR-05 | Works after finalization | integration | reuse `CreateTestQuestAsync(..., isFinalized: true, finalizedDate: ...)` seeding pattern from `QuestJoinFinalizedQuestTests.cs` | ❌ Wave 0 |
| SIGNCHAR-06 | Works for waitlisted signups, all 3 roles | integration | parametrized over `signupRole` (0/1/2) and `isSelected: false` via `CreatePlayerSignupAsync` | ❌ Wave 0 |
| SIGNCHAR-07 | Cross-user (same group) rejected; cross-group rejected | integration | `dotnet test --filter "FullyQualifiedName~CrossGroup\|FullyQualifiedName~AnotherUser"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController|FullyQualifiedName~UpdateSignupCharacter"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] New test file `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs` — no existing test file covers `UpdateSignupCharacter` at all today (confirmed via grep across `QuestBoard.IntegrationTests`); model its fixture usage on `QuestJoinFinalizedQuestTests.cs` (same controller, adjacent action, already uses `TestDataHelper.CreateTestQuestAsync`/`CreatePlayerSignupAsync`/`AuthenticationHelper.CreateAuthenticatedClientWithUserAsync`).
- [ ] For the cross-group case specifically: model on `TenantIsolationTests.cs`'s `factory.TestGroupContext.ActiveGroupId` mutable-singleton pattern (not `SeedCampaignGroupAsync` + real membership) — it is the fastest, most direct way to seed a character under `GroupId=2` (via `factory.Database.CreateContext()`, which bypasses the query filter for writes) then make the authenticated request scoped to `ActiveGroupId=1`, and it is the only pattern in this codebase actually used to test the query-filter boundary.
- [ ] `CreateTestCharacterAsync(..., int groupId = 1)` in `TestDataHelper.cs` already accepts `groupId` and `status` — reuse it directly for both the cross-group and Retired/Dead test fixtures; no new helper needed.
- [ ] Framework install: none — everything is already referenced in `QuestBoard.IntegrationTests.csproj`.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V4 Access Control | yes | Ownership check (`character.OwnerId != user.Id`) — existing; tenant/group isolation via EF Core `HasQueryFilter` (existing) + explicit `GroupId` check (this phase, defense-in-depth) |
| V5 Input Validation | yes | `int?` model binding on `characterId`; server never trusts a client-supplied signup id (existing pattern: signup is always looked up via `quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id)`, never by a posted signup id) |
| V2 Authentication | yes | `[Authorize]` on `UpdateSignupCharacter` — existing, unchanged |
| V3 Session Management | yes | `ActiveGroupId` resolved from ASP.NET Core Session (`ActiveGroupContextService`) — existing, unchanged |
| V6 Cryptography | no | Not applicable to this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| IDOR — assigning another user's character via a hand-crafted `characterId` POST | Tampering / Elevation of Privilege | `character.OwnerId != user.Id` check — already exists, real, and testable (same-group case is the meaningful SIGNCHAR-07 test) |
| Cross-tenant data leak — assigning a character from a group the caller isn't currently viewing | Information Disclosure / Tampering | **Already enforced by the `CharacterEntity` global `HasQueryFilter`** (`QuestBoardContext.cs:328-331`) before any Phase 72 code runs; this phase's explicit `GroupId` check is additive defense-in-depth, not the primary control — write the test to document this, not to imply the explicit check is what blocks it |
| CSRF on the character-change POST | Tampering | `[ValidateAntiForgeryToken]` — already present on `UpdateSignupCharacter`, unchanged |

**Why the explicit `GroupId` check still belongs in the plan even though it's not load-bearing today:** query filters are a single point of failure — if a future refactor ever calls `.IgnoreQueryFilters()` on a `Characters` query (as several `SuperAdmin`-facing flows already do for `Quest`/`ShopItem`, per the `QuestBoardContext.cs` comments), the explicit check is what keeps this specific action safe. This matches the codebase's own stated rationale for `RemovePlayerSignup`'s explicit check. Frame it in the plan and in the D-16 code comment as insurance against filter regression, not as closing a currently-open hole — because there isn't one to close in `UpdateSignupCharacter` today.

## Sources

### Primary (HIGH confidence — direct codebase reads, this session)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:278-370` — all `HasQueryFilter` declarations, including the `CharacterEntity` filter central to this research's key finding
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (full file sections read: 1-40, 300-580, 600-670) — `Details` GET/POST, `JoinFinalizedQuest`, `UpdateSignup`, `UpdateSignupCharacter`, `RevokeSignup`, `RemovePlayerSignup`
- `QuestBoard.Domain/Models/Character.cs`, `QuestBoard.Domain/Enums/CharacterStatus.cs` — `Status`, `OwnerId`, `GroupId` fields and enum values
- `QuestBoard.Repository/CharacterRepository.cs` — `GetCharactersByOwnerIdAsync`, `GetCharacterWithDetailsAsync` full bodies
- `QuestBoard.Domain/Services/PlayerSignupService.cs`, `QuestBoard.Repository/PlayerSignupRepository.cs` — full nullable-`CharacterId` write path
- `QuestBoard.Service/Views/Quest/Details.cshtml`, `Details.Mobile.cshtml` — character cells, signup-time selects, `#addCharacterModal`
- `QuestBoard.Service/Views/Shop/Index.cshtml`, `ShopManagement/Index.cshtml` (+ `.Mobile` variants) — `show.bs.modal`/`event.relatedTarget` idiom
- `QuestBoard.Service/Middleware/MobileDetectionMiddleware.cs`, `QuestBoard.Service/ViewExpanders/MobileViewLocationExpander.cs` — mobile view-selection mechanism
- `QuestBoard.Service/Views/Shared/_Toasts.cshtml`, `_Layout.cshtml`, `_Layout.Mobile.cshtml` — toast plumbing
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`, `Controllers/QuestJoinFinalizedQuestTests.cs`, `Mobile/MobileViewsTests.cs`, `Helpers/TestDataHelper.cs`, `Helpers/AuthenticationHelper.cs` — test infrastructure and fixture patterns

### Secondary (MEDIUM confidence)
- `.planning/research/ARCHITECTURE.md`, `.planning/research/PITFALLS.md` (milestone-level v9.0 research, produced prior to this phase's discuss-phase) — cross-checked against this session's direct code reads; largely consistent except for the query-filter finding above, which that research did not surface

### Tertiary (LOW confidence)
- None — this phase required no external web research; every claim was verified directly against the working tree.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, all versions read directly from `.csproj`/`_Layout.cshtml`
- Architecture: HIGH — every pattern cited has a verbatim, verified source in this exact codebase
- Pitfalls: HIGH — Pitfall 1 (query filter) is a first-hand finding from this session's own `OnModelCreating` read, not inherited from prior research
- Security: HIGH — the load-bearing claim (CharacterEntity query filter) was read directly, not assumed

**Research date:** 2026-08-25
**Valid until:** No expiry driver — this is a closed-codebase research pass with no external/ecosystem dependency; stays valid until the `QuestBoardContext` model or `UpdateSignupCharacter` action changes.
