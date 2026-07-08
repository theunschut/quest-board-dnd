---
phase: 43-mobile-parity-fixes
reviewed: 2026-07-04T00:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
  - QuestBoard.Service/wwwroot/css/mobile.css
  - QuestBoard.Service/wwwroot/css/quest-log.mobile.css
  - QuestBoard.Service/wwwroot/css/site.css
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 43: Code Review Report

**Reviewed:** 2026-07-04
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

Reviewed the new mobile Quest Log view (`Index.Mobile.cshtml`), the new global mobile stylesheet (`mobile.css`), the new `quest-log.mobile.css`, and the diff against `site.css`. The mobile view and its dedicated stylesheet are small, well-scoped, and consistent with existing project conventions (whole-card `onclick` navigation, `.mobile.css` per-page split, `asp-append-version`). No bugs found in the two new mobile-specific files themselves.

**Correction (post-review verification):** The original draft of this report flagged a "Critical" issue (`.signup-seal-image` leaking to global CSS scope via an unclosed `@media` block) as introduced/worsened by this phase. That attribution was wrong — it was produced from an incorrectly computed diff base that spanned far more history than this phase's actual changes. Direct verification (`git show` on this phase's actual two commits touching `site.css`, plus `git log`/blame on the `.signup-seal-image` region) confirms:
- This phase's only `site.css` change is the `body`→`body::before` background-fix edit at lines ~374-393 (commit `bdd0951`).
- The unclosed `@media (max-width: 480px)` block and the `.signup-seal-image`/`.seal-image` global-scope leak (originally reported at lines 969-1007) pre-date this phase by many commits (traceable back to the project's initial rename/setup history) and were never touched by phase 43.

The underlying bug is real and worth fixing, but it is **out of scope for this phase** (CONTEXT.md scopes phase 43 as "two independent, isolated bug fixes... no shared code between the two fixes," neither of which touches this region). It has been re-filed as Info (IN-03) below instead of a blocker, and flagged separately for a follow-up fix outside this phase.

## Critical Issues

None. (See correction note above — the original CR-01 was a misattributed pre-existing issue, not a defect in this phase's changes.)

## Warnings

### WR-01: Mobile Quest Log card omits adventurer count shown on desktop, creating an unflagged parity gap

**File:** `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml:41-61`
**Issue:** The desktop `Index.cshtml` shows an "Adventurers: N" line (`selectedPlayers.Count`) for non-Campaign boards (`Index.cshtml:52-56`). The mobile view has no equivalent — it renders only title, CR badge, date, DM, and recap badge. Given this phase is explicitly named "mobile-parity-fixes," a silent content omission (rather than a documented, intentional simplification) is a regression risk: a reviewer scanning only the mobile view has no way to tell whether this was a deliberate simplification or a missed field.
**Fix:** Either add the adventurer count to the mobile card (consistent with desktop), or leave an explicit comment noting the omission is intentional for mobile information density, e.g. `@* Adventurer count omitted on mobile card for space; available on Details page *@`.

### WR-02: Duplicate/conflicting color+shadow declarations for the same element between `.quest-log-item-title` and `.quest-log-item h6`

**File:** `QuestBoard.Service/wwwroot/css/quest-log.mobile.css:17-30`
**Issue:** The `<h6 class="quest-log-item-title mb-0 me-2">` element in `Index.Mobile.cshtml:42` is matched by both `.quest-log-item-title` (lines 17-24) and `.quest-log-item h6` (lines 27-30). The latter re-declares `color` and a shorter `text-shadow` with `!important`, silently overriding part of the former rule's shadow layering (three shadow layers reduced to two) for no visible reason — the color value happens to be identical so there's no visual bug today, but the two rules are fighting over the same properties on the same element, which will produce a confusing regression the next time either rule is edited in isolation.
**Fix:** Remove the redundant `color`/`text-shadow` from `.quest-log-item h6` (scope it to non-title headings only, if any exist) or consolidate both rules into a single selector.

## Info

### IN-01: `site.css` comment describes a workaround for a rule not reachable in production, adding maintenance noise

**File:** `QuestBoard.Service/wwwroot/css/site.css:380-393`
**Issue:** The comment states the `body::before` rule in `site.css` "is not reachable by real iOS Safari sessions today (they route to mobile.css)" and is kept only for parity. This is accurate per the routing in `MobileViewLocationExpander`/`MobileDetectionMiddleware`, but it means `site.css` now carries an admittedly-dead rule purely for symmetry with `mobile.css`. This is a reasonable call, but worth flagging as a candidate for removal if `site.css` and `mobile.css` ever diverge on this rule, since nothing enforces the two staying in sync.
**Fix:** No action required; consider a code comment cross-reference or a shared CSS custom property if this needs to stay in sync long-term.

### IN-02: Whole-card `onclick="window.location.href=...` navigation lacks keyboard/screen-reader affordance

**File:** `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml:39-40`
**Issue:** The `<div class="quest-log-item" onclick="...">` is not focusable and has no `role="link"`/`tabindex`/keyboard handler, so keyboard and screen-reader users cannot activate navigation via this element. This matches the existing project-wide pattern (also present in desktop `Index.cshtml`, `Quest/Index.Mobile.cshtml`, etc.), so it is not a regression introduced by this phase, but it's worth surfacing since this phase is specifically about mobile UX.
**Fix:** Out of scope for this phase given the pattern is pre-existing and consistent; consider a follow-up phase to add `role="link" tabindex="0"` plus a `keydown` handler across all whole-card-clickable views.

### IN-03: Pre-existing unclosed `@media (max-width: 480px)` block in `site.css` leaks `.signup-seal-image`/`.seal-image` size overrides to global scope (not introduced by this phase)

**File:** `QuestBoard.Service/wwwroot/css/site.css:969-1007`
**Issue:** The `@media (max-width: 480px) { ... }` block opens at line 969 and closes prematurely after only `.quest-board-container` and `.fantasy-quest-card`. The rules that follow — `.quest-description`, `.seal-image`, `.signup-seal-image`, `.difficulty-icon`, `.difficulty-dice` — sit outside any media query and apply unconditionally to every viewport width (desktop included, since `site.css` loads globally via `_Layout.cshtml`), silently shrinking both wax seals from the base 80px to 65px everywhere instead of only on narrow screens. Confirmed via `git log`/`git show` that this bug and every affected rule pre-date phase 43 by many commits — this phase never touched this file region.
**Fix (out of scope for this phase, tracked separately):** Close the 480px media query properly and re-nest the trailing rules inside it.

---

_Reviewed: 2026-07-04_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
