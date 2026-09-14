using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MIS.Application.Interfaces;
using MIS.Infrastructure.Authentication;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Persistence.Repositories;
using MIS.Infrastructure.Services;
using MIS.Infrastructure.Files;

namespace MIS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<HrFileStorageOptions>(configuration.GetSection(HrFileStorageOptions.SectionName));
        ValidateJwtOptions(configuration);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IHrDashboardRepository, HrDashboardRepository>();
        services.AddScoped<IHrEmployeeRepository, HrEmployeeRepository>();
        services.AddScoped<IEmployeeCreationService, EmployeeCreationService>();
        services.AddScoped<IEmployeeImportService, EmployeeImportService>();
        services.AddScoped<IHrAbsenceRepository, HrAbsenceRepository>();
        services.AddScoped<IHrTransactionRunner, EfHrTransactionRunner>();
        services.AddScoped<IHrAuditService, HrAuditService>();
        services.AddScoped<IHrMasterDataService, HrMasterDataService>();
        services.AddScoped<IHrEmployeeProfileService, HrEmployeeProfileService>();
        services.AddScoped<IHrEmployeeDocumentService, HrEmployeeDocumentService>();
        services.AddScoped<IEmployeeDocumentBulkService, EmployeeDocumentBulkService>();
        services.AddScoped<IHrDelegationService, HrDelegationService>();
        services.AddScoped<ISocialInsuranceService, SocialInsuranceService>();
        services.AddScoped<ISocialInsuranceImportService, SocialInsuranceImportService>();
        services.AddScoped<IAbsenceImportService, AbsenceImportService>();
        services.AddScoped<IHrLeaveService, HrLeaveService>();
        services.AddScoped<IHrReportService, HrReportService>();
        services.AddScoped<IHrAttendanceService, HrAttendanceService>();
        services.AddScoped<IHrExcuseMissionService, HrExcuseMissionService>();
        services.AddScoped<HrMissionSynchronizer>();
        services.AddScoped<IHrAttendanceImportService, HrAttendanceImportService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<IFinanceService>(serviceProvider => serviceProvider.GetRequiredService<FinanceService>());
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IFinancePostingService>(serviceProvider => serviceProvider.GetRequiredService<FinanceService>());
        services.AddScoped<ICollectionsClassificationContext, CollectionsClassificationContext>();
        services.AddScoped<ICollectionsService, CollectionsService>();
        services.AddScoped<IBanksService, BanksService>();
        services.AddScoped<IBankPortfolioImportService, BankPortfolioImportService>();
        services.AddScoped<IBankPortfolioCaseService, BankPortfolioCaseService>();
        services.AddScoped<IBankCustomerService, BankCustomerService>();
        services.AddScoped<IBankCustomerImportService, BankCustomerImportService>();
        services.AddScoped<IDataEntryService, DataEntryService>();
        services.AddScoped<IBankCaseDistributionService, BankCaseDistributionService>();
        services.AddScoped<IBankDistributionImportService, BankDistributionImportService>();
        services.AddScoped<IBankCaseActivityService, BankCaseActivityService>();
        services.AddScoped<IBankPtpService, BankPtpService>();
        services.AddScoped<IBankVisitService, BankVisitService>();
        services.AddScoped<IBankDcrService, BankDcrService>();
        services.AddScoped<IBankComplaintService, BankComplaintService>();
        services.AddScoped<IBankArchiveService, BankArchiveService>();
        services.AddScoped<ICollectionsImportService, CollectionsImportService>();
        services.AddScoped<ICollectionsAttachmentService, CollectionsAttachmentService>();
        services.AddScoped<ICollectionsReportService, CollectionsReportService>();
        services.AddScoped<ICollectionsBrandingService, CollectionsBrandingService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<HrCalendarService>();
        services.AddScoped<IHrCalendarService>(serviceProvider => serviceProvider.GetRequiredService<HrCalendarService>());
        services.AddScoped<IWorkingCalendarCalculator>(serviceProvider => serviceProvider.GetRequiredService<HrCalendarService>());
        services.AddSingleton<IHrFileStorage, LocalHrFileStorage>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }

    private static void ValidateJwtOptions(IConfiguration configuration)
    {
        var secretKey = configuration[$"{JwtOptions.SectionName}:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < JwtOptions.MinimumSecretBytes)
        {
            throw new InvalidOperationException($"Jwt:SecretKey must be configured with at least {JwtOptions.MinimumSecretBytes} bytes. Use environment variables or user secrets.");
        }
    }
}
