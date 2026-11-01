# Phase 94 deferred items

Out-of-scope discoveries made while building the audit record. Not fixed in this phase's capture plans.

## Shop item creation fails with a database foreign-key error (found in plan 02)

- **Where:** `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs`, `Create` (POST).
- **Symptom:** submitting the Create item form as a Dungeon Master answers with an unhandled exception:
  `The INSERT statement conflicted with the FOREIGN KEY constraint "FK_ShopItems_Groups_GroupId"`.
- **Cause:** the controller builds the `ShopItem` without setting its `GroupId` (the entity requires it and the
  quest controller does set it from the active group), so the row is inserted with `GroupId` 0.
- **Effect on the audit:** no shop item can be created through the app's own form, so the audit board has no
  items. The manifest rows `Shop-Details` and `ShopManagement-Edit` are recorded with a `skipReason`; the Shop
  index and the empty-category state are still captured.
- **Suggested handling:** a separate bug fix outside the UI phase (or a decision to seed one item by other means
  for the item-detail audit). Not changed here because plan 02 modifies no application code.
- **Interim audit workaround (orchestrator, after plan 02):** four fantasy shop items (three published, one draft;
  Quest Items left empty) were inserted directly on the audit board's local database so the item-detail and
  item-edit pages could be captured. `fixtures.json` records them as `shop.item` / `shop.items` with
  `method: sql`, and the four Shop rows of the BEFORE record were re-captured on the frozen baseline. The app bug
  itself is unchanged.
