# Phase 94 Audit: Admin and Platform area (`A-admin-nnn`)

Audit of the Admin area (`Views/Admin`) and the Platform area (`Areas/Platform/Views`), desktop and mobile, against the
frozen baseline (BASELINE_SHA `b6770322`, port 8001). Method: senior-frontend Critic mode (visual critique checklist),
the BEFORE record (`before/report.json`), the fixed twin-comparison procedure, and live read-only probes of the baseline.

## Scope

- **Views (13 index rows, 25 files):** Admin `Users`, `CreateUser`, `EditUser`, `ResetPassword`, `Quests` (each with a `.Mobile` twin) and
  the twin-less `EmailStats`; Platform `Group/{Index, Create, Edit, Delete, Members}`, `Users/Index`, `LinkedBoards/Index` (each with a `.Mobile` twin).
- **Layouts reached:** Admin views render in the main `_Layout` (desktop) and `_Layout.Mobile` (phones). Platform views render in
  `_Layout.Platform` (desktop) and `_Layout.Platform.Mobile` (phones), see "Platform layout on phones". The two main layouts and the
  two Platform layouts belong to the Foundation; findings on them are marked shared layout and listed once.
- **Partials:** these pages render no page-specific partial. The Platform pages pull `_Toasts`, `_Footer` and `_Footer.Mobile`
  (Foundation). The `createMemberModal` on Platform Members is inline markup in both twins and is audited as part of that page.
- **Stylesheets:** `admin-form.mobile.css`, `admin-quests.mobile.css`, `admin-users.mobile.css`, `platform-group.mobile.css`,
  `platform-users.mobile.css`, `platform-linked-boards.mobile.css` (see "Stylesheet census"). The desktop pages rely on shared
  `site.css` and `modern-card.css`.
- **Evidence paths:** `QBR` below means `~/qb-ui-review/phase-94`. Screenshots are scratch only and never committed:
  `QBR/before/desktop/<Id>.png` and `QBR/before/mobile/<Id>.png` with `<Id>` the manifest id (for example `Platform-Group-Index`);
  machine records in `QBR/before/report.json` (probe fields: contrastFailures, overflow, overflow320, interactive, focus, requests);
  this audit's own probes in `QBR/audit/admin/` (`platform-layout-probe.json`, `members-modal-probe.json`, `text-spacing-probe.json`,
  `render-cost-probe.json`, plus the probe scripts). Fixture ids: Audit Board `group.audit`, empty board `group.empty`, fixture player `user.player`.
- **Privacy:** Admin/Users, Platform/Users, Platform/Members and Platform/Group/Index list real members and boards. They were captured
  (scratch only, `QBR/` only) and are described here structurally: no member or board name, email or user-entered text is quoted anywhere in this report.
- **Bar used:** D-16 (4.5:1 text, 3:1 large text and non-text, translucent surfaces composited over B-max `#794317`) via
  `QBR/tools/contrast.mjs`; D-17 for how gold and parchment may be used. The BEFORE record scores B-med, B-99 and B-max; the figure
  quoted is the worst blocking-board result unless a board is named.
- **View-only:** no form was submitted and no account, role, board, membership or link set was touched. The one interaction was opening the
  Create New User modal client-side on Platform Members.
- **Not exercised (limit):** the Admin/EmailStats statistics branch (stat cards) needs a configured email provider key; the baseline
  has none, so only the "key not configured" state was captured. That branch is audited from source only (A-admin-036).

## Index

One row per view, twins on one row. Counts are per class (finding ids per view are listed in Findings; shared-layout findings
A-admin-008 and A-admin-014 are not counted in any row). Highest severity: S1 text unreadable or control unreachable, S2 fails the AA bar or visibly wrong, S3 inconsistency, S4 polish.

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|---------------------------|-------------------------|
| Admin/Users | `Views/Admin/Users.cshtml` | `Views/Admin/Users.Mobile.cshtml` | 5b Admin and Platform | visual-bug 2, inconsistency 4, cascade-fight 1, twin-drift 2, twin-content 3, a11y 6, perf 2, dead-code 1 (21) | S1 | desktop "No users found."; mobile "No users found. Users appear here once registered." (differs: A-admin-027) | 4 (Q1, Q2, Q9, R-3) |
| Admin/CreateUser | `Views/Admin/CreateUser.cshtml` | `Views/Admin/CreateUser.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, twin-drift 2, a11y 4, perf 1 (10) | S2 | none (form) | 2 (Q6, R-1) |
| Admin/EditUser | `Views/Admin/EditUser.cshtml` | `Views/Admin/EditUser.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, twin-drift 1, twin-content 1, a11y 6, perf 1 (12) | S1 | none (form) | 3 (Q6, Q7, R-1) |
| Admin/ResetPassword | `Views/Admin/ResetPassword.cshtml` | `Views/Admin/ResetPassword.Mobile.cshtml` | 5b Admin and Platform | inconsistency 2, twin-content 1, a11y 5, perf 1 (9) | S2 | none (form) | 1 (Q8) |
| Admin/Quests | `Views/Admin/Quests.cshtml` | `Views/Admin/Quests.Mobile.cshtml` | 5b Admin and Platform | visual-bug 2, inconsistency 2, cascade-fight 1, twin-drift 3, twin-content 2, a11y 6, perf 2, dead-code 1 (19) | S1 | desktop "No quests found."; mobile "No quests found. Quests appear here once created by a DM." (differs: A-admin-027) | 4 (Q3, Q5a, Q5b, Q9) |
| Admin/EmailStats | `Views/Admin/EmailStats.cshtml` | none (served under both) | 5b Admin and Platform | inconsistency 1, twin-drift 1, a11y 3, perf 1 (6) | S2 | no list; state copy only, see Empty-state copy | 0 |
| Group/Index (Platform) | `Areas/Platform/Views/Group/Index.cshtml` | `Areas/Platform/Views/Group/Index.Mobile.cshtml` | 5b Admin and Platform | visual-bug 2, inconsistency 2, cascade-fight 1, twin-drift 2, a11y 5, perf 2, dead-code 1 (15) | S1 | "No groups yet." / "Create the first group to get started." (same in both) | 0 |
| Group/Create (Platform) | `Areas/Platform/Views/Group/Create.cshtml` | `Areas/Platform/Views/Group/Create.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, a11y 5, perf 1 (9) | S2 | none (form) | 1 (M-1) |
| Group/Edit (Platform) | `Areas/Platform/Views/Group/Edit.cshtml` | `Areas/Platform/Views/Group/Edit.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, twin-drift 1, a11y 6, perf 1 (11) | S2 | none (form) | 1 (M-3) |
| Group/Delete (Platform) | `Areas/Platform/Views/Group/Delete.cshtml` | `Areas/Platform/Views/Group/Delete.Mobile.cshtml` | 5b Admin and Platform | inconsistency 1, a11y 3, perf 1 (5) | S2 | none (confirmation page) | 1 (M-2) |
| Group/Members (Platform) | `Areas/Platform/Views/Group/Members.cshtml` | `Areas/Platform/Views/Group/Members.Mobile.cshtml` | 5b Admin and Platform | visual-bug 1, inconsistency 3, cascade-fight 1, twin-drift 2, twin-content 1, a11y 7, perf 2, dead-code 1 (18) | S1 | members "No members in this group yet."; users "All users are already members of this group." plus search states (same in both) | 2 (Q4, R-2) |
| Users/Index (Platform) | `Areas/Platform/Views/Users/Index.cshtml` | `Areas/Platform/Views/Users/Index.Mobile.cshtml` | 5b Admin and Platform | visual-bug 1, inconsistency 2, twin-drift 1, a11y 7, perf 2, dead-code 1 (14) | S1 | "No users found." (same in both) | 0 |
| LinkedBoards/Index (Platform) | `Areas/Platform/Views/LinkedBoards/Index.cshtml` | `Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml` | 5b Admin and Platform | inconsistency 3, cascade-fight 1, twin-drift 2, a11y 5, perf 2, dead-code 1 (14) | S1 | "No linked sets yet." / "Create a set above, then add boards of the same type to share their calendars." (same in both) | 0 |

Totals: 44 findings (S1 8, S2 14, S3 16, S4 6); 2 of them shared-layout. Operator questions Q1 to Q9 (Q5a and Q5b are two questions) plus M-1 to M-3 (merges) and R-1 to R-3 (relayouts).

## Platform layout on phones

Answer: **a SuperAdmin on a phone receives `_Layout.Platform.Mobile`, not the desktop Platform layout.** The note in PROJECT.md calling
`_Layout.Platform.Mobile.cshtml` dead code is stale and should be corrected in the milestone docs (not by this phase's code changes).

Proof (read-only probe `QBR/audit/admin/probe-layout.mjs`, results in `QBR/audit/admin/platform-layout-probe.json`, baseline app, SuperAdmin session; marker is the
`#platformMobileNav` offcanvas and `body.mobile-layout`, which exist only in the mobile Platform layout), cross-checked with the BEFORE records
(`mobileLayout: true`, linked stylesheets):

| Platform page | Desktop UA renders | Mobile UA (Pixel 7, touch) renders | Stylesheets under mobile UA |
|---------------|--------------------|-------------------------------------|------------------------------|
| Group/Index | `_Layout.Platform` | `_Layout.Platform.Mobile` | `mobile.css`, `platform-group.mobile.css` |
| Group/Create | `_Layout.Platform` | `_Layout.Platform.Mobile` | `mobile.css`, `platform-group.mobile.css` |
| Group/Edit | `_Layout.Platform` | `_Layout.Platform.Mobile` | `mobile.css`, `platform-group.mobile.css` |
| Group/Delete | `_Layout.Platform` | `_Layout.Platform.Mobile` | `mobile.css`, `platform-group.mobile.css` |
| Group/Members | `_Layout.Platform` | `_Layout.Platform.Mobile` | `mobile.css`, `platform-group.mobile.css` |
| Users/Index | `_Layout.Platform` | `_Layout.Platform.Mobile` | `mobile.css`, `platform-users.mobile.css` |
| LinkedBoards/Index | `_Layout.Platform` | `_Layout.Platform.Mobile` | `mobile.css`, `platform-group.mobile.css`, `platform-linked-boards.mobile.css` |

Desktop UA: `site.css` and `modern-card.css` only. Mobile UA: `mobile.css` and the page sheet only; **neither `site.css` nor `modern-card.css` is loaded on
Platform phones**, so `.modern-card` does not exist there and each page sheet rebuilds its own glass card (A-admin-020, A-admin-038), and rules that live in
`modern-card.css` (for example the `.email-link` colour, A-admin-005) are absent. Consequences for the Foundation: the mobile Platform shell must receive the
tokens and the shared components exactly like `_Layout.Mobile`; the two CSS header comments that say "exclusively loaded by `_Layout.Platform.Mobile`" are accurate.
Both Platform layouts carry the same brand text ("D&D Quest Board"); only the footer differs between the pair (shared `_Footer` against `_Footer.Mobile`), which is the
seeded footer-text operator question and needs no extra question here.

## Findings

Fixed fields: ID | page and layout | class | severity | WCAG (a11y) | evidence (scratch screenshot, selector, `file:line`) | proposed fix (token or component class) | area phase | operator question. S1 first.
"5b" is area phase 5b Admin and Platform. Contrast ratios are from `QBR/before/report.json` unless a probe is named; "halo" means text-shadow, which the D-16 bar ignores.
All file paths below are under `QuestBoard.Service/` unless stated. Fix names use the UI-SPEC token and component set only.

### S1 (text unreadable or control unreachable)

| ID | Page and layout | Class | Sev | WCAG | Evidence | Proposed fix | Area phase | Op Q |
|----|-----------------|-------|-----|------|----------|--------------|------------|------|
| A-admin-001 | Platform Group/Index (desktop; same markup on LinkedBoards/Index desktop) | cascade-fight | S1 | n/a | `QBR/before/desktop/Platform-Group-Index.png`; `span.badge.bg-info.text-dark` is parchment `#F4E4BC` on cyan, 1.55:1; the bare `.modern-card span { color: … !important }` at `wwwroot/css/modern-card.css:91-97` out-ranks the Bootstrap `text-dark`; markup `Areas/Platform/Views/Group/Index.cshtml:47`, `LinkedBoards/Index.cshtml:75` | `.badge` on `--qb-info` with label `--qb-info-fg`; delete the bare `span`/`p`/`li` colour rule (Foundation) so a badge keeps its own label colour | 5b | N |
| A-admin-002 | Admin/Users (desktop) and Platform Group/Members (desktop) | cascade-fight | S1 | n/a | `QBR/before/desktop/Admin-Users.png`, `QBR/before/desktop/Platform-Group-Members.png`; `span.badge.bg-warning` (Dungeon Master role badge) parchment on amber `#ffc107`, 1.29:1; same cause as A-admin-001 (`modern-card.css:91-97` beats `text-dark`); `Views/Admin/Users.cshtml:61`, `Areas/Platform/Views/Group/Members.cshtml:64` (`bg-warning text-dark`) | `.badge` on `--qb-warning` with label `--qb-warning-fg` (near-black); gold stays a fill only (D-17) | 5b | N |
| A-admin-003 | Admin/Users (mobile) | a11y | S1 | 1.4.3 | `QBR/before/mobile/Admin-Users.png`; `span.badge.bg-warning` white on amber, 1.63:1 (the mobile twin has no `text-dark` and no label rule); `Views/Admin/Users.Mobile.cshtml:39` | same badge fix as A-admin-002, one rule for both layouts (also removes the label-colour twin-drift in A-admin-011) | 5b | N |
| A-admin-004 | Admin/Quests (desktop) | cascade-fight | S1 | 1.4.3 | `QBR/before/desktop/Admin-Quests.png`; the Description cell `span.text-truncate.d-inline-block` is parchment text on a parchment paper cell, 1.20 to 1.45:1 in every row (15 rows in the fixture); the bare-`span` rule `modern-card.css:91-97` re-colours text inside a cell that `modern-card.css:288-295` paints parchment; markup `Views/Admin/Quests.cshtml:50` (also carries `style="max-width: 300px;"`, D-12) | table cell text inherits `--qb-ink` on the `--qb-surface-paper` cell (UI-SPEC Tables); width in a class | 5b | N |
| A-admin-005 | Platform Users/Index (mobile) | a11y | S1 | 1.4.3 | `QBR/before/mobile/Platform-Users-Index.png`; every `a.email-link` is Bootstrap link blue `rgb(13,110,253)` on glass, 1.05:1 at B-99 (one per listed user); the `.email-link` rule exists only in `modern-card.css:307-313`, which Platform phones do not load; `Areas/Platform/Views/Users/Index.Mobile.cshtml:46`, `wwwroot/css/platform-users.mobile.css` (no rule) | `a` colour `--qb-link` on glass (plain-link style shared by both layouts) | 5b | N |
| A-admin-006 | Platform Group/Members (desktop, Create New User modal) | a11y | S1 | 1.4.3 | probe `QBR/audit/admin/probe-modal.mjs` and `QBR/audit/admin/members-modal-probe.json`, scratch `members-modal-desktop.png`; modal title and `.form-label`s are dark ink `rgb(26,15,8)` on the Bootstrap `bg-dark` content `#212529`, 1.22:1 (readable only through the white halo); cause `wwwroot/css/site.css:586` (headings) and `:594` (`.form-label`) apply outside a card; markup `Areas/Platform/Views/Group/Members.cshtml:185-226`; the mobile twin renders the label in `#F8F9FA` (14.63:1) but its title is still dark ink | modal on `--qb-surface-overlay` with `--qb-fg` text (opaque overlay, UI-SPEC "Modal and offcanvas"); one rule for both layouts | 5b | N |
| A-admin-007 | Admin/EditUser (desktop and mobile) | a11y | S1 | 1.4.3, 1.4.11 | `QBR/before/desktop/Admin-EditUser.png`, `QBR/before/mobile/Admin-EditUser.png`; `a.btn.btn-outline-danger` Reset Password is red `rgb(220,53,69)` on glass, 1.21:1 at B-max; it is also the only outline button in the area, against the filled-only rule; `Views/Admin/EditUser.cshtml:56`, `Views/Admin/EditUser.Mobile.cshtml:48` | `.btn-danger` filled (`--qb-danger` fill, `--qb-danger-fg` label); stays a link, control type unchanged | 5b | N |
| A-admin-008 | Platform desktop layout (shared layout; all seven Platform pages at 320px) | a11y | S1 | 1.4.10 | probe `QBR/audit/admin/probe320.mjs` (`superadmin`, `/platform/Group/Create`, 320): `ul.navbar-nav` is 377px wide and ends at x=393, so the user name and the Logout button sit beyond the 320px viewport of a fixed navbar and cannot be scrolled to; `Areas/Platform/Views/Shared/_Layout.Platform.cshtml:15-40` (`navbar-expand-lg` with no toggler or collapse). Only narrow desktop windows get this layout; phones get the mobile layout | Foundation: a collapsing nav for the Platform desktop layout (nav toggler named "Toggle navigation") | Foundation (Platform layouts), 5b verifies | N |

### S2 (visibly wrong or fails the AA bar)

| ID | Page and layout | Class | Sev | WCAG | Evidence | Proposed fix | Area phase | Op Q |
|----|-----------------|-------|-----|------|----------|--------------|------------|------|
| A-admin-009 | Platform Group/Index (desktop) | a11y | S2 | 1.4.10 | `QBR/before/desktop/Platform-Group-Index.png`; `overflow320: true`, document scroll width 455px at 320 (A-6); probe `probe320.mjs` names `div.d-flex.gap-2` (three header buttons, ends at x=455) inside a header row with no wrap; `Areas/Platform/Views/Group/Index.cshtml:8,13` (the mobile twin wraps: `Index.Mobile.cshtml:108,112`) | `.modern-card-header` actions wrap below the title (`flex-wrap`, `--qb-space-sm` gap), same as the twin | 5b | N |
| A-admin-010 | all 13 views (desktop) | a11y | S2 | 1.4.3 | white labels on role fills fail in every desktop view: `btn-warning` 2.19, `btn-success` 2.87 (disabled Add Board in LinkedBoards 2.01), `btn-info` 3.04, `btn-sm` rows 3.15 to 3.82, `btn-primary` 3.15, `btn-secondary` 3.49, `btn-danger` 3.82; rules `modern-card.css:221-250` (in-card gradients) and `wwwroot/css/site.css:646-725` (`.btn` and role gradients with `!important` and a white label); per-view detail in `before/report.json` (`a.btn`, `button.btn` selectors) | UI-SPEC button semantics: `--qb-{success,info,secondary,danger}` fills with `-fg` labels at 4.5:1, `btn-warning` near-black label on amber, the commit action on `--qb-action` | 5b | N |
| A-admin-011 | Admin/Users, Admin/Quests, Group/Index, Group/Members, Users/Index, LinkedBoards/Index | twin-drift | S2 | 1.4.3 | the same badge has different label colours per layout: desktop parchment on the fill (`bg-primary` 3.57, `bg-success` 3.60, `bg-danger` 3.59, `bg-secondary` 3.72) from `modern-card.css:91-97`, mobile Bootstrap white (4.50 to 4.53, passes); evidence `before/report.json` records `span.badge.*` for both UAs | one `.badge` component, label `--qb-{status}-fg` on `--qb-{status}`, in both layouts | 5b | N |
| A-admin-012 | CreateUser, EditUser, ResetPassword, Users, Quests, Group/Index, Create, Edit, Delete, Members, Users/Index, LinkedBoards | a11y | S2 | 1.4.3 | full-strength parchment on glass misses 4.5:1 at B-max: desktop `label.form-label`, `p`, `h5`, `.form-check-label` 4.34; LinkedBoards `h5.mb-0` 3.91; mobile `label.form-label` 4.34, and the bold 16px `strong.parchment-text` 3.18 (not "large text": bold must be 18.66px); rules `modern-card.css:78-97,121-127`, `wwwroot/css/platform-group.mobile.css:152-164`, `admin-form.mobile.css:17-22`, `admin-users.mobile.css`/`admin-quests.mobile.css`/`platform-users.mobile.css` heading blocks | `--qb-fg` on `--qb-surface-glass` with the scrim so the composite passes at B-max (UI-SPEC A-1, S2); the 28px Display step only may use gold | 5b | N |
| A-admin-013 | EditUser, Quests, Group/Index, Create, Edit, Members, LinkedBoards | a11y | S2 | 1.4.3 | the "muted" text is 70%-alpha parchment, forbidden by D-17: desktop `.text-muted`/`.form-text` 2.99:1 (`modern-card.css:102,129`), mobile `small`/`.text-muted` 2.99 and 2.35 (`platform-group.mobile.css:167-172`, `admin-quests.mobile.css`, `platform-users.mobile.css`); the Group/Edit hint was re-done opaque `#D9CDA8` (`platform-group.mobile.css:204-209`) and still measures 3.45 | `--qb-fg-muted` as an opaque colour chosen at 4.5:1 on glass at B-max | 5b | N |
| A-admin-014 | Platform desktop layout (shared layout) | a11y | S2 | 1.4.3 | navbar link text on the translucent navbar over B-max: `a.nav-link.text-light` parchment 3.78:1 (`site.css:501-552`, `_Layout.Platform.cshtml:27`). The BEFORE probe also lists the signed-in `span.navbar-text` (3.63 desktop, 2.56 mobile), but a live computed-style check shows `#F8F9FA` because `text-light` wins, so that figure is not relied on and the user-name text is left unscored | navbar and footer chrome tokens (UI-SPEC S6) with a parchment label at 4.5:1 at the lightest gradient stop | Foundation (Platform layouts) | N |
| A-admin-015 | Admin/CreateUser and Admin/EditUser (desktop) | inconsistency | S2 | 1.3.1, 2.4.6, 1.4.3 | `QBR/before/desktop/Admin-EditUser.png`; a bare `<h1>@ViewData["Title"]</h1>` sits above the card and repeats the card's own `<h2>` title; the h1 is dark ink `rgb(26,26,26)` on bare board, 1.38:1 (white halo only; D-17: never dark text on bare board); `Views/Admin/EditUser.cshtml:8-10,16-19`, `Views/Admin/CreateUser.cshtml:82-84,90-93`; ResetPassword has no such h1; mobile has none | single page title in the card header as the Display step (page header component); see relayout R-1 | 5b | Y (Q6, R-1) |
| A-admin-016 | Admin/Quests (mobile against desktop) | twin-drift | S2 | n/a | `Views/Admin/Quests.cshtml:11,74-88` computes Open or Closed for a Campaign board from `quest.IsClosed`; `Views/Admin/Quests.Mobile.cshtml:24-45` has no board-type branch and falls through to Done, Finalized or Open, so a Campaign quest can show a different status on the phone than on the desktop (not exercised: the Audit Board is One-Shot) | one shared status mapping in both views; the badge classes per A-admin-011 | 5b | Y (Q3) |
| A-admin-017 | Admin/Users (mobile) | twin-content | S2 | n/a | desktop has the "Resend Welcome Email" action (`SendConfirmationEmail`, shown for unconfirmed users with an address) and the mobile view has none; `Views/Admin/Users.cshtml:123-133`, absent from `Users.Mobile.cshtml` | per the answer: record as deliberate, or add the form and button to the mobile card (needs its own approval and tests) | 5b | Y (Q1) |
| A-admin-018 | Admin/Users (mobile) | twin-content | S2 | n/a | desktop lists each user's email as a mailto link ("No email provided" otherwise); the mobile card shows name and role only; `Views/Admin/Users.cshtml:41-50`, absent from `Users.Mobile.cshtml` | per the answer (add to the mobile card, link in `--qb-link`) | 5b | Y (Q2) |
| A-admin-019 | CreateUser, EditUser, ResetPassword, Group/Create, Edit, Members, LinkedBoards (desktop) | a11y | S2 | 2.4.7, 1.4.11 | three different focus looks on form controls: `site.css:40` gold, `site.css:430-436` brown, and in-card `modern-card.css:161-166` pale blue 25% ring; the focus probe records the drawn outline as `rgb(26,26,26)` 3px on Group/Create stops 13 and 14, which measures 3.18:1 on glass at B-max and 2.00:1 at B-med | the base-layer two-tone ring (`--qb-focus-inner`, `--qb-focus-outer`, `--qb-focus-width`, `--qb-focus-offset`); remove the three per-component looks | 5b | N |
| A-admin-020 | Admin/Users, Admin/Quests, Group/Index, Group/Members, Users/Index, LinkedBoards (mobile) | perf | S2 | n/a | `backdrop-filter` on repeated per-row cards: probe `QBR/audit/admin/render-cost-probe.json` counts nodes with `backdrop-filter` under a phone UA: Platform Members 46 and Platform Users 46 (43 repeated rows each), Admin/Quests 18 (15), Group/Index 10 (7), Admin/Users 7 (4), LinkedBoards 6 (3), against 3 on a single-form page; rules `platform-group.mobile.css:143-150`, `admin-quests.mobile.css` `.quest-card-mobile`, `admin-users.mobile.css` `.user-card-mobile`, `platform-users.mobile.css` `.user-card-mobile`; each row also carries `box-shadow: 0 8px 32px` | row surface on `--qb-surface-glass` without blur and with `--qb-shadow-rest`; blur only on the shell and at most six panels (UI-SPEC Depth) | 5b | N |
| A-admin-021 | Admin/Users, Admin/Quests, Users/Index (desktop) | a11y | S2 | 1.4.3 | `a.email-link` `#8B4513` bold on a striped paper cell, 3.52:1 at B-med (4.50 at B-max); `modern-card.css:297-299,307-319` | `--qb-link` on `--qb-surface-paper-alt`, hover `--qb-link-hover` | 5b | N |
| A-admin-022 | Admin/EmailStats (desktop and mobile) | a11y | S2 | 1.4.3 | the `code` element inside the warning alert is Bootstrap pink `#d63384` on the alert fill, 2.37:1; `Views/Admin/EmailStats.cshtml:19` | `code` takes `--qb-ink` inside `.alert` | 5b | N |

### S3 (inconsistency with the system)

| ID | Page and layout | Class | Sev | WCAG | Evidence | Proposed fix | Area phase | Op Q |
|----|-----------------|-------|-----|------|----------|--------------|------------|------|
| A-admin-023 | Admin/Quests (mobile against desktop) | twin-content | S3 | n/a | desktop has the Description column (plain text, truncated) with "No description provided" and the Dungeon Master's mailto link; mobile shows only "DM: {name}"; `Views/Admin/Quests.cshtml:44-67`, absent from `Quests.Mobile.cshtml:55-57` | per the answer | 5b | Y (Q5a, Q5b) |
| A-admin-024 | Platform Group/Members (desktop against mobile) | twin-content | S3 | n/a | desktop has a "Current Members" section heading above the member search; mobile goes from the page title straight to the search and cards (it keeps the "Available Users" heading); `Areas/Platform/Views/Group/Members.cshtml:25` against `Members.Mobile.cshtml:26` | per the answer | 5b | Y (Q4) |
| A-admin-025 | Admin/EditUser (desktop against mobile) | twin-content | S3 | n/a | desktop has a "Password Management" heading and the sentence "Reset this user's password (they will need to use the new password to log in)." above the Reset Password button; mobile has only the button; `Views/Admin/EditUser.cshtml:51-60` against `EditUser.Mobile.cshtml:47-50` | per the answer | 5b | Y (Q7) |
| A-admin-026 | Admin/ResetPassword (desktop against mobile) | twin-content | S3 | n/a | the desktop card heading names the user whose password is being reset; the mobile heading says only "Reset Password" (the warning alert names the user in both); `Views/Admin/ResetPassword.cshtml:15` against `ResetPassword.Mobile.cshtml:14-16` | per the answer | 5b | Y (Q8) |
| A-admin-027 | Admin/Users and Admin/Quests (desktop against mobile) | twin-content | S3 | n/a | the empty-state sentence differs: desktop is one short line, mobile adds an explanatory second sentence (verbatim in "Empty-state copy"); `Views/Admin/Users.cshtml:157` and `Users.Mobile.cshtml:112`, `Views/Admin/Quests.cshtml:136` and `Quests.Mobile.cshtml:76` | per the answer; the icon and `py-5` block already match | 5b | Y (Q9) |
| A-admin-028 | all 13 views | a11y | S3 | 1.3.1, 2.4.6 | no view has a page `h1` except the duplicate in A-admin-015; desktop titles are `h2` in `.modern-card-header`, phone titles are `h5` (Group/Edit uses `h5` on desktop and `h6` on mobile for the Pages section); EmailStats on mobile is the shared view | one Display-step page title per page as the `h1` in the page header component; section headings one level lower in both layouts | 5b | N |
| A-admin-029 | Platform Group/Members (both layouts), Platform layouts | a11y | S3 | 4.1.2, 1.3.1, 2.4.1 | `.btn-close` in the Create New User modal has no accessible name and the dialog has `role="dialog"` but no `aria-labelledby` (probe `members-modal-probe.json`: `closeName: null`); the Platform mobile toggler and offcanvas close button have no names (`_Layout.Platform.Mobile.cshtml:76-90`); neither Platform layout has a skip link, `<header>`, `<nav aria-label>` or `<main id="main-content">` (desktop has `<main role="main">` only, `_Layout.Platform.cshtml:43`); the footer GitHub icon link has no name (`before/report.json`, `a` 23x46 with `hasName: false` on every page) | UI-SPEC A-8 / Layout contract: names "Open navigation menu", "Close navigation menu", "Skip to main content", "Quest Board source on GitHub"; modal title wired with `aria-labelledby` | Foundation (Platform layouts); 5b for the modal | N |
| A-admin-030 | Admin/Users, Admin/Quests, Users/Index, Group/Edit | a11y | S3 | 2.5.8, 2.5.5 | targets under the bar: mailto links 19px tall (Quests, desktop) and 22px (Users and Platform Users, both layouts; the probe counts 44 targets under 24px on Platform Users desktop and 45 under 44px on mobile); switch inputs 20x20 on desktop and 32x16 on mobile Group/Edit (the mobile label fills a 44px row, `platform-group.mobile.css:192-201`); the Platform mobile nav toggler is 56x40, under the 44px bar (A-5) | `.row-nav-link`/link padding to `--qb-hit-min`; toggler `min-height` `--qb-hit-min` | 5b (Foundation for the toggler) | N |
| A-admin-031 | Group/Create, Group/Edit, CreateUser, EditUser, ResetPassword, LinkedBoards, Group/Index, Admin/Users | inconsistency | S3 | n/a | the single commit action has a different fill per page: Create Group `btn-success`, Save Changes in Group/Edit `btn-warning`, Create User `btn-warning`, Save Changes in EditUser `btn-success`, Reset Password `btn-danger`, Create Set `btn-success`; the labels in one toolbar also differ in weight (`.modern-card .btn` 500 at `modern-card.css:221-226` against `.btn-secondary` 600 at `site.css:667-673`; on mobile `btn-primary` 600 at `mobile.css:120-126`, the rest 400) | the one `--qb-action` fill for the commit action; one label weight in `--qb-weight-strong` | 5b | N |
| A-admin-032 | all 13 views (desktop) | perf | S3 | 2.3.3 | `.modern-card` is a static card but lifts 2px and changes fill on hover (`modern-card.css:16-20`), including cards that hold tables and forms; `transition: all` at `modern-card.css:13,223` and `site.css:553`; probe: `transition: all` on 6 (Group/Delete) to 92 (Group/Members) nodes per desktop page, 2 to 4 on phones; no `prefers-reduced-motion` block anywhere | lift only on `.modern-card--interactive`; transition `transform` and `opacity` only (UI-SPEC Depth) | 5b | N |
| A-admin-033 | Admin/Users, Admin/Quests, Group/Members, LinkedBoards, Users/Index (both layouts) | inconsistency | S3 | n/a | D-12 census: inline `<script>` plus `onclick=` plus native `confirm(`/`alert(` in Admin/Users and Admin/Quests (the desktop twin puts the script in the body, the mobile twin in `@section Scripts`, so each function exists four times with a different location: `Users.cshtml:142,163-180`, `Users.Mobile.cshtml:101,117-136`, `Quests.cshtml:121,142-159`, `Quests.Mobile.cshtml:65,81-100`); a failed removal or delete shows a browser `alert(` instead of an Error toast; inline `onsubmit`/`onclick` handlers with `confirm(`: Members 1+1 (`Members.cshtml:77`, `Members.Mobile.cshtml:66`), LinkedBoards 2+2 (handler strings built in Razor, `LinkedBoards/Index.cshtml:64-65,123-124,128,164`), Platform Users 1+1 (`Users/Index.cshtml:86`, `Index.Mobile.cshtml:73`); one inline `style=` (`Quests.cshtml:50`) | one script file under `wwwroot/js` with `data-` attributes; failures through the Error toast (A-9) | 5b | N |
| A-admin-034 | Admin/Quests, CreateUser, EditUser, EmailStats | twin-drift | S3 | n/a | page-header icon colour differs by page and by layout with no rule: Quests desktop `text-danger` and mobile `text-warning` (`Quests.cshtml:18`, `Quests.Mobile.cshtml:17`), CreateUser and EditUser `text-warning`, EmailStats `text-info`, all Platform headers `text-danger` | one icon colour in the page header component (`--qb-accent` decoration per D-17, or `--qb-danger-text`) | 5b | N |
| A-admin-035 | Group/Index, Members, Edit, LinkedBoards, Admin/Users, CreateUser | twin-drift | S3 | n/a | the same button has a different look per layout: desktop `btn-warning`/`btn-info`/`btn-success` white label on a gradient fill, mobile Bootstrap flat fill with a dark label (compare `QBR/before/desktop/Platform-Group-Index.png` with `QBR/before/mobile/Platform-Group-Index.png`); `modern-card.css:221-250` and `site.css:646-725` do not load on Platform phones | one `.btn-*` set in both layouts (as A-admin-010) | 5b | N |
| A-admin-036 | Admin/EmailStats (statistics branch) | inconsistency | S3 | 1.4.3 (unmeasured) | not captured: needs a configured provider key. By source: four nested `.modern-card` stat tiles inside the outer card (card in card, each with the hover lift), figures coloured with Bootstrap `text-primary`/`text-success`/`text-warning`/`text-danger` (no token; pale on glass), label `small text-muted` (70% alpha parchment); `Views/Admin/EmailStats.cshtml:31-60` | tiles as `.modern-card` siblings (not nested) with `--qb-{status}-text` figures and `--qb-fg-muted` labels; re-measure once a key exists | 5b | N |
| A-admin-037 | Admin/ResetPassword (both layouts) | a11y | S3 | 3.3.8 (A-11) | the new and confirm password inputs are `type=password` (via `[DataType(DataType.Password)]`) with no `autocomplete`; paste is not blocked; `Views/Admin/ResetPassword.cshtml:27,33`, `ResetPassword.Mobile.cshtml:27,33` | `autocomplete="new-password"` on both | 5b | N |
| A-admin-038 | Admin/Users, Quests, CreateUser, EditUser, ResetPassword and all Platform views (mobile) | inconsistency | S3 | n/a | the same glass card is defined in five sheets (`.admin-form-card-mobile`, `.admin-quests-card-mobile`, `.admin-users-card-mobile`, `.platform-group-card-mobile`, `.platform-users-card-mobile`) and the per-row card four more times (`.quest-card-mobile`, `.user-card-mobile` twice, `.group-card-mobile`) with identical `rgba(255,255,255,0.15)`, `blur(15px)`, `0 8px 32px` values; `admin-form.mobile.css` says it serves EditUser and ResetPassword but `CreateUser.Mobile.cshtml` loads it too | one `.modern-card` in the shared components layer for both layouts; retire the nine duplicates in 5b | 5b | N |

### S4 (polish)

| ID | Page and layout | Class | Sev | WCAG | Evidence | Proposed fix | Area phase | Op Q |
|----|-----------------|-------|-----|------|----------|--------------|------------|------|
| A-admin-039 | Group/Index, Group/Members, Users/Index, Admin/Users, Admin/Quests (desktop) | visual-bug | S4 | n/a | the "Actions" column heading is left-aligned while its cells are right-aligned (`QBR/before/desktop/Platform-Group-Index.png`); `Group/Index.cshtml:36` against `:60`, `Admin/Users.cshtml:30` against `:74` | `.text-end` on that `th` (as LinkedBoards already does, `LinkedBoards/Index.cshtml:117`) | 5b | N |
| A-admin-040 | Group/Index (desktop) | visual-bug | S4 | n/a | the Delete button exists only for empty boards, so that row's action buttons shift left against the rows above (`QBR/before/desktop/Platform-Group-Index.png`, last row); `Group/Index.cshtml:74-79` | right-align the group with a stable gap so the first three buttons keep their x-position | 5b | N |
| A-admin-041 | Admin/Quests (mobile), Admin/Users (mobile) | visual-bug | S4 | n/a | the delete and remove icons have no spacing from the label on phones (`<i class="fas fa-trash"></i>Delete` with no whitespace or `me-1`), so the icon touches the word (`QBR/before/mobile/Admin-Users.png`); `Quests.Mobile.cshtml:66`, `Users.Mobile.cshtml:102`, desktop only works because of line breaks (`Quests.cshtml:122-123`, `Users.cshtml:143-144`) | `me-1` on the icon, as every other button does | 5b | N |
| A-admin-042 | Platform Users/Index (both layouts) | a11y | S4 | 1.3.1, 3.3.2 | the disabled Disable button for the signed-in account explains itself only in a `title` attribute, which keyboard and touch users never get; `Users/Index.cshtml:78`, `Index.Mobile.cshtml:65` | the reason as visible `.form-text` text (via `aria-describedby`) | 5b | N |
| A-admin-043 | Admin/Users, Quests, Group/Index, Members, Users/Index, LinkedBoards (desktop) | dead-code | S4 | n/a | `.modern-card .table` is defined twice: a cream cell block (`modern-card.css:252-272`) fully overridden by the parchment block (`:274-295`); the first block never shows | keep the parchment-paper cell with `--qb-ink` and wood header only (UI-SPEC Tables) | 5b (Foundation retires the rules) | N |
| A-admin-044 | Group/Create, Group/Edit, Group/Members, Admin/Users | inconsistency | S4 | n/a | the same labels are written differently: the board type select shows "OneShot" (enum name) while the lists and badges show "One-Shot"; the role badge reads "DungeonMaster" in Platform Members and "Dungeon Master" in Admin/Users, and "Admin" against "Administrator"; `Group/Create.cshtml:29`, `Group/Edit.cshtml:39`, `Members.cshtml:65`, `Admin/Users.cshtml:56-63`. User-facing text is unchanged in Phase 94 (copy contract), so this is recorded, not fixed | none in Phase 94 (copy stays); flag for the operator's later copy pass | 5b | N |

## Twin comparison

Procedure (fixed): for each pair the inventories below were extracted from both files by script (`QBR/audit/admin/inventory.py`: `asp-for` and `name=` fields,
`asp-action`/`href` links and `type="submit"` buttons, `h1`-`h6` and card blocks, `BoardPages` conditions) and then read against the source. No difference in this area
is recorded as deliberate: none of the 12 pairs carries a view comment, a PROJECT.md entry or a quick-task record that calls a difference deliberate (searched for "desktop-only",
"mobile omits", "intentionally"). Every `content-differs` item therefore becomes an operator question. `BoardPages` conditions: only Platform Group/Index reads board page data
(`BoardPagePolicy.Label(item.HomePage)` guarded by `item.HomePage != BoardPage.Quests`, identical in both twins); no other view in the area has one.

| # | Pair | Classification | Deliberate differences | Operator questions |
|---|------|----------------|------------------------|--------------------|
| 1 | Platform Group/Index | layout-only | none | none |
| 2 | Platform Group/Create | identical | none | none |
| 3 | Platform Group/Edit | identical | none | none |
| 4 | Platform Group/Delete | identical | none | none |
| 5 | Platform Group/Members | content-differs | none | Q4 |
| 6 | Platform Users/Index | layout-only | none | none |
| 7 | Platform LinkedBoards/Index | layout-only | none | none |
| 8 | Admin/Users | content-differs | none | Q1, Q2, Q9 |
| 9 | Admin/CreateUser | content-differs | none | Q6 |
| 10 | Admin/EditUser | content-differs | none | Q6, Q7 |
| 11 | Admin/ResetPassword | content-differs | none | Q8 |
| 12 | Admin/Quests | content-differs | none | Q3, Q5a, Q5b, Q9 |
| n/a | Admin/EmailStats | no twin (served under both) | n/a | none |

### Platform Group/Index (the tracer pair): layout-only

Extracted inventories (desktop and mobile are the same sets unless marked):

- **Form fields:** no `asp-for`; one `name="groupId"` hidden input in each per-row Open Board form (both).
- **Links and buttons (both):** Create Group (`Platform/Group/Create`), Manage Users (`Platform/Users/Index`), Linked Boards (`Platform/LinkedBoards/Index`); per row: Open Board (`type="submit"`, POST `GroupPicker/SelectGroup`), Members (`Platform/Group/Members/{id}`), Edit (`Platform/Group/Edit/{id}`), Delete (`Platform/Group/Delete/{id}`, only when `item.MemberCount == 0`). 13 `asp-action` references and 7 distinct button labels on each side; 1 `type="submit"` on each side.
- **Headed sections:** both "Group Management" (desktop `h2` in `.modern-card-header`, mobile `h5`). Desktop only: column headings Name, Board Type, Members, Created, Actions (`th`); on mobile the same values are present as the bold name, the type badge, and the inline labels "Members:" and "Created:" (Name, Board Type and Actions have no label). Card blocks: desktop `card modern-card` with `modern-card-header` and `modern-card-body`; mobile `platform-group-card-mobile` plus one `group-card-mobile` per row.
- **Conditions (both, 4):** `Model.Groups.Any()`, `item.BoardType == BoardType.Campaign`, `item.HomePage != BoardPage.Quests`, `item.MemberCount == 0`.
- **Result:** the same fields, links, buttons and conditions on both sides; only grouping and order differ (table against cards). Table against stacked cards is not CSS-only, so no merge qualifies (see Merge proposals).

### Platform Group/Create: identical

Fields `Name` and `BoardType` (both `asp-for`, same select with the enum list and the hint "This cannot be changed after creation."); links Back to Groups; one submit Create Group; heading "Create Group" (`h2` desktop, `h5` mobile); no conditions. Differences: wrapper markup (`row`/`col-md-6`/`card` against one glass div) and heading level. Layout only.

### Platform Group/Edit: identical

Fields: hidden `Id`, `RenderedDisabledPages`, `RenderedHomePage`; `Name`; `BoardType` (disabled select); the Pages section with one switch row per page (`Pages[i].Page`, `Pages[i].Enabled`, locked rows with their reason text), `HomePage` select with its hint; links Back to Groups; one submit Save Changes. Conditions: the refusal block (`ModelState` entry for the empty key) and `row.Locked`, in both. Section heading "Pages" is `h5` on desktop and `h6` on mobile (inside `platform-group-pages-mobile`). Layout only.

### Platform Group/Delete: identical

Confirmation sentence naming the board, Back to Groups, one submit Delete Group (POST `Delete` with the board id). Layout only (wrapper and heading level).

### Platform Group/Members: content-differs

Common: Back to Groups; the Create New User button and the same `createMemberModal` (`CreateMember.Email`, `CreateMember.Name`, `CreateMember.GroupRole`, Cancel, Create User); two search forms (`memberSearch`, `search`); the RemoveMember form (`userId`, with the inline confirm) and the AddMember form (`UserId`, `Role` select); role badges Admin, DungeonMaster, Player; "Available Users" heading; the empty and no-match states. Desktop only: the "Current Members" heading (`h5`, `Members.cshtml:25`; mobile has none) and the column headings Name, Email, Role, Actions (layout: mobile shows the same values unlabelled). Conditions identical (4 plus the three empty-state branches). **Difference to ask: the "Current Members" heading (Q4).**

### Platform Users/Index: layout-only

Common: Enable and Disable forms (`userId`), the disabled own-account Disable button, badges Confirmed or Unconfirmed and Active or Disabled, mailto link, "No email provided", the empty state. Desktop only: `th` Name, Email, Confirmed, Status, Actions (mobile folds the two badge groups into the name row and puts the email on its own line). Same conditions (4). Layout only.

### Platform LinkedBoards/Index: layout-only

Common: Back to Groups; the Create set form (`Name`, `BoardType`), per set Rename (`name`), Remove Board (`groupId`, confirm), Add a board (`groupId` select), Delete Set (confirm); status and type badges; all the explanatory sentences. Desktop only: `th` Board and Actions and a heading `h5` per set (mobile uses a bold name). Same conditions (5). Layout only (table against stacked rows).

### Admin/Users: content-differs

Common: Create User link (`CreateUser`), the PromoteToAdmin, PromoteToDM, DemoteFromAdmin and DemoteToPlayer forms (`userId`), Edit link (`EditUser`), the Remove from Group button (`removeFromGroup(id)`), role badges, the empty state. **Desktop only: (a) the Resend Welcome Email form and button (`SendConfirmationEmail`, condition `!EmailConfirmed && !string.IsNullOrEmpty(Email)`), (b) the Email column with the mailto link and "No email provided" (condition `!string.IsNullOrEmpty(Email)`).** Desktop only (layout): `th` Name, Email, Current Role, Actions. Mobile only: nothing in the inventory; the empty-state sentence is longer (Q9). Ask Q1, Q2, Q9.

### Admin/CreateUser: content-differs

Fields `Email`, `Name`, `GroupRole` (all three `asp-for`, same types); links Back to Users; one submit Create User. **Desktop only: a second page title (`h1`) above the card repeating the card heading (`CreateUser.cshtml:82-84`).** Mobile stacks the two buttons (`d-grid`) while desktop puts Back left and Create right (layout). Ask Q6.

### Admin/EditUser: content-differs

Fields hidden `Id`, `Name`, `Email`, `HasKey` (checkbox); links Reset Password (`ResetPassword`, `userId`) and Back to Users; one submit Save Changes. **Desktop only: the second page title (`h1`) and the "Password Management" heading with its explanatory sentence above the Reset Password button.** Ask Q6 and Q7.

### Admin/ResetPassword: content-differs

Fields hidden `UserId` and `UserName`, `NewPassword`, `ConfirmPassword`; the warning alert (same sentence naming the user in both); links Cancel (`Users`); one submit Reset Password. **The card heading differs: desktop "Reset Password for {user}", mobile "Reset Password".** Ask Q8.

### Admin/Quests: content-differs

Common: Edit link (`Quest/Edit`), Delete button (`deleteQuest(id)`), title, Dungeon Master name, status badge, the empty state. **Desktop only: (a) the Description column (plain-text excerpt with "No description provided", condition `!string.IsNullOrEmpty(plainQuestDescription)`), (b) the Dungeon Master email mailto link (condition `!string.IsNullOrEmpty(quest.DungeonMaster?.Email)`).** Desktop only (layout): `th` Title, Description, Dungeon Master, Status, Actions. **Logic difference: the desktop status badge branches on the board type (Campaign shows Open or Closed from `IsClosed`); mobile has no such branch (A-admin-016).** Ask Q3, Q5a, Q5b, Q9.

### Admin/EmailStats: no twin

One view served under both layouts (the BEFORE record shows `mobileLayout: true` with the same file). No comparison applies; it is audited once (stat tiles unexercised, A-admin-036).

## Operator questions

Standard wording, two answers only (`Deliberate, record it` or `Add it to the other view`). Removal is never offered. Until answered, nothing is added or removed.

| # | Question | Findings |
|---|----------|----------|
| Q1 | Admin/Users: the desktop view has the Resend Welcome Email button (unconfirmed users with an address) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-017 |
| Q2 | Admin/Users: the desktop view has each user's email (mailto link, or "No email provided") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-018 |
| Q3 | Admin/Quests: the desktop view has the Campaign board status logic (Open or Closed from the quest's closed flag) and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-016 |
| Q4 | Platform Group/Members: the desktop view has the "Current Members" section heading and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-024 |
| Q5a | Admin/Quests: the desktop view has the Description excerpt column and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-023 |
| Q5b | Admin/Quests: the desktop view has the Dungeon Master email link and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-023 |
| Q6 | Admin/CreateUser and Admin/EditUser: the desktop view has a second page title (an `h1`) above the card and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] (asked together with relayout R-1) | A-admin-015 |
| Q7 | Admin/EditUser: the desktop view has the "Password Management" heading and its explanatory sentence and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-025 |
| Q8 | Admin/ResetPassword: the desktop view names the user in the card heading ("Reset Password for {user}") and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-026 |
| Q9 | Admin/Users and Admin/Quests: the mobile view has a second explanatory sentence in the empty state and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view] | A-admin-027 |

Seeded questions (footer text and brand text) are asked in the layout audit. The Platform pair inherits the footer one (shared `_Footer` against `_Footer.Mobile`); the Platform
navbar brand reads the same in both Platform layouts, so there is no brand question for this area. Findings with Operator question = Y are exactly A-admin-015 to 018 and A-admin-023
to 027; merges (M-1 to M-3) and relayouts (R-1 to R-3) are also Y by rule.

## Merge proposals (D-13)

A pair qualifies only if it is `identical` or `layout-only`, has no deliberate difference, and the mobile differences are CSS-only. Three Platform pairs qualify; the other nine do not
(Group/Index, Users/Index and LinkedBoards/Index swap a table for stacked cards, so the markup differs; Members, Admin/Users, Admin/Quests, Admin/EditUser, Admin/ResetPassword and
Admin/CreateUser have content differences awaiting Q1 to Q9 and R-1; Admin/CreateUser would qualify once Q6 is answered). Each proposal is **costly to reverse and awaits explicit operator approval.**

| # | Pair | Why it qualifies | Precedent views | CSS-loading change in both layouts | Tests whose facts would change |
|---|------|------------------|-----------------|-------------------------------------|--------------------------------|
| M-1 | Platform Group/Create | identical inventory; the mobile differences are the wrapper (`row`/`col-md-6`/`card` against one glass div) and the heading level, all expressible in CSS | `Events/Create`, `Events/Edit`, `Events/Details`, `Series/Details` | `_Layout.Platform` keeps the shared card styles; `_Layout.Platform.Mobile` gains the same shared card and form styles (today it loads `mobile.css` only), and the mobile-only form rules move into a shared sheet scoped under `.mobile-layout` loaded by both; `platform-group.mobile.css` stops being linked from this view | `MobileViewsTests` (selects the `.Mobile` view under a real mobile User-Agent: the Create view would no longer have a `.Mobile` file); `BoardPageStaticGuardTests` (reads `platform-group.mobile.css`, line 137 of that test); `PlatformAreaLayoutTests` (the Platform layout loads the card styles) |
| M-2 | Platform Group/Delete | identical inventory; wrapper and heading level only | same four | same as M-1 | same as M-1 |
| M-3 | Platform Group/Edit | identical inventory; the mobile-only `platform-group-pages-mobile` wrapper and the 44px switch rows (`platform-group.mobile.css:192-201`) are CSS rules | same four | same as M-1; the 44px row and label-as-tap-target rule becomes a `.mobile-layout` rule in the shared sheet (A-5) | same as M-1, plus the Phase 93 board-pages guards that read `platform-group.mobile.css` |

## Relayout proposals (D-04)

Every field, link, button and action is kept, no control changes type, nothing collapses behind a toggle. Each needs sign-off with its BEFORE screenshot.

| # | Page | Proposal | BEFORE screenshot (scratch) |
|---|------|----------|------------------------------|
| R-1 | Admin/CreateUser and Admin/EditUser (desktop) | merge the bare-board duplicate `h1` and the card's `h2` into one page title in the card header (page header component, Display step); no field or action moves | `QBR/before/desktop/Admin-CreateUser.png`, `QBR/before/desktop/Admin-EditUser.png` |
| R-2 | Platform Group/Members (desktop) | stack "Current Members" and "Available Users" full width instead of two `col-lg-6` columns; at 1440px the half-width tables wrap emails and the Remove Member label onto several lines; all columns, forms and buttons stay | `QBR/before/desktop/Platform-Group-Members.png` (shows member data) |
| R-3 | Admin/Users (desktop) | stack the Email under the Name in one cell so the Actions cell gets the freed width; today the five or six action buttons wrap to a second row; every column's content and every button stays | `QBR/before/desktop/Admin-Users.png` (shows member data) |

## Empty-state copy

Verbatim, per page and layout; `{q}` stands for the user-entered search text, which is not reproduced.

| Page | Desktop | Mobile |
|------|---------|--------|
| Admin/Users | "No users found." | "No users found. Users appear here once registered." (differs, A-admin-027) |
| Admin/Quests | "No quests found." | "No quests found. Quests appear here once created by a DM." (differs, A-admin-027) |
| Admin/EmailStats | no list; state alerts: "ResendApiKey not configured — add it to appsettings.json to enable stats." and "Could not fetch email stats — Resend API returned an error. Check your API key and try again." | same view |
| Platform Group/Index | "No groups yet." and "Create the first group to get started." | same |
| Platform Group/Members | "No members in this group yet." ; search miss: "No members match "{q}"." with a "Clear search" link ; users list: "All users are already members of this group." ; search miss: "No users match "{q}"." with "Clear search" | same |
| Platform Users/Index | "No users found." | same |
| Platform LinkedBoards/Index | "No linked sets yet." and "Create a set above, then add boards of the same type to share their calendars." ; inside a set: "No boards in this set yet." and "Inactive: add at least one more board to share calendars." ; "No unlinked {One-Shot or Campaign} boards are available." | same |
| CreateUser, EditUser, ResetPassword, Group/Create, Group/Edit, Group/Delete | none (forms or confirmation) | none |

The legacy empty-state markup in this area is the shared `py-5 text-center` block with a `fa-3x text-muted` icon (no per-page empty class); `.empty-state` adoption stays with the area phase.

## Stylesheet census

Per area stylesheet (all are `*.mobile.css`, none has an `@media` rule; the word appears only in each header comment). "Re-themes Bootstrap component" counts rules whose selector
names a Bootstrap component class; "re-colours a Bootstrap `!important` utility" counts rules that name `.text-muted` or `.text-danger`. Measured by script and read by hand.

| Stylesheet | Lines | Hex literals | `rgb()`/`rgba()` literals | `!important` | Re-themes Bootstrap component | Re-colours Bootstrap `!important` utility (cascade-fight) | `transition: all` | Infinite animations | `backdrop-filter` | `text-shadow` |
|------------|-------|--------------|---------------------------|--------------|-------------------------------|------------------------------------------------------------|-------------------|---------------------|-------------------|---------------|
| `admin-form.mobile.css` | 32 | 1 | 7 | 4 | 3 (`.form-label`, `.form-check-label`, `.form-text`, `.badge`) | 0 | 0 | 0 | 1 | 4 |
| `admin-quests.mobile.css` | 40 | 1 | 10 | 4 | 1 (`.badge`) | 0 (its `small` rule out-ranks the markup's `.text-muted`) | 0 | 0 | 2 | 4 |
| `admin-users.mobile.css` | 40 | 1 | 10 | 4 | 1 (`.badge`) | 0 | 0 | 0 | 2 | 4 |
| `platform-group.mobile.css` | 98 | 8 | 19 | 12 | 9 (`.form-label`, `.form-text`, `.badge`, `.form-check`, `.form-check-label`, `.form-check-input:checked` and `:focus`) | 3 (`.text-muted` twice, `[role="alert"] .text-danger` at `:212`) | 0 | 0 | 2 | 8 |
| `platform-users.mobile.css` | 46 | 1 | 11 | 5 | 1 (`.badge`) | 2 (`.text-muted`, `.fa-3x.text-muted`) | 0 | 0 | 2 | 4 |
| `platform-linked-boards.mobile.css` | 39 | 0 | 0 | 0 | 2 (`.btn` sizing only) | 0 (one `p.text-muted` weight reset, no colour) | 0 | 0 | 0 | 0 |

Shared sheets these pages depend on (counted by the Foundation audit, listed here for the cascade section): `modern-card.css` re-themes `.btn`, `.btn-primary`, `.btn-success`, `.badge`, `.alert-*`, `.form-control`, `.form-select`,
`.form-label`, `.form-text`, `.table` and re-colours `.text-muted` and `.text-danger` with `!important` (`:102`, `:114`), repaints bare `p`, `li`, `span`, `small` (`:91-97`, the cause of A-admin-001, 002, 004), and
defines `.table` twice (A-admin-043). `site.css` sets `.btn`, the five role gradients, `.form-label`, `.text-muted` and the `navbar` rules with `!important`. The mobile glass card is defined five times (A-admin-038).
Where moving Bootstrap into a layer would change an outcome in this area: `.badge.bg-warning.text-dark` (A-admin-002) and `.text-light` on the Platform navbar user name would both resolve differently once the bare-element colour rules stop winning, which is the intended fix.

Inline markup census (views only; counts of lines):

| View | `style=` | inline `<script>` | inline `on*=` handler | `confirm(` or `alert(` |
|------|----------|-------------------|------------------------|------------------------|
| Admin/Users | 0 / 0 | 1 body block / 1 in `@section Scripts` | 1 / 1 (`onclick`) | 2 / 2 |
| Admin/Quests | 1 / 0 | 1 body block / 1 in `@section Scripts` | 1 / 1 (`onclick`) | 2 / 2 |
| Platform Group/Members | 0 / 0 | 0 / 0 | 1 / 1 (`onsubmit`) | 1 / 1 |
| Platform Users/Index | 0 / 0 | 0 / 0 | 1 / 1 (`onclick`) | 1 / 1 |
| Platform LinkedBoards/Index | 0 / 0 | 0 / 0 | 2 / 2 (`onsubmit`, generated) | 2 / 2 |
| the other 7 views | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |

(Each cell is desktop / mobile. No view in the area has a `<style>` block.) Infinite animations in the area: none; `img` without `alt`: none (BEFORE record: `imgNoAlt` empty on all 26 captures).
Text spacing (WCAG 1.4.12): passes on all 26 captures (override of line-height 1.5, letter-spacing 0.12em, word-spacing 0.16em, paragraph spacing 2em: no new horizontal scroll, no clipped text;
`QBR/audit/admin/text-spacing-probe.json`). Console and failed-request errors: none on any Admin-area page.

## Area-phase scope notes

**Owned by the Admin and Platform phase (5b), 25 view files:** the 11 files under `Views/Admin` (five twin pairs plus `EmailStats`), the 10 files under `Areas/Platform/Views/Group`, the two `Users/Index` files and the two
`LinkedBoards/Index` files; the six area stylesheets listed in the census (each retired or folded into the shared component layer as pages convert; `platform-linked-boards.mobile.css` is already clean of literals and `!important`).
Partials: none of its own.

**Findings count for the phase:** 44 findings, S1 8 (A-admin-001 to 008; 006 and 008 sit in an inline modal and a shared layout), S2 14 (009 to 022), S3 16, S4 6; of these two (008, 014) and the layout half of 029 and 030 are Foundation layout work that 5b only verifies.
The S1 items that block D-08 at merge are: the four badge and table-cell readability failures (001 to 004), the invisible mailto links on Platform phones (005), the dark-on-dark modal (006), the outline Reset Password button (007) and the unreachable Platform navbar controls at 320px (008).

**Adjustments to the draft cut:**

1. The two Platform layouts are part of the Foundation's five layouts; the 5b phase should treat A-admin-008, 014 and 029 (layout part) as inputs it verifies rather than owns.
2. A-admin-016 (Campaign status in the mobile Admin/Quests) needs the Quests area owner's knowledge of the Campaign `IsClosed` rules; keep it in 5b but have it answered with Q3 before the Quests phase fixes the shared status mapping.
3. PROJECT.md line about `_Layout.Platform.Mobile.cshtml` being dead code is stale (see "Platform layout on phones"); correct it with the milestone documentation, not in 5b code.
4. Admin/EmailStats stat tiles have never been exercised in this audit; 5b's before and after sweep should run once with a configured provider key (or a seeded stub) so the tile contrast is measured, not read from source.
5. No page is better owned by another area. Order within 5b suggestion: shared card, badge, button and table fixes first (they clear A-admin-001 to 004, 010 to 013 and 043 in one pass), then the Platform mobile shell, then per-page twin answers.

