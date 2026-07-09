# Feature Research

**Domain:** Markdown write/preview editing UI (toolbar + textarea + preview toggle) for a small trusted-group web app — non-developer end users
**Researched:** 2026-07-09
**Confidence:** MEDIUM-HIGH (toolbar button sets verified against official library sources; preview-toggle and mobile conventions cross-checked across multiple production tools; one long-standing GitHub UI convention cited from general/product knowledge rather than a freshly pulled source — flagged inline)

*Replaces the v7.0 Backlog Cleanup research (crop-before-save avatar UX + waitlist auto-promotion) previously in this file — that milestone shipped (Phases 45/46/44, see PROJECT.md). This is fresh research for the v8.0 Markdown Support milestone.*

## Scope Note

This research answers "what's table stakes vs optional" for the toolbar/preview UX layered on top of the app's **already-locked** design: plain textarea + formatting toolbar (Bold/Italic/Heading/List insert syntax at cursor) + a single Preview button that toggles the same input area between edit and rendered-HTML preview. Those four locked decisions are not re-litigated here. This file's job is everything *beyond* that: which additional toolbar buttons earn their keep, how the Preview toggle itself should look/behave, how to signal "Markdown is supported," and how the toolbar should adapt on mobile.

The audience distinction matters throughout: this is **not** GitHub, GitLab, or a docs tool built for developers who already know Markdown syntax by heart. It's ~17 D&D players/DMs writing quest blurbs, character backstories, and shop item flavor text — casually, on both desktop and phone. Every recommendation below is filtered through that lens, not through "what does every markdown editor library ship by default."

## Feature Landscape

### Table Stakes (Beyond the Locked Bold/Italic/Heading/List)

Buttons users will genuinely reach for, given the fields in play (Quest Description, Quest Rewards, Quest Recap, Character Description/Backstory, Contact Description/Notes, DM Profile Bio, Shop Item Description).

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Link insertion | Every general-purpose Markdown toolbar surveyed includes it (GitHub's `markdown-toolbar-element`, EasyMDE, Discourse composer). D&D players will link D&D Beyond sheets, wiki pages, session notes, shop reference images. | LOW–MEDIUM | Minimal viable version needs no modal/dialog: wrap the current selection as `[selected text](url)` and place the cursor inside the `()` ready to paste — this is exactly what GitHub's own `<md-link>` toolbar button does. No URL-prompt dialog required for v1. |
| Blockquote | Present in every toolbar surveyed (GitHub, EasyMDE, Discourse). Genuinely useful for this domain specifically — NPC dialogue, in-character quotes, DM flavor text in Quest Description/Recap/Contact Notes. | LOW | Mechanically identical to the already-locked Heading/List buttons: prefix each line of the selection with `> `. Because Heading/List already establish the "prefix-insertion" mechanism, adding Blockquote is near-zero marginal engineering cost — this is the strongest argument for including it as table stakes rather than deferring it. |
| Preview toggle disables the rest of the toolbar while active | Confirmed convention: EasyMDE's Preview button carries a `no-disable` class specifically so it stays clickable while every *other* toolbar button is disabled during preview mode (there's no textarea selection to act on while viewing rendered HTML). | LOW | Not a "button" per se, but a required *behavior* of the toolbar + preview interaction. Skipping this means Bold/Italic/etc. appear clickable but do nothing meaningful while in preview mode — confusing for non-technical users. |
| 44px+ icon-only touch targets on mobile toolbar buttons | This app already enforces `min-height: 44px` on `textarea`/`.form-control`/`.form-select` (`QuestBoard.Service/wwwroot/css/mobile.css`). WCAG 2.2 AAA and platform HIG guidance (Apple 44pt, Android 48dp) both converge on this figure; general mobile-UX research confirms 44px minimum with ~8px gaps between adjacent icon buttons is the accepted floor. | LOW | Not a "feature" but a hard mobile constraint the whole toolbar must satisfy — this app already has the precedent, so it's non-negotiable to extend it to the new toolbar. |

### Differentiators (Nice-to-Have, Genuinely Optional for a Casual 17-Person Group)

These appear in mainstream toolbars (GitHub, EasyMDE) but the case for building them *now* is weaker than the table-stakes set above — either low marginal value for this domain, or non-trivial added complexity relative to the payoff.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Strikethrough | EasyMDE ships it by default; visually fun for "crossing out" old quest rewards or jokey corrections. Not part of core CommonMark — it's a GFM extension. | LOW (if the chosen Markdown renderer already has GFM strikethrough enabled — a STACK.md decision, not verified here) | Cheap add-after-launch if the parser supports it out of the box; skip if it requires extra extension wiring for marginal value. |
| Horizontal rule | Appears in EasyMDE's default set but is notably absent from GitHub's `markdown-toolbar-element` (a comment-focused, not document-focused, tool). Could help visually separate scenes in a long Quest Recap. | LOW | Trivial fixed-string insertion (`\n\n---\n\n`). Weak case for v1: strict CommonMark's blank-line paragraph spacing (already a locked behavior change) already gives visual separation between blocks, reducing the need for an explicit divider. |
| Cheatsheet link/popover ("?" icon or footer link) | Common pattern (GitHub's well-known small "Markdown is supported" link near the compose box footer; various forum tools use a "?" popover instead). | LOW | See the dedicated discoverability section below — recommend deferring a full cheatsheet in favor of a narrower, more targeted hint (see Table Stakes discoverability item). Add a cheatsheet only if users report confusion after the narrower hint ships. |
| Task list / checkboxes | Present in GitHub's toolbar (their audience uses it heavily for PR checklists) and EasyMDE's `check-list`. Could suit a "loot checklist" in Quest Recap. | MEDIUM | Raises a real design question this milestone hasn't resolved: should rendered checkboxes be interactive (clickable, persisting state) or static? Static-only is simple but half-useful; interactive requires new persistence plumbing this milestone doesn't otherwise need. In the 3 HTML email templates, checkboxes can only ever render static/non-interactive regardless — inconsistent behavior between web view and email is a real risk. Recommend deferring entirely rather than half-building it. |

### Anti-Features (Skip Entirely — Not Just "Later")

Features that appear in general-purpose Markdown toolbars but actively don't fit this app's domain or already-shipped architecture. Building these would be over-engineering for a 17-person casual group, not under-building.

| Feature | Why It's In Other Toolbars | Why Problematic Here | Alternative |
|---------|---------------------------|------------------------|-------------|
| Inline code / code block button | Core to GitHub's toolbar and EasyMDE because their audience is developers referencing code. | Zero use case in a D&D quest-board domain — no dice notation, no stat blocks, no code snippets anywhere in the 9 target fields. Building it just because "every markdown toolbar has it" is exactly the generic-developer-tool assumption this research is meant to guard against. | Don't build. If a user types raw `` `backtick` `` syntax manually, strict CommonMark will still render it — no toolbar affordance needed. |
| Image embed button (`![alt](url)`) | Standard in GitHub/EasyMDE toolbars, usually paired with drag-and-drop upload. | This app already has a dedicated, purpose-built photo-upload pipeline (Cropper.js crop-before-save, dual original/cropped `byte[]` storage) for Character/Contact/DM Profile photos. A markdown image-embed button would (a) duplicate that feature with a worse UX, and (b) introduce unmoderated external-image loading (hotlinking, mixed-content risk, no upload target) for a feature nobody asked for. | Don't build. Structured photo upload already exists where images matter; free-text fields don't need inline images. |
| Table button/syntax support | EasyMDE ships it; common in longer-form document editors. | GFM tables are notoriously painful to hand-edit as pipe-delimited plain text in a bare textarea, especially on a 320–390px mobile viewport — exactly the device class this app must support well. None of the 9 target fields are long-form documents that would benefit from tabular data. | Don't build a toolbar button. If the underlying parser happens to support GFM table *syntax*, that's an incidental capability, not a promoted feature — no UI encouragement. |
| @Mention / #Ref buttons | Core to GitHub's toolbar because GitHub has users and issues to reference. | No analog in this app's data model — there's no issue tracker, and @mentioning a specific group member would require new notification infrastructure this milestone doesn't include. | Don't build. Out of scope, not deferred. |
| Side-by-side / split-pane preview | Common "advanced" mode in EasyMDE and other editors. | Already excluded by the locked design (toggle-in-place, not split-pane) — and this research *confirms* that decision is correct, not just simpler: EasyMDE explicitly ships a `no-mobile` CSS class that disables side-by-side (and fullscreen) specifically because it doesn't work on small screens. Discourse's own UX team independently converged on the same toggle-not-split approach for mobile after live debate. | The locked toggle-in-place design is the right call for both desktop and mobile — no further action needed. |
| Fullscreen editing mode | Present in EasyMDE's default toolbar for long-form writing. | None of the 9 target fields are long-form documents (they're quest blurbs, item descriptions, short bios) — fullscreen solves a document-editing problem this app doesn't have. | Don't build. |
| Full syntax cheatsheet as a modal/popover, shipped in v1 | Many tools link out to or pop up a full Markdown reference. | Given the toolbar already covers Bold/Italic/Heading/List/Link/Blockquote as clickable buttons, most users will never need to know raw syntax exists — a full reference document is solving a problem this button-driven design mostly prevents. The one behavior genuinely worth calling out (strict CommonMark's blank-line paragraph rule, a locked breaking change from today's behavior) is narrow enough that a full cheatsheet is overkill for it. | Ship a single short inline hint about the paragraph-break change instead (see below). Add a cheatsheet later only if users actually get confused. |

## Preview Toggle: Standard UX Conventions

Two dominant conventions exist in production tools, and they matter for how the *already-locked* single-toggle-button design should look and behave:

1. **Tab-style "Write / Preview" segmented control** (GitHub comments/PRs, and tools built in imitation of GitHub) — two labeled tabs, clicking either one switches which pane is shown. Functionally single-pane-replace, same as this app's locked design, just styled as tabs instead of a single button.
2. **Single toggle button** (EasyMDE's `preview` toolbar icon) — one button that flips between "show editor" and "show rendered preview," with the button's own icon/label changing to reflect the action it will perform next (i.e., it reads "Preview" while editing, and effectively becomes "back to edit" once toggled).

The locked decision ("a Preview button that toggles the same input area... not a split-pane") matches convention #2 more closely. Recommendations:

- **Icon:** eye / eye-slash (a widely recognized "preview/reveal" glyph, e.g. FontAwesome `fa-eye` / `fa-eye-slash`, already the icon family used elsewhere in this codebase).
- **Label behavior:** switch the button's text between "Preview" and "Edit" depending on current mode, rather than using one static label. This mirrors a pattern the codebase already uses elsewhere (e.g. the Quest Recap "Add Recap"/"Edit Recap" button whose label switches based on state, shipped in Phase 53) — reusing an established in-app convention is stronger precedent here than importing an external tool's convention wholesale.
- **Getting back to editing:** the same toggle button flips back — do not add a separate "Cancel preview" or "Back" control. EasyMDE's preview button is a literal toggle, not two one-way actions, and that's the simpler, more discoverable pattern for non-technical users.
- **Toolbar state during preview:** grey out/disable Bold/Italic/Heading/List/Link/Blockquote while in preview mode, leaving only the Preview/Edit toggle itself clickable — directly mirrors EasyMDE's `no-disable` class convention and avoids dead-looking-but-clickable buttons.

## "Markdown Is Supported" Discoverability

Three patterns observed across the tools surveyed:

1. **The toolbar itself is the affordance.** Because Bold/Italic/Heading/List/Link icons are self-explanatory, many casual tools (Trello's current editor, Notion-style tools) don't separately announce "Markdown is supported" at all — button-driven formatting makes the underlying syntax mostly invisible to the end user. This is the closest analog to this app's locked design.
2. **A small, unobtrusive icon + link near the compose box** pointing to a syntax reference doc — the long-standing convention on GitHub's comment/PR text boxes (a small markdown glyph + "Markdown is supported" text near the bottom of the box, linking out to their docs). **Confidence: MEDIUM** — this is a very widely recognized, long-standing pattern from personal/product familiarity with GitHub's UI, but a fresh citable source for the exact current wording/placement could not be pulled this session; treat as a well-known convention rather than a freshly verified one.
3. **A "?" icon opening an inline popover/tooltip cheatsheet** — used by various forum/comment tools to avoid navigating the user away from the compose box.

**Recommendation for this app:** lean toward pattern #1 as the primary approach (the toolbar buttons already do the explaining), plus one narrow, targeted piece of inline hint text rather than a full syntax reference — because the one thing users can get wrong even while using toolbar buttons is the **strict CommonMark paragraph rule** (blank line required between paragraphs; a single Enter no longer creates a visible line break). That's a deliberate, locked behavior change from how these fields render today, and it's the one surprise worth calling out explicitly. A short line near the toolbar — something like "Press Enter twice to start a new paragraph" — solves the actual confusion risk without the overhead of a full cheatsheet link/popover (pattern #2/#3), which can be added later if real usage shows people still need it.

## Mobile Toolbar Adaptation

- **Icon-only buttons, no text labels**, is the near-universal mobile pattern to conserve horizontal space — confirmed in GitHub's `markdown-toolbar-element` (icon buttons with tooltips serving as the accessible name/label) and EasyMDE.
- **Keep the button count small enough that no overflow mechanism is needed at all.** The recommended v1 set (Bold, Italic, Heading, List, Link, Blockquote, Preview toggle = 7 controls) should fit a single row on a ~320–390px mobile viewport at 44px touch targets with adequate gaps, avoiding the need to build a horizontal-scroll row or an overflow "more" menu — both of which are real, nontrivial extra engineering for a 17-person casual-use app. If a future differentiator (Strikethrough, Horizontal Rule) pushes the count higher, horizontal scroll (not wrapping to a second row, which eats vertical space that's already scarce on mobile) is the standard fallback — but it isn't needed for the recommended v1 set.
- **Advanced/complex modes are what actually get stripped on mobile in the wild**, not the core formatting buttons — EasyMDE's `no-mobile` class specifically targets side-by-side and fullscreen, not Bold/Italic/Link. This app's locked single-toggle-preview design already avoids that whole problem class on both desktop and mobile, which this research confirms is the right simplification rather than a corner cut.
- **Touch target sizing**: reuse the app's own existing `min-height: 44px` convention (`mobile.css`) for every toolbar button, with ~8px gaps between adjacent icons to prevent mis-taps, consistent with WCAG 2.2 AAA / platform HIG guidance found across the mobile-accessibility sources reviewed.

## Feature Dependencies

```
Preview toggle
    └──requires──> Markdown→HTML rendering pipeline
                       └──MUST be the exact same renderer used by the read views and the 3 HTML email templates

Link button ──shares mechanism with──> (none; simplest as its own selection-wrap insertion)

Blockquote button ──reuses mechanism from──> Heading/List (already-locked prefix-insertion pattern)

Horizontal rule / Strikethrough ──independent of──> everything else (pure fixed-string / wrap insertions)

Task list / checkboxes ──conflicts with──> Email template rendering (interactive checkboxes have no equivalent in static HTML email; risks inconsistent behavior between web and email)
```

### Dependency Notes

- **Preview requires the shared rendering pipeline, not a separate "preview renderer":** this is the single most important dependency to flag for requirements/architecture. If Preview mode renders Markdown through different logic/rule-set than the final read-view or email render, users will see a preview that doesn't match what actually gets saved/displayed later — a classic "preview lied to me" trust failure. The Preview button must call the identical Markdown→HTML conversion used everywhere else the field is displayed (read views, and the 3 email templates that echo Quest Description). This should be locked as a hard requirement, not left as an implementation detail.
- **Blockquote reuses the Heading/List mechanism:** both already-locked buttons establish a "prefix each selected line with a fixed string" insertion pattern. Blockquote (`> `) is the same mechanic with a different prefix — near-zero marginal cost, which is why it's recommended as table stakes rather than deferred.
- **Task list conflicts with the email requirement:** the milestone's locked requirement that formatting must also render correctly in the 3 HTML email templates makes interactive checkboxes a genuine architectural mismatch (emails can't have live, stateful checkboxes) — this is the concrete reason to defer/skip Task List rather than a vague "added complexity" hand-wave.

## MVP Definition

### Launch With (this milestone)

- [ ] Link button — selection-wrap insertion (`[text](url)`), no modal dialog required
- [ ] Blockquote button — reuses the Heading/List prefix-insertion mechanism
- [ ] Preview toggle behavior refinements: eye icon, label switches Preview ⇄ Edit, disables the rest of the toolbar while active, renders through the exact same pipeline used by read views and the 3 email templates
- [ ] One short inline hint calling out the strict-CommonMark paragraph-break behavior change (not a full cheatsheet)
- [ ] Mobile: icon-only buttons at 44px touch targets, single row, no scroll/overflow mechanism needed at this button count

### Add After Validation (if users ask, or if trivially cheap given the chosen parser)

- [ ] Strikethrough — only if the Markdown renderer already has GFM strikethrough enabled at no extra cost
- [ ] Horizontal rule
- [ ] Cheatsheet link/popover — only if users report confusion beyond the paragraph-break hint

### Explicitly Out of Scope (Anti-Features for This Domain)

- [ ] Inline code / code block button — no code/dice-notation use case in this app
- [ ] Image embed button — redundant with the existing structured photo-upload pipeline; introduces external-image risk
- [ ] Table button/syntax promotion — painful to hand-edit on mobile, low value for short free-text fields
- [ ] Task list / checkboxes — interactivity mismatch with static HTML email rendering
- [ ] @Mention / #Ref — no analog in this app's data model
- [ ] Side-by-side / split-pane preview — already correctly excluded by the locked design
- [ ] Fullscreen editing mode — none of the 9 fields are long-form documents

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Link button | HIGH | LOW | P1 |
| Blockquote button | MEDIUM-HIGH | LOW | P1 |
| Preview toggle refinements (icon/label/disable-toolbar) | HIGH | LOW | P1 |
| Shared render pipeline for preview = read view = email | HIGH (prevents a trust bug) | LOW-MEDIUM | P1 |
| Paragraph-break inline hint | MEDIUM | LOW | P1 |
| Mobile 44px icon-only toolbar | HIGH (accessibility/usability) | LOW | P1 |
| Strikethrough | LOW-MEDIUM | LOW (conditional) | P2 |
| Horizontal rule | LOW | LOW | P2 |
| Cheatsheet link/popover | LOW | LOW | P3 |
| Task list / checkboxes | LOW-MEDIUM | MEDIUM (email mismatch) | P3 (or skip) |
| Inline code | LOW (no domain fit) | LOW | Skip |
| Image embed | LOW (duplicates existing feature) | MEDIUM | Skip |
| Table | LOW (poor mobile fit) | MEDIUM | Skip |
| @Mention / #Ref | NONE (no data model fit) | HIGH | Skip |
| Side-by-side preview | NONE (contradicts locked design + mobile evidence) | — | Skip |
| Fullscreen mode | LOW (no long-form fields) | — | Skip |

**Priority key:**
- P1: Recommended for this milestone
- P2: Cheap to add later if users want it
- P3: Only if real usage demonstrates need
- Skip: Explicitly out of scope for this domain, not just deferred

## Competitor / Reference Tool Analysis

| Aspect | GitHub (`markdown-toolbar-element`) | EasyMDE (generic embeddable JS library) | Discourse (forum composer) | Our Approach |
|--------|--------------------------------------|------------------------------------------|------------------------------|--------------|
| Core toolbar | Bold, Italic, Header, Quote, Code, Link, Image, UL, OL, Task List, Mention, Ref | Bold, Italic, Strikethrough, Heading, Quote, UL, OL, Check-list, Link, Image, Table, Preview, Side-by-side, Fullscreen, Guide | Formatting + quoting + upload buttons | Bold, Italic, Heading, List (locked) + Link, Blockquote (this research) — audience-scoped down from developer/document-tool defaults |
| Preview | Separate "Write/Preview" tab pair | Single toggle button + separate side-by-side/fullscreen modes (side-by-side disabled on mobile via `no-mobile` class) | Live preview panel, with a newer rich-text WYSIWYG mode offered as an alternative to raw Markdown | Single toggle button (locked), in-place replace on both desktop and mobile — matches the mobile-tested subset of both reference tools |
| "Markdown supported" hint | Small icon/link near compose box (long-standing convention, MEDIUM confidence per above) | "Guide" toolbar button linking to a syntax guide | N/A (dual-mode UI reduces the need) | Narrow inline hint about the paragraph-break rule; skip full cheatsheet for v1 |
| Mobile handling | Icon buttons w/ tooltip accessible names | `no-mobile` class strips side-by-side/fullscreen; core buttons remain | Toolbar visibility is context-dependent, debated at length by their own UX team, converged on discrete/toggled preview over split view | 44px icon-only buttons, single row, no overflow mechanism needed for the recommended button count |
| Code / images / tables / mentions | All present (developer-focused audience) | Code absent from list but table/image present (document-focused audience) | N/A | All explicitly excluded — none fit this app's domain or existing architecture |

## Sources

- [github/markdown-toolbar-element (official GitHub repo)](https://github.com/github/markdown-toolbar-element) — HIGH confidence, official source for GitHub's actual toolbar button set
- [Ionaru/easy-markdown-editor (official EasyMDE repo)](https://github.com/Ionaru/easy-markdown-editor) — HIGH confidence, official source for EasyMDE's default toolbar, mobile (`no-mobile`) class, and preview `no-disable` behavior
- [Discourse Meta: Choosing the default composer mode](https://meta.discourse.org/t/choosing-the-default-composer-mode-for-your-community/375476) — MEDIUM confidence, official product forum
- [Discourse Meta: Mobile editor preview button and toolbar](https://meta.discourse.org/t/mobile-editor-preview-button-and-toolbar/113942) — MEDIUM confidence, real UX debate among Discourse's own team on mobile preview/toolbar tradeoffs
- [Smart Interface Design Patterns: Accessible Tap Target Sizes](https://smart-interface-design-patterns.com/articles/accessible-tap-target-sizes/) — MEDIUM confidence, corroborates existing in-app 44px convention
- [Atlassian Support: Format text in Trello](https://support.atlassian.com/trello/docs/how-to-format-your-text-in-trello/) — MEDIUM confidence, illustrates the alternative WYSIWYG-over-raw-Markdown industry direction (contrast case, not directly adopted since this app's design is already locked to raw-Markdown-plus-toolbar)
- GitHub's small "Markdown is supported" compose-box hint — LOW-MEDIUM confidence, cited from general product familiarity; a fresh citable source for current exact wording/placement was not found this session. Treat as a well-known convention needing no further verification, not as a load-bearing claim.
- `QuestBoard.Service/wwwroot/css/mobile.css` — this repository's own existing `min-height: 44px` touch-target convention, used as the grounding constraint for all mobile toolbar recommendations above

---
*Feature research for: Markdown write/preview editor UX (toolbar buttons + preview toggle conventions)*
*Researched: 2026-07-09*
