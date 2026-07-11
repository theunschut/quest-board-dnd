# Phase 65: Markdown Rendering Foundation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-09
**Phase:** 65-markdown-rendering-foundation
**Areas discussed:** Markdown extension set, Image handling

---

## Markdown Extension Set

| Option | Description | Selected |
|--------|-------------|----------|
| Enable strikethrough now (Recommended) | One extension call, `del` already in the sanitizer allowlist; v2's toolbar button becomes pure front-end work later | ✓ |
| Leave strikethrough disabled for v1 | `~~text~~` renders literally until explicitly revisited | |

| Option | Description | Selected |
|--------|-------------|----------|
| Keep minimal — autolinks (+ strikethrough) only (Recommended) | Matches REQUIREMENTS.md's toolbar exclusions; unpromoted syntax renders as literal text | |
| Yes, enable other GFM extras (pipe tables, task lists, definition lists, footnotes) | Full parser support even without a toolbar button | ✓ |

**User's choice:** Full CommonMark + GFM parser support (autolinks, strikethrough, pipe tables, task lists, definition lists, footnotes) — deliberately broader than the milestone research's original "keep minimal" recommendation. Toolbar UI stays curated to the app's actual use case; the parser's capability and the toolbar's promoted feature set are treated as two separate, independent decisions.

**Notes:** User asked a clarifying question first — "will unpromoted syntax still render if hand-typed even without a toolbar button?" — confirming that parser-level extension enablement, not toolbar presence, is what actually controls whether syntax renders live. This clarification directly led to the "enable everything, curate the toolbar separately" decision. Claude flagged a critical security constraint during this discussion: the broader extension set must be composed via individual Markdig extension methods, NOT the blanket `.UseAdvancedExtensions()` bundle, since that bundle pulls in `UseGenericAttributes()` — an attribute-injection XSS vector unrelated to the extensions actually wanted. This constraint is recorded as D-04 in CONTEXT.md.

---

## Image Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, allow real images too | `![alt](url)` renders a live `<img>` everywhere, including the 3 emails | |
| No, strip image syntax specifically | Renders as literal text everywhere — the one exception to full support | |

**User's choice (round 1):** Asked clarifying questions before choosing — (a) does this conflict with the existing Cropper.js upload pipeline? (b) do emails actually display Quest Description at all? (c) can images be excluded from emails specifically while still allowed elsewhere?

**Claude's answers:** (a) No conflict — Cropper.js stores bytes in the DB for structured portrait fields; Markdown images reference external URLs inside free-text fields, an orthogonal mechanism. (b) Corrected a mistaken assumption — yes, emails do display Quest Description; that's EMAILMD-01, a core v8.0 requirement. (c) Yes, architecturally clean — two sanitizer profiles (web allows `<img>`, email strips it) sharing one Markdig parser satisfies RENDER-02's "single shared pipeline" requirement, since sanitization is a policy step after parsing, not duplicated parsing logic.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, web allows images / email strips them (Recommended) | Full support where safe; email-specific risk (tracking pixels, hotlinking, clients block external images by default anyway) scoped narrowly | ✓ |
| No, allow images everywhere including email | True full support, no exceptions | |
| No, strip images everywhere including the web app | Simplest/safest, reverts the "full support" decision for this one syntax element | |

**User's choice (round 2):** Web app allows real image rendering; the email-rendering path uses a stricter sanitizer profile that strips `<img>` tags specifically. This split becomes a useful seam for Phase 71 (Email-Safety Hardening), which already needs its own email-specific rendering treatment.

---

## Claude's Discretion

- Exact service/interface naming (research used slightly different names across STACK.md/ARCHITECTURE.md/PITFALLS.md — not user-facing)
- Exact `HtmlSanitizer` `AllowedTags`/`AllowedAttributes` configuration needed to support the expanded extension set (tables, definition lists, footnotes, the narrow task-list checkbox `<input>` allowance) — needs verified research into each Markdig extension's actual HTML output, not guesswork
- Whether to add a defensive input-size guard on the render service — not raised as a product decision, left to planner/researcher judgment

## Deferred Ideas

None — both discussed areas stayed within Phase 65's scope.
