---
schema_version: 1
open_count: 18
waived_count: 1
fixed_count: 7
total_count: 26
last_updated: 2026-11-06T16:16:26.365Z
---

# Broken Windows Ledger

> Cross-phase defect register. With `workflow.windows_enforce` enabled, `/gsd-ship` blocks while `open_count > 0`.
> Waive with `gsd-tools windows waive <id> "<reason>"` (reason required).
> Mark fixed with `gsd-tools windows fixed <id>`.

| id | phase | kind | file | line | description | status | reason | recorded_at | resolved_at |
|----|-------|------|------|------|-------------|--------|--------|-------------|-------------|
| 1 | 84 | deviation | .planning/ROADMAP.md |  | 84-01 Task 2 ROADMAP.md edits (Requirements line + 16 Coverage rows) computed and verified but not committed - worktree harness blocks commits touching ROADMAP.md; orchestrator must apply after wave merge (see 84-01-SUMMARY.md) | fixed |  | 2026-09-17T20:23:58.316Z | 2026-09-18T07:10:44.090Z |
| 2 | 84 | deviation | QuestBoard.Service/Controllers/Admin/AccountController.cs |  | Task 3 acceptance criterion 'grep -cE Request.Scheme\|Request.Host outputs 0' is unsatisfiable without touching two pre-existing, unrelated Url.Action(..., Request.Scheme) call sites in ForgotPassword/Edit (email callback URLs) - out of this plan's scope, left as-is | open |  | 2026-09-18T09:38:07.149Z |  |
| 3 | 84 | unrun-verify | .planning/phases/84-calendar-feed-foundation-and-event-subscription/84-08-PLAN.md |  | Task 3's real-device checkpoint (real phone subscribing via iOS Calendar/Google Calendar/Outlook, calendar naming, refresh latency, stale-entry check, camera QR scan) was deferred to deployment by operator decision, not run or approved -- server-side coverage and an external RFC 5545 validator pass do not substitute for it. | open |  | 2026-09-18T13:02:54.169Z |  |
| 4 | 84 | deviation | .planning/ROADMAP.md |  | 84-08 Task 4 acceptance criterion 'grep -c ^**Plans**: 8/8 plans complete$ outputs 1' is unsatisfiable as literally written -- two other completed phases in ROADMAP.md already carry the identical string; Phase 84's own Plans line was correctly set to 8/8 plans complete, verified by content not by the raw whole-file count | open |  | 2026-09-18T13:03:05.007Z |  |
| 5 | 85 | unrun-verify | .planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md |  | Relational SQL translation of the one-shot-quest predicate (seat-or-Dungeon-Master disjunction + board-type narrowing) has no automated relational coverage -- the third consecutive phase to defer this gap. Every integration fact runs on the EF Core InMemory provider. UPDATE 2026-09-18: the compensating manual check WAS performed against real SQL Server 2022 on the real dev dataset -- the predicate translated and executed relationally, no client-side-evaluation exception, and the served quest-id set matched an independently written SQL query exactly; evidence in 85-VALIDATION.md 'Manual verification performed'. This stays OPEN because the deferred item is relational test INFRASTRUCTURE (a SQL Server test fixture CI can run), not a single hand-run observation of one query shape. | open |  | 2026-09-18T17:11:30.295Z |  |
| 6 | 85 | deviation | QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs |  | NoPlanningOrTrackingReference_ReachedTheSourceTree fails on Linux dev environments: its ResolveRepoFile helper's existence-based repo-root walk stops early because the Linux apphost binary (bare name QuestBoard.Service, no extension) collides with the project-folder name it is looking for. Predates this phase (Phase 84, commit 78aa5286); this plan's Task 1 was explicitly instructed to leave the planning-reference guard exactly as it is, so it was not fixed here. Causes 'dotnet test' to exit 1 for the whole solution on Linux even though every fact this phase added passes individually. See deferred-items.md item 1. | fixed |  | 2026-09-18T17:11:39.383Z | 2026-09-18T18:46:48.793Z |
| 7 | 94 | deviation | QuestBoard.Service/wwwroot/lib/cinzel/cinzel-latin-wght.woff2 |  | 94-11 Task 2 zero-difference gate printed FAIL: all 7 desktop gate pages 0 px, all 7 mobile pages 4.5k-9.5k px confined to the Cinzel brand/heading edges. Google Fonts serves an Android UA a differently hinted build of the same Cinzel than a desktop UA; the self-hosted files are the desktop-UA build. Orchestrator reviewed the overlay at 3x: identical glyph shapes and weight, edge anti-aliasing plus a sub-pixel advance shift only, brand box unchanged 198x46 CSS px - judged not visible and accepted. Strict-zero alternative (both builds, selected by @media (hover:none) and (pointer:coarse), +~40 KB) not taken. Revisit only if the user objects; commit 0a5604ac. | open |  | 2026-11-01T14:12:21.045Z |  |
| 8 | 94 | deviation | ~/qb-ui-review/phase-94/tools/probes.mjs (scratch) |  | 94-16 found the shared contrast probe flattens a multi-layer glass background and orders the colour above its own image, giving 134 false failures on Direction A through audit-shoot --probes. 94-16 used a corrected package-local copy (mockups/package/probe-lib.mjs). The wave-3 area audits scored legacy glass with the shared probe, so any S1/S2 contrast finding measured on a multi-layer glass background may be a false positive; re-check those before an area phase acts on them. | open |  | 2026-11-01T15:35:45.120Z |  |
| 9 | 94 | deviation | QuestBoard.Service/Views/Calendar/Index.cshtml |  | Calendar month view (Events area phase): operator-accepted trade-off of the fit-without-scrolling requirement - compact entry rows and the '+N more' button are 20px tall, under the 24px WCAG 2.5.8 target-size minimum; titles truncate at about 13-16 characters (full title in the hover/focus card); footer sits below the fold. Meeting 2.5.8 would cost one visible entry per day cell. Recorded in the UI-SPEC G1 sign-off record (G1-O3). | waived | Operator decision 2026-11-01: fitting the whole month on one screen takes priority over the 24px target size; accepted after trying the calendar-fit mockup | 2026-11-01T17:34:27.324Z | 2026-11-01T17:34:27.469Z |
| 10 | 94 | unmet-truth | QuestBoard.Service/wwwroot/css/tokens.css |  | Signed-off status text colours (success, danger, warning, info) measure 4.23-4.31:1 on HOVERED glass at B-max (~4.46 at B-99), under 4.5:1; the G1 sign-off never scored status text on the hover surface. 94-18 kept the signed-off values and the token-contrast guard lists these four pairs as an explicit, reasoned exclusion. Resolve in the component layer (94-20): prefer easing the hover glass tint so status text reaches 4.5:1 without changing the signed-off status colours; otherwise lighten the status text tokens minimally (e.g. success ~#8ce4b0 = 4.62) and re-score. Then remove the guard exclusion. | fixed |  | 2026-11-01T17:43:22.717Z | 2026-11-01T19:59:39.260Z |
| 11 | 94 | deviation | QuestBoard.Service/wwwroot/css/base.css |  | Heading and strong type scale (h1 display step, h2-h3 heading step, h4-h6 body step at 600) written in 94-19 as commit 21c68026, then reverted by the orchestrator (14bc9dcb): at the scale's sizes the cream headings on today's unscrimmed legacy glass count as normal text and measure 3.91-4.34:1, so the commit added 132 new readability failures and shrank page titles 32px->20px on every page. Re-land in 94-24 by cherry-picking 21c68026 once modern-card.css is retired and the scrimmed card surface is live, inside that plan's full readability sweep against BEFORE. | open |  | 2026-11-01T18:59:59.308Z |  |
| 12 | 94 | unmet-truth | QuestBoard.Service/wwwroot/css/components.css |  | After 94-19's re-theme, ten failures already present in BEFORE measure lower (e.g. phone form required asterisks 4.34->3.36, Quest/Manage success text 4.34->3.3): the light status-text tokens now reach Bootstrap but were designed for the scrimmed surface, which legacy glass does not have yet. Not new failures, so the comparison passes; resolve when the scrimmed card surface goes live (94-20 component layer, then 94-24 retiring modern-card.css) and confirm in 94-24's sweep against BEFORE. | open |  | 2026-11-01T18:59:59.408Z |  |
| 13 | 94 | deviation | QuestBoard.Service/wwwroot/css/modern-card.css |  | modern-card.css:165 keeps an !important blue box-shadow on .modern-card .form-control:focus that beats the base layer's two-tone focus ring's gold inner band on card form inputs (the dark outer stroke still draws, so focus stays visible). Retire with modern-card.css in 94-24 and re-check the ring on a card input. | open |  | 2026-11-01T18:59:59.509Z |  |
| 14 | 94 | deviation | ~/qb-ui-review/phase-94/tools/readability-compare.py |  | Shared readability-compare keyed a contrast failure with no text sample (icon, glyph) by font size AND colour, so an element that already failed and only changed colour read as new (7 false rows on 94-19's layering). The orchestrator re-keyed it by size only (backup readability-compare.py.pre-94-19); bump, layering and base-layer pairs all PASS with it. Distinct from entry 8 (probe flattening multi-layer glass). | fixed |  | 2026-11-01T18:59:59.611Z | 2026-11-01T19:00:03.201Z |
| 15 | 94 | deviation | QuestBoard.Service/wwwroot/css/site.css |  | site.css's bare 'div { background: transparent; }' outranks every layered background on a div on the desktop layouts (Bootstrap alert, modal-content, card, toast). 94-20 restated '.toast { background-color: var(--bs-toast-bg) }' in modern-card.css as a stopgap. 94-23 already deletes the div rule (check the Quest Log exception right below it); the toast restatement goes with modern-card.css in 94-24. | open |  | 2026-11-01T19:59:39.366Z |  |
| 16 | 94 | deviation | QuestBoard.Service/wwwroot/css/components.css |  | The phone 20px page-title step on .modern-card-header ('.mobile-layout .modern-card-header :is(h1, h2) { font-size: var(--qb-text-lg) }') was held back in 94-20: on unscrimmed legacy glass it produced 5 new failures at 3.91 (same cause as entry 11). Re-land in 94-24 together with the heading scale (entry 11). The desktop 28px card title stays (large text, no regression). | open |  | 2026-11-01T19:59:39.478Z |  |
| 17 | 94 | deviation | QuestBoard.Service/wwwroot/css/components.css |  | Alerts ship as a parchment panel with ink text and a status stripe rather than Direction A's dark overlay panel: the unlayered legacy heading colour rules paint alert headings dark (a dark h6.alert-heading measured 1.15:1 on the dark panel). The dark alert can return once 94-23 retires the global heading colour rules; revisit in 94-24's operator review. | open |  | 2026-11-01T19:59:39.612Z |  |
| 18 | 94 | deviation | QuestBoard.Service/wwwroot/css/components.css |  | On paper surfaces (tables, alerts, list rows) a status colour used as TEXT now renders as ink: the status fills measure 4.24-4.32:1 on parchment (amber 1.68:1) and the light text variants are made for glass, so the icon and the word carry the status there. Guard ComponentsCss_MapsBootstrapVariablesPerSurface updated to match. | open |  | 2026-11-01T19:59:39.713Z |  |
| 19 | 94 | deviation | ~/qb-ui-review/phase-94/tools/readability-compare.py |  | After 94-20, the shared comparison still flagged 10 pre-existing failures whose only change was font size (badges 12->14px, form labels 16->14px; identical ratios). The orchestrator re-keyed sample-less failures by selector and bar only (multiset, so a real new failure on the same selector still raises the count). All pairs so far (bump, layering, base, p20-after, p20-final) PASS. | fixed |  | 2026-11-01T19:59:39.818Z | 2026-11-01T19:59:43.664Z |
| 20 | 94 | deviation | QuestBoard.Service/wwwroot/css/site.css |  | Widens entry 15: besides the bare div rule, site.css's unlayered 'ul, ol, li { background: none }' and 'p, li, span, small { color; text-shadow }' rules also beat the layered shell. 94-21 routed menu fills through --qb-menu-fill painted by one small unlayered '.chrome .navbar-collapse, .chrome .dropdown-menu' rule in site.css, and changed the footer text, GroupPicker brand and Platform user name from span to div. 94-23 must retire the ul/ol/li background reset and the p/li/span/small colour rule with the div rule, and delete that restatement rule in the same change. | fixed |  | 2026-11-01T20:55:12.633Z | 2026-11-06T16:16:26.157Z |
| 21 | 94 | deviation | QuestBoard.Service/Views/Shared/_Layout.cshtml |  | Main desktop navbar collapses below 1200px (navbar-expand-xl, was lg) with a compact link step from 1200 to 1399px: the fullest menu needs about 1326px in one row, and the legacy bar wrapped to 77px at 1200 and 101px at 992, covering content. Windows 992-1199px wide now get the hamburger menu. Also removed text-danger from the Admin and Dungeon Master toggles (it already computed parchment and its important colour blocked the gold hover). Surface in 94-24's operator review. | open |  | 2026-11-01T20:55:12.732Z |  |
| 22 | 94 | deviation | QuestBoard.Service/wwwroot/css/tokens.css |  | Board image is now '--qb-board-image: var(--qb-board-photo, none)' with shell.css setting ':root { --qb-board-photo: url(notice-board-1600.webp) }' (tokens.css edited, not on 94-21's list) so a Shop theme on <html> is not overridden by a body-level value. 94-22 must set the 900px phone photo through --qb-board-photo on :root, not 'body { --qb-board-image }'. base.css also makes the body fill transparent (Bootstrap's white body had hidden the backdrop), so phone pages already show the board via mobile.css's own 3 MB JPEG until 94-22 replaces it. | open |  | 2026-11-01T20:55:12.835Z |  |
| 23 | 94 | deviation | QuestBoard.Service/wwwroot/css/mobile.css |  | Phone side of entries 15/20: the phone layouts never load site.css, so only mobile.css's unlayered h1-h6 colour rule beats the layered shell there. 94-22 worked around it by changing the offcanvas title from h5 to div.h5 on both phone layouts (no restatement rule added). 94-23 retires that mobile.css heading rule; the div.h5 may then stay or return to h5. | fixed |  | 2026-11-01T21:28:16.779Z | 2026-11-06T16:16:26.261Z |
| 24 | 94 | deviation | QuestBoard.Service/wwwroot/css/quests.mobile.css |  | Page-owned links under the 44px phone target (the shell's 44px rule covers controls only): /quests has 6 a.row-nav-link quest titles at 27px (quests.mobile.css); /Platform/Users has 43 a.email-link links at 22px. Left to the Quests and Platform area phases; include in the G2 cut's per-area findings. | open |  | 2026-11-01T21:28:16.878Z |  |
| 25 | 94 | deviation | QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml |  | Phone shell structure: the top bar is a div inside <header> (not a nav), so each phone layout has exactly one named nav (the offcanvas body); <main> gained flex-grow-1 because the important mt-auto utility beat the footer clearance; site.js gained 13 lines keeping aria-expanded on offcanvas togglers in step (Bootstrap never updates it on the opening button). The 900px backdrop is upscaled about 1.4x on tall phones and looks slightly soft; surface in 94-24's operator review. | open |  | 2026-11-01T21:28:16.980Z |  |
| 26 | 94 | deviation | QuestBoard.Service/wwwroot/css/legacy-pages.css |  | Entry 15 narrowed by 94-23: the bare div reset, the .container reset and the Quest Log exception are gone (no Quest Log regression in the sweeps). Only modern-card.css's '.toast { background-color: var(--bs-toast-bg) }' restatement remains; delete it with modern-card.css in 94-24, then close entry 15. Also for 94-24: nothing colours .alert-heading any more, so the dark-panel alert (entry 17) may now pass; confirm visually at the operator review. mobile.css keeps 'h1-h6 { font-weight: bold }' as a stopgap until the heading scale (entry 11) lands. | open |  | 2026-11-06T16:16:26.365Z |  |

````json
[
  {
    "id": 1,
    "kind": "deviation",
    "phase": "84",
    "file": ".planning/ROADMAP.md",
    "line": null,
    "description": "84-01 Task 2 ROADMAP.md edits (Requirements line + 16 Coverage rows) computed and verified but not committed - worktree harness blocks commits touching ROADMAP.md; orchestrator must apply after wave merge (see 84-01-SUMMARY.md)",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-09-17T20:23:58.316Z",
    "resolved_at": "2026-09-18T07:10:44.090Z"
  },
  {
    "id": 2,
    "kind": "deviation",
    "phase": "84",
    "file": "QuestBoard.Service/Controllers/Admin/AccountController.cs",
    "line": null,
    "description": "Task 3 acceptance criterion 'grep -cE Request.Scheme|Request.Host outputs 0' is unsatisfiable without touching two pre-existing, unrelated Url.Action(..., Request.Scheme) call sites in ForgotPassword/Edit (email callback URLs) - out of this plan's scope, left as-is",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T09:38:07.149Z",
    "resolved_at": null
  },
  {
    "id": 3,
    "kind": "unrun-verify",
    "phase": "84",
    "file": ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-08-PLAN.md",
    "line": null,
    "description": "Task 3's real-device checkpoint (real phone subscribing via iOS Calendar/Google Calendar/Outlook, calendar naming, refresh latency, stale-entry check, camera QR scan) was deferred to deployment by operator decision, not run or approved -- server-side coverage and an external RFC 5545 validator pass do not substitute for it.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T13:02:54.169Z",
    "resolved_at": null
  },
  {
    "id": 4,
    "kind": "deviation",
    "phase": "84",
    "file": ".planning/ROADMAP.md",
    "line": null,
    "description": "84-08 Task 4 acceptance criterion 'grep -c ^**Plans**: 8/8 plans complete$ outputs 1' is unsatisfiable as literally written -- two other completed phases in ROADMAP.md already carry the identical string; Phase 84's own Plans line was correctly set to 8/8 plans complete, verified by content not by the raw whole-file count",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T13:03:05.007Z",
    "resolved_at": null
  },
  {
    "id": 5,
    "kind": "unrun-verify",
    "phase": "85",
    "file": ".planning/phases/85-one-shot-quests-in-the-calendar-feed/85-VALIDATION.md",
    "line": null,
    "description": "Relational SQL translation of the one-shot-quest predicate (seat-or-Dungeon-Master disjunction + board-type narrowing) has no automated relational coverage -- the third consecutive phase to defer this gap. Every integration fact runs on the EF Core InMemory provider. UPDATE 2026-09-18: the compensating manual check WAS performed against real SQL Server 2022 on the real dev dataset -- the predicate translated and executed relationally, no client-side-evaluation exception, and the served quest-id set matched an independently written SQL query exactly; evidence in 85-VALIDATION.md 'Manual verification performed'. This stays OPEN because the deferred item is relational test INFRASTRUCTURE (a SQL Server test fixture CI can run), not a single hand-run observation of one query shape.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-09-18T17:11:30.295Z",
    "resolved_at": null
  },
  {
    "id": 6,
    "kind": "deviation",
    "phase": "85",
    "file": "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs",
    "line": null,
    "description": "NoPlanningOrTrackingReference_ReachedTheSourceTree fails on Linux dev environments: its ResolveRepoFile helper's existence-based repo-root walk stops early because the Linux apphost binary (bare name QuestBoard.Service, no extension) collides with the project-folder name it is looking for. Predates this phase (Phase 84, commit 78aa5286); this plan's Task 1 was explicitly instructed to leave the planning-reference guard exactly as it is, so it was not fixed here. Causes 'dotnet test' to exit 1 for the whole solution on Linux even though every fact this phase added passes individually. See deferred-items.md item 1.",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-09-18T17:11:39.383Z",
    "resolved_at": "2026-09-18T18:46:48.793Z"
  },
  {
    "id": 7,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/lib/cinzel/cinzel-latin-wght.woff2",
    "line": null,
    "description": "94-11 Task 2 zero-difference gate printed FAIL: all 7 desktop gate pages 0 px, all 7 mobile pages 4.5k-9.5k px confined to the Cinzel brand/heading edges. Google Fonts serves an Android UA a differently hinted build of the same Cinzel than a desktop UA; the self-hosted files are the desktop-UA build. Orchestrator reviewed the overlay at 3x: identical glyph shapes and weight, edge anti-aliasing plus a sub-pixel advance shift only, brand box unchanged 198x46 CSS px - judged not visible and accepted. Strict-zero alternative (both builds, selected by @media (hover:none) and (pointer:coarse), +~40 KB) not taken. Revisit only if the user objects; commit 0a5604ac.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T14:12:21.045Z",
    "resolved_at": null
  },
  {
    "id": 8,
    "kind": "deviation",
    "phase": "94",
    "file": "~/qb-ui-review/phase-94/tools/probes.mjs (scratch)",
    "line": null,
    "description": "94-16 found the shared contrast probe flattens a multi-layer glass background and orders the colour above its own image, giving 134 false failures on Direction A through audit-shoot --probes. 94-16 used a corrected package-local copy (mockups/package/probe-lib.mjs). The wave-3 area audits scored legacy glass with the shared probe, so any S1/S2 contrast finding measured on a multi-layer glass background may be a false positive; re-check those before an area phase acts on them.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T15:35:45.120Z",
    "resolved_at": null
  },
  {
    "id": 9,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/Views/Calendar/Index.cshtml",
    "line": null,
    "description": "Calendar month view (Events area phase): operator-accepted trade-off of the fit-without-scrolling requirement - compact entry rows and the '+N more' button are 20px tall, under the 24px WCAG 2.5.8 target-size minimum; titles truncate at about 13-16 characters (full title in the hover/focus card); footer sits below the fold. Meeting 2.5.8 would cost one visible entry per day cell. Recorded in the UI-SPEC G1 sign-off record (G1-O3).",
    "status": "waived",
    "reason": "Operator decision 2026-11-01: fitting the whole month on one screen takes priority over the 24px target size; accepted after trying the calendar-fit mockup",
    "recorded_at": "2026-11-01T17:34:27.324Z",
    "resolved_at": "2026-11-01T17:34:27.469Z"
  },
  {
    "id": 10,
    "kind": "unmet-truth",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/tokens.css",
    "line": null,
    "description": "Signed-off status text colours (success, danger, warning, info) measure 4.23-4.31:1 on HOVERED glass at B-max (~4.46 at B-99), under 4.5:1; the G1 sign-off never scored status text on the hover surface. 94-18 kept the signed-off values and the token-contrast guard lists these four pairs as an explicit, reasoned exclusion. Resolve in the component layer (94-20): prefer easing the hover glass tint so status text reaches 4.5:1 without changing the signed-off status colours; otherwise lighten the status text tokens minimally (e.g. success ~#8ce4b0 = 4.62) and re-score. Then remove the guard exclusion.",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-11-01T17:43:22.717Z",
    "resolved_at": "2026-11-01T19:59:39.260Z"
  },
  {
    "id": 11,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/base.css",
    "line": null,
    "description": "Heading and strong type scale (h1 display step, h2-h3 heading step, h4-h6 body step at 600) written in 94-19 as commit 21c68026, then reverted by the orchestrator (14bc9dcb): at the scale's sizes the cream headings on today's unscrimmed legacy glass count as normal text and measure 3.91-4.34:1, so the commit added 132 new readability failures and shrank page titles 32px->20px on every page. Re-land in 94-24 by cherry-picking 21c68026 once modern-card.css is retired and the scrimmed card surface is live, inside that plan's full readability sweep against BEFORE.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T18:59:59.308Z",
    "resolved_at": null
  },
  {
    "id": 12,
    "kind": "unmet-truth",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/components.css",
    "line": null,
    "description": "After 94-19's re-theme, ten failures already present in BEFORE measure lower (e.g. phone form required asterisks 4.34->3.36, Quest/Manage success text 4.34->3.3): the light status-text tokens now reach Bootstrap but were designed for the scrimmed surface, which legacy glass does not have yet. Not new failures, so the comparison passes; resolve when the scrimmed card surface goes live (94-20 component layer, then 94-24 retiring modern-card.css) and confirm in 94-24's sweep against BEFORE.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T18:59:59.408Z",
    "resolved_at": null
  },
  {
    "id": 13,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/modern-card.css",
    "line": null,
    "description": "modern-card.css:165 keeps an !important blue box-shadow on .modern-card .form-control:focus that beats the base layer's two-tone focus ring's gold inner band on card form inputs (the dark outer stroke still draws, so focus stays visible). Retire with modern-card.css in 94-24 and re-check the ring on a card input.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T18:59:59.509Z",
    "resolved_at": null
  },
  {
    "id": 14,
    "kind": "deviation",
    "phase": "94",
    "file": "~/qb-ui-review/phase-94/tools/readability-compare.py",
    "line": null,
    "description": "Shared readability-compare keyed a contrast failure with no text sample (icon, glyph) by font size AND colour, so an element that already failed and only changed colour read as new (7 false rows on 94-19's layering). The orchestrator re-keyed it by size only (backup readability-compare.py.pre-94-19); bump, layering and base-layer pairs all PASS with it. Distinct from entry 8 (probe flattening multi-layer glass).",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-11-01T18:59:59.611Z",
    "resolved_at": "2026-11-01T19:00:03.201Z"
  },
  {
    "id": 15,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/site.css",
    "line": null,
    "description": "site.css's bare 'div { background: transparent; }' outranks every layered background on a div on the desktop layouts (Bootstrap alert, modal-content, card, toast). 94-20 restated '.toast { background-color: var(--bs-toast-bg) }' in modern-card.css as a stopgap. 94-23 already deletes the div rule (check the Quest Log exception right below it); the toast restatement goes with modern-card.css in 94-24.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T19:59:39.366Z",
    "resolved_at": null
  },
  {
    "id": 16,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/components.css",
    "line": null,
    "description": "The phone 20px page-title step on .modern-card-header ('.mobile-layout .modern-card-header :is(h1, h2) { font-size: var(--qb-text-lg) }') was held back in 94-20: on unscrimmed legacy glass it produced 5 new failures at 3.91 (same cause as entry 11). Re-land in 94-24 together with the heading scale (entry 11). The desktop 28px card title stays (large text, no regression).",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T19:59:39.478Z",
    "resolved_at": null
  },
  {
    "id": 17,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/components.css",
    "line": null,
    "description": "Alerts ship as a parchment panel with ink text and a status stripe rather than Direction A's dark overlay panel: the unlayered legacy heading colour rules paint alert headings dark (a dark h6.alert-heading measured 1.15:1 on the dark panel). The dark alert can return once 94-23 retires the global heading colour rules; revisit in 94-24's operator review.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T19:59:39.612Z",
    "resolved_at": null
  },
  {
    "id": 18,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/components.css",
    "line": null,
    "description": "On paper surfaces (tables, alerts, list rows) a status colour used as TEXT now renders as ink: the status fills measure 4.24-4.32:1 on parchment (amber 1.68:1) and the light text variants are made for glass, so the icon and the word carry the status there. Guard ComponentsCss_MapsBootstrapVariablesPerSurface updated to match.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T19:59:39.713Z",
    "resolved_at": null
  },
  {
    "id": 19,
    "kind": "deviation",
    "phase": "94",
    "file": "~/qb-ui-review/phase-94/tools/readability-compare.py",
    "line": null,
    "description": "After 94-20, the shared comparison still flagged 10 pre-existing failures whose only change was font size (badges 12->14px, form labels 16->14px; identical ratios). The orchestrator re-keyed sample-less failures by selector and bar only (multiset, so a real new failure on the same selector still raises the count). All pairs so far (bump, layering, base, p20-after, p20-final) PASS.",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-11-01T19:59:39.818Z",
    "resolved_at": "2026-11-01T19:59:43.664Z"
  },
  {
    "id": 20,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/site.css",
    "line": null,
    "description": "Widens entry 15: besides the bare div rule, site.css's unlayered 'ul, ol, li { background: none }' and 'p, li, span, small { color; text-shadow }' rules also beat the layered shell. 94-21 routed menu fills through --qb-menu-fill painted by one small unlayered '.chrome .navbar-collapse, .chrome .dropdown-menu' rule in site.css, and changed the footer text, GroupPicker brand and Platform user name from span to div. 94-23 must retire the ul/ol/li background reset and the p/li/span/small colour rule with the div rule, and delete that restatement rule in the same change.",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-11-01T20:55:12.633Z",
    "resolved_at": "2026-11-06T16:16:26.157Z"
  },
  {
    "id": 21,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/Views/Shared/_Layout.cshtml",
    "line": null,
    "description": "Main desktop navbar collapses below 1200px (navbar-expand-xl, was lg) with a compact link step from 1200 to 1399px: the fullest menu needs about 1326px in one row, and the legacy bar wrapped to 77px at 1200 and 101px at 992, covering content. Windows 992-1199px wide now get the hamburger menu. Also removed text-danger from the Admin and Dungeon Master toggles (it already computed parchment and its important colour blocked the gold hover). Surface in 94-24's operator review.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T20:55:12.732Z",
    "resolved_at": null
  },
  {
    "id": 22,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/tokens.css",
    "line": null,
    "description": "Board image is now '--qb-board-image: var(--qb-board-photo, none)' with shell.css setting ':root { --qb-board-photo: url(notice-board-1600.webp) }' (tokens.css edited, not on 94-21's list) so a Shop theme on <html> is not overridden by a body-level value. 94-22 must set the 900px phone photo through --qb-board-photo on :root, not 'body { --qb-board-image }'. base.css also makes the body fill transparent (Bootstrap's white body had hidden the backdrop), so phone pages already show the board via mobile.css's own 3 MB JPEG until 94-22 replaces it.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T20:55:12.835Z",
    "resolved_at": null
  },
  {
    "id": 23,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/mobile.css",
    "line": null,
    "description": "Phone side of entries 15/20: the phone layouts never load site.css, so only mobile.css's unlayered h1-h6 colour rule beats the layered shell there. 94-22 worked around it by changing the offcanvas title from h5 to div.h5 on both phone layouts (no restatement rule added). 94-23 retires that mobile.css heading rule; the div.h5 may then stay or return to h5.",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-11-01T21:28:16.779Z",
    "resolved_at": "2026-11-06T16:16:26.261Z"
  },
  {
    "id": 24,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/quests.mobile.css",
    "line": null,
    "description": "Page-owned links under the 44px phone target (the shell's 44px rule covers controls only): /quests has 6 a.row-nav-link quest titles at 27px (quests.mobile.css); /Platform/Users has 43 a.email-link links at 22px. Left to the Quests and Platform area phases; include in the G2 cut's per-area findings.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T21:28:16.878Z",
    "resolved_at": null
  },
  {
    "id": 25,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
    "line": null,
    "description": "Phone shell structure: the top bar is a div inside <header> (not a nav), so each phone layout has exactly one named nav (the offcanvas body); <main> gained flex-grow-1 because the important mt-auto utility beat the footer clearance; site.js gained 13 lines keeping aria-expanded on offcanvas togglers in step (Bootstrap never updates it on the opening button). The 900px backdrop is upscaled about 1.4x on tall phones and looks slightly soft; surface in 94-24's operator review.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-01T21:28:16.980Z",
    "resolved_at": null
  },
  {
    "id": 26,
    "kind": "deviation",
    "phase": "94",
    "file": "QuestBoard.Service/wwwroot/css/legacy-pages.css",
    "line": null,
    "description": "Entry 15 narrowed by 94-23: the bare div reset, the .container reset and the Quest Log exception are gone (no Quest Log regression in the sweeps). Only modern-card.css's '.toast { background-color: var(--bs-toast-bg) }' restatement remains; delete it with modern-card.css in 94-24, then close entry 15. Also for 94-24: nothing colours .alert-heading any more, so the dark-panel alert (entry 17) may now pass; confirm visually at the operator review. mobile.css keeps 'h1-h6 { font-weight: bold }' as a stopgap until the heading scale (entry 11) lands.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-06T16:16:26.365Z",
    "resolved_at": null
  }
]
````
