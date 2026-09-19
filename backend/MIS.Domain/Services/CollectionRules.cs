using MIS.Domain.Constants;

namespace MIS.Domain.Services;

public sealed record PromiseEvaluation(string Status, decimal PaidAmount);

public static class CollectionRules
{
    public static readonly string[] DcrQualifyingActivityTypes = [CollectionsValues.ActivityTypes.Call, CollectionsValues.ActivityTypes.Sms,
        CollectionsValues.ActivityTypes.Email, CollectionsValues.ActivityTypes.Note, CollectionsValues.ActivityTypes.FollowUp,
        CollectionsValues.ActivityTypes.PtpCreated, CollectionsValues.ActivityTypes.PtpKept, CollectionsValues.ActivityTypes.PtpBroken,
        CollectionsValues.ActivityTypes.PtpCancelled, CollectionsValues.ActivityTypes.Payment, CollectionsValues.ActivityTypes.Visit];
    public static readonly string[] DcrSuccessfulContactOutcomes = ["ANSWERED", "CALLBACK_REQUESTED", "REFUSED_TO_PAY"];

    public static bool IsSuccessfulDcrContact(string activityType, string? outcome) =>
        activityType.Equals(CollectionsValues.ActivityTypes.Call, StringComparison.OrdinalIgnoreCase) &&
        outcome is not null && DcrSuccessfulContactOutcomes.Contains(outcome, StringComparer.OrdinalIgnoreCase);

    public static PromiseEvaluation EvaluatePromise(decimal promisedAmount, decimal approvedPaidAmount, DateOnly promiseDate, DateOnly today, int graceDays, decimal toleranceAmount)
    {
        if (promisedAmount <= 0) throw new ArgumentOutOfRangeException(nameof(promisedAmount));
        if (approvedPaidAmount < 0 || graceDays < 0 || toleranceAmount < 0) throw new ArgumentOutOfRangeException(nameof(approvedPaidAmount));
        if (approvedPaidAmount + toleranceAmount >= promisedAmount) return new PromiseEvaluation(CollectionsValues.PromiseStatuses.Fulfilled, approvedPaidAmount);
        if (today <= promiseDate.AddDays(graceDays))
            return new PromiseEvaluation(today == promiseDate ? CollectionsValues.PromiseStatuses.DueToday : today < promiseDate ? CollectionsValues.PromiseStatuses.Upcoming : CollectionsValues.PromiseStatuses.Active, approvedPaidAmount);
        return new PromiseEvaluation(approvedPaidAmount > 0 ? CollectionsValues.PromiseStatuses.PartiallyFulfilled : CollectionsValues.PromiseStatuses.Broken, approvedPaidAmount);
    }

    public static (int Score, string[] Reasons) CalculatePriority(decimal outstanding, int daysPastDue, bool brokenPromise, bool promiseDueToday, int daysSinceContact)
    {
        var score = 0; var reasons = new List<string>();
        if (outstanding >= 100_000m) { score += 25; reasons.Add("HIGH_OUTSTANDING"); }
        else if (outstanding >= 25_000m) { score += 15; reasons.Add("MATERIAL_OUTSTANDING"); }
        if (daysPastDue >= 180) { score += 25; reasons.Add("SEVERE_DELINQUENCY"); }
        else if (daysPastDue >= 90) { score += 15; reasons.Add("HIGH_DELINQUENCY"); }
        if (brokenPromise) { score += 30; reasons.Add("BROKEN_PTP"); }
        if (promiseDueToday) { score += 20; reasons.Add("PTP_DUE_TODAY"); }
        if (daysSinceContact >= 10) { score += 15; reasons.Add("NO_RECENT_CONTACT"); }
        return (Math.Clamp(score, 0, 100), reasons.ToArray());
    }

    public static string MaskNationalId(string? value) => Mask(value, 3, 3);
    public static string MaskPhone(string? value) => Mask(value, 2, 2);
    public static string MaskCard(string? value) => Mask(value, 4, 4);
    private static string Mask(string? value, int visibleStart, int visibleEnd)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (value.Length <= visibleStart + visibleEnd) return new string('*', value.Length);
        return string.Concat(value.AsSpan(0, visibleStart), new string('*', value.Length - visibleStart - visibleEnd), value.AsSpan(value.Length - visibleEnd));
    }
}
