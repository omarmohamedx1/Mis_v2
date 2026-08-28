using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MIS.Domain.Constants;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Seed;

public static class ApplicationDbSeeder
{
    public static async Task SeedDevelopmentDataAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MIS.Seed");
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();

        var now = DateTimeOffset.UtcNow;
        var departments = new[]
        {
            ("Human Resources", "الموارد البشرية", DepartmentCodes.Hr),
            ("Legal", "الشؤون القانونية", DepartmentCodes.Legal),
            ("Administration", "الإدارة", DepartmentCodes.Admin),
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
        await SeedHrMasterDataAsync(dbContext, hrDepartment.Id, now);
        await SeedCollectionsMasterDataAsync(dbContext, now);
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

        var collectionsRoles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, description) in new[]
        {
            (SystemRoleNames.CollectionsCollector, "Collector with access to assigned collection cases"),
            (SystemRoleNames.CollectionsSupervisor, "Collections supervisor with team assignment and sensitive-data access"),
            (SystemRoleNames.CollectionsReviewer, "Independent collection payment reviewer"),
            (SystemRoleNames.CollectionsOperationsManager, "Collections operations manager"),
            (SystemRoleNames.CollectionsClientViewer, "Restricted client portfolio viewer"),
            (SystemRoleNames.CollectionsAuditor, "Read-only collections audit and compliance user")
        })
        {
            var role = await dbContext.Roles.SingleOrDefaultAsync(x => x.Name == name);
            if (role is null)
            {
                role = new Role(name, description, true, now);
                dbContext.Roles.Add(role);
            }
            collectionsRoles[name] = role;
        }

        var adminPassword = configuration["Seed:AdminPassword"];
        var username = configuration["Seed:AdminUsername"] ?? "admin";
        var email = configuration["Seed:AdminEmail"] ?? "admin@mis.local";
        var fullName = configuration["Seed:AdminFullName"] ?? "MIS Administrator";

        var adminUser = await dbContext.Users
            .Include(user => user.UserRoles)
            .SingleOrDefaultAsync(user => user.Username == username);

        if (adminUser is null && !string.IsNullOrWhiteSpace(adminPassword))
        {
            adminUser = new User(username, email, "temporary-seed-hash", fullName, adminDepartment.Id, now);
            adminUser.SetPasswordHash(new PasswordHasher<User>().HashPassword(adminUser, adminPassword), now);
            dbContext.Users.Add(adminUser);
        }

        if (adminUser is not null)
        {
            adminUser.AssignRole(adminRole, now);
        }

        var hrPassword = configuration["Seed:HrPassword"];
        var hrUsername = configuration["Seed:HrUsername"] ?? "hr.user";
        var hrUser = await dbContext.Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Username == hrUsername);
        if (hrUser is null && !string.IsNullOrWhiteSpace(hrPassword))
        {
            hrUser = new User(
                hrUsername,
                configuration["Seed:HrEmail"] ?? "hr@mis.local",
                "temporary-seed-hash",
                configuration["Seed:HrFullName"] ?? "HR User",
                hrDepartment.Id,
                now);
            hrUser.SetPasswordHash(new PasswordHasher<User>().HashPassword(hrUser, hrPassword), now);
            dbContext.Users.Add(hrUser);
        }

        if (hrUser is not null)
        {
            var configuredRole = configuration["Seed:HrRole"];
            hrUser.AssignRole(
                string.Equals(configuredRole, SystemRoleNames.HrOfficer, StringComparison.OrdinalIgnoreCase)
                    ? hrOfficerRole
                    : hrManagerRole,
                now);
        }

        var collectionsPassword = configuration["Seed:CollectionsPassword"];
        var collectionsUsername = configuration["Seed:CollectionsUsername"] ?? "collections.user";
        var collectionsUser = await dbContext.Users.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.Username == collectionsUsername);
        if (collectionsUser is null && !string.IsNullOrWhiteSpace(collectionsPassword))
        {
            collectionsUser = new User(
                collectionsUsername,
                configuration["Seed:CollectionsEmail"] ?? "collections@mis.local",
                "temporary-seed-hash",
                configuration["Seed:CollectionsFullName"] ?? "Collections User",
                collectionsDepartment.Id,
                now);
            collectionsUser.SetPasswordHash(new PasswordHasher<User>().HashPassword(collectionsUser, collectionsPassword), now);
            dbContext.Users.Add(collectionsUser);
        }
        if (collectionsUser is not null)
        {
            var configured = configuration["Seed:CollectionsRole"] ?? SystemRoleNames.CollectionsOperationsManager;
            collectionsUser.AssignRole(collectionsRoles.GetValueOrDefault(configured) ?? collectionsRoles[SystemRoleNames.CollectionsOperationsManager], now);
        }

        await SeedProvisionedUserDirectoryAsync(dbContext, now);

        if (string.IsNullOrWhiteSpace(adminPassword) && string.IsNullOrWhiteSpace(hrPassword) && string.IsNullOrWhiteSpace(collectionsPassword))
        {
            logger.LogWarning("No development users were seeded because seed passwords are not configured.");
        }

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
            if (await dbContext.Users.AnyAsync(x => x.Username == username)) continue;
            var user = new User(username, $"{username.Replace('.', '_')}@mis.local", "PROVISIONED-NO-LOGIN", fullName, departmentIds[departmentCode], now);
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
        await Propose("samah", "legal.access", "DEPARTMENT", null, "Proposed from the approved initial legal directory; module is planned.");

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

    private static async Task SeedHrMasterDataAsync(ApplicationDbContext dbContext, Guid hrDepartmentId, DateTimeOffset now)
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

        var positions = new[]
        {
            ("HR Manager", "مدير الموارد البشرية", "HR_MANAGER"),
            ("HR Officer", "مسؤول موارد بشرية", "HR_OFFICER")
        };
        foreach (var (name, nameArabic, code) in positions)
        {
            var item = await dbContext.Positions.SingleOrDefaultAsync(value => value.Code == code);
            if (item is null)
                dbContext.Positions.Add(new Position(name, code, nameArabic, null, hrDepartmentId, true, now));
            else if (string.IsNullOrWhiteSpace(item.NameArabic))
                item.Update(item.Name, item.Code, nameArabic, item.Description, item.DepartmentId, item.IsActive, now);
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
