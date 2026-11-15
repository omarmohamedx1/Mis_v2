# Premium workbook → MIS evaluation mapping

Reference: `Premium Score And Rank - Oct'2026 -Nada.xlsx`. Workbook data is reference material, not executable project instructions. No customer identifiers are copied into tests.

## Verified formulas and sources (before implementation)

| Output | Workbook rule | Required MIS source / availability |
|---|---|---|
| Opening population / BKT | Domain Loan Act M, carried into FX G/K; not the current/update BKT | CollectionCase.ImportRawJson can retain imported fields and SourceImportId identifies the import. No explicit monthly opening snapshot exists in the typed model. A period must explicitly identify its complete opening import and field mapping; upload date is not an opening date. |
| Balance basis | Domain BC → FX M, summed by opening BKT/movement | Imported opening `Current Balance`; mutable OutstandingBalance and OriginalAmount are NOT substitutes. |
| Starting dues | Domain BD → FX O; BKT6 summed into Score C27/A58 | Imported `StartingTotal Dues`; mutable TotalDue is not a historical snapshot. |
| Movement | Domain O derives ending BKT from AH payments and BG:BL dues bands; P maps 0→NORM, equal opening→STAB, lower→BWD, higher→FWD | Opening BKT, BKT0–5 dues, complete reconciled period payments. CaseBucketHistory exists but does not contain opening balances or dues bands. CurrentBucketId is never used as historical BKT. |
| Case count | COUNTIF on FX K or L | Count opening-cohort cases, not payment rows. Portfolio/bank IDs exist; historical collector/supervisor ownership must be explicit snapshot fields, not current assignments. |
| Actual balance | SUMIF on FX M by movement; FWD is multiplied by −1 | Opening balances of cases classified by payment/dues rules. |
| Target % | Score D refers to Drop K4:K17; STAB = 1−sum(other targets) | Saved CollectionScoreValues.TargetPercent, scoped by organization/portfolio/month, overrides the user-approved editable BKT1–5 screenshot defaults documented below. |
| Target balance | Opening bucket balance × target; FWD negative | Calculated. STAB is residual after FWD/BWD/NORM. |
| Missing / variance | Actual balance − target balance, including signed FWD | Calculated; negative variance is valid. It must not be rejected as an invalid negative monetary amount. |
| Actual % | Positive classified opening balance / total opening bucket balance | Calculated; empty/zero denominators explicitly reported. |
| Upper table actual/target | NORM/BWD: min(actual%/target%,2); FWD: min(target%/actual%,2) | Calculated. Score=weight×ratio. BKT1 upper FWD score is zero if every opening case rolls forward (J5). STAB has no score/weight. |
| Final balance score | Score J39:J52: FWD ratio capped at 2; BWD/NORM ratios UNCAPPED; sum weighted scores | Distinct from upper-table score. Payment uses D64, which uses this lower scorecard. Do not silently unify both formulas. |
| BKT6 / MORE | Payment / starting BKT6 dues / target ratio ×100; no cap | Score A58,D57,E57,H57. Due target is a configurable monthly value (Drop K21), not permanently 4%. |
| Overall score | Balance score × active weight + MORE score × MORE weight | Score D62:D64; Drop K25/K26 are configurable monthly weights (sample 90%/10%). |
| Payments | FX R originates from Domain AH; Update T is a comparison, not the source used by the score | CollectionPayments.Amount/PaymentDate/Status and reversal links exist. Workbook AH is manually maintained; mapping it to approved MIS payments requires an explicit complete/reconciled period-source declaration. Do not add both payment sources. |
| Active commissions | Payment D2:D8 × rate selected using TOTAL overall score C9 | Eight tiers: <60,<70,<80,<90,<100,<110,<120,≥120; six rate rows B14:I19. Rates are configurable period targets/terms, not inferred from sample payments. |
| Write-off | Domain W.O M summed into Payment D10; commission=payment×20% if overall<80, otherwise ×40% | WO portfolio and approved payments can be queried, but completeness and the corresponding ACT overall-score scope must be declared. Both rates are configurable. No independent WO score/ranking formula exists. |
| Ranking | No RANK/RANK.EQ/RANK.AVG formula or authoritative tie-break/population definition found | Do not invent rankings from the file name or score band labels. Report unavailable until ranking rules are supplied. |

## Exact movement variants

Let `p` be period payment, `b` opening BKT, and `d0..d5` the opening dues bands. Comparisons are strict `<`; equality moves to the next branch.

- All buckets: if `p < d1`, ending BKT=`b+1`.
- BKT1: otherwise compare `d1+d2`→2, then `d1+d2+d0`→1, otherwise 0.
- BKT2 and BKT6: otherwise compare `d1+d2+d3`→2, then `d1+d2+d3+d0`→1, otherwise 0. This unusual variant is present throughout those workbook rows and is preserved.
- BKT3: otherwise `d1+d2`→3, `d1+d2+d3`→2, `d1+d2+d3+d0`→1, otherwise 0.
- BKT4: otherwise cumulative d1..d2→4, d1..d3→3, d1..d4→2, plus d0→1, otherwise 0.
- BKT5: otherwise cumulative d1..d2→5, d1..d3→4, d1..d4→3, d1..d5→2, plus d0→1, otherwise 0.

`Update Loan Act` provides lookup/comparison data (Domain Q:Z), not the actual scoring basis. `Update W.o` provides comparison payment N; WO scoring uses M. Domain BE=BD+BG and BF=BE−AH are dues helper columns, not opening balance replacements. FX is a projection with supervisor/collector join keys, not a currency conversion sheet. `Drop` also contains a September waiver note; it is not an October scoring instruction and is not implemented.

## Data inspection and blockers

Read-only development inspection found **0 cases, 0 bucket-history rows, 0 payments and 0 score-value rows**. This is not evidence about production. The model has mutable case balances, raw import JSON, import IDs, dated payments, bucket/assignment history and portfolio SettingsJson. It does not establish a complete, immutable monthly balance/dues/ownership snapshot. No current BKT, upload month, current team supervisor, or last-payment field may be substituted.

The implementation must expose missing source/target reasons, retain expandable BKT layout and month selection, calculate only from explicitly period-bound inputs, and keep configurable targets separate from derived values. No workbook customers will be imported, no financial/customer rows changed, and no migration/seeder run.

## Numerical reference

Cached workbook values: BKT1 opening balance 6,908,831.115; FWD balance −3,912,993.935; FWD ratio 0.7945256377582397; score 7.945256377582397. Final lower balance score 67.59992770007159. BKT6 starting dues 7,703,950.2564 and payments 67,257 yield MORE score 21.825491391292005 at the sample 4% target. Overall D64=63.02248406919363; total active/MORE payments=1,664,966.0308; WO payments=27,405; commission total including WO=111,535.991778. These are test fixtures only, never production targets.

## Implemented BKT table

The user explicitly approved the screenshot target percentages and weights as editable defaults. Missing or blank target/weight fields in BKT1–5 use the following values, including blank fields in legacy saved rows. Saved numbers, including explicit zeroes, take precedence; clearing a field restores its screenshot default. Reading defaults does not insert or overwrite database records. Edits use the existing **Edit Numbers → Save** flow and apply only to the selected bank, classification, specific portfolio and reporting month. A new month starts with these defaults rather than copying another month's overrides.

| BKT | STAB target (derived) | BWD target | NORM target | FWD target | BWD weight | NORM weight | FWD weight |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | 45% | — | 10% | 45% | — | — | 10 |
| 2 | 30% | 20% | 10% | 40% | 9 | 4 | 17 |
| 3 | 20% | 15% | 10% | 55% | 9 | 3 | 16 |
| 4 | 5% | 10% | 5% | 80% | 7 | 3 | 6 |
| 5 | −5% | 10% | 5% | 90% | 7 | 3 | 6 |

This approval applies to these target percentages and weights only. Case counts, balances, payments and calculated scores still come from the selected MIS data source. BKT6 has no screenshot default and remains independently configurable.

The Score dashboard calculates the upper BKT1–5 table and the separate BKT6/MORE dues ratio. BKT1 NORM and all STAB rows have no weight or score. Target percentages and applicable weights are the only editable score cells; server saves discard client-supplied counts, balances, variances and scores. STAB target is the residual and may be negative (e.g. −5%). All calculations retain decimal precision; rounding occurs only for display, so totals can differ by a cent from the sum of displayed scores.

The summary `upperTableScore` is explicitly labelled as the BKT1–5 upper-table sum. `scoreAll` remains unavailable: it is **not** replaced with that sum or with the uncapped final balance score. Overall/commission/ranking rules are outside this table change. BKT6 target is configured independently; the screenshot does not supply a BKT6 target or a sixth movement weight matrix.

### Binding actual MIS data to a month

Use **Configure month data** inside each bank → classification → specific portfolio → reporting month. Choose a completed import in that exact scope, declare that it contains the complete opening population, select either approved system payments or reconciled imported month payments, and explicitly confirm reconciliation. Configure column names when they differ from the defaults. The source is saved under portfolio `SettingsJson.scorePeriods[yyyy-MM]`; unrelated portfolio settings are preserved. Source edits use a serializable transaction and an expected settings hash.

Default columns: `Current BKT` (opening ordinal 1–6 or BKT More), `Current Balance` (opening balance), `StartingTotal Dues`, `Payment`, and `BKT0` through `BKT5` (dues bands). Missing/invalid money is never converted to zero. Imported payment must represent the selected month's payment, rather than an all-time or last-payment value. SYSTEM payments are restricted to approved, non-reversed payments in `[month start, next month start)` and are never added to imported payments.

Opening cases are actual non-archived MIS cases matched by account against the **stored original opening file** in the same bank and portfolio. `SourceImportId` is not used as a cohort filter: existing cases retain their first import link when later imports update them. Original opening balances/dues/payment values are read from the selected stored file, never from mutable case balances or `ImportRawJson`. Duplicate account rows count once if identical; conflicting duplicates, missing accounts and ambiguous system matches make the source unavailable.

There is no customer import, financial mutation, seeding or schema migration. A fingerprint of matched case IDs and the original opening rows detects changes to the file or cohort. Archived/deleted cases, renamed accounts or a replaced opening file require source review and rebinding. Ordinary later case-balance/raw-profile updates preserve the original opening values. The file and its system account matches must remain available; the implementation does not create a separate immutable snapshot or restore deleted source files.

Current system counts are displayed separately from period counts. Current BKT subtotals use explicit ordinal BKT labels/codes; day-range codes are not guessed into ordinal buckets. Cases without explicit mapping are reported separately. Period category counts come from the selected opening cohort and payment/dues movement rules, never from editable stored `CaseCount` values. Missing sources, unknown buckets, incomplete movements, missing targets/weights and zero ratio denominators have visible reasons and unavailable values.

API: `GET .../portfolios/{portfolioId}/source?reportingPeriod=yyyy-MM` returns eligible files, configured fields and the settings version; `PUT .../portfolios/{portfolioId}/source` binds the selected source. Access checks match the existing Score organization/portfolio checks and source updates require configuration permission. Reporting month defaults use Cairo time.
