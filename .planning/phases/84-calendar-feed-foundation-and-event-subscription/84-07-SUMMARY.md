---
phase: 84-calendar-feed-foundation-and-event-subscription
plan: 07
subsystem: calendar-feed
tags: [razor, bootstrap-modal, aria, csrf, qrcode-svg]

requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: "84-06's CalendarSubscriptionViewModel, ProfileViewModel.CalendarSubscriptions, and the AddCalendarSubscription/RenameCalendarSubscription/RevokeCalendarSubscription POST actions this plan's markup posts to"
provides:
  - "Calendar Subscription section on Views/Account/Profile.cshtml -- empty state, populated table (name/meta, address+copy, calendar-handoff link, QR trigger, rename trigger, delete form), shared rename modal, per-row QR modal, and the Copy/Add/Delete scripts"
  - "The identical section on Views/Account/Profile.Mobile.cshtml as stacked .calendar-subscription-row cards, with icon-only Copy/Show QR controls at the 44px touch floor"
  - "Prefixed .calendar-subscription-* rules appended to site.css (desktop) and account.mobile.css (mobile), additions only"
affects: [84-08]

actuals:
  tokens: 7857
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "show.bs.modal + event.relatedTarget data-attribute prefill, reused from Shop/Index.cshtml and _CharacterSelectModal.cshtml, applied to a single shared rename modal for every row"
    - "Delete-confirm script duplicated identically across desktop and mobile views rather than extracted, matching ContactCategoryManagement's existing both-layout convention"
    - "Clipboard write with a focus-and-select fallback when navigator.clipboard is unavailable or the write is rejected"

key-files:
  modified:
    - QuestBoard.Service/Views/Account/Profile.cshtml
    - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/site.css
    - QuestBoard.Service/wwwroot/css/account.mobile.css

key-decisions:
  - "Desktop table uses three columns (Subscription | Address+Copy | Actions) rather than folding Copy into a shared actions column, so the address field and its Copy button sit in the same table cell as one visual unit per the UI-SPEC's within-row hierarchy, while Show QR/Rename/Delete/webcal-link stay together in a dedicated actions column."
  - "The three scripts (copy, add, delete) and both modals (shared rename, per-row QR) are duplicated character-for-character between Profile.cshtml and Profile.Mobile.cshtml rather than extracted to a shared partial or script file, per the plan's explicit instruction and matching ContactCategoryManagement's existing both-layout precedent."
  - "The destructive delete-confirm string is verified byte-identical between layouts via `diff` on the extracted substring, not just eyeballed, since a divergence here is exactly the both-layout failure mode this phase is guarding against."

requirements-completed: [CALFEED-03, CALFEED-14]

coverage:
  - id: D1
    description: "The Calendar Subscription section ships on the desktop Profile layout with an empty state (explainer + Add Subscription), a populated table, and per-row Copy/webcal-link/Show QR/Rename/Delete controls, with the purple accent spent exactly once"
    requirement: CALFEED-03
    verification:
      - kind: other
        ref: "grep-based acceptance criteria (fa-rss text-purple, text-purple count==1, No calendar subscriptions yet, Never fetched yet, calendar-subscription-address readonly/not-disabled, fa-qrcode, fa-mobile-screen-button, renameSubscriptionModal, maxlength=60, aria-label, tabindex==-1-only, fetch()==0, confirm text, AntiForgeryToken>=3, forbidden-claims==0) all pass against Profile.cshtml; dotnet build/test green (508+748 passed)"
        status: pass
    human_judgment: true
    rationale: "No automated test renders the Razor view or exercises the empty/populated branches at runtime; acceptance is verified via source-level grep and a green build/test run only, not a rendered-page or browser assertion."
  - id: D2
    description: "The identical section ships on the mobile Profile layout as stacked card rows, with icon-only Copy and Show QR Code controls at the 44px touch floor, and every copy-contract string -- including the destructive confirm text -- matches the desktop layout character for character"
    requirement: CALFEED-14
    verification:
      - kind: other
        ref: "grep-based acceptance criteria on Profile.Mobile.cshtml (string presence, text-purple==1, calendar-subscription-icon-btn>=2, aria-label>=3, min-height/min-width:44px in account.mobile.css, fetch()==0, AntiForgeryToken>=3, forbidden-claims==0); diff of the extracted 'Delete \"...afterward.' substring between both views produced no output (byte-identical)"
        status: pass
    human_judgment: true
    rationale: "No real-mobile-user-agent render proof exists yet -- that verification is explicitly deferred to 84-08 per this plan's platform notes. This plan's own verification is structural (grep/diff) only."
  - id: D3
    description: "The address field is readonly (never disabled), always visible and selectable, with a Copy control that writes it via the clipboard API and falls back to focusing/selecting the field when the clipboard interface is unavailable or the write is rejected"
    requirement: CALFEED-14
    verification:
      - kind: other
        ref: "grep confirms the calendar-subscription-address input line carries readonly and never disabled on both layouts; the copy script's navigator.clipboard.writeText().then()/.catch() -> showManualFallback() path (focus+select+label swap) verified by code reading"
        status: pass
    human_judgment: true
    rationale: "No browser-driven test exercises an actual clipboard write, a denied/unavailable clipboard permission, or the 2-second label-swap timing; the affordance is verified by code reading only."
  - id: D4
    description: "Each subscription's QR modal renders the server-generated inline SVG with an accessible label describing its purpose (never the address text), and omits the QR region entirely -- with the rest of the modal (address, copy, calendar-handoff link) still rendering -- when generation failed"
    requirement: CALFEED-14
    verification:
      - kind: other
        ref: "grep confirms aria-label=\"QR code encoding the {name} calendar subscription address\" contains no address text on either layout; @if (!string.IsNullOrEmpty(subscription.QrCodeSvg)) gates only the SVG block, leaving the heading, copy button and webcal link outside the conditional"
        status: pass
    human_judgment: true
    rationale: "No test drives a subscription row with QrCodeSvg == null through this view to prove the omitted-block path renders without a broken-image artifact; verified by code reading only."
  - id: D5
    description: "Add, Rename and Delete are antiforgery-protected form POSTs issued by neither layout via a scripted request"
    requirement: CALFEED-03
    verification:
      - kind: other
        ref: "grep -c '@Html.AntiForgeryToken()' == 3 on both Profile.cshtml and Profile.Mobile.cshtml; grep -c 'fetch(' == 0 on both"
        status: pass
    human_judgment: false

duration: 32min
completed: 2026-09-18
status: complete
---

# Phase 84 Plan 7: Calendar Subscription Section on Both Profile Layouts Summary

**The Calendar Subscription section now lives on both `Views/Account/Profile.cshtml` and `Profile.Mobile.cshtml` -- empty state, a per-row address field with a Copy control and clipboard fallback, a calendar-handoff link, a per-row QR modal, a shared rename modal, and a confirm-guarded delete -- every mutation a plain antiforgery-protected form POST, with the purple accent spent exactly once per layout and the destructive confirm text byte-identical between them.**

## Performance

- **Duration:** 32 min (approximate; not captured at task-launch time)
- **Started:** ~2026-09-18T10:10:00Z
- **Completed:** 2026-09-18T10:41:36Z
- **Tasks:** 2
- **Files modified:** 4 (0 created)

## Accomplishments

- Desktop `Profile.cshtml`: widened the card wrapper to `col-lg-9 col-md-11` with a comment recording why, then added the section with an explainer-only empty state, a three-column populated table (Subscription | Address+Copy | Actions), a shared `renameSubscriptionModal`, one QR modal per subscription, and the Copy/Add/Delete scripts
- Mobile `Profile.Mobile.cshtml`: added the identical section as stacked `.calendar-subscription-row` cards, with Copy and Show QR Code reduced to icon-only `.calendar-subscription-icon-btn` controls (44px floor) while the calendar-handoff link, Rename and Delete keep their text labels; the same shared rename modal, per-row QR modals and three scripts duplicated character for character
- `site.css` gained a prefixed `.calendar-subscription-*` block (address field sizing, meta text, actions layout, QR SVG sizing) with a leading comment naming the one view it serves; `account.mobile.css` gained the mobile-scoped equivalent plus the row divider and 44px touch-target rules, both additions-only per `git diff`
- Every copy-contract string (heading, empty-state body, button labels, modal headings, the destructive confirm sentence) matches between layouts; the confirm sentence specifically verified byte-identical via a `diff` of the extracted substring, not just read side by side

## Task Commits

Each task was committed atomically:

1. **Task 1: The Calendar Subscription section on the desktop Profile layout** - `57d5cb94` (feat)
2. **Task 2: The same section on the mobile Profile layout, with its own card-row shape and touch targets** - `1766a8a6` (feat)

**Plan metadata:** committed as part of this SUMMARY.

## Files Created/Modified

- `QuestBoard.Service/Views/Account/Profile.cshtml` - widened wrapper, the section (empty/populated states, add form, shared rename modal, per-row QR modals), and the Copy/Add/Delete scripts
- `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml` - the identical section as stacked card rows with icon-only touch-target controls, and the duplicated scripts
- `QuestBoard.Service/wwwroot/css/site.css` - appended `.calendar-subscription-*` desktop rules
- `QuestBoard.Service/wwwroot/css/account.mobile.css` - appended `.calendar-subscription-*` mobile rules including the 44px icon-button floor

## Decisions Made

See `key-decisions` in the frontmatter above. The most consequential: splitting the desktop table into a dedicated Address+Copy column (rather than lumping Copy into the actions column) to satisfy the UI-SPEC's "one visual unit" requirement for the address field and its Copy control, without disturbing the destructive-action-last ordering of the remaining controls.

## Deviations from Plan

None - plan executed exactly as written. The plan left the exact `data-address-target` attribute value and the table's column layout to the executor's discretion ("..." in the plan text); both choices are recorded under Key Decisions above rather than logged as deviations, since the plan explicitly deferred them.

## Issues Encountered

None. `dotnet build` and `dotnet test` (508 unit + 748 integration) were green on the first run after both tasks; every grep-based acceptance criterion for both tasks passed without a fix cycle.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Both layouts now expose the full Calendar Subscription feature end to end; the address, copy, calendar-handoff link, QR code, rename and delete affordances are all reachable on a phone, which was this phase's stated purpose.
- This plan's verification is structural only (grep against source, plus a green `dotnet build`/`dotnet test`) -- no automated test renders the Razor views or exercises the JavaScript at runtime. The must-have truths around clipboard behavior, modal prefill/focus, and the omitted-QR-on-failure path (coverage `D1`-`D4` above) are unverified beyond code reading. The next plan in this phase (84-08) explicitly owns the real mobile-user-agent render proof; a full markup/behavior verification suite for this section (desktop + mobile, per the phase's `## success_criteria`) should close the remaining gap.
- No deferred items, stubs, or threat flags were introduced by this plan.

## Self-Check: PASSED

- `QuestBoard.Service/Views/Account/Profile.cshtml` - FOUND
- `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml` - FOUND
- `QuestBoard.Service/wwwroot/css/site.css` - FOUND (calendar-subscription-qr svg / max-width: 240px present)
- `QuestBoard.Service/wwwroot/css/account.mobile.css` - FOUND (min-height/min-width: 44px present)
- Commit `57d5cb94` - FOUND in `git log --oneline`
- Commit `1766a8a6` - FOUND in `git log --oneline`
- `dotnet build` - PASSED (0 errors, 6 projects)
- `dotnet test QuestBoard.UnitTests` - PASSED (508/508)
- `dotnet test QuestBoard.IntegrationTests` - PASSED (748/748)
- All plan-level `<acceptance_criteria>` greps for both tasks re-verified after both commits: all PASSED
- `git diff` on both stylesheets confirmed additions-only (0 deletions of existing lines)
- Destructive confirm text confirmed byte-identical between layouts via `diff` of the extracted substring
- No requirement id, phase number, or plan number found in either view file

---
*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Completed: 2026-09-18*
