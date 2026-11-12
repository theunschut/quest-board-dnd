# Phase 94: Whole-App UI Review and Unification - Context

**Gathered:** 2026-10-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 94 is the **first** phase of a multi-phase UI sweep, not the whole sweep. It delivers:

1. **A page-by-page audit** of every view (133 `.cshtml` files across `Views/` and
   `Areas/Platform/Views/`, 55 of them `.Mobile.cshtml` twins) on desktop and mobile, run with the
   `senior-frontend` skill. It lists visual bugs, inconsistencies, cascade fights, twin drift, twin
   content differences and accessibility failures per page. It also proposes the follow-up area
   phases.
2. **A signed-off visual direction**: `senior-frontend` mockups that evolve today's look, approved
   by the operator before any production view changes.
3. **The foundation**: the token layer, the shared component classes, both main layouts and the
   Platform layouts (navigation, footer, background), self-hosted vendor assets, and dead
   dependencies removed.
4. **A rewritten `.claude/ui-guidelines.md`** recording the resulting system so new views follow it.
5. **The follow-up area phases appended to the roadmap**, after the operator approves the cut the
   audit proposes, including a full email-template pass.

Moving the individual views onto the new system is the job of those appended area phases, not of
Phase 94. Every existing flow keeps working exactly as it does today. No capability is added or
removed.

</domain>

<decisions>
## Implementation Decisions

### Visual direction
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

### Phase shape and release
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

### Third-party assets
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

### Inline scripts and styles
- **D-12: Inline code moves out as pages convert.** Phase 94 removes inline `style=""`, `<style>`
  and `<script>` from the layouts it rebuilds and writes the rule into `.claude/ui-guidelines.md`:
  new code carries no inline style or script. Each area phase moves its own pages' inline CSS into
  stylesheets and inline JS into files or ES modules. The Content-Security-Policy header itself is
  **not** in this sweep (see Deferred).

### Mobile twins
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

### Accessibility (operator delegated to Claude)
- **D-16: WCAG 2.2 AA plus 44px mobile targets.** Text contrast is at least 4.5:1, or 3:1 for
  large text and for control borders and focus indicators. Every interactive element has a visible
  `:focus-visible` ring. Touch targets are 44×44px on the mobile layout. These are built into the
  tokens and components in Phase 94, and each area phase brings its pages up to the bar.
- **D-17: Keep the signature colours and adjust their use or shade.** Where gold or cream fails
  contrast in a given use (for example gold body text on parchment), keep the colour in the
  palette and change how it is used (decoration, borders, large headings) or use a darker or
  lighter passing shade for text. The mockups (D-02) must already meet D-16, so the operator sees
  the effect before signing off.

### Email templates
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

### Post-research decisions (operator, 2026-10-30)
- **D-20: View-level CDN libraries are self-hosted in Phase 94 too.** Cropper.js, Tagify and EasyMDE,
  loaded from jsDelivr by individual views, move to `wwwroot/lib` in this phase at the versions the
  views load today. Those views change only their `<link>`/`<script>` tags; their restyle still waits
  for the area phases. The email layout's remote Google Fonts and Font Awesome stay as they are; they
  belong to the email area phase (D-18 unchanged).
- **D-21: The pre-sign-off exception is narrowed to provably identical changes.** Before direction
  sign-off only these land: self-hosting at the **same** versions loaded today (Bootstrap 5.3.0,
  Font Awesome 6.4.0, and the D-20 libraries), jQuery and Font Awesome v4-shim removal once proven
  unused (D-10), and the bare `@layer` order statement. Each is gated by zero-visible-difference
  screenshots. The bump to the latest Bootstrap 5.3.x and Font Awesome 6.x, and importing Bootstrap
  into `layer(vendor)`, come after sign-off as their own commits, because neither is
  appearance-neutral.
- **D-22: `site.css` is renamed to `legacy-pages.css` in Phase 94**, in the commit that moves its
  shared rules into the new foundation files; the tests that read it by name are updated in that
  same commit.
- **D-23: Audit logins are local test accounts.** Once the operator has SQL Server running at
  `localhost:1433` (never provisioned by the agent), the executor registers one test account per role
  (player, Dungeon Master, admin, platform super-admin) through the app on localhost; the operator
  grants the roles. Credentials live in the scratch audit directory outside the repository and are
  never committed. **Amendment (planning, 2026-10-30):** the app has no self-registration route —
  accounts are created under Admin > Create user, and the platform super-admin role can only be
  granted by the operator. So the operator bootstraps the audit admin and audit platform accounts at
  the plan 94-01 checkpoint, and the executor creates the player and Dungeon Master accounts through
  the admin pages. The operator confirms this at that checkpoint.
- **D-25: Audit fixture writes are data, not code.** The project rule that nothing writes to `Events`
  or `Quests` is about code paths (the calendar feed revision). Creating fixture quests, events and a
  series through the app's own forms on the local audit database, and casting a date vote on a
  fixture quest for the console proof, are allowed: they go through the normal change-tracked app
  flows, touch only rows the fixture step created, and change no code.
- **D-24: Error and warning toasts persist until dismissed** (`data-bs-autohide="false"`); success,
  info and gold toasts keep their auto-hide delays. This is an approved behaviour change, recorded in
  the UI-SPEC as departure A2 / S13. Rendering-neutral pre-sign-off work is approved as UI-SPEC
  assumption A1, as narrowed by D-21.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and project rules
- `.planning/ROADMAP.md` § "Phase 94: Whole-App UI Review and Unification": goal, starting-point
  numbers, the to-decide list this discussion answered, and constraints (no flow removed, every
  desktop change checked against its twin, dates through the helpers, no writes to
  `Events`/`Quests`, before/after screenshots, Docker path keeps working, no planning references in
  CSS comments).
- `CLAUDE.md`: branching (`milestone/v9-rolling-improvements`, never `main`), no planning IDs in
  source comments, local DB contract, Linux build traps.
- `.claude/project-overview.md`: compatibility, self-hosting and Docker constraints.
- `.claude/architecture.md` § "Time and the board clock": the `Html.LocalTime` and
  `Html.WallClock` rendering rules every restyled date must keep.

### UI system
- `.claude/ui-guidelines.md`: the current UI contract (modern-card pattern, button layout, mobile
  twins, date helpers). Phase 94 rewrites it to record the new system.
- `~/.claude/skills/senior-frontend/SKILL.md`: the skill the audit, mockups and builds run under.
  It also has `references/materials-and-depth.md`, `references/critique-checklist.md` and
  `scripts/shoot.mjs`. Note that the skill assumes a strict CSP, which this app does not have yet
  (D-12).
- `.planning/phases/93-per-board-page-selection-and-home-page/93-UI-SPEC.md` and `93-UI-REVIEW.md`:
  the most recent UI contract and audit format in this project, including the refusal markup
  pattern (red lines with an icon in a `role=alert` wrapper).
- `.planning/phases/91-linked-boards-with-a-shared-calendar/91-UI-REVIEW.md`: where the
  `modern-card` span-colour bug class was caught.

### Release and deployment
- `.planning/phases/89-pull-based-release-deployment/89-CONTEXT.md`: draft release plus `deploy`
  approval gate (D-02 there), which is how deployment is held under D-07 here; GHCR image publishes
  on every tag push.
- `docs/releasing.md`, `docs/deploy.md`: the operator-facing release and deploy procedure.

### Prior UI lessons (from PROJECT.md Key Decisions and STATE.md)
- `.planning/PROJECT.md` Key Decisions: Phase 35 (global `:disabled` styling fix), Phase 80
  (Contacts category headings use `<h2>` to dodge the `modern-card` element enumeration), Phase
  66/67 (gold `.modern-card` rule leaking into the editor; a page-wide `!important` hiding recap text
  on mobile; mobile EasyMDE toolbar buttons narrower than 44px, accepted as a deviation).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/ViewExpanders/MobileViewLocationExpander.cs`: selects `.Mobile.cshtml` by
  User-Agent. This is the mechanism D-13 and D-15 depend on.
- Layouts: `Views/Shared/_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml`,
  `Areas/Platform/Views/Shared/_Layout.Platform.cshtml` and `_Layout.Platform.Mobile.cshtml`, plus
  `_Footer.cshtml` and `_Footer.Mobile.cshtml` and `_Toasts.cshtml`. All of them are in the
  foundation's scope.
- `wwwroot/css/modern-card.css` (424 lines): the shared card component that becomes the basis
  of the component layer.
- `wwwroot/js/site.js`: `TIMESTAMP_FORMATS` and the client-side `.local-time` re-formatting. It
  must keep working after restyling.
- The notice-board background is `wwwroot/images/Notice Board-blank2.jpg`, rendered on a fixed
  `body::before` in both `site.css` and `mobile.css`.
- Existing partial patterns: `_MarkdownEditor.cshtml`, `_Calendar(.Mobile).cshtml`,
  `_LinkedDayEntries.cshtml`, `_ShopItemDetailsContent.cshtml`, `_CharacterSelectModal.cshtml`.

### Established Patterns
- Desktop `_Layout.cshtml` loads about 14 page stylesheets globally on every page (`site.css`,
  `modern-card.css`, `calendar.css`, `quests.css`, `shop.css`, `characters.css`, `contacts.css`,
  and so on). The mobile layout loads `mobile.css`, `modern-card.css` and the markdown CSS, with
  per-page `*.mobile.css` files.
- Today's 18 custom properties are local (card widths, tag colours), so there is no global token
  set. `--card-width` is defined twice, in `site.css` and in `quests.css`.
- Vendor assets today: Bootstrap 5.3.0 (CSS and bundle JS) from jsDelivr and Font Awesome 6.4.0
  plus the v4 shim from cdnjs (only the shim carries SRI). Cinzel comes from Google Fonts and jQuery
  3.6.0 from cdnjs, with no SRI. Icons in use are `fas fa-*` (v5/v6 names).
- **Static guard tests read CSS and view files and pin facts.** A refactor that moves rules will
  break them. They must be rewritten to pin the same intent, never deleted:
  `QuestBoard.IntegrationTests/Mobile/MobileCssTests.cs`, `Mobile/MobileViewsTests.cs`,
  `Controllers/EventsOverviewMobileStyleTests.cs`, `Controllers/PlatformAreaLayoutTests.cs`,
  `Controllers/LinkedBoardsPlatformIntegrationTests.cs`, `Tests/ContactCategoryContrastGuardTests.cs`,
  `Tests/LinkedBoardCalendarStyleTests.cs`, `Tests/LinkedBoardStaticGuardTests.cs`,
  `Tests/BoardPageStaticGuardTests.cs`, `Tests/BoardMoveStaticGuardTests.cs`,
  `Tests/BoardMoveViewsTests.cs` and `Tests/CalendarSubscriptionStaticGuardTests.cs`.
- Pages with no mobile twin, served under both layouts: `Events/Create`, `Events/Details`,
  `Events/Edit`, `Series/Details`, `Admin/EmailStats` and `Shared/AccessDenied`.

### Integration Points
- The release zip and the Docker image must carry the self-hosted vendor files (D-09). Check the
  `Dockerfile` and the release workflow include `wwwroot/lib`.
- Email templates are in `QuestBoard.Service/Components/Emails/` (10 components including
  `_EmailLayout.razor`), for the email area phase (D-18).
- The audit needs a running app with representative data. The DB is SQL Server at
  `localhost:1433`, provisioned by the operator, never by the agent. Phase 92 UAT left test data
  in place locally (quests 991001 and 991002, event 1).

</code_context>

<specifics>
## Specific Ideas

- The operator expects "many enhancements are possible across the views" and wants
  `senior-frontend` to "update, fix, unify and possibly upgrade" the UI. The upgrade is real but
  stays within the existing identity (D-01).
- `senior-frontend`'s Designer-mode practice applies: show rendered options, not descriptions.
  Each direction differs in kind, not intensity, and comes with zoomed detail crops. Independent
  axes (for example how strong the parchment texture is) can be offered as a switcher on the
  mockup page.
- Mockups must already meet the D-16 accessibility bar, so sign-off is on what can actually ship.

</specifics>

<deferred>
## Deferred Ideas

- **Adopt a Content-Security-Policy header.** This is a separate security phase after the sweep
  has moved inline style and script out of the views (D-12). The sweep prepares for it but does
  not ship it.
- **Bootstrap 6, Font Awesome 7 or other major library migrations** were excluded by D-11. Revisit
  only if a future need justifies it.

</deferred>

---

*Phase: 94-whole-app-ui-review-and-unification*
*Context gathered: 2026-10-30*
