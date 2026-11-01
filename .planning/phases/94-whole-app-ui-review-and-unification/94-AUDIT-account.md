# Phase 94 Audit: Account, pickers and access denied

Area audit for the Account views, the board picker and the access-denied page, desktop and mobile, written in Critic mode. Finding IDs run `A-account-001` onward. This is deliverable 1 for the area; it is one of the per-area files that the index report links.

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
| Account/Login | Views/Account/Login.cshtml | Views/Account/Login.Mobile.cshtml | Account and pickers (5a) | twin-content 1, a11y 10, perf 1 (total 12); also covered by shared 014, 015 | S2 | none (no empty state) | Q-account-1; M1 |
| Account/ForgotPassword | Views/Account/ForgotPassword.cshtml | Views/Account/ForgotPassword.Mobile.cshtml | Account and pickers (5a) | none as primary view (total 0); also covered by shared 002, 003, 004, 005, 006, 007, 009, 010, 011, 012, 014 | S2 (shared) | none (no empty state) | Q-account-2; M2 |
| Account/SetPassword | Views/Account/SetPassword.cshtml | Views/Account/SetPassword.Mobile.cshtml | Account and pickers (5a) | twin-drift 1 (total 1); also covered by shared 003, 004, 005, 006, 007, 009, 010, 011, 012, 013, 014 | S4 | none (no empty state) | Q-account-3; M3 |
| Account/ChangePassword | Views/Account/ChangePassword.cshtml | Views/Account/ChangePassword.Mobile.cshtml | Account and pickers (5a) | a11y 1 (total 1); also covered by shared 003, 004, 005, 006, 007, 009, 010, 011, 012, 014 | S2 | none (no empty state) | Q-account-4; M4 |
| Account/Profile | Views/Account/Profile.cshtml | Views/Account/Profile.Mobile.cshtml | Account and pickers (5a) | visual-bug 3, inconsistency 3, cascade-fight 1, twin-drift 1, twin-content 2, a11y 4, dead-code 2 (total 16); also covered by shared 002, 005, 007, 009, 010, 011, 012, 013, 015 | S1 | desktop and mobile identical: heading "No calendar subscriptions yet"; body in the Empty-state copy section | Q-account-6, 8, 9; R2 |
| Account/Edit | Views/Account/Edit.cshtml | Views/Account/Edit.Mobile.cshtml | Account and pickers (5a) | twin-content 1, a11y 2 (total 3); also covered by shared 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 014, 015, 020 | S2 | none (no empty state) | Q-account-5, 7 |
| GroupPicker/Index | Views/GroupPicker/Index.cshtml | Views/GroupPicker/Index.Mobile.cshtml | Account and pickers (5a) | inconsistency 1, a11y 4, perf 2 (total 7); also covered by shared 009, 010, 011, 012 | S1 | desktop and mobile identical: "Your account is not assigned to any group. Please contact your administrator." | Q-account-10; R1; M5 |
| Shared/AccessDenied | Views/Shared/AccessDenied.cshtml | none (served under both) | Account and pickers (5a) | visual-bug 1, inconsistency 1, a11y 1 (total 3); also covered by shared 002, 009, 010, 011, 012, 014, 020 | S2 | none (the page is itself the denial state; copy in the Empty-state copy section) | none |

Totals: 43 findings; S1 2, S2 15, S3 14, S4 12. Foundation-owned findings (marked Foundation (94)) are included in the counts above and are not area work.

## Findings

| ID | Primary view | Class | Severity | Title |
|----|--------------|-------|----------|-------|
| A-account-019 | Profile | a11y | S1 | Profile desktop: calendar-subscription meta text is `#6c757d` on glass and on the parchment cell |
| A-account-031 | GroupPicker | a11y | S1 | Board cards are clickable `div`s: a keyboard or screen-reader user cannot choose a board |
| A-account-001 | Login | a11y | S2 | Forgot-password link is Bootstrap link blue on glass |
| A-account-002 | Login | a11y | S2 | Same `btn-warning` renders as white on orange on desktop and black on gold on mobile |
| A-account-003 | Login | a11y | S2 | Validation text colour differs by layout and fails on both |
| A-account-005 | Login | a11y | S2 | Text-control borders are under 3:1 on both layouts |
| A-account-007 | Login | twin-content | S2 | Mobile Account views have an empty document title |
| A-account-010 | Login | a11y | S2 | Parchment text on glass is 4.34:1 at the lightest board pixels |
| A-account-011 | Login | a11y | S2 | Navbar and footer text under the bar on every page in the area |
| A-account-013 | ChangePassword | a11y | S2 | In-card `btn-primary` is a blue gradient that fails and differs from the mobile brown |
| A-account-016 | Edit | a11y | S2 | In-card `btn-success` (Save Changes) is under 4.5:1 |
| A-account-017 | Edit | a11y | S2 | Edit's Change Password link is an outline button in link blue on desktop and a gold button on mobile |
| A-account-020 | Profile | a11y | S2 | 70%-alpha parchment muted text is 2.99:1 at B-max |
| A-account-021 | Profile | visual-bug | S2 | Profile with subscriptions overflows at 320px on the desktop layout, and the BEFORE capture missed it |
| A-account-032 | GroupPicker | a11y | S2 | Glass card inside a glass card drops text under the bar |
| A-account-038 | AccessDenied | visual-bug | S2 | AccessDenied on the mobile layout scrolls sideways |
| A-account-039 | AccessDenied | a11y | S2 | AccessDenied: info alert is white on blue under 4.5:1; the log-in link inside the warning alert is under 4.5:1 |
| A-account-004 | Login | a11y | S3 | Validation summary is not announced and an invalid control looks like a valid one |
| A-account-006 | Login | a11y | S3 | No `autocomplete` tokens on any sign-in, reset or profile field |
| A-account-008 | Login | a11y | S3 | Mobile targets under 44px: Remember-me checkbox row and Forgot-password link |
| A-account-009 | Login | a11y | S3 | No `h1` on any page in the area; heading levels skip |
| A-account-014 | Profile | inconsistency | S3 | The commit action is a different button class on almost every page |
| A-account-022 | Profile | visual-bug | S3 | Profile mobile: the icon-only Copy button turns into a labelled button after one use and never returns |
| A-account-023 | Profile | dead-code | S3 | ~90 lines of inline script are duplicated across the Profile twins |
| A-account-024 | Profile | a11y | S3 | A visually hidden, focusable address field sits in the tab order |
| A-account-025 | Profile | inconsistency | S3 | Profile dialogs are stock white Bootstrap modals with a 32px close button |
| A-account-030 | Profile | visual-bug | S3 | Muted text with a text-shadow halo smears at body size |
| A-account-033 | GroupPicker | perf | S3 | One `backdrop-filter: blur(15px)` per board card |
| A-account-034 | GroupPicker | a11y | S3 | Picker layout has no skip link, no navigation label, a non-link brand, and a redundant `role="main"` |
| A-account-036 | GroupPicker | a11y | S3 | Logout on the picker is 87x33 and the layout is served to phones |
| A-account-041 | Profile | cascade-fight | S3 | account.mobile.css re-colours Bootstrap components and an `!important` utility |
| A-account-012 | Login | perf | S4 | Static cards lift and brighten on hover; `transition: all` on links, labels, alerts and cards |
| A-account-015 | SetPassword | twin-drift | S4 | Title icon colours drift between twins and between pages |
| A-account-018 | Edit | twin-content | S4 | Edit: the desktop view has the sentence "Want to change your password?" and the mobile view does not |
| A-account-026 | Profile | a11y | S4 | Profile desktop: field labels are `label` elements with no control |
| A-account-027 | Profile | twin-content | S4 | Profile: the desktop view says "Administrator" and the mobile view says "Admin" |
| A-account-028 | Profile | twin-content | S4 | Profile: "Has Building Key / No Building Key" on desktop, "Has building key / No building key" on mobile |
| A-account-029 | Profile | twin-drift | S4 | Profile twins differ in label punctuation, heading levels and empty-state sizing |
| A-account-035 | GroupPicker | inconsistency | S4 | The picker layout has no footer |
| A-account-037 | GroupPicker | perf | S4 | Picker layout still loads Bootstrap, Font Awesome, Cinzel and jQuery from third-party hosts |
| A-account-040 | AccessDenied | inconsistency | S4 | AccessDenied headings skip a level and the mobile `h4` is 21.6px |
| A-account-042 | Profile | dead-code | S4 | account.mobile.css has dead rules and a stale header comment |
| A-account-043 | Profile | inconsistency | S4 | Desktop calendar-subscription rules live in site.css and duplicate the mobile sheet |

### A-account-019: Profile desktop: calendar-subscription meta text is `#6c757d` on glass and on the parchment cell

- **Page and layout:** Account/Profile desktop (populated state)
- **Class:** a11y
- **Severity:** S1
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/audit/account/shots/profile-subs-desktop.png; `div.calendar-subscription-meta.mb-3` 1.17:1 at B-max (probe); in-cell `.calendar-subscription-meta` hand-computed 2.56 at B-max; site.css:1713-1716 (`color: #6c757d`), :1719-1722; Profile.cshtml:107,153
- **Proposed fix:** Outside the table `--qb-fg-muted`; inside the paper cell `--qb-ink-muted`; both at least 4.5:1
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** The "If nothing happens, copy the address..." line is grey on dark glass at 1.86 / 1.28 / 1.17 (B-med / B-99 / B-max) and the screenshot shows it barely legible. The Created / Last fetched line inside the parchment cell measures 2.32 to 2.56 by hand; the probe did not flag it, so this is a probe gap (cell background is a layered translucent fill). The BEFORE capture shows neither, because it holds the empty state only (see A-account-021).

### A-account-031: Board cards are clickable `div`s: a keyboard or screen-reader user cannot choose a board

- **Page and layout:** GroupPicker/Index desktop + mobile (rendered in _Layout.GroupPicker)
- **Class:** a11y
- **Severity:** S1
- **WCAG:** 2.1.1 Keyboard; 4.1.2 Name, Role, Value
- **Evidence:** $QBR/before/mobile/GroupPicker-Index.png, $QBR/before/desktop/GroupPicker-Index.png; Tab walk of 12 presses visits only Logout, Go to Platform, then leaves the page (`body`); 7 `[onclick]` divs, 0 `tabindex`, 0 `role`, 0 submit controls inside the SelectGroup forms; GroupPicker/Index.cshtml:44, Index.Mobile.cshtml:41 (`style="cursor:pointer" onclick="this.closest('form').submit()"`)
- **Proposed fix:** Make the card a real submit control (a `button type="submit"` carrying `.modern-card--interactive` inside the existing form, hidden `groupId` and `returnUrl` fields and POST unchanged); focus ring from the base layer
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** The picker is the only way into the app for a member of more than one board, and its only controls are not focusable. Pointer and touch users are unaffected; keyboard, switch and screen-reader users cannot proceed. The form, fields, action and the two hidden inputs stay exactly as they are, so no capability changes.

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

- **Page and layout:** Forms in the area (Login, ForgotPassword, SetPassword, ChangePassword, Edit; rename dialog on Profile)
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

### A-account-013: In-card `btn-primary` is a blue gradient that fails and differs from the mobile brown

- **Page and layout:** Account/SetPassword, ChangePassword, Profile (Copy, Open in Calendar App, Show QR Code, Edit Profile) desktop vs mobile
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/before/desktop/Account-Profile.png (Edit Profile is blue), $QBR/before/mobile/Account-Profile.png (Edit Profile is brown); modern-card.css:228-238 (`linear-gradient(135deg,#3498db,#2980b9) !important`, white label); ChangePassword.cshtml:47, SetPassword.cshtml:39
- **Proposed fix:** `--qb-action` / `--qb-action-fg` (the same fill in card and out of card, desktop and mobile)
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** White on `#3498db` is 3.15 and on `#2980b9` 4.30 (C10); the mobile layout renders the same class in brown, so the submit button of a password form changes colour with the device.

### A-account-016: In-card `btn-success` (Save Changes) is under 4.5:1

- **Page and layout:** Account/Edit desktop (mobile uses the Bootstrap default green, not scored by the probe)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/before/desktop/Account-Edit.png (Save Changes); modern-card.css:240-250 (`linear-gradient(135deg,#27ae60,#219a52) !important`, white label); Edit.cshtml:58
- **Proposed fix:** `btn-success` from `--qb-success` / `--qb-success-fg`, or `btn-primary` if the commit-action mapping in A-account-014 is adopted
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** White on `#27ae60` is 2.87 and on `#219a52` 3.62 (C11).

### A-account-017: Edit's Change Password link is an outline button in link blue on desktop and a gold button on mobile

- **Page and layout:** Account/Edit desktop vs mobile
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum); 1.4.11 Non-text Contrast
- **Evidence:** $QBR/before/desktop/Account-Edit.png; `a.btn.btn-outline-primary` 1.22:1 at B-max (before/report.json); Edit.cshtml:45 vs Edit.Mobile.cshtml:37 (`btn-warning`)
- **Proposed fix:** One button for one action: `btn-secondary` (neutral) or the commit mapping; no outline variant (UI-SPEC: outline not used)
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Same action, same destination, two different treatments, and the desktop one is text and border in `#0d6efd` on glass (1.22). Twin-drift as well as an AA failure.

### A-account-020: 70%-alpha parchment muted text is 2.99:1 at B-max

- **Page and layout:** Account/Profile desktop (empty-state paragraph) and mobile (`.account-field-label`, subscription meta, empty-state paragraph); Edit desktop; AccessDenied both
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/before/mobile/Account-Profile.png; before/report.json at B-max: `div.account-field-label` 2.99 (x4), `p.text-muted` 2.99, `div.account-field-label.calendar-subscription-meta` 2.99 (14px); `p.text-muted` 2.99 on Edit desktop and AccessDenied; modern-card.css:102-108, account.mobile.css:33-38,48-50,58-62
- **Proposed fix:** `--qb-fg-muted` (opaque, at least 4.5:1 on glass at B-max), no alpha, no halo (C3)
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Parchment at 0.7 alpha is not a muted colour at B-max, it is a failing colour (4.32 at B-med, 3.21 at B-99, 2.99 at B-max).

### A-account-021: Profile with subscriptions overflows at 320px on the desktop layout, and the BEFORE capture missed it

- **Page and layout:** Account/Profile desktop (populated state)
- **Class:** visual-bug
- **Severity:** S2
- **WCAG:** 1.4.10 Reflow
- **Evidence:** $QBR/audit/account/shots/profile-subs-desktop-320.png (scrollWidth 503 at 320); $QBR/audit/account/shots/profile-subs-desktop.png; site.css:1701-1705 (`.calendar-subscription-actions .btn` with `white-space: nowrap`), :1690-1693; Profile.cshtml:94-149
- **Proposed fix:** Table inside `.table-responsive`, or the row actions drop under the name and meta at narrow widths in the `pages` layer (Relayout R2)
- **Area phase:** Account and pickers (5a)
- **Operator question:** Y
- **Notes:** A-6 fails for the populated Profile at 320px. The manifest row is labelled `calendar-subscriptions` but the BEFORE capture shows the empty state: no subscription existed in the fixtures. For this audit one subscription was added and removed again on the Audit Board through the page's own Add and Delete forms (net zero rows); the captures sit in the scratch folder. The AFTER sweep needs this state as a fixture. Operator question is Y because the fix is a relayout (R2).

### A-account-032: Glass card inside a glass card drops text under the bar

- **Page and layout:** GroupPicker/Index desktop + mobile
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/before/mobile/GroupPicker-Index.png; probe at B-max: `h5.card-title` 3.18 and `p.card-text.text-muted` 2.35 (7 each, per board card); hand check: parchment on two stacked glass fills 4.60 / 3.41 / 3.18 and 70% parchment 3.14 / 2.48 / 2.35; Index.cshtml:19,44 (`.modern-card` inside `.modern-card`), Index.Mobile.cshtml:12,41
- **Proposed fix:** A flat row on the single panel (Relayout R1), text `--qb-fg` and `--qb-fg-muted`
- **Area phase:** Account and pickers (5a)
- **Operator question:** Y
- **Notes:** Nested glass raises the backing luminance, so the same parchment that passes in a single card fails here. Operator question is Y because the fix is the R1 relayout.

### A-account-038: AccessDenied on the mobile layout scrolls sideways

- **Page and layout:** Shared/AccessDenied mobile (twin-less view)
- **Class:** visual-bug
- **Severity:** S2
- **WCAG:** 1.4.10 Reflow
- **Evidence:** $QBR/before/mobile/Shared-AccessDenied.png; scrollWidth 416 at 412 and 394 at 390 (before/report.json `overflow390: true`); `div.row.justify-content-center` (width 420, right edge 416) and `div.col-md-6` inside `main.container-fluid.px-2` (_Layout.Mobile.cshtml:235); AccessDenied.cshtml:6-7
- **Proposed fix:** Drop the `.row`/`.col-md-6` wrapper (its -12px margins exceed the mobile container's 8px padding) and centre with the card's own max width in the `pages` layer
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** The row's negative margins push the document 4px past the viewport, so the page scrolls horizontally; the account twins avoid this because they use no `.row`. The same pattern exists on Events/Details (the other twin-less overflow in the BEFORE report), which that area's audit owns.

### A-account-039: AccessDenied: info alert is white on blue under 4.5:1; the log-in link inside the warning alert is under 4.5:1

- **Page and layout:** Shared/AccessDenied desktop + mobile (signed-in and signed-out variants)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3 Contrast (Minimum)
- **Evidence:** $QBR/before/mobile/Shared-AccessDenied.png (info alert); $QBR/audit/account/shots/accessdenied-anon-mobile.png (warning alert with link); probe `div.alert.alert-info` 3.39; modern-card.css:188-192 (`rgba(52,152,219,.9)`, white), :200-204; computed `.alert-link` `#664d03` on amber 4.21 / 4.32 / 4.35 (hand check)
- **Proposed fix:** `alert-info` from `--qb-info` / `--qb-info-fg`; alert link colour from the warning ink token
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** The signed-out variant is not in the BEFORE capture (the manifest row is the signed-in Player); it was captured for this audit in the scratch folder.

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

### A-account-014: The commit action is a different button class on almost every page

- **Page and layout:** Account/Login, ForgotPassword (warning), SetPassword, ChangePassword (primary), Edit (success), Profile (warning Add Subscription, warning Back to Quest Board, primary Edit Profile), AccessDenied (warning Back)
- **Class:** inconsistency
- **Severity:** S3
- **Evidence:** Login.cshtml:43; SetPassword.cshtml:39; ChangePassword.cshtml:47; Edit.cshtml:58; Profile.cshtml:158,168,172; AccessDenied.cshtml:41
- **Proposed fix:** Commit = `btn-primary` (`--qb-action`); cancel/back = `btn-secondary`; `btn-warning` stays edit/caution; `btn-success` stays positive confirmation (UI-SPEC role table)
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Four classes carry the same job; `Back to Quest Board` (navigation) is styled as the caution role. Which fill `--qb-action` takes is the direction sign-off (S3); the class mapping is area work.

### A-account-022: Profile mobile: the icon-only Copy button turns into a labelled button after one use and never returns

- **Page and layout:** Account/Profile mobile (populated)
- **Class:** visual-bug
- **Severity:** S3
- **Evidence:** $QBR/audit/account/shots/profile-copied-mobile.png, $QBR/audit/account/shots/profile-copy-after-mobile.png; measured width 44 then 88 (`Copied!`) then 74 (`Copy` with icon and `me-2`); Profile.Mobile.cshtml:204 (`restoreLabel` writes the desktop label markup; its three parameters are unused)
- **Proposed fix:** Restore the original icon-only markup (or toggle a class), no layout shift; keep the 44x44 target and `aria-label`
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** The mobile twin shares the desktop script verbatim, so after the first copy the 44px icon button becomes a 74px text button that wraps the action row.

### A-account-023: ~90 lines of inline script are duplicated across the Profile twins

- **Page and layout:** Account/Profile desktop + mobile
- **Class:** dead-code
- **Severity:** S3
- **Evidence:** Profile.cshtml:243-336 and Profile.Mobile.cshtml:187-280 (copy, add-lock, rename, delete-confirm); `confirm(` at Profile.cshtml:331 and Profile.Mobile.cshtml:275 (identical wording)
- **Proposed fix:** One ES module `wwwroot/js/` file loaded by both views; the delete confirmation wording and behaviour stay as they are (UI-SPEC: confirm wording unchanged)
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Two copies drift (see A-account-022). Pinned by ProfileCalendarSubscriptionTests, which assert markup markers rather than the script text, so the move does not need test rewrites beyond a check that the module is referenced.

### A-account-024: A visually hidden, focusable address field sits in the tab order

- **Page and layout:** Account/Profile desktop + mobile (populated)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 2.4.7 Focus Visible; 2.4.11 Focus Not Obscured
- **Evidence:** Probe interactive record `input|text||subscriptionAddress-...` visible 1x1 (desktop) and 1x44 (mobile); Profile.cshtml:124, Profile.Mobile.cshtml:78 (`visually-hidden`, `readonly`, no `tabindex="-1"`; source-based, keyboard walk of the populated page not run)
- **Proposed fix:** `tabindex="-1"` while hidden (the reveal path in the script already focuses it explicitly)
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** A keyboard user tabs onto an invisible control with no visible focus. The view comment explains why the field exists (clipboard-denied fallback); this finding concerns only its tab stop.

### A-account-025: Profile dialogs are stock white Bootstrap modals with a 32px close button

- **Page and layout:** Account/Profile desktop + mobile (Rename, QR code)
- **Class:** inconsistency
- **Severity:** S3
- **Evidence:** $QBR/audit/account/shots/profile-rename-mobile.png, $QBR/audit/account/shots/profile-qr-desktop.png, $QBR/audit/account/shots/profile-qr-mobile.png; computed modal background `rgb(255,255,255)`; close button 32x32; Profile.cshtml:188-241, Profile.Mobile.cshtml:132-185
- **Proposed fix:** The foundation's opaque overlay surface (`--qb-surface-overlay`) and the shared modal component; close button at `--qb-hit-min` on mobile
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** The dialogs ignore the parchment and glass identity (a white sheet over a dark page) and the close target is under 44px on mobile. Modals already live outside the card for the backdrop-filter reason documented in the view; that stays. QR renders at 240px in both; fits at 412.

### A-account-030: Muted text with a text-shadow halo smears at body size

- **Page and layout:** Account/Profile mobile (empty-state paragraph, field labels, meta)
- **Class:** visual-bug
- **Severity:** S3
- **Evidence:** $QBR/before/mobile/Account-Profile.png (the paragraph under "No calendar subscriptions yet" reads as a doubled, blurred image); account.mobile.css:33-38 (`text-shadow: 1px 1px 3px rgba(0,0,0,.9) !important` on `rgba(244,228,188,.7)`), modern-card.css:102-108 (three-layer halo on the desktop equivalent)
- **Proposed fix:** Solid `--qb-fg-muted`, halo only through `--qb-text-halo` as a supplement
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** The shadow is darker and wider than the 70%-alpha glyph can carry; legibility comes from the shadow rather than the text.

### A-account-033: One `backdrop-filter: blur(15px)` per board card

- **Page and layout:** GroupPicker/Index desktop + mobile
- **Class:** perf
- **Severity:** S3
- **Evidence:** Computed: 10 elements with a backdrop filter on desktop, 9 on mobile (7 board cards plus the panel and chrome); modern-card.css:9
- **Proposed fix:** Blur on the panel only; cards drawn with the paper surface or a plain fill
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Violates the no-repeated-blur budget, and the page is served to phones with no stylesheet of its own.

### A-account-034: Picker layout has no skip link, no navigation label, a non-link brand, and a redundant `role="main"`

- **Page and layout:** _Layout.GroupPicker.cshtml as rendered for GroupPicker/Index
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 2.4.1 Bypass Blocks; 1.3.1 Info and Relationships
- **Evidence:** _Layout.GroupPicker.cshtml:16-31 (`<nav>` without `aria-label`, brand a `span`, `<main role="main">`); probe `landmarks: {main: true, skip: false, navLabel: null}`
- **Proposed fix:** Foundation shell contract for the GroupPicker layout (UI-SPEC surface 4)
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** Shared-layout finding, owned by the foundation; the page itself needs nothing beyond it.

### A-account-036: Logout on the picker is 87x33 and the layout is served to phones

- **Page and layout:** GroupPicker/Index mobile
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 2.5.8 passes; project bar A-5 fails
- **Evidence:** $QBR/before/mobile/GroupPicker-Index.png; before/report.json `button|submit||||Logout` 87x33; _Layout.GroupPicker.cshtml:20 (`btn btn-outline-light btn-sm`, an outline variant)
- **Proposed fix:** Mobile 44px rule from the foundation shell; `btn-secondary` rather than an outline
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** Passes the 24px minimum; misses the project's 44px mobile bar. The known 390px overflow was checked and does not occur (390 and 412 both report no horizontal overflow), and the other targets on the page are 50px.

### A-account-041: account.mobile.css re-colours Bootstrap components and an `!important` utility

- **Page and layout:** account.mobile.css (all mobile area views)
- **Class:** cascade-fight
- **Severity:** S3
- **Evidence:** account.mobile.css:15-21 (`.form-label`, `.form-check-label`, `h5`, `h6` colour `!important` plus halo), :33-38 (`.form-text`, `small`), :48-50 (`.account-card-mobile .text-muted` re-colours Bootstrap's `!important` `.text-muted` utility), :53-55 (`.badge`); counts in the Stylesheet section
- **Proposed fix:** Surfaces set `color`; muted text through `--qb-fg-muted`; all of this goes in the area phase's `pages` file with no `!important`
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** 11 `!important`, 4 hex literals (all `#F4E4BC`), 14 `rgba(` calls, 7 `text-shadow` declarations in a 109-line file.

### A-account-012: Static cards lift and brighten on hover; `transition: all` on links, labels, alerts and cards

- **Page and layout:** All area views using `.modern-card` (desktop), picker cards (both)
- **Class:** perf
- **Severity:** S4
- **Evidence:** Computed `transition-property: all` on `a.text-decoration-none`, `label`, `.alert`, `.modern-card`; modern-card.css:13 (`transition: all 0.3s ease`), :16-19 (`.modern-card:hover` raises and brightens every card including the sign-in card), :157, :225, :363 (4 `transition: all` in the file; 15 in site.css)
- **Proposed fix:** Hover lift only on `.modern-card--interactive`; named properties from `--qb-dur-*`
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** The Login, Profile, Edit and AccessDenied cards move 2px when the pointer crosses them although nothing on them is a link. The UI-SPEC contract is that a static card does not move.

### A-account-015: Title icon colours drift between twins and between pages

- **Page and layout:** Account/SetPassword (desktop `text-primary`, mobile `text-warning`), Login (desktop none, mobile `text-warning`), Edit/Profile/Change
- **Class:** twin-drift
- **Severity:** S4
- **Evidence:** SetPassword.cshtml:13 vs SetPassword.Mobile.cshtml:10; Login.cshtml:13 vs Login.Mobile.cshtml:10; Edit.cshtml:13 (warning); Profile.cshtml:16 (success); ChangePassword.cshtml:14 (primary)
- **Proposed fix:** One decorative-icon rule: gold (`--qb-accent`) beside the title, or none; no blue
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Gold as a decorative icon beside text is allowed by D-17; blue `text-primary` is not a system colour. Source-only for SetPassword (no capture).

### A-account-018: Edit: the desktop view has the sentence "Want to change your password?" and the mobile view does not

- **Page and layout:** Account/Edit desktop vs mobile
- **Class:** twin-content
- **Severity:** S4
- **Evidence:** Edit.cshtml:44 (`<p class="text-muted">`) vs Edit.Mobile.cshtml:36-38 (heading and button only)
- **Proposed fix:** None until the operator answers (Q-account-7); if added to mobile it uses the muted-text token
- **Area phase:** Account and pickers (5a)
- **Operator question:** Y
- **Notes:** The only inventory difference found in the Edit pair. The mobile heading also changes element (`h5` vs a `div.account-section-heading`), which is layout-only.

### A-account-026: Profile desktop: field labels are `label` elements with no control

- **Page and layout:** Account/Profile desktop
- **Class:** a11y
- **Severity:** S4
- **WCAG:** 1.3.1 Info and Relationships
- **Evidence:** Profile.cshtml:22,29,36,62 (`<label class="col-sm-3 col-form-label">` with no `for`, values in `p.form-control-plaintext`)
- **Proposed fix:** A description list (`dl`/`dt`/`dd`) styled with the same grid; mobile already uses plain divs
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** A label not tied to a control is announced inconsistently and groups nothing.

### A-account-027: Profile: the desktop view says "Administrator" and the mobile view says "Admin"

- **Page and layout:** Account/Profile desktop vs mobile
- **Class:** twin-content
- **Severity:** S4
- **Evidence:** Profile.cshtml:42 vs Profile.Mobile.cshtml:26
- **Proposed fix:** None until the operator answers (Q-account-8)
- **Area phase:** Account and pickers (5a)
- **Operator question:** Y
- **Notes:** Same condition, same icon, different word.

### A-account-028: Profile: "Has Building Key / No Building Key" on desktop, "Has building key / No building key" on mobile

- **Page and layout:** Account/Profile desktop vs mobile
- **Class:** twin-content
- **Severity:** S4
- **Evidence:** Profile.cshtml:68,75 vs Profile.Mobile.cshtml:42,46
- **Proposed fix:** None until the operator answers (Q-account-9)
- **Area phase:** Account and pickers (5a)
- **Operator question:** Y
- **Notes:** Capitalisation differs. The Edit twins use the capitalised form ("Has Building Key", Edit.cshtml:37 and Edit.Mobile.cshtml:31).

### A-account-029: Profile twins differ in label punctuation, heading levels and empty-state sizing

- **Page and layout:** Account/Profile desktop vs mobile
- **Class:** twin-drift
- **Severity:** S4
- **Evidence:** Labels `Name:` with colon (Profile.cshtml:22) vs `Name` (Profile.Mobile.cshtml:13); section heading `h5` vs `h6` (:82 vs :51); empty state `h4` + `fa-3x` + `py-5` vs `h5` + `fa-2x` + `py-4` (:86-90 vs :55-59)
- **Proposed fix:** One heading mapping and one `.empty-state` structure for both
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Presentation only; the empty-state copy is identical on both (see Empty-state copy). Subscription block classified layout-only: desktop table and mobile rows carry the same fields and actions, and both shapes are pinned by ProfileCalendarSubscriptionTests (lines 177-200).

### A-account-035: The picker layout has no footer

- **Page and layout:** GroupPicker/Index desktop + mobile (layout)
- **Class:** inconsistency
- **Severity:** S4
- **Evidence:** _Layout.GroupPicker.cshtml (no `_Footer`); probe `footer: false` on both layouts' captures
- **Proposed fix:** None until the operator answers (Q-account-10)
- **Area phase:** Account and pickers (5a)
- **Operator question:** Y
- **Notes:** Seeded question: every other layout has a footer; the picker's does not.

### A-account-037: Picker layout still loads Bootstrap, Font Awesome, Cinzel and jQuery from third-party hosts

- **Page and layout:** _Layout.GroupPicker.cshtml
- **Class:** perf
- **Severity:** S4
- **Evidence:** _Layout.GroupPicker.cshtml:7-9,34-35
- **Proposed fix:** Foundation vendor self-hosting (D-09, D-10)
- **Area phase:** Foundation (94)
- **Operator question:** N
- **Notes:** Shared-layout finding already covered by the foundation work; listed for completeness.

### A-account-040: AccessDenied headings skip a level and the mobile `h4` is 21.6px

- **Page and layout:** Shared/AccessDenied desktop + mobile
- **Class:** inconsistency
- **Severity:** S4
- **Evidence:** AccessDenied.cshtml:10 (`h2`), :18 (`h4`); probe font size 21.636px for `h4` on mobile (not a type-scale step)
- **Proposed fix:** Heading step from the type scale (Heading 20px, 600), levels in order
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Folds into the heading rule in A-account-009.

### A-account-042: account.mobile.css has dead rules and a stale header comment

- **Page and layout:** account.mobile.css
- **Class:** dead-code
- **Severity:** S4
- **Evidence:** account.mobile.css:1 (names `Register.Mobile.cshtml`, which does not exist, and omits ForgotPassword, SetPassword and the two GroupPicker twins that load it); rules with no user in any view: `.account-subtitle` (:58-62), `.account-card-mobile .badge` (:53-55), `.account-card-mobile small` and `.form-text` (:33-38; no view in the area renders either)
- **Proposed fix:** Delete in the commit that converts the views; the comment is rewritten
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** Verified by searching every view for the class names.

### A-account-043: Desktop calendar-subscription rules live in site.css and duplicate the mobile sheet

- **Page and layout:** site.css:1675-1738 and account.mobile.css:64-109
- **Class:** inconsistency
- **Severity:** S4
- **Evidence:** site.css:1679-1683 and account.mobile.css:76-83 both define `.calendar-subscription-address`; site.css:1732-1738 and account.mobile.css:103-109 both define `.calendar-subscription-qr svg`; site.css:1713 hard-codes `#6c757d`
- **Proposed fix:** One area file in the `pages` layer loaded through `@section Styles`
- **Area phase:** Account and pickers (5a)
- **Operator question:** N
- **Notes:** These rules sit in the global stylesheet that the foundation renames to the legacy file; they travel with the Profile view when it converts. The QR sizing rule is pinned by guard tests and moves verbatim.

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

### Account/ForgotPassword: layout-only

Fields: Email (both). Form `asp-action="ForgotPassword"` (both). Buttons: `submit` "Send reset link" (`btn-warning btn-lg`, `fa-paper-plane`) on both. Links: none on either. Headings: `h2` "Forgot Password" (ViewData title) against `h5` literal. Summary `All`; `BoardPages`: none. Differences are the same wrapper, heading and col-width differences as Login. Document title missing on mobile (A-account-007, Q-account-2).

### Account/SetPassword: layout-only (source-only, not captured)

Fields: UserId and Token (hidden), NewPassword, ConfirmPassword (both). Form `asp-action="SetPassword"`. Buttons: `submit` "Set Password" (`btn-primary`, `fa-save`) on both; links: none. Headings: `h2` "Set Your Password" against `h5`; title icon `text-primary` (desktop) against `text-warning` (mobile), A-account-015. Desktop has an `<hr>` before the button, which is decoration. `BoardPages`: none. Document title missing on mobile (A-account-007, Q-account-3). This view needs an emailed token, so every visual statement about it comes from the source.

### Account/ChangePassword: layout-only

Fields: CurrentPassword, NewPassword, ConfirmPassword (both). Form `asp-action="ChangePassword"`. Links: Cancel `asp-action="Profile"` (`btn-secondary`) on both. Buttons: `submit` "Change Password" (`btn-primary`) on both. Headings: `h2` against `h5`; icon `fa-key text-primary` on both. `<hr>` before the actions on both. `BoardPages`: none. Document title missing on mobile (A-account-007, Q-account-4).

### Account/Edit: content-differs

| Inventory | Desktop | Mobile |
|-----------|---------|--------|
| Fields | Id (hidden), Name, Email (`type=email`), HasKey (checkbox, label "Has Building Key") | same four |
| Links and buttons | Change Password (`asp-action="ChangePassword"`, `btn-outline-primary`); Cancel (`asp-action="Profile"`, `btn-secondary`); submit "Save Changes" (`btn-success`) | Change Password (`btn-warning`); Cancel (`btn-secondary`); submit "Save Changes" (`btn-success`) |
| Headed sections | `h2` "Edit Profile"; `h5` "Password Management" | `h5` "Edit Profile"; `div.account-section-heading` "Password Management" |
| Text present on one side only | `p.text-muted` "Want to change your password?" (Edit.cshtml:44) | none |
| Validation | summary `ModelOnly` | summary `ModelOnly` |

Result: content-differs, one item (the sentence). The Change Password button also differs in style, which is twin-drift (A-account-017). Operator question Q-account-7; document title Q-account-5.

### Account/Profile: content-differs

| Inventory | Desktop | Mobile |
|-----------|---------|--------|
| Read-only fields | Name, Email, Account Type, Building Access (labels with a colon) | same four (labels without a colon) |
| Text that differs | "Administrator"; "Has Building Key" / "No Building Key" | "Admin"; "Has building key" / "No building key" |
| Subscription block | table with columns Subscription and Actions; per row: name, created and last-fetched line, Copy, Open in Calendar App (`webcal` href), Show QR Code, Rename, Delete (post form); one note line under the table | per-row blocks with the same name, dates and the same five actions (Copy and Show QR Code are icon-only with `aria-label`s); the note line repeats per row |
| Forms | AddCalendarSubscription, RevokeCalendarSubscription (per row), RenameCalendarSubscription (modal) | same three |
| Modals | Rename dialog; one QR dialog per row | same |
| Links | Back to Quest Board (`Home/Index`), Edit Profile (`Account/Edit`) | same two |
| Headed sections | `h2` Profile; `h5` Calendar Subscription; `h4` empty state | `h5` Profile; `h6` Calendar Subscription; `h5` empty state |

Result: content-differs because of two wording pairs (Q-account-8 and 9). The table-against-rows structure and the visible-label against icon-only difference carry the same fields and actions, so they are layout-only; both shapes are pinned by `ProfileCalendarSubscriptionTests` (the desktop table shape and the mobile `calendar-subscription-row` shape, lines 177-200). Document title Q-account-6.

### GroupPicker/Index: layout-only

Both views use `Layout = _Layout.GroupPicker.cshtml` under either User-Agent. Fields per board: hidden `groupId` and `returnUrl` plus the antiforgery token, in a form `asp-action="SelectGroup"`. Link: "Go to Platform" (`/platform`, `btn-secondary btn-lg`) shown when `Model.IsSuperAdmin`. Alert `alert-warning` ("Your account is not assigned to any group...") when `Model.HasNoGroups`. Headings: `h2` "Select Your Group" in a card header (desktop) against `h5` (mobile); per-board `h5.card-title` with a member count on both. Differences: panel chrome (card with header against `.account-card-mobile`) and grid (`row-cols-1 row-cols-md-2 row-cols-lg-3` against `row-cols-1`). `BoardPages`: none. Only the mobile view links `account.mobile.css`, through the layout's `Styles` section. This is layout-only. The layout having no footer is Q-account-10.

### Shared/AccessDenied: twin-less

One view served under both layouts (no `.Mobile` twin), so there is nothing to compare. Inventory: `h2` "Access Denied", `h4` "You Don't Have Permission", one muted paragraph, an `alert-info` message when the user is authenticated or an `alert-warning` with a "log in" link (`Account/Login`) when not, and one "Back to Quest Board" button. Branching is on `User.Identity.IsAuthenticated`; `BoardPages`: none. It is one of the six twin-less views the UI-SPEC lists, so area CSS for it may use `@media (max-width: 767.98px)`.

## Operator questions

Standard wording; every question has the same two answers and removal is never offered.

| ID | Question | Answers | Finding |
|----|----------|---------|---------|
| Q-account-1 | Account/Login: the desktop view has a document title ("Log in") and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-007 |
| Q-account-2 | Account/ForgotPassword: the desktop view has a document title ("Forgot Password") and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-007 |
| Q-account-3 | Account/SetPassword: the desktop view has a document title ("Set Your Password") and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-007 |
| Q-account-4 | Account/ChangePassword: the desktop view has a document title ("Change Password") and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-007 |
| Q-account-5 | Account/Edit: the desktop view has a document title ("Edit Profile") and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-007 |
| Q-account-6 | Account/Profile: the desktop view has a document title ("Profile") and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-007 |
| Q-account-7 | Account/Edit: the desktop view has the sentence "Want to change your password?" above the Change Password button and the mobile view does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-018 |
| Q-account-8 | Account/Profile: the desktop view has the Account Type word "Administrator" and the mobile view has "Admin". Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-027 |
| Q-account-9 | Account/Profile: the desktop view has "Has Building Key" and "No Building Key" and the mobile view has "Has building key" and "No building key". Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-028 |
| Q-account-10 | GroupPicker/Index: the main layouts have a footer and the picker layout (both views) does not. Is this deliberate? | [Deliberate, record it] [Add it to the other view] | A-account-035 |

Wording note: for Q-account-8 and Q-account-9 the item exists on both sides in different words, so "add it to the other view" means aligning the other twin's wording to the answered one; the operator picks which wording. Q-account-10 concerns one shared layout, so "the other view" is the main layouts' footer. The two questions the UI-SPEC seeds for every audit (footer text, navbar brand text) belong to the shell, not to this area, and are asked once in the index report.

## Merge proposals (D-13)

Flagged costly to reverse. Each awaits the operator's explicit approval; none is started by this audit.

Qualifying rule applied: identical or layout-only, no deliberate difference, and the mobile differences are CSS-only. The four pairs below differ only in wrapper, heading element and widths, which collapse into one markup structure the same way the four precedent views did (`Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details`).

| # | View pair | Precedent views | CSS-loading change in both layouts | MobileViewsTests facts that would change | Condition |
|---|-----------|-----------------|------------------------------------|-------------------------------------------|-----------|
| M1 | Account/Login | the four above | The merged view links one area file through `@section Styles`. The mobile layout already renders `Styles`; the desktop layout gains `@await RenderSectionAsync("Styles", required: false)` in the foundation; `account.mobile.css` drops this view from its consumers. | `MobileAccountLogin_MobileUserAgent_RendersGlassCardForm` and `MobileAccountLogin_DesktopUserAgent_DoesNotRenderGlassCard` (MobileViewsTests.cs:552-575) assert `account-card-mobile` and `account.mobile.css` on mobile and their absence on desktop; both would be rewritten to pin the same intent against the merged markup | Q-account-1 must not be answered "Deliberate" (an untitled mobile page cannot merge with a titled desktop one) |
| M2 | Account/ForgotPassword | the four above | same as M1 | no mobile-view fact pins this view (the only mobile Account facts are Login, Edit, Profile and GroupPicker); `AccountControllerIntegrationTests` run under the default User-Agent | Q-account-2 |
| M3 | Account/SetPassword | the four above | same as M1 | none (no mobile-view fact pins this view) | Q-account-3; source-only audit, so the merge must be verified with a freshly mailed token |
| M4 | Account/ChangePassword | the four above | same as M1 | none (no mobile-view fact pins this view) | Q-account-4 |
| M5 | GroupPicker/Index | the four above | `Index.Mobile.cshtml` is the only consumer of `account.mobile.css` on this page; the layout already renders `Styles`, so only the link moves; desktop has no CSS-loading change | `MobileGroupPicker_MobileUserAgent_RendersGroupCardsAndStylesSection` (MobileViewsTests.cs:619-663) asserts `account-card-mobile` and `account.mobile.css` plus the Styles-section regression; it would be rewritten to pin the same regression against the merged view; `GroupPickerControllerIntegrationTests` do not branch on User-Agent | A-account-031 and the R1 relayout land in the same change |

Not proposed: Account/Edit (content-differs until Q-account-7 and the title question are answered; reassess after the answers) and Account/Profile (content-differs, and both subscription shapes are deliberately pinned by `ProfileCalendarSubscriptionTests`; a merge would first need the operator's decisions on Q-account-8 and 9 and a rewrite of those two shape facts). Shared/AccessDenied has no twin to merge.

## Relayout proposals (D-04)

Each keeps every field, link, button and action and each control's type, collapses no section behind a toggle, and awaits sign-off with its BEFORE screenshot. Neither is started by this audit.

| # | Page | What moves | What stays | BEFORE screenshot | Findings |
|---|------|-----------|-----------|-------------------|----------|
| R1 | GroupPicker/Index desktop + mobile | the per-board cards stop being cards inside the glass panel and become flat rows on the one panel; the card becomes the row's submit control | every board entry, the hidden `groupId` and `returnUrl` inputs, the `SelectGroup` POST and antiforgery token, the Go to Platform link for super admins, the no-groups alert, the member count on each row | `$QBR/before/desktop/GroupPicker-Index.png`, `$QBR/before/mobile/GroupPicker-Index.png` (these show other boards' names; view locally only) | A-account-031, 032, 033 |
| R2 | Account/Profile desktop, populated, narrow widths | the five row actions drop beneath the subscription name and dates (reordering within the row) instead of sitting to the right of them | the table, the Subscription and Actions columns at wide widths, every action, the Add Subscription form, the QR sizing rule (pinned by guard tests) | `$QBR/audit/account/shots/profile-subs-desktop-320.png` against `$QBR/audit/account/shots/profile-subs-desktop.png` (no BEFORE capture exists for the populated state; these two were taken for this audit) | A-account-021 |

No other page in the area needs a relayout.

## Empty-state copy

Verbatim, per page and layout. Differences between twins are twin-content findings; none was found in this inventory.

| Page | Layout | Heading | Body |
|------|--------|---------|------|
| Account/Profile (no subscriptions) | desktop | No calendar subscriptions yet | Get this board's schedule onto your phone's own calendar app. The address below acts like a password — anyone who has it can see your events, dates, and times, and it never expires on its own. Press Add Subscription to create one; nothing is generated until you do. |
| Account/Profile (no subscriptions) | mobile | No calendar subscriptions yet | Get this board's schedule onto your phone's own calendar app. The address below acts like a password — anyone who has it can see your events, dates, and times, and it never expires on its own. Press Add Subscription to create one; nothing is generated until you do. |
| GroupPicker/Index (no groups) | desktop | none (alert, no heading) | Your account is not assigned to any group. Please contact your administrator. |
| GroupPicker/Index (no groups) | mobile | none (alert, no heading) | Your account is not assigned to any group. Please contact your administrator. |
| Shared/AccessDenied, signed in | both | You Don't Have Permission | You don't have permission to view this page. Alert: If you believe you should have access to this page, please contact an administrator. |
| Shared/AccessDenied, signed out | both | You Don't Have Permission | You don't have permission to view this page. Alert: Please log in to access this page. ("log in" is the link) |
| Login, ForgotPassword, SetPassword, ChangePassword, Edit | both | none | none (forms have no empty state) |

Observation for the operator, not a finding and not a rewrite: the Profile empty-state body says "The address below" while no address is shown on that page until Add Subscription is pressed. The copy stays as it is unless the operator approves a change.

## Stylesheet and markup inventory

| Item | account.mobile.css (109 lines) | Profile block in site.css (lines 1675-1738) |
|------|-------------------------------|----------------------------------------------|
| Hex literals | 4 (1 distinct, `#F4E4BC`; lines 19, 25, 42, 82) | 1 (`#6c757d`, line 1715) |
| `rgb(`/`rgba(` | 14 | 0 |
| `!important` | 11 | 0 |
| Rules re-theming a Bootstrap component class | 4 selector groups (`.form-label`, `.form-check-label`, `.form-text`, `.badge`) | 0 |
| Rules re-colouring a Bootstrap `!important` utility (cascade-fight) | 1 (`.account-card-mobile .text-muted`, line 48) | 0 |
| `transition: all` | 0 | 0 |
| Infinite animations | 0 | 0 |
| `backdrop-filter` | 1 (the `.account-card-mobile` container, once) | 0 |
| `text-shadow` declarations | 7 | 0 |
| Real `@media` rules | 0 (the single grep hit is the header comment) | 0 |

Global rules the area depends on: `modern-card.css` has 4 `transition: all` (lines 13, 157, 225, 363); `site.css` has 15. The `.modern-card` family supplies the in-card button, form, alert and element colour rules cited in the findings (lines 7-19, 78-137, 151-158, 188-204, 228-250).

Markup in the area's views: inline `style=""` 2 (`style="cursor:pointer"` at GroupPicker/Index.cshtml:44 and Index.Mobile.cshtml:41); `<style>` blocks 0; inline `<script>` blocks 2 (Profile and Profile.Mobile, about 90 lines each, identical logic); inline event handlers 2 (`onclick` on the same two picker lines); `confirm(` 2 (Profile.cshtml:331 and Profile.Mobile.cshtml:275, identical wording); `alert(` 0. Layout `_Layout.GroupPicker.cshtml` carries 5 third-party links (Bootstrap, Font Awesome, Cinzel, jQuery, Bootstrap bundle) and no inline script.

## A-11 checks (WCAG 2.2)

| Check | Account/Login | ForgotPassword | SetPassword (source-only) | ChangePassword | Notes |
|-------|---------------|----------------|---------------------------|----------------|-------|
| 3.3.8 Accessible Authentication | pass | pass | pass | pass | no `onpaste` or paste-blocking handler in any view, `site.js` or the view models; no memory, transcription or puzzle step; reset goes by emailed link. Missing `autocomplete` tokens are recorded as A-account-006 |
| 3.3.7 Redundant Entry | pass | pass | pass (exception) | pass (exception) | ConfirmPassword and CurrentPassword re-entry are security-essential, which the criterion allows |
| 1.4.12 Text Spacing | pass | pass | not rendered | pass | line-height 1.5, letter-spacing 0.12em, word-spacing 0.16em, paragraph spacing 2em injected: no clipped or overflowing text on Login, ForgotPassword, ChangePassword, Edit, Profile (empty state), AccessDenied or the picker on either layout; AccessDenied mobile keeps its baseline 4px overflow (A-account-038) |
| 1.3.5 Identify Input Purpose | n/a (Email is an account identifier, token recommended) | same | n/a | n/a | Edit's Name and Email collect the user's own details and fail it, A-account-006 |
| 2.5.7 Dragging | not applicable | | | | no dragging interaction in the area |

## Area-phase scope notes

Owned by phase 5a, Account and pickers: the six Account views with twins, `GroupPicker/Index` with its twin, `Shared/AccessDenied`, `account.mobile.css`, and the Profile block of `site.css` (lines 1675-1738). Partials rendered: none beyond the layouts (`_Toasts` is shared); `Html.LocalTime` markup on Profile stays untouched. Layouts used (`_Layout`, `_Layout.Mobile`, `_Layout.GroupPicker`) belong to the foundation; findings against them are marked Foundation (94).

Counts: S1 2 (2 area-owned), S2 15 (13 area-owned), S3 14, S4 12; 43 findings in all.

Adjustments to the draft cut:

- Keep the Account, GroupPicker and AccessDenied views together in 5a. AccessDenied's overflow (A-account-038) is a twin-less-view pattern that also occurs on Events/Details, so the Events phase should reuse the same fix rather than invent another.
- Order inside 5a: the picker first (A-account-031 blocks keyboard users from entering the app at all), then Profile (A-account-019 and 021), then the four credential pages, which share one change (button mapping, validation line, autocomplete, titles).
- Ask the foundation to deliver `.modern-card--interactive`, the opaque modal surface, `--qb-fg-muted` and `--qb-danger-text` before 5a starts, because 5a's findings 003, 013, 016, 020, 025 and 031 cannot be closed without them.
- Add to the AFTER sweep, since BEFORE lacked them: Profile with one subscription (and the QR and Rename dialogs), AccessDenied signed out, and the Login validation state. Profile populated needs a fixture subscription; the plan's own fixture step did not create one.
- Ask for the SetPassword capture path (a token from a real reset email) before 5a verifies that page; every claim about it here is from source.
- Out of scope but noticed: SetPassword enforces a minimum of 8 characters and ChangePassword a minimum of 6 (view-model attributes). That is validation, not UI, so it is left to the operator.
