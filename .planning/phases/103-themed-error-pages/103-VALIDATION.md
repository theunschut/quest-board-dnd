---
phase: "103"
slug: "themed-error-pages"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-18"
---

# Phase 103 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `103-RESEARCH.md` § "Validation Architecture" (copied below; the plans' `<verify>` blocks cite these commands).

---

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2, FluentAssertions 8.10.0, Microsoft.AspNetCore.Mvc.Testing 10.0.9, EF Core InMemory 10.0.9, NSubstitute 5.3.0 (unit) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`, `parallelizeTestCollections: false`); no separate config for UnitTests |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~ErrorPage\|FullyQualifiedName~CrossBoard"` |
| Full suite command | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (per project; a `0x80131506` CLR error is a flake, re-run once; never set GC env vars) |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-02/D-03/D-07 | 404 page for navigations: same body for missing and foreign id; Switch board only with 2+ memberships; per-code copy | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~ErrorPageRender"` | no, Wave 0 |
| D-09 | Fetch-shaped requests stay bare (404/400/403 empty body, 429 plain text) | integration | `... ~ErrorPageNavigationRule` | no, Wave 0 |
| D-09 | Rule truth table (Accept, X-Requested-With, Sec-Fetch-Dest, HEAD) | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BrowserNavigation"` | no, Wave 0 |
| D-10 | 429 navigation themed and still 429; non-navigation plain sentence | integration | `... ~ErrorPageRateLimit` | no, Wave 0 (use `forgot-password`, limit 3) |
| D-11/D-12/D-14 | Testing env: throwing service gives themed 500 with `Reference`; no exception text; Reference equals the logged `RequestId` scope; scopes option is on | integration | `... ~ErrorPageServerError` | no, Wave 0 (extend `CapturingLoggerProvider` for scopes) |
| D-08 | AccessDenied is the 403 variant, both viewer cases, URL unchanged; cookie path unchanged | integration | `... ~AccountControllerIntegrationTests` (rewrite) | exists, rewrite |
| D-17 | `Logout(toLogin)` redirects to login; default still Home; GET is not allowed | integration | `... ~AccountControllerIntegrationTests` | exists, extend |
| Claude's-discretion | Page still renders when `IUserService`/`IBoardPageResolver`/`IGroupService` throw | integration | `... ~ErrorPageFallbackLayout` | no, Wave 0 |
| D-04/D-05 | Per route x role x record state: resolver switches iff the action would serve 2xx with the target active (agreement matrix) | integration | `... ~CrossBoardVisibilityMatrix` | no, Wave 0 |
| D-04/D-06 | Hidden record and nonexistent id are indistinguishable (status, masked body, headers), board unchanged, no banner | integration | `... ~CrossBoardOracleParity` (extend, rewrite the empty-body fact) | exists, extend |
| D-05 | Picker skip applies the same predicate | integration | `... ~CrossBoardPickerSkip` | exists, extend |
| D-05 | Registry carries an access level per route; 18 routes | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CrossBoardLinkRegistry"` | exists, extend |
| D-05 | Predicate shapes against InMemory (shop, contact, quest log, owners, page availability, SuperAdmin) | unit | `... ~CrossBoardLinkVisibility` | no, Wave 0 |
| D-04 | Shop member-visibility Domain rule and its EF twin agree | unit | `... ~ShopItemAvailabilityTests` | no, Wave 0 |
| Seams | `IgnoreQueryFilters` stays in the five listed files; resolver/repository added to the ambient-clock guard | unit | `... ~CrossBoardIgnoreQueryFiltersSeam` and `~AmbientClockSeam` | exist, extend |
| D-15 | Back button: same-site Referer renders a local relative `href`; off-site Referer renders none and the button is hidden; no inline script | integration | `... ~ErrorPageRender` | no, Wave 0 |
| D-18 | Phone UA gets the twin and the phone layout; twin has no `@media`; targets 44px; guard scanners (inline code, planning refs, literals, layer, token pairs) cover the new sheets and layouts | integration (static) | `... ~ErrorPagesGuard` and `~UiFoundationLayerGuard` | no / exists, extend lists |
| D-07 | `/Error/{code}` exempt in `GroupSessionMiddleware`; authenticated user with no active board sees the page | integration | `... ~GroupSessionMiddlewareIntegration` | exists, extend |
| D-19 | Gate 1 look check and phone mockup; live click-through of Back/Home/Log out | manual + Playwright tooling | `node ~/qb-ui-review/phase-103/tools/...` | no, Wave 0 (tooling copy) |

The **agreement matrix** is the drift guard for D-05: for each of the 18 routes and each of Player/DungeonMaster/Admin/SuperAdmin-member x record state (shop: Draft, Denied, scheduled, Published, Archived; contact: revealed, hidden, own; quest log: open, finalized-past, closed; quest/character: own, other's), seed the record on board B, first select B and record the real action's outcome (the oracle), then select A and follow the deep link and assert: switched and served if the oracle was 2xx, otherwise unchanged board, themed 404, and a body/header-equal match with a nonexistent id. It needs no copy of the business rules in the test, so a later change to a controller rule fails it.

### Sampling Rate
- **Per task commit:** the quick command above, narrowed to the touched class.
- **Per wave merge:** both projects' full suites, run separately.
- **Phase gate:** both full suites green plus the static guards, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Helpers/BrowserNavigationTests.cs` - rule truth table, same-site Referer helper
- [ ] `QuestBoard.UnitTests/Repository/CrossBoardLinkVisibilityTests.cs` and `QuestBoard.UnitTests/Models/ShopItemAvailabilityTests.cs` - predicates on InMemory
- [ ] `QuestBoard.IntegrationTests/Tests/ErrorPageRenderTests.cs`, `ErrorPageNavigationRuleTests.cs`, `ErrorPageRateLimitTests.cs`, `ErrorPageServerErrorTests.cs`, `ErrorPageFallbackLayoutTests.cs`
- [ ] `QuestBoard.IntegrationTests/Security/CrossBoardVisibilityMatrixTests.cs`
- [ ] `QuestBoard.IntegrationTests/Security/CrossBoardHiddenRecordTests.cs` - the leak reproduction facts (committed skipped in 103-03, enabled in 103-07/103-08)
- [ ] `QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs` - converted-view list, sheet links, no inline code, no planning references, twin has no `@media`, scanner self-tests on seeded strings (the phase 98-100 pattern, e.g. `ShopConversionGuardTests`)
- [ ] Helpers: a parity helper that primes the antiforgery cookie and masks the token; `CapturingLoggerProvider` gains `ISupportExternalScope`; a factory variant with throwing stubs
- [ ] Update `UiFoundationLayerGuardTests` lists (`Layouts`, `LayeredFiles`, `NoLiteralFiles`): a new layout must either link a legacy sheet or the guard gets an explicit "no legacy sheet" allowance (the picker layout does link `legacy-pages.css`)
- [ ] Rewrite (same intent): `CrossBoardOracleParity_UnresolvableEntity_Returns404WithEmptyBody`, the two `CrossBoardAuthorizationBoundaryTests` facts, `AccessDenied_Get_...`, the `AccessDenied` entries in `AccountConversionGuardTests` (`:26`, `:54`, `:414`)
- [ ] Framework install: none


---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
