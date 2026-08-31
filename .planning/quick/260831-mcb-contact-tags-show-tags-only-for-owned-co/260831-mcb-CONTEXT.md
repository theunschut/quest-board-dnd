# Quick Task 260831-mcb: Contact tags visibility tied to ownership + ShowHidden toggle - Context

**Gathered:** 2026-08-31
**Status:** Ready for planning

<domain>
## Task Boundary

Phase 81 added tags to Contacts. Tags are currently shown to any DM-tier viewer
(`ViewerIsDmTier` / `CanManage`), regardless of who created the contact. This
means an Admin (in practice: a `SuperAdmin`, whose effective board role always
bypasses to `GroupRole.Admin` even when their actual membership on that board
is `Player`) sees tags on contacts they don't own, on boards where they're
meant to be experiencing things as a player.

There is already a working mechanism for an analogous problem: hidden contacts
(`Contact.IsRevealed == false`) are visible only to their creator
(`CreatedByUserId == currentUserId`) unless a DM-tier viewer flips the
"Show Hidden" toggle (`ContactsController.ToggleShowHidden`, session-backed per
board via `SessionKeys.ShowHiddenContactsKey(groupId)`, read via
`ReadShowHiddenToggle()`).

This task extends that same ownership + toggle pattern to tag *visibility*
(not contact visibility, not tag *editability*/`CanManage`). Tags for a
contact should render only if the viewer owns the contact OR the ShowHidden
toggle is on for that board. This applies on top of the existing
`ViewerIsDmTier` / `CanManage` gate — non-DM-tier viewers still never see tags,
same as today.

</domain>

<decisions>
## Implementation Decisions

### Scope: universal rule vs. SuperAdmin-bypass-only
- **Decision: Universal rule.** The owned/toggle tag rule applies to every
  DM-tier viewer, not just the SuperAdmin-viewing-as-player edge case.
- Rationale (user-confirmed): matches the mental model of the existing
  hidden-contact toggle (owner-or-toggle-on to see it) — one consistent rule
  rather than a special case that needs detecting "true" board role vs.
  bypassed role.
- Known side effect, explicitly accepted by the user: on a board with two real
  co-DMs, DM-A will not see tags on DM-B's contacts by default — DM-A must use
  the "Show Hidden" toggle to reveal them. This is intentional, not a bug to
  avoid.

### Reuse existing toggle, not a new one
- Do not add a second button/flag for tags. Reuse the same `ShowHidden`
  session flag/toggle that already governs hidden-contact visibility
  (`ToggleShowHidden`, `SessionKeys.ShowHiddenContactsKey(groupId)`,
  `ReadShowHiddenToggle()`). One toggle now governs both "see hidden
  contacts" and "see tags on contacts you don't own."

### What does NOT change
- Contact visibility itself (`IsVisibleTo` / the hidden-contact filtering
  logic) is untouched — this task is only about whether *tag badges* render
  for a contact that IS visible.
- Tag *edit* rights (`CanManage`, Create/Edit forms) are untouched — this is
  a read-only display change on Index and Details views (desktop + mobile).
- The `[Authorize(Policy = "DungeonMasterOnly")]` gates on Create/Edit/Delete
  are untouched.

### Claude's Discretion
- Exact server-side vs. view-level implementation approach (e.g. adding an
  `IsOwnedByCurrentUser` bool to `ContactViewModel`/`ContactTagViewModel` vs.
  computing ownership inline in the view) — pick whichever fits the existing
  code style in `ContactsController` / `ContactViewModel` most cleanly.
- Whether to strip `Tags` server-side (defense in depth, matching the existing
  `if (!viewerIsDmTier) { vm.Tags = []; }` pattern) in addition to gating the
  Razor `@if` — recommended for consistency with how hidden-contact tags are
  already defended in depth today.

### Filter dropdown scope (added after initial planning pass)
- **Decision: Extend scope to the filter dropdown too.** The Index page's tag
  filter vocabulary (`ContactsIndexViewModel.AvailableTags`, derived by
  `GetVisibleTagVocabularyAsync`) must also respect ownership/toggle — a
  viewer should not see a tag *name* in the filter dropdown (or be able to
  filter by it) for a contact whose chip they can't see. Tag names from
  non-owned, non-toggle-visible contacts should be excluded from
  `AvailableTags` for that viewer.
- Constraint: `GetVisibleTagVocabularyAsync` is shared with
  `PopulateTagSuggestionsAsync`, which feeds the Create/Edit tag-suggestion
  whitelist. That whitelist is for authoring (autocomplete when typing a new
  tag) and must NOT be scoped down by ownership — a DM creating/editing a
  contact should still be able to reuse any tag already in use on the board,
  regardless of who created the contact that introduced it. Only the
  Index-page filter-row derivation should apply the ownership/toggle
  restriction; the Create/Edit suggestion path stays as it is today. Split or
  parameterize the helper rather than changing its one existing behavior
  wholesale.
- Filtering-by-tag behavior: once a tag name is excluded from a viewer's
  `AvailableTags`, filtering should naturally follow (the tag isn't offered as
  a filter option for that viewer). A contact whose only matching tag is
  hidden from that viewer should not surface via a filter the viewer can't
  even select — this falls out of restricting `AvailableTags` correctly, no
  separate filtering logic should need to change.

</decisions>

<specifics>
## Specific Ideas

Views affected (from exploration):
- `Views\Contacts\Index.cshtml:45` — `@if (Model.ViewerIsDmTier && contact.Tags.Any())`
- `Views\Contacts\Index.Mobile.cshtml:30` — same pattern
- `Views\Contacts\Details.cshtml:36` — `@if (Model.CanManage && Model.Tags.Any())`
- `Views\Contacts\Details.Mobile.cshtml:40` — same pattern
- `ContactsController.cs` Index/Details — server-side tag stripping
  (`if (!viewerIsDmTier) { vm.Tags = []; }`)

Ownership field: `Contact.CreatedByUserId` (`QuestBoard.Domain\Models\Contact.cs`,
`QuestBoard.Repository\Entities\ContactEntity.cs`). Note the existing comment
in `ContactViewModel.cs:33-34` explicitly says "There is no owner concept for
Contacts" (referring to edit/delete rights, i.e. `CanManage`) — this task
introduces ownership as a *display* concept for tags specifically, without
touching `CanManage`.

</specifics>

<canonical_refs>
## Canonical References

No external specs — requirements fully captured in decisions above. See
Phase 81 (tags) and the existing hidden-contact toggle implementation in
`ContactsController.cs` for the pattern being extended.

</canonical_refs>
