---
phase: 87
slug: cross-board-deep-link-recovery
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-09-21
---

# Phase 87 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

This phase deliberately opens a narrow read past the application's 18 EF Core global query
filters, so the tenancy boundary is the subject of the audit rather than a background
assumption. The register below is the deduplicated union of the `<threat_model>` blocks in
all four plan files; several threats recur across plans and are recorded once.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| browser → application | An authenticated viewer's request carries a URL naming an entity id on some board, plus `Sec-Fetch-*` headers the browser generates and a direct HTTP client can forge | Entity id, route identity, navigation-intent headers |
| session → EF Core global query filters | `ActiveGroupId` in Session is the single value 18 `HasQueryFilter` predicates key off; anything that writes it decides what every subsequent query in that request can see | Active board id — the tenancy selector |
| application → tenancy boundary | `CrossBoardLinkRepository` is the only production code permitted to read past those filters | A single `int` board id the caller already belongs to; never row data |
| return URL → redirect | A viewer-supplied `returnUrl` reaches `Redirect(...)` — the classic open-redirect shape | Arbitrary attacker-influenceable string |
| another origin's page → this application | An inline subresource reference (image, iframe, fetch) to one of these URLs is a request this application answers while carrying the viewer's cookies | Ambient session authority |
| resolved board → authorization | The board the request switched to is the board every policy handler on the landed page is judged against | Effective role for the request |
| response → requester | The response is the only channel through which membership or existence could leak | Presence/absence signal |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-87-01 | Information Disclosure | Middleware guard 6; picker non-resolving path; unresolvable response at full width | high | mitigate | No distinguishing branch. `CrossBoardDeepLinkMiddleware.cs:79-84` falls to the same `await next(context); return;` as every other guard — no log, status, header or early-return variant. Picker twin at `GroupPickerController.cs:46-59`. Pinned by `CrossBoardOracleParityTests.cs:172-181` asserting status + body + all non-volatile headers across 8 entity families (`:187-213`) and for a SuperAdmin (`:216-240`) | closed |
| T-87-02 | Tampering | Middleware verb check and Fetch Metadata gate | high | mitigate | Explicit `IsGet`/`IsHead` at `:50-54` (not inherited from the header gate, because a form post carries the same navigation headers as a link click); Fetch Metadata gate at `:118-134` requires `Sec-Fetch-Dest: document` + `Sec-Fetch-Mode: navigate` and refuses `Sec-Purpose`/`Purpose`/`X-Moz` prefetch hints. `CrossBoardDeepLinkMiddlewareTests.cs:200-214` and `:217-236` assert the board is **unchanged**, not merely that the response matches | closed |
| T-87-03 | Tampering | `CrossBoardLinkRepository` — the `IgnoreQueryFilters()` seam, and its spread by copy-paste | high | mitigate | Membership pinned *inside* every predicate (`memberGroupIds.Contains(x.GroupId)`), never post-filtered. `CrossBoardIgnoreQueryFiltersSeamTests.cs:16-23` allowlist is exactly 5 entries; `:29-34` scans all three production roots; `:144` uses `BeEquivalentTo` so a new call site *or* a stale entry fails | closed |
| T-87-04 | Elevation of Privilege | Pipeline position relative to `UseAuthorization` | high | mitigate | `Program.cs:332` registers the middleware, `:334` is `UseAuthorization()`. Both directional facts exist: `CrossBoardAuthorizationBoundaryTests.cs:102-121` (Player on active, DM on target → 200 on `/Quest/Edit`) and `:123-140` (DM on active, Player on target → refused) | closed |
| T-87-05 | Spoofing | `IsRealTopLevelNavigation` header inspection | medium | accept | Any HTTP client can forge `Sec-Fetch-*`. Knowingly accepted — see Accepted Risks Log | closed |
| T-87-06 | Information Disclosure | Banner payload carried in TempData | low | accept | Cookie-based TempData under Data Protection. Independently verified rather than assumed: `Program.cs:35` calls `AddControllersWithViews()` with no `AddSessionStateTempDataProvider`, so the default `CookieTempDataProvider` is genuinely in use. See Accepted Risks Log | closed |
| T-87-07 | Denial of Service | Switch-back control returning to the deep link | low | mitigate | `_Toasts.cshtml:90-101` posts only `__RequestVerificationToken` + `groupId`, no return-path field, so `RedirectToLocal` falls through to the ordinary post-switch destination. The bounce loop is structurally impossible. Pinned twice: `CrossBoardDeepLinkMiddlewareTests.cs:169-173` and `:260-279` | closed |
| T-87-08 | Information Disclosure | A projection returning more than a board id | high | mitigate | All seven entity projections at `CrossBoardLinkRepository.cs:61-124` are `Select(x => (int?)x.GroupId)`; `:50-55` selects `ug.GroupId`. No `Include`, no entity-returning query, no navigation property in the file. Unmapped kinds throw rather than returning a silent null (`:34-40`) | closed |
| T-87-09 | Elevation of Privilege | A route gaining cross-board reach by accident — a new or renamed action | medium | mitigate | `CrossBoardLinkRegistry.cs:28-57` is a closed dictionary with every key built from `nameof(Controller.Action)`, so a rename breaks the build. `CrossBoardLinkRegistryTests.cs:49-53` asserts exactly 18 entries; `:58-88` asserts each pair maps to its named kind; `:114-130` proves ordinary routes stay unresolvable | closed |
| T-87-10 | Information Disclosure | Profile routes resolving ambiguously and revealing a shared board | medium | mitigate | `CrossBoardLinkResolverService.cs:58-62` returns null unless exactly one board is shared, and the shared-boards query is itself membership-pinned (`CrossBoardLinkRepository.cs:50-55`). `CrossBoardRouteCoverageTests.cs:301,315,331` cover unambiguous, two-shared-boards and no-id cases | closed |
| T-87-11 | Tampering | Image subresources repointing the board during page load | medium | mitigate | Two independent conditions, both confirmed: the six image routes are absent from the registry (`CrossBoardLinkRegistryTests.cs:93-110`), **and** `CrossBoardRouteCoverageTests.cs:259-275` sends them with `Sec-Fetch-Dest: image` and asserts the board is unchanged. Shop modal AJAX variant covered at `:280-297` | closed |
| T-87-12 | Tampering | `GroupPickerController.Index` redirecting to a supplied return URL (open redirect) | high | mitigate | `Url.IsLocalUrl` applied at `:46` before parsing and again at `:91` inside `RedirectToLocal` before every redirect. `CrossBoardRouteTarget.cs:48` independently refuses absolute, protocol-relative (`//`) and backslash-prefixed (`/\`) forms. `CrossBoardRouteTargetTests.cs:104-127` pins all three | closed |
| T-87-13 | Elevation of Privilege | The picker-skip branch running for a SuperAdmin | medium | mitigate | `GroupPickerController.cs:46` gates the branch on `!isSuperAdmin`, and the resolver takes a bare `int userId` answering only from the viewer's own `UserGroups` rows regardless (D-12). `CrossBoardPickerSkipTests.cs:213-245` — a SuperAdmin with a resolvable returnUrl still gets the picker | closed |
| T-87-14 | Tampering | Double-decoding the return URL | medium | mitigate | `CrossBoardRouteTarget.cs:36-84` performs no decode of any kind — no `UnescapeDataString`, no `UrlDecode`; the comment at `:33-35` records why. `CrossBoardRouteTargetTests.cs:130-139` asserts `/Quest%2FDetails%2F42` resolves to nothing | closed |
| T-87-15 | Spoofing | A crafted link silently placing a viewer on a different board of theirs | low | accept | Bounded by the membership-pinned lookup; the banner names the landed board (`_Toasts.cshtml:87`). See Accepted Risks Log | closed |
| T-87-16 | Information Disclosure | An entity the page will not serve leaking through resolution | medium | accept | Accepted behaviour is itself pinned: `CrossBoardAuthorizationBoundaryTests.cs:142-193` (cancelled event) and `:195-250` (draft shop item) both resolve, switch, and defer to the page's own rules. See Accepted Risks Log | closed |
| T-87-17 | Repudiation | A board switch happening with no trace for the viewer | low | mitigate | `_Toasts.cshtml:78-105` names the target board and renders the switch-back control; one-shot behaviour asserted at `CrossBoardDeepLinkMiddlewareTests.cs:176-180`. Mobile rendering confirmed at the human checkpoint after three defects were found and fixed | closed |
| T-87-SC | Tampering | Package installs (supply chain) | high | mitigate | `git diff 0ff07765 HEAD` touches zero `.csproj`/`.props`/lock files — 34 files changed, none package-related, with `0ff07765` confirmed an ancestor of HEAD. `87-RESEARCH.md:235-241` records the Package Legitimacy Audit as N/A with reasoning | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-87-01 | T-87-05 | Any direct HTTP client can set `Sec-Fetch-Dest: document`. The gate is a usability safety valve for an authenticated member's own browser, not a security boundary — the code comment at `CrossBoardDeepLinkMiddleware.cs:113-117` says so explicitly. A client that forges the headers still only reaches the membership-pinned lookup with its own identity, so it can only ever repoint its own session to a board it already belongs to. No test asserts this gate as an attacker-facing control. | Operator (plan-time, D-02) | 2026-09-21 |
| R-87-02 | T-87-06 | The banner payload is two board names and a previous board id, carried in Data-Protection-protected cookie TempData. The values name boards the viewer is already a member of. | Operator (plan-time) | 2026-09-21 |
| R-87-03 | T-87-15 | The worst outcome of a crafted link is that a viewer lands on one of their own boards — which is precisely the feature. No board the viewer is not a member of is reachable, and the banner tells them where they were placed. | Operator (plan-time) | 2026-09-21 |
| R-87-04 | T-87-16 | A cancelled event or undisplayable shop item still resolves and still switches the board, revealing only that the viewer's own board owns some id — already learnable from that board's list pages. Teaching the lookup each entity's visibility rules was rejected because it would copy business logic into the tenancy bypass and require permanent synchronisation with every controller. | Operator (plan-time, D-18) | 2026-09-21 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-09-21 | 18 | 18 | 0 | gsd-security-auditor |

Verification depth exceeded ASVS L1 grep-depth for the seven high-severity threats: boundary
placement and data flow were traced directly rather than pattern-matched. All cited tests were
executed, not merely located — `dotnet test --filter "FullyQualifiedName~CrossBoard"` returned
145 passed / 0 failed (82 unit, 63 integration), with none skipped.

Two claims were checked rather than taken from the plans:

1. **The allowlist's real shape.** It contains exactly five entries — `EventSignupRepository`,
   `EventRepository`, `GroupRepository`, `QuestRepository`, `CrossBoardLinkRepository` — and the
   scan covers all three production projects, skips `obj`/`bin`, strips `//`, `/* */` and `@* *@`
   before matching, and uses exact set equality so a stale entry fails as loudly as a new call
   site. An independent live grep returned precisely those five files. Scope note, not a gap
   against the declared mitigation (which says `.cs` files): `.cshtml` files are not enumerated
   by the scan. None currently contains the call shape, and Razor views have no `DbContext` reach
   in this architecture.
2. **Structural parity for T-87-01.** Guard 6 and the picker's non-resolving path were read
   directly, looking for any distinguishing artifact — a differently shaped early return, a log
   line, a status or header difference. There is none. A nonexistent-id request and a
   real-but-foreign-entity request are literally the same code path with the same projection.

No `## Threat Flags` section appears in any of the four SUMMARY files — the executors declared no
new attack surface, so that input contributed no independent signal. This verdict rests on direct
code and test verification rather than on executor self-report.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-09-21
