# Phase 96: Account and pickers UI conversion - Research

**Researched:** 2026-11-10
**Domain:** Razor view conversion onto the Phase 94 CSS foundation (layers, tokens, UA-selected `.Mobile.cshtml` twins), static/guard tests that read views and sheets, small vanilla-JS extraction
**Confidence:** HIGH for the in-repo inventory and guard-test constraints (every file below was opened this session); MEDIUM for the design recommendations that wait on the Gate 1 mockups; the few LOW items are in the Assumptions Log.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
Copied verbatim from `96-CONTEXT.md` `## Implementation Decisions` (the inherited operator answers in `<domain>` are summarised in the phase-requirements table below).

- **D-01: Hybrid sign-off.** The two relayouts with no AFTER drawing, the picker's flat rows (Q-122) and Profile's populated narrow-width rows (Q-123), are drawn as scratch mockups and approved **before** they are built. No Direction A render exists for any Account page, so every other page is converted without a relayout and confirmed from real before/after screenshots.
- **D-02: Two operator gates.** Gate 1, early: the picker mockup, the Profile mockup, and the `Home/Index` merge proposal with its unified look (D-07, D-08). Gate 2, at the end: one review sheet with every before/after pair, desktop and phone, including the extra AFTER states the roadmap lists (Profile with one subscription and its QR and Rename dialogs, AccessDenied signed out, the Login validation state).
- **D-03: A rejected AFTER is iterated until the operator approves it.** The phase waits on it; plan for a possible redraw loop at either gate.
- **D-04: Re-capture BEFORE screenshots here, from the current branch.** The Phase 94 scratch folder is not on this Windows machine and shows the pages before the foundation landed, so the current branch is the true BEFORE. Scratch lives outside the repository (for example `~/qb-ui-review/phase-96/`). The populated Profile has no BEFORE capture at all, so one is taken here before Profile changes.
- **D-05: The first plan opens with an operator checkpoint, as 94-01 did.** The operator confirms SQL Server at `localhost:1433` (never provisioned by the agent) and bootstraps the admin account. The executor creates, through the app's own forms (Phase 94 D-25), the accounts and fixtures the captures need: a player on one board, a user on several boards (the picker), a user on no board (the no-groups alert), a super admin (the Go to Platform link), and a Profile with one calendar subscription. Credentials stay in the scratch folder, never in the repository.
- **D-06: Work lands in slices on `milestone/v9-rolling-improvements`, with one release at the end** once both gates have passed. Each slice leaves every page usable and the suite green (Phase 94 D-08). Order, per the audit: the picker (merge, R1 relayout and keyboard fix in one change), then Profile (A-account-019, A-account-021, R2), then the four credential pages as one change (button mapping, validation line, `autocomplete` tokens, titles), then Edit, AccessDenied (with the overflow fix that Phase 97 reuses on `Events/Details`) and `Home/Index`.
- **D-07: The `Home/Index` merge goes to Gate 1 as a proposal.** The pair carries the same content (dice icon, "D&D Quest Board", the one sentence, the Log In button) and differs only in layout, but it joined the phase after G1, so the merge was never asked. Research confirms it meets the Phase 94 D-13 criteria and lists the tests it would change; it merges only on the operator's explicit yes. — **Reversibility:** costly — a merge touches the view, both layouts' CSS loading and any mobile-view test that pins the `.Mobile` file.
- **D-08: Draw one unified look for `Home/Index` and show it at Gate 1** with the merge proposal: one centred glass card on both layouts with the icon, the title as the page's `h1`, the sentence at body contrast (not muted) and the Log In button, full width on the phone. If the operator rejects the merge, each twin is still converted to that look.
- **D-09: An operator checkpoint supplies a freshly mailed SetPassword link.** At verification time the operator triggers Forgot Password (or Admin user creation) for a test account whose mailbox they can read, through the SMTP relay the app already uses, and puts the link in a file in the scratch folder. The link and token never enter the repository, a commit or a log. No app code is added to expose tokens. The link is used before it expires; a stale one is replaced at a second checkpoint rather than worked around.
- **D-10: Fix the empty-state copy.** The body says "The address below acts like a password" while no address is shown until Add Subscription is pressed. Reword only that clause so it no longer points at something absent (for example "The address you create acts like a password"); the rest of the sentence, and its warning that anyone with the address can see the events, stays. The same text on both twins. The exact wording is shown at Gate 2.
- **D-11: Replace the Delete confirmation's `confirm()` with the foundation's opaque modal**, on both twins, keeping today's message wording and today's outcome (cancel does nothing; confirm submits the same revoke POST). The dialog takes focus on open, traps it, returns it to the Delete button on close, and closes on Escape. It moves out of the inline script with the rest of the Profile script.

Inherited operator answers (CONTEXT `<domain>`): Q-76 to Q-81 every Account mobile view gets the document title its desktop twin has; Q-82 (deliberate) desktop Edit keeps "Want to change your password?", the phone does not get it; Q-83 (deliberate) Profile says "Administrator" on desktop and "Admin" on the phone; Q-84 building-key wording aligned to "Has Building Key" / "No Building Key" on both; Q-03 the picker layout stays footerless; Q-103 to Q-107 Login, ForgotPassword, SetPassword, ChangePassword and GroupPicker each merge into one view, the GroupPicker merge landing in the same change as the R1 relayout and the keyboard fix; Q-122 (R1) and Q-123 (R2) relayouts approved in principle, re-confirmed with their AFTER; Q-136 `Home/Index` belongs to this phase. Because Q-82 and Q-83 were "Deliberate", `Account/Edit` and `Account/Profile` stay twins.

### Claude's Discretion
- How the picker row becomes the submit control (a button per board inside the existing per-board form, or equivalent), provided every board entry, the hidden `groupId` and `returnUrl`, the antiforgery token, the member count, the no-groups alert and Go to Platform stay.
- The account sheet's name and how `account.mobile.css`, `group-picker.css` and the Profile block of `legacy-pages.css` are consolidated, within the `@section Styles` rules in the roadmap.
- Module structure for the ~90 lines of duplicated Profile script (one shared ES module for both twins), within `.claude/ui-guidelines.md` § "No inline code".
- The button-class mapping for the commit action on each page (A-account-024), from the UI-SPEC's component contract, and aligning the Edit Change Password button between twins (A-account-017).
- Exact slice boundaries and plan count.

### Deferred Ideas (OUT OF SCOPE)
- SetPassword requires at least 8 characters and ChangePassword at least 6 (view-model attributes). This is validation, not UI, and is left to the operator as a possible later fix; this phase does not change it.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are mapped to Phase 96 (`Requirements: TBD` in ROADMAP). The requirement set is therefore the ROADMAP "Definition of converted" (DoC 1-10) plus the phase extras, which the planner should cite as `DoC-n` / `X-n`.

| ID | Description | Research Support |
|----|-------------|------------------|
| DoC-1 | Zero inline `style=""`, `<style>`, `<script`, inline handlers in the area's views | Inventory below: 2 `style=` + 2 `onclick` (picker twins), 2 inline `<script>` (Profile twins). Module plan, modal plan. |
| DoC-2 | Zero hex / `rgb()` / `!important` in the area's sheets | `account.mobile.css` 3 hex / 14 rgb / 11 important; Profile block 1 hex; plan: one layered `account.css`, add to `UiFoundationLayerGuardTests` lists. |
| DoC-3 | Area sheet loaded via `@section Styles` | Both layouts render `Styles` (verified). Merged views link `account.css`; picker keeps its layout-linked `group-picker.css`. |
| DoC-4 | Twins aligned, answers applied, approved merges done | Twin table + Q-76..Q-84 application list. |
| DoC-5 | A-1..A-11 verified per page, both layouts | Validation Architecture: scratch Playwright probes + guard tests. |
| DoC-6 | Before/after shots outside the repo; relaid-out pages signed off | Gate 1 / Gate 2 flow, scratch tooling rebuild notes. |
| DoC-7 | Mobile checks under a mobile UA with touch | UA keyword list verified in `MobileDetectionMiddleware`; Pixel 7 descriptor. |
| DoC-8 | Guard tests rewritten to the same intent, none deleted | Guard-test matrix (section "Guard tests that read these files"). |
| DoC-9 | `Html.LocalTime`/`Html.WallClock` untouched; no writes to Events/Quests | Profile keeps the two `Html.LocalTime` calls byte-identical; no data-layer work in scope. |
| DoC-10 | At merge, every page works and reads | Slice order with link-then-remove rule; suite green per slice. |
| X-1 | Foundation items 003, 013, 016, 020, 025, 031 are present | Verified in `components.css` / `tokens.css` (see Standard Stack). |
| X-2 | AFTER sweep adds Profile with one subscription (+QR, Rename dialogs), AccessDenied signed out, Login validation state | D-02, D-05. |
| X-3 | SetPassword verified with a freshly mailed token | D-09; operator checkpoint. |
| X-4 | AccessDenied overflow fix reusable on `Events/Details` | Root cause and recommended single fix below. |
</phase_requirements>

## Summary

The foundation Phase 94 delivered already does most of the visual work: `.modern-card` is clear glass with the surface owning the text colour, `.modern-card--interactive`, the opaque `.modal`, `.alert`, `.refusal-line`, `.empty-state`, validation colours, button roles and the 44px phone floor are all live in `components.css` / `shell.mobile.css` and load on both layouts, so a merged view needs almost no CSS of its own. The phase is mostly (a) markup conversion of 17 files to 11 or 12, (b) a single new layered area sheet `account.css` that replaces `account.mobile.css` and the Profile block of `legacy-pages.css`, (c) one shared Profile script, (d) rewriting seven groups of static guard tests that currently read the old file names, markers and inline-script text, and (e) browser verification under a phone UA.

Three facts shape the plan. First, a layered sheet loses to any unlayered rule, and `legacy-pages.css` is unlayered, so the Profile block must be removed from it in the same change that the new rules appear (move, never duplicate). Second, `MobileCssTests` requests `/` with a desktop UA and asserts the response never contains the string `mobile.css`; `/` signed out is `Home/Index`, so no page sheet that a merged view links may be named `*.mobile.css` (this is also why the area sheet must be `account.css`, not a renamed `account.mobile.css`). Third, `ProfileCalendarSubscriptionTests` and `CalendarSubscriptionStaticGuardTests` pin exact substrings (class names counted per row, the desktop table class string, the name/meta `<div>` pair, the copy-contract strings, the `confirm()` sentence extracted by regex from the view), so R2, D-10 and D-11 each force a named, in-lockstep rewrite of those facts.

The picker is the one S1 and goes first: the board card is a `div` with `style="cursor:pointer"` and `onclick="this.closest('form').submit()"` on both twins; the fix is a real `<button type="submit">` row inside the existing per-board form, with the hidden inputs and the exact `name="returnUrl" value="` attribute order left alone because `CrossBoardPickerSkipTests` normalises that pattern when comparing non-member and nonexistent-board responses.

**Primary recommendation:** Convert in the D-06 order with `account.css` (`@layer pages`, no colour literals, no `!important`, not named `*.mobile.css`) as the only new sheet; use `.modern-card` markup on both layouts and retire `.account-card-mobile`; add `account.css` to the `LayeredFiles`/`NoLiteralFiles` arrays of `UiFoundationLayerGuardTests` plus a new `AccountConversionGuardTests`; do the Delete confirmation as a Bootstrap modal opened by `data-bs-toggle="modal"` (Bootstrap then returns focus to the Delete button itself, verified in the vendored 5.3.8 source); fix the AccessDenied 4px overflow at its root (top-level `.row` gutter on the phone layout) so Phase 97 only has to verify `Events/Details`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Choose which twin renders (`.Mobile.cshtml` vs desktop) | API / Backend (Razor view-location expander + UA middleware) | — | `MobileViewLocationExpander` + `MobileDetectionMiddleware` decide server-side from the User-Agent; viewport width never selects a twin. |
| Board selection (POST `SelectGroup`, antiforgery, `returnUrl`) | API / Backend | Browser (submit control) | Controller owns membership check and redirect; the row is only a native submit control. |
| Sign-in, reset, set/change password, profile edit | API / Backend | — | Controllers and Identity own all rules; this phase changes markup only. |
| Validation message presentation, `autocomplete` tokens | Browser / Client (markup attributes) | API (ModelState) | Server-side validation only (no unobtrusive-validation script is loaded on these pages); tokens are static markup. |
| Calendar subscription row actions (copy, rename, delete, QR) | Browser / Client | API (mint/rename/revoke POSTs) | Clipboard, modal and button relabel are client behaviour moved to one script; writes stay server-side. |
| Delete confirmation dialog | Browser / Client (Bootstrap modal) | API (revoke POST) | Same POST as today; the dialog replaces `window.confirm`. |
| Stylesheet delivery | CDN / Static (`wwwroot/css`) | Frontend Server (`@section Styles` in layouts) | Page sheet linked by the view; layers decide precedence in the browser. |
| Date rendering on Profile | Frontend Server (`Html.LocalTime`) | Browser (`site.js` hydrates) | Untouched; real instants follow the viewer. |

## Standard Stack

No packages are added; the phase uses what is already vendored and tested. [VERIFIED: QuestBoard.IntegrationTests.csproj:13-21]

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core MVC Razor | .NET 10 (`dotnet --version` 10.0.400) | Views, tag helpers, view-location expander | Project stack; no change allowed (CLAUDE.md). |
| Bootstrap | 5.3.8 (self-hosted `wwwroot/lib/bootstrap-5.3.8`) | Grid, modal JS, utilities | Layer `vendor`; modal JS gives focus trap, Escape, focus return. |
| Font Awesome | 6.7.2 (self-hosted) | Icons (`fa-circle-exclamation` etc.) | Already linked on every layout. |
| Foundation sheets | in repo | `tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css` | The converted look; this phase adds only an area sheet. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit v3 + FluentAssertions | 3.2.2 / 8.10.0 | Guard and integration tests | Rewriting guard tests; new `AccountConversionGuardTests`. [VERIFIED: csproj:13,21] |
| Microsoft.AspNetCore.Mvc.Testing | 10.x | In-memory host for UA-specific requests | `GetWithUserAgentAsync` helpers already in `MobileViewsTests`. |
| Playwright (scratch only, outside the repo) | install into scratch at the checkpoint | Screenshots under desktop and `devices['Pixel 7']`, probes | Not in the suite; same approach as 94/95. Edge 156/157 is installed (see Environment). |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New `account.css` | Keep `account.mobile.css` for the twins that stay | Two sheets for one area, still named `*.mobile.css`, and desktop twins would have to link a "mobile" sheet; contradicts A-account-043 ("one area file in the `pages` layer"). Rejected. |
| Bootstrap modal for Delete | Intercept `submit` and call `modal.show()` programmatically | Programmatic show does not get Bootstrap's automatic focus return to the trigger (only the `data-bs-toggle="modal"` data-api path wires it); needs hand-written focus code. Rejected here, then adopted by plan 96-07 with the focus return written by hand: keeping the row's no-script post (Gate 1 item 14) means a data-api opener could show the dialog while that post goes out if the module failed to load. |
| Flat row = `.modern-card--interactive` button | Flat row styled in `group-picker.css` | Audit A-account-031 suggested `.modern-card--interactive`, but R1 (Q-122) says rows replace cards (a glass card inside the glass panel is A-account-032's contrast failure). Recommend the flat row; Gate 1 mockup decides (open question 2). |

**Installation:** none (no new packages).

**Version verification:** all versions above are the ones already committed (`wwwroot/lib/bootstrap-5.3.8`, `fontawesome-6.7.2`, csproj package references); nothing was upgraded.

## Package Legitimacy Audit

No external package is installed by this phase, so the legitimacy gate has nothing to check. The scratch Playwright install is outside the repository and is gated by the D-05 operator checkpoint, as in Phase 94/95.

**Packages removed due to SLOP verdict:** none
**Packages flagged as suspicious:** none

## Architecture Patterns

### How a merged view works with UA-selected twins (verified)

- `_ViewStart.cshtml` sets `Layout = isMobile ? "~/Views/Shared/_Layout.Mobile.cshtml" : "_Layout"` from `Context.Items["IsMobile"]`. [VERIFIED: Views/_ViewStart.cshtml]
- `MobileDetectionMiddleware` sets `IsMobile` when the UA contains any of `"Mobi", "Android", "iPhone", "iPad", "Windows Phone", "BlackBerry"` (case-insensitive). [VERIFIED: Middleware/MobileDetectionMiddleware.cs:5-6] Window width never matters; a narrow desktop window gets the desktop layout.
- `MobileViewLocationExpander.ExpandViewLocations` returns, for a mobile request, each location twice: first with `.cshtml` replaced by `.Mobile.cshtml`, then the original. [VERIFIED: ViewExpanders/MobileViewLocationExpander.cs:27-34] Consequence: deleting `Login.Mobile.cshtml` makes a phone request fall through to `Login.cshtml` automatically; nothing else needs registering. `PopulateValues` adds the `isMobile` value to the view cache key, so a phone and a desktop request never share a compiled view.
- A merged view therefore renders under `_Layout.Mobile` (phone) and `_Layout` (desktop), and must look right inside both: phone main is `<main id="main-content" tabindex="-1" class="container-fluid px-2 mt-2 flex-grow-1">`, desktop main is inside `<div class="container mt-3 flex-grow-1">`. [VERIFIED: _Layout.Mobile.cshtml:242, _Layout.cshtml:290-291]
- `@section Styles` is rendered by all three layouts that serve this phase: `_Layout.cshtml:37`, `_Layout.Mobile.cshtml:24`, `_Layout.GroupPicker.cshtml` (`@await RenderSectionAsync("Styles", required: false)`), always after the last global `<link>`. [VERIFIED by reading the three layouts] (The Phase 94 UI-SPEC text saying the desktop layout "has no Styles section" is out of date; the foundation added it.) `UiFoundationLayerGuardTests.Layouts_LinkFoundationInCascadeOrder` additionally pins that every layout renders the section after its last stylesheet link (lines 211-214).
- The picker views set `Layout = "~/Views/Shared/_Layout.GroupPicker.cshtml"` themselves, so the picker layout is used under both UAs and `.mobile-layout`-scoped rules never apply there; its phone rules must be `@media` inside `group-picker.css` (already the case).
- Rule A7: `@media` is allowed in twin-less and shared (merged) views in the `pages` layer; a surviving twin carries none. [CITED: 94-UI-SPEC.md:175-176, 716] Prefer a `.mobile-layout`-scoped selector for UA-driven differences in a merged view (UA decides, not width), and keep `@media` for genuine width reflow.

### System Architecture Diagram

```
 Browser (desktop UA | phone UA, touch)
        |
        v
 MobileDetectionMiddleware --sets--> HttpContext.Items["IsMobile"]
        |
        v
 MVC route -> Controller (Account | GroupPicker | Home | Account/AccessDenied)
        |                                         |
        |  ModelState / viewmodel                 |  POST SelectGroup (antiforgery, groupId, returnUrl)
        v                                         v
 View resolution:  MobileViewLocationExpander  --> [X.Mobile.cshtml]  else  [X.cshtml]
        |            (merged pair: only X.cshtml exists, both UAs)
        v
 _ViewStart picks layout: _Layout.Mobile | _Layout     (picker view overrides -> _Layout.GroupPicker)
        |
        v
 HTML: foundation links (app, tokens, base, components, shell[.mobile])
       + legacy unlayered sheets (desktop layout only, beat every layered rule)
       + @section Styles -> account.css (layer: pages)   [picker: group-picker.css via layout]
       + @section Scripts -> profile-subscriptions.js (Profile twins only)
        |
        v
 Browser cascade: unlayered legacy > pages > components > base > tokens > vendor
        |
        +--> Profile only: Copy -> clipboard | Rename -> modal(show.bs.modal fills id/name)
                         | Delete -> confirm modal -> POST RevokeCalendarSubscription
                         | Add -> disable + "Adding..." | QR -> modal
```

### Recommended Project Structure

```
QuestBoard.Service/
├── Views/Account/        # Login, ForgotPassword, SetPassword, ChangePassword (single files);
│                         # Edit + Edit.Mobile, Profile + Profile.Mobile (stay twins)
├── Views/GroupPicker/    # Index.cshtml only (Index.Mobile.cshtml deleted)
├── Views/Home/           # Index.cshtml (+ Index.Mobile.cshtml only if the operator declines the merge)
├── Views/Shared/AccessDenied.cshtml
├── wwwroot/css/
│   ├── account.css       # NEW, @layer pages: area sheet (replaces account.mobile.css + Profile block)
│   ├── group-picker.css  # existing, layered: gains the flat-row rules, loses the card rules
│   └── account.mobile.css, legacy-pages.css (Profile block 1262-1329) -> removed last (link-then-remove)
└── wwwroot/js/profile-subscriptions.js   # NEW: one file loaded by both Profile twins
```

### Pattern 1: Merged account form (both layouts, foundation card)
**What:** One view with the desktop card pattern; no `account-card-mobile`; one `h1`; the foundation supplies glass, label, control, button and phone 44px rules. Wrap in the same `row justify-content-center / col-md-6` the desktop twin uses.
**When to use:** Login, ForgotPassword, SetPassword, ChangePassword (and the Edit/Profile twins' cards).
**Example:**
```cshtml
@* Source: pattern from .claude/ui-guidelines.md "Components" + the current Login.cshtml structure *@
@model LoginViewModel
@{ ViewData["Title"] = "Log in"; }
@section Styles {
    <link rel="stylesheet" href="~/css/account.css" asp-append-version="true" />
}
<div class="row justify-content-center">
  <div class="col-md-6 col-lg-4">
    <div class="card modern-card">
      <div class="card-header modern-card-header">
        <h1 class="mb-0"><i class="fas fa-sign-in-alt account-title-icon me-2" aria-hidden="true"></i>@ViewData["Title"]</h1>
      </div>
      <div class="card-body modern-card-body">
        <form asp-action="Login" asp-route-returnurl="@ViewData["ReturnUrl"]" method="post">
          <partial name="_AccountValidationSummary" />   @* refusal-line wrapper, see Pattern 3 *@
          <div class="mb-3">
            <label asp-for="Email" class="form-label"></label>
            <input asp-for="Email" class="form-control" autocomplete="username" />
            <span asp-validation-for="Email" class="field-validation-error"></span>
          </div>
          ...
          <a asp-action="ForgotPassword" class="account-link">Forgot password?</a>  @* keep the underline: no text-decoration-none *@
          <hr>
          <div class="d-grid"><button type="submit" class="btn btn-primary btn-lg"><i class="fas fa-sign-in-alt me-2" aria-hidden="true"></i>Log in</button></div>
        </form>
      </div>
    </div>
  </div>
</div>
```
(`_AccountValidationSummary` is a recommendation, not a requirement; inline markup per page is acceptable.)

### Pattern 2: Picker row = native submit control
**What:** Each board is `<li><form asp-action="SelectGroup" method="post">` holding the two hidden inputs and one `<button type="submit" class="group-picker-row">`. Keep the hidden inputs exactly as today.
**Example:**
```cshtml
<ul class="group-picker-list">
@foreach (var group in Model.Groups)
{
    <li>
        <form asp-action="SelectGroup" method="post">
            @Html.AntiForgeryToken()
            <input type="hidden" name="groupId" value="@group.Id" />
            <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
            <button type="submit" class="group-picker-row">
                <span class="group-picker-name">@group.Name</span>
                <span class="group-picker-meta"><i class="fas fa-users me-1" aria-hidden="true"></i>@group.MemberCount member(s)</span>
            </button>
        </form>
    </li>
}
</ul>
```
CSS (in `group-picker.css`, inside the existing `@layer pages`): `button` does not inherit colour or font, so the row must reset them or it renders UA dark text on glass: `width:100%; display:flex; justify-content:space-between; align-items:center; gap:var(--qb-space-md); min-height:var(--qb-hit-min); padding:var(--qb-space-sm) var(--qb-space-md); border:0; border-radius:var(--qb-radius-md); background:transparent; color:inherit; font:inherit; text-align:start;` hover `background-color: var(--qb-surface-glass-hover)`; separators `border-bottom: 1px solid var(--qb-border)` on `li`. The focus ring needs no code: `base.css` lists `button` in its `:focus-visible` set. [ASSUMED: UA button colour behaviour is standard browser behaviour; confirm in the Gate 1 mockup render.] Do not use `.card-text` or `.card-title` on the row: `components.css` gives `.modern-card .card-text` a 125px scroll box with `pre-wrap`. [VERIFIED: components.css `.modern-card .card-text` rule]

### Pattern 3: Validation line (A-account-004) and `autocomplete`
`components.css` already styles `.validation-summary-errors`, `.field-validation-error`, `.input-validation-error` and `.refusal-line` in `--qb-danger-text`. The tag helper `asp-validation-summary` always emits a `<div>` (valid or not). To get an announced refusal line with an icon: render the wrapper only when there is something to say. `All` summaries: `@if (!ViewData.ModelState.IsValid)`. `ModelOnly` summaries (Edit): test the model-level key (`ViewData.ModelState.TryGetValue(string.Empty, out var e) && e.Errors.Count > 0`), otherwise a field-only error draws an empty alert box. Whether a server-rendered `role="alert"` present at load is announced differs by screen reader [ASSUMED]; verify in UAT rather than relying on it, and consider `aria-live` only if a screen-reader pass shows silence.
`autocomplete` tokens (markup only; the tag helper does not emit them): Login Email `username`, Password `current-password`; ForgotPassword Email `email`; SetPassword both `new-password`; ChangePassword Current `current-password`, New and Confirm `new-password`; Edit Name `name`, Email `email`. [CITED: 94-AUDIT-account.md A-account-006]

### Pattern 4: Shared Profile script and the Delete modal
One file `wwwroot/js/profile-subscriptions.js`, loaded `<script type="module" src="~/js/profile-subscriptions.js" asp-append-version="true"></script>` in `@section Scripts` of both twins. It imports nothing (so a classic script would also satisfy the guideline; the repo has no `type="module"` precedent yet, only classic scripts such as `contact-tags.js`, which say they follow "site.js's no-module, no-bundler convention"). Module scripts are deferred and run after parsing, so no `DOMContentLoaded` wrapper is needed; the `bootstrap` global from the classic bundle loaded earlier in the layout is available. Contents (behaviour identical to the 90 inline lines, which were diffed this session and are the same on both twins): Copy (clipboard with the `visually-hidden` reveal fallback), Add-subscription disable and "Adding…" relabel, Rename modal `show.bs.modal` fill and `shown.bs.modal` focus, and the new delete-modal fill. Fix A-account-022 by capturing `button.innerHTML` once at wiring time and restoring that, not a hard-coded `'<i class="fas fa-copy me-2"></i>Copy'` (the phone's Copy is icon-only; the hard-coded restore turns it into a 74px labelled button). The existing `restoreLabel(label, iconClass, extraButtonClass)` has three parameters nothing passes; drop them.
Delete (D-11): the row's Delete becomes `<button type="button" class="btn btn-danger btn-sm" data-bs-toggle="modal" data-bs-target="#deleteSubscriptionModal" data-subscription-id="@subscription.Id" data-subscription-name="@subscription.Name">`, with one modal per page (outside the card, like Rename) containing a form posting `RevokeCalendarSubscription` with a hidden `id` and antiforgery, Cancel (`data-bs-dismiss`) and a `btn-danger` confirm. On `show.bs.modal` read `event.relatedTarget.dataset` and set the name with `textContent` (the name is user-supplied through Rename; never `innerHTML`). Verified in the vendored source: the `[data-bs-toggle="modal"]` data-api click handler does `P.one(e,gn,()=>{l(this)&&this.focus()})`, i.e. on `hidden.bs.modal` it refocuses the trigger if still visible, and `Modal.show` calls `this._element.focus()`; `{backdrop:!0,focus:!0,keyboard:!0}` are the defaults, so a focus trap and Escape come with it. [VERIFIED: wwwroot/lib/bootstrap-5.3.8/js/bootstrap.bundle.min.js, Modal class and data-api handler] Because Delete moves from a per-row `<form>` to a trigger plus one shared form, a row's Delete without JS no longer posts; before this change the no-JS path posted without any confirmation, so nothing a user relied on is lost, but the plan should state it.

### Anti-Patterns to Avoid
- **Duplicating a rule across `legacy-pages.css` and `account.css`:** the unlayered copy wins and the layered one is dead. Move the Profile block (lines 1262-1329, including the orphaned banner comment that sits just above it) out of the legacy file in the same commit.
- **A class containing `calendar-subscription-row` or `calendar-subscription-address` as a prefix** (`...-row-actions`, `...-address-note`): the tests count substring occurrences per row (see pitfalls).
- **`text-muted`/colour utilities and `fw-bold` in new markup:** use the surface-owned colour; a muted line reads `var(--bs-secondary-color)`, which resolves to `--qb-fg-muted` on glass and to `--qb-ink-muted` inside a `.table` (both set in `base.css`/`components.css`), so one class serves both surfaces.
- **`.row` as the first child of the phone `main`** without the gutter fix (see AccessDenied); this is the 394-at-390 overflow.
- **Renaming `account.mobile.css` to anything ending `.mobile.css` and linking it from a merged view:** a desktop response would then link a phone sheet.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Confirm dialog (focus trap, Escape, focus return, backdrop) | A custom overlay or `confirm()` shim | Bootstrap 5.3.8 Modal via `data-bs-toggle="modal"` | Trap, Escape, scroll lock and focus return are built in; the opaque surface is `.modal` in `components.css`; phone buttons get the 44px rule (`.mobile-layout :where(.modal, .offcanvas) :where(.btn, .btn-close)`). |
| Refusal/validation line | New red box styles | `.refusal-line`, `.validation-summary-errors`, `.field-validation-error` | Already `--qb-danger-text` at 4.5:1. |
| Empty state on Profile | Bespoke `text-center py-5` block | `.empty-state`, `.empty-state-icon`, `.empty-state-heading`, `.empty-state-text` | Shared component (A-account-029). |
| Focus ring on the picker row | `outline` rules | The `:focus-visible` set in `base.css` (lists `button`) | One ring everywhere. |
| Muted/secondary text | `rgba()` at 70% alpha plus `text-shadow` | `var(--qb-fg-muted)` / `var(--bs-secondary-color)` | Alpha muted text fails 4.5:1 at the lightest board pixels (A-account-020). |
| Disabled submit | Own disabled styling | `.btn:disabled` rule in `components.css`, and `data-disable-on-submit` in `site.js` if no relabel is needed | One look for a locked button. |
| Date display | `.ToString()` on dates | `Html.LocalTime` (already on Profile) | Project rule; stays byte-identical. |

**Key insight:** the foundation owns colour, focus, touch size and disabled/validation looks; the area sheet should contain layout only (flex/grid, spacing from `--qb-space-*`, one `max-width` for the QR, the ellipsis on the phone name, the picker row geometry). If a rule in `account.css` names a colour other than through a token, it is probably re-fighting the foundation.

## Runtime State Inventory

Not a rename/refactor/migration phase. Not applicable (omitted by design). One adjacent note: no stored data, service config, OS registration, env var or build artifact carries a name this phase changes; the only renamed on-disk items are the stylesheet `account.mobile.css` (references are in views and three test files, all enumerated below) and removal of five `.Mobile.cshtml` files.

## Current state of the 17 view files and the stylesheets

All counts are from opening the files this session (the `style=`, `<script`, `on*=` counts came from a scripted count over the 17 files and match the ROADMAP's "2 inline style, 0 `<style>`, 2 inline `<script>`, 2 inline handlers").

| View (desktop / phone) | Lines | Today | Conversion notes |
|---|---|---|---|
| `Account/Login` / `.Mobile` | 52 / 43 | Desktop: `h2`, `btn-warning btn-lg`, `text-decoration-none` on Forgot link, `ViewData["Title"]="Log in"`. Phone: `account-card-mobile`, `h5`, no title, no `<hr>`. | Merge (Q-103). Title `Log in`. Submit `btn-primary btn-lg`. Link keeps underline and gets the 44px class. autocomplete tokens. |
| `Account/ForgotPassword` / `.Mobile` | 38 / 28 | Same pattern; title "Forgot Password" desktop only; icon `fa-paper-plane`. | Merge (Q-104). Title `Forgot Password`. |
| `Account/SetPassword` / `.Mobile` | 49 / 37 | Desktop icon `text-primary`, `<hr>`, `col-md-6`; phone icon `text-warning`; hidden `UserId`, `Token`. Source-only audit. | Merge (Q-105). Title `Set Your Password`. Verify with a mailed token (D-09). |
| `Account/ChangePassword` / `.Mobile` | 58 / 45 | Both have Cancel (`btn-secondary`) + submit (`btn-primary`); phone has no title. | Merge (Q-106). Title `Change Password`. |
| `Account/Edit` / `.Mobile` | 69 / 53 | Desktop: Change Password is `btn btn-outline-primary` + `p.text-muted` "Want to change your password?"; phone: `btn-warning`, `div.account-section-heading`, no sentence. Save is `btn-success`. | Stay twins (Q-82). Add `ViewData["Title"]="Edit Profile"` to phone (Q-80). Both Change Password buttons `btn-secondary`. Save `btn-primary`. `autocomplete` `name`/`email`. |
| `Account/Profile` / `.Mobile` | 337 / 282 | Desktop `col-lg-9 col-md-11`, label/value `row`s with `label` elements, subscription `table table-striped table-hover align-middle`, 90-line inline `<script>`. Phone: `account-field-label/value` divs, `calendar-subscription-row` blocks, icon-only Copy and QR (`aria-label`), same inline script. | Stay twins. Title `Profile` on phone (Q-81). Phone wording "Has Building Key"/"No Building Key" (Q-84). Keep "Administrator" vs "Admin" (Q-83). `dl/dt/dd` on desktop (A-account-026). R2. Script to module. Modal for Delete. D-10 copy. |
| `GroupPicker/Index` / `.Mobile` | 61 / 55 | Both: `div.card.modern-card.h-100` with `style="cursor:pointer" onclick="this.closest('form').submit()"` per board (inside `<form asp-action="SelectGroup">` with `@Html.AntiForgeryToken()` + hidden `groupId`, `returnUrl`). Desktop grid `row-cols-1 row-cols-md-2 row-cols-lg-3`; phone `row-cols-1`. Phone links `account.mobile.css`. | Merge (Q-107) in the same change as R1 and the keyboard fix. Delete `Index.Mobile.cshtml`. Picker needs no `@section Styles` (see below). |
| `Home/Index` / `.Mobile` | 24 / 15 | Desktop: card, `h2`, `lead` sentence, `hr`, `btn-warning btn-lg` Log In. Phone: bare stack, `fa-4x` icon, `h3`, `text-muted` sentence, `d-grid` button. No sheet linked by either (`home.mobile.css` is not used by Home; it belongs to quest list views owned by Phase 95). | Merge proposal at Gate 1 (D-07). One card, `h1`, sentence at body colour, button full width on the phone via a `.mobile-layout` rule. |
| `Shared/AccessDenied` | 49 | One view, both layouts: root `.row` + `.col-md-6`, `h2`, `h4`, `text-muted`, `alert-info`/`alert-warning` with `alert-link`, `btn-warning` "Back to Quest Board", `d-grid gap-2 d-md-block`. | `h1` + `h2`; remove the overflow (below); Back becomes `btn-secondary` (navigation, not caution). |

**Account sheets.** `account.mobile.css` (109 lines): 3 hex (`#F4E4BC`), 14 `rgb()`/`rgba()`, 11 `!important`, 7 `text-shadow`, one `backdrop-filter` (counts reproduced this session; the audit's 4 hex included one in a comment). It re-colours `.form-label`, `.form-check-label`, `.form-text`, `.badge`, `.text-muted` (a cascade fight with a Bootstrap important utility), defines `.account-card-mobile`, `.account-section-heading`, `.account-field-value/-label`, `.account-subtitle` (dead), and the phone calendar rules (`.calendar-subscription-row`, `-address`, `-meta`, `-icon-btn`, `.calendar-subscription-row .calendar-subscription-name`, `.calendar-subscription-qr svg`). **Profile block of `legacy-pages.css`:** file has 1329 lines; the banner "QUEST EVENT STYLES" and the comment "Calendar Subscription section — Views/Account/Profile.cshtml only" start at about line 1262/1266 and run to the end of the file; the block contains `#6c757d` (the S1 grey-on-glass meta text) and the QR rule `.calendar-subscription-qr svg`. **`group-picker.css`:** layered (`@layer pages`), 0 hex, 0 rgb, 0 important, one `@media (max-width: 767.98px)` block giving `.group-picker .btn` the 44px floor, `.group-picker .modern-card` a min height and `max-width:100%`, and `overflow-wrap:anywhere` on `.card-title/.card-text`; the card rules go and row rules replace them. **Foundation items needed (X-1):** `.modern-card--interactive` and its hover/reduced-motion split (components.css), the opaque `.modal`, `--qb-fg-muted: #e9dab3`, `--qb-danger-text: #ffbab1`, `--qb-ink-muted`, `--qb-hit-min: 44px`, `--qb-link: #ffe08a` are all present. [VERIFIED: components.css, tokens.css]

**Cascade hazard for the desktop layout.** `_Layout.cshtml` links `legacy-pages.css`, `calendar.css`, `quests.css`, `shop.css`, `characters.css`, `contacts.css` and others unlayered; an unlayered rule beats any layered `account.css` rule whatever the specificity. A grep of the generic selectors in `legacy-pages.css` found no top-level `.card`, `.btn`, `.alert`, `.form-*`, `.text-*`, `h1-h6` rule, and `quests.css`/`contacts.css`/`events-overview.css` only scope `.modern-card` under page classes, so no existing rule reaches the account views. The only unlayered hits on these pages come from the Profile block itself (removed with the move). Verify with a computed-style spot check on the desktop render rather than trusting grep.

## Guard tests that read these files (what each pins, and the same-intent rewrite)

None may be deleted (DoC-8). Line numbers are from this session's reads.

| Test (file) | What it pins today | Breaks when | Same-intent rewrite |
|---|---|---|---|
| `MobileViewsTests` Login facts (`Mobile/MobileViewsTests.cs` ~552-575) | Mobile UA GET `/Account/Login`: 200, contains `account-card-mobile` and `account.mobile.css`. Desktop UA: 200 and `NotContain("account-card-mobile")`. | Merge + class retired + sheet renamed. | Intent = "the phone shell serves the login form and the area sheet is linked; the desktop response is the plain desktop layout". Mobile UA: 200, body class `mobile-layout`, `account.css`, the Email/Password inputs. Desktop UA: 200, `account.css`, `NotContain("mobile-layout")`, no `.Mobile`-only marker. Both: one `h1`, non-empty `<title>` containing "Log in" (adds the Q-76 intent). |
| `MobileViewsTests` GroupPicker fact (~619-663) | Seeds a user into two groups; mobile UA; 200, contains `account-card-mobile` and `account.mobile.css`; docstring: guards "the unrendered-Styles-section white-page regression" (a view `@section Styles` the layout does not render). | Merge; picker has no `@section Styles`. | Mobile UA: 200, rows are `<button type="submit"` inside `SelectGroup` forms (one per seeded group), `group-picker.css` linked, `mobile-layout` NOT present (the picker layout is served under both UAs). The Styles-section regression is already covered for every layout by `UiFoundationLayerGuardTests.Layouts_LinkFoundationInCascadeOrder` (renders the section); say so in the rewritten summary. |
| `MobileViewsTests` Edit fact (~598-615) and Profile fact (~667-690) | Mobile UA: 200 and contains `account-card-mobile`. | Class retired. | Mobile UA: 200, `mobile-layout`, `modern-card`, a phone-twin marker that only the twin renders (Profile: the title and, with a subscription, `calendar-subscription-row`; Edit: the absence of "Want to change your password?" pins Q-82), non-empty title. |
| `MobileViewsTests` Register fact | 404 (route removed) | Not affected. | Untouched. |
| `ProfileCalendarSubscriptionTests` (`Tests/ProfileCalendarSubscriptionTests.cs`) | (a) desktop row marker `calendar-subscription-address` counted exactly once per subscription, phone marker `calendar-subscription-row` exactly once per row, also for 3 and for 10 rows (172-174, 241-243); (b) desktop `Contain("table table-striped table-hover align-middle")` and `NotContain("calendar-subscription-row")`; phone the reverse (190-199); (c) desktop name cell regex `<div>(.*?)</div>\s*<div class="calendar-subscription-meta">` must yield "New subscription" (107-112, 305-308); (d) `maxlength="60"` on both (261); (e) empty state `Contain("No calendar subscriptions yet")`, `Contain("Get this board's schedule onto your phone's own calendar app. The address below acts like a password")`, `Contain("Add Subscription")` (127-131); (f) `"Never fetched yet"`, `"Last fetched"`, no "All subscriptions"; (g) `Subscription added.`, the "Couldn&#x27;t rename/delete this subscription" toasts. | R2 re-markup, class additions, D-10 wording. | Keep (a)-(d),(f),(g) passing unchanged by construction rules below. (e) must change in the same commit as D-10: update the long string to the new clause (for example "The address you create acts like a password"), and update the same string in `CopyContractStrings` (next row). Phone truncation fact (277-281) mentions "account.mobile.css" only in its message: reword the message. |
| `CalendarSubscriptionStaticGuardTests` (`Tests/CalendarSubscriptionStaticGuardTests.cs`) | (a) `CopyContractStrings` 20 strings asserted in BOTH Profile view files (68-112), including "Copied!", "Press Ctrl+C" and the two halves of the delete sentence, which today live in the inline script; (b) `DeleteConfirmText_IsByteIdenticalBetweenLayouts` extracts `var message = 'Delete "' + name + '(.*?)';` from each view with a regex (116-132); (c) forbidden-claims scan of both views (150-184); (d) `MobileTouchTargetFloor...` reads `account.mobile.css` for the rule `.calendar-subscription-icon-btn {` with `min-height: 44px`/`min-width: 44px`, and counts `calendar-subscription-icon-btn` >= 2 in the phone view (188-205); (e) `RowLabelTruncationRule...` reads the rule `.calendar-subscription-row .calendar-subscription-name {` with `text-overflow: ellipsis` (209-227); (f) `QrCodeSizingRule_ExistsInBothStylesheets` reads `legacy-pages.css` and `account.mobile.css` for `.calendar-subscription-qr svg {` with `max-width: 240px` (231-247); (g) a repo-wide scan of `.cs/.cshtml/.css` for "CALFEED-n", "Phase 84", "84-0n", "n-REVIEW" (278-308). | Script text leaves the views; `account.mobile.css` removed; the Profile block leaves `legacy-pages.css`; one sheet now. | (a) split: strings that stay in markup remain asserted on both views; "Copied!", "Press Ctrl+C", Copy label restore are asserted on `wwwroot/js/profile-subscriptions.js`; and add "both views reference `profile-subscriptions.js`" (parity by construction). The two halves of the delete sentence move into the confirm modal's static text in each view (so they stay on both views) with only the name filled by script; (b) becomes "the modal body text is byte-identical between layouts" (extract the modal paragraph from each view) and "neither view contains `confirm(`"; (c) extend the scan to the script file; (d) read `account.css`, expect `min-height: var(--qb-hit-min)`/`min-width: var(--qb-hit-min)` in that rule (a layered sheet may not carry the literal if it were a colour; a size literal is allowed, but the token is the convention); keep the `>= 2` phone-view count; (e) read `account.css`; (f) rewrite to "one sheet, `account.css`, holds the rule, and both Profile views link it" (the old reason, two layouts load different sheets, no longer holds); (g) unchanged. |
| `UiFoundationLayerGuardTests` (`Tests/UiFoundationLayerGuardTests.cs`) | `LayeredFiles` (53-61) and `NoLiteralFiles` (64) list `group-picker.css`; `LayeredFiles_DeclareTheirLayer` requires each to start with `@layer <name> {` after comment stripping; no `!important` outside reduced-motion/visually-hidden; no colour literal; planning-reference scan. `DesktopLayouts_LinkNoMobileStylesheet` forbids the string `mobile.css` in desktop layouts (219-224). `LegacyDesktopSheet_IsUnlayeredAndRenamed` requires `legacy-pages.css` to exist and contain no `@layer`. | Adding `account.css`. | Add `("account.css", "pages")` to `LayeredFiles` and `"account.css"` to `NoLiteralFiles`: this buys the layer, no-important, no-literal and no-planning-reference guards for the new sheet with no new test. `account.css` must open with `@layer pages {` as its first non-comment statement. `legacy-pages.css` must stay (shrinks only). |
| `MobileCssTests.MobileCss_DesktopUserAgent_ResponseDoesNotLinkMobileStylesheet` (`Mobile/MobileCssTests.cs:134-145`) | Desktop UA GET `/` (signed out = `Home/Index`): `NotContain("mobile.css")` and `Contain("legacy-pages.css")`. | Any `*.mobile.css` link or the text "mobile.css" in the Home response. | No rewrite needed; it is a constraint: Home's merged view must link no `*.mobile.css`. `MobileLayoutTests` additionally requires the desktop `/` response to not contain `mobile-layout`: never emit that class in markup (a CSS selector in a sheet is fine). |
| `GroupPickerControllerIntegrationTests` (`Controllers/...`, default UA) | Empty state `Contain("not assigned to any group")` (71); multi-group `Contain("Select Your Group")` and `Contain("SelectGroup")` (106-107); super admin `Contain("Go to Platform")` and `Contain("/platform")` (124-125); redirects. | Dropping any of those strings. | None needed; keep the copy, the `SelectGroup` form action, the `/platform` href. |
| `CrossBoardPickerSkipTests` (`Controllers/...`) | Same strings plus `NormalizeVolatileFields` (205-210) replacing `(name="__RequestVerificationToken" type="hidden" value=")[^"]*(")` and `(name="returnUrl" value=")[^"]*(")` before asserting the non-member and nonexistent-board bodies are identical (181-202). Also login POST redirects to a URL containing `GroupPicker` (432). | A changed attribute order on these inputs, or echoing `returnUrl` anywhere else in the markup (a `data-return-url`, a button `formaction`), would leave an un-normalised difference and fail the oracle-parity fact. | Keep `<input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />` byte-for-byte and keep the token via `@Html.AntiForgeryToken()` or the tag helper; echo `returnUrl` only in that one input. |
| `HomeControllerIntegrationTests` (`Controllers/...:42-77`) | Desktop UA `/`: `Contain("Log In")` and `Contain("/Account/Login")`; no quest titles. | Changing the button label or link. | None; keep the label "Log In" and the `Account/Login` link. |
| `AccountControllerIntegrationTests` (`Controllers/...`) | Profile desktop: `Contain("Administrator")` (116), `Contain("Dungeon Master & Player")` (133; raw `&`, as emitted by static markup), `NotContain` for non-admin; AccessDenied `Contain("You Don't Have Permission")` (35, literal apostrophe) and `NotContain("Dungeon Master")`; login/forgot flows with antiforgery extraction. | Re-encoding "Dungeon Master & Player" on desktop, or HTML-encoding the apostrophe in the AccessDenied heading. | Keep those literals as static markup text. |
| `BoardPageStaticGuardTests` (`Tests/...:125-128`) | Planning-reference scan over `Views/Account/Profile.cshtml` and `Profile.Mobile.cshtml`. | A "D-nn", phase, plan or planning-document name in a comment. | Follow CLAUDE.md: no planning IDs in source comments. |
| `UiFoundationDesktopShellGuardTests.GroupPickerLayout_LinksItsOwnSheetAfterTheLegacyOnes` (`:151-157`) | `<body>` carries `group-picker`; `~/css/group-picker.css` linked after `legacy-pages.css`. | Not affected if the layout is untouched. | None. |

**Files that must NOT be referenced by name after deletion:** only `CalendarSubscriptionStaticGuardTests` loads `Views/Account/Profile*.cshtml` by path (which stay) and `account.mobile.css` by path (rows d, e, f). `BoardPageStaticGuardTests` skips files that do not exist. No test resolves `Login.Mobile`, `GroupPicker/Index.Mobile` or `Home/Index.Mobile` by path (grepped the two test projects; `AmbientClockSeamTests` lists none of these views), so deleting those twins breaks only the mobile-UA facts in the table.

## Common Pitfalls

### Pitfall 1: A new layered rule silently loses to an unlayered one
**What goes wrong:** the area sheet is `@layer pages`; `legacy-pages.css` and the other desktop sheets are unlayered and win regardless of specificity. A copy of a rule left in the old sheet masks the new one on desktop only.
**How to avoid:** delete the Profile block from `legacy-pages.css` and the matching rules from `account.mobile.css` in the commit that creates them in `account.css`; verify a computed style on the desktop render.
**Warning signs:** desktop and phone disagree after a conversion; DevTools shows the unlayered rule.

### Pitfall 2: Substring-counting tests
**What goes wrong:** `CountOccurrences(html, "calendar-subscription-row")` and `"calendar-subscription-address"` count substrings across the whole page. A class `calendar-subscription-row-actions`, or a CSS class on the desktop view containing `calendar-subscription-row`, changes the count or violates `NotContain("calendar-subscription-row")`.
**How to avoid:** name new classes with other stems (`subscription-item`, `subscription-actions`); keep exactly one occurrence of each marker per row; keep the desktop table class string `table table-striped table-hover align-middle` and the `<div>@name</div>` + `<div class="calendar-subscription-meta">` adjacency.

### Pitfall 3: Colour on `<button>`
**What goes wrong:** the picker row as a `button` renders UA button text colour (dark) on glass and a UA border and background.
**How to avoid:** the row rule resets `color: inherit; font: inherit; background: transparent; border: 0`. Check at the Gate 1 mockup render and a contrast probe.

### Pitfall 4: `.row` at the root of the phone `main`
**What goes wrong:** `mobile.css` sets `.container-fluid` to 0.5rem (8px) side padding; Bootstrap's `.row` margin is `-0.5 * 1.5rem = -12px`; the 4px difference is the document overflow (394 at 390). [CITED: 94-AUDIT-account.md A-account-038, which states the cause; the numbers match]
**How to avoid:** see "AccessDenied overflow fix" below. Account twins never hit it because they use no `.row`; a merged account view that keeps `.row/.col-md-6` will, unless the fix is in place.

### Pitfall 5: Focus not returning after the Delete dialog
**What goes wrong:** opening the modal programmatically (`new bootstrap.Modal(el).show()`) skips the data-api's focus-return wiring.
**How to avoid:** open through `data-bs-toggle="modal"` on the Delete button; confirm in the browser that Escape and Cancel return focus to that row's Delete (UAT item). Superseded by plan 96-07: the module opens the dialog through the Modal API and focuses the opening Delete on `hidden.bs.modal` itself, and its dialog probe checks Escape, Keep Subscription and the close button.

### Pitfall 6: Untrusted text into the confirm dialog
**What goes wrong:** the subscription name is user-editable (Rename); putting it in the dialog with `innerHTML` is stored XSS.
**How to avoid:** `textContent` only; the attribute is already HTML-encoded by Razor.

### Pitfall 7: Confirm sentence drift or loss in the move
**What goes wrong:** the sentence has an em dash and a closing question mark placement ('Delete "' + name + '? Any device ... — this cannot be undone. You can always create a new subscription afterward.'); a retype can change it, and the guard test pins both halves.
**How to avoid:** copy the static text verbatim from the current script into the modal body in both twins; only the name is dynamic.

### Pitfall 8: The empty-state sentence is pinned in two places
**What goes wrong:** D-10 changes "The address below acts like a password" but both `CopyContractStrings` and `Profile_RendersTheEmptyState_WhenTheMemberHoldsNoSubscription` assert the old clause.
**How to avoid:** change both tests and both views in one commit; the Gate 2 wording sign-off may then force a second edit of the same three places, so keep the clause in one constant per file.

### Pitfall 9: Parallel Phase 95 session in the same checkout
**What goes wrong:** both phases rewrite facts in `Mobile/MobileViewsTests.cs` (Phase 95 touches the Quest facts around lines 172-247 and 872-987; this phase touches 552-690), and both run `dotnet build`/`dotnet test` into the same `bin`/`obj`; CLAUDE.md already warns about locked output files.
**How to avoid:** do not execute the two phases concurrently in one checkout, or use separate worktrees (`use_worktrees` is on); keep Phase 96 out of `components.css`/`_Layout*.cshtml` except the single phone-gutter rule below (Phase 95 plans to add `.move-control-reason` to `components.css`).

### Pitfall 10: CRLF
**What goes wrong:** the working-tree sources are CRLF (`core.autocrlf=true`; e.g. `legacy-pages.css` has 1329 CR for 1329 lines). Edits that anchor on `\n`, or new files written with LF, produce mixed endings in diffs.
**How to avoid:** editors should match the file's endings; new files may be LF (git normalises) but do not mix inside one file. (Memory note: this repo's planning docs also have mixed endings.)

### Pitfall 11: Hidden address input in the tab order (A-account-024)
The `visually-hidden` readonly address input is focusable. Add `tabindex="-1"`; the reveal path calls `field.focus()` explicitly, which still works on `tabindex="-1"`.

## Code Examples

### AccessDenied overflow fix (single root-cause rule, recommended)
```css
/* shell.mobile.css, inside @layer components: a root-level row on the phone shell would otherwise be 4px
   wider than the screen, because the shell pads its main by 8px and a Bootstrap row pulls out by 12px. */
.mobile-layout main > .row {
  --bs-gutter-x: var(--qb-space-md);
}
```
`.row` declares `--bs-gutter-x` on itself, so the override must target `.row` (not `main`). Effect: row margin -8px, column padding 8px, card inset unchanged at 8px, no overflow. Scope `main > .row` limits the blast radius to pages whose root is a row (AccessDenied, Events/Details, merged Account views). [ASSUMED: derived from the audit's stated cause and the measured 394 vs 390; verify with the overflow probe at 390 and 320 before calling it done, and confirm Events/Details also clears for Phase 97.] The audit's alternative (AccessDenied only: drop `.row/.col-md-6` and centre with the card's own `max-width` in the `pages` layer) is acceptable but does not help `Events/Details`, whose root `<div class="row">` (`Views/Events/Details.cshtml:38`) is a real two-column grid.

### Profile R2, intrinsic wrapping inside the existing table (no media query)
```cshtml
<td>
  <div class="subscription-item">
    <div class="subscription-summary">
      <div>@subscription.Name</div>
      <div class="calendar-subscription-meta">Created @Html.LocalTime(subscription.CreatedAt, "date") ... </div>
    </div>
    <div class="subscription-actions"> ...five controls... </div>
  </div>
</td>
```
```css
@layer pages {
  .subscription-item { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: var(--qb-space-sm) var(--qb-space-md); }
  .subscription-summary { flex: 1 1 16rem; min-width: 0; overflow-wrap: anywhere; }
  .subscription-actions { display: flex; flex-wrap: wrap; gap: var(--qb-space-sm); }
  .calendar-subscription-meta { color: var(--bs-secondary-color); font-size: var(--qb-text-sm); }
}
```
This keeps the pinned table markup and the name/meta `<div>` pair, drops the second "Actions" column (so the header row has one `th`), needs no `@media`, and the actions wrap beneath the summary exactly when there is no room (Q-123 "narrow widths"); whether the actions should always sit beneath is a Gate 1 decision (open question 3). The phone twin keeps its own `calendar-subscription-row` blocks; the same classes can serve both if the planner wants one stylesheet vocabulary, but never the substring `calendar-subscription-row-...`.

### Shared script skeleton (Copy restore bug fixed)
```js
// wwwroot/js/profile-subscriptions.js (module, no imports)
function wireCopyButtons() {
  document.querySelectorAll('.calendar-subscription-copy-btn').forEach((button) => {
    const original = button.innerHTML;            // icon-only on the phone, icon + "Copy" on desktop
    const restore = () => setTimeout(() => {
      button.classList.remove('btn-success'); button.classList.add('btn-primary'); button.innerHTML = original;
    }, 2000);
    button.addEventListener('click', () => { /* same clipboard + manual-fallback logic as today */ });
  });
}
const deleteModal = document.getElementById('deleteSubscriptionModal');
deleteModal?.addEventListener('show.bs.modal', (event) => {
  const trigger = event.relatedTarget; if (!trigger) return;
  deleteModal.querySelector('input[name="id"]').value = trigger.dataset.subscriptionId ?? '';
  deleteModal.querySelector('[data-subscription-name-target]').textContent = trigger.dataset.subscriptionName ?? '';
});
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `div` + `onclick` card as the control | Native `button type="submit"` row inside the form | This phase (A-account-031) | Keyboard, switch and screen-reader users can choose a board. |
| `window.confirm()` | Foundation opaque Bootstrap modal | This phase (D-11) | Branded, keyboard-trapped, closable with Escape. |
| Per-twin glass wrapper `.account-card-mobile` with `!important` colour overrides | `.modern-card` surface owns text colour; area sheet is layout only | Phase 94 foundation | Removes 11 `!important`, 14 `rgb()`, 7 halos. |
| Duplicated inline script | One cached script file | This phase | One place to fix the Copy-label bug. |

**Deprecated/outdated:** `.account-card-mobile`, `.account-section-heading`, `.account-field-*`, `.account-subtitle` (dead), the `account.mobile.css` header comment (lists a deleted Register view), the stale view comment saying `.modern-card` "applies a backdrop filter" (the foundation card has none; the modals-outside-the-card practice stays, with the comment corrected to the real reason: a transformed or filtered ancestor becomes the containing block for fixed descendants).

## Slice proposal (D-06 order, link-then-remove)

1. **Checkpoint plan (D-04, D-05):** operator confirms SQL Server on 1433 (it is reachable here: port 1433 accepted a connection; never provisioned by the agent), bootstraps the admin; executor rebuilds scratch tooling under `~/qb-ui-review/phase-96/` (Phase 95's research lists the Windows adaptation: Edge via `channel: 'msedge'`, `devices['Pixel 7']`, `dotnet run --project QuestBoard.Service --urls http://localhost:8000`); do not depend on Phase 95's scratch if it is not there; fixtures through the forms; BEFORE captures incl. populated Profile.
2. **Gate 1 package (scratch only):** picker mockup (flat rows, long names, 1 row and 12 rows, no-groups alert, super admin Go to Platform), Profile mockup (populated, wide and narrow, row-action mapping), `Home/Index` unified look and merge proposal. Blocking.
3. **Slice A picker:** merge, R1, keyboard fix, `group-picker.css` rows, delete `Index.Mobile.cshtml`, rewrite the GroupPicker fact.
4. **Slice B Profile:** create `account.css` (Profile rules from both sheets), move the Profile block out of `legacy-pages.css` and the calendar rules out of `account.mobile.css` in the same commit, shared script, delete modal, D-10, twin alignment (Q-81, Q-84), `dl/dt/dd`, `.empty-state`, R2, `tabindex="-1"`, rewrite `CalendarSubscriptionStaticGuardTests`/`ProfileCalendarSubscriptionTests`/Profile fact, add `account.css` to `LayeredFiles`/`NoLiteralFiles`.
5. **Slice C credentials:** four merges, button mapping, validation line, autocomplete, titles, 44px link; rewrite the Login fact. Also lands the phone-gutter rule here or in D (must precede any merged view that keeps `.row`).
6. **Slice D:** Edit twins, AccessDenied (+ overflow proof), `Home/Index` per Gate 1; then delete `account.mobile.css` once no view links it.
7. **Guard plan:** `AccountConversionGuardTests` (DoC 1-3 over the converted views and sheets: no `style="`, `<style`, inline `<script` (a `<script src=` is fine), `on\w+=`, no hex/rgb/`!important` in `account.css`/`group-picker.css`, `@layer pages`, each Account view links `account.css`, `account.mobile.css` gone).
8. **AFTER + UAT + Gate 2 plan:** AFTER capture both UAs incl. the extra states, SetPassword with the mailed link (D-09), 4.5:1 and 44px probes, overflow at 390/320, UAT list, review sheet, release prep (operator-timed).

Plan count is the planner's call; 8 plans fits the above.

### Button-class mapping (recommendation for A-account-014/017/024, shown at Gate 1/2)
| Page | Control | Today | Recommended |
|---|---|---|---|
| Login | Log in | `btn-warning btn-lg` | `btn-primary btn-lg` |
| ForgotPassword | Send reset link | `btn-warning btn-lg` | `btn-primary btn-lg` |
| SetPassword / ChangePassword | commit | `btn-primary` | `btn-primary` (unchanged class, new fill) |
| ChangePassword / Edit | Cancel | `btn-secondary` | `btn-secondary` |
| Edit | Save Changes | `btn-success` | `btn-primary` |
| Edit (both twins) | Change Password | desktop `btn-outline-primary`, phone `btn-warning` | `btn-secondary` on both |
| Profile | Edit Profile | `btn-primary` | per UI-SPEC table `btn-warning` is "edit or caution"; keep `btn-primary` unless the operator prefers the table's reading (open question 4) |
| Profile | Back to Quest Board | `btn-warning` | `btn-secondary` (navigation) |
| Profile | Add Subscription | `btn-warning` | `btn-primary` (the page's one mutation) |
| Profile row | Copy / Open in Calendar App / Show QR Code | three `btn-primary btn-sm` | `btn-secondary btn-sm` or `btn-info btn-sm` so a row has no more than one wood-fill commit look; show at Gate 1 |
| Profile row | Rename / Delete | `btn-warning` / `btn-danger` | unchanged |
| AccessDenied / Home | Back to Quest Board / Log In | `btn-warning` | `btn-secondary` / `btn-primary btn-lg` |

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.400 | — |
| Node.js | scratch Playwright tooling | yes | v22.16.0 | — |
| Microsoft Edge | screenshots (`channel: 'msedge'`) | yes | 156.0.4314.58 and 157.0.4341.42 present under Edge Application | Chromium download into scratch |
| Playwright | screenshots, probes | no (not installed globally) | — | install into the scratch folder at the D-05 checkpoint |
| SQL Server `localhost:1433` | running the app for captures | port accepts connections (probe this session) | — | operator confirms; never provisioned by the agent; tests need none (EF InMemory) |
| SMTP relay + operator mailbox | SetPassword link (D-09) | operator-side | — | operator checkpoint; no app code added |
| `rg` (ripgrep) | n/a | not on PATH (the `rtk` wrapper falls back) | — | use the Grep tool |

**Missing dependencies with no fallback:** none for planning. **Execution-time:** the operator mailbox for D-09 and the admin bootstrap (D-05) are human checkpoints.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 + FluentAssertions 8.10.0; `Microsoft.AspNetCore.Mvc.Testing`; EF InMemory (no database) [VERIFIED: IntegrationTests csproj:13-21] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`) [CITED: 95-RESEARCH.md Validation Architecture] |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MobileViewsTests|FullyQualifiedName~ProfileCalendarSubscriptionTests|FullyQualifiedName~CalendarSubscriptionStaticGuardTests|FullyQualifiedName~GroupPickerControllerIntegrationTests|FullyQualifiedName~CrossBoardPickerSkipTests|FullyQualifiedName~UiFoundation|FullyQualifiedName~MobileCssTests|FullyQualifiedName~MobileLayoutTests|FullyQualifiedName~HomeControllerIntegrationTests|FullyQualifiedName~AccountControllerIntegrationTests|FullyQualifiedName~AccountConversionGuardTests"` (filter syntax cited from Phase 95, which measured 173 integration tests in 13s after build; I did not run the suite here because a build would write `bin/obj` in the checkout the Phase 95 session shares) |
| Full suite command | `dotnet test` (needs no database); re-run once on a transient `0x80131506`; a failure of `FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour` in the first minute of 20:00 UTC is a recorded flake [CITED: 95-RESEARCH.md] |
| Browser checks | scratch Playwright (not in the suite), outputs in `~/qb-ui-review/phase-96/` |

### Phase Requirements -> Test Map
| Req | Behavior | Test Type | Automated Command | File Exists? |
|-----|----------|-----------|-------------------|-------------|
| DoC-1 | no `style=`/`<style>`/inline `<script>`/`on*=` in the converted views | static guard | `--filter "FullyQualifiedName~AccountConversionGuardTests"` | Wave 0 gap (new) |
| DoC-2 | `account.css`, `group-picker.css`: no colour literal, no `!important`, open `@layer pages {` | static guard | `--filter "FullyQualifiedName~UiFoundationLayerGuardTests"` (after adding `account.css` to the arrays) | exists, edit |
| DoC-3 | Account views link `account.css`; picker layout still links `group-picker.css`; `account.mobile.css` gone; `legacy-pages.css` has no `calendar-subscription` | static guard | `AccountConversionGuardTests`; `UiFoundationDesktopShellGuardTests` | Wave 0 + exists |
| DoC-4 | titles on every merged/phone view; Q-82/83/84 wording; merged pairs render under both UAs | integration | `MobileViewsTests` (rewritten Login, GroupPicker, Edit, Profile facts) + new title facts | exists, rewrite |
| DoC-8 | rewritten guards pin the same intent | static | `CalendarSubscriptionStaticGuardTests`, `ProfileCalendarSubscriptionTests` | exists, rewrite |
| D-10/D-11 | empty-state clause on both layouts; delete modal markup on both layouts; no `confirm(`; script file holds "Copied!" and "Press Ctrl+C" | integration + static | `ProfileCalendarSubscriptionTests`, `CalendarSubscriptionStaticGuardTests` | exists, rewrite |
| R1 | each board is a `<button type="submit"` in a `SelectGroup` form with `groupId` and `returnUrl`; oracle parity unchanged | integration | `GroupPickerControllerIntegrationTests`, `CrossBoardPickerSkipTests` + a new markup fact in `MobileViewsTests`/`GroupPicker` | exists + 1 new fact |
| D-07 | `/` desktop UA never contains `mobile.css` / `mobile-layout`; contains "Log In" and `/Account/Login` | integration | `MobileCssTests`, `MobileLayoutTests`, `HomeControllerIntegrationTests` | exists |
| X-4 | no horizontal overflow at 390 (phone UA) and 320 (desktop) on AccessDenied, Profile populated | scratch browser probe | scratch `audit-shoot` overflow flag | scratch |
| DoC-5/7 | contrast 4.5:1 (text), 3:1 (non-text), 44px phone targets, focus ring visible and unclipped, Tab reaches every board row and Enter/Space selects, Escape closes and focus returns in the delete modal | scratch browser probe + manual UAT | probe list in UAT | scratch |
| X-3 | SetPassword renders, submits, and redirects with a mailed token | manual (operator-supplied link) | UAT item | manual-only, justified: token cannot enter the repo |

### Sampling Rate
- **Per task commit:** the quick filter for the slice's touched pages (picker slice: `GroupPickerControllerIntegrationTests|CrossBoardPickerSkipTests|MobileViewsTests|UiFoundation`; Profile slice adds `ProfileCalendarSubscriptionTests|CalendarSubscriptionStaticGuardTests|AccountControllerIntegrationTests`; credentials/Edit/AccessDenied/Home slices add `AccountControllerIntegrationTests|HomeControllerIntegrationTests|MobileCssTests|MobileLayoutTests`) plus the scratch probe on the touched pages under both UAs.
- **Per wave merge:** `dotnet test` (full).
- **Phase gate:** full suite green and `dotnet build` clean before `/gsd-verify-work`; AFTER sweep with `readability-compare` against BEFORE showing no new contrast failure and no control hidden.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs` — DoC-1..3 over the converted views and sheets
- [ ] New mobile-UA facts: title present on each merged/twin phone view; picker row markup; Q-84 wording on phone Profile
- [ ] Scratch tooling and fixtures (plan 1), outside the repo
- Framework install: none

## Security Domain

`security_enforcement` is absent in `.planning/config.json`, which means enabled.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes (sign-in, reset, set/change password pages) | Behaviour unchanged (Identity). Markup adds `autocomplete` tokens and keeps paste allowed (no `onpaste`); do not add any client-side password handling. |
| V3 Session Management | no change | Cookie/session unchanged; the picker still posts `SelectGroup`. |
| V4 Access Control | yes (picker) | `SelectGroup` membership check stays server-side (`GroupPickerController.SelectGroup`); the row is only a submit control; `returnUrl` still passes `Url.IsLocalUrl`. |
| V5 Input Validation | yes | Server-side ModelState unchanged (8/6-character minimums out of scope); subscription name rendered with Razor encoding and `textContent`. |
| V6 Cryptography | no | None added. |
| V13/V14 config | note | No Content-Security-Policy header exists today (adopting one is a recorded deferred item); keeping the pages free of inline script/style makes a later strict policy cheap. |

### Known Threat Patterns
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stored XSS via the subscription name in the delete dialog | Tampering/Elevation | `textContent`, never `innerHTML`; attribute encoded by Razor |
| CSRF on revoke, rename, add, select-board | Tampering | Keep `@Html.AntiForgeryToken()`/tag-helper token in every POST form including the new shared delete form |
| Bearer address exposure (the subscription address is a never-expiring credential) | Information disclosure | Keep the address input `visually-hidden`, reveal only in the clipboard-denied fallback; `tabindex="-1"` while hidden; never render it as visible text; do not add it to any `data-*` attribute |
| Account/board enumeration | Information disclosure | Keep the forgot-password message and the picker's identical-response behaviour (oracle parity test) unchanged |
| Reset token leakage during verification | Information disclosure | D-09: link and token stay in the scratch folder only; never in the repo, a commit or a log |
| Open redirect via `returnUrl` | Tampering | Hidden input value unchanged; controller `RedirectToLocal` unchanged |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The one-line rule `.mobile-layout main > .row { --bs-gutter-x: var(--qb-space-md) }` clears the AccessDenied and Events/Details 4px overflow with no visual regression elsewhere (derived from the audit's stated cause and the 394/390 measurement, not measured) | Code Examples; X-4 | Overflow stays or other root-row phone pages shift by 4px; fall back to the audit's AccessDenied-only wrapper and let Phase 97 fix Details separately. Verify with the overflow probe first. |
| A2 | A single-cell flex-wrap inside the existing table satisfies R2 ("actions drop beneath the name and dates") with no `@media` | Code Examples | The operator wants the actions always beneath, or a different structure; resolved by the Gate 1 mockup. |
| A3 | The picker row should be a flat row styled in `group-picker.css` rather than `.modern-card--interactive` | Alternatives; Pattern 2 | The operator prefers each board as a lifted glass card; trivially switchable at Gate 1. |
| A4 | `account.css` is an acceptable name and the Account desktop twins linking it through `@section Styles` is within the roadmap's "no sheet leaves a desktop layout's global list" wording | Summary | A different name only changes strings in three test files and the guard test. |
| A5 | A server-rendered `role="alert"` summary present at page load is announced by assistive tech | Pattern 3 | Errors stay silent to screen-reader users; add an explicit live region or focus management after a UAT screen-reader pass. |
| A6 | UA default `button` colour/background/border must be reset on the picker row (standard browser behaviour) | Pattern 2 | None if wrong beyond redundant declarations; the mockup render proves it either way. |
| A7 | Per-slice quick-test filters and timings come from Phase 95's measurements, not a run here | Validation Architecture | Slower or differently-named filters; the planner should smoke-run one filter at the first task. |
| A8 | The recommended button-class mapping (Profile Add Subscription `btn-primary`, row Copy/Open/QR neutral) matches the operator's taste | Button-class mapping | Pure presentation; shown at Gate 1/2. |

## Open Questions (RESOLVED)

1. **Does the Home/Index merge get a yes?** (D-07). What we know: the pair is content-identical, no test pins the `.Mobile` file (the only Home tests are `HomeControllerIntegrationTests`, `MobileCssTests`, `MobileLayoutTests`, all UA-agnostic apart from the two negative string checks); a merged view needs no stylesheet except one `.mobile-layout` rule for the full-width button. Recommendation: put the proposal and the unified look at Gate 1; plan both outcomes (merge deletes `Index.Mobile.cshtml`; decline converts each twin to the same look and keeps `.Mobile`). **RESOLVED:** by CONTEXT D-07 and D-08: the merge goes to Gate 1 as a proposal and merges only on the operator's explicit yes, Gate 1 item 9 (no default), with the unified look approved as item 10 whichever way item 9 goes (96-03 Task 3 builds the proposal, 96-04 Task 3 records `home.merge` and `home.lookApproved`); 96-10 Task 1 implements both outcomes.
2. **Flat row versus lifted card for the picker** (A3). Recommendation: flat row, per R1's wording; show a one-board, a twelve-board and a long-name case. **RESOLVED:** Gate 1 item 1 (flat row recommended, lifted card the alternative; recorded as `picker.rowShape`), with the mockup approved as item 4; 96-03 draws the one, three and twelve-board and long-name cases, and 96-05 builds the approved shape.
3. **Q-123 scope: always beneath, or only when narrow** (A2). Recommendation: intrinsic wrap (beneath only when there is no room) unless the operator wants a stable stacked row at every width. **RESOLVED:** Gate 1 item 5 (intrinsic recommended, always beneath the alternative; recorded as `profile.reflow`), with the Profile mockup approved as item 8; 96-06 builds the answer.
4. **Profile "Edit Profile" button role** (`btn-primary` today; the UI-SPEC table reads `btn-warning` as "edit or caution"). Cosmetic; settle at Gate 2. **RESOLVED:** ships as `btn-warning` (96-06 Task 3, per the UI-SPEC button-role table) and is confirmed or changed at Gate 2, where 96-12 Task 2 lists `Edit Profile` as `btn-warning` among the Gate 2 items with a yes or change question.
5. **Where the AccessDenied overflow fix lives** (A1). Recommendation: the one phone-shell rule in `shell.mobile.css` (a foundation sheet) so Phase 97 only verifies; confirm with the operator that a one-rule edit to a foundation sheet in this phase is acceptable, otherwise use the AccessDenied-local fix. **RESOLVED:** Gate 1 item 12 (one rule in `shell.mobile.css` recommended, the AccessDenied-local fallback the alternative; recorded as `gutter`), measured for both placements in 96-03 Task 3; 96-08 Task 1 applies the answer and 96-09 Task 2 proves it on AccessDenied and Events/Details.
6. **Should `Home/Index` and `AccessDenied` link `account.css`?** They need at most two rules (`.mobile-layout` full-width button, title icon). Recommendation: yes, to avoid a second area sheet for two small pages. **RESOLVED:** adopted as recommended (no operator question needed: it is the stylesheet consolidation left to Claude's discretion in CONTEXT): 96-09 Task 2 links `account.css` from AccessDenied and 96-10 Task 1 links it from Home/Index; the credential pages (96-08) and Edit (96-09 Task 1) use the same sheet, so the area has one sheet.

## Sources

### Primary (HIGH confidence, read this session)
- `.planning/phases/96-account-and-pickers-ui-conversion/96-CONTEXT.md`; `.planning/ROADMAP.md` § Phase 96 (lines 1587-1646) and Phase 97 context
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-account.md` (findings table, twin comparison, merge/relayout proposals, inventory, scope notes); `94-UI-SPEC.md` lines 160-190, 716
- `.claude/ui-guidelines.md`, `CLAUDE.md`, `.claude/architecture.md`, `.claude/project-overview.md`
- All 17 view files, `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml`, `_ViewStart.cshtml`, `MobileViewLocationExpander.cs`, `MobileDetectionMiddleware.cs`, `HomeController.cs`, `GroupPickerController.cs`, `AccountController.cs` (head)
- `wwwroot/css/{tokens,base,components,shell.mobile,app,mobile,account.mobile,group-picker,home.mobile}.css`, `legacy-pages.css` lines 1255-1329, `wwwroot/js/site.js` and `contact-tags.js` (conventions)
- `wwwroot/lib/bootstrap-5.3.8/js/bootstrap.bundle.min.js` (Modal class, data-api handler, focus-trap defaults)
- Tests: `MobileViewsTests` 540-700, `MobileCssTests`, `UiFoundationLayerGuardTests` 25-284, `CalendarSubscriptionStaticGuardTests` (whole), `ProfileCalendarSubscriptionTests` 20-340 + grep, `GroupPickerControllerIntegrationTests`, `CrossBoardPickerSkipTests` 175-210 and 380-434, `HomeControllerIntegrationTests`, `BoardPageStaticGuardTests` 95-200, `UiFoundationViewLibraryGuardTests`, `MobileLayoutTests` 20-70

### Secondary (MEDIUM)
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-RESEARCH.md` (scratch tooling adaptation, validation filters, `QuestsConversionGuardTests` precedent, parallel-edit hazards)

### Tertiary (LOW)
- Assumptions A1, A5 (unmeasured); everything else tagged above

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new dependency; versions read from committed files.
- Architecture (twin/layout/section mechanics): HIGH, read from the expander, middleware, `_ViewStart` and all three layouts.
- Guard-test constraints: HIGH, every pinned string and line range was read this session.
- Design recommendations (flat row, R2 wrap, gutter fix, button mapping): MEDIUM, they wait on Gate 1 mockups and one browser measurement.
- Pitfalls: HIGH for the test-driven ones; MEDIUM for the screen-reader announcement.

**Research date:** 2026-11-10
**Valid until:** 2026-12-10 (stable; invalidated if Phase 95 lands edits to `components.css`, `MobileViewsTests.cs`, or the layouts first)
