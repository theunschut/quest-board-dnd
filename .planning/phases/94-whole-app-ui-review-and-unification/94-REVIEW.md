---
phase: 94-whole-app-ui-review-and-unification
reviewed: 2026-11-09T00:00:00Z
depth: standard
files_reviewed: 82
files_reviewed_list:
  - QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs
  - QuestBoard.IntegrationTests/Controllers/PlatformAreaLayoutTests.cs
  - QuestBoard.IntegrationTests/Mobile/MobileCssTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationBaseGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationComponentGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationDesktopShellGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationMobileShellGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationToastGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationVendorGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationViewLibraryGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationWcagContrastTests.cs
  - QuestBoard.IntegrationTests/Tests/WcagContrast.cs
  - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
  - QuestBoard.Service/Views/Characters/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Create.cshtml
  - QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Edit.cshtml
  - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Create.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
  - QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.cshtml
  - QuestBoard.Service/Views/Shared/_Footer.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Footer.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml
  - QuestBoard.Service/Views/Shared/_Toasts.cshtml
  - QuestBoard.Service/wwwroot/css/account.mobile.css
  - QuestBoard.Service/wwwroot/css/admin-form.mobile.css
  - QuestBoard.Service/wwwroot/css/admin-quests.mobile.css
  - QuestBoard.Service/wwwroot/css/admin-users.mobile.css
  - QuestBoard.Service/wwwroot/css/app.css
  - QuestBoard.Service/wwwroot/css/base.css
  - QuestBoard.Service/wwwroot/css/character-detail.mobile.css
  - QuestBoard.Service/wwwroot/css/character-form.mobile.css
  - QuestBoard.Service/wwwroot/css/characters.mobile.css
  - QuestBoard.Service/wwwroot/css/components.css
  - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
  - QuestBoard.Service/wwwroot/css/contact-form.mobile.css
  - QuestBoard.Service/wwwroot/css/contacts.css
  - QuestBoard.Service/wwwroot/css/contacts.mobile.css
  - QuestBoard.Service/wwwroot/css/dm-create.mobile.css
  - QuestBoard.Service/wwwroot/css/dm-editprofile.mobile.css
  - QuestBoard.Service/wwwroot/css/dm-manage.mobile.css
  - QuestBoard.Service/wwwroot/css/dm-profile.mobile.css
  - QuestBoard.Service/wwwroot/css/group-picker.css
  - QuestBoard.Service/wwwroot/css/home.mobile.css
  - QuestBoard.Service/wwwroot/css/legacy-pages.css
  - QuestBoard.Service/wwwroot/css/markdown-content.css
  - QuestBoard.Service/wwwroot/css/markdown-editor.css
  - QuestBoard.Service/wwwroot/css/mobile.css
  - QuestBoard.Service/wwwroot/css/move-entry.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-group.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-users.mobile.css
  - QuestBoard.Service/wwwroot/css/players.mobile.css
  - QuestBoard.Service/wwwroot/css/quest-edit.mobile.css
  - QuestBoard.Service/wwwroot/css/quest-followup.mobile.css
  - QuestBoard.Service/wwwroot/css/quest-log-detail.mobile.css
  - QuestBoard.Service/wwwroot/css/quest-log.mobile.css
  - QuestBoard.Service/wwwroot/css/quests.css
  - QuestBoard.Service/wwwroot/css/quests.mobile.css
  - QuestBoard.Service/wwwroot/css/shell.css
  - QuestBoard.Service/wwwroot/css/shell.mobile.css
  - QuestBoard.Service/wwwroot/css/shop-details.mobile.css
  - QuestBoard.Service/wwwroot/css/shop-management-create.mobile.css
  - QuestBoard.Service/wwwroot/css/shop-management-edit.mobile.css
  - QuestBoard.Service/wwwroot/css/shop-management-index.mobile.css
  - QuestBoard.Service/wwwroot/css/shop.mobile.css
  - QuestBoard.Service/wwwroot/css/tokens.css
  - QuestBoard.Service/wwwroot/js/site.js
  - build/tests/package-release-smoke-test.sh
findings:
  critical: 0
  warning: 2
  info: 6
  total: 8
status: issues_found
---

# Phase 94: Code Review Report

**Reviewed:** 2026-11-09
**Depth:** standard
**Files Reviewed:** 82 (the deleted `modern-card.css` was reviewed through its diff only; 81 files exist at HEAD)
**Status:** issues_found

## Summary

No critical findings. The five layouts keep every menu item and every condition unchanged (diffed against `b6770322`), the Logout forms are still `method="post"` tag-helper forms (antiforgery token is auto-emitted), `Url.IsLocalUrl(movedEntryUrl)` is still in `_Toasts.cshtml`, nothing uses `Html.Raw`, jQuery removal is safe (no `$(`/`jQuery` anywhere in views or `wwwroot/js`), and no CDN host remains outside `wwwroot/lib`. The site.js offcanvas aria-expanded sync is correct (Bootstrap dispatches `show.bs.offcanvas`/`hidden.bs.offcanvas` as bubbling native events, the handler is registered after `hydrateWallClockTimes()` and does not touch it). `components.css`, `base.css`, `shell.css`, `shell.mobile.css`, `group-picker.css` have no colour literals and no `!important` outside reduced-motion and `.visually-hidden-focusable`. No planning references (phase/plan numbers, decision ids, planning doc names) exist in any reviewed source, CSS, view or test; the only matches are the scanner regexes themselves.

Two warnings concern the cascade-layer design (a latent behaviour change that the guards do not see) and the weak scope of the planning-reference guard.

## Warnings

### WR-01: Unlayered `!important` page rules silently lose to layered Bootstrap `!important` utilities

**File:** `QuestBoard.Service/wwwroot/css/legacy-pages.css:150-153`, `QuestBoard.Service/wwwroot/css/quests.mobile.css:26-29`, `QuestBoard.Service/wwwroot/css/account.mobile.css:48-50`, `QuestBoard.Service/wwwroot/css/dm-manage.mobile.css:45-48` (representative; roughly 100 such declarations across the unlayered sheets)
**Issue:** Bootstrap now lives in the `vendor` layer. For `!important` declarations the layer order inverts and unlayered rules rank lowest, so a page-sheet rule such as `.item-name.text-light { color: #F4E4BC !important }` or `.participant-list-mobile .text-muted { color: rgba(244,228,188,.7) !important }` is now beaten by Bootstrap's own `.text-light` / `.text-muted` `!important` on the same property, regardless of specificity. `app.css` documents this for the foundation, but the legacy sheets were left carrying the old assumption: these overrides are now dead (the element takes `--qb-fg-muted` or Bootstrap light `#f8f9fa` instead of the intended parchment), which is a silent visual change that no test observes. `UiFoundationLayerGuardTests.LegacyDesktopSheet_IsUnlayeredAndRenamed` even asserts the opposite premise in its comment ("keeps beating every layered rule"), which is false for important declarations.
**Fix:** Delete the colour `!important` overrides that target Bootstrap colour utilities (`.text-muted`, `.text-light`, `.text-danger`, ...) in the legacy/mobile sheets and rely on the `--bs-*` variable mapping, or move those few rules into a layer; correct the guard comment to say "beats layered non-important rules".

### WR-02: The planning-reference guard only covers a handful of files and only this phase's identifiers

**File:** `QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs:146-164`
**Issue:** `FoundationFiles_CarryNoPlanningReferences` scans `app.css`, the six layered files, the five layouts and `.claude/ui-guidelines.md`, and its patterns match only decision ids D-01..D-24, `G1-O/R#`, "Phase 94", `94-NN` and five document names. CLAUDE.md forbids any phase/plan/requirement/review id in source; a reference to another phase, a `WR-`/`CR-` finding id, or any reference in `site.js`, the 29 `*.mobile.css` sheets, the `.cshtml` views or the tests passes silently. Missing files are also skipped by `.Where(File.Exists)`, so a renamed file drops out of the scan without a failure.
**Fix:** Generalise the patterns (`Phase\s+\d+`, `\b\d{2,3}-\d{2}\b` in comments, `\b(WR|CR|IN|BL)-\d+\b`, `[A-Z]{3,8}-\d{2}\b`) and extend the file list to all of `wwwroot/css/*.css`, `wwwroot/js/site.js`, `Views/**` and `Areas/**/*.cshtml`; fail instead of skip when a listed file is missing.

## Info

### IN-01: Comments still describe the deleted `modern-card.css` behaviour

**File:** `QuestBoard.Service/wwwroot/css/contacts.css:283-285`, `QuestBoard.Service/wwwroot/css/markdown-editor.css:17-30`, `QuestBoard.Service/wwwroot/css/quests.css:669,909-913`
**Issue:** These comments cite a "shared card stylesheet" forced heading colour and `legacy-pages.css` `.modern-card h1-h6/p/li/span !important` rules; those rules no longer exist anywhere (`legacy-pages.css` has no `.modern-card` selector). The tripled `.CodeMirror` specificity bump and the `!important` "super-specific" quest-log selectors are justified by rules that are gone, which will mislead the next editor.
**Fix:** Rewrite the comments to the current reason (card text colour is inherited from `--qb-fg`, so the editor surface and Tagify input must reset it), and drop the specificity hacks once confirmed unnecessary.

### IN-02: No guard pins the offcanvas aria-expanded sync or the Logout forms

**File:** `QuestBoard.Service/wwwroot/js/site.js:416-427`, `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:222-229`
**Issue:** Removing the new `show/hidden.bs.offcanvas` listener, or turning a Logout `<form method="post">` into a link, breaks no test; the mobile guards assert `aria-expanded="false"` on the toggler but nothing checks it ever becomes `true`.
**Fix:** Add a source guard that `site.js` contains `'show.bs.offcanvas'`-style sync (or the `['show','hidden']` loop) and that each layout's Logout control sits inside a `<form ... method="post">`.

### IN-03: Silent-skip and weak-pin patterns in guards

**File:** `QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs:73-74,265`, `QuestBoard.IntegrationTests/Tests/UiFoundationVendorGuardTests.cs:246-248`, `QuestBoard.IntegrationTests/Mobile/MobileCssTests.cs:~108`, `QuestBoard.IntegrationTests/Controllers/PlatformAreaLayoutTests.cs:28`
**Issue:** `ExistingCssFiles` drops missing files from the no-colour-literal scan; `LibReferences_ResolveToCommittedFiles` returns early if `app.css` is missing; `MobileCss_File_Contains16pxBodyFontSize` only checks that `font-size: var(--qb-text-base)` appears anywhere in `base.css` (any rule satisfies it); the Platform layout test matches `"app.css"`/`"base.css"` as bare substrings of the whole HTML. Each can pass while the thing pinned is broken.
**Fix:** Assert file existence instead of filtering, scope the body-size check to the `body` rule, and match `<link ... href="...app.css...">` tags.

### IN-04: Vendored Font Awesome CSS references a font file that is not committed

**File:** `QuestBoard.Service/wwwroot/lib/fontawesome-6.7.2/css/all.min.css` (url `../webfonts/fa-v4compatibility.woff2` / `.ttf`), guards in `UiFoundationVendorGuardTests.cs:230-250`
**Issue:** `webfonts/` holds only brands/regular/solid; `fa-v4compatibility.*` is referenced by an `@font-face` for the `FontAwesome` family. No app code uses that family today, so nothing breaks, but neither the guard (which checks only `app.css` and layout URLs) nor the smoke test would notice a 404 if a v4-compat icon is ever used.
**Fix:** Either vendor the two files or extend `LibReferences_ResolveToCommittedFiles` to resolve `url()` targets inside the vendored CSS and document the omission.

### IN-05: Minor markup and test hygiene

**File:** `QuestBoard.Service/Views/Shared/_Layout.cshtml:~290`, `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml`, `QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml`, `QuestBoard.IntegrationTests/Tests/UiFoundationDesktopShellGuardTests.cs:~125`, `QuestBoard.IntegrationTests/Tests/UiFoundationMobileShellGuardTests.cs:~170`
**Issue:** `<main ... role="main">` repeats the implicit landmark role; the two `dir!` dereferences in the `BackdropImage`/`MobileBackdropImage` tests throw a `NullReferenceException` instead of a clear message if the repo root is missing; `DesktopFooter_TextUnchanged` is not separated from the next `[Fact]` by a blank line.
**Fix:** Drop `role="main"`, reuse `FindRepositoryRoot()` (which throws a descriptive error), add the blank line.

### IN-06: Unused design tokens

**File:** `QuestBoard.Service/wwwroot/css/tokens.css` (`--qb-radius-pill`, `--qb-space-3xl`, `--qb-wood-light`; `--qb-accent-ink` is read only by the contrast table)
**Issue:** Declared but never referenced by any stylesheet.
**Fix:** Remove them or keep them intentionally with a one-line note.

---

_Reviewed: 2026-11-09_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
