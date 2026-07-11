---
phase: 69
slug: contact-fields
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-07-10
---

# Phase 69 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser → `ContactsController` (Create/Edit POST) | Authenticated group member submits untrusted Description free-text | Free-text (Markdown source) |
| Browser → `POST /markdown/preview` | Editor Preview toggle (Description + per-note Notes) sends untrusted text for server-side render — now reachable from Details pages for the first time | Free-text (Markdown source, not persisted) |
| Contact Description / Note free-text → Details render | Stored untrusted free-text is converted to HTML and shown to every group member viewing the contact | Sanitized HTML |
| Multiple note edit `<form>`s on one page | Client-side DOM id space shared across N per-note editors | Client-side DOM ids only, no server trust implication |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-69-01 | Tampering | Contact Description free-text (write side) | high | mitigate | Write forms only collect/store raw text; no rendering sink added. All display is sanitized by `IMarkdownService.RenderToHtml(..., Web)` (delivered 69-02). Verified: `grep -rn "Html.Raw" QuestBoard.Service/Views/Contacts/*.cshtml` returns zero matches. `[StringLength(2000)]` on `ContactViewModel.Description` unchanged. | closed |
| T-69-02 | Spoofing | `POST /markdown/preview` reachable from Contact write forms | medium | mitigate | `MarkdownController` confirmed `[Authorize]` (class-level) + `[ValidateAntiForgeryToken]` (action-level). All 4 write forms (`Create`/`Create.Mobile`/`Edit`/`Edit.Mobile`) confirmed to include `_QuestFormScripts.cshtml`, which supplies `window.markdownAntiforgeryToken`. No new endpoint. | closed |
| T-69-03 | Tampering | `_MarkdownEditor.cshtml` `ElementId` override affecting existing call sites | low | accept | `ElementId` is additive and null at every pre-existing call site; `name="@Model.FieldName"` derivation untouched — confirmed unchanged by direct read, so no existing form's POST binding changed. | closed |
| T-69-04 | Tampering (stored XSS) | Description + Note free-text rendered on Details (desktop + mobile) | high | mitigate | All render goes through `Html.Markdown()` → `IMarkdownService.RenderToHtml(..., Web)` (Markdig + HtmlSanitizer Web profile), the same sanitizing choke point unit-tested in Phase 65 and reused verbatim for Character (68-SECURITY.md). Confirmed zero `Html.Raw` in any Contacts view; per-note rendering is one isolated `Html.Markdown(note.Text)` call per note (never concatenated). | closed |
| T-69-05 | Spoofing (CSRF) | `POST /markdown/preview` now reachable from `Details.cshtml`/`Details.Mobile.cshtml` (Add Note + per-note Edit) | high | mitigate | Both Details views confirmed to include `_QuestFormScripts.cshtml`, which calls `Antiforgery.GetAndStoreTokens` and sets `window.markdownAntiforgeryToken`, sent by `markdown-editor.js` as the `RequestVerificationToken` header. No new endpoint. | closed |
| T-69-06 | Tampering | Per-note editor DOM id collision misleading which note is edited | low | mitigate | `ElementId = $"Text_{note.Id}"` confirmed present in both `Details.cshtml:160` and `Details.Mobile.cshtml:145`, making each editor's DOM id/`<label for>` unique. POST target is decided by the submitted `<form asp-action="EditNote">`'s own hidden `id`/`contactId`, not the textarea id — never server-exploitable, but ambiguity removed for the user. `name="Text"` stays constant so binding is correct. | closed |
| T-69-07 | Information Disclosure | EasyMDE CDN assets now downloaded by every Details visitor (Add Note visible to any group member) | low | accept | Functional cost, not a leak — no contact data crosses a new boundary; the Add Note form was already visible to any authenticated group member. | closed |
| T-69-08 | Tampering (stored XSS, verification) | Live confirmation of T-69-04's mitigation | high | mitigate | `69-03-SUMMARY.md` documents a live operator/assistant XSS spot-check — a `<script>`-style payload typed into a note confirmed to render inert, not executed — exercising the `Html.Markdown()` → sanitizer path in the running app. | closed |
| T-69-SC | Tampering (supply chain) | npm/NuGet installs | low | accept | No new packages across all 3 plans — Markdig/Ganss.Xss/EasyMDE reused verbatim (slopcheck `[OK]`, Phase 66/68). No install task in any plan this phase. | closed |

*Status: open · closed · open — below {block_on} threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-69-01 | T-69-03 | `ElementId` is additive/nullable; existing `name`-based binding verified unchanged by direct read | plan-time author | 2026-07-10 |
| AR-69-02 | T-69-07 | CDN asset download is a functional cost, not a data-exposure — the form was already visible to the same audience | plan-time author | 2026-07-10 |
| AR-69-03 | T-69-SC | No new dependency installs this phase; existing SRI-hashed CDN assets and NuGet packages reused unchanged | plan-time author | 2026-07-10 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-07-10 | 9 | 9 | 0 | orchestrator (L1 grep-depth, register authored at plan time — short-circuit per asvs_level 1) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-07-10
