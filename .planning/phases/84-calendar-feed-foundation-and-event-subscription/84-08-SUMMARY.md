---
phase: 84-calendar-feed-foundation-and-event-subscription
plan: 08
subsystem: calendar-feed
tags: [xunit, integration-test, static-guard, mobile-user-agent, csrf, requirements-ledger]

requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: "84-05's log-capture harness and 29-fact feed suite, 84-06's plumbing (ICalendarSubscriptionService, the row view model, the Add/Rename/Revoke POST actions), and 84-07's Calendar Subscription section on both Profile layouts -- this plan proves the two things that only show up at the seams of all of it"
provides:
  - "ProfileCalendarSubscriptionTests: 28 facts proving both Profile layouts under their own selection mechanism (a real mobile User-Agent, not viewport emulation) -- empty state, mints-nothing-on-load, zero/one/many row shapes, per-row fetch-state independence, no pagination, the 60-char name cap, the mobile truncation class, the freshly-minted default name, and the full add/rename/revoke round trip with ownership, replay and antiforgery refusal all proven at runtime"
  - "CalendarSubscriptionStaticGuardTests: 39 facts guarding contracts no server-side integration test can see -- copy parity between the two layouts from a single string array, the destructive confirm text proven byte-identical, a forbidden-claims sweep, the 44px touch-target and name-truncation CSS rules each proven to exist AND to reach their element, the QR sizing rule proven present in both stylesheets, and a whole-repo sweep proving no requirement/phase/plan/review-finding reference reached source"
  - "REQUIREMENTS.md and ROADMAP.md closed for Phase 84: all seventeen CALFEED requirements read complete, all eight plans ticked, Plans line at 8/8"
affects: [85]

actuals:
  tokens: 9650
  tasks: 4
  commits: 5
  plan_head_before: b649b979

tech-stack:
  added: []
  patterns:
    - "Real-antiforgery variant WebApplicationFactory: WebApplicationFactoryBase installs a TestAntiforgeryDecorator that always passes validation (so every other fact in the suite can post without fetching a token), so the one fact that must prove CSRF rejection at runtime swaps the decorator back out for the framework's own real IAntiforgery via WithWebHostBuilder, then asserts on state (no mutation occurred) rather than on a specific status code, since the app's Testing-environment UseExceptionHandler(\"/Error\") re-execution makes the exact failure status unpredictable"
    - "Scoped row-label extraction over whole-page substring search: a freshly-minted subscription's default name is asserted against the row's own label element (via a targeted regex), not against the whole rendered page, because a shared modal's placeholder text legitimately coexists elsewhere on the same page once any row exists"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs
  modified:
    - QuestBoard.Service/Controllers/Admin/AccountController.cs
    - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
    - QuestBoard.Service/Views/Account/Profile.cshtml
    - QuestBoard.Service/wwwroot/css/site.css
    - QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md
    - .planning/phases/84-calendar-feed-foundation-and-event-subscription/84-UI-SPEC.md

key-decisions:
  - "Task 3 (the real-device subscription checkpoint) was DEFERRED to deployment by explicit operator decision -- not approved, not failed. It is recorded as an open item, not glossed as done: Outlook and Google Calendar fetch server-side from their own infrastructure, so no localhost or LAN address can ever satisfy them, and the check needs either a public tunnel or the deployed application. See '## Task 3: Real-Device Verification -- DEFERRED' below."
  - "The whole-page NotContain(\"e.g. My Phone\") assertion originally written for the freshly-minted-default-name fact was wrong in scope: the shared rename modal's placeholder legitimately appears on the page once any subscription row exists. Replaced with a targeted extraction of the row's own label text."
  - "The antiforgery-rejection fact could not use the plan's originally sketched status-code assertion: WebApplicationFactoryBase's TestAntiforgeryDecorator always passes validation for every other fact in this suite, so proving genuine CSRF rejection needed a variant factory with the real IAntiforgery service, and that service's failure surfaces through this app's Testing-environment UseExceptionHandler(\"/Error\") re-execution rather than a clean 400/403 -- the fact asserts on unchanged database state and the absence of the success redirect instead of a specific status code."

requirements-completed: [CALFEED-01, CALFEED-02, CALFEED-03, CALFEED-14]

coverage:
  - id: D1
    description: "Both Profile layouts are proven, under their own real selection mechanism (a mobile User-Agent header, not viewport emulation), to render the empty state, mint nothing on page load, show one row per subscription at 0/1/3 counts with no separate single-row branch, keep each row's fetch state independent, stay uncapped past 10 rows, cap the rename field at 60 characters, and truncate an overlong name on the mobile row label"
    requirement: CALFEED-14
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs (28 facts)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Add, Rename and Delete round-trip end to end through real form POSTs: exactly one subscription is minted per intent (an immediate replay is refused), a rename changes the label and refuses an empty name, delete retires the subscription (the row disappears but the address answers 410 Gone rather than 404), and neither rename nor delete can touch another member's subscription without revealing whether that id exists"
    requirement: CALFEED-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs#AddSubscription_CreatesExactlyOneSubscription_AndShowsIt, #AddSubscription_RefusesAnImmediateSecondSubmit, #RenameSubscription_ChangesTheLabel, #RenameSubscription_RefusesAnEmptyName, #RenameSubscription_DoesNothing_ForAnotherMembersSubscription, #RevokeSubscription_RemovesTheRowFromTheList_ButKeepsTheAddressAnswering, #RevokeSubscription_DoesNothing_ForAnotherMembersSubscription"
        status: pass
    human_judgment: false
  - id: D3
    description: "CSRF protection on the three mutations is proven at runtime -- not just present as an attribute -- by swapping the test harness's always-succeeds antiforgery decorator for the framework's real validator and posting with no token"
    requirement: CALFEED-03
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs#Mutations_AreRejectedWithoutAnAntiforgeryToken"
        status: pass
    human_judgment: false
  - id: D4
    description: "Every copy-contract string is proven present on both layouts from one data-driven theory, the destructive confirm text is proven byte-identical between them, no forbidden claim (duration, refresh cadence, automatic calendar naming) survives in either, the 44px touch-target and name-truncation CSS rules each exist and actually reach the element they target, the QR sizing rule exists in both stylesheets, and no requirement/phase/plan/review-finding reference reached any .cs/.cshtml/.css file across all five projects"
    requirement: CALFEED-14
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs (39 facts)"
        status: pass
    human_judgment: false
  - id: D5
    description: "A real phone subscribes to a real minted address and its own calendar application renders the events correctly"
    verification: []
    human_judgment: true
    rationale: "DEFERRED to deployment by operator decision, not run. Outlook and Google Calendar fetch server-side from their own infrastructure and cannot reach a localhost or LAN address, so this check needs either a public tunnel or the deployed application. No test in this repository can substitute for it. See '## Task 3: Real-Device Verification -- DEFERRED' below for what is and is not independently covered."
  - id: D6
    description: "All seventeen CALFEED requirements read complete in both REQUIREMENTS.md's body and traceability table, and all eight Phase 84 plans are ticked with the plan count at 8/8"
    requirement: CALFEED-01
    verification:
      - kind: other
        ref: "grep -c '^- \\[x\\] \\*\\*CALFEED-' REQUIREMENTS.md == 17; grep -c '^- \\[x\\] 84-0[1-8]-PLAN.md' ROADMAP.md == 8"
        status: pass
    human_judgment: false

duration: 137min
completed: 2026-09-18
status: complete
---

# Phase 84 Plan 8: Both-Layout Round-Trip Suite, the Phase Static Guard, and the Ledger Close-Out Summary

**67 new automated facts (28 markup/round-trip, 39 static-guard) prove both Profile layouts under a real mobile User-Agent and every contract a server-side test cannot otherwise see; the phase's one genuinely unverifiable promise -- a real phone actually subscribing -- was deferred to deployment by operator decision rather than approved, and all seventeen CALFEED requirements plus all eight plans are now closed on that basis.**

## Performance

- **Duration:** 137 min (includes a pause while the operator's local Visual Studio debugger held file locks on `QuestBoard.Service`'s output DLLs, and the round-trip through the Task 3 checkpoint)
- **Started:** ~2026-09-18T10:45:13Z
- **Completed:** 2026-09-18T13:03:05Z
- **Tasks:** 4 (2 auto, 1 checkpoint deferred, 1 auto)
- **Files modified:** 10 (2 created, 8 modified)

## Accomplishments

- `ProfileCalendarSubscriptionTests` (new, 28 facts): both Profile layouts proven under a real mobile User-Agent for the empty state, mints-nothing-on-load, zero/one/many row rendering with no separate single-row branch, per-row fetch-state independence, an uncapped list past 10 rows, the 60-character name cap, and the mobile truncation class -- plus the full add/rename/revoke round trip proving replay refusal, ownership isolation and CSRF rejection all hold at runtime
- `CalendarSubscriptionStaticGuardTests` (new, 39 facts): copy parity between both layouts driven from one data array, the destructive confirm text proven byte-identical, a forbidden-claims sweep (duration, refresh cadence, automatic calendar naming), the 44px touch-target rule and the name-truncation rule each proven to exist *and* to reach their element, the QR sizing rule proven present in both stylesheets, and a whole-repo walk proving no requirement/phase/plan/review-finding reference reached any `.cs`/`.cshtml`/`.css` file across all five projects
- Two real defects caught and fixed while writing the new coverage (Rule 1 -- see Deviations): a `NullReferenceException` on a whitespace-only rename, and a lowercase-vs-uppercase copy-parity mismatch on the mobile Show QR Code control's `aria-label`
- One pre-existing CLAUDE.md violation caught by the new static guard's own sweep and fixed: a stray "plan 84-05" reference in a `QuestBoard.UnitTests` comment
- Task 3 (the real-device subscription checkpoint) was **deferred to deployment by explicit operator decision** -- see the dedicated section below
- Three UAT-found UI defects were fixed centrally during the Task 3 review window (commits `3a8cd98f`, `a809c772`, already on this branch before this summary): modals freed from a `backdrop-filter` stacking-context trap on both layouts, the subscription row rebalanced from ~150px to 68px, and the address stopped being displayed on screen (kept in the DOM, readonly and selectable, revealed only by the clipboard-denied fallback) -- CALFEED-03 and 84-UI-SPEC E3/E4 were amended accordingly as part of those commits
- All seventeen CALFEED requirements and all eight Phase 84 plans are now closed in `REQUIREMENTS.md`/`ROADMAP.md`

## Task Commits

Each task was committed atomically:

1. **Task 1: Both-layout markup facts and the add, rename and delete round trip** - `78aa5286` (test)
2. **Task 2: The static guard for contracts no server-side test can observe** - `f5b9478d` (test)
3. **Task 3: Subscribe a real phone to a real address** - DEFERRED, no commit (see below)
4. **Task 4: Close the requirement and roadmap ledgers** - `593a8cda` (docs)

**Interim commits made during the Task 3 review window** (not authored by this task's executor, already on the branch when this plan resumed after the checkpoint):
- `3a8cd98f` (fix) - freed the modals from the card's `backdrop-filter` stacking context and tightened the row
- `a809c772` (fix) - stopped displaying the subscription address on Profile

**Plan metadata:** committed as part of this SUMMARY.

## Files Created/Modified

- `QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs` - new, 28 facts (Task 1)
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs` - new, 39 facts (Task 2)
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` - null-guarded `RenameCalendarSubscription`'s `name.Trim()` (Task 1 deviation)
- `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml` - fixed the Show QR Code `aria-label` casing (Task 1 deviation); modal/row layout changes from the interim UAT fixes
- `QuestBoard.Service/Views/Account/Profile.cshtml` - modal/row layout changes from the interim UAT fixes
- `QuestBoard.Service/wwwroot/css/site.css` - modal-positioning rules from the interim UAT fixes
- `QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs` - stripped a stray plan-number reference from a comment (Task 2 deviation)
- `.planning/REQUIREMENTS.md` - all seventeen CALFEED requirements marked complete, both in the body and the traceability table (Task 4)
- `.planning/ROADMAP.md` - Phase 84's Plans line set to 8/8, all eight plan lines ticked, one risks-block line recording the deferred real-device check (Task 4)
- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-UI-SPEC.md` - amended centrally during the interim UAT fixes (not re-applied by this plan)

## Decisions Made

See `key-decisions` in the frontmatter above. The most consequential: Task 3 is recorded as **deferred**, not approved and not failed, because a real-device check against Outlook and Google Calendar structurally cannot run against a localhost or LAN address -- it needs either a public tunnel or the deployed application, and the operator chose not to block the phase's close on standing one up right now.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `RenameCalendarSubscription` threw `NullReferenceException` on a whitespace-only name**
- **Found during:** Task 1, writing `RenameSubscription_RefusesAnEmptyName`
- **Issue:** ASP.NET Core's default model binding (`ConvertEmptyStringToNull` combined with `IsNullOrWhiteSpace`) binds an all-whitespace form field to `null` rather than to a string of spaces. `RenameCalendarSubscription` called `name.Trim()` unconditionally, so a real whitespace-only submission crashed with an unhandled exception instead of being refused through the normal "Couldn't rename this subscription" path.
- **Fix:** `var trimmedName = name?.Trim() ?? string.Empty;`
- **Files modified:** `QuestBoard.Service/Controllers/Admin/AccountController.cs`
- **Verification:** `RenameSubscription_RefusesAnEmptyName` passes; full suite green.
- **Committed in:** `78aa5286`

**2. [Rule 1 - Bug] Mobile Show QR Code control's `aria-label` silently drifted from the desktop copy**
- **Found during:** Task 1, researching the exact strings for the copy-parity theory
- **Issue:** Desktop's visible button text read "Show QR Code" (capital C); the mobile icon-only button's `aria-label` read "Show QR code" (lowercase c) -- exactly the both-layout copy-parity defect T-84-33 exists to catch, and it would have failed the Task 2 static guard's copy-parity theory once written.
- **Fix:** Corrected the mobile `aria-label` to "Show QR Code".
- **Files modified:** `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml`
- **Verification:** Static guard's `CopyContractString_AppearsOnBothLayouts` theory entry for "Show QR Code" passes on both layouts.
- **Committed in:** `78aa5286`

**3. [CLAUDE.md compliance] Stray plan-number reference in a test comment**
- **Found during:** Task 2, running the new `NoPlanningOrTrackingReference_ReachedTheSourceTree` guard against the live repo before finalizing it
- **Issue:** `QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs` carried a comment reading "...are proved end to end in plan 84-05 against real HTTP..." -- a plan-number reference in source, which CLAUDE.md forbids and this exact guard exists to catch.
- **Fix:** Reworded to "...are proved end to end against real HTTP elsewhere..." with no phase or plan reference.
- **Files modified:** `QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs`
- **Verification:** The new guard's sweep returns zero violations across all five projects; full suite green.
- **Committed in:** `f5b9478d`

### Noted, Not Fixed

**1. Task 4's `**Plans**: 8/8 plans complete` acceptance criterion cannot be satisfied as a whole-file count**
- **Found during:** Task 4 verification
- **Issue:** The plan's acceptance criterion `grep -c '^\*\*Plans\*\*: 8/8 plans complete$' .planning/ROADMAP.md` outputs `1` assumes this exact string is unique in the file. It is not: two other, unrelated, already-completed phases in `ROADMAP.md` carry the identical string for their own plan counts.
- **Resolution:** Verified Phase 84's own `**Plans**` line reads exactly `8/8 plans complete` by content (not by the raw whole-file grep count), and left the other two phases' identical, correct lines untouched -- rewriting them to force a literal count of 1 would falsify their own state for no benefit to this plan's actual claim. Logged to `.planning/WINDOWS.md` as a `deviation` entry.
- **Files modified:** None beyond this plan's own planned edit to Phase 84's line.
- **Impact:** None on correctness; only the literal whole-file grep as written cannot distinguish "Phase 84's line" from "any phase's identical line."

---

**Total deviations:** 3 auto-fixed (2 Rule 1 bugs, 1 CLAUDE.md-compliance correction), 1 noted-and-verified (1 unsatisfiable-as-literally-written acceptance-criteria grep against unrelated pre-existing content).
**Impact on plan:** All three auto-fixes are necessary for correctness, copy-parity, and CLAUDE.md compliance. No scope creep; no unrelated code changed to force a literal grep to pass.

## Task 3: Real-Device Verification -- DEFERRED

**Status: deferred to deployment by operator decision. Not approved. Not failed.**

The checkpoint asked for a real phone subscribing to a real minted address and its own calendar application rendering the events correctly -- the one thing in this phase no automated test can observe. It was not run, for a structural reason rather than a scheduling one: Outlook and Google Calendar fetch the feed **server-side** from Microsoft's and Google's own infrastructure, so neither client can ever reach a `localhost` or LAN address. Running this check for real needs either a public tunnel or the already-deployed application, and the operator chose not to block this phase's close on standing one up right now.

**What is independently covered, so this deferral is not a blank spot:**
- A live feed response was run through an external RFC 5545 conformance validator and returned **zero errors and zero warnings**. Two false positives were investigated and ruled out first: an HTML `<textarea>` normalises CRLF to LF, so pasting the document always trips the CRLF rule; and the validator itself miscounts the RFC-mandated trailing CRLF after `END:VCALENDAR` as a 279th content line. Byte-identical input without that trailing terminator validated clean. **The writer's trailing CRLF is correct per RFC 5545 section 3.1 and matches what Google, Apple and Microsoft themselves emit -- it must not be changed.**
- Server-side coverage stands at 104 test methods for this feature, including the 29 end-to-end facts (plan 84-05) covering tenancy isolation, every response code (200/404/410/429), content rules, window bounds, the fetch-time throttle, ETag/304 behaviour and the no-address-in-logs guarantee, plus this plan's 67 new facts for both Profile layouts and their shared contracts.

**What remains genuinely unverified** -- stated plainly, not glossed:
- Whether iOS Calendar, Google Calendar and Outlook actually subscribe to the address and render the events
- What iOS names the subscribed calendar (the phase never promised it would honour the published name -- Pitfall 1 in the research)
- Real-world refresh latency in each client (vendor-chosen, undocumented, and the copy deliberately makes no promise about it)
- Whether the `webcal://` handoff actually launches a calendar client when scanned or tapped on a real device
- A real camera actually scanning the QR code and resolving to a working subscription

These are client behaviours, not server contracts -- no test in this repository can assert them, and none of the automated work in this plan substitutes for the check. It is logged as an open item in `.planning/WINDOWS.md` (`unrun-verify`) and in `ROADMAP.md`'s Phase 84 risks block, so it stays visible past this summary rather than only living here.

## Issues Encountered

- A local Visual Studio debugger session held file locks on `QuestBoard.Service`'s output DLLs mid-plan, blocking `dotnet build`/`dotnet test`. Resolved once the operator stopped the debugger; no code change required.
- See "Task 3: Real-Device Verification -- DEFERRED" above for the plan's one substantive open item.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 84 is closed: all seventeen CALFEED requirements read complete, all eight plans are ticked at 8/8, and the full solution is green (508 unit + 815 integration, 0 failures).
- Phase 85 (One-Shot Quests in the Calendar Feed) depends on this phase's token, endpoint, writer and subscribe surface all existing -- they do. Phase 85's own requirements are still `TBD` and unplanned, unaffected by this plan.
- The one open item carried forward is the real-device subscription check (see above), tracked in `.planning/WINDOWS.md` as an `unrun-verify` entry and in `ROADMAP.md`'s Phase 84 risks block. It should be run against the deployed application (or a tunnel) before treating the phone-subscription promise as proven, even though everything the server controls is green and the document itself validates clean.
- No stubs were introduced by this plan. The `Mutations_AreRejectedWithoutAnAntiforgeryToken` fact required a variant `WebApplicationFactory` with the real antiforgery service (documented in `key-decisions` and inline in the test file) rather than the shared harness's always-succeeds decorator -- that pattern is now available to any future suite that needs to prove CSRF rejection at runtime rather than merely by attribute inspection.

## Threat Flags

| Flag | File | Description |
|------|------|--------------|
| threat_flag: deferred-mitigation | `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-08-PLAN.md` (Task 3) | T-84-25's accepted-risk disposition names the blocking real-device checkpoint as what discharges "what an in-memory harness and a markup suite cannot prove." That checkpoint was deferred rather than run, so the threat's acceptance is not yet backed by the evidence the threat register itself names -- tracked as an open `unrun-verify` item in `.planning/WINDOWS.md` rather than closed. |

## Self-Check: PASSED

- `QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs` - FOUND
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs` - FOUND
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` - FOUND
- `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml` - FOUND
- `QuestBoard.Service/Views/Account/Profile.cshtml` - FOUND
- `QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs` - FOUND
- `.planning/REQUIREMENTS.md` - FOUND
- `.planning/ROADMAP.md` - FOUND
- Commit `78aa5286` - FOUND in `git log --oneline --all`
- Commit `f5b9478d` - FOUND in `git log --oneline --all`
- Commit `3a8cd98f` - FOUND in `git log --oneline --all`
- Commit `a809c772` - FOUND in `git log --oneline --all`
- Commit `593a8cda` - FOUND in `git log --oneline --all`
- `dotnet test QuestBoard.IntegrationTests --filter ProfileCalendarSubscriptionTests` - PASSED (28/28)
- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionStaticGuardTests` - PASSED (39/39)
- `dotnet test` (full solution) - PASSED (508 unit + 815 integration, 0 failures)
- All plan-level `<acceptance_criteria>` re-verified after each task's commit: all PASSED except the one documented, unsatisfiable-as-literally-written whole-file grep (see Deviations, "Noted, Not Fixed")
- `grep -c '^- \[x\] \*\*CALFEED-' .planning/REQUIREMENTS.md` outputs `17`; `grep -c '^- \[ \] \*\*CALFEED-'` outputs `0`
- `grep -c '^- \[x\] 84-0[1-8]-PLAN.md' .planning/ROADMAP.md` outputs `8`
- No requirement id, phase number or plan number found in any file this plan touched (enforced by this plan's own new static guard)

---
*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Completed: 2026-09-18*
