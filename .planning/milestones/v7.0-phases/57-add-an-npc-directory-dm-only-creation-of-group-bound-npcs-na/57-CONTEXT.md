# Phase 57: Add an NPC directory: DM-only creation of group-bound NPCs (name, image, description, town/city, optional sub-location like a shop or smithy name) with a player-and-DM-editable list of freeform notes, plus dedicated Index/Details/Edit views mirroring the Characters pattern - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a new "Contacts" directory (final chosen name — see Naming below; the ROADMAP phase title calls this "NPCs") for DM-tier-created, group-bound world characters: name, image, description, town/city, optional sub-location (e.g. a shop/smithy name the contact runs). Each Contact also carries a freeform notes list that any group member (Player or DM-tier) can add to, edit, and delete from. Dedicated Index/Details/Edit/Create views (desktop + mobile) mirror the existing Characters (formerly Guild Members) feature's structure and modern-card UI conventions.

This phase executes before Phase 58 (the Guild Members → Characters rename) per ROADMAP dependency order, but should be built against the Characters feature's *current* file/class names (`GuildMembersController`, `Views/GuildMembers/`, `CharacterEntity`, etc.) since that's what exists on disk today — not the post-rename names Phase 58 will introduce.

Not in scope: quest/session linkage for Contacts, structured location taxonomy (towns are freeform text, not a managed list), server-side image cropping (mirrors Character's current *pre-crop-UI* upload behavior exactly, since Phases 45/46 — the crop UI — have not shipped yet as of this discussion).

</domain>

<decisions>
## Implementation Decisions

### Naming
- **D-01:** Feature name is **Contact / Contacts** — not "NPC". Considered and rejected during discussion: plain "NPC" (roadmap's working title), "World Characters", "Cast", "Denizen/Denizens", "Acquaintance/Acquaintances" (the English translation of the user's own suggestion, Dutch "Kennis/Kennissen" in the "person you know" sense), "Local/Locals", "Face/Faces". "Contact" won for being maximally genre-neutral and naturally singular/plural.
- **D-02:** Code naming follows this app's `[Feature]Entity`/`[Feature]Controller`/`[Feature]ViewModel` convention exactly: `ContactController`, `ContactEntity`, `ContactImageEntity` (mirrors `CharacterImageEntity`'s separate-table shape), `ContactViewModel`, `ContactsIndexViewModel`-equivalent, `ContactNote` (or similar) for the notes collection, `Views/Contacts/`, `ViewModels/ContactViewModels/`, route `/Contacts/*`, nav label "Contacts".

### Fields & structure
- **D-03:** `SubLocation` is its own structured optional string field (e.g. "The Guilded Rose Smithy"), not folded into the free-text description.
- **D-04:** `TownCity` is optional free text — no required value, no pre-defined/dropdown list of towns.
- **D-05:** `Description` is optional free text, `StringLength(2000)` — matches `CharacterEntity.Description`'s exact limit/convention.
- **D-06:** Contact image mirrors `CharacterEntity`/`CharacterImageEntity` exactly: optional, JPG/PNG/GIF only, 5 MB max (`MaxFileSizeAttribute`/`AllowedExtensionsAttribute` reused), stored as a separate `ContactImageEntity`-shaped table (`byte[] ImageData`, 1:1 FK-as-PK on the Contact's Id) — same as Character today, not the not-yet-shipped dual-image crop pipeline from Phases 45/46.
- **D-07:** Contact records a `CreatedByUserId` (recorded at creation, analogous to `Character.OwnerId` but carrying **no** ownership/edit-restriction meaning — see D-09) — used only for the "creator always sees their own hidden Contacts" visibility rule (D-12).

### Freeform notes list
- **D-08:** Notes are separate authored+timestamped entries (a running log) — each note is its own row (author, timestamp, text), not one shared editable text blob. New notes append; nothing is silently overwritten.
- **D-09 (correction of an initial answer):** During the authorization discussion, edit/delete of *notes* was decided as: **any group member can edit or delete any note**, no author-only or DM-override restriction — fully collaborative, no ownership check on the note itself. (Distinct from D-10/D-11, which govern the *Contact record's* own core fields.)
- **D-10:** Notes are displayed **newest first**.
- **D-11:** Notes use the same `StringLength(2000)` convention as `Description` — no cap on the number of notes per Contact.
- **D-16 (UI placement):** The add/edit/delete-note UI lives directly **on the Details page** (not a separate dedicated sub-page) — an add-note form plus the list with per-note edit/delete controls, visible to everyone who can view Details.

### Contact record authorization
- **D-09b:** Create/Edit/Delete of the Contact's own core fields (name, image, description, town/city, sub-location) is gated with **`[Authorize(Policy = "DungeonMasterOnly")]`** at the controller/action level — no bespoke owner-or-admin guard needed. Verified directly against `QuestBoard.Service/Authorization/DungeonMasterHandler.cs:17-35`: the `DungeonMasterOnly` policy already succeeds for `GroupRole.Admin || GroupRole.DungeonMaster`, plus an explicit SuperAdmin bypass (line 17) — this is confirmed source-code fact, not a research assumption, though a final sanity-check during research/planning is still worthwhile.

### Visibility (hide/reveal)
- **D-12:** Contacts have a **visibility state** (e.g. `IsRevealed` boolean) — this is new scope beyond the literal ROADMAP title text, but was judged an implementation detail of the same feature (HOW to implement "DM-only creation"), not a new capability, so it stays in this phase.
- **D-13:** A hidden Contact is filtered out of the Index list for viewers who shouldn't see it, and a direct `Details/{id}` URL 404s for them — same not-found convention this app already uses for cross-tenant/unauthorized access (Phase 49/55 precedent), not a softer "just hidden from the list" guarantee.
- **D-14:** New Contacts default to **hidden** (`IsRevealed = false`) — DM-tier explicitly reveals when ready (a "prep before the reveal" workflow).
- **D-15 (visibility exceptions, in order of precedence):**
  1. The Contact's **creator** (`CreatedByUserId`) always sees their own hidden Contacts, regardless of any toggle.
  2. For everyone else with DM-tier access (DungeonMaster, Admin, SuperAdmin — the same `DungeonMasterOnly`-policy group), a **"Show Hidden" toggle** button (next to "+ Contact" on the Index page) controls whether hidden Contacts (badged, e.g. muted "Hidden" badge) also appear in their own Index view.
  3. Plain Players never see hidden Contacts at all — the toggle only affects DM-tier viewers.
  - **Rationale:** generalizes Phase 55's governing principle ("a SuperAdmin should experience the site exactly as a normal user would") to the DM/Admin dual-role case — an Admin/SuperAdmin who is *also* playing as a Player in a given campaign shouldn't accidentally spoil themselves via role-based access alone. A plain DungeonMaster who also happens to be the Admin in a one-shot they're actively running was the concrete scenario that surfaced this.
- **D-15b (toggle persistence):** The toggle is **session-based**, not a new database table — stored per-group within session state (e.g. a session key scoped by `groupId`, alongside `ActiveGroupId`'s existing Phase 33 SQL-backed distributed-session mechanism), so a user can have it ON for Group 1 and OFF for Group 2 simultaneously within one session. The *entire* session (all groups' toggle states together) resets to the safe default (OFF/hidden) on session expiry or fresh login — this is deliberate: it re-establishes the safe default exactly in the scenario where the user might have forgotten they left it ON. A durable (never-expiring) DB-persisted preference was explicitly considered and rejected for reintroducing that exact spoiler risk.

### Index page organization
- **D-17:** Index is a **flat list, alphabetical by name** — no grouping by town/city (Character's owner-based "My Characters" vs "Other Characters" split doesn't map to Contacts, which have no per-user ownership in the edit-rights sense).

### Mobile parity
- **D-18:** Full mobile parity ships **in this phase** — `Index.Mobile`, `Details.Mobile`, `Edit.Mobile`, `Create.Mobile.cshtml`, matching Character's existing 8-file desktop+mobile set. Explicitly not deferred, to avoid the mobile-parity-gap pattern this app has had to fix as separate bug-fix phases before (Phase 43, Phase 54).

### Navigation & board-type visibility
- **D-19:** New "Contacts" nav link is visible to **everyone** (Players included, read-only + can add/edit/delete notes) and shows for **both** One-Shot and Campaign board types — same allowlist treatment as Characters/Quest Log (not the One-Shot-only restriction Shop/Calendar/Manage Shop/Edit Profile/Players get).

### Layout (confirmed walkthrough)
- **D-20:**
  - **Index** ("Contacts"): flat alphabetical list, thumbnail + name + town/city per row. "+ Contact" and "Show Hidden" toggle buttons in the header row (DM-tier only see the toggle; "+ Contact" only usable by DM-tier per D-09b but the page itself is visible to all per D-19). Hidden Contacts get a muted "Hidden" badge, shown per the D-15 visibility rules.
  - **Details**: left column — portrait card (image or placeholder icon) + name + town/city + sub-location; Actions card below (Edit / Reveal-or-Hide toggle / Delete — DM-tier only, per D-09b). Right column — Description card, then a Notes card (add-note form + newest-first list with per-note edit/delete, per D-08/D-09/D-10/D-16). Visible to all group members subject to D-13's hidden-Contact 404 rule.
  - **Edit** (DM-tier only): form for Name, Image, Description, Town/City, Sub-location. No notes editing here — notes stay on Details.
  - **Create** (DM-tier only): same fields as Edit; new Contact defaults to hidden (D-14).
  - All four (Index/Details/Edit/Create) get Mobile counterparts per D-18.

### Claude's Discretion
- Exact C# property/class names beyond what's specified above (e.g. the precise name of the notes entity/collection — "ContactNote" suggested but not mandated; exact `ContactImageEntity` field names) — follow existing `CharacterEntity`/`CharacterImageEntity` naming 1:1 where a direct analog exists.
- Exact wording of empty-state copy, badge text, button labels — should read naturally and avoid "guild"/D&D-specific framing, consistent with this app's existing tone.
- Whether the Index list additionally supports search/filter beyond alphabetical ordering — not discussed; default to none unless research/planning finds a compelling reason.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

No external ADRs/specs — this is an ad-hoc backlog phase (no REQUIREMENTS.md mapping), same pattern as Phases 47-56, 58. All scope is captured in the decisions above and the file-level pointers below.

### Roadmap & project context
- `.planning/ROADMAP.md` — Phase 57 entry (line ~418) and Phase 58 entry (line ~429, depends on Phase 57)
- `.planning/PROJECT.md` — "Roadmap Evolution" note explaining Phase 57 was added alongside Phase 58 in the same user request; `.planning/STATE.md` "Roadmap Evolution" section has the identical phase-addition note

### Pattern to mirror (existing "Characters"/Guild Members feature — current, pre-Phase-58 names)
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` — controller pattern to mirror (Index/Details/Create/Edit/Delete/ToggleRetirement/GetProfilePicture action shapes, `CanManageCharacterAsync`-style guard is NOT needed here per D-09b, image upload validation logic to reuse)
- `QuestBoard.Repository/Entities/CharacterEntity.cs`, `CharacterImageEntity.cs` — entity shape to mirror (group-scoped `HasQueryFilter`, 1:1 image table via FK-as-PK)
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` — ViewModel shape, `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` custom validators to reuse verbatim
- `QuestBoard.Service/Views/GuildMembers/*.cshtml` (all 8 files) — view structure/modern-card layout to mirror, per the D-20 layout walkthrough
- `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` — confirms `DungeonMasterOnly` policy already includes Admin + SuperAdmin (D-09b)
- `.planning/phases/58-rename-the-guild-members-feature-to-characters-everywhere-co/58-CONTEXT.md` — Phase 58's full rename manifest; useful for understanding what the Characters feature will be called *after* this phase ships, though Phase 57 itself should target the current (pre-rename) file/class names

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CharacterImageEntity`'s 1:1 FK-as-PK image-table pattern — directly reusable shape for `ContactImageEntity`.
- `MaxFileSizeAttribute` / `AllowedExtensionsAttribute` (in `CharacterViewModel.cs`) — reusable custom validation attributes for the Contact image upload field.
- `GuildMembersController.DetectImageMimeType` — reusable byte-sniffing helper for serving the stored image with the right content type.

### Established Patterns
- Group-scoped EF Core query filter (`HasQueryFilter` on `GroupId`), fail-closed per Phase 55's hardening precedent — `ContactEntity` should follow this from day one rather than needing a later hardening pass.
- `modern-card`/`modern-card-header`/`modern-card-body` CSS classes (CLAUDE.md UI/UX convention) — applies to all new Contact views.
- Nav allowlist pattern (Phase 37) — "show only when confirmed [board type]" via an allowlist, never a blocklist; Contacts joins Characters/Quest Log's "visible for all board types" allowlist entry per D-19.
- Session-based per-user state alongside `ActiveGroupId` (Phase 33's SQL-backed distributed session cache) — the pattern the "Show Hidden" toggle (D-15b) reuses.

### Integration Points
- New nav link in `Views/Shared/_Layout.cshtml` and `_Layout.Mobile.cshtml`, following the exact allowlist entries Characters/Quest Log already use.
- `QuestBoardContext.OnModelCreating()` needs the new `ContactEntity`/`ContactImageEntity`/notes-entity registrations plus the group-scoped query filter.
- New EF Core migration required for the 3 new tables (Contact, ContactImage, ContactNote-or-equivalent).

</code_context>

<specifics>
## Specific Ideas

- User's own words on the naming search: tried "NPC" (roadmap default), then "World Characters", "Cast", reacted to Claude's "Denizen" suggestion, offered the Dutch word "Kennis/Kennissen" (translated to "Acquaintance/Acquaintances"), then "Local/Locals" and "Face/Faces" before settling on "Contact/Contacts".
- The visibility-toggle design (D-12 through D-15b) emerged from the user's own real situation: "I am the super admin of the app, but I'm a player in a campaign setting... I need some advice here" — followed by a further real scenario: "there are multiple DMs that run one-shots. I'm a DM as well but also the admin... if the DM of a specific campaign is also the admin, they hinder themselves" — which is what produced the creator-always-sees-own-hidden-Contacts exception (D-15.1) layered on top of the per-group session toggle (D-15.2/D-15b), rather than a single hardcoded role-based default.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The visibility/hide-reveal feature (D-12–D-15b) was judged an implementation detail of "DM-only creation," not scope creep, since it doesn't add a capability beyond what the roadmap phase already implies (DM-controlled content visible to players).

### Reviewed Todos (not folded)
None — `gsd_run query todo.match-phase 57` returned zero matches.

</deferred>

---

*Phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na*
*Context gathered: 2026-07-06*
