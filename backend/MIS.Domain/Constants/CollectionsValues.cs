namespace MIS.Domain.Constants;

public static class CollectionsValues
{
    public static class OrganizationTypes
    {
        public const string Bank = "BANK";
        public const string ConsumerFinance = "CONSUMER_FINANCE";
        public const string FinancialInstitution = "FINANCIAL_INSTITUTION";
        public const string Other = "OTHER";
    }

    public static class CaseStatuses
    {
        public const string Active = "ACTIVE";
        public const string OnHold = "ON_HOLD";
        public const string Settled = "SETTLED";
        public const string Closed = "CLOSED";
        public const string Legal = "LEGAL";
        public const string WriteOff = "WRITE_OFF";
    }

    public static class PromiseStatuses
    {
        public const string Active = "ACTIVE";
        public const string DueToday = "DUE_TODAY";
        public const string Upcoming = "UPCOMING";
        public const string UnderReview = "UNDER_REVIEW";
        public const string Fulfilled = "FULFILLED";
        public const string PartiallyFulfilled = "PARTIALLY_FULFILLED";
        public const string Broken = "BROKEN";
        public const string Cancelled = "CANCELLED";
        public const string Rescheduled = "RESCHEDULED";
    }

    public static class PaymentStatuses
    {
        public const string Submitted = "SUBMITTED";
        public const string UnderReview = "UNDER_REVIEW";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
        public const string Reversed = "REVERSED";
    }

    public static class AssignmentSources
    {
        public const string Manual = "MANUAL";
        public const string Automatic = "AUTOMATIC";
        public const string Import = "IMPORT";
    }

    public static class ActivityTypes
    {
        public const string Call = "CALL";
        public const string Sms = "SMS";
        public const string Email = "EMAIL";
        public const string Note = "NOTE";
        public const string FollowUp = "FOLLOW_UP";
        public const string Assignment = "ASSIGNMENT";
        public const string PtpCreated = "PTP_CREATED";
        public const string PtpKept = "PTP_KEPT";
        public const string PtpBroken = "PTP_BROKEN";
        public const string PtpCancelled = "PTP_CANCELLED";
        public const string Payment = "PAYMENT";
        public const string Visit = "VISIT";
        public const string Complaint = "COMPLAINT";
        public const string StatusChange = "STATUS_CHANGE";
    }

    public static class VisitStatuses
    {
        public const string Scheduled = "SCHEDULED";
        public const string Planned = "PLANNED";
        public const string Assigned = "ASSIGNED";
        public const string InProgress = "IN_PROGRESS";
        public const string Completed = "COMPLETED";
        public const string Missed = "MISSED";
        public const string Failed = "FAILED";
        public const string Rescheduled = "RESCHEDULED";
        public const string Cancelled = "CANCELLED";
    }

    public static class VisitResults
    {
        public const string CustomerMet = "CUSTOMER_MET";
        public const string CustomerNotAvailable = "CUSTOMER_NOT_AVAILABLE";
        public const string AddressNotFound = "ADDRESS_NOT_FOUND";
        public const string WrongAddress = "WRONG_ADDRESS";
        public const string RefusedContact = "REFUSED_CONTACT";
        public const string PromiseToPayDiscussed = "PROMISE_TO_PAY_DISCUSSED";
        public const string FollowUpRequired = "FOLLOW_UP_REQUIRED";
        public const string Other = "OTHER";
    }

    public static class ComplaintStatuses
    {
        public const string Open = "OPEN";
        public const string New = "NEW";
        public const string Assigned = "ASSIGNED";
        public const string InProgress = "IN_PROGRESS";
        public const string AwaitingInformation = "AWAITING_INFORMATION";
        public const string Resolved = "RESOLVED";
        public const string Reopened = "REOPENED";
        public const string Closed = "CLOSED";
        public const string Rejected = "REJECTED";
        public const string Escalated = "ESCALATED";
    }

    public static class ComplaintPriorities
    {
        public const string Low = "LOW";
        public const string Medium = "MEDIUM";
        public const string High = "HIGH";
        public const string Critical = "CRITICAL";
    }

    public static class PrimaryClassifications
    {
        public const string Act = "ACT";
        public const string Wo = "WO";
        public const string Corp = "CORP";

        /// <summary>Write-off is presented as "W.O" but always persisted as "WO".</summary>
        public const string WoDisplay = "W.O";

        public static readonly string[] All = [Act, Wo, Corp];
    }

    public static class SubClassifications
    {
        public const string Loan = "LOAN";
        public const string Visa = "VISA";
        public const string Auto = "AUTO";
        public const string Act = PrimaryClassifications.Act;
        public const string Wo = PrimaryClassifications.Wo;

        /// <summary>Products valid under the ACT and WO primary classifications.</summary>
        public static readonly string[] Products = [Loan, Visa, Auto];

        /// <summary>Segments valid under the CORP primary classification.</summary>
        public static readonly string[] CorporateSegments = [Act, Wo];
    }
}

public static class PortfolioClassification
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return normalized.Length == 0 ? null : normalized;
    }

    public static string[] SubClassificationsFor(string? primary) => Normalize(primary) switch
    {
        CollectionsValues.PrimaryClassifications.Act or CollectionsValues.PrimaryClassifications.Wo => CollectionsValues.SubClassifications.Products,
        CollectionsValues.PrimaryClassifications.Corp => CollectionsValues.SubClassifications.CorporateSegments,
        _ => []
    };

    public static bool IsValid(string? primary, string? sub)
    {
        var normalizedSub = Normalize(sub);
        return normalizedSub is not null && SubClassificationsFor(primary).Contains(normalizedSub);
    }

    /// <summary>Normalizes a classification pair and fails when the combination is not supported.</summary>
    public static (string Primary, string Sub) Require(string? primary, string? sub)
    {
        var normalizedPrimary = Normalize(primary);
        var normalizedSub = Normalize(sub);
        if (normalizedPrimary is null || !CollectionsValues.PrimaryClassifications.All.Contains(normalizedPrimary))
            throw new ArgumentException("Primary classification must be ACT, WO, or CORP.", nameof(primary));
        if (normalizedSub is null || !SubClassificationsFor(normalizedPrimary).Contains(normalizedSub))
            throw new ArgumentException($"Sub classification must be one of {string.Join(", ", SubClassificationsFor(normalizedPrimary))} for {normalizedPrimary}.", nameof(sub));
        return (normalizedPrimary, normalizedSub);
    }

    public static string Code(string primary, string sub) => $"{primary}-{sub}";

    public static string Display(string? value) =>
        Normalize(value) == CollectionsValues.PrimaryClassifications.Wo ? CollectionsValues.PrimaryClassifications.WoDisplay : Normalize(value) ?? string.Empty;

    /// <summary>True when a portfolio code is the seeded default or a code this classification scheme generated.</summary>
    public static bool IsReplaceableCode(string? code)
    {
        var value = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value)) return true;
        if (value == "DEFAULT") return true;
        var parts = value.Split('-');
        return parts.Length == 2 && IsValid(parts[0], parts[1]);
    }
}
