---
phase: 72-change-character-on-an-existing-signup
reviewed: 2026-08-25T13:59:48Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs
  - QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs
  - QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs
  - QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs
  - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
  - QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs
findings:
  critical: 0
  warning: 6
  info: 4
  total: 10
findings_as_reviewed:
  critical: 2
  warning: 7
  info: 4
  total: 13
resolved:
  - CR-01
  - CR-02
  - WR-03
status: issues_found
---

# Phase 72: Code Review Report

**Reviewed:** 2026-08-25T13:59:48Z
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

Phase 72 adds a shared character-select modal to the desktop and mobile quest Details
pages and loosens `UpdateSignupCharacter` so a player may bring a Retired or Dead
character to an existing signup. The authorization shape of the POST path is sound —
the signup is always re-derived from the caller's identity against the loaded quest,
never taken from a client-supplied id — and the Razor encoding is correct everywhere a
character label reaches the DOM (attribute values are `@`-encoded by Razor, then written
back with `textContent`, never `innerHTML`). No XSS or ownership-bypass was found.

Two blocking defects were found instead, both on the write path this phase exists to
exercise:

1. Saving a character change **deletes every date vote on that signup**. The service
   round-trips the whole `PlayerSignup` aggregate through a loader that does not include
   `DateVotes`, into a repository `UpdateAsync` that clears and repopulates `DateVotes`
   from the model. The 11 new integration tests all assert only `signup.CharacterId`, so
   none of them catch it.
2. Widening `ViewBag.UserCharacters` to all statuses (the deliberate D-12 spillover) was
   not carried into the two **signup-time** actions, which still reject non-Active
   characters — and both reject *silently*, because neither Details view renders a
   validation summary and one of them discards `ModelState` across a redirect.

The rest of the findings are robustness, test-strength, and convention issues.

## Narrative Findings (AI reviewer)

## Critical Issues

### CR-01: Changing or clearing the signup character silently deletes all of the player's date votes

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:570`
(defect body in `QuestBoard.Domain/Services/PlayerSignupService.cs:36-46` and
`QuestBoard.Repository/PlayerSignupRepository.cs:109-130`)

**Issue:**
`UpdateSignupCharacter` calls `playerSignupService.UpdateSignupCharacterAsync(playerSignup.Id, characterId)`.
That method loads the signup through `BaseRepository.GetByIdAsync`
(`QuestBoard.Repository/BaseRepository.cs:43-47`), which is `DbSet.FindAsync([id])` —
**no `.Include(ps => ps.DateVotes)`**. The only prior load in the request is
`questService.GetQuestWithDetailsAsync`, and its projection helper is `AsNoTracking()`
(`QuestBoard.Repository/QuestRepository.cs:335-338`), so the change tracker is empty and
`FindAsync` issues a fresh navigation-free query. `PlayerSignupEntity.DateVotes` is
therefore the empty initializer collection (`PlayerSignupEntity.cs:42`), and AutoMapper's
convention mapping (`QuestBoard.Repository/Automapper/EntityProfile.cs:49-50`) hands back
a `PlayerSignup` whose `DateVotes` is `[]`.

The service then calls `repository.UpdateAsync(playerSignup)`, and
`PlayerSignupRepository`'s override does:

```csharp
entity.DateVotes.Clear();
var dateVoteEntities = Mapper.Map<List<PlayerDateVoteEntity>>(model.DateVotes); // empty
foreach (var vote in dateVoteEntities) { entity.DateVotes.Add(vote); }
await DbContext.SaveChangesAsync(token);
```

`entity` here *is* loaded `.Include(ps => ps.DateVotes)`, so `Clear()` orphans the real
rows and nothing is re-added. `SaveChanges` deletes every `PlayerDateVote` for that signup.

User-visible impact on a finalized quest: the player's Yes/Maybe vote for the finalized
date disappears, so the Details participant/waitlist row flips to "No Vote", `SendReminder`
drops them from `eligibleSignups` (`QuestController.cs:853-857`), and
`GetTopWaitlistedCandidateAsync` (`PlayerSignupRepository.cs:79-100`) can never promote
them off the waitlist. This is unrecoverable data loss from a UI action advertised as
"Character updated."

**Fix:** Do not round-trip the aggregate for a single-column change. Either load with the
vote-aware method, or add a targeted repository call:

```csharp
// QuestBoard.Repository/PlayerSignupRepository.cs
public async Task UpdateCharacterAsync(int playerSignupId, int? characterId, CancellationToken token = default)
{
    var entity = await DbSet.FirstOrDefaultAsync(ps => ps.Id == playerSignupId, token);
    if (entity == null) throw new ArgumentException("Player signup not found", nameof(playerSignupId));
    entity.CharacterId = characterId;
    await DbContext.SaveChangesAsync(token);
}
```

and have `PlayerSignupService.UpdateSignupCharacterAsync` delegate to it. Add a regression
test that seeds `PlayerDateVote` rows, posts a character change, and asserts the votes are
still present with the same `Vote` values (see WR-03).

### CR-02: Widened character list makes the two signup-time pickers offer characters the server silently rejects

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:326-333`
(reject sites at `:409-417` and `:454-462`; render sites at
`Views/Quest/Details.cshtml:446-449`, `:363-366` and `Views/Quest/Details.Mobile.cshtml:329-332`)

**Issue:**
This phase removed the `Status == CharacterStatus.Active` narrowing from the single writer
of `ViewBag.UserCharacters`. That ViewBag feeds six read sites, including the two
signup-time `<select>`s. But the two signup-time actions were **not** updated and still
enforce the Active gate:

```csharp
// Details POST, line 412
if (character == null || character.OwnerId != user.Id || character.Status != CharacterStatus.Active)
// JoinFinalizedQuest, line 457
if (character == null || character.OwnerId != user.Id || character.Status != CharacterStatus.Active)
```

Both rejections are invisible to the player:

- `Details` POST does `ModelState.AddModelError("", "Invalid character selection."); return await Details(questId);`
  — but neither `Details.cshtml` nor `Details.Mobile.cshtml` contains an
  `asp-validation-summary` / `Html.ValidationSummary` anywhere (grep returns zero hits),
  so the page re-renders with no message at all.
- `JoinFinalizedQuest` does `ModelState.AddModelError(...); return RedirectToAction("Details", ...)`
  — `ModelState` does not survive a redirect, so the message is discarded outright.

Net behaviour introduced by this phase: a player who picks a Retired or Dead character from
the now-widened "Sign up without character" / "Join without character" dropdown lands back
on the quest page with **no signup created and no explanation**. Before this phase those
options were not offered, so the dead-end was unreachable.

CONTEXT.md D-12 accepted the *spillover into signup-time behaviour* as deliberate; it did
not accept a picker whose options the save path refuses. The phase comment at
`QuestController.cs:327-329` even asserts the opposite of what the code does — "so the
signup/change pickers offer exactly what the save path will accept" is false for two of
the three pickers.

**Fix:** Pick one and make it true everywhere. Preferred — carry D-10/D-11 through, since
the picker already claims it:

```csharp
// Details POST (:412) and JoinFinalizedQuest (:457)
if (character == null || character.OwnerId != user.Id)
{
    TempData["Error"] = "Invalid character selection.";
    return RedirectToAction("Details", new { id = questId });
}
```

(Use `TempData["Error"]` in both so `_Toasts.cshtml` actually surfaces it, rather than a
`ModelState` entry no view reads.) If instead signup-time must stay Active-only, add a
second, narrower list for those two selects and correct the comment at `:327-329`.

## Warnings

### WR-01: Modal early-return leaves stale `questId` and a stale injected option in the form

**File:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:62-114`

**Issue:** The `show.bs.modal` handler bails at `:64-66` when `event.relatedTarget` is
falsy, *before* the reset block at `:72-85`. Any open that is not driven by a trigger
element (a programmatic `bootstrap.Modal(el).show()`, or a future keyboard/deep-link path)
therefore reuses whatever the previous open left behind: the previous row's `questId`, a
previously injected `#characterSelectCurrentOption`, and a `disabled`/`required` state.
The modal this replaced hard-rendered `value="@Model.Quest?.Id"` server-side
(`Details.cshtml`, removed block) and had no such failure mode. A stale `questId` makes an
untouched Save write to the wrong quest's signup.

**Fix:** Reset unconditionally, then prime:

```js
characterSelectModal.addEventListener('show.bs.modal', function (event) {
    const select = document.getElementById('characterSelect');
    select.disabled = false;
    select.required = true;
    document.getElementById('characterSelectCurrentOption')?.remove();
    document.getElementById('characterSelectQuestId').value = '';
    select.value = '';

    const trigger = event.relatedTarget;
    if (!trigger) { return; }
    // ...prime from trigger.dataset...
});
```

### WR-02: "Clear the character" is encoded as the *absence* of a field, so any dropped field silently wipes the character

**File:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:116-133` and
`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:525,550,572-574`

**Issue:** The Remove path disables the `<select>` so the browser omits `characterId`
entirely; the action treats "field absent" as an explicit clear and reports
`"Character removed from your signup."`. There is no way for the server to distinguish a
deliberate clear from a malformed/partial submission, a stripped field, or a future
refactor that renames the input — all of which now silently null out the player's
character and report success.

**Fix:** Make the intent explicit rather than inferred, e.g. keep the select enabled with
`value=""` and add a distinct posted flag:

```html
<input type="hidden" name="clearCharacter" id="characterSelectClear" value="false" />
```

```csharp
public async Task<IActionResult> UpdateSignupCharacter(int questId, int? characterId, bool clearCharacter = false)
{
    if (!clearCharacter && !characterId.HasValue) return BadRequest("No character selected.");
    // ...
}
```

### WR-03: No test asserts the vote-preservation invariant that CR-01 breaks

**File:** `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs:22-451`

**Issue:** All 11 tests seed a signup with `CreatePlayerSignupAsync` (which never creates
`PlayerDateVote` rows) and assert only `signup.CharacterId`. The action's most damaging
side effect is therefore completely uncovered, which is why CR-01 shipped green. The
"finalized quest" test at `:95-130` gets closest — it creates a `ProposedDate` — but still
seeds no votes and never re-reads them.

**Fix:** Add a test that seeds votes, posts the change, and asserts they survive:

```csharp
// after CreatePlayerSignupAsync(...) seed a PlayerDateVoteEntity for proposedDate.Id with Vote = Yes
var votes = await context.PlayerDateVotes
    .Where(v => v.PlayerSignupId == signup.Id)
    .ToListAsync(TestContext.Current.CancellationToken);
votes.Should().HaveCount(1);
votes[0].Vote.Should().Be((int)VoteType.Yes);
```

Run it against `main` first — it must fail before the CR-01 fix.

### WR-04: Success assertions are "not a 400" rather than "the action succeeded"

**File:** `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs:48,84,122,159,199,272,308,344`

**Issue:** Every success case asserts
`Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found)`.
`HttpStatusCode.Redirect` and `HttpStatusCode.Found` are the **same value (302)**, so the
accepted set is `{200, 302}`. `UpdateSignupCharacter` can never return 200, so the OK arm
is dead; and the assertion would still pass if the action began returning a 200 error page
instead of redirecting. The `Location` header is only checked in one test (`:380-382`).

**Fix:** `response.StatusCode.Should().Be(HttpStatusCode.Found);` plus
`response.Headers.Location!.ToString().Should().Be($"/Quest/Details/{quest.Id}");` in the
success cases.

### WR-05: Desktop "other player's row" test cannot distinguish correct scoping from no controls at all

**File:** `QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs:168-194`

**Issue:** The viewer in this test owns no characters and their signup has
`characterId: null`, so the view's own-row Add trigger is suppressed by the
`ViewBag.UserCharacters.Any()` gate. The only assertion is a negative
(`NotContain(data-current-character-id="{otherCharacter.Id}")`), which also passes on a
page that renders zero triggers. The mobile counterpart
(`Mobile/QuestDetailsMobileCharacterControlTests.cs:197-212`) gets this right by asserting
the viewer's own id *is* present alongside the other player's id being absent.

**Fix:** Give the viewer a character with `characterId` set, and assert both halves —
own id present, other player's id absent — the same way the mobile test does.

### WR-06: `ViewBag.UserCharacters` is consumed through two incompatible cast idioms, both fragile

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:146,277,363,446`,
`QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:227,272,329`,
`QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:30`

**Issue:** The visibility gates use a hard cast `((List<Character>)ViewBag.UserCharacters).Any()`
while the option loops use `ViewBag.UserCharacters as List<Character> ?? new List<Character>()`.
The controller declares `IList<Character>? userCharacters` (`QuestController.cs:318`) fed by
`GetCharactersByOwnerIdAsync`, whose declared return type is `IList<Character>` — the
concrete `List<Character>` is an AutoMapper implementation detail
(`CharacterRepository.cs:47`). If that ever changes to any other `IList<T>`, the hard-cast
sites throw `InvalidCastException` at render time and the `as` sites silently render an
**empty** picker, which reads to the player as "you own no characters". Two different
failure modes for the same contract.

**Fix:** Stop routing a typed collection through `dynamic`. Cast once at the top of each
view against the declared interface and reuse it:

```csharp
@{
    var userCharacters = ViewBag.UserCharacters as IList<Character> ?? [];
}
```

### WR-07: `ToSelectLabel`'s null guard is unreachable and its only untested branch

**File:** `QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs:21` and
`QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs:22`

**Issue:** `var classes = character.Classes ?? [];` guards against null, but
`Character.Classes` is a non-nullable `IList<CharacterClass>` initialized to `[]`
(`QuestBoard.Domain/Models/Character.cs:42`). The guard is dead code. The unit-test helper
compounds it: `Classes = classes ?? [new CharacterClass { ... }]` means passing
`classes: null` yields a default Fighter, so no test can ever drive `character.Classes`
to null — the branch is both unreachable in production and untestable in the suite.

**Fix:** Drop the guard (`var classList = string.Join(", ", character.Classes.Select(...))`),
or make `Character.Classes` genuinely nullable if a not-loaded state is meant to be
representable. Either way the test helper should stop swallowing an explicit `null`.

## Info

### IN-01: Modal footer uses an outline button against the project's stated convention

**File:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:38`

**Issue:** `class="btn btn-outline-danger"` on the Remove button. `CLAUDE.md`'s UI/UX
guidelines state "Use filled colored buttons (not outline)". The Cancel/Save pair in the
same footer are correctly filled.

**Fix:** `class="btn btn-danger d-none"`.

### IN-02: The current-character injection branch is unreachable in practice

**File:** `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml:92-103`

**Issue:** The `if (!matchingOption)` fallback exists for a signup whose character is not in
`ViewBag.UserCharacters`. But the option list is `GetCharactersByOwnerIdAsync(currentUser.Id)`
filtered by the same board-level query filter that produced the rendered signup character,
and the status narrowing that used to exclude Retired/Dead is gone as of this phase. Every
character that can appear in a trigger's `data-current-character-id` is therefore already in
the list. The comment cites "belongs to another group's roster" — which the query filter makes
impossible.

**Fix:** Keep it only as an explicitly-labelled defensive fallback (correct the comment so it
does not claim a scenario the query filter rules out), or drop it and instead assert the
invariant in a view test.

### IN-03: `IAsyncLifetime` comment misstates when the reset runs

**File:** `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs:11-13`

**Issue:** "reset the singleton group context after each test class run" — xUnit constructs a
new test-class instance per test and runs `IAsyncLifetime.DisposeAsync` per test, not per
class. (Per-class teardown would be `IClassFixture`/`IAsyncLifetime` on the fixture itself.)
The behaviour is correct; the comment describes something else.

**Fix:** "reset the singleton group context after each test in this class".

### IN-04: The shared modal is rendered on pages that can never open it

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:843`,
`QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:420`

**Issue:** `_CharacterSelectModal` is rendered unconditionally — on non-finalized quests and
Campaign-board quests (where no trigger is emitted at all, since triggers live only in the
finalized participant/waitlist blocks), and for anonymous visitors. Those pages ship dead
markup, an always-registered `DOMContentLoaded` handler, and a `<select>` enumerating the
caller's full character roster with no control that can use it.

**Fix:** Wrap the render in the same condition that governs whether any trigger exists, e.g.
`@if (User.Identity?.IsAuthenticated == true && Model.Quest?.IsFinalized == true) { await Html.RenderPartialAsync("_CharacterSelectModal"); }`.

---

_Reviewed: 2026-08-25T13:59:48Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

## Resolution

Both blockers were fixed before the phase closed. Counts in `findings` above reflect what
remains open; `findings_as_reviewed` preserves the original review outcome.

| ID | Status | How |
|----|--------|-----|
| CR-01 | Resolved | Added `IPlayerSignupRepository.UpdateCharacterAsync`, a targeted scalar write that leaves the date-vote collection untouched, and routed `PlayerSignupService.UpdateSignupCharacterAsync` through it. Loading the votes first was tried and rejected: re-adding id-bearing vote entities after `Clear()` raises a delete/insert conflict. |
| CR-02 | Resolved | Dropped the `CharacterStatus.Active` clause from both signup-time save paths in `QuestController`, leaving ownership as the only gate — matching the change path and the full list the pickers offer. |
| WR-03 | Resolved | `UpdateSignupCharacter_Post_WhenSignupHasDateVotes_LeavesThoseVotesIntact` seeds a date vote, changes the character, and asserts the vote survives. Verified to fail against the pre-fix implementation. |

CR-02 coverage lives in `QuestBoard.IntegrationTests/Controllers/QuestSignupCharacterStatusTests.cs`:
Retired at signup, Retired and Dead at join, plus one pinning that ownership is still enforced.
The first three were verified to fail against the pre-fix implementation; the ownership test
passes either way, confirming it discriminates the right thing.

Still open: WR-01, WR-02, WR-04, WR-05, WR-06, WR-07 and all four Info findings.
Full suite after the fixes: 313 unit + 431 integration, 0 failures.
