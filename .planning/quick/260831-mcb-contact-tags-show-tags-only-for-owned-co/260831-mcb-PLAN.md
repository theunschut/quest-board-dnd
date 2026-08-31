---
phase: quick-260831-mcb
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs
  - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
autonomous: true
requirements: [TAGOWN-01, TAGOWN-02]

must_haves:
  truths:
    - "A DM-tier viewer who created a contact sees that contact's tag chips on Contacts/Index and Contacts/Details, with the Show Hidden toggle off — unchanged from today."
    - "A DM-tier viewer who did NOT create a contact sees no tag chips for it on Contacts/Index or Contacts/Details while the Show Hidden toggle is off."
    - "That same non-owning viewer also sees no filter option for that contact's tag names — the Index filter row (desktop) and filter drawer (mobile) offer only tag names whose chips the viewer can see."
    - "Both the chips and the filter options appear for the non-owning viewer after POSTing to /Contacts/ToggleShowHidden, and disappear again after toggling back off."
    - "The Create and Edit tag-suggestion whitelist is NOT scoped by ownership: a DM authoring a contact is still offered every tag name already in use on visible contacts of that board, including names introduced by contacts they did not create, with the toggle off."
    - "The rule holds identically on the mobile views (Index.Mobile, Details.Mobile), which are selected by User-Agent."
    - "A non-DM-tier viewer (Player) still receives zero tag markup — no chips, no filter row, no tag names — regardless of ownership or toggle state."
    - "Tag names a viewer may not see are absent from the response body of Index/Details, not merely hidden by CSS — both ContactViewModel.Tags and ContactsIndexViewModel.AvailableTags are emptied server-side."
    - "Contact visibility (IsVisibleTo), tag edit rights (CanManage), the DungeonMasterOnly policy gates, ApplyTagFilter, and the `tag` query-string binding are all behaviourally unchanged."
    - "No source comment added by this work names a phase number, plan number, requirement id, or review-finding id."
  artifacts:
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
    - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
  key_links:
    - "ContactsController.Index -> per-viewmodel tag gate reading vm.CreatedByUserId + currentUser.Id + viewerIsDmTier + includeHidden -> vm.Tags = [] when the gate fails."
    - "ContactsController.Details -> same gate on the single mapped viewModel -> viewModel.Tags = [] when the gate fails."
    - "Emptied Tags list -> the four existing Razor chip guards (Index.cshtml:45, Index.Mobile.cshtml:30, Details.cshtml:36, Details.Mobile.cshtml:40) already test Tags.Any()/Tags.Count > 0, so no chip wrapper renders and no view file needs editing."
    - "ContactsController.Index -> GetVisibleTagVocabularyAsync(..., TagVocabularyScope.ViewerVisible) -> the same AreTagsVisibleTo predicate applied per contact before BuildTagVocabulary -> ContactsIndexViewModel.AvailableTags -> the desktop filter row and the mobile filter drawer, both of which already render their empty-state branch when the list is empty."
    - "Create GET / Edit GET / PopulateTagSuggestionsAsync -> GetVisibleTagVocabularyAsync(..., TagVocabularyScope.Authoring) -> board-wide vocabulary exactly as today -> ContactViewModel.AvailableTagNames."
    - "AreTagsVisibleTo is the single predicate behind both the chip gate and the filter vocabulary, so a chip a viewer cannot see can never have a filter option and the two rules cannot drift apart."
    - "ReadShowHiddenToggle() -> SessionKeys.ShowHiddenContactsKey(groupId) -> the single per-board session flag that now governs hidden-contact visibility, non-owned tag chips, and the filter vocabulary."
---

<objective>
Extend the existing ownership + Show Hidden toggle pattern from *contact* visibility to *tag* visibility, on both surfaces that expose a tag name to a viewer: the per-contact chips and the Index filter vocabulary. Today any DM-tier viewer sees tag chips on every visible contact and every visible tag name in the filter row, regardless of who created the contact — so a SuperAdmin experiencing a board as a player (their effective board role always bypasses to `GroupRole.Admin`) reads DM-authored tags they were never meant to see, both on the cards and in the filter checkboxes.

Purpose: one consistent mental model — "you see it if you own it, or if you flipped Show Hidden" — applied consistently enough that a tag name a viewer cannot see on a chip also cannot be seen or selected in the filter row. The known and user-accepted side effect is that on a board with two real co-DMs, DM-A no longer sees DM-B's tags, or offers them as filters, until DM-A turns Show Hidden on.

Deliberately excluded from the restriction: the Create/Edit tag-suggestion whitelist. That list is an authoring aid — a DM writing a contact must still be able to reuse any tag already in use on the board, whoever introduced it. The shared vocabulary helper is therefore parameterized into two named scopes rather than narrowed wholesale.

Output: a server-side chip gate plus a scoped filter vocabulary in `ContactsController`, one comment-only correction on `ContactsIndexViewModel`, and integration coverage of the ownership × toggle × role × surface matrix on desktop and mobile. No Razor view changes and no new toggle.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

@.planning/quick/260831-mcb-contact-tags-show-tags-only-for-owned-co/260831-mcb-CONTEXT.md
@QuestBoard.Service/Controllers/Contacts/ContactsController.cs
@QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs
@QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
@QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs
@QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
</context>

<interface_context>

**Ownership is already on the view model — nothing new to plumb.**

`ContactViewModel.CreatedByUserId` (ContactViewModel.cs:35) is populated by convention through the
`CreateMap<Contact, ContactViewModel>()` profile in `QuestBoard.Service/Automapper/ViewModelProfile.cs`
(the member is not in the `Ignore()` list), and the domain `Contact.CreatedByUserId` is what
`IsVisibleTo` already compares. Both `Index` and `Details` already have `currentUser.Id` in scope. No
view model member, no AutoMapper change, and no new `ContactsIndexViewModel` member is needed.

**The existing sibling helper to mirror** (`ContactsController.cs:673`):

```
private static bool IsVisibleTo(Contact contact, int currentUserId, bool includeHidden)
```

Three-branch, `static`, guards `currentUserId != 0` before comparing `CreatedByUserId`. The new tag
gate is a sibling static helper on the same class, in the same shape, placed next to it.

**The two chip call sites that already do per-viewmodel post-mapping fixups:**

- `ContactsController.Index` lines 56-64 — `foreach (var vm in contactViewModels) { vm.CanManage = viewerIsDmTier; if (!viewerIsDmTier) { vm.Tags = []; } }`
- `ContactsController.Details` lines 124-129 — `viewModel.CanManage = viewerIsDmTier; if (!viewerIsDmTier) { viewModel.Tags = []; }`

Both already have `viewerIsDmTier` and `includeHidden` computed locally
(`var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();`). The change widens the existing
`Tags = []` condition at both sites; it does not add a second pass.

**The vocabulary helper and its four call sites** (`ContactsController.cs:692`):

```
private async Task<IList<ContactTag>> GetVisibleTagVocabularyAsync(int currentUserId, bool viewerIsDmTier, CancellationToken token)
```

It early-returns `[]` for a non-DM-tier viewer, computes `includeHidden = ReadShowHiddenToggle()`,
narrows all contacts through `IsVisibleTo`, and hands the survivors to `BuildTagVocabulary`. Callers:

| Call site | Line | Surface | Scope after this change |
|---|---|---|---|
| `Index` | 48-49 | `ContactsIndexViewModel.AvailableTags` — filter row / drawer | restricted by ownership + toggle |
| `Create` GET | 141 | `ContactViewModel.AvailableTagNames` — authoring autocomplete | unchanged, board-wide |
| `Edit` GET | 270-271 | `ContactViewModel.AvailableTagNames` — authoring autocomplete | unchanged, board-wide |
| `PopulateTagSuggestionsAsync` | 627-633 | authoring autocomplete on invalid-model re-render | unchanged, board-wide |

`BuildTagVocabulary` (711) takes an `IEnumerable<Contact>` and needs no change — the narrowing
happens in its caller, on the collection handed to it.

**Why no Razor edits are needed.** Every render site already guards on its list being non-empty, so
emptying the list server-side suppresses the wrapper and its contents together:

- `Views/Contacts/Index.cshtml:45` — `@if (Model.ViewerIsDmTier && contact.Tags.Any())`
- `Views/Contacts/Index.Mobile.cshtml:30` — `@if (Model.ViewerIsDmTier && contact.Tags.Count > 0)`
- `Views/Contacts/Details.cshtml:36` — `@if (Model.CanManage && Model.Tags.Any())`
- `Views/Contacts/Details.Mobile.cshtml:40` — `@if (Model.CanManage && Model.Tags.Count > 0)`
- `Views/Contacts/Index.cshtml:99-131` — `@if (Model.AvailableTags.Any())` renders the
  `contact-filter-row` form, `else` renders the `contact-filter-empty` hint ("No tags yet. Add tags
  when creating or editing a contact to start filtering."). The mobile view has the equivalent pair:
  an enabled "Filter Tags" trigger plus the `contactFilterOffcanvas` drawer, or a `disabled>` trigger
  plus the same hint text.

Duplicating the ownership comparison into Razor would require threading a current-user id onto
`ContactsIndexViewModel` and would put one rule in two places that can drift. The existing emptiness
guards *are* the view-level defence; leave all four view files untouched.

**Test helpers available:**

```
TestDataHelper.CreateTestContactAsync(services, createdByUserId, name, townCity, subLocation, isRevealed, groupId, imageData)
TestDataHelper.CreateTestContactTagAsync(services, name, groupId, params int[] contactIds)
TestDataHelper.ClearDatabaseAsync(services)
AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory, userName, email, roles: [...])
AuthenticationHelper.CreateTestUserAsync(services, userName, email, password, displayName)
```

`CreateAuthenticatedClientWithUserAsync` returns `(HttpClient, UserEntity)` — the `UserEntity.Id` is
what `CreateTestContactAsync` wants as `createdByUserId`, which is how a second, non-owning DM-tier
client is built. `CreateTestUserAsync` returns a bare user entity for an owner who never issues a
request. See `ContactsTagsMobileTests.Index_MobileTagOnlyOnUnrevealedContact_AbsentFromDrawer`
(ContactsTagsMobileTests.cs:176) for the exact two-owner seeding rhythm.

Show Hidden is flipped by `POST /Contacts/ToggleShowHidden` with `new FormUrlEncodedContent([])`,
accepting `Redirect`/`Found`/`OK`. It is a session-backed per-board toggle, so the *same*
`HttpClient` must be reused across the before/after assertions.

Mobile requests must carry a real mobile User-Agent — devtools-style emulation never selects the
`.Mobile.cshtml` files. `ContactsTagsMobileTests` already holds the `MobileUserAgent` constant and a
private `SendAsync(client, url, userAgent, authorization)` helper (ContactsTagsMobileTests.cs:15-36);
the mobile assertion belongs in that file so it can reach them.

</interface_context>

<scope_boundaries>

Explicitly **out of scope** — do not change these even if they look adjacent:

- `IsVisibleTo` and every hidden-contact filtering path. Whether a contact appears at all is untouched.
- `CanManage`, the Create/Edit forms, `TagsInput`, and the *content* of `AvailableTagNames`. The
  authoring suggestion whitelist keeps deriving from the full board-visible vocabulary; only its
  call into the vocabulary helper gains an explicit scope argument naming that existing behaviour.
- The `[Authorize(Policy = "DungeonMasterOnly")]` attributes on Create/Edit/Delete/ToggleReveal/ToggleShowHidden.
- `ApplyTagFilter`, the `tag` query-string binding, and the `selectedTagIds` derivation. Restricting
  `AvailableTags` is the whole mechanism: a viewer is never offered a filter option for a tag they
  cannot see. Do not additionally intersect `selectedTagIds` against `AvailableTags` — doing so would
  change `Index_SelectedTagOnUnrevealedContact_StaysHiddenWhileShowHiddenIsOff`'s contract and is not
  what was asked for.
- `BuildTagVocabulary` itself, and its comment, which stays true: it still projects exactly the
  collection it is handed.
- Any `.cshtml` file. Any new toggle, button, or session key — the one existing
  `SessionKeys.ShowHiddenContactsKey(groupId)` flag now governs all three behaviours.

</scope_boundaries>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Gate contact tag chips on ownership or the Show Hidden toggle</name>
  <files>QuestBoard.Service/Controllers/Contacts/ContactsController.cs</files>
  <behavior>
    Truth table the new gate must satisfy (viewer is always authenticated; `includeHidden` is
    the already-computed `viewerIsDmTier && ReadShowHiddenToggle()`):

    - viewerIsDmTier = false, any owner, any toggle -> Tags emptied (today's behaviour, preserved)
    - viewerIsDmTier = true, currentUserId == contact.CreatedByUserId, toggle off -> Tags kept
    - viewerIsDmTier = true, currentUserId == contact.CreatedByUserId, toggle on  -> Tags kept
    - viewerIsDmTier = true, currentUserId != contact.CreatedByUserId, toggle off -> Tags emptied
    - viewerIsDmTier = true, currentUserId != contact.CreatedByUserId, toggle on  -> Tags kept
    - currentUserId == 0 -> Tags emptied (ownership can never match a zero id)
  </behavior>
  <action>
Add one private static helper to `ContactsController`, placed immediately after the existing
`IsVisibleTo` helper (around ContactsController.cs:686) so the two ownership rules read together:

`private static bool AreTagsVisibleTo(int createdByUserId, int currentUserId, bool viewerIsDmTier, bool includeHidden)`

Return `false` when `viewerIsDmTier` is false. Otherwise return `true` when
`currentUserId != 0 && createdByUserId == currentUserId`, else return `includeHidden`. Guard the zero
id exactly the way `IsVisibleTo` does — an unresolved user must never match a contact whose
`CreatedByUserId` happens to be 0. Take the owner as a bare `int` rather than a `Contact`, because
Task 2 calls this same predicate from a path that has domain `Contact` instances while these two call
sites have view models.

Then widen the two existing post-mapping fixups to call it:

1. In `Index` (lines 56-64), keep `vm.CanManage = viewerIsDmTier;` exactly as it is and replace the
   `if (!viewerIsDmTier)` condition with a call to the new helper passing `vm.CreatedByUserId`,
   `currentUser.Id`, `viewerIsDmTier`, and the local `includeHidden`; empty `vm.Tags` when it
   returns false.
2. In `Details` (lines 124-129), make the identical substitution against the single `viewModel`,
   passing `viewModel.CreatedByUserId` and the local `currentUser.Id` / `viewerIsDmTier` /
   `includeHidden`.

Do not touch any `.cshtml` file — the four chip guards already test the list for emptiness, which is
what makes emptying the list server-side sufficient and keeps the rule in exactly one place.
Do not add a view model property, an AutoMapper mapping, or a second session flag. Leave
`GetVisibleTagVocabularyAsync` alone in this task; Task 2 owns it.

Write a short comment above the helper explaining, in plain language that stays true after this
task closes, that tag badges follow the same owner-or-toggle rule the hidden-contact check uses so
a DM-tier viewer does not read another author's tags on a board they are playing on. Per CLAUDE.md,
that comment must not name a phase number, plan number, requirement id, or review-finding id, and
must not name any specific role such as SuperAdmin as the motivating case — the rule is universal.

Save the file with CRLF line endings (this repo has no `.gitattributes`, and the Windows/CRLF
convention in CLAUDE.md is enforced by convention only).
  </action>
  <verify>
    <automated>dotnet build QuestBoard.Service/QuestBoard.Service.csproj -v q</automated>
    <automated>test $(grep -c 'AreTagsVisibleTo' QuestBoard.Service/Controllers/Contacts/ContactsController.cs) -ge 3</automated>
    <automated>test $(git status --porcelain -- QuestBoard.Service/Views/Contacts/ | wc -l) -eq 0</automated>
    <automated>test $(grep -cE 'Phase [0-9]|D-[0-9]{2}|TAGOWN-' QuestBoard.Service/Controllers/Contacts/ContactsController.cs) -eq 0</automated>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagsDesktopMarkupTests|FullyQualifiedName~ContactsTagsMobileTests|FullyQualifiedName~ContactsTagsFormMarkupTests" -v q</automated>
  </verify>
  <done>`AreTagsVisibleTo` exists as a private static helper next to `IsVisibleTo`; both `Index` and `Details` empty `Tags` through it; the Service project builds; zero files under `Views/Contacts/` are modified; no planning identifier appears in the controller's code or comments; the whole pre-existing tag test surface is still green, because every one of those tests requests as the contact's own creator or as a Player.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Scope the Index filter vocabulary the same way, without touching the authoring suggestion list</name>
  <files>QuestBoard.Service/Controllers/Contacts/ContactsController.cs, QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs, QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs</files>
  <behavior>
    Two callers, two outcomes, one derivation:

    - Index filter vocabulary, DM-tier viewer, toggle off -> only tag names carried by contacts the
      viewer created. A board where the viewer owns nothing tagged yields an empty list, which the
      existing view branch renders as the "No tags yet" hint rather than an empty checkbox row.
    - Index filter vocabulary, DM-tier viewer, toggle on -> every tag name on every visible contact,
      exactly as today.
    - Index filter vocabulary, non-DM-tier viewer -> empty, exactly as today.
    - Create GET / Edit GET / invalid-model re-render, DM-tier viewer, toggle off -> every tag name on
      every visible contact, including contacts the viewer did not create. Unchanged from today.
  </behavior>
  <action>
Parameterize the vocabulary derivation so the two surfaces are named rather than implied.

1. Add a nested private enum to `ContactsController`, declared next to `GetVisibleTagVocabularyAsync`:
   `private enum TagVocabularyScope { Authoring, ViewerVisible }`. `Authoring` is the existing
   board-wide behaviour used by the create/edit suggestion whitelist; `ViewerVisible` is the new
   narrowed behaviour used by the index filter row. Give the enum a short comment stating that the
   two members exist precisely because the filter row and the authoring autocomplete are allowed to
   differ, and that widening `Authoring` to match `ViewerVisible` would stop a DM being able to reuse
   a colleague's existing tag name.

2. Add the scope as a required parameter on `GetVisibleTagVocabularyAsync` — required, not optional
   with a default, so a future call site cannot silently inherit either behaviour. Inside the method,
   after the existing `IsVisibleTo` narrowing, apply a second narrowing only when the scope is
   `ViewerVisible`: keep a contact when `AreTagsVisibleTo(c.CreatedByUserId, currentUserId, true, includeHidden)`
   returns true, reusing the Task 1 predicate rather than restating the comparison. Hand the resulting
   collection to the untouched `BuildTagVocabulary`.

3. Update the four call sites: `Index` (line 48-49) passes `TagVocabularyScope.ViewerVisible`;
   `Create` GET (141), `Edit` GET (270-271), and `PopulateTagSuggestionsAsync` (627-633) each pass
   `TagVocabularyScope.Authoring`.

4. Correct the three comments this change makes untrue. All three must be rewritten in plain language
   with no planning identifiers:
   - `Index` lines 44-47 currently claims the vocabulary is derived through the same shared helper the
     create/edit suggestion lists use "so the two surfaces cannot drift apart". Rewrite: the shared
     helper is still the single derivation, but the index asks for the viewer-visible scope while the
     authoring forms ask for the board-wide one, and say why (a filter option for a tag whose chip the
     viewer cannot see would disclose the name the chip gate just withheld).
   - The comment above `GetVisibleTagVocabularyAsync` (688-691) makes the same "cannot drift apart"
     claim. Rewrite it to describe the scope parameter and what each member means.
   - `ContactsIndexViewModel.AvailableTags` (ContactsIndexViewModel.cs:27) says "The tags carried by
     contacts this viewer can see, which is the whole filter vocabulary." Rewrite to say these are the
     tags whose chips this viewer can see, which is the whole filter vocabulary. This is a comment-only
     edit — do not add, remove, or retype any member in that file.

5. Adapt the one pre-existing test this behaviour change invalidates:
   `ContactsTagsDesktopMarkupTests.Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn`
   (ContactsTagsDesktopMarkupTests.cs:100). Today it has one creator own both contacts and requests as
   a second DM, then asserts the revealed contact's `town-guard` name is present before the toggle —
   which is exactly what this task stops being true. Re-seed it so ownership is held constant and only
   revealed-ness varies, mirroring the shape `ContactsTagsMobileTests.Index_MobileTagOnlyOnUnrevealedContact_AbsentFromDrawer`
   already uses: the *requesting* DM owns the revealed `town-guard` contact, and a second owner created
   via `AuthenticationHelper.CreateTestUserAsync` (no client needed) owns the unrevealed `secret-tag`
   contact. Every assertion in the test body then stays exactly as written. Replace the test's
   two-line comment about the creator's own-contact visibility exemption with one describing the new
   seed: the requester owns the revealed contact so its name is present on its own merits, and the
   unrevealed contact belongs to someone else so only the toggle can surface its tag.

Do not touch any `.cshtml` file. Do not change `ApplyTagFilter`, `BuildTagVocabulary`, `IsVisibleTo`,
`selectedTagIds`, or any `[Authorize]` attribute. Save every edited file with CRLF line endings.
  </action>
  <verify>
    <automated>dotnet build QuestBoard.Service/QuestBoard.Service.csproj -v q</automated>
    <automated>test $(grep -c 'TagVocabularyScope' QuestBoard.Service/Controllers/Contacts/ContactsController.cs) -ge 6</automated>
    <automated>test $(grep -c 'GetVisibleTagVocabularyAsync' QuestBoard.Service/Controllers/Contacts/ContactsController.cs) -ge 5</automated>
    <automated>test $(git status --porcelain -- QuestBoard.Service/Views/Contacts/ | wc -l) -eq 0</automated>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagsFormMarkupTests" -v q</automated>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagsDesktopMarkupTests|FullyQualifiedName~ContactsTagsMobileTests|FullyQualifiedName~ContactsControllerIntegrationTests" -v q</automated>
  </verify>
  <done>The vocabulary helper takes a required scope; the index passes the viewer-visible scope and all three authoring call sites pass the board-wide one; `ContactsTagsFormMarkupTests` passes untouched, proving the authoring whitelist did not narrow; the re-seeded desktop vocabulary test passes with its assertions unchanged; every other pre-existing Contacts test still passes; zero view files modified.</done>
</task>

<task type="auto">
  <name>Task 3: Integration coverage for the ownership x toggle x role x surface matrix</name>
  <files>QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs, QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs</files>
  <action>
Create `QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs` in namespace
`QuestBoard.IntegrationTests.Controllers`, as
`public class ContactsTagOwnershipTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>`,
following the clear-seed-authenticate rhythm of `ContactsTagsDesktopMarkupTests`. Every test starts
with `await TestDataHelper.ClearDatabaseAsync(factory.Services);`.

Seed shape shared by these tests: one owning DM, one revealed contact created by that DM carrying a
distinctive hyphenated tag name, and a second DM-tier client that did not create it. Use
`isRevealed: true` and `groupId: 1` throughout so contact visibility is never the thing under test — a
hidden contact would confound the tag assertion with the pre-existing hidden-contact rule. Choose tag
names that cannot collide as substrings with page chrome (for example `owner-only-tag`,
`shared-vocab-tag`).

Write these facts:

1. `Index_OwningDungeonMaster_StillSeesOwnTagChipsAndFilterOption` — the owner's client GETs
   `/Contacts/Index`, asserts 200 and that the html contains `contact-tag-chip`, the tag name, and
   `contact-filter-row`. Proves the change did not regress the ordinary DM case on either surface.
2. `Index_NonOwningDungeonMaster_SeesNeitherChipsNorFilterOptionWhileShowHiddenIsOff` — the second
   DM's client GETs `/Contacts/Index`, asserts 200, no `contact-tag-chip`, no occurrence of the tag
   name anywhere in the body, and `contact-filter-empty` present. Seed nothing tagged for this viewer,
   so the empty-hint branch is the one that must render. The tag-name assertion is the point of the
   fact: before this work the name still reached the browser through the filter checkboxes.
3. `Index_NonOwningDungeonMaster_SeesChipsAndFilterOptionAfterTogglingShowHidden` — same non-owning
   client asserts absence, POSTs `/Contacts/ToggleShowHidden` with `new FormUrlEncodedContent([])`
   accepting `Redirect`/`Found`/`OK`, re-GETs and asserts `contact-tag-chip`, the tag name, and
   `contact-filter-row` are all now present, then POSTs the toggle a second time, re-GETs, and asserts
   chips and name are gone again. The toggle is session-backed per board, so reuse the same
   `HttpClient` for all four requests.
4. `Details_NonOwningDungeonMaster_SeesNoTagChipsUntilShowHiddenIsOn` — the non-owning client GETs
   `/Contacts/Details/{id}`, asserts 200 with no `contact-tag-chip` and no tag name, then toggles Show
   Hidden on, re-GETs, and asserts both are present.
5. `Details_OwningDungeonMaster_StillSeesOwnTagChips` — the owner's client GETs its own contact's
   Details and asserts the chip class and the tag name are present with the toggle off.
6. `Index_Player_StillSeesNoTagMarkupRegardlessOfOwnership` — a `["Player"]` client GETs the index and
   asserts no `contact-tag-chip`, no `contact-filter-row`, no `contact-filter-empty`, and no tag name,
   confirming widening the condition did not open a path for players on either surface.
7. `CreateAndEditForms_NonOwningDungeonMaster_StillSuggestEveryBoardTagName` — the fact that pins the
   deliberate asymmetry, and the one most likely to be broken by a later well-meaning "consistency"
   change. Seed a revealed contact owned by a first DM carrying `shared-vocab-tag`, plus a second,
   separately owned revealed contact belonging to the requesting DM so there is something for that DM
   to edit. With the Show Hidden toggle OFF, the requesting DM: GETs `/Contacts/Create` and asserts the
   body contains `shared-vocab-tag`; GETs `/Contacts/Edit/{ownContactId}` and asserts the same; then
   GETs `/Contacts/Index` and asserts the body does NOT contain `shared-vocab-tag`. Comment the fact to
   say the authoring autocomplete is intentionally board-wide so a DM can reuse a colleague's tag,
   while the filter row is intentionally viewer-scoped, and that this test exists to keep the two from
   being "unified" by mistake.

Then append one fact to the existing `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs`
(it owns the `MobileUserAgent` constant and the private `SendAsync` helper the mobile views need):

8. `Index_MobileNonOwningDungeonMaster_SeesNoChipsOrDrawerOptionsUntilShowHiddenIsOn` — seed an owning
   DM (created with `CreateTestUserAsync`, no client needed) and a revealed tagged contact, build a
   DM-tier client that owns nothing, send `/Contacts/Index` through `SendAsync` with `MobileUserAgent`,
   assert 200, no `contact-tag-chip`, no tag name, and the disabled-trigger hint text "No tags yet. Add
   tags when creating or editing a contact to start filtering."; POST `/Contacts/ToggleShowHidden` on
   that same client; re-send with `MobileUserAgent` and assert `contact-tag-chip`, the tag name, and
   `contactFilterOffcanvas` are present. Add a brief comment noting the mobile view is chosen by the
   User-Agent header, matching the file's existing convention. Do not modify any existing fact in that
   file.

Use xUnit + FluentAssertions and `TestContext.Current.CancellationToken` on every async call, matching
the surrounding files. Save both files with CRLF line endings — the Write tool emits LF, and a
recently added test file in this repo needed a post-write conversion for exactly this reason.
  </action>
  <verify>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagOwnershipTests" -v q</automated>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagsMobileTests" -v q</automated>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~Contact" -v q</automated>
  </verify>
  <done>All seven new desktop facts and the one new mobile fact pass, including the create/edit-versus-index asymmetry fact. Every pre-existing Contacts test still passes.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| authenticated viewer -> Contacts/Index and Contacts/Details response body | DM-authored tag names cross into a browser session whose viewer may be role-bypassed (SuperAdmin resolves to `GroupRole.Admin` on every board) |
| authenticated viewer -> Contacts/Create and Contacts/Edit response body | the authoring autocomplete deliberately carries the board's whole tag vocabulary across the same boundary |
| browser -> POST /Contacts/ToggleShowHidden | the single per-board session flag that now widens three different disclosures at once |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-mcb-01 | Information Disclosure | `ContactsController.Index` / `Details` tag chip projection | medium | mitigate | Empty `ContactViewModel.Tags` server-side via `AreTagsVisibleTo` so non-owned tag names never reach the response body; a CSS-only or client-side hide is explicitly rejected |
| T-mcb-02 | Information Disclosure | `ContactsIndexViewModel.AvailableTags` filter-row and drawer vocabulary | medium | mitigate | Narrow the vocabulary with the same `AreTagsVisibleTo` predicate under `TagVocabularyScope.ViewerVisible`, closing the residual channel that would otherwise have re-disclosed by name every tag the chip gate withheld |
| T-mcb-03 | Elevation of Privilege | `AreTagsVisibleTo` zero-id branch | low | mitigate | Guard `currentUserId != 0` before comparing against `CreatedByUserId`, mirroring `IsVisibleTo`, so an unresolved user cannot match a contact stamped with owner id 0 |
| T-mcb-04 | Information Disclosure | Create/Edit `ContactViewModel.AvailableTagNames` authoring whitelist | medium | accept | Accepted by explicit user decision: a DM authoring a contact must be able to reuse any tag already in use on the board, whoever introduced it. Bounded by the `DungeonMasterOnly` policy (only DM-tier viewers reach either form) and by the unchanged `IsVisibleTo` narrowing (tags borne solely by contacts the viewer cannot see are still excluded). Pinned by an integration fact so it stays a decision rather than becoming an accident |
| T-mcb-05 | Information Disclosure | `tag` query-string parameter vs. the narrowed vocabulary | low | accept | A DM-tier viewer can still hand-craft `?tag={id}` for an id no longer offered to them and observe which already-visible contacts carry it. No tag *name* is disclosed by this, contact visibility is unchanged, and the ids would have to be guessed. Closing it would mean intersecting `selectedTagIds` against `AvailableTags`, which the scope decision explicitly rules out |
| T-mcb-06 | Tampering | new package installs | low | accept | No package-manager install in this task; no new dependency is added |
</threat_model>

<verification>
1. `dotnet build` succeeds for the whole solution.
2. Full Contacts test surface green: `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~Contact" -v q`.
3. `git diff --stat` shows exactly five files touched: `ContactsController.cs`, `ContactsIndexViewModel.cs` (comment only), `ContactsTagsDesktopMarkupTests.cs`, the new `ContactsTagOwnershipTests.cs`, and `ContactsTagsMobileTests.cs`. Zero `.cshtml` files, zero AutoMapper profiles, zero migrations.
4. `git diff -- QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs` shows comment lines only — no member added, removed, renamed, or retyped.
5. `git diff -- QuestBoard.Service/Controllers/Contacts/ContactsController.cs` shows no behavioural change to `IsVisibleTo`, `BuildTagVocabulary`, `ApplyTagFilter`, or any `[Authorize]` attribute, and no change to what `PopulateTagSuggestionsAsync` produces beyond passing the board-wide scope explicitly.
6. `git diff -- QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs` touches exactly one fact — the re-seeded vocabulary test — and changes its seeding and comment, not its assertions.
</verification>

<success_criteria>
- A DM-tier viewer sees tag chips only on contacts they created, unless the existing Show Hidden toggle is on for that board.
- The same viewer is offered filter options only for tag names whose chips they can see, so the filter row can no longer re-disclose a withheld name.
- The Create/Edit tag-suggestion whitelist still offers every board tag name regardless of ownership, and an integration fact pins that asymmetry as intentional.
- The rule is identical on desktop and mobile, Index and Details.
- Non-DM-tier viewers still receive no tag markup at all.
- Tag names a viewer may not see are absent from the HTML, not merely visually suppressed.
- No second toggle, no new session key, no view change, no view model member change.
- Every pre-existing Contacts test still passes, with a single re-seeded fact whose assertions are unchanged.
</success_criteria>

<output>
Create `.planning/quick/260831-mcb-contact-tags-show-tags-only-for-owned-co/260831-mcb-SUMMARY.md` when done.
</output>
