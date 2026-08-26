---
phase: 78
slug: link-preview-foundation-and-quest-cards
status: draft
nyquist_compliant: false
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
| TBD | TBD | TBD | LINKPREV-01 | Header spoofing (Spoofing) | Forwarded scheme/host honoured only from `KnownProxies` | integration | `dotnet test --filter FullyQualifiedName~ForwardedHeaders` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-02 | — | Copy control present on desktop **and** mobile Details views | integration | `dotnet test --filter FullyQualifiedName~QuestDetailsCopyLink` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-03 | — | Signed URL emits `og:title`/`og:description`/`og:image`/`og:url`/`twitter:card` | integration | `dotnet test --filter FullyQualifiedName~QuestPreview` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-04 | Unauthenticated read (Info Disclosure) | Unsigned quest URL serves no quest data anonymously | integration | `dotnet test --filter FullyQualifiedName~QuestControllerAuthorizationRegressionTests` | ✅ exists — **needs rewrite (D-09)** | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-05 | Token tampering (Tampering) | `Unprotect` throws → no card, rejected not degraded | integration | `dotnet test --filter FullyQualifiedName~TamperedSignature` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-06 | Hidden-content leak via truncation | Plain text from `ExtractPlainText`, truncated on rendered text, HTML-escaped | unit + integration | `dotnet test --filter FullyQualifiedName~CardDescriptionTruncation` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-07 | Cross-tenant replay (Spoofing / EoP) | Signature minted in group A yields nothing against a group-B id | integration | `dotnet test --filter FullyQualifiedName~CrossGroupReplay` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-08 | — | Branded image 200 anonymously, no `Location` header, correct `Content-Type` | integration | `dotnet test --filter FullyQualifiedName~BrandedImageServing` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | LINKPREV-09 | Token-as-auth (EoP) | Signature never satisfies `[Authorize]` and never authorises a POST | integration | `dotnet test --filter FullyQualifiedName~SignatureNotAuthentication` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/QuestPreviewControllerTests.cs` — LINKPREV-03, -05, -07, -09. Follow the `TenantIsolationTests.cs` / `ContactsControllerIntegrationTests.cs` pattern of setting `factory.TestGroupContext.ActiveGroupId` **for seeding only**. It must NOT be set before the preview-route request itself — the whole point is that an anonymous caller arrives with no group context and the route establishes it from the verified token.
- [ ] `QuestBoard.UnitTests/Services/LinkSigningServiceTests.cs` — Protect/Unprotect round-trip, tamper rejection, cross-purpose rejection.
- [ ] Truncator unit tests (location follows wherever the helper lands) — word-boundary truncation, HTML escaping, empty-description fallback, and truncation applied to rendered plain text rather than Markdown source.
- [ ] `QuestBoard.IntegrationTests/Controllers/QuestControllerAuthorizationRegressionTests.cs` — rewrite `Details_Anonymous_DoesNotSeeManageQuestLinkAndDoesNotThrow` to assert the `[Authorize]` login redirect instead of 200 OK. **This must not be "fixed" by weakening or removing the attribute.**
- [ ] Forwarded-header test class for LINKPREV-01.

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

- [ ] All tasks have `<automated>` verify or a Wave 0 dependency
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all ❌ references above
- [ ] No watch-mode flags
- [ ] Quick-suite feedback latency measured and under ~60s
- [ ] Every LINKPREV requirement has at least one row with a real task ID
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
