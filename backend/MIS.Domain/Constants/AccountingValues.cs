namespace MIS.Domain.Constants;

public static class AccountingValues
{
    public static class PayrollStatuses
    {
        public const string Draft = "DRAFT";
        public const string PendingReview = "PENDING_REVIEW";
        public const string Approved = "APPROVED";
        public const string Paid = "PAID";
        public const string Cancelled = "CANCELLED";
        public static readonly string[] All = [Draft, PendingReview, Approved, Paid, Cancelled];
        public static readonly string[] Editable = [Draft, PendingReview];
    }

    public static class TransportationStatuses
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
        public const string Paid = "PAID";
        public const string Cancelled = "CANCELLED";
        public static readonly string[] All = [Pending, Approved, Rejected, Paid, Cancelled];
    }

    public static class CommissionStatuses
    {
        public const string Draft = "DRAFT";
        public const string PendingReview = "PENDING_REVIEW";
        public const string Approved = "APPROVED";
        public const string Paid = "PAID";
        public const string Cancelled = "CANCELLED";
        public static readonly string[] All = [Draft, PendingReview, Approved, Paid, Cancelled];
        public static readonly string[] Editable = [Draft, PendingReview];
    }

    public static class CommissionScopes
    {
        public const string Collector = "COLLECTOR";
        public const string Supervisor = "SUPERVISOR";
    }

    public static class CommissionBases
    {
        public const string PercentOfCollected = "PERCENT_OF_COLLECTED";
        public const string FixedAmount = "FIXED_AMOUNT";
        public const string Tiered = "TIERED";
    }
}
