# Phase 96: Account and pickers UI conversion - Context

**Gathered:** 2026-11-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Convert the way into the app and the account pages to the Phase 94 foundation: the board picker
(`GroupPicker/Index`), `Account/{Login,ForgotPassword,SetPassword,ChangePassword,Edit,Profile}`,
the twin-less `Shared/AccessDenied`, and the signed-out landing page `Home/Index` (moved here by
Q-136). The picker goes first, because its keyboard barrier (A-account-031, S1) stops a keyboard
user entering the app at all. The scope, counts, stylesheet list, guard tests, deferred items and
the ten-point "Definition of converted" with its phase-specific extras are fixed by
`.planning/ROADMAP.md` § "Phase 96" and are not repeated here.

Every content question for this area was answered at the Phase 94 G1 sign-off (2026-11-01) and is
inherited as recorded in `94-AUDIT.md` § "Operator questions for sign-off":

- Q-76 to Q-81: every Account mobile view gets the document title its desktop twin has.
- Q-82 (deliberate): the desktop Edit keeps "Want to change your password?"; the phone does not get it.
- Q-83 (deliberate): Profile says "Administrator" on desktop and "Admin" on the phone.
- Q-84: building-key wording aligned to "Has Building Key" / "No Building Key" on both.
- Q-03: the picker layout stays footerless.
- Q-103 to Q-107: Login, ForgotPassword, SetPassword, ChangePassword and GroupPicker each merge
  into one view; the GroupPicker merge lands in the same change as the R1 relayout and the
  keyboard fix.
- Q-122 (R1) and Q-123 (R2): the picker's flat-row relayout and Profile's narrow-width row actions,
  approved in principle, re-confirmed here with their AFTER.
- Q-136: `Home/Index` belongs to this phase.

Because Q-82 and Q-83 were answered "Deliberate", `Account/Edit` and `Account/Profile` stay twins:
a pair with a deliberate content difference does not meet the merge criteria.

This discussion settled the remaining implementation and process choices. Where Phase 95 already
decided the same kind of question, the same answer is used (D-01 to D-06 mirror Phase 95 D-18 to
D-24; D-07 mirrors Phase 95 D-21). No capability is added: signing in, resetting and setting a
password, editing the profile, managing calendar subscriptions and choosing a board keep working
exactly as today.

</domain>

<decisions>
## Implementation Decisions

### Relayout sign-off
- **D-01: Hybrid sign-off.** The two relayouts with no AFTER drawing, the picker's flat rows (Q-122)
  and Profile's populated narrow-width rows (Q-123), are drawn as scratch mockups and approved
  **before** they are built. No Direction A render exists for any Account page, so every other page
  is converted without a relayout and confirmed from real before/after screenshots.
- **D-02: Two operator gates.** Gate 1, early: the picker mockup, the Profile mockup, and the
  `Home/Index` merge proposal with its unified look (D-07, D-08). Gate 2, at the end: one review
  sheet with every before/after pair, desktop and phone, including the extra AFTER states the
  roadmap lists (Profile with one subscription and its QR and Rename dialogs, AccessDenied signed
  out, the Login validation state).
- **D-03: A rejected AFTER is iterated until the operator approves it.** The phase waits on it; plan
  for a possible redraw loop at either gate.

### Working baseline on this machine
- **D-04: Re-capture BEFORE screenshots here, from the current branch.** The Phase 94 scratch folder
  is not on this Windows machine and shows the pages before the foundation landed, so the current
  branch is the true BEFORE. Scratch lives outside the repository (for example
  `~/qb-ui-review/phase-96/`). The populated Profile has no BEFORE capture at all, so one is taken
  here before Profile changes.
- **D-05: The first plan opens with an operator checkpoint, as 94-01 did.** The operator confirms SQL
  Server at `localhost:1433` (never provisioned by the agent) and bootstraps the admin account. The
  executor creates, through the app's own forms (Phase 94 D-25), the accounts and fixtures the
  captures need: a player on one board, a user on several boards (the picker), a user on no board
  (the no-groups alert), a super admin (the Go to Platform link), and a Profile with one calendar
  subscription. Credentials stay in the scratch folder, never in the repository.

### Delivery
- **D-06: Work lands in slices on `milestone/v9-rolling-improvements`, with one release at the end**
  once both gates have passed. Each slice leaves every page usable and the suite green (Phase 94
  D-08). Order, per the audit: the picker (merge, R1 relayout and keyboard fix in one change), then
  Profile (A-account-019, A-account-021, R2), then the four credential pages as one change (button
  mapping, validation line, `autocomplete` tokens, titles), then Edit, AccessDenied (with the
  overflow fix that Phase 97 reuses on `Events/Details`) and `Home/Index`.

### Home/Index
- **D-07: The `Home/Index` merge goes to Gate 1 as a proposal.** The pair carries the same content
  (dice icon, "D&D Quest Board", the one sentence, the Log In button) and differs only in layout, but
  it joined the phase after G1, so the merge was never asked. Research confirms it meets the Phase 94
  D-13 criteria and lists the tests it would change; it merges only on the operator's explicit yes.
  — **Reversibility:** costly — a merge touches the view, both layouts' CSS loading and any
  mobile-view test that pins the `.Mobile` file.
- **D-08: Draw one unified look for `Home/Index` and show it at Gate 1** with the merge proposal: one
  centred glass card on both layouts with the icon, the title as the page's `h1`, the sentence at
  body contrast (not muted) and the Log In button, full width on the phone. If the operator rejects
  the merge, each twin is still converted to that look.

### SetPassword verification
- **D-09: An operator checkpoint supplies a freshly mailed SetPassword link.** At verification time
  the operator triggers Forgot Password (or Admin user creation) for a test account whose mailbox
  they can read, through the SMTP relay the app already uses, and puts the link in a file in the
  scratch folder. The link and token never enter the repository, a commit or a log. No app code is
  added to expose tokens. The link is used before it expires; a stale one is replaced at a second
  checkpoint rather than worked around.

### Profile
- **D-10: Fix the empty-state copy.** The body says "The address below acts like a password" while
  no address is shown until Add Subscription is pressed. Reword only that clause so it no longer
  points at something absent (for example "The address you create acts like a password"); the rest
  of the sentence, and its warning that anyone with the address can see the events, stays. The same
  text on both twins. The exact wording is shown at Gate 2.
- **D-11: Replace the Delete confirmation's `confirm()` with the foundation's opaque modal**, on both
  twins, keeping today's message wording and today's outcome (cancel does nothing; confirm submits
  the same revoke POST). The dialog takes focus on open, traps it, returns it to the Delete button on
  close, and closes on Escape. It moves out of the inline script with the rest of the Profile script.

### Claude's Discretion
- How the picker row becomes the submit control (a button per board inside the existing per-board
  form, or equivalent), provided every board entry, the hidden `groupId` and `returnUrl`, the
  antiforgery token, the member count, the no-groups alert and Go to Platform stay.
- The account sheet's name and how `account.mobile.css`, `group-picker.css` and the Profile block of
  `legacy-pages.css` are consolidated, within the `@section Styles` rules in the roadmap.
- Module structure for the ~90 lines of duplicated Profile script (one shared ES module for both
  twins), within `.claude/ui-guidelines.md` § "No inline code".
- The button-class mapping for the commit action on each page (A-account-024), from the UI-SPEC's
  component contract, and aligning the Edit Change Password button between twins (A-account-017).
- Exact slice boundaries and plan count.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and inherited answers
- `.planning/ROADMAP.md` § "Phase 96: Account and pickers UI conversion": scope, counts, stylesheet
  list, `@section Styles` moves, guard tests to rewrite, inherited decisions, approved merges and
  relayouts with their BEFORE paths, the ten-point Definition of converted and the phase extras.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md` § "Operator questions for
  sign-off" (Q-03, Q-76 to Q-84, Q-103 to Q-107, Q-122, Q-123, Q-136 and the G1 sign-off record)
  and § "Phase 5a: Account and pickers".
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-account.md`: all 43 findings
  this phase owns (A-account-001 onward), the twin comparison, merge proposals M1 to M5, relayout
  proposals R1 and R2, empty-state copy, stylesheet and markup inventory, A-11 checks and the
  area-phase scope notes (order inside the phase, AFTER-sweep additions, SetPassword capture path).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-CONTEXT.md`: D-04 (relayout rules),
  D-08 (usable between merges), D-12 (inline code), D-13 to D-15 (twins, merges, mobile
  verification), D-16 and D-17 (accessibility bar, signature colours), D-25 (fixture writes).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-UI-SPEC.md`: the design contract the
  foundation implements, including the GroupPicker layout row (served to phones, its own responsive
  rules in the `pages` layer) and rule A7 (`@media` only in twin-less and shared views).
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-CONTEXT.md` D-18 to D-24: the process
  decisions this phase mirrors.

### UI system and project rules
- `.claude/ui-guidelines.md`: tokens and layers, components, colour, accessibility checklist, mobile
  twins, no inline code (`@section Styles` / `@section Scripts`, ES modules), vendor files, dates.
- `.claude/architecture.md` § "Time and the board clock": `Html.LocalTime` on Profile stays untouched.
- `CLAUDE.md`: branching, no planning IDs in source or CSS comments, local database contract.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Foundation sheets `wwwroot/css/tokens.css`, `base.css`, `components.css`, `shell.css`,
  `shell.mobile.css`, with `.modern-card--interactive`, the opaque modal surface, `--qb-fg-muted`
  and `--qb-danger-text`, which this phase's findings 003, 013, 016, 020, 025 and 031 depend on.
- `group-picker.css` is already layered and clean.

### Established Patterns
- `Views/GroupPicker/Index.cshtml:44` and `Index.Mobile.cshtml:41`: the board card is a `div` with
  `style="cursor:pointer"` and an inline `onclick` that submits the form; this is the keyboard
  barrier.
- `Views/Account/Profile.cshtml:331` and `Profile.Mobile.cshtml:275`: the identical `confirm()` call
  inside ~90 lines of inline script duplicated across the twins.
- `Views/Home/Index.cshtml` (card, `h2`, `lead` sentence, `hr`, `btn-warning btn-lg` Log In) and
  `Index.Mobile.cshtml` (bare stack, 4x icon, `h3`, `text-muted` sentence, `d-grid` button).
- SetPassword links are generated in `Controllers/Admin/AdminController.cs` and
  `Areas/Platform/Controllers/GroupController.cs` (user creation) and by the forgot-password flow,
  and are sent by `ForgotPasswordEmailJob` / `WelcomeEmailJob` through SMTP; the token is not logged.

### Integration Points
- `MobileViewLocationExpander` selects `.Mobile.cshtml` views by User-Agent; merged pairs drop their
  `.Mobile` file. The picker layout `_Layout.GroupPicker.cshtml` is served under both User-Agents.
- `ProfileCalendarSubscriptionTests` pins both subscription shapes deliberately (desktop table,
  mobile `calendar-subscription-row`); `CalendarSubscriptionStaticGuardTests` pins the QR sizing rule.
- `GroupPickerControllerIntegrationTests` and `CrossBoardPickerSkipTests` must stay green and do not
  branch on User-Agent.

</code_context>

<specifics>
## Specific Ideas

- The operator expects answers already given for Phase 95 to carry over to this phase rather than be
  asked again; the process decisions here are taken from Phase 95 for that reason.

</specifics>

<deferred>
## Deferred Ideas

- SetPassword requires at least 8 characters and ChangePassword at least 6 (view-model attributes).
  This is validation, not UI, and is left to the operator as a possible later fix; this phase does
  not change it.

</deferred>

---

*Phase: 96-account-and-pickers-ui-conversion*
*Context gathered: 2026-11-10*
