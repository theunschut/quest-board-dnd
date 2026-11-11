# Phase 95: Quests and Quest Log UI conversion - Research

> **Revision 2026-11-11:** the operator moved execution to the Linux machine that ran Phase 94. Every
> Windows-specific finding below (Edge via `channel: 'msedge'`, a fresh `playwright-core` 1.65.0 install,
> PowerShell listener lookup, `Trusted_Connection`, Python 3.14 with scipy) is superseded. The phase now
> reuses `~/qb-ui-review/phase-94` (Playwright 1.64.1 with its own headless Chromium, the Audit Board and
> its `qb-audit-*` accounts, the Direction A renders). The Python here is `python3` with Pillow 12.1 and
> numpy 2.3, and has no scipy. See 95-CONTEXT.md D-22 revision and 95-01-PLAN.md.

**Researched:** 2026-11-10
**Domain:** ASP.NET Core 10 MVC Razor views, plain CSS cascade layers, vanilla JS modules, EasyMDE 2.21.0, scripted browser capture (Windows 11)
**Confidence:** MEDIUM-HIGH (in-repo facts and measurements HIGH; browser-behaviour claims that need a probe in the real page are flagged ASSUMED)

## Summary

The phase converts 25 view files and 13 stylesheets plus three ranges of `legacy-pages.css` onto the Phase 94 foundation. The ROADMAP counts reproduce exactly from the working tree (26 inline `style=`, 11 inline `<script>`, 59 inline event handlers, 0 `<style>`; 126 hex, 276 `rgb()`, 212 `!important`), so the scope numbers are trustworthy. All Phase 94 capture tooling (`audit-shoot.mjs`, `manifest-gen.mjs`, `contrast.mjs`, `run-baseline.sh`, mockup scripts) is **not committed anywhere**; it must be rebuilt in a scratch folder from the 94-01/94-02 plans and the reference code in 94-RESEARCH.md. Node 22.16, Python 3.14 with Pillow 12.2 (WebP works), numpy and scipy are present; Playwright is not installed, but Microsoft Edge 156/157 is, so `playwright-core` with `channel: 'msedge'` needs no browser download. SQL Server answers on `localhost:1433` right now.

Five findings change how the phase must be planned and are not in the ROADMAP or CONTEXT:

1. **`Quest/_QuestFormScripts` and `Shared/_MarkdownEditor` are consumed by 26 and 26 view files across Characters, Contacts, DM profile, Events, ShopManagement and QuestLog, not just quests.** The editor module's globals (`initMarkdownEditor`, `window.markdownAntiforgeryToken`, `textarea.easyMDE`) are called from inline scripts in `Contacts/Details` (Phase 98). Moving them into a module without keeping those globals breaks Contacts. `markdown-editor.css` / `markdown-content.css` are likewise used by Characters, Contacts, DM Profile, Events, Series and Shop views, so "leave the global list" needs a per-consumer link or a different mechanism (see Open Question 1).
2. **`.move-control-reason` is used by `Events/Details` (Phase 97) and is defined in `move-entry.css` (desktop) and `mobile.css` (phone).** Moving `move-entry.css` out of the global list strips it from `Events/Details` on desktop. Promote it to `components.css`.
3. **Desktop poster measurement: only the five kept blanks plus at most one ruined poster (Ruined 1, borderline) hold the full content block at the 14px floor.** Ruined 2 to 8 have burnt bottoms that leave 41 to 144 card-px of usable height against a 176px need (finalized quest). D-04 as written therefore returns a pool of 5 to 6, not 12; the operator should see that at Gate 1.
4. **Dark ink on a lightened zone is bound by the *darkest* pixel, not the brightest.** The "brightest board pixel / B-max" language in D-08 applies to translucent glass over the board; the poster art is opaque, so B-max does not enter. Measured values are in the Poster section.
5. **The board's layout is driven by `site.js`** (`setCardHeights`, `layoutMasonry` read `data-image-width/height`, write inline sizes and positions at runtime; `addProposedDate()` emits `onclick=` markup). Moving poster selection to CSS classes breaks the height logic unless heights come from CSS `aspect-ratio`.

**Primary recommendation:** Plan 11 plans in the D-24 order (below): one opening operator-checkpoint plan that rebuilds the scratch tooling and captures BEFORE from the current branch, one scratch-only Gate 1 plan (poster mockup with measured pools, phone Manage mockup, merge evidence), then one source plan per slice, each leaving the suite green; remove the four stylesheets from the layouts' global lists only in the last source plan, after every consumer links its own copy.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Desktop quest board posters (G1-R1, drawn before it is built)**

The board is a fantasy notice board that players take quests from; the posters are the app's
identity and the reason the app exists. Keep them, keep them varied, and make them readable.

- **D-01: The quest description leaves the desktop poster.** Players read it on Quest Details,
  which keeps it unchanged. The poster carries the title, the DM, the session date (once
  finalized), the adventurer count line and the CR (CR stays hidden on Campaign boards, as today).
  This is an explicit operator decision about displayed text; no field, link, button or action is
  removed, so D-04's control rule still holds. The phone board is unaffected.
- **D-02: Poster3 (601x1400, the tall narrow one) is retired from the rotation.** The pool is
  Poster1, Poster2, Poster4, Poster5, Poster6 and the eight `Ruined Posters`, all from their
  `w Shadow` folders. The image files themselves stay in the repository.
- **D-03: Each poster gets its own measured text area.** Several blanks have holes or torn edges,
  and Poster2's height comes from a tail, not from usable space. For every poster the researcher
  or mockup step measures the usable content rectangle (the insets that keep text clear of holes,
  tears and the tail) and records it as that poster's padding. Fit is judged by this measured area,
  never by the image's height.
- **D-04: A poster joins the pool only if its measured area holds the full poster content** (title,
  DM, date, adventurer count, CR) at the 14px text floor without touching a hole or edge. The
  research and mockup step decides which of the eight ruined posters qualify; any that do not
  qualify stay out of the rotation, and the mockup shows the operator which ones made it.
- **D-05: Poster choice is random but stable per quest.** A quest keeps the same poster and seal on
  every visit (derived from the quest's id, as today), chosen from the posters that fit it. A title
  too long for a squat poster's area goes to a taller one so the title wraps and is never truncated
  (A-quests-022). The variety across quests is what makes the board feel alive. Today's
  character-count selection (`<= 130` Poster4, `<= 250` Poster5, `>= 500` Poster3) is replaced.
- **D-06: The wax seals stay as the status marker; no status word on the desktop poster.** This
  **revises the G1 answer to Q-23** for the desktop board. Behaviour as today: an open quest has
  no seal, a finalized quest (or any quest on a Campaign board) shows the seal, and a second,
  hue-shifted seal marks "you are signed up". The seal design is chosen per quest from the four
  `Wax Seals` images. Seals sit at a fixed, per-poster position clear of the text area (they cover
  the DM line and titles today). The status reaches assistive technology through the seal `alt`
  text and the poster link's accessible name. The phone board keeps its text badges (Q-23 stands
  there).
- **D-07: The poster mockup draws both information orders**, date first and adventurer count first
  after the title, and the operator picks at the first gate. The adventurer count line stays on the
  poster (Q-22).
- **D-08: The lightened text zone is shown as a switcher across two or three strengths**, every
  one measuring at least 4.5:1 for body text at the brightest board pixels (titles may rely on the
  3:1 large-text bar). The operator picks the strength at the first gate. No slip surface
  (`--qb-surface-slip` does not exist and must not be introduced).
- **D-09: An empty desktop board is the bare board**: no poster or card is drawn, and a
  screen-reader-only heading ("No quests available") keeps the page from being silent. This
  **revises the visible part of the G1 answer to Q-21**; the phone keeps its visible empty state.
- **D-10: Add WebP derivatives of the poster artwork and keep the PNGs as fallback.** Committed
  WebP copies next to the PNGs, loaded through CSS `image-set()` with the PNG as fallback; the
  originals are untouched. This **revises the G1 answer to Q-131**. The posters are 0.25 to 1.8 MB
  each, so a crowded board currently loads several MB. The same treatment applies to the four seal
  images if it pays off.
- **D-11: The poster AFTER is a scratch mockup shown to the operator before any board code is
  written**, drawn with a crowded board (so the operator can judge whether the taller Poster1, 2
  and 6 look empty without a description), long titles, open, finalized and signed-up quests, and
  a Campaign board.

**Shared Markdown editor**
- **D-12: Fix the keyboard trap by letting Tab leave the editor.** Tab and Shift+Tab move focus out
  of the editor like any other field; Tab no longer indents inside it (nested lists still work via
  the toolbar's list buttons and typed spaces). The editing surface gets an accessible name from its
  label. This also fixes the six Characters-area pages that render the partial (A-characters-006);
  Phase 98 verifies it there.
- **D-13: The "supports Markdown" hint becomes visible help text under the field**, linked with
  `aria-describedby`, replacing the hover-only tooltip icon (A-quests-040). The sentence is unchanged.

**Phone signup on Quest Details (Q-14, Q-15)**
- **D-14: The phone signup form gets plain selects, the same controls as desktop**: "Select
  Character (Optional)" (only when the board serves Characters, as on desktop) and "Select Your
  Role" (Player, Assistant DM, Spectator), each with its help text, placed above the date voting.
  The existing character-select modal stays where it is used today (the join forms).
- **D-15: Choosing Spectator hides the date voting on both layouts**, with the "As a spectator, you
  will be automatically approved" message, as desktop does today. The script targets a dedicated
  marker on the voting cards, so it no longer hides the DM Controls card (A-quests-011).

**Twin wording**
- **D-16: Twins use one wording, in full.** "Assistant DM", "Open for Signups", "Participants" and
  the Manage labels read the same on both layouts. The phone abbreviates only where a label
  measurably does not fit at 390px; each such exception is recorded in the phase verification.
  The Manage date format keeps the year on both.
- **D-17: Quest form exit buttons read "Cancel" on both layouts**, with the same link targets as
  today, matching the approved Cancel-left, commit-right row (Q-113).

**Relayout sign-off**
- **D-18: Hybrid sign-off.** The two relayouts with no AFTER yet, the desktop poster board (G1-R1)
  and phone Quest Manage (Q-114), are drawn as scratch mockups and approved **before** they are
  built. Quest Details (Q-111, Q-112) and Quest Create and Edit (Q-113) already have Direction A
  renders, so they are built and confirmed from real before/after screenshots.
- **D-19: Two operator gates.** Gate 1, early: the poster mockup (with the D-07 and D-08
  switchers and the D-04 pool), the phone Manage mockup and any merge proposals (D-21). Gate 2, at
  the end: one review sheet with every before/after pair, desktop and phone.
- **D-20: A rejected AFTER is iterated until the operator approves it.** The phase waits on it. Plan
  for the possibility of a redraw loop at either gate.

**Twin merges**
- **D-21: Re-check the remaining pairs for merging and bring any that qualify to Gate 1.** With the
  "add it to the other view" answers applied, Move/Index (OQ-MV-1 answered), Quest/Edit and
  Quest/CreateFollowUp may now be layout-only. Research checks each against the D-13 criteria from
  Phase 94 (identical or layout-only, no deliberate difference, phone differences CSS-only) and
  lists the tests each would change. Nothing merges without the operator's explicit yes. The two
  merges already approved (QuestLog/EditRecap, QuestLog/Details) go ahead as decided.
  — **Reversibility:** costly — a merge touches the view, both layouts' CSS loading and the
  mobile-view tests that prove the `.Mobile` view is selected under a real phone User-Agent.

**Working baseline on this machine**
- **D-22: Re-capture BEFORE screenshots here, from the current branch.** The Phase 94 scratch folder
  (`~/qb-ui-review/phase-94`) is not on this Windows machine, and its BEFORE set shows the pages
  before the foundation landed, so the current branch is the true BEFORE. The operator copies only
  the Direction A renders (`mockups/direction-a/png/quest-details-*`, `quest-form-*`,
  `quest-form--edit-*`) over from the machine Phase 94 ran on, as the AFTER references for Q-111 to
  Q-113. Phase 95 scratch lives outside the repository (for example `~/qb-ui-review/phase-95/`).
- **D-23: The first plan opens with an operator checkpoint, as 94-01 did.** The operator confirms
  SQL Server at `localhost:1433` (never provisioned by the agent) and bootstraps the admin account;
  the executor creates player and DM test accounts through Admin and the fixture quests the
  captures need (crowded board, empty board, voting, finalized, signed up, long titles, a Campaign
  board, a quest with a follow-up, a completed quest with and without a recap). Credentials stay in
  the scratch folder, never in the repository. Fixture writes go through the app's own forms
  (Phase 94 D-25).

**Delivery**
- **D-24: Work lands in slices on `milestone/v9-rolling-improvements`, with one release at the
  end** once both gates have passed. Each slice leaves every page usable and the test suite green
  (Phase 94 D-08). A sensible slice order: the shared editor, form scripts and modal; Quest Details
  with the vote patch; the board; the forms; Manage; Quest Log with its two merges; Move.

### Claude's Discretion
- How the per-poster text areas are stored (CSS modifier classes per poster, a small lookup in the
  view or view model) and how "fits" is estimated server-side, provided text never overflows or is
  truncated mid-word and the poster URL no longer comes from an inline `style`.
- Hover behaviour on the posters: keep today's lift and seal tilt if wanted, animating `transform`
  only (no `filter`, which doubles the shadow already in the artwork, A-quests-023) and none under
  `prefers-reduced-motion`.
- How each poster and log card becomes one link (the stretched-link pattern from the audit),
  replacing the inline `onclick` navigation.
- The poster date and any other remaining `.ToString` date renderings move to `Html.WallClock` /
  `Html.LocalTime` according to what the value is; the helpers themselves are not changed.
- Module structure for the moved scripts, within `.claude/ui-guidelines.md` § "No inline code".
- Exact slice boundaries and plan count.

### Deferred Ideas (OUT OF SCOPE)
None. The discussion stayed within the phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are mapped to this phase (`Requirements: TBD` in ROADMAP). The contract is the ROADMAP "Definition of converted" (10 points plus the phase extras) and CONTEXT D-01 to D-24. Research support per contract point:

| Contract point | Research support |
|----------------|------------------|
| DoC 1 no inline style/script/handler | Inventory by file and line (Q3), delegation pattern, token/data-attribute delivery, avatar `onerror` replacement |
| DoC 2 no hex/rgb/`!important` in area sheets | Per-sheet counts reproduce ROADMAP; legacy range notes incl. the shared `.container.mt-4` rule |
| DoC 3 `@section Styles` | Cross-consumer finding for `markdown-*.css` and `.move-control-reason`; two-step removal order |
| DoC 4 twins, merges | Q-07..Q-39 answer table, D-21 verdicts (Edit, CreateFollowUp, Move qualify; Create does not) |
| DoC 5 accessibility bar | Contrast numbers, editor trap fix, stretched-link focus ring, target sizes |
| DoC 6-7 screenshots, mobile UA | Scratch tooling rebuild spec; mobile UA keywords |
| DoC 8 guard tests rewritten | Test-by-test table incl. five tests not in the ROADMAP list |
| DoC 9 helpers untouched, no writes to Events/Quests | Date table; fixture writes only through forms |
| DoC 10 usable at every merge | Slice order, link-then-remove rule |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Source: `C:/Repos/quest-board/CLAUDE.md` and `.claude/*.md` (read this session).

- Never commit to `main`; all work on `milestone/v9-rolling-improvements` or a feature branch. Do not create GSD tags.
- Local database: SQL Server at `localhost:1433`; never install, start, provision or tear down. Windows uses `Trusted_Connection=true` as committed; never edit `appsettings.json`. Integration tests use EF InMemory.
- No GSD/planning references (requirement ids like `D-06`, `Q-23`, phase or plan numbers, planning document names) in source, XML docs, string literals, **or CSS comments**. `BoardPageStaticGuardTests` already scans listed files for `D-01..D-21`, "Phase 93" and plan ids; `UiFoundation*` guards scan the new sheets.
- Read `.claude/ui-guidelines.md` before any Razor view edit (done): modern-card pattern, filled buttons, Cancel left/commit right after `<hr>`, tokens only, no `!important` in layered sheets, `@section Styles`/`Scripts`, `.Mobile.cshtml` twins carry no `@media`, dates via `Html.LocalTime`/`Html.WallClock`.
- EF packages only in `QuestBoard.Repository`. Writes to `Events` and `Quests` only through the change tracker (a guard test fails otherwise). Nothing in this phase writes to either.
- `AmbientClockSeamTests` holds a closed list; do not weaken it. Server-side "what day is it" goes through `IBoardClock`.
- `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` are high-risk; untouched.
- Build/test: `dotnet build`, `dotnet test`. On locked files ask the user to stop the debugger. On Linux never set GC env vars; on `0x80131506` re-run once.
- RIP MCP navigation is preferred for symbol questions where available (not needed here; this phase is views/CSS/JS).
- Memory notes that matter: auto-created worktrees cut from `main` miss milestone work (cut from HEAD); planning docs have mixed line endings (never anchor a match on `\n`); the GSD parsers assume LF.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Poster choice (stable per quest, fits the content) | Service (view helper/view model) | Browser (CSS owns geometry) | Needs title length and quest state, which only the server has; CSS modifier class carries the measured insets |
| Poster/seal artwork delivery, WebP with PNG fallback | CDN/Static (wwwroot) | Browser (`image-set()`) | Pure static files; the browser picks the format |
| Board masonry and card heights | Browser (JS module) | CSS (`aspect-ratio`, column fallback) | Needs viewport width; heights should come from CSS, not data attributes |
| Lightened text zone, seal positions, hover | Browser (CSS `pages` layer) | — | Presentation only; tokens and `color-mix()` |
| Markdown editor behaviour (Tab, name, description) | Browser (`markdown-editor.js`) | Service (partial markup, help text id) | CodeMirror lives client-side; the help text and ids are server-rendered |
| Signup role/character selects, spectator hiding | Browser (module with data markers) | Service (selects, marker attributes) | Form fields post to the existing `Details` action; hiding is presentation |
| Vote buttons markup (`_Calendar`) | Service (partial) | Browser (CSS) | Radio `btn-check` inputs post `DateVotes[i].Vote`; only class changes allowed |
| Revoke/ChangeVote/Delete/RemovePlayerSignup fetches | Browser (modules) | API (existing endpoints, antiforgery) | Endpoints unchanged; token must reach the module without an inline script |
| Dates | Service (`Html.WallClock`/`LocalTime`) | Browser (`site.js` hydration) | Helpers are fixed; views choose the right one |
| Twin selection | Service (`MobileViewLocationExpander`) | — | UA keywords decide; a merge drops the `.Mobile` file |

## Standard Stack

No runtime package is added. The phase is views, CSS and vanilla JS on the existing stack.

### Core (existing, unchanged)
| Item | Version | Purpose | Source |
|------|---------|---------|--------|
| ASP.NET Core MVC + Razor | .NET 10.0.400 SDK | views, view expander | [VERIFIED: `dotnet --version` printed 10.0.400] |
| Bootstrap | 5.3.8 (self-hosted) | `.stretched-link`, grid, `btn-check` | [VERIFIED: `app.css` imports `../lib/bootstrap-5.3.8/css/bootstrap.min.css`] |
| EasyMDE | 2.21.0 (self-hosted) | Markdown editor | [VERIFIED: `_QuestFormScripts.cshtml:53-54` quotes `~/lib/easymde-2.21.0/easymde.min.css`, `easymde.min.js`] |
| Cinzel | variable woff2, self-hosted | display face | [VERIFIED: `app.css` `src: url("../lib/cinzel/cinzel-latin-wght.woff2")`] |
| xUnit v3 3.2.2, FluentAssertions 8.10.0 | | guard and integration tests | [VERIFIED: both csproj `PackageReference` lines] |

### Scratch tooling (never a project dependency, installed only in `~/qb-ui-review/phase-95/tools`)
| Tool | Version | Purpose | Note |
|------|---------|---------|------|
| `playwright-core` | 1.65.0 (latest 2026-11-10) | headless capture, `channel: 'msedge'` | registry says `latest: '1.65.0'` [VERIFIED: `npm view playwright dist-tags`]; legitimacy gate says SUS "too-new" (below) |
| Pillow | 12.2.0, WebP yes | WebP encode, luminance stats, image diff | [VERIFIED: `PIL.__version__`, `features.check('webp')` True] |
| numpy 2.4.4, scipy 1.17.1 | | rectangle search, morphology | [VERIFIED: imports ran] |
| Microsoft Edge 156 / 157 | | browser for Playwright | [VERIFIED: directories `156.0.4314.58`, `157.0.4341.42` under `C:\Program Files (x86)\Microsoft\Edge\Application`] |

**Installation (scratch only, after the operator's legitimacy check):**
```bash
mkdir -p ~/qb-ui-review/phase-95/tools && cd ~/qb-ui-review/phase-95/tools
npm init -y && npm pkg set type=module
npm install playwright-core@1.65.0      # no browser download; launch with channel 'msedge'
```
Never run `npx playwright install msedge`: the docs state it replaces the existing Edge at the OS default location [CITED: playwright.dev/docs/browsers]. If enterprise policy blocks the installed Edge, fall back to `PLAYWRIGHT_BROWSERS_PATH=~/qb-ui-review/phase-95/browsers npx playwright-core install chromium` (scratch path, not system-wide) [ASSUMED: command shape].

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| JS shortest-column masonry | CSS multi-column (`column-width`) | No JS, but fills top to bottom so visual order differs from today; keep JS, add CSS fallback |
| `playwright` package | `playwright-core` | Same API, no bundled browsers; needs a system browser |
| `cwebp` / ImageMagick | Pillow | Neither is installed here; Pillow is enough [VERIFIED: `where cwebp`/`magick` found nothing] |

## Package Legitimacy Audit

Only scratch tooling is installed; no project dependency changes.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| playwright-core | npm | published 2026-11-05 (1.65.0) | ~184.7M/wk | github.com/microsoft/playwright | SUS (too-new) | Flagged. Same false positive Phase 94 cleared; planner puts a `checkpoint:human-verify` before the scratch install (inside the opening operator checkpoint) |
| playwright | npm | published 2026-11-05 | ~166.8M/wk | github.com/microsoft/playwright | SUS (too-new) | Not needed; use `playwright-core` |
| Pillow, numpy, scipy | PyPI | already installed system-wide | — | — | not installed by this phase | No action |

**Packages removed due to SLOP:** none. **Flagged SUS:** `playwright-core` (and `playwright`): `gsd-tools query package-legitimacy check --ecosystem npm playwright playwright-core` returned `"verdict": "SUS"`, `"reasons": ["too-new"]`, `"postinstall": null` [VERIFIED: command output this session]. Because it came from the GSD gate and the 94-01 operator checkpoint already reviewed the same publisher, tag it `[ASSUMED]` for the planner and keep it behind the operator checkpoint.

## Architecture Patterns

### System Architecture Diagram

```
Operator (Gate 1 / Gate 2) <---------------------------------------------+
   |                                                                       |
   v                                                                       |
Scratch tooling (outside repo: ~/qb-ui-review/phase-95)                    |
   manifest + fixtures --(app forms)--> dev app :8000 (working tree)       |
   audit-shoot (desktop UA | mobile UA, touch) --> PNG + report.json ------+
   contrast probe (ancestor-composited) ---> readability report
   poster_stats (Pillow/numpy) --> per-poster insets, luminance, seal slots

Request path in the app
 Browser UA --> MobileDetectionMiddleware ("Mobi","Android","iPhone",...)
            --> MobileViewLocationExpander (Index.Mobile.cshtml if present)
 /quests --> QuestController.Index --> view model + PosterChoice(quest)
        --> Index.cshtml: <article class="quest-poster quest-poster--p4 ...">
              one <a class="row-nav-link" href=Details|Manage> (stretched by CSS)
              seal <img> positioned by .quest-poster--pN modifier (no style=)
        --> quests.css (@layer pages): art via image-set(webp, png), text zone
        --> quest-board.js (module): masonry over aspect-ratio heights
 /Quest/Details --> Details.cshtml | Details.Mobile.cshtml
        --> _Calendar (scoped vote patch) inside the one signup card
        --> quest-details.js (module): revoke/vote fetch, role select hides [data-vote-section]
 Forms --> _MarkdownEditor + _QuestFormScripts (26 consumers!)
        --> markdown-editor.js: Tab leaves, accessible name, aria-describedby
```

### Recommended Project Structure
```
QuestBoard.Service/
  Helpers/QuestPosterCatalog.cs        # poster id, css class, measured text box (px at 352), image size
  Helpers/QuestPosterPicker.cs         # stable-from-id choice among posters that fit (unit-tested)
  Views/Quest/Index.cshtml             # one link per poster, no inline style/onclick
  wwwroot/css/quests.css               # rewritten into @layer pages (poster + shared quest rules)
  wwwroot/css/quests.mobile.css        # phone board/details, @layer pages
  wwwroot/js/quest-board.js            # masonry (moved out of site.js)
  wwwroot/js/quest-form.js             # add/remove proposed dates, submit guard (moved out of site.js and the partial)
  wwwroot/js/quest-details.js          # revoke, change vote, role select, spectator marker, update toggle
  wwwroot/js/quest-manage.js           # delete, remove player, availability sync, max players
  wwwroot/js/avatar-fallback.js        # replaces the repeated onerror= avatar swap
  wwwroot/js/markdown-editor.js        # existing classic script, extended (keep its globals)
  wwwroot/images/Blanks/Blanks w Shadow/Poster1.webp ... (new, next to the PNGs)
```

### Pattern 1: Stable choice among posters that fit
**What:** pick by a deterministic mix of `quest.Id` (an `int`) over the posters whose measured text box holds the estimated content height.
**When to use:** the board view model; never `string.GetHashCode()` (randomised per process in .NET Core).
```csharp
// Skeleton only; constants come from the measurement tables below.
internal static class QuestPosterPicker
{
    private const double CharWidthEm = 0.64;   // Cinzel at weight 600 measured 0.58-0.63 em/char, plus wrap slack
    public static QuestPoster Pick(int questId, string title, bool finalized, bool campaign)
    {
        var fits = QuestPosterCatalog.All.Where(p => p.Holds(title, finalized, campaign)).ToList();
        if (fits.Count == 0) fits = [QuestPosterCatalog.Tallest];       // never truncate: the tallest, at the smaller title step
        var mixed = unchecked((uint)questId * 2654435761u);              // sequential ids must not cycle visibly
        return fits[(int)(mixed % (uint)fits.Count)];
    }
}
```
Source: derived from D-05 and the measurements below.

### Pattern 2: One link per card without changing the pinned anchor
`RowNavigationAccessibilityTests` matches `<a class="row-nav-link" href="...Quest/Manage/{id}...">` exactly (class attribute must be only `row-nav-link`). Stretch it from CSS, not with the `stretched-link` class:
```css
@layer pages {
  .quest-poster { position: relative; }
  .quest-poster .row-nav-link::after { content: ""; position: absolute; inset: 0; z-index: 1; }
  .quest-poster:has(.row-nav-link:focus-visible) { outline: var(--qb-focus-width) solid var(--qb-focus-outer);
    outline-offset: var(--qb-focus-offset); box-shadow: 0 0 0 calc(var(--qb-focus-offset) + var(--qb-focus-width)) var(--qb-focus-inner); }
}
```
The foundation ring is applied to `.row-nav-link:focus-visible` itself [VERIFIED: `base.css:135` lists `.row-nav-link` in the `:focus-visible` selector list]; with a stretched pseudo-element the ring would draw only around the small link text, hence the `:has()` ring on the card.

### Pattern 3: Delegated actions replace inline handlers
```html
<button type="button" class="btn btn-danger" data-action="revoke-signup" data-quest-id="@Model.Quest?.Id">
```
```js
document.addEventListener('click', (e) => {
  const el = e.target.closest('[data-action]');
  if (!el) return;
  // switch (el.dataset.action) { ... }
});
```
Values that were interpolated into JS strings (`removePlayerSignup(@player.Id, '@player.Player.Name')`) move to `data-player-name`, which also removes a JS-string injection path (Security section). The antiforgery token (`@tokens.RequestToken`) moves to a `data-` attribute on a page root or a `<meta>` read by the module.

### Anti-Patterns to Avoid
- **Deleting `legacy-pages.css` lines 299-302 with the board range.** `.container.mt-4 { max-width: none !important; }` is used by `DungeonMaster/EditProfile`, `DungeonMaster/Profile` and `_Layout.GroupPicker` [VERIFIED: grep of `container mt-4` returned exactly those three]. Relocate or leave; delete only 304-791 content that is board-specific or dead.
- **Re-using `site.js` as the home of page code.** It already carries masonry, proposed-date and date-option code; move quest code out, leave hydration, toasts, `data-disable-on-submit`.
- **`filter: drop-shadow()` on hover** (A-quests-023): artwork already has a baked shadow; animate `transform` only.
- **A merged view that keeps a `.Mobile` file in test lists.** Deleting a `.Mobile.cshtml` makes `AmbientClockSeamTests` throw `FileNotFoundException` (it resolves every listed path).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| WebP creation | a build step or package | Pillow one-off script, commit the `.webp` files | Alpha is preserved exactly (max alpha error 0 measured) |
| Contrast probe | ad-hoc eyeballing | the 94-RESEARCH.md `contrast.mjs` reference plus ancestor compositing | The ratios must match the UI-SPEC numbers (4.34 etc.) |
| Card link | JS click handler | CSS stretched pseudo-element over one real `<a>` | Keyboard, middle-click, link preview, screen-reader name |
| Modal | custom dialog | existing Bootstrap modal in `_CharacterSelectModal` | Focus trap and Escape are built in |
| Spectator hiding | selector on `.card.modern-card.mb-3` | one marker (`data-vote-section`) on the vote wrappers | The old selector also matches the DM Controls card |
| Poster text fit | JS measuring and shrinking | server estimate + conservative measured boxes + a Playwright overflow probe in capture | JS fit-to-box reflows and flickers; the probe proves it per fixture |

**Key insight:** this phase is mostly relocation. The risk is not building something wrong but silently dropping a behaviour that another page imports (the editor globals, `.move-control-reason`, `.container.mt-4`).

## Runtime State Inventory

This is a conversion, not a rename. State that could still carry old names after the files change:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None. Quest `Title`, `Description` and `Recap` are content; no stored string carries a class or file name. `Quest.Title` is `[StringLength(200)]` | none |
| Live service config | None found in the repo (no CSP header configured in `*.cs` or `deploy/`; the code comment in `site.js` mentions a CSP, so a reverse proxy may set one) | Operator to confirm whether the proxy sets a CSP before assuming inline code is currently allowed |
| OS-registered state | None | none |
| Secrets/env vars | None; credentials for scratch accounts live only in the scratch folder | none |
| Build artifacts / caches | Browser caches keyed by `asp-append-version` hash for CSS/JS; new `.webp` files are new URLs. `bin/obj` rebuilds | none beyond `dotnet build` |

## Common Pitfalls

### Pitfall 1: The editor and form-scripts partials have 26 consumers
**What goes wrong:** moving `markdown-editor.js` to an ES module or the token into a module hides `initMarkdownEditor` and `window.markdownAntiforgeryToken`; `Contacts/Details` (desktop line 252, mobile line 236) calls both from its own inline script.
**How to avoid:** keep `markdown-editor.js` a classic script, keep `function initMarkdownEditor`, keep `window.markdownAntiforgeryToken`, keep `textarea.easyMDE`. Deliver the token as `<meta name="request-verification-token">` or a `data-` attribute and assign `window.markdownAntiforgeryToken` from it in the same file.
**Warning signs:** `Contacts/Details` note editor throws `initMarkdownEditor is not defined`; the guard `QuestFormPartial_LoadsEditorFromLib` still needs the partial to contain `~/lib/easymde-2.21.0/easymde.min.css` and `.js` [VERIFIED: `UiFoundationViewLibraryGuardTests.cs:73-76`].

### Pitfall 2: `form[action*="/Quest/"]` scoping inside `_QuestFormScripts`
**What goes wrong:** the partial's submit guard targets `form[action*="/Quest/"]` so it no-ops on the other 24 consumers. A module that attaches to `document.querySelector('form')` would wire the logout form.
**How to avoid:** keep the exact selector (quoted in the existing comment: `Scoped to the quest form specifically`).

### Pitfall 3: Removing a stylesheet from the layouts before every consumer links it
`markdown-editor.css` consumers (non-Quest): Characters Create/Edit, Contacts Create/Edit/Details, DM EditProfile, Events Create/Edit, ShopManagement Create/Edit (+ twins). `markdown-content.css` (`Html.Markdown` output) consumers: Characters/Details, Contacts/Details, DM Profile, Events/Details, Series/Details, `_ShopItemDetailsContent`, `Shop/Details.Mobile` [VERIFIED: `Html.Markdown` grep list]. None of Phases 96-98's ROADMAP scopes mention linking these. **How to avoid:** see Open Question 1; default: each slice adds the link where it renders the markup, a final plan adds the links to the foreign consumers and removes the layout entries in the same commit with a guard test.

### Pitfall 4: `.move-control-reason` and `Events/Details`
Defined at `move-entry.css` ("`.move-hint, .move-control-reason { font-size: .875rem; }`") and `mobile.css` (`.move-control-reason { font-size: 0.875rem; color: #D9CDA8; }`), used at `Events/Details.cshtml:111`, `Quest/Details.cshtml:792`, `Details.Mobile.cshtml:473`, `Manage.cshtml:711`, `Manage.Mobile.cshtml:499`. **How to avoid:** add the rule to `components.css` (layered, with a token colour), delete it from both sheets. `BoardMoveControlTests` pin `<p class="move-control-reason[^"]*" id="move-reason">` (lines 105, 112).

### Pitfall 5: `site.js` masonry reads `data-image-*` and `.fantasy-quest-card`
`setCardHeights()` computes `cardWidth * (imageHeight / imageWidth)` and writes `card.style.height`; `layoutMasonry()` absolutely positions every child of `.quest-board-container`. Removing the data attributes without replacing the height source collapses every poster to height 0 (text is `position: absolute`). **How to avoid:** per-poster `aspect-ratio` in the modifier class; `quest-board.js` positions via CSSOM only; leave a CSS column fallback.

### Pitfall 6: Avatar `onerror` swaps and images that failed before the script ran
Eight view sites use `onerror="this.style.display='none'; this.nextElementSibling.style.display='inline-flex';"`. `error` does not bubble and fires before a deferred module runs. **How to avoid:** a capture-phase listener plus a sweep `img.complete && img.naturalWidth === 0` at load; the placeholder becomes `hidden` by default and is un-hidden by class.

### Pitfall 7: Tests that read view sources or rendered class names
`BoardMoveViewsTests.cs:393-410` forbid `style="`, `<script`, `text-muted`, `form-text`, `fw-bold`, `Html.Raw` and `<span` (desktop) in the Move views; `:416-421` forbid `@media` in both move sheets. Keep them true or rewrite deliberately. `MobileViewsTests` pins phone markers by class name (table below).

### Pitfall 8: Flaky wall-clock guard at 20:00 UTC
`WallClockUnmovedTests.FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour` fails if run in the first minute of 20:00 UTC (recorded in Phase 94 `deferred-items.md`). A red there at that time is the known flake, not a regression.

### Pitfall 9: Role select and the quest signup are posted by the same existing action
The phone form must post the same field names as desktop: `CharacterId` and `selectedRole` [VERIFIED: `Details.cshtml:558` `name="CharacterId"`, `:575` `name="selectedRole"`]. Spectator voting hides the calendar inputs but the hidden `DateVotes[i].ProposedDateId` inputs still post; that is today's behaviour on desktop, keep it.

## Q1: Poster measurements (D-03, D-04, D-05, D-06, D-08)

### Method (re-runnable by the mockup step)
Scripts were written to the session scratchpad (not durable): `poster_stats.py`, `seal_slots.py`, `strengths.py`. The method, so it can be rebuilt in a minute: [VERIFIED: ran this session on all 14 PNGs]
1. Read RGBA. **Body** = alpha >= 250 (drops the baked semi-transparent drop shadow).
2. **Hazard** = dark ink pixels (relative luminance < 0.03: outlines, hatch marks, burn) union **nail** pixels (bright, saturation < 0.30, luminance > 0.12); dilate by 1.2% of image width.
3. **Clear** = body minus hazard, eroded by 3% of width from the torn edge.
4. Largest axis-aligned rectangle of Clear on a 1/5 grid, with the constraint "at least 190 card-px wide" (card width 352px, the board's `--card-width`).
5. Luminance and contrast inside that rectangle, with the ink `--qb-ink` `#1a0f08` (L = 0.0058) against the artwork blended with the paper colour `#f4e4bc` at alpha a in sRGB (what a browser composites).

### Text boxes (insets are % of the image; px are CSS px at the 352px card)
`--qb-ink: #1a0f08` and `--qb-surface-paper: #f4e4bc` quoted from `tokens.css:46,33` [VERIFIED: Read this session].

| Poster | Image px | Card height | Text box T / R / B / L % | Box w x h px | Verdict vs full content |
|--------|----------|-------------|---------------------------|--------------|--------------------------|
| Poster1 | 1000x1400 | 493 | 7.5 / 16.5 / 9.6 / 20.5 (crease tolerated; with the crease excluded the box is only 7.5/15.5/52.1/20.5, 225x199) | 222 x 408 | holds everything; best for long titles |
| Poster2 | 1000x1400 | 493 | 7.9 / 16.5 / 46.4 / 21.0 | 220 x 225 | holds it; the lower 46% is the baked tail (rope and a printed wax seal), not usable |
| Poster4 | 1000x980 | 345 | 13.8 / 23.5 / 15.8 / 25.0 (tall) or 38.8 / 15.0 / 15.8 / 14.5 (wide, lower sheet only) | 181 x 243 or 248 x 157 | marginal: the tall box is 9px under the 190px width; the wide box is too short for a finalized quest |
| Poster5 | 600x675 | 396 | 17.8 / 23.3 / 20.0 / 17.5 | 208 x 246 | holds it |
| Poster6 | 600x800 | 469 | 30.0 / 21.7 / 13.8 / 20.0 | 205 x 264 | holds it |
| Ruined 1 | 1000x900 | 317 | 14.4 / 16.5 / 26.1 / 21.0 | 220 x 188 | borderline: holds a finalized quest only with a one-line title (176 needed) |
| Ruined 2 | 1000x900 | 317 | 20.6 / 19.0 / 38.9 / 21.0 | 211 x 128 | no |
| Ruined 3 | 1000x1025 | 361 | 13.7 / 16.5 / 51.2 / 17.0 | 234 x 127 | no |
| Ruined 4 | 1000x980 | 345 | 43.4 / 14.5 / 33.7 / 28.5 | 201 x 79 | no |
| Ruined 5 | 600x675 | 396 | 20.0 / 25.0 / 43.7 / 19.2 | 197 x 144 | no for finalized; maybe an open quest with a one-line title (126 needed) |
| Ruined 6 | 600x548 | 321 | 26.5 / 18.3 / 47.1 / 28.3 | 188 x 85 | no |
| Ruined 7 | 600x548 | 321 | 26.5 / 22.5 / 43.4 / 18.3 | 208 x 97 | no |
| Ruined 8 | 601x690 | 404 | 19.6 / 19.3 / 58.7 / 27.5 | 187 x 88 | no |
| Poster3 | 601x1400 | 820 | retired (D-02); 9.3 / 20.1 / 17.1 / 23.3 would be 199 x 603 | | not in the pool |

Content-height budget at 14px floor (card px, line heights 1.5 body, 1.2 title; gaps 8): DM line 21; date block 2 lines 42; count line 21; CR badge 28.
- open quest: 21 + 21 + 28 + 24 gaps = **94**, plus title lines x 24 (20px title) and 8 gap.
- finalized quest: 21 + 42 + 21 + 28 + 32 gaps = **144**, plus title and gap.
- Campaign board: DM line only, 21 + 8.
A finalized quest with a one-line title needs 176; with two lines 200.

**Recommendation for D-04:** the pool is Poster1, Poster2, Poster4 (tall box), Poster5, Poster6 plus Ruined 1 as a candidate the mockup shows. Ruined 2 to 8 do not qualify under the "full content" rule as measured; the burnt lower part is black-red (L < 0.03) and cannot be lightened to a clean text zone without a visible slab. If the operator wants more ruined posters back, the only honest route is relaxing D-04 to a **per-quest** fit (a ruined poster serves only open quests with short titles); record that as a Gate 1 question, not a research assumption.

### Title budget (server-side "fits" estimate, D-05)
Cinzel at weight 600 measured through Pillow on the shipped variable woff2 (axes: Weight 400-900): 0.614 em/char for a 27-char title, 0.621 for 32, 0.588 for 96, 0.578 for mixed lowercase prose, 0.633 for all caps [VERIFIED: measurement this session; font reads required `set_variation_by_axes([600])`]. Use **0.64 em/char** plus word-wrap slack. At the 20px step that is 12.8px/char; at 16px 10.2px/char. Title lines available = floor((box height - meta - 8) / line height), line height 24 (20px) or 19 (16px).

| Poster | Open: title chars at 20px | Finalized: title chars at 20px |
|--------|---------------------------|--------------------------------|
| Poster1 (222x408) | about 190 | about 160 |
| Poster6 (205x264) | about 90 | about 60 |
| Poster2 (220x225) | about 85 | about 50 |
| Poster5 (208x246) | about 80 | about 48 |
| Poster4 tall (181x243) | about 70 | about 40 |

The title limit is 200 characters (`[Required] [StringLength(200)] public string Title`, `Quest.cs:7-11`) [VERIFIED: Read]. A 200-character title does not fit even Poster1 at 20px; at the 16px step (`--qb-text-base`) it needs about 9 lines x 19 = 171 plus meta 144 = 315 < 408, so Poster1 at 16px is the guaranteed floor. Plan: three title tiers by estimated lines (20px, 16px), long titles routed to Poster1, and a Playwright overflow probe (`scrollHeight <= clientHeight` of the text box) over the long-title fixtures.

### Lightened text zone (D-08)
Measured over each box: contrast of `#1a0f08` against artwork blended with `#f4e4bc` at alpha a. Raw ink contrast at the 5th percentile reproduces the G1 figure (3.8 to 4.2:1: Poster6 3.98, Poster1 4.05, Poster4 4.19, Poster5 4.29, Poster2 4.33; Ruined 1 only 2.90).

| Poster | brightest L | a = 0.15: p5 / % px under 4.5 | a = 0.25 | a = 0.35 |
|--------|-------------|-------------------------------|----------|----------|
| Poster1 | 0.462 | 5.04 / 2.1% | 5.82 / 0.6% | 6.69 / 0.1% |
| Poster2 | 0.412 | 5.32 / 0.9% | 6.08 / 0.1% | 6.94 / 0.0% |
| Poster4 | 0.500 | 5.15 / 1.4% | 5.91 / 0.1% | 6.77 / 0.0% |
| Poster5 | 0.481 | 5.28 / 2.2% | 6.05 / 0.7% | 6.92 / 0.1% |
| Poster6 | 0.480 | 4.98 / 2.1% | 5.76 / 0.2% | 6.64 / 0.0% |
| Ruined 1 | 0.462 | 3.80 / 8.1% | 4.55 / 4.8% | 5.44 / 0.7% |

- **Which pixel binds:** for dark ink the darkest pixels bind (min raw ink contrast 1.5 to 2.5:1 on stain specks and print lines). A true every-pixel 4.5:1 needs alpha 0.32 to 0.46 across the pool. The brightest pixel (L 0.32 to 0.50) matters only if the text were light on a darkened zone, which D-08 does not use. State this at Gate 1 so the strengths are chosen on the right metric.
- **Recommended switcher:** 0.15 ("whisper", p5 >= 4.98 on blanks), 0.25 (p5 >= 5.76, <= 0.7% specks), 0.35 (nearly every pixel). Judge the shipped value with the rendered probe on the actual glyph boxes, not the whole rectangle (a speck under no glyph does not fail).
- **No new colour token needed:** build the zone with `color-mix(in srgb, var(--qb-surface-paper) 25%, transparent)`. The token budget notes "at most 40" opaque hex values with 38 in use [VERIFIED: `tokens.css:12-13`], so a new hex literal is not available without a cap discussion; `color-mix()` on `--qb-surface-paper` adds none.
- Feather the zone with a matching `box-shadow` spread of the same mixed colour so it does not read as a slip (a hard rectangle is the rejected slip treatment).
- Re-run the numbers on the **WebP** that ships, not the PNG.

### Seal positions (D-06)
Searched for an 80px seal (the current `.seal-image` size; 65 and 55px under 480 and 320 viewport) whose box lies fully inside the image and clear of the text box plus 6px. Candidates (centre x% / y% of the image) [VERIFIED: ran]; the mockup step confirms visually:

| Poster | Status seal (bottom-left) | "Signed up" seal (top-right) | Note |
|--------|---------------------------|-------------------------------|------|
| Poster1 | 11.6 / 91.7 | 88.4 / 57.4 | text box top-aligned at 199px keeps both seals clear; with the tall 408px box only the margins are free |
| Poster2 | 28.4 / 63.1 | 77.2 / 63.1 | on the sheet edge above the tail; the art already prints a seal on the tail, do not use it |
| Poster4 | 11.6 / 25.3 | 88.4 / 11.8 | uses the tall box |
| Poster5 | none at 80px | none at 80px | the box leaves no free 80px square; use a 56px seal or shrink the box |
| Poster6 | 17.3 / 20.0 | 88.7 / 8.5 | |
| Ruined 1 | 11.6 / 87.1 | 80.8 / 85.8 | both on the burnt bottom |

Today's rule `bottom: 15%; left: 15%` with `translate(-50%, -50%)` puts the seal over the DM line and titles (verified in `Index.cshtml` lines 85-97 and `quests.css:222-249`). The per-poster slot goes in the modifier class as custom properties (`--seal-x`, `--seal-y`), not `style=`.

## Q2: WebP derivatives (D-10)

Pillow WebP quality 82, `method=6`, alpha kept lossless by default [VERIFIED: measured]:

| Set | PNG total | WebP at full size | WebP at 2x card width |
|-----|-----------|-------------------|------------------------|
| 14 posters | 10,564 KB | 881 KB (-91%) | 652 KB |
| Seals (4) | 1,042 KB | 118 KB (-89%) | about 44 KB at 240px |
Examples: Poster1 1834 -> 124 KB, Ruined 3 701 -> 57 KB, `Eagle Seal.png` 969x936 (556 KB) is displayed at 80px: 61 KB at full size, 11 KB at 240px. Alpha maximum error 0; composite mean error 1.0 to 2.0 of 255 (p99 9 to 19), so shadows do not band. Quality 90 is +40 to 60% size for mean error 0.8 to 1.6; 82 is the recommendation (operator judges at Gate 1).

**CSS pattern.** `image-set()` with `type()` is Baseline widely available since September 2023 [CITED: developer.mozilla.org/en-US/docs/Web/CSS/image/image-set]. Declare the plain PNG first as the fallback, then the set; quote URLs because the folders contain spaces (`Blanks w Shadow`):
```css
@layer pages {
  .quest-poster--p1 {
    background-image: url("../images/Blanks/Blanks w Shadow/Poster1.png");
    background-image: image-set(
      url("../images/Blanks/Blanks w Shadow/Poster1.webp") type("image/webp"),
      url("../images/Blanks/Blanks w Shadow/Poster1.png") type("image/png"));
    aspect-ratio: 1000 / 1400;
  }
}
```
Seals benefit most (Eagle). Seals are `<img>` today (`src="/images/Wax Seals/@waxSeal"`); use `<picture><source type="image/webp" srcset=...><img ...></picture>` so `alt` is kept, or keep `<img>` with the WebP only if every supported browser reads WebP (not verified for the group's devices, so keep the PNG fallback).
Resizing the 1000px posters to 704px (2x for a 352px card) halves the size again but should be validated for crispness on the 1.25x/1.5x Windows displays; recommend shipping the full-size WebP first.

## Q3: Inventory (reproduced) and where each goes

Per file, inline `style=`, inline `<script>`, `on*=` handlers, total lines [VERIFIED: scripted count this session, matches the ROADMAP totals 26 / 11 / 59 / 0]:

| File | style= | script | on*= | Lines | Where it goes |
|------|-------:|-------:|-----:|------:|----------------|
| Quest/Index | 3 | 0 | 1 | 154 | poster url + seal offsets -> modifier classes; `onclick` -> one link |
| Quest/Index.Mobile | 0 | 0 | 1 | 119 | `onclick` card navigation -> stretched link |
| Quest/Details | 10 | 2 | 9 | 1064 | avatars (6 style, 3 onerror) -> classes + avatar module; `font-size: 0.8em` x3 -> `small` helper class; `display:none` (update form) -> `hidden` class; `revokeSignup/changeVote/showUpdateForm/hideUpdateForm`, role select and character sync -> `quest-details.js` |
| Quest/Details.Mobile | 3 | 2 | 5 | 554 | same module, same markers |
| Quest/Create, Create.Mobile | 0 | 0 | 2 each | 149 / 128 | `addProposedDate`/`removeProposedDate` -> `quest-form.js` |
| Quest/Edit, Edit.Mobile | 0 | 0 | 2 each | 174 / 134 | same |
| Quest/CreateFollowUp (+Mobile) | 0 | 1 each | 3 each | 202 / 198 | `addDate/removeDate` inline script -> `quest-form.js` |
| Quest/Manage | 7 | 1 | 13 | 938 | delete/remove/refresh/confirm handlers and the vote-availability script -> `quest-manage.js`; `display: inline` x3 on forms -> classes; avatars |
| Quest/Manage.Mobile | 0 | 1 | 10 | 619 | same module |
| QuestLog/Index (+Mobile) | 0 | 0 | 1 each | 110 / 79 | `onclick` -> stretched link |
| QuestLog/Details (+Mobile) | 1 each | 0 | 1 each | 216 / 189 | avatar fallback |
| QuestLog/EditRecap (+Mobile) | 0 | 0 | 0 | 53 / 54 | merge |
| Move/Index (+Mobile) | 0 | 0 | 0 | 278 / 259 | already clean; merge candidate |
| Quest/_QuestFormScripts | 0 | 2 inline + 2 `src` | 0 | 58 | `quest-form.js`; keep the 2 lib tags |
| Quest/_QuestCard | 1 | 0 | 2 | 105 | delete (Q-130) |
| Quest/_QuestSection | 0 | 0 | 0 | 41 | delete (Q-130) |
| Shared/_MarkdownEditor | 0 | 0 | 0 | 29 | help text, describedby |
| Shared/_CharacterSelectModal | 0 | 1 | 0 | 144 | modal module |

Stylesheets (counts exclude comments) [VERIFIED: scripted]: `quests.css` 52 hex / 89 rgb / 96 `!important`; `quests.mobile.css` 6/31/14; `quest-edit.mobile.css` 1/11/7; `quest-followup.mobile.css` 2/16/8; `quest-log.mobile.css` 2/16/6; `quest-log-detail.mobile.css` 5/18/9; `home.mobile.css` 1/16/6; `move-entry.css` 5/0/0; `move-entry.mobile.css` 7/5/0; `markdown-editor.css` 1/0/2; `markdown-content.css` 2/2/0; `dm-create.mobile.css` 1/11/7; `dm-manage.mobile.css` 3/13/10. Sum of the 13 sheets 88/228/165; `legacy-pages.css` lines 1-38 (6/0/0), 299-791 (30/41/42), 1185-1264 (1/7/5) = 37/48/47; `mobile.css` 1/0/0. **Grand total 126 / 276 / 212: identical to the ROADMAP.**

`legacy-pages.css` ranges [VERIFIED: Read]:
- **1-38** `.quest-card`, `.quest-status-open|finalized|done`, `.date-option`, `.vote-summary` (duplicated verbatim at `quests.css:5-41`). `quest-status-*` is used only by the dead `_QuestCard`; `vote-summary` only by `Manage.Mobile`.
- **299-791** board container overrides (299-316, **including the shared `.container.mt-4` rule 299-302**), poster/seal/CR rules duplicated in `quests.css`, the dead custom-radio vote UI (`.custom-radio-*`, 635-750: no view uses `custom-radio-label`/`custom-radio-group`/`vote-btn`/`difficulty-*`), and `.date-option`/`.manage-date-option` (752-790; both used by `Manage*`, and `site.js` `makeDataOptionsClickable` binds `.date-option` and `.manage-date-option`).
- **1185-1264** older Quest Log card rules (overridden by `quests.css`) and `.vote-btn` (dead).
- **`mobile.css` Move rule:** `.move-control-reason { font-size: 0.875rem; color: #D9CDA8; }` (the only hex; shared by phone Details/Manage and `Events/Details`).

Cascade facts that matter: `quests.css` is unlayered and redeclares `:root { --card-width: 350px }` while `tokens.css` declares 352px; the unlayered one wins and `site.js` reads it with `getPropertyValue('--card-width')`. Deleting it changes the card width 350 -> 352 (2px); acceptable, but re-baseline the capture.

## Q4: EasyMDE keyboard trap (D-12, D-13)

**How Tab is bound.** In EasyMDE 2.21.0 the merged key map (`l`) is built and then Tab is set by the library: `l.Enter="newlineAndIndentContinueMarkdownList",l.Tab="tabAndIndentMarkdownList",l["Shift-Tab"]="shiftTabAndUnindentMarkdownList",l.Esc=function(e){e.getOption("fullScreen")&&v(a)}`, passed to CodeMirror as `extraKeys:l` [VERIFIED: read from `easymde.min.js`]. The indent commands call `execCommand("indentMore")` inside lists and otherwise insert spaces, so Tab never reaches the browser; Escape does nothing outside full screen. That is A-characters-006 (two Tabs and Escape leave `document.activeElement` on the same textarea).

**Fix.** CodeMirror treats a key bound to `false` as "not handled" and leaves it to the browser: in the vendored source `if(!1===r)return"nothing"` in the lookup, and the manual says "If the property is set to `false`, CodeMirror leaves handling of the key up to the browser" [VERIFIED: `easymde.min.js` function `Zo`; CITED: codemirror.net/5/doc/manual.html]. After constructing the editor:
```js
const cm = easyMDE.codemirror;
cm.setOption('extraKeys', Object.assign({}, cm.getOption('extraKeys'), { 'Tab': false, 'Shift-Tab': false }));
```
(mutating the existing object also works, since `e.options.extraKeys` is read on every key.) Enter list continuation stays.

**Accessible name.** CodeMirror 5 exposes `screenReaderLabel` [VERIFIED: option registered in the vendored build: `n("screenReaderLabel",null,...)`; it sets `aria-label` on the input textarea or the contenteditable div]. The visible `<label for="@elementId">` points at the original textarea, which EasyMDE hides, so it names nothing. Set the option from the label text (`document.querySelector('label[for=...]').textContent` trimmed) and give the input field `aria-describedby`:
```js
cm.setOption('screenReaderLabel', labelText);
cm.getInputField().setAttribute('aria-describedby', textarea.id + '-help');
```
`inputStyle` defaults to `contenteditable` on mobile and `textarea` otherwise, so use `getInputField()` and not a selector [VERIFIED: option default `x?"contenteditable":"textarea"`].

**Visible help text (D-13).** In `_MarkdownEditor.cshtml` replace the `<i ... data-bs-toggle="tooltip" title="Supports Markdown formatting. Leave a blank line between paragraphs.">` with `<div id="@(elementId)-help" class="form-text">Supports Markdown formatting. Leave a blank line between paragraphs.</div>` after the field (the sentence is unchanged), and put the required marker beside the label. Keep `data-valmsg-for` and the char counter markup: `markdown-editor.js` looks up `.markdown-editor-char-count-current` through `textarea.closest('.mb-3')`.

**Side effects to record.** EasyMDE toolbar buttons are created with `l.tabIndex=-1` [VERIFIED: minified source], so they were and stay outside the Tab order; formatting stays reachable by typing Markdown and EasyMDE shortcuts. Phase 67 already accepted the sub-44px phone toolbar buttons. Tab no longer indents, as D-12 states. **Verify with a probe**: focus the editor, press Tab; `document.activeElement` must leave the CodeMirror input (probe in the capture tool, same shape as the audit's `probe3.json`).

**Which pages render the partial.** The six Characters-area pages the audit names (each with a twin): `Characters/Create`, `Characters/Edit`, `Contacts/Create`, `Contacts/Edit`, `Contacts/Details`, `DungeonMaster/EditProfile`. The partial is also rendered by `Events/Create`, `Events/Edit` (merged views), `ShopManagement/Create`, `ShopManagement/Edit` and the Quest and QuestLog forms [VERIFIED: grep of `_MarkdownEditor`]. All of them get the new behaviour; Events and Shop have no phase that verifies it, so Phase 95 should probe one Events form and one ShopManagement form.

## Q5: Signup forms, role select, character sync, vote patch (D-14, D-15)

- **Desktop (open quest, not signed up):** `Details.cshtml:549-630`: `<form asp-action="Details" method="post">`, hidden `Id`=0 and `Quest.Id`; `CharacterId` select inside `@if (charactersOn)` (lines 553-567, "Select Character (Optional)" with `-- Sign up without character --`); role select `id="selectedRole"` (lines 570-587) with the three options `Player - Join as an active player`, `Assistant DM - Help the Dungeon Master`, `Spectator - Watch the session`; the instruction paragraph `id="votingInstruction"`; one `card modern-card mb-3` per calendar month (lines 613-624).
- **Mobile:** the equivalent form (`Details.Mobile.cshtml:176-210`) has neither select; it posts only `Id`, `Quest.Id` and `DateVotes[i].ProposedDateId`. [VERIFIED: Read] The server defaults the role to Player. D-14 adds both selects with the same names; the desktop help list becomes the same visible text.
- **Spectator script (desktop, `Details.cshtml:1035-1062`):** `const calendarCards = document.querySelectorAll('.card.modern-card.mb-3');` hides **every** such card on role change, which includes the DM Controls card (`Details.cshtml:770` `<div class="card modern-card mb-3">`). That is A-quests-011. Replace the selector with a dedicated attribute on the voting wrapper: after Q-112 (months directly inside the one signup card) a single `<div data-vote-section>` wraps the months, and the module toggles `hidden` on it; the message element `#votingInstruction` is kept (text: "As a spectator, you will be automatically approved. No date voting required." and the restored "Click on the Yes/Maybe/No buttons for each proposed date in the calendar below.").
- **Join forms (finalized quest)** copy a single character select into three hidden `characterId` inputs through a small inline script on each layout (`Details.cshtml:514-525`, `Details.Mobile.cshtml:439-449`); both move into `quest-details.js`. Ids differ (`finalizedQuestCharacter` vs `finalizedQuestCharacterMobile`); unify on data attributes.
- **Vote patch in `_Calendar` (Phase 97 owns the partial):** the three radio sets use `btn-check` plus `<label class="btn btn-sm btn-outline-success|warning|danger">` on desktop and `btn btn-outline-* flex-fill` in `.calendar-vote-row` on phone [VERIFIED: `_Calendar.cshtml:132-150`, `_Calendar.Mobile.cshtml:44-60`]. Outline buttons fail the foundation rule (filled, never outline; 1.21 to 3.36:1 on glass). Scoped patch: change only the label classes (filled success/warning/danger need a "checked" state that is not colour alone, so add an icon or `aria-label`) and the sizes (`font-size: 7px` badge and 8 to 10px type in `calendar.css` 385-440 and 673-765 break the 14px floor). Constraints the patch must keep, all pinned by tests:
  - `_Calendar.cshtml` must still contain the string `isDetailsPage && questOnDay.Quest.Id == currentQuestId` and exactly one `_LinkedDayEntries` after it (`LinkedBoardStaticGuardTests.cs:119-128`).
  - `_Calendar.Mobile.cshtml` keeps `calendar-date-label-mobile` < `_LinkedAlsoThatDay` < `calendar-vote-row` in source order (`:130-142`) and Mobile tests need `btn-check` and `calendar-date-entry-mobile` (`MobileViewsTests.cs:343-362`).
  - `.linked-entry*` rules in `calendar.css` and `.also-that-day--cancelled` in `quests.mobile.css` (`line-through`) stay (`LinkedBoardCalendarStyleTests`).
  - `calendar.css` stays in the desktop global list; `Details.cshtml` has no `@section Styles` today, so add one linking `calendar.css` (a duplicate link is harmless until Phase 97 moves it) and `quests.css`.
  - The `.current-quest` block (673-765) uses `!important` and fixed heights (`.quest-event.current-quest:has(.vote-buttons) { height: 90px !important; }`); the vote patch should override from the `pages` layer only what the vote buttons need, and leave the rest for Phase 97. Q-134 says Quests carries the scoped patch and Events verifies.
- **Avatar placeholders** (3 in Details, 2 in Manage, 2 in QuestLog/Details one per layout, 1 in `_QuestCard`) use `onerror` plus inline width/height 32px; one `.avatar-32` class and `avatar-fallback.js`.

## Q6: D-21 merge re-check (identical or layout-only, no deliberate difference, phone differences CSS-only)

Method: source diff of each pair after applying the recorded "add it to the other view" answers and D-16/D-17. [VERIFIED: files read this session; verdicts are judgement on source, not rendered output]

| Pair | Verdict | Why |
|------|---------|-----|
| QuestLog/EditRecap | merge (approved) | identical inventory; wrapper classes, `<hr>`, `flex-fill` differ |
| QuestLog/Details | merge (approved) | same sections, links, gates; grouping (8/4 grid vs three stacked cards) is CSS |
| **Quest/Edit** | **qualifies, propose at Gate 1** | same fields in the same order (`Id`, `Quest.Title/Description/Rewards/ChallengeRating/TotalPlayerCount/DungeonMasterSession`, hidden `Quest.ProposedDates[i]`), same gates (`Campaign`, `IsFinalized`). The remaining differences are all closed by recorded answers: Editing Tips card (Q-26 add to phone), helper sentences (Q-27), per-date validation (Q-28), existing-signups warning position (pick one), Cancel wording (D-17). `Edit.cshtml:11-170` two-column `row`/`col-lg-8` + tips card vs `Edit.Mobile.cshtml:15-129` single card with `quest-edit-card-mobile` |
| **Quest/CreateFollowUp** | **qualifies, propose at Gate 1** | identical fields; one sentence to add on the phone (Q-29); the only other difference is the date-row remove control (desktop icon-only `btn-outline-danger` without a name, phone filled "Remove"): unify on a filled `btn-danger` with icon and the word "Remove". That is a control-appearance change, not a removed action |
| Quest/Create | **does not qualify** | Q-24 recorded the desktop "Quest Creation Tips" card as **deliberate** (desktop only), so a deliberate difference remains |
| **Move/Index** | **qualifies, propose at Gate 1** | the pair was content-differs by exactly one heading (Q-39 answered add). Everything else is the same markup (`Index.cshtml:102-255` vs `Index.Mobile.cshtml:88-239` copy the consequence list) and the same `Model.Show*` conditions; layout differs by `col-lg-4/8` vs stacked, `<hr>` and `flex-fill` |
| Quest/Details, Index, Manage | do not merge | content differs by recorded deliberate items (Q-08, Q-12, Q-16, Q-17, Q-20, Q-30, Q-32, Q-33, Q-34) and larger structure |
| Home/Index | moved to Phase 96 | not in this phase |

**Tests each merge would change** (from the audit's merge table, confirmed against the files):
- **EditRecap:** no `MobileViewsTests` fact names it; `QuestLogControllerIntegrationTests` (recap link at line 259 `/QuestLog/EditRecap/{quest.Id}`, status facts at 265-289, 367-396, 476-489) assert links and status codes only, so they pass once the desktop view serves both UAs; `AmbientClockSeamTests` is unaffected (EditRecap is not listed).
- **QuestLog/Details:** `MobileViewsTests.GetMobilePage_QuestLogDetails_ReturnsSuccessAndMobileLayout` (asserts `quest-log-detail-main-card` and `quest-log-detail.mobile.css`, lines 119-120 of that fact) must be rewritten to assert the mobile layout body class plus a surviving phone marker; `quest-log-detail.mobile.css` retires.
- **Quest/Edit merge:** `MobileViewsTests.GetMobilePage_QuestEdit_ReturnsSuccessAndMobileLayout` (asserts `quest-edit-card-mobile` and `quest-edit.mobile.css`, ~lines 887-888); `BoardPageStaticGuardTests` and `AmbientClockSeamTests` do not list Edit.
- **CreateFollowUp merge:** `GetMobilePage_QuestCreateFollowUp_...` (asserts `quest-followup-card-mobile`, `quest-followup.mobile.css`, ~lines 922-923). The Manage DM-controls tests pin a CreateFollowUp link (`QuestManageDmControlsTests.cs:117` regex `class="btn btn-primary w-100 mt-2"`), unaffected by the form merge.
- **Move merge:** `BoardMoveViewsTests.cs:225-236` (desktop sheet only, mobile sheet only, `move-card-mobile`), `:390-393` InlineData names `Index.Mobile.cshtml` (read throws if the file is deleted), `BoardMoveEventTests.cs:451, 463`, `BoardMovePageTests.cs:281`, `BoardMoveEndToEndTests.cs:197`, the presence lists `BoardMoveStaticGuardTests.cs:91, 120` and `BoardPageStaticGuardTests.cs:130`, and `AmbientClockSeamTests.cs:43-44` (both Move views are in the closed list; removing the `.Mobile` entry from `GuardedRelativePaths` is required) [line numbers for the BoardMove* tests come from the audit's merge table and were not re-opened].
- A merge also removes the `.Mobile.cshtml` from `MobileViewLocationExpander` selection tests only where such a test exists for that page (none found for Edit/CreateFollowUp/Move beyond the facts above).

## Q7: Guard tests (what each pins, how to rewrite to the same intent)

| Test | What it pins | File:line | Rewrite |
|------|--------------|-----------|---------|
| `MobileViewsTests` board facts | phone board renders `quest-card-mobile`, never `fantasy-quest-card` or `Blanks w Shadow`; `CR 3`; one of `bg-success|bg-primary|bg-secondary`; `bg-primary` for finalized; `/Quest/Details/{id}`; "Signed up" | `MobileViewsTests.cs:62-166` | keep; the desktop poster markup moves to `quest-poster` names, so `NotContain("fantasy-quest-card")` stays true; add the adventurer count assertion for Q-22 |
| `MobileQuestDetails_..._RendersVoteButtons`, `..._ParticipantListIsStacked` | `btn-check`, `quests.mobile.css`, `participant-row`, no `table-responsive`, a player name | `:172-213` | keep markers; if `quests.mobile.css` is renamed/merged re-pin on the link in `@section Styles` |
| Quest Log mobile facts | `quest-log-item`, title, DM name; `quest-log.mobile.css` link | `:219-247` | keep (log Index is not merged) |
| Calendar vote fact | `btn-check`, `calendar-date-entry-mobile` | `:343-362` | keep |
| DM create/manage | `dm-create-card-mobile` + `dm-create.mobile.css`; `manage-date-option`, `dm-vote-summary`, `dm-manage.mobile.css` | `:367-381`, `:388-405` | the sheets move with their pages; keep class markers or re-pin on the layout body class plus a surviving marker |
| Edit / CreateFollowUp / Log detail | `quest-edit-card-mobile` + `quest-edit.mobile.css`; `quest-followup-card-mobile` + sheet; `quest-log-detail-main-card` + sheet | `:872-888`, `:900-923`, `:961-987` | if merged, the UA-selection intent becomes "mobile layout body class present and desktop-only markers absent"; if not merged keep |
| `RowNavigationAccessibilityTests` (**not in ROADMAP list**) | `<a class="row-nav-link" href="...Quest/Manage|Details/{id}...">` desktop and phone board, `quest-list-mobile` | `RowNavigationAccessibilityTests.cs:111-243` | pins the **exact** anchor markup; keep `class="row-nav-link"` as the only class, stretch it in CSS (Pattern 2) |
| `QuestManageDmControlsTests` (**not in ROADMAP list**) | DM Controls card: `class="btn btn-secondary w-100"`, `class="btn btn-primary w-100 mt-2"`, `<button type="submit" class="btn btn-info w-100"` | `:116-118` | keep these exact class strings in the Manage conversion or rewrite to match on `href` and button role |
| `BoardMoveControlTests` (**not in ROADMAP list**) | disabled Move control: `<button type="button" class="btn btn-info w-100[^"]*disabled" aria-disabled="true" aria-describedby="move-reason"` and `<p class="move-control-reason[^"]*" id="move-reason">`; Moved toast markup | `:104-112`, `:468-506` | keep; the `.move-control-reason` relocation does not touch markup |
| `BoardMoveViewsTests` | one sheet per layout; `move-card-mobile`; no `style="`, `<script`, `text-muted`, `form-text`, `fw-bold`, `Html.Raw`; no `<span` on desktop; no `@media` in both sheets; the page copy; `class="wall-clock"` with `datetime="2031-05-10T00:00"` | `:225-237`, `:390-421`, `:318-335` | keep (the view is already clean); a merge rewrites `:225-237` to one sheet for both layouts and drops `Index.Mobile.cshtml` from `:391-392` |
| `BoardMoveStaticGuardTests`, `BoardPageStaticGuardTests` | planning-reference scan over listed files (`D-01..D-21`, "Phase 93", plan ids, document names); presence list needs >= 50 files present | `BoardPageStaticGuardTests.cs:37-44`, `:48-139`, `:155` | no GSD ids in new/edited files; deleted `.Mobile` files are skipped (`File.Exists`), count stays far above 50 |
| `QuestLogControllerIntegrationTests` | recap link `/QuestLog/EditRecap/{id}` on Details for a non-owner Admin; EditRecap 200 for Player/Admin; 404 for not-completed | `:223-300`, `:365-396`, `:476-489` | survive both merges unchanged |
| `LinkedBoardCalendarStyleTests` | `.linked-entry` dashed edge etc. in `calendar.css`; `.also-that-day--cancelled` `text-decoration: line-through` in `quests.mobile.css` | `LinkedBoardCalendarStyleTests.cs:121-133` and the `[InlineData(QuestsMobileCss, ".also-that-day--cancelled")]` theory | the rule extractor finds a selector preceded by `{`, `}`, `,` or a comment end, so the rule may sit inside `@layer pages {` but must stay a flat rule with its own block |
| `LinkedBoardStaticGuardTests` | `_Calendar` linked-partial placement (see Q5); planning-reference scan lists `quests.mobile.css` and the calendar partials | `:103-142`, `:302-324` | no change if the patch keeps the pinned strings |
| `AmbientClockSeamTests` | closed list; views `Move/Index`, `Move/Index.Mobile`, `Quest/Details`, `Details.Mobile`, `Index.Mobile`, `Manage`, `_QuestCard` must contain `ViewBag.BoardToday` where they read the board day; `_QuestCard` listed at `:49` and token map `:109`; a missing file throws | `AmbientClockSeamTests.cs:15-50`, `:100-109`, `:136-141` | on deleting `_QuestCard` remove both entries; on a Move merge remove the `.Mobile` entry; keep `ViewBag.BoardToday` in every surviving listed view; never add `DateTime.Today/Now/UtcNow` |
| `UiFoundationViewLibraryGuardTests` | `_QuestFormScripts` contains the two `~/lib/easymde-2.21.0/...` paths; no jsDelivr host in any view; every `~/lib/` reference exists | `:67-106` | keep the lib tags in the partial |

Add one new guard (suggested): a `QuestsConversionGuardTests` that, for the converted views and sheets, asserts no `style="`, no `<script>` without `src`, no `on*=`, no hex/`rgb(`/`!important` in the converted sheets, each converted page links its sheet, and (Open Question 1) every view that renders `_MarkdownEditor` or `Html.Markdown` links the matching sheet.

## Q8: Dates (every `.ToString` date rendering in scope)

`Html.WallClock` for floating local times (proposed dates, finalized session date), `Html.LocalTime` for real instants (UTC-stored). The audit and `QuestLog` already treat `FinalizedDate` as wall-clock (`QuestLog/Index.cshtml:52` `@Html.WallClock(quest.FinalizedDate.Value, "date")`) and `CreatedAt`/`ClosedDate`/`SignupTime` as instants. The helper styles are only `date`, `date-time`, `date-compact`, `date-time-compact` [VERIFIED: `HtmlHelperExtensions.cs:33-37` `["date"] = "MMM d, yyyy"`, `["date-time"] = "MMM d, yyyy, h:mm tt"`, `["date-compact"] = "MMM d"`, `["date-time-compact"] = "MMM d, h:mm tt"`], so the weekday-long formats below lose the weekday: accepted by CONTEXT ("helpers themselves are not changed").

| Site | Value | Kind | Replace with |
|------|-------|------|--------------|
| `Quest/Index.cshtml:135`, `Index.Mobile.cshtml:110` | `FinalizedDate` | wall-clock | `Html.WallClock(..., "date-time")` |
| `Quest/Details.cshtml:74, 85, 450`, `Details.Mobile.cshtml:379` | `FinalizedDate` | wall-clock | `Html.WallClock(..., "date-time")` |
| `Quest/Manage.cshtml:103, 183, 812`, `Manage.Mobile.cshtml:94, 267` | `ProposedDate.Date`, `FinalizedDate` | wall-clock | `Html.WallClock(..., "date-time")`; `812` (best date) `"date-time-compact"`; D-16: the year stays on both layouts |
| `Quest/Edit.cshtml:96`, `Edit.Mobile.cshtml:100` | existing proposed date shown in a `readonly` text **input value** | wall-clock | an `<input value>` cannot hold the helper's `<time>`; show the date as `Html.WallClock` in a `form-control-plaintext` block and keep the hidden machine field |
| `Quest/Edit:97`, `Edit.Mobile:101`, `CreateFollowUp:74`, `CreateFollowUp.Mobile:79` | `ToString("yyyy-MM-ddTHH:mm")` | form value (machine format for `datetime-local`/hidden) | **not a display**; leave as is |
| `Quest/Details.cshtml:877`, `Manage.cshtml:777`, `QuestLog/*` | `CreatedAt`, `ClosedDate`, `SignupTime` | instant | already `Html.LocalTime` |
| `Shared/_Calendar.cshtml:76, 84`, `_Calendar.Mobile.cshtml:26-27` | `ProposedDate.Date` as `HH:mm` / `dddd, MMMM d` | wall-clock | Phase 97 owns the partial; leave unless the vote patch touches the line |
| `Quest/_QuestCard.cshtml:50` | `FinalizedDate` | wall-clock | file is deleted |

Poster date: `Html.WallClock(FinalizedDate, "date-time")` emits `<time class="wall-clock" datetime="...">`; the date-time text is two lines tall at 14px in a 205px box, as budgeted above. The client hydrates it through `hydrateWallClockTimes` in `site.js`; no new JS.

## Q9: Dead partials (Q-130)

`_QuestCard.cshtml` is referenced only by `_QuestSection.cshtml:26` (`@await Html.PartialAsync("_QuestCard", quest)`); `_QuestSection.cshtml` is rendered nowhere; `QuestSectionViewModel` is used only by `_QuestSection.cshtml:3` and its own file [VERIFIED: repo-wide search of `.cs/.cshtml/.js/.css/.json`, planning docs excluded]. G1 answered **Approve the cleanup** (three files; no page changes). Test fallout: delete `AmbientClockSeamTests.cs:49` and the `["QuestBoard.Service/Views/Quest/_QuestCard.cshtml"] = "ViewBag.BoardToday"` entry at `:109` in the same commit, or the closed-list fact throws `FileNotFoundException`. The ROADMAP says the 25 file count includes the two dead partials.

## Q10: Recommended plans, gates, redraw loop

The Phase 94 wave rule applies: plans that edit tracked source never share a wave; scratch-only plans may run beside one source plan.

| Plan | Wave | Type | Contents | Gate |
|------|-----:|------|----------|------|
| 95-01 | 1 | operator checkpoint + scratch | Part A: operator checks `playwright-core` legitimacy (SUS too-new) and chooses scratch install; Part B: confirm SQL at `localhost:1433`, operator bootstraps the admin account, executor creates Player and DM through Admin and the fixture quests (crowded >= 12 open, empty board, voting, finalized with signup, long titles, Campaign board, follow-up pair, completed with and without recap) through the app's forms; rebuild `audit-shoot.mjs`, `login.mjs`, `fixtures.mjs`, `contrast.mjs`, `probes.mjs`, `diff.py`; capture BEFORE for every in-scope page, both UAs (D-22); operator copies the Direction A PNGs over | operator checkpoint (blocking) |
| 95-02 | 2 | scratch only | Gate 1 package: poster mockup (static HTML on the real art; crowded board; both information orders; three lightening strengths; the measured pool incl. Ruined 1; long titles; open/finalized/signed-up; Campaign board; empty board), phone Manage mockup, merge evidence for Edit, CreateFollowUp, Move, a one-page measurement appendix (this document's tables rebuilt from the scripts) | **Gate 1 (blocking, D-19)** |
| 95-03 | 2 (beside 95-02) | source | Slice A: `markdown-editor.js` (Tab, name, describedby), `_MarkdownEditor` help text, `quest-form.js` out of `_QuestFormScripts` (keep globals and the `form[action*="/Quest/"]` scope), `_CharacterSelectModal` script to a module and `btn-outline-danger` to filled, avatar fallback module | none |
| 95-04 | 3 | source | Slice B: Quest Details both layouts: relayout Q-111/112, Q-07..Q-19 additions, D-14/D-15, D-16 wording, vote patch in `_Calendar`/`calendar.css`, `quest-details.js`, dates, `@section Styles` links | confirm from real before/after (D-18) |
| 95-05 | 4 | source | Slice C (needs Gate 1): `QuestPosterCatalog`/`Picker` + unit tests, board view, `quests.css` poster block in `@layer pages`, WebP files, `quest-board.js` masonry out of `site.js`, phone board additions (Q-22 adventurer count line), empty states, row-nav tests | Gate 1 outcome |
| 95-06 | 5 | source | Slice D: Create/Edit/CreateFollowUp (Q-113 action row, D-17, Q-25..Q-29, merges if approved), `dm-create.mobile.css`, `quest-edit/followup.mobile.css` retired or converted | merge yes/no from Gate 1 |
| 95-07 | 6 | source | Slice E: Manage both layouts (Q-114 per the approved mockup, Q-31, Q-35, D-16 labels), `dm-manage.mobile.css`, `quest-manage.js` | Gate 1 outcome for Q-114 |
| 95-08 | 7 | source | Slice F: Quest Log Index, Details (merge), EditRecap (merge), Q-38, `empty-state`, `quest-log*.mobile.css` retired | none (merges pre-approved) |
| 95-09 | 8 | source | Slice G: Move (merge if approved, Q-39 heading), `move-entry*.css`, **`.move-control-reason` into `components.css`**, `mobile.css` rule removed | merge yes/no |
| 95-10 | 9 | source | Closure: delete dead partials and `QuestSectionViewModel`, delete the dead `legacy-pages.css` ranges (not 299-302), add foreign-consumer links and remove `quests.css`, `markdown-editor.css`, `markdown-content.css`, `move-entry.css` from both layouts' global lists, new conversion guard, planning-reference scan | none |
| 95-11 | 10 | operator checkpoint + scratch | AFTER capture both UAs, `readability-compare` against BEFORE, mobile UA UAT list, Gate 2 review sheet (every before/after pair), release prep (operator-timed, nothing new built) | **Gate 2 (blocking)** |
| 95-12 | conditional | source | Gap closure for any pair rejected at Gate 2, with re-capture | redraw loop |

- **Redraw loops (D-20):** at Gate 1 a rejected poster or Manage mockup re-opens 95-02 (re-render, re-show) before 95-05 or 95-07 start; at Gate 2 a rejected pair becomes a gap-closure plan on the owning slice plus a re-capture of that pair only. Keep 95-02 revisions in the scratch folder as numbered renders so the operator compares versions.
- **Order of linking vs removing stylesheets:** every slice adds its `@section Styles` link while the layouts still link the sheet globally (a duplicate `<link>` is harmless), and only 95-10 removes the global entries, so every page stays usable at every commit (D-08).
- 95-03 may sit in wave 2 beside the scratch plan 95-02; every other source plan has its own wave.

## Capture tooling to rebuild (scratch, outside the repo)

Nothing from Phase 94's tooling is in git (`git ls-files` lists only `build/` and `deploy/` shell scripts). Rebuild from these committed sources: `94-01-PLAN.md` (harness, login, per-role storage state, `--port`, `stateFor`), `94-02-PLAN.md` (`manifest-gen`, fixtures through the forms, `run-app`, probes, sweep, gates, `readability-compare`), `94-RESEARCH.md` "Contrast script" (reference `contrast.mjs`, reproduces 4.34, 2.99, 3.36, 1.97). Windows adaptations:
- Replace `run-app.sh`/`/proc` logic with a PowerShell or Node launcher: `dotnet run --project QuestBoard.Service --urls http://localhost:8000` (`launchSettings.json` already names 8000/8001; `.claude/launch.json` uses 8000), identify a port owner with `Get-NetTCPConnection -LocalPort 8000` instead of `ss`/`/proc`.
- Launch Edge with `chromium.launch({ channel: 'msedge' })`; desktop context 1440x900, mobile context `devices['Pixel 7']` (UA contains `Android` and `Mobile`, `isMobile`, `hasTouch`). The phone layout is chosen by UA keywords only: `["Mobi", "Android", "iPhone", "iPad", "Windows Phone", "BlackBerry"]` [VERIFIED: `MobileDetectionMiddleware.cs:5-6`]; resizing the window never selects it. Assert `document.body.classList.contains('mobile-layout')` on every mobile record.
- Authenticated pages need an active board: log in, pick the board at `/groups/pick`, save `storageState`; states are per port.
- Fixed `timezoneId` to the board zone so `time.local-time` hydration is deterministic.
- Pillow, not pixelmatch, for pixel diffs. Never write credentials under the repository.
- New probes for this phase: editor Tab probe (activeElement leaves), poster overflow probe (text box `scrollHeight <= clientHeight` per poster class), `aria-describedby` present on the CodeMirror input, per-glyph-box contrast on poster text (composite ink over the actual rendered pixels sampled from a screenshot, not over the whole rectangle), 44px target check on the phone forms and vote buttons.
- Baseline note: the current branch is the true BEFORE (D-22); also `git archive HEAD` a frozen copy on port 8001 if a stable comparison is needed while slices land.

## Security Domain

`security_enforcement` is absent from `.planning/config.json`, so it is treated as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication / V3 Session | no change | untouched; no auth page in scope |
| V4 Access Control | yes (preserve) | `Manage` stays DM-only (`ViewBag.IsAuthorized`), DM Controls card visibility and `BoardPages.Serves(...)` conditions copied unchanged; the stretched poster link goes to Manage for the DM and Details otherwise (pinned by `RowNavigationAccessibilityTests`) |
| V5 Input Validation / Output Encoding | yes | Razor encoding; stop interpolating `@player.Player.Name` into JS strings; `Html.Markdown` output untouched |
| V6 Cryptography | no | antiforgery tokens only; no new crypto |
| V13/V14 Config | yes (preserve) | no CDN host (`UiFoundationViewLibraryGuardTests`), vendor files from `wwwroot/lib` |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Player name with an apostrophe or markup breaking out of `onclick="removePlayerSignup(1, '@name')"` (HTML-decoded back to `'` before the JS parses) | Tampering | `data-player-name` read by the delegated handler; the confirm text uses `textContent`, never `innerHTML` |
| Antiforgery token placed in an inline script (`'@tokens.RequestToken'`) | Information disclosure | `<meta name="request-verification-token">` or `data-` attribute, sent as the `RequestVerificationToken` header / `__RequestVerificationToken` form field exactly as today |
| `innerHTML` with status strings in `updatePlayerAvailability()` | Tampering | the strings are static literals today; keep literals or use classes + `textContent` |
| Missing `rel`/target hijack: none (same-origin links) | — | none |
| Scratch accounts and captures with real names | Information disclosure | audit board with fantasy fixtures, credentials mode-restricted in the scratch folder, nothing in git |
| Hidden spectator vote inputs still post | Tampering | server already validates; keep the existing server-side rule |

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Inline `style=` for a per-item image | CSS modifier classes plus `image-set()` | `image-set()` Baseline Sept 2023 | no inline style, WebP with PNG fallback |
| Whole-card `onclick` navigation | One real `<a>` stretched with a pseudo-element | long established | keyboard, link preview, screen readers |
| `outline` buttons on glass | filled role buttons | Phase 94 foundation | contrast 1.2 to 3.4:1 becomes >= 4.5:1 |
| EasyMDE default `Tab` indent | `Tab: false` in `extraKeys` | CodeMirror 5 behaviour | WCAG 2.1.2 No Keyboard Trap |

**Deprecated/outdated here:** the `data-image-width/height` + `setCardHeights()` mechanism, the duplicated quest-log rules in `legacy-pages.css` (overridden by `quests.css`), `Poster3` and the character-count poster thresholds.

## Code Examples

### Minimal accessible poster markup (desktop board)
```cshtml
<article class="quest-poster quest-poster--@poster.Key" aria-label="@quest.Title">
  <a class="row-nav-link" href="@navUrl">@quest.Title</a>
  <img class="quest-poster-seal" src="@sealUrl" alt="Finalized quest" />   @* alt carries the status (no status word on the poster) *@
  <p class="quest-poster-meta">DM: @quest.DungeonMaster?.Name</p>
  <p class="quest-poster-meta">@Html.WallClock(quest.FinalizedDate.Value, "date-time")</p>
</article>
```
The heading level stays `h3` as today (`<h3 class="quest-title">` in `Index.cshtml:115`); put the link inside the heading, not the `article` label, if the audit's heading outline requires it. The poster link's accessible name must include the status (seal `alt` text is not part of the link name unless the image is inside the link): keep the seal inside the stretched link's text or add a visually hidden status phrase inside the `<a>`.

### Empty desktop board (D-09)
```cshtml
@if (!Model.Any())
{
    <h2 class="visually-hidden">No quests available</h2>
}
```
`.visually-hidden` is Bootstrap's helper in the vendor layer; `components.css` already has a visually-hidden helper in the allowed `!important` set.

### Reduced motion for the poster hover
```css
@layer pages {
  .quest-poster { transition: transform var(--qb-dur-base) var(--qb-ease); }
  .quest-poster:hover { transform: scale(1.03) rotate(0.5deg); }
  @media (prefers-reduced-motion: reduce) { .quest-poster:hover { transform: none; } }
}
```
`base.css` already carries a global reduced-motion block (the one allowed `!important` use), so the `@media` guard is belt and braces.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.400 | none needed |
| SQL Server `localhost:1433` | capture, fixtures, UAT | yes at research time (`netstat`: LISTENING, PID 3764; TCP connect succeeded) | operator-owned | stop and ask the operator (CLAUDE.md); never provision |
| Node / npm | scratch Playwright | yes | v22.16.0 / npm 11.6.2 | none needed |
| Playwright | capture | **no** | would install `playwright-core@1.65.0` into scratch | pause for the operator legitimacy check |
| Microsoft Edge | browser | yes | 156.0.4314.58, 157.0.4341.42 | `playwright-core install chromium` into a scratch path |
| Chrome | browser | no (not at the default paths) | — | Edge |
| Python + Pillow + numpy + scipy | WebP, stats, diffs | yes | 3.14.4 / 12.2.0 / 2.4.4 / 1.17.1 | none needed |
| `cwebp`, ImageMagick, fontTools | WebP, font metrics | no | — | Pillow (also read the woff2 for advance widths) |
| Dev app on 8000/8001 | capture | not running now | — | `dotnet run --project QuestBoard.Service --urls http://localhost:8000` |

**Missing with no fallback:** none. **Missing with fallback:** Playwright (install into scratch after the checkpoint), `cwebp`/ImageMagick (Pillow).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 + FluentAssertions 8.10.0; `Microsoft.AspNetCore.Mvc.Testing` for integration; EF InMemory (no database) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`) |
| Quick run command | see per-slice table (filters verified to run: 173 integration tests in 13s, 65 unit tests in under 1s after build) |
| Full suite command | `dotnet test` |
| Browser checks | scratch Playwright (not in the suite); summarised in each SUMMARY, stored outside the repo |

### Phase Requirements -> Test Map
| Contract point | Behavior | Test type | Automated command | File exists? |
|----------------|----------|-----------|-------------------|--------------|
| DoC 1 | no inline style/script/handler in converted views | static guard | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestsConversionGuardTests"` | Wave 0 gap (new) |
| DoC 2 | zero hex/rgb/`!important` in converted sheets | static guard | same | Wave 0 gap |
| DoC 3 | page links its sheet; foreign consumers link theirs | static guard | same | Wave 0 gap |
| D-05 | poster pick stable per id, fits, long titles to tall posters, never throws | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestPosterPickerTests"` | Wave 0 gap |
| D-12/D-13 | editor markup has help text with the id the script wires; partial still loads lib files | rendered HTML + guard | `--filter "FullyQualifiedName~UiFoundationViewLibraryGuardTests|FullyQualifiedName~MarkdownEditorMarkupTests"` | guard exists; markup test Wave 0 |
| D-14/D-15 | phone open-quest form posts `CharacterId` and `selectedRole`; marker `data-vote-section` present, DM Controls not matched | integration | `--filter "FullyQualifiedName~MobileViewsTests|FullyQualifiedName~QuestDetailsSignupFormTests"` | Mobile exists; new class Wave 0 |
| D-09 | empty desktop board has the visually hidden heading and no poster | integration | `--filter "FullyQualifiedName~QuestBoardEmptyStateTests"` | Wave 0 gap |
| D-21 merges | UA selection under a real phone UA | integration | `--filter "FullyQualifiedName~MobileViewsTests|FullyQualifiedName~BoardMoveViewsTests|FullyQualifiedName~QuestLogControllerIntegrationTests"` | exists, rewrite per merge |
| Dates | no raw `.ToString` date in converted views | static guard | `QuestsConversionGuardTests` | Wave 0 gap |
| Closed clock list | `ViewBag.BoardToday` still present; dead files removed from list | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests"` | exists |
| Contrast, focus, 44px, overflow, editor Tab, poster fit | rendered behaviour | scratch Playwright | `node audit-shoot.mjs --probes ...` | scratch (95-01) |

### Per-slice sampling
| Slice | Quick command (per task commit) | Wave merge |
|-------|----------------------------------|------------|
| A editor/forms/modal | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UiFoundation|FullyQualifiedName~MobileViewsTests"` | full `dotnet test` |
| B Details | `... --filter "FullyQualifiedName~MobileViewsTests|FullyQualifiedName~LinkedBoard|FullyQualifiedName~BoardMoveControlTests|FullyQualifiedName~QuestManageDmControlsTests|FullyQualifiedName~WallClockUnmovedTests"` + `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests"` | full |
| C board | `... --filter "FullyQualifiedName~RowNavigationAccessibilityTests|FullyQualifiedName~MobileViewsTests|FullyQualifiedName~BoardPage"` + unit picker tests | full |
| D forms | `... --filter "FullyQualifiedName~MobileViewsTests|FullyQualifiedName~QuestCampaignUiParityTests|FullyQualifiedName~QuestFinalizedEditTests|FullyQualifiedName~UiFoundationViewLibraryGuardTests"` | full |
| E Manage | `... --filter "FullyQualifiedName~QuestManageDmControlsTests|FullyQualifiedName~MobileViewsTests|FullyQualifiedName~BoardMoveControlTests"` | full |
| F Quest Log | `... --filter "FullyQualifiedName~QuestLogControllerIntegrationTests|FullyQualifiedName~MobileViewsTests|FullyQualifiedName~WallClockUnmovedTests"` | full |
| G Move | `... --filter "FullyQualifiedName~BoardMove|FullyQualifiedName~BoardPageStaticGuardTests"` + `AmbientClockSeamTests` | full |
| Closure/gate | `dotnet test` (full suite, needs no database), `dotnet build` 0 errors | phase gate |
Each IntegrationTests command is `dotnet test QuestBoard.IntegrationTests --filter "<expr>"`; UnitTests commands use `QuestBoard.UnitTests`. Re-run once on a transient CLR error `0x80131506`; a failure of `FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour` in the first minute of 20:00 UTC is the recorded flake. Sampling per slice also includes the scratch Playwright sweep on the touched pages, both UAs, with `readability-compare` against BEFORE (no new contrast failure, no control hidden, no new overflow or console error).

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs` — DoC 1-3 and the dates rule
- [ ] `QuestBoard.UnitTests/Helpers/QuestPosterPickerTests.cs` — D-05
- [ ] `QuestBoard.IntegrationTests/Tests/QuestDetailsSignupFormTests.cs`, `MarkdownEditorMarkupTests.cs`, `QuestBoardEmptyStateTests.cs` — D-09, D-12-D-15
- [ ] scratch tooling (95-01), outside the repo
- [ ] Framework install: none

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `playwright-core@1.65.0` drives the installed Edge via `channel: 'msedge'` on this enterprise machine | Standard Stack | If policy blocks it, fall back to a scratch Chromium download; no repo impact |
| A2 | `playwright-core` alone is enough (no `playwright` package) | Standard Stack | trivial; install the full package in scratch |
| A3 | Setting `Tab`/`Shift-Tab` to `false` in the merged `extraKeys` lets focus leave the live editor in both input styles | Q4 | The code reading is sound but not run in a browser; the plan must include the Tab probe before the slice is called done |
| A4 | The 0.64 em/char title estimate plus tiers keeps text inside every poster box | Q1 | Overflow on some titles; mitigated by the overflow probe over long-title fixtures |
| A5 | WebP quality 82 is visually acceptable on the poster art | Q2 | Operator judgement at Gate 1; raise to 90 (+40-60% size) |
| A6 | A seal fully inside the image and clear of the text box looks right (positions are computed, not seen) | Q1 | Mockup adjusts per poster; Poster5 needs a smaller seal or smaller box |
| A7 | No CSP is enforced (none in `*.cs`/`deploy/`), so inline-to-file moves are hygiene, not a fix for a blocked page | Runtime State | If a proxy sets a strict CSP, inline scripts are broken in production today; operator can confirm |
| A8 | Edit, CreateFollowUp and Move qualify as layout-only | Q6 | Judged from source diff; Gate 1 mockups/renders and a rendered diff confirm; the operator decides |
| A9 | Ruined 1 qualifies and Ruined 2-8 do not (measurement parameters 3% edge, 1.2% dilation, `L < 0.03`, 190px min width are mine) | Q1 | The pool changes with the parameters; the mockup shows the actual placement |
| A10 | Poster 1's crease (a shading line) is acceptable under text | Q1 | If not, Poster1's box is 225x199 and long titles lose their best poster |
| A11 | `playwright-core` publisher and the SUS "too-new" verdict are a false positive (as Phase 94 concluded) | Package Audit | Scratch tooling only; operator check retained |
| A12 | Apparent browser support for `image-set()` `type()` covers the group's devices | Q2 | Old browsers fall back to the plain PNG declaration |

## Open Questions

1. **Where do `markdown-editor.css` and `markdown-content.css` load for foreign consumers?**
   - What we know: ROADMAP moves both out of the layouts; 20+ non-Phase-95 views render the editor or `Html.Markdown`; no later phase scope links them.
   - What's unclear: whether Phase 95 may edit those other-phase views (one `@section Styles` line each).
   - Recommendation: add the one-line link to each foreign consumer in the closure plan with a guard test, or keep the two files global until their last consumer phase and record a deviation from DoC 3. Ask the operator at Gate 1 (cheap).
2. **Per-quest fit versus pool membership for ruined posters.** D-04 says full content; measurement says only Ruined 1 qualifies. Does the operator want a relaxed rule (short open quests on more ruined posters)?
3. **Title tiers on the poster.** 28px (large text, 3:1) never fits; 20px and 16px need the 4.5:1 lightened zone. Confirm 20/16 in the mockup.
4. **Toolbar keyboard reachability.** EasyMDE toolbar buttons stay `tabIndex = -1`. Accept (shortcuts and typing Markdown), or set `tabIndex = 0` after init (7 more tab stops per editor)?
5. **`quests.css` `--card-width` 350 vs token 352.** Accept the 2px change?
6. **Whether a reverse proxy sets a CSP** (affects how urgent the inline-script moves are, not whether to do them).
7. **Poster5 seal.** 56px seal, or a shorter text box?

## Sources

### Primary (HIGH confidence)
- Repository files read or scripted this session: `.planning/phases/95-.../95-CONTEXT.md`; ROADMAP Phase 95; `94-01-PLAN.md`, `94-02-PLAN.md`, `94-02-SUMMARY.md`, `94-RESEARCH.md`, `94-AUDIT.md` (operator answers, G1 record), `94-AUDIT-quests.md`, `94-AUDIT-characters.md` (A-characters-006), `94-VERIFICATION.md`, `deferred-items.md`; `.claude/ui-guidelines.md`, `architecture.md`; views, stylesheets, scripts and tests cited by path above.
- `gsd-tools query package-legitimacy check --ecosystem npm playwright playwright-core` (verdict SUS, too-new).
- Measurements run this session with Pillow/numpy/scipy on the 14 poster PNGs and 4 seal PNGs.
- `dotnet test` runs with the filters in the Validation section.

### Secondary (MEDIUM confidence)
- MDN `image-set()`: developer.mozilla.org/en-US/docs/Web/CSS/image/image-set (syntax, `type()`, Baseline).
- CodeMirror 5 manual `extraKeys`, `screenReaderLabel`: codemirror.net/5/doc/manual.html.
- Playwright browsers page (`channel: 'msedge'`, the replace-Edge warning): playwright.dev/docs/browsers.

### Tertiary (LOW confidence)
- None used as authority; items depending on a live browser are tagged ASSUMED.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new runtime dependency
- Architecture and inventory: HIGH, reproduced from the working tree
- Poster measurements: MEDIUM-HIGH, method is mine and parameter-sensitive; seal slots computed not seen
- Editor fix: MEDIUM-HIGH, grounded in the vendored source and docs, not yet probed live
- Pitfalls: HIGH, each traced to a file and line

**Research date:** 2026-11-10
**Valid until:** 2026-12-10 (stable; Playwright versions move weekly, re-check `npm view playwright-core version` at install)
