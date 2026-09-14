# Social insurance import

Manual entry remains on `/hr/social-insurance`. The adjacent import action opens `/hr/social-insurance/import`: upload, column mapping, preview/validation, and completion with a link back to the list (which fetches fresh data).

## API

All endpoints are under `/api/hr/social-insurance/imports` and require the HR department policy plus the existing insurance management role/permission. Every service operation checks permission; preview, confirmation and history are scoped to the uploader.

- `POST /`: multipart `file`; returns the upload ID and detected worksheets/headers. No insurance records are created.
- `POST /{id}/preview`: `{ sheetName, headerRow, firstDataRow, dateFormat, columns }`; creates a server-stored preview and returns its ID and validation rows.
- `POST /{id}/confirm`: `{ previewId }`; imports eligible rows from the latest stored preview. Client-supplied records are never accepted. Returns `{ imported, skipped, failed }`.
- `GET /`: uploader's latest 50 imports, file names, uploaders, timestamps, preview row counts and completion counts.

## Files and mapping

Uses the existing Attendance parser/signature validation, Employee Import workflow conventions, private HR file storage, and audit-backed history. CSV, XLSX and XLS are supported, up to 20 MB and 2,000 nonempty data rows. Sheet/header/data-start selection and optional date format are supported. Common English and Arabic headers are suggested; every mapping can be changed manually.

Supported columns: EmployeeNumber, EmployeeName (verification only), NationalId, EmployeeId (existing system UUID), SocialInsuranceNumber, InsuranceStartDate, InsuranceEndDate, InsurableSalary, InsuranceStatus, InsuranceOffice, ReferenceNumber, Notes. Employee identity fields are used for matching only; employees are never created or updated.

Matching uses the first supplied identifier in this order: employee number (case insensitive), national ID, system UUID. An unknown stronger identifier does not fall back to a weaker identifier. Ambiguous matches and conflicting additional identifiers are rejected. Name mismatches are warnings; names alone cannot match an employee.

## Validation and persistence

The existing SocialInsuranceRecord constructor and End method enforce insurance-number normalization and length (required, max 50), positive salary with at most two decimal places, text limits, allowed statuses, required insured/suspended start dates and end-date ordering. Ended imports require start/end dates; other statuses cannot carry an end date. Salary accepts an invariant decimal without thousands separators. English and Arabic status labels map to Insured, NotInsured, Suspended and Ended. Unknown/blank statuses are errors.

Existing non-ended records are skipped, including NotInsured and Suspended, consistent with existing database indexes. Active insurance numbers cannot be shared. Duplicates within the file are also skipped. No update or overwrite mode exists. Ready and Warning rows import only after confirmation; invalid/existing rows count as skipped. Confirmation rechecks employee existence and active conflicts. Unique database indexes arbitrate concurrent active creates. Savepoints isolate row failures, and an advisory lock makes confirmation retries idempotent.

Records and the completion audit commit together. Audit events SocialInsuranceImportUploaded (started), SocialInsuranceImportPreviewed, and SocialInsuranceImportCompleted capture history and aggregate imported/skipped/failed counts without per-row audit noise. Preview payloads use private file storage rather than duplicating records in a database table. No import schema changes or additional insurance/employee tables are introduced.

## Verification

`SocialInsuranceImportTests` covers identifier precedence, name-only/unmatched rejection, conflicting identifiers, salary/date/status validation, duplicate skips, ended records and service permissions. The existing disposable PostgreSQL insurance lifecycle test also covers upload without writes, confirmation without preview rejection, mixed-row import, retry idempotency, history and list visibility. Set MIS_SOCIAL_TEST_CONNECTION to run it; it creates and drops a uniquely named test database.

Browser checks use mocked API data, including manual create/edit/end, search/filters, import navigation, auto mapping, preview/edit/confirm, completion and Arabic RTL. No permanent test insurance records are created.
