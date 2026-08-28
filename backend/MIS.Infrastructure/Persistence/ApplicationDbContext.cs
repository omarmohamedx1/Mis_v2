using Microsoft.EntityFrameworkCore;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserAccessGrant> UserAccessGrants => Set<UserAccessGrant>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<ContractType> ContractTypes => Set<ContractType>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DelegationType> DelegationTypes => Set<DelegationType>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeContract> EmployeeContracts => Set<EmployeeContract>();
    public DbSet<EmployeeCompensation> EmployeeCompensations => Set<EmployeeCompensation>();
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts => Set<EmployeeEmergencyContact>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeAbsence> EmployeeAbsences => Set<EmployeeAbsence>();
    public DbSet<HrAuditLog> HrAuditLogs => Set<HrAuditLog>();
    public DbSet<WorkingCalendar> WorkingCalendars => Set<WorkingCalendar>();
    public DbSet<WorkingDaySetting> WorkingDaySettings => Set<WorkingDaySetting>();
    public DbSet<CalendarException> CalendarExceptions => Set<CalendarException>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendancePunch> AttendancePunches => Set<AttendancePunch>();
    public DbSet<AttendanceImportBatch> AttendanceImportBatches => Set<AttendanceImportBatch>();
    public DbSet<AttendanceImportRow> AttendanceImportRows => Set<AttendanceImportRow>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<EmployeeLeaveEntitlement> EmployeeLeaveEntitlements => Set<EmployeeLeaveEntitlement>();
    public DbSet<EmployeeDelegation> EmployeeDelegations => Set<EmployeeDelegation>();
    public DbSet<ClientOrganization> CollectionClientOrganizations => Set<ClientOrganization>();
    public DbSet<CollectionPortfolio> CollectionPortfolios => Set<CollectionPortfolio>();
    public DbSet<CollectionCustomer> CollectionCustomers => Set<CollectionCustomer>();
    public DbSet<DelinquencyBucketDefinition> CollectionBucketDefinitions => Set<DelinquencyBucketDefinition>();
    public DbSet<CollectionTeam> CollectionTeams => Set<CollectionTeam>();
    public DbSet<CollectionTeamMember> CollectionTeamMembers => Set<CollectionTeamMember>();
    public DbSet<CollectionUserAccess> CollectionUserAccess => Set<CollectionUserAccess>();
    public DbSet<CollectionCase> CollectionCases => Set<CollectionCase>();
    public DbSet<CaseBucketHistory> CollectionCaseBucketHistory => Set<CaseBucketHistory>();
    public DbSet<CollectionAssignmentHistory> CollectionAssignmentHistory => Set<CollectionAssignmentHistory>();
    public DbSet<CollectionActivity> CollectionActivities => Set<CollectionActivity>();
    public DbSet<CollectionDcr> CollectionDcrs => Set<CollectionDcr>();
    public DbSet<PromiseToPay> CollectionPromisesToPay => Set<PromiseToPay>();
    public DbSet<CollectionPayment> CollectionPayments => Set<CollectionPayment>();
    public DbSet<FieldVisit> CollectionFieldVisits => Set<FieldVisit>();
    public DbSet<CollectionComplaint> CollectionComplaints => Set<CollectionComplaint>();
    public DbSet<CollectionComplaintNote> CollectionComplaintNotes => Set<CollectionComplaintNote>();
    public DbSet<CollectionAuditLog> CollectionAuditLogs => Set<CollectionAuditLog>();
    public DbSet<CollectionImportBatch> CollectionImportBatches => Set<CollectionImportBatch>();
    public DbSet<CollectionImportRow> CollectionImportRows => Set<CollectionImportRow>();
    public DbSet<CollectionAttachment> CollectionAttachments => Set<CollectionAttachment>();
    public DbSet<BankPortfolioImport> BankPortfolioImports => Set<BankPortfolioImport>();
    public DbSet<FinanceLegalEntity> FinanceLegalEntities => Set<FinanceLegalEntity>();
    public DbSet<FinanceCurrency> FinanceCurrencies => Set<FinanceCurrency>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<AccountingEvent> AccountingEvents => Set<AccountingEvent>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<ClientLedgerEntry> ClientLedgerEntries => Set<ClientLedgerEntry>();
    public DbSet<FinancialAuditLog> FinancialAuditLogs => Set<FinancialAuditLog>();
    public DbSet<CollectionFinancialReceipt> CollectionFinancialReceipts => Set<CollectionFinancialReceipt>();
    public DbSet<CollectionPaymentAllocation> CollectionPaymentAllocations => Set<CollectionPaymentAllocation>();
    public DbSet<CollectorCustodyAccount> CollectorCustodyAccounts => Set<CollectorCustodyAccount>();
    public DbSet<CollectorCustodyTransaction> CollectorCustodyTransactions => Set<CollectorCustodyTransaction>();
    public DbSet<CollectionClearingEvent> CollectionClearingEvents => Set<CollectionClearingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
