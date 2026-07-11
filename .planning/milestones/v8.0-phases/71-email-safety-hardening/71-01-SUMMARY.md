---
phase: 71-email-safety-hardening
plan: 01
subsystem: email
tags: [markdig, anglesharp, htmlsanitizer, email-html, outlook, dotnet]

# Dependency graph
requires:
  - phase: 65-markdown-rendering-foundation
    provides: MarkdownService with RenderToHtml/EmailSanitizer split by MarkdownRenderTarget
  - phase: 67-remaining-quest-fields-email-templates
    provides: all 3 quest email templates wired to RenderToHtml(..., MarkdownRenderTarget.Email)
provides:
  - "IMarkdownService.RenderEmailHtml(markdown, readMoreUrl, maxTopLevelBlocks, maxPlainTextChars) — new public API"
  - "Email-safe inline styling for every Markdig-emittable block/inline element (h1-h6, p, ul/ol/li, blockquote, a, strong, em, hr)"
  - "Outlook MSO bullet-visibility fallback comment on every <li>"
  - "Block-boundary-aware truncation with a read-more link, tunable via maxTopLevelBlocks/maxPlainTextChars"
affects: [71-02-quest-email-template-wiring, 71-03-live-outlook-gmail-verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Post-sanitization AngleSharp DOM pass for injecting compile-time-constant styles (never touches EmailSanitizer.AllowedAttributes)"
    - "Block-boundary truncation walk over document.Body.Children, distinct from ExtractPlainText's flat-text truncation"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/IMarkdownService.cs
    - QuestBoard.Domain/Services/MarkdownService.cs
    - QuestBoard.UnitTests/Services/MarkdownServiceTests.cs

key-decisions:
  - "Styles injected via IElement.SetAttribute() strictly after EmailSanitizer.Sanitize() has run, never via EmailSanitizer.AllowedAttributes — avoids the Ganss.Xss/AngleSharp.Css whole-attribute-drop defect and keeps WebSanitizer (used by every other Markdown field) untouched"
  - "Truncation and margin corrections apply uniformly to whichever block ends up first/last, regardless of tag — matches the UI-SPEC's Spacing Scale exceptions, which name no tag-based carve-out"

patterns-established:
  - "New IMarkdownService methods should follow RenderEmailHtml's shape: reuse RenderToHtml/EmailSanitizer for sanitization, then a private AngleSharp DOM pass for target-specific post-processing"

requirements-completed: [EMAILMD-02, EMAILMD-03]

coverage:
  - id: D1
    description: "Every Markdig-emittable block/inline element gets its exact UI-SPEC inline style (h1-h6 3-tier heading scale, p, ul/ol, li disc/decimal variants, blockquote, a, strong, em, hr), out-of-scope tags (table/code/del/sup/dl) stay unstyled"
    requirement: "EMAILMD-02"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/MarkdownServiceTests.cs#RenderEmailHtml_Heading1And2_UseLargeStyle, RenderEmailHtml_Heading3And4_UseMediumStyle, RenderEmailHtml_Heading5And6_UseSmallStyle, RenderEmailHtml_AllHeadingLevels_AreBoldAndNeverItalic, RenderEmailHtml_Paragraph_UsesBodyStyle, RenderEmailHtml_UnorderedList_StylesListAndBulletItems, RenderEmailHtml_OrderedList_UsesDecimalListItemStyle, RenderEmailHtml_Blockquote_UsesGoldLeftBorder, RenderEmailHtml_Link_UsesBrownColorNotGold, RenderEmailHtml_StrongAndEmphasis_HaveExplicitStyles, RenderEmailHtml_ThematicBreak_UsesGoldTopBorder, RenderEmailHtml_OutOfScopeTags_ReceiveNoInjectedStyle"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every <li> carries the MSO conditional-comment bullet fallback; the existing EmailSanitizer/RenderToHtml(..., Email) path is unchanged (no style= attribute, still strips <img>)"
    requirement: "EMAILMD-02"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/MarkdownServiceTests.cs#RenderEmailHtml_ListItems_HaveMsoBulletFallbackComment, RenderToHtml_EmailTarget_StillEmitsNoStyleAttribute, RenderEmailHtml_Image_StripsImgTag, RenderEmailHtml_NullOrWhitespace_ReturnsEmpty"
        status: pass
    human_judgment: false
  - id: D3
    description: "Content within the block/char budget passes through untouched (no read-more link); content over budget is cut at a complete top-level block boundary (never mid-element) and gets the exact read-more link/copy appended, with first/last-block margin corrections applied"
    requirement: "EMAILMD-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/MarkdownServiceTests.cs#RenderEmailHtml_UnderBothBudgets_ReturnsUntruncatedWithNoReadMoreLink, RenderEmailHtml_OverBlockBudget_KeepsAtMostMaxBlocksAndAppendsReadMore, RenderEmailHtml_OverCharBudget_TruncatesAtBlockBoundaryBeforeExceedingBudget, RenderEmailHtml_TruncationNeverCutsMidElement, RenderEmailHtml_Truncated_ReadMoreLinkHasExactCopyAndHref, RenderEmailHtml_FirstKeptBlock_HasMarginTopZero, RenderEmailHtml_LastRenderedElement_HasMarginBottomZero"
        status: pass
    human_judgment: false
  - id: D4
    description: "Real Outlook desktop and Gmail webmail actually render the styled/truncated output correctly (visible bullets, intact styling, working read-more link) — the live D-05 verification this engine exists to support"
    verification: []
    human_judgment: true
    rationale: "Outlook's Word rendering engine and Gmail webmail cannot be simulated by any unit test, browser preview, or headless tool available to this project — this is explicitly Plan 71-03's blocking human-verification checkpoint (D-05), not this plan's scope."

duration: 20min
completed: 2026-07-10
status: complete
---

# Phase 71 Plan 01: Email-Safe Markdown Rendering Engine Summary

**New `IMarkdownService.RenderEmailHtml` method: post-sanitization AngleSharp DOM pass injecting verbatim UI-SPEC inline styles + Outlook MSO bullet fallback, plus block-boundary-aware truncation with a read-more link — closes both EMAILMD-02/03 gaps for the 3 quest email templates.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-07-10T21:37:59+02:00 (base commit)
- **Completed:** 2026-07-10T21:57:13+02:00
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- `RenderEmailHtml(markdown, readMoreUrl, maxTopLevelBlocks = 5, maxPlainTextChars = 650)` added to `IMarkdownService`/`MarkdownService`, reusing the existing `EmailSanitizer` pipeline unchanged
- Every Markdig-emittable block/inline element (3-tier heading scale, paragraphs, ordered/unordered lists with disc/decimal `<li>` variants, blockquotes, links, strong/em, `hr`) gets its exact UI-SPEC inline `style=` attribute, injected via `IElement.SetAttribute()` strictly after sanitization — never through `EmailSanitizer.AllowedAttributes`
- Every `<li>` gets the MSO conditional-comment bullet fallback (`<!--[if mso]>&#8226;&nbsp;<![endif]-->`) as a belt-and-suspenders Outlook fix
- Content exceeding the block/char budget is truncated at a complete top-level block boundary (never mid-element) and gets the exact `…continue reading on the quest board` read-more link appended, pointing at the caller-supplied `readMoreUrl`
- First/last rendered element margin corrections (`margin-top:0`/`margin-bottom:0`) applied per the Spacing Scale contract
- 25 new `RenderEmailHtml_*`/`RenderToHtml_EmailTarget_StillEmitsNoStyleAttribute` test cases added; full suite (299 unit + 396 integration tests) green

## Task Commits

Each task was committed atomically, following RED→GREEN TDD:

1. **Task 1: Add RenderEmailHtml with email-safe styling + Outlook bullet fallback** - `50351b6e` (test) — added interface signature, style-table implementation, MSO fallback, plain serialize; 19 new tests
2. **Task 2: Block-boundary truncation + read-more link inside RenderEmailHtml** - `21c7f812` (feat) — replaced plain serialize with the budget-aware truncation walk, read-more paragraph builder, margin corrections; 7 new tests

_Task 1's commit bundles the interface, implementation, and its own RED test cases in a single commit (verified RED via a failed build before implementing, then GREEN before committing) rather than two separate commits — Task 2 follows the stricter test-then-feat two-commit split._

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IMarkdownService.cs` - `RenderEmailHtml` signature + XML doc documenting the null/empty guard, the hard-coded-styles security invariant, and truncation behavior
- `QuestBoard.Domain/Services/MarkdownService.cs` - `EmailBlockStyles` table, li/MSO-comment handling, `RenderEmailHtml`, `TruncateAtBlockBoundary`, `BuildReadMoreParagraph`
- `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs` - 25 new `RenderEmailHtml_*` and regression test cases

## Decisions Made
- Styles are injected via `IElement.SetAttribute()` after `EmailSanitizer.Sanitize()` has already run, per the plan's mechanism decision (71-RESEARCH.md Pitfall 1 / 71-PATTERNS.md "Sanitizer separation" supersede 71-UI-SPEC.md line 137's "add style to AllowedAttributes" note) — `EmailSanitizer`/`WebSanitizer`/`AllowedAttributes` are byte-for-byte unchanged.
- Margin corrections (`margin-top:0`/`margin-bottom:0`) apply to whichever block ends up first/last in the kept set, with no tag-based exception — matches the UI-SPEC's Spacing Scale wording exactly, which does not carve out out-of-scope tags (e.g. a `<table>` or `<dl>` landing first/last still gets the correction).
- On a pathological-nesting fallback (Markdig's own nesting-depth guard trips, producing bare HTML-encoded text with no wrapping element), `RenderEmailHtml` passes the already-sanitized text straight through rather than losing it, mirroring `ExtractPlainText`'s existing fallback philosophy — not explicitly required by the plan's behavior list but consistent with the file's established safety-net convention.

## Deviations from Plan

None - plan executed exactly as written. Task 1's action text ("Serialize and return the styled body's inner HTML (Task 2 replaces the plain serialize with the truncation-aware serialize)") was followed literally: Task 1's commit contains only the plain serialize, Task 2's commit swaps it for the truncation-aware serialize.

## Issues Encountered

One test-authoring mistake caught and fixed during implementation (not a source-code bug): the first draft of `RenderEmailHtml_OutOfScopeTags_ReceiveNoInjectedStyle` asserted `<table>` never carries a `style=` attribute, but the margin-top:0 correction legitimately lands on whatever the first kept block is, tag notwithstanding — the test's markdown was restructured so table/dl aren't the first/last block, isolating the "no typography style injected" assertion from the separate margin-correction concern. No source change was needed.

## Next Phase Readiness
- `IMarkdownService.RenderEmailHtml` is ready for Plan 02 to wire into the 3 Razor email components (`QuestFinalized.razor`, `SessionReminder.razor`, `WaitlistPromoted.razor`), replacing the existing `RenderToHtml(QuestDescription, MarkdownRenderTarget.Email)` call site and removing the inner `overflow-y:auto` scroll wrapper.
- Truncation budgets (`maxTopLevelBlocks = 5`, `maxPlainTextChars = 650`) are exposed as tunable parameters, ready for Plan 03's live D-05 Outlook/Gmail verification to adjust without a structural code change.
- No blockers.

---
*Phase: 71-email-safety-hardening*
*Completed: 2026-07-10*

## Self-Check: PASSED

- FOUND: `QuestBoard.Domain/Interfaces/IMarkdownService.cs`
- FOUND: `QuestBoard.Domain/Services/MarkdownService.cs`
- FOUND: `QuestBoard.UnitTests/Services/MarkdownServiceTests.cs`
- FOUND: `.planning/phases/71-email-safety-hardening/71-01-SUMMARY.md`
- FOUND commit: `50351b6e` (test)
- FOUND commit: `21c7f812` (feat)
