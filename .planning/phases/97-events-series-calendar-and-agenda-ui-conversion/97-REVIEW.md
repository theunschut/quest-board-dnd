---
phase: 97-events-series-calendar-and-agenda-ui-conversion
reviewed: 2026-11-13T00:00:00Z
depth: standard
files_reviewed: 61
files_reviewed_list:
  - .claude/ui-guidelines.md
  - QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs
  - QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs
  - QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs
  - QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventDetailsAvailabilityRenderTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventDetailsLayoutTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventFormMarkupTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventsOverviewMobileStyleTests.cs
  - QuestBoard.IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs
  - QuestBoard.IntegrationTests/Controllers/SeriesDetailsMarkupTests.cs
  - QuestBoard.IntegrationTests/Helpers/DesktopPageRegions.cs
  - QuestBoard.IntegrationTests/Helpers/PhonePageRegions.cs
  - QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs
  - QuestBoard.IntegrationTests/Tests/AgendaConversionTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarMonthMarkupTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarPhoneAgendaTests.cs
  - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarStyleTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardEndToEndTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardMobileCalendarTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
  - QuestBoard.Service/Extensions/HtmlHelperExtensions.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/QuestOnDay.cs
  - QuestBoard.Service/Views/Agenda/Index.cshtml
  - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Calendar/Index.cshtml
  - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml
  - QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml
  - QuestBoard.Service/Views/Events/Create.cshtml
  - QuestBoard.Service/Views/Events/Details.cshtml
  - QuestBoard.Service/Views/Events/Edit.cshtml
  - QuestBoard.Service/Views/Events/Index.cshtml
  - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Series/Details.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml
  - QuestBoard.Service/wwwroot/css/agenda.css
  - QuestBoard.Service/wwwroot/css/agenda.mobile.css
  - QuestBoard.Service/wwwroot/css/calendar.css
  - QuestBoard.Service/wwwroot/css/calendar.mobile.css
  - QuestBoard.Service/wwwroot/css/components.css
  - QuestBoard.Service/wwwroot/css/event-forms.css
  - QuestBoard.Service/wwwroot/css/events-overview.css
  - QuestBoard.Service/wwwroot/css/events-overview.mobile.css
  - QuestBoard.Service/wwwroot/css/quests.mobile.css
  - QuestBoard.Service/wwwroot/js/agenda.js
  - QuestBoard.Service/wwwroot/js/calendar-month.js
  - QuestBoard.Service/wwwroot/js/event-details.js
  - QuestBoard.Service/wwwroot/js/event-series-form.js
  - QuestBoard.Service/wwwroot/js/row-link.js
  - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  - QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs
findings:
  critical: 0
  warning: 2
  info: 7
  total: 9
status: issues_found
fixes:
  WR-01: fixed (29dddb00)
  WR-02: fixed (560e7d55, d6f6e8ef)
  info: left open
---

# Phase 97: Code Review Report

**Reviewed:** 2026-11-13
**Depth:** standard
**Files Reviewed:** 61
**Status:** issues_found

## Summary

Only the phase 97 commits in `14fcdefb..HEAD` were reviewed. The phase 96 commits in the same range are out of scope. `UiFoundationLayerGuardTests.cs` is the one listed file that phase 96 also touched; only its phase 97 hunks were judged.

The server side and the scripts are sound. These were checked and hold:

- **Board clock.** `CalendarController` sets `ViewBag.BoardToday` from `IBoardClock` on the only path that renders. Every caller of `_Calendar` sets it: the Calendar page, and Quest Details through `QuestController.Details`. No ambient `DateTime.Now`/`Today` read remains in the converted views. `AmbientClockSeamTests` only grew its guarded lists.
- **Wall clock.** Every date the area renders goes through `Html.WallClock`. The four new server-only styles are unknown to `site.js`'s `TIMESTAMP_FORMATS`, so the client leaves their text alone. No value is converted between zones.
- **No inline code.** The `onclick`/`onsubmit` handlers and inline scripts are gone. The tokens move into the meta element, and URLs and messages move into `data-*` attributes. The confirm sentences are now attribute-encoded instead of being spliced into JS string literals, which also removes a latent quoting bug in the old `confirm('@...')`.
- **No markup built from strings.** `calendar-month.js` builds its rows with `textContent`/`cloneNode` and writes only `--pop-x`/`--pop-y` through CSSOM. CSP does not block that. `agenda.js` and `event-series-form.js` also write text and values only.
- **Stylesheets moved off the layout.** The three area sheets were dropped from `_Layout.cshtml`. No desktop view outside the area relied on a class from them: only scoped `.legend-card .card-body` and `.vote-buttons … .btn-outline-danger` rules lived there. The availability vocabulary moved to `components.css`, so Events/Details and the Agenda keep their styling.
- **Layer cascade.** No unlayered sheet loaded on Quest Details (`legacy-pages.css`, `shop.css`, …) targets a calendar class. Moving `calendar.css` into `@layer pages` therefore loses no rule to an unlayered one.
- **Planning references.** The phase's source files carry no GSD planning IDs.

There are no blockers. The two warnings:

- The 44px "Also that day" link on phone Quest Details uses the same in-sentence `inline-flex` box that phase 96 WR-01 fixed elsewhere (WR-01).
- `calendar.css` reintroduces colour literals as `black`, which slips past the guard tests' literal scan (WR-02).

The rest is Info: accessibility and culture inconsistencies, guard blind spots and leftovers.

## Warnings

### WR-01: The "Also that day" link becomes a 44px inline-flex box inside a sentence

**Fix status:** Fixed in 29dddb00. The link stays an inline box and takes the 44px floor through padding of `calc((var(--qb-hit-min) - 1em) / 2)` with an equal negative sideways margin, the same pattern as `account-link-inline`. The style test now pins the padding and rejects `display` and `min-height` on the rule.

**File:** `QuestBoard.Service/wwwroot/css/quests.mobile.css:114-121` (markup: `QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml:14-21, 28-35`, rendered by `_Calendar.Mobile.cshtml:32` on phone Quest Details)

**Issue:** The new rule gives `.also-that-day .row-nav-link` `display: inline-flex; align-items: center; min-height: var(--qb-hit-min)`. That anchor sits in the middle of a sentence: "Also that day: **Board** — *Title*, 19:00".

An inline-flex box is atomic. It forces a 44px line box onto the line it sits on, so the sentence gets a tall gap around the link. A long title then wraps inside the box instead of flowing with the sentence, and ", 19:00" is pushed after the box.

This is the defect phase 96 WR-01 found in `.account-link` and fixed in bfbd1eb5 with the inline `account-link-inline` treatment. The comment on the rule ("a link inside a sentence still takes the 44px floor") describes the intent, but the method breaks the sentence.

**Fix:** Keep the link inline and grow its hit area with padding and a matching negative margin, as `account.css:149-152` already does:

```css
.also-that-day .row-nav-link {
  padding-block: calc((var(--qb-hit-min) - 1lh) / 2);
  margin-block: calc(-1 * (var(--qb-hit-min) - 1lh) / 2);
  text-decoration: underline;
  color: inherit;
}
```

Alternatively, reuse the `account-link-inline` pattern under a shared class. Re-run the phone 44px sweep on Quest Details afterwards with a long linked title.

### WR-02: `calendar.css` reintroduces colour literals as `black`, past the literal guards

**Fix status:** Fixed in 560e7d55 and refined in the follow-up commit. The twelve `black` mixes no longer name a colour literal and no token was added. Every shadow keeps its original offsets, blur and strength and mixes the `--qb-ink` token instead of black (`0 4px 12px` at 30% on the hover lifts, `0 2px 8px` at 30% on the vote group, `0 2px 4px` at 10% on the vote box, `2px 2px 4px` at 90% on the text shadows), and the empty-cell tint and vote group edge mix `--qb-ink` the same way, so Quest Details renders as it was approved. Both colour guards now also flag any CSS named colour used as a value, with a self-test, and the widened scan finds nothing else in any file on either list.

**File:** `QuestBoard.Service/wwwroot/css/calendar.css:970, 982, 1108, 1163, 1312, 1369, 1382, 1389, 1393, 1507, 1529, 1599` (guards: `QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs:203-209`, `QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs:168-174`)

**Issue:** When the sheet moved into `@layer pages`, each `rgba(0, 0, 0, X)` in the Quest Details half became `color-mix(in srgb, black X%, transparent)`. Two examples are the shadows on the chip hover and on `current-quest`, and the `text-shadow` on the day number and on `calendar-linked-note`.

`black` is a colour literal. The guidelines say a stylesheet other than `tokens.css` "never contains a colour literal". The sheet's own header (line 7) claims "Every colour is a design token or a mix of one."

Both guards that are meant to hold this line match only hex, `rgb()` and `hsl()`. Named colours pass, so `ConvertedSheets_HaveNoColourLiteral` and `LayeredFiles_HaveNoColourLiteralOutsideTokens` are green while the rule is broken. `calendar.css` is the only layered sheet that does this, which shows a mechanical rewrite to satisfy the regex rather than a move onto tokens.

**Fix:**

1. Replace the shadows with the existing elevation tokens (`--qb-shadow-rest`, `--qb-shadow-raised`, `--qb-shadow-overlay`). If none fits, add a shadow-colour token to `tokens.css` (for example `--qb-shadow-color: rgb(0 0 0)`) and mix from it:

   ```css
   box-shadow: var(--qb-shadow-raised);
   /* or */
   text-shadow: 2px 2px 4px color-mix(in srgb, var(--qb-shadow-color) 90%, transparent);
   ```

2. Extend both `ColourLiteralPatterns` lists with a named-colour pattern that ignores `white-space`, for example:

   ```csharp
   ("a named colour", new Regex(@"(?<![\w-])(?:black|white|red|green|blue|gray|grey|silver|maroon|navy|orange|yellow|purple)(?![\w-])", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
   ```

   Add a self-test line for it to `Scanners_FlagWhatTheyForbidAndAllowWhatTheyPermit`.

## Info

### IN-01: Linked month entries never say "linked board" or "Event" to assistive technology

**File:** `QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml:90, 127`; `QuestBoard.Service/wwwroot/js/calendar-month.js:237, 244-246`

**Issue:** The two kinds of entry get different accessible names. An own event row's name ends ", Event, 18:30". A linked event row's hidden text is only "{Board}. 18:30." Its marker and link glyph are `aria-hidden`, and its detail block (the only place with "Event, linked board") is `hidden` and is shown only inside the `aria-hidden` card. In the day panel, `buildRow` strips ", linked board" from the status word and adds the link glyph as an `aria-hidden` icon. A screen reader therefore never hears "linked board" for these entries, and never hears "Event" for a linked event.

The interface contract pins the hidden text verbatim, so this is not a regression. But the recorded rationale for exception 2 ("the visually hidden text … and the panel row all say 'linked board'") is not true of the build.

On the same theme, the contract accepts that an inert linked entry has no keyboard path to its card. Its title is ellipsis-truncated from 992px up and fully hidden below 768px, so a sighted keyboard-only user cannot read it unless the day overflows into the panel.

**Fix:** In `buildRow`, keep the stripped words as visually hidden text: append `hiddenText(', linked board')` when `linked` is true. When the contract is next revisited, consider ", Event" / ", linked board" in the linked rows' hidden text, and correct the exception 2 wording.

### IN-02: Stale XML docs on the wall-clock helper

**File:** `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs:92-102, 131-137`

**Issue:** `BuildWallClock` is still documented as rendering "invariant-culture text at first paint". `WallClock` is still documented as reusing "the exact same LocalTimeFormats style table". Both are now false for the four server-only styles, which format with `CultureInfo.CurrentCulture` from `ServerOnlyWallClockFormats`.

**Fix:** Update both summaries to name the two tables and the culture each uses.

### IN-03: Mixed cultures on one page, and a culture-dependent test

**File:** `QuestBoard.Service/ViewModels/CalendarViewModels/QuestOnDay.cs:15`; `HtmlHelperExtensions.cs:43-49, 112-116`; `QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs:120-131`

**Issue:** Nothing configures request localization, so these strings follow the host process culture:

- `QuestOnDay.TimeLabel` and the four server-only styles use `CurrentCulture`.
- `LinkedQuestOnDay`/`LinkedEventOnDay.TimeLabel` use `InvariantCulture`.
- The month's weekday header, "Today. ", "Proposed" and so on are hard-coded English.

On a non-English host, the month mixes languages: Dutch panel titles and detail dates under English weekday columns. Own and linked times can also use different separators in the same cell (for example "19.00" against "19:00" under a culture whose time separator is a dot). This keeps the old behaviour, but the phase made it a documented rule rather than an accident.

`CalendarBoardClockTests.PhoneAgenda_MarksTheBoardsTodaySection` matches the literal English `september\s+6`, so it fails on hosts whose culture spells the month differently.

**Fix:** Pin one culture for these server-only strings (invariant, or a configured board culture) and make `LinkedQuestOnDay`/`LinkedEventOnDay`/`EventOnDay.TimeLabel` agree with it. In the test, build the expected label with the same format and culture the view uses, as `LinkedBoardMobileCalendarTests.DayLabel` does.

### IN-04: Date-only values carry a midnight time in `datetime`

**File:** `QuestBoard.Service/Views/Events/Details.cshtml:50`; `Series/Details.cshtml:45, 73, 125, 133`; `Events/Index.cshtml:69`; `Events/Index.Mobile.cshtml:83`; `Agenda/Index.cshtml:110`; `Agenda/Index.Mobile.cshtml:108`; `Shared/_Calendar.cshtml:313, 368`

**Issue:** Each `DateOnly` is passed as `date.ToDateTime(TimeOnly.MinValue)`, and `BuildWallClock` always writes `datetime="yyyy-MM-ddTHH:mm"`. The machine-readable value of an all-day or date-only fact is therefore "midnight" (`2031-03-10T00:00`). If a client entry is ever added for `weekday-long(-year)`, it would treat these as timed values.

**Fix:** Add a `DateOnly` overload, or a date-only flag, that writes `datetime="yyyy-MM-dd"` for the date styles.

### IN-05: The raw-date guard misses interpolation format strings

**File:** `QuestBoard.Service/Views/Events/Details.cshtml:20-21`; `QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs:162-164`

**Issue:** `cancelConfirmMessage` and `restoreConfirmMessage` format the date with `{Model.Date:dddd, MMMM d, yyyy}`. That is the same host-culture formatting the guard forbids, but `DateFormatCall` only matches `.ToString("…")`. Attribute text cannot hold a `<time>`, so the formatting itself is defensible. The guard simply cannot see it.

**Fix:** Also match `\{[^}:]+:(?<format>[^}]*(?:yy|MM|dd|HH|hh|mm|ss|tt)[^}]*)\}` and exempt the confirm sentences explicitly, or move them to a view-model property, as `SeriesDetailsViewModel.TodayLabel` already does.

### IN-06: Interaction edge cases in `calendar-month.js`

**File:** `QuestBoard.Service/wwwroot/js/calendar-month.js:94, 325, 459-462; 139-142, 155-179, 281-291; 489-505`

**Issue:**

- **Sticky pointer type.** `lastPointerType` is only updated on `pointerdown`. After any touch on a hybrid device, a keyboard Enter on an entry whose card is not showing (for example after Esc suppressed it) is cancelled and opens the card instead of following the link.
- **Overlays do not follow scrolling.** The card and the day panel are `position: fixed` at a rectangle measured once, and no `scroll` listener moves or closes them. When the page does scroll (the known six-week overshoot at 992x720, or the runway alert), an open panel stays put while its cell moves away.
- **Focus trap in a non-modal dialog.** The panel is declared `aria-modal="false"`, yet Tab and Shift+Tab are trapped inside it, so a keyboard user can leave only with Esc or Close.

**Fix:** Reset `lastPointerType` to `'keyboard'` on `keydown`, and skip the touch path for a keyboard-initiated click (`event.detail === 0`). Close the card and the panel silently on `scroll` (capture, passive). Either drop the Tab loop, or document it as intentional next to the `aria-modal="false"` choice.

### IN-07: Leftovers

- **`Views/Calendar/Index.Mobile.cshtml:9`:** `var pages = await BoardPages.GetAsync();` is unused now that the two header buttons are gone, but the call still runs on every request. `@using Microsoft.AspNetCore.Html` and `@using QuestBoard.Domain.Interfaces` (line 1, 3) are unused, as is `@using QuestBoard.Domain.Interfaces` in `Views/Calendar/Index.cshtml:1`.
- **`Views/Calendar/Index.Mobile.cshtml:76`:** The phone legend says "Click quests or events for details". The phone has no detail card (a tap opens the page), and a phone user taps rather than clicks.
- **`Views/Events/Create.cshtml:100` and `Views/Events/Edit.cshtml:84`:** "Next occurrences" and the save-scope modal title are still `h5`, so the outline goes from h1 to h5. The Agenda and Series modals in the same phase moved to `h2`.
- **`Views/Series/Details.cshtml:194`:** Series loads `event-details.js`, a module whose header describes the Event Details page, just for the shared `form[data-confirm-message]` listener. Move that listener into a small shared module so a later Event-Details-only change cannot leak onto Series.
- **`Views/Shared/_Calendar.cshtml:303-306, 355-357`:** The time is read twice in each entry's accessible name: the visible `cal-entry__time` comes first, then the hidden suffix ", Proposed, 19:00 …". The same happens in `_LinkedDayEntries.cshtml:87-90, 125-127`. Mark the visible time `aria-hidden="true"`, since the suffix carries it.
- **`QuestBoard.IntegrationTests/Helpers/DesktopPageRegions.cs` and `PhonePageRegions.cs`:** Two identical `Between` implementations. Fold them into one helper with the two region selectors as parameters.

---

_Reviewed: 2026-11-13_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
