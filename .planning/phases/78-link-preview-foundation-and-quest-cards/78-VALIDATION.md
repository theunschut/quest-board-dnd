---
phase: 78
slug: link-preview-foundation-and-quest-cards
status: ready
nyquist_compliant: true
wave_0_complete: false
created: 2026-08-26
---

# Phase 78 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `78-RESEARCH.md` § Validation Architecture. Per-task rows are seeded by
> requirement and get their task IDs filled in once plans exist.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2, `Microsoft.NET.Test.Sdk` 18.7.0) — both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests` |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json`; host wiring in `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | quick: to be measured on first run · full: to be measured on first run |

**Build note:** if `dotnet build`/`dotnet test` fails on locked output files, Visual Studio is holding the binaries under the debugger — stop it (Shift+F5) before retrying. A second Claude session is also active on this branch for Phase 74; expect occasional build contention rather than a real failure.

---

## Sampling Rate

- **After every task commit:** `dotnet test QuestBoard.UnitTests`
- **After every plan wave:** `dotnet test`
- **Before `/gsd-verify-work`:** full suite green **plus** both deployed-host checks below — neither is satisfiable by the automated suite
- **Max feedback latency:** quick suite must stay under ~60s; if it grows past that, split rather than sample less often

---

## Per-Task Verification Map

Task IDs are assigned at planning; rows below are seeded by requirement so no requirement can be silently dropped from a plan.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 78-01-T3 | 78-01 | 1 | LINKPREV-01 | T-78-05 Header spoofing (Spoofing) | Forwarded scheme/host/client-IP honoured together; trust stays config-driven | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ForwardedHeaders` | ❌ W0 — `Middleware/ForwardedHeadersOptionsTests.cs` | ⬜ pending |
| 78-02-T1 | 78-02 | 1 | LINKPREV-01 | T-78-05, T-78-10 | The trusted-proxy value is actually set on the App CT | manual (blocking checkpoint) | none - server env file, see Manual-Only below | n/a | ⬜ pending |
| 78-08-T3 | 78-08 | 4 | LINKPREV-02 | - | Copy control present on desktop and mobile Details views, and the URL it hands out resolves | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestDetailsCopyLink` | ❌ W0 — `Controllers/QuestDetailsCopyLinkTests.cs` | ⬜ pending |
| 78-07-T3 | 78-07 | 3 | LINKPREV-03 | T-78-19, T-78-20 | Signed URL emits `og:title`/`og:description`/`og:image`/`og:url`/`twitter:card`, identical for every User-Agent | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestPreview` | ❌ W0 — `Controllers/QuestPreviewControllerTests.cs` | ⬜ pending |
| 78-03-T1 | 78-03 | 1 | LINKPREV-04 | T-78-12 Unauthenticated read (Info Disclosure) | Unsigned quest URL serves no quest data anonymously; login redirect, not 404 | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestControllerAuthorizationRegressionTests` | ✅ exists — rewritten by 78-03-T1 | ⬜ pending |
| 78-05-T3 | 78-05 | 2 | LINKPREV-05 | T-78-04 Token tampering (Tampering) | Tampered, malformed, empty, cross-purpose, wrong-type and over-long tokens all return false and never throw | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~TamperedSignature` | ❌ W0 — `Services/LinkSigningServiceTests.cs` | ⬜ pending |
| 78-07-T3 | 78-07 | 3 | LINKPREV-05 | T-78-04 | A tampered token on the route yields not-found and no card markup - rejected, not degraded | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestPreview` | ❌ W0 — `Controllers/QuestPreviewControllerTests.cs` | ⬜ pending |
| 78-05-T3 | 78-05 | 2 | LINKPREV-06 | T-78-06 Hidden-content leak via truncation | Plain text from `ExtractPlainText`, truncated on the rendered text, bounded at 200 including the ellipsis, caller-supplied fallback when empty | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CardDescriptionTruncation` | ❌ W0 — `Services/CardDescriptionTruncationTests.cs` | ⬜ pending |
| 78-07-T3 | 78-07 | 3 | LINKPREV-06 | T-78-20 | Description encoded exactly once at render; no raw markup anywhere in the body | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestPreview` | ❌ W0 — `Controllers/QuestPreviewControllerTests.cs` | ⬜ pending |
| 78-04-T3 | 78-04 | 1 | LINKPREV-07 | T-78-14 Test-double divergence | Setting the group through the abstraction is observed by the query filter in tests exactly as in production | integration (full suite) | `dotnet test` | ✅ existing suite | ⬜ pending |
| 78-07-T3 | 78-07 | 3 | LINKPREV-07 | T-78-01, T-78-02 Cross-tenant replay (Spoofing / EoP) | A signature minted on board A yields nothing against a board-B id, with no ambient group context set | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossGroupReplay` | ❌ W0 — `Controllers/QuestPreviewControllerTests.cs` | ⬜ pending |
| 78-06-T3 | 78-06 | 1 | LINKPREV-08 | T-78-08 | Branded image returns 200 anonymously, no `Location` header, image content type, inside the size budget | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~BrandedImageServing` | ❌ W0 — `Controllers/BrandedImageServingTests.cs` | ⬜ pending |
| 78-03-T2 | 78-03 | 1 | LINKPREV-09 | T-78-03 Token-as-auth (EoP) | The quest page requires a login independently of group context | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestControllerAuthorizationRegressionTests` | ✅ exists — rewritten | ⬜ pending |
| 78-07-T3 | 78-07 | 3 | LINKPREV-09 | T-78-03 | The signature never satisfies the login requirement and never authorises a post | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~SignatureNotAuthentication` | ❌ W0 — `Controllers/QuestPreviewControllerTests.cs` | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/QuestPreviewControllerTests.cs` — **owned by 78-07-T3** — LINKPREV-03, -05, -06, -07, -09. Follow the `TenantIsolationTests.cs` / `ContactsControllerIntegrationTests.cs` pattern of setting `factory.TestGroupContext.ActiveGroupId` **for seeding only**. It must NOT hold a board value before the preview-route request itself — set it explicitly to `null` immediately before each preview request, because the harness default of board one would otherwise make the route's own group override a no-op and every test would pass without the mechanism working. The whole point is that an anonymous caller arrives with no group context and the route establishes it from the verified token. The same nulling applies to `QuestDetailsCopyLinkTests`, which follows the copied URL through the preview route.
- [ ] `QuestBoard.UnitTests/Services/LinkSigningServiceTests.cs` — **owned by 78-05-T3** — Protect/Unprotect round-trip, tamper rejection, cross-purpose rejection, wrong-entity-type rejection, over-long-token rejection.
- [ ] `QuestBoard.UnitTests/Services/CardDescriptionTruncationTests.cs` — **owned by 78-05-T3** — word-boundary truncation, HTML escaping, empty-description fallback, and truncation applied to rendered plain text rather than Markdown source.
- [ ] `QuestBoard.IntegrationTests/Controllers/QuestControllerAuthorizationRegressionTests.cs` — **owned by 78-03-T1** — rewrite `Details_Anonymous_DoesNotSeeManageQuestLinkAndDoesNotThrow` to assert the `[Authorize]` login redirect instead of 200 OK. **This must not be "fixed" by weakening or removing the attribute.**
- [ ] `QuestBoard.IntegrationTests/Middleware/ForwardedHeadersOptionsTests.cs` — **owned by 78-01-T3** — LINKPREV-01. Asserts the configured flag set and known-proxy parsing rather than live header rewriting: the test server supplies no remote IP, so the middleware correctly refuses to trust any forwarded header and a behavioural assertion there would be a false negative. The behavioural proof is the deployed-host check owned by 78-02-T1 and 78-09-T2.
- [ ] `QuestBoard.IntegrationTests/Controllers/BrandedImageServingTests.cs` — **owned by 78-06-T3** — LINKPREV-08.
- [ ] `QuestBoard.IntegrationTests/Controllers/QuestDetailsCopyLinkTests.cs` — **owned by 78-08-T3** — LINKPREV-02.

**Harness caveat that shapes all of the above:** `WebApplicationFactoryBase.cs` registers a **singleton** `MutableGroupContext` for `IActiveGroupContext`, defaulting `ActiveGroupId = 1` for every test regardless of authentication state. Two consequences: the existing anonymous-200 assertion never exercised production's fail-closed-on-null-Session path, and a preview implementation that resolved the concrete `ActiveGroupContextService` would pass its tests while doing nothing in production. See CONTEXT.md D-12.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Rich card renders in a real Discord channel | LINKPREV-03 (ROADMAP criterion 1) | Discord's crawler is external and its embed rendering cannot be reproduced locally; markup inspection proves only that tags exist, not that a card appears | Mint a share link from a real quest, paste into an actual Discord channel, confirm title, description snippet, image, and site name all render as a large card |
| Card renders in Slack and iMessage | LINKPREV-03 | Same — external fetchers, differing tag support (Apple ignores `og:description`) | Paste the same link into Slack and into an iMessage thread |
| Absolute URLs correct on the deployed host | LINKPREV-01 (ROADMAP criterion 4) | Requires the real reverse proxy and hostname; localhost cannot prove it | `curl -A Discordbot https://<prod-host>/<signed-preview-url>` — assert `og:url` and `og:image` are absolute `https://` on the real hostname, never `http://localhost` |
| `ReverseProxy__KnownProxies__0` is actually set on the App CT | LINKPREV-01 | Server-side env file, not in the repo; unconfirmed since 2026-07-01 | Same `curl` as above. On .NET 10 an empty `KnownProxies` makes the middleware drop every `X-Forwarded-*` header, so a wrong scheme/host in the response is the tell — and would also mean Phase 32's rate limiting is already broken |
| Copy-to-clipboard UX on desktop and mobile | LINKPREV-02 | Clipboard API behaviour and the confirmation affordance need a real browser; mobile needs a **real mobile User-Agent**, not devtools emulation | Open a quest on desktop and on a real phone, click Copy, paste elsewhere, confirm the copied URL and the confirmation feedback |
| Logged-out click lands on the login page | LINKPREV-09 (ROADMAP criterion 6) | End-to-end browser behaviour across meta-refresh then `[Authorize]` | Open a signed link in a logged-out private window; confirm it reaches the login page, not the quest and not a 404 |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify, a Wave 0 dependency, or are one of the three blocking human checkpoints listed under Manual-Only (78-02-T1, 78-06-T2, 78-09-T2)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all outstanding references above
- [x] No watch-mode flags
- [ ] Quick-suite feedback latency measured and under ~60s
- [x] Every LINKPREV requirement has at least one row with a real task ID
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** task IDs assigned at planning, 2026-08-26
