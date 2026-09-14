namespace MIS.Domain.Constants;

public static class DataEntryValues
{
    public static class BatchStatuses
    {
        public const string Draft = "DRAFT";
        public const string Submitted = "SUBMITTED";
        public const string Accepted = "ACCEPTED";
        public const string Distributed = "DISTRIBUTED";
        public const string Rejected = "REJECTED";
        public static readonly string[] All = [Draft, Submitted, Accepted, Distributed, Rejected];
    }

    public static class RowStatuses
    {
        public const string Ready = "READY";
        public const string ExistingCustomer = "EXISTING_CUSTOMER";
        public const string DuplicateInFile = "DUPLICATE_IN_FILE";
        public const string InvalidNationalId = "INVALID_NATIONAL_ID";
        public const string InvalidMobile = "INVALID_MOBILE";
        public const string Error = "ERROR";
        public static readonly string[] All = [Ready, ExistingCustomer, DuplicateInFile, InvalidNationalId, InvalidMobile, Error];
        public static readonly string[] Importable = [Ready, ExistingCustomer];
    }

    public static class Sources
    {
        public const string Manual = "MANUAL";
        public const string Imported = "IMPORTED";
    }

    public static class NotificationKinds
    {
        public const string BatchSubmitted = "BATCH_SUBMITTED";
    }
}
