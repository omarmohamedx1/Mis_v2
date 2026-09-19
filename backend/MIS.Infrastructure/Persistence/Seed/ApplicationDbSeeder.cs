using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;

namespace MIS.Infrastructure.Persistence.Seed;

public static class ApplicationDbSeeder
{
    public static async Task SeedDevelopmentDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();

        var now = DateTimeOffset.UtcNow;
        var departments = new[]
        {
            ("Human Resources", "الموارد البشرية", DepartmentCodes.Hr),
            ("Legal", "الشؤون القانونية", DepartmentCodes.Legal),
            ("Administration", "الإدارة", DepartmentCodes.Admin),
            ("Office", "الأوفيس", DepartmentCodes.Office),
            ("Data Entry", "إدخال البيانات", DepartmentCodes.DataEntry),
            ("Accounting", "الحسابات", DepartmentCodes.Accounting)
        };

        foreach (var (name, nameArabic, code) in departments)
        {
            var department = await dbContext.Departments.SingleOrDefaultAsync(x => x.Code == code);
            if (department is null)
            {
                dbContext.Departments.Add(new Department(name, code, nameArabic, null, true, now));
            }
            else if (string.IsNullOrWhiteSpace(department.NameArabic))
                department.Update(department.Name, department.Code, nameArabic, department.Description, department.IsActive, now);
        }

        if (!await dbContext.Departments.AnyAsync(x => x.Code == DepartmentCodes.Collections))
            dbContext.Departments.Add(new Department("Collections", DepartmentCodes.Collections, "التحصيل", null, true, now));

        await dbContext.SaveChangesAsync();
        var adminDepartment = await dbContext.Departments.SingleAsync(x => x.Code == DepartmentCodes.Admin);
        var hrDepartment = await dbContext.Departments.SingleAsync(x => x.Code == DepartmentCodes.Hr);
        var collectionsDepartment = await dbContext.Departments.SingleAsync(x => x.Code == DepartmentCodes.Collections);
        var accountingDepartment = await dbContext.Departments.SingleAsync(x => x.Code == DepartmentCodes.Accounting);
        var dataEntryDepartment = await dbContext.Departments.SingleAsync(x => x.Code == DepartmentCodes.DataEntry);
        var officeDepartment = await dbContext.Departments.SingleAsync(x => x.Code == DepartmentCodes.Office);
        await SeedHrMasterDataAsync(dbContext, hrDepartment.Id, adminDepartment.Id, officeDepartment.Id, accountingDepartment.Id,
            collectionsDepartment.Id, dataEntryDepartment.Id, now);
        await SeedCollectionsMasterDataAsync(dbContext, now);
        await RepairEmployeeDirectoryAsync(dbContext, now);
        var adminRole = await dbContext.Roles.SingleOrDefaultAsync(role => role.Name == SystemRoleNames.Admin);
        var hrManagerRole = await dbContext.Roles.SingleOrDefaultAsync(role => role.Name == SystemRoleNames.HrManager);
        var hrOfficerRole = await dbContext.Roles.SingleOrDefaultAsync(role => role.Name == SystemRoleNames.HrOfficer);

        if (adminRole is null)
        {
            adminRole = new Role(SystemRoleNames.Admin, "System administrator role", true, now);
            dbContext.Roles.Add(adminRole);
        }
        if (hrManagerRole is null)
        {
            hrManagerRole = new Role(SystemRoleNames.HrManager, "HR manager with access to restricted compensation data", true, now);
            dbContext.Roles.Add(hrManagerRole);
        }
        if (hrOfficerRole is null)
        {
            hrOfficerRole = new Role(SystemRoleNames.HrOfficer, "HR operations user without restricted compensation access", true, now);
            dbContext.Roles.Add(hrOfficerRole);
        }

        foreach (var (name, description) in new[]
        {
            (SystemRoleNames.CollectionsCollector, "Collector with access to assigned collection cases"),
            (SystemRoleNames.CollectionsSupervisor, "Collections supervisor with team assignment and sensitive-data access"),
            (SystemRoleNames.CollectionsReviewer, "Independent collection payment reviewer"),
            (SystemRoleNames.CollectionsOperationsManager, "Collections operations manager"),
            (SystemRoleNames.CollectionsClientViewer, "Restricted client portfolio viewer"),
            (SystemRoleNames.CollectionsAuditor, "Read-only collections audit and compliance user"),
            (SystemRoleNames.DataEntry, "Data entry operator for client capture and batch submission"),
            (SystemRoleNames.LegalOfficer, "Legal officer with firm-wide access to collection cases referred to legal")
        })
        {
            if (await dbContext.Roles.AnyAsync(x => x.Name == name)) continue;
            dbContext.Roles.Add(new Role(name, description, true, now));
        }

        await SeedProvisionedUserDirectoryAsync(dbContext, now);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedProvisionedUserDirectoryAsync(ApplicationDbContext dbContext, DateTimeOffset now)
    {
        var departmentIds = await dbContext.Departments.ToDictionaryAsync(x => x.Code, x => x.Id);
        var directory = new[]
        {
            ("ola", "Ola", DepartmentCodes.Accounting), ("ayman", "Ayman", DepartmentCodes.Accounting),
            ("manar", "Manar", DepartmentCodes.Admin), ("ahmed", "Ahmed", DepartmentCodes.Admin), ("duaa", "Duaa", DepartmentCodes.Admin),
            ("fatma", "Fatma", DepartmentCodes.Hr), ("samah", "Samah", DepartmentCodes.Legal),
            ("islam", "Islam", DepartmentCodes.Collections), ("marwan", "Marwan", DepartmentCodes.Collections),
            ("mohamed", "Mohamed", DepartmentCodes.Collections), ("yousef", "Yousef", DepartmentCodes.Collections),
            ("omar", "Omar", DepartmentCodes.Collections), ("tamer", "Tamer", DepartmentCodes.Collections),
            ("malak", "Malak", DepartmentCodes.Collections), ("mohamed.said", "Mohamed Said", DepartmentCodes.Collections),
            ("rahma", "Rahma", DepartmentCodes.Collections), ("hussein", "Hussein", DepartmentCodes.Collections),
            ("ziad", "Ziad", DepartmentCodes.Collections), ("eman", "Eman", DepartmentCodes.Collections), ("nadia", "Nadia", DepartmentCodes.Collections)
        };
        foreach (var (username, fullName, departmentCode) in directory)
        {
            var email = $"{username.Replace('.', '_')}@mis.local";
            if (await dbContext.Users.AnyAsync(x => x.Username == username || x.Email == email)) continue;
            var user = new User(username, email, "PROVISIONED-NO-LOGIN", fullName, departmentIds[departmentCode], now);
            user.SetActive(false, now);
            dbContext.Users.Add(user);
        }
        await dbContext.SaveChangesAsync();

        var users = await dbContext.Users.Where(x => directory.Select(d => d.Item1).Contains(x.Username)).ToDictionaryAsync(x => x.Username, x => x.Id);
        async Task Propose(string username, string permission, string scope, Guid? clientId, string reason)
        {
            if (!users.TryGetValue(username, out var userId)) return;
            if (await dbContext.UserAccessGrants.AnyAsync(x => x.UserId == userId && x.PermissionCode == permission && x.ScopeType == scope && x.ClientOrganizationId == clientId && x.Status == "PENDING")) return;
            dbContext.UserAccessGrants.Add(new UserAccessGrant(userId, permission, scope, clientId, "PENDING", reason, Guid.Empty, now, null));
        }

        foreach (var username in new[] { "ola", "ayman" }) await Propose(username, "accounting.access", "DEPARTMENT", null, "Proposed from the approved initial department directory; requires administrator review.");
        foreach (var username in new[] { "manar", "ahmed", "duaa" }) await Propose(username, "admin.dashboard.view", "ALL", null, "Proposed from the approved initial administration directory; does not grant administrator role.");
        await Propose("fatma", "hr.access", "DEPARTMENT", null, "Proposed from the approved initial HR directory; requires administrator review.");
        await Propose("fatma", "data_entry.access", "DEPARTMENT", null, "Proposed from the approved initial data-entry directory; module is planned.");
        await Propose("samah", "legal.access", "DEPARTMENT", null, "Proposed from the approved initial legal directory.");
        await Propose("samah", "legal.case.manage", "DEPARTMENT", null, "Proposed from the approved initial legal directory.");

        var assignments = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ALEXBANK"] = ["ola","ayman","manar","islam","marwan","mohamed","yousef"],
            ["ATTIJARIWAFA"] = ["ola","ayman","manar","omar","tamer","malak","mohamed.said","rahma"],
            ["CAE"] = ["ola","ayman","manar","hussein"], ["QIB"] = ["ola","ayman","manar"],
            ["BDC"] = ["ola","ayman","manar"], ["ELAB"] = ["ola","ayman","manar"],
            ["RAYA"] = ["ola","ayman","manar","ziad"], ["AMAN"] = ["ola","ayman","manar","eman"],
            ["MNT_HALAN"] = ["ola","ayman","manar","mohamed"], ["PREMIUM_CARD"] = ["ola","ayman","manar","hussein","nadia"]
        };
        var clients = await dbContext.CollectionClientOrganizations.ToDictionaryAsync(x => x.Code, x => x.Id);
        foreach (var (code, usernames) in assignments)
            if (clients.TryGetValue(code, out var clientId))
                foreach (var username in usernames)
                    await Propose(username, "collections.access", "CLIENT", clientId, $"Proposed client access from the initial operating directory for {code}; review before activation.");
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedCollectionsMasterDataAsync(ApplicationDbContext dbContext, DateTimeOffset now)
    {
        var organizations = new[]
        {
            ("ALEXBANK", "بنك الإسكندرية", "AlexBank", CollectionsValues.OrganizationTypes.Bank),
            ("ATTIJARIWAFA", "التجاري وفا بنك إيجيبت", "Attijariwafa Bank Egypt", CollectionsValues.OrganizationTypes.Bank),
            ("CAE", "كريدي أجريكول مصر", "Credit Agricole Egypt", CollectionsValues.OrganizationTypes.Bank),
            ("QIB", "بنك قطر الدولي", "QIB", CollectionsValues.OrganizationTypes.Bank),
            ("BDC", "بنك القاهرة", "Banque du Caire", CollectionsValues.OrganizationTypes.Bank),
            ("ELAB", "إيلاب", "ELAB", CollectionsValues.OrganizationTypes.Other),
            ("RAYA", "راية", "Raya", CollectionsValues.OrganizationTypes.ConsumerFinance),
            ("AMAN", "أمان", "Aman", CollectionsValues.OrganizationTypes.ConsumerFinance),
            ("MNT_HALAN", "إم إن تي حالا", "MNT-Halan", CollectionsValues.OrganizationTypes.ConsumerFinance),
            ("PREMIUM_CARD", "بريميوم كارد", "Premium Card", CollectionsValues.OrganizationTypes.ConsumerFinance)
        };
        foreach (var (code, ar, en, type) in organizations)
            if (!await dbContext.CollectionClientOrganizations.AnyAsync(x => x.Code == code))
                dbContext.CollectionClientOrganizations.Add(new ClientOrganization(code, ar, en, type, now));
        await dbContext.SaveChangesAsync();

        foreach (var organization in await dbContext.CollectionClientOrganizations.ToArrayAsync())
        {
            if (!await dbContext.CollectionPortfolios.AnyAsync(x => x.OrganizationId == organization.Id && x.Code == "MAIN"))
                dbContext.CollectionPortfolios.Add(new CollectionPortfolio(organization.Id, "MAIN", "المحفظة الرئيسية", "Main Portfolio", "EGP", now));
            if (await dbContext.CollectionBucketDefinitions.AnyAsync(x => x.OrganizationId == organization.Id)) continue;
            var buckets = new[]
            {
                ("CURRENT", "منتظم", "Current", (int?)0, (int?)0), ("1_29", "من 1 إلى 29", "1-29", 1, 29),
                ("30_59", "من 30 إلى 59", "30-59", 30, 59), ("60_89", "من 60 إلى 89", "60-89", 60, 89),
                ("90_119", "من 90 إلى 119", "90-119", 90, 119), ("120_179", "من 120 إلى 179", "120-179", 120, 179),
                ("180_PLUS", "180 فأكثر", "180+", 180, (int?)null), ("WRITE_OFF", "إعدام", "Write-Off", (int?)null, (int?)null),
                ("LEGAL", "قانوني", "Legal", (int?)null, (int?)null)
            };
            var order = 0;
            foreach (var (code, ar, en, min, max) in buckets)
                dbContext.CollectionBucketDefinitions.Add(new DelinquencyBucketDefinition(organization.Id, null, code, ar, en, min, max, order++, now));
        }
        await dbContext.SaveChangesAsync();
    }

    private static async Task RepairEmployeeDirectoryAsync(ApplicationDbContext dbContext, DateTimeOffset now)
    {
        var departments = await dbContext.Departments.ToListAsync();
        var organizations = await dbContext.CollectionClientOrganizations.ToListAsync();
        var organizationLookups = organizations
            .Select(organization => (organization.Code, organization.NameEnglish, organization.NameArabic))
            .ToList();
        foreach (var fake in departments.Where(department => department.IsActive
            && !DepartmentCodes.IsOperationalUnit(department.Code)
            && (DepartmentCodes.IsLegacyTitleUnit(department.Code)
                || EmployeeDirectoryDepartmentFilter.IsClientNamedDepartment(
                    department.Code, department.Name, department.NameArabic, organizationLookups))))
            fake.Update(fake.Name, fake.Code, fake.NameArabic, fake.Description, false, now);

        var byCode = departments.ToDictionary(department => department.Code, StringComparer.OrdinalIgnoreCase);
        if (!byCode.TryGetValue(DepartmentCodes.Collections, out var collections)
            || !byCode.TryGetValue(DepartmentCodes.Admin, out var administration))
            return;
        byCode.TryGetValue(DepartmentCodes.Office, out var office);
        office ??= administration;
        var assignments = await dbContext.EmployeeOrganizationAssignments.ToListAsync();
        var employees = await dbContext.Employees
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .ToListAsync();

        foreach (var employee in employees)
        {
            var names = EmployeeName.Resolve(employee.FullName, employee.FullNameArabic, employee.FullNameEnglish);
            if (!string.Equals(employee.FullNameArabic, names.Arabic, StringComparison.Ordinal)
                || !string.Equals(employee.FullNameEnglish, names.English, StringComparison.Ordinal))
                employee.RepairLocalizedNames(now);
            var department = employee.Department;
            var position = employee.Position;
            var matchedOrganizations = organizations.Where(organization =>
                HrOrganizationLookup.Mentions(department.Name, organization.Code, organization.NameEnglish, organization.NameArabic)
                || HrOrganizationLookup.Mentions(department.Code, organization.Code, organization.NameEnglish, organization.NameArabic)
                || HrOrganizationLookup.Mentions(department.NameArabic, organization.Code, organization.NameEnglish, organization.NameArabic)
                || (position is not null && (
                    HrOrganizationLookup.Mentions(position.Code, organization.Code, organization.NameEnglish, organization.NameArabic)
                    || HrOrganizationLookup.Mentions(position.Name, organization.Code, organization.NameEnglish, organization.NameArabic)
                    || HrOrganizationLookup.Mentions(position.NameArabic, organization.Code, organization.NameEnglish, organization.NameArabic))))
                .ToList();

            foreach (var matchedOrganization in matchedOrganizations)
            {
                if (!assignments.Any(assignment => assignment.EmployeeId == employee.Id && assignment.OrganizationId == matchedOrganization.Id))
                {
                    var assignment = new EmployeeOrganizationAssignment(
                        employee.Id,
                        matchedOrganization.Id,
                        isPrimary: assignments.All(existing => existing.EmployeeId != employee.Id),
                        now);
                    dbContext.EmployeeOrganizationAssignments.Add(assignment);
                    assignments.Add(assignment);
                }
            }

            if (matchedOrganizations.Count > 0)
            {
                if (EmployeeDirectoryDepartmentFilter.IsClientNamedDepartment(
                        department.Code, department.Name, department.NameArabic, organizationLookups)
                    && employee.DepartmentId != collections.Id)
                    employee.Update(employee.EmployeeNumber, employee.FullName, collections.Id, employee.IsActive, now);
                continue;
            }

            if (!DepartmentCodes.IsLegacyTitleUnit(department.Code)) continue;
            var target = department.Code.ToUpperInvariant() switch
            {
                "COLLECTION_MANAGER_UNIT" or "LOWER" => collections,
                "OFFICE_GIRL_UNIT" => office,
                _ => administration
            };
            if (employee.DepartmentId != target.Id)
                employee.Update(employee.EmployeeNumber, employee.FullName, target.Id, employee.IsActive, now);
        }

        void EnsureAssignment(Guid employeeId, Guid organizationId)
        {
            if (assignments.Any(assignment => assignment.EmployeeId == employeeId && assignment.OrganizationId == organizationId))
                return;
            var assignment = new EmployeeOrganizationAssignment(
                employeeId,
                organizationId,
                isPrimary: assignments.All(existing => existing.EmployeeId != employeeId),
                now);
            dbContext.EmployeeOrganizationAssignments.Add(assignment);
            assignments.Add(assignment);
        }

        var userEmployees = await dbContext.Users
            .Where(user => user.EmployeeId != null)
            .Select(user => new { user.Id, EmployeeId = user.EmployeeId!.Value })
            .ToListAsync();
        var employeeIdByUserId = userEmployees.ToDictionary(item => item.Id, item => item.EmployeeId);

        foreach (var access in await dbContext.CollectionUserAccess.AsNoTracking().ToListAsync())
        {
            if (employeeIdByUserId.TryGetValue(access.UserId, out var employeeId))
                EnsureAssignment(employeeId, access.OrganizationId);
        }

        foreach (var grant in await dbContext.UserAccessGrants.AsNoTracking()
            .Where(grant => grant.Status == "ACTIVE" && grant.ClientOrganizationId != null)
            .ToListAsync())
        {
            if (employeeIdByUserId.TryGetValue(grant.UserId, out var employeeId))
                EnsureAssignment(employeeId, grant.ClientOrganizationId!.Value);
        }

        var caseAssignments = await (
            from collectionCase in dbContext.CollectionCases.AsNoTracking()
            where !collectionCase.IsArchived && collectionCase.AssignedCollectorId != null
            join user in dbContext.Users.AsNoTracking() on collectionCase.AssignedCollectorId equals user.Id
            where user.EmployeeId != null
            select new { EmployeeId = user.EmployeeId!.Value, collectionCase.Portfolio.OrganizationId }
        ).Distinct().ToListAsync();
        foreach (var link in caseAssignments)
            EnsureAssignment(link.EmployeeId, link.OrganizationId);

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedHrMasterDataAsync(ApplicationDbContext dbContext, Guid hrDepartmentId,
        Guid adminDepartmentId, Guid officeDepartmentId, Guid accountingDepartmentId, Guid collectionsDepartmentId,
        Guid dataEntryDepartmentId, DateTimeOffset now)
    {
        var mainBranch = await dbContext.Branches.SingleOrDefaultAsync(item => item.Code == "MAIN");
        if (mainBranch is null)
            dbContext.Branches.Add(new Branch("Main Branch", "MAIN", "الفرع الرئيسي", null, null, true, now));
        else if (string.IsNullOrWhiteSpace(mainBranch.NameArabic))
            mainBranch.Update(mainBranch.Name, mainBranch.Code, "الفرع الرئيسي", mainBranch.Description, mainBranch.Address, mainBranch.IsActive, now);

        var employmentTypes = new[]
        {
            ("Full Time", "دوام كامل", "FULL_TIME"),
            ("Part Time", "دوام جزئي", "PART_TIME"),
            ("Temporary", "مؤقت", "TEMPORARY"),
            ("Internship", "تدريب", "INTERNSHIP")
        };
        foreach (var (name, nameArabic, code) in employmentTypes)
        {
            var item = await dbContext.EmploymentTypes.SingleOrDefaultAsync(value => value.Code == code);
            if (item is null)
                dbContext.EmploymentTypes.Add(new EmploymentType(name, code, nameArabic, null, true, now));
            else if (string.IsNullOrWhiteSpace(item.NameArabic))
                item.Update(item.Name, item.Code, nameArabic, item.Description, item.IsActive, now);
        }

        var contractTypes = new[]
        {
            ("Permanent", "دائم", "PERMANENT"),
            ("Fixed Term", "محدد المدة", "FIXED_TERM"),
            ("Project Based", "مرتبط بمشروع", "PROJECT_BASED")
        };
        foreach (var (name, nameArabic, code) in contractTypes)
        {
            var item = await dbContext.ContractTypes.SingleOrDefaultAsync(value => value.Code == code);
            if (item is null)
                dbContext.ContractTypes.Add(new ContractType(name, code, nameArabic, null, true, now));
            else if (string.IsNullOrWhiteSpace(item.NameArabic))
                item.Update(item.Name, item.Code, nameArabic, item.Description, item.IsActive, now);
        }

        var leaveTypes = new[]
        {
            ("Annual Leave", "إجازة سنوية", "ANNUAL", 21m, false),
            ("Sick Leave", "إجازة مرضية", "SICK", 0m, true),
            ("Emergency Leave", "إجازة طارئة", "EMERGENCY", 0m, false),
            ("Unpaid Leave", "إجازة بدون راتب", "UNPAID", 0m, false),
            ("Maternity Leave", "إجازة وضع", "MATERNITY", 0m, true),
            ("Permission", "إذن", "PERMISSION", 0m, false),
            ("Early Leave", "انصراف مبكر", "EARLY_LEAVE", 0m, false)
        };
        foreach (var (name, nameArabic, code, entitlement, requiresAttachment) in leaveTypes)
        {
            var item = await dbContext.LeaveTypes.SingleOrDefaultAsync(value => value.Code == code);
            if (item is null)
                dbContext.LeaveTypes.Add(new LeaveType(name, code, nameArabic, null, entitlement, requiresAttachment, true, now));
            else if (string.IsNullOrWhiteSpace(item.NameArabic))
                item.Update(item.Name, item.Code, nameArabic, item.Description, item.DefaultAnnualEntitlement, item.RequiresAttachment, item.IsActive, now);
        }

        var documentTypes = new[]
        {
            ("Criminal Record", "الفيش الجنائي", "CRIMINAL_RECORD", false),
            ("Employment / Appointment Paper", "ورقة التعيين", "EMPLOYMENT_APPOINTMENT_PAPER", false),
            ("Labor Office Registration (Kaab El Amal)", "كعب العمل", "LABOR_OFFICE_REGISTRATION", false),
            ("Birth Certificate", "شهادة الميلاد", "BIRTH_CERTIFICATE", false),
            ("National ID Copy", "صورة البطاقة", "NATIONAL_ID_COPY", false),
            ("Military Exemption / Status", "ورق الإعفاء / موقف التجنيد", "MILITARY_STATUS", false),
            ("National ID", "بطاقة الرقم القومي", "NATIONAL_ID", true),
            ("Contract", "عقد العمل", "CONTRACT", true),
            ("Graduation Certificate", "شهادة التخرج", "GRADUATION_CERTIFICATE", false),
            ("Military Certificate", "شهادة الموقف من التجنيد", "MILITARY_CERTIFICATE", false),
            ("Insurance Document", "مستند التأمينات", "INSURANCE_DOCUMENT", true),
            ("Medical Document", "مستند طبي", "MEDICAL_DOCUMENT", true),
            ("CV", "السيرة الذاتية", "CV", false),
            ("Other", "أخرى", "OTHER", false)
        };
        foreach (var (name, nameArabic, code, requiresExpiry) in documentTypes)
        {
            var item = await dbContext.DocumentTypes.SingleOrDefaultAsync(value => value.Code == code);
            if (item is null)
                dbContext.DocumentTypes.Add(new DocumentType(name, code, nameArabic, null, requiresExpiry, true, now));
            else if (string.IsNullOrWhiteSpace(item.NameArabic))
                item.Update(item.Name, item.Code, nameArabic, item.Description, item.RequiresExpiryDate, item.IsActive, now);
        }

        var delegationTypes = new[]
        {
            ("Cheque Collection", "استلام شيكات", "CHEQUE_COLLECTION"),
            ("Document Collection", "استلام مستندات", "DOCUMENT_COLLECTION"),
            ("Government Procedures", "إجراءات حكومية", "GOVERNMENT_PROCEDURES"),
            ("General Administrative", "تفويض إداري عام", "GENERAL_ADMINISTRATIVE")
        };
        foreach (var (name, nameArabic, code) in delegationTypes)
        {
            var item = await dbContext.DelegationTypes.SingleOrDefaultAsync(value => value.Code == code);
            if (item is null)
                dbContext.DelegationTypes.Add(new DelegationType(name, code, nameArabic, null, true, now));
            else if (string.IsNullOrWhiteSpace(item.NameArabic))
                item.Update(item.Name, item.Code, nameArabic, item.Description, item.IsActive, now);
        }

        var positions = new (string Name, string NameArabic, string Code, Guid? DepartmentId)[]
        {
            ("HR Manager", "مدير الموارد البشرية", "HR_MANAGER", hrDepartmentId),
            ("HR Officer", "مسؤول موارد بشرية", "HR_OFFICER", hrDepartmentId),
            ("Financial & Administrative Director", "المدير المالي والإداري", "FIN_ADMIN_DIRECTOR", null),
            ("Director", "مدير", "DIRECTOR", null),
            ("Collection Manager", "مدير تحصيل", "COLLECTION_MANAGER", null),
            ("Collector", "محصل", "COLLECTOR", null),
            ("Supervisor", "مشرف", "SUPERVISOR", null),
            ("Admin", "إداري", "ADMIN", null),
            ("Accounting", "حسابات", "ACCOUNTING", null),
            ("Data Entry", "إدخال بيانات", "DATA_ENTRY", null),
            ("Office Boy", "عامل خدمات", "OFFICE_BOY", officeDepartmentId),
            ("Office Girl", "عاملة خدمات", "OFFICE_GIRL", officeDepartmentId)
        };
        foreach (var (name, nameArabic, code, departmentId) in positions)
        {
            var item = await dbContext.Positions.SingleOrDefaultAsync(value => value.Code == code);
            if (item is null)
                dbContext.Positions.Add(new Position(name, code, nameArabic, null, departmentId, true, now));
            else if (string.IsNullOrWhiteSpace(item.NameArabic) || item.DepartmentId != departmentId)
                item.Update(item.Name, item.Code, nameArabic, item.Description, departmentId, item.IsActive, now);
        }

        if (!await dbContext.WorkingCalendars.AnyAsync())
        {
            var calendar = new WorkingCalendar("Default company calendar", "Africa/Cairo", now);
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                var isWorkingDay = day is not DayOfWeek.Friday and not DayOfWeek.Saturday;
                calendar.SetDay(
                    day,
                    isWorkingDay,
                    isWorkingDay ? new TimeOnly(9, 0) : null,
                    isWorkingDay ? new TimeOnly(17, 0) : null,
                    isWorkingDay ? 60 : 0,
                    isWorkingDay ? 15 : 0,
                    isWorkingDay ? 15 : 0,
                    isWorkingDay ? 30 : 0,
                    now);
            }

            dbContext.WorkingCalendars.Add(calendar);
        }

        await dbContext.SaveChangesAsync();
    }
}
