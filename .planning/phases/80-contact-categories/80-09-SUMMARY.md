---
phase: 80-contact-categories
plan: 09
subsystem: ui
tags: [css, accessibility, wcag, mobile-detection, aspnet-core-mvc, integration-tests]

requires:
  - phase: 80-contact-categories
    provides: "Manage.Mobile.cshtml add-category form (80-05/80-06), the modern-card.css / contact-form.mobile.css glass-card theming system (80-01 through 80-08), and the zero-category helper-text link on Contacts Create/Edit (80-07)"
provides:
  - "Manage.Mobile.cshtml's add-category <form> carries the category-mgmt-add-form scoping class, and contacts.mobile.css themes its .form-label to the parchment token #F4E4BC"
  - "modern-card.css's .modern-card .form-text a rule and contact-form.mobile.css's .contact-form-card .form-text a rule theme the zero-category 'Manage Categories' helper link to #F4E4BC on both desktop and mobile Contacts Create/Edit"
  - "ContactCategoryContrastGuardTests -- six facts pinning both fixes structurally (rule exists with the right colour, selector actually reaches the target element, mobile file is genuinely selected) plus a regression guard on the two pre-existing scoped overrides (.modern-card .text-danger, .modern-card-header .header-subtitle)"
affects: [80-contact-categories]

tech-stack:
  added: []
  patterns:
    - "Scoping class on an existing element (category-mgmt-add-form) rather than an unscoped selector, when the element needing a card-scoped rule sits outside every card wrapper -- avoids both a bare !important selector and a layout-changing card wrap"
    - "Narrow .card-selector .form-text a rules rather than a broad .card-selector a, to avoid out-specifying .btn and .row-nav-link and repainting unrelated anchor-buttons/clickable rows"
    - "CSS guard facts extract a single rule body via ExtractCssRule(css, 'selector {') and assert case-insensitively against that scoped substring, so an assertion cannot be satisfied by an unrelated declaration elsewhere in the file"
    - "Behavioural fact locates a helper-text anchor by searching backward from the link's own index for its nearest form-text token, rather than assuming the file's first form-text occurrence is the relevant one -- both affected views also carry an unrelated form-text caption earlier in the DOM"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs
  modified:
    - QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/contacts.mobile.css
    - QuestBoard.Service/wwwroot/css/modern-card.css
    - QuestBoard.Service/wwwroot/css/contact-form.mobile.css

key-decisions:
  - "Gap 1 fixed via a scoping class (category-mgmt-add-form) on the existing <form>, not a card wrap or a bare .form-label rule -- zero visual/layout change, follows the file's own .category-mgmt-row/.category-mgmt-reorder-btn naming convention."
  - "Gap 2 fixed with .modern-card .form-text a / .contact-form-card .form-text a, never a broad .modern-card a -- the narrow selector avoids out-specifying .modern-card .btn (would repaint the Cancel button anchor on Create.Mobile.cshtml) and .row-nav-link (would break color:inherit on clickable rows elsewhere in the app)."
  - "Gap 2 fix landed in BOTH stylesheets deliberately: desktop Contacts Create/Edit wrap in .modern-card, mobile wraps in .contact-form-card and loads contact-form.mobile.css -- fixing only modern-card.css would have left the mobile half of the gap rendering Bootstrap link blue."
  - "Colour is cream #F4E4BC, not gold -- follows the existing .contact-detail-card a / .character-detail-card a precedent; 80-UI-SPEC.md reserves gold (#ffc107/#FFD700) for a closed list of elements that does not include this link."
  - "No text-decoration change on either new rule -- the underline is the link's only non-colour affordance now that it shares a hue family with its surrounding helper text; suppressing it would create a new WCAG 1.4.1 gap while fixing the 1.4.3 contrast gap."

requirements-completed: [CONTACTCAT-08, CONTACTCAT-15]

coverage:
  - id: D1
    description: "On /ContactCategoryManagement under a real mobile User-Agent, the 'New Category Name' label resolves to the parchment token #F4E4BC instead of Bootstrap's default rgb(33,37,41)"
    requirement: "CONTACTCAT-08"
    verification:
      - kind: integration
        ref: "ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_ManagementPageLabel_RendersInsideScopedFormOnMobileOnly -- 1 passed, 0 failed"
        status: pass
      - kind: integration
        ref: "ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_MobileAddFormLabelRule_SetsParchmentColourAndShadow -- 1 passed, 0 failed"
        status: pass
      - kind: manual_procedural
        ref: "Visual confirmation on a real handset that the label renders as cream, not near-black"
        status: unknown
    human_judgment: true
    rationale: "No browser-automation harness exists in this repo, and no server-side integration test can observe a computed CSS colour. The automated facts prove the scoped rule exists with the right colour and that its selector structurally reaches the label; the final rendered pixel is confirmable only by eye, per the plan's own <human-check> verification block."
  - id: D2
    description: "On Contacts Create and Edit, for a board with zero categories, the 'Manage Categories' helper-text link resolves to #F4E4BC on both desktop (.modern-card) and mobile (.contact-form-card), instead of Bootstrap's link blue rgb(13,110,253), and keeps its underline"
    requirement: "CONTACTCAT-15"
    verification:
      - kind: integration
        ref: "ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_ModernCardFormTextLinkRule_SetsParchmentColourWithUnderlineIntact -- 1 passed, 0 failed"
        status: pass
      - kind: integration
        ref: "ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_MobileFormTextLinkRule_SetsParchmentColourWithUnderlineIntact -- 1 passed, 0 failed"
        status: pass
      - kind: integration
        ref: "ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_ZeroCategoryHelperLink_RendersOnBothDesktopAndMobileCardSurfaces -- 1 passed, 0 failed"
        status: pass
      - kind: manual_procedural
        ref: "Visual confirmation on desktop and a real handset that the link renders cream and underlined, not blue"
        status: unknown
    human_judgment: true
    rationale: "No browser-automation harness exists in this repo, and no server-side integration test can observe a computed CSS colour. The automated facts prove both scoped rules exist with the right colour, keep the underline, and that the link structurally sits inside the form-text element both rules target on both platforms; the final rendered pixel is confirmable only by eye, per the plan's own <human-check> verification block."
  - id: D3
    description: "The two pre-existing scoped overrides shipped earlier in phase 80 -- .modern-card .text-danger (validation red #ff6b6b) and .modern-card-header .header-subtitle (#1a1a1a) -- still pass their regression fact"
    requirement: "CONTACTCAT-15"
    verification:
      - kind: integration
        ref: "ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_PreExistingScopedOverrides_StillPinValidationRedAndHeaderSubtitle -- 1 passed, 0 failed"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-08-31
status: complete
---

# Phase 80 Plan 09: UAT Gap Closure — Mobile Label Contrast and Zero-Category Link Colour Summary

**Scoped CSS fixes for two WCAG contrast defects UAT found post-merge: a mobile form label falling back to Bootstrap near-black outside any themed card, and a zero-category helper link falling back to Bootstrap link blue because both card stylesheets theme text by element enumeration and never included anchors.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-08-31T00:53:55+02:00
- **Completed:** 2026-08-31T01:05:08+02:00
- **Tasks:** 2
- **Files modified:** 4 (1 view, 3 stylesheets) + 1 new test file

## Accomplishments
- Mobile "New Category Name" label on `/ContactCategoryManagement` now resolves to the parchment token `#F4E4BC` instead of Bootstrap's default `rgb(33,37,41)`, via a non-visual scoping class (`category-mgmt-add-form`) on the existing add-category form and a matching scoped rule in `contacts.mobile.css`.
- The zero-category "Manage Categories" helper link on all four Contacts Create/Edit views (desktop + mobile) now resolves to `#F4E4BC` instead of Bootstrap's link blue `rgb(13,110,253)` (previously ~3.09:1 contrast, below WCAG AA), via narrow `.form-text a` rules added to both `modern-card.css` and `contact-form.mobile.css`, keeping the underline as the non-colour affordance.
- Six-fact `ContactCategoryContrastGuardTests` guard suite added: two structural facts for gap 1 (rendered-markup pairing under a real mobile User-Agent + a rule-scoped stylesheet assertion), three facts for gap 2 (both stylesheets' rules + a two-platform behavioural fact proving the link sits inside `form-text` on both `.modern-card` and `.contact-form-card`), and one explicit regression guard pinning the two scoped overrides phase 80 and phase 83 already shipped (`.modern-card .text-danger`, `.modern-card-header .header-subtitle`).

## Task Commits

Each task was committed atomically:

1. **Task 1: Scope the mobile add-category label to the parchment token and pin it with a guard suite** - `e5529ffc` (fix)
2. **Task 2: Theme the helper-text link on both card surfaces and guard the two scoped overrides already shipped** - `dc79de31` (fix)

**Plan metadata:** commit pending (this file + STATE.md/ROADMAP.md/REQUIREMENTS.md are owned by the orchestrator after the wave completes, per this plan's execution mode)

_Note: no TDD tasks in this plan; both tasks are single fix commits each combining view/CSS changes with their own guard facts._

## Files Created/Modified
- `QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml` - Add-category `<form>` gains the `category-mgmt-add-form` scoping class alongside its existing `mb-3`; no other markup change
- `QuestBoard.Service/wwwroot/css/contacts.mobile.css` - New `.category-mgmt-add-form .form-label` rule (cream `#F4E4BC` + drop shadow), placed beside the existing `.category-mgmt-row` block
- `QuestBoard.Service/wwwroot/css/modern-card.css` - New `.modern-card .form-text a` rule (cream, three-layer shadow, `font-weight: 600`), placed directly after the existing `.modern-card .form-text` rule; `.modern-card .text-danger` and `.modern-card-header .header-subtitle` untouched
- `QuestBoard.Service/wwwroot/css/contact-form.mobile.css` - New `.contact-form-card .form-text a` rule (cream, single-layer shadow matching this file's lighter convention), placed directly after the existing `.contact-form-card .form-text, .contact-form-card small` rule
- `QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs` - New test class with six facts and three private helpers (`GetMobileAsync`, `ResolveCssPath`, `ExtractCssRule`)

## Decisions Made
See `key-decisions` in frontmatter. In short: scoping class over unscoped rule or card wrap (gap 1); narrow `.form-text a` selector over a broad `.modern-card a` / `.contact-form-card a` (gap 2, to avoid repainting `.btn` and `.row-nav-link` anchors elsewhere in the app); the fix must land in both stylesheets because desktop and mobile Contacts Create/Edit use different card wrapper classes; cream `#F4E4BC` (not gold) matching the existing `.contact-detail-card a` / `.character-detail-card a` precedent; underline preserved as the link's non-colour affordance.

## Deviations from Plan

None - plan executed exactly as written. One adaptation worth recording as it affects how a future reader should trust the behavioural fact's exact mechanics: the plan's action text for the two-surface behavioural fact describes taking "the HTML slice from the `form-text` token to the next `</small>`" using the first `form-text` occurrence. Both Contacts Create views actually contain an earlier, unrelated `<small class="form-text">` element (the contact-image upload caption) before the category helper text. A literal first-occurrence implementation would have sliced the wrong `<small>` and produced a false pass. `AssertHelperLinkPresentInsideFormText` instead locates the "Manage Categories" link first, then searches backward for the nearest preceding `form-text` token and forward for the next `</small>`, which correctly identifies the category helper's own `<small>` element on both platforms regardless of how many other `form-text` elements precede it. This is the same intent the plan describes (proving the link sits inside its `form-text` wrapper) implemented robustly rather than literally; no functional gate or acceptance criterion was weakened.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Both UAT-reported gaps are closed and pinned by automated facts; `dotnet build` is clean (0 errors, only pre-existing NU1608 package-constraint warnings unrelated to this plan) and the full suite is green: 437 unit tests + 674 integration tests (668 pre-existing + 6 new guard facts), including the six-fact `ContactCategoryContrastGuardTests` class in isolation.

**Outstanding, by design:** the rendered-pixel confirmation for both gaps (does the label/link actually paint cream on a real device, not just resolve a scoped rule with the right hex value server-side) remains a human-verify item. This repository has no browser-automation harness, and a server-side integration test structurally cannot observe a computed CSS colour — the plan's own `<verification>` section calls this out as an honest limit of the automated coverage, not a gap in this plan's execution. Re-run the two `<human-check>` verification steps from the plan (load `/ContactCategoryManagement` and Contacts → Create on a real handset / genuine mobile User-Agent) during the next UAT pass to close this out visually.

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs
- FOUND: e5529ffc (fix(80-09): scope mobile add-category label to parchment token)
- FOUND: dc79de31 (fix(80-09): theme zero-category helper link on both card surfaces)
- FOUND: b9551ce6 (docs(80-09): create plan summary)

---
*Phase: 80-contact-categories*
*Completed: 2026-08-31*
