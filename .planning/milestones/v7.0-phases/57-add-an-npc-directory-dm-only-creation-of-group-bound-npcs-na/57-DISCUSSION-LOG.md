# Phase 57: Add an NPC directory: DM-only creation of group-bound NPCs (name, image, description, town/city, optional sub-location), with a player-and-DM-editable list of freeform notes, plus dedicated Index/Details/Edit views mirroring the Characters pattern - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
**Areas discussed:** Freeform notes list, NPC edit/delete authorization, NPC fields & structure, Index page organization, Mobile views, Notes UI placement, Nav link & board-type visibility, Feature naming

---

## Freeform Notes List

| Option | Description | Selected |
|--------|-------------|----------|
| Separate entries (log) | Each note is its own row: author, timestamp, text. New notes append. | ✓ |
| One shared text box | Single freeform textarea everyone edits together. | |
| You decide | | |

**User's choice:** Separate entries (log)

| Option | Description | Selected |
|--------|-------------|----------|
| Author + DM/Admin only | Player edits own notes; DM/Admin can moderate anyone's. | |
| Anyone in the group | Fully collaborative, no ownership check. | ✓ |
| Author only, no override | Strictly the author, not even DM can touch it. | |

**User's choice:** Anyone in the group

| Option | Description | Selected |
|--------|-------------|----------|
| Newest first | Most recent note at top. | ✓ |
| Oldest first | Chronological, oldest at top. | |
| You decide | | |

**User's choice:** Newest first

| Option | Description | Selected |
|--------|-------------|----------|
| Match Description (2000 chars), no count cap | Same StringLength(2000) as Character.Description. | ✓ |
| Shorter cap (e.g. 500 chars) per note, no count cap | Tighter per-note limit. | |
| You decide | | |

**User's choice:** Match Description (2000 chars), no count cap
**Notes:** Notes UI placement (where add/edit/delete happens) was resolved later in a separate new gray area — see below.

---

## NPC Edit/Delete Authorization

| Option | Description | Selected |
|--------|-------------|----------|
| DM or Admin (mirrors Character) | GetEffectiveGroupRoleAsync-style check. | |
| DungeonMaster role only | Strictly DungeonMasterOnly policy. | |
| You decide | | |

**User's choice:** Free text — "As a general rule, if a DM can do it, an admin and a super admin can do it too. That's why the user is an admin. I believe if a controller has the 'DungeonMasterOnly' policy, the admins automatically gain access as well?"
**Notes:** Claude verified this directly by reading `DungeonMasterHandler.cs:17-35` — confirmed `DungeonMasterOnly` policy already succeeds for `GroupRole.Admin || GroupRole.DungeonMaster`, plus a SuperAdmin bypass. User then confirmed proceeding with `[Authorize(Policy = "DungeonMasterOnly")]` but asked Claude to flag it for a research sanity-check rather than take it purely on faith.

| Option | Description | Selected |
|--------|-------------|----------|
| All NPCs visible to everyone | No draft/hidden state. | |
| DM can hide/reveal NPCs | Visibility flag; hidden shows only to DM/Admin. | ✓ |

**User's choice:** DM can hide/reveal NPCs

| Option | Description | Selected |
|--------|-------------|----------|
| Hidden from Index list + 404 on direct Details URL | Strict — matches app's cross-tenant 404 convention. | ✓ |
| Hidden from Index list only, Details still reachable | Weaker guarantee. | |
| You decide | | |

**User's choice:** Hidden from Index list + 404 on direct Details URL

| Option | Description | Selected |
|--------|-------------|----------|
| Default hidden | New NPCs start hidden, DM reveals later. | ✓ |
| Default visible | New NPCs visible immediately. | |
| You decide | | |

**User's choice:** Default hidden

**Follow-up discussion (not a scripted question):** User raised a real scenario — "I am the super admin of the app, but I'm a player in a campaign setting. This will result in me seeing things I'm not supposed to see... I want the ability to show everything, but default to hidden." Claude proposed a per-user session toggle (mirroring `ActiveGroupId`'s Phase 33 session mechanism), defaulting OFF, sticky once turned ON for the session. User then raised a second real scenario — multiple DMs running different one-shots in the same group, one of whom is also the Admin — where a hardcoded default fights whichever hat the user is wearing. This led to the two-part resolution: (1) the Contact's *creator* always sees their own hidden Contacts regardless of the toggle, (2) the toggle governs everyone else's hidden-Contact visibility, per-group within session, resetting to OFF on session expiry/fresh login (deliberately, not a durable DB preference, to avoid the original spoiler risk recurring). User confirmed per-group toggle persistence within one session ("group 1 ON, group 2 OFF").

---

## NPC Fields & Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Own field (e.g. SubLocation) | Separate optional string field. | ✓ |
| Folded into description | No separate field. | |
| You decide | | |

**User's choice:** Own field (e.g. SubLocation)

| Option | Description | Selected |
|--------|-------------|----------|
| Required, free text | Every NPC must have a location. | |
| Optional, free text | Can be left blank. | ✓ |
| You decide | | |

**User's choice:** Optional, free text

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, mirror exactly | Same optional/5MB/JPG-PNG-GIF/separate-image-table pattern as Character. | ✓ |
| Something different | | |

**User's choice:** Yes, mirror exactly

| Option | Description | Selected |
|--------|-------------|----------|
| 2000 chars (match Character.Description) | | ✓ |
| You decide | | |
| Something else | | |

**User's choice:** 2000 chars (match Character.Description)

---

## Index Page Organization

| Option | Description | Selected |
|--------|-------------|----------|
| Flat list, alphabetical by name | Simplest, mirrors general directory feel. | ✓ |
| Grouped by town/city | Clustered under town, alphabetical within group. | |
| You decide | | |

**User's choice:** Flat list, alphabetical by name

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, a 'Hidden' badge | Muted badge, mirrors Retired/Dead convention. | ✓ (with expansion) |
| No badge needed | | |
| You decide | | |

**User's choice:** Free text — requested an additional "Show Hidden" toggle button for Admin/SuperAdmin next to "+ NPC", default hidden, because as SuperAdmin they're sometimes also a Player and don't want to see spoilers by default. Resolved via the follow-up discussion captured above (session-based, per-group, creator-always-sees-own-hidden exception).

---

## Mobile Views

| Option | Description | Selected |
|--------|-------------|----------|
| Full mobile parity in this phase | 8-file set matching Character. | ✓ |
| Desktop-only, mobile deferred | Follow-up phase. | |
| You decide | | |

**User's choice:** Full mobile parity in this phase

---

## Notes UI Placement

| Option | Description | Selected |
|--------|-------------|----------|
| On the Details page | Inline notes section, add-note form + list with per-note controls. | ✓ |
| Separate dedicated Notes page | Mirrors Phase 53's dedicated EditRecap page. | |
| You decide | | |

**User's choice:** On the Details page

---

## Nav Link & Board-Type Visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Visible to everyone, both board types | Same allowlist as Characters/Quest Log. | ✓ |
| Visible to everyone, One-Shot only | Same restriction as Shop/Calendar. | |
| You decide | | |

**User's choice:** Visible to everyone, both board types

---

## Feature Naming

Extended back-and-forth, not a single scripted question:

1. Claude proposed keeping "NPC" (genre-generic, pairs with Phase 58's "Characters") with `.NET` Pascal-case (`Npc`) — **user rejected**, wanted a different name entirely.
2. Claude proposed "World Characters" / "Cast" — user didn't pick either, offered "Folk" / "Townsfolk" instead.
3. Claude flagged the singular/plural awkwardness of "Folk" and proposed "Denizen/Denizens" — user asked about the Dutch word "Kennis/Kennissen" instead.
4. Claude translated: "Kennis" (in the "person you know" sense) = "Acquaintance"; presented Acquaintance vs. Denizen — user still undecided, asked for more options.
5. Claude proposed "Contact/Contacts", "Local/Locals", "Face/Faces", or falling back to plain "NPC" — **user chose "Contact/Contacts"**.
6. Claude confirmed no naming collisions in the codebase (grep for "contact" found only unrelated "contact an administrator" copy) and locked in `ContactController`, `ContactEntity`, `Views/Contacts/`, nav label "Contacts".

**Final choice:** Contact / Contacts

---

## Layout Confirmation

Claude presented a full Index/Details/Edit/Create layout walkthrough synthesizing all decisions above (see CONTEXT.md D-20 for the full text). **User's choice:** "Matches what I had in mind" — confirmed as-is, no changes requested.

---

## Claude's Discretion

- Exact C# property/class names beyond what's explicitly specified (e.g. precise notes-entity name, `ContactImageEntity` field names) — follow `CharacterEntity`/`CharacterImageEntity` naming 1:1 where a direct analog exists.
- Exact empty-state copy, badge text, button labels — avoid "guild"/D&D-specific framing.
- Whether Index supports search/filter beyond alphabetical ordering — default to none unless research finds a compelling reason.

## Deferred Ideas

None — discussion stayed within phase scope throughout. The visibility/hide-reveal design was judged an implementation detail of "DM-only creation," not a new capability, so it was kept in-phase rather than deferred.
