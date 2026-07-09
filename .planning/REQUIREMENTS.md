# Requirements: D&D Quest Board — v8.0 Markdown Support

**Defined:** 2026-07-09
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

## v1 Requirements

Requirements for the v8.0 milestone. Each maps to roadmap phases.

### Rendering Infrastructure

- [ ] **RENDER-01**: Markdown text in any of the 9 target fields is converted to safe, sanitized HTML for display — no raw HTML or script execution is possible regardless of what a user types
- [ ] **RENDER-02**: Page views and the 3 HTML email templates that echo Quest Description use the exact same rendering pipeline — no separate or duplicated rendering logic
- [ ] **RENDER-03**: A blank line (not a single Enter) is required to start a new paragraph (strict CommonMark) — a deliberate change from today's line-break-preserving plain-text display

### Editor UI

- [ ] **EDITOR-01**: Editing a free-text field shows a formatting toolbar (Bold, Italic, Heading, List, Link, Blockquote) that inserts Markdown syntax around the current selection or at the cursor
- [ ] **EDITOR-02**: A Preview button toggles the same input area between the raw-text editor and a rendered-HTML preview, without leaving the page
- [ ] **EDITOR-03**: While in Preview mode, the rest of the toolbar is disabled (not clickable-but-nonfunctional)
- [ ] **EDITOR-04**: What a user sees in Preview mode exactly matches how the content actually displays once saved — no formatting surprises after saving
- [ ] **EDITOR-05**: An inline hint near the editor explains that a blank line starts a new paragraph
- [ ] **EDITOR-06**: The toolbar and editor work identically on desktop and mobile, with icon-only buttons sized for touch (44px+) on mobile, fitting one row with no overflow/scroll mechanism

### Quest Fields

- [ ] **QUESTMD-01**: Quest Description supports the Markdown editor on Create/Edit/Follow-Up forms (desktop + mobile) and renders as formatted HTML wherever displayed (board card, Details, Manage)
- [ ] **QUESTMD-02**: Quest Rewards supports the Markdown editor and renders as formatted HTML on Details/QuestLog
- [ ] **QUESTMD-03**: Quest Recap (via the EditRecap form) supports the Markdown editor and renders as formatted HTML on Details/QuestLog

### Character Fields

- [ ] **CHARMD-01**: Character Description supports the Markdown editor and renders as formatted HTML on Character Details
- [ ] **CHARMD-02**: Character Backstory supports the Markdown editor and renders as formatted HTML on Character Details

### Contact Fields

- [ ] **CONTACTMD-01**: Contact Description supports the Markdown editor and renders as formatted HTML on Contact Details/Index
- [ ] **CONTACTMD-02**: Each Contact Note supports the Markdown editor and renders independently as formatted HTML — one author's formatting never bleeds into another note

### DM Profile & Shop

- [ ] **PROFILEMD-01**: DM Profile Bio supports the Markdown editor and renders as formatted HTML on the DM Profile page
- [ ] **PROFILEMD-02**: Shop Item Description supports the Markdown editor and renders as formatted HTML on Shop Index/Details/Manage

### Email Integration

- [ ] **EMAILMD-01**: Quest Description renders as formatted HTML (not raw Markdown syntax) in the Quest Finalized, Session Reminder, and Waitlist Promoted emails
- [ ] **EMAILMD-02**: A recipient viewing these 3 emails in real Outlook desktop or Gmail webmail sees correctly formatted content (visible bullets, intact styling) — not broken or missing formatting
- [ ] **EMAILMD-03**: A recipient can read the full quest description in these emails even when formatted with headings/lists/blockquotes — content is not silently clipped by a fixed-height card

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Editor UI

- **EDITOR-07**: Strikethrough toolbar button — only worth adding if the chosen renderer has GFM strikethrough enabled at no extra wiring cost
- **EDITOR-08**: Horizontal rule toolbar button
- **EDITOR-09**: Markdown cheatsheet link/popover — add only if users report confusion beyond the inline paragraph-break hint

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Inline code / code block toolbar button | No dice-notation or code use case anywhere in these 9 fields |
| Image embed button (`![alt](url)`) | Duplicates the existing Cropper.js structured photo-upload pipeline; would introduce unmoderated external-image loading |
| Table button / GFM table promotion | Painful to hand-edit as pipe-delimited text in a bare mobile textarea; none of the 9 fields are long-form documents |
| @Mention / #Ref toolbar buttons | No analog in this app's data model; would require new notification infrastructure |
| Task list / interactive checkboxes | Interactive checkboxes have no equivalent in static HTML email — a real architectural mismatch, not just added complexity |
| Side-by-side / split-pane preview | Contradicts the locked single-toggle-in-place design; confirmed wrong for mobile by both EasyMDE's own `no-mobile` class and Discourse's UX team |
| Fullscreen editing mode | None of the 9 fields are long-form documents |
| TradeItem Description/Notes | Entity exists but is not wired into any controller or view — dead code, out of scope entirely |
| UserTransaction.Notes | Property exists but is never populated anywhere in the codebase — dead code, out of scope entirely |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| RENDER-01 | Phase 65 | Pending |
| RENDER-02 | Phase 65 | Pending |
| RENDER-03 | Phase 65 | Pending |
| EDITOR-01 | Phase 66 | Pending |
| EDITOR-02 | Phase 66 | Pending |
| EDITOR-03 | Phase 66 | Pending |
| EDITOR-04 | Phase 66 | Pending |
| EDITOR-05 | Phase 66 | Pending |
| EDITOR-06 | Phase 66 | Pending |
| QUESTMD-01 | Phase 66 | Pending |
| QUESTMD-02 | Phase 67 | Pending |
| QUESTMD-03 | Phase 67 | Pending |
| CHARMD-01 | Phase 68 | Pending |
| CHARMD-02 | Phase 68 | Pending |
| CONTACTMD-01 | Phase 69 | Pending |
| CONTACTMD-02 | Phase 69 | Pending |
| PROFILEMD-01 | Phase 70 | Pending |
| PROFILEMD-02 | Phase 70 | Pending |
| EMAILMD-01 | Phase 67 | Pending |
| EMAILMD-02 | Phase 71 | Pending |
| EMAILMD-03 | Phase 71 | Pending |

**Coverage:**
- v1 requirements: 21 total
- Mapped to phases: 21/21 ✓
- Unmapped: 0

---
*Requirements defined: 2026-07-09*
*Last updated: 2026-07-09 after roadmap creation (v8.0, Phases 65–71)*
