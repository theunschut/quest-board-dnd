# Phase 96: Account and pickers UI conversion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-10
**Phase:** 96-account-and-pickers-ui-conversion
**Areas discussed:** Process and sign-off, Home/Index twin, SetPassword token, Profile details

Not asked again because the G1 sign-off already answered them: Q-03, Q-76 to Q-84, Q-103 to Q-107,
Q-122, Q-123, Q-136. Edit and Profile stay twins because Q-82 and Q-83 were answered "Deliberate".

---

## Process and sign-off

| Question | Options | Selected |
|----------|---------|----------|
| Relayouts without an AFTER (picker, Profile) | Mockup first / Build, then confirm | Mockup first |
| Gates | Two gates / One gate at the end | Two gates |
| BEFORE baseline | Re-capture here / Copy Phase 94 set | Re-capture here |
| Setup and delivery | Checkpoint + slices, one release / Release per slice | Checkpoint + slices, one release |

---

## Home/Index twin

The first attempt at this area was interrupted: the operator noted that similar questions had been
answered in the Phase 95 discussion and asked for them to be read first and the questions re-asked.
Phase 95 D-21 (new merge candidates go to Gate 1 as proposals) was then applied.

| Question | Options | Selected |
|----------|---------|----------|
| Merge | Proposal to Gate 1 (as Phase 95 D-21) / Keep the twin | Proposal to Gate 1 |
| Look | Show one unified look at Gate 1 / Keep each look | Show it at Gate 1 |

---

## SetPassword token

| Question | Options | Selected |
|----------|---------|----------|
| How to get a freshly mailed link | Operator checkpoint, link in scratch / Source-only + unit test, waive the mailed check | Operator checkpoint |

---

## Profile details

| Question | Options | Selected |
|----------|---------|----------|
| Changes (multi-select) | Fix the empty-state copy / Styled confirm dialog | Both |

---

## Claude's Discretion

- How the picker row becomes the submit control
- Account sheet name and consolidation of the three style sources
- Module structure for the shared Profile script
- Commit-button class mapping and the Edit Change Password button alignment
- Slice boundaries and plan count

## Deferred Ideas

- SetPassword (8) and ChangePassword (6) minimum password length mismatch — validation, not UI.
