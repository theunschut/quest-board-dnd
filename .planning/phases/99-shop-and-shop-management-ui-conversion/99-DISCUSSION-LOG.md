# Phase 99: Shop and Shop management UI conversion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-15
**Phase:** 99-shop-and-shop-management-ui-conversion
**Areas discussed:** Merchant button place, Dead controls, Status wording, Item visuals, Process

These were not asked again, because they were answered at G1 (2026-11-01) in `94-AUDIT.md`:
- Q-56 to Q-75 (11 "add it to the other view", 9 deliberate);
- no merges;
- Q-120 and Q-121 (approved in principle);
- Q-128 (Shop variant (a), navy);
- Q-137 (this phase fixes the four breaks).

Confirmations staying native through `confirm-submit.js` follows the Phase 95 and 98 precedent.

---

## Merchant button place

| Option | Description | Selected |
|--------|-------------|----------|
| In the hero card | A real button labelled "Talk to the merchant" inside the Emporium header card, in the page flow | |
| Floating, lifted clear | Keep bottom-right fixed but stop above the footer; focusable | |
| Beside the page title | Small icon button at the end of the title line | |

**User's choice:** Other: "I like the floating place it's currently at, but i'd like to see the recommended option in a mockup"
**Notes:** Gate 1 draws both, the floating spot kept clear of the footer and the in-hero button, and the operator picks one there.

| Option | Description | Selected |
|--------|-------------|----------|
| Keep, make it accessible | Same 20 quotes; polite live region; stays while hovered or focused | ✓ |
| Keep exactly as is | Only the button moves | |

**User's choice:** Keep, make it accessible

## Dead controls

| Option | Description | Selected |
|--------|-------------|----------|
| Live card preview modal | Dialog with the player-facing card built from the form values; no save, no endpoint | ✓ |
| Full details preview | Renders the whole item-details layout | |
| Inline preview panel | Live card beside the form (a relayout) | |

**User's choice:** Live card preview modal

| Option | Description | Selected |
|--------|-------------|----------|
| Point at /Shop/Details/{id} | Same target as the management lists | ✓ |
| Open the details modal | Modal on the Edit page | |

**User's choice:** Point at /Shop/Details/{id}

| Option | Description | Selected |
|--------|-------------|----------|
| Keep the stub, convert it | Restyle; alerts move to a module, same wording | |
| Disable the three buttons | Disabled with a "Coming soon" note | |
| Remove it | Needs explicit yes | |

**User's choice:** "let the researcher/planner decide? advise?"
**Notes:** Recorded as discretion. The advised default is to keep and convert it. Removing the control or changing its copy happens only as a Gate 1 proposal.

| Option | Description | Selected |
|--------|-------------|----------|
| Create only, as recorded | Q-73 scope | |
| Also on desktop Edit | Shared module, small extra | |

**User's choice:** "again, researcher/planner?"
**Notes:** Recorded as discretion. It is built only after a yes at Gate 1.

## Status wording

| Option | Description | Selected |
|--------|-------------|----------|
| Pending Review / Available | Matches the "Items Pending Review" heading and the Edit label | ✓ |
| Draft / Available | Desktop's words | |
| Pending Review / Active | Phone's words | |

**User's choice:** Pending Review / Available

## Item visuals

| Option | Description | Selected |
|--------|-------------|----------|
| Rarity/type emblem tile | Shorter navy tile with the type icon in the rarity colour; decorative | ✓ |
| Remove the block | Details start with the name | |
| Keep a 250px placeholder, restyled | Same size on the foundation surface | |

**User's choice:** Rarity/type emblem tile

| Option | Description | Selected |
|--------|-------------|----------|
| All static | Glows kept as still styles, nothing loops | |
| Play a few times, then stop | 2-3 runs, none under reduced motion | |
| Only the merchant moves | Everything static except a gentle merchant bob that stops, none under reduced motion | ✓ |

**User's choice:** Only the merchant moves

| Option | Description | Selected |
|--------|-------------|----------|
| As drawn in the Direction A shop render | Solid chips, text colour picked for 4.5:1, same on both layouts | ✓ |
| Coloured text, no chip | Lightened rarity text on navy | |

**User's choice:** As drawn in the Direction A shop render

| Option | Description | Selected |
|--------|-------------|----------|
| Under the item list, like desktop | Same in-flow section as Q-120 | ✓ |
| Above the item list | Recent purchases first on the phone | |

**User's choice:** Under the item list, like desktop

## Process

| Option | Description | Selected |
|--------|-------------|----------|
| Same as phase 98 | Gate 1 with mockups and look check, Gate 2, p99 fixtures kept, slices on the milestone branch, v6.4.0 | ✓ |
| Same, but remove fixtures after | Delete the p99 fixtures after Gate 2 | |

**User's choice:** Same as phase 98

## Claude's Discretion

- Bulk Actions treatment (advised: keep and convert)
- Preview on desktop Edit (only as a Gate 1 proposal)
- How the floating merchant stays clear of the footer; bob amplitude and cycles
- Emblem tile size and icon mapping
- Pricing script module, sheet consolidation, availability date helper, ports, slice order

## Deferred Ideas

- Real bulk actions, a new capability for a phase of its own.
