# Phase 93: Per-Board Page Selection and Home Page - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-20
**Phase:** 93-per-board-page-selection-and-home-page
**Areas discussed:** Page list & board type, Switched-off behaviour, Home page, Platform settings screen

---

## Page list & board type

### How should the page switches relate to board type?

| Option | Description | Selected |
|--------|-------------|----------|
| Type is a hard limit (Recommended) | Switches can only remove pages the type already allows; Shop, Players, Shop Management and DM profile can never be on for a Campaign board | ✓ |
| Type is just the default | A new board starts with its type's pages, then any page can be switched on or off, including Shop on a Campaign board | |

**User's choice:** Type is a hard limit

### Which pages get a switch?

| Option | Description | Selected |
|--------|-------------|----------|
| Every board page (Recommended) | Shop, Players, DM profile, Characters, Contacts, Quest Log, Events, Calendar; management pages follow their page | ✓ |
| Only the four you named | Shop, Contacts, Players, Characters; the rest stay on wherever the type allows | |

**User's choice:** Every board page

### Can the quest list itself be switched off?

| Option | Description | Selected |
|--------|-------------|----------|
| Always on (Recommended) | The quest board is the core loop and can't be switched off | |
| Can be switched off | A board that only uses events could drop quests; every quest surface then needs a rule | |

**User's choice:** Free text: "I want to be able to turn it off, IF the researcher sees no problem doing so. Otherwise it's always on."
**Notes:** Recorded as a research gate (CONTEXT D-03).

### If research finds quests can be switched off, what should "off" mean?

| Option | Description | Selected |
|--------|-------------|----------|
| Quests off entirely (Recommended) | No quest list, no Create Quest, no quest entries on the board's calendar, quest pages refused | ✓ |
| Only the list page hidden | Menu link and list go; DMs can still create quests, and existing ones still show on the calendar | |

**User's choice:** Quests off entirely

---

## Switched-off behaviour

### What does someone get when they open a switched-off page by URL?

| Option | Description | Selected |
|--------|-------------|----------|
| Redirect home + message (Recommended) | Board's home page with a toast like "Shop isn't used on this board." | ✓ |
| Plain 404 | As if the page doesn't exist on this board | |

**User's choice:** Redirect home + message

### Does a switched-off page stay reachable for anyone on that board?

| Option | Description | Selected |
|--------|-------------|----------|
| Off for everyone (Recommended) | Players, DMs, board Admins and super admins all get the redirect | ✓ |
| Admins still get in | Board Admins and super admins can still open it, e.g. to stock a shop before launch | |

**User's choice:** Off for everyone

### What happens to places on other pages that point into a switched-off page?

| Option | Description | Selected |
|--------|-------------|----------|
| Hide links, keep data (Recommended) | Links and pickers disappear; data already shown (signup character name and portrait) stays as plain text | ✓ |
| Hide everything about it | Character names and portraits vanish from quest pages too | |
| Leave links alone | Only the menu and the page change; links elsewhere hit the redirect | |

**User's choice:** Hide links, keep data

### Switching off Events (or Quests) while the board has upcoming entries?

| Option | Description | Selected |
|--------|-------------|----------|
| Refuse while upcoming exist (Recommended) | Platform refuses and says how many; a recurring series has to be ended first; feed code untouched | ✓ |
| Board's own pages only | Feed, My Agenda and linked calendars keep the entries, but their links lead to the redirect | |
| Vanish everywhere | Entries drop out of the feed, My Agenda and linked calendars; changes the guarded feed code | |

**User's choice:** Refuse while upcoming exist

---

## Home page

### Which pages can be a board's home page?

| Option | Description | Selected |
|--------|-------------|----------|
| Any switched-on page (Recommended) | Any switched-on page every member can open; DM-only pages excluded | ✓ |
| Quest list or Calendar | Just the two originally asked about | |

**User's choice:** Any switched-on page

### Where should a board's home page apply?

| Option | Description | Selected |
|--------|-------------|----------|
| Everywhere "home" is (Recommended) | `/`, the brand link, after login or picking a board with no return URL, and the switched-off redirect; a return URL still wins | ✓ |
| Only `/` and the brand link | Login and picking a board still land on the quest list | |

**User's choice:** Everywhere "home" is

### Per board, or shared by a linked set?

| Option | Description | Selected |
|--------|-------------|----------|
| Per board (Recommended) | Each board has its own; boards in one linked set can differ | ✓ |
| Shared by the linked set | Set once on the linked set; boards outside a set need their own setting anyway | |

**User's choice:** Per board
**Notes:** Clarified the original wording "for this board group".

### Switching off the page that is the current home page?

| Option | Description | Selected |
|--------|-------------|----------|
| Refuse it (Recommended) | "Calendar is this board's home page; pick another home page first." | ✓ |
| Fall back automatically | Quietly move the home page back to the quest list or the first switched-on page | |

**User's choice:** Refuse it

---

## Platform settings screen

### Where do the switches and home page live?

| Option | Description | Selected |
|--------|-------------|----------|
| On the board's Edit page (Recommended) | A Pages section under name and board type, saved with one Save | ✓ |
| Separate Pages screen | A new Pages button on the board list opening its own screen | |
| One grid for all boards | Boards against pages on one screen, with a home page column | |

**User's choice:** On the board's Edit page

### How do pages the board type doesn't allow appear?

| Option | Description | Selected |
|--------|-------------|----------|
| Greyed out with reason (Recommended) | Disabled, off switch with "Not available on Campaign boards." | ✓ |
| Left out | Only the pages the board can have are listed | |

**User's choice:** Greyed out with reason

### Should the Platform board list show page settings?

| Option | Description | Selected |
|--------|-------------|----------|
| Home page only, when not default (Recommended) | "Home: Calendar" badge beside the board-type badge, only when home isn't the quest list | ✓ |
| Home page + pages off | Also a "3 pages off" note per row | |
| Nothing | Board list unchanged | |

**User's choice:** Home page only, when not default

### Are page settings part of creating a board?

| Option | Description | Selected |
|--------|-------------|----------|
| Defaults, adjust in Edit (Recommended) | Create unchanged; new board gets every page its type allows, quest list as home | ✓ |
| Set on Create too | The Create form also gets the switches and home dropdown | |

**User's choice:** Defaults, adjust in Edit

---

## Claude's Discretion

- Storage shape, page identifiers, and how the server check is wired, with one source of truth for
  menus, links, redirect, home page and the Platform form.
- Where the available-pages lookup lives (alongside `IBoardTypeResolver`), read fresh per request.
- Toast and refusal wording, switch order, dropdown labels.
- Share-link preview cards for characters and contacts on a board where that page is off.
- Redirect fallback safety, and how past entries on linked calendars render.

Two decisions follow from the answers rather than from a separate question: switching off never
deletes data (CONTEXT D-09), and a Phase 92 move onto a board where that kind of entry is switched
off is refused (in CONTEXT D-08).

## Deferred Ideas

None.
