---
phase: 81
slug: contact-tags-and-filtering
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-10-19
---

# Phase 81 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Written retroactively on 2026-10-19 (the phase shipped 2026-08-31), from the threat models
> authored in plans 01–08 at plan time.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| planning docs → executor agents / verification tooling | Plans and the coverage/validation maps are the instruction source and audit baseline; a wrong or stale row silently removes or hides a control | requirement ids, validation map |
| board A session → board B data; no-active-board session → all boards | Any tag read or write must be confined to the caller's active board; a null active group must return nothing | ContactTags, ContactContactTag join |
| application layer → database constraint | Case-insensitive tag identity is a database guarantee (collation + unique (GroupId, Name) index); the in-memory test provider honours neither | tag names |
| submitted tag name or id → persisted association; caller's board → another board's tag rows | Untrusted form input crosses into an entity association; a name existing on another board must create a fresh local row, never attach across or disclose that it exists | tag names, contact ids |
| query string → contact list; visibility gate → tag filter | Untrusted repeated tag ids enter the read path; the filter must sit strictly downstream of the visibility gate | tag ids |
| player-tier session → DM-only vocabulary / DM-tier surface → player-tier response (desktop and mobile) | A player receives no tag names, ids or markup, and no observable difference from supplying a tag id | tag vocabulary |
| session toggle POST → redirect target | The redirect URL is composed by the app and must stay local | redirect URL |
| view model → suggestion list / filter checkbox list; server-rendered list → inline script block | Derived server-side from the caller's visible contacts; DM-authored names are emitted into a script context | tag names |
| third-party CDN → browser execution context | The pinned Tagify script runs with full page privileges in a DM's session | script bytes (SRI) |
| DM-authored tag name → rendered page (desktop and mobile) | Free-typed text reaches every viewer and must never become markup | tag names |
| user-agent header → layout selection | Tests must send an explicit User-Agent or they silently test the wrong layout | test requests |

---

## Threat Register

51 register rows across plans 01–08 (28 unique ids; several ids recur across plans for different components and are listed per plan; the seven identical T-81-SC rows are collapsed). C = `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`, R = `QuestBoard.Repository/ContactRepository.cs`, Ctx = `QuestBoard.Repository/Entities/QuestBoardContext.cs`, V = `QuestBoard.Service/Views/Contacts/`. Line numbers are as audited at c5f5e91e.

| Threat ID (plan) | Category | Component | Severity | Disposition | Mitigation / evidence | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-81-01-01 (01) | Repudiation | Requirement traceability | medium | mitigate | 17 CONTACTTAG ids identical across REQUIREMENTS.md, ROADMAP coverage table and plan frontmatter | closed |
| T-81-01-02 (01) | Tampering | Validation contract | medium | mitigate | 81-VALIDATION.md per-task map; cross-tenant rows present | closed |
| T-81-01-03 (01) | Tampering | ROADMAP whole-file rewrite | medium | mitigate | Commit b8574113: same 12 phase headings before and after | closed |
| T-81-01-04 (01) | Information disclosure | planning-only plan | low | accept | AR-81-01 | closed |
| T-81-01-SC (01) | Tampering | package installs | high | accept | AR-81-02 | closed |
| T-81-01 (02) | Information disclosure | ContactTagEntity reads | high | mitigate | Fail-closed `HasQueryFilter` (Ctx:600-603); `ContactTags_NullActiveGroup_ReturnsNoRows`, `ContactTags_OtherBoardsTag_IsNotVisible` | closed |
| T-81-01b (02) | Elevation of privilege | query-filter bypass | high | mitigate | No `IgnoreQueryFilters` in contact-tag code; `CrossBoardIgnoreQueryFiltersSeamTests` allowlist is the live guard | closed |
| T-81-02 (02) | Information disclosure | Contact.Tags skip navigation | high | mitigate | Join through two filtered ends (Ctx:609-613); `Contact_TagsNavigation_ExcludesOtherBoardsTag` | closed |
| T-81-05 (02) | Tampering | duplicate tag identity | medium | mitigate | CI collation (Ctx:587-589) + unique `IX_ContactTags_GroupId_Name` (migration AddContactTags) | closed |
| T-81-06 (02) | Denial of service | unbounded tag name length | low | mitigate | `[StringLength(30)]`, `nvarchar(30)` | closed |
| T-81-01 (03) | Elevation of privilege | ReplaceContactTagsAsync write path | high | mitigate | Contact and vocabulary resolved through filtered queries; new rows take the filtered contact's GroupId; `Edit_TagNameExistingOnlyOnAnotherBoard_CreatesOwnBoardRow` | closed |
| T-81-01b (03) | Information disclosure | cross-board contact id | high | mitigate | Unresolvable contact is a silent no-op (R:82) | closed |
| T-81-01c (03) | Elevation of privilege | query-filter bypass | high | mitigate | 0 `IgnoreQueryFilters` in R; seam allowlist test | closed |
| T-81-05 (03) | Tampering | duplicate tag rows | medium | mitigate | Case-insensitive in-memory match (R:100-101); repository tests | closed |
| T-81-07 (03) | Denial of service | unbounded reconciliation | low | accept | AR-81-03 | closed |
| T-81-08 (03) | Tampering | orphan-prune drift | medium | mitigate | One `PruneOrphanedTagsAsync`, called on save and delete; repository tests | closed |
| T-81-03 (04) | Information disclosure | ApplyTagFilter ordering | high | mitigate | `IsVisibleTo` applied before `ApplyTagFilter` (C:42 → C:56); `Index_SelectedTagOnUnrevealedContact_StaysHiddenWhileShowHiddenIsOff` | closed |
| T-81-02 (04) | Information disclosure | vocabulary input set | high | mitigate | Vocabulary projected from the visible set only | closed |
| T-81-02b (04) | Information disclosure | player-tier response | high | mitigate | Selected ids, vocabulary and tags cleared for non-DM tier (C:39, C:732-735, C:62); `Index_PlayerTierWithTagId_IgnoresTheFilter` | closed |
| T-81-01 (04) | Information disclosure | other-board tag id probing | high | mitigate | Unknown and foreign ids give an identical empty result, never an error | closed |
| T-81-09 (04) | Tampering | open redirect on toggle | medium | mitigate | Target built from `Url.Action(Index)` + bound int ids | closed |
| T-81-10 (04) | Spoofing | unauthenticated toggle | low | accept | AR-81-04 | closed |
| T-81-01 (05) | Elevation of privilege | tag write path | high | mitigate | Controller delegates to `ReplaceContactTagsAsync`; cross-board name test | closed |
| T-81-02 (05) | Information disclosure | suggestion list | high | mitigate | Shared visibility-scoped vocabulary on every form surface; `ContactsTagsFormMarkupTests` | closed |
| T-81-11 (05) | Spoofing | unauthorized tag write | high | mitigate | `DungeonMasterOnly` + `[ValidateAntiForgeryToken]` on POSTs, `DungeonMasterOnly` on GETs | closed |
| T-81-05 (05) | Tampering | silent tag truncation | medium | mitigate | `ValidateTagNameLengths` raises a ModelState error | closed |
| T-81-12 (05) | Denial of service | oversized tag payload | medium | mitigate | `[StringLength(1000)]` exists and the parser drops empties, but `ParseTagNames` runs on the raw value before ModelState is checked (C:174 vs :177, C:297 vs :300) and its de-duplication is quadratic — 40k distinct names took 2.4 s; the default 4 MB form-value limit admits far more. DM-tier only, behind antiforgery. | open — below high threshold (non-blocking) |
| T-81-13 (05) | Tampering | lost suggestion list on re-render | low | mitigate | All six invalid re-render paths repopulate suggestions | closed |
| T-81-04 (06) | Tampering | CDN-delivered library | high | mitigate | Tagify 4.38.0 with `integrity` + `crossorigin` on all four form views; served bytes re-hashed by the auditor and match | closed |
| T-81-04b (06) | Tampering | silent version drift | medium | mitigate | Single pinned version across all views; no other Tagify reference | closed |
| T-81-02 (06) | Tampering (XSS) | suggestion list injection | high | mitigate | `Json.Serialize` escapes `</script>`, quotes and `&` (verified) | closed |
| T-81-02b (06) | Tampering (XSS) | tag name in input value | high | mitigate | `asp-for` tag helper HTML-encodes the value | closed |
| T-81-14 (06) | Denial of service | unavailable CDN | low | accept | AR-81-05 | closed |
| T-81-11 (06) | Spoofing | player reaching the tag field | high | mitigate | Same policy attributes; `EditForm_PlayerTier_IsRefusedOnBothUserAgents` | closed |
| T-81-02 (07) | Information disclosure | player-tier index/details | high | mitigate | View gates + controller clears; desktop markup tests | closed |
| T-81-02b (07) | Information disclosure | filter vocabulary | high | mitigate | Binds `Model.AvailableTags` only; `Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn` | closed |
| T-81-15 (07) | Tampering (XSS) | tag name rendering | high | mitigate | No raw helpers on tag output; `Index_TagNameWithMarkupCharacters_IsEscaped` | closed |
| T-81-16 (07) | Tampering | lost filter on toggle | medium | mitigate | Hidden `tag` inputs per selected id | closed |
| T-81-17 (07) | Information disclosure | empty-state confusion | low | mitigate | Separate filtered and empty branches | closed |
| T-81-02 (08) | Information disclosure | player-tier mobile responses | high | mitigate | Mobile view gates + controller clears; `ContactsTagsMobileTests` | closed |
| T-81-02b (08) | Information disclosure | drawer vocabulary | high | mitigate | Binds `Model.AvailableTags` | closed |
| T-81-15 (08) | Tampering (XSS) | tag name rendering, mobile | high | mitigate | Encoded `@t.Name` / `@tag.Name`; no raw helpers | closed |
| T-81-18 (08) | Repudiation | untested mobile markup | high | mitigate | Every mobile test request sets an explicit User-Agent (0 `GetAsync(`) | closed |
| T-81-16 (08) | Tampering | lost filter on mobile toggle | medium | mitigate | Hidden `tag` inputs in the mobile form | closed |
| T-81-SC (02–08) | Tampering | package installs | high | accept | AR-81-02 | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-81-01 | T-81-01-04 | This plan writes no code and touches no runtime surface; it cannot itself disclose board data. Verified: all four plan-01 commits touch `.planning/` only. | Plan 81-01 threat model (operator-approved plan) | 2026-10-19 |
| AR-81-02 | T-81-01-SC, T-81-SC (02–08) | No package manager runs in any phase-81 plan and no lockfile entry is created; the one external dependency, `@yaireo/tagify`, is consumed by URL and recorded as OK in the research's package audit. The only remaining trust decision — whether the pinned hash matches the served bytes — was re-verified by the auditor on 2026-10-19. | Plans 81-01…08 threat models | 2026-10-19 |
| AR-81-03 | T-81-07 | A board's tag vocabulary is loaded whole on each save. Vocabularies are tens of rows on a per-board NPC roster, and a per-name round trip is strictly worse. Reconciliation runs only after ModelState passes, so the form bound caps the names it receives. | Plan 81-03 threat model | 2026-10-19 |
| AR-81-04 | T-81-10 | `ToggleShowHidden` keeps its existing `DungeonMasterOnly` policy and antiforgery attribute; the phase adds a parameter, not a new entry point. | Plan 81-04 threat model | 2026-10-19 |
| AR-81-05 | T-81-14 | A blocked or failed CDN leaves a plain text input that still submits the correct comma-separated value; this is the designed degradation. | Plan 81-06 threat model | 2026-10-19 |

*Accepted risks do not resurface in future audit runs.*

### Residual notes (informational, not counted in threats_open)

- **T-81-12 fix direction:** check ModelState (or the raw length) before calling `ParseTagNames`, and make its de-duplication linear (a case-insensitive `HashSet`). Reach is DM-tier only.
- **Weak test:** `ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp` asserts `ContactTags` is empty with `ActiveGroupId = null`, which the fail-closed filter makes always true; only its NotThrow half proves anything. Use `IgnoreQueryFilters()` in that assertion, as other tests already do.
- **No Content-Security-Policy** is configured; no register row relies on one. If one is added, the inline `initContactTags(...)` blocks need a nonce or hash.
- **Mitigation wording drift** (properties still hold): T-81-01b (02) "zero across the whole repository project" — the allowlist test is the guard; T-81-01 (04) "ordinary visible list" — strict narrowing replaced it; T-81-04 — the executor did not re-hash, the auditor did; T-81-02b (06) — `Html.Raw` wraps `Json.Serialize` only.
- **Edit GET pre-fills `TagsInput`** with all of a contact's tags without the ownership narrowing; consistent with any DM being able to edit any contact on the board.
- **Later changes to the same code** were re-checked after this audit: the WR-02 retry re-runs the whole reconciliation, including the board-filtered contact lookup and the single prune helper; the WR-03 single fetch keeps the DM-tier early return and the ownership/Show Hidden narrowing; the WR-01 comment introduces no `GetAsync(`. Full suite green afterwards.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-19 | 28 unique (51 rows) | 27 unique (50 rows) | 1 (T-81-12, medium, non-blocking) | gsd-security-auditor (opus) at c5f5e91e; targeted 57 unit + 93 integration tests passed. Re-checked by the orchestrator after the review fixes merged (91aa14b2): 1105 unit + 1324 integration passed, 11 skipped |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-19 (one medium threat, T-81-12, open below the blocking threshold)
