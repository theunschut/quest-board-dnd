---
phase: quick-260831-mcb
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs
  - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
autonomous: true
requirements: [TAGOWN-01]

must_haves:
  truths:
    - "A DM-tier viewer who created a contact sees that contact's tag chips on Contacts/Index and Contacts/Details, with the Show Hidden toggle off — unchanged from today."
    - "A DM-tier viewer who did NOT create a contact sees no tag chips for it on Contacts/Index or Contacts/Details while the Show Hidden toggle is off."
    - "The same non-owning DM-tier viewer sees that contact's tag chips after POSTing to /Contacts/ToggleShowHidden (toggle on), and loses them again after toggling back off."
    - "The rule holds identically on the mobile views (Index.Mobile, Details.Mobile), which are selected by User-Agent."
    - "A non-DM-tier viewer (Player) still receives zero tag markup regardless of ownership or toggle state — the existing gate is unchanged."
    - "Tag names for a non-owned contact are absent from the response body of Index/Details, not merely hidden by CSS — the ContactViewModel.Tags list is emptied server-side."
    - "Contact visibility (IsVisibleTo), tag edit rights (CanManage), the DungeonMasterOnly policy gates, and the Create/Edit tag input and suggestion whitelist are all byte-for-byte unchanged."
    - "No source comment added by this work names a phase number, plan number, requirement id, or review-finding id."
  artifacts:
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs
    - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
  key_links:
    - "ContactsController.Index -> per-viewmodel tag gate reading vm.CreatedByUserId + currentUser.Id + viewerIsDmTier + includeHidden -> vm.Tags = [] when the gate fails."
    - "ContactsController.Details -> same gate on the single mapped viewModel -> viewModel.Tags = [] when the gate fails."
    - "Emptied Tags list -> the four existing Razor chip guards (Index.cshtml:45, Index.Mobile.cshtml:30, Details.cshtml:36, Details.Mobile.cshtml:40) already test Tags.Any()/Tags.Count > 0, so no chip wrapper renders and no view file needs editing."
    - "ReadShowHiddenToggle() -> SessionKeys.ShowHiddenContactsKey(groupId) -> the single per-board session flag that now governs both hidden-contact visibility and non-owned tag visibility."
---

<objective>
Extend the existing ownership + Show Hidden toggle pattern from *contact* visibility to *tag badge* visibility. Today any DM-tier viewer sees tag chips on every visible contact regardless of who created it, which means a SuperAdmin experiencing a board as a player (their effective board role always bypasses to `GroupRole.Admin`) sees DM-authored tags they were never meant to see.

Purpose: one consistent mental model — "you see it if you own it, or if you flipped Show Hidden" — rather than a special case that would need to distinguish a true board role from a bypassed one. The known and user-accepted side effect is that on a board with two real co-DMs, DM-A no longer sees tags on DM-B's contacts until DM-A turns Show Hidden on.

Output: a single server-side gate in `ContactsController` (Index + Details), plus integration coverage of the full ownership × toggle × role matrix on desktop and mobile. No Razor view changes and no new toggle.
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
@QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
@QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
@QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs
</context>

<interface_context>

**Ownership is already on the view model — nothing new to plumb.**

`ContactViewModel.CreatedByUserId` (ContactViewModel.cs:35) is populated by convention through the
`CreateMap<Contact, ContactViewModel>()` profile in `QuestBoard.Service/Automapper/ViewModelProfile.cs:83`
(the member is not in the `Ignore()` list). Both `Index` and `Details` already have `currentUser.Id`
in scope. No view model field, no AutoMapper change, and no `ContactsIndexViewModel` change is needed.

**The existing sibling helper to mirror** (`ContactsController.cs:673`):

```
private static bool IsVisibleTo(Contact contact, int currentUserId, bool includeHidden)
```

Three-branch, `static`, guards `currentUserId != 0` before comparing `CreatedByUserId`. The new tag
gate should be a sibling static helper on the same class in the same shape and placed next to it.

**The two call sites that already do per-viewmodel post-mapping fixups:**

- `ContactsController.Index` lines 56-64 — `foreach (var vm in contactViewModels) { vm.CanManage = viewerIsDmTier; if (!viewerIsDmTier) { vm.Tags = []; } }`
- `ContactsController.Details` lines 124-129 — `viewModel.CanManage = viewerIsDmTier; if (!viewerIsDmTier) { viewModel.Tags = []; }`

Both already have `viewerIsDmTier` and `includeHidden` computed locally
(`var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();`). The change is to widen the
existing `Tags = []` condition at both sites, not to add a second pass.

**Why no Razor edits are needed.** All four chip render sites already guard on the list being
non-empty, so emptying `Tags` server-side suppresses the wrapper div and the chips together:

- `Views/Contacts/Index.cshtml:45` — `@if (Model.ViewerIsDmTier && contact.Tags.Any())`
- `Views/Contacts/Index.Mobile.cshtml:30` — `@if (Model.ViewerIsDmTier && contact.Tags.Count > 0)`
- `Views/Contacts/Details.cshtml:36` — `@if (Model.CanManage && Model.Tags.Any())`
- `Views/Contacts/Details.Mobile.cshtml:40` — `@if (Model.CanManage && Model.Tags.Count > 0)`

Duplicating the ownership comparison into Razor would require threading a current-user id onto
`ContactsIndexViewModel` and would put the same rule in two places that can drift. The existing
emptiness guard *is* the view-level defence; leave all four files untouched.

**Test helpers available:**

```
TestDataHelper.CreateTestContactAsync(services, createdByUserId, name, townCity, subLocation, isRevealed, groupId, imageData)
TestDataHelper.CreateTestContactTagAsync(services, name, groupId, params int[] contactIds)
TestDataHelper.ClearDatabaseAsync(services)
AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory, userName, email, roles: [...])
```

`CreateAuthenticatedClientWithUserAsync` returns `(HttpClient, UserEntity)` — the `UserEntity.Id` is
what `CreateTestContactAsync` wants as `createdByUserId`, which is how a second, non-owning DM-tier
client is built (see `ContactsTagsDesktopMarkupTests.Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn`
at ContactsTagsDesktopMarkupTests.cs:101 for the exact two-DM setup rhythm).

Show Hidden is flipped by `POST /Contacts/ToggleShowHidden` with `new FormUrlEncodedContent([])`;
it is a session-backed per-board toggle, so the *same* `HttpClient` must be reused across the
before/after assertions.

Mobile requests must carry a real mobile User-Agent — devtools-style emulation never selects the
`.Mobile.cshtml` files. `ContactsTagsMobileTests` already holds the `MobileUserAgent` constant and a
private `SendAsync(client, url, userAgent, authorization)` helper (ContactsTagsMobileTests.cs:15-36);
the mobile assertion belongs in that file so it can reach them.

</interface_context>

<scope_boundaries>

Explicitly **out of scope** — do not change these even if they look adjacent:

- `IsVisibleTo` and every hidden-contact filtering path. Whether a contact appears at all is untouched.
- `CanManage`, the Create/Edit forms, `TagsInput`, `AvailableTagNames`, and `PopulateTagSuggestionsAsync`.
- The `[Authorize(Policy = "DungeonMasterOnly")]` attributes on Create/Edit/Delete/ToggleReveal/ToggleShowHidden.
- `GetVisibleTagVocabularyAsync` / `BuildTagVocabulary` / `ContactsIndexViewModel.AvailableTags` — the
  index filter row's vocabulary. It keeps deriving from the visible-but-unfiltered set exactly as today.
- `ApplyTagFilter`. A DM filtering by a tag that only a non-owned contact carries will still get that
  contact back in the list, now rendering with no chips. That is the accepted consequence of scoping
  this to badge rendering only; do not "fix" it by making the filter ownership-aware.
- Any new toggle, button, or session key. The one existing `SessionKeys.ShowHiddenContactsKey(groupId)`
  flag now governs both behaviours.

</scope_boundaries>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Gate contact tag rendering on ownership or the Show Hidden toggle</name>
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

Return `false` when `viewerIsDmTier` is false. Otherwise return `true` when `currentUserId != 0 && createdByUserId == currentUserId`, else return `includeHidden`. Guard the zero id exactly the way
`IsVisibleTo` does — an unresolved user must never match a contact whose `CreatedByUserId` happens
to be 0.

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
Do not add a view model property, an AutoMapper mapping, or a second session flag.

Write a short comment above the helper explaining, in plain language that stays true after this
task closes, that tag badges follow the same owner-or-toggle rule the hidden-contact check uses so
a DM-tier viewer does not read another author's tags on a board they are playing on. Per CLAUDE.md,
that comment must not name a phase, plan, requirement id, or review-finding id, and must not name
any specific role such as SuperAdmin as the motivating case — the rule is universal.

Save the file with CRLF line endings (this repo has no `.gitattributes`, and the Windows/CRLF
convention in CLAUDE.md is enforced by convention only).
  </action>
  <verify>
    <automated>dotnet build QuestBoard.Service/QuestBoard.Service.csproj -v q</automated>
    <automated>grep -c 'AreTagsVisibleTo' QuestBoard.Service/Controllers/Contacts/ContactsController.cs</automated>
    <automated>test $(git status --porcelain -- QuestBoard.Service/Views/Contacts/ | wc -l) -eq 0</automated>
    <automated>test $(grep -cE 'Phase [0-9]|D-[0-9]{2}|TAGOWN-' QuestBoard.Service/Controllers/Contacts/ContactsController.cs) -eq 0</automated>
  </verify>
  <done>`AreTagsVisibleTo` exists as a private static helper next to `IsVisibleTo`; both `Index` and `Details` empty `Tags` through it; the Service project builds; zero files under `Views/Contacts/` are modified; no planning identifier appears anywhere in the controller.</done>
</task>

<task type="auto">
  <name>Task 2: Integration coverage for the ownership x toggle x role matrix, desktop and mobile</name>
  <files>QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs, QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs</files>
  <action>
Create `QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs` in namespace
`QuestBoard.IntegrationTests.Controllers`, as
`public class ContactsTagOwnershipTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>`,
following the clear-seed-authenticate rhythm of `ContactsTagsDesktopMarkupTests`. Every test starts
with `await TestDataHelper.ClearDatabaseAsync(factory.Services);`.

Seed shape shared by the desktop tests: one owning DM (`CreateAuthenticatedClientWithUserAsync`,
roles `["DungeonMaster"]`), one revealed contact created by that DM carrying a distinctive tag name,
and a second DM-tier client that did not create it. Use `isRevealed: true` throughout so contact
visibility is never the thing under test — a hidden contact would confound the tag assertion with the
pre-existing hidden-contact rule.

Write these facts:

1. `Index_OwningDungeonMaster_StillSeesOwnTagChips` — owner's client GETs `/Contacts/Index`, asserts
   200 and that the html contains both `contact-tag-chip` and the tag name. Proves the change did not
   regress the ordinary DM case.
2. `Index_NonOwningDungeonMaster_SeesNoTagChipsWhileShowHiddenIsOff` — the second DM's client GETs
   `/Contacts/Index`, asserts 200 and `html.Should().NotContain("contact-tag-chip")`. Assert on the
   chip class rather than on the tag name: the tag name legitimately still appears in the index filter
   row vocabulary, which this task deliberately leaves alone, so a name-based assertion would fail for
   the wrong reason.
3. `Index_NonOwningDungeonMaster_SeesTagChipsAfterTogglingShowHidden` — same non-owning client asserts
   no chips, POSTs `/Contacts/ToggleShowHidden` with `new FormUrlEncodedContent([])` accepting
   `Redirect`/`Found`/`OK`, re-GETs the index and asserts `contact-tag-chip` and the tag name are now
   present, then POSTs the toggle a second time, re-GETs, and asserts the chips are gone again. The
   toggle is session-backed per board, so reuse the same `HttpClient` for all four requests.
4. `Details_NonOwningDungeonMaster_SeesNoTagChipsUntilShowHiddenIsOn` — the non-owning client GETs
   `/Contacts/Details/{id}`, asserts 200 with no `contact-tag-chip` and no tag name (on Details there
   is no filter row, so the name assertion is safe and worth having), then toggles Show Hidden on,
   re-GETs, and asserts both are present.
5. `Details_OwningDungeonMaster_StillSeesOwnTagChips` — owner's client GETs its own contact's Details
   and asserts the chip class and the tag name are present with the toggle off.
6. `Index_Player_StillSeesNoTagMarkupRegardlessOfToggle` — a `["Player"]` client GETs the index and
   asserts no `contact-tag-chip`, no `contact-filter-row`, and no tag name, confirming the non-DM-tier
   gate is unchanged and that widening the condition did not accidentally open a path for players.

Then append one fact to the existing
`QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs` (it owns the `MobileUserAgent`
constant and the private `SendAsync` helper the mobile views need):

7. `Index_MobileNonOwningDungeonMaster_SeesNoTagChipsUntilShowHiddenIsOn` — seed an owning DM and a
   revealed tagged contact, build a second DM-tier client, send `/Contacts/Index` through `SendAsync`
   with `MobileUserAgent`, assert 200 and no `contact-tag-chip`; POST `/Contacts/ToggleShowHidden` on
   that same client; re-send with `MobileUserAgent` and assert `contact-tag-chip` is present. Add a
   brief comment noting the mobile view is chosen by the User-Agent header, matching the file's
   existing convention. Do not modify any existing fact in that file.

Use xUnit + FluentAssertions and `TestContext.Current.CancellationToken` on every async call, matching
the surrounding files. Save both files with CRLF line endings — the Write tool emits LF, and a
recently added test file in this repo needed a post-write conversion for exactly this reason.
  </action>
  <verify>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagOwnershipTests" -v q</automated>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagsMobileTests" -v q</automated>
    <automated>dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~ContactsTagsDesktopMarkupTests|FullyQualifiedName~ContactsControllerIntegrationTests|FullyQualifiedName~ContactsTagsFormMarkupTests" -v q</automated>
  </verify>
  <done>All six new desktop facts and the one new mobile fact pass. Every pre-existing Contacts test still passes — in particular `ContactsTagsDesktopMarkupTests.Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn`, whose non-owning viewer asserts on tag *names* served by the filter-row vocabulary that this change leaves intact.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| authenticated viewer -> Contacts/Index and Contacts/Details response body | DM-authored tag names cross into a browser session whose viewer may be role-bypassed (SuperAdmin resolves to `GroupRole.Admin` on every board) |
| browser -> POST /Contacts/ToggleShowHidden | the single per-board session flag that now widens two different disclosures at once |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-mcb-01 | Information Disclosure | `ContactsController.Index` / `Details` tag projection | medium | mitigate | Empty `ContactViewModel.Tags` server-side via `AreTagsVisibleTo` so non-owned tag names never reach the response body; a CSS-only or client-side hide is explicitly rejected |
| T-mcb-02 | Information Disclosure | `ContactsIndexViewModel.AvailableTags` filter-row vocabulary | low | accept | Left unchanged by explicit scope decision — the vocabulary already respects contact visibility and the Show Hidden toggle, and the shared derivation also feeds the Create/Edit suggestion whitelist that this task must not alter. Surfaced to the operator as a follow-up decision, not silently dropped |
| T-mcb-03 | Elevation of Privilege | `AreTagsVisibleTo` zero-id branch | low | mitigate | Guard `currentUserId != 0` before comparing against `CreatedByUserId`, mirroring `IsVisibleTo`, so an unresolved user cannot match a contact stamped with owner id 0 |
| T-mcb-04 | Tampering | new package installs | low | accept | No package-manager install in this task; no new dependency is added |
</threat_model>

<verification>
1. `dotnet build` succeeds for the whole solution.
2. Full Contacts test surface green: `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~Contact" -v q`.
3. `git diff --stat` shows exactly three files touched: `ContactsController.cs`, the new `ContactsTagOwnershipTests.cs`, and `ContactsTagsMobileTests.cs`. Zero `.cshtml` files, zero view models, zero AutoMapper profiles, zero migrations.
4. `git diff -- QuestBoard.Service/Controllers/Contacts/ContactsController.cs` shows no change to `IsVisibleTo`, `GetVisibleTagVocabularyAsync`, `BuildTagVocabulary`, `ApplyTagFilter`, `PopulateTagSuggestionsAsync`, or any `[Authorize]` attribute.
</verification>

<success_criteria>
- A DM-tier viewer sees tag chips only on contacts they created, unless the existing Show Hidden toggle is on for that board.
- The rule is identical on desktop and mobile, Index and Details.
- Non-DM-tier viewers still receive no tag markup at all.
- Tag names for non-owned contacts are absent from the HTML, not merely visually suppressed.
- No second toggle, no new session key, no view or view model change.
- Every pre-existing Contacts test still passes.
</success_criteria>

<output>
Create `.planning/quick/260831-mcb-contact-tags-show-tags-only-for-owned-co/260831-mcb-SUMMARY.md` when done.
</output>
