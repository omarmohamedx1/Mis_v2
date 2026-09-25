using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using Npgsql;

namespace MIS.Infrastructure.Services;

public sealed class EmployeeImportService(ApplicationDbContext db, IHrFileStorage storage, IHrAuditService audit,
    ICurrentUserContext user, IEmployeeCreationService creation, IWorkingCalendarCalculator calendar) : IEmployeeImportService
{
    private const string Entity = "EmployeeImport";
    private const long MaximumBytes = ExcelImportLimits.MaximumBytes;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, string StorageKey, int TotalRows);

    public async Task<EmployeeImportTemplate> BuildTemplateAsync(CancellationToken cancellationToken)
    {
        var departments = await db.Departments.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Code, item.Name, item.NameArabic })
            .ToListAsync(cancellationToken);
        var positions = await db.Positions.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Code, item.Name, item.NameArabic })
            .ToListAsync(cancellationToken);
        var organizations = await db.CollectionClientOrganizations.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.NameEnglish)
            .Select(item => new { item.Code, item.NameArabic, item.NameEnglish, item.OrganizationType })
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var employees = workbook.Worksheets.Add("Employees");
        var headers = new[]
        {
            "Employee Number", "Employee Name Arabic", "Employee Name English", "National ID", "Position / Job Title", "Employment Date",
            "Department", "Assigned Bank / Company", "Employee Role", "Mobile Number", "Gender", "Date of Birth", "Fingerprint Date",
            "End Work Date", "Address", "Status", "Basic Salary", "Allowances", "Work Number", "Package Type"
        };
        for (var index = 0; index < headers.Length; index++) employees.Cell(1, index + 1).Value = headers[index];
        var header = employees.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0B638F");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        employees.SheetView.FreezeRows(1);
        employees.Range("F2:F2001").Style.DateFormat.Format = "yyyy-mm-dd";
        employees.Range("L2:N2001").Style.DateFormat.Format = "yyyy-mm-dd";
        employees.Range("Q2:R2001").Style.NumberFormat.Format = "#,##0.00";
        employees.Columns().AdjustToContents();
        employees.Column(2).Width = Math.Max(employees.Column(2).Width, 28);
        employees.Column(3).Width = Math.Max(employees.Column(3).Width, 28);
        employees.Column(15).Width = 32;
        employees.Column(17).Width = Math.Max(employees.Column(17).Width, 16);
        employees.Column(18).Width = Math.Max(employees.Column(18).Width, 14);
        employees.Column(19).Width = Math.Max(employees.Column(19).Width, 16);
        employees.Column(20).Width = Math.Max(employees.Column(20).Width, 16);
        employees.Range(1, 1, 2001, headers.Length).SetAutoFilter();

        var instructions = workbook.Worksheets.Add("Instructions - تعليمات");
        var guidance = new[]
        {
            ("How to use", "طريقة الاستخدام"),
            ("Enter one employee per row in the Employees sheet. Do not rename the headers.", "أدخل موظفًا واحدًا في كل صف داخل ورقة Employees ولا تغيّر أسماء الأعمدة."),
            ("Required: Employee Number, Arabic name and/or English name, National ID, Position and Employment Date.", "إلزامي: رقم الموظف، الاسم العربي أو الإنجليزي، الرقم القومي، المسمى الوظيفي وتاريخ التعيين."),
            ("Recommended: Basic Salary and Allowances. Leave empty only if compensation will be entered later.", "موصى به: الراتب الأساسي والبدلات. اتركهما فارغين فقط إذا هتسجّل الراتب بعد كده."),
            ("Use yyyy-mm-dd for dates and keep National ID as 14 digits.", "استخدم yyyy-mm-dd للتواريخ، ويجب أن يكون الرقم القومي 14 رقمًا."),
            ("Department is the internal HR unit (Collections, HR, Accounting, Office). Banks and finance companies go in Assigned Bank / Company.", "القسم هو الوحدة الداخلية (التحصيل، الموارد البشرية، الحسابات، الأوفيس). البنوك وشركات التمويل تُسجل في البنك / الشركة المكلّف بها."),
            ("Work Number and Package Type are optional collector assignment fields.", "رقم الشغل ونوع الباقة اختياريان لتكليف المحصل."),
            ("Use values from Reference Data for departments, positions, and assigned banks/companies.", "استخدم القيم الموجودة في ورقة Reference Data للأقسام والمسميات والبنوك/الشركات."),
            ("Employee Role: ADMIN, COLLECTOR, SUPERVISOR or OFFICE. Status: Active, Inactive, OnLeave, Suspended or Terminated.", "الدور الوظيفي: ADMIN أو COLLECTOR أو SUPERVISOR أو OFFICE. الحالة: Active أو Inactive أو OnLeave أو Suspended أو Terminated."),
            ("Existing employee numbers or National IDs are skipped and never overwritten.", "الموظف الموجود بنفس الرقم الوظيفي أو القومي يتم تجاوزه ولا تُستبدل بياناته.")
        };
        instructions.Cell(1, 1).Value = "English";
        instructions.Cell(1, 2).Value = "العربية";
        for (var index = 0; index < guidance.Length; index++)
        {
            instructions.Cell(index + 2, 1).Value = guidance[index].Item1;
            instructions.Cell(index + 2, 2).Value = guidance[index].Item2;
        }
        instructions.Range(1, 1, 1, 2).Style.Font.Bold = true;
        instructions.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7F3FA");
        instructions.Columns(1, 2).Width = 70;
        instructions.Columns(1, 2).Style.Alignment.WrapText = true;

        var references = workbook.Worksheets.Add("Reference Data");
        references.Cell(1, 1).Value = "Department Code";
        references.Cell(1, 2).Value = "Department";
        references.Cell(1, 3).Value = "القسم";
        references.Cell(1, 5).Value = "Position Code";
        references.Cell(1, 6).Value = "Position";
        references.Cell(1, 7).Value = "المسمى الوظيفي";
        references.Cell(1, 9).Value = "Organization Code";
        references.Cell(1, 10).Value = "Assigned Bank / Company";
        references.Cell(1, 11).Value = "البنك / الشركة";
        for (var index = 0; index < departments.Count; index++)
        {
            references.Cell(index + 2, 1).Value = departments[index].Code;
            references.Cell(index + 2, 2).Value = departments[index].Name;
            references.Cell(index + 2, 3).Value = departments[index].NameArabic ?? string.Empty;
        }
        for (var index = 0; index < positions.Count; index++)
        {
            references.Cell(index + 2, 5).Value = positions[index].Code;
            references.Cell(index + 2, 6).Value = positions[index].Name;
            references.Cell(index + 2, 7).Value = positions[index].NameArabic ?? string.Empty;
        }
        for (var index = 0; index < organizations.Count; index++)
        {
            references.Cell(index + 2, 9).Value = organizations[index].Code;
            references.Cell(index + 2, 10).Value = organizations[index].NameEnglish;
            references.Cell(index + 2, 11).Value = organizations[index].NameArabic;
        }
        references.Range(1, 1, 1, 11).Style.Font.Bold = true;
        references.Range(1, 1, 1, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7F3FA");
        references.SheetView.FreezeRows(1);
        references.Columns().AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return new EmployeeImportTemplate(output.ToArray(), "Employee_Import_Template.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<EmployeeImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException(ExcelImportLimits.SizeMessage);
        var stored = await storage.SaveAsync("employee-imports", file.FileName, file.ContentType, file.Content, MaximumBytes, cancellationToken);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, cancellationToken);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, cancellationToken);
            var id = Guid.NewGuid();
            await Write(id, "EmployeeImportUploaded", new Uploaded(stored.OriginalFileName, stored.StorageKey, extension), cancellationToken);
            return new EmployeeImportUpload(id, stored.OriginalFileName, sheets);
        }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken cancellationToken) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id
            && log.Action == "EmployeeImportUploaded" && log.UserId == user.UserId, cancellationToken)
        ?? throw new HrNotFoundException("Employee import was not found.");

    public async Task<EmployeeImportPreview> PreviewAsync(Guid id, EmployeeImportMapping mapping, CancellationToken cancellationToken)
    {
        var upload = Read<Uploaded>((await OwnedUpload(id, cancellationToken)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportCompleted", cancellationToken))
            throw new HrConflictException("This employee import is already complete.");
        if (mapping.Columns.Count > EmployeeImportMapper.Fields.Length || mapping.Columns.Keys.Except(EmployeeImportMapper.Fields).Any())
            throw new HrValidationException("Unsupported employee field mapping.");
        await using var stream = await storage.OpenReadAsync(upload.StorageKey, cancellationToken);
        var identityColumns = new[] { mapping.Columns.GetValueOrDefault("MobileNumber"), mapping.Columns.GetValueOrDefault("NationalId") }
            .Where(column => !string.IsNullOrWhiteSpace(column)).Cast<string>().ToArray();
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow,
            mapping.FirstDataRow, cancellationToken, identityColumns, mapping.SheetNames);
        var indexes = new Dictionary<string, int>();
        foreach (var pair in mapping.Columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }
        var departments = await db.Departments.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new EmployeeImportMapper.Lookup(item.Id, item.Name, item.Code, item.NameArabic))
            .ToArrayAsync(cancellationToken);
        var collectionsDepartment = departments.SingleOrDefault(item => string.Equals(item.Code, "COLLECTIONS", StringComparison.OrdinalIgnoreCase));
        var organizations = await db.CollectionClientOrganizations.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new EmployeeImportMapper.Lookup(item.Id, item.NameEnglish, item.Code, item.NameArabic))
            .ToArrayAsync(cancellationToken);
        var positions = await db.Positions.AsNoTracking().Where(item => item.IsActive).Select(item => new EmployeeImportMapper.Lookup(item.Id, item.Name, item.Code, item.NameArabic, item.DepartmentId)).ToArrayAsync(cancellationToken);
        var rows = new List<EmployeeImportRow>();
        var existingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingNationalIds = new HashSet<string>(StringComparer.Ordinal);
        var sourceNumbers = indexes.TryGetValue("EmployeeNumber", out var numberIndex)
            ? table.Rows.Where(row => numberIndex < row.Length).Select(row => row[numberIndex].Trim().ToUpperInvariant()).ToArray() : [];
        var sourceIds = indexes.TryGetValue("NationalId", out var idIndex)
            ? table.Rows.Where(row => idIndex < row.Length).Select(row => row[idIndex].Trim()).ToArray() : [];
        var existing = await db.Employees.AsNoTracking().Where(employee => sourceNumbers.Contains(employee.EmployeeNumber.ToUpper())
            || (employee.NationalId != null && sourceIds.Contains(employee.NationalId)))
            .Select(employee => new { employee.EmployeeNumber, employee.NationalId }).ToArrayAsync(cancellationToken);
        foreach (var employee in existing) { existingNumbers.Add(employee.EmployeeNumber); if (employee.NationalId is not null) existingNationalIds.Add(employee.NationalId); }
        var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nationalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cells in table.Rows)
        {
            var values = indexes.ToDictionary(pair => pair.Key, pair => pair.Value < cells.Length ? cells[pair.Value] : "");
            var errors = new List<string>();
            var warnings = new List<string>();
            var request = EmployeeImportMapper.Map(values, mapping.DateFormat, departments, positions, errors, warnings, organizations, collectionsDepartment);
            var duplicate = (!string.IsNullOrWhiteSpace(request.EmployeeNumber) && !numbers.Add(request.EmployeeNumber.Trim()))
                | (!string.IsNullOrWhiteSpace(request.NationalId) && !nationalIds.Add(request.NationalId));
            var existingEmployee = existingNumbers.Contains(request.EmployeeNumber) || existingNationalIds.Contains(request.NationalId);
            rows.Add(await EvaluateRowAsync(rows.Count + 1, request, departments, positions, organizations,
                errors, warnings, values.GetValueOrDefault("Department", ""), values.GetValueOrDefault("Position", ""),
                values.GetValueOrDefault("Organization", ""), duplicate, existingEmployee, cancellationToken));
        }
        return await SavePreviewAsync(id, rows, cancellationToken);
    }

    public async Task<EmployeeImportPreview> ReviseAsync(Guid id, ReviseEmployeeImportRequest request, CancellationToken cancellationToken)
    {
        await OwnedUpload(id, cancellationToken);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportCompleted", cancellationToken))
            throw new HrConflictException("This employee import is already complete.");
        if (request.Rows.Count is < 1 or > 200)
            throw new HrValidationException("Revise between 1 and 200 employee rows.");
        var preview = await LoadPreviewAsync(id, request.PreviewId, cancellationToken);
        var lookups = await LoadLookupsAsync(cancellationToken);
        var revisions = request.Rows.GroupBy(item => item.Row).ToDictionary(group => group.Key, group => group.Last().Employee);
        var next = preview.Rows.Select(row => revisions.TryGetValue(row.Row, out var employee)
            ? row with { Employee = employee }
            : row).ToList();
        var fileNumbers = next.Select(row => row.Employee.EmployeeNumber?.Trim() ?? "").Where(value => value.Length > 0)
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileNationalIds = next.Select(row => row.Employee.NationalId?.Trim() ?? "").Where(value => value.Length > 0)
            .GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var sourceNumbers = next.Select(row => row.Employee.EmployeeNumber?.Trim().ToUpperInvariant() ?? "").Where(value => value.Length > 0).ToArray();
        var sourceIds = next.Select(row => row.Employee.NationalId?.Trim() ?? "").Where(value => value.Length > 0).ToArray();
        var existing = await db.Employees.AsNoTracking().Where(employee => sourceNumbers.Contains(employee.EmployeeNumber.ToUpper())
            || (employee.NationalId != null && sourceIds.Contains(employee.NationalId)))
            .Select(employee => new { employee.EmployeeNumber, employee.NationalId }).ToArrayAsync(cancellationToken);
        var existingNumbers = existing.Select(item => item.EmployeeNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNationalIds = existing.Where(item => item.NationalId is not null).Select(item => item.NationalId!).ToHashSet(StringComparer.Ordinal);
        var evaluated = new List<EmployeeImportRow>();
        foreach (var row in next)
        {
            var duplicate = fileNumbers.Contains(row.Employee.EmployeeNumber?.Trim() ?? "")
                || fileNationalIds.Contains(row.Employee.NationalId?.Trim() ?? "");
            var existingEmployee = existingNumbers.Contains(row.Employee.EmployeeNumber ?? "")
                || existingNationalIds.Contains(row.Employee.NationalId ?? "");
            evaluated.Add(await EvaluateRowAsync(row.Row, row.Employee, lookups.Departments, lookups.Positions, lookups.Organizations,
                [], [], row.SourceDepartment ?? "", row.SourcePosition ?? "", row.Organization ?? "",
                duplicate, existingEmployee, cancellationToken));
        }
        return await SavePreviewAsync(id, evaluated, cancellationToken);
    }

    public async Task<EmployeeImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null)
    {
        await OwnedUpload(id, cancellationToken);
        // The advisory transaction lock serializes confirmations of this batch, including retries.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", cancellationToken);
        var completed = await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportCompleted", cancellationToken);
        if (completed is not null) return Read<EmployeeImportResult>(completed.NewValue);
        var events = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportPreviewed").OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault();
        if (saved is null || saved.PreviewId != previewId)
            throw new HrValidationException("Build and review the employee preview before confirming.");
        await using var stream = await storage.OpenReadAsync(saved.StorageKey, cancellationToken);
        var preview = await JsonSerializer.DeserializeAsync<EmployeeImportPreview>(stream, Json, cancellationToken)
            ?? throw new HrValidationException("The employee preview could not be read.");
        var imported = 0; var skipped = 0; var failed = 0;
        var excluded = (excludedRows ?? []).ToHashSet();
        foreach (var row in preview.Rows)
        {
            if (excluded.Contains(row.Row) || row.Status is not ("Ready" or "Warning")) { skipped++; continue; }
            await transaction.CreateSavepointAsync("employee_row", cancellationToken);
            try { await creation.CreateAsync(row.Employee, cancellationToken); imported++; }
            catch (Exception error) when (error is HrException or ArgumentException or DbUpdateException)
            {
                await transaction.RollbackToSavepointAsync("employee_row", cancellationToken);
                if (error is HrConflictException || error is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }) skipped++;
                else failed++;
            }
            finally { db.ChangeTracker.Clear(); }
            await transaction.ReleaseSavepointAsync("employee_row", cancellationToken);
        }
        var result = new EmployeeImportResult(imported, skipped, failed);
        await Write(id, "EmployeeImportCompleted", result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyCollection<EmployeeImportHistory>> HistoryAsync(CancellationToken cancellationToken)
    {
        var uploads = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.Action == "EmployeeImportUploaded" && log.UserId == user.UserId)
            .OrderByDescending(log => log.Timestamp).Take(50).Select(log => new { log.EntityId, log.NewValue, log.Timestamp, UserName = log.User == null ? "" : log.User.Username }).ToArrayAsync(cancellationToken);
        var ids = uploads.Select(item => item.EntityId).ToArray();
        var events = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && ids.Contains(log.EntityId) && (log.Action == "EmployeeImportCompleted" || log.Action == "EmployeeImportPreviewed"))
            .OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        return uploads.Select(item => new EmployeeImportHistory(item.EntityId, Read<Uploaded>(item.NewValue).FileName, item.UserName, item.Timestamp,
            events.Where(log => log.EntityId == item.EntityId && log.Action == "EmployeeImportPreviewed").Select(log => (int?)Read<PreviewSaved>(log.NewValue).TotalRows).FirstOrDefault(),
            events.Where(log => log.EntityId == item.EntityId && log.Action == "EmployeeImportCompleted").Select(log => Read<EmployeeImportResult>(log.NewValue)).FirstOrDefault())).ToArray();
    }

    public async Task DeleteHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var events = await db.HrAuditLogs
            .Where(log => log.EntityType == Entity && log.EntityId == id)
            .ToListAsync(cancellationToken);
        if (events.Count == 0 || events.All(log => log.Action != "EmployeeImportUploaded" || log.UserId != user.UserId))
            throw new HrNotFoundException("Employee import was not found.");

        var storageKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in events)
        {
            try
            {
                if (item.Action == "EmployeeImportUploaded")
                    storageKeys.Add(Read<Uploaded>(item.NewValue).StorageKey);
                else if (item.Action == "EmployeeImportPreviewed")
                    storageKeys.Add(Read<PreviewSaved>(item.NewValue).StorageKey);
            }
            catch (HrValidationException)
            {
                // Ignore malformed metadata so the history row can still be removed.
            }
        }

        db.HrAuditLogs.RemoveRange(events);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var key in storageKeys.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch { /* History removal should not fail because a stored file is already gone. */ }
        }
    }

    private Task Write(Guid id, string action, object value, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditWriteRequest(action, Entity, id.ToString(), null, null, value, action), cancellationToken);
    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");
    private static EmployeeImportPreview Localize(EmployeeImportPreview preview) => preview with
    { Rows = preview.Rows.Select(row => row with { Errors = ApiTextLocalizer.LocalizeErrors(row.Errors) }).ToArray() };

    private async Task<(EmployeeImportMapper.Lookup[] Departments, EmployeeImportMapper.Lookup[] Positions, EmployeeImportMapper.Lookup[] Organizations)> LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var departments = await db.Departments.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new EmployeeImportMapper.Lookup(item.Id, item.Name, item.Code, item.NameArabic))
            .ToArrayAsync(cancellationToken);
        var organizations = await db.CollectionClientOrganizations.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new EmployeeImportMapper.Lookup(item.Id, item.NameEnglish, item.Code, item.NameArabic))
            .ToArrayAsync(cancellationToken);
        var positions = await db.Positions.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new EmployeeImportMapper.Lookup(item.Id, item.Name, item.Code, item.NameArabic, item.DepartmentId))
            .ToArrayAsync(cancellationToken);
        return (departments, positions, organizations);
    }

    private async Task<EmployeeImportPreview> LoadPreviewAsync(Guid id, Guid previewId, CancellationToken cancellationToken)
    {
        var events = await db.HrAuditLogs.AsNoTracking()
            .Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportPreviewed")
            .OrderByDescending(log => log.Timestamp)
            .ToArrayAsync(cancellationToken);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault(item => item.PreviewId == previewId)
            ?? throw new HrValidationException("Build and review the employee preview before confirming.");
        await using var stream = await storage.OpenReadAsync(saved.StorageKey, cancellationToken);
        return await JsonSerializer.DeserializeAsync<EmployeeImportPreview>(stream, Json, cancellationToken)
            ?? throw new HrValidationException("The employee preview could not be read.");
    }

    private async Task<EmployeeImportPreview> SavePreviewAsync(Guid id, IReadOnlyCollection<EmployeeImportRow> rows, CancellationToken cancellationToken)
    {
        var preview = new EmployeeImportPreview(id, Guid.NewGuid(), rows);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(preview, Json));
        var stored = await storage.SaveAsync("employee-import-previews", "preview.json", "application/json", content, MaximumBytes, cancellationToken);
        try { await Write(id, "EmployeeImportPreviewed", new PreviewSaved(preview.PreviewId, stored.StorageKey, rows.Count), cancellationToken); }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return Localize(preview);
    }

    private async Task<EmployeeImportRow> EvaluateRowAsync(
        int rowNumber,
        SaveEmployeeRequest request,
        IReadOnlyCollection<EmployeeImportMapper.Lookup> departments,
        IReadOnlyCollection<EmployeeImportMapper.Lookup> positions,
        IReadOnlyCollection<EmployeeImportMapper.Lookup> organizations,
        List<string> mappingErrors,
        List<string> mappingWarnings,
        string sourceDepartment,
        string sourcePosition,
        string sourceOrganization,
        bool fileDuplicate,
        bool existingEmployee,
        CancellationToken cancellationToken)
    {
        var errors = mappingErrors.ToList();
        var warnings = mappingWarnings.ToList();
        var status = fileDuplicate || existingEmployee ? "Existing" : "Ready";
        if (fileDuplicate) errors.Add("Duplicate employee within this file; skipped.");
        else if (existingEmployee) errors.Add("Existing employee; skipped.");
        if (status != "Existing")
        {
            try { await creation.ValidateAsync(request, cancellationToken); }
            catch (HrConflictException) { status = "Existing"; errors.Add("Existing employee; skipped."); }
            catch (HrValidationException error) { errors.AddRange(error.Errors.Count > 0 ? error.Errors : [error.Message]); }
        }
        if (status != "Existing" && errors.Count > 0) status = "Error";
        else if (status == "Ready" && warnings.Count > 0) status = "Warning";
        if (status == "Ready" && request.WorkEndDate.HasValue && request.Status is null)
        {
            status = "Warning";
            warnings.Add("End work date is recorded; employee status is unchanged, as in manual creation.");
        }
        var resolvedDepartment = departments.SingleOrDefault(item => item.Id == request.DepartmentId)?.Name ?? sourceDepartment;
        var resolvedPosition = positions.SingleOrDefault(item => item.Id == request.PositionId)?.Name ?? sourcePosition;
        var resolvedOrganization = string.Join(" · ", organizations.Where(item => request.OrganizationIds.Contains(item.Id)).Select(item => item.Name));
        return new EmployeeImportRow(rowNumber, request, resolvedDepartment, resolvedPosition, status,
            errors.Concat(warnings).Distinct(StringComparer.Ordinal).ToArray(),
            sourceDepartment, sourcePosition,
            resolvedOrganization.Length > 0 ? resolvedOrganization : sourceOrganization);
    }
}
