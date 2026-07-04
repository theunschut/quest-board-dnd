# Phase 42: Site-Wide Toast Notification Redesign - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Convert all TempData-driven flash messages app-wide onto one shared Bootstrap toast mechanism, replacing today's fragmented state: ~20 views render a duplicated static `alert-dismissible` banner block, and Shop's `Index`/`Details` views (desktop + mobile) already have their own local Bootstrap toast implementation. This phase unifies all of it — including Shop's — onto a single shared partial rendered from all 3 layouts.

**Explicitly excluded:** ASP.NET form-validation messages (`asp-validation-summary`, field-level `asp-validation-for`) and Shop's non-flash "Mystical Merchant" novelty toast (random flavor-text dialogue, unrelated to TempData).

</domain>

<decisions>
## Implementation Decisions

### Toast Architecture
- **D-01:** Shared partial view `_Toasts.cshtml` rendered from all 3 layouts (`_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml`) — not per-view duplication. Eliminates the ~20-file duplication plus Shop's local toast markup in one place.
- **D-02:** Implemented as a plain Razor partial view (not a View Component) — matches this codebase's existing lightweight partial convention (`_Calendar.cshtml`, `_ShopItemDetailsContent.cshtml`); no new architectural pattern introduced.
- **D-03:** Wired into all 3 layouts including `_Layout.GroupPicker.cshtml`, even though `GroupPickerController` sets no TempData today — full site-wide coverage and future-proofing, at the cost of one extra `<partial>` line.

### Dismiss Behavior
- **D-04:** Success toasts auto-hide after 5000ms (matches Shop's existing delay exactly). Error and Warning toasts are **sticky** — no `data-bs-autohide`, manual dismiss (`btn-close`) only, since users may need time to read/act on them.
- **D-05:** Add a dedicated 4th toast type, **Info** (`bg-info`, blue) — distinct from Warning. Currently only used for the Profile page's pending email-change confirmation message, which today renders as a generic alert.
- **D-06:** Shop's "Mystical Merchant" random-flavor-text toast (`showMerchantDialog()`/`showMerchantToast()` in `Views/Shop/Index.cshtml`) is **explicitly out of scope** — it is not a TempData flash message, it's a standalone novelty/theming feature, and keeps its yellow/`bg-warning` styling untouched regardless of the new standardized color scheme. Surfaced mid-discussion by the user — do not conflate this with the real Warning toast type.

### Migration Scope
- **D-07:** Shop's `Index`/`Details` views (desktop + mobile — 4 files) migrate their existing local Success/Error toast markup onto the shared `_Toasts.cshtml` partial too. Full unification — no exceptions, matches the phase's "site-wide" intent.
- **D-08:** All ~20 remaining alert-banner views convert in this phase — no exclusions. See Canonical References below for the full file list.
- **D-09:** Form validation (`asp-validation-summary`, field-level `asp-validation-for`) is confirmed **out of scope** — a separate mechanism from TempData flash messages, untouched by this phase.

### Visual Consistency
- **D-10:** Toast container position stays fixed top-right (`position-fixed top-0 end-0`), matching Shop's existing placement exactly.
- **D-11:** Solid colored toast-header bar per type (`bg-success`/`bg-danger`/`bg-warning`/`bg-info` + `text-white`, FontAwesome icon) — matches Shop's existing look. Not a subtle left-border-accent style.
- **D-12:** Icons per type: `fa-check-circle` (Success, existing), `fa-exclamation-circle` (Error, existing), `fa-exclamation-triangle` (Warning, new — kept visually distinct from Error's circle), `fa-info-circle` (Info, new).
- **D-13:** Multiple simultaneous toasts (rare — no current code path sets more than one TempData key per request) stack vertically via Bootstrap's default toast-container behavior. No severity-suppression logic needed.

### Claude's Discretion

- **TempData key naming standardization:** Today most controllers use `Success`/`Error`/`Warning`, but `AccountController.cs`'s Profile-page actions use `SuccessMessage`/`ErrorMessage`/`InfoMessage` instead (3 call sites, `Views/Account/Profile.cshtml` + `.Mobile.cshtml`). User explicitly said "you decide" when asked whether to standardize on `Success`/`Error`/`Warning`/`Info` everywhere (updating those 3 outlier call sites) vs. having the shared partial support both naming schemes. **Lean:** standardize on `Success`/`Error`/`Warning`/`Info` — D-01 already requires one shared partial reading consistent keys, and supporting two parallel naming schemes indefinitely adds partial-logic complexity for no real benefit.
- **`GoldReceived` "+X gp" celebratory toast** (Shop `Details`, shown after a sale, currently rides alongside the Success message): user said "you decide" on how it fits with the shared partial. **Lean:** keep it as a bespoke Shop-specific toast layered on top of (not folded into) the shared partial's generic Success toast — it's a unique celebratory UI element, not a generic message type, and forcing it into the generic system would lose its distinct "+X gp" badge treatment.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Prior Phase Context
- `.planning/phases/39-shared-collision-aware-user-creation-email/39-DISCUSSION-LOG.md` — origin of this phase: the deferred "site-wide toast conversion" idea and the scope-tradeoff discussion that led to deferring it out of Phase 39
- `.planning/phases/39-shared-collision-aware-user-creation-email/39-CONTEXT.md` (D-10) — Phase 39's decision to stay on the static alert-banner pattern rather than partially converting. **Correction:** D-10's claim that the alert pattern "lives in `_Layout.cshtml`'s TempData rendering" is inaccurate — confirmed by direct source read that `_Layout.cshtml` has zero alert/TempData rendering; every view duplicates its own markup today.

### Existing Toast/Alert Implementation (design precedent)
- `QuestBoard.Service/Views/Shop/Index.cshtml:450-489` — existing local Bootstrap toast pattern (Success/Error) that is the visual reference for the new shared partial: `toast-container position-fixed top-0 end-0 p-3`, `bg-success text-white`/`bg-danger text-white` headers, `fa-check-circle`/`fa-exclamation-circle` icons, `data-bs-delay="5000"`, `data-bs-autohide="true"`
- `QuestBoard.Service/Views/Shop/Index.cshtml:514-572` — **out-of-scope** "Mystical Merchant" novelty toast (`showMerchantDialog`/`showMerchantToast` JS functions) — do not touch its theming (see D-06)
- `QuestBoard.Service/Views/Shop/Details.cshtml:53-93` (+ `Details.Mobile.cshtml:11-51` equivalent) — bespoke `GoldReceived` "+X gp" toast, in scope for migration alongside Success/Error (see Claude's Discretion)
- `QuestBoard.Service/Extensions/ControllerExtensions.cs` — `RedirectWithSuccess`/`RedirectWithError`/`RedirectWithWarning` helpers already standardize how most controllers set TempData; a parallel `RedirectWithInfo` will likely be needed for the new Info type

### Layout Injection Points
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` — main desktop layout; no current alert/toast rendering (see correction above)
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` — mobile layout
- `QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml` — third layout (used only by `GroupPicker/Index`), in scope per D-03
- `QuestBoard.Service/Views/_ViewStart.cshtml` — confirms only 2 layouts are auto-selected via `IsMobile`; `_Layout.GroupPicker.cshtml` is opted into explicitly by `Views/GroupPicker/Index.cshtml` + `.Mobile.cshtml`

### Views In Scope for Migration
Plain alert-banner views (~20 files, convert per D-08):
- `QuestBoard.Service/Views/Admin/Users.cshtml`
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` + `.Mobile.cshtml`
- `QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml` + `.Mobile.cshtml`
- `QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml` + `.Mobile.cshtml`
- `QuestBoard.Service/Views/Quest/Manage.cshtml` + `.Mobile.cshtml`
- `QuestBoard.Service/Views/Account/Login.cshtml`
- `QuestBoard.Service/Views/Account/ForgotPassword.cshtml` + `.Mobile.cshtml`
- `QuestBoard.Service/Views/Account/Profile.cshtml` + `.Mobile.cshtml`

Existing local-toast views to migrate onto the shared partial (4 files, per D-07):
- `QuestBoard.Service/Views/Shop/Index.cshtml` + `.Mobile.cshtml`
- `QuestBoard.Service/Views/Shop/Details.cshtml` + `.Mobile.cshtml`

Controllers setting TempData flash keys (reference for the migration, from a repo-wide scan):
- `QuestBoard.Service/Controllers/Admin/AccountController.cs`
- `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs`, `ShopController.cs`
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`
- `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs`, `GroupController.cs`

### TempData Key Naming (current inconsistency, to reconcile — see Claude's Discretion)
- `Success` / `Error` / `Warning` — majority convention, used in nearly all controllers
- `SuccessMessage` / `ErrorMessage` / `InfoMessage` — used only in `AccountController.cs`'s Profile-page actions
- `GoldReceived` — Shop-specific, non-generic, not a message-type key

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ControllerExtensions.RedirectWithSuccess/Error/Warning` — existing TempData-setting helpers most controllers already use; extending with a `RedirectWithInfo` keeps the established pattern consistent
- `_Calendar.cshtml`, `_ShopItemDetailsContent.cshtml` — existing precedent for lightweight shared partials in this codebase (informs the plain-partial-view choice, D-02)

### Established Patterns
- Two layouts auto-selected via `_ViewStart.cshtml`'s `IsMobile` check, plus a third opt-in layout (`_Layout.GroupPicker.cshtml`) — any shared UI element needs wiring into all 3 to be truly site-wide
- Bootstrap 5 `.toast` / `bootstrap.Toast` JS API already proven working in this codebase (Shop)

### Integration Points
- New `_Toasts.cshtml` partial included via `<partial name="_Toasts" />` in `_Layout.cshtml`, `_Layout.Mobile.cshtml`, and `_Layout.GroupPicker.cshtml`
- ~24 view files (20 alert-banner + 4 Shop toast) lose their local flash-message markup, relying on the shared partial instead
- `AccountController.cs`'s Profile-page TempData sets are the likely touch point if key standardization proceeds (Claude's discretion)

</code_context>

<specifics>
## Specific Ideas

- Shop's exact current implementation (`Views/Shop/Index.cshtml:450-489`) is the explicit visual reference: `toast-container position-fixed top-0 end-0 p-3`, `bg-success text-white`/`bg-danger text-white` headers, `fa-check-circle`/`fa-exclamation-circle` icons, `data-bs-delay="5000"`.
- User specifically distinguished the "Mystical Merchant" shop-flavor toast (yellow, thematic, not a flash message) from the real flash-message Warning color — an important nuance surfaced mid-discussion that should not be assumed from the code alone.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. No new scope-creep items surfaced this session.

</deferred>

---

*Phase: 42-Site-Wide Toast Notification Redesign*
*Context gathered: 2026-07-04*
