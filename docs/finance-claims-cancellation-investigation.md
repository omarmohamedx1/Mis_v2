# Finance claim cancellation investigation — 2026-11-14

## Confirmed findings

- The public Vercel page loads `index-r_4Rz-cb.js`, `FinanceClaimsPage-20oC8pER.js`, and `LocalizationContext-JOVyENxW.js`. The latter compiles the API base URL as `https://mis-api.runasp.net/api`. The claims bundle sends POST `/finance/claims/{id}/cancel` with `{ reason }`, matching the repository. With no reason supplied, JSON serialization produces `{}`. The ID is the claim UUID, not the displayed serial 244.
- The API health endpoint returned 200 with `{"status":"Healthy"}`. Public Swagger returned 404. A read-only GET to `/api/finance/claims/00000000-0000-0000-0000-000000000000/cancel` returned 405 with `Allow: POST`, confirming a deployed POST route. This does not prove its body contract or backend revision. No mutation request was used to probe it.
- Local `FinanceClaimsController.Cancel` exists, accepts a nullable reason in a JSON body, and requires FinanceAccess plus FinanceClaimsCancel. The service separately checks `finance.claims.cancel` (or admin/wildcard). The frontend sends its bearer token and Accept-Language.
- `FinanceClaimsService.CancelAsync` starts a serializable transaction, loads the non-deleted claim, rejects an already cancelled claim, and rejects any receipt posting whose ReversedAt is null. It then changes status to CANCELLED, records actor/time, inserts a financial audit event, saves and commits, and reads details. It does not delete or reverse financial records.
- The explicit financial relationship is claim → receipt posting → accounting book entry → journal. There is no direct collection relationship in the claim model. The actual linked records of claim 244 have **not** been inspected; no authenticated production session or original failure response was available.
- The domain and database model permit CANCELLED. ACTIVE excludes CANCELLED, CANCELLED selects it, and ALL includes it (all exclude soft-deleted rows). The frontend already reloads the list after success and exposes all three filters.
- Conflict exceptions become HTTP 409, missing claims 404, permission failures 403, validation/integrity failures 400, and unhandled failures 500. `ApiErrorResponse.Failure` localizes these messages. The four cancellation messages were missing from the Arabic dictionary, causing the exact generic message in the screenshot. The frontend displays that server message, overriding its more descriptive fallback.

## Fix and limits

Added explicit Arabic translations for missing claims, already-cancelled claims, invalid cancellation state, and active receipt postings, with four regression cases through the actual API error factory. No financial rules, routes, schema, or frontend behavior needed changes based on the available evidence.

This fixes a confirmed loss of error information. It does **not** establish why claim 244 was rejected or establish that the backend is outdated. A received date in the screenshot does not prove a live posting: historical imports can have received status without a posting.

## Remaining production verification

1. Inspect the original failed request's HTTP status, response, UUID, timestamp and available server logs; do not retry cancellation of a real claim for testing.
2. With authorized read-only access, GET the claim details and inspect receipt postings/reversal timestamps and journal links. Compare the running backend revision with the repository. Check schema compatibility read-only if logs indicate missing columns or constraints. Do not run migrations or seeders as part of this investigation.
3. Deploy the backend localization fix to the API host. If its cancellation implementation is outdated, deploy the reviewed compatible backend implementation as well. Vercel-only deployment will not update the API. A changed VITE_API_URL requires rebuilding the frontend; there is no evidence that the currently compiled host is wrong.
4. Keep `MIS:SkipStartupDatabaseChanges=true` (`MIS__SkipStartupDatabaseChanges=true` as an environment variable) during safe restarts. Preserve the existing read-only signing-key initialization and disabled startup seeding/worker behavior. No migration is required by this localization patch.
5. Verify end-to-end successful cancellation and blocked posted receipts using synthetic records in an isolated non-production database, including Cancelled/All/Active filters. Existing AccountingBookIntegrationTests cover these service paths but create database objects and were deliberately not run here.

All pre-existing uncommitted changes were preserved. No backend startup, database connection, migration, seeder, production cancellation, receipt reversal, or financial write was performed.

## Verification results

- `dotnet test MIS.Domain.Tests/MIS.Domain.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false --filter "FullyQualifiedName~ApiTextLocalizerTests|FullyQualifiedName~AccountingBookTests" --verbosity minimal` from `backend`: 38 passed, 0 failed, 0 skipped. Builds Domain, Application, Infrastructure, API and test assemblies with the pinned .NET 10 SDK. Includes the four new Arabic cases and existing cancellation state, monetary preservation and authorization tests.
- Existing restore metadata reports NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 8.0.1. No dependency changes were made.
- `git diff --check`: passed.
- `npm --prefix frontend run build`: passed TypeScript checking and Vite production bundling (2097 modules). Build output was not deployed.
- Initial sandbox build attempts were blocked by compiler pipe access and Vite spawn EPERM; verification was retried with approved execution access. Database-writing integration tests were not run.
