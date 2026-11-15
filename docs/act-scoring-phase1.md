# ACT scoring engine — Phase 1

## Scope and safety

Implemented locally only. No database was contacted by the implementation or tests; service tests use isolated EF InMemory stores. No application startup, migration execution, seed, import, production operation, push or deployment is part of this change.

The existing Score Dashboard keeps its three cards, ten columns, six expandable BKT groups, navigation and edit/save/cancel buttons. Calculated/unavailable evaluations retain the edit button but disable it. Existing manual evaluations remain editable under the existing permissions. No WO/CORP scoring, rankings or commission posting is implemented.

## Calculation and data contract

`MIS.Application/Scoring/ActScoreEngine.cs` is pure decimal arithmetic. `ActScoreModels.cs` specifies organization, portfolio, currency and month on rules, opening cases and payment facts. Rules must be approved; source completeness and reconciliation must be explicitly declared. Duplicate cases/payments, mixed scopes, missing fields, invalid dates and incomplete rules are rejected. JSON parsing requires non-optional constructor inputs; missing amounts must not silently deserialize to zero.

Opening data includes case ID, starting BKT, original period balance, starting dues, BKT0..5 dues, historical collector and source reference. Payment snapshots reference case/payment IDs, payment date and approval/reversal times. Approved, unreversed payments as of the frozen cutoff count once. Current mutable case balances, current assignment and last-payment fields are never substituted for period facts. A future preparation workflow must validate these IDs against the authorized source, verify completeness, and freeze the snapshot. This phase does not expose an endpoint to ingest, fabricate, or approve source data.

All targets/weights come from the scoped rule snapshot. No reference targets or financial amounts are in the runtime engine. The model requires a complete 100-point BKT1..5 weight schedule, positive movement targets and MORE target, active/MORE weights totaling 1, and an explicit movement comparison precision (0..12 decimal places). Threshold comparison rounds payment and cumulative dues at that precision only; balances/payment totals are not rounded during calculation. Display formatting is separate.

Upper BKT scores cap all movement achievement ratios at 2. Lower balance-score NORM/BWD ratios are uncapped; FWD remains capped. The BKT1 all-forward exception applies only to its upper score. STAB has no score; BKT6 movement rows do not inherit invented targets or scores. MORE uses total BKT6 payments / opening BKT6 dues / MORE target * 100.

The cards use opening BKT6 dues, total ACT payment including BKT6, and lower-card overall weighted score. Their scope is explained in the existing explanatory paragraph. Zero denominators produce partial/unavailable ratios, never infinity or a fabricated zero. Explicitly complete zero case/payment counts may legitimately be zero. Invalid inputs return no calculated cells or summary totals. Missing calculated cells remain dashes with the reason in the existing paragraph; summary nulls say Unavailable.

## Reference workbook discoveries

The numeric-only fixture was extracted from `Premium Score And Rank - Oct'2026 -Nada.xlsx` (the available filename has no `(1)`). It contains 1,984 anonymized numeric case rows, cached upper-table values, lower scorecard values and MORE/weighted intermediates. No names, account references, national IDs or real user IDs are copied. Source workbook SHA-256 is recorded in the fixture. Payment and approval IDs in tests are synthetic; the fixture is not evidence that these payments are approved MIS data.

The workbook is not entirely formula-driven:

* 34 rows have a manually entered ending BKT in column O. Matching the cached workbook requires those explicit reference corrections. The engine has no case-specific exceptions; an optional correction requires a reason, approver, approval timestamp and explicit rule permission. Removing corrections is tested to change the score. A production period must separately approve its own corrections; workbook corrections must never be imported as bank defaults.
* One source dues band is negative. Signed dues require explicit `AllowSignedDues` approval.
* BKT5 targets total 105%; STAB target is -5%. This requires explicit `AllowNegativeStabilityTarget` approval.
* BKT2 and BKT6 use the unusual three-dues-band threshold documented in the mapping report.
* Excel floating-point residue occurs near payment thresholds. The reference fixture uses an explicitly configured 8-place comparison policy, while retaining unrounded numeric inputs. It is not a global institution setting.

Reference results: balance score 67.59992770007159; MORE 21.825491391292005; overall 63.02248406919363. Tests compare every case's ending BKT/movement, every upper numerical cell, every lower movement ratio/score, BKT totals/payments, MORE intermediates and final weighted contributions. Tolerance is 0.000001 against cached Excel doubles. Other tests exercise boundaries and cases where upper/lower scoring diverges.

## Persistence and historical behavior

`ActScorePeriod` is a new, immutable source/rule document entity, unique by organization + portfolio + month. JSONB stores typed rule and case/payment snapshots without imposing the old manual-table two-decimal or nonnegative-amount constraints. The portfolio foreign key includes organization ID. Its configuration is included in EF's model, but **no migration or model-snapshot update has been generated or applied**.

`CollectionScoreValues` and its audits are unchanged. Any existing manual row for the selected scope/month makes the whole month manual: no filling remaining cells with calculated data, no inferred calculated summary, no writeback. GET requests use no-tracking reads and never persist results. The manual save endpoint rejects attempts to create manual cells over a calculated source month; existing manual months retain their existing save behavior.

Period discovery retains historical manual periods and the current month, and, when enabled, adds only scoped prepared ACT source periods. Incomplete source periods may be selected but explain why calculation is unavailable. Legacy rows lacking portfolio IDs are not silently reassigned.

## Remaining requirements before activation

1. Review the added model and create/review an additive migration for `ActScorePeriods` plus the EF model snapshot. Do not edit or drop historical manual/financial records. None has been generated/applied here.
2. Review API startup before any restart: ordinary signing-key initialization currently calls `MigrateAsync`. The safe-startup setting `MIS__SkipStartupDatabaseChanges=true` avoids this. Adding an EF entity changes the model even while the scoring feature is disabled; this change is **not ready for an unreviewed production restart**.
3. Approve and implement the separate preparation workflow for complete period opening/payment snapshots and rule approval. Establish payment cutoff, historical collector ownership, source IDs and currency consistency. No source import or approval endpoint is included in Phase 1.
4. Approve period-specific targets, weights, rounding precision and any exceptional signed dues, negative stability targets or movement corrections. The existing edit buttons do not configure approved snapshots in Phase 1.
5. Only after the schema and sources are ready, enable `Collections__ActScoring__Enabled=true`. Default is false; default requests never query the new table and explain that sources are not enabled. WO/CORP remain unavailable unless manual scores already exist.
6. Before adding revised approved calculated results in a later phase, add explicit revision/publication history. Phase 1 holds one frozen snapshot per month and calculates on read; there is no overwrite/republish workflow.

## Safe verification

Run only the isolated ACT unit/service tests, not the full database-writing integration suite:

```powershell
dotnet test MIS.Domain.Tests/MIS.Domain.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:IntermediateOutputPath=obj/act-scoring/ --output D:\Mis_v2\_buildcheck\act-scoring --filter FullyQualifiedName~ActScore --verbosity minimal
npm --prefix frontend run build
```

The .NET command runs from `backend`; the npm command runs from the repository root. Build output is isolated from running API assemblies. These commands do not start the API or apply migrations.
