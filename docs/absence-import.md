# Absence import

Company absences remain on `EmployeeAbsences`. Manual Register Absence is unchanged.

Import reuses the Social Insurance / Employee import pattern:

- Upload CSV/XLS/XLSX via `IHrFileStorage` (`absence-imports`)
- Column mapping + preview JSON (`absence-import-previews`)
- Confirm creates the same `EmployeeAbsence` rows as manual entry
- History is audit-backed (`AbsenceImportUploaded` / `Previewed` / `Completed`) — no new import tables

Employee matching order: Employee Number → National ID → Mobile Number → uniquely matching name.

Duplicates (`EmployeeId` + `AbsenceDate`) are skipped. Approved leave, conflicting attendance (non-absent / punches), and approved excuses/field duties block the row. Raw fingerprint punches are never modified.
