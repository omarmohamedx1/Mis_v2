using System.Globalization;
using MIS.Application.DTOs.Collections;

namespace MIS.Infrastructure.Services;

/// <summary>Calculates the upper BKT scorecard using period-bound opening balances.</summary>
public static class CollectionScoreCalculator
{
    public sealed record CaseInput(int? Bucket, decimal? Balance, string? Movement, decimal? Dues, decimal? Paid);

    public static int EndingBucket(int opening, decimal payment, IReadOnlyList<decimal> dues)
    {
        if (opening is < 1 or > 6 || payment < 0 || dues.Count != 6 || dues.Any(value => value < 0))
            throw new ArgumentException("Invalid opening BKT, payment, or dues bands.");
        if (payment < dues[1]) return opening + 1;
        var sum = dues[1];
        if (opening == 1)
        {
            sum += dues[2];
            if (payment < sum) return 2;
        }
        else if (opening is 2 or 6)
        {
            sum += dues[2] + dues[3];
            if (payment < sum) return 2;
        }
        else
        {
            for (var band = 2; band <= Math.Max(2, opening); band++)
            {
                sum += dues[band];
                if (payment < sum) return opening - band + 2;
            }
        }
        return payment < sum + dues[0] ? 1 : 0;
    }

    public static string Movement(int opening, int ending) =>
        ending == 0 ? "NORM" : ending == opening ? "STAB" : ending < opening ? "BWD" : "FWD";

    public static CollectionScoreBucketDto Calculate(string code, string version,
        IReadOnlyCollection<CollectionScoreCellDto> settings, IReadOnlyCollection<CaseInput> cases,
        bool sourceAvailable)
    {
        var bucket = int.Parse(code[3..], CultureInfo.InvariantCulture);
        var cohort = cases.Where(item => item.Bucket == bucket).ToArray();
        var issues = new HashSet<string>(StringComparer.Ordinal);
        if (!sourceAvailable) issues.Add("MISSING_PERIOD_SOURCE");
        if (cases.Any(item => item.Bucket is null)) issues.Add("MISSING_OPENING_BUCKET");
        var bucketsComplete = cases.All(item => item.Bucket.HasValue);
        var balancesComplete = sourceAvailable && bucketsComplete && cohort.All(item => item.Balance.HasValue);
        var movementsComplete = sourceAvailable && bucketsComplete && cohort.All(item => item.Movement is not null &&
            !(bucket == 1 && item.Movement == "BWD"));
        if (!balancesComplete && sourceAvailable) issues.Add("MISSING_OPENING_BALANCE");
        if (!movementsComplete && sourceAvailable && bucket != 6) issues.Add("MISSING_MOVEMENT");
        decimal? balance = balancesComplete ? cohort.Sum(item => item.Balance!.Value) : null;
        if (bucket == 6)
        {
            var config = settings.Single(item => item.Category == "MORE");
            decimal? dues = sourceAvailable && bucketsComplete && cohort.All(item => item.Dues.HasValue) ? cohort.Sum(item => item.Dues!.Value) : null;
            decimal? paid = sourceAvailable && bucketsComplete && cohort.All(item => item.Paid.HasValue) ? cohort.Sum(item => item.Paid!.Value) : null;
            if (dues is null) issues.Add("MISSING_STARTING_DUES");
            if (paid is null) issues.Add("MISSING_PERIOD_PAYMENTS");
            if (config.TargetPercent is null or <= 0) issues.Add("MISSING_TARGETS");
            decimal? percent = dues > 0 ? paid / dues * 100 : null;
            decimal? ratio = config.TargetPercent > 0 ? percent / config.TargetPercent * 100 : null;
            var cell = config with { CaseCount = sourceAvailable && bucketsComplete ? cohort.Length : null, ActualBalance = paid,
                TargetBalance = dues * config.TargetPercent / 100, Missing = paid - dues * config.TargetPercent / 100,
                Percent = percent, Weight = null, ActualVsTarget = ratio, Score = ratio };
            if (dues == 0) issues.Add("ZERO_BALANCE");
            return new(code, version, [cell], cell.CaseCount, dues, cell.TargetBalance, null, cell.Score, issues.ToArray());
        }
        var targetsComplete = settings.Where(item => item.Category != "STAB").All(item => item.TargetPercent.HasValue);
        if (!targetsComplete) issues.Add("MISSING_TARGETS");
        bool IsScored(string category) => category != "STAB" && !(bucket == 1 && category == "NORM");
        var weightsComplete = settings.Where(item => IsScored(item.Category)).All(item => item.Weight.HasValue);
        if (!weightsComplete) issues.Add("MISSING_WEIGHTS");
        if (balance == 0) issues.Add("ZERO_BALANCE");
        var rows = settings.Select(config =>
        {
            var category = config.Category;
            var matching = cohort.Where(item => item.Movement == category).ToArray();
            var sign = category == "FWD" ? -1m : 1m;
            decimal? actual = balancesComplete && movementsComplete ? sign * matching.Sum(item => item.Balance!.Value) : null;
            decimal? target = category == "STAB"
                ? targetsComplete ? 100 - settings.Where(item => item.Category != "STAB").Sum(item => item.TargetPercent!.Value) : null
                : config.TargetPercent;
            var targetBalance = balance * target / 100 * sign;
            decimal? percent = balance > 0 ? Math.Abs(actual ?? 0) / balance * 100 : null;
            if (actual is null) percent = null;
            decimal? ratio = category == "STAB" ? null : category == "FWD"
                ? percent > 0 ? Math.Min(target.GetValueOrDefault() / percent.Value, 2) : null
                : target > 0 ? Math.Min(percent.GetValueOrDefault() / target.Value, 2) : null;
            if (percent is null || target is null) ratio = null;
            if (IsScored(category) && percent.HasValue && target.HasValue && ratio is null && balance > 0) issues.Add("ZERO_RATIO_BASE");
            decimal? score = IsScored(category) ? ratio * config.Weight : null;
            // Workbook J5 changes the score, while the displayed achievement ratio keeps its normal formula.
            if (bucket == 1 && category == "FWD" && cohort.Length > 0 && matching.Length == cohort.Length && score.HasValue) score = 0;
            return config with { CaseCount = movementsComplete ? matching.Length : null, ActualBalance = actual,
                TargetPercent = target, TargetBalance = targetBalance, Missing = actual - targetBalance,
                Percent = percent, Weight = IsScored(category) ? config.Weight : null,
                ActualVsTarget = ratio * 100, Score = score };
        }).ToArray();
        var scoringRows = rows.Where(item => IsScored(item.Category)).ToArray();
        decimal? totalScore = scoringRows.All(item => item.Score.HasValue) ? scoringRows.Sum(item => item.Score!.Value) : null;
        return new(code, version, rows, sourceAvailable && bucketsComplete ? cohort.Length : null, balance,
            targetsComplete && balance.HasValue ? rows.Where(item => item.Category != "FWD").Sum(item => item.TargetBalance) : null,
            weightsComplete ? scoringRows.Sum(item => item.Weight) : null, totalScore, issues.ToArray());
    }

    public static int? ParseBucket(string? value)
    {
        var text = value?.Trim().Replace(" ", "").ToUpperInvariant();
        if (text is "MORE" or "BKTMORE" or "BKT6+") return 6;
        if (text?.StartsWith("BKT", StringComparison.Ordinal) == true) text = text[3..];
        return int.TryParse(text, out var bucket) && bucket is >= 0 and <= 6 ? bucket : null;
    }
}
