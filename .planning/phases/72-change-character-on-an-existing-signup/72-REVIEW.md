---
phase: 72-change-character-on-an-existing-signup
reviewed: 2026-08-26T00:00:00Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs
  - QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs
  - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
  - QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml
  - QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs
findings:
  critical: 0
  warning: 8
  info: 7
  total: 15
status: issues_found
---

# Phase 72: Code Review Report

**Reviewed:** 2026-08-26T00:00:00Z
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

This is a fresh, independent review of the phase at current HEAD (`c728b30`). Nothing was
carried forward from the earlier review in git history; the two defects it recorded as
resolved (vote destruction on save, non-Active rejection on the signup save paths) were
re-verified against the current code and are genuinely fixed — `PlayerSignupService`
now routes through the scalar `IPlayerSignupRepository.UpdateCharacterAsync`, and
`QuestUpdateSignupCharacterTests.cs:474` proves votes survive a character swap.

**What holds up under attack.** The authorization shape of `UpdateSignupCharacter` is
correct: the quest is loaded through the group-filtered `DbSet`, the signup is re-derived
from the caller's identity (`QuestController.cs:545`) and never taken from a client id,
and ownership is re-checked on the character (`:557`). XSS was probed specifically:
`data-current-character-label` is Razor attribute-encoded, read back via
`trigger.dataset`, and written with `textContent` — never `innerHTML`
(`_CharacterSelectModal.cshtml:70,101`). The antiforgery token is auto-emitted because
the form carries `asp-action` and `method="post"`, and the action carries
`[ValidateAntiForgeryToken]`. `Character.GroupId` is mapped by AutoMapper convention, so
the added board comparison at `:567` does not misfire and silently break every save. All
28 tests in the three new test classes pass against a clean `dotnet build`.

**Where it does not hold up.** Eight warnings, concentrated in three clusters:

1. **Type-safety regression the phase introduced.** Removing `.Where(...).ToList()` from
   the `ViewBag.UserCharacters` writer left four *hard* `(List<Character>)` casts and four
   *soft* `as List<Character>` casts depending on an AutoMapper implementation detail, with
   two different failure modes for the same value (WR-01).
2. **A role the requirement claims to cover cannot reach the UI.** A non-selected
   AssistantDM signup on a finalized quest renders in neither the participants table nor
   the waitlist table, so SIGNCHAR-06's "all three signup roles" is only true at the
   controller level — and the role-coverage test posts directly, so it cannot see this
   (WR-02).
3. **Error reporting that goes nowhere.** Five `ModelState.AddModelError` calls in the two
   sibling signup actions are structurally incapable of reaching the user, while
   `UpdateSignupCharacter` in the same file does it correctly with `TempData` (WR-04). Two
   reachable non-tamper states still produce raw 400/500 responses (WR-05, WR-06).

No Critical finding is claimed. I attempted to prove ownership bypass, cross-group write,
CSRF omission, XSS through the label path, and an AutoMapper-null break of the new
`GroupId` gate; each attempt failed against the code as written.

## Narrative Findings (AI reviewer)

### Warnings

#### WR-01: `ViewBag.UserCharacters` hard casts now rest on an AutoMapper implementation detail

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:332`
**Also:** `QuestBoard.Service/Views/Quest/Details.cshtml:146,277` (hard cast),
`QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:227,272` (hard cast),
`QuestBoard.Service/Views/Quest/Details.cshtml:363,446`,
`QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:329`,
`QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:30` (soft cast)

**Issue:** The phase replaced

```csharp
var allCharacters = await characterService.GetCharactersByOwnerIdAsync(currentUser.Id, token);
userCharacters = allCharacters.Where(c => c.Status == CharacterStatus.Active).ToList();
```

with a direct assignment of the service result. The old `.ToList()` *structurally guaranteed*
the ViewBag value was a `List<Character>`. It now comes from
`CharacterRepository.GetCharactersByOwnerIdAsync`, which returns
`Mapper.Map<IList<Character>>(entities)` — the concrete type is whatever AutoMapper's
collection mapper picks for an `IList<T>` destination. It picks `List<T>` today, so this
works; nothing in the code enforces it.

Eight call sites consume that dynamic value with two different failure modes:

- `((List<Character>)ViewBag.UserCharacters).Any()` — an `InvalidCastException` inside a
  Razor `@if`, i.e. a **500 on the quest Details page**, desktop and mobile.
- `ViewBag.UserCharacters as List<Character> ?? new List<Character>()` — **silently renders
  an empty dropdown**. In the modal that select is `required`, so the user would be locked
  out of saving with no explanation.

A dynamic value whose failure mode is "500" in one branch and "silent empty picker with a
`required` control" three lines later is not a safe contract to leave behind.

**Fix:** Pin the concrete type once, at the writer, and widen the reads:

```csharp
// QuestController.Details (GET)
ViewBag.UserCharacters = userCharacters?.ToList() ?? [];
```

```cshtml
@* Details.cshtml / Details.Mobile.cshtml — replace the hard casts *@
@{ var ownedCharacters = ViewBag.UserCharacters as IEnumerable<Character> ?? []; }
@if (isCurrentUser && ownedCharacters.Any()) { ... }
```

```cshtml
@* _CharacterSelectModal.cshtml:30 *@
@foreach (var character in ViewBag.UserCharacters as IEnumerable<Character> ?? [])
```

---

#### WR-02: A non-selected AssistantDM gets no character-change control, contradicting SIGNCHAR-06

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:73-79,86-91`
**Also:** `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:17-36`

**Issue:** Both Details views render a change trigger from exactly two collections:

- `allSelectedParticipants` — requires `ps.IsSelected`
- `waitlistPlayers` — requires `!ps.IsSelected && ps.Role == SignupRole.Player`

`QuestRepository.FinalizeQuestAsync` auto-approves **Spectators only**; every other role
gets `IsSelected = selectedPlayerSignupIds.Contains(ps.Id)`. So a signup with
`Role == AssistantDM && IsSelected == false` — produced whenever a DM finalizes an open
quest without ticking an Assistant DM who had signed up — matches **neither** collection.
That player sees their name in the read-only "Current Signups" sidebar with no character
column and no trigger, on desktop and mobile alike.

SIGNCHAR-06 states the change "remains possible … for all three signup roles (Player,
Spectator, AssistantDM)". It is true of the controller and false of the UI. The test that
appears to cover it —
`QuestUpdateSignupCharacterTests.UpdateSignupCharacter_Post_ForEachSignupRole_...:170` —
POSTs the form directly and never renders Details, so it cannot detect the gap. Neither
`QuestDetailsCharacterControlTests` nor `QuestDetailsMobileCharacterControlTests` covers a
non-selected non-Player signup.

**Fix:** Either render the missing rows, or narrow the requirement. To render them, add a
third block for non-selected, non-Player signups alongside the waitlist table:

```csharp
var unselectedNonPlayers = Model.Quest?.PlayerSignups
    .Where(ps => !ps.IsSelected && ps.Role != SignupRole.Player)
    .OrderBy(ps => ps.SignupTime).ToList() ?? [];
```

Add a rendering test that seeds `signupRole: 2, isSelected: false` on a finalized quest and
asserts a trigger carrying that signup's character id appears.

---

#### WR-03: Board-scope re-check applied to one write path, skipped on the other two

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:567`
**Compare:** `:411-419` (`Details` POST), `:458-466` (`JoinFinalizedQuest`)

**Issue:** `UpdateSignupCharacter` adds an explicit board comparison and documents it as
insurance "if a future query ever opts out of that filter". The two sibling actions that
write the identical `CharacterId` column validate ownership only. If the stated failure
mode ever materialises — a character lookup that bypasses `CharacterEntity`'s query filter —
`UpdateSignupCharacter` is protected and the two signup-creation paths are not, which is
the opposite of useful: those paths are reachable by more users, more often.

Defence-in-depth that covers one of three doors is a false sense of security and a
maintenance trap: a reader of `Details` POST will reasonably conclude no board check is
needed anywhere.

**Fix:** Extract the gate once and call it from all three sites:

```csharp
private async Task<bool> IsAssignableCharacterAsync(int characterId, int userId)
{
    var character = await characterService.GetCharacterWithDetailsAsync(characterId);
    return character != null
        && character.OwnerId == userId
        && activeGroupContext.ActiveGroupId is { } groupId
        && character.GroupId == groupId;
}
```

---

#### WR-04: Five `ModelState.AddModelError` calls that can never reach a user

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:443,463,474`
**Also:** `:398,416`

**Issue:** Two separate defects with the same symptom — a user action fails silently.

1. `:443`, `:463`, `:474` add a model error and then `return RedirectToAction(...)`.
   ModelState does not survive a redirect; the dictionary is discarded with the request.
   These three messages ("You have already signed up for this quest.", "Invalid character
   selection.", "Could not find the finalized date information.") are **unconditionally
   lost**. The user clicks Join, is bounced back to the same page, and nothing indicates
   why nothing happened.
2. `:398` and `:416` add a model error and re-render via `return await Details(questId)`.
   That is the right shape, but neither `Details.cshtml`, `Details.Mobile.cshtml`, nor
   either layout contains an `asp-validation-summary` or `Html.ValidationSummary` — grep
   returns zero matches project-wide. So these two are dropped as well.

`UpdateSignupCharacter` in the same file gets this right (`:548`, `:576`) using `TempData`,
which the shared `_Toasts` partial renders from every layout. The phase touched the
character-validation block inside `:463` and left the broken reporting in place directly
beside a working example of the correct pattern.

**Fix:** Use `TempData["Error"]` on every redirect path, matching the working sibling:

```csharp
if (character == null || character.OwnerId != user.Id)
{
    TempData["Error"] = "Invalid character selection.";
    return RedirectToAction("Details", new { id = questId });
}
```

For `:398`/`:416`, either switch to `TempData` + redirect (PRG, consistent with the rest of
the action) or add `<div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>`
to both Details views.

---

#### WR-05: A reachable, non-tampering state produces a raw 400 text page

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:559`

**Issue:** The comment at `:540-544` correctly identifies stale-modal states as something
"a player can hit without tampering" and gives the missing-signup case a friendly redirect.
The very next branch does not extend the same courtesy to an equally reachable stale state:

- Player opens the change modal on the Details page.
- In another tab, they delete the character currently on the signup (or a DM/Admin does).
- They press Save. `GetCharacterWithDetailsAsync` returns `null`, and the response is
  `BadRequest("Invalid character selection.")` — a bare text/plain 400, outside the app
  chrome, with no way back other than the browser Back button.

Ownership failure and cross-board failure both warrant a hard rejection; "the row you were
looking at no longer exists" does not, and is the same class of race the code already
handles gracefully one branch up.

**Fix:** Split the null case from the ownership/board case:

```csharp
var character = await characterService.GetCharacterWithDetailsAsync(characterId.Value);
if (character == null)
{
    TempData["Error"] = "That character no longer exists.";
    return RedirectToAction("Details", new { id = questId });
}
if (character.OwnerId != user.Id) return BadRequest("Invalid character selection.");
```

---

#### WR-06: TOCTOU between the signup check and the write surfaces as an unhandled 500

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:574`

**Issue:** `UpdateSignupCharacter` checks the signup exists at `:545`, then calls
`playerSignupService.UpdateSignupCharacterAsync(playerSignup.Id, characterId)` at `:574`.
`PlayerSignupService.UpdateSignupCharacterAsync` throws
`ArgumentException("Player signup not found")` when `UpdateCharacterAsync` returns `false`.
Nothing in the controller or the pipeline catches it, so a signup revoked in another tab
between `:545` and `:574` turns the friendly-redirect design at `:548` into an unhandled
500. The same window exists for the character row itself: a delete landing between `:556`
and `:574` produces an FK `DbUpdateException` on SQL Server.

Narrow, but this action is explicitly designed around users leaving stale modals open, and
the window is exactly the scenario the code's own comments describe.

**Fix:** Treat a lost race as the same "you are no longer signed up" state rather than a crash:

```csharp
try
{
    await playerSignupService.UpdateSignupCharacterAsync(playerSignup.Id, characterId);
}
catch (ArgumentException)
{
    TempData["Error"] = "You are no longer signed up for this quest.";
    return RedirectToAction("Details", new { id = questId });
}
```

---

#### WR-07: The "shared" modal partial has two unguarded host contracts

**File:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:22,30`

**Issue:** The partial's header block documents itself as shared and spells out a trigger
contract, but leaves two host obligations undocumented and unguarded:

1. `<form asp-action="UpdateSignupCharacter" method="post">` omits `asp-controller`. The
   action URL is resolved from the *ambient* `ViewContext`, so the partial only posts to
   `QuestController` because both current hosts happen to be `Quest` views. Rendering it
   from any other controller's view silently generates a URL for the wrong controller —
   most likely a 404, and no compile-time or test signal.
2. It reads `ViewBag.UserCharacters` with `as … ?? new List<Character>()`. A host view that
   forgets to populate it renders an empty `required` select with no diagnostic; the user
   simply cannot save.

**Fix:**

```cshtml
<form asp-controller="Quest" asp-action="UpdateSignupCharacter" method="post" id="characterSelectForm">
```

and document `ViewBag.UserCharacters` in the partial's header comment block alongside the
existing trigger contract.

---

#### WR-08: Status assertions are loose enough to pass on a failed request

**File:** `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs:49,85,123,160,200,273,309,345`

**Issue:** Eight tests assert
`BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found)`. Two problems:

- `HttpStatusCode.Redirect` and `HttpStatusCode.Found` are **the same value** (302). The
  three-way list is really a two-way list; the redundancy suggests the set was assembled
  without checking what it asserts.
- The success path of `UpdateSignupCharacter` is *always* 302. Admitting `OK` means a
  regression that re-renders a view (200) instead of redirecting — or any future change to
  a 200-returning error page — passes the status assertion unnoticed. The DB assertion that
  follows would still catch a *write* regression, but the response-shape contract these
  lines claim to pin is not actually pinned.

That the tight assertion is achievable is proved in the same file: line 381 uses
`Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found)` and line 421 uses
`Should().Be(HttpStatusCode.Found)`, both of which pass.

**Fix:** `response.StatusCode.Should().Be(HttpStatusCode.Found);` in all eight, and assert
the `Location` header points back at the quest, as the no-signup test at `:382-383`
already does.

---

### Info

#### IN-01: `character.Classes ?? []` is unreachable, and the test named for it exercises a different case

**File:** `QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs:21`
**Also:** `QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs:82-92`

**Issue:** `Character.Classes` is declared `public IList<CharacterClass> Classes { get; set; } = [];`
— non-nullable with an initializer, never mapped to null by
`EntityProfile`'s `CharacterEntity → Character` map. The `?? []` guard cannot fire. The unit
test `ToSelectLabel_WithNoClasses_RendersEmptyParentheses` passes `classes: []`, which
exercises the empty-collection path through `string.Join`, not the null-coalesce. So the
branch is both dead and uncovered while appearing covered.

**Fix:** Drop the coalesce (`var classList = string.Join(", ", character.Classes.Select(...))`),
or keep it and add an explicit `Classes = null!` case to the test so the guard is real.

---

#### IN-02: Modal footer left-aligns Cancel/Save whenever the Remove button is hidden

**File:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:37-48`

**Issue:** The footer is `d-flex justify-content-between` with the Remove button carrying
`d-none` in the "Add character" state (`:110`). `d-none` is `display:none`, removing it from
the flex flow, so the single remaining child — the Cancel/Save group — lands at
`flex-start`, i.e. **left-aligned**. The old inline modal this replaced used the default
Bootstrap `.modal-footer` (`justify-content: flex-end`), so the Add flow visibly regressed.
`CLAUDE.md` pins button layout as "secondary (cancel) left, primary (submit) right".

**Fix:** Keep the Remove button in the flow and hide it with `visibility` instead, or add
`ms-auto` to the Cancel/Save group so it stays right-aligned when it is the only child:

```html
<div class="d-flex gap-2 ms-auto">
```

---

#### IN-03: The modal is rendered for visitors who can never use it

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:843`
**Also:** `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:420`

**Issue:** `Html.RenderPartialAsync("_CharacterSelectModal")` is unconditional. Anonymous
visitors, users with no signup on the quest, and Campaign-board viewers all receive the
full modal markup plus its `<script>` block, with no trigger anywhere on the page that can
open it. Harmless (no data leaks — the dropdown only ever lists the viewer's own
characters, and is empty when anonymous) but it is dead weight on every render.

**Fix:** Wrap both call sites: `@if (User.Identity?.IsAuthenticated == true && (bool)ViewBag.IsPlayerSignedUp) { ... }`.

---

#### IN-04: The injected stand-in option and its reset logic are unreachable

**File:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:76-103`

**Issue:** The comment block justifies the reset logic with "one modal instance is reused by
every row on the page". In practice at most **one** trigger per page can belong to the
caller: a user has at most one signup per quest, and `allSelectedParticipants` /
`waitlistPlayers` are disjoint. Further, the character on that signup is by construction
owned by the caller and visible under the active board's query filter, so it is always
already present in `ViewBag.UserCharacters` — which the phase widened to all statuses
precisely to guarantee that. Both `matchingOption` injection (`:92-103`) and
`previousCurrentOption.remove()` (`:82-85`) are therefore dead in every reachable state.

Not a defect — it is the belt to SIGNCHAR-04's braces — but the justifying comment
overstates the situation and will mislead the next reader into thinking multiple
simultaneous triggers exist.

**Fix:** Keep the code, correct the comment to say it is a fail-safe for a state the current
data flow cannot produce.

---

#### IN-05: Signup-time pickers now offer Dead and Retired characters, which no SIGNCHAR requirement asks for

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:332`
**Also:** `QuestBoard.Service/Views/Quest/Details.cshtml:363,446`,
`QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:329`

**Issue:** `ViewBag.UserCharacters` feeds three dropdowns, not one: the change modal, the
`JoinFinalizedQuest` picker, and the open-quest signup picker. SIGNCHAR-04 asks only that
"the change UI shows *that* character as the current selection" — i.e. the one already on
the signup. Widening the writer widened all three, so a player can now select a Dead
character for a **brand-new** signup on a quest they have never joined. That is a
user-visible behaviour change to two flows outside the phase's requirement set.

It is deliberate and tested (`QuestSignupCharacterStatusTests.cs`, commit `e7a3245`), and
`ToSelectLabel` suffixes the status so the choice is not blind, so this is recorded rather
than objected to. Worth confirming with the operator that offering a Dead character for a
fresh signup is intended, not just tolerated.

**Fix:** If unintended, keep the widened list for the modal only and pass an Active-filtered
list to the two signup-time selects via a second ViewBag key.

---

#### IN-06: Comment on `InitializeAsync` describes what `DisposeAsync` does

**File:** `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs:12-15`

**Issue:** The three-line comment about resetting the singleton group context sits above
`InitializeAsync`, which is `ValueTask.CompletedTask`. The reset it describes is in
`DisposeAsync` at `:17-21`. The comment also says "does not bleed into subsequently-executed
test classes", but `IClassFixture<T>` constructs a fresh factory per class, so the stated
hazard does not exist in this shape.

**Fix:** Move the comment onto `DisposeAsync` and drop the cross-class claim.

---

#### IN-07: Desktop Details compares `int` to a `dynamic` ViewBag where mobile does it safely

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:56,182`

**Issue:** `var isCurrentUser = participant.Player.Id == ViewBag.CurrentUserId;` binds an
`int` against a `dynamic` that is `null` for anonymous visitors. The runtime binder resolves
this to a lifted `int? == null` comparison and returns `false`, so it works — but it is a
DLR-dependent behaviour, not a language guarantee, and the value now gates whether a
character-change control renders. `Details.Mobile.cshtml:12` does the safe thing
(`var currentUserId = ViewBag.CurrentUserId as int?`) and compares against that.

**Fix:** Mirror the mobile view — hoist `var currentUserId = ViewBag.CurrentUserId as int?;`
into the `@{ }` block at the top of `Details.cshtml` and compare against it in both loops.

---

_Reviewed: 2026-08-26T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
