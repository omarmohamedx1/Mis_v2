namespace MIS.Domain.Constants;

public static class AccountingBookValues
{
    public static readonly string[] Categories = ["PETTY", "FIXED", "TRANSPORTATION", "ADVANCE", "CARRIED_ADVANCE", "SUPERVISOR_COMMISSION", "COLLECTOR_COMMISSION", "SALARY", "BONUS", "LEGAL", "INCOME", "ADVANCE_REPAYMENT", "PAYROLL_COMMISSION", "OTHER"];
    public static readonly string[] ManualCategories = ["PETTY", "FIXED", "ADVANCE", "CARRIED_ADVANCE", "BONUS", "LEGAL", "INCOME", "ADVANCE_REPAYMENT", "OTHER"];
    public static string Direction(string category) => category switch
    {
        "INCOME" => "INCOME", "ADVANCE" => "ADVANCE", "CARRIED_ADVANCE" => "OPENING_ADVANCE", "ADVANCE_REPAYMENT" => "ADVANCE_REPAYMENT", _ => "EXPENSE"
    };
    public static string AccountType(string category) => category switch
    {
        "INCOME" => "REVENUE", "ADVANCE" or "CARRIED_ADVANCE" or "ADVANCE_REPAYMENT" => "ASSET", _ => "EXPENSE"
    };
    public static bool IsAdvance(string category) => category is "ADVANCE" or "CARRIED_ADVANCE";
    public static bool IsCashIn(string category) => category is "INCOME" or "ADVANCE_REPAYMENT";
    public static bool IsCashOut(string category) => category != "CARRIED_ADVANCE" && !IsCashIn(category);
    public static string AccountCode(string category) => category switch
    {
        "PETTY" => "610110", "FIXED" => "610120", "TRANSPORTATION" => "610130", "SALARY" => "610140", "BONUS" => "610150", "LEGAL" => "610160", "COLLECTOR_COMMISSION" => "610170", "SUPERVISOR_COMMISSION" => "610180", "PAYROLL_COMMISSION" => "610190", "ADVANCE" or "CARRIED_ADVANCE" or "ADVANCE_REPAYMENT" => "113100", "INCOME" => "410100", _ => "610100"
    };
}
