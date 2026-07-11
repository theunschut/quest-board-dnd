# Phase 66: Quest Description Editor & Rendering (Proof-of-Concept) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-09
**Phase:** 66-quest-description-editor-rendering-proof-of-concept
**Areas discussed:** Manage page placement, Toolbar visual styling, Board card rendering, Paragraph-break hint

---

## Manage page placement

| Option | Description | Selected |
|--------|-------------|----------|
| Top, under title | Read-only rendered HTML right below the page header, before vote/date controls — mirrors Details.cshtml | |
| Collapsible section | Collapsed by default since Manage is action-focused; DM expands only to double-check wording | ✓ |

**User's choice:** Collapsible section, reusing the existing `_QuestSection.cshtml` Bootstrap-collapse pattern (card-header button + chevron icon), collapsed by default on both desktop and mobile.

**Notes:** User initially pushed back, believing Description was already visible on Manage ("What are we talking about here? The description is already visible on the manage page right?"). Verified by direct grep against `Manage.cshtml` and `Manage.Mobile.cshtml` — zero references, no partial renders it either. User then asked to confirm which "Manage" page was meant (there's also Quest board index, etc.) — confirmed `QuestController.Manage(id)` / `/Quest/Manage/{id}`, the DM's vote/date/finalize page. Once confirmed, user picked the collapsible option and endorsed reusing the existing `_QuestSection.cshtml` collapse pattern already in the codebase over inventing a new one.

---

## Toolbar visual styling

| Option | Description | Selected |
|--------|-------------|----------|
| Restyle to match app | Custom CSS re-skins toolbar buttons to look like this app's filled-colored controls | |
| Keep EasyMDE default | Ship the library's stock toolbar appearance as-is | ✓ |
| Light-touch: icons only | Keep default button chrome, swap icon classes for FA6 icons | |

**User's choice:** Keep EasyMDE default toolbar appearance, no restyling.

**Notes:** No pushback or follow-up — straightforward selection. Flagged as a planner note that EasyMDE's default icons use FA4-era class names while this app loads FontAwesome 6 with no v4-shim; if icons don't render, the fix is adding the shim, not restyling (restyling was explicitly declined).

---

## Board card rendering

| Option | Description | Selected |
|--------|-------------|----------|
| Full render, same scroll box | Render full formatted HTML inside the existing scroll box | (superseded) |
| Full render, taller box | Same, but increase box height for formatted content | |
| Plain-text preview on card | Card shows a stripped plain-text preview; full HTML only on Details/Manage | ✓ (final) |

**User's choice:** Plain-text preview on the desktop board card only (headings/bold/lists collapse to unstyled text); full rendered HTML on Details, Manage, and the email. Mobile board list stays exactly as today (no Description shown at all — that was already true before this phase). `SelectPosterByContent()`'s existing 130/250/500 character-count thresholds stay untouched, since they now measure the same stripped plain text used for card display.

**Notes:** This area went through several rounds of correction and discovery:
1. Original framing (`.modern-card .card-text`, a fixed 125px scroll box) turned out to be **dead code** — no controller or view renders that partial chain (`_QuestCard.cshtml` is only reachable via `_QuestSection.cshtml`, which itself is never instantiated anywhere in the codebase). Found via targeted Explore-agent investigation after the user initially picked "Full render, same scroll box" against the wrong component.
2. The real live component is `Views/Quest/Index.cshtml`'s `.fantasy-quest-card`, where per-card height is JS-calculated (`site.js:88-102`) from the aspect ratio of a poster image selected by `SelectPosterByContent()` based on `Title.Length + Description.Length` (raw character count) — user confirmed this was intentional, built by them specifically so longer descriptions get a taller/larger poster image.
3. User raised a concern independent of my original question: Markdown syntax overhead (`**`, `#`, `- `, `[text](url)`) would inflate that raw character count, skewing poster selection toward the biggest poster (Poster3, the uncapped ≥500-char bucket) more often than intended for equivalent visual content. User also noted the ≥500 threshold already felt too eager even before Markdown ("almost always resulting in the biggest image being chosen in the end") and that a scrollbar in a smaller poster is an acceptable trade-off in their view — invited a "please advise" design conversation, not just a locked-choice pick.
4. Presented 3 scope options (minimal metric fix / medium fix+threshold-raise / defer). User instead proposed a fourth path: strip Markdown to plain text for card display entirely, sidestepping the length-inflation problem by construction, since the same stripped text can feed both the display and the length calculation. Confirmed this is architecturally clean (post-process the already-rendered HTML rather than a second Markdown parser, consistent with Phase 65's single-parse design) and adopted it as the final decision — no threshold changes needed.
5. Separately confirmed the mobile board list (`Index.Mobile.cshtml`, `.quest-card-mobile`) shows no Description today on any device, addressing the user's suspicion that `.modern-card` was the mobile board style (it is not — mobile board is a distinct compact list component entirely). Decision: leave mobile board list as-is, no Description added there in this phase.

Poster-threshold recalibration itself (the pre-existing "almost always Poster3" concern, independent of Markdown) was **not** addressed in this phase — captured as a Deferred Idea.

---

## Paragraph-break hint

| Option | Description | Selected |
|--------|-------------|----------|
| Short caption below textarea | Small muted "Tip: leave a blank line..." text always visible under the editor | |
| Icon + tooltip near toolbar | Info icon at the end of the toolbar row, tooltip on hover/tap | (superseded) |

**User's choice:** Info-circle icon next to the field's existing label (e.g. next to "Description"), showing a Bootstrap tooltip on hover (desktop) / tap (mobile). Tooltip text: **"Supports Markdown formatting. Leave a blank line between paragraphs."**

**Notes:** User didn't pick either originally offered option — instead proposed a third approach based on this app's existing convention that every textbox already has a title/label above it ("what about a (i) icon next to this title which shows a tooltip on hover?"). Confirmed feasible: Bootstrap's JS bundle is already loaded (used for the `_QuestSection.cshtml` collapse component), so a tooltip needs a one-time JS init call — new wiring, but no new dependency. Then picked the fuller tooltip wording (mentions Markdown support generally, not just the paragraph-break mechanic) over the terser alternative.

---

## Claude's Discretion

- Exact placement/route/controller for the new `POST /markdown/preview` endpoint — no existing AJAX + antiforgery-token precedent exists anywhere in this codebase to follow; this phase introduces the pattern from scratch.
- Whether `_QuestFormScripts.cshtml` gets generalized to also wire EasyMDE, or a separate shared partial is introduced — and how `CreateFollowUp(.Mobile)` (which doesn't currently include `_QuestFormScripts` at all) picks up the same editor wiring.
- Removing Phase 64's `white-space: pre-wrap` CSS from Description's rendered-output containers (`Details.cshtml` has it; `Details.Mobile.cshtml` and `_QuestCard.cshtml` do not) as a companion edit.
- Exact HTML/CSS for the Manage-page collapsible section — follow `_QuestSection.cshtml`'s existing structure; exact class/ID naming is implementation detail.

## Deferred Ideas

- **Poster-selection threshold recalibration** — the `SelectPosterByContent()` 130/250/500 character-count bucket boundaries. User feels the ≥500 "always Poster3" bucket is already too eager even independent of Markdown, and would accept a scrollbar in a smaller poster as a trade-off for more visual variety. Not addressed in this phase (Phase 66's plain-text-on-card decision sidesteps the Markdown-specific version of the problem without touching the numbers). Worth a dedicated look later if still a concern after Phase 66 ships.
