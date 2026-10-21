# Phase 93 - UI Review (re-audit, code-only)

**Audited:** 2026-10-21
**Baseline:** 93-UI-SPEC.md (surface "built / not built" labels ignored; current code scored)
**Screenshots:** not captured (code-only audit, no dev server). Mobile views are chosen by user agent, so real-device checks are listed at the end as needs_human_review.

Paths below are relative to `QuestBoard.Service/` unless stated.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 4/4 | Verbatim contract copy on Edit, badge, Move suffix; only the spec-pinned imperfections remain |
| 2. Visuals | 3/4 | Every surface built and twinned; inert DM rows keep an icon-less, non-tappable look; desktop switches render as square checkboxes (accepted A2) |
| 3. Color | 3/4 | No new colour values; mobile refusal lines have no mobile red rule, so legibility on the glass card is unproven |
| 4. Typography | 4/4 | Only `h5`/`h6`/`small`/`.form-text` used; no authored weights or sizes beyond the contract |
| 5. Spacing | 3/4 | Scale respected, 44px mobile rows present; mobile Edit page relies on class-scoped CSS under a chrome the spec flags as uncertain (A5) |
| 6. Experience Design | 4/4 | Gating consistent across menus, in-page controls, pickers and server; no confirmed state gaps |

**Overall: 21/24**

No BLOCKER found. Every finding below is a WARNING (polish or unverified-on-device).

---

## Top Fixes (all WARNING)

1. **Verify the refusal lines on the mobile glass card** - `Areas/Platform/Views/Group/Edit.Mobile.cshtml:23` renders `div.text-danger`, and neither `wwwroot/css/platform-group.mobile.css` nor `wwwroot/css/mobile.css` contains a `.text-danger` rule (grep: 0 matches). Only `modern-card.css:114` styles it (desktop). On mobile it falls back to Bootstrap `#dc3545` on dark translucent glass, which the spec itself lists as a backstop. If it reads poorly, add one scoped rule under `.platform-group-card-mobile` using a lighter red with the card's text-shadow.
2. **Confirm the Platform mobile Edit page really loads `platform-group.mobile.css` styling under the right chrome** (spec A5) - the `@section Styles` link is at `Edit.Mobile.cshtml:6-8` and rules are class-scoped (`platform-group.mobile.css:58-81`), so they hold either way; only a phone UA can confirm the 44px rows and parchment labels.
3. **Players mobile: DM-off row drops the chevron but the link branch still carries an inline `style` chevron** - `Views/Players/Index.Mobile.cshtml:31` (`style="color: rgba(244,228,188,0.5);"`). Pre-existing, not introduced by this phase, so this is cleanup only, not a regression.

---

## Detailed Findings

### Pillar 1: Copywriting (4/4)
- `Areas/Platform/Views/Group/Edit.cshtml:41-43,70` and `Edit.Mobile.cshtml:43-45,72` match the contract strings: `Pages`, hint, `Home page`, home hint, `Save Changes`, `Back to Groups`.
- Move suffix: `ViewModels/MoveViewModels/MovePageViewModel.cs:39` returns ` (quests aren't used there)` / ` (events aren't used there)`, rendered at `Views/Move/Index.cshtml:84` and `Index.Mobile.cshtml:72`.
- Badge: `Areas/Platform/Views/Group/Index.cshtml:55` and `Index.Mobile.cshtml:46` render `Home: @BoardPagePolicy.Label(item.HomePage)` with `fa-house me-1`.
- Known imperfect copy (`Needs the quest list` without full stop, "Delete them" for one event) is pinned by spec A7; not a defect.

### Pillar 2: Visuals (3/4)
- Pages section is in the contract position, between Board Type and the `<hr>` (`Edit.cshtml:40-73`, `Edit.Mobile.cshtml:41-76`); button row order (Back left, Save right) intact.
- Locked rows: disabled unchecked switch, no `name`, reason in `small.d-block` with `aria-describedby` (`Edit.cshtml:52-54`). Unlocked rows post paired hidden `false` and a gap-free `rowIndex` (`Edit.cshtml:45,58-62`).
- Inert DM rows: Players desktop plain text (`Players/Index.cshtml:39-41`); Players mobile `players-row no-link` with no chevron (`Players/Index.Mobile.cshtml:35-38`); DM profile history plain text (`DungeonMaster/Profile.cshtml:87-96`) with `.dm-quest-history-item--static` at `wwwroot/css/dm-profile.mobile.css:112-118` (no cursor, no active tint).
- Hidden character pickers: every select, "change character" block, inline script and `_CharacterSelectModal` is behind `charactersOn` (`Quest/Details.cshtml:150,168,281,299,382,400,464,514,553,974`; `Details.Mobile.cshtml:241-509`).
- Calendar Board Availability button gated on `Serves(Events)` on both twins (`Calendar/Index.cshtml:17`, `Index.Mobile.cshtml:43`).
- WARNING: desktop switches look like the app's square checkboxes (spec A2, accepted), so the "switch" metaphor depends on `role="switch"` for assistive tech only.

### Pillar 3: Color (3/4)
- No new colour value found in the phase's views; accent stays on `btn-warning` and the existing info toast. Home badge is `bg-secondary` (`Group/Index.cshtml:55`).
- Mobile parchment/opaque secondary text matches contract: `platform-group.mobile.css:59-61` (`#F4E4BC`) and `:76-81` (`#D9CDA8`).
- WARNING: mobile refusal red is unstyled (see fix 1). Desktop is covered by `modern-card.css:114`.

### Pillar 4: Typography (4/4)
- Desktop `h5` heading (`Edit.cshtml:40`), mobile `h6` (`Edit.Mobile.cshtml:42`), `small` hint and reason, `.form-text` home hint. No `fw-bold` or authored weights in the phase markup; mobile small text set to `0.875rem` (`platform-group.mobile.css:78`).

### Pillar 5: Spacing (3/4)
- Spacing uses `mt-4 mb-2`, `mb-2` rows, `mb-3` blocks, per the scale. Mobile row padding `0.5rem` with `min-height: 44px` (`platform-group.mobile.css:65-69`) and block labels (`:71-73`).
- WARNING: whether the 44px rows render as intended depends on the chrome that wraps the Platform mobile twin (spec A5); unverifiable in code.

### Pillar 6: Experience Design (4/4)
- Menus: `Serves`/`Allows` gating in both layouts (`Views/Shared/_Layout.cshtml:70,101-105,173-205,221`; `_Layout.Mobile.cshtml:69-179`). Quests item rendered only when `EffectiveHome != Quests` and Quests available (`_Layout.cshtml:166-172`, `_Layout.Mobile.cshtml:127-133`), ids `nav-quests` / `nav-quests-mobile`.
- Move: picker disables unavailable boards (`Move/Index.cshtml:78-85`, `Index.Mobile.cshtml:72`), and the Details move control is nulled when no linked board can take the kind (`Domain/Services/MoveControlService.cs:17,23,37`; consumed at `Quest/Details.cshtml:779-792`, `Events/Details.cshtml:98-110`). The view shows a disabled button plus a reason only for block reasons (closed, played, series, past), not for "no board takes it".
- Refusals render in `role="alert"` with icon (`Edit.cshtml:19-24`). No confirm dialogs added, none needed. Switch-off never deletes.
- Registry audit: not applicable (no `components.json`).

---

## Needs human review (phone user agent, real device)

1. Edit page at 390px: rows with a two-line lock reason, select, and refusal lines wrap without horizontal scroll; rows at least 44px tall; tapping the label flips the switch.
2. Mobile refusal lines legible against the glass card (fix 1).
3. Board list Home badge wraps under the type badge instead of overflowing.
4. Move picker: long board name plus suffix inside the native select.
5. Inert DM history row and inert Players DM row show no pointer or pressed tint.
6. Quest Details with Characters off: join and finalized-join flows still post without a character id on both twins.

---

## Files Audited
`Areas/Platform/Views/Group/{Edit,Edit.Mobile,Index,Index.Mobile}.cshtml`, `Views/Shared/{_Layout,_Layout.Mobile}.cshtml`, `Views/Quest/{Details,Details.Mobile}.cshtml`, `Views/Players/{Index,Index.Mobile}.cshtml`, `Views/Calendar/{Index,Index.Mobile}.cshtml`, `Views/Events/{Create,Edit,Details}.cshtml`, `Views/Move/{Index,Index.Mobile}.cshtml`, `Views/DungeonMaster/Profile.cshtml`, `wwwroot/css/{platform-group.mobile,dm-profile.mobile,modern-card}.css`, `ViewModels/MoveViewModels/MovePageViewModel.cs`, `Helpers/MoveCopy.cs`, `Controllers/Events/EventsController.cs`, `Controllers/QuestBoard/QuestController.cs`, `QuestBoard.Domain/Services/MoveControlService.cs`.
Not re-opened this pass: `DungeonMaster/Profile.Mobile.cshtml`, `Shared/_LinkedDayEntries.cshtml` (surface 5), controller-side gate filter (no markup).
