namespace MIS.Domain.Constants;

public static class AccountingBookValues
{
    public static readonly string[] Categories = ["PETTY", "FIXED", "TRANSPORTATION", "ADVANCE", "CARRIED_ADVANCE", "SUPERVISOR_COMMISSION", "COLLECTOR_COMMISSION", "SALARY", "BONUS", "LEGAL", "INCOME", "REVENUE", "ADVANCE_REPAYMENT", "PAYROLL_COMMISSION", "OTHER", "CLAIM_RECEIPT"];
    public static readonly string[] ManualCategories = ["PETTY", "FIXED", "ADVANCE", "CARRIED_ADVANCE", "BONUS", "LEGAL", "INCOME", "REVENUE", "ADVANCE_REPAYMENT", "OTHER"];
    public static string Direction(string category) => category switch
    {
        "CLAIM_RECEIPT" => "CLAIM_RECEIPT", "INCOME" => "COMPANY_INCOME", "REVENUE" => "REVENUE", "ADVANCE" => "ADVANCE", "CARRIED_ADVANCE" => "OPENING_ADVANCE", "ADVANCE_REPAYMENT" => "ADVANCE_REPAYMENT", _ => "EXPENSE"
    };
    public static int CompanyIncomeSign(string category) => Direction(category) switch
    {
        "COMPANY_INCOME" or "REVENUE" => 1,
        "EXPENSE" => -1,
        _ => 0
    };
    public static string AccountType(string category) => category switch
    {
        "INCOME" or "CLAIM_RECEIPT" => "LIABILITY", "REVENUE" => "REVENUE", "ADVANCE" or "CARRIED_ADVANCE" or "ADVANCE_REPAYMENT" => "ASSET", _ => "EXPENSE"
    };
    public static bool IsAdvance(string category) => category is "ADVANCE" or "CARRIED_ADVANCE";
    public static bool IsCashIn(string category) => category is "INCOME" or "REVENUE" or "ADVANCE_REPAYMENT";
    public static bool IsCashOut(string category) => category is not ("CARRIED_ADVANCE" or "CLAIM_RECEIPT") && !IsCashIn(category);
    public static string AccountCode(string category) => category switch
    {
        "PETTY" => "610110", "FIXED" => "610120", "TRANSPORTATION" => "610130", "SALARY" => "610140", "BONUS" => "610150", "LEGAL" => "610160", "COLLECTOR_COMMISSION" => "610170", "SUPERVISOR_COMMISSION" => "610180", "PAYROLL_COMMISSION" => "610190", "ADVANCE" or "CARRIED_ADVANCE" or "ADVANCE_REPAYMENT" => "113100", "INCOME" => "210300", "REVENUE" => "410100", _ => "610100"
    };
}
