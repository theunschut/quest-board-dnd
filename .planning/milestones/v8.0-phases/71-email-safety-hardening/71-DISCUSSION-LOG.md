# Phase 71: Email-Safety Hardening - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-10
**Phase:** 71-email-safety-hardening
**Areas discussed:** Fixed-height card & clipping, Real-client verification method

---

## Scope Assessment

Before presenting gray areas, Claude scouted `Components/Emails/QuestFinalized.razor`, `SessionReminder.razor`, `WaitlistPromoted.razor`, `QuestBoard.Domain/Services/MarkdownService.cs`, and `Controllers/Admin/EmailPreviewController.cs`, plus `.planning/research/SUMMARY.md`'s Pitfalls 2/3. Found that the `EmailSanitizer` strips all inline `style` attributes from Markdig's output (zero styling on headings/lists/blockquotes today), and that all 3 templates share an identical `height:840px;overflow:hidden` card with a scrollable inner div that Outlook's Word rendering engine does not honor — the exact clipping bug this phase exists to fix. All 3 templates also already have an identical "View Quest Details" CTA button.

## Fixed-height card & clipping

**Round 1 — initial framing:**

| Option | Description | Selected |
|--------|-------------|----------|
| Remove the fixed height entirely | Card grows to fit content; zero clipping risk | |
| Truncate with a link to read more | Keep the fixed frame; cap content, rely on existing button | |

**User's choice (Other, free text):** "have the description be a scrollable field... Similar to the quest index quest items" — proposed reintroducing/keeping scrolling.

**Claude's response:** Explained scrolling is the *current* implementation and Outlook's Word rendering engine does not support CSS overflow/scroll on divs at all — it silently clips with no scrollbar. This is exactly the bug the phase exists to fix, so a scroll-based fix can't satisfy "works in both Outlook and Gmail."

**Round 2 — re-presented with the scrolling option removed:**

| Option | Description | Selected |
|--------|-------------|----------|
| Remove the fixed height entirely (recommended) | Card grows to fit content | |
| Truncate with a link to read more | Keep the fixed frame, cap + reuse existing button | |

**User's choice (Other, free text):** "The fixed height was added to stop the background image from growing and break the actual quest page. So the fixed height needs to stay. So truncate with a link to read more should be the answer."

**Notes:** This is D-01/D-02 — the fixed height is load-bearing for the poster background image, not just aesthetic preference. Truncate-with-link locked as the only viable path.

**Follow-up — truncation UX:**

| Option | Description | Selected |
|--------|-------------|----------|
| Add explicit "...read more" copy (recommended) | Makes truncation look intentional | ✓ |
| Silent cutoff, rely on existing button | Simpler, no new copy | |

**Follow-up — truncation formatting style:**

| Option | Description | Selected |
|--------|-------------|----------|
| Preserve formatting, cut at a block boundary (recommended) | Real HTML up to a safe cutoff point | ✓ |
| Plain-text fallback (simpler) | Reuse ExtractPlainText()-based truncation from Phase 66/70 | |

**Notes:** User confirmed preserving formatting — deliberately diverges from the `ExtractPlainText()` precedent used elsewhere in this milestone, since the whole point of this phase is that structured content should display correctly, including in the truncated case.

---

## Real-client verification method

| Option | Description | Selected |
|--------|-------------|----------|
| Send a real test email to your own inbox (recommended) | Trigger real sends via existing Resend/Postfix relay to a real inbox, operator opens in real clients | ✓ |
| Something else | Different approach | |

**Follow-up — client access:**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, both available | | |
| Only one of them / need to arrange | | ✓ |

**Follow-up — test content setup:**

| Option | Description | Selected |
|--------|-------------|----------|
| Claude creates a throwaway test quest (recommended) | Claude sets up quest + triggers all 3 job dispatches | ✓ |
| You'll set up the test quest yourself | | |

**Notes:** Operator does not currently have both Outlook desktop and Gmail webmail readily available simultaneously — flagged as a known constraint for the verification checkpoint (D-05/D-06) rather than resolved in discussion. The plan's human-verify task should surface this rather than assume simultaneous access.

---

## Claude's Discretion

- Exact CSS/inline-style values for email headings/lists/blockquotes (colors, sizes, MSO bullet-fix technique) — deferred to this phase's UI-SPEC via `/gsd-ui-phase`, matching every prior field-migration phase in this milestone.
- Exact truncation length/threshold.
- Exact mechanism for creating/cleaning up the throwaway test quest and triggering each of the 3 email job dispatches.
- Whether to reuse/extend `EmailPreviewController` as an interim dev-loop preview tool (not a substitute for the real send-test-email verification).

## Deferred Ideas

None — no scope creep surfaced during this discussion.
