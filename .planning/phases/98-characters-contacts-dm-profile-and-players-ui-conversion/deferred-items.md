# Phase 98 deferred items

Found by the closing sweep (98-22). Each is also an entry in `~/qb-ui-review/phase-98/contrast-exceptions.json`, pending Gate 2. None was fixed in 98-22, whose scope is guards, records and the ledger only.

| Item | Where | Measured | Why deferred |
|---|---|---|---|
| ~~Picture file input has no associated label~~ **CLOSED at Gate 2 (fixed in commit `5a6f7c84`)** | `Characters/Create`, `Characters/Edit`, `Contacts/Create`, `Contacts/Edit`, `DungeonMaster/EditProfile` | the label's `for` names the model property, the input's `id` is overridden, so `input.labels` is empty; the sweep's name probe lists the Characters inputs as unnamed | present in the baseline views; a one-attribute fix per host view, outside the closing plan's file list |
| Phone Contacts page has no `h1` on a board without categories | `Contacts/Index.Mobile` | `contacts-index-empty` under the phone User-Agent: 0 `h1` | kept as the plan worded it; Gate 2 decides Gate 2: kept as is (d1, approved at Gate 1 item 10); known debt. |
| Tagify remove buttons are 14 by 14 px | `contact-create`, `contact-edit` and the two crop host rows | 14 px remove buttons, 32 px chips, 33 px typing node | the widget's own markup, unchanged in size since before the phase Gate 2: accepted (e4). |
| Second focus ring inside the Tagify box | same pages | wrapper ring plus a ring on the typing node | polish; both rings are legible and unclipped Gate 2: accepted (d3). |
| Fixed navbar wraps over the page title at 320 px on the desktop layout | shell (`_Layout`) | bar 90 px tall, title starts at 89 px | out of scope by the first gate's answer; listed as a shell finding Gate 2: follow-up phase (d2). |
| ~~Production `LANG` not inspected~~ **CLOSED at Gate 2** | release check | the first-paint text of Dungeon Master quest dates was read under the sweep browser's locale only | open assumption from the Dungeon Master date slice; read the production server culture before release Checked by the orchestrator: production runs with LANG=C and no application culture, so the server formats with the invariant culture; assumption 7 closed. |

## Gate 2 outcome (98-23)

Gate 2 was approved on sheet v2. The picture-input label item was fixed after Gate 2 (`5a6f7c84`: each label now carries `for` with its input's own id on the five host views; the rendered test `ImageCropDialogRenderTests.Host_PictureInputHasAnAccessibleNameFromItsVisibleLabel` pins it under both layouts; unnamed visible controls 64 to 0 over the 16 affected rows). The production culture check is closed. The 320 px navbar finding (d2) goes to a follow-up phase; the phone Contacts empty-board heading (d1) stays known debt; the Tagify remove buttons (e4) and the inner Tagify ring (d3) were accepted as recorded.
