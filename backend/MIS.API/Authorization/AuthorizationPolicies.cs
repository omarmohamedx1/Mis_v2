namespace MIS.API.Authorization;

public static class AuthorizationPolicies
{
    public const string HrDepartment = "HrDepartment";
    public const string HrSensitiveData = "HrSensitiveData";
    public const string CollectionsAccess = "CollectionsAccess";
    public const string CollectionsSensitiveData = "CollectionsSensitiveData";
    public const string CollectionsAssignmentManage = "CollectionsAssignmentManage";
    public const string CollectionsPaymentApprove = "CollectionsPaymentApprove";
    public const string CollectionsAuditView = "CollectionsAuditView";
    public const string CollectionsImportManage = "CollectionsImportManage";
    public const string CollectionsConfigurationManage = "CollectionsConfigurationManage";
    public const string CollectionsReportExport = "CollectionsReportExport";
    public const string AdminAccess = "AdminAccess";
    public const string FinanceAccess = "FinanceAccess";
    public const string FinanceJournalCreate = "FinanceJournalCreate";
    public const string FinanceJournalApprove = "FinanceJournalApprove";
    public const string FinanceJournalPost = "FinanceJournalPost";
    public const string FinanceReverse = "FinanceReverse";
    public const string FinancePeriodClose = "FinancePeriodClose";
    public const string FinanceConfiguration = "FinanceConfiguration";
    public const string FinanceAudit = "FinanceAudit";
    public const string FinanceCollectionReview = "FinanceCollectionReview";
    public const string FinanceCustodyView = "FinanceCustodyView";
    public const string FinanceCustodyReconcile = "FinanceCustodyReconcile";
}
