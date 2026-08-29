---
phase: 77
slug: availability-overview-page
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-08-29
---

# Phase 77 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Register origin: `register_authored_at_plan_time: true` — all 10 PLAN files carried a
> `<threat_model>` block. Audited in verify-mitigations mode (not retroactive-STRIDE).

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| active board → EF Core read | Every read on `Events`/`EventSignups` crosses the tenant boundary; the ambient global query filter is the only thing holding it | Other boards' events, members and availability answers |
| query string → `.Take()` | An integer originating in a query string reaches the repository's page size via the controller and service | Server query cost |
| viewer identity → navigation destination | Two quest-index sites choose Manage vs Details by whether the viewer owns the quest | Reachability of a manage surface |
| server clock → "upcoming" window | The date boundary that decides which events appear | Event visibility at the day boundary |

---

## Threat Register

Resolved by `(plan, threat_id)` pair, **not** by `threat_id` alone — see finding F1.

| Threat ID | Plan | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|------|----------|-----------|----------|-------------|------------|--------|
| T-77-01 | 77-01/03/04/05/07/09/10 | Information Disclosure | Aggregating read across events × signups × users | critical | mitigate | No `IgnoreQueryFilters()` and no manual `GroupId` predicate; the read rides the fail-closed filters in `QuestBoardContext.cs:448-451` / `:462-465`. Grep = 0 in `EventRepository.cs`, `EventService.cs`, `EventsController.cs`. Proven end-to-end by `EventsOverviewTenantIsolationTests` | closed |
| T-77-01 | 77-06/08 | Information Disclosure | (same, disposition recorded as `accept` in these two plans — see F1) | critical | accept | Neither plan touches the read path; the control above applies | closed |
| T-77-02 | 77-01/03/05/06/07/09 | Denial of Service | `.Take(take)` page size | high (medium in some plans — see F1) | mitigate | `EventsController.Index:37` — `Math.Clamp(take ?? options.DefaultTake, 1, Math.Max(1, options.MaxTake))`; `EventsOverviewOptions.IsValid()` wired via `.Validate(...).ValidateOnStart()` so a bad ceiling fails at boot | closed |
| T-77-03 | 77-01 | Tampering | Package installs | high | accept | Zero `.csproj`/lockfile changes across the entire phase. See Accepted Risks R-01 | closed |
| T-77-03 | 77-03 | Denial of Service (reliability) | SuperAdmin with no active group | low | mitigate | `GroupSessionMiddleware` redirects before the action runs; no unconditional active-group assertion | closed |
| T-77-04 | 77-01 | Information Disclosure | Cell-state classification | low | accept | Aggregation makes per-member answering patterns visible to all board members — the deliberate, discussed outcome of D-16. See Accepted Risks R-02 | closed |
| T-77-05 | 77-02 | Information Disclosure | Navigation entry visibility | medium | mitigate | Nav entry sits under the unchanged board-type gate; no role condition added (D-16, D-22). Asserted by `LayoutNavigationTests` | closed |
| T-77-05 | 77-08 | Elevation of Privilege | Nav test fixture state | low | mitigate | `IAsyncLifetime.DisposeAsync` resets `TestGroupContext.BoardType`; every test sets its own board type | closed |
| T-77-06 | 77-02 | Elevation of Privilege | Nav gate | low | accept | Existing board-type gate reused unchanged | closed |
| T-77-07 | 77-02 | Denial of Service | Static stylesheet serving | low | accept | Two static CSS files, served by the existing static-file pipeline | closed |
| T-77-08 | 77-03/05/07 | Elevation of Privilege | Overview page authorization | low | mitigate | `[Authorize]` only; D-16 makes this an all-authenticated-members page, so no role gate applies. No `DungeonMasterOnly` condition added | closed |
| T-77-09 | 77-03/05/09/10 | Injection (XSS) | Member display names and event titles in views | medium | mitigate | Zero `Html.Raw` across **all** modified `.cshtml`; every name/title is Razor-encoded | closed |
| T-77-10 | 77-04 | Information Disclosure | Same-named member on two boards | high | mitigate | `EventsOverviewTenantIsolationTests.cs:144-145` counts occurrences (`Split(name).Length - 1` → `Be(1)`) rather than using containment, so a leaked column with an identical display name still fails. `AuthenticationHelper.cs:22-24` GUID-suffixes username/email so the two members are genuinely distinct users | closed |
| T-77-11 | 77-04 | Information Disclosure | Null active group resolving to "show everything" | high | mitigate | `QuestBoardContext.cs:448-451` and `:462-465` are fail-closed (`ActiveGroupId != null && …`), short-circuiting to zero rows. **The predicates are the control, not the test** — see F3 | closed |
| T-77-12 | 77-04 | Tampering | Existing `IgnoreQueryFilters` call sites | medium | mitigate | No phase-77 file bypasses filters; pre-existing sites unchanged. Premise miscounted — see F4 | closed |
| T-77-13 | 77-06 | Repudiation | Stylesheet-only change | low | mitigate | One stylesheet touched; no markup, controller, view model or DI change | closed |
| T-77-14 | 77-07 | Tampering | Injected clock | medium | mitigate | `TimeProvider.System` registered via `TryAddSingleton`; boundary reads `timeProvider.GetUtcNow().UtcDateTime`, aligning with the UTC timestamps the feature already writes | closed |
| T-77-15 | 77-08 | Repudiation | Nav test independence | medium | mitigate | Fixture state restored in `DisposeAsync`; latent ordering dependency removed | closed |
| T-77-16 | 77-09 | Repudiation | Mobile surface rendering coverage | high | mitigate | Four `Index_MobileUserAgent_*` facts send a real iPhone UA and assert behaviour (guard count `Be(2)`, computed `take=11`, empty-state negative). View selection is load-bearing: `avail-card` appears only in the mobile view, `avail-grid` only in the desktop view | closed |
| T-77-17 | 77-10 | Elevation of Privilege | Ownership-conditional quest navigation | high | mitigate | Desktop `Quest/Index.cshtml`: `onclick` (L83) and `href` (L115) expressions are **byte-identical** (verified by string equality, not inspection). Mobile `Quest/Index.Mobile.cshtml`: `navUrl` computed once (L32-34) and read by both `onclick` (L75) and `href` (L78) — single source of truth. Of the 13 new anchors, exactly 2 are ownership-conditional; the other 11 target read surfaces. `QuestController.cs` untouched, so `[Authorize(Policy = "DungeonMasterOnly")]` and the `IsQuestOwner` check remain the real control. Residual regression risk — see F5 | closed |
| T-77-18 | 77-10 | Denial of Service (usability) | Anchor added inside clickable rows | low | mitigate | Anchors are additive; all 13 original `onclick` handlers preserved; no nested interactive element (the outer element is a `div`/`tr` with a handler, not a focusable control) | closed |
| T-77-SC | 77-02..77-10 | Tampering | Supply chain (package installs) | high | accept (mitigate in 77-07) | Zero `.csproj`/lockfile changes across the entire phase. 77-07 deliberately hand-wrote a 9-line `FixedTimeProvider` double rather than adding `Microsoft.Extensions.TimeProvider.Testing`. See Accepted Risks R-01 | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above `workflow.security_block_on` (high) count toward `threats_open`*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-01 | T-77-03 (77-01), T-77-SC (77-02..77-10) | The phase installs no packages at all — zero `.csproj` and lockfile changes verified across every phase-77 commit. With no new dependency there is no supply-chain surface to mitigate | Theun Schut | 2026-08-29 |
| R-02 | T-77-04 (77-01) | An aggregating page makes each member's answering pattern visible to every other board member. This is the deliberate outcome of decision D-16 (the overview is an all-members page, not DM-only), discussed and locked during discuss-phase rather than an unmitigated leak | Theun Schut | 2026-08-29 |

---

## Findings

Raised by the security audit; none is an open threat.

| ID | Finding | Impact |
|----|---------|--------|
| F1 | **Threat IDs are not globally unique.** Each plan authored its register independently; the same ID carries different categories, severities and dispositions across plans (`T-77-03`, `T-77-05`, `T-77-08`, `T-77-09`, severity of `T-77-02`, and disposition flips on `T-77-01` and `T-77-SC`). The register therefore cannot be queried by ID — tooling that aggregates by `threat_id` will silently merge unrelated threats and pick an arbitrary severity. Every resolution here is keyed by `(plan, threat_id)` | Process. Use globally unique threat IDs in future phases |
| F2 | **`## Threat Flags` missing from 8 of 10 summaries** (only 77-05 and 77-09 emit it). Absence of flags is not evidence of no new attack surface; the auditor compensated by diffing the full production surface, and in doing so corrected the phase base commit to `e184bbe5` — a narrower range had omitted `EventRepository.cs`, `IEventRepository.cs` and `IEventService.cs`. No unregistered attack surface found in the corrected set | Process |
| F3 | **T-77-11's test is weaker than its mitigation text claims.** `Overview_WithNoActiveBoardSelected_ShowsNothingFromEitherBoard` asserts `BeOneOf(NotFound, Redirect, Found, OK)`; when the middleware redirects, the body is empty and both `NotContain` assertions pass trivially. The fail-closed filter predicates are the real control and are verified independently — but this test should not be cited as proof | Documentation accuracy |
| F4 | **T-77-12's premise is factually wrong.** It asserts "the one existing occurrence"; there are three `IgnoreQueryFilters` call sites — `GroupRepository.cs:135` and `:147` (private, group-pinned) and `QuestRepository.cs:268` (`GetQuestsForTomorrowAllGroupsAsync`, a deliberate cross-group read the plan does not mention). None was added by phase 77, so the material claim holds | Documentation accuracy |
| F5 | **T-77-17 has no behavioural regression test.** `RowNavigationAccessibilityTests` covers only `/Events`, neither ownership-conditional quest anchor. Protection is a static occurrence count, which an edit changing both occurrences identically would still satisfy. The code is correct today (verified directly) | Residual regression risk. Worth a fact asserting a non-owner gets `Quest/Details` and an owner gets `Quest/Manage` |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-29 | 47 register entries (19 IDs × plans) | 47 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-29

> Evidence dependency: T-77-10's verdict rests on the suite being green (the `Be(1)` occurrence
> baseline). Verified against 408 unit + 560 integration = 968 tests, 0 failures. Re-verify
> T-77-10 if that changes.
