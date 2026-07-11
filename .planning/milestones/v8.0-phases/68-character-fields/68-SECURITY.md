---
phase: 68
slug: character-fields
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-07-10
---

# Phase 68 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| browser → Character write forms (Create/Edit POST) | User-authored Description/Backstory free-text crosses into server model binding. Pre-existing boundary; 68-01 changes only the client-side editing widget, not server intake or persistence. | Free-text (Markdown source) |
| browser → `POST /markdown/preview` (editor's Preview toggle) | Editor JS posts in-progress text to the existing preview endpoint, reused verbatim from Phase 65/66. | Free-text (Markdown source, not persisted) |
| stored Description/Backstory → rendered HTML on Character Details | Previously-stored user free-text is converted to HTML and injected into the page — the phase's actual XSS-relevant boundary. | Sanitized HTML |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|-----------|-------------|------------|--------|
| T-68-01-W | Tampering (stored XSS) | Description/Backstory free-text on Character write forms | medium | mitigate | No new server surface — fields persisted as raw text unchanged. XSS neutralized on the READ side by `IMarkdownService`'s HtmlSanitizer pipeline (Phase 65 RENDER-01) via `Html.Markdown()`, delivered in 68-02. Verified: `Details.cshtml:152,160` and `Details.Mobile.cshtml:85,92` both call `@Html.Markdown(...)`; no `Html.Raw` bypass found in either file. | closed |
| T-68-01-P | Spoofing (CSRF on preview) | `POST /markdown/preview` reached by the editor | low | accept | Endpoint already enforces `[Authorize]` + `[ValidateAntiForgeryToken]` (confirmed unchanged in `MarkdownController.cs`); `_QuestFormScripts` supplies the antiforgery token. Already-vetted surface, no new exposure this phase. | closed |
| T-68-02-I | Injection / Tampering (stored XSS) | `@Html.Markdown(Model.Description)` / `@Html.Markdown(Model.Backstory)` on Character Details desktop + mobile | medium | mitigate | Both calls route through `IMarkdownService.RenderToHtml(..., MarkdownRenderTarget.Web)`; HtmlSanitizer (Web profile) strips `<script>`, `javascript:` URLs, and attribute-injection payloads — proven by Phase 65 unit tests (RENDER-01). No raw `@Html.Raw` or unsanitized string interpolation introduced. Independently confirmed by both the phase verifier and code reviewer's direct file reads. | closed |
| T-68-02-R | Repudiation / integrity | Rendered output vs saved output | low | accept | Preview (68-01) and the saved read view both call the same `MarkdownRenderTarget.Web` pipeline — what a user previews is byte-identical to what renders on Details (RENDER-02). No divergence to exploit. | closed |
| T-68-03 | (verification only) | live app review | n/a | accept | No code changes — 68-03 only confirms rendered behavior and runtime view resolution. The stored-XSS mitigation (T-68-02-I) is delivered and tested in 68-02. | closed |
| T-68-SC | Tampering (supply chain) | EasyMDE 2.21.0 / FA v4-shim CDN assets | low | accept | No new package installs across all 3 plans. EasyMDE + FA v4-shims reused verbatim from Phase 66 (slopcheck `[OK]`, 2026-07-09) via the same SRI-hashed CDN URLs in `_QuestFormScripts.cshtml`. No version change, no new registry entry. | closed |

*Status: open · closed · open — below {block_on} threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-68-01 | T-68-01-P | CSRF preview endpoint already vetted in Phase 65/66; reused verbatim with no new exposure this phase | plan-time author | 2026-07-10 |
| AR-68-02 | T-68-02-R | Preview and saved-render share one code pathway by construction (RENDER-02); no divergence possible | plan-time author | 2026-07-10 |
| AR-68-03 | T-68-03 | Verification-only plan, introduces no code | plan-time author | 2026-07-10 |
| AR-68-04 | T-68-SC | No new dependency installs this phase; existing SRI-hashed CDN assets reused unchanged | plan-time author | 2026-07-10 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-07-10 | 6 | 6 | 0 | orchestrator (L1 grep-depth, register authored at plan time — short-circuit per asvs_level 1) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-07-10
