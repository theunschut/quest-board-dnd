# Pitfalls Research

**Domain:** Retrofitting Markdown authoring/rendering onto existing plain-text fields with real production data (D&D Quest Board, v8.0 Markdown Support)
**Researched:** 2026-07-09
**Confidence:** HIGH (grounded directly in this codebase's email templates, entity schemas, and CSS; external claims corroborated by the CommonMark spec, Markdig's own docs, and multiple independent sources)

## Critical Pitfalls

### Pitfall 1: Removing Razor's automatic HTML-encoding to show rendered Markdown reopens an XSS hole that has never existed in this codebase

**What goes wrong:**
Today, every read view and all 3 target email Razor components (`QuestFinalized.razor`, `SessionReminder.razor`, `WaitlistPromoted.razor`) echo these fields via plain `@FieldName` interpolation, which Razor auto-HTML-encodes. Even though these fields are already user-authored and cross-viewed within the group, this app has never had a stored-HTML-injection surface on them. The instant this milestone switches Description/Notes/Bio/etc. from `@Field` to `@Html.Raw(RenderedHtml)` / `MarkupString` — unavoidable, since you cannot show a rendered `<strong>`/`<ul>` without disabling encoding on that output — the safety net Razor was silently providing disappears everywhere at once: 9 fields simultaneously, plus 3 emails, not one field at a time.

**Why it happens:**
Teams treat "switch to `Html.Raw`" as a trivial view-layer rendering change rather than recognizing it as removing the only XSS control this data has ever had. The Markdown library gets evaluated for "does it render correctly," not "what happens when the input is `<img src=x onerror=alert(1)>` or `[link](javascript:alert(1))`."

**How to avoid:**
Build the sanitization step as its own Domain-layer service (e.g. `IMarkdownRenderingService.ToSafeHtml(string markdown)`) before any view is touched, and route every call site — all 9 read views AND all 3 email components — through that single service, never a raw `Markdown.ToHtml()` call in a view or controller. Pipe the Markdig output through an allowlist sanitizer (HtmlSanitizer / Ganss.XSS is the standard .NET pairing — Markdig's own `.DisableHtml()` extension only encodes literal HTML typed into the Markdown source; it does not validate `javascript:`/`data:` URI schemes in `<a href>`/`<img src>` attributes that Markdig legitimately generates from valid `[text](url)`/`![alt](url)` syntax). Enable Markdig's `nofollow`/`noopener`/`noreferrer` link-rel extensions so Markdown-authored links can't be used for tabnabbing.

**Warning signs:**
Any code path where a `Markdown.ToHtml(...)`-equivalent result reaches a view without a sanitizer call in the same method; a PR adding `MarkupString`/`Html.Raw` without a corresponding sanitizer dependency; sanitization applied only to Contact Notes "because that's the collaborative one" while the other 8 fields are treated as lower-risk (all 9 are rendered to other group members' browsers — none should get a "trusted author" exemption).

**Phase to address:**
The rendering-service phase — build the shared sanitizing renderer before any field is wired to it, not re-solved per field.

---

### Pitfall 2: Email HTML output isn't validated as email-safe just because it's XSS-safe

**What goes wrong:**
A sanitizer answers "is this HTML safe to inject into a DOM," not "will this HTML render legibly in Outlook/Gmail/Apple Mail." Markdig's default HTML output for headings/lists/blockquotes relies on browser-default CSS spacing with no inline styles — fine for the main app (which loads `site.css`), but the 3 email templates are self-contained documents sent through Postfix/Resend where CSS support is inconsistent per client. A `<ul>` dropped unmodified into `QuestFinalized.razor` shows correctly in Apple Mail/most webmail but silently loses its bullets in Outlook desktop (the Word rendering engine does not support `<ul>`/`<li>` bullets without the proprietary `mso-special-format:bullet` CSS hack), and `<style>` blocks are inconsistently honored across clients (Gmail webmail strips `<style>` tags and requires inline styles — this app's own `_EmailLayout.razor` already relies entirely on inline `style=` attributes for exactly this reason). A naive Markdig HTML dump breaks that existing inline-styling convention.

**Why it happens:**
"XSS-safe" and "looks right in the email" get conflated into one checkbox. The team's only existing precedent for HTML-in-email is the hand-crafted, fully-inline-styled `_EmailLayout.razor` family — there is no existing pattern in this codebase for injecting a variable-shape HTML fragment (arbitrary mix of headings/lists/paragraphs/blockquotes/code) into that layout.

**How to avoid:**
Do not pipe raw sanitized Markdig HTML directly into the email templates. Add an email-specific post-processing step (custom Markdig `HtmlRenderer`/`ObjectRenderer` overrides, or a CSS-inlining pass after sanitization) that emits inline `style=` attributes on every block element Markdig can produce (`p`, `ul`, `ol`, `li`, `blockquote`, `h1`-`h6`, `strong`, `em`, `a`, `code`) matching the visual language already used in `_EmailLayout.razor` (Georgia serif, `#1a0f08` text color, consistent margins). Add the MSO conditional-comment bullet fix for lists. Verify all 3 templates in a real Outlook desktop instance and Gmail webmail — not just a browser preview of the raw HTML — before considering the phase done.

**Warning signs:**
Templates render correctly when previewed in a browser (opening the raw HTML file, or a local `dotnet run` preview) but the milestone is marked done without ever opening a sent test email in real Outlook desktop or Gmail webmail; every test Quest Description used during verification is a single short paragraph (same shape as today's data) rather than genuine Markdown structure (heading + list + blockquote).

**Phase to address:**
A dedicated email-integration phase, sequenced after the rendering-service phase is stable. Do not fold email HTML-safety into the same phase as the core Markdig pipeline — it needs its own template-level styling work and its own non-automatable manual multi-client check.

---

### Pitfall 3: The 3 email templates' fixed-height, `overflow:hidden` description card was tuned for one short italic paragraph — Markdown block content can silently overflow or get clipped, especially in Outlook

**What goes wrong:**
`QuestFinalized.razor`, `SessionReminder.razor`, and `WaitlistPromoted.razor` all wrap the content in `<td style="height:840px;overflow:hidden;">` with the description itself in `<div style="height:100%;overflow-y:auto;padding-right:6px;">` around a single `<p>@QuestDescription</p>`. This was built assuming `QuestDescription` is one flowing paragraph — its current production shape, and there's no length ceiling forcing brevity either (`QuestEntity.Description`/`.Rewards` carry no `[StringLength]` attribute, so EF Core maps them to unbounded `nvarchar(max)`). Once Description is Markdown-rendered, a DM writing a heading + a bulleted loot list + a blockquote for read-aloud text produces meaningfully taller content (extra margin between block elements) than the same word count as one paragraph ever did. `overflow-y:auto` is a browser scrolling affordance that most email clients — Outlook desktop chief among them — do not honor; Outlook will either clip the overflow at 840px with no way to see it, or push content past the "Poster1.png" background frame, breaking the card's visual design.

**Why it happens:**
The card layout was designed and tuned against the historical shape of the data. Nobody revisits fixed-height/overflow CSS when the feature that changes the content's *shape* (Markdown) is designed and reviewed independently of the emails that consume it — the rendering-service phase's own test data will almost certainly be a few sentences of prose (matching every other test in the 600+ suite) and will never surface this, because it will "fit."

**How to avoid:**
Treat the email description card as needing an explicit design decision, not an inherited one: either (a) render only a truncated excerpt of the Markdown server-side with a "View full quest details" link back into the app, sidestepping unbounded-height content in email entirely, or (b) remove the fixed `840px`/`overflow:hidden` and accept a taller, variable-height email. Whichever is chosen, test with a realistic worst-case Markdown Quest Description (heading + list + blockquote + several paragraphs) in real Outlook and Gmail before shipping.

**Warning signs:**
Email templates verified only with a one-sentence placeholder string identical in shape to today's data; no test quest description used during manual verification contains an actual heading, list, or blockquote.

**Phase to address:**
Email-integration phase — this is a layout redesign decision that should be made explicitly (with the user, since it changes visual design) rather than discovered as a bug during human verification.

---

### Pitfall 4: Live client-side preview and final server-rendered HTML will disagree unless the SAME parser produces both

**What goes wrong:**
If the editor's "Preview" toggle uses a JavaScript Markdown library — the natural choice for instant, no-round-trip preview, e.g. EasyMDE, the most common toolbar+preview editor, which ships `marked.js` as its bundled renderer — while the server renders final HTML with Markdig (the standard CommonMark-compliant .NET library), the two will not agree on every input. `marked.js` fails roughly a quarter of the official CommonMark spec test suite (157 of 624 tests per the library's own maintainers), with documented gaps specifically in nested lists, blockquotes, and lazy paragraph continuation — exactly the constructs a D&D-flavored quest description (nested loot lists, blockquoted read-aloud boxes) is likely to use. A user could see a correctly nested list in live preview, save, and see it render differently (or flatten) on the actual Details page and in the email.

**Why it happens:**
Client-side Markdown editors are chosen for their toolbar UX (bold/italic/heading/list buttons, keyboard shortcuts), and their bundled preview renderer is treated as an implementation detail rather than a second parser that must be spec-matched against the server's.

**How to avoid:**
This app has no SPA/bundler — Bootstrap/jQuery/Cropper.js are all loaded from CDN in `_Layout.cshtml`, no npm build step exists — so the lowest-risk option is to skip a second JS parser entirely: wire the editor's "Preview" toggle to a small debounced AJAX call to a server endpoint running the exact same `IMarkdownRenderingService` used for final rendering, and swap the preview pane's `innerHTML` with the response. This guarantees byte-for-byte parity by construction instead of by testing. If a bundled JS parser is used anyway (e.g. for offline/instant preview), prefer a CommonMark-compliant engine (`markdown-it` passes 100% of the CommonMark spec suite, unlike `marked`) and add integration tests feeding a shared corpus of edge-case Markdown (nested lists, mixed HTML+Markdown, unusual whitespace, the D&D-prose gotchas in Pitfall 5) through both parsers, asserting equal output — this codebase has zero precedent for that kind of test (greenfield), so budget explicit time for it.

**Warning signs:**
Editor library chosen primarily because "it has a preview toggle," without checking which parser powers it; no test in the plan compares client preview output to server output for the same input; manual QA only exercises simple bold/italic/single-level-list cases (which virtually every parser agrees on) and never nested lists or blockquotes-containing-lists.

**Phase to address:**
Editor/toolbar phase — the preview mechanism choice (server round-trip vs. bundled JS parser) is an architectural decision that should be locked before toolbar buttons are wired, since it determines what JS dependency (if any) gets added.

---

### Pitfall 5: Existing plain-text data contains characters that are valid Markdown syntax today and will silently change MEANING, not just layout, beyond the already-accepted paragraph-reflow trade-off

**What goes wrong:**
The milestone has already accepted that single line breaks will reflow paragraphs. What hasn't been surfaced is that several characters casual D&D prose uses routinely are *also* CommonMark syntax with side effects beyond spacing:

- **Asterisks for old-fashioned emphasis or multiplication/dice notation.** CommonMark's delimiter-flanking rule explicitly allows `*` to open/close emphasis *mid-word* (unlike `_`, which cannot — `un_believ_able` stays literal but `un*believ*able` italicizes "believ"). A DM who wrote "roll 2*4 damage," "AC 15*2 vs the shield spell," or genuinely used IRC-style `*emphasis*` gets real, sometimes lopsided, italics — `5*10*20` becomes "5" + italic("10") + "20", silently consuming one asterisk and altering what reads as a multiplication expression.
- **A leading `#` on a line becomes a real heading.** ATX headings require `#` followed by a space, so "#3 on the priority list" (no space) stays literal — safe. But "# 3rd floor landing" or any casual "# " prefix used as an aside in old free-text notes becomes an oversized `<h1>`.
- **A line of `---`, `***`, or `___`** used as a visual section divider (plausible in Recap/Notes fields separating scenes) becomes a harmless `<hr>` — unless it directly follows a text line with no blank line between them, in which case CommonMark instead treats it as a **setext heading underline**, silently promoting the preceding prose line into a giant `<h1>`/`<h2>`.
- **Text pasted from Word/Google Docs with residual leading whitespace** (plausible for copy-pasted read-aloud boxes or stat blocks) that happens to carry 4+ leading spaces becomes an **indented code block** — rendered in monospace, and critically, Markdown inside it is no longer parsed at all, producing inconsistent formatting versus the rest of the same field.
- **Angle-bracket placeholders and stage directions** like `<insert PC name>` or `<gasps>` (plausible in freeform DM notes) are inline raw HTML by CommonMark's rules. Markdig either passes them through as literal (invisible, since browsers ignore unknown tags) or — once a sanitizer is added per Pitfall 1 — strips them outright, silently deleting that bracketed text from the visible render even though it's still present in the stored Markdown source.
- **A line starting with `-`/`*`/`+ ` immediately after a paragraph with no blank line** converts the rest of that paragraph into a real bulleted list — CommonMark's documented exception that lists (unlike every other block type) CAN interrupt a paragraph without a preceding blank line. A Rewards field like "The party gets:\n- 50 gold\n- a magic ring" (no blank line, exactly the old typing habit) actually renders as intended bullets — a rare case where the "accidental Markdown" outcome is desirable. But this is inconsistent with nearby numbered-list habits: CommonMark only lets an *ordered* list interrupt a paragraph when the first number is exactly `1`, so results are unpredictable per-entry rather than one simple learnable rule.

**Why it happens:**
The team correctly identified and accepted the headline trade-off (paragraph reflow), but that was scoped to spacing/layout. Character-level reinterpretation is a distinct failure mode — it changes what the text *says*, and is much harder to spot in a quick re-read of an old entry, because the corrupted output still looks like plausible prose (a reader won't necessarily notice a missing asterisk or an unexpectedly bold/italic word).

**How to avoid:**
Before rollout, run every existing value of all 9 fields through the real rendering pipeline in a one-off script (not a UI flow) and diff the rendered *text content* (tags stripped) against the original plain text, flagging entries where the stripped text differs by more than whitespace — this isolates asterisk-consumption, angle-bracket-swallowing, and code-block whitespace cases specifically, distinct from the already-accepted harmless paragraph-boundary reflow. Extend the "reflow until re-edited" communication (e.g. a one-time in-app notice: "descriptions written before [date] may display differently — click Edit to review") explicitly to this class of issue rather than letting users discover silently altered old quest rewards/recaps on their own.

**Warning signs:**
The diff script above is skipped because "we already decided reflow is fine"; a user reports post-launch that an old Recap's dice-roll notation or gold-reward list "looks wrong" and nobody connects it to the Markdown migration.

**Phase to address:**
Field-migration phase, per field group — run the diff-and-flag step once per field type as that field's display switches over, not as one big-bang pass across all 9 at once, so any surprises are attributable to a specific field/phase.

---

### Pitfall 6: Leftover `white-space: pre-wrap` from Phase 64 will double-space or misrender real Markdown-generated HTML if not removed

**What goes wrong:**
Phase 64 added `white-space: pre-wrap` across at least 13 files covering several of these exact 9 fields (`character-detail.mobile.css`, `quests.css`'s `.quest-description-box`, Shop's inline styles on desktop+mobile, the shared quest-card `.card-text` in `site.css`, plus multiple Contacts/Characters/DungeonMaster Edit/Create/Details views) specifically to stop single line breaks from collapsing in *plain-text* rendering. Once these containers hold real block-level HTML (`<p>`, `<ul><li>`, `<blockquote>`) instead of a raw text node, `pre-wrap` no longer does anything useful and can actively hurt: it preserves *all* whitespace verbatim, including newlines Markdig commonly emits between sibling block elements in its HTML source (for readability of the HTML output) — newlines normally collapsed by default browser whitespace handling between block tags become visible extra vertical gaps once `pre-wrap` forces them to render, stacking on top of each element's own CSS margin. The visual symptom looks like a Markdown-renderer bug but is actually a leftover CSS rule predating this milestone.

**Why it happens:**
`pre-wrap` is a container-level style, and field-migration work will likely focus on the *view template* (swapping `@Field` for `@Html.Raw(RenderedField)`) without necessarily touching the CSS file governing that container, since the two live in different files typically edited by different tasks.

**How to avoid:**
When each field's view is migrated to render HTML, explicitly audit and remove (or override) `white-space: pre-wrap` on that field's specific *display* container as a required companion edit in the same task, not a follow-up. The files identified via a repo-wide `pre-wrap` search: `site.css`, `quests.css`, `quest-log-detail.mobile.css`, `character-detail.mobile.css`, `quests.mobile.css`, `dm-profile.mobile.css`, the 3 `_Layout*.cshtml` files (verify whether this is a global textarea/`pre` rule before touching), `DungeonMaster/EditProfile(.Mobile).cshtml`, `Contacts/Edit(.Mobile).cshtml`, `Contacts/Create(.Mobile).cshtml`, `Characters/Edit(.Mobile).cshtml`, `Characters/Create(.Mobile).cshtml`, and the Details-page instances (`Shop/Details.Mobile.cshtml`, `_ShopItemDetailsContent.cshtml`, `Quest/Details.cshtml`, `Contacts/Details(.Mobile).cshtml`, `Characters/Details.cshtml`). Note that some of these are on the *editor's textarea* itself (Edit/Create views) — those should legitimately KEEP `pre-wrap` since a raw-Markdown-source textarea still benefits from visible line breaks while typing; only the *rendered-output* containers need the rule removed.

**Warning signs:**
Post-migration visual QA shows "too much space" between list items or paragraphs that doesn't match the app's normal typography rhythm; the gap appears on some fields but not others, because pre-wrap removal was done ad hoc rather than systematically against the file list above.

**Phase to address:**
Field-migration phase, one CSS audit per field as its view is converted — cross-reference against the file list above rather than rediscovering it from scratch.

---

### Pitfall 7: Rendering Contact Notes (or any future multi-entry aggregate view) at the wrong granularity lets one broken note's formatting bleed into another's

**What goes wrong:**
`ContactNoteEntity.Text` (2000-char cap) stores one row per note, authored independently by potentially different group members at different times; `Contacts/Details` renders them as a list of separate entries. As long as each note's Markdown is parsed and rendered via its own independent `ToSafeHtml()` call, an unclosed marker in one note (e.g. `**bold text` with no closing `**`) safely self-terminates within that note's own render — CommonMark parsers don't error on unmatched delimiters, they fall back to literal text — and cannot affect a different note's formatting. The risk is a *future* feature (a "print/export this Contact" view, a digest email, anything that concatenates multiple notes' raw Markdown source into one string before calling the renderer once) built without preserving that per-note boundary: at that point, one note's unclosed code fence or unterminated blockquote genuinely can swallow and reformat every note that follows it in the concatenated blob, misattributing formatting across different authors' independent, timestamped entries.

**Why it happens:**
Concatenating strings before rendering "for convenience" (one `ToSafeHtml()` call instead of N) looks like a pure simplicity/performance win once the rendering service exists, with the cross-contamination risk invisible until a specific pathological note triggers it.

**How to avoid:**
When building the Contact Notes rendering path, render each `ContactNoteEntity.Text` through the shared rendering service independently, one call per note, never concatenated. Document the constraint (a code comment or a small unit test asserting per-note rendering equals what a naive concatenate-then-render-once approach would NOT produce) so a future feature doesn't silently regress it.

**Warning signs:**
A future PR introduces a `string.Join(...)` or `StringBuilder` accumulation of multiple notes' raw `Text` before a single call into the Markdown renderer.

**Phase to address:**
Field-migration phase covering Contact Notes specifically — state the per-note rendering boundary explicitly in that phase's plan rather than leaving it an implicit assumption.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Reuse Markdig's raw sanitized HTML output directly in emails without a dedicated email-styling pass | Ships email support fast, one code path for browser + email | Emails look broken in Outlook/Gmail (missing bullets, inconsistent spacing) — a user-visible regression versus today's reliably plain-text emails | Never — email HTML needs its own inline-styled render path (Pitfall 2) |
| Skip a client/server parser-parity test corpus, rely on manual click-testing of the preview toggle | Faster to ship the editor/preview phase | Silent preview-vs-final-render mismatches surface as user-reported bugs post-launch; no regression net if the JS or Markdig library is upgraded later | Only acceptable if the server-round-trip preview approach (Pitfall 4) is used, which removes the need for parity tests entirely |
| Sanitize at write-time and store already-sanitized HTML, instead of storing raw Markdown and sanitizing at read-time | Simpler read path, no re-render cost per view | Loses the original Markdown source (can't re-render if sanitizer rules improve, can't re-diff old data later); a HtmlSanitizer version bump can't retroactively fix already-stored HTML | Never for this milestone — store raw Markdown, sanitize at render time, so future sanitizer improvements apply automatically to all historical data |
| Leave Phase 64's `white-space: pre-wrap` in place "just in case" instead of auditing container-by-container | Avoids touching CSS during the field-migration phase | Doubled/inconsistent spacing bugs (Pitfall 6) that look like renderer bugs and cost more time to diagnose later than to prevent | Never — the audit is cheap; do it as part of each field's migration task |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|-----------------|-------------------|
| Outlook desktop (Word rendering engine) | Assuming `<ul>`/`<li>` renders with visible bullets like a browser | Add `mso-special-format:bullet` MSO-conditional CSS or a manual inline-styled bullet fallback; verify visually in real Outlook, not a browser preview |
| Gmail webmail | Assuming a `<style>` block in the email `<head>` (like `_EmailLayout.razor`'s current one) will style Markdown-generated tags | Gmail strips `<style>` blocks in many contexts; every Markdown-generated tag needs inline `style=` styling, matching the convention already used elsewhere in `_EmailLayout.razor` |
| CDN-loaded JS Markdown editor (no npm/bundler in this app — Bootstrap/jQuery/Cropper.js v2.1.1 are all CDN-pinned per Phase 46) | Pinning the editor/parser script to `@latest` or an unversioned CDN URL | Pin an exact version in the CDN URL, mirroring how Cropper.js was pinned, and note the version explicitly in a code comment so a future upgrade is a deliberate, tested decision, not silent drift |
| HtmlSanitizer / Markdig NuGet packages | Building the sanitizer allowlist ad hoc, per field | One shared allowlist configuration (tags: `p, ul, ol, li, blockquote, h1-h6, strong, em, a, code, pre, br, hr`; attributes: `href`/`title` on `a` only, scheme-restricted to http/https/mailto) reused by the single rendering service from Pitfall 1 |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Rebuilding a `MarkdownPipeline` on every render call instead of once, shared | Slight per-request CPU/GC overhead building the pipeline's extension graph on every Description/Note render | Build the `MarkdownPipeline` once (singleton or static readonly field) — Markdig's own docs state the pipeline is thread-safe and meant to be shared, not rebuilt per call | Negligible at this app's scale (17 members, ~50 quests/year); worth doing correctly anyway since it's zero extra effort at initial implementation |
| Rendering full Markdown HTML on every read of a list view (quest board card previews, Contact Notes list) instead of only where full formatting is shown | Repeated sanitize+render cost on pages showing many truncated previews | For truncated/preview contexts (quest board cards), consider a plain-text-stripped excerpt instead of full rendered HTML, reserving full rendering for Details pages | Only matters if list views grow large; at ~50 quests this is unmeasurable, but avoids unnecessary work on every board-list request |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Sanitizing only Contact Notes because it's "the collaborative one," treating DM-authored fields (Quest Description, Rewards, Recap, DM Bio) as lower-risk | Every one of the 9 fields is rendered to other group members' browsers; a compromised or copy-paste-tricked DM/Admin account is just as capable of injecting `<img onerror>` as any player | Apply the same sanitizing rendering service to all 9 fields uniformly — no field gets a "trusted author" exemption |
| Allowing `javascript:`/`data:` URI schemes through in Markdown-authored links/images | `[click](javascript:alert(document.cookie))` executes in-browser on click if the sanitizer's `href` allowlist doesn't validate URI schemes | Explicitly configure the sanitizer's allowed URI schemes (http/https/mailto only); most sanitizer libraries default-block `javascript:` but verify rather than assume |
| Treating sanitization as optional for the email path because "email clients don't run JavaScript" | Even without script execution, un-sanitized HTML in an email can carry tracking-pixel `<img>` tags or malformed markup some clients mishandle — and the SAME rendered fragment is typically reused for the in-app browser view, where XSS very much does execute | Sanitize once, centrally, and feed identical sanitized HTML into both the browser view and the email template — never weaken sanitization because "it's just an email" |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|--------------|-------------------|
| Editor gives no visual cue that a single Enter no longer starts a new paragraph (new strict-CommonMark *authoring* behavior going forward, distinct from the accepted historical-data reflow trade-off) | Non-technical players hit Enter once out of habit — every other textarea in the app treats one Enter as a new line — and are confused when their next sentence visually runs into the previous one on save | Toolbar/editor should auto-insert a blank line on Enter, or show a subtle inline hint ("Press Enter twice for a new paragraph") near the textarea |
| Toolbar designed for one desktop width, then simply shrunk for `.Mobile.cshtml` | Given this project's repeated mobile-parity misses (Phase 43/54/64 all required after-the-fact fixes for mobile-specific gaps), a cramped or overflowing Bold/Italic/Heading/List/Preview toolbar on mobile is a near-certain repeat of that pattern | Design the mobile toolbar layout as its own explicit task (icon-only buttons, horizontal scroll, or a collapsed "more" menu) rather than assuming the desktop toolbar's CSS reflows acceptably |
| Preview toggle shows fully-rendered HTML while the textarea still shows raw `**`/`#`/`-` syntax with no in-context guidance | First-time Markdown authors (this group has never used Markdown syntax in these fields before) don't know what triggers what until they experiment and get surprised | Toolbar buttons should insert the correct syntax around the current selection on click (not just act as a static cheat-sheet), so most authoring never requires knowing raw syntax at all |

## "Looks Done But Isn't" Checklist

- [ ] **Sanitization coverage:** Often verified only for Contact Notes (the "obviously risky" field) — verify all 9 fields route through the same sanitizing rendering service, including DM Profile Bio and Quest Recap
- [ ] **Email visual QA:** Often verified only via a local `dotnet run` preview or opening the raw HTML file in a browser — verify by opening a real sent test email in actual Outlook desktop and Gmail webmail with Markdown-structured (not single-paragraph) test content
- [ ] **Mobile toolbar parity:** Often the desktop toolbar ships and mobile is assumed "close enough" — verify the toolbar is usable (not overflowing/cramped) on an actual narrow mobile viewport for all 9 fields' Create/Edit forms, both `.cshtml` and `.Mobile.cshtml`
- [ ] **Pre-wrap CSS removal:** Often the view template is migrated to render HTML but the companion CSS file is left untouched — verify `white-space: pre-wrap` was removed from every rendered-*output* container (not the editor textareas, which should keep it) across the files listed in Pitfall 6
- [ ] **Old-data corruption sweep:** Often "reflow is expected" is used to wave off all rendering differences — verify a diff pass distinguished harmless paragraph-boundary reflow from actual content-altering cases (dropped asterisks, swallowed angle-bracket text, unintended headings) per Pitfall 5
- [ ] **Client/server parser parity:** Often assumed correct because "the preview looked right during manual testing" — verify either a server-round-trip preview is used (no second parser exists) or an explicit edge-case test corpus (nested lists, blockquotes, mixed whitespace) was run through both parsers with an equality assertion

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|-----------------|------------------|
| XSS gap discovered post-launch (Pitfall 1) | HIGH | Rotate session/auth tokens if exploitation is suspected; since sanitization happens at render time from stored raw Markdown (not at write time), a single fix to the shared rendering service retroactively protects all historical data — no data migration needed |
| Email looks broken in Outlook/Gmail post-launch (Pitfall 2/3) | MEDIUM | Fix is contained to the 3 Razor email components plus the email-specific styling pass; no schema/data changes needed, redeploy is sufficient; consider a temporary rollback to plain-text `@QuestDescription` interpolation in emails only (keeping Markdown rendering live in the browser app) while the email-safe styling is reworked |
| Old entry discovered with silently altered meaning (Pitfall 5) | LOW | Single-row data fix — the original plain text is still fully recoverable from the stored Markdown source (nothing was deleted, only mis-rendered); the DM/owner re-edits that one entry; escalate to a small one-off review script only if the diff-sweep reveals many affected rows |
| Client/server preview mismatch reported by users (Pitfall 4) | MEDIUM | If using a bundled JS parser, either swap to the server-round-trip preview approach (removes the mismatch class entirely, no ongoing parser-parity maintenance) or add the specific failing edge case to the shared test corpus and pin/patch the JS parser's config to match |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| 1: Auto-encoding removal reopens XSS | Rendering-service phase | Unit tests feed `<script>`, `<img onerror>`, `javascript:` links through the service, asserting sanitized output; code review confirms no view/controller calls Markdig directly, only the shared service |
| 2: Email HTML not email-client-safe | Email-integration phase | Manual verification in real Outlook desktop + Gmail webmail with Markdown-structured (not single-paragraph) test content |
| 3: Fixed-height email card overflow | Email-integration phase | Manual verification with a worst-case Markdown Quest Description (heading + list + blockquote) in real Outlook |
| 4: Client/server preview mismatch | Editor/toolbar phase | Either the architectural choice of server-round-trip preview (no test needed) or a parity test corpus comparing JS-parser and Markdig output on nested lists/blockquotes/mixed whitespace |
| 5: Old-data character-level corruption | Field-migration phase (per field) | A one-off diff script run against a production data snapshot before each field's display switches over, flagging stripped-text differences beyond whitespace |
| 6: Leftover pre-wrap CSS | Field-migration phase (per field) | Visual QA checklist item cross-referencing the Phase 64 file list; confirm rendered-output containers (not editor textareas) have pre-wrap removed |
| 7: Contact Notes rendering-boundary bleed | Field-migration phase (Contact Notes specifically) | Unit test asserting per-note independent rendering is used, not batch-concatenate-then-render-once; code review of the Notes rendering call site |

## Sources

- CommonMark Spec 0.31.2 — https://spec.commonmark.org/0.31.2/ (ATX headings, setext headings, indented code blocks, thematic breaks) — HIGH confidence, official spec
- CommonMark Discuss, intraword emphasis rule — https://talk.commonmark.org/t/single-asterisks-in-subsequent-words-should-not-lead-to-emphasis/1035 — MEDIUM-HIGH, corroborates spec-documented delimiter-run rules
- Markdig (xoofx/markdig) — https://github.com/xoofx/markdig — HIGH, official repo/docs; confirms plain-CommonMark default pipeline, `UseAdvancedExtensions()` opt-in, `nofollow`/`noopener`/`noreferrer` link extensions, shared/thread-safe pipeline guidance
- Rick Strahl, "Markdown and Cross Site Scripting" — https://weblog.west-wind.com/posts/2018/Aug/31/Markdown-and-Cross-Site-Scripting — MEDIUM, single blog source, corroborated by HtmlSanitizer's own positioning as the standard pairing
- mganss/HtmlSanitizer — https://github.com/mganss/HtmlSanitizer — HIGH, official repo of the standard .NET HTML sanitizer library
- marked.js CommonMark compliance discussion — https://github.com/markedjs/marked/discussions/1202 — MEDIUM-HIGH, maintainer-acknowledged compliance gap (157/624 failing spec tests)
- Ionaru/easy-markdown-editor (EasyMDE) — https://github.com/ionaru/easy-markdown-editor — MEDIUM-HIGH, confirms marked.js as the bundled preview renderer
- Email client CSS/list-support community sources (GetResponse, Litmus community discussion, others) — MEDIUM, general email-dev community knowledge, corroborated across multiple independent sources on Outlook's `<ul>`/`<li>` limitations
- Direct codebase inspection (this repository, commit at time of research): `QuestBoard.Service/Components/Emails/{QuestFinalized,SessionReminder,WaitlistPromoted,_EmailLayout}.razor`, `QuestBoard.Repository/Entities/{QuestEntity,CharacterEntity,ContactEntity,ContactNoteEntity,DungeonMasterProfileEntity,ShopItemEntity}.cs`, repo-wide `pre-wrap` grep (13 files), `.planning/PROJECT.md`, `.planning/codebase/STACK.md` — HIGH confidence, first-party source verification

---
*Pitfalls research for: Markdown authoring/rendering retrofit onto existing plain-text fields (D&D Quest Board v8.0)*
*Researched: 2026-07-09*
