# Phase 42: Site-Wide Toast Notification Redesign - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 42-site-wide-toast-notification-redesign
**Areas discussed:** Toast architecture, Dismiss behavior, Migration scope, Visual consistency

---

## Toast Architecture

| Option | Description | Selected |
|--------|-------------|----------|
| Shared partial | One `_Toasts.cshtml` partial rendered from all 3 layouts | ✓ |
| Per-view duplication | Copy Shop's toast markup into each of the ~20 views individually | |
| You decide | | |

**User's choice:** Shared partial (Recommended)
**Notes:** Confirmed there's no existing shared toast/alert partial in the codebase — every view duplicates its own markup today, contradicting Phase 39's D-10 claim that it lives in `_Layout.cshtml`.

| Option | Description | Selected |
|--------|-------------|----------|
| Standardize on Success/Error/Warning/Info | Update AccountController.cs's 3 outlier TempData keys to match the majority convention | |
| Support both key sets in the partial | No controller changes; partial checks both naming schemes | |
| You decide | | ✓ |

**User's choice:** You decide
**Notes:** Recorded as Claude's Discretion in CONTEXT.md, leaning toward standardization since a shared partial needs consistent keys.

| Option | Description | Selected |
|--------|-------------|----------|
| Plain partial view | `<partial name="_Toasts" />`, matches `_Calendar.cshtml`/`_ShopItemDetailsContent.cshtml` convention | ✓ |
| View Component | C# ViewComponent class encapsulating TempData-reading logic | |
| You decide | | |

**User's choice:** Plain partial view (Recommended)
**Notes:** No new architectural pattern introduced.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, include _Layout.GroupPicker.cshtml | Wire into all 3 layouts for full coverage/future-proofing | ✓ |
| No, just the 2 main layouts | Skip GroupPicker layout since it has no current flash-message use case | |
| You decide | | |

**User's choice:** Yes, include it (Recommended)

---

## Dismiss Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Success auto-hides, error/warning sticky | Success disappears after a delay; error/warning require manual dismiss | ✓ |
| Auto-hide everything | Match Shop's current behavior — every type auto-dismisses | |
| You decide | | |

**User's choice:** Success auto-hides, error/warning sticky (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Keep 5000ms | Matches Shop's existing success delay | ✓ |
| Shorter (3000ms) | Snappier feel | |
| You decide | | |

**User's choice:** Keep 5000ms (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Add dedicated Info style (blue) | Bootstrap bg-info toast header, distinct from Warning | ✓ (with follow-up) |
| Fold Info into Warning | Treat Info as Warning-styled | |
| You decide | | |

**User's choice:** Add dedicated Info style — but raised a clarifying question about the Shop "Mystical Merchant" toast's existing yellow coloring.
**Notes:** User clarified: "Currently this one is yellow, because it fits the 'style' of the shop... All other toast messages can follow the color scheme we set for them (success, error, warning)." Confirmed via follow-up question: the Merchant toast is a standalone novelty/theming feature (not a TempData flash message) and is explicitly out of scope — keeps its yellow/bg-warning styling regardless of the new standardized color scheme. This distinction is recorded as D-06 in CONTEXT.md.

---

## Migration Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Migrate Shop onto the shared partial too | Full unification, Shop's local toast markup removed | ✓ |
| Leave Shop's local toast markup as-is | Only restyle colors/timing, don't refactor markup | |
| You decide | | |

**User's choice:** Migrate Shop onto the shared partial too (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Keep GoldReceived as a bespoke addition | Shared partial handles standard types; GoldReceived stays Shop-specific extra markup | |
| You decide | | ✓ |

**User's choice:** You decide
**Notes:** Recorded as Claude's Discretion in CONTEXT.md, leaning toward keeping it bespoke since it's a unique celebratory element, not a generic message type.

| Option | Description | Selected |
|--------|-------------|----------|
| No exclusions — convert everything | All ~20 views convert, true site-wide | ✓ |
| Yes, exclude some views | User specifies exclusions | |

**User's choice:** No exclusions — convert everything (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Correct, leave validation errors alone | Only TempData flash messages convert; form validation untouched | ✓ (after clarification) |
| Actually, include those too | | |

**User's choice:** Correct, leave validation errors alone
**Notes:** User initially asked for clarification ("you mean the messages that are shown when a form is incorrectly filled?") before confirming. Confirmed: field-level/asp-validation-summary messages stay exactly as they are; only TempData-driven flash messages (Success/Error/Warning/Info/GoldReceived) become toasts.

---

## Visual Consistency

| Option | Description | Selected |
|--------|-------------|----------|
| Top-right, fixed | Matches Shop's existing `position-fixed top-0 end-0` | ✓ |
| Bottom-right | Alternative placement | |
| You decide | | |

**User's choice:** Top-right, fixed (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Solid colored header bar | Matches Shop's existing look (bg-success/bg-danger text-white + icon) | ✓ |
| Subtle left-border accent | More modern/muted style, new visual pattern | |
| You decide | | |

**User's choice:** Solid colored header bar (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| fa-exclamation-triangle (Warning) / fa-info-circle (Info) | Consistent with existing icon conventions, visually distinct from Error's circle | ✓ |
| You decide | | |

**User's choice:** fa-exclamation-triangle (Warning) / fa-info-circle (Info) (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Allow stacking | Bootstrap's default toast-container behavior, multiple toasts stack vertically | ✓ |
| Only show highest-severity | Suppress lower-priority messages when multiple are set | |
| You decide | | |

**User's choice:** Allow stacking (Recommended)

---

## Claude's Discretion

- TempData key naming standardization (Success/Error/Warning/Info vs. AccountController.cs's SuccessMessage/ErrorMessage/InfoMessage) — user deferred to Claude; CONTEXT.md leans toward standardizing.
- GoldReceived "+X gp" toast treatment (bespoke addition vs. other approach) — user deferred to Claude; CONTEXT.md leans toward keeping it bespoke.

## Deferred Ideas

None — discussion stayed within phase scope.
