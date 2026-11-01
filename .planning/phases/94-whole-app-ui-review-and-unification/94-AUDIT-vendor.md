# Phase 94 Audit: Dead Dependencies (jQuery and the Font Awesome v4 shim)

Proofs that the two dependencies the layouts still loaded from a CDN are unused, recorded with the command, the result and a verdict, before each was removed. `QBR` is `~/qb-ui-review/phase-94`; scratch scripts live under `$QBR/plans/94-13/`. Screenshots are never committed.

## jQuery

jQuery 3.6.0 was loaded from cdnjs, without a subresource integrity attribute, by all five layouts (`_Layout`, `_Layout.Mobile`, `_Layout.GroupPicker`, `_Layout.Platform`, `_Layout.Platform.Mobile`). It is removed from all five. All four proofs passed, so it was dropped and not self-hosted.

### Proof 1

Search of the whole front-end source for any use of jQuery.

Command (from `QuestBoard.Service/`):

```
grep -rnE '\$\(|\$\.|jQuery|\.ajax\(|\.on\(' Views Areas Components wwwroot/js \
  --include='*.cshtml' --include='*.razor' --include='*.js' \
  | grep -vE '\$toCanvas|\$center|\$initSelection'
```

Result: 1 hit, `wwwroot/js/markdown-editor.js:75: easyMDE.codemirror.on('change', function () {`. The receiver is the CodeMirror editor instance EasyMDE exposes (`easyMDE.codemirror`), whose own `.on` registers an event listener; it is not jQuery. The Cropper v2 method names `$toCanvas`, `$center` and `$initSelection` are excluded by the filter. Zero `$(`, zero `$.`, zero `jQuery`, zero `.ajax(` hits. `wwwroot/lib` holds Bootstrap 5.3.0 (no jQuery dependency), Font Awesome, Cinzel, EasyMDE, Tagify and Cropper.js v2 (none use jQuery).

Verdict: pass. Nothing in the application source uses jQuery.

### Proof 2

Every inline `<script>` body in the views, searched for an identifier-style use of `$` or `jQuery`.

Command: `python3 -I $QBR/plans/94-13/proof2.py QuestBoard.Service`. It reads every `<script>` without a `src` attribute in `.cshtml` and `.razor` under Views, Areas and Components and flags `$(`, `$.`, `$[`, `$name` and `jQuery` (excluding template-literal `${` and the Cropper method names).

Result: 143 files scanned, 55 inline script blocks, 0 hits.

Verdict: pass.

### Proof 3

Console check on the five layouts under both User-Agents, with the jQuery tag removed. Run: `node $QBR/plans/94-13/flows.mjs` against the working tree on port 8013 (Audit Board, roles from the audit storage states). The log is `$QBR/plans/94-13/flows-task1.log`.

| Layout | User-Agent and route | Result |
| --- | --- | --- |
| `_Layout` | desktop, `/quests` (player) | no page error, no console error, `window.bootstrap` is an object, `window.jQuery` undefined |
| `_Layout.Mobile` | mobile, `/quests` (player) | same |
| `_Layout.GroupPicker` | desktop and mobile, `/groups/pick` (SuperAdmin) | same |
| `_Layout.Platform` | desktop, `/platform/Group` (SuperAdmin) | same |
| `_Layout.Platform.Mobile` | mobile, `/platform/Group` (SuperAdmin) | same |

Every row also has no request whose URL contains `jquery`. The same page-error and console-error assertions run on every Proof 4 page.

Verdict: pass. Zero page errors and zero console errors mentioning `$` or `jQuery`.

### Proof 4

Smoke pass of every script-driven flow, under both User-Agents (desktop 1440x900 and a Pixel 7 profile). Script: `$QBR/plans/94-13/flows.mjs`.

| Flow | Check | Result |
| --- | --- | --- |
| Success toast | Saving Account Edit unchanged as the audit player account redirects to Account Profile with a Success toast ("Profile updated successfully!"); the toast carries the `show` class and then loses it after its 5 second delay | pass on both |
| Tooltips | The only `data-bs-toggle="tooltip"` elements in the application are the two Markdown editor hints (`_MarkdownEditor.cshtml`); the Quest board itself has none. On `/Quest/Create` (DM) every such element has a `bootstrap.Tooltip` instance (2 of 2) | pass on both |
| Character-select modal | On `/Quest/Details/<quest.finalized>` as the player the trigger opens `#characterSelectModal` and the dismiss button closes it | pass on both |
| Image crop | On `/Characters/Create` choosing a 400x400 PNG opens `#cropPhotoModal`; `cropper-canvas` is laid out (766x400 desktop, 362x320 mobile) | pass on both |
| Markdown editor | `/Quest/Create` shows the EasyMDE toolbar with 7 buttons, each icon computing a Font Awesome glyph (U+F032, F033, F1DC, F0CA, F0C1, F10D, F06E) | pass on both |
| Date voting | See below | pass on both |

Date vote. The vote was cast only on the voting quest the fixture step created: `fixtures.json` entry `quest.voteFixture`, id `992004`, `created: true`. The script asserts `created` is true before voting and stops otherwise; it logged `quest id 992004` for both User-Agents and the final assertion "vote only on quest.voteFixture" passed. Desktop: the audit player was not signed up, picked Yes on the first proposed date (and Maybe on the second), pressed "Sign Up with Votes", and the reloaded page showed Yes on date 1. Mobile: the signed-up player opened the update form, flipped date 1 to Maybe, pressed "Update Votes", and the reloaded page showed Maybe. This is a data write through the application's own change-tracked signup flow on the local audit database, on a row the fixture step created (a data write allowed for this verification); no code path writes to Events or Quests and no code changed. At the end of the run the script revoked that signup through the app's own "Revoke My Signup" flow, so the Quest board card for the quest is back to "Adventurers signed up: 0" and later zero-difference captures are not disturbed.

The Success toast was raised by saving the audit player's own account unchanged (Account Edit, name, email and key flag as loaded); that account was created by the audit bootstrap, not by a member.

Verdict: pass. 97 PASS lines, 0 FAIL, exit 0.

### Gate

`c2-before` (captured before the first edit) against `c2-after-jq`: PASS, 26 captures (7 default gate pages plus the six form pages of the `94-12` extra routes, under both User-Agents), 0 differing pixels on every capture, no new console error, page error or failed request, no status change. A first `c2-after-jq` capture showed 5,453 and 29,127 differing pixels on `/quests`; the overlay showed it was the quest card for 992004 reading "Adventurers signed up: 1" with a seal, caused by the Proof 4 vote taken before that capture and not by the change. After the vote was revoked the capture was repeated and is exactly zero.

Final verdict: jQuery removed from all five layouts; self-hosting not needed.

## Font Awesome v4 shim

The compatibility stylesheet `v4-shims.min.css` (Font Awesome 6.4.0, cdnjs, SRI-pinned) was loaded by the two layouts that host a Markdown editor, `_Layout.cshtml` and `_Layout.Mobile.cshtml`. The other three layouts never loaded it.

### Font Awesome glyph proof

Command: `python3 -I $QBR/plans/94-13/glyphs.py` (fontTools 4.61.1). It collects every `(prefix, fa-name)` in `.cshtml`, `.razor`, `.cs`, `.js` and `.css` under QuestBoard.Service (Views, Areas, Components, wwwroot excluding `wwwroot/lib`, plus Controllers, ViewModels, Helpers, Services, Extensions, Constants, ViewExpanders) and adds the seven class names EasyMDE assigns to its default toolbar (`fa fa-bold`, `fa fa-italic`, `fa fa-header`, `fa fa-list-ul`, `fa fa-link`, `fa fa-quote-left`, `fa fa-eye`). For each it finds the `.fa-<name>:before` rule and codepoint in the self-hosted 6.4.0 `all.min.css` and checks the codepoint against the cmap of the matching self-hosted woff2 (`fa`, `fas` and `fa-solid` to fa-solid-900, `far` and `fa-regular` to fa-regular-400, `fab` and `fa-brands` to fa-brands-400).

Result: 319 files scanned, 143 distinct uses, 137 glyph names resolved, 6 styling helper classes with a rule, 0 misses. The base `.fa` rule sets `font-family: var(--fa-style-family, "Font Awesome 6 Free")` and `font-weight: var(--fa-style, 900)`; the `.fas`/`.far`/`.fa-solid` family rule is present. The EasyMDE classes resolve to U+F032, F033, F1DC (`fa-header` is an alias of `fa-heading`), F0CA, F0C1, F10D and F06E.

Verdict by the plan's test: pass, zero misses.

### Gate result: the proof was not sufficient

The shim was removed from both layouts (and the stale comment in `markdown-editor.js` rewritten) and the app rebuilt. Gate `c2-before` against `c2-after-shim-dropped` (kept as evidence): FAIL on every page that hosts an editor, 117 to 236 differing pixels on desktop and 769 to 1,528 on mobile, no new error and no status change. The overlay and crops show exactly one cause: the editor toolbar's preview icon. With the shim it is the outline eye; without it, the solid eye. The shim contains `.fa.fa-eye,.fa.fa-eye-slash{font-family:"Font Awesome 6 Free";font-weight:400}`, a restyle, not a missing glyph, so a proof that only checks that each name resolves to a glyph cannot see it. EasyMDE renders its preview button as `<i class="fa fa-eye">`.

Judgement: a different icon style on every editor toolbar is visible, so the removal was reverted and the plan's fallback applied. The shim is not dead; it carries one visible effect on exactly one icon class (`fa fa-eye` in the EasyMDE toolbar; `fa-eye-slash` is not used).

### Fallback applied: self-hosted shim

The exact bytes of the shim were fetched from the URL the layouts used and saved as `QuestBoard.Service/wwwroot/lib/fontawesome-6.4.0/css/v4-shims.min.css` (27,593 bytes, no `@font-face` and no `url()` inside, so no further file is needed). Both layouts point at `~/lib/fontawesome-6.4.0/css/v4-shims.min.css` (same position, same attributes minus `integrity` and `crossorigin`, which apply to cross-origin loads only).

| File | Source URL | SHA-256 |
| --- | --- | --- |
| fontawesome-6.4.0/css/v4-shims.min.css | https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/v4-shims.min.css | d3037f42050da6c98f31f801c26a5fe3df7327240ce40314b805e5e25b052ea2 |

Its SHA-384 is `TjXU13dTMPo+5ZlOUI1IGXvpmajjoetPqbUJqTx+uZ1bGwylKHNEItuVe/mg/H6l`, equal to the integrity value the layouts carried, so the committed bytes are the ones browsers already verified. The existing licence file in the directory covers it.

Guard: `Layouts_LoadTheShimLocally` (no layout requests a Font Awesome stylesheet from a CDN; the two editor layouts load the shim from the versioned directory).

Follow-up for the editor re-skin: the shim can go once the preview icon's outline weight is set by one rule of the application's own for the toolbar button; that is a visible-change decision, not a foundation change.

### Gate

`c2-before` against `c2-after-shim` (shim self-hosted): PASS, 26 captures, 0 differing pixels on every capture, no new error. The EasyMDE toolbar crop on `/Quest/Create` is identical to the capture before the plan (0 differing pixels on desktop and mobile; the dropped-shim capture differs by 117 and 766 pixels in the same crop). No request leaves localhost on any capture. `flows.mjs --strict-external` (both User-Agents, same script as Proof 4 plus a no-external-request assertion): 111 PASS, 0 FAIL, including the toolbar's 7 buttons each computing a Font Awesome glyph.

Final verdict: the shim is kept and self-hosted; no layout references a third-party host.
