# Phase 95: Quests and Quest Log UI conversion - Context

**Gathered:** 2026-11-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Convert the core loop to the Phase 94 foundation: the quest board, Quest Details, Quest Create, Edit
and CreateFollowUp, DM Manage, Quest Log (Index, Details, EditRecap) and the Move page (which serves
quests and events). The shared pieces come along: `Quest/_QuestFormScripts`, `Shared/_MarkdownEditor`,
`Shared/_CharacterSelectModal`, and a scoped patch to the vote UI of `_Calendar` (Phase 97 owns the
partial itself). The scope, counts, stylesheet list, guard tests, deferred items and the ten-point
"Definition of converted" are fixed by `.planning/ROADMAP.md` § "Phase 95" and are not repeated here.

Almost every content question was answered at the Phase 94 G1 sign-off (2026-11-01): the 33 twin
questions Q-07 to Q-39, the merges Q-95 and Q-96, the relayouts Q-111 to Q-114, the Q-127 top-bar
verification and the other decisions Q-129 to Q-136. They are inherited as recorded in
`94-AUDIT.md` § "Operator questions for sign-off". This discussion settled the remaining
implementation choices and **revises four G1 answers** for the desktop quest board (Q-21, Q-23,
Q-131, and the poster description; see D-01 to D-10).

No capability is added. Signup, voting, managing, logging and moving keep working exactly as today.

</domain>

<decisions>
## Implementation Decisions

### Desktop quest board posters (G1-R1, drawn before it is built)

The board is a fantasy notice board that players take quests from; the posters are the app's
identity and the reason the app exists. Keep them, keep them varied, and make them readable.

- **D-01: The quest description leaves the desktop poster.** Players read it on Quest Details,
  which keeps it unchanged. The poster carries the title, the DM, the session date (once
  finalized), the adventurer count line and the CR (CR stays hidden on Campaign boards, as today).
  This is an explicit operator decision about displayed text; no field, link, button or action is
  removed, so D-04's control rule still holds. The phone board is unaffected.
- **D-02: Poster3 (601x1400, the tall narrow one) is retired from the rotation.** The pool is
  Poster1, Poster2, Poster4, Poster5, Poster6 and the eight `Ruined Posters`, all from their
  `w Shadow` folders. The image files themselves stay in the repository.
- **D-03: Each poster gets its own measured text area.** Several blanks have holes or torn edges,
  and Poster2's height comes from a tail, not from usable space. For every poster the researcher
  or mockup step measures the usable content rectangle (the insets that keep text clear of holes,
  tears and the tail) and records it as that poster's padding. Fit is judged by this measured area,
  never by the image's height.
- **D-04: A poster joins the pool only if its measured area holds the full poster content** (title,
  DM, date, adventurer count, CR) at the 14px text floor without touching a hole or edge. The
  research and mockup step decides which of the eight ruined posters qualify; any that do not
  qualify stay out of the rotation, and the mockup shows the operator which ones made it.
- **D-05: Poster choice is random but stable per quest.** A quest keeps the same poster and seal on
  every visit (derived from the quest's id, as today), chosen from the posters that fit it. A title
  too long for a squat poster's area goes to a taller one so the title wraps and is never truncated
  (A-quests-022). The variety across quests is what makes the board feel alive. Today's
  character-count selection (`<= 130` Poster4, `<= 250` Poster5, `>= 500` Poster3) is replaced.
- **D-06: The wax seals stay as the status marker; no status word on the desktop poster.** This
  **revises the G1 answer to Q-23** for the desktop board. Behaviour as today: an open quest has
  no seal, a finalized quest (or any quest on a Campaign board) shows the seal, and a second,
  hue-shifted seal marks "you are signed up". The seal design is chosen per quest from the four
  `Wax Seals` images. Seals sit at a fixed, per-poster position clear of the text area (they cover
  the DM line and titles today). The status reaches assistive technology through the seal `alt`
  text and the poster link's accessible name. The phone board keeps its text badges (Q-23 stands
  there).
- **D-07: The poster mockup draws both information orders**, date first and adventurer count first
  after the title, and the operator picks at the first gate. The adventurer count line stays on the
  poster (Q-22).
- **D-08: The lightened text zone is shown as a switcher across two or three strengths**, every
  one measuring at least 4.5:1 for body text at the brightest board pixels (titles may rely on the
  3:1 large-text bar). The operator picks the strength at the first gate. No slip surface
  (`--qb-surface-slip` does not exist and must not be introduced).
  **Outcome at Gate 1 (2026-11-11):** the operator rejected any visible lightened block ("i don't
  want that light visible block/patch") and chose option C, heavier ink only, with no zone and no halo.
  Poster text is below 4.5:1 per glyph (worst glyph 2.46 to 3.65; Ruined 1 over the burn mark is the
  weakest), recorded as a contrast exception held to per-poster floors. See 95-UI-SPEC "Poster text
  treatment" and 95-03-SUMMARY.
- **D-09: An empty desktop board is the bare board**: no poster or card is drawn, and a
  screen-reader-only heading ("No quests available") keeps the page from being silent. This
  **revises the visible part of the G1 answer to Q-21**; the phone keeps its visible empty state.
- **D-10: Add WebP derivatives of the poster artwork and keep the PNGs as fallback.** Committed
  WebP copies next to the PNGs, loaded through CSS `image-set()` with the PNG as fallback; the
  originals are untouched. This **revises the G1 answer to Q-131**. The posters are 0.25 to 1.8 MB
  each, so a crowded board currently loads several MB. The same treatment applies to the four seal
  images if it pays off.
- **D-11: The poster AFTER is a scratch mockup shown to the operator before any board code is
  written**, drawn with a crowded board (so the operator can judge whether the taller Poster1, 2
  and 6 look empty without a description), long titles, open, finalized and signed-up quests, and
  a Campaign board.

### Shared Markdown editor
- **D-12: Fix the keyboard trap by letting Tab leave the editor.** Tab and Shift+Tab move focus out
  of the editor like any other field; Tab no longer indents inside it (nested lists still work via
  the toolbar's list buttons and typed spaces). The editing surface gets an accessible name from its
  label. This also fixes the six Characters-area pages that render the partial (A-characters-006);
  Phase 98 verifies it there.
- **D-13: The "supports Markdown" hint becomes visible help text under the field**, linked with
  `aria-describedby`, replacing the hover-only tooltip icon (A-quests-040). The sentence is unchanged.

### Phone signup on Quest Details (Q-14, Q-15)
- **D-14: The phone signup form gets plain selects, the same controls as desktop**: "Select
  Character (Optional)" (only when the board serves Characters, as on desktop) and "Select Your
  Role" (Player, Assistant DM, Spectator), each with its help text, placed above the date voting.
  The existing character-select modal stays where it is used today (the join forms).
- **D-15: Choosing Spectator hides the date voting on both layouts**, with the "As a spectator, you
  will be automatically approved" message, as desktop does today. The script targets a dedicated
  marker on the voting cards, so it no longer hides the DM Controls card (A-quests-011).

### Twin wording
- **D-16: Twins use one wording, in full.** "Assistant DM", "Open for Signups", "Participants" and
  the Manage labels read the same on both layouts. The phone abbreviates only where a label
  measurably does not fit at 390px; each such exception is recorded in the phase verification.
  The Manage date format keeps the year on both.
- **D-17: Quest form exit buttons read "Cancel" on both layouts**, with the same link targets as
  today, matching the approved Cancel-left, commit-right row (Q-113).

### Relayout sign-off
- **D-18: Hybrid sign-off.** The two relayouts with no AFTER yet, the desktop poster board (G1-R1)
  and phone Quest Manage (Q-114), are drawn as scratch mockups and approved **before** they are
  built. Quest Details (Q-111, Q-112) and Quest Create and Edit (Q-113) already have Direction A
  renders, so they are built and confirmed from real before/after screenshots.
- **D-19: Two operator gates.** Gate 1, early: the poster mockup (with the D-07 and D-08
  switchers and the D-04 pool), the phone Manage mockup and any merge proposals (D-21). Gate 2, at
  the end: one review sheet with every before/after pair, desktop and phone.
- **D-20: A rejected AFTER is iterated until the operator approves it.** The phase waits on it. Plan
  for the possibility of a redraw loop at either gate.

### Twin merges
- **D-21: Re-check the remaining pairs for merging and bring any that qualify to Gate 1.** With the
  "add it to the other view" answers applied, Move/Index (OQ-MV-1 answered), Quest/Edit and
  Quest/CreateFollowUp may now be layout-only. Research checks each against the D-13 criteria from
  Phase 94 (identical or layout-only, no deliberate difference, phone differences CSS-only) and
  lists the tests each would change. Nothing merges without the operator's explicit yes. The two
  merges already approved (QuestLog/EditRecap, QuestLog/Details) go ahead as decided.
  — **Reversibility:** costly — a merge touches the view, both layouts' CSS loading and the
  mobile-view tests that prove the `.Mobile` view is selected under a real phone User-Agent.

### Working baseline on this machine
- **D-22: Re-capture BEFORE screenshots here, from the current branch.** The Phase 94 scratch folder
  (`~/qb-ui-review/phase-94`) is not on this Windows machine, and its BEFORE set shows the pages
  before the foundation landed, so the current branch is the true BEFORE. The operator copies only
  the Direction A renders (`mockups/direction-a/png/quest-details-*`, `quest-form-*`,
  `quest-form--edit-*`) over from the machine Phase 94 ran on, as the AFTER references for Q-111 to
  Q-113. Phase 95 scratch lives outside the repository (for example `~/qb-ui-review/phase-95/`).
- **D-22 revision (2026-11-11, operator decision):** the phase runs on the Linux machine that ran
  Phase 94 instead of the Windows machine, because the `senior-frontend` skill and the Phase 94
  scratch mockups live there. The Phase 94 scratch (`~/qb-ui-review/phase-94`) is reused: its
  Playwright 1.64.1 install and tools, the Audit Board (id 7) with its `qb-audit-*` accounts, and the
  Direction A renders (copied by the executor, not the operator). BEFORE is still re-captured
  from the current branch into `~/qb-ui-review/phase-95/`. Front-end work (mockups, the Gate sheets,
  view and CSS conversion) invokes the `senior-frontend` skill. Python is `python3`; scipy is not
  installed and is not added.
- **D-23: The first plan opens with an operator checkpoint, as 94-01 did.** The operator confirms
  SQL Server at `localhost:1433` (never provisioned by the agent) and the admin account (since the
  2026-11-11 revision: signs off on reusing the Phase 94 Audit Board accounts);
  the executor creates player and DM test accounts through Admin and the fixture quests the
  captures need (crowded board, empty board, voting, finalized, signed up, long titles, a Campaign
  board, a quest with a follow-up, a completed quest with and without a recap). Credentials stay in
  the scratch folder, never in the repository. Fixture writes go through the app's own forms
  (Phase 94 D-25).

### Delivery
- **D-24: Work lands in slices on `milestone/v9-rolling-improvements`, with one release at the
  end** once both gates have passed. Each slice leaves every page usable and the test suite green
  (Phase 94 D-08). A sensible slice order: the shared editor, form scripts and modal; Quest Details
  with the vote patch; the board; the forms; Manage; Quest Log with its two merges; Move.

### Claude's Discretion
- How the per-poster text areas are stored (CSS modifier classes per poster, a small lookup in the
  view or view model) and how "fits" is estimated server-side, provided text never overflows or is
  truncated mid-word and the poster URL no longer comes from an inline `style`.
- Hover behaviour on the posters: keep today's lift and seal tilt if wanted, animating `transform`
  only (no `filter`, which doubles the shadow already in the artwork, A-quests-023) and none under
  `prefers-reduced-motion`.
- How each poster and log card becomes one link (the stretched-link pattern from the audit),
  replacing the inline `onclick` navigation.
- The poster date and any other remaining `.ToString` date renderings move to `Html.WallClock` /
  `Html.LocalTime` according to what the value is; the helpers themselves are not changed.
- Module structure for the moved scripts, within `.claude/ui-guidelines.md` § "No inline code".
- Exact slice boundaries and plan count.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and inherited answers
- `.planning/ROADMAP.md` § "Phase 95: Quests and Quest Log UI conversion": scope, counts, stylesheet
  list, `@section Styles` moves, guard tests to rewrite, inherited decisions, approved relayouts
  with their BEFORE/AFTER paths, the ten-point Definition of converted and the phase-specific extras.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md` § "Operator questions for
  sign-off" (Q-07 to Q-39, Q-95, Q-96, Q-111 to Q-114, Q-127, Q-129 to Q-136, and the G1 sign-off
  record with open items G1-O1 and G1-R1), § "Phase 1: Quests and Quest Log".
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-quests.md`: every finding this
  phase owns (A-quests-001 to 100), the twin comparison, copy differences, empty-state copy,
  merge and relayout proposals, stylesheet and view-markup metrics.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-characters.md` § A-characters-006:
  the editor keyboard trap and its probe.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-CONTEXT.md`: D-04 (relayout rules),
  D-08 (usable between merges), D-12 (inline code), D-13 to D-15 (twins, merges, mobile
  verification), D-16 and D-17 (accessibility bar, signature colours), D-25 (fixture writes).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-UI-SPEC.md`: the design contract the
  foundation implements (tokens, components, contrast bar, Direction A values).

### UI system and project rules
- `.claude/ui-guidelines.md`: tokens and layers, components, colour, accessibility checklist, mobile
  twins, no inline code (`@section Styles` / `@section Scripts`, ES modules), vendor files, dates.
- `.claude/architecture.md` § "Time and the board clock": `Html.LocalTime` and `Html.WallClock`.
- `CLAUDE.md`: branching, no planning IDs in source or CSS comments, local database contract.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Foundation sheets `wwwroot/css/tokens.css`, `base.css`, `components.css`, `shell.css`,
  `shell.mobile.css`: the tokens, layers and components every converted page uses.
- Poster art: `wwwroot/images/Blanks/Blanks w Shadow/Poster1-6.png` (Poster1 and 2 1000x1400,
  Poster3 601x1400, Poster4 1000x980, Poster5 600x675, Poster6 600x800) and
  `wwwroot/images/Ruined Posters/Ruined w Shadow/Ruined Poster 1-8.png` (height/width 0.90 to 1.15).
  Seals: `wwwroot/images/Wax Seals/{Crown,Eagle,Flower,Sigil} Seal.png`.
- `wwwroot/js/markdown-editor.js` and the self-hosted EasyMDE under `wwwroot/lib`.

### Established Patterns
- `Views/Quest/Index.cshtml`: today's random pool is only Poster1 and Poster6; Poster3 to 5 are
  commented out of it and reached only through the character-count thresholds, and Poster2 is never
  used; the poster URL and seal
  offsets are inline `style` attributes; the card navigates with an inline `onclick`; the poster
  date uses `.ToString("MMM dd, yyyy 'at' h:mm tt")`.
- `wwwroot/css/quests.css` seals: `.open-seal { display: none }`, `.finalized-seal` shown,
  `.signup-seal-image` hue-rotated 180 degrees; hover scales and rotates the seal.
- `Views/Quest/Details.cshtml` around lines 553-590 (desktop character and role selects) and
  1035-1060 (the role-select script whose `.card.modern-card.mb-3` selector also hides DM Controls).
  `Details.Mobile.cshtml` has neither select on the open-quest signup.

### Integration Points
- `MobileViewLocationExpander` selects `.Mobile.cshtml` views by User-Agent; merged pairs drop their
  `.Mobile` file. Mobile checks run under a mobile User-Agent with touch (Phase 94 D-15).
- **Phase 78** (Link Preview Foundation and Quest Cards) has 9 plans written against today's Quest
  Details markup, none executed. Converting Quest Details here will leave those plans out of date;
  they need a refresh before Phase 78 executes.

</code_context>

<specifics>
## Specific Ideas

- "Quests on a board as if it's a fantasy game with a questboard where the player can grab quests
  from." The randomness of the posters is what makes it feel like a living board.
- The operator cut posters in the past only because text would not fit around the holes; with
  measured per-poster text areas, every blank (bar Poster3) and as many ruined posters as fit should
  return.
- "I don't like the view of a very large poster when there's not much content on it": choose by
  usable area, and show the crowded board in the mockup to check that the taller posters still look
  full.
- The seal is "very easy to spot": it stays the at-a-glance status marker.

</specifics>

<deferred>
## Deferred Ideas

None. The discussion stayed within the phase scope.

</deferred>

---

*Phase: 95-quests-and-quest-log-ui-conversion*
*Context gathered: 2026-11-10*
