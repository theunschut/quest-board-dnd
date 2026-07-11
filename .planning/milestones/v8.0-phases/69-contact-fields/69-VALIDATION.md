---
phase: 69
slug: contact-fields
status: draft
nyquist_compliant: false
wave_0_complete: true
created: 2026-07-10
---

# Phase 69 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`QuestBoard.IntegrationTests`, `QuestBoard.UnitTests`) |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (WebApplicationFactory-based) |
| **Quick run command** | `dotnet test --filter FullyQualifiedName~Contacts` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30-60 seconds (quick filter), full suite per prior-phase precedent (269 unit + 396 integration as of Phase 68) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build` (view/CS compile check) + `dotnet test --filter FullyQualifiedName~Contacts`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green; manual UAT click-through covering Description toolbar/preview/render, 2+ notes with independent formatting, D-03 auto-collapse with unsaved-text-discard, and mobile viewport for both
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 69-01-XX | 01 | 1 | CONTACTMD-01 | Stored XSS (Tampering) | Description renders via `Html.Markdown()` → `IMarkdownService.RenderToHtml(..., Web)`, no `Html.Raw()` bypass introduced | manual (UAT) | — (justified: view-rendering behavior; `IMarkdownService.RenderToHtml` itself already unit-tested Phase 65, this phase only adds call sites) | N/A | ⬜ pending |
| 69-01-XX | 01 | 1 | CONTACTMD-02 | Stored XSS (Tampering) | Each note renders independently via its own `Html.Markdown(note.Text)` call — no formatting bleed between notes (verify with 2+ notes, one containing unclosed `**bold`) | manual (UAT) | — (justified: structural guarantee already exists pre-phase via the per-note `@foreach`; this phase preserves it, doesn't create it) | N/A | ⬜ pending |
| 69-01-XX | 01 | 1 | CONTACTMD-02 (D-03) | — | Opening one note's editor auto-collapses (hides + reverts unsaved text on) any other currently-open note editor | manual (UAT) | — (justified: pure client-side DOM/JS interaction, no automated test infra for this pattern in the codebase — matches Character phase precedent of zero new test files) | N/A | ⬜ pending |
| 69-01-XX | 01 | 1 | CONTACTMD-01/02 (access control regression) | CSRF (Spoofing) on `POST /markdown/preview`; access-control unchanged | `[Authorize]` + `[ValidateAntiForgeryToken]` on `/markdown/preview` still enforced once `Details.cshtml`/`Details.Mobile.cshtml` load the antiforgery token for the first time; `AddNote`/`EditNote`/`DeleteNote`/`Details` authorization unchanged by view-only edits | integration | `dotnet test --filter FullyQualifiedName~ContactsControllerIntegrationTests` | ✅ — `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` already covers `AddNote_AnyGroupMember_CanAddNoteToVisibleContact` and hidden-contact visibility | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. `ContactsControllerIntegrationTests.cs` already exercises the access-control surface this phase's view changes touch (`AddNote`/`EditNote`/`DeleteNote`/`Details`); no new automated coverage is being added, matching the precedent Phase 68 (Character) established — zero new test files, verification via manual UAT + re-running the existing regression suite to confirm no regression from the view/JS changes.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Description toolbar inserts syntax, Preview toggles, matches saved render | CONTACTMD-01 | Zero JS test tooling in this repo; EasyMDE's own insertion/preview logic already verified against source in Phase 66's research | Open Contact Create/Edit (desktop + mobile), use toolbar buttons, toggle Preview, compare against saved Details render |
| Two notes with different/unclosed formatting render independently | CONTACTMD-02 | Structural guarantee, but the actual rendered HTML in a live browser is the real proof | Add a note with `**bold` (unclosed) and a second note with normal `**bold**` text; confirm the first note's stray `**` does not affect the second note's rendering |
| Opening a note's editor auto-collapses any other open note editor, discarding unsaved text in the collapsed one | CONTACTMD-02 (D-03) | Pure client-side interaction state, no automated E2E harness in this repo | Open Edit on Note A, type unsaved text, open Edit on Note B; confirm Note A's editor closes and reverts to its original saved text (not the unsaved edit) |
| EasyMDE initializes correctly on first reveal of a note's Edit form (no zero-width/broken CodeMirror) | CONTACTMD-02 (Unknown 2, research Assumption A1) | Live-browser-only concern (CodeMirror hidden-container sizing); research recommends lazy-init specifically to avoid this, but the fix should be visually confirmed | Click Edit on any note, confirm the toolbar/textarea render at full width immediately, not just after a resize/scroll |
| Markdown list items (`<li>`) inside a Note render with correct contrast on mobile | CONTACTMD-02 (research Open Question 2) | `.contact-detail-card .note-item`'s CSS scope-out rule doesn't explicitly list `li` — inherited color chain not independently verified against a rendered browser | View a note containing a Markdown list on a mobile viewport (or real device); confirm list text is legible, not falling through to an unintended color |
| Existing multi-line Description/Note text displays without doubled spacing (pre-wrap removal) | Roadmap success criterion 3 | Visual/CSS regression check | Compare a multi-paragraph Description/Note before and after migration — no doubled blank lines between paragraphs |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references — none missing, existing `ContactsControllerIntegrationTests.cs` covers the touched surface
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
