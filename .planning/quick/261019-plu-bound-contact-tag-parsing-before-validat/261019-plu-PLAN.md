---
phase: quick-261019-plu
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
  - QuestBoard.Domain/Services/ContactService.cs
  - QuestBoard.UnitTests/Services/ContactServiceTests.cs
  - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
autonomous: true
requirements: [quick-261019-plu]

estimate:
  tokens: 45000
  raw_tokens: 45000
  tasks: 3
  confidence: low

must_haves:
  truths:
    - "A Create or Edit POST whose TagsInput is longer than 1000 characters is never split into tag names: the response is the form re-render (200) carrying 'Tags cannot exceed 1000 characters in total', and an over-long name inside that payload produces NO '30-character limit' error, which proves the parser never ran."
    - "That over-bound re-render still repopulates the tag suggestion list from the board vocabulary, and persists nothing: no contact is created on Create; on Edit the contact's name and tags are unchanged and no new ContactTags row exists."
    - "A TagsInput of exactly 1000 characters is still parsed and saved: every distinct name in it is attached to the contact, exactly as before."
    - "The 1000-character bound has a single source of truth, ContactViewModel.TagsInputMaxLength, referenced by both the StringLength attribute on TagsInput and the controller's pre-parse check; the rendered validation message text is unchanged."
    - "Fail-closed: if the over-bound branch ever runs while ModelState holds no TagsInput error, the controller adds one, so a skipped parse can never reach a write with an empty tag list (which on Edit would silently strip every tag)."
    - "ContactService.ParseTagNames de-duplicates through a case-insensitive HashSet (StringComparer.OrdinalIgnoreCase) in one linear pass, with identical behaviour: trims, drops empty parts, keeps the first occurrence's casing and the input order; the four existing ParseTagNames facts are unchanged and green."
    - "The cross-board no-op repository test asserts the absence of tag rows through IgnoreQueryFilters(), so it would fail if a row were created on either board."
    - "No source or test line added by this work carries a threat id, quick-task id, phase number or planning document name."
  artifacts:
    - path: "QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs"
      provides: "TagsInputMaxLength constant shared by the length attribute and the controller"
      contains: "public const int TagsInputMaxLength = 1000;"
    - path: "QuestBoard.Service/Controllers/Contacts/ContactsController.cs"
      provides: "ParseTagsWithinBound helper; both POSTs bound the raw field before parsing it"
      contains: "ParseTagsWithinBound"
    - path: "QuestBoard.Domain/Services/ContactService.cs"
      provides: "Linear-time ParseTagNames"
      contains: "StringComparer.OrdinalIgnoreCase"
    - path: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs"
      provides: "Over-bound (Create, Edit, one-over) and exactly-at-bound facts for the tag field"
      contains: "TagsInputOverLengthBound"
    - path: "QuestBoard.UnitTests/Services/ContactServiceTests.cs"
      provides: "Interleaved-casing and large-input facts for ParseTagNames"
      contains: "ParseTagNames_LargeDistinctInputWithRepeats"
    - path: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs"
      provides: "Cross-board no-op test that actually sees rows on every board"
      contains: "IgnoreQueryFilters"
  key_links:
    - from: "QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs"
      to: "QuestBoard.Service/Controllers/Contacts/ContactsController.cs"
      via: "the same constant drives the model-validation bound and the controller's skip-the-parse check"
      pattern: "ContactViewModel\\.TagsInputMaxLength"
    - from: "QuestBoard.Service/Controllers/Contacts/ContactsController.cs"
      to: "QuestBoard.Domain/Services/ContactService.cs"
      via: "ParseTagNames is reached only through ParseTagsWithinBound, called once by each POST"
      pattern: "ParseTagsWithinBound\\(viewModel\\)"
    - from: "QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs"
      to: "TagsInput StringLength attribute"
      via: "attribute argument is the constant, message formats the bound through the {1} placeholder"
      pattern: "StringLength\\(TagsInputMaxLength"
---

<objective>
Close the open medium-severity denial-of-service finding T-81-12 ("oversized contact tag payload")
recorded in `.planning/phases/81-contact-tags-and-filtering/81-SECURITY.md`, with both halves of
the auditor's fix direction as defence in depth:

1. **Do not parse an over-limit payload.** Both Contacts POSTs (Create, Edit) currently split the raw
   `TagsInput` value into tag names *before* `ModelState.IsValid` is checked, so a DM can post up
   to the 4 MB default form-value limit and have all of it parsed. After this plan the controller
   checks the raw length against the view model's own bound first and skips parsing and per-name
   validation entirely when it is exceeded. The existing StringLength error then drives the
   existing invalid re-render.
2. **Make the parser linear.** `ContactService.ParseTagNames` de-duplicates by scanning the result
   list for every item, which is quadratic. The auditor measured 40k distinct names at 2.4 s. After
   this plan it uses a case-insensitive `HashSet<string>` with byte-identical output.
3. **Fix the weak test** the auditor flagged. `ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp`
   asserts emptiness through the fail-closed board filter, which is always empty. It should
   assert through `IgnoreQueryFilters()`.

Purpose: an authenticated DM-tier request can no longer burn minutes of CPU on one form post, and
the rejection path is pinned by tests that observe "was not parsed" over HTTP, not by timing.

Output: bounded `ContactsController` POSTs, a `TagsInputMaxLength` constant on `ContactViewModel`, a
linear `ParseTagNames`, four new integration facts, two new unit facts, one strengthened repository
fact, and a SUMMARY carrying the evidence the orchestrator needs to close the threat.

## Decisions taken in this plan (planner's discretion, justified)

- **Three `type="auto"` tasks, no tracer task, no checkpoint.** The orchestrator asked for an
  autonomous plan with no human-verify gate, and the executor turns a tracer task into a
  human-verify stop in interactive mode. This is a bounds fix inside an already-proven feature
  with no new layer, so a tracer slice would add no information.
- **The bound lives on the view model as `public const int TagsInputMaxLength = 1000;`.** Both the
  `StringLength` attribute and the controller check reference it. The attribute's message becomes
  `"Tags cannot exceed {1} characters in total"`. `StringLengthAttribute.FormatErrorMessage`
  substitutes `{1}` with the maximum length on both the server error and the client
  `data-val-length` attribute, so the rendered text stays exactly
  `Tags cannot exceed 1000 characters in total` and no second numeric literal is needed.
- **Fail-closed fallback in the controller.** When the raw value is over the bound and ModelState
  has no error under `TagsInput`, the helper adds
  `$"Tags cannot exceed {ContactViewModel.TagsInputMaxLength} characters in total"`. This only
  happens if a future change drops or bypasses the attribute. Normally the attribute's error is
  already present and nothing is added. Without this fallback, skipping the parse would let the
  Edit POST proceed with an empty tag list and strip every tag. That is a data-loss hazard which
  the skip itself would introduce.
- **"Not parsed" is proven over HTTP, not by a stopwatch.** All four Contacts form views render
  `asp-validation-summary="All"`, and `ValidateTagNameLengths` adds a `30-character limit` error
  for any parsed name longer than 30 characters. An over-bound payload that contains such a name
  therefore shows that error today, because it is parsed, and must not show it after the fix.
  This gives a deterministic RED that fails on the current code.
- **No timing assertion on the linear parser.** The large-input unit fact asserts correctness on
  100,000 distinct names plus their upper-cased repeats. Its measured duration is recorded in the
  SUMMARY as evidence, not asserted, to avoid a flaky test.
- **Who updates the threat register:** the executor does NOT edit `81-SECURITY.md`. It records the
  closing evidence in its SUMMARY (see `<output>`), and the orchestrator edits the register.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md
@.planning/phases/81-contact-tags-and-filtering/81-SECURITY.md
@QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
@QuestBoard.Domain/Services/ContactService.cs

## Interfaces and facts the executor needs (verified against the tree at e99ca74b)

`QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs`, lines 59-63:
- A three-line comment, then `[StringLength(<literal>, ErrorMessage = "Tags cannot exceed 1000 characters in total")]`
  on `public string? TagsInput { get; set; }`. Nothing else in the repo pins that message text
  (grep confirmed: no test, view or script contains it).

`QuestBoard.Service/Controllers/Contacts/ContactsController.cs`:
- Create POST (line 159, `[Authorize(Policy = "DungeonMasterOnly")]` + `[ValidateAntiForgeryToken]`):
  lines 174-177 are a two-line comment, then `var parsedTagNames = contactService.ParseTagNames(viewModel.TagsInput);`
  and `ValidateTagNameLengths(parsedTagNames, viewModel);`. Line 179 is the first
  `if (!ModelState.IsValid)` re-render (PopulateCategoryOptionsAsync + PopulateTagSuggestionsAsync + `return View(viewModel)`).
  `parsedTagNames` is used once more at line 250 (`ReplaceContactTagsAsync(contact.Id, parsedTagNames, token)`).
- Edit POST (line 284, same two attributes): lines 297-300 are the same comment and the same two
  calls, line 302 is the first `if (!ModelState.IsValid)` re-render (also sets `viewModel.CanManage = true`),
  and `parsedTagNames` is used at line 389.
- `private bool ValidateTagNameLengths(IReadOnlyList<string> parsedTagNames, ContactViewModel viewModel)`
  at line 615. Its max is a local const of 30, and it adds
  `Tag "{name}" exceeds the 30-character limit.` under `nameof(viewModel.TagsInput)`.
- `private async Task PopulateTagSuggestionsAsync(ContactViewModel viewModel, CancellationToken token)`
  at line 631 fills `AvailableTagNames` from the board's Authoring vocabulary. A DM's own
  contact's tag is part of that vocabulary (see `ContactsTagsFormMarkupTests.EditForm_SuggestionList_ContainsOnlyOwnBoardTagNames`).
- ModelState keys for this action have no prefix: the existing code adds errors under
  `nameof(viewModel.TagsInput)`, i.e. `"TagsInput"`.

`QuestBoard.Domain/Services/ContactService.cs`, lines 25-41: `ParseTagNames(string? rawInput)`.
It returns `[]` for null/whitespace, splits on `','`, trims each part, skips empty parts, and
de-duplicates with a per-item scan of the result list using `StringComparison.OrdinalIgnoreCase`.
Behaviour contract (also documented on `IContactService.ParseTagNames`): trim, drop empties, keep
the first occurrence's casing and the input order.

Contacts form views: `Create.cshtml:18`, `Edit.cshtml:20`, `Create.Mobile.cshtml:18`,
`Edit.Mobile.cshtml:19` all render `<div asp-validation-summary="All" ...>`, so every ModelState
error appears in the HTML. No view or script contains the text `30-character`.

Test conventions:
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`: the tag
  write-path facts start at line 1591 (block comment) and run to the end of the class (line ~1829).
  Pattern: `await TestDataHelper.ClearDatabaseAsync(factory.Services);`, then
  `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory, "<unique_user>", "<unique>@example.com", roles: ["DungeonMaster"])`
  returning `(dmClient, dmUser)`, `TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "<name>", groupId: 1)`,
  `TestDataHelper.CreateTestContactTagAsync(factory.Services, "<tag>", groupId: 1, contact.Id)`,
  a `FormUrlEncodedContent` posted with `dmClient.PostAsync(...)` and no antiforgery token
  needed, then assertions against `QuestBoardContext` from `factory.Services.CreateScope()`. See
  `Edit_TagNameOverLengthLimit_RerendersWithValidationError` (line ~1738) for the re-render shape.
  Usernames must be unique per fact.
- `QuestBoard.UnitTests/Services/ContactServiceTests.cs`: the ParseTagNames facts are at lines
  295-357. Each builds `MutableTestGroupContext { ActiveGroupId = 1 }`,
  `CreateContext("ContactServiceTests." + nameof(<test>), groupContext)`, `CreateMapper()` and
  `new ContactService(new ContactRepository(context, mapper), mapper)`.
- `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs`: the weak fact is at lines 658-683.
  The final block sets `groupContext.ActiveGroupId = null; // see-all` (a misleading comment: the
  filter is fail-closed, so null sees nothing) and asserts `context.ContactTags.ToListAsync(...)`
  is empty. `SeedTwoGroupContactsAsync` seeds no tags. The same file already uses
  `IgnoreQueryFilters()` at line 381. `CrossBoardIgnoreQueryFiltersSeamTests` scans production
  projects only, so a test-file call is allowed.

Environment (Linux host): never set `DOTNET_GCHeapHardLimit` or other GC env vars. A
`Fatal error. Internal CLR error. (0x80131506)` is a transient flake: re-run the same command once,
or `dotnet build` once and run each test project separately with `--no-build`. Integration tests
use EF Core InMemory, so no SQL Server is needed. Commit on the current branch (never `main`).
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Bound the tag field before parsing it on Create and Edit POST</name>
  <files>QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs, QuestBoard.Service/Controllers/Contacts/ContactsController.cs, QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs</files>
  <read_first>
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs lines 156-185, 281-310 and 610-638 (both POST heads, ValidateTagNameLengths, PopulateTagSuggestionsAsync)
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs lines 1591-1829 (tag write-path facts to mirror)
  </read_first>
  <behavior>
    Append four facts to the end of `ContactsControllerIntegrationTests`, after the existing tag
    write-path facts, under a short plain-language block comment explaining that they pin the
    form's total-length bound on the tag field, and that an over-bound value must be refused
    without being split.

    Use the board tag `wharfmaster` as the suggestion probe. It appears in the HTML only through
    the suggestion list: the posted value never contains it, and a re-rendered model's `Tags` is
    not posted back.

    - `Create_TagsInputOverLengthBound_RerendersWithoutParsingOrPersisting`: the DM owns a seed
      contact carrying tag `wharfmaster`. POST /Contacts/Create with Name `Over Bound Create Contact`
      and TagsInput = a 31-character name (`new string('z', 31)`), then `,`, then 20,000 distinct
      short names `n0..n19999` joined by `,` (about 120 KB). Expect 200; the body contains
      `Tags cannot exceed 1000 characters in total`, does NOT contain `30-character limit`,
      contains `initContactTags(` and contains `wharfmaster`. In the database: no contact named
      `Over Bound Create Contact`, ContactTags count is exactly 1 (the seed), and no tag named `n0`.
    - `Edit_TagsInputOverLengthBound_RerendersWithoutParsingOrChangingContact`: the DM's contact
      `Over Bound Edit Contact` carries tag `wharfmaster`. POST /Contacts/Edit/{id} with Id, Name
      `Renamed While Over Bound` and the same over-bound payload shape. Expect 200 with the same
      four body assertions. In the database: the contact's Name is still `Over Bound Edit Contact`,
      its Tags hold exactly the seeded tag id, ContactTags count is exactly 1, and no tag named `n0`.
    - `Edit_TagsInputOneCharacterOverBound_IsNotParsed`: the DM's contact carries one existing
      tag. POST Edit with TagsInput = `new string('z', 1001)` (assert `.Length` is 1001 in Arrange).
      Expect 200, the bound message, NO `30-character limit`, and the contact's tags unchanged.
      This pins the exact boundary on the refused side.
    - `Edit_TagsInputExactlyAtBound_SavesEveryTag`: the DM's contact has no tags. TagsInput is the
      100 names `t00000000..t00000099` (format `t{i:D8}`, 9 chars each) joined by `,`, plus one
      trailing `,` (assert `.Length` is exactly 1000 in Arrange; the trailing empty part is
      dropped by the parser). Expect a 302 whose Location does not contain `AccessDenied`. The
      persisted contact's tag names are equivalent to those 100 names. This pins the boundary on
      the accepted side and shows a valid at-limit value still saves as before.

    RED: on the current code the first three facts fail on the `30-character limit` assertion,
    because the payload is parsed. The fourth passes and is a regression pin.
  </behavior>
  <action>
    Step 1, RED: write the four facts described in `<behavior>`, run the filter in `<verify>`, and
    confirm the three over-bound facts fail on the `30-character limit` assertion. Commit them as
    `test(261019-plu): add failing facts for the tag field length bound`.

    Step 2, the shared bound: in `ContactViewModel`, add `public const int TagsInputMaxLength = 1000;`
    directly above the `TagsInput` property's existing comment, with a short plain-language comment
    of its own. That comment says the length attribute below and the Contacts controller share this
    bound, and that the controller refuses to split a longer value, so an oversized post costs no
    parsing work. Change the attribute so its maximum-length argument is `TagsInputMaxLength`
    (replacing the numeric literal), with
    `ErrorMessage = "Tags cannot exceed {1} characters in total"`. The `{1}` placeholder is
    formatted to the maximum length by `StringLengthAttribute`, so the rendered text is
    unchanged. Keep the existing explanatory comment on `TagsInput`. It may be lightly reworded
    if it repeats the number, but it must still say this bounds one request's payload and is not
    a cap on tags per contact.

    Step 3, the controller helper: in `ContactsController`, add
    `private IReadOnlyList<string> ParseTagsWithinBound(ContactViewModel viewModel)` next to
    `ValidateTagNameLengths`. Behaviour:
    (a) If `viewModel.TagsInput` is longer than `ContactViewModel.TagsInputMaxLength`, do not call
    the service parser or `ValidateTagNameLengths`. If ModelState has no error under
    `nameof(viewModel.TagsInput)` (no entry, or an entry with zero errors), add one with the text
    `$"Tags cannot exceed {ContactViewModel.TagsInputMaxLength} characters in total"`. Then return
    an empty list.
    (b) Otherwise call `contactService.ParseTagNames(viewModel.TagsInput)`, pass the result to
    `ValidateTagNameLengths(parsed, viewModel)`, and return the parsed list.

    Give the helper a plain-language comment covering three points:
    - an over-long field is never split, because splitting is the expensive part;
    - the view model's length attribute normally has already recorded the error, so the existing
      invalid-model re-render reports it;
    - the fallback error exists so a skipped parse can never reach a write with an empty tag list,
      which on Edit would strip every tag.

    Step 4, the call sites: in both the Create POST and the Edit POST, replace the two-line
    parse-and-validate pair with `var parsedTagNames = ParseTagsWithinBound(viewModel);`. Leave the
    following `if (!ModelState.IsValid)` re-render blocks, the later `ReplaceContactTagsAsync`
    calls and every other re-render path exactly as they are. Update the comment above each call
    site to say the field is bounded and parsed before any ModelState check and before any write:
    an over-long field is refused without being split, and an over-long name is reported through
    the existing invalid-model re-render path rather than a second one. The service parser must be
    reached from the controller only through the new helper.

    Step 5, GREEN: run the filter. All four new facts and every existing ContactsControllerIntegrationTests
    and ContactsTagsFormMarkupTests fact must pass. Commit as
    `fix(261019-plu): refuse to parse an over-long contact tag field`.

    Project rule: no comment, XML doc or string literal may carry a threat id, quick-task id, phase
    or plan number, or planning document name. Explain the why in plain language. Do not change
    `ValidateTagNameLengths`, `PopulateTagSuggestionsAsync`, either GET action, any view, the
    attributes on either POST, or Program.cs form limits.
  </action>
  <verify>
    <automated>cd /mnt/Data/repos/quest-board-dnd && dotnet build && dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~ContactsControllerIntegrationTests|FullyQualifiedName~ContactsTagsFormMarkupTests"</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c 'public const int TagsInputMaxLength = 1000;' QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` prints 1
    - `grep -c 'StringLength(TagsInputMaxLength, ErrorMessage = "Tags cannot exceed {1} characters in total")' QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` prints 1
    - `grep -v '^\s*//' QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs | grep -c 'StringLength(1000'` prints 0
    - `grep -c 'ParseTagsWithinBound(viewModel)' QuestBoard.Service/Controllers/Contacts/ContactsController.cs` prints 2
    - `grep -v '^\s*//' QuestBoard.Service/Controllers/Contacts/ContactsController.cs | grep -c 'contactService.ParseTagNames('` prints 1
    - `grep -c 'ContactViewModel.TagsInputMaxLength' QuestBoard.Service/Controllers/Contacts/ContactsController.cs` is at least 1
    - `grep -c 'TagsInputOverLengthBound\|TagsInputOneCharacterOverBound\|TagsInputExactlyAtBound' QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` prints 4
    - The RED commit's run shows the three over-bound facts failing; the GREEN run shows the filter at 0 failed
  </acceptance_criteria>
  <done>Both POSTs bound the raw tag field before splitting it. An over-bound value re-renders the form with the existing message, its suggestion list populated, no "30-character limit" error and no persisted change. An exactly-1000-character value still saves every tag. Both commits are on the branch.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Make ParseTagNames de-duplicate in linear time</name>
  <files>QuestBoard.Domain/Services/ContactService.cs, QuestBoard.UnitTests/Services/ContactServiceTests.cs</files>
  <read_first>
    - QuestBoard.UnitTests/Services/ContactServiceTests.cs lines 295-357 (the four existing ParseTagNames facts)
  </read_first>
  <behavior>
    Add two facts directly after `ParseTagNames_CommasOnlyInput_ReturnsEmptyList`, using the same
    Arrange pattern (own InMemory database name via `nameof`):
    - `ParseTagNames_InterleavedCaseVariants_KeepsFirstCasingAndInputOrder`: the input
      `"Alpha, beta, ALPHA, Gamma, BETA, alpha, gamma "` yields exactly `"Alpha", "beta", "Gamma"`
      in that order.
    - `ParseTagNames_LargeDistinctInputWithRepeats_ReturnsEachNameOnceInOrder`: build `expected` as
      the 100,000 names `Tag0..Tag99999`. The input is those names joined by `,`, then `,`, then the
      same names upper-cased with `ToUpperInvariant` and joined by `,`. Assert the result has
      count 100,000, the first item is `Tag0`, the last is `Tag99999`, and
      `result.SequenceEqual(expected)` is true. Use SequenceEqual rather than a collection Equal
      so a failure message stays small. Do not assert on elapsed time.

    This is a behaviour-preserving change, so the TDD gate is characterization rather than a
    failing RED. The interleaved-casing fact and the four existing facts must be green on the
    current implementation and stay green after the swap.
  </behavior>
  <action>
    Step 1: add `ParseTagNames_InterleavedCaseVariants_KeepsFirstCasingAndInputOrder` and run the
    ContactServiceTests filter against the current implementation. It must pass, which pins the
    current semantics. Do NOT add the 100,000-name fact yet: the current quadratic algorithm takes
    tens of seconds on it.

    Step 2: in `ContactService.ParseTagNames`, keep the null/whitespace early return, the split on
    `','`, the per-part `Trim()` and the skip of empty parts. Replace only the duplicate check:
    keep a `HashSet<string>` built with `StringComparer.OrdinalIgnoreCase` beside the result list,
    and append a trimmed part to the list only when the set's `Add` returns true. The first
    occurrence then wins, keeping its casing and its position. This is the same equality the old
    scan used (ordinal, case-insensitive), so the output is identical. Update the inline comment
    to say that de-duplication is case-insensitive through a set, so each part costs constant
    time, while the first occurrence's casing and the input order are preserved. Do not change
    the method signature or the `IContactService` doc.

    Step 3: add `ParseTagNames_LargeDistinctInputWithRepeats_ReturnsEachNameOnceInOrder` and run the
    filter. All ContactServiceTests must pass. Note the large fact's duration from the test output
    for the SUMMARY.

    Commit as `perf(261019-plu): de-duplicate parsed contact tag names in linear time`, with the
    test and the implementation in one commit, since this is a characterization-guarded refactor.
    No threat id, quick-task id, phase number or planning document name may appear in any added
    comment or literal.
  </action>
  <verify>
    <automated>cd /mnt/Data/repos/quest-board-dnd && dotnet build && dotnet test QuestBoard.UnitTests --no-build --filter "FullyQualifiedName~ContactServiceTests"</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c 'StringComparer.OrdinalIgnoreCase' QuestBoard.Domain/Services/ContactService.cs` is at least 1
    - `grep -v '^\s*//' QuestBoard.Domain/Services/ContactService.cs | grep -c 'names.Any('` prints 0
    - `grep -c 'ParseTagNames_InterleavedCaseVariants_KeepsFirstCasingAndInputOrder\|ParseTagNames_LargeDistinctInputWithRepeats_ReturnsEachNameOnceInOrder' QuestBoard.UnitTests/Services/ContactServiceTests.cs` prints 2
    - The four pre-existing ParseTagNames facts are byte-for-byte unchanged (`git diff` shows no removed lines inside lines 295-357 of ContactServiceTests.cs)
    - ContactServiceTests filter: 0 failed
  </acceptance_criteria>
  <done>ParseTagNames runs in one linear pass with identical output. The interleaved-casing and 100,000-name facts pass alongside the four existing ones. The large fact's measured duration is noted for the SUMMARY.</done>
</task>

<task type="auto">
  <name>Task 3: Strengthen the cross-board no-op repository fact and run the full sweep</name>
  <files>QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs</files>
  <read_first>
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs lines 658-683 (the fact to change) and line 381 (existing IgnoreQueryFilters usage to mirror)
  </read_first>
  <action>
    Step 1: in `ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp`, change only the final
    assertion block so that the tag rows are read with
    `context.ContactTags.IgnoreQueryFilters().ToListAsync(TestContext.Current.CancellationToken)`
    and still asserted empty. Replace the misleading trailing remark on the
    `groupContext.ActiveGroupId = null;` line with a plain-language comment. It should say the board filter is bypassed here so a row created on either board
    would be seen, and that a null active group sees nothing through the fail-closed filter, which
    is why the plain query proved nothing. The Arrange and Act blocks and the NotThrow assertion
    stay unchanged. The fact must still pass (the seed creates no tags). Commit as
    `test(261019-plu): assert the cross-board tag no-op past the board filter`.

    Step 2: run the sweep in the order given in `<verify>`: build once, the targeted unit filter,
    the targeted integration filter, then the full unit suite and the full integration suite, each
    with `--no-build`. If a run dies with `Internal CLR error. (0x80131506)`, re-run that same
    command once before treating it as real. Record every pass/fail/skip count for the SUMMARY.

    Step 3: run the planning-reference gate from `<acceptance_criteria>` over the diff of the four
    source and test projects against e99ca74b. It must print nothing.
  </action>
  <verify>
    <automated>cd /mnt/Data/repos/quest-board-dnd && dotnet build && dotnet test QuestBoard.UnitTests --no-build --filter "FullyQualifiedName~ContactServiceTests|FullyQualifiedName~ContactRepositoryTests|FullyQualifiedName~CrossBoardIgnoreQueryFiltersSeamTests" && dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~ContactsControllerIntegrationTests|FullyQualifiedName~ContactsTags|FullyQualifiedName~AntiForgeryTokenCoverageTests" && dotnet test QuestBoard.UnitTests --no-build && dotnet test QuestBoard.IntegrationTests --no-build</automated>
  </verify>
  <acceptance_criteria>
    - `sed -n '/ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp()/,/^    }/p' QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs | grep -c 'IgnoreQueryFilters()'` is at least 1
    - `sed -n '/ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp()/,/^    }/p' QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs | grep -c 'see-all'` prints 0
    - `cd /mnt/Data/repos/quest-board-dnd && git diff e99ca74b -- QuestBoard.Domain QuestBoard.Service QuestBoard.UnitTests QuestBoard.IntegrationTests | grep '^+' | grep -E 'T-[0-9]+-[0-9]+|261019|quick-[0-9]|SECURITY\.md|RESEARCH\.md|CONTEXT\.md|PLAN\.md|Phase [0-9]+'` prints nothing
    - `git diff --stat e99ca74b -- QuestBoard.Domain QuestBoard.Service QuestBoard.UnitTests QuestBoard.IntegrationTests` lists exactly the six files in this plan's files_modified
    - Targeted filters, the full unit suite and the full integration suite: 0 failed
  </acceptance_criteria>
  <done>The cross-board no-op fact can now fail if a row lands on either board. Both full suites are green, and the counts are recorded for the SUMMARY.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser -> ContactsController Create/Edit POST | DM-tier, antiforgery-protected form post; `TagsInput` is an untrusted string up to the 4 MB default form-value limit |
| ContactsController -> ContactService.ParseTagNames | Domain parser receives whatever the controller hands it; its cost must be bounded at the caller and linear in itself |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-plu-01 | Denial of service | Create/Edit POST tag parsing (`ContactsController.ParseTagsWithinBound`) | medium | mitigate | Raw length checked against `ContactViewModel.TagsInputMaxLength` before any split; over-bound values are never parsed. Task 1 facts prove it over HTTP (no `30-character limit` error from an over-bound payload carrying a 31-char name) on Create, Edit and at 1001 chars |
| T-plu-02 | Denial of service | `ContactService.ParseTagNames` | medium | mitigate | De-duplication through a case-insensitive `HashSet`, linear in the input. Task 2 fact parses 100,000 distinct names plus 100,000 repeats correctly; duration recorded in the SUMMARY |
| T-plu-03 | Tampering (integrity) | Edit POST write path after a skipped parse | medium | mitigate | Fail-closed: the helper adds a `TagsInput` ModelState error when the attribute's error is absent, so an empty tag list from a skipped parse can never reach `ReplaceContactTagsAsync`. Task 1 Edit facts assert tags and name unchanged |
| T-plu-04 | Elevation of privilege / CSRF | Create/Edit POST | low | mitigate | `[Authorize(Policy = "DungeonMasterOnly")]` and `[ValidateAntiForgeryToken]` untouched; `AntiForgeryTokenCoverageTests` in the Task 3 sweep |
| T-plu-05 | Information disclosure | Re-render suggestion list | low | accept | Re-render still goes through the unchanged board-filtered `PopulateTagSuggestionsAsync`; `ContactsTagsFormMarkupTests` (own-board-only suggestions) stays in the sweep |
| T-plu-06 | Denial of service | Re-render echoes the posted over-bound value into the input's `value` attribute | low | accept | Pre-existing 1:1 reflection of the DM's own payload (no amplification, no parse), bounded by the existing 4 MB form-value and 10 MB body limits; DM-tier only. Reported in the SUMMARY as a residual note |
</threat_model>

<verification>
- `dotnet build` succeeds with 0 errors.
- Task 1 RED run: the three over-bound facts fail on `30-character limit`; GREEN run: the
  ContactsControllerIntegrationTests + ContactsTagsFormMarkupTests filter at 0 failed.
- ContactServiceTests, ContactRepositoryTests and CrossBoardIgnoreQueryFiltersSeamTests green;
  ContactsControllerIntegrationTests, ContactsTags* and AntiForgeryTokenCoverageTests green.
- Full unit suite and full integration suite: 0 failed.
- Exactly the six planned files changed under the source and test projects; no view, GET action,
  Program.cs or IContactService change.
- No planning references in added source or test lines.
</verification>

<success_criteria>
- A DM posting an oversized tag field to Contacts Create or Edit gets the normal form back with
  "Tags cannot exceed 1000 characters in total" and a populated suggestion list, and the server
  never splits the value. Nothing is saved.
- A tag field of up to 1000 characters behaves exactly as before.
- Parsing a large but in-bound or direct-service input is linear in its length.
- The repository's cross-board no-op fact can actually fail.
</success_criteria>

<output>
Create `.planning/quick/261019-plu-bound-contact-tag-parsing-before-validat/261019-plu-SUMMARY.md` when done.

Do NOT edit `.planning/phases/81-contact-tags-and-filtering/81-SECURITY.md`. The orchestrator
closes the threat there from the SUMMARY.

The SUMMARY MUST include a section titled "Evidence for closing T-81-12" listing:
- the closing commit SHAs (RED, fix, perf, test);
- the new helper's file and line, the two call-site lines in `ContactsController.cs`, the constant's
  line in `ContactViewModel.cs`, and the `HashSet` line in `ContactService.cs`;
- the names of the four integration facts and two unit facts, plus the strengthened repository fact;
- the RED failure count from Task 1, and the measured duration of the 100,000-name unit fact (stated
  as observed, not asserted);
- targeted and full-suite pass/fail/skip counts;
- the residual note that an over-bound re-render still echoes the posted value back into the input
  (pre-existing 1:1 reflection, DM-tier, bounded by the form limits; not a parse cost);
- a one-line proposed register update for the orchestrator: T-81-12 status `closed`, with this
  evidence, plus an audit-trail row dated with the completion date. Also remove the two now-resolved
  residual notes (T-81-12 fix direction and the weak test).
</output>
