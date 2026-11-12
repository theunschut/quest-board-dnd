# Phase 94: Whole-App UI Review and Unification - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-30
**Phase:** 94-whole-app-ui-review-and-unification
**Areas discussed:** How far the upgrade goes, Phase split, Cascade strategy, Third-party assets, Inline scripts & styles, Mobile twins, Accessibility bar, Email templates

---

## How far the upgrade goes

**Q: What visual direction should the unified system have?**

| Option | Description | Selected |
|--------|-------------|----------|
| Evolve today's look (Recommended) | Keep the identity; senior-frontend mocks 2–3 polished takes on key pages; sign-off before any view changes | ✓ |
| Unify only, no mockups | Keep today's look exactly, make it consistent and bug-free | |
| Fresh direction | Mockups of genuinely different directions, possibly dropping parchment/notice-board | |

**Q: The Shop runs its own dark-navy "Witcher 3 inspired" theme. Where should it land?**

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it distinct, tokenised (Recommended) | A named theme variant on the same tokens and components | |
| Fold into the main look | Parchment/gold like every other page | |
| Decide from the mockups | Mock the shop both ways and choose at sign-off | ✓ |

**Q: Which pages should the direction mockups cover?**

| Option | Description | Selected |
|--------|-------------|----------|
| Core loop + extremes (Recommended) | Quest board, Quest Details, Quest Create/Edit, Calendar, Shop both ways | ✓ |
| Quest board + Details only | Just the core loop | |
| You pick after the audit | senior-frontend picks pages after auditing | |

**Q: When the review finds a page that would clearly benefit, how far may its upgrade go?**

| Option | Description | Selected |
|--------|-------------|----------|
| Restyle + re-layout, same controls (Recommended) | Rearranging allowed; every field/link/button/action stays; before/after sign-off | ✓ |
| Restyle only | Classes and wrappers only, no structural change | |
| Anything short of new features | Re-layout plus small UX changes (move actions, collapse sections, change control types) | |

**Notes:** No further questions on typography, motion or dark mode were taken up.

---

## Phase split

**Q: What should Phase 94 itself deliver?**

| Option | Description | Selected |
|--------|-------------|----------|
| Audit + direction + foundation (Recommended) | Audit, mockups and sign-off, tokens and components, layouts, ui-guidelines; area phases after | ✓ |
| Audit + direction only | No production code in 94; foundation becomes its own phase | |
| One big phase | Everything in 94 | |

**Q: How should the follow-up area phases be cut and added to the roadmap?**

| Option | Description | Selected |
|--------|-------------|----------|
| Audit proposes, you approve (Recommended) | Roadmap's five-area draft adjusted to findings; 94 appends them | ✓ |
| Fix the roadmap's five now | Lock the five areas today | |
| Add them later yourself | 94 only suggests a split | |

**Q: How should releases handle the gap between the foundation and the area phases?**

| Option | Description | Selected |
|--------|-------------|----------|
| Ship each phase; no page worse (Recommended) | Release as usual; foundation keeps unconverted pages looking right | |
| Hold until the sweep is done | No UI release until every area phase is done | |
| Opt-in preview | New look behind a toggle | |

**User's choice (free text):** "We can release each phase as usual, but we don't have to deploy it immediatly. If we wait untill all (or most) phases are done until deployment that's also fine with me"
**Notes:** The draft-release `deploy` approval gate from Phase 89 already lets the operator hold deployment.

**Q: Since deployment can wait, how good must unconverted pages look while the sweep is in progress?**

| Option | Description | Selected |
|--------|-------------|----------|
| Usable, not pixel-perfect (Recommended) | Every page working and readable at every merge; may look older until converted | ✓ |
| No page worse than today | Keep all old classes looking exactly right | |
| Anything goes until deploy | Only the final state is judged | |

---

## Cascade strategy

**Q: How should the foundation tackle the 615 hex literals and 814 !important declarations?**

| Option | Description | Selected |
|--------|-------------|----------|
| Tokens + @layer, convert per area (Recommended) | Tokens and ordered layers in 94; area phases strip literals and !important | |
| Tokens only, targeted fixes | Custom properties; fix !important only where it bites | |
| Rewrite the stylesheets | New stylesheet set; old files deleted per area | |

**User's choice (free text):** "I don't care, leave it up to you or the researcher to decide"
**Notes:** Recorded as Claude's discretion, with tokens plus `@layer` as the lean.

---

## Third-party assets

**Q: Where should Bootstrap, Font Awesome and the Cinzel font be served from?**

| Option | Description | Selected |
|--------|-------------|----------|
| Self-host everything (Recommended) | wwwroot/lib, ships in release zip and Docker image, no Google Fonts IP leak | ✓ |
| Keep CDNs, add SRI | Pin every CDN link with an integrity hash | |
| You decide | Leave it to research and planning | |

**Q: jQuery 3.6.0 and the Font Awesome v4 shim look unused. What about them and the library versions?**

| Option | Description | Selected |
|--------|-------------|----------|
| Drop dead ones, patch-bump the rest (Recommended) | Remove once proven unused; latest Bootstrap 5.3.x and Font Awesome 6.x | ✓ |
| Drop dead ones, keep versions | Remove if unused; keep 5.3.0 and 6.4.0 | |
| Also consider majors | Let research weigh Font Awesome 7 or similar | |

---

## Inline scripts & styles

**Q: Should the 97 style="" attributes, 4 <style> blocks and 43 inline <script> blocks move out of the views?**

| Option | Description | Selected |
|--------|-------------|----------|
| Move them as pages convert (Recommended) | 94 cleans layouts and sets the rule; area phases move their pages' inline code; CSP header later | ✓ |
| Styles only | Move inline styles, leave inline scripts | |
| Leave them | Only new code avoids inline style and script | |

---

## Mobile twins

**Q: What should happen to the desktop/mobile twin pairs?**

| Option | Description | Selected |
|--------|-------------|----------|
| Keep by default; audit may propose merges (Recommended) | Align every twin; near-identical pairs may merge with operator approval | ✓ |
| Keep all 55, align only | No merges | |
| Merge wherever possible | One responsive view per page unless mobile genuinely differs | |

**Q: When a twin pair differs in content, what does alignment do?**

| Option | Description | Selected |
|--------|-------------|----------|
| Flag, you decide each (Recommended) | Look only; audit lists differences, marks known-deliberate ones, asks about the rest | ✓ |
| Look only, never content | Content differences not even listed | |
| Bring mobile up to desktop | Desktop is the reference | |

**Q: When do real-phone checks happen?**

| Option | Description | Selected |
|--------|-------------|----------|
| Per phase, may defer to deploy (Recommended) | Listed per phase; run on LAN dev build or batched on production after deploy | ✓ |
| Per phase, before close | No phase closes until real-phone checks pass | |
| One batch at the end | Emulated checks only until a final real-phone pass | |

**User's choice (free text):** "use recommended option, but if it's possible using the browser with a mobile view mode, that's also fine with me."
**Notes:** Browser emulation with a mobile User-Agent (so the server serves the `.Mobile` views) is accepted as verification; a physical phone is optional.

---

## Accessibility bar

**Q: What accessibility bar should the unified system meet?**

| Option | Description | Selected |
|--------|-------------|----------|
| WCAG AA + 44px on mobile (Recommended) | 4.5:1 text, 3:1 large text and controls, visible focus, 44px mobile targets | ✓ (by Claude) |
| Fix the clearly broken only | No formal contrast target | |
| Stricter (AAA-leaning) | 7:1 body text, 44px targets on desktop too | |

**User's choice (free text):** "I don't know? up to you"
**Notes:** Claude chose WCAG AA plus 44px mobile targets. Signature colours stay, and their use or shade changes where they fail. The mockups must meet the bar so the operator sees the effect at sign-off.

---

## Email templates

**Q: Should the .razor email templates join this sweep?**

| Option | Description | Selected |
|--------|-------------|----------|
| Keep them out (Recommended) | Different rules; note mismatches as deferred | |
| Light alignment only | Match colours and header after sign-off | |
| Full email pass | Review and redesign every template as an appended area phase | ✓ |

**Q: For the email phase, how are redesigned emails checked?**

| Option | Description | Selected |
|--------|-------------|----------|
| Rendered preview + real inbox (Recommended) | Screenshot rendered HTML; test-send each to the operator's inbox | ✓ |
| Rendered preview only | Browser screenshots only | |
| You decide | Leave to the planner | |

---

## Claude's Discretion

- The cascade strategy for hex literals and `!important` (lean: tokens plus `@layer`, converted per area).
- Accessibility bar (Claude chose WCAG AA plus 44px mobile targets).
- Whether and when the `modern-card.css` element-enumerated cream `!important` rule is retired.
- How page stylesheets are loaded (today they're all global on desktop).
- The self-hosting mechanism, the audit report format, where screenshots are stored, and the order of the area phases.

## Deferred Ideas

- Adopt a Content-Security-Policy header: a separate security phase after the sweep.
- Major library migrations (Bootstrap 6, Font Awesome 7): excluded.
