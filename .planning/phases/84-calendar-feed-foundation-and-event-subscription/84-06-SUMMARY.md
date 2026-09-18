---
phase: 84-calendar-feed-foundation-and-event-subscription
plan: 06
subsystem: calendar-feed
tags: [qrcoder, svg-qr, automapper, aspnetcore-mvc]

requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: "84-02's ICalendarSubscriptionService (MintForUserAsync/GetForUserAsync/RenameAsync/RevokeAsync) and the CalendarSubscription domain model this plan's controller actions and view model wrap"
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: "84-04's EmailSettings.AppUrl-based absolute-address pattern, already proven correct in production by existing email links"
provides:
  - "QRCoder 1.8.0, installed into QuestBoard.Service only after operator legitimacy sign-off"
  - "CalendarSubscriptionAddress.BuildHttps/BuildWebcal -- both address forms built from configuration, never from the request"
  - "CalendarSubscriptionQrCode.ToSvg -- inline vector QR markup via SvgQRCode, returning null rather than throwing on failure"
  - "CalendarSubscriptionViewModel and ProfileViewModel.CalendarSubscriptions, plus the CalendarSubscription -> CalendarSubscriptionViewModel AutoMapper map"
  - "AccountController.Profile widened to populate the subscription list; three new antiforgery-protected POST actions (Add/Rename/Revoke) that a later markup plan can call"
affects: [84-07, 84-08]

actuals:
  tokens: 4000
  tasks: 3
  commits: 2

tech-stack:
  added: ["QRCoder 1.8.0 (QuestBoard.Service only)"]
  patterns:
    - "Static helper class with no injected state (AppVersion.cs precedent) for both the address builder and the QR renderer"
    - "Null-return-on-failure contract for a rendering helper, so a caller degrades gracefully instead of throwing into a page render"
    - "Ten-second server-side mint guard keyed on the caller's own newest-subscription timestamp, absorbing a double submit without a dedicated idempotency-token table"

key-files:
  created:
    - QuestBoard.Service/Helpers/CalendarSubscriptionAddress.cs
    - QuestBoard.Service/Helpers/CalendarSubscriptionQrCode.cs
    - QuestBoard.Service/ViewModels/AccountViewModels/CalendarSubscriptionViewModel.cs
  modified:
    - QuestBoard.Service/QuestBoard.Service.csproj
    - QuestBoard.Service/ViewModels/AccountViewModels/ProfileViewModel.cs
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Controllers/Admin/AccountController.cs

key-decisions:
  - "Task 1's checkpoint:decision (QRCoder package legitimacy, gate=blocking-human) was answered by the operator before this dispatch (approved, all five checks confirmed by hand) -- recorded here, not re-asked. Installed QRCoder 1.8.0 into QuestBoard.Service only."
  - "The QR payload encodes the webcal:// (calendar-handoff) address, not the plain https:// address -- scanning hands off to the phone's calendar app and subscribes, whereas the plain form would download a one-time import. Commented in Profile() as the rationale for a later reader."
  - "Rename/Revoke report one neutral TempData[\"Error\"] on a false service result rather than distinguishing 'not yours' from 'not found', so a message can never confirm whether a foreign subscription id exists."

requirements-completed: [CALFEED-01, CALFEED-02, CALFEED-03, CALFEED-14]

coverage:
  - id: D1
    description: "The absolute address shown on Profile is built from the configured application address, never from the current request's scheme or host"
    requirement: CALFEED-01
    verification:
      - kind: other
        ref: "grep -cE 'Request\\.Scheme|Request\\.Host|HttpContext' CalendarSubscriptionAddress.cs -> 0, plus dotnet build/test green"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every subscription offers the same address in two forms -- a plain web form and a calendar-handoff form -- differing only in their scheme"
    requirement: CALFEED-14
    verification:
      - kind: other
        ref: "grep -c 'feeds/calendar' and grep -c 'webcal://' CalendarSubscriptionAddress.cs both >= 1; BuildWebcal derives strictly from BuildHttps's output"
        status: pass
    human_judgment: false
  - id: D3
    description: "A scannable code is generated on the server as inline vector markup, needing no image encoder and no native library in the container"
    requirement: CALFEED-14
    verification:
      - kind: other
        ref: "grep -cE 'PngByteQRCode|ArtQRCode|new QRCode\\(' CalendarSubscriptionQrCode.cs -> 0; grep -c 'SvgQRCode' -> 1"
        status: pass
    human_judgment: true
    rationale: "No automated test actually invokes ToSvg and inspects the returned SVG, or exercises it in an image-encoder-less environment; only the absence of the bitmap/styled renderer classes is structurally confirmed."
  - id: D4
    description: "When code generation fails, the row and its modal still render every other way of getting the address, and no broken image appears"
    requirement: CALFEED-14
    verification: []
    human_judgment: true
    rationale: "No Profile markup exists yet -- this plan deliberately builds the plumbing before any view (see plan objective). Only ToSvg's null-return contract and the controller's log-and-leave-null path are structurally verified; the 'no broken image' claim is about markup a later plan builds."
  - id: D5
    description: "Pressing Add mints exactly one subscription for the signed-in member, named with a non-empty default the member can rename"
    requirement: CALFEED-03
    verification:
      - kind: other
        ref: "grep -c '\"New subscription\"' CalendarSubscriptionService.cs -> 1 (pre-existing from 84-02)"
        status: pass
    human_judgment: true
    rationale: "No controller-level test invokes AddCalendarSubscription directly; the default-name literal is confirmed at the service layer only."
  - id: D6
    description: "A replayed or double-submitted Add cannot mint a second subscription from one intent"
    requirement: CALFEED-03
    verification: []
    human_judgment: true
    rationale: "No automated test drives two rapid Add requests and asserts only one subscription results; the ten-second guard is verified by code reading only."
  - id: D7
    description: "Rename and delete act only on a subscription owned by the signed-in member; an id belonging to someone else changes nothing and reports nothing about that subscription"
    requirement: CALFEED-03
    verification: []
    human_judgment: true
    rationale: "Ownership matching is enforced at the repository layer proven in 84-02; this plan's new controller actions pass the authenticated user id through but are not exercised by a dedicated foreign-id test."
  - id: D8
    description: "Delete retires the subscription rather than removing it, and the retired subscription disappears from the member's own list"
    requirement: CALFEED-03
    verification: []
    human_judgment: true
    rationale: "Tombstone behavior (RevokedAt) was proven at the repository/service layer in 84-02/84-04; this plan's RevokeCalendarSubscription action wiring itself has no dedicated test."
  - id: D9
    description: "A rename to an empty name is refused, and a name is capped at sixty characters at the point it is accepted"
    requirement: CALFEED-03
    verification: []
    human_judgment: true
    rationale: "No automated test submits an empty, whitespace-only, or 61-character name to RenameCalendarSubscription; the trim/length guard is verified by code reading only."
  - id: D10
    description: "Every add, rename and delete outcome is reported through the application's existing server-rendered notice mechanism, with no new error surface introduced"
    requirement: CALFEED-03
    verification:
      - kind: other
        ref: "grep -c 'TempData[\"Success\"]' AccountController.cs risen by exactly 3 from baseline; every branch of all three actions sets TempData[\"Success\"] or TempData[\"Error\"]"
        status: pass
    human_judgment: false

duration: 16min
completed: 2026-09-18
status: complete
---

# Phase 84 Plan 6: Calendar Subscription Plumbing -- QRCoder, Address Builder, Row View Model, and the Add/Rename/Revoke Actions Summary

**QRCoder 1.8.0 installed into QuestBoard.Service alone after operator sign-off, backing a configuration-derived address builder and a null-safe SVG QR renderer; Profile's view model now carries a fully populated subscription row per live subscription, and three antiforgery-protected POST actions mint, rename and retire -- each ownership-scoped, replay-guarded, and reported through the existing TempData notice convention -- with no markup yet, by design.**

## Performance

- **Duration:** 16 min
- **Started:** 2026-09-18T09:20:51Z
- **Completed:** 2026-09-18T09:36:51Z
- **Tasks:** 3 (Task 1 checkpoint answered by the operator before dispatch; Task 2 and 3 built and verified)
- **Files modified:** 7 (3 created, 4 modified)

## Accomplishments

- `QRCoder` 1.8.0 added to `QuestBoard.Service`'s `PackageReference` list alone, matching the file's existing tab indentation, confirmed absent from `QuestBoard.Domain` and `QuestBoard.Repository`
- `CalendarSubscriptionAddress.BuildHttps`/`BuildWebcal` build both address forms from `IOptions<EmailSettings>.AppUrl`, touching neither `Request.Scheme` nor `Request.Host` -- pinned by a grep asserting zero occurrences in the file
- `CalendarSubscriptionQrCode.ToSvg` renders inline vector markup via QRCoder's `SvgQRCode` renderer at error-correction level `Q`, returning `null` rather than throwing when generation fails, with the bitmap/styled renderers absent by construction
- `CalendarSubscriptionViewModel` and a widened `ProfileViewModel.CalendarSubscriptions` (defaulting to an empty list), mapped from `CalendarSubscription` via a new AutoMapper map that ignores the three controller-populated address/QR properties
- `AccountController.Profile` populates the subscription list, filling each row's HTTPS and webcal addresses and a QR code encoding the calendar-handoff form specifically, logging a warning naming only the subscription id when QR generation fails
- Three new `[HttpPost] [Authorize] [ValidateAntiForgeryToken]` actions -- `AddCalendarSubscription`, `RenameCalendarSubscription`, `RevokeCalendarSubscription` -- each resolve the acting member from `userService.GetUserAsync(User)` and never bind a member id from request input; `Add` refuses a mint when the newest live subscription is under ten seconds old; `Rename`/`Revoke` report one neutral failure notice for a false service result

## Task Commits

Each task was committed atomically:

1. **Task 2: Install QRCoder and add the address builder and the code renderer** - `fc01815d` (feat)
2. **Task 3: The row view model and the add, rename and delete POST actions** - `8c058bd9` (feat)

_Task 1 (`checkpoint:human-verify`, `gate="blocking-human"`) required no commit -- the operator's `approved` answer, with all five legitimacy checks confirmed by hand, was recorded before this dispatch and is documented under Key Decisions above._

**Plan metadata:** committed as part of this SUMMARY.

## Files Created/Modified

- `QuestBoard.Service/Helpers/CalendarSubscriptionAddress.cs` - static `BuildHttps`/`BuildWebcal`, built from configuration only
- `QuestBoard.Service/Helpers/CalendarSubscriptionQrCode.cs` - static `ToSvg`, vector renderer only, null on failure
- `QuestBoard.Service/ViewModels/AccountViewModels/CalendarSubscriptionViewModel.cs` - the new row shape
- `QuestBoard.Service/QuestBoard.Service.csproj` - `QRCoder` `PackageReference` added to the existing item group
- `QuestBoard.Service/ViewModels/AccountViewModels/ProfileViewModel.cs` - new `CalendarSubscriptions` list property
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - new `CalendarSubscription` -> `CalendarSubscriptionViewModel` map
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` - widened constructor and `Profile()`, three new POST actions

## Decisions Made

See `key-decisions` in the frontmatter above. The most consequential: the QR payload deliberately encodes the `webcal://` form rather than the plain `https://` form, and both the checkpoint answer and the reasoning behind it are carried forward from the operator's pre-dispatch review rather than re-asked.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Rewrote Rename/Revoke's success-notice assignment from a ternary to an explicit if/else**
- **Found during:** Task 3 acceptance-criteria verification
- **Issue:** The first draft wrote `TempData[renamed ? "Success" : "Error"] = renamed ? "..." : "...";`. This is behaviorally identical to an if/else, but the acceptance criterion `grep -c 'TempData["Success"]'` (a literal-text grep) requires the literal substring `TempData["Success"]` to appear in the source for each of the three new actions; the ternary form never emits that literal text, so the criterion was blocked from passing.
- **Fix:** Rewrote both `RenameCalendarSubscription` and `RevokeCalendarSubscription` to set `TempData["Success"]`/`TempData["Error"]` in explicit `if`/`else` branches, matching `AddCalendarSubscription`'s style and the literal grep's expectation.
- **Files modified:** `QuestBoard.Service/Controllers/Admin/AccountController.cs`
- **Verification:** `grep -c 'TempData["Success"]'` rose from baseline 5 to 8 (exactly +3); `dotnet build`/`dotnet test` stayed green.
- **Committed in:** `8c058bd9` (Task 3 commit) -- caught and fixed before the commit was made.

### Noted, Not Fixed

**1. Task 2/3's `Request.Scheme|Request.Host` acceptance-criteria grep has two unavoidable pre-existing matches**
- **Found during:** Task 3 acceptance-criteria verification
- **Issue:** The criterion `grep -cE 'Request\.Scheme|Request\.Host' AccountController.cs` outputs `0` is written to prove this plan's new code never derives an address from the request. The file already contains two occurrences of `Request.Scheme` predating this plan, at `ForgotPassword`'s and `Edit`'s `Url.Action(..., Request.Scheme)` calls -- both build absolute callback URLs for unrelated password-reset and email-change-confirmation flows, not the calendar subscription address.
- **Resolution:** Verified that neither of this plan's new files (`CalendarSubscriptionAddress.cs`, the widened `Profile()`, or the three new POST actions) contains `Request.Scheme` or `Request.Host` anywhere -- the two matches are both in code this plan did not touch. Per CLAUDE.md's scope boundary ("only auto-fix issues directly caused by the current task's changes; pre-existing issues in unrelated files are out of scope"), did not modify the unrelated `ForgotPassword`/`Edit` call sites to force the literal grep to `0`, since doing so risks the password-reset and email-change flows for no safety benefit to this plan's actual security property.
- **Files modified:** None beyond this plan's own planned changes.
- **Impact:** None on the actual security property (this plan's address-building code is verified free of request-derived values); only the whole-file literal grep as written cannot distinguish "this plan's code" from "the rest of the file." Logged to `.planning/WINDOWS.md` as a `deviation` entry for visibility at ship time.

---

**Total deviations:** 1 auto-fixed (1 blocking, caught pre-commit), 1 noted-and-verified (1 unsatisfiable-as-literally-written acceptance-criteria grep against pre-existing unrelated code).
**Impact on plan:** Neither affects correctness, security, or scope. No unrelated code was changed to force a literal grep to pass.

## Issues Encountered

None beyond the two items above.

## User Setup Required

None - no external service configuration required. The one new dependency (`QRCoder`) is a NuGet package installed automatically; no account, API key, or dashboard configuration is needed.

## Next Phase Readiness

- The plumbing this plan built -- the address builder, the QR renderer, the row view model, and the three ownership-scoped POST actions -- is committed and green, so the next plan can build both Profile layouts' markup against a view model and action set that are already settled, per this plan's stated purpose.
- Several must-have truths (D3-D9 above) are currently verified only structurally (grep/code-reading), because no markup exists yet to exercise them end to end. The plan that adds the views should either add controller/integration tests for `AddCalendarSubscription`/`RenameCalendarSubscription`/`RevokeCalendarSubscription`, or rely on the phase-level UAT pass to close this gap -- see coverage `D3` through `D9` for the precise items a reviewer should weigh.
- The two pre-existing `Request.Scheme` occurrences in `AccountController.cs` (unrelated to this plan) remain unaddressed and are now tracked in `.planning/WINDOWS.md`.

## Self-Check: PASSED

- `QuestBoard.Service/Helpers/CalendarSubscriptionAddress.cs` - FOUND
- `QuestBoard.Service/Helpers/CalendarSubscriptionQrCode.cs` - FOUND
- `QuestBoard.Service/ViewModels/AccountViewModels/CalendarSubscriptionViewModel.cs` - FOUND
- Commit `fc01815d` - FOUND in `git log --oneline --all`
- Commit `8c058bd9` - FOUND in `git log --oneline --all`
- `dotnet build` - PASSED (0 errors)
- `dotnet test QuestBoard.UnitTests` - PASSED (508/508)
- `dotnet test QuestBoard.IntegrationTests` - PASSED (748/748)
- All plan-level `<acceptance_criteria>` greps re-verified after Task 3's fix: package scoping (Service only), vector-renderer-only, address builder request-free, action signatures exact-match (3/3), `ValidateAntiForgeryToken` risen by exactly 3, `GetUserAsync(User)` risen by 3, `TempData["Success"]` risen by exactly 3, `CreateMap<CalendarSubscription, CalendarSubscriptionViewModel>` with 3 `opt.Ignore()` members - all PASSED except the documented `Request.Scheme|Request.Host` whole-file literal grep (2 pre-existing, unrelated matches -- see Deviations)

---
*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Completed: 2026-09-18*
