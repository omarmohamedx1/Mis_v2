# Social Insurance

Route: `/hr/social-insurance`. The HR sidebar and employee profile expose the module to authorized users.

`SocialInsuranceRecord.EmployeeId` references the existing Employee with restricted deletion. Employee names, departments, positions and national IDs are loaded from Employee; they are not copied into insurance records. Migration `20260911232025_AddSocialInsuranceRecords` creates only the new table and its constraints/indexes.

The main table shows one row per employee: their open record, latest ended record, or Not Insured when no record exists. View includes all historical records. Insured, Suspended and Not Insured records reserve the employee and insurance number until ended. PostgreSQL partial unique indexes enforce these reservations, including concurrent requests. Arabic digits and letter case are normalized before storing insurance numbers.

Ending requires an end date, preserves the record, and releases its reservations. Ended records cannot be edited or reopened; a subsequent insurance period uses Add Insurance Record. Insurable salary is positive with at most two decimal places. Suspended and Insured records require a start date.

HR managers and HR officers can manage insurance. The administration permission catalog also offers `hr.social_insurance.view` and `hr.social_insurance.manage`, with explicit ALL scope; users still need access to the HR module. Only HR managers or users with existing sensitive-data permission receive national IDs. The server checks insurance permissions, and insurance audit entries are hidden from users without insurance access. Each successful write and its HR audit entry commit in one transaction.

Summary cards and filters use the employee selection/search/department scope. The three cards show insured employees, employees without a record or with a Not Insured record, and the number of historical ended records. Status filters use each employee's displayed record.

## Verification

- Backend build passed; all 204 tests passed, including the PostgreSQL insurance integration test.
- The integration test checks migrations, creation, editing, ending, history, normalized duplicate numbers, one open record per employee, filters, read-only authorization and audit persistence. Set `MIS_SOCIAL_TEST_CONNECTION` to a PostgreSQL connection whose user can create databases to run it; otherwise this test is explicitly skipped. It creates and removes a uniquely named `mis_insurance_test_*` database and never writes insurance test rows to the configured application database.
- Frontend TypeScript and production Vite build passed.
- Headless Edge checked selection, create/edit/end, history, search, department/status filters, read-only controls and English/Arabic RTL with mocked browser data.
- Live read-only browser checks verified HR login, existing employee selection, the employee-profile insurance tab and full-module link.
- The local migration was applied. No permanent fake insurance records were created.
