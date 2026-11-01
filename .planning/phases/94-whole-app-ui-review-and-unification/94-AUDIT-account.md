# Phase 94 Audit: Account, pickers and access denied

Area audit for the Account views, the board picker and the access-denied page, desktop and mobile, written in Critic mode. Finding IDs run `A-account-001` onward. This is deliverable 1 for the area; it is one of the per-area files that the index report links.

> Status: tracer version. Account Login is audited completely; the other area views are added by the second task.

## Scope

- `$QBR` below stands for `~/qb-ui-review/phase-94` (scratch, outside the repository). Screenshots are referenced by that path only and are not committed.
- Captures read: `$QBR/before/{desktop,mobile}/<id>.png` and `$QBR/before/report.json` (frozen baseline, port 8001, mobile pass under the Pixel 7 descriptor so the real `.Mobile.cshtml` and mobile layout are selected). Extra captures taken for this audit, because the BEFORE set misses states, are under `$QBR/audit/account/shots/` (probe outputs `probe1.json`, `probe2.json`, `probe3.json` beside them).
- Views: `Views/Account/{Login,ForgotPassword,SetPassword,ChangePassword,Profile,Edit}` with `.Mobile` twins, `Views/GroupPicker/Index` with `.Mobile` (both rendered inside `_Layout.GroupPicker` under either User-Agent), and the twin-less `Views/Shared/AccessDenied`. Stylesheet: `account.mobile.css` plus the global rules these pages rely on in `site.css`, `modern-card.css` and `mobile.css`.
- Method: critique checklist items 1-12 against the BEFORE captures; rendered-page probes (contrast over B-med, B-99 and B-max, interactive inventory, accessible names, focus walk) from `before/report.json`; contrast re-measured by hand with `contrast.mjs` where the probe does not score the element (buttons, the anonymous AccessDenied variant, the table cell); live probes against the baseline app on port 8001 for validation states, keyboard walk, text spacing, reflow and the populated Profile; source read of every view and `account.mobile.css`.
- Gaps in the BEFORE set, handled here: `Account-SetPassword` needs an emailed token and was not captured (source-only review, marked wherever it matters); `Account-Profile` was captured in the empty state only (the populated state was captured for this audit); the signed-out AccessDenied variant and the validation states were not captured (captured here).
- Not touched: no shared account credential, session or storage state was changed. Validation states were produced with an empty submit from a throwaway context; one calendar subscription was added to the Audit Player and deleted again through the page's own forms; the picker was viewed but no board was selected. The picker lists other boards: its text is not quoted anywhere in this report.
- Severity: S1 text hidden or control unreachable (blocks D-08); S2 visibly wrong or fails the AA bar; S3 inconsistency with the system; S4 polish. Index counts are by the view where the evidence was taken; a finding that spans several views lists them under Page and layout. Stylesheet findings are counted under Account/Profile, its largest consumer.

## Index

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|--------------------------|-------------------------|
| Account/Login | Views/Account/Login.cshtml | Views/Account/Login.Mobile.cshtml | Account and pickers (5a) | twin-content 1, a11y 10, perf 1 (total 12) | S2 | none (no empty state) | Q-account-1 |

## Findings

| ID | Primary view | Class | Severity | Title |
|----|--------------|-------|----------|-------|
| A-account-001 | Login | a11y | S2 | Forgot-password link is Bootstrap link blue on glass |
| A-account-002 | Login | a11y | S2 | Same `btn-warning` renders as white on orange on desktop and black on gold on mobile |
| A-account-003 | Login | a11y | S2 | Validation text colour differs by layout and fails on both |
| A-account-005 | Login | a11y | S2 | Text-control borders are under 3:1 on both layouts |
| A-account-007 | Login | twin-content | S2 | Mobile Account views have an empty document title |
| A-account-010 | Login | a11y | S2 | Parchment text on glass is 4.34:1 at the lightest board pixels |
| A-account-011 | Login | a11y | S2 | Navbar and footer text under the bar on every page in the area |
| A-account-004 | Login | a11y | S3 | Validation summary is not announced and an invalid control looks like a valid one |
| A-account-006 | Login | a11y | S3 | No `autocomplete` tokens on any sign-in, reset or profile field |
| A-account-008 | Login | a11y | S3 | Mobile targets under 44px: Remember-me checkbox row and Forgot-password link |
| A-account-009 | Login | a11y | S3 | No `h1` on any page in the area; heading levels skip |
| A-account-012 | Login | perf | S4 | Static cards lift and brighten on hover; `transition: all` on links, labels, alerts and cards |

### A-account-001: Forgot-password link is Bootstrap link blue on glass

- **Page and layout:** Account/Login desktop + mobile
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/before/desktop/Account-Login.png and $QBR/before/mobile/Account-Login.png; `form a.text-decoration-none`; Login.cshtml:39, Login.Mobile.cshtml:33; before/report.json probe `a.text-decoration-none` 1.22:1 at B-max
- **Proposed fix:** Use the link token `--qb-link` (parchment-family, at least 4.5:1 on glass at B-max); keep the underline so the link is not colour-only
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** `#0d6efd` on glass composited over the board measures 1.94 at B-med, 1.33 at B-99 and 1.22 at B-max (hand check with contrast.mjs agrees with the probe). This is the only route to the password-reset flow from the sign-in page, and its ratio is among the lowest recorded on the page. Nothing in `modern-card.css` restyles anchors (the only anchor rule is `.modern-card .form-text a`, modern-card.css:142), so every plain link inside a card inherits Bootstrap blue.

### A-account-002: Same `btn-warning` renders as white on orange on desktop and black on gold on mobile

- **Page and layout:** Account/Login desktop + mobile; also ForgotPassword, Profile (Add Subscription, Back to Quest Board, Rename), AccessDenied (Back to Quest Board)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/before/desktop/Account-Login.png vs $QBR/before/mobile/Account-Login.png; `form button[type=submit].btn-warning`; site.css:682-688 (`linear-gradient(45deg,#f39c12,#e67e22)`, `color: white !important`); computed on mobile `rgb(255,193,7)` with black label (Bootstrap default); Login.cshtml:43, Login.Mobile.cshtml:37
- **Proposed fix:** One commit-action token pair `--qb-action` / `--qb-action-fg` (UI-SPEC S3), label at least 4.5:1 in rest, hover, active and focus-visible; `btn-warning` keeps its near-black-on-amber caution role
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** White on the desktop gradient is 2.19 at the `#f39c12` stop and 2.85 at the `#e67e22` stop (C8). On the mobile layout the same class is gold with a black label (12.88:1). The probe does not score button labels, so these ratios are computed from the computed styles. This is the page's call to action and it differs in colour, weight and text-shadow between the twins (twin-drift).

### A-account-003: Validation text colour differs by layout and fails on both

- **Page and layout:** Account/Login, ForgotPassword, SetPassword, ChangePassword, Edit (desktop + mobile)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum); 1.4.1 Use of Color
- **Evidence:** $QBR/audit/account/shots/login-validation-desktop.png and $QBR/audit/account/shots/login-validation-mobile.png (empty submit, no credentials sent); `.validation-summary-errors`, `.field-validation-error`; modern-card.css:114-120 (`#ff6b6b !important` plus 3-layer halo); mobile has no rule, so Bootstrap `.text-danger` `#dc3545` applies; Login.Mobile.cshtml:12,18,24
- **Proposed fix:** `--qb-danger-text` inside a `role="alert"` wrapper with the icon-plus-sentence pattern, one value for both layouts, ratio at least 4.5:1 with the halo removed
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Desktop `#ff6b6b` measures 3.14 / 2.15 / 1.97 (B-med / B-99 / B-max); mobile `#dc3545` measures 1.93 / 1.32 / 1.21 and has no halo. Both fail; desktop only reads because of its halo, mobile reads only at the darker parts of the board. The message carries no icon, so an error is also colour-led (A-10).

### A-account-005: Text-control borders are under 3:1 on both layouts

- **Page and layout:** All forms in the area (Login, ForgotPassword, SetPassword, ChangePassword, Edit; rename dialog on Profile)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.11 Non-text Contrast
- **Evidence:** $QBR/before/desktop/Account-Login.png, $QBR/before/mobile/Account-Login.png; `.form-control`; modern-card.css:151-158 (`border: 1px solid rgba(0,0,0,.3) !important` over a 95%-white fill); mobile `#dee2e6` on white
- **Proposed fix:** `--qb-control-border` at least 3:1 against the control's own surface (C14)
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Desktop control border against its near-white fill measures 2.11:1; mobile `#dee2e6` on white measures 1.30:1. The field boundary is the only non-text cue that a field exists.

### A-account-007: Mobile Account views have an empty document title

- **Page and layout:** Account/Login, ForgotPassword, SetPassword, ChangePassword, Edit, Profile (mobile only)
- **Class:** twin-content
- **Severity:** S2
- **WCAG:** 2.4.2 Page Titled
- **Evidence:** curl of /Account/Login and /Account/ForgotPassword under a mobile User-Agent returns `<title> - D&D Quest Board</title>`; the desktop twin returns `<title>Log in - D&D Quest Board</title>`; no `ViewData["Title"]` in any Account `.Mobile.cshtml` (the two GroupPicker twins and AccessDenied do set it); _Layout.Mobile.cshtml:10
- **Proposed fix:** Set `ViewData["Title"]` in each mobile view to the same string the desktop twin uses (this is a question, see Q-account-1 to 6)
- **Area phase:** Account and pickers (5a)
- **Operator question:** Y
- **Notes:** Under a mobile User-Agent the tab, history entry and screen-reader page announcement for six pages read as just the site name. The desktop twin names each page. Whether to add it is the operator's call under D-14; failing the criterion is the reason it is S2.

### A-account-010: Parchment text on glass is 4.34:1 at the lightest board pixels

- **Page and layout:** All area views (labels, field values, headings, checkbox labels)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** before/report.json probe at B-max: `label.form-label` 4.34, `label.form-check-label` 4.34, `h5`/`h6` 4.34, `p.form-control-plaintext` 4.34, `div.account-field-value` 4.34; modern-card.css:78-137 (element-enumerated cream rules), account.mobile.css:15-21
- **Proposed fix:** `.modern-card { color: var(--qb-fg) }` with the surface-owned colour; scrim strength at direction sign-off so `--qb-fg` passes 4.5 at B-max (C2)
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** Fails the bar by 0.16 only on the lightest 0.1% of the board (4.75 at B-99, 6.92 at B-med), so it is a foundation decision, not a per-page edit. Listed here so the area phase verifies it after the foundation lands.

### A-account-011: Navbar and footer text under the bar on every page in the area

- **Page and layout:** Shared layouts (_Layout, _Layout.Mobile) as rendered on all area views
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** before/report.json at B-max: `a.nav-link` and `a#navbarDropdown` 3.78 (15.2px 700), `span.navbar-text` 3.63 desktop and 2.56 mobile (`rgba(255,255,255,.55)`); $QBR/before/desktop/Account-Profile.png footer, $QBR/before/mobile/Account-Login.png footer
- **Proposed fix:** Foundation: `--qb-fg` on `--qb-surface-chrome` for the bar, `--qb-fg` for footer text (C7); no area work
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** Shared partial finding, owned by the foundation shell work; recorded because it appears on every area capture. The mobile footer text (2.56) is the lowest value in the area outside the picker.

### A-account-004: Validation summary is not announced and an invalid control looks like a valid one

- **Page and layout:** Account/Login, ForgotPassword, SetPassword, ChangePassword, Edit (desktop + mobile)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 4.1.3 Status Messages; 3.3.1 Error Identification; 1.4.11 Non-text Contrast
- **Evidence:** $QBR/audit/account/shots/login-validation-desktop.png; `div[asp-validation-summary]` has no `role` or live region (Login.cshtml:19, Login.Mobile.cshtml:12); computed border of `input.input-validation-error` unchanged (`rgba(0,0,0,.3)` desktop, `#dee2e6` mobile)
- **Proposed fix:** Wrap the summary in the refusal-line pattern (`role="alert"`, icon, `--qb-danger-text`) and give `.input-validation-error` a border from `--qb-danger` at 3:1 or better
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** After a failed submit a screen-reader user is not told that anything failed, and the invalid field has no visual distinction of its own; the only cue is the red sentence beneath it.

### A-account-006: No `autocomplete` tokens on any sign-in, reset or profile field

- **Page and layout:** Account/Login, ForgotPassword, SetPassword, ChangePassword, Edit (desktop + mobile)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 1.3.5 Identify Input Purpose (AA); 3.3.8 Accessible Authentication (supporting)
- **Evidence:** Rendered HTML of /Account/Login and /Account/ForgotPassword under both User-Agents; `input` list shows `Email:none, Password:none`; Login.cshtml:23,29; Edit.cshtml:25,31; SetPassword.cshtml:26,32; ChangePassword.cshtml:24,30,36
- **Proposed fix:** Add the matching tokens in markup: `username` or `email`, `current-password`, `new-password`, `name`; no new colour or class
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** 3.3.8 itself passes (see the A-11 table: paste is not blocked and there is no cognitive test), but Edit's Name and Email fields collect the user's own details and need an input purpose, and password managers fill more reliably with the tokens. The tag helper does not emit `autocomplete`, so this is a markup edit.

### A-account-008: Mobile targets under 44px: Remember-me checkbox row and Forgot-password link

- **Page and layout:** Account/Login mobile; Account/Edit mobile (Has Building Key checkbox)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 2.5.8 Target Size (Minimum) passes; project bar A-5 fails
- **Evidence:** $QBR/before/mobile/Account-Login.png; before/report.json interactive: `RememberMe` 16x16, `Forgot password?` 133x22 (Login mobile), `HasKey` 16x16 (Edit mobile); Login.Mobile.cshtml:27-34
- **Proposed fix:** The Phase 93 whole-row tap target for `.form-check` (label as the row) and a padded link, both at `--qb-hit-min`
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** The label text is clickable (about 118x24), the box itself is 16x16 and the link is 22px tall. They pass the 24px WCAG minimum through the spacing exception but miss the project's 44px mobile bar (A-5).

### A-account-009: No `h1` on any page in the area; heading levels skip

- **Page and layout:** All area views (desktop + mobile)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 1.3.1 Info and Relationships; 2.4.6 Headings and Labels
- **Evidence:** Probe of heading tags: Login desktop `H2:Log in`; Login mobile `H5:Quest Board` (offcanvas title) then `H5:Log in`; Profile desktop h2, h5, h4; Profile mobile h5, h6, h5; GroupPicker h2 then h5; AccessDenied h2 then h4; Login.cshtml:12, Login.Mobile.cshtml:10
- **Proposed fix:** One `h1` per page carrying the Display step (the UI-SPEC heading mapping makes this a CSS change), with section headings stepping down one level at a time
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** No page has an `h1`, the mobile pages start at `h5`, and the first heading a mobile screen-reader user meets is the layout's offcanvas title. The visual sizes come from classes, so reordering levels does not change how anything looks once the base heading mapping lands.

### A-account-012: Static cards lift and brighten on hover; `transition: all` on links, labels, alerts and cards

- **Page and layout:** All area views using `.modern-card` (desktop), picker cards (both)
- **Class:** perf
- **Severity:** S4
- **Evidence:** Computed `transition-property: all` on `a.text-decoration-none`, `label`, `.alert`, `.modern-card`; modern-card.css:13 (`transition: all 0.3s ease`), :16-19 (`.modern-card:hover` raises and brightens every card including the sign-in card), :157, :225, :363 (4 `transition: all` in the file; 15 in site.css)
- **Proposed fix:** Hover lift only on `.modern-card--interactive`; named properties from `--qb-dur-*`
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** The Login, Profile, Edit and AccessDenied cards move 2px when the pointer crosses them although nothing on them is a link. The UI-SPEC contract is that a static card does not move.

## Twin comparison

Procedure: extract from both files the form fields (`asp-for`, `name=`), links and buttons (`asp-action`, `href`, `type="submit"`), headed sections (`h1`-`h6`, card blocks) and the `BoardPages` conditions guarding them, then compare. No view in the area contains a `BoardPages` condition (searched `BoardPages`, `Allows(` and `Serves(`: none), so that row is empty for every pair. No deliberate difference was pre-recorded for this area: none is commented in a view or recorded in PROJECT.md for these pages.

### Account/Login: layout-only

| Inventory | Desktop (Login.cshtml) | Mobile (Login.Mobile.cshtml) |
|-----------|------------------------|------------------------------|
| Fields (`asp-for`) | Email, Password, RememberMe (checkbox) | Email, Password, RememberMe (checkbox) |
| Form | `asp-action="Login"`, `asp-route-returnurl` | same |
| Links and buttons | `asp-action="ForgotPassword"` link "Forgot password?"; `button type=submit` "Log in" (`btn-warning btn-lg`, icon) | same link; same submit (`btn-warning btn-lg`, icon) |
| Headed sections | one card: `div.card-header` with `h2` (text from `ViewData["Title"]`), `card-body` | one glass block `div.account-card-mobile` with `h5` (literal "Log in") |
| Validation | summary `All` inside the form before the first field; per-field `asp-validation-for` | summary `All` before the form; per-field `asp-validation-for` |
| `BoardPages` conditions | none | none |

Result: the control, link, button and heading inventory is the same; the differences are wrapper (card with header against glass block), heading element (`h2` against `h5`), the summary's position relative to the form, and the `.col-md-6 .col-lg-4` width wrapper. That is layout-only. One non-inventory difference exists and is recorded separately: the document `<title>` is set on desktop and missing on mobile (A-account-007, Q-account-1).

## Operator questions

Standard wording; every question has the same two answers and removal is never offered.

| ID | Question | Answers | Finding |
|----|----------|---------|---------|
| Q-account-1 | Account/Login: the desktop view has a document title ("Log in") and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-007 |

Wording note: for Q-account-8 and Q-account-9 the item exists on both sides in different words, so "add it to the other view" means aligning the other twin's wording to the answered one; the operator picks which wording. Q-account-10 concerns one shared layout, so "the other view" is the main layouts' footer. The two questions the UI-SPEC seeds for every audit (footer text, navbar brand text) belong to the shell, not to this area, and are asked once in the index report.

## Merge proposals (D-13)

Pending: completed with the remaining area views.

## Relayout proposals (D-04)

Pending: completed with the remaining area views.

## Empty-state copy

Pending: completed with the remaining area views.

## Area-phase scope notes

Pending: completed with the remaining area views.
