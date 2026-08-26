# Phase 78: Link Preview Foundation and Quest Cards - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-26
**Phase:** 78-link-preview-foundation-and-quest-cards
**Areas discussed:** Expiry & revocation, Absolute URL source, Signed-link shape, Card content & minting

---

## Expiry & revocation

### Does the share signature carry an expiry?

| Option | Description | Selected |
|--------|-------------|----------|
| No expiry | Works until the key ring is destroyed or the quest is gone; metadata is thin and serve-time scoping is the real control | ✓ |
| Fixed TTL via `ITimeLimitedDataProtector` | Bounds a leaked link; built in, no storage. Cost: a working card silently stops later | |
| Tied to the quest's lifecycle | Card dies when the quest stops mattering. Cost: serve path must evaluate quest state | |

**User's choice:** No expiry
**Notes:** Grounding offered before the question — Data Protection retains old keys for decryption indefinitely, so "expires when the key rotates" is not a real bound.

### Can a minted link be revoked after the fact?

| Option | Description | Selected |
|--------|-------------|----------|
| No per-link revocation | Deleting the quest is the only retraction; honest about Discord/Slack caching unfurls server-side | ✓ |
| Board-wide kill switch | Version integer in the signing purpose invalidates every link for a board. Cost: blunt | |
| Per-link revocation list | Precise. Cost: new table, management UI, write on every mint — for a retraction that cannot retract a cached card | |

**User's choice:** No per-link revocation

### The Data Protection key ring is ephemeral — how should that be handled?

| Option | Description | Selected |
|--------|-------------|----------|
| Persist to the database | `PersistKeysToDbContext<QuestBoardContext>`; DB already has a durable volume, no compose change, one migration | ✓ |
| Persist to a mounted volume | `PersistKeysToFileSystem` + a named volume. Cost: server-side compose change, second stateful thing to back up | |
| Out of scope — handle separately | Accepts that signed links break on every deploy until fixed | |

**User's choice:** Persist to the database
**Notes:** Raised mid-area after scouting found no `AddDataProtection()` call anywhere, no `PersistKeysTo*` config, and no volume on the `questboard` service. This contradicts the ROADMAP's "keys already survive container restarts" scope note — they survive a restart, not a container recreate. Side benefit noted: users stop being logged out on every deploy.

### What does the signed token carry?

| Option | Description | Selected |
|--------|-------------|----------|
| Identifiers only, live lookup at serve time | Type + id + group id; edits reach the card, deletion kills it; the shape Phase 79's `IsRevealed` gate needs | ✓ |
| Self-contained — embed title and snippet | No DB read at serve time. Cost: with no expiry the text is frozen forever and deletion does not stop the card | |

**User's choice:** Identifiers only, live lookup at serve time

---

## Absolute URL source

### Where does the absolute base URL come from? (first pass)

| Option | Description | Selected |
|--------|-------------|----------|
| Forwarded headers + optional canonical override | `XForwardedProto` + `XForwardedHost`, with a canonical config winning for card/share URLs | ✓ |
| Forwarded headers only | Literal LINKPREV-01. Cost: depends entirely on `KnownProxies` being set, and fails silently | |
| Reuse `EmailSettings:AppUrl` | No new key. Cost (as framed at the time): defaults to localhost, absent from the documented env | |

**User's choice:** Forwarded headers + optional canonical override

### How is `X-Forwarded-Host` trust bounded?

| Option | Description | Selected |
|--------|-------------|----------|
| Rely on `KnownProxies`, leave `AllowedHosts` as-is | Keeps the Phase 32 config-driven trust decision; a direct-to-Kestrel caller cannot inject a host | ✓ |
| Also tighten `AllowedHosts` | Defence in depth. Cost: a wrong value is a total 400 outage | |
| Skip `XForwardedHost` entirely | Removes the spoofing surface. Cost: needs Traefik verified to preserve Host, else no card renders | |

**User's choice:** Rely on `KnownProxies`, leave `AllowedHosts` as-is

### Do the canonical key and `EmailSettings:AppUrl` unify? (asked twice)

**First pass — user correction rather than a selection.**

**User's response:** *"The deployment uses an env file on the server. So some appsettings are overridden. Please keep that in mind. And ask me the latest questions again if needed?"*

**Notes:** This corrected a factual error in how the question was framed. The claim that production email links might point at `https://localhost:8001` was wrong — `docs/server-setup.md` is an incomplete snapshot of the live env file, not evidence the variable is unset. `AppUrl` is a real, correct production value. The question was re-asked with corrected options.

**Second pass:**

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse `EmailSettings:AppUrl` | Already the app's single answer to "what is my public URL", already set, already proven by working email links | ✓ |
| New dedicated key, e.g. `PublicBaseUrl` | Clean semantics, no email coupling. Cost: a second env var that can drift from the first | |
| Rename `AppUrl` to a shared key | Fixes the naming properly. Cost: touches every email template and requires a coordinated env rename at deploy | |

**User's choice:** Reuse `EmailSettings:AppUrl`
**Notes:** Naming smell accepted and deferred as a future rename rather than fixed here.

---

## Signed-link shape

### What shape is the signed link?

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated preview route | Narrow anonymous-allowed endpoint; `Details` never touched; Phase 79 adds siblings. Cost: one extra hop | ✓ |
| Query param on the Details URL | Copied link is the real quest URL. Cost: `Details` must branch on signature and auth state | |

**User's choice:** Dedicated preview route
**Notes:** Grounding offered — today anonymous access to `/Quest/Details/47` is blocked by the fail-closed data filter producing a 404, not by auth. Setting group context from a signature removes that protection.

### What replaces the fail-closed filter as the page's auth gate?

| Option | Description | Selected |
|--------|-------------|----------|
| Add `[Authorize]` to `Details` GET | Makes the login requirement explicit and independent of group context; delivers success criterion 6 | ✓ |
| Keep relying on the group filter | No change to existing auth surface. Cost: protection stays implicit and one refactor from failing silently | |
| Add `[Authorize]` and redirect from the preview route | Most explicit. Cost: preview response is no longer identical for every caller | |

**User's choice:** Add `[Authorize]` to `Details` GET

### Where does the Open Graph markup live?

| Option | Description | Selected |
|--------|-------------|----------|
| Standalone minimal preview view | No app layout, so the UA-driven layout switch cannot produce card-less markup; `_Layout.cshtml` needs no head section | ✓ |
| Shared partial in a new head section on both layouts | The ROADMAP's stated approach. Cost: must go in both layouts or iMessage may get nothing; new plumbing in `_Layout.cshtml` | |

**User's choice:** Standalone minimal preview view
**Notes:** Deliberately overrides a ROADMAP scope note. Its underlying intent — one shared markup surface Phase 79 extends rather than copies — is preserved; only the host changes.

### How does a human get onward to the quest?

| Option | Description | Selected |
|--------|-------------|----------|
| 200 + meta tags + meta-refresh + visible link | Identical response for every caller, no UA branching; crawlers read tags, browsers move on | ✓ |
| 200 + meta tags + visible link only | Most predictable. Cost: an extra deliberate click for every member | |
| 302 straight to the quest page | Simplest for humans. Cost: no body, so a non-following crawler gets no tags at all | |

**User's choice:** 200 with meta tags plus meta-refresh and a visible link

### How does the preview route establish group scope?

| Option | Description | Selected |
|--------|-------------|----------|
| In-memory override via `SetGroupId`, never Session | Reuses the existing Hangfire seam; scoped override dies with the request; filter does the work unchanged | ✓ |
| A dedicated read path taking `groupId` explicitly | No ambient state. Cost: a second scoping mechanism alongside the query filter | |

**User's choice:** In-memory override via `SetGroupId`, never Session
**Notes:** Writing the group id into Session was called out as a genuine privilege escalation — it would give an anonymous visitor a live group context for the whole session.

---

## Card content & minting

### What is the branded fallback card image?

| Option | Description | Selected |
|--------|-------------|----------|
| New 1200×630 asset composed from existing board art | Gets the large-card treatment; `summary_large_image`. Cost: an actual asset must be produced | ✓ |
| Reuse an existing poster as-is | Zero new assets. Cost: portrait ratio demotes to a thumbnail, 0.9–1.7 MB, spaces in filenames | |
| No image at all | Simplest. Cost: fails LINKPREV-08 outright | |

**User's choice:** New 1200×630 asset composed from existing board art
**Notes:** Measured during scouting — `Poster1/2.png` are 1000×1400, `Poster3` 601×1400, `Poster5` 600×675.

### How is the card description derived, and what happens when empty?

| Option | Description | Selected |
|--------|-------------|----------|
| `ExtractPlainText`, ~200 chars on a word boundary, generic fallback | Reuses the project's single plain-text mechanism; truncates rendered text, never the Markdown source | ✓ |
| Same, but omit `og:description` when empty | Cleaner, no filler. Cost: a conspicuous blank on the card | |
| Structured detail instead of prose | Always populated. Cost: LINKPREV-06 explicitly specifies plain text derived from the Markdown | |

**User's choice:** `ExtractPlainText`, ~200 chars on a word boundary, generic fallback

### Who gets the "Copy shareable link" control?

| Option | Description | Selected |
|--------|-------------|----------|
| Any board member | A quest is board-level information (cf. Phase 74 D-05); a member could screenshot anyway | ✓ |
| DMs only | A signed link is durable and machine-readable in a way a screenshot is not. Cost: a player must ask a DM | |

**User's choice:** Any board member

### Where does the external-cache-permanence note surface?

| Option | Description | Selected |
|--------|-------------|----------|
| Short line in the copy UI plus a docs note | Establishes the pattern Phase 79 inherits, where it matters much more | ✓ |
| Docs only | The ROADMAP's literal wording. Cost: nobody reads docs at the moment they click copy | |
| UI only | States it where the action happens. Cost: no durable record for an operator | |

**User's choice:** Short line in the copy UI plus a docs note

### How does the copy control obtain the signed URL?

| Option | Description | Selected |
|--------|-------------|----------|
| Minted at page render, embedded in the view | No new endpoint, no antiforgery, no round trip; nothing stored so re-rendering is free | ✓ |
| Fetched from an endpoint on click | Keeps the URL out of page source; one place for future rate limiting. Cost: new endpoint, silent-failure mode | |

**User's choice:** Minted at page render, embedded in the view

---

## Claude's Discretion

- Exact preview route path and token format.
- Data Protection purpose string, and whether the group id sits in the purpose or the payload.
- Whether `IActiveGroupContext` is widened to expose `SetGroupId` or the concrete service is resolved directly.
- Button placement, iconography, wording, and copy-confirmation mechanism on desktop and mobile.
- Exact fallback description wording, truncation length, and ellipsis character.
- Meta-refresh delay and the wording of the visible fallback link.
- Test structure beyond the required cross-group replay test and the `curl -A Discordbot` check.
- Where the docs paragraph lives.

## Deferred Ideas

- Rename `EmailSettings:AppUrl` to a properly-scoped public base URL key shared by emails and link previews.
- A time-limited or revocable share link (`ITimeLimitedDataProtector` as a cheap retrofit).
- Per-quest generated card images — explicitly out of scope in the ROADMAP.
- Tightening `AllowedHosts` from `"*"` to the production hostname as defence in depth.
- Confirming `ReverseProxy__KnownProxies__0` is actually set on the App CT — outstanding since the Phase 32 UAT (2026-07-01).
