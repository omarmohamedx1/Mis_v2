namespace MIS.Application.Common;

public static class ExcelImportLimits
{
    public const long MaximumBytes = 50L * 1024 * 1024;
    public const long RequestBytes = 52L * 1024 * 1024;
    public const string SizeMessage = "Choose a CSV, XLS, or XLSX file no larger than 50 MB.";
}
