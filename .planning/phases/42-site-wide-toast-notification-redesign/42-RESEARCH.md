# Phase 42: Site-Wide Toast Notification Redesign - Research

**Researched:** 2026-07-04
**Domain:** ASP.NET Core 10 MVC / Razor views — TempData flash-message rendering, Bootstrap 5 toast component, shared partial views
**Confidence:** HIGH (all findings verified by direct source read against the live codebase)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Shared partial view `_Toasts.cshtml` rendered from all layouts — not per-view duplication.
- **D-02:** Implemented as a plain Razor partial view (not a View Component) — matches this codebase's existing lightweight partial convention (`_Calendar.cshtml`, `_ShopItemDetailsContent.cshtml`).
- **D-03:** Wired into `_Layout.GroupPicker.cshtml` too, even though `GroupPickerController` sets no TempData today.
- **D-04:** Success toasts auto-hide after 5000ms. Error and Warning toasts are sticky (no `data-bs-autohide`, manual dismiss only).
- **D-05:** Add a dedicated 4th toast type, Info (`bg-info`, blue).
- **D-06:** Shop's "Mystical Merchant" random-flavor-text toast is explicitly out of scope — not a TempData flash message, keeps yellow/`bg-warning` styling untouched.
- **D-07:** Shop's `Index`/`Details` views (desktop + mobile — 4 files) migrate their existing local Success/Error toast markup onto the shared partial too.
- **D-08:** All ~20 remaining alert-banner views convert in this phase — no exclusions.
- **D-09:** Form validation (`asp-validation-summary`, `asp-validation-for`) is confirmed out of scope.
- **D-10:** Toast container position stays fixed top-right (`position-fixed top-0 end-0`).
- **D-11:** Solid colored toast-header bar per type (`bg-success`/`bg-danger`/`bg-warning`/`bg-info` + `text-white`, FontAwesome icon).
- **D-12:** Icons per type: `fa-check-circle` (Success), `fa-exclamation-circle` (Error), `fa-exclamation-triangle` (Warning, new), `fa-info-circle` (Info, new).
- **D-13:** Multiple simultaneous toasts stack vertically via Bootstrap's default toast-container behavior. No severity-suppression logic needed.

### Claude's Discretion

- **TempData key naming standardization:** Lean confirmed in UI-SPEC — standardize on `Success`/`Error`/`Warning`/`Info` everywhere, updating `AccountController.cs`'s 3 outlier call sites away from `SuccessMessage`/`ErrorMessage`/`InfoMessage`.
- **`GoldReceived` "+X gp" celebratory toast:** Lean confirmed in UI-SPEC — keep as bespoke Shop-specific toast layered alongside the shared partial's generic Success toast, preserving the existing dual-render of `@TempData["Success"]` as a `<small>` sub-line inside the GoldReceived toast.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. No new scope-creep items surfaced this session.

</user_constraints>

## Summary

This phase is a pure refactor/consolidation of an already-decided visual and interaction design (locked in `42-CONTEXT.md` and `42-UI-SPEC.md`). The research below does not revisit those decisions — it verifies the current-state code facts the planner needs to sequence tasks safely, and surfaces three material discrepancies between CONTEXT.md's description of the codebase and what direct source inspection actually found.

**Primary recommendation:** Build `_Toasts.cshtml` exactly per the UI-SPEC, wire it into **5 layouts** (not 3 — see Critical Discrepancy #1 below), standardize all TempData keys to `Success`/`Error`/`Warning`/`Info` (including fixing 2 call sites in `AccountController.cs` that currently write orphaned messages — see Discrepancy #2), and consolidate the toast-init JS into `wwwroot/js/site.js` (already the site-wide global script, referenced by every layout including the Platform ones).

**Critical discrepancies found (all HIGH confidence, verified by direct read):**

1. **5 layouts need the partial, not 3.** The Platform Area (`Areas/Platform/Views/Group/*.cshtml`, `Areas/Platform/Views/Users/Index.cshtml`, and their `.Mobile` pairs) uses its own layout pair — `_Layout.Platform.cshtml` / `_Layout.Platform.Mobile.cshtml` — selected by `Areas/Platform/Views/_ViewStart.cshtml`, completely separate from the root `_Layout.cshtml`/`_Layout.Mobile.cshtml`/`_Layout.GroupPicker.cshtml` trio CONTEXT.md names. Both Platform layouts must also get `<partial name="_Toasts" />`.
2. **`AccountController.cs`'s `ErrorMessage`/`InfoMessage` TempData values are currently never rendered anywhere** — not "rendered as a generic alert" as CONTEXT.md/UI-SPEC D-05 states. `Profile.cshtml`/`.Mobile.cshtml` only check `TempData["SuccessMessage"]`; they have no markup for `ErrorMessage` or `InfoMessage` at all. Separately, `ConfirmEmailChange()` sets `TempData["SuccessMessage"]`/`["ErrorMessage"]` but redirects to `Login`, which only checks `TempData["Success"]`/`["Error"]` (the standard keys) — so those messages are silently dropped today too. This is good news for the migration (no working behavior to preserve for these 2 call sites beyond "the message should now actually appear"), but the planner should know it's fixing a latent bug, not preserving parity.
3. **`_Layout.Platform.Mobile.cshtml` (`Areas/Platform/Views/Shared/`) appears to be dead code today.** `Areas/Platform/Views/_ViewStart.cshtml` unconditionally sets `Layout = "_Layout.Platform"` with no `IsMobile` branch (unlike the root `_ViewStart.cshtml`, which does branch). No controller action or view overrides `Layout`. Two CSS file header comments (`platform-group.mobile.css`, `platform-users.mobile.css`) assert mobile Platform views render "via `_Layout.Platform.Mobile.cshtml`" — that comment is incorrect against current source; those mobile-optimized view files (`.Mobile.cshtml` content) actually render inside the **desktop** `_Layout.Platform.cshtml` chrome. This is a pre-existing bug, unrelated to toasts, and out of scope for this phase — but the planner must still wire `<partial name="_Toasts" />` into `_Layout.Platform.Mobile.cshtml` for future-proofing/completeness (mirroring D-03's reasoning for `_Layout.GroupPicker.cshtml`), since fixing it is not part of this phase.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Toast markup rendering | Frontend Server (SSR) | — | Razor partial reads `TempData` server-side per request; no client-side state management involved |
| Toast show/hide/autohide timing | Browser / Client | — | `bootstrap.Toast` JS API, purely client-side once markup is in the DOM |
| TempData key writing | API / Backend (MVC Controllers) | — | Controllers/`ControllerExtensions` set `TempData[...]` before redirect; this phase adds `RedirectWithInfo` alongside existing helpers |
| Toast-init script hosting | Browser / Client (static asset) | — | Currently duplicated inline per-view; consolidates into `wwwroot/js/site.js`, already loaded by all 5 layouts |
| Layout wiring (`<partial>` include) | Frontend Server (SSR) | — | Razor `_ViewStart.cshtml`/layout files control which partial renders per request; no business logic |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Bootstrap | 5.3.0 (CDN, `cdn.jsdelivr.net/npm/bootstrap@5.3.0`) `[VERIFIED: direct grep of all 5 layout files]` | `.toast` component + `bootstrap.Toast` JS API | Already the exact version pinned across every layout in this app; no version bump needed or in scope |
| FontAwesome | 6.4.0 (CDN, `cdnjs.cloudflare.com/.../font-awesome/6.4.0`) `[VERIFIED: direct grep]` | Toast header icons (`fa-check-circle`, `fa-exclamation-circle`, `fa-exclamation-triangle`, `fa-info-circle`) | Already vendored site-wide; all 4 icon classes used in the new design already exist in 6.4.0 |

No new packages are introduced by this phase — both dependencies are already CDN-vendored across every layout. Package Legitimacy Audit is not applicable (see below).

### Supporting
None — this phase adds zero new dependencies. It only reorganizes existing Razor/Bootstrap/FontAwesome usage into one shared partial.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Plain Razor partial (`_Toasts.cshtml`) | ASP.NET Core View Component | View Components add DI-friendly logic encapsulation but this codebase has zero View Components today (`_Calendar.cshtml`, `_ShopItemDetailsContent.cshtml` are both plain partials) — introducing the pattern here would be inconsistent per D-02, already decided against |
| Server-rendered conditional toasts | Client-side JS toast library (e.g., toastr, Notyf) | Would require a new client dependency; TempData-driven server rendering is simpler for this app's non-SPA request/redirect flow and matches the existing Shop precedent exactly |

**Installation:** None required — no new packages.

## Package Legitimacy Audit

Not applicable. This phase installs zero external packages (no `npm install`, no NuGet additions). Bootstrap and FontAwesome are pre-existing CDN `<script>`/`<link>` tags already present in every layout file — this phase only adds a new partial view and JS function using APIs already loaded.

## Architecture Patterns

### System Architecture Diagram

```
Controller action (e.g. GroupController.RemoveMember)
        │
        ▼
  TempData["Success"|"Error"|"Warning"|"Info"] = "message"
        │  (via RedirectWithSuccess/Error/Warning/Info OR raw TempData[key] = ...)
        ▼
  RedirectToAction(...)
        │
        ▼
┌───────────────────────────────────────────────────────────┐
│  Razor view rendering pipeline for the redirected-to action │
│                                                             │
│  _ViewStart.cshtml picks Layout                            │
│    - root: "_Layout" or "_Layout.Mobile" (IsMobile check)  │
│    - Platform area: "_Layout.Platform" (unconditional)     │
│    - GroupPicker: explicit per-view Layout override         │
│                                                             │
│  MobileViewLocationExpander picks the .cshtml/.Mobile.cshtml│
│  FILE to render as the view body (independent of Layout!)  │
│                                                             │
│  Layout renders:                                           │
│    <main>@RenderBody()</main>   ← view-specific content     │
│    <partial name="_Toasts" />   ← NEW: reads TempData here  │
│    <footer>...</footer>                                     │
│    <script src="site.js">        ← toast-init JS lives here │
└───────────────────────────────────────────────────────────┘
        │
        ▼
  Browser: DOMContentLoaded → document.querySelectorAll('.toast')
           → new bootstrap.Toast(el) → .show()
        │
        ▼
  Toast displays top-right; auto-hides (Success/Info, 5000ms)
  or stays sticky until manual btn-close (Error/Warning)
```

### Recommended Project Structure
```
QuestBoard.Service/
├── Views/
│   └── Shared/
│       ├── _Toasts.cshtml              # NEW — shared partial, all 4 generic types + GoldReceived
│       ├── _Layout.cshtml              # add <partial name="_Toasts" />
│       ├── _Layout.Mobile.cshtml       # add <partial name="_Toasts" />
│       └── _Layout.GroupPicker.cshtml  # add <partial name="_Toasts" />
├── Areas/Platform/Views/Shared/
│   ├── _Layout.Platform.cshtml         # add <partial name="_Toasts" /> (NOT in original CONTEXT.md scope — see Discrepancy #1)
│   └── _Layout.Platform.Mobile.cshtml  # add <partial name="_Toasts" /> (same)
├── wwwroot/js/
│   └── site.js                         # consolidate toast-init JS here (already globally loaded)
└── Extensions/
    └── ControllerExtensions.cs         # add RedirectWithInfo
```

### Pattern 1: Shared TempData-driven toast partial
**What:** A single `_Toasts.cshtml` that reads `TempData["Success"|"Error"|"Warning"|"Info"|"GoldReceived"]`, rendering a `.toast-container` with conditional `.toast` blocks — one per key present.
**When to use:** Rendered unconditionally from every layout; each `@if (TempData[key] != null)` block is a no-op when the key is absent, so it's safe to include everywhere including `_Layout.GroupPicker.cshtml` where no controller currently sets TempData.
**Example (verified pattern, ported from the existing Shop reference):**
```razor
@* Source: QuestBoard.Service/Views/Shop/Index.cshtml:450-478 (existing reference implementation) *@
<div class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 1055;">
    @if (TempData["Success"] != null)
    {
        <div class="toast show" role="alert" data-bs-autohide="true" data-bs-delay="5000">
            <div class="toast-header bg-success text-white">
                <i class="fas fa-check-circle me-2"></i>
                <strong class="me-auto">Success</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">@TempData["Success"]</div>
        </div>
    }
    @if (TempData["Error"] != null)
    {
        <div class="toast show" role="alert">
            <div class="toast-header bg-danger text-white">
                <i class="fas fa-exclamation-circle me-2"></i>
                <strong class="me-auto">Error</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">@TempData["Error"]</div>
        </div>
    }
    @* Warning, Info, and GoldReceived blocks follow the same shape per UI-SPEC *@
</div>
```
Note: sticky types (Error/Warning) omit `data-bs-autohide`/`data-bs-delay` entirely per D-04 — do not set `data-bs-autohide="false"` explicitly since Bootstrap's default is already non-autohide when the attribute is absent, matching the UI-SPEC's exact wording.

### Pattern 2: Consolidated toast-init script
**What:** Move the identical `DOMContentLoaded` → `querySelectorAll('.toast')` → `new bootstrap.Toast(el).show()` snippet (currently duplicated in 4 separate `@section Scripts` blocks: `Shop/Index.cshtml`, `Index.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`) into `wwwroot/js/site.js`, which is already `<script src="~/js/site.js">`-included by every layout (`_Layout.cshtml:220`, `_Layout.Mobile.cshtml:188`, `_Layout.GroupPicker.cshtml:34`, `_Layout.Platform.cshtml:49`, `_Layout.Platform.Mobile.cshtml:75`).
**When to use:** Add the init logic to `site.js`'s existing `document.addEventListener('DOMContentLoaded', function() { ... })` block (site.js already has one at the bottom of the file — extend it, don't add a second listener).
**Caveat:** The Shop "Mystical Merchant" novelty toast (`showMerchantDialog`/`showMerchantToast` in `Index.cshtml:514-572`) creates and initializes its OWN `bootstrap.Toast` instance dynamically at click-time — it does NOT rely on the `DOMContentLoaded` bulk-init. Moving the bulk-init to `site.js` does not affect or need to touch this function; it stays untouched in `Index.cshtml` per D-06.

### Anti-Patterns to Avoid
- **Two parallel TempData key schemes surviving in `_Toasts.cshtml`:** don't add branching logic to check both `Success` and `SuccessMessage` — CONTEXT.md's discretion lean (and UI-SPEC's confirmation) already settled on standardizing to one scheme. Update the 3 `AccountController.cs` call sites instead.
- **Re-initializing already-shown toasts:** the bulk `querySelectorAll('.toast')` init runs once per page load; do not also wire it into any AJAX-partial-reload path (Shop's modal `itemDetailsBody` swap via `fetch()` does not touch `.toast` elements, so no conflict exists today — but a future task should not assume `_Toasts.cshtml` needs re-running after an AJAX swap since it's a layout-level partial that always renders fresh on full page navigation).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Toast show/auto-hide/dismiss timing | Custom `setTimeout`+`classList` toggle logic | `bootstrap.Toast` JS API (`data-bs-autohide`, `data-bs-delay`, `data-bs-dismiss="toast"`) | Already proven working in Shop; reinventing it risks losing accessibility (`role="alert"`) and animation behavior Bootstrap provides for free |
| Multiple-toast stacking | Custom flexbox/position offset math | Bootstrap's default `.toast-container` vertical stacking (D-13) | Bootstrap's `.toast-container` already stacks children in document order with `p-3` gap; no CSS needed |

**Key insight:** This phase is explicitly a consolidation of an already-correct pattern (Shop's toasts), not new UI engineering — the only real net-new code is the `Info` type, the `RedirectWithInfo` extension method, and the JS/partial consolidation plumbing.

## Runtime State Inventory

> Not applicable — this is a code/markup refactor phase (Razor views, a C# extension method, a JS file). No databases, external services, OS-level registrations, secrets, or build artifacts reference the TempData key strings (`Success`/`Error`/`Warning`/`Info`/`SuccessMessage`/etc.) outside of the C#/Razor source itself.

- **Stored data:** None — TempData is transient per-request state (session/cookie-backed by ASP.NET Core), never persisted to a database table or external store.
- **Live service config:** None — no n8n/Datadog/Tailscale/Cloudflare equivalents in this stack for this concern.
- **OS-registered state:** None — no Task Scheduler/pm2/launchd/systemd entries reference these string keys.
- **Secrets/env vars:** None — TempData key names are not read from configuration or environment variables anywhere in this codebase.
- **Build artifacts:** None — no compiled/cached artifact embeds these key names outside the source tree.

## Common Pitfalls

### Pitfall 1: Missing the Platform Area's separate layout pair
**What goes wrong:** If the planner follows CONTEXT.md's literal "3 layouts" list, the Platform Area (`Areas/Platform/Views/Group/*.cshtml`, `Areas/Platform/Views/Users/Index.cshtml`) — which is explicitly listed in CONTEXT.md's "Views In Scope for Migration" — will render alert-banner-free views with no toast container at all, because those views resolve to `_Layout.Platform.cshtml`/`_Layout.Platform.Mobile.cshtml`, not the root layouts.
**Why it happens:** `Areas/Platform/Views/_ViewStart.cshtml` sets `Layout = "_Layout.Platform"` independently of the root `Views/_ViewStart.cshtml`. This is a separate, per-area `_ViewStart.cshtml` file that CONTEXT.md's Canonical References section did not enumerate.
**How to avoid:** Add `<partial name="_Toasts" />` to both `Areas/Platform/Views/Shared/_Layout.Platform.cshtml` and `_Layout.Platform.Mobile.cshtml` in the same plan wave as the other 3 layouts.
**Warning signs:** After migration, a manual test of `Group/Members.cshtml`'s "Add User" flow shows no toast at all (the alert-banner markup was removed but no partial exists in its layout to replace it).

### Pitfall 2: Assuming `AccountController.cs`'s `*Message` keys have working current behavior to preserve
**What goes wrong:** Treating the `SuccessMessage`/`ErrorMessage`/`InfoMessage` → `Success`/`Error`/`Info` rename as a like-for-like behavior port risks under-testing, since 2 of the 3 current call sites (`ConfirmEmailChange`'s Error path, `Edit`'s InfoMessage path) produce **no visible message today** due to a Login-page/Profile-page key mismatch (see Critical Discrepancy #2 above).
**Why it happens:** The bug is subtle — `TempData[key] != null` conditionals silently no-op when the key doesn't match what the view checks; there's no exception or visible symptom in normal use.
**How to avoid:** After the key rename, manually verify (or add a UAT step) that: (a) an expired/invalid `ConfirmEmailChange` link now shows an Error toast on the Login page, and (b) an email-change request from `Edit` now shows an Info toast on the Profile page. Both are new-to-the-user-visible behavior, not regressions, but should be called out in the plan's verification steps so it isn't mistaken for scope creep.
**Warning signs:** If a reviewer says "this Info toast wasn't showing before, are we sure this is in scope" — point to this pitfall; it's an intentional fix that falls naturally out of the key standardization, not additional scope.

### Pitfall 3: Duplicating the toast-init script instead of consolidating it
**What goes wrong:** If the planner leaves the toast-init `<script>` in each of Shop's 4 `@section Scripts` blocks (rather than moving it to `site.js`), those 4 views will double-initialize toasts once `_Toasts.cshtml` is also rendering from the layout — Bootstrap doesn't error on double-init, but it's dead duplicated code that recreates exactly the fragmentation this phase is meant to eliminate.
**Why it happens:** `@section Scripts` blocks are easy to leave behind since they don't visually break anything if left in place (harmless redundant `querySelectorAll` + re-`new bootstrap.Toast()` calls).
**How to avoid:** As part of migrating Shop's 4 views (D-07), explicitly delete their local `@section Scripts` toast-init blocks once the identical logic is confirmed present in `site.js`. Leave the Shop-specific `showMerchantDialog`/`showMerchantToast` functions in `Index.cshtml` untouched (D-06) — only remove the generic bulk-init snippet.
**Warning signs:** Grep for `new bootstrap.Toast` after the phase completes — it should appear exactly twice: once in the consolidated `site.js` init, once in `showMerchantToast()` (Shop's novelty toast, which self-creates its own toast element dynamically and must keep its own `new bootstrap.Toast(newToast)` call).

## Code Examples

### Extending ControllerExtensions.cs with RedirectWithInfo
```csharp
// Source: QuestBoard.Service/Extensions/ControllerExtensions.cs (existing file, exact current content read this session)
/// <summary>
/// Sets TempData["Info"] and redirects to the given action.
/// </summary>
internal static IActionResult RedirectWithInfo(this Controller controller, string action, string message)
    => controller.RedirectWithMessage(action, "Info", message);
```
This follows the exact one-line pattern of the 3 existing sibling methods (`RedirectWithSuccess`/`RedirectWithError`/`RedirectWithWarning`) — no new abstraction needed.

### Current Shop toast-init script (to be moved into site.js verbatim)
```javascript
// Source: QuestBoard.Service/Views/Shop/Index.cshtml:481-489 (one of 4 identical copies)
document.addEventListener('DOMContentLoaded', function() {
    const toastElements = document.querySelectorAll('.toast');
    toastElements.forEach(function(toastElement) {
        const toast = new bootstrap.Toast(toastElement);
        toast.show();
    });
});
```
`wwwroot/js/site.js` already has its own `document.addEventListener('DOMContentLoaded', function() { ... })` block (lines 273-304) — the toast-init lines should be added inside that existing listener, not as a second separate listener registration.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| ~20 views each duplicate their own `alert alert-dismissible` TempData block | 1 shared `_Toasts.cshtml` partial rendered from every layout | This phase | Eliminates ~20 duplicated markup blocks; single point of change for future toast types |
| Shop's local toast markup + local init script (4 files) | Same shared partial + `site.js`-hosted init | This phase | Removes the last 2 duplicate init scripts; Shop's toasts become indistinguishable from the rest of the site except for the bespoke GoldReceived block |
| 2 parallel TempData key naming schemes (`Success`/`Error`/`Warning` vs `SuccessMessage`/`ErrorMessage`/`InfoMessage`) | 1 standardized scheme (`Success`/`Error`/`Warning`/`Info`) | This phase | Fixes 2 silently-broken message paths in `AccountController.cs` (see Pitfall 2) as a side effect |

**Deprecated/outdated:** None — Bootstrap 5.3.0's `.toast` component is current and already the newest Bootstrap version used anywhere in this app; no framework-level upgrade involved.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | No other controller sets `TempData["SuccessMessage"/"ErrorMessage"/"InfoMessage"]` beyond the 3 call sites found in `AccountController.cs` — confirmed by a full repo-wide grep this session, so this is actually `[VERIFIED: ripgrep across QuestBoard.Service]`, not assumed. Listed here only for completeness since the grep covers `.cs` and `.cshtml` files only (not e.g. any JS-side TempData reads, of which none exist in this codebase). | Package Legitimacy / TempData Key Naming | Low — grep coverage was total across the Service project; a missed occurrence would surface immediately as a visibly broken alert during manual QA of that specific flow |

**This table is nearly empty because every claim in this research was verified by direct source read or repo-wide grep this session** — no training-data guesses were used for file paths, TempData keys, layout structure, or Bootstrap/FontAwesome versions. The one item logged above is included out of an abundance of caution, not because contradicting evidence was found.

## Open Questions

1. **Should `_Layout.Platform.Mobile.cshtml`'s dead-code status be fixed in this phase or left alone?**
   - What we know: The layout file exists, has offcanvas mobile nav markup, and correct `site.css`/`mobile.css` styling — but is currently unreachable because `Areas/Platform/Views/_ViewStart.cshtml` never selects it.
   - What's unclear: Whether fixing this (making the Platform area's `_ViewStart.cshtml` mobile-aware like the root one) is within this phase's blast radius, or a separate bug-fix phase.
   - Recommendation: Treat as strictly out of scope for Phase 42 — this phase is about toasts, not layout selection logic. Still add `<partial name="_Toasts" />` to `_Layout.Platform.Mobile.cshtml` for completeness/future-proofing (mirrors the reasoning behind D-03 for `_Layout.GroupPicker.cshtml`), but do not attempt to fix the underlying dead-code bug. Flag it to the user as a discovered-but-out-of-scope issue for a future phase.

2. **Does `Account/Login.Mobile.cshtml` (a file CONTEXT.md's canonical view list omitted) need migrating too?**
   - What we know: `Views/Account/Login.Mobile.cshtml` exists (confirmed via glob) and almost certainly duplicates `Login.cshtml`'s `TempData["Success"]`/`["Error"]` alert-banner block, matching the pattern of every other `.cshtml`/`.Mobile.cshtml` pair in this migration.
   - What's unclear: Its exact current markup wasn't read this session (time-boxed to the sample CONTEXT.md called out), but there is no plausible reason it would differ from the pattern of every other reviewed `.Mobile.cshtml` file.
   - Recommendation: Add `Views/Account/Login.Mobile.cshtml` to the migration file list — treat it as included under D-08's "no exclusions" umbrella; CONTEXT.md's omission was very likely a listing oversight, not an intentional exclusion.

## Environment Availability

Not applicable — this phase has no new external tool/service/runtime dependencies. Bootstrap and FontAwesome are CDN-loaded `<link>`/`<script>` tags already present in every layout; no local install, package manager, or service availability check is needed.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None detected — no `*.Tests` project, no `pytest`/`jest`/`vitest` config found in this repo `[VERIFIED: no test project found in solution structure during this session's exploration]` |
| Config file | none |
| Quick run command | n/a |
| Full suite command | n/a |

This project has no automated test suite. Verification for this phase is manual/UAT-based (matches the pattern of prior phases in this milestone, e.g. Phase 39/40/41's human-verify checkpoints).

### Phase Requirements → Test Map
No REQ-IDs are mapped to this phase (confirmed: `.planning/REQUIREMENTS.md` v6.1 traceability table has no Phase 42 entry — this phase is a deferred-idea promotion, not a numbered requirement). Verification should instead walk each of the ~24+2 migrated views and the Platform layouts, confirming:
- Success/Error/Warning/Info toasts render top-right, correct color/icon, correct autohide behavior.
- Shop's GoldReceived toast still shows the "+X gp" badge alongside (not replacing) the generic Success toast.
- The Mystical Merchant novelty toast is visually unchanged.
- `Account/Profile` now shows an Info toast for pending email-change confirmations (new-but-intentional visible behavior — see Pitfall 2).

### Sampling Rate
- **Per task commit:** Manual browser check of the specific view(s) touched by that task.
- **Per wave merge:** Full click-through of all 5 layouts (at least one view each) plus the Shop 4-file group.
- **Phase gate:** Full manual UAT pass per the Verification Architecture above before `/gsd-verify-work`.

### Wave 0 Gaps
None — no test framework exists in this repo to gap-fill; this phase's Nyquist validation is inherently manual/UAT-based, consistent with prior phases in this milestone.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Not touched by this phase |
| V3 Session Management | No | TempData rides the existing session/cookie mechanism, unchanged by this phase |
| V4 Access Control | No | Not touched by this phase |
| V5 Input Validation | No | This phase renders pre-existing, already-escaped Razor `@TempData[...]` output (Razor HTML-encodes by default) — no new user input paths introduced |
| V6 Cryptography | No | Not applicable |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Reflected XSS via TempData message content | Tampering/Information Disclosure | Razor `@TempData["Key"]` auto-HTML-encodes by default (no `@Html.Raw` used anywhere in the existing Shop/alert-banner reference code) — the new `_Toasts.cshtml` partial must continue using plain `@TempData[...]` interpolation, never `@Html.Raw(TempData[...])`, to preserve this existing protection |

No new attack surface is introduced by this phase — it relocates existing, already-safe TempData rendering into one shared file.

## Sources

### Primary (HIGH confidence)
- Direct source reads this session: `ControllerExtensions.cs`, `AccountController.cs` (full), `AdminController.cs` (grep), `GroupController.cs` (partial), all 5 layout files, `_ViewStart.cshtml` (root + Platform area), `MobileViewLocationExpander.cs`, `MobileDetectionMiddleware.cs`, `Program.cs` (middleware registration), `site.js`, `Shop/Index.cshtml`, `Shop/Index.Mobile.cshtml`, `Shop/Details.cshtml`, `Shop/Details.Mobile.cshtml`, `Admin/Users.cshtml`, `Account/Profile.cshtml`, `Account/Profile.Mobile.cshtml`, `Account/Login.cshtml`, `Account/ForgotPassword.cshtml`, `Areas/Platform/Views/Group/Members.cshtml`, `Areas/Platform/Views/Group/Index.Mobile.cshtml`, `Areas/Platform/Views/Users/Index.Mobile.cshtml`, `Quest/Manage.cshtml`
- Repo-wide grep for `TempData\[` across `QuestBoard.Service` — full inventory of every TempData flash-message write and read site

### Secondary (MEDIUM confidence)
None — all findings this session were directly verified against source, not inferred from documentation or web search (this is an internal-codebase-only research task, no external library research was needed beyond confirming Bootstrap/FontAwesome versions already pinned in the codebase).

### Tertiary (LOW confidence)
None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Bootstrap 5.3.0 / FontAwesome 6.4.0 versions read directly from every layout's CDN URL; no new packages introduced
- Architecture: HIGH — layout structure, `_ViewStart.cshtml` resolution, and `MobileViewLocationExpander` behavior all verified by direct source read, including tracing an undocumented 4th/5th layout pair CONTEXT.md missed
- Pitfalls: HIGH — all 3 pitfalls trace to specific line numbers read this session, not inferred

**Research date:** 2026-07-04
**Valid until:** 30 days (stable internal codebase facts; low churn risk since no external dependencies are involved)
