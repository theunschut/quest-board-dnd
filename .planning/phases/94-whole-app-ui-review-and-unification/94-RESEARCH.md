# Phase 94: Whole-App UI Review and Unification - Research

**Researched:** 2026-10-30
**Domain:** ASP.NET Core 10 MVC front end: self-hosted vendor assets, CSS cascade layers and design tokens, five Razor layouts, static guard tests, headless-browser audit tooling
**Confidence:** HIGH on mechanics (almost every mechanical claim was reproduced in this session); MEDIUM on the one visual-risk item (backdrop worst case, finding F4)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Visual direction
- **D-01: Evolve today's look; do not replace it.** The identity stays: the notice-board
  background image, parchment, gold, the Cinzel display face and the modern-card pattern.
  `senior-frontend` (Designer mode) produces 2–3 directions that are each a polished take on this
  identity. They are not alternatives to it. The operator signs one off before any production view
  or stylesheet changes. Mixing is allowed ("A's cards with B's header").
- **D-02: Mockup coverage is the core loop plus the extremes.** Mock the Quest board, Quest
  Details, a form page (Quest Create/Edit), Calendar and the Shop, each at desktop and phone width,
  with realistic content (real-length titles, a crowded area and an empty area). Mockups live in a
  scratch directory, never in the repository. Each direction also shows the current page as a
  baseline.
- **D-03: The Shop's dark-navy "Witcher 3 inspired" theme is decided from the mockups.** Mock the
  Shop both ways: (a) a distinct, deliberately different theme variant built on the same tokens and
  components, and (b) folded into the main parchment/gold look. The operator chooses at sign-off.
- **D-04: A page upgrade may restyle and re-lay out, with the same controls.** A page may reorder
  sections, merge or split cards and change its hierarchy, but every field, link, button and
  action it has today stays and keeps working. The operator turned down the wider option, so two
  things are out of bounds: changing a control's type, and collapsing sections behind a toggle. An
  action may move only as part of reordering sections. Nothing is added or removed. Every
  re-laid-out page is listed for operator sign-off with before/after screenshots.

#### Phase shape and release
- **D-05: Phase 94 covers the audit, the direction and the foundation.** Scope is exactly the five
  deliverables in the Phase Boundary. Converting area views waits for the appended phases.
- **D-06: The audit proposes the area-phase cut and the operator approves it.** Start from the
  roadmap's draft grouping (quests and quest log; events, series and calendar; characters,
  contacts and DM profile; shop; account, admin and Platform) and adjust it to what the audit
  finds. Add a **full email-template pass** as its own area phase (D-18). After approval, Phase 94
  appends the phases with `/gsd-phase` before it closes.
- **D-07: Release as usual; deployment timing is the operator's.** Each phase merges and releases
  in the normal way. Production deployment does not have to follow immediately. Waiting until all
  or most of the sweep is done is fine. The existing draft-release `deploy` approval gate is the
  mechanism, so nothing new is built for this. Note that `docker-publish.yml` still publishes the
  GHCR image on every tag push, whatever the approval says.
- **D-08: Between phases, pages must be usable but need not be pixel-perfect.** At every merge,
  every page must stay working and readable: no hidden or invisible text, no broken or unreachable
  controls, and a green test suite. That keeps an urgent, unrelated hotfix deployable mid-sweep.
  Pages not yet converted may look visibly older until their area phase lands. Do not build a
  heavy compatibility layer to keep old class names looking exactly right.

#### Third-party assets
- **D-09: Self-host every vendor asset.** Bootstrap (CSS and JS), Font Awesome and the Cinzel font
  are served from `wwwroot/lib` (or an equivalent local path) and ship inside the release zip and
  the Docker image. No CDN links remain on any layout, and no request goes to Google Fonts. SRI
  becomes moot for these files. The Docker self-hosting path must keep working with no extra setup
  step. — **Reversibility:** reversible — swapping back to CDN links is a layout-only change.
- **D-10: Drop dead dependencies once proven unused.** Remove jQuery 3.6.0 and the Font Awesome v4
  shim **only after** research proves nothing uses them: a code search across views, partials and
  `wwwroot/js`, plus a browser console check on every layout (main, mobile, GroupPicker, Platform
  desktop and mobile). The discuss-phase scout found no `$(`/`jQuery` call and no v4-style icon
  name, but that is a lead, not proof.
- **D-11: Patch- or minor-bump the libraries; no major migrations.** Move to the latest Bootstrap
  5.3.x and the latest Font Awesome 6.x. No Bootstrap 6 or Font Awesome 7 migration.

#### Inline scripts and styles
- **D-12: Inline code moves out as pages convert.** Phase 94 removes inline `style=""`, `<style>`
  and `<script>` from the layouts it rebuilds and writes the rule into `.claude/ui-guidelines.md`:
  new code carries no inline style or script. Each area phase moves its own pages' inline CSS into
  stylesheets and inline JS into files or ES modules. The Content-Security-Policy header itself is
  **not** in this sweep (see Deferred).

#### Mobile twins
- **D-13: Keep twins by default; the audit may propose merges.** Every `.Mobile.cshtml` twin stays
  and is aligned with its partner. Where the audit finds a near-identical pair with no deliberate
  mobile difference, it may propose merging it into one responsive view served under both layouts,
  following the precedent of `Events/Create`, `Events/Details`, `Events/Edit` and
  `Series/Details`, which already have no twin. **Each merge needs the operator's explicit
  approval.** The User-Agent mechanism (`MobileViewLocationExpander`) stays.
  — **Reversibility:** costly — a merged pair touches the view, both layouts' CSS loading and the
  mobile-view tests that prove the `.Mobile` view is selected under a real mobile User-Agent.
- **D-14: Twin content differences are flagged, never fixed silently.** Alignment unifies the
  look only. The audit lists every content difference between a pair (a field, action or section
  on one side only), marks the ones known to be deliberate (for example the desktop-only DM
  Controls card on Quest Manage, quick task 261019-jba), and asks the operator about each of the
  rest. Nothing is added or removed without that answer.
- **D-15: Mobile verification can use browser emulation.** Each phase lists its mobile checks in
  its UAT. Emulating a mobile browser is an acceptable verification: the in-app browser's mobile
  preset sends an Android Chrome User-Agent with touch points, so the server really selects the
  `.Mobile.cshtml` views and layout. A physical phone is optional. Real-phone checks, when wanted,
  can be run against a dev build on the LAN or deferred to a batched run on production after
  deployment, as Phase 93 did. Viewport-only resizing without a mobile User-Agent does **not**
  count, because it would render the desktop views.

#### Accessibility (operator delegated to Claude)
- **D-16: WCAG 2.2 AA plus 44px mobile targets.** Text contrast is at least 4.5:1, or 3:1 for
  large text and for control borders and focus indicators. Every interactive element has a visible
  `:focus-visible` ring. Touch targets are 44×44px on the mobile layout. These are built into the
  tokens and components in Phase 94, and each area phase brings its pages up to the bar.
- **D-17: Keep the signature colours and adjust their use or shade.** Where gold or cream fails
  contrast in a given use (for example gold body text on parchment), keep the colour in the
  palette and change how it is used (decoration, borders, large headings) or use a darker or
  lighter passing shade for text. The mockups (D-02) must already meet D-16, so the operator sees
  the effect before signing off.

#### Email templates
- **D-18: The email templates are in the sweep as their own area phase.** All `.razor` components
  under `QuestBoard.Service/Components/Emails/` (including `_EmailLayout.razor`) get a full review
  and redesign aligned with the signed-off direction. Email constraints apply: inline styles are
  required there, CSS custom properties are unavailable, and mail-client CSS support is limited.
  Phase 94 itself does not change the email templates.
- **D-19: Email verification uses a rendered preview plus a real inbox.** Each template is
  rendered to HTML with sample data and screenshotted. A test send of each goes to the operator's
  inbox, and the operator checks it in the mail clients the group actually uses. Email content,
  triggers and recipients stay exactly as they are; only presentation changes. Markdown-rendered
  sections keep their `<div>` wrapper (a `<p>` wrapper drops block content, as Phase 66 found).

### Claude's Discretion
- **The cascade strategy (operator: "leave it up to you or the researcher").** Choose how the
  foundation handles about 615 hex colour literals and about 814 `!important` declarations. The
  lean from discussion: define tokens (colour, type, spacing, radius, shadow) as custom
  properties; order vendor, base, components and page CSS with `@layer` so later rules win by
  layer rather than by `!important`; and have each area phase strip its own pages' literals and
  `!important` as it converts them. Self-hosting (D-09) makes importing Bootstrap into a layer
  straightforward. Note that unlayered CSS beats all layered CSS. The researcher may choose
  differently if the evidence says so, and must state why.
- **The `modern-card.css` element-enumerated `!important` cream colour rule**
  (`p, li, span, small { color: #F4E4BC !important; }` inside cards). It caused real bugs in Phases
  67, 80 and 91. Whether and when the foundation retires it is open. If it goes in Phase 94, D-08
  still holds: no unconverted page may end up with unreadable text.
- **How each page's stylesheet is loaded.** The desktop layout loads every page stylesheet
  globally on every page today, which is part of why rules leak between pages.
- **The self-hosting mechanism** (committed files, LibMan or similar), as long as D-09's "no extra
  setup step" for Docker holds.
- **The audit report's format and where before/after screenshots are stored**, as long as the
  operator can review them and nothing private is committed to the public repository.
- **The order of the appended area phases.**

### Deferred Ideas (OUT OF SCOPE)
- **Adopt a Content-Security-Policy header.** This is a separate security phase after the sweep
  has moved inline style and script out of the views (D-12). The sweep prepares for it but does
  not ship it.
- **Bootstrap 6, Font Awesome 7 or other major library migrations** were excluded by D-11. Revisit
  only if a future need justifies it.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are mapped to Phase 94 (ROADMAP: "Requirements: TBD"). Scope comes from CONTEXT D-01..D-19 and the approved UI-SPEC (cascade, tokens, A-1..A-11, shell contract, vendor assets, mockup protocol, audit contract, guard-test contract; assumptions A1 and A2 already approved by the operator). The table below maps each deliverable to the research that enables it, so the planner can attach must-haves.

| Deliverable (CONTEXT Phase Boundary) | Research support (this document) |
|----|----|
| 1. Page-by-page audit | Audit Tooling, Route Enumeration, Twin-comparison note, Findings F1/F10/F11, Wave 0-2 |
| 2. Signed-off direction (mockups, operator gate) | Mockup tooling, Contrast tooling, finding F4 (backdrop worst case), Plan-Shaping Guidance |
| 3. Foundation (tokens, layers, components, 5 layouts, footers, toasts, backdrop, vendor assets, dead deps) | Standard Stack, Cascade Layers, Self-hosting, D-10 proof, Guard-test impact table, Pitfalls |
| 4. Rewritten `.claude/ui-guidelines.md` | UI-SPEC section list; Don't Hand-Roll; Code Examples |
| 5. Appended area phases | Plan-Shaping Guidance (gate G2), UI-SPEC area cut |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Branching:** never commit to `main`; stay on `milestone/v9-rolling-improvements`. Push after each green wave (memory: push-during-phase-execution). Orchestrator commits this file.
- **Local DB:** SQL Server at `localhost:1433` on every platform; **never install, start, provision or tear it down**. If unreachable, stop and tell the user. Committed `appsettings.json` uses `Trusted_Connection=true`; on Linux override with `dotnet user-secrets`, never edit `appsettings.json`. Integration tests use EF InMemory and need no DB.
- **Linux .NET traps:** never set `DOTNET_GCHeapHardLimit` or other GC env vars; `Fatal error. Internal CLR error. (0x80131506)` is a flake, re-run once. If a build fails on locked files, ask the user to stop the debugger.
- **Code comments:** no GSD planning/tracking references (requirement IDs, `D-06`, phase/plan numbers, review IDs, planning document names) in source comments, XML docs, string literals, **or CSS comments**. Two existing guard tests enforce this by regex over listed files (see Guard-test table, row 6).
- **UI:** read `.claude/ui-guidelines.md` before any Razor view; modern-card pattern; button layout; `.Mobile.cshtml` twins; dates via `Html.LocalTime` / `Html.WallClock`.
- **Architecture:** Service -> Domain -> Repository; writes to `Events`/`Quests` only through the change tracker (this phase writes nothing to them); time via `IBoardClock`.
- **Navigation:** prefer RIP MCP for symbol questions (not applicable to a CSS/Razor phase; grep/Read were used here and no RIP gap was hit).
- **Project skill:** `~/.claude/skills/senior-frontend/SKILL.md` governs audit, mockups and builds (Designer, Builder, Critic). No `.claude/skills/` directory exists in the repo.

## Summary

Phase 94 is mostly executable as the UI-SPEC wrote it. The mechanics were reproduced in this session: committed vendor files under `QuestBoard.Service/wwwroot/lib/` are carried by `dotnet publish` (so by the Docker image and the release zip) and are not git-ignored; `@import url(...) layer(vendor)` works with a leading `@layer` order statement; Bootstrap's `!important` utilities in an earlier layer **do** beat unlayered legacy `!important` rules (reproduced in Chromium 156 with the real Bootstrap 5.3.8), which confirms the UI-SPEC's reason for re-theming Bootstrap through its `--bs-*` variables instead of out-specifying it; a `<link rel="preload" as="style">` for the same URL de-duplicates with the `@import` (one request, earlier start). The D-10 proofs came out clean: zero jQuery or `$` usage anywhere, no validation-script dependency, and all 1,225 `fas`/`far`/`fab` icon usages (136 distinct `fa-*` names) resolve to a glyph in Font Awesome 6.7.2 **without** the v4 shim.

The research also found things the UI-SPEC and CONTEXT do not say, and the planner should treat them as plan inputs (details in Findings and in the guard-test table): (F1) PROJECT.md's "Platform mobile layout is dead code" is **wrong**: a SuperAdmin under a mobile User-Agent really receives `_Layout.Platform.Mobile` (loads `mobile.css` only, no `modern-card.css`, no `site.css`). (F2) The guard-test list in the UI-SPEC is partly mis-described: `LinkedBoardCalendarStyleTests` does not read `modern-card.css`, `BoardMoveStaticGuardTests`/`BoardPageStaticGuardTests` are planning-reference scanners, and several tests **not** on the list will break (`BoardMoveControlTests` pins the exact toast markup; `MobileLayoutTests` pins layout words; two brand-tag regex suites). (F3) View-level CDN scripts remain after the layouts are clean (Cropper.js x10 views, Tagify x4, EasyMDE x1) and `_EmailLayout.razor` loads Google Fonts; D-09's wording covers layouts only, so this needs an operator decision. (F4) The UI-SPEC's worst-case board colour `B-max` equals the 99.9th percentile of the **390px** rendering; at 1440px raw (un-blurred, which is what a repeated card without `backdrop-filter` sits on) 0.6% of pixels are lighter and parchment-on-glass drops to about 3.7:1 there. (F5) `shoot.mjs` has no user-agent, cookie or touch option, so the audit needs a small wrapper. (F6) SQL Server at `localhost:1433` refused connections at research time, so the audit cannot start until the operator brings it up.

**Primary recommendation:** Build in this order: Wave 0 audit harness plus BEFORE baseline; Wave 1 audit reports, mockups and the zero-diff neutral changes (self-host the **same** versions first, then remove jQuery and the shim, then add the layer-order statement only) in parallel; operator sign-off gate; then tokens/base, vendor bump, components, shell layouts, legacy retirement (single high-risk plan), guard tests riding with the plan that breaks them, guidelines rewrite, final sweep, area-phase cut gate. Do **not** layer Bootstrap before sign-off: it is not appearance-neutral; measure its effect on a scratch copy for the audit's cascade section instead.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Design tokens, cascade layer order, vendor re-theme | CDN / Static (CSS under `wwwroot/css`) | Frontend Server (layouts emit the `<link>` order) | Pure CSS; layer order is decided by the first stylesheet in document order, which the Razor layout controls |
| Vendor asset hosting (Bootstrap, FA, Cinzel) | CDN / Static (`wwwroot/lib` served by `UseStaticFiles`) | Build/package (`dotnet publish`, release zip, Dockerfile) | Same-origin files; no CDN, no runtime third party |
| Mobile vs desktop layout and view selection | Frontend Server (`MobileDetectionMiddleware` + `MobileViewLocationExpander`) | — | Chosen server-side by User-Agent, not by media query; the CSS must not second-guess it |
| Skip link, landmarks, toggler names, toast roles and persistence | Frontend Server (Razor markup) | Browser (Bootstrap JS behaviour, `site.js` toast init) | Markup carries the semantics; Bootstrap JS reads `data-bs-autohide` |
| Focus ring, reduced motion, 44px targets | Browser (CSS) | — | Computed-style behaviour; verified by headless browser plus static guard tests |
| Contrast proof | Build-time scripts and static guard tests | Browser (rendered-page check) | Token pairs are static; real pages need a rendered check |
| Timestamps (`Html.LocalTime`, `Html.WallClock`) | Browser (`site.js` hydration) | Frontend Server (helpers) | Untouched by this phase; restyling must not alter markup |
| Audit screenshots and fixtures | Local scratch tooling (outside repo) | Database (operator-provisioned SQL Server) | Public repo: no screenshots or member data committed |
| Release proof (zip carries `wwwroot/lib`) | Build/package (`build/tests/package-release-smoke-test.sh`) | CI (release workflow) | The smoke test already boots the packaged app |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Bootstrap (CSS + `bootstrap.bundle.min.js`) | **5.3.8** (latest 5.3.x; `npm view bootstrap version` = `5.3.8`, 6.0.0-alpha.1 exists and is excluded by D-11) [VERIFIED: npm registry] | Grid, components, JS behaviours (toast, offcanvas, dropdown, modal, tooltip, collapse) | Already the app's framework; 5.3 exposes `--bs-*` variables for re-theming |
| Font Awesome Free | **6.7.2** (latest 6.x; 7.3.1 exists and is excluded by D-11) [VERIFIED: npm registry] | `fas fa-*` icons (1,244 uses) | Already in use; 6.x CSS carries v3/v4/v5 name aliases so the shim is not needed (proof below) |
| Cinzel (variable `wght` 400-900), `latin` + `latin-ext` woff2 | Google Fonts `v26` files; OFL 1.1 [VERIFIED: fonts.googleapis.com CSS2 API + fontTools fvar read, this session] | Display face for titles and brand | Identity font (D-01); self-hosting removes the Google Fonts request (D-09) |

### Supporting (scratch-only tooling, never added to the repository)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Playwright (npm) | 1.64.1 (Chromium headless shell 156 launches here) `[WARNING: flagged as suspicious ("too-new") by the legitimacy seam — verify before using.]` | Screenshots, console/page-error capture, computed-style probes, mobile emulation | Audit, mockup renders, zero-diff gates, D-08 sweep |
| pixelmatch | 8.0.0 `[WARNING: flagged as suspicious ("too-new") — verify before using.]` | Before/after pixel diff for the A1 zero-visible-difference gate (superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01) | Neutral-change gates and the D-08 sweep |
| pngjs | 7.0.0 (OK) | PNG decode/encode for pixelmatch (superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01; pngjs is not needed either) | Same |
| Python 3 + Pillow (WebP ok), numpy, fontTools, brotli | system | Backdrop WebP derivatives; luminance statistics; font/icon verification | Backdrop re-encode; re-measuring `B-max` |
| `scratchpad/tools/contrast.mjs` (code in Code Examples) | n/a | WCAG ratios with alpha compositing over the board | Mockup gate, A-1 proof |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Committed files under `wwwroot/lib` | LibMan or npm restore at build | Adds a setup step the Docker and zip paths do not have; rejected by D-09 and UI-SPEC |
| `@import ... layer(vendor)` | Pre-wrapping Bootstrap as `@layer vendor{...}` in a committed file | Removes the import waterfall but the vendor file is no longer byte-identical to upstream (provenance checksum lost). Preload already removes the waterfall |
| Google Fonts API one-time download of the woff2 | `@fontsource-variable/cinzel` npm package | Fontsource is a third-party repackaging; the gstatic files are the exact files Google serves today. Either is OFL; prefer the gstatic files |
| Self-host Cropper/Tagify/EasyMDE in Phase 94 | Leave to the area phases that own the views loading them | See F3; operator decision |

**Installation (executor, scratch directory outside the repository):**
```bash
mkdir -p ~/qb-ui-review/tools/pw && cd ~/qb-ui-review/tools/pw
npm init -y >/dev/null && npm install playwright pixelmatch pngjs   # superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01; install playwright only
npx playwright install chromium            # or reuse ~/.cache/ms-playwright (a headless shell is already there)
# vendor files: take them from the official npm tarballs, do not install them as dependencies
npm pack bootstrap@5.3.8 @fortawesome/fontawesome-free@6.7.2
```

**Version verification (run this session):** `npm view bootstrap version` -> `5.3.8` (modified 2026-10-08); `npm view @fortawesome/fontawesome-free versions` -> latest 6.x is `6.7.2` (7.3.1 exists); `npm view bootstrap@5.3.0 version` and `@fortawesome/fontawesome-free@6.4.0` both exist (needed for the same-version zero-diff step). [VERIFIED: npm registry]

**Exact files to commit** (all sizes and SHA-256 measured this session from the official tarballs / Google Fonts; record source URL and checksum in the vendor commit message per UI-SPEC):

| File (committed under `QuestBoard.Service/wwwroot/lib/`) | Source | Bytes | SHA-256 |
|---|---|---|---|
| `bootstrap-5.3.8/css/bootstrap.min.css` | `npm pack bootstrap@5.3.8` `package/dist/css/` | 232,111 | `d85327d99c7a3ee1f9b5d0500d1370acea3ad2db39c163c2f51f232baedbdede` |
| `bootstrap-5.3.8/js/bootstrap.bundle.min.js` | same, `dist/js/` | 80,496 | `e4fd49181388c48ec5040bd3fe66f57c29c8e67fcd8502b3354b96ec7ab47cc7` |
| `bootstrap-5.3.8/LICENSE` | same, `package/LICENSE` | 1,093 | — |
| `fontawesome-6.7.2/css/all.min.css` | `npm pack @fortawesome/fontawesome-free@6.7.2` `package/css/` | 73,890 | `74005d7c17d4a02f2f25404ec0655d9bc2fdaa53166874c87d7b7eec69d9088a` |
| `fontawesome-6.7.2/webfonts/fa-solid-900.woff2` | same, `package/webfonts/` | 158,220 | `aa75998623a391e61c6901794ace832e3ecdd288b56d608f21bea0411acc0b8e` |
| `fontawesome-6.7.2/webfonts/fa-regular-400.woff2` | same | 25,472 | `e3456d1283b9d75337a773dfd147bf908fd02c01b4bf48576d8603a69b13cbe5` |
| `fontawesome-6.7.2/webfonts/fa-brands-400.woff2` | same | 118,684 | `d7236a19bf23cbb2027280e8f51dc99d6c45976a2ed60de73382b034b18a2b68` |
| `fontawesome-6.7.2/LICENSE.txt` | same, `package/LICENSE.txt` | 7,427 | — (keep it: icons are CC BY 4.0, fonts OFL, code MIT; do not strip the attribution comment from `all.min.css`) |
| `cinzel/cinzel-latin-wght.woff2` | `https://fonts.gstatic.com/s/cinzel/v26/8vIJ7ww63mVu7gt79mT7.woff2` (rename on commit) | 25,904 | `09941fb1c169c38fd414536b37690057fc01b3117a5c63dd6571186540c8f370` |
| `cinzel/cinzel-latin-ext-wght.woff2` | `https://fonts.gstatic.com/s/cinzel/v26/8vIJ7ww63mVu7gt7-GT7LEc.woff2` (rename on commit) | 14,540 | `8fc2606f8ae6c569f444e138dba052d9a6e3f0657f9031796503bbb94eced600` |
| `cinzel/OFL.txt` | `https://raw.githubusercontent.com/google/fonts/main/ofl/cinzel/OFL.txt` | 4,383 | — |

Notes, all [VERIFIED: this session]: both Cinzel files are variable fonts, axis `wght` 400..900, default 400 (fontTools `fvar`); the Google CSS2 response gives these two `unicode-range` blocks for latin and latin-ext (copy the ranges into the local `@font-face`). `all.min.css` in 6.7.2 references only `../webfonts/*.woff2` and `*.ttf`; browsers fetch the first supported `src`, so the `.ttf` files and `fa-v4compatibility.*` are never requested by `fas`/`far`/`fab` usage and are not committed (the `FontAwesome` family declared for v4 compat is only used by the `.fa` class, which the app never uses). Bootstrap and FA `.min` files end with a `sourceMappingURL` comment; leave the bytes untouched (provenance) and accept a DevTools-only 404 for the absent map. Total committed weight is about 0.75 MB.

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| bootstrap | npm | 14+ yrs (5.3.8 published 2025-08-26) | 7.98M/wk | github.com/twbs/bootstrap | OK | Approved (files copied from the official tarball, not installed as a dependency) |
| @fortawesome/fontawesome-free | npm | 8+ yrs (6.7.2 line; 7.3.1 latest published 2026-07-15) | 3.50M/wk | github.com/FortAwesome/Font-Awesome | OK | Approved (same method) |
| @fontsource-variable/cinzel | npm | n/a | 50.4k/wk | github.com/fontsource/font-files | OK | Not used (alternative only); files come from Google Fonts directly |
| playwright | npm | n/a (1.64.1 published 2026-10-14) | 154M/wk | github.com/microsoft/playwright | SUS ("too-new") | Flagged: scratch-only tooling, never a project dependency. Planner adds a `checkpoint:human-verify` before the executor installs it |
| pixelmatch | npm | n/a (8.0.0 published 2026-10-06) | 14.4M/wk | github.com/mapbox/pixelmatch | SUS ("too-new") | Flagged: scratch-only; same checkpoint. Fallback: compare PNGs with Pillow/numpy (already present). Superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01 |
| pngjs | npm | 3+ yrs | 97.6M/wk | github.com/pngjs/pngjs | OK | Approved (scratch-only) |
| cropperjs (view-level CDN, not touched) | npm | 2.1.1 in use | 2.55M/wk | github.com/fengyuanchen/cropperjs | SUS ("too-new", informational: latest published 2026-10-16) | Not added by this phase; only relevant if F3 is decided "self-host" |
| easymde, @yaireo/tagify (view-level CDN, not touched) | npm | — | 300k / 184k per wk | Ionaru/easy-markdown-editor, yairEO/tagify | OK | Same |

**Packages removed due to [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** playwright, pixelmatch (both scratch-only, false positives on "too-new"; pixelmatch superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01), cropperjs (not added). `npm view <pkg> scripts.postinstall` returned empty for `bootstrap` and `@fortawesome/fontawesome-free` (no postinstall) [VERIFIED: npm registry]. The legitimacy seam does not cover the Google Fonts / gstatic files; they are `[VERIFIED]` by direct download from Google's own endpoint, checksummed above.

## Architecture Patterns

### System Architecture Diagram

```
 Browser request (desktop UA or mobile UA)
        |
        v
 UseStaticFiles  --serves /css, /lib, /js, /images directly (no UA logic; no Cache-Control header,
        |         ETag + Last-Modified only; asp-append-version adds ?v=<hash> to <link>/<script>)
        v
 MobileDetectionMiddleware  --UA contains Mobi|Android|iPhone|iPad|Windows Phone|BlackBerry-->  Items["IsMobile"]
        v
 MVC + Razor view engine
   |  view lookup: MobileViewLocationExpander yields  Foo.Mobile.cshtml  before  Foo.cshtml
   |  layout lookup: root _ViewStart picks _Layout.Mobile (explicit path) or "_Layout";
   |                 Platform _ViewStart names "_Layout.Platform" -- a NAME, so the same expander
   |                 resolves it to _Layout.Platform.Mobile.cshtml under a mobile UA
   |                 GroupPicker views name "_Layout.GroupPicker" -- no twin, served to everyone
   v
 HTML <head>, in this order (UI-SPEC, FIXED):
   preload(vendor css x2, cinzel latin) -> app.css -> tokens.css -> base.css -> components.css
   -> shell.css (+ shell.mobile.css on mobile layouts) -> legacy page sheets (unlayered) -> @section Styles
        |
        |  app.css:  @layer vendor, tokens, base, components, pages, utilities;
        |            @import url(../lib/bootstrap-5.3.8/css/bootstrap.min.css) layer(vendor);
        |            @import url(../lib/fontawesome-6.7.2/css/all.min.css)    layer(vendor);
        v
 Cascade resolution (author origin):
   normal declarations  : vendor < tokens < base < components < pages < utilities < UNLAYERED legacy
   !important           : vendor > tokens > ... > utilities > UNLAYERED legacy   (order inverts)
        v
 Body end: bootstrap.bundle.min.js -> site.js (toast init, tooltips, local-time hydration)
```

### Recommended Project Structure
```
QuestBoard.Service/wwwroot/
├── lib/
│   ├── bootstrap-5.3.8/{css,js}/ + LICENSE       # version in the path: @import targets are not fingerprinted
│   ├── fontawesome-6.7.2/{css,webfonts}/ + LICENSE.txt
│   └── cinzel/*.woff2 + OFL.txt
├── css/
│   ├── app.css            # layer order statement, 2 vendor @imports, Cinzel @font-face (declared once)
│   ├── tokens.css         # @layer tokens { :root{--qb-*} [data-theme]{...} }   only custom properties
│   ├── base.css           # @layer base { html/body/backdrop/headings/links/focus ring/reduced motion/--bs-* mapping }
│   ├── components.css     # @layer components { card, buttons, forms, alerts, table, badge, modal, toast, nav, empty-state }
│   ├── shell.css          # @layer components { navbar, footer, skip link, toast position }   (desktop layouts)
│   ├── shell.mobile.css   # @layer components { offcanvas, mobile bar, 44px targets }          (mobile layouts only)
│   ├── legacy-pages.css   # what is left of site.css after the globals are extracted (UNLAYERED, shrinks to nothing)
│   └── <48 existing page sheets, untouched, unlayered>
└── images/ Notice Board-blank2.jpg (source, kept), notice-board-1600.webp, notice-board-900.webp   # ~158 KB / ~62 KB measured
```

### Pattern 1: Re-theme Bootstrap through its own variables, never through selector fights
**What:** Bootstrap 5.3 colours are variable-driven for utilities and buttons (`.text-muted` -> `var(--bs-secondary-color)`, `.text-danger` -> `rgba(var(--bs-danger-rgb), var(--bs-text-opacity))`, `.btn-*` -> `--bs-btn-*`, toast -> `--bs-toast-*`, badge -> `--bs-badge-*`). Set the variables in `base`/`components` (later layers win over `vendor` for normal declarations, whatever the specificity).
**When to use:** every Bootstrap component the app uses; per-surface values (card, bare board, overlay) go on the surface class (`.modern-card { --bs-secondary-color: var(--qb-fg-muted) }`).
**Caveat (verified in `bootstrap.css` 5.3.8):** not everything is a variable. `.form-control:focus`, `.form-check-input:focus` and `.nav-link:focus-visible` hard-code `outline: 0` and `box-shadow: 0 0 0 0.25rem rgba(13, 110, 253, 0.25)`; `.btn:focus-visible` uses `--bs-btn-focus-box-shadow`. The two-tone focus ring (A-2) must therefore be a **real rule** in `base`/`components` for those selectors; no `!important` is needed because the foundation layer is later than `vendor`.
**Example:** see Code Examples.

### Pattern 2: Surfaces set colour; descendants inherit
**What:** `.modern-card { color: var(--qb-fg) }` and children inherit. Replaces the element-enumerated cream rule (`.modern-card p, li, span, small { color: #F4E4BC !important; ...}`, `modern-card.css:91-95`). Anything that must differ is a token-driven class.
**When to use:** all cards, offcanvas, toasts, dropdowns, tables.

### Pattern 3: Legacy sheets stay unlayered and win on their own selectors
**What:** the 48 existing sheets remain linked from the same places. Unlayered normal declarations beat every layered normal declaration; so the foundation can never be overridden by a rule it did not retire, and an unconverted page keeps its look. The foundation therefore **removes** the legacy global rule families from `site.css`, `modern-card.css` and `mobile.css` rather than fighting them (UI-SPEC "What the foundation retires").

### Anti-Patterns to Avoid
- **Layering Bootstrap and leaving the legacy `.text-muted { color: ... !important }` rules unconverted.** Bootstrap's layered `!important` then wins and muted text turns near-invisible on every unconverted page. Fix by setting `--bs-secondary-color` etc. on the right surfaces in the same change that layers Bootstrap.
- **`asp-append-version` on a vendor `<link rel="preload">`.** The `?v=` makes the preload URL differ from the `@import` URL and the file downloads twice. Preload without it.
- **`!important` in any layered file** (guard test pins it). **Colour literals outside `tokens.css`.**
- **Renaming `site.css` blindly:** two tests read it by name (see table).
- **Treating `html.Should().Contain("mobile.css")` as "the mobile shell is present":** any `*.mobile.css` link (for example `account.mobile.css` on the GroupPicker) satisfies it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Vendor delivery | A package-manager step or custom copy script | Committed official release files + checksum in the commit message | `dotnet publish` already ships `wwwroot`; no setup step for Docker (verified) |
| Layer-aware vendor loading | A concatenating/wrapping build step | `@import url() layer(vendor)` plus `<link rel=preload as=style>` | Native, no build step; preload de-duplicates (verified) |
| Muted/danger/primary colours | Descendant selectors with `!important` | `--bs-*` variable overrides per surface | Variables are Bootstrap's supported theming interface |
| Tints and hover shades | New hex values | `color-mix(in srgb, var(--qb-x) N%, transparent)` | One source of truth; guard test forbids literals outside `tokens.css` |
| Toast behaviour | Custom timers | Bootstrap `data-bs-autohide` / `data-bs-delay` (already initialised by `site.js`) | Pauses on hover/focus; already wired |
| Timestamp formatting | Any new formatting in restyled views | `Html.LocalTime` / `Html.WallClock` (untouched) | Project rule; markup must not change |
| Image derivatives | Installing ImageMagick/cwebp | Pillow (`Image.save(..., 'WEBP', quality=78, method=6)`; WebP support confirmed) | Already on the machine; 3000px JPEG -> 158 KB (1600w) and 62 KB (900w) measured |
| Contrast maths in prose | Eyeballing | the `contrast.mjs` script (reproduces every UI-SPEC baseline number to 2 dp) and a C# guard test with the same maths | Numbers must be reproducible |
| Screenshot diffing | Visual judgement only | pixelmatch (or Pillow/numpy; superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01) with a stated threshold, then a human look at flagged pages | A1 requires a provable "no visible difference" |

**Key insight:** the cascade fights are a symptom of three palettes and a global element rule; fixing the *model* (surface sets colour, variables carry theme) deletes the fights instead of winning them.

## Runtime State Inventory

Not a rename/refactor/migration phase. The nearest analogue is the `site.css` -> `legacy-pages.css` rename and the vendor URL change:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None. No database value references stylesheet names, CDN URLs or class names (views are server-rendered from code) | None. Verified by the fact that the change set is `wwwroot` plus layouts |
| Live service config | None for this app. The CDN URLs exist only in the five layouts and `Components/Emails/_EmailLayout.razor` (emails out of scope, D-18) | Layout edits only |
| OS-registered state | The production LXC and any browser still hold cached `site.css?v=<hash>`; the hash changes with content, so caches bust on deploy | None |
| Secrets/env vars | None | None |
| Build artifacts | `QuestBoard.Service/bin|obj` carry stale copies of `wwwroot`; `dotnet build` refreshes. Browser cache of unversioned `@import` targets is solved by the version-in-path rule | Use versioned vendor directory names; bump the directory on each upgrade |

## Common Pitfalls

### Pitfall 1: Layered `!important` inversion silently breaks muted text on unconverted pages
**What goes wrong:** after Bootstrap is imported into `vendor`, `.text-muted`, `.text-danger`, `.d-none` etc. (all `!important`) beat the unlayered legacy `!important` rules.
**Why:** CSS Cascade 5: for important declarations the layer order inverts and unlayered important loses to layered important [CITED: developer.mozilla.org/en-US/docs/Web/CSS/CSS_cascade/Cascade]. **Reproduced** here: a legacy `.text-muted.legacy-muted { color: rgb(10,20,30) !important }` computed `rgb(10, 20, 30)` with unlayered Bootstrap and `rgb(244, 228, 188)` (the value of `--bs-secondary-color` set in a `base` layer) with Bootstrap in `layer(vendor)` [VERIFIED: Chromium 156.0.8078.4, Bootstrap 5.3.8, scratch test]. Sampling `wwwroot/css` finds 45 legacy rules with an `!important` colour or background whose selector names a Bootstrap `text-*`/`bg-*` utility (29 of them `.text-muted`; the UI-SPEC says 41 and 28, method difference) [VERIFIED: grep/regex over the 48 sheets].
**How to avoid:** set `--bs-secondary-color`, `--bs-danger-rgb` etc. per surface in `base`/`components` **in the same commit** that layers Bootstrap; run the D-08 sweep.
**Warning signs:** any `.text-muted` or `.d-*` element whose computed colour/display changed between BEFORE and AFTER screenshots.

### Pitfall 2: Layering Bootstrap is not appearance-neutral, so it cannot ride the A1 pre-sign-off exception
**What goes wrong:** with `vendor` first, every legacy *normal* rule (any specificity) now beats Bootstrap, and every Bootstrap `!important` beats every legacy `!important`. Pages can change with zero foundation CSS written.
**How to avoid:** treat "Bootstrap into a layer" as a post-sign-off foundation change. For the audit's cascade section, **measure it** on a scratch copy of the app (layered vendor + no other change) and list the pages that differ. Only the `@layer` order *statement* (no imports) is neutral enough for pre-sign-off.

### Pitfall 3: Preload URL must equal the import URL
**What goes wrong:** a vendor `<link rel="preload">` with `asp-append-version` (or a different relative path) causes two downloads.
**How to avoid:** `href="~/lib/bootstrap-5.3.8/css/bootstrap.min.css"` with **no** `asp-append-version`, `as="style"`; same for FA. Verified: with a matching preload, the stylesheet is requested once and earlier (request log: `/lib/...bootstrap.min.css@8ms` before `app.css`, versus `@16ms` after it without preload) [VERIFIED: Chromium 156 scratch test]. Font preload needs `as="font" type="font/woff2" crossorigin` even though it is same-origin.

### Pitfall 4: Desktop pages must never contain the strings "offcanvas", "mobile-layout", `id="mobileNav"`
`MobileLayoutTests.cs:82-84`: `html.Should().NotContain("mobile-layout"); html.Should().NotContain("offcanvas"); html.Should().NotContain("id=\"mobileNav\"");` and `:87` `html.Should().Contain("D&D Quest Board");` (raw `&`, not `&amp;`). A rebuilt desktop layout that adopts an "offcanvas" collapse, a `mobile-layout` class, or encodes the brand ampersand fails. Also `MobileCssTests.cs:128` `html.Should().NotContain("mobile.css")` on the desktop `/`: do not link any `*.mobile.css` (including `shell.mobile.css`) or preload one from the desktop layout.

### Pitfall 5: The moved/board-switch toast markup is pinned verbatim
`BoardMoveControlTests.cs:471`: `html.Should().MatchRegex("class=\"toast show[^\"]*\" role=\"alert\" data-bs-autohide=\"false\"[^>]*>\\s*<div class=\"toast-header bg-success");`. The `_Toasts.cshtml` rebuild must keep `class="toast show ..."`, then `role="alert"`, then `data-bs-autohide="false"` in that order on the Moved toast, and its header must still begin `class="toast-header bg-success`. `CrossBoardPickerSkipTests.cs:94,112-116` pin the `board-switch-toast` class and that no `<form` or `__RequestVerificationToken` follows it when there is no previous board. If the foundation wants semantic header classes, rewrite these tests to the same intent in the same commit.

### Pitfall 6: The brand link and nav ids are parsed by regex
`BoardPageNavigationTests.cs:28-29`, `BoardPageQuestsOffTests.cs:48-49`, `BoardPageEndToEndTests.cs:39`: `@"<a\b[^>]*class=""[^""]*navbar-brand[^""]*""[^>]*>"` then an `href`. Keep the brand an `<a>` with `navbar-brand` in `class` and its `href` in the same tag; keep `id="nav-quests"` (desktop) and `id="nav-quests-mobile"` (mobile) (`BoardPageNavigationTests.cs:104,131`).

### Pitfall 7: Bootstrap's focus styles are hard-coded blue
See Pattern 1 caveat. Without a real override the app keeps three different rings (UI-SPEC C15). Add the foundation ring rule for the selector list A-2 names, and verify with a screenshot of each surface; `overflow: hidden` ancestors clip the ring (A-3), an audit item per page.

### Pitfall 8: Cache busting for `@import` targets
`asp-append-version` rewrites only the `href`/`src` of the tag it sits on (`/css/site.css?v=zPKUNXDsl_xp-m5Foic68dRapDGLeQ7xxr4CKyPKgR0` observed in rendered HTML) [VERIFIED: rendered response, scratch run]; it cannot rewrite a URL inside a CSS file. `UseStaticFiles()` sends **no `Cache-Control`** (observed: ETag and Last-Modified only), so browsers apply heuristic caching to `@import` targets. Use the version-in-directory rule (`lib/bootstrap-5.3.8/`); never edit a vendor file in place. A change to `app.css` itself busts correctly (it is linked with `asp-append-version`), but a change to `tokens.css` etc. is also fingerprinted because they are separate `<link>` tags; do not `@import` the project's own sheets.

### Pitfall 9: Cinzel `@font-face` placement and the old family request
`@font-face` is not affected by layers; declare it once in `app.css` (after the imports) with `font-display: swap`, `font-weight: 400 900` and the two `unicode-range` blocks. All existing CSS says `'Cinzel', serif`; keep the family name `Cinzel` so legacy rules keep working. Remove the Google Fonts `<link>` from all five layouts in the same commit or the page makes both requests.

### Pitfall 10: Hosting the audit
Playwright's default UA is "HeadlessChrome" and contains none of the mobile keywords, so the **desktop** views render. The mobile pass must send a UA containing `Mobi` or `Android` (for example the Pixel 7 descriptor) with `isMobile` and `hasTouch`; viewport resizing alone does not count (D-15). Note `iPad` is also treated as mobile (tablet gets the phone layout).

## Findings that Correct or Extend the UI-SPEC and CONTEXT

| # | Finding | Evidence | Planner impact |
|---|---------|----------|----------------|
| F1 | **Platform mobile layout is live, not dead.** A SuperAdmin with a mobile UA on `/Platform/Group` and `/Platform/Users` receives `_Layout.Platform.Mobile` (`platformMobileNav=True`, `mobile-layout=True`, `offcanvas=True`), with **only** `mobile.css`-family sheets: `modern-card.css=False`, `site.css=False`. Desktop UA gets `_Layout.Platform` (`modern-card.css=True`, `site.css=True`, no offcanvas). `/groups/pick` under mobile still gets `_Layout.GroupPicker` with `site.css` + `modern-card.css` (no twin layout). Mechanism: `Areas/Platform/Views/_ViewStart.cshtml:2` is `Layout = "_Layout.Platform";` (a name) and `MobileViewLocationExpander.cs:32-34` yields the `.Mobile.cshtml` candidate first, so the layout resolves through the expander | [VERIFIED: scratch integration test in a copy of the repo, output captured; `Areas/Platform/Views/_ViewStart.cshtml:2`; `ViewExpanders/MobileViewLocationExpander.cs:32,34`] | `.planning/PROJECT.md` line 217 ("appears to be dead code ... render inside the desktop layout's chrome") is stale and should be corrected in docs. The UI-SPEC's "audit confirms which layout phones receive" is answered; the audit still records it per page. Loading the same foundation files in both Platform layouts changes Platform mobile appearance (it gains `modern-card` rules); that is a before/after item |
| F2 | **Guard-test inventory is mis-described** (see the Guard-test table): `LinkedBoardCalendarStyleTests` never reads `modern-card.css` (it reads `calendar.css`, `calendar.mobile.css`, `quests.mobile.css`); `BoardMoveStaticGuardTests`/`BoardPageStaticGuardTests` scan listed files for planning references, they do not "pin menu conditions"; tests **not** on the CONTEXT list also break (`MobileLayoutTests`, `BoardMoveControlTests:471`, brand-regex suites, `LayoutNavigationTests`) | [VERIFIED: Read of each file] | Plans must carry the right rewrite list; do not budget work for tests that will not change |
| F3 | **CDN loads outside the layouts.** Views load `cropperjs@2.1.1` (10 view references), `@yaireo/tagify@4.38.0` (4 JS + 4 CSS), `easymde@2.21.0` (1 JS + 1 CSS) from jsDelivr, and `Components/Emails/_EmailLayout.razor` loads Font Awesome and Google Fonts `Cinzel:wght@400;700`. None depend on jQuery. D-09 says "no CDN links remain on any layout", which the plan satisfies, but its first sentence says "Self-host every vendor asset" | [VERIFIED: `grep` of `Views`, `Areas`, `Components`] | Operator decision (Open Question 1). Recommendation: leave view-level libraries to the area phases that own those views (Quests: EasyMDE; Characters/Contacts/DM: Cropper/Tagify), record them in the audit as `dead-code`/`a11y`-adjacent debt, and leave emails to the email phase (remote fonts in mail are normal) |
| F4 | **`B-max` is the 390px worst case, not the 1440px raw worst case.** `#794317` has relative luminance 0.0814 = the 99.9th percentile of the image resized to 390px wide (0.0802). At 1440px wide the 99.9th percentile is 0.108 (about `#835121`), 0.62% of pixels exceed 0.0814, and the maximum is 0.32 (nail highlights). A 6px box blur (text/glass-blur scale) brings the 1440 99.9th percentile down to 0.067. Parchment on glass is 4.34 at `B-max` and about **3.72** at the raw 1440 99.9th colour; gold on glass 3.36 -> 2.88. The UI-SPEC rule "no `backdrop-filter` on a repeated item" means repeated cards (quest cards, list rows) sit on the **raw** pixels, not blurred ones | [VERIFIED: Pillow/numpy on `Notice Board-blank2.jpg`: 99.9th percentile 1440px 0.1083, 390px 0.0802; `contrast.mjs`] | Ask the sign-off (S2/S7) to treat glass-without-blur as needing a scrim or an opaque paper surface for repeated items, or add a second, report-only worst case `B-hi` (~`#835121`) to the contrast script. The shipped guard test should keep `B-max` (stable number) and the executor should add the report-only line to the mockup package |
| F5 | **`shoot.mjs` cannot do the mobile or authenticated passes by itself.** Its options are `--widths --height --out --name --wait --crop --scale`; it launches plain `chromium.launch()` with `browser.newPage({viewport})`: no `userAgent`, `isMobile`, `hasTouch`, cookies or `storageState`. It does report console errors, page errors, failed requests and horizontal overflow, and grows the viewport to full page height | [VERIFIED: `~/.claude/skills/senior-frontend/scripts/shoot.mjs` read in full] | Plan a scratch wrapper (`audit-shoot.mjs`) that imports the same approach with `browser.newContext({ ...devices['Pixel 7'], storageState })`; do not edit the skill file |
| F6 | **SQL Server is not reachable now.** `bash -c '</dev/tcp/127.0.0.1/1433'` -> connection refused at research time | [VERIFIED: shell probe] | Per CLAUDE.md the executor must stop and ask the operator; Wave 0 starts with a `checkpoint:human-action` ("bring SQL Server up, give a local login per role, confirm fantasy-name fixture data") |
| F7 | **Toasts: Bootstrap's own z-index is 1090**, above navbar (1030), offcanvas (1045) and modal (1055); the container's inline `z-index: 1055` is therefore lower than a toast's own `--bs-toast-zindex`, confirming the UI-SPEC claim | [VERIFIED: `bootstrap.css` 5.3.8 `.toast { --bs-toast-zindex: 1090; ...}`; `_Toasts.cshtml:5`] | Safe to drop the inline style; replace with a class that sets `top: var(--qb-nav-h)` |
| F8 | **Error and warning toasts have no autohide attribute today** (`_Toasts.cshtml` Error and Warning `<div class="toast show" role="alert">`; Success/Info carry `data-bs-autohide="true" data-bs-delay="5000"`; gold 6000; both banners `data-bs-autohide="false"`), so Bootstrap's default (autohide, 5s) applies. Adding `data-bs-autohide="false"` to those two is a one-attribute change and `site.js` (`new bootstrap.Toast(el).show()` for every `.toast`) already honours data attributes | [VERIFIED: `_Toasts.cshtml:8,47,60,80,110`; `wwwroot/js/site.js:417-421`] | A-9 guard: attribute present on Error/Warning, absent-or-"true" on Success/Info |
| F9 | **Counts re-verified.** 814 `!important`, 615 hex literals, 1,027 `rgb(`/`rgba(`, 48 stylesheets, 133 `.cshtml` (55 `.Mobile`), 97 inline `style="`, 4 views with `<style>`, 50 `confirm(`, 31 `alert(`, 50 views with `@section Styles` all match the UI-SPEC. Inline handlers 136 (UI-SPEC 134), `<img>` count needs a multi-line parser (a single-line grep sees only 15 tags, none without `alt`) | [VERIFIED: grep/regex this session] | Audit must use a real HTML parse for `<img>`/`alt` findings |
| F10 | **There is no seed routine and no client-side validation.** Nothing in `Program.cs` seeds users or boards (migrations and Hangfire only); there is no `_ValidationScriptsPartial`, `jquery.validate` or unobtrusive script anywhere | [VERIFIED: grep] | The audit needs operator-provisioned data; jQuery has no hidden consumer |

## Self-Hosting Mechanics (Question 1)

- **Committed files, no package manager** (UI-SPEC DISCRETION, confirmed). **`dotnet publish` carries `wwwroot/lib`:** a scratch copy of the repo with sample `lib/bootstrap`, `lib/fa/webfonts/*.woff2`, `lib/cinzel/*.woff2` and `OFL.txt` published to a clean directory contained every file under `pub/wwwroot/lib/...` (plus `.br`/`.gz` siblings the SDK generates) [VERIFIED: `dotnet publish -c Release` in scratch]. `Dockerfile` publishes `QuestBoard.Service` and copies `/app/publish` (`COPY --from=publish /app/publish .`); `.dockerignore` excludes `**/bin`, `**/obj`, `**/node_modules`, `**/Dockerfile*`, `**/docker-compose*` and others but not `lib` or `woff2`. `git check-ignore -v` on `QuestBoard.Service/wwwroot/lib/bootstrap/css/bootstrap.min.css`, `.../lib/cinzel/a.woff2`, `.../lib/fa/webfonts/fa-solid-900.woff2`, `.../lib/x/OFL.txt` printed nothing (exit 1 = not ignored) [VERIFIED: shell]. Docker therefore needs no edit.
- **Release zip:** `build/package-release.sh` runs `dotnet publish QuestBoard.Service ... -o "$STAGE_DIR/app"` and zips `$STAGE_DIR` with `zip -r -q -X`, so entries are `app/wwwroot/lib/...`. `build/tests/package-release-smoke-test.sh` checks zip entries with a `for entry in release-manifest.json app/QuestBoard.Service.dll migrator/... deploy/...` loop using `grep -qxF "$entry" <<<"$ENTRIES"` and then boots the packaged app under `ASPNETCORE_ENVIRONMENT=Testing` and calls `/health`. Add (a) entries for the vendor files, **derived from `app/wwwroot/css/app.css`** (parse each `@import url(...)`; resolve relative to `/css/`) so a version bump needs no smoke-test edit, plus the three `webfonts/*.woff2`, both `cinzel/*.woff2`, both licence files; and (b) `curl` 200 checks for each URL against the running packaged app (this proves static serving from the packaged layout, not just presence in the zip). `release.yml` already runs this script (step "Check packaged release").
- **Serving:** `Program.cs:326` `app.UseStaticFiles();` precedes `app.UseMiddleware<MobileDetectionMiddleware>();` (`:327`); so vendor files are UA-independent. Observed headers for `.woff2`: `Content-Type: font/woff2`, `.css`: `text/css`, `OFL.txt`: `text/plain`, ETag and Last-Modified present, no Cache-Control [VERIFIED: in-process request through `WebApplicationFactory` in scratch].
- **Same-version first, bump second.** Bootstrap `5.3.0` and Font Awesome `6.4.0` exist as npm tarballs [VERIFIED: `npm view`]; self-hosting those exact versions (jsDelivr serves the npm package) is provably appearance-neutral and satisfies A1. The bump to 5.3.8 and 6.7.2 (D-11) is a second commit; FA 6.5-6.7 redrew some glyphs, so a visible diff there is a finding rather than a blocker, but under A1 it then waits for sign-off.
- **Cinzel:** obtain the two woff2 files from the Google Fonts CSS2 response (`https://fonts.googleapis.com/css2?family=Cinzel:wght@400..900&display=swap` with a modern User-Agent returns the two `fonts.gstatic.com/s/cinzel/v26/...woff2` URLs and their `unicode-range` values). This is a one-time developer download, not a runtime request. Preload only the latin file.

## Cascade Layers in This Stack (Question 2)

- **Browser support:** `@layer` and `@import ... layer()` shipped together in Chrome 99, Firefox 97, Safari 15.4; Baseline Widely Available since 2024-09-14 [CITED: caniuse.com/css-cascade-layers; web-platform-dx.github.io/web-features-explorer/features/cascade-layers]. The `layer()` import form being in those same versions is `[ASSUMED]` (the sources cite the feature, not the import form separately); it also worked in Chromium 156 here.
- **Placement rule:** an `@import` must precede every rule except `@charset` and `@layer` statements [CITED: developer.mozilla.org/en-US/docs/Web/CSS/@import]. `app.css` therefore starts with `@layer vendor, tokens, base, components, pages, utilities;`, then the two `@import ... layer(vendor)`, then the Cinzel `@font-face`. Reproduced working (Chromium 156). Bootstrap's leading `@charset "UTF-8";` is harmless inside an import (served as UTF-8).
- **Relative URLs:** resolve against the importing stylesheet (`/css/app.css` -> `../lib/...`). FA's `url(../webfonts/...)` resolves against the FA file, so the webfonts directory must sit beside `css/` as in the npm package.
- **Layer order is global and first-seen:** `app.css` must be the first stylesheet in document order on every layout; later `@layer foo { }` blocks in other files only add rules.
- **`!important` across layers:** see Pitfall 1 and the MDN quotation: "Important declarations found in the first layer (A) take precedence over important declarations found in layer B ... which take precedence over important declarations found outside of a layer." Normal declarations: later layer beats earlier; unlayered beats all layered. Reproduced.
- **Sampling the UI-SPEC claim:** the 41 legacy `!important` rules re-colouring a Bootstrap utility is confirmed in kind (45 by this session's looser regex: `.text-muted` 29, `.text-danger` 4, `.bg-dark` 3, `.text-warning` 3, `.text-success` 3, `.text-light` 2, `.text-white` 1). The two global offenders are `site.css:639-643` (`.text-muted { color: #2c1810 !important; font-weight: 600; text-shadow: ... !important; }`) and `modern-card.css:102` (`.modern-card .text-muted`).
- **Preload:** one request, earlier (Pitfall 3).

## D-10 Proof (Question 3)

| Proof (UI-SPEC rows 1-4) | Result this session | Evidence |
|---|---|---|
| 1. Search of views, partials, `wwwroot/js`, `Components/` for `$(`, `$.`, `jQuery`, `.ajax(`, `.on(` | **Zero hits** other than the five `<script src=".../jquery.min.js">` tags in the layouts (`_Layout.cshtml:293`, `_Layout.Mobile.cshtml:242`, `_Layout.GroupPicker.cshtml:34`, `_Layout.Platform.cshtml:51`, `_Layout.Platform.Mobile.cshtml:67`). `wwwroot/js/*.js` has `$` only inside Cropper.js v2 method names (`$toCanvas`, `$center`, `$initSelection`, `image-crop.js:48,197,198`) and a regex (`site.js:283`) | grep over `Views Areas Components wwwroot/js` |
| 2. No inline `<script>` references `$` | 55 inline script blocks in 43 files parsed; **0 lines** with a `$` identifier-style use; no jQuery-plugin call forms (`$(…)`, `jQuery(…)`, `.modal('…')` string-API) | Python regex over all `.cshtml`/`.razor` script bodies |
| 3. Console check on each of the five layouts with the script removed | **Not yet run** (needs the running app). Execute in the neutral-change plan with the Playwright wrapper: five layouts x two UAs, record `pageerror`/`console.error` and require none mention `$`/`jQuery` | Wave 1 |
| 4. Script-driven flows still work | **Not yet run.** Flows: toasts (`site.js:417-421`), tooltips (`site.js:425-427`), modals (`image-crop.js:183,315`, `Views/Events/Edit.cshtml:111`), Shop toast (`Views/Shop/Index.cshtml:528`), markdown editor (`markdown-editor.js`), image crop, date voting | Wave 1 smoke pass |

Third-party scripts loaded by views (all standalone, none needs jQuery): `cropperjs@2.1.1` (web components), `@yaireo/tagify@4.38.0`, `easymde@2.21.0` (CodeMirror bundled). `bootstrap.*` JS API call sites: `site.js`, `image-crop.js`, `Shop/Index.cshtml`, `Events/Edit.cshtml`. There is no `AddDefaultUI` in `Program.cs` (only `AddIdentity`), so Identity UI's bundled jQuery is not wired in. **Verdict: proofs 1 and 2 pass today; 3 and 4 remain execution work; if either fails, self-host jQuery rather than drop it (UI-SPEC rule).**

**Font Awesome v4 shim proof:** `grep` finds `fas` (1,244) and `fab` (2) class forms and **no** `fa fa-` form. A stronger check was run: every `(prefix, fa-name)` occurrence in `Views`, `Areas`, `Components`, `wwwroot` (`.cshtml .cs .js .css .razor`) was resolved to its codepoint in FA 6.7.2 `all.css` and tested against the `cmap` of the matching shipped webfont (`fa-solid-900.woff2`, `fa-regular-400.woff2`, `fa-brands-400.woff2` read with fontTools): **1,225 of 1,225 usages resolve to a glyph; 136 distinct `fa-*` tokens, all present in `all.css`; zero missing** [VERIFIED: Python/fontTools scratch script]. 36 names are FA5/FA4-era aliases (`fa-edit`, `fa-times`, `fa-sign-out-alt`, `fa-redo`, `fa-cog`, ...) that FA 6's own `all.css` maps, so the shim is not needed. Residual: icons built by string concatenation in JS (`'fa-' + name`) would not be caught; none were found in `wwwroot/js`.

## Guard-Test Impact (Question 4)

All twelve named files were read. "Breaks" means the assertion fails when the foundation change lands, not that the file is long.

| # | Test file | What it pins today (verbatim facts) | Foundation change that breaks it | Rewrite to the same intent |
|---|---|---|---|---|
| 1 | `Mobile/MobileCssTests.cs` | file read: `mobile.css` must contain `"min-height: 44px"` (`:80-81`) and `"font-size: 16px"` (`:96-97`); response: mobile UA `/` `html.Should().Contain("mobile.css")` (`:112`); desktop UA `/` `html.Should().NotContain("mobile.css")` (`:128`) and `html.Should().Contain("site.css")` (`:130`) | `mobile.css` globals move to `shell.mobile.css`/`components.css`; `site.css` renamed `legacy-pages.css` | Point the file tests at the file that owns the 44px rule and the 16px body size (`--qb-hit-min` form: assert the rule `.mobile-layout :where(.btn, .nav-link, ...)` exists and `min-height: var(--qb-hit-min)`; assert `--qb-hit-min: 44px` in `tokens.css`; 16px body size in `base.css`); mobile `/` links `shell.mobile.css`; desktop `/` has no `*.mobile.css` link and links `app.css` (or whichever file now carries the desktop shell) instead of `site.css`. Keep `NotContain("mobile.css")` true by never referencing a `.mobile.css` from the desktop layout |
| 2 | `Mobile/MobileViewsTests.cs` (1,367 lines) | ~40 assertions of the form `html.Should().Contain("<page>.mobile.css")` for per-page sheets, plus `NotContain` markers such as `"fantasy-quest-card"` (`:74`), `"quest-card-mobile"` (`:86`), `"table-responsive"` (`:211`), `"calendar-grid"` (`:282`), `"agenda-quest-entry"` (`:315`), `"joinPlayerFormMobile"` (`:476,491,543`), `"account-card-mobile"` (`:573`), `"purchase-history-panel"` (`:733`); `:620-663` pins that `_Layout.GroupPicker.cshtml` renders `Styles` | **None from the foundation** as long as legacy page sheets stay linked through `@section Styles` and the GroupPicker layout keeps `RenderSectionAsync("Styles", required: false)` | No rewrite in Phase 94; rewritten by the area phases that convert each page |
| 3 | `Controllers/EventsOverviewMobileStyleTests.cs` | reads `events-overview.mobile.css`: `"background: rgba(255, 255, 255, 0.15);"`, `"backdrop-filter: blur(15px);"`, no `#343a40`/`#495057`, `.avail-card .avail-count-headline {` has `"color: #FFFFFF;"`, `.avail-card .avail-count-detail {` has `"color: #FFFFFF !important;"`, file has `"min-height: 44px"` (`:128-173`) | **None** (legacy page sheet, untouched in Phase 94) | Area phase (events). Note its literals conflict with the foundation rules (literal colours, `!important`, blur on repeated card) so that phase must rewrite it to assert intent (readable count text, glass-or-paper surface, 44px target) |
| 4 | `Controllers/PlatformAreaLayoutTests.cs` | desktop `/Platform/Group` HTML `Should().Contain("modern-card.css")` (`:27`) | `modern-card.css` becomes `components.css` | Assert the Platform desktop layout links `components.css` (and `tokens.css`, `base.css`, `app.css`) |
| 5 | `Controllers/LinkedBoardsPlatformIntegrationTests.cs` | `html.Should().Contain("platform-linked-boards.mobile.css")` (`:609`), `"platform-group-card-mobile"` ("the mobile layout must be the one rendered") | **None** (page sheet via `@section Styles`) | Area phase |
| 6 | `Tests/ContactCategoryContrastGuardTests.cs` | `modern-card.css`: `ExtractCssRule(css, ".modern-card .form-text a {")` must contain `#f4e4bc` and not `#0d6efd` and not `text-decoration: none` (`:127-140`); `".modern-card .text-danger {"` must contain `#ff6b6b` and `".modern-card-header .header-subtitle {"` must contain `#1a1a1a` (`:164-178`); two facts on `contacts.mobile.css` `.category-mgmt-add-form .form-label {` (`#f4e4bc`, `text-shadow`) and `contact-form.mobile.css` `.contact-form-card .form-text a {`; two rendered-HTML structure facts | The three `modern-card.css` facts break (selectors and literals disappear under the surface-inheritance model and the no-literal rule). The `contacts.mobile.css`/`contact-form.mobile.css` facts and the HTML facts are untouched | Re-pin the Phase-80 intent against the new model: (a) the `.form-text a` rule in `components.css` sets `color: var(--qb-link)` (or the chosen token), keeps the underline; (b) `.text-danger` inside a card resolves to the danger *text* token via variable (`--bs-danger-rgb` set on the card), (c) the header subtitle token is a defined `--qb-*` ink token. Add a contrast assertion (A-1) computed from `tokens.css` for each of those tokens on its surface. `ExtractCssRule` matches `"selector {"` exactly, so a reformatted selector silently empties the rule and the test fails loudly (good) |
| 7 | `Tests/LinkedBoardCalendarStyleTests.cs` | `calendar.css`: `.linked-entry` has `border-left: 4px dashed`; `.linked-entry-detail` has `display: none`; `.linked-entry:hover .linked-entry-detail` and `.linked-entry--open:focus-visible .linked-entry-detail` have `display: block`; five `text-decoration: line-through` selectors across `calendar.css`, `calendar.mobile.css`, `quests.mobile.css` | **None**: it does not read `modern-card.css` (UI-SPEC row is wrong) | No Phase-94 rewrite |
| 8 | `Tests/LinkedBoardStaticGuardTests.cs` | partial files contain no banned tokens; **desktop linked text must be a non-`span` or a `visually-hidden` span** (`:87-112`, reason text "a visible span inside the calendar card is repainted cream by the card's !important span rule"); placement of partials; planning-reference scan over a file list incl. `calendar.css`, `quests.mobile.css`, views (`:302-324`) | The assertions keep passing, but their stated reason (the cream span rule) disappears with the rule | Keep the structural assertions (they also guard readability on unconverted pages), update only the explanatory strings; scan list needs no change |
| 9 | `Tests/BoardPageStaticGuardTests.cs` | planning-reference scanner over ~85 listed files incl. `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `platform-group.mobile.css`, `dm-profile.mobile.css`; regex set `\bD-(0[1-9]|1[0-9]|2[01])\b`, `Phase 93`, `93-0x`, and document names `CONTEXT.md|RESEARCH.md|UI-SPEC|PATTERNS.md|VALIDATION.md`; `present.Count >= 50` | A new/rebuilt layout or CSS comment that contains `D-10`, `Phase 93`, `UI-SPEC`, etc. fails; deleted files are skipped | No rewrite needed; **author foundation comments in plain language**. Consider adding the new foundation files to a Phase-94 scanner (see New Guards) |
| 10 | `Tests/BoardMoveStaticGuardTests.cs` | same scanner for Phase 92 ids over a list including `_Layout.cshtml`, `_Toasts.cshtml`, `mobile.css`, `move-entry*.css` | same as row 9 | none |
| 11 | `Tests/BoardMoveViewsTests.cs` | desktop Move page contains `"/css/move-entry.css"` and not `"move-entry.mobile.css"`; mobile the inverse; Move views contain no `style="`, `<script`, `text-muted`, `form-text`, `fw-bold`, `Html.Raw`; stylesheets have no `@media` | **None** unless the layout stops rendering `Styles` or reorders it out of the document | none |
| 12 | `Tests/CalendarSubscriptionStaticGuardTests.cs` | `site.css` must contain `.calendar-subscription-qr svg {` with `max-width: 240px` (`:234-244`) and `account.mobile.css` the same rule; `.calendar-subscription-icon-btn {`, `.calendar-subscription-row .calendar-subscription-name {` in `account.mobile.css`; planning scan (`Phase 84`, `CALFEED-\d+`, `84-0\d`) over `.cs .cshtml .css` | Renaming `site.css` -> `legacy-pages.css` breaks the `site.css` read. (The UI-SPEC says "the subscription UI facts it pins": it is the QR sizing rule on the **desktop** sheet) | Read `legacy-pages.css` for the desktop rule in the commit that renames, or move the QR rule into the file that will own it and update the path; keep both-layout intent |

**Additional existing tests that the foundation can break** (not on CONTEXT's list): `Mobile/MobileLayoutTests.cs` (Pitfall 4); `Tests/BoardMoveControlTests.cs:471` and `Controllers/CrossBoardPickerSkipTests.cs:94,112-116` (Pitfall 5); `Controllers/BoardPageNavigationTests.cs`, `BoardPageQuestsOffTests.cs`, `Tests/BoardPageEndToEndTests.cs` (Pitfall 6); `Controllers/LayoutNavigationTests.cs` (menu items by text; keep labels and conditions). `Mobile/MobileViewLocationExpanderTests.cs` is unit-level and unaffected.

**New guard tests the UI-SPEC FIXES** (implement in one new file, e.g. `Tests/UiFoundationStaticGuardTests.cs`, using the existing `FindRepositoryRoot()` pattern that looks for `*.slnx` or `.git`): (1) no `!important` in `tokens.css`, `base.css`, `components.css`, `shell*.css` except inside `@media (prefers-reduced-motion: reduce)` and the `.visually-hidden*` helpers; (2) no colour literal (`#hex`, `rgb(`, `rgba(`, `hsl(`) in those files except `tokens.css` (strip `url(...)` data URIs first; Bootstrap-style `data:image/svg+xml` arrows contain `rgba%28` and `%23`); (3) the five layouts contain no `cdn.jsdelivr.net`, `cdnjs.cloudflare.com`, `fonts.googleapis.com`, `integrity=`, `style="`, and no `<script>` without `src`; (4) skip link text `Skip to main content` first in `<body>`, `<main id="main-content" tabindex="-1">`, `aria-label` on togglers, footer GitHub link name (copy table in UI-SPEC); (5) token contrast pairs recomputed from `tokens.css` (A-1); (6) `base.css` declares the focus ring for the A-2 selector list and a `prefers-reduced-motion` block, no `outline: none|0` without replacement; (7) `_Toasts.cshtml` A-9 attributes; (8) every URL referenced under `/lib/` from the layouts and every `@import` target in `app.css` exists on disk; (9) a Phase-94 planning-reference scanner over the new files (same regex style as rows 9-10). For (5) keep contrast-bearing tokens declared as hex or `rgba(r,g,b,a)` literals (no `color-mix`/`oklch`) so a 30-line C# resolver can read them; reproduce the UI-SPEC baseline numbers as known-value unit tests of the resolver (4.34, 2.99, 3.36, 1.97, 4.94, 7.10, 4.53).

## Audit Tooling (Question 5)

**Setup.** Scratch directory outside the repository (UI-SPEC suggests `~/qb-ui-review/phase-94/{before,after}/{desktop,mobile}/{view}.png`). Playwright 1.64.1 installs cleanly with `npm install playwright` (done in this session's scratch) and Chromium 156 headless shell launches; browsers are already cached in `~/.cache/ms-playwright`. `shoot.mjs` is usable as-is for static mockup files and for unauthenticated pages; for the app it needs the wrapper (F5).

**Wrapper sketch (scratch, not committed):**
```js
// audit-shoot.mjs  (scratch)  node audit-shoot.mjs manifest.json --ua mobile|desktop --out DIR
import { chromium, devices } from 'playwright';
const mobile = { ...devices['Pixel 7'] };   // UA contains "Android" + "Mobile Safari", hasTouch, isMobile, 412x915
const desktop = { viewport: { width: 1440, height: 900 } };
const ctx = await browser.newContext({ ...(ua === 'mobile' ? mobile : desktop), storageState: `${role}.state.json`, baseURL });
// per route: goto, networkidle, grow viewport to document height, screenshot fullPage,
// collect console errors / pageerror / requestfailed, report scrollWidth > clientWidth,
// optional: page.evaluate computed-style probe for contrast (getComputedStyle color vs effective background)
```
MobileDetectionMiddleware keywords are `Mobi`, `Android`, `iPhone`, `iPad`, `Windows Phone`, `BlackBerry` (`Middleware/MobileDetectionMiddleware.cs:5-6`); the Pixel 7 descriptor matches.

**Authentication and state.** Every non-public route sits behind `[Authorize]` and `GroupSessionMiddleware` (an authenticated user needs an active group; `GroupPickerController.SelectGroup` is a POST). Playwright logs in once per role through the real `/Account/Login` form, picks the board through the GroupPicker UI, and saves `storageState`. Roles needed: Player, DungeonMaster, Admin, SuperAdmin (the Platform area and `Admin/EmailStats` are SuperAdmin; Admin pages need `AdminOnly`). Credentials for the operator's local DB are not in the repository: ask the operator (checkpoint). `appsettings.Development.json` already sets `EmailSettings:SuppressSending: true`, so audit traffic sends no mail.

**Local server.** `.claude/launch.json` defines `dotnet run --project QuestBoard.Service --urls http://localhost:8000`. The default connection string uses `Trusted_Connection=true`, which only works on Windows; on Linux the operator-provisioned DB needs `dotnet user-secrets set "ConnectionStrings:DefaultConnection" ...` (CLAUDE.md). Do not install or start SQL Server. A DB-less fallback is possible in principle (the integration `WebApplicationFactoryBase` runs the real app under `ASPNETCORE_ENVIRONMENT=Testing` with EF InMemory and a `TestAuthHandler`), but it serves through `TestServer`, not a real port; whether the .NET 10 `WebApplicationFactory` can bind Kestrel for Playwright is `[ASSUMED]` and unproven here. Treat it as plan B only.

**Enumerating the 133 views into routes.** 133 = 2 `_ViewStart`/`_ViewImports` files plus layouts/partials/shared (14 in `Views/Shared`, 2 in `Areas/Platform/Views/Shared`) plus **57 routable view pairs** (non-underscore view files that are not `.Mobile`), of which **51 have a `.Mobile.cshtml` twin** and six do not: `Admin/EmailStats`, `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details`, `Shared/AccessDenied` [VERIFIED: file inventory script]. Route rule: `Views/<Controller>/<Action>.cshtml` -> `/<Controller>/<Action>/{id?}` (default route `{controller=Home}/{action=Index}/{id?}`, `Program.cs:375-377`); Platform: `/platform/<controller>/<action>/{id?}` (`Program.cs:370-373`). Exceptions to hard-code in the manifest: `Quest/Index` -> `/quests` (`[Route("quests")]`); `GroupPicker/Index` -> `/groups/pick`; `Move/Index` is returned by the Move controller's Quest and Event actions (`/Move/Quest/{id}`, `/Move/Event/{id}`); `ContactCategoryManagement/Manage` is the controller's index action (`/ContactCategoryManagement`); `Shared/AccessDenied` is reached by an authorisation failure (a Player requesting a DM-only URL). Views with several states need data: a finalized quest (Details/Manage/Edit/CreateFollowUp), a closed finalized past quest (QuestLog/Details, EditRecap), characters, contacts, a shop with items in each category, an event and a series, users and groups with ids (Admin EditUser/ResetPassword, Platform Group Edit/Delete/Members). `Account/SetPassword` needs a valid token (screenshot via the email-preview route or skip with a note). `Admin/EmailPreviewController` renders the email templates in the browser (useful to the email phase; outside Phase 94). Build the manifest as JSON `{ id, desktopFile, mobileFile|null, path, role, fixture, states[] }` generated from the inventory so "nothing is missed" is checkable (count rows = 57 pairs + partial/layout coverage through the pages that include them).

**Screenshot hygiene.** Real member names appear on the operator's local DB pages: keep audit screenshots in the scratch tree (never in the repo, UI-SPEC). Anything delivered to the operator as a **mockup package** uses fixture data with fantasy names only. Cache nothing under `.planning/`; the audit report links by path and lists a manifest per image (route, UA class, viewport, fixture).

**What the run records per page:** screenshot, console errors, page errors, failed requests, horizontal overflow at 320 (desktop layout) and 390 (mobile), focus-ring frames on each surface, computed-colour probe for text on its effective background, and `<img>` without `alt` from a real HTML parse (F9).

## Mobile View Selection and Platform Layout (Question 6)

- **Selection:** `MobileDetectionMiddleware` sets `Items["IsMobile"]` after `UseStaticFiles` (`Program.cs:326-327`). `MobileViewLocationExpander.PopulateValues` copies it into `context.Values["isMobile"]`; `ExpandViewLocations` returns, for each location, `location.Replace(".cshtml", ".Mobile.cshtml", ...)` first and then the original (`ViewExpanders/MobileViewLocationExpander.cs:32-34`). Because the placeholder `{0}` is still in the location string, **both views and layouts referenced by name** are subject to it.
- **Root layout:** `Views/_ViewStart.cshtml:3-4` `var isMobile = Context.Items["IsMobile"] is true; Layout = isMobile ? "~/Views/Shared/_Layout.Mobile.cshtml" : "_Layout";` (explicit path on mobile, name on desktop).
- **Platform layout:** `Areas/Platform/Views/_ViewStart.cshtml:2` `Layout = "_Layout.Platform";` is a name, so phones receive `_Layout.Platform.Mobile` (F1, verified). The mobile Platform layout loads `mobile.css` and `@section Styles` only, so none of `modern-card.css` / `site.css` applies there; the Platform mobile page sheets (`platform-group.mobile.css`, `platform-users.mobile.css`, `platform-linked-boards.mobile.css`) carry their own styling.
- **GroupPicker:** `_Layout.GroupPicker.cshtml` has no twin; under a mobile UA the page gets the desktop sheets (`site.css`, `modern-card.css`) plus the mobile twin's `@section Styles` (`account.mobile.css`). It must therefore carry its own responsive rules in the `pages` layer (UI-SPEC) and is the only layout that needs `@media` for phones apart from the six twin-less pages.
- **Twin-less views** (`Events/Create`, `Details`, `Edit`, `Series/Details`, `Admin/EmailStats`, `Shared/AccessDenied`) render inside the mobile layout on phones, so their CSS cannot assume either shell.

## Contrast Tooling (Question 7)

`contrast.mjs` (full code below, Code Examples) was run against the UI-SPEC baseline and reproduced **every** published ratio to two decimals (C1 1.38, C2 4.34 and 4.75, C3 2.99, C4 3.45, C5 1.97, C6 3.36, C16 4.94, white on `#8B4513` 7.10, white on `#dc3545` 4.53, `#1a0f08` on `#ffc107` 11.55 vs the stated 11.6) [VERIFIED: scratch run]. It composites translucent layers over an opaque board colour (source-over, sRGB, then WCAG 2.x relative luminance), so it can score a token pair as "text over glass over scrim over board". Use it in three places: mockup gate (every text and control pair), the executor's `tokens.css` check, and as the reference implementation for the C# guard test.

Extra measurements for the sign-off package (this session): image luminance statistics at three scales (F4); `#F4E4BC` on glass is 5.20 at the 95th percentile, 4.75 at `B-99`, 4.34 at `B-max`, 3.72 at the 1440px raw 99.9th. Recommend the contrast report lists `B-med`, `B-99`, `B-max` (blocking) and `B-hi` (report-only).

A rendered-page check complements it (UI-SPEC backstop): in Playwright, for each text node, read `getComputedStyle(el).color` and walk ancestors to the first non-transparent background, composite over a sampled board colour; flag under 4.5 (3.0 for 28px+). The static token proof does not catch a page whose legacy sheet overrides a token.

## Plan-Shaping Guidance (Question 9)

### What can run in parallel and what is sequenced

| Stream | Needs | Blocks | Parallel with |
|---|---|---|---|
| **W0 Harness**: scratch dir, Playwright wrapper, route manifest, role logins, fixtures, contrast + diff scripts, **BEFORE** sweep of all 57 pairs x 2 UAs | operator: SQL Server up, per-role logins (F6) | everything that needs screenshots | nothing (start) |
| **A Audit reports** (one plan per area, six writers of six different files `94-AUDIT-{area}.md`, plus index `94-AUDIT.md`) | W0 | cut proposal, relayout list, twin questions | B, C |
| **B Mockups** (senior-frontend Designer, scratch only) | W0 baseline of the 5 pages, contrast script | **sign-off gate G1** | A, C |
| **C Neutral changes** (A1): C1 self-host same versions; C2 remove jQuery and the v4 shim after proofs 3-4; C3 `app.css` with the `@layer` order **statement only** | W0 BEFORE set; each step zero-diff | sign-off not required | A, B (C1 then C2 sequential: both edit the five layouts) |
| **G1 Operator sign-off** (S1-S12; `checkpoint:decision`, `autonomous: false`) | A summary, B package | all appearance changes | — |
| **D1 Tokens + base + backdrop + vendor bump + Bootstrap into `layer(vendor)` + variable mapping** | G1 | D2-D4 | — (one plan: layering and re-theme must land together, Pitfall 1) |
| **D2 components.css + `_Toasts.cshtml`**, **D3 shell.css + three desktop-side layouts + `_Footer`**, **D4 shell.mobile.css + two mobile layouts + `_Footer.Mobile`** | D1 (token names are FIXED in the UI-SPEC, so D2-D4 may start from names while D1 finalises values) | D5 | each other (disjoint files) |
| **D5 Legacy retirement**: extract globals from `site.css`/`modern-card.css`/`mobile.css`, rename the remainder `legacy-pages.css`, update link lists, retire the cream rule | D2-D4 | verification | — (single plan, highest risk, D-08 sweep inside it) |
| **E Guard tests** | ride with the plan that breaks each test (same commit) so no wave ends red; new guards in a dedicated plan after D5 | final gate | — |
| **F `.claude/ui-guidelines.md` rewrite** | D5 | — | E |
| **G Final verification + audit cascade section + area-phase cut (G2 operator approval) + `/gsd-phase` appends + phase close** | all | close | — |

`use_worktrees: true` and `parallelization: true` (`.planning/config.json`): plans in one wave must own disjoint files. The disjoint sets above respect that; the three layout plans D3/D4/(toasts in D2) share only the toast partial and footers, which are assigned to a single owner each.

### Recommended plans and waves

| Wave | Plan | Autonomous | Deliverable |
|---|---|---|---|
| 0 | 94-01 Audit harness, fixtures, BEFORE baseline, contrast and diff scripts | no (operator provides DB access and logins) | scratch tooling, manifest of 57 pairs, BEFORE screenshots, console/overflow report |
| 1 | 94-02a..f Audit per area (quests, events/calendar, characters/contacts/DM/players, shop, account/pickers, admin/platform) + 94-AUDIT index | yes | per-area reports, twin comparison, operator-question list, empty-state copy inventory, relayout list |
| 1 | 94-03 Mockups (2-3 directions x 5 pages x 2 widths + baseline + crops + switchers + contrast report + cost write-up) | no (delivery to the operator) | scratch package |
| 1 | 94-04 Self-host same versions: `wwwroot/lib/**`, five layouts, preload, font-face scaffolding, smoke test additions | yes, gated by zero-diff diff | vendor commit with source URLs and checksums |
| 2 | 94-05 Remove jQuery and the FA shim (after proofs 3-4 in the live app) + `app.css` layer-order statement | yes, gated by zero-diff | layouts without those links |
| gate | **G1 sign-off** (`checkpoint:decision`) | no | S1-S12 recorded in the UI-SPEC table |
| 3 | 94-06 D1: tokens, base, backdrop derivatives, vendor bump to 5.3.8/6.7.2, Bootstrap + FA into `layer(vendor)`, `--bs-*` mapping, reduced motion, focus ring | yes | foundation base + guard rewrites for rows 1 partial |
| 4 | 94-07 components + toasts; 94-08 desktop shell (3 layouts + footer); 94-09 mobile shell (2 layouts + footer) | yes | guard rewrites that these break (rows 1, 4, Pitfalls 4-6) |
| 5 | 94-10 Legacy retirement + `legacy-pages.css` + D-08 sweep (BEFORE/AFTER all pages, both UAs) + guard rewrites rows 6, 8, 12 | no (human review of the sweep) | pages usable, suite green |
| 6 | 94-11 New guard tests; 94-12 `ui-guidelines.md` rewrite | yes | tests and guidelines |
| 7 | 94-13 Final a11y/console/overflow sweep on the five layouts, audit cascade section, area-phase cut proposal | no | G2 gate, `/gsd-phase` appends, close |

If the planner prefers fewer plans, fold 94-11 into 94-10 and 94-12 into 94-13; do not merge D1 with D5 or C1 with C2.

### Gate rules to state in the plans
- No file under `wwwroot/css` other than the new foundation files and **no view** changes before G1, except the three A1 neutral steps, each accepted only on a zero-visible-difference diff (pixel diff with an explicit threshold, then a human look at any flagged page).
- The vendor **bump** (D-11) and **layering** are after G1.
- The cream-rule retirement is after G1 and inside D5 (A3), protected by the D-08 sweep: every page, both UAs, text readable, no control hidden, suite green.
- Operator questions (twin content, footer text, brand text, merges) are asked in batch at G1/G2; nothing is added or removed without the answer (D-14).

## Code Examples

### `app.css` (layer order, vendor imports, Cinzel) — verified working in Chromium 156
```css
/* Declares the cascade layer order once. This must be the first stylesheet on every layout. */
@layer vendor, tokens, base, components, pages, utilities;

@import url("../lib/bootstrap-5.3.8/css/bootstrap.min.css") layer(vendor);
@import url("../lib/fontawesome-6.7.2/css/all.min.css") layer(vendor);

@font-face {
  font-family: 'Cinzel';
  font-style: normal;
  font-weight: 400 900;
  font-display: swap;
  src: url("../lib/cinzel/cinzel-latin-wght.woff2") format('woff2');
  unicode-range: U+0000-00FF, U+0131, U+0152-0153, U+02BB-02BC, U+02C6, U+02DA, U+02DC, U+0304, U+0308, U+0329, U+2000-206F, U+20AC, U+2122, U+2191, U+2193, U+2212, U+2215, U+FEFF, U+FFFD;
}
@font-face {
  font-family: 'Cinzel';
  font-style: normal;
  font-weight: 400 900;
  font-display: swap;
  src: url("../lib/cinzel/cinzel-latin-ext-wght.woff2") format('woff2');
  unicode-range: U+0100-02BA, U+02BD-02C5, U+02C7-02CC, U+02CE-02D7, U+02DD-02FF, U+0304, U+0308, U+0329, U+1D00-1DBF, U+1E00-1E9F, U+1EF2-1EFF, U+2020, U+20A0-20AB, U+20AD-20C4, U+2113, U+2C60-2C7F, U+A720-A7FF;
}
```
(The `unicode-range` values are the ones the Google CSS2 API returned this session. Fix the file names to whatever the vendor commit uses.)

### Layout head (Razor)
```cshtml
<link rel="preload" href="~/lib/bootstrap-5.3.8/css/bootstrap.min.css" as="style" />
<link rel="preload" href="~/lib/fontawesome-6.7.2/css/all.min.css" as="style" />
<link rel="preload" href="~/lib/cinzel/cinzel-latin-wght.woff2" as="font" type="font/woff2" crossorigin />
<link rel="stylesheet" href="~/css/app.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/tokens.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/base.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/components.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/shell.css" asp-append-version="true" />
@* ...legacy page sheets, then: *@
@await RenderSectionAsync("Styles", required: false)
```
No `asp-append-version` on the three preloads (Pitfall 3). Add the `Styles` section to the desktop and Platform desktop layouts (today only the mobile, GroupPicker and Platform mobile layouts render it).

### Variable-based re-theme (layered, no `!important`)
```css
@layer base {
  :root {
    --bs-body-font-family: var(--qb-font-body);
    --bs-secondary-color: var(--qb-fg-on-board-muted);   /* .text-muted on bare board */
    --bs-border-radius: var(--qb-radius-md);
    --bs-headings-font-weight: var(--qb-weight-strong);
  }
}
@layer components {
  .modern-card {
    color: var(--qb-fg);
    --bs-secondary-color: var(--qb-fg-muted);            /* .text-muted inside a card */
    --bs-danger-rgb: var(--qb-danger-text-rgb);          /* .text-danger inside a card */
  }
  /* Two-tone focus ring: replaces Bootstrap's hard-coded blue box-shadow. */
  :where(a, button, input, select, textarea, summary, [tabindex], .btn, .nav-link, .dropdown-item,
         .page-link, .btn-close, .form-check-input, .row-nav-link):focus-visible {
    outline: var(--qb-focus-width) solid var(--qb-focus-outer);
    outline-offset: var(--qb-focus-offset);
    box-shadow: 0 0 0 calc(var(--qb-focus-offset) + var(--qb-focus-width)) var(--qb-focus-inner);
  }
}
```
(The `--qb-danger-text-rgb` form is needed because Bootstrap's `.text-danger` reads an `rgb` triple; the UI-SPEC token list gives `--qb-{name}-text`, so either add the `-rgb` triple tokens or set `color` on the surface's `.text-danger` through the component layer. The ring recipe is a starting point: check both strokes against each surface with the contrast script and look at a frame, since `box-shadow` and `outline` interact with `overflow` clipping.)

### Contrast script (reference implementation, reproduces the UI-SPEC baseline)
```js
// contrast.mjs  (scratch)
export const parse = (c) => {
  c = c.trim().toLowerCase();
  let m = c.match(/^#([0-9a-f]{3,8})$/);
  if (m) { let h = m[1]; if (h.length <= 4) h = [...h].map(x => x + x).join('');
    return { r: parseInt(h.slice(0,2),16), g: parseInt(h.slice(2,4),16), b: parseInt(h.slice(4,6),16),
             a: h.length === 8 ? parseInt(h.slice(6,8),16) / 255 : 1 }; }
  m = c.match(/^rgba?\(\s*([\d.]+)[ ,]+([\d.]+)[ ,]+([\d.]+)(?:\s*[,/]\s*([\d.]+%?))?\s*\)$/);
  if (m) { const a = m[4] === undefined ? 1 : m[4].endsWith('%') ? parseFloat(m[4]) / 100 : parseFloat(m[4]);
    return { r: +m[1], g: +m[2], b: +m[3], a }; }
  throw new Error('unparsed colour: ' + c);
};
export const over = (fg, bg) => ({ r: fg.r*fg.a + bg.r*(1-fg.a), g: fg.g*fg.a + bg.g*(1-fg.a), b: fg.b*fg.a + bg.b*(1-fg.a), a: 1 });
const lin = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
export const lum = ({ r, g, b }) => 0.2126*lin(r) + 0.7152*lin(g) + 0.0722*lin(b);
export const ratio = (a, b) => { const l1 = lum(a), l2 = lum(b); return (Math.max(l1,l2)+0.05) / (Math.min(l1,l2)+0.05); };
export const textOn = (text, layers, board) => {            // layers painted bottom-up over an opaque board colour
  const surface = layers.reduce((under, l) => over(parse(l), under), parse(board));
  return ratio(over(parse(text), surface), surface);
};
// textOn('#F4E4BC', ['rgba(255,255,255,0.15)'], '#794317') -> 4.34 ;  textOn('#ffc107', [...], '#794317') -> 3.36
```

### Cropping the board image to WebP (Pillow)
```python
from PIL import Image
im = Image.open('QuestBoard.Service/wwwroot/images/Notice Board-blank2.jpg').convert('RGB')
for w in (1600, 900):
    im.resize((w, int(2000*w/3000)), Image.LANCZOS).save(f'notice-board-{w}.webp', 'WEBP', quality=78, method=6)
# measured: 1600 -> 158 KB, 900 -> 62 KB (source JPEG 2.5 MB). Re-measure contrast after the encode.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Specificity wars and `!important` to beat a framework | `@layer` plus theming through the framework's own custom properties | CSS Cascade Layers Baseline widely available 2024-09-14 | Foundation needs no `!important`; the one trap is the inverted order for the framework's own `!important` utilities |
| CDN with SRI for Bootstrap/FA | Self-hosted, versioned directory, same origin | D-09 | No third-party request at page load; SRI moot |
| Font Awesome v4 shim for renamed icons | FA 6 `all.css` carries name aliases | FA 6.0 | Shim removable (proven for this codebase) |
| Single `focus` glow in framework colours | Two-tone `:focus-visible` ring, WCAG 2.2 (2.4.11 focus not obscured, 2.5.8 target size) | WCAG 2.2 | `scroll-padding-top` for the fixed navbar; 24px desktop / 44px mobile targets |

**Deprecated/outdated here:** Bootstrap 5.3.0 and Font Awesome 6.4.0 (CDN, no SRI), jQuery 3.6.0 (no consumer), the FA v4 shim, the Google Fonts request. Bootstrap 6.0.0-alpha.1 and Font Awesome 7.x exist and are excluded by D-11.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `@import url() layer()` shipped in the same browser versions as `@layer` (Chrome 99 / Firefox 97 / Safari 15.4); sources cite the feature, not the import form separately. It worked in Chromium 156 here | Cascade Layers | Very old browsers would ignore the vendor import and render unstyled; very low for this group's devices, but not verified on Safari/Firefox in this session |
| A2 | `UseStaticFiles` behaviour for `.woff2` and heuristic caching holds the same under Kestrel in production as under `TestServer` | Self-Hosting | Low; same middleware |
| A3 | Whether the .NET 10 `WebApplicationFactory` can host a real Kestrel port for Playwright (plan B when SQL Server is down) | Audit Tooling | Plan B may not exist; plan A needs the operator's DB |
| A4 | Icons assembled by string concatenation in JS (e.g. `'fa-' + name`) do not exist | D-10 proof | A missing glyph (empty box) after the shim goes; none found in `wwwroot/js`, but the console/visual pass should look |
| A5 | The 2px two-tone ring recipe in Code Examples produces the intended visual geometry (gap, inner band, outer band) | Code Examples | Cosmetic; verify with the contrast script and a screenshot per surface before it is pinned by tests |
| A6 | Bootstrap's `bootstrap.min.css` and `all.min.css` can stay byte-identical with a trailing `sourceMappingURL` comment and a 404 for the map (DevTools only) | Standard Stack | Cosmetic console noise only when DevTools is open |
| A7 | `iPad` being treated as mobile (keyword list) means tablets get the phone layout; unchanged by this phase | Pitfall 10 | Informational |
| A8 | Playwright and pixelmatch `SUS` verdicts are "too-new" false positives (very high download counts, official repos; pixelmatch superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01) | Package Audit | Scratch tooling only; a human check before installing is already required |

## Open Questions (RESOLVED)

All five were resolved after research (operator, 2026-10-30) or are routed to a named gate:

- Q1: **RESOLVED by D-20.** Cropper.js, Tagify and EasyMDE are self-hosted in Phase 94 (plan 94-12); the email layout's remote fonts stay with the email area phase (D-18, appended by 94-27).
- Q2: **ROUTED to G1** (plan 94-17) as an input to sign-off item S2, with the report-only `B-hi` numbers in the mockup package (94-02, 94-09, 94-10, 94-15, 94-16).
- Q3: **RESOLVED by D-22.** Rename to `legacy-pages.css` in Phase 94 (plan 94-23), readers updated in the same commit.
- Q4: **RESOLVED by D-23** (as amended). Local test accounts per role, bootstrapped at the 94-01 checkpoint.
- Q5: **RESOLVED by D-21 and plan 94-19 Task 1.** Same-version self-hosting first; the bump lands as its own commit after sign-off, and its icon diffs are reviewed there.

Original questions, for the record:

1. **View-level and email CDN loads (F3).**
   - What we know: layouts will be CDN-free; views load Cropper.js (10), Tagify (4+4), EasyMDE (1+1) from jsDelivr, no SRI; `_EmailLayout.razor` loads Font Awesome and Google Fonts.
   - What's unclear: whether D-09's first sentence ("every vendor asset") is meant to cover those.
   - Recommendation: keep Phase 94 to the layouts; record the three libraries as audit findings owned by the Quests (EasyMDE) and Characters/Contacts/DM (Cropper, Tagify) area phases, where the views and CSS are converted anyway; leave email remote fonts to the email phase. Ask the operator once at G1.
2. **Backdrop worst case (F4).** Is glass-without-blur on repeated cards acceptable at the raw 1440px worst case (parchment about 3.7:1 at the lightest 0.1% of pixels), or should repeated items get a scrim/paper surface? Recommendation: decide at S2 with the `B-hi` numbers in the mockup package.
3. **`site.css` rename and its two readers.** The UI-SPEC renames the remainder to `legacy-pages.css`; two tests and the desktop `MobileCssTests` assertion read `site.css` by name. Recommendation: rename in the retirement commit and update the readers in that commit; alternatively keep the name `site.css` until the last area phase. Either is fine if tests move with it.
4. **Audit data and logins.** Per-role local logins and fixture data are not in the repository. Needs the operator at W0 (F6, F10).
5. **FA 6.5-6.7 glyph redraws.** Some icons changed shape between 6.4 and 6.7; the same-version-first step avoids blocking A1, but the bump commit may show icon diffs that become findings.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests, publish | yes | 10.0.114 | — |
| SQL Server `localhost:1433` | live app for audit, mockup baseline from real pages, D-10 proof 3-4, D-08 sweep | **no (connection refused at research time)** | — | Stop and ask the operator (CLAUDE.md). Plan B (unproven): InMemory host |
| Node / npm | Playwright, `npm pack` of vendor files | yes | node v24.19.0 | — |
| Playwright + Chromium | screenshots, console checks, computed-style probes | yes (installed in scratch; headless shell 156.0.8078.4 launched) | 1.64.1 | Any other headless Chromium |
| Python 3 with Pillow (WebP), numpy, fontTools, brotli | WebP derivatives, luminance stats, icon/glyph proof | yes | system | — |
| `cwebp`, ImageMagick | WebP encoding | no | — | Pillow (above) |
| Docker | optional image-path check | yes | 29.9.2 | Publish check already proves `wwwroot/lib` ships |
| `zip`, `unzip`, `curl`, `git` | release smoke test | yes | — | — |
| Network (jsDelivr/npm/Google Fonts/GitHub raw) | one-time vendor downloads | yes | — | — |

**Missing dependencies with no fallback:** SQL Server for the audit and sweeps (operator action).
**Missing dependencies with fallback:** `cwebp`/ImageMagick (Pillow).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 `3.2.2` with `xunit.runner.visualstudio 3.1.5`, FluentAssertions `8.10.0`, `Microsoft.AspNetCore.Mvc.Testing 10.0.9`, EF Core InMemory 10.0.9 [VERIFIED: csproj] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`, `parallelizeTestCollections: false`) |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MobileCssTests\|FullyQualifiedName~MobileLayoutTests\|FullyQualifiedName~PlatformAreaLayoutTests\|FullyQualifiedName~ContactCategoryContrastGuardTests\|FullyQualifiedName~CalendarSubscriptionStaticGuardTests\|FullyQualifiedName~UiFoundationStaticGuardTests"` (the `--filter "FullyQualifiedName~..."` form ran green in this session) |
| Full suite command | `dotnet test` (needs no database: InMemory) |
| Browser checks | scratch Playwright wrapper (not in the suite); results summarised in the plan SUMMARY and stored outside the repo |

### Phase Requirements -> Test Map
| Must-have (source) | Behavior | Test Type | Automated Command | File Exists? |
|---|---|---|---|---|
| D-09 no CDN, no Google Fonts on layouts | five layouts contain no CDN host, no `integrity=`, no `style="`, no bare `<script>` | static guard | `dotnet test --filter "FullyQualifiedName~UiFoundationStaticGuardTests"` | Wave 0 gap (new file) |
| D-09 vendor files ship | every `@import`/`/lib/` URL exists on disk; zip contains them; packaged app serves them 200 | static guard + shell | same filter; `bash build/tests/package-release-smoke-test.sh <zip>` | guard: Wave 0 gap; smoke test: extend |
| D-10 jQuery/shim removal | no `jquery`, `v4-shims`, `jquery.min.js` in any layout; zero console errors on five layouts x two UAs | static guard + browser | filter above; Playwright wrapper | guard: gap; browser: scratch |
| D-16/A-1 contrast | every declared token pair >= 4.5 (3.0 large/non-text) composited over `B-max` | static guard (C# resolver) | filter above | gap |
| A-2 focus ring / A-7 reduced motion | `base.css` declares ring for the selector list and a `prefers-reduced-motion` block; no bare `outline: none/0` | static guard | filter above | gap |
| A-5 44px mobile targets | `.mobile-layout :where(...)` rule with `min-height: var(--qb-hit-min)`; `--qb-hit-min: 44px` | static guard (rewrites `MobileCssTests` rows) | `--filter "FullyQualifiedName~MobileCssTests"` | exists, rewrite |
| A-8 landmarks, skip link, names | each layout: skip link first, `<main id="main-content" tabindex="-1">`, toggler `aria-label` | static guard / rendered HTML | filter above | gap |
| A-9 toast persistence | Error and Warning toasts carry `data-bs-autohide="false"`; Success/Info do not carry `"false"`; Moved toast regex intact | static guard + existing test | `--filter "FullyQualifiedName~BoardMoveControlTests\|FullyQualifiedName~UiFoundationStaticGuardTests"` | exists + gap |
| Cascade rules (layered files) | no `!important` (except allowed), no colour literal outside `tokens.css` | static guard | filter above | gap |
| D-08 pages usable | every page both UAs: text readable, no control hidden, suite green | browser sweep + `dotnet test` | Playwright BEFORE/AFTER + pixel diff + human review | scratch |
| A1 neutral changes | zero visible difference, five mockup pages x five layouts | browser diff | Playwright + pixelmatch with stated threshold (superseded: pixel diffs use Pillow + numpy; do not install pixelmatch — see plan 94-01) | scratch |
| D-12 inline code gone from rebuilt layouts | covered by the layout guard | static guard | filter above | gap |
| Mobile selection intact (D-13/D-15) | `.Mobile` view and layout under a mobile UA | existing integration | `--filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~MobileLayoutTests"` | exists |
| Planning references absent from new files | regex scan of new foundation files | static guard | filter above | gap |
| Build/test health | no regression | full suite | `dotnet test` | exists |

### Sampling Rate
- **Per task commit:** the quick filter above (seconds once built).
- **Per wave merge:** `dotnet test` full suite, plus the scratch Playwright sweep for any wave that touched CSS or layouts.
- **Phase gate:** full suite green, D-08 sweep reviewed, contrast report on the final `tokens.css`, console check on five layouts, before `/gsd-verify-work`.
- Remember CLAUDE.md: re-run once on `0x80131506`; never set GC env vars.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/UiFoundationStaticGuardTests.cs` — covers the new guards (layout hygiene, layered-file rules, token contrast, focus ring, reduced motion, toast attributes, `/lib` existence, planning-reference scan)
- [ ] a small WCAG helper (private static in that file) with known-value tests (4.34, 2.99, 3.36, 1.97, 4.94, 7.10, 4.53)
- [ ] `build/tests/package-release-smoke-test.sh` additions (vendor entries derived from `app.css`, curl 200s)
- [ ] scratch tooling (`audit-shoot.mjs`, manifest generator, `contrast.mjs`, diff script) — outside the repo, recreated by plan 94-01 from this document
- [ ] Framework install: none (xUnit/FluentAssertions already referenced)

## Security Domain

`security_enforcement` is absent from `.planning/config.json`, so it is treated as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1/V14 Architecture, Configuration (supply chain) | **yes** | Self-host vendor files from official releases with a recorded checksum; remove un-SRI'd CDN scripts (jQuery 3.6.0, Bootstrap, FA CSS had no SRI) |
| V2 Authentication / V3 Session | no change | Layout changes keep `Logout` POST forms with antiforgery (`asp-controller`/`asp-action`) |
| V4 Access Control | no change | Menu conditions (`AuthorizationService`, `BoardPages.Allows/Serves`) must be copied unchanged; tests pin them |
| V5 Input Validation / Output Encoding | yes (do not regress) | Razor encodes `@TempData[...]` and `@currentUser.Name`; keep `text-break` and no `Html.Raw` in layouts/toasts |
| V6 Cryptography | no | n/a |
| V12/V13 Files and resources | yes | Static files only from `wwwroot`; licence files shipped |
| V14.4 HTTP security headers | deferred | CSP is explicitly out of the sweep (Deferred Ideas); preparing for it by removing inline script/style from the rebuilt layouts |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Third-party CDN compromise or tampering (jQuery, Bootstrap, FA served without SRI) | Tampering | Self-host (D-09); checksums in the vendor commit; view-level CDN libs remain until their area phases (F3, no SRI today) |
| Tabnabbing via `target="_blank"` (footer GitHub link) | Information disclosure | `rel="noopener noreferrer"` (UI-SPEC) |
| Stored XSS through toast text or user display name in navigation | Tampering | Razor auto-encoding; no `Html.Raw`; keep toast markup as is |
| Open redirect via toast "Open it on" link | Tampering | existing `Url.IsLocalUrl(movedEntryUrl)` check in `_Toasts.cshtml` must survive the rebuild |
| Silent behaviour change hiding an error toast (A2) | Repudiation (UX) | error and warning toasts persist until dismissed (approved departure) |
| Typosquatted/hallucinated package | Spoofing | packages `[VERIFIED]` through the legitimacy seam; vendor files taken from official tarballs; scratch tools flagged `SUS` need a human check |

## Sources

### Primary (HIGH confidence)
- Repository files read this session: `94-CONTEXT.md`, `94-UI-SPEC.md`, `.planning/config.json`, `ROADMAP.md` (Phase 94 section), `CLAUDE.md` and `.claude/*.md`, `.claude/launch.json`, `QuestBoard.Service/Program.cs`, `Middleware/MobileDetectionMiddleware.cs`, `ViewExpanders/MobileViewLocationExpander.cs`, the five layouts, both footers, `_Toasts.cshtml`, `wwwroot/js/site.js`, `wwwroot/css/site.css`, `modern-card.css`, `mobile.css`, `Dockerfile`, `.dockerignore`, `.gitignore`, `build/package-release.sh`, `build/tests/package-release-smoke-test.sh`, `.github/workflows/release.yml`, the twelve named guard tests and the layout/navigation/toast tests listed above
- `~/.claude/skills/senior-frontend/SKILL.md`, `scripts/shoot.mjs`, `references/critique-checklist.md`
- Official npm tarballs `bootstrap@5.3.8` and `@fortawesome/fontawesome-free@6.7.2` (files, checksums, `bootstrap.css` rule bodies, `metadata/icons.yml`)
- Google Fonts CSS2 API and `fonts.gstatic.com/s/cinzel/v26/...woff2`; `github.com/google/fonts` `ofl/cinzel/OFL.txt`
- Scratch experiments run this session: (1) scratch copy of the repo + integration test probe of Platform/GroupPicker layouts under desktop and mobile UAs; (2) `dotnet publish` of the copy with sample `wwwroot/lib` files; (3) in-process requests for `/lib/...` and `/css/site.css`; (4) Chromium 156 layer/`!important` and preload tests; (5) Pillow/numpy luminance and WebP tests; (6) fontTools glyph proof for all `fa-*` usages; (7) `contrast.mjs` baseline reproduction

### Secondary (MEDIUM confidence)
- [MDN: CSS cascade (cascade layers and `!important` order)](https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_cascade/Cascade)
- [MDN: `@import` (placement, `layer()` syntax)](https://developer.mozilla.org/en-US/docs/Web/CSS/@import)
- [Can I use: CSS Cascade Layers](https://caniuse.com/css-cascade-layers) and [Web Platform Features Explorer: cascade layers](https://web-platform-dx.github.io/web-features-explorer/features/cascade-layers/)

### Tertiary (LOW confidence)
- `.NET 10 WebApplicationFactory` Kestrel binding for plan B: unverified (A3)

## Metadata

**Confidence breakdown:**
- Standard stack and self-hosting mechanics: HIGH — versions, files, checksums, publish output, static-file headers all reproduced
- Cascade-layer semantics: HIGH — spec text plus a Chromium 156 reproduction with the real Bootstrap
- D-10 proof: HIGH for static proofs 1-2 and the FA glyph proof; proofs 3-4 are execution work (browser)
- Guard-test impact: HIGH — every file was read; the rewrite recipes are MEDIUM (design choices)
- Platform mobile layout: HIGH — empirical
- Backdrop worst case (F4): MEDIUM — measured on the image, but the visual consequence depends on the sign-off surface choice
- Audit tooling: MEDIUM — wrapper design is straightforward but unexecuted against the live app (DB unreachable)
- Pitfalls: HIGH

**Research date:** 2026-10-30
**Valid until:** 2026-11-29 for library versions (Bootstrap 5.3.8, FA 6.7.2; re-run `npm view` at execution); the in-repo findings stay valid until the files change.
