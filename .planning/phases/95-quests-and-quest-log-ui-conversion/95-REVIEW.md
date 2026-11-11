---
phase: 95-quests-and-quest-log-ui-conversion
reviewed: 2026-11-11T00:00:00Z
depth: standard
files_reviewed: 89
files_reviewed_list:
  - QuestBoard.Service/Helpers/QuestPosterCatalog.cs
  - QuestBoard.Service/Helpers/QuestPosterPicker.cs
  - QuestBoard.Service/wwwroot/js/avatar-fallback.js
  - QuestBoard.Service/wwwroot/js/markdown-editor.js
  - QuestBoard.Service/wwwroot/js/quest-board.js
  - QuestBoard.Service/wwwroot/js/quest-details.js
  - QuestBoard.Service/wwwroot/js/quest-form.js
  - QuestBoard.Service/wwwroot/js/quest-manage.js
  - QuestBoard.Service/wwwroot/js/site.js
  - QuestBoard.Service/Views/Quest/Index.cshtml
  - QuestBoard.Service/Views/Quest/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Create.cshtml
  - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml
  - QuestBoard.Service/Views/Quest/Edit.cshtml
  - QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml
  - QuestBoard.Service/Views/QuestLog/Index.cshtml
  - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.cshtml
  - QuestBoard.Service/Views/QuestLog/EditRecap.cshtml
  - QuestBoard.Service/Views/Move/Index.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
  - "(plus the remaining view, css, and test files in the 89-file list; the Characters, Contacts, DungeonMaster, Events, Series and Shop views change only by a markdown-content.css link)"
findings:
  critical: 0
  warning: 8
  info: 6
  total: 14
status: issues_found
---

# Phase 95: Code Review Report

**Reviewed:** 2026-11-11
**Depth:** standard (diff against 7194364c)
**Files Reviewed:** 89
**Status:** issues_found

## Summary

No blockers. The project-specific risks came out clean:

- **XSS:** every JS module builds nodes with `createElement`/`textContent`. The only `innerHTML` writes are the markdown preview (server-sanitised HTML) and one static string.
- **Antiforgery:** all POST/DELETE fetches send the token. Server forms use tag-helper forms.
- **Dates:** every wall-clock value on the touched Quest/QuestLog/Move views goes through `Html.WallClock`, and instants go through `Html.LocalTime`.
- **Clock and planning references:** no `DateTime.Now/UtcNow` was added. No planning IDs appear in source comments or string literals.
- **Behaviour:** signup, vote change, revoke, finalize, delete, remove-player, move, follow-up and recap are all still wired. The old inline Manage script's null-dereference on assistant rows is now guarded.
- **Poster geometry:** the catalog box sizes match the CSS insets at a 352px card (checked by hand). The hash and range-reduction math is correct and deterministic.

The real problems:

- The poster picker does not deliver its "text never runs past the artwork" guarantee for long titles.
- A few UX and robustness gaps in the new JS.
- Heading structure regressions on desktop Manage.
- A leftover ambient-clock read and direct `DateTime.ToString` in the shared calendar partials this phase edited.

## Warnings

### WR-01: Poster picker fallback can overflow the artwork, contradicting its own contract

**File:** `QuestBoard.Service/Helpers/QuestPosterPicker.cs:38-48` (doc claim at 11-16)
**Issue:** When no poster holds the content at tier 1, `Pick` returns `Tallest` at tier 2 without checking `Holds(..., 2, ...)`. By the estimator's own arithmetic this does not fit for a long title:

- Poster p1: available = (408 - 43) * 0.8636 = 315px.
- A 200-character title at tier 2 is 18 characters per line, so about 12-13 lines * 19.2 = 230-250px.
- Add TitleGap 8 plus FinalizedMeta 144 (or OpenMeta 94): 382-402px finalized, 332-352px open.
- That overflows the box by 17-87px. Titles are allowed up to 200 characters (`StringLength(200)`).

`.quest-poster-text` in `quests.css` has no overflow handling, so the text spills past the box and, on p1, past the bottom edge of the artwork. `Pick_TitleOfTheFieldMaximum_GoesToTheTallestPosterAtTheSmallerTier` asserts which poster is returned but never that the content fits.

Related weaknesses:

- `TitleEm` and `BodyEm` are averages over mixed-case measurements. Cinzel is a caps face, so a caps-heavy or wide-glyph title (for example "WWW...") is underestimated by about 40%.
- `Pick_ResultsAtTierOne_HoldTheirContentInTheBox` is tautological. It asserts the same `Holds` predicate that `Pick` filters on, so it cannot catch a wrong constant.
- The doc says the choice is "a pure function of the quest". It is a function of the id, title, DM name, finalized state and board type. Editing a title or finalizing a quest can swap the poster.

**Fix:**
```csharp
// after computing fits == empty
var tallest = QuestPosterCatalog.Tallest;
// at tier 2 on the tallest poster, if it still does not hold, allow a tier 3 (smaller title / tighter meta)
// or clamp the title: .quest-poster-title { display: -webkit-box; -webkit-line-clamp: N; overflow: hidden }
// and add a test asserting Holds(Tallest, TitleOf(200), dm, 2, finalized, false) is true
```
Also add a safety margin (for example 10%) to `TitleEm` for uppercase-dominant titles, or measure per-glyph. Soften the "never runs past the artwork" doc line until it is true.

### WR-02: Desktop Manage uses `h5`/`h6` for card titles (heading levels skipped)

**File:** `QuestBoard.Service/Views/Quest/Manage.cshtml:670, 714, 797`
**Issue:** The page jumps from `h1` to `<h5>DM Controls</h5>`, `<h5>Quest Summary</h5>` and `<h6>Quick Stats</h6>`.

- This breaks heading navigation for screen readers.
- It contradicts the type scale in `ui-guidelines.md`, where `h4`-`h6` are 16px/600 and card titles are `h2`.
- The converted Details page uses `h2`/`h3` for the same cards.

**Fix:** Use `<h2 class="mb-0">` for DM Controls and Quest Summary (as Details does) and `<h3>` for Quick Stats.

### WR-03: A required Markdown field that is empty can block submit with no visible message

**File:** `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml:19`, `QuestBoard.Service/wwwroot/js/markdown-editor.js:127-146`
**Issue:** The textarea carries `required`, and EasyMDE hides it (`display:none`).

- `markdown-editor.js` fixes only the stale-content case by calling `codemirror.save()` on click.
- If the editor is genuinely empty, the browser's constraint validation fails on an unfocusable control and cancels submit silently. This hits Quest Description on Create, Edit and Follow-Up.
- The `aria-hidden` asterisk plus the removed tooltip means sighted keyboard users get no explanation.

**Fix:** On the submit-control click handler, after `save()`, check `textarea.required && !textarea.value.trim()`. If empty, `preventDefault()`, focus `easyMDE.codemirror`, and write a message into the `[data-valmsg-for]` span. (Confirm in a browser; this is the standard EasyMDE-with-`required` failure.)

### WR-04: Quest form submit guard relabels and permanently disables the submit button

**File:** `QuestBoard.Service/wwwroot/js/quest-form.js:158-166`
**Issue:**

- Setting `submitButton.textContent` drops the icon and the `me-2` spacing.
- It decides "Updating" versus "Creating" by `textContent.includes('Update')`, which is brittle: the Follow-Up button "Save Follow-Up Quest" becomes "Creating Quest...".
- The button stays disabled after bfcache restore (Back after a redirect). `site.js` only re-enables `form[data-disable-on-submit]`.
- The listener runs before any other submit handler, so if a later handler (client validation, `MarkdownEditor` sync) cancels the submit, the button is already dead.

**Fix:** Use `data-disable-on-submit` on the quest forms (the existing site.js behaviour already handles `pageshow`). If a busy label is wanted, toggle a CSS class or swap only the text node.

### WR-05: Shared calendar partials still read the container clock and format wall-clock times directly

**File:** `QuestBoard.Service/Views/Shared/_Calendar.cshtml:33, 76, 84`; `QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml:26-27`
**Issue:**

- `day.Date.Date == DateTime.Today` marks "today" using the container clock. It is UTC, so the highlight is wrong for board-local evenings. This breaks the `IBoardClock` rule. It is unchanged by this phase, but this phase edits the file and `AmbientClockSeamTests` does not guard it.
- `questOnDay.ProposedDate.Date.ToString("HH:mm")` and `.ToString("dddd, MMMM d")` format a wall-clock value without `Html.WallClock`, against the rendering rule in `ui-guidelines.md`. The same partial correctly uses `Html.WallClock` in the lines this phase added.

**Fix:** Pass `ViewBag.BoardToday` (a `DateOnly`, already set by `QuestController`) into the partial and compare `DateOnly.FromDateTime(day.Date)`. Render times with `Html.WallClock(..., "date-time-compact")`, or add a time-only style to both format tables. Add `_Calendar.cshtml` to the ambient-clock guard.

### WR-06: Desktop quest board renders nothing visible when there are no quests

**File:** `QuestBoard.Service/Views/Quest/Index.cshtml:20-23`
**Issue:** With zero quests the page shows only `visually-hidden` headings, so a sighted user gets a blank board.

- `QuestBoardEmptyStateTests` pins this, so it looks deliberate.
- It contradicts the empty-state rule in the guidelines (heading, one sentence, optional verb-noun action).
- It differs from the phone view and from the Quest Log, both of which render `.empty-state`.
- DMs get no "Create quest" prompt.

**Fix:** Render the shared `.empty-state` on desktop too (the board background is already provided by the shell), and update the test.

### WR-07: Role select does not apply its state on load

**File:** `QuestBoard.Service/wwwroot/js/quest-details.js:21-52`
**Issue:** `initRoleSelect` only calls `apply()` on `change`. If the browser restores a form value (Back navigation, soft reload) with Spectator selected, the vote section stays visible and the instruction text stays the Player text, while the select says Spectator. The old inline code had the same gap.

**Fix:** Call `apply()` once at the end of `initRoleSelect`.

### WR-08: `deleteQuest` has no failure path for network errors

**File:** `QuestBoard.Service/wwwroot/js/quest-manage.js:15-30`
**Issue:** The fetch chain has no `.catch`. A network failure gives an unhandled rejection and no feedback after the user has confirmed a destructive action. `removePlayer`, `revokeSignup` and `changeVote` all handle it.

**Fix:** Append `.catch(function () { alert('An error occurred while deleting the quest.'); })`.

## Info

### IN-01: Culture-sensitive `DateTime.ToString` in hidden date inputs

**File:** `QuestBoard.Service/Views/Quest/Edit.cshtml:103`, `QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml:85`
**Issue:** `ToString("yyyy-MM-ddTHH:mm")` uses the current culture's time separator (`:` in a custom format is culture-sensitive). A culture whose separator is `.` would emit a value `datetime-local` and the model binder reject. It also breaks the "never `.ToString` on a DateTime in a view" rule.
**Fix:** Use `ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)`, or add a form-value helper next to `Html.WallClock`.

### IN-02: Remove button on the last proposed-date row does nothing

**File:** `QuestBoard.Service/wwwroot/js/quest-form.js:100-104`
**Issue:** `removeProposedDate` returns when only one row is left, but the "Remove" button stays enabled and gives no feedback.
**Fix:** Disable the last row's remove button (or hide it) when `container.children.length <= 1`.

### IN-03: Antiforgery `<meta>` is emitted inside `<body>`

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:976`, `Details.Mobile.cshtml:700`, `Manage.cshtml:825`, `Manage.Mobile.cshtml:509`, `_QuestFormScripts.cshtml:15`
**Issue:** `Scripts` sections render after `RenderBody`, so `<meta name="request-verification-token">` lands in the body, which is invalid HTML. Browsers tolerate it and `querySelector` finds it, but a validator would flag it.
**Fix:** Add a `@section Head` (or put the meta in the layout's head when a view sets it), or use a `data-` attribute on `<main>`.

### IN-04: Dead `player-list` class and unused catalog URLs

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:910,931,952`, `Details.Mobile.cshtml:589,604`; `QuestBoard.Service/Helpers/QuestPosterCatalog.cs:9-18`
**Issue:**

- `.player-list` lost its only (empty) rule in this phase but is still applied.
- `QuestPoster.PngUrl` and `WebpUrl` are used only by tests, because the artwork URLs are duplicated in `quests.css`. The box geometry is likewise duplicated, in CSS percentages and catalog pixels. A future edit to one side will drift silently.

**Fix:** Drop the `player-list` class. Either drive the poster `background-image` from the catalog (a custom property set on the element) or add a test that parses `quests.css` and compares the insets and URLs against the catalog.

### IN-05: Quest Log excerpt can split a surrogate pair

**File:** `QuestBoard.Service/Views/QuestLog/Index.cshtml:30-32`
**Issue:** `plainDescription.Substring(0, 200)` can cut between the halves of an emoji and emit an invalid UTF-16 sequence.
**Fix:** Truncate on a text-element boundary (`StringInfo`) or back up one char when `char.IsHighSurrogate(s[199])`.

### IN-06: Character-select helper copy leaves a stale value after form restore

**File:** `QuestBoard.Service/wwwroot/js/quest-details.js:55-62`
**Issue:** The join forms' hidden `characterId` fields are only synced on the select's `change` event. If the browser restores the select's value without firing `change`, a role button posts an empty character while the UI shows one selected.
**Fix:** Run the same copy once at init (and on `pageshow`).

## Fix outcomes

Applied on `milestone/v9-rolling-improvements`, one commit per finding.

| Finding | Outcome |
|---------|---------|
| WR-01 | fixed (partly), 3a7ab939. The fallback now picks the poster with the most room at the smaller title size. The class doc names its real inputs (id, title, DM name, finalized state, board type) and says the estimate can still be exceeded. The tautological test is replaced by one that checks a known long title against hand arithmetic. Skipped on purpose: the 10% `TitleEm` margin (it reshuffles every poster pick) and any CSS clamp or text-shadow change (the poster text treatment is an operator decision). A 200-character caps-heavy title can still run past the box on the roomiest poster. |
| WR-02 | fixed, 8c402797. DM Controls and Quest Summary are `h2.h5`, Quick Stats is `h3.h6`, and a one-rule override in `quest-manage.css` keeps them at the body step (16px/600, body face) instead of the 28px display face the card header gives an `h2`. `QuestManageDmControlsTests` and `BoardMoveControlTests` pin the new markup with the same intent, and a new test checks that no heading level is skipped. |
| WR-03 | fixed, 7da440df. The browser's `invalid` event on the hidden textarea now writes "Description is required." into the field's message element (`role="alert"`), sets `aria-invalid` and `aria-describedby` on the editor input, and focuses it. The message clears when the user types. The `required` attribute and the server rule are untouched. |
| WR-04 | fixed, f3913d2e. The busy wording comes from `data-busy-label` on each submit button ("Saving follow-up quest..." for the Follow-Up form). Only the text node changes, so the icon stays. The lock runs after a `setTimeout` and only if the submit was not prevented. `pageshow` with `persisted` restores the label and re-enables the button. |
| WR-05 | skipped: the calendar is phase 97's and the issue predates this phase. |
| WR-06 | skipped: the bare empty board on desktop is the approved design. |
| WR-07 | fixed, e49108bc. `apply()` runs at init and on `pageshow`. |
| WR-08 | fixed, 4d7570b3. `deleteQuest` has a `.catch` with an alert, in the style of `removePlayer`. |
| IN-01 | fixed, 3d5b5000. The two hidden date inputs format with `CultureInfo.InvariantCulture`. |
| IN-02 | skipped: disabling or hiding the last remove button means choosing a behaviour and touching server-rendered rows on three forms; the current no-op is harmless. |
| IN-03 | skipped: moving the antiforgery meta into the head needs a head section in both layouts and five views, which is more than a trivial change. |
| IN-04 | fixed in part, 802bd8eb (dead `player-list` class removed). Skipped: the catalog-versus-CSS drift test, because the poster CSS is out of scope for this pass. |
| IN-05 | fixed, c3d32afd. The excerpt backs up one character when the cut would split a surrogate pair. |
| IN-06 | fixed, ea2179ad. The join forms' character field is copied at init and on `pageshow`. |

---

_Reviewed: 2026-11-11_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
