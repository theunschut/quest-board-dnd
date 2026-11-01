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
