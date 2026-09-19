namespace MIS.Domain.Constants;

public static class LegalValues
{
    public static class Stages
    {
        public const string Intake = "INTAKE";
        public const string Notice = "NOTICE";
        public const string Court = "COURT";
        public const string Hearing = "HEARING";
        public const string Judgment = "JUDGMENT";
        public const string Settlement = "SETTLEMENT";
        public const string Returned = "RETURNED";

        public static readonly string[] All = [Intake, Notice, Court, Hearing, Judgment, Settlement, Returned];
        public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToUpperInvariant());
    }

    public static class Actions
    {
        public const string Note = "NOTE";
        public const string Notice = "NOTICE";
        public const string Hearing = "HEARING";
        public const string Judgment = "JUDGMENT";
        public const string Settlement = "SETTLEMENT";
        public const string Return = "RETURN";

        public static readonly string[] All = [Note, Notice, Hearing, Judgment, Settlement, Return];
        public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToUpperInvariant());
    }

    public static string StageForAction(string action) => action.Trim().ToUpperInvariant() switch
    {
        Actions.Notice => Stages.Notice,
        Actions.Hearing => Stages.Hearing,
        Actions.Judgment => Stages.Judgment,
        Actions.Settlement => Stages.Settlement,
        Actions.Return => Stages.Returned,
        _ => Stages.Intake,
    };

    public static string? CollectionStatusForAction(string action) => action.Trim().ToUpperInvariant() switch
    {
        Actions.Settlement => CollectionsValues.CaseStatuses.Settled,
        Actions.Judgment => CollectionsValues.CaseStatuses.Closed,
        Actions.Return => CollectionsValues.CaseStatuses.Active,
        _ => null,
    };
}
