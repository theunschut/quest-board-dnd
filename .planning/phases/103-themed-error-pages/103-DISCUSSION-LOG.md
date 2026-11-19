# Phase 103: Themed Error Pages - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-18
**Phase:** 103-themed-error-pages
**Areas discussed:** Cross-board 404s, Which codes and which requests, 500 in prod vs dev,
Buttons/anonymous/phone

---

## Cross-board 404s

| Option | Description | Selected |
|--------|-------------|----------|
| No change; page explains | Leave the switch middleware and resolver; the 404 page explains generically | ✓ |
| Widen the route list too | Add more routes to the closed registry | |
| Separate phase | Keep any switch work out of 103 | |

| Option (404 wording) | Description | Selected |
|--------|-------------|----------|
| Same text for everyone | One fixed sentence on every 404 | ✓ |
| Only for multi-board members | The other-board sentence only when the viewer has 2+ boards | |
| No board mention | Plain "Page not found" | |

| Option (board picker link) | Description | Selected |
|--------|-------------|----------|
| Yes, when 2+ boards | Secondary "Switch board" button | ✓ |
| No, three buttons only | Exactly Back, Home, Log out | |

| Option (phase 99 Draft debt) | Description | Selected |
|--------|-------------|----------|
| Stays known debt | Resolver stays role-blind | |
| Fix it here | Resolver skips items the viewer can't see on the target board | ✓ |

| Option (leak rule) | Description | Selected |
|--------|-------------|----------|
| Only if viewer could open it | Switch only when the viewer would pass Shop/Details on the target board | ✓ |
| Never switch for hidden items | Draft/Denied/scheduled never switch, even for DMs | |

| Option (leak breadth) | Description | Selected |
|--------|-------------|----------|
| Audit all kinds | Apply the same rule to every registry kind with a visibility rule | ✓ |
| Shop items only | Fix only the recorded debt | |

**User's choice:** Recommended options throughout, except the Draft debt: "Fix it here".

---

## Which codes, which requests

| Option | Description | Selected |
|--------|-------------|----------|
| Every 4xx/5xx with no body | One status-code page; per-code copy for 404/400/500 plus fallback | ✓ |
| 404 and 500 only | Other codes stay bare | |

| Option (403) | Description | Selected |
|--------|-------------|----------|
| Fold into the new page | AccessDenied becomes the 403 variant; URL unchanged | ✓ |
| Leave AccessDenied alone | Keeps its single button | |

| Option (AJAX) | Description | Selected |
|--------|-------------|----------|
| Bare status, unchanged | HTML page only for document navigations | ✓ |
| You decide | Research picks the rule | |

| Option (429) | Description | Selected |
|--------|-------------|----------|
| Yes, themed for navigations | Same sentence, themed | ✓ |
| Leave as is | Plain text | |

---

## 500 in prod vs dev

| Option | Description | Selected |
|--------|-------------|----------|
| Keep developer page | Dev keeps the stack trace; Production and Testing are themed | ✓ |
| Themed page everywhere | Dev also themed | |

| Option (reference) | Description | Selected |
|--------|-------------|----------|
| Show trace id | "Reference: <TraceIdentifier>" | ✓ |
| No reference | Generic text only | |

| Option (logging) | Description | Selected |
|--------|-------------|----------|
| Built-in logging is enough | No new sink or alert | ✓ |
| Add an admin alert | Email/notify on 500 | |

---

## Buttons, anonymous, phone

| Option (go back) | Description | Selected |
|--------|-------------|----------|
| history.back() via JS file | Same-site Referer fallback, otherwise hidden | ✓ |
| Referer link only | Server-rendered link | |

| Option (log out) | Description | Selected |
|--------|-------------|----------|
| Same POST form → login | Reuse Account/Logout POST with an anti-forgery token | |
| You decide | Any mechanism with no logout over GET | ✓ |

| Option (anonymous) | Description | Selected |
|--------|-------------|----------|
| "Log in" button | Back, Home, Log in (local returnUrl only) | ✓ |
| Just Back and Home | Drop the third button | |

| Option (phone/gate) | Description | Selected |
|--------|-------------|----------|
| Twin + Gate 1 mockup | Error.Mobile.cshtml twin and a Gate 1 mockup | ✓ |
| One view, no gate | @media in @layer pages, screenshots at verification | |
| Twin, no gate | Twin without mockup gate | |

---

## Claude's Discretion

- Log out mechanism (the operator said "you decide"); no logout over GET.
- Per-code copy; the layout for /platform errors; a fallback when the layout throws; excluded
  paths; rendering with no active board.

## Deferred Ideas

- Widening the automatic board switch (more routes, or non-member boards).
- Alerting on 500s.
