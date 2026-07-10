# Phase 71: Email-Safety Hardening - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Make Markdown-structured Quest Description content (headings, lists, blockquotes) actually display correctly and completely in the 3 quest email templates (`QuestFinalized.razor`, `SessionReminder.razor`, `WaitlistPromoted.razor`) when opened in real Outlook desktop and Gmail webmail — not just in a browser preview. EMAILMD-02, EMAILMD-03 only. Quest Description is the only Markdown-bearing field that ever reaches an email (confirmed across Phases 67–70: Rewards/Recap/Character/Contact/DM-Bio/Shop-Description all have "no email surface" as a locked, carried-forward decision) — no other templates are in scope.

Two concrete, pre-existing technical gaps this phase closes:
1. **No email-safe styling on structured content.** `MarkdownService`'s `EmailSanitizer` (`QuestBoard.Domain/Services/MarkdownService.cs`) does not include `style` in its `AllowedAttributes` set, so every `<h1>-<h6>`, `<ul>/<ol>/<li>`, and `<blockquote>` Markdig emits comes out with **zero inline styling** once sanitized. Gmail strips `<style>` blocks (this app's own email convention already inlines every style for exactly that reason), and Outlook's Word rendering engine typically shows no bullets at all for unstyled `<ul>/<li>`.
2. **Clipping risk from the fixed-height poster card.** All 3 templates wrap Description in an identical `<td style="height:840px;overflow:hidden;">` with an inner `<div style="height:100%;overflow-y:auto;">` — a scroll trick that **Outlook desktop's Word rendering engine does not support at all** (no CSS overflow/scroll on divs), so long structured content is silently and unrecoverably clipped in Outlook specifically. This is NOT a browser-preview-detectable bug — it only manifests in the real Outlook client.

</domain>

<decisions>
## Implementation Decisions

### Locked at milestone-research level (not re-discussed, carried forward)
- **Scope is exactly 3 templates, exactly Quest Description.** No other field, no other email template. Confirmed via direct code read — `IQuestEmailDispatcher`/`QuestService` are the only dispatch paths that pass `QuestDescription` into a Razor email component.
- **Inline styles are the only viable email-CSS approach** — `<style>` blocks are stripped by Gmail; this app's existing email convention (see `_EmailLayout.razor` and all 8 email components) already inlines every style for this exact reason. Any new heading/list/blockquote styling for Markdown output must follow the same inline-`style=` convention, matching `_EmailLayout.razor`'s existing Georgia-serif/gold-parchment palette.
- **MSO-specific bullet-visibility fixes (e.g. conditional-comment fallback markup for `<ul>/<li>` in Outlook) are an implementation detail, not a user decision** — research names this pitfall but the exact technique (inline `list-style` + explicit unit `display:list-item`, vs. MSO conditional-comment fallback markup) is Claude's Discretion / research territory.

### Fixed-height card & clipping (the phase's central decision)
- **D-01:** The `height:840px;overflow:hidden` fixed poster-frame card is **not removed** — it stays exactly as-is. Rationale (from the user, not an engineering default): the fixed height exists specifically to prevent the poster background image from growing/breaking on the actual quest board page's equivalent layout; making the email card's height dynamic was explicitly rejected.
- **D-02:** Instead, Description is **truncated with a "read more" link** when it exceeds the space available inside the fixed-height card. All 3 templates already have an identical "View Quest Details" `<a href="@QuestUrl">` button in a later row (Row 6) — the truncation messaging should point at that existing button; no new link/button needs to be added.
- **D-03:** The truncated portion **preserves Markdown formatting** rather than falling back to plain text. Truncation must cut at a complete block-element boundary (never mid-list-item, mid-heading, or mid-blockquote) — the truncated HTML must remain valid, well-formed markup up to the cut point. This is a genuinely new truncation mechanism, distinct from the `ExtractPlainText()`-based character truncation used for the Shop/Quest board card teasers elsewhere in this milestone (Phase 66 D-06, Phase 70 D-01/D-02) — those strip to flat text; this one must preserve real HTML structure up to the cutoff.
- **D-04:** When truncated, show explicit "...read more" copy (e.g. "...continue reading on the quest board") at the cut-off point — do NOT rely on a silent cutoff. Makes truncation look intentional, not like a rendering bug.
- **Explicitly rejected:** the previously-existing `overflow-y:auto` scrollable-div approach (today's actual code) does not satisfy the requirement — confirmed with the user that Outlook's Word engine does not support scrollable divs at all, so "make it scroll" cannot be the fix.

### Real-client verification method
- **D-05:** Verification happens by sending a **real test email** (via the existing Resend/Postfix relay — no new send infrastructure) to the operator's own inbox for each of the 3 templates, triggered against a throwaway test quest with a Markdown-structured Description (headings + list + blockquote, long enough to exceed the truncation threshold and exercise D-01 through D-04). The operator opens each email in their actual Gmail webmail and actual Outlook desktop client and reports what they see. This is a live, blocking human-verification checkpoint — not resolvable by browser preview or automated screenshot tooling.
- **D-06:** Claude creates the throwaway test quest (and any signups/finalization/waitlist state needed to legitimately trigger each of the 3 job dispatches) rather than asking the operator to set it up manually.
- **Known constraint (not resolved here — surface at the checkpoint):** the operator currently has ready access to only one of Outlook desktop / Gmail webmail, not both, and may need to arrange access to the other before the live verification checkpoint can fully close both EMAILMD-02 acceptance criteria (Outlook AND Gmail). Planning should not assume both are trivially available at checkpoint time — the plan's human-verify task should surface this rather than silently assume simultaneous access.

### Claude's Discretion
- Exact CSS/inline-style values for headings, lists, and blockquotes inside the email context (font sizes, colors, bullet-marker styling, MSO conditional-comment technique vs. CSS-only fallback for Outlook bullets) — this phase has "UI hint: yes" in ROADMAP.md and will get its own UI-SPEC via `/gsd-ui-phase`, matching every prior field-migration phase in this milestone (66–70). Discuss-phase intentionally did not re-litigate typography/color choices here.
- Exact truncation length/threshold (character count or rendered-height estimate) for D-02/D-03 — no user preference expressed; pick a value that reliably fits the 840px frame given the existing Row 1/2/4/5/6 content around it.
- Exact mechanism for creating/cleaning up the throwaway test quest and triggering each of the 3 job dispatches (`QuestFinalizedEmailJob`, `SessionReminderJob`/`DailyReminderJob`, `QuestWaitlistPromotedEmailJob`) for D-05/D-06 — implementation detail for planning/research, not a user-facing decision.
- Whether to reuse/extend the existing `EmailPreviewController` (Admin-only, browser-render-only, does not currently have a `WaitlistPromoted` preview action) as an interim dev-loop tool before the real send-test-email verification — useful but not a substitute for D-05, since research is explicit that browser preview alone does not surface the Outlook-specific overflow/bullet bugs this phase exists to fix.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone Research (primary source for this phase's technical grounding)
- `.planning/research/SUMMARY.md` lines 59-60 — Pitfall 2 (Outlook `<ul>/<li>` bullet visibility, Gmail `<style>`-stripping) and Pitfall 3 (the 840px fixed-height card clipping risk, framed explicitly as "a visual-design decision... that needs explicit user input, not an engineering default") — this phase's two central technical facts
- `.planning/research/SUMMARY.md` line 97 — the originally-envisioned phase deliverable: "Inline `style=` attributes on every block element Markdig can produce..., MSO conditional-comment bullet fix for lists, a resolved fixed-height-card design decision..., verified by actually opening sent test emails in real Outlook desktop and Gmail webmail... — not just a browser preview"
- `.planning/research/SUMMARY.md` line 110 — "Email-safety phase: the fixed-height card redesign is a genuine design decision — flag for discuss-phase or a live checkpoint with the user, not resolved by an engineer alone" (the decision this discussion's D-01/D-02 resolves)

### Phase 65 (Markdown Rendering Foundation — owns the sanitizer this phase must extend)
- `.planning/phases/65-markdown-rendering-foundation/65-CONTEXT.md` — the two-sanitizer-profile design (`WebSanitizer` allows `<img>`, `EmailSanitizer` doesn't); this phase adds `style` to `EmailSanitizer.AllowedAttributes` (or an equivalent scoped mechanism) without reopening Phase 65's XSS-hardening decisions

### Phase 66 (established the `<div>`-not-`<p>` email wrapper fix and the plain-text-teaser precedent D-03/D-05 explicitly do NOT reuse)
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-CONTEXT.md` D-06 — `ExtractPlainText()`-based plain-text truncation; this phase's D-03 deliberately diverges from that precedent (preserves HTML structure instead) and downstream agents must not default back to `ExtractPlainText()` for the email truncation case
- `.planning/phases/66-quest-description-editor-rendering-proof-of-concept/66-07-SUMMARY.md` — the `<p>`-cannot-contain-block-elements email fix this phase's new truncation/styling work must not regress

### Phase 67 (wired all 3 target templates to render Description as HTML — this phase's direct dependency per ROADMAP.md)
- `.planning/phases/67-remaining-quest-fields-email-templates/67-CONTEXT.md` — confirms `SessionReminder.razor`/`WaitlistPromoted.razor` were brought in line with `QuestFinalized.razor`'s `<div>` + `RenderToHtml(..., MarkdownRenderTarget.Email)` pattern in that phase; this phase extends that pattern, does not replace it

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — EMAILMD-02, EMAILMD-03 (this phase's requirements); EMAILMD-01 (Phase 67, already complete) is the prerequisite this phase builds on
- `.planning/ROADMAP.md` — Phase 71 goal, success criteria (verbatim source of the "truncate-with-link or remove the fixed height" framing D-01/D-02 resolve), dependency on Phase 67

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Domain/Services/MarkdownService.cs` — `EmailSanitizer` (line 77) is the exact object whose `AllowedAttributes` needs `style` added; `BaseAllowedTags` (line 34) already includes all the block tags (`h1-h6`, `ul/ol/li`, `blockquote`) that need styling, so no new tags need allowlisting — only the `style` attribute is missing.
- `Components/Emails/_EmailLayout.razor` — the existing Georgia-serif/gold-parchment inline-style convention every other element in these templates already follows; new heading/list/blockquote styles should match this palette, not invent a new one.
- All 3 templates' existing `<a href="@QuestUrl">View Quest Details</a>` button (`QuestFinalized.razor:82`, `SessionReminder.razor:83`, `WaitlistPromoted.razor:82`) — identical markup/styling across all 3, exactly what D-02's "read more" messaging should point at. No new button needed.
- `Controllers/Admin/EmailPreviewController.cs` — Admin-only, browser-render-only email template previewer with hardcoded sample data. Has `QuestFinalized()` and `SessionReminder()` preview actions but **no `WaitlistPromoted()` action** (a pre-existing gap, not part of this phase's required scope, but worth noting if the executor wants a quick dev-loop preview before the real send-test-email verification). Does not itself send email — `Content(html, "text/html")` returns rendered HTML directly to the browser.
- `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` + `QuestBoard.Domain/Services/QuestService.cs` — the actual dispatch path for all 3 target emails; `Jobs/QuestFinalizedEmailJob.cs`, `Jobs/SessionReminderJob.cs` (or `Jobs/DailyReminderJob.cs`), `Jobs/QuestWaitlistPromotedEmailJob.cs` are the Hangfire job classes that actually trigger sends — relevant for D-06's test-quest setup.

### Established Patterns
- All 8 email components share `_EmailLayout.razor` and the same fully-inlined-style convention (no `<style>` blocks anywhere) — this phase extends that convention to Markdig's block-level output, it doesn't introduce a new styling mechanism.
- `MarkdownRenderTarget.Email` already exists as a distinct enum value/sanitizer path from `MarkdownRenderTarget.Web` — this phase modifies only the Email path; the Web path (used everywhere else in this milestone) must not regress.

### Integration Points
- **Sanitizer:** `QuestBoard.Domain/Services/MarkdownService.cs` — `EmailSanitizer.AllowedAttributes` needs `style` (or a scoped equivalent) added.
- **Styling application:** needs a mechanism to inject inline `style=` onto Markdig's raw block-level HTML output for the Email target specifically — Markdig itself does not add inline styles; this is new logic, not present anywhere in the codebase today (Claude's Discretion for exact approach: post-process via AngleSharp DOM manipulation vs. regex vs. a Markdig extension hook).
- **Truncation:** `Components/Emails/QuestFinalized.razor:43`, `SessionReminder.razor:44`, `WaitlistPromoted.razor:43` — the `@((MarkupString)MarkdownService.RenderToHtml(QuestDescription, MarkdownRenderTarget.Email))` call sites where D-02/D-03/D-04's truncation-with-read-more-link logic needs to apply, identically across all 3 templates.
- **Fixed-height card:** `<td style="height:840px;overflow:hidden;">` (line 8 in all 3 templates) and the inner `<div style="height:100%;overflow-y:auto;padding-right:6px;">` (line ~42-43) — per D-01, the outer fixed height stays; the inner scrollable-div wrapper should likely be removed/simplified since truncation replaces the need for scrolling (Claude's Discretion on exact restructuring).

</code_context>

<specifics>
## Specific Ideas

The user directly corrected two premises during discussion, both load-bearing for planning:
1. Proposed making the description scrollable inside the email — corrected with the concrete technical fact that Outlook's Word rendering engine does not support CSS overflow/scroll on divs at all, which is why today's actual `overflow-y:auto` implementation is the bug this phase exists to fix, not a viable solution.
2. When offered "remove the fixed height" as the recommended option, explained the fixed height is load-bearing for the poster background image (would grow/break otherwise) and must stay — locking D-01/D-02 (truncate-with-link) as the only real path, contrary to the initial recommendation.

</specifics>

<deferred>
## Deferred Ideas

None — no scope creep surfaced during this discussion.

### Reviewed Todos (not folded)
None — no pending todos existed for this phase (`todo.match-phase 71` returned 0 matches).

</deferred>

---

*Phase: 71-email-safety-hardening*
*Context gathered: 2026-07-10*
