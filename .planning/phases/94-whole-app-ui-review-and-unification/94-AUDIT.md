# Phase 94 Audit: Index and Summary

Index of the whole-app UI audit: one row per manifest view from the six area reports and the shell report, totals, the cascade measurement, the operator question batch for sign-off, the relayout list, merge proposals, dead dependencies and the proposed area-phase cut. Reports: [quests](94-AUDIT-quests.md), [events](94-AUDIT-events.md), [characters](94-AUDIT-characters.md), [shop](94-AUDIT-shop.md), [account](94-AUDIT-account.md), [admin](94-AUDIT-admin.md), [shell](94-AUDIT-shell.md), [vendor](94-AUDIT-vendor.md).

`QBR` is `~/qb-ui-review/phase-94`. Screenshots are never committed; paths below are scratch paths.

## Summary

Work in progress: the desktop layout has been audited end to end and indexed (see `94-AUDIT-shell.md`); the remaining sections are completed in the following steps of the same plan.

## Index

One row per view, twins on one row, plus one row per layout.

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|---------------------------|-------------------------|
| Shared/_Layout (desktop layout, with `_Footer` and `_Toasts`) | `Views/Shared/_Layout.cshtml` | `Views/Shared/_Layout.Mobile.cshtml` (audited next) | Foundation (94) | a11y 13, visual-bug 4, cascade-fight 2, perf 2, inconsistency 1, dead-code 1 (23) | S2 | none (the shell has no list) | 1 (R-shell-1) plus the pair's questions in `94-AUDIT-shell.md` |

## Totals by class and severity

To be computed once every report is indexed.

## Cascade

To be filled by the cascade probe.

## Operator questions for sign-off

To be assembled from the seven reports.

## Relayout list

To be assembled from the seven reports.

## Merge proposals

To be assembled from the seven reports.

## Dead dependencies

The dead-dependency proofs (jQuery, the Font Awesome v4 shim, and the other vendor lines) are in [94-AUDIT-vendor.md](94-AUDIT-vendor.md), written by plan 94-13; this index links to it and does not copy it.

## Proposed area-phase cut

To be written after the totals.
