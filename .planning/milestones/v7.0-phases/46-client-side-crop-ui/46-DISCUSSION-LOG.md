# Phase 46: Client-Side Crop UI - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 46-Client-Side Crop UI
**Areas discussed:** Crop aspect ratio & shape, Cropped-vs-original display rule, Crop interaction pattern, Real-device verification access

---

## Crop Aspect Ratio & Shape

| Option | Description | Selected |
|--------|-------------|----------|
| Keep distinct per field | Character/Contact landscape ratio, DM 1:1 circle — no display CSS changes | |
| Unify to square (1:1) | All three fields crop to one square ratio | ✓ |
| Let me specify per field | Custom ratio per field | |

**Follow-up:** User asked whether a unified square crop still displays as a circle for DM (yes — `border-radius: 50%` over a square image, matching DM's existing `rounded-circle` pattern). This raised a second question: would a square crop look wrong inside Character/Contact's current landscape list-card box?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, make list cards square too | `.character-image`/`.contact-image` boxes change from landscape (200px tall, fluid width) to a square aspect-ratio box | ✓ |
| No, keep landscape list cards | Square crop gets re-cropped by `object-fit: cover` to fill the landscape box | |
| Keep distinct shapes instead | Revert to the original "distinct per field" option | |

**User's choice:** Unified square (1:1) crop for Character, Contact, and DM; Character/Contact list-card image boxes change to square to match.
**Notes:** User confirmed understanding of the CSS mechanics (square crop + `border-radius: 50%` = circle) before committing.

---

## Cropped vs. Original Display Rule

**Round 1** — which quest-related avatar spots (currently all showing the original) should switch to cropped:

| Option | Description | Selected |
|--------|-------------|----------|
| Quest Details participant list | Selected + waitlist avatars | ✓ |
| Quest Manage roster | DM's participant roster avatar | ✓ |
| QuestLog Details recap | Per-participant avatar on completed-quest recap | ✓ |
| _QuestCard partial thumbnail | Inline quest-board card avatar | ✓ |

**Round 2** — Create/Edit form's own "current photo" preview:

| Option | Description | Selected |
|--------|-------------|----------|
| Cropped | Matches what other members see of you | ✓ |
| Original | Shows the full unmodified source | |

**Round 3** — final confirmation on which pages keep the original:

| Option | Description | Selected |
|--------|-------------|----------|
| All 3 detail pages = original | Character Details, Contact Details, DM Profile all show original (matches REQUIREMENTS.md IMAGE-04 literal text) | |
| Only Character/Contact Details = original | DM Profile switches to cropped; only 2 pages keep the original | ✓ |

**User's choice:** Original image displays ONLY on Character Details and Contact Details. Every other location — including DM Profile itself — displays the cropped image.
**Notes:** This explicitly diverges from REQUIREMENTS.md's IMAGE-04 literal text (which pairs DM with Character as "original"). User was asked to confirm directly given the requirement-text conflict, and confirmed DM Profile should show cropped.

---

## Crop Interaction Pattern

| Option | Description | Selected |
|--------|-------------|----------|
| Modal, auto-default crop if untouched | File-select opens a Cropper.js modal; unsubmitted crop auto-saves a centered default at the locked ratio | ✓ |
| Modal, must confirm crop | Same modal, but requires an explicit "Confirm Crop" click | |
| Inline widget in the form | No modal — crop frame renders directly in the page | |

**User's choice:** Modal, auto-default crop if untouched.
**Notes:** None.

---

## Real-Device Verification Access

| Option | Description | Selected |
|--------|-------------|----------|
| Same iPhone, same LAN setup | Reuse Phase 43's exact physical-iPhone-over-LAN method | |
| iPhone + an Android device | Same iPhone setup, plus Android for a second touch-gesture platform | |
| Not yet confirmed | Flag as a pre-planning blocker | ✓ |

**User's choice:** Not yet confirmed.
**Notes:** Recorded as a pre-execution blocker in CONTEXT.md — do not schedule the verification checkpoint until device access is confirmed.

---

## Claude's Discretion

- Exact naming for new "read cropped" controller actions/service methods (mirror Phase 45's `GetCharacterOriginalPictureAsync`-style naming).
- Whether DM gains a separate original-read method for future-proofing, or `GetDMProfilePicture` is simply repointed in place (no UI currently needs a DM original).
- EXIF-orientation-correction snippet sourcing and canvas-downscale-before-crop implementation details — flagged for a dedicated `--research-phase` pass during planning, per STATE.md's existing pending todo.
- Exact crop modal markup/styling.
- Cropped-output resolution (fixed size vs. native crop-box resolution).

## Deferred Ideas

None — discussion stayed within phase scope. IMAGE-06 (rotate/flip) and IMAGE-07 (multiple aspect-ratio presets) are pre-existing v2 deferrals in REQUIREMENTS.md, not new ideas from this session.
