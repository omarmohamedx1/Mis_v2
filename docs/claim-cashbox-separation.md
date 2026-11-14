# Finance Claims and operating cashbox separation

## Confirmed cause and traced workflow

`FinanceClaimsPage` calls `financeClaimsService.confirmReceipt(id, dateInBank)` → POST `/api/finance/claims/{id}/confirm-receipt` with `{ dateInBank }`. The historical-posting endpoint calls the same posting service. Controller and service permission checks remain in place.

`FinanceClaimsService.ConfirmReceiptAsync` checks claim status, receipt date, and existing active postings inside a serializable transaction. It calls `AccountingBookService.RecordClaimReceiptAsync`, confirms the claim as RECEIVED, stores `FinanceClaimReceiptPosting`, and writes an audit event. The receipt posting links the claim to an accounting book entry, which links to a journal and its lines. These are direct calls; the claim confirmation path does not enqueue an accounting event or invoke a cashbox transfer handler. General client collections use their separate financial receipt/client ledger/custody workflow.

The defect was in `RecordClaimReceiptAsync`: it created category INCOME, journal type COMPANY_INCOME, debited operating bank account 110400, and credited Company Income clearing account 210300. `AccountingBookService.SummaryAsync` counted these book entries in cumulative CompanyIncome and period CashIn/NetCashFlow. The company register and cashbook export also included them. The frontend descriptions and older integration assertions explicitly expected this coupling.

## Corrected behavior

- New receipts use category and journal type CLAIM_RECEIPT. They retain balanced, posted journals, receipt links, audit history, idempotency, closed-period protection, and existing claim reversal permissions.
- The configured claim asset account is debited and the configured claim clearing liability account is credited. No operating cash/bank or Company Income account is used.
- Accounts must already exist, be active posting accounts in the MIS legal entity, have the expected asset/liability types, and have no company-cash classification, operating-account ID, control-account classification or required client/collector/branch dimensions. Missing/invalid configuration blocks posting with a specific Arabic error; it never silently falls back to operating cash accounts or creates accounts.
- Operating register/list totals, summary categories, cumulative CompanyIncome, period cash flow, pending totals and cashbook exports exclude CLAIM_RECEIPT entries, legacy `CLAIM:` sources, and entries linked by claim receipt postings. Reversal effects are excluded through the same original-entry selection. Claim lists, receipt details and claim exports remain available.
- General ledger, chart-of-accounts balances and trial balance continue to show the stored historical journals truthfully. They are not rewritten or hidden to conceal legacy postings. New claim journals have no operating-account lines; old mispostings in the ledger still require approved reconciliation.
- Company Income still uses its existing explicit, authorized workflow. This patch does not add an automatic transfer or a new transfer endpoint. Any actual movement from claim-held funds to operating cash must have separately authorized transfer evidence and accounting entries.
- The previous Finance sidebar/page-heading label swap is preserved.

## Configuration and deployment (not performed)

Finance must approve existing dedicated accounts and configure:

```text
Finance__ClaimReceipts__AssetAccountId=<dedicated non-operating asset account UUID>
Finance__ClaimReceipts__ClearingAccountId=<dedicated non-operating clearing liability account UUID>
MIS__SkipStartupDatabaseChanges=true
```

No accounts are created by this patch. If dedicated accounts do not exist, their creation is a separate approved finance setup task, not a migration or seeder. This is a deployment prerequisite: receipts cannot be posted until valid account IDs are configured.

Deploy backend and frontend together after review. Vercel-only deployment cannot fix posting or report calculations. No schema changes or migrations are required: category and journal type are existing string columns without value-enumeration constraints. The existing safe-startup mechanism is unchanged. No deployment or API restart was performed.

## Historical impact and proposed reconciliation

`docs/sql/claim-cashbox-impact-readonly.sql` counts identifiable historical claim-generated operating entries, active posted amounts, reversed entries, and soft-deleted entries without modifying records.

The configured connection points to localhost. A connection attempt with PostgreSQL `default_transaction_read_only=on` and a 15-second statement timeout failed with OperationalError before the SELECT could run. An initial elevated retry was not executed because automatic approval review failed due to a usage limit. After approval review became available, an approved retry also failed with OperationalError before the SELECT ran. No production connection was established. **Affected count is unknown, not zero.** No historical financial records were altered.

Reconciliation proposal, requiring separate approval:

1. Run the supplied aggregate SELECT using a read-only role on the confirmed production database. Export a controlled detail inventory of matching claim/receipt/book/journal IDs for finance review, including all reversals and period dates.
2. Reconcile each active posting to bank evidence and any independently authorized transfer into the operating cashbox. Do not infer a transfer from claim receipt confirmation alone; identify duplicates before preparing corrections.
3. Finance approves dedicated claim account mapping, a per-journal correction schedule, reconciliation totals and an open posting period. Retain original claims, receipts, journals and audit history.
4. After explicit approval, use authorized accounting adjustments to reclassify mistaken operating-account balances to the approved claim accounts, referencing original journal IDs. Record genuine transfers separately; do not delete or reverse real receipts merely to repair classification.
5. Before posting any corrections, specify and test how their source classification will interact with the operating reports, which already exclude legacy CLAIM entries. This prevents subtracting the legacy amounts a second time. No correction execution or correction-source implementation is included here.
6. Validate claim totals, operating reports, the bank reconciliation and the full trial balance against the approved schedule. Preserve an audit trail and reconciliation sign-off.

## Verification scope

New tests use uniquely named EF in-memory databases and synthetic records only. They cover normal/historical confirmation, duplicate confirmation, balanced claim journals, operating-account balances, explicit Company Income funding, legacy source/link exclusion, exports, and missing/invalid account configuration. In-memory tests do not prove PostgreSQL locking or transaction isolation.

The existing PostgreSQL integration scenario was updated for the new rule and dedicated synthetic accounts. It was not run because it creates database objects. No database migrations, seeders, real claim confirmations, real reversals, or historical corrections were performed.

Frontend verification: `npm --prefix frontend run build` passed the system-control check, TypeScript checking and Vite production bundling (2101 modules). Existing bundle-size and plugin-timing warnings remain. `git diff --check` passed. Backend verification uses separate output and intermediate directories because an existing API process locks its normal build artifacts; that process was not stopped or restarted.

Backend verification: **44 passed, 0 failed, 0 skipped**, including six new separation cases and the existing accounting/localization tests. Command (from `backend`):

```text
dotnet test MIS.Domain.Tests/MIS.Domain.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:IntermediateOutputPath=obj/claim-separation/ --output D:\Mis_v2\_buildcheck\claim-separation --filter "FullyQualifiedName~ClaimCashboxSeparationTests|FullyQualifiedName~AccountingBookTests|FullyQualifiedName~ApiTextLocalizerTests" --verbosity minimal
```

The command built Domain, Application, Infrastructure, API and test assemblies without starting the API. The existing EF in-memory dependency needed a package restore before the tests could compile. Existing NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 8.0.1 were reported; dependency versions were not changed.

## Files changed for this task

- `backend/MIS.Domain/Constants/AccountingBookValues.cs`: distinct non-operating receipt category.
- `backend/MIS.Domain/Entities/FinanceEntities.cs`: allow balanced confirmation of claim receipt journals.
- `backend/MIS.Infrastructure/Services/AccountingBookService.cs`: dedicated posting accounts and operating-report source selection.
- `backend/MIS.Infrastructure/Services/ClaimReceiptOptions.cs`: explicit account configuration and validation.
- `backend/MIS.Infrastructure/DependencyInjection.cs`: bind claim account configuration.
- `backend/MIS.Infrastructure/Services/AccountingBookReporting.cs`: exclude claim sources from operating balances and cash flow.
- `backend/MIS.Infrastructure/Services/AccountingBookExport.cs`: apply the same separation to cashbook exports.
- `backend/MIS.Application/Common/ApiTextLocalizer.cs`: actionable Arabic configuration error.
- `frontend/src/pages/accounting/FinanceClaimsPage.tsx`: explain independent claim collections accurately.
- `frontend/src/features/finance/financeUi.tsx`: display the claim receipt journal type in Arabic/English.
- `backend/MIS.Domain.Tests/ClaimCashboxSeparationTests.cs`: synthetic-data service regression tests.
- `backend/MIS.Domain.Tests/AccountingBookTests.cs`: exclude claim receipts from company-income category signs.
- `backend/MIS.Domain.Tests/AccountingBookIntegrationTests.cs`: update PostgreSQL expectations and synthetic account mapping.
- This report and `docs/sql/claim-cashbox-impact-readonly.sql`.

Pre-existing changes in `FinanceLayout.tsx` and `AccountingBookPage.tsx` belong to the previous display-name swap and were not changed by this task.
