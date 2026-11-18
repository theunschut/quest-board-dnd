---
phase: "100"
slug: "admin-and-platform-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-18"
---

# Phase 100 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `100-RESEARCH.md` § "Validation Architecture" (copied below; the plans' `<verify>` blocks cite these commands).

---

## Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 + FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF Core InMemory (no database) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run (integration) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminPlatform\|FullyQualifiedName~AdminAccountReach\|FullyQualifiedName~AdminRoleGuard\|FullyQualifiedName~AdminQuestDelete\|FullyQualifiedName~EmailStats\|FullyQualifiedName~AdminControllerIntegrationTests\|FullyQualifiedName~AdminControllerRateLimitIntegrationTests\|FullyQualifiedName~AdminHandlerIntegrationTests\|FullyQualifiedName~UsersControllerIntegrationTests\|FullyQualifiedName~GroupManagementIntegrationTests\|FullyQualifiedName~LinkedBoardsPlatformIntegrationTests\|FullyQualifiedName~Platform\|FullyQualifiedName~BoardDeletion\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~MobileCssTests\|FullyQualifiedName~QuestStatusBoardClockTests\|FullyQualifiedName~BoardPageStaticGuardTests\|FullyQualifiedName~BoardPageGateMatrixTests\|FullyQualifiedName~BoardPageCoverageTests\|FullyQualifiedName~AntiForgeryTokenCoverageTests\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~KeyboardScriptGuardTests\|FullyQualifiedName~QuestsConversionGuardTests"` |
| Quick run (unit) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~UserService\|FullyQualifiedName~GroupService\|FullyQualifiedName~QuestService\|FullyQualifiedName~ResendStats\|FullyQualifiedName~LinkedBoardSetService\|FullyQualifiedName~AmbientClockSeamTests"` |
| Full suite | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (separately, avoids the CLR flake). SQL-backed tests need `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and SQL at 1433; once at the end, on the operator's yes |
| Browser checks | Scratch Playwright under `~/qb-ui-review/phase-100/` (desktop 1440x900; phone `devices['Pixel 7']` with touch); probes copied from `phase-94/audit/admin/` |
| Estimated runtime | about 20 s quick after build, 2 to 3 minutes full |

Notes: re-run once on `0x80131506`; never set GC environment variables; stop the running app before build or test; stage files explicitly; no watch mode.

## Phase Requirements to Test Map

| Req | Behavior | Test Type | Automated Command | File Exists? |
|-----|----------|-----------|-------------------|-------------|
| D-01 | Every gap has a reproducing fact (red before its fix, green after) and a row in the security document | integration + unit | `~AdminAccountReach`, `~AdminRoleGuard`, `~AdminQuestDelete` | ❌ Wave 0 |
| D-02 | Board Admin refused (Error toast, nothing changed) for EditUser GET/POST, ResetPassword GET/POST, SendConfirmationEmail on a SuperAdmin and on an Admin of another board; allowed for Player, DM, same-board Admin; SuperAdmin acts on anyone; chain through `CreateUser` by email; `UserService.ResetPasswordAsync` refuses at service level; rows render disabled with a visible reason | unit + integration + render | `~UserServiceTests`, `~AdminAccountReach`, `~AdminPlatformRenderTests` | ❌ Wave 0 |
| D-03 | Self demotion and self removal refused; last-Admin demotion (all four role actions, including a crafted `PromoteToDM` on an Admin) and removal refused; each outcome an Error toast; non-last-Admin and non-self paths still work; Platform `RemoveMember` unaffected; buttons hidden or disabled with a visible reason | unit + integration + render | `~GroupServiceTests`, `~AdminRoleGuard`, `~GroupManagementIntegrationTests` | ❌ Wave 0 |
| D-04 | Deferred rows exist in `deferred-items.md` with severity | process | review | ❌ |
| D-05 | Checkpoint answers recorded; security document registers each gap with evidence | process + doc | `100-SECURITY.md`, `audit-answers.json` | ❌ |
| D-06 to D-09, D-11, D-19, D-20 | Fixtures through forms, no existing row changed, email suppression verified in `/proc` environ, local-only captures, SQL confirmed by the operator | manual / scratch scripts | `fixtures.mjs --check`, `run-app.mjs` | manual-run |
| D-10 | EmailStats statistics branch renders canned figures (four tiles, "as of" line), the error and missing-key states, force refresh bypasses cache; `ResendBaseUrl` default unchanged | integration | `~EmailStatsRenderTests`, `~ResendStatsClientTests` | ❌ Wave 0 |
| D-12 | `DeleteUser` and `DeleteQuest` are `[HttpPost]` with the token attribute, both layouts render a form (no fetch, no `onclick`), PRG with Success and Error toasts, cross-board quest id deletes nothing and toasts, reminder-log and follow-up cleanup, antiforgery sweep covers them | integration + static | `~AdminControllerIntegrationTests`, `~AdminQuestDelete`, `~AntiForgeryTokenCoverageTests`, `~BoardPageGateMatrixTests` | ✅ rewrite + ❌ new |
| D-13 | Each of the six confirmations is a `data-confirm-message` attribute with today's sentence, HTML-encoded; no `onsubmit`/`onclick` in the area; `confirm-submit.js` module loaded | static + render | `~AdminPlatformConversionGuardTests`, `~LinkedBoardsPlatformIntegrationTests` | ✅ rewrite + ❌ new |
| D-14, D-15, D-18 | Gate 1 and Gate 2 packages approved by the operator | manual | `~/qb-ui-review/phase-100/gate1/`, `gate2/` | manual-only |
| D-16 | One mapping for role, board type and quest status used by both layouts; no raw enum text in the area's views | static + render | `~AdminPlatformConversionGuardTests`, `~AdminPlatformRenderTests` | ❌ Wave 0 |
| D-17 | Platform layouts: landmarks, names, toggler, no horizontal scroll at 320, nav contrast, 44px toggler, on all seven pages | static (exists) + Playwright | `~UiFoundationDesktopShellGuardTests`, `~UiFoundationMobileShellGuardTests`; scratch `probe320.mjs`, `contrast.mjs` | ✅ static / manual-run |
| D-21 | Plans' docs commits carry STATE and ROADMAP | process | `git show --name-only --format= HEAD \| grep -c -E '(^|/)(STATE\|ROADMAP)\.md$'` = 2 | process |
| Twin answers Q-85, 87, 88, 92, 93 | Phone Users has the Resend form for unconfirmed users with an address; phone Quests shows Open and Closed for Campaign boards through the shared mapping; phone Members has "Current Members"; phone EditUser has "Password Management" and its sentence; phone ResetPassword heading names the user | integration | `~AdminPlatformRenderTests`, `~MobileViewsTests` | ❌ Wave 0 |
| Relayouts Q-124, 125, 126 | After Gate 1: one page title in the card header (Create and Edit user), Members stacked full width, Users with Email under Name; every field, column, form and button still present | integration | `~AdminPlatformRenderTests` | ❌ (after Gate 1) |
| Definition of converted 1 to 3, 8 | No inline `style`, `<style>`, `<script>` or handler; no hex, `rgb()`, `!important`; sheets in `@layer pages` linked through `@section Styles`; guards rewritten, none deleted | static | `~AdminPlatformConversionGuardTests`, `~UiFoundationLayerGuardTests` | ❌ Wave 0 |
| Definition of converted 5, 7 | A-1 to A-11 bar, targets (24 desktop, 44 phone), contrast incl. EmailStats tiles measured with the stub | Playwright | scratch `sweep.mjs`, `contrast.mjs`, `a11y-*` | manual-run |
| Definition of converted 9 | Helpers untouched; calendar feed files untouched; `DeleteQuest` write stays a tracked remove | unit + diff | `~WallClockMarkupTests`, `~LocalTimeMarkupTests`, `~CalendarSubscriptionStaticGuardTests`; `git diff --stat` | ✅ |
| Images | No `aspect-ratio` or `object-fit` in the area's sheets, scanner self-test | static | `~AdminPlatformConversionGuardTests` | ❌ Wave 0 |
| Definition of converted 10 | Every page works | full suites + sweep | both full suites | ✅ |

## Sampling Rate
- **Per task commit:** the quick filters narrowed to the slice's files; for UI slices the scratch probe on the touched pages under both User-Agents.
- **Per wave merge:** both full suites, per project.
- **Phase gate:** full suites green, `dotnet build` clean, AFTER sweep shows no new contrast failure and no hidden control against BEFORE; `git diff --stat` shows no change to `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`; no bulk, raw or hand-set write.
- **Max feedback latency:** 120 seconds.

## Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs` (clone of `ShopConversionGuardTests`)
- [ ] `QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs` (both User-Agents, seeded boards and members)
- [ ] `QuestBoard.IntegrationTests/Controllers/AdminAccountReachTests.cs`, `AdminRoleGuardTests.cs`, `AdminQuestDeleteTests.cs`, `EmailStatsRenderTests.cs` (stub handler in the factory)
- [ ] Facts in `QuestBoard.UnitTests/Services/UserServiceTests.cs`, `GroupServiceTests.cs`, `QuestServiceTests.cs`
- [ ] `~/qb-ui-review/phase-100/tools` and `ENVIRONMENT.md` re-pointed from phase 99
- Framework install: none

## Manual-Only Verifications
| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gate 1 and Gate 2 approvals; each contrast or target exception accepted or rejected | D-14, D-15, D-18 | Operator taste gate | local packages under `~/qb-ui-review/phase-100/` |
| Live proof of the D-02 and D-03 guards on p100 fixtures | D-08 | Needs the running app and the database | `fixtures.mjs` click-throughs, recorded in the security document |
| Rendered contrast, targets, overflow at 320 desktop and 390 phone, EmailStats tiles | Definition of converted 5 | Rendered measurement | `~/qb-ui-review/phase-100/after/` |
| Real phone, screen reader on the disabled-with-reason rows | D-21 | Assistive technology and device | phase UAT after deploy |
| SQL-backed quest delete with a reminder log | G-05 | EF InMemory ignores FKs | `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`, on the operator's yes |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
