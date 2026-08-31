---
status: complete
phase: 80-contact-categories
source: [80-VERIFICATION.md]
started: 2026-08-30T12:57:24Z
updated: 2026-08-31T06:02:37Z
---

## Current Test

[testing complete]

## Tests

### 1. Live database collation and case-differing duplicate category name

expected: Collation query reports a case-insensitive (`*_CI_*`) collation, and the case-differing duplicate is refused rather than stored.
result: pass
verified_by: orchestrator, against the live SQL Server instance and the running app

evidence: |
  - `DATABASEPROPERTYEX('QuestBoard','Collation')` = `SQL_Latin1_General_CP1_CI_AS` (case-insensitive).
    `SERVERPROPERTY('Collation')` = `Latin1_General_CI_AS`.
  - `IX_ContactCategories_GroupId_Name` exists, `is_unique = 1`, and its `Name` column carries
    collation `SQL_Latin1_General_CP1_CI_AS` -- the assumption is real, not merely inherited by luck.
  - Direct DB probe inside a transaction: inserting "ZZTest Guild Members" then "zztest guild members"
    on the same board raised error 2601 ("Cannot insert duplicate key row ... with unique index
    IX_ContactCategories_GroupId_Name"). Rolled back; 0 rows persisted.
  - Through the UI: created "Last Bastion", then submitting "last bastion" was refused with
    "A category with that name already exists. Please choose a different name." No second row created.
  This closes the one behavior-unverified item from 80-VERIFICATION.md (CONTACTCAT-04).

### 2. Real-handset mobile pass over the category surfaces

expected: On a real device layout is usable, tap targets are adequate, and a long category name does not break the heading.
result: pass
retested: |
  Fixed by gap-closure plan 80-09 (commits e5529ffc, dc79de31), merged and post-merge gate green
  (437 unit + 674 integration, 0 failures). Re-verified live in-browser on TWO separate app launches
  (2026-08-31): under a real mobile User-Agent (Pixel 8 / Android 14) on /ContactCategoryManagement,
  the "New Category Name" label now resolves to rgb(244,228,188) with a text-shadow -- was
  rgb(33,37,41), no shadow. Confirmed both by computed-style read and by screenshot.
partial_coverage: |
  Exercised with a real mobile User-Agent (Pixel 8 / Android 14), which is how this project selects
  `.Mobile.cshtml` views -- confirmed active via `mobile-layout` body class and 2 mobile stylesheets.
  Grouping, headings, category cards, pluralised contact counts ("0 contacts" / "1 contact"), boundary
  arrow states and the Details category line all render correctly on mobile.
  Physical-device pass CONFIRMED BY USER 2026-08-31 ("physical device seems fine") -- tap targets and
  long-category-name wrapping are acceptable on a real handset. No residual coverage gap on this test;
  the recorded issue below is the label-legibility defect only.

### 3. First-run discovery on a board with zero categories

expected: The category select on Contacts -> Create is disabled with helper text linking to Manage Categories, and reads as an obvious invitation. The index shows no headings at all.
result: pass
retested: |
  Fixed by gap-closure plan 80-09 (commits e5529ffc, dc79de31) on BOTH surfaces the fix required --
  desktop `.modern-card .form-text a` and mobile `.contact-form-card .form-text a` (the original
  report understated scope: mobile Create/Edit use a different card class than desktop and needed
  its own rule). Re-verified live on two separate app launches (2026-08-31) on a genuinely
  zero-category board ("The Boundless Domain"): the "Manage Categories" link now resolves to
  rgb(244,228,188) on both desktop and mobile, ~11.04:1 contrast (was ~3.09:1), underline preserved.
  Regression-checked: the unrelated Cancel anchor-button on the same card still resolves
  rgb(255,255,255) -- the fix did not repaint anchors it should not have.
verified_correct: |
  Tested on a genuinely zero-category board ("The Boundless Domain", 17 contacts, 0 categories):
  - Contacts index renders a completely flat list with NO headings at all, not even "Ungrouped" --
    the pre-phase-80 rendering is preserved exactly for a board that never adopted the feature.
  - Create form select is `disabled`, sole option "— None —", helper text reads
    "No categories yet. Manage Categories to create one." with an anchor to /ContactCategoryManagement.
  The wording does read as an invitation; only the link's contrast undercuts it.

## Summary

total: 3
passed: 3
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "Mobile Manage Categories form labels are legible against the dark background"
  status: resolved
  reason: "User-visible: the 'New Category Name' label renders rgb(33,37,41) (Bootstrap default) on dark wood, effectively unreadable."
  severity: minor
  test: 2
  root_cause: "Every mobile form label in this project gets its cream colour from a CARD-SCOPED rule (.character-form-card .form-label, .contact-form-card .form-label, .account-card-mobile .form-label, etc.). Manage.Mobile.cshtml places the Add form and its label at lines 8-10, OUTSIDE any card wrapper -- the first card (.contact-section-card) does not open until line 18. contacts.mobile.css defines no .form-label rule at all, so nothing matches and Bootstrap's default #212529 wins."
  artifacts:
    - path: "QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml"
      issue: "Add-category form and its .form-label sit outside any *-card wrapper (lines 8-16)"
    - path: "QuestBoard.Service/wwwroot/css/contacts.mobile.css"
      issue: "No .form-label rule, unlike every other mobile form stylesheet in the project"
  missing:
    - "Either wrap the Add form in a card whose stylesheet themes .form-label, or add a scoped .form-label rule to contacts.mobile.css matching the cream used by .contact-form-card .form-label"
  debug_session: ""
  resolved_by: "80-09 (commits e5529ffc, dc79de31) -- re-verified live in-browser 2026-08-31"

- truth: "The zero-category helper link reads as an obvious invitation to create the first category"
  status: resolved
  reason: "User-visible: the 'Manage Categories' anchor renders Bootstrap link-blue rgb(13,110,253) inside a .modern-card on a dark background, ~3.09:1 contrast, below WCAG AA 4.5:1."
  severity: minor
  test: 3
  root_cause: "modern-card.css themes text by enumerating elements (.modern-card p, li, span, small) and has no rule for anchors other than .modern-card .table .email-link. The helper <small class='form-text text-muted'> is themed cream, but the <a> nested inside it is not matched by that enumeration and falls back to Bootstrap's default link colour. The four Contacts Create/Edit views are the ONLY views in the project that place a link inside form-text helper text, so phase 80 is the first code to hit this hole."
  artifacts:
    - path: "QuestBoard.Service/wwwroot/css/modern-card.css"
      issue: "No .modern-card a rule; element-enumeration theming leaves anchors unstyled"
    - path: "QuestBoard.Service/Views/Contacts/Create.cshtml"
      issue: "Helper-text anchor inherits Bootstrap link blue (same in Create.Mobile, Edit, Edit.Mobile)"
  missing:
    - "Add a scoped .modern-card .form-text a rule (or .modern-card a) using an on-theme accent that clears 4.5:1 against the card background"
  debug_session: ""
  resolved_by: "80-09 (commits e5529ffc, dc79de31) -- re-verified live in-browser 2026-08-31"
