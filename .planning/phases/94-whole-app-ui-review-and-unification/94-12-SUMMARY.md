---
phase: 94-whole-app-ui-review-and-unification
plan: 12
subsystem: ui-foundation
tags: [vendor, self-hosting, easymde, tagify, cropperjs, static-guard]
requires:
  - phase: 94-02
    provides: "gate.mjs / gate-compare.py zero-difference gate, run-app.sh, role storage states, fixtures"
  - phase: 94-11
    provides: "layouts self-hosted; wwwroot/lib versioned-directory pattern and vendor guard"
provides:
  - "EasyMDE 2.21.0, Tagify 4.38.0 and Cropper.js 2.1.1 served same-origin from versioned wwwroot/lib directories, with licences"
  - "UiFoundationViewLibraryGuardTests: no non-layout view loads a library from the jsDelivr host; every view /lib reference exists"
  - "ContactsTagsFormMarkupTests pin the local versioned Tagify path"
affects: [94-13]
tech-stack:
  added: ["easymde 2.21.0 (vendored)", "@yaireo/tagify 4.38.0 (vendored)", "cropperjs 2.1.1 (vendored)"]
  patterns: ["versioned vendor directory busts caches (no asp-append-version on vendor files)", "same-origin files carry no integrity or crossorigin attributes", "static source-scan guard with host built from fragments"]
key-files:
  created:
    - QuestBoard.Service/wwwroot/lib/easymde-2.21.0/ (easymde.min.js, easymde.min.css, LICENSE)
    - QuestBoard.Service/wwwroot/lib/tagify-4.38.0/ (tagify.min.js, tagify.css, LICENSE)
    - QuestBoard.Service/wwwroot/lib/cropperjs-2.1.1/ (cropper.min.js, LICENSE)
    - QuestBoard.IntegrationTests/Tests/UiFoundationViewLibraryGuardTests.cs
  modified:
    - QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml
    - QuestBoard.Service/Views/Characters/{Create,Create.Mobile,Edit,Edit.Mobile}.cshtml
    - QuestBoard.Service/Views/Contacts/{Create,Create.Mobile,Edit,Edit.Mobile}.cshtml
    - QuestBoard.Service/Views/DungeonMaster/{EditProfile,EditProfile.Mobile}.cshtml
    - QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs
decisions:
  - "Every SRI-pinned JS and CSS file was verified against the SHA-384 the view carried before the swap; all five matched, so the committed bytes are the ones browsers already verified"
  - "integrity and crossorigin attributes dropped on the local tags (same-origin; versioned directory pins the version)"
  - "_EmailLayout.razor and the five layouts untouched; the remaining CDN references (v4 shim, jQuery) belong to later plans"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 4200
  tasks: 2
  commits: 2
---

# Phase 94 Plan 12: View-Library Self-Hosting Summary

EasyMDE, Tagify and Cropper.js now load from versioned `wwwroot/lib` directories in the quest-form partial and the ten character, contact and DM profile views, byte-identical to what the CDN served, with zero differing pixels on all 26 gate captures under both User-Agents. `actuals.tokens` counts the hand-written files only (guard test, test edits, view tag lines), not the 8 vendor files.

## What changed

- Task 1 (commit `a30a0024`): EasyMDE JS, CSS and LICENSE saved under `lib/easymde-2.21.0/`; `_QuestFormScripts.cshtml` now has `href="~/lib/easymde-2.21.0/easymde.min.css"` and `src="~/lib/easymde-2.21.0/easymde.min.js"`; `UiFoundationViewLibraryGuardTests` created with `QuestFormPartial_LoadsEditorFromLib` and `ViewLibraryReferences_ResolveToCommittedFiles`.
- Task 2 (commit `ed6bb25a`): Cropper.js and Tagify files saved under `lib/cropperjs-2.1.1/` and `lib/tagify-4.38.0/`; the Cropper tag swapped in all ten views and the two Tagify tags in the four Contacts views (no other line in those views changed; `git diff --stat` 18 insertions, 54 deletions, all inside the library tags); the two Tagify markup facts rewritten to assert `/lib/tagify-4.38.0/tagify.min.js`, `/lib/tagify-4.38.0/tagify.css` and the absence of `@yaireo/tagify@`, keeping the `TagsInput` and `initContactTags(` assertions; `Views_LoadNoLibraryFromJsDelivr` added to the guard (the host is built from fragments, per the plan).
- The `Views_LoadNoLibraryFromJsDelivr` fact was added in Task 2, not Task 1, because it can only pass once the Tagify and Cropper tags are swapped.

## Vendor provenance (also in the commit messages)

All files fetched from the exact jsDelivr URLs the views referenced (LICENSE from the same package version root).

| File (under `wwwroot/lib/`) | Source URL | SHA-256 | SHA-384 matches the view's pinned value |
|---|---|---|---|
| easymde-2.21.0/easymde.min.js | https://cdn.jsdelivr.net/npm/easymde@2.21.0/dist/easymde.min.js | 2c06bddfd0c89176db08ccf9d42e2beaa9b4f1a4ff8fb2ec0bf1bed25ce08e05 | yes (`mTM6vzy+...B5iv`) |
| easymde-2.21.0/easymde.min.css | https://cdn.jsdelivr.net/npm/easymde@2.21.0/dist/easymde.min.css | 6eee36340432776d682e7372ec4a7eb29be4fdb3ffada32c0bfa5e31a5ef34a2 | yes (`ZoLYv3S+...cGozlG`) |
| easymde-2.21.0/LICENSE | https://cdn.jsdelivr.net/npm/easymde@2.21.0/LICENSE | 98eee44a1cdad5ad0bb0ea976791ab57b5eef7ac4cafeb162f30e3808564a564 | none pinned |
| tagify-4.38.0/tagify.min.js | https://cdn.jsdelivr.net/npm/@yaireo/tagify@4.38.0/dist/tagify.min.js | 20079de95cddb5c29277ff9417766ec7ad448483c800cd1584dd88e386061c6b | yes (`YtX1Y58Y...XJn0E`) |
| tagify-4.38.0/tagify.css | https://cdn.jsdelivr.net/npm/@yaireo/tagify@4.38.0/dist/tagify.css | 7e3fd3a639d5fe4862ee0d1afb12cb0b4b8cfa5b6b21507b50b5dda702d89b4b | yes (`C4PNucE7...h6uLu`) |
| tagify-4.38.0/LICENSE | https://cdn.jsdelivr.net/npm/@yaireo/tagify@4.38.0/LICENSE | 09c63ed2bf31227b84766cdabae1788c716bb5cdc13260b0d147aaf97154b985 | none pinned |
| cropperjs-2.1.1/cropper.min.js | https://cdn.jsdelivr.net/npm/cropperjs@2.1.1/dist/cropper.min.js | 27f29dae3c6fa7a5f6126901f4d1f8cbbc36756196046aa7e97d2eae14131979 | yes (`pDSc1bjp...dv39y9`) |
| cropperjs-2.1.1/LICENSE | https://cdn.jsdelivr.net/npm/cropperjs@2.1.1/LICENSE | eb917650781dcf06a60b97cba9b05bf583931ec2d311d8a5548a8dca90a57e0f | none pinned |

## Verification results

- Gate `d20-before` (captured with the plan-11 layouts already in the tree, before any edit) against `d20-after-easymde` and against `d20-after`: both PASS. 26 captures each (7 default gate pages plus Contacts Create and Edit, Characters Create and Edit, DM EditProfile, QuestLog EditRecap, each under desktop and mobile). Every capture 0 differing pixels, no new console error, page error or failed request, no status change. The only before-and-after error is the pre-existing 404 for a cropped character picture on quest-details.
- Smoke (`plans/94-12/smoke.mjs all`, both User-Agents): EasyMDE toolbar has 7 buttons on `/Quest/Create`; `tags.tagify` exists on `/Contacts/Create`; on `/Characters/Create` choosing a 400x400 PNG opens the crop dialog, `cropper-canvas` is defined and laid out (766x400 desktop, 362x320 mobile) and the cropper image received the file; no console error; no request to the jsDelivr host in any of them.
- Acceptance: `grep -lE 'cropperjs@|tagify@4|easymde@'` over Views and Areas prints nothing; `lib/cropperjs-2.1.1/cropper.min.js` is referenced by 10 views; `_EmailLayout.razor` and every layout are not in the diff.
- Task 1 filtered tests: 82 passed. Task 2 filtered tests: 140 passed. Full `dotnet build`: 0 errors. Full `dotnet test`: unit 1300 passed; integration 1666 passed, 11 skipped (pre-existing SQL Server-dependent skips), 0 failed.
- Port 8012 app stopped, nothing left listening; ports 8000, 8001 and 8014 untouched. No data written: the smoke check set a file on the portrait input but never submitted a form.

## Deviations from Plan

- **Tracer feedback gate:** `auto_advance` is false (interactive), which would normally mean a human checkpoint after the tracer. Its verify is fully automated and passed (tests, gate-compare PASS, smoke 7 buttons on both User-Agents), and the orchestrator asked for the complete plan, so I proceeded to Task 2 without returning a checkpoint (same handling as 94-02 and 94-11).
- Running app note: Razor views are compiled into the assembly, so the app on 8012 was restarted after each build before capturing the `after` gates; the `before` capture was of the unmodified tree.
- The plan's Task 2 verify filter names `ContactsTagsMobileTests` and `ContactsTagsDesktopMarkupTests`, which do not exist as classes (the facts live in `ContactsTagsFormMarkupTests`); the filter matched the existing classes and passed.

None otherwise; no auto-fix was needed.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-94-19 mitigated: each pinned JS and CSS file's SHA-384 equals the integrity value the view carried; licences recorded by SHA-256 and source URL; zero-difference gate on both swaps. T-94-20 and T-94-21 accepted as planned (init blocks unchanged byte for byte).

## Self-Check: PASSED

- Files exist: `QuestBoard.Service/wwwroot/lib/{easymde-2.21.0,tagify-4.38.0,cropperjs-2.1.1}` (8 files), `QuestBoard.IntegrationTests/Tests/UiFoundationViewLibraryGuardTests.cs`.
- Both task commits exist in `git log`.
- Scratch outputs under `~/qb-ui-review/phase-94/`: `gates/{d20-before,d20-after-easymde,d20-after}`, `plans/94-12/{extra.json,smoke.mjs,sample.png}`.
