# MIS

MIS Collection Firm is the foundation for an internal collection management system. The repository is organized as a production-oriented full-stack application with a React/TypeScript frontend and a Clean Architecture ASP.NET Core backend.

## One-step Development Start

From the repository root, run:

```powershell
.\start-dev.cmd
```

This starts the API and React application together, installs frontend packages when
`node_modules` is missing, opens `http://localhost:5173` after both services are ready,
and stops both processes with `Ctrl+C`. Double-clicking `start-dev.cmd` provides the
same behavior. Use `.\start-dev.cmd -NoBrowser` when automatic browser launch is not
wanted, or `-SkipInstall` to skip the dependency check.

## Architecture

```text
MIS/
  frontend/
    src/
      assets/                       # MIS logo and visual assets
      components/common/            # Reusable buttons, spinners, error blocks
      components/forms/             # Reusable form inputs
      components/layout/            # Auth page shell and page layouts
      config/                       # Frontend runtime configuration
      context/                      # Auth context
      features/auth/                # Auth feature components, services, types, validation
      pages/auth/                   # Login page
      pages/dashboard/              # Protected dashboard placeholder
      routes/                       # App routes and ProtectedRoute
      services/                     # Central Axios API client
      types/                        # Shared API types
      utils/                        # Storage helpers
  backend/
    MIS.API/                        # Controllers, middleware, CORS/JWT/API composition
    MIS.Application/                # DTOs, interfaces, auth use case
    MIS.Domain/                     # Auth and normalized Core HR domain entities
    MIS.Infrastructure/             # EF Core/PostgreSQL, HR services, storage, exports, auth, seed data
    MIS.Domain.Tests/               # Core HR domain and data-integrity tests
    MIS.sln
    dotnet-tools.json               # Repo-local dotnet-ef tool
```

## Prerequisites

- .NET SDK 10
- Node.js 24+
- PostgreSQL

## Administration Center

Administrators are routed to `/admin/dashboard`. The administration center provides:

- a decision queue for pending, expiring, privileged, and unused access;
- account provisioning, secure temporary-password reset, activation, and immediate suspension;
- granular permissions for current and planned modules with own/team/department/client/all scopes;
- explicit impact review, business justification, responsibility acknowledgement, and typed confirmation before access is granted;
- client-scoped Collections access synchronized with backend row-level organization access;
- an immutable administration audit trail and immediate JWT invalidation after access, password, or account-status changes.

The users supplied in the initial operating directory are seeded in development as inactive, non-login accounts. Their department/client relationships are saved only as `PENDING` access proposals. An administrator must set a secure temporary password, complete the access review, and activate each account separately.

## Backend Setup

The backend does not store secrets in `appsettings.json`. Configure sensitive values with environment variables or user secrets.

PowerShell environment variable example:

```powershell
cd backend
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=mis_dev;Username=postgres;Password=YOUR_DEV_DB_PASSWORD"
$env:Jwt__SecretKey="replace-with-a-long-random-dev-secret-at-least-32-bytes"
$env:Seed__AdminPassword="choose-a-strong-development-password"
$env:Seed__HrPassword="choose-a-strong-development-hr-password"
dotnet restore
dotnet build
dotnet run --project MIS.API
```

User secrets example:

```powershell
cd backend/MIS.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=mis_dev;Username=postgres;Password=YOUR_DEV_DB_PASSWORD"
dotnet user-secrets set "Jwt:SecretKey" "replace-with-a-long-random-dev-secret-at-least-32-bytes"
dotnet user-secrets set "Seed:AdminPassword" "choose-a-strong-development-password"
dotnet user-secrets set "Seed:HrPassword" "choose-a-strong-development-hr-password"
dotnet run
```

Development seed user:

- Username: `admin`
- Department: `ADMIN`
- Password: supplied by `Seed:AdminPassword`

Development HR user (when `Seed:HrPassword` is configured):

- Username: `hr.user`
- Email: `hr@mis.local`
- Department: `HR`
- Password: supplied by `Seed:HrPassword`
- Role: `HrManager` by default; set `Seed:HrRole=HrOfficer` for an operations-only user

The seed password is intentionally not checked into source control.

HR roles are enforced by the API, not only by the UI. `HrManager` can view and update
restricted compensation and banking data; `HrOfficer` can operate the remaining HR
workflows without receiving those fields in employee-profile responses.

## Database

All additive migrations are kept at:

```text
backend/MIS.Infrastructure/Persistence/Migrations
```

To create a future migration:

```powershell
cd backend
dotnet tool restore
dotnet tool run dotnet-ef migrations add MigrationName --project MIS.Infrastructure --startup-project MIS.API --output-dir Persistence\Migrations
```

To apply migrations:

```powershell
cd backend
dotnet tool restore
dotnet tool run dotnet-ef database update --project MIS.Infrastructure --startup-project MIS.API
```

If you prefer a global EF tool, the equivalent command is:

```powershell
dotnet ef database update --project MIS.Infrastructure --startup-project MIS.API
```

Design-time EF commands load `MIS.API/appsettings.json`, the active environment file,
MIS.API user secrets, and then environment variables (in that precedence order).
The checked-in connection string intentionally has no password, so configure
`ConnectionStrings:DefaultConnection` through user secrets or set
`ConnectionStrings__DefaultConnection` / `MIS_DB_CONNECTION` before running migrations.

Core HR migrations preserve history: employee-related foreign keys are restricted rather
than cascaded, and employees are deactivated or terminated instead of deleted.

## Frontend Setup

```powershell
cd frontend
copy .env.example .env
npm install
npm run dev
```

Default frontend API URL:

```text
VITE_API_URL=http://localhost:5000/api
```

Routes:

- `/login` - MIS branded sign-in page
- `/hr` and `/hr/dashboard` - protected HR dashboard
- `/hr/employees` and `/hr/employees/:id` - directory and full employee profile
- `/hr/attendance` - manual attendance and day processing
- `/hr/attendance/import` - validated Excel/CSV import and persisted import history
- `/hr/leaves` - requests, decisions, entitlements, and balances
- `/hr/calendar` - working week, weekends, holidays, and special working days
- `/hr/absences` - existing company absence records retained for compatibility
- `/hr/employee-documents` - document lifecycle and expiry tracking
- `/hr/delegations` - administrative delegations and A4 print view
- `/hr/master` - organization and HR master data
- `/hr/reports` - report preview and organized Excel/PDF exports
- `/hr/audit` - searchable HR audit history
- `/collections/dashboard` - Collections Command Center and personalized work queue
- `/collections/clients` - configurable client organizations and portfolio workspaces
- `/collections/cases` and `/collections/cases/:id` - paged workbench and audited case 360
- `/collections/promises` - deterministic promise-to-pay hub
- `/collections/payments` - maker-checker daily collections review
- `/collections/assignments` - manual or deterministic balanced/geographic assignment preview and confirmation
- `/collections/visits` - field visit planning and results
- `/collections/complaints` - complaint ownership, lifecycle, and SLA tracking
- `/collections/imports` - validated portfolio upload, preview, errors, and safe confirmation
- `/collections/audit` - immutable Collections audit history
- `/collections/reports` - server-calculated executive, client, bucket, and collector reports with authorized CSV export
- `/collections/settings` - audited client, portfolio, target, PTP policy, and bucket configuration
- `/collections/branding` - validated bank/client logo management with polished fallback identity marks
- `/collections/profile` and `/hr/profile` - self-service account, login email, and password security
- `/finance/dashboard` - posted-ledger finance command center with client-money separation
- `/finance/journals` and `/finance/journals/:id` - journal workflow, approval, posting, and linked reversal
- `/finance/accounts` - bilingual chart of accounts and live posted balances
- `/finance/periods` - fiscal-year initialization, soft close, close, and controlled reopen
- `/finance/reports` - as-of trial balance sourced from posted/reversed journal chains

## Accounting & Finance Module

Finance is implemented as a bounded context inside the existing modular monolith. PostgreSQL schema `finance` owns legal entities, currencies, accounting periods, accounts, accounting events, journal headers/lines, the client subledger, and append-only financial audit records. The initial chart separates collection-channel assets from client-money liabilities and company revenue.

Approving a Collections payment now performs the operational balance update and the `CollectionConfirmed` accounting event, balanced journal, client-subledger entry, audit record, and source-to-journal link inside the same serializable database transaction. A failure rolls back the complete approval. Database uniqueness protects the source event and idempotency key, and a payment can link to only one financial journal.

Manual journals follow `DRAFT -> PENDING_APPROVAL -> APPROVED -> POSTED`. The maker cannot approve their own journal, closed periods reject posting, control accounts require specialized access and dimensions, and posted entries are corrected through linked reversals rather than editing or deletion. The current implementation posts EGP at rate 1 and deliberately blocks foreign-currency posting until an approved exchange-rate source/profile is configured.

## Enterprise Collections Module

`CollectionCase` is the operational aggregate connecting the configurable client
organization, portfolio, customer, account, money position, bucket, assignee, activity,
PTP, payment, visit, complaint, and audit records. Client records are not hardcoded into
business logic; the development seed creates initial configurable organizations and
their default portfolios and bucket definitions.

Financial amounts use PostgreSQL `numeric(18,2)`. Critical writes preserve assignment,
bucket, activity, and audit history. Collection payment approval enforces maker-checker
separation of duties. Sensitive customer fields are masked by default, the API checks
the reveal capability, and every reveal is audited. Backend queries enforce collector,
team supervisor, and client/portfolio row scopes.

PTP evaluation is deterministic and audited. Portfolio settings override client settings
using `ptpGraceDays` (0-30) and `ptpToleranceAmount`; due/broken/partial/fulfilled
transitions are refreshed by authoritative backend services. Case attachments use private
storage, accept only content-validated PDF/JPEG/PNG up to 10 MB, and require authorized,
audited download endpoints rather than public URLs.

Case 360 also calculates a bilingual next-best-action from live operational state
(breached complaint SLA, pending payment review, broken/due PTP, overdue follow-up, or
priority score). Automatic assignment uses the audited `BALANCED_GEO_V1` rule: priority
ordering, active-workload capacity, existing governorate coverage, and stable name
tie-breaking. Both preview and confirmation reapply the caller's row-level scope.

Collections roles are granular system capabilities:

- `CollectionsCollector`
- `CollectionsSupervisor`
- `CollectionsReviewer`
- `CollectionsOperationsManager`
- `CollectionsClientViewer`
- `CollectionsAuditor`

For a development Collections user, configure `Seed:CollectionsPassword` and optionally
`Seed:CollectionsUsername`, `Seed:CollectionsEmail`, `Seed:CollectionsFullName`, and
`Seed:CollectionsRole`. The default seeded role is `CollectionsOperationsManager`.

Every user has an immutable login code in the format `USR-XXXXXXXX`. Authentication
accepts this code, the username, or the current email address. The bilingual profile
page allows users to change their login email and password after confirming their
current password; login codes are system-generated, unique, and cannot be edited.

Desktop module navigation can be collapsed to an icon rail and remembers the user's
choice per module. Collection and shared HR date fields use the in-system bilingual
calendar rather than the browser's inconsistent native RTL picker. Client branding
accepts content-validated PNG, JPEG, or WebP logos up to 2 MB; when no official logo has
been uploaded, the UI shows a deterministic branded monogram instead of an empty icon.

Portfolio import accepts CSV and XLSX up to 20 MB and 20,000 rows. Required logical
columns (English or supported Arabic aliases) are account reference, customer code,
customer name, outstanding balance, overdue balance, and days past due. Optional columns
include national ID, mobile, contract reference, and product type. The workflow is:

```text
Upload -> signature/size validation -> parse -> persisted row validation
       -> preview and error CSV -> explicit confirmation
       -> serializable safe upsert by client + account reference -> audit
```

No invalid row is silently imported. Confirmed duplicate files are rejected by SHA-256
within the portfolio. Configure private storage with `HrFiles__RootPath`; Collections
imports reuse the existing protected storage service under a separate scope.

## Core HR Modules

The employee profile is the central aggregate for personal/contact data, organization
assignment, reporting line, contract, restricted compensation/bank information,
emergency contact, documents, attendance, leave/absence, delegations, and audit history.

Attendance import uses a persisted, reviewable workflow:

```text
Upload CSV/XLS/XLSX
  -> inspect sheets and headers
  -> map columns/layout/time zone
  -> validate and group employee/day punches
  -> paged preview and error summary
  -> explicit confirmation
  -> duplicate/leave recheck in a serializable transaction
  -> attendance records, punches, audit, dashboard, and reports
```

Imports are limited to 20 MB, 100,000 source rows, 50,000 employee/day groups, and
256 columns. Files are checked by content signature and SHA-256, not filename alone.
Employee documents are limited to 10 MB and accept content-validated PDF, JPEG, PNG,
and DOCX files.

Configure an absolute production storage path with, for example:

```text
HrFiles__RootPath=D:\MISData\HrFiles
```

The initial working calendar is editable database data (Africa/Cairo, Sunday through
Thursday). Weekend, holiday, special-day, grace, break, and overtime rules are read from
the database for attendance and leave calculations.

Egypt-oriented employee validation normalizes Arabic/Western digits, validates the
14-digit national ID against birth date and gender, normalizes Egyptian mobile prefixes,
checks IBAN length/checksum, and prevents attendance, leave, absence, delegation,
contract, and compensation dates from falling outside the employee service period.
Compensation changes are effective-dated and preserve prior versions. Excel and PDF HR
exports embed the company logo, including repeated PDF page branding.

## Verification

```powershell
cd backend
dotnet restore
dotnet build MIS.sln -m:1
dotnet test MIS.sln -m:1

cd ../frontend
npm run build
```

## Authentication Flow

1. React posts credentials to `POST /api/auth/login`.
2. ASP.NET Core validates the user against PostgreSQL.
3. Password verification uses ASP.NET Core `PasswordHasher<T>`.
4. The API returns a JWT access token and safe user profile fields.
5. React stores the token in `sessionStorage` or `localStorage` depending on "Remember me".
6. Department is read from the database, included in the signed JWT, and used for post-login routing.
7. HR API endpoints enforce the `HrDepartment` policy server-side; frontend guards provide the matching navigation boundary.

## API Error Format

Errors use a consistent response shape:

```json
{
  "success": false,
  "message": "Invalid username or password.",
  "errors": []
}
```

Technical exception details are logged by the API and are not shown to users.

