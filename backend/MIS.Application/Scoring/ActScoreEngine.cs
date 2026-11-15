namespace MIS.Application.Scoring;

/// <summary>Pure ACT reference-model calculation. Never reads or changes financial records.</summary>
public static class ActScoreEngine
{
    public const string ModelVersion = "ACT-EXCEL-1";

    public static ActScoreResult Calculate(ActScoreScope scope, ActScoreRules? rules, ActScoreInput? input)
    {
        if (rules is null) return ActScoreResult.Unavailable("RULES_MISSING");
        if (input is null) return ActScoreResult.Unavailable("SOURCE_MISSING");
        try
        {
            var issues = Validate(scope, rules, input);
            return issues.Count != 0 ? ActScoreResult.Unavailable(issues.ToArray()) : CalculateValidated(rules, input);
        }
        catch (OverflowException) { return ActScoreResult.Unavailable("NUMERIC_RANGE"); }
    }

    private static List<string> Validate(ActScoreScope scope, ActScoreRules rules, ActScoreInput input)
    {
        var issues = new List<string>();
        if (scope.OrganizationId == Guid.Empty || scope.PortfolioId == Guid.Empty || scope.Period.Day != 1
            || scope.Period.Year >= 9999 || string.IsNullOrWhiteSpace(scope.Currency)
            || rules.Scope != scope || input.Scope != scope) return ["SCOPE_MISMATCH"];
        if (rules.Version != ModelVersion) issues.Add("RULE_VERSION_UNSUPPORTED");
        if (rules.ApprovedBy == Guid.Empty || rules.ApprovedAt == default) issues.Add("RULES_UNAPPROVED");
        if (!input.OpeningComplete || !input.PaymentsComplete || input.ReconciledBy == Guid.Empty
            || string.IsNullOrWhiteSpace(input.SourceVersion)) issues.Add("SOURCE_INCOMPLETE");
        if (DateOnly.FromDateTime(input.Cutoff.UtcDateTime) < scope.Period) issues.Add("CUTOFF_INVALID");
        if (rules.MoreTarget <= 0 || rules.MoreTarget > 1 || rules.ActiveWeight < 0 || rules.MoreWeight < 0
            || rules.ActiveWeight + rules.MoreWeight != 1 || rules.MovementDecimalPlaces is < 0 or > 12) issues.Add("WEIGHTS_INVALID");
        if (rules.Movements is null || input.Cases is null || input.Payments is null)
        { issues.Add("SOURCE_INCOMPLETE"); return issues; }
        var expected = Enumerable.Range(1, 5).SelectMany(b => Movements(b).Where(m => m != "STAB").Select(m => (b, m))).ToHashSet();
        if (rules.Movements.Any(r => r is null) || rules.Movements.Count != expected.Count
            || !rules.Movements.Select(r => (r.Bucket, r.Movement)).ToHashSet().SetEquals(expected))
            issues.Add("RULES_INCOMPLETE");
        else
        {
            if (rules.Movements.Any(r => r.Target <= 0 || r.Target > 1 || r.Weight < 0 || r.Weight > 100)
                || rules.Movements.Sum(r => r.Weight) != 100) issues.Add("WEIGHTS_INVALID");
            if (!rules.AllowNegativeStabilityTarget && rules.Movements.GroupBy(r => r.Bucket).Any(g => g.Sum(r => r.Target) > 1))
                issues.Add("NEGATIVE_STAB_TARGET_REQUIRES_APPROVAL");
        }
        if (input.Cases.Any(c => c is null) || input.Payments.Any(p => p is null))
        { issues.Add("SOURCE_INCOMPLETE"); return issues; }
        if (input.Cases.Any(c => c.Scope != scope) || input.Payments.Any(p => p.Scope != scope)) issues.Add("SCOPE_MISMATCH");
        if (input.Cases.Any(c => c.CaseId == Guid.Empty || c.Bucket is < 1 or > 6 || c.Balance < 0 || c.StartingDues < 0
            || c.Dues is null || c.Dues.Count != 6 || string.IsNullOrWhiteSpace(c.SourceReference)))
            issues.Add("OPENING_INVALID");
        if (!rules.AllowSignedDues && input.Cases.Any(c => c.Dues?.Any(d => d < 0) == true))
            issues.Add("SIGNED_DUES_REQUIRE_APPROVAL");
        if (input.Cases.Any(c => c.EndingOverride is { } o && (!rules.AllowEndingBucketOverrides
            || o.EndingBucket < 0 || o.EndingBucket > c.Bucket + 1 || o.ApprovedBy == Guid.Empty
            || o.ApprovedAt == default || string.IsNullOrWhiteSpace(o.Reason))))
            issues.Add("MOVEMENT_OVERRIDE_UNAPPROVED");
        if (input.Cases.Select(c => c.CaseId).Distinct().Count() != input.Cases.Count
            || input.Payments.Select(p => p.PaymentId).Distinct().Count() != input.Payments.Count) issues.Add("DUPLICATE_SOURCE");
        var ids = input.Cases.Select(c => c.CaseId).ToHashSet();
        if (input.Payments.Any(p => p.PaymentId == Guid.Empty || !ids.Contains(p.CaseId) || p.Amount <= 0
            || p.PaymentDate < scope.Period || p.PaymentDate >= scope.Period.AddMonths(1)
            || p.PaymentDate > DateOnly.FromDateTime(input.Cutoff.UtcDateTime)
            || (p.ReversedAt.HasValue && (!p.ApprovedAt.HasValue || p.ReversedAt < p.ApprovedAt))))
            issues.Add("PAYMENT_INVALID");
        return issues.Distinct().ToList();
    }

    public static string[] Movements(int bucket) => bucket == 1 ? ["STAB", "NORM", "FWD"] : ["STAB", "BWD", "NORM", "FWD"];

    public static int EndingBucket(int bucket, decimal paid, IReadOnlyList<decimal> d, int decimalPlaces = 8)
    {
        if (bucket is < 1 or > 6 || paid < 0 || d.Count != 6 || decimalPlaces is < 0 or > 12)
            throw new ArgumentException("Invalid movement inputs.");
        // Normalize comparison precision only; never round the underlying financial facts or sums.
        bool Below(decimal threshold) => decimal.Round(paid, decimalPlaces, MidpointRounding.AwayFromZero)
            < decimal.Round(threshold, decimalPlaces, MidpointRounding.AwayFromZero);
        if (Below(d[1])) return bucket + 1;
        if (bucket == 1)
            return Below(d[1] + d[2]) ? 2 : Below(d[1] + d[2] + d[0]) ? 1 : 0;
        if (bucket is 2 or 6)
            return Below(d[1] + d[2] + d[3]) ? 2 : Below(d[1] + d[2] + d[3] + d[0]) ? 1 : 0;
        decimal sum = d[1];
        for (int i = 2; i <= bucket; i++)
        {
            sum += d[i];
            if (Below(sum)) return bucket - i + 2;
        }
        return Below(sum + d[0]) ? 1 : 0;
    }

    private static ActScoreResult CalculateValidated(ActScoreRules rules, ActScoreInput input)
    {
        var issues = new List<string>();
        var payments = input.Payments.Where(p => p.ApprovedAt.HasValue && p.ApprovedAt <= input.Cutoff
                && (!p.ReversedAt.HasValue || p.ReversedAt > input.Cutoff))
            .GroupBy(p => p.CaseId).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var facts = input.Cases.Select(c =>
        {
            var paid = payments.GetValueOrDefault(c.CaseId);
            var ending = c.EndingOverride?.EndingBucket ?? EndingBucket(c.Bucket, paid, c.Dues, rules.MovementDecimalPlaces);
            var movement = ending == 0 ? "NORM" : ending == c.Bucket ? "STAB" : ending < c.Bucket ? "BWD" : "FWD";
            return (Case: c, Paid: paid, Movement: movement);
        }).ToArray();
        var buckets = new List<ActBucketResult>();
        for (int b = 1; b <= 6; b++)
        {
            var cohort = facts.Where(f => f.Case.Bucket == b).ToArray();
            var balance = cohort.Sum(f => f.Case.Balance);
            var cells = new List<ActScoreCell>();
            foreach (var movement in Movements(b))
            {
                var matches = cohort.Where(f => f.Movement == movement).ToArray();
                var amount = matches.Sum(f => f.Case.Balance);
                var sign = movement == "FWD" ? -1 : 1;
                decimal? percent = balance == 0 ? null : amount / balance;
                decimal? target = null, weight = null, ratio = null, score = null, final = null;
                if (b < 6)
                {
                    if (movement == "STAB") target = 1 - rules.Movements.Where(r => r.Bucket == b).Sum(r => r.Target);
                    else
                    {
                        var rule = rules.Movements.Single(r => r.Bucket == b && r.Movement == movement);
                        target = rule.Target; weight = rule.Weight;
                        if (percent.HasValue && (movement != "FWD" || percent > 0))
                        {
                            var uncapped = movement == "FWD" ? target.Value / percent.Value : percent.Value / target.Value;
                            ratio = Math.Min(uncapped, 2);
                            score = weight * ratio;
                            final = weight * (movement == "FWD" ? ratio : uncapped);
                            if (b == 1 && movement == "FWD" && matches.Length == cohort.Length) score = 0;
                        }
                        else issues.Add($"BKT{b}_DENOMINATOR_ZERO");
                    }
                }
                var targetBalance = target * balance * sign;
                cells.Add(new(movement, matches.Length, amount * sign, target * 100, targetBalance,
                    targetBalance.HasValue ? amount * sign - targetBalance : null, percent * 100, weight, ratio, score, final));
            }
            decimal? displayScore = b == 6 || cells.Where(c => c.Weight.HasValue).Any(c => c.Score is null) ? null : cells.Sum(c => c.Score ?? 0);
            decimal? finalScore = b == 6 || cells.Where(c => c.Weight.HasValue).Any(c => c.FinalScore is null) ? null : cells.Sum(c => c.FinalScore ?? 0);
            buckets.Add(new(b, cohort.Length, balance, cohort.Sum(f => f.Case.StartingDues), cohort.Sum(f => f.Paid), displayScore, finalScore, cells));
        }
        var more = buckets.Single(b => b.Bucket == 6);
        decimal? balanceScore = buckets.Where(b => b.Bucket < 6).Any(b => b.FinalScore is null) ? null : buckets.Sum(b => b.FinalScore ?? 0);
        decimal? moreScore = more.StartingDues > 0 ? more.Paid / more.StartingDues / rules.MoreTarget * 100 : null;
        if (moreScore is null) issues.Add("MORE_DENOMINATOR_ZERO");
        // A zero component weight does not make an undefined component invent a zero score.
        decimal? activeContribution = rules.ActiveWeight == 0 ? 0m : balanceScore * rules.ActiveWeight;
        decimal? moreContribution = rules.MoreWeight == 0 ? 0m : moreScore * rules.MoreWeight;
        decimal? overall = activeContribution + moreContribution;
        return new(issues.Count == 0 ? "CALCULATED" : "PARTIAL", issues.Distinct().ToArray(), buckets,
            more.StartingDues, buckets.Sum(b => b.Paid), balanceScore, moreScore, overall,
            more.StartingDues * rules.MoreTarget, more.StartingDues > 0 ? more.Paid / more.StartingDues * 100 : null,
            moreScore / 100, activeContribution, moreContribution);
    }
}
