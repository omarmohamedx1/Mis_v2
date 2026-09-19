using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MIS.Application.Common;
using MIS.Application.Interfaces;
using MIS.Application.Services;
using MIS.Infrastructure;
using MIS.Infrastructure.Authentication;
using MIS.API.Authorization;
using MIS.Domain.Constants;
using MIS.API.Authentication;
using MIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MIS.API.Configuration;

public static class ApiServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "FrontendCorsPolicy";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(modelState => modelState.Value?.Errors.Count > 0)
                        .SelectMany(modelState => modelState.Value!.Errors)
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid request value." : error.ErrorMessage)
                        .ToArray();
                    var message = errors.Length == 1 ? errors[0] : "Validation failed.";

                    return new BadRequestObjectResult(ApiErrorResponse.Failure(message, errors));
                };
            });

        var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ar") };
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("en");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.ApplyCurrentCultureToResponseHeaders = true;
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        });

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddInfrastructureServices(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.HrDepartment,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.HrAccess) || context.User.HasClaim("department", DepartmentCodes.Hr)));
            options.AddPolicy(
                AuthorizationPolicies.HrSensitiveData,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.HrSensitiveView) || context.User.IsInRole(SystemRoleNames.HrManager)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsAccess,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    context.User.IsInRole(SystemRoleNames.Admin) ||
                    HasPermission(context.User, SystemPermissionCodes.CollectionsAccess) ||
                    context.User.HasClaim("department", DepartmentCodes.Collections)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsSensitiveData,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.CollectionsSensitiveView) || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager) || context.User.IsInRole(SystemRoleNames.CollectionsSupervisor) || context.User.IsInRole(SystemRoleNames.CollectionsAuditor)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsAssignmentManage,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.CollectionsAssignmentManage) || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager) || context.User.IsInRole(SystemRoleNames.CollectionsSupervisor)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsPaymentApprove,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.CollectionsPaymentApprove) || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager) || context.User.IsInRole(SystemRoleNames.CollectionsReviewer)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsAuditView,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.CollectionsAuditView) || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager) || context.User.IsInRole(SystemRoleNames.CollectionsAuditor)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsImportManage,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.CollectionsImportManage) || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsConfigurationManage,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.CollectionsConfigurationManage) || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager)));
            options.AddPolicy(
                AuthorizationPolicies.CollectionsReportExport,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context => HasPermission(context.User, SystemPermissionCodes.CollectionsReportExport) || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager) || context.User.IsInRole(SystemRoleNames.CollectionsSupervisor) || context.User.IsInRole(SystemRoleNames.CollectionsAuditor)));
            options.AddPolicy(
                AuthorizationPolicies.AdminAccess,
                policy => policy.RequireAuthenticatedUser().RequireRole(SystemRoleNames.Admin));
            options.AddPolicy(AuthorizationPolicies.FinanceAccess, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceAccess) || HasPermission(context.User, "accounting.access") || context.User.HasClaim("department", DepartmentCodes.Accounting)));
            options.AddPolicy(AuthorizationPolicies.FinanceJournalCreate, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceJournalCreate) || HasPermission(context.User, "accounting.transaction.manage")));
            options.AddPolicy(AuthorizationPolicies.FinanceJournalApprove, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceJournalApprove) || HasPermission(context.User, "accounting.approve")));
            options.AddPolicy(AuthorizationPolicies.FinanceJournalPost, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceJournalPost) || HasPermission(context.User, "accounting.approve")));
            options.AddPolicy(AuthorizationPolicies.FinanceReverse, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceTransactionReverse) || HasPermission(context.User, "accounting.approve")));
            options.AddPolicy(AuthorizationPolicies.FinancePeriodClose, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinancePeriodClose) || HasPermission(context.User, "accounting.approve")));
            options.AddPolicy(AuthorizationPolicies.FinanceConfiguration, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceConfigurationManage)));
            options.AddPolicy(AuthorizationPolicies.FinanceAudit, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceAuditView)));
            options.AddPolicy(AuthorizationPolicies.FinanceCollectionReview, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceCollectionReview) || HasPermission(context.User, "accounting.transaction.manage")));
            options.AddPolicy(AuthorizationPolicies.FinanceCustodyView, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceCustodyView) || HasPermission(context.User, "accounting.access")));
            options.AddPolicy(AuthorizationPolicies.FinanceCustodyReconcile, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole(SystemRoleNames.Admin) || HasPermission(context.User, SystemPermissionCodes.FinanceCustodyReconcile) || HasPermission(context.User, "accounting.approve")));
            options.AddPolicy(AuthorizationPolicies.AccountingAccess, policy => policy.RequireAuthenticatedUser().RequireAssertion(AccountingUser));
            options.AddPolicy(AuthorizationPolicies.AccountingPayrollManage, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => AccountingUser(context) || HasPermission(context.User, SystemPermissionCodes.AccountingPayrollManage)));
            options.AddPolicy(AuthorizationPolicies.AccountingPayrollApprove, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => AccountingUser(context) || HasPermission(context.User, SystemPermissionCodes.AccountingPayrollApprove) || HasPermission(context.User, "accounting.approve")));
            options.AddPolicy(AuthorizationPolicies.AccountingTransportationManage, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => AccountingUser(context) || HasPermission(context.User, SystemPermissionCodes.AccountingTransportationManage)));
            options.AddPolicy(AuthorizationPolicies.AccountingCommissionManage, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => AccountingUser(context) || HasPermission(context.User, SystemPermissionCodes.AccountingCommissionManage)));
            options.AddPolicy(AuthorizationPolicies.AccountingCommissionApprove, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => AccountingUser(context) || HasPermission(context.User, SystemPermissionCodes.AccountingCommissionApprove) || HasPermission(context.User, "accounting.approve")));
            options.AddPolicy(AuthorizationPolicies.DataEntryAccess, policy => policy.RequireAuthenticatedUser().RequireAssertion(DataEntryUser));
            options.AddPolicy(AuthorizationPolicies.DataEntryManage, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                DataEntryUser(context)
                || HasPermission(context.User, SystemPermissionCodes.DataEntryManage)
                || HasPermission(context.User, SystemPermissionCodes.DataEntryAccess)
                || context.User.IsInRole(SystemRoleNames.DataEntry)));
            options.AddPolicy(AuthorizationPolicies.DataEntryBatchReview, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                context.User.IsInRole(SystemRoleNames.Admin)
                || HasPermission(context.User, SystemPermissionCodes.DataEntryBatchReview)
                || context.User.IsInRole(SystemRoleNames.CollectionsSupervisor)
                || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager)));
            options.AddPolicy(AuthorizationPolicies.LegalAccess, policy => policy.RequireAuthenticatedUser().RequireAssertion(LegalUser));
            options.AddPolicy(AuthorizationPolicies.LegalCaseManage, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                LegalUser(context)
                || HasPermission(context.User, SystemPermissionCodes.LegalCaseManage)
                || context.User.IsInRole(SystemRoleNames.LegalOfficer)));
        });

        return services;
    }

    private static bool AccountingUser(AuthorizationHandlerContext context) =>
        context.User.IsInRole(SystemRoleNames.Admin)
        || HasPermission(context.User, SystemPermissionCodes.AccountingAccess)
        || HasPermission(context.User, "accounting.access")
        || HasPermission(context.User, SystemPermissionCodes.FinanceAccess)
        || context.User.HasClaim("department", DepartmentCodes.Accounting);

    private static bool LegalUser(AuthorizationHandlerContext context) =>
        context.User.IsInRole(SystemRoleNames.Admin)
        || HasPermission(context.User, SystemPermissionCodes.LegalAccess)
        || HasPermission(context.User, SystemPermissionCodes.LegalCaseManage)
        || context.User.IsInRole(SystemRoleNames.LegalOfficer)
        || context.User.HasClaim("department", DepartmentCodes.Legal);

    private static bool DataEntryUser(AuthorizationHandlerContext context) =>
        context.User.IsInRole(SystemRoleNames.Admin)
        || HasPermission(context.User, SystemPermissionCodes.DataEntryAccess)
        || HasPermission(context.User, SystemPermissionCodes.DataEntryManage)
        || context.User.IsInRole(SystemRoleNames.DataEntry)
        || context.User.HasClaim("department", DepartmentCodes.DataEntry)
        || HasPermission(context.User, SystemPermissionCodes.DataEntryBatchReview)
        || context.User.IsInRole(SystemRoleNames.CollectionsSupervisor)
        || context.User.IsInRole(SystemRoleNames.CollectionsOperationsManager);

    private static bool HasPermission(System.Security.Claims.ClaimsPrincipal user, string permission) =>
        user.HasClaim(SystemPermissionCodes.ClaimType, "*") || user.HasClaim(SystemPermissionCodes.ClaimType, permission);

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSigningKeyAccessor>((options, signingKey) =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    IssuerSigningKeyResolver = (_, _, _, _) => [signingKey.SecurityKey]
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (IsAnonymousAuthPath(context.Request.Path))
                            context.NoResult();
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var versionValue = context.Principal?.FindFirst("access_version")?.Value;
                        if (!Guid.TryParse(userIdValue, out var userId) || !int.TryParse(versionValue, out var accessVersion))
                        {
                            context.Fail("The access token is no longer valid.");
                            return;
                        }
                        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                        var account = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.IsActive, x.AccessVersion }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                        if (account is null || !account.IsActive || account.AccessVersion != accessVersion)
                            context.Fail("The account or its access has changed. Sign in again.");
                    },
                    OnChallenge = async context =>
                    {
                        if (IsAnonymousAuthPath(context.Request.Path)
                            || context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                        {
                            return;
                        }

                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure("Authentication is required."));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(ApiErrorResponse.Failure("You are not authorized to access this resource."));
                    }
                };
            });

        return services;
    }

    private static bool IsAnonymousAuthPath(PathString path) =>
        path.StartsWithSegments("/api/auth/login");
}
