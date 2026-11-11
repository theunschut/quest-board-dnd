---
phase: 95-quests-and-quest-log-ui-conversion
plan: 16
subsystem: ui-markdown
tags: [markdown, editor, easymde, cascade-layers, vendor-layer, per-view-stylesheets, contrast, foreign-pages]
requires:
  - phase: 95-03
    provides: Gate 1 answers for items 10 (per-view loading), 11 (toolbar out of the Tab order) and 14 (EasyMDE into layer(vendor))
  - phase: 95-15
    provides: per-User-Agent readability compare, allow-file chain, sweep tooling
provides:
  - easymde-vendor.css, a one-line wrapper that imports EasyMDE's own stylesheet into layer(vendor)
  - markdown-editor.css as one @layer pages block on tokens only (paper writing surface with ink text, alternate-paper toolbar, control border, two-tone focus ring on the whole editor)
  - markdown-content.css as one @layer pages block on tokens only, with no important flag and no colour literal
  - both sheets out of both layouts' global lists: the editor sheets travel with the editor through the shared editor partial, the content sheet is linked by every view that renders Markdown output
  - guard facts that pin the wrapper path, the vendor-layer import, where each sheet loads, and a search that finds every view rendering Markdown output
affects: [closure plan: the layouts no longer link either Markdown sheet; Phase 98 owns the Characters, Contacts and DM profile pages]
tech-stack:
  added: []
  patterns: [third-party stylesheet imported into layer(vendor) through a wrapper so a layered page sheet beats it without important, :not(:where(.EasyMDEContainer *)) to exclude the editor from an unlayered catch-all without changing its specificity, per-view stylesheet link with a guard that searches the views]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/easymde-vendor.css
  modified:
    - QuestBoard.Service/wwwroot/css/markdown-editor.css
    - QuestBoard.Service/wwwroot/css/markdown-content.css
    - QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/character-form.mobile.css
    - QuestBoard.Service/wwwroot/css/contact-form.mobile.css
    - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
    - QuestBoard.Service/wwwroot/css/contacts.css
    - QuestBoard.Service/wwwroot/css/dm-editprofile.mobile.css
    - QuestBoard.Service/wwwroot/css/shop-management-create.mobile.css
    - QuestBoard.Service/wwwroot/css/shop-management-edit.mobile.css
    - QuestBoard.IntegrationTests/Tests/UiFoundationViewLibraryGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
key-decisions:
  - "Gate 1 item 14 = (a) taken: EasyMDE's stylesheet is imported into layer(vendor) by easymde-vendor.css; the lib script tag stays; the rewritten guard pins the wrapper path and its vendor-layer import."
  - "Gate 1 item 10 = per-view taken: markdown-editor.css (with the wrapper) is linked by _QuestFormScripts, which every one of the 23 editor-hosting views renders; markdown-content.css is linked through @section Styles by each view that renders Markdown output. Both entries left both layouts in the same commits."
  - "Gate 1 item 11 = accept: the toolbar stays out of the Tab order. markdown-editor.js is untouched; the Tab probe passes on all 40 editor captures."
  - "Markdown headings take --qb-fg-heading on every surface. The old rule that switched headings to dark ink inside a card body assumed a light card; every card is now glass with light text, so that rule made headings unreadable on Quest Details, Contacts, DM profile, Events, Series and the Shop. It is removed, not tokenised."
requirements-completed: [D-12, D-13, D-24, DoC-2, DoC-3, DoC-5, DoC-8, DoC-10]
duration: ~2.5 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 5800
  tasks: 2
  commits: 2
---

# Phase 95 Plan 16: Markdown stylesheets converted and loaded where they are used Summary

The editor sheet and the content sheet are both `@layer pages` sheets on tokens only; EasyMDE's own stylesheet sits in the vendor layer through a one-line wrapper; neither sheet is in a layout any more, and every page that renders an editor or Markdown output, in this area or another, still reads and still lets Tab leave the editor.

## What changed

**Task 1 (`750fcfa5`), the editor sheet (Gate 1 items 14 and 10).**
- `easymde-vendor.css` holds a why-comment and `@import url("../lib/easymde-2.21.0/easymde.min.css") layer(vendor);`. `_QuestFormScripts.cshtml` links the wrapper and then `markdown-editor.css` in place of the lib stylesheet link; the lib script tag, the antiforgery meta and the two scripts are unchanged.
- `markdown-editor.css` is one `@layer pages` block, no important flag, no colour literal, no tripled selector: the editing area and the live preview on `--qb-surface-paper` with `--qb-ink` text and no text shadow, the toolbar on `--qb-surface-paper-alt` with ink icons, borders on `--qb-control-border`, the placeholder on `--qb-ink-muted` at full opacity (half-opacity ink does not hold 4.5:1 on paper), the preview's links in ink and underlined, and the two-tone focus ring on `.EasyMDEContainer:focus-within` (computed outline 2px ink with a 2px gold box-shadow; checked in a rendered crop, not clipped). Toolbar buttons keep `var(--qb-hit-min)` and `padding: 0` as before.
- Both layouts stop linking `markdown-editor.css`.
- The three foreign catch-alls the plan names are narrowed with `:not(:where(.EasyMDEContainer *))`. The `:where()` keeps their specificity exactly as it was, which matters: the Tagify override in `contact-form.mobile.css` outranks the catch-all by specificity, and a plain `:not(.EasyMDEContainer *)` would have raised the catch-all above it.
- Guards: `QuestFormPartial_LoadsEditorFromLib` rewritten to the same intent (the partial links the wrapper, no longer links the lib stylesheet directly, still links the lib script; the wrapper imports the versioned lib stylesheet into `layer(vendor)`; no CDN host in either file; the lib file exists); new fact `EditorPartial_LinksTheEditorSheets_AndNoLayoutDoes`; `markdown-editor.css` joins `ConvertedSheets`; the wrapper and both sheets join the planning-reference scan list. No fact deleted.

**Task 2 (`a67c7c56`), the content sheet (Gate 1 item 10).**
- `markdown-content.css` is one `@layer pages` block on tokens only with a why-comment. Selectors kept. Headings use `--qb-fg-heading` and `--qb-text-halo`; the quote bar is `color-mix` over `--qb-accent`; font weights use the two-weight tokens.
- 17 views link it from `@section Styles` (the link first, so a page sheet linked after it still wins). Both layouts stop linking it.
- New fact `MarkdownOutputViews_LinkTheContentSheet_AndNoLayoutDoes` searches every non-partial view for `Html.Markdown(`, the shop item details partial, or a details modal load, requires at least 15 hosts (17 found), and requires each to link the sheet; `markdown-content.css` joins `ConvertedSheets` and the scan list.

## Views that render Markdown or host the editor: difference from the plan

The plan's list was checked with a repository search (`Html.Markdown`, `_ShopItemDetailsContent`, `_MarkdownEditor`, `_QuestFormScripts`), not trusted.
- Gone from the list because earlier plans merged or deleted them: `Quest/Edit.Mobile`, `Quest/CreateFollowUp.Mobile`, `Move/Index.Mobile`, `QuestLog/Details.Mobile` (not on the plan's file list either; nothing to link).
- **Added: `Shop/Index.cshtml` and `Shop/Index.Mobile.cshtml`.** Both load `Shop/Details` as a modal partial (`isModal = true`), and that partial renders `Html.Markdown`. A link only on `Shop/Details` would have left the modal unstyled; the modal heading was measured after the change (parchment, 22px, 600) under both User-Agents.
- Linked views (17): Characters/Details and `.Mobile`, Contacts/Details and `.Mobile`, DungeonMaster/Profile and `.Mobile`, Events/Details, Series/Details, Shop/Details and `.Mobile`, Shop/Index and `.Mobile`, Quest/Details and `.Mobile`, Quest/Manage and `.Mobile`, QuestLog/Details. Events, Series and Characters/Contacts/Profile desktop views had no Styles section; one was created in each, after the view's opening code block. `Shop/Details.cshtml` is also the modal partial; its Styles section is simply not rendered in that mode, as `Shop/Details.Mobile` already did.
- Editor hosts: 23 views render `_QuestFormScripts` (the plan says 26 editor views; the count of files that render `_MarkdownEditor` outside the partial itself is 23), all of them covered by the one partial link.

## Sweep results

All sweeps run under the desktop and the mobile (Pixel 7) User-Agents at every width the manifest names. Frames: `~/qb-ui-review/phase-95/after/95-16/` (editor pages) and `after/95-16-content/` (Markdown output pages); the HEAD frames taken before any change are in `after/95-16-head/`.

| Consumer | Result |
|---|---|
| create, create-editor-focused, edit, followup, log-editrecap | compared against the HEAD capture: 0 problems; `--zero-contrast` on these five rows: 0 residual failures; Tab leaves the editor on all 40 editor captures (10 pages x 4 captures) |
| fx-characters-create, fx-contacts-details, fx-events-create, fx-shop-create, fx-dm-editprofile | 0 new problems against HEAD |
| details-open, details-finalized-selected, manage-open, manage-finalized, log-details-recap | `--zero-contrast`: 0 residual failures under both User-Agents |
| fx-contacts-details, fx-events-details, fx-characters-details, fx-series-details, fx-shop-details, fx-dm-profile | against the baseline captures and the chain of all earlier allow files: no new failure, apart from the help-line rename below |

Extra probes (scripts in `tools/`, outputs next to the frames):
- **Computed-style probe**, 11 pages x 2 User-Agents: a heading, quote, paragraph, strong and list item are injected into each `.markdown-content` and read back before and after. Headings were dark ink (rgb 26,26,26) on Quest Details, Contacts, DM profile, Events, Series and Shop desktop before, and are parchment now; everywhere else they stay parchment (the lighter heading parchment now). Paragraph colour, size and white-space are unchanged on every page. Files `markdown-content-before.json` and `markdown-content-after.json`.
- **Editor text probe**, 9 hosting pages x 2 User-Agents (plus Contacts Create and Edit as the DM): the editor is filled with all six heading levels, quote, list, link, code and a table, and every element of the editing area and of the preview is read for colour and text shadow. All ink with no shadow, after the extra narrowings listed under Deviations.
- Shop modal on `/Shop` under both User-Agents: the content sheet is loaded and applies.

## Deviations from Plan

**1. [Rule 1 - Bug] More foreign catch-alls than the plan names reach the editor.** The all-descendants probe found parchment text with a drop shadow on the editor's preview or editing area on: `contacts.css` (`.contacts-page h1`, desktop Contacts/Details, preview H1 in parchment on paper), `dm-editprofile.mobile.css`, `shop-management-create.mobile.css` and `shop-management-edit.mobile.css` (`h5` rule), and in the three sheets the plan names more selectors than the `span` one: `p`, `h4` to `h6`, and in `contact-detail.mobile.css` also `a`, `li` and the note-item group. Each is the same one-selector `:not(:where(.EasyMDEContainer *))` narrowing, changing nothing else. The old file hid all of this behind important-flagged black text. Commit `750fcfa5`.

**2. [Rule 3 - Blocking] The editor's preview quote bar rode on the content sheet.** `markdown-content.css` carried `.editor-preview blockquote`; once the content sheet leaves the layouts, editor pages that render no Markdown output would lose it. The rule moved into `markdown-editor.css`, on `--qb-control-border` instead of gold (gold is not used on a paper surface). Commit `a67c7c56`.

**3. [Judgement] Seven toolbar buttons do not fit one row at 320, as before this plan.** The plan states both 44px buttons and one row at 320 under a mobile User-Agent. The editor's container is 236 to 252px wide at 320 on every hosting page (card and page padding), and seven 44px buttons need 308px. Measured at HEAD with the old sheet, the toolbar already wraps to two rows there. I kept the 44px targets and the two rows: no horizontal overflow (document scroll width 320), every button 44 by 44. Making them fit one row would need buttons of about 33px or page padding changes outside this plan. Needs the operator's call if one row is a hard requirement.

**4. [Judgement] Markdown headings and the quote bar re-toned.** The stale dark-ink heading rule for card bodies was removed rather than moved onto a token, because it made headings unreadable on dark cards (measured, above). Heading and strong weight went from 700 to the 600 token (two-weight type scale), the quote bar padding from 12px to the 8px space token, the bar colour is the same gold at 50% via `color-mix`.

**5. [Process] BEFORE for four new foreign rows is HEAD on 8000, not the frozen baseline on 8001.** The plan says to capture `fx-characters-details`, `fx-dm-profile`, `fx-series-details`, `fx-shop-details` from the baseline app on 8001. The standing instruction for this run is not to touch port 8001 and a sweep against it was refused, so those four rows were captured on 8000 at HEAD before this plan changed any of their sheets. Those four pages' Markdown styling is changed only by this plan, so HEAD is an equivalent BEFORE for them. Rows added to `manifest.json` (not `manifest.mjs`, which is already out of step with the file: seven rows added by earlier plans exist only in the json); fixtures `dm.profile` (5029), `series.any` (1) and `shop.item` (5444) added to `fixtures.json`. A combined BEFORE is in `before95-16/report.json`.

**6. [Process] Fixture side effect, restored.** No DM on the Audit Board had a bio, so the DM profile rendered no Markdown. I wrote a bio as the audit DM through the EditProfile form, swept, then cleared it again through the same form so the frozen BEFORE of `fx-dm-editprofile` stays comparable (a prefilled editor changes the character counter sample and breaks the compare). The profile now renders no Markdown block again; its Markdown output was verified with the bio present.

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1311 passed; integration 1793 passed, 11 skipped (pre-existing), 0 failed. Task filters: `UiFoundation|MarkdownEditorMarkupTests|QuestsConversionGuardTests|MobileViewsTests` 177 passed at Task 1; `QuestsConversionGuardTests|UiFoundation|MobileViewsTests|ContactCategory` 211 passed at Task 2.
- The plan's literal compare commands, run against the original `before` capture, still exit 1. Every remaining line is an earlier plan's recorded change, not this plan's: (a) `followup` controls missing (the follow-up merge), the Delete Quest link, Revoke, Create Follow-Up and the signup labels (Details and Manage reworks); with the chain of every earlier `allow.json` merged (saved as `after/95-16/allow.json`) these clear; (b) `div#Description-help` and its siblings, "Supports Markdown formatting...", measured 4.25:1 on the unconverted foreign forms: the hint line was added to every editor by the shared editor partial earlier in this phase, was already failing identically at HEAD before this plan's first edit, and shows up as new only because BEFORE has no such line. It is `.form-text` on glass on pages owned by later phases; I did not add an exception entry (`contrast-exceptions.json` untouched, 12 entries).
- Same compare against the HEAD capture taken before this plan: 0 problems on every editor and Markdown row (the one exception is `fx-dm-profile`, whose HEAD row is a different DM and so not comparable; it is compared against its own BEFORE instead, clean).
- Working-tree app restarted on `http://localhost:8000` on this build and left running; port 8001 untouched.

## Not changed on purpose

- `contacts.css` keeps its note-item heading override (`#1a1a1a` on the notes list). Notes sit on dark glass, so a dark heading in a note is hard to read; this existed before, belongs to the Contacts page, and the plan says those overrides keep winning. Worth fixing in Phase 98.
- Stale comments in `contacts.css` and `contact-detail.mobile.css` still describe the old pale-gold heading rule; foreign files, comments only.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-95-38: the wrapper imports only the committed versioned `wwwroot/lib` file, and the rewritten guard keeps the no-CDN and lib-path assertions. T-95-39: foreign edits are one-line links and one-selector narrowings, all swept. T-95-40: `Html.Markdown` and its sanitising are untouched.

## Self-Check: PASSED

Verified: `easymde-vendor.css` contains `layer(vendor)`; `markdown-editor.css` and `markdown-content.css` open with `@layer pages {`; `_QuestFormScripts.cshtml` links both editor sheets; neither layout contains either sheet; 17 views link `markdown-content.css`; commits `750fcfa5` and `a67c7c56` exist; full build and test green; editor Tab probe `ok` on all 40 editor captures; zero-contrast compare over the five editor rows and the five Markdown output rows printed `ok`.
