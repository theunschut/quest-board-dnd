---
status: complete
phase: 75-event-availability-signups
source: [75-VERIFICATION.md, 75-VALIDATION.md]
started: 2026-08-28T10:14:49Z
updated: 2026-08-28T09:07:13.634Z
---

## Current Test

[testing complete]

## Tests

### 1. Roster and three availability buttons render correctly on a real mobile device
requirement: EVTAVAIL-01/03
expected: Buttons and roster usable at mobile size; layout does not break; page matches the app's mobile design language.
result: passed — after a fix. **This test failed on first run.**

**What failed:** every card on the event pages rendered as an opaque white Bootstrap card,
instead of the frosted-glass style used everywhere else in the mobile UI. Found by the user
on visual inspection, not by the structural checks.

**Root cause:** the mobile layout loads `mobile.css` and never loads `site.css`, and
`.modern-card` was defined only in `site.css`. Any view without a `.Mobile.cshtml` variant
therefore fell back to Bootstrap's default white `.card` on phones. The entire `Events/`
folder has no mobile variants, so all three event pages were affected. This is exactly the
failure mode this test was written to catch.

**Fix:** commit `210c471` — extracted the self-contained `.modern-card` block out of
`site.css` into `wwwroot/css/modern-card.css` and linked it from all three layouts
(`_Layout`, `_Layout.Mobile`, `_Layout.GroupPicker`). A pure move: no rule bodies changed.
Desktop computed styles were captured before and after and are identical, so there is no
desktop regression. The block carries its own text-colour rules, so cards that flip to
translucent also flip their text from dark to parchment and stay readable.

**Also fixed by the same change** (same root cause, outside phase 75's scope):
`Events/Create`, `Events/Edit`, `Shared/AccessDenied`, `Admin/EmailStats`,
`Quest/_QuestCard`, `Quest/_QuestSection`, `Shared/_ShopItemDetailsContent`.

**Verified after fix:** card `rgba(255,255,255,0.15)` + `blur(15px)`, headings and body text
parchment `rgb(244,228,188)`, roster table does not overflow, all tap targets >= 44px
(answer buttons are 62px tall), full answer -> change -> withdraw lifecycle works at 375x812.
Contacts mobile confirmed unchanged.

**Open caveat:** verified under device emulation, not on physical hardware. This test's own
rationale notes that emulation has previously masked a live mobile bug in this codebase, so
a glance on a real phone is still worth doing before trusting it fully.

### 2. Both confirmation dialogs read correctly
requirement: D-24, D-25
expected: Delete-event and remove-member dialogs both appear and state what will be lost.
result: passed — after a fix.

The remove-member dialog was **broken by this phase and fixed within it** (`62cfa06`). The
handler embedded an apostrophe as `&#39;` inside a single-quoted JS string; attribute values
are HTML-decoded before being parsed as JS, so the decoded apostrophe terminated the string
early, the handler never compiled, and the browser treated it as absent — the form submitted
with no confirmation at all.

Verified live in-browser rather than by reading code:

| Dialog | Handler compiles | Fires with correct text | Cancel blocks submit |
|---|---|---|---|
| Remove member (all 22 forms) | yes | yes | yes — returns `false` |
| Delete event | yes | yes, with live signup count | yes |
| Withdraw answer | yes | yes | yes |

The pre-fix markup was rebuilt in the same browser engine and produced **no handler at all**,
confirming the bug was real and the fix addresses it. The Withdraw dialog proved itself
incidentally: the test browser suppresses native dialogs, so the first withdraw click was
auto-cancelled and the answer correctly stayed put.

**Second defect found during this test:** the delete dialog read "1 people have signed up" —
`Details.cshtml` hardcoded "people" regardless of count. Fixed in `ed986c8`, which picks
person/people and has/have from the count, and stops a zero-signup delete from claiming
availability will be lost when there is none. Regression tests were added for the singular
and zero cases and confirmed to fail against the old wording. The pre-existing test only
seeded a two-signup event, which is why the bug survived it.

**Open caveat:** the test browser suppresses native dialogs, so these were verified to fire
with the correct text, not to render correctly on screen.

### 3. Answering a past-dated event is acceptable in practice
requirement: PD-01
expected: A product-intent judgment on whether past events should stay answerable.
result: passed — confirmed correct as-is by the user.

Verified live: on an event dated 26 August with the current date 28 August, answering,
changing the answer, and withdrawing all worked. Decision: availability is a record of who
was actually there, so past events remain editable to let the record be corrected after the
session. No code change required.

## Summary

total: 3
passed: 3
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

None blocking. Two follow-ups worth tracking separately, neither owned by this phase:

- `Events/` has no `.Mobile.cshtml` variants at all, unlike 49 other views. The styling
  symptom is fixed, but the structural inconsistency remains and predates phase 75.
- Both mobile and dialog checks were verified under emulation with native dialogs
  suppressed. A pass on real hardware would close the last gap.
