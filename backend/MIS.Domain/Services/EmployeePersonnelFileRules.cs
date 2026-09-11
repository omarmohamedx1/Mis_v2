namespace MIS.Domain.Services;

public static class EmployeePersonnelFileRules
{
    public static bool IsMilitaryDocumentRequired(string? gender) =>
        string.Equals(gender?.Trim(), "Male", StringComparison.OrdinalIgnoreCase);

    public static int RequiredDocumentCount(string? gender) => IsMilitaryDocumentRequired(gender) ? 7 : 6;

    public static int CompletionPercentage(int completedDocuments, string? gender)
    {
        var required = RequiredDocumentCount(gender);
        if (completedDocuments < 0 || completedDocuments > required)
            throw new ArgumentOutOfRangeException(nameof(completedDocuments));
        return completedDocuments * 100 / required;
    }
}
