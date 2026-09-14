# Excuses & Field Duties / الأعذار والمأموريات

The HR sidebar and employee profile expose `/hr/excuses`. The page supports manual requests, a pending visit queue, employee/department/type/source/status/date/search filters, review, supporting files, and navigation to a live read-only view of the original Field Visit.

## Data and visit lifecycle

- Completes the existing `HrExcuseMission` scaffold. One row represents a pending request and becomes an attendance excuse only after explicit approval. `HrExcuseAttachment` stores file metadata.
- `SourceVisitId` is a foreign key to the existing `CollectionFieldVisits` table. A filtered unique index permits only one request per visit, including across rescheduling and review cycles. No employee or visit is copied.
- Manual excuses reuse the same `HrExcuseMissions` row with `SourceType = Manual` and `SourceVisitId = null`. `FullDay` is stored on the same table (`20260912052615_AddHrExcuseFullDay`) so period-based and full-day manual excuses share one entity.
- Adds the existing `User.EmployeeId` scaffold to the database with a unique nullable foreign key. Unlinked collectors remain visible in the queue but cannot be approved. An approver can explicitly link them to an existing employee; an existing conflicting link is never overwritten.
- A scoped synchronizer runs every minute and before list/detail/notification/review operations. It discovers today's and future visits in Cairo time. Existing linked requests continue to be tracked after their visit date passes. It does not backfill unrelated historical visits.
- Pending rescheduling refreshes the same request. Cancellation/missed/failed visits cancel their request. Rescheduling or reassignment after a decision records the previous employee, dates, period and decision in HR audit history, then returns the same request to pending review. An approved visit cancelled later retains its decision metadata and receives an audited cancelled status.
- Completion/result/notes changes are flagged and audited; they do not fabricate an end time or automatically approve a request. HR can confirm an end time during review. Unknown end times remain null.
- PostgreSQL advisory locking, a unique visit index, optimistic revision checks and serializable review transactions protect against duplicate creation and concurrent decisions. Review details return the persisted database timestamp to avoid precision-related false conflicts.

## Notifications and access

There was no general persistent notification service in the inspected application. The pending persisted request itself is the notification identity, presented through the existing module header extension. Repeated refreshes do not insert notification rows. The header reads today's pending requests, including employee number, time, customer/case, bank/company, address and review link. Approving, rejecting, rescheduling away from today or cancelling removes that item from today's notification feed.

All endpoints first require the existing `HrDepartment` policy. The service additionally enforces:

| Capability | Allowed access |
| --- | --- |
| View requests, original linked visit and files | `HrManager`, `HrOfficer`, or `hr.excuses.view/manage/approve` |
| Create/edit pending manual requests and manage attachments | `HrManager`, `HrOfficer`, or `hr.excuses.manage/approve` |
| Approve/reject, receive notifications, link a collector | `HrManager` or `hr.excuses.approve` |
| Cancel an approved excuse | Approval access |

The three permission codes appear in the existing admin permission catalog. An HR officer does not receive approval notifications unless explicitly granted approval permission. File reads validate the attachment's excuse ID; there are no public storage URLs. Audit visibility is restricted consistently with excuse access.

## Attendance

`AttendanceListItemDto` and `AttendanceDetailsDto` include `ApprovedExcuses`, containing the request ID, source/type, date and optional start/end times. The attendance table displays these periods alongside its existing status. The read query checks the live visit's schedule, collector, employee link and cancellation status, so an approval cannot remain effective during a synchronizer delay after a visit changes.

This is an explanatory attendance overlay. It leaves raw fingerprint punches, calculated working/late/overtime minutes, existing day statuses, absence records and payroll deductions unchanged. A partial or open-ended field duty does not imply a whole day Present or automatic payroll relief. Existing reports/calculations can consume the approved periods without losing the original attendance evidence.

## Files and audit

Uploads reuse `IHrFileStorage`/`LocalHrFileStorage`, the configured `HrFiles.RootPath`, and the existing employee-document signature/extension validator. PDF, JPG/JPEG, PNG and the existing DOCX support are available, with a 10 MB limit. File contents remain in storage; the database contains name, MIME type, storage key, hash, length, uploader, upload time and excuse ID. Upload failure cleans up the new file; replacement and deletion remove the old file after the metadata transaction succeeds.

Requests, source changes, approvals, rejections, cancellations, employee linking and file uploads/replacements/deletions use `HrAuditLog`, including actor (system for synchronization), timestamp, record ID and before/after values.

## Migration and deployment

`20260912042454_AddHrExcuses` adds `HrExcuseMissions`, `HrExcuseAttachments` and the nullable unique `Users.EmployeeId` relationship. `20260912052615_AddHrExcuseFullDay` additively adds `HrExcuseMissions.FullDay`. Both reference existing employee, visit and user tables and do not recreate them. They follow the workspace's existing social-insurance migration.

The migration has been applied to the development PostgreSQL database additively (no data deleted). Disposable PostgreSQL integration tests still create uniquely named databases when `MIS_EXCUSE_TEST_CONNECTION` is set.
## Verification

`MIS.Domain.Tests/HrExcuseTests.cs` covers explicit approval/rejection, missing employee links, unknown/invalid periods, migration execution, request/notification idempotence, details and filtering, authorization, real local file storage upload/read/replace/delete, mismatched file signatures, stale revisions, concurrent reviewers, visit rescheduling/cancellation, database uniqueness, and attendance/punch preservation.

The PostgreSQL test reads `MIS_EXCUSE_TEST_CONNECTION`, creates a uniquely named disposable database, applies migrations and deletes that database and its temporary files in `finally`. No test employee, visit or excuse is created in the application database.

```powershell
cd backend
dotnet test MIS.Domain.Tests/MIS.Domain.Tests.csproj
cd ../frontend
npm run build
```

Headless browser checks exercised notification review links, approval with an end time, original visit navigation, attachment view/download/upload/replace/delete, manual creation/rejection, pending filters, and English LTR/Arabic RTL at desktop/mobile widths. Browser API responses were mocked; PostgreSQL and local file-storage behavior were tested separately.

Final verification: 232 backend tests passed, none skipped; solution build completed with zero warnings/errors; frontend TypeScript/production build passed; EF reported no pending model changes. Build output was isolated from the running API to avoid its locked DLLs.
