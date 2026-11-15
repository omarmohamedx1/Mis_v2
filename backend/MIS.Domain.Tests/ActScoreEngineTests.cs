using System.Text.Json;
using MIS.Application.Scoring;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class ActScoreEngineTests
{
    internal static readonly ActScoreScope Scope = new(Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"), new(2026, 10, 1), "EGP");
    internal static readonly DateTimeOffset Cutoff = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    internal static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    internal static JsonDocument Reference() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "act-score-reference.json")));
    internal static (ActScoreRules Rules, ActScoreInput Input) Example()
    {
        using var doc = Reference(); var root = doc.RootElement;
        var cases = new List<ActOpeningCase>(); var payments = new List<ActPaymentFact>(); int index = 0;
        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            var id = new Guid(++index, 0, 0, new byte[8]);
            cases.Add(new(id, Scope, c.GetProperty("bucket").GetInt32(), c.GetProperty("balance").GetDecimal(),
                c.GetProperty("startingDues").GetDecimal(), c.GetProperty("dues").EnumerateArray().Select(x => x.GetDecimal()).ToArray(),
                Actor, $"anonymized-reference-row-{index}", c.TryGetProperty("endingOverride", out var correction)
                    ? new(correction.GetInt32(), "Synthetic approval of workbook manual ending-BKT reference", Actor, Cutoff) : null));
            var paid = c.GetProperty("paid").GetDecimal();
            if (paid > 0) payments.Add(new(id, id, Scope, new(2026, 10, 31), paid, Cutoff.AddDays(-1), null));
        }
        var rules = new ActScoreRules(Scope, ActScoreEngine.ModelVersion, Actor, Cutoff,
            root.GetProperty("rules").EnumerateArray().Select(r => new ActMovementRule(r.GetProperty("bucket").GetInt32(),
                r.GetProperty("movement").GetString()!, r.GetProperty("target").GetDecimal(), r.GetProperty("weight").GetDecimal())).ToArray(),
            root.GetProperty("moreTarget").GetDecimal(), root.GetProperty("activeWeight").GetDecimal(), root.GetProperty("moreWeight").GetDecimal(), 8, true, true, true);
        return (rules, new(Scope, true, true, Actor, Cutoff, "anonymized-excel-oracle", cases, payments));
    }

    private static void Near(decimal expected, decimal? actual, string label)
    {
        Assert.True(actual.HasValue, $"{label}: missing");
        Assert.True(Math.Abs(expected - actual!.Value) <= 0.000001m, $"{label}: expected {expected}, actual {actual}");
    }

    [Fact]
    public void Every_reference_case_and_every_upper_and_lower_BKT_cell_matches_Excel()
    {
        var (rules, input) = Example(); var result = ActScoreEngine.Calculate(Scope, rules, input);
        Assert.True(result.Status == "CALCULATED", string.Join(", ", result.Issues)); Assert.Empty(result.Issues);
        using var doc = Reference(); var root = doc.RootElement;
        int index = 0;
        foreach (var reference in root.GetProperty("cases").EnumerateArray())
        {
            var c = input.Cases[index++]; var paid = reference.GetProperty("paid").GetDecimal();
            var ending = c.EndingOverride?.EndingBucket ?? ActScoreEngine.EndingBucket(c.Bucket, paid, c.Dues, rules.MovementDecimalPlaces);
            Assert.Equal(reference.GetProperty("endingBucket").GetInt32(), ending);
            Assert.Equal(reference.GetProperty("movement").GetString(), ending == 0 ? "NORM" : ending == c.Bucket ? "STAB" : ending < c.Bucket ? "BWD" : "FWD");
        }
        foreach (var b in root.GetProperty("buckets").EnumerateArray())
        {
            var actual = result.Buckets.Single(x => x.Bucket == b.GetProperty("bucket").GetInt32());
            Assert.Equal(b.GetProperty("count").GetInt32(), actual.CaseCount);
            Near(b.GetProperty("balance").GetDecimal(), actual.OpeningBalance, "bucket balance");
            Near(b.GetProperty("paid").GetDecimal(), actual.Paid, "bucket payments");
            Near(b.GetProperty("displayScore").GetDecimal(), actual.DisplayScore, "bucket displayed score");
            foreach (var row in b.GetProperty("cells").EnumerateArray())
            {
                var cell = actual.Cells.Single(x => x.Category == row.GetProperty("category").GetString());
                decimal?[] values = [cell.CaseCount, cell.ActualBalance, cell.TargetPercent / 100, cell.TargetBalance,
                    cell.Missing, cell.Percent / 100, cell.Weight, cell.ActualVsTarget, cell.Score];
                int column = 0;
                foreach (var expected in row.GetProperty("values").EnumerateArray())
                {
                    if (expected.ValueKind == JsonValueKind.Null) Assert.Null(values[column]);
                    else Near(expected.GetDecimal(), values[column], $"BKT{actual.Bucket}/{cell.Category}/column{column}");
                    column++;
                }
            }
        }
        foreach (var f in root.GetProperty("finals").EnumerateArray())
        {
            var cell = result.Buckets.Single(b => b.Bucket == f.GetProperty("bucket").GetInt32()).Cells.Single(c => c.Category == f.GetProperty("movement").GetString());
            Near(f.GetProperty("score").GetDecimal(), cell.FinalScore, "lower final cell");
            var target = cell.TargetPercent!.Value / 100;
            var percent = cell.Percent!.Value / 100;
            Near(f.GetProperty("ratio").GetDecimal(), cell.Category == "FWD" ? Math.Min(target / percent, 2) : percent / target, "lower ratio");
        }
        var more = result.Buckets.Single(b => b.Bucket == 6);
        Assert.Equal(root.GetProperty("moreCount").GetInt32(), more.CaseCount);
        Near(root.GetProperty("moreBalance").GetDecimal(), more.OpeningBalance, "MORE balance");
        Near(root.GetProperty("moreDue").GetDecimal(), result.TotalDueAmount, "MORE dues");
        Near(root.GetProperty("morePaid").GetDecimal(), more.Paid, "MORE payments");
        Near(root.GetProperty("moreScore").GetDecimal(), result.MoreScore, "MORE score");
        Near(root.GetProperty("balanceScore").GetDecimal(), result.BalanceScore, "balance score");
        Near(root.GetProperty("paid").GetDecimal(), result.Paid, "all paid");
        Near(root.GetProperty("overall").GetDecimal(), result.ScoreAll, "overall");
        Near(root.GetProperty("moreTargetAmount").GetDecimal(), result.MoreTargetAmount, "MORE target amount C57");
        Near(root.GetProperty("moreActualPercent").GetDecimal() * 100, result.MoreActualPercent, "MORE actual percent F57");
        Near(root.GetProperty("moreActualVsTarget").GetDecimal(), result.MoreActualVsTarget, "MORE ratio G57");
        Near(root.GetProperty("activeContribution").GetDecimal(), result.ActiveContribution, "active contribution D62");
        Near(root.GetProperty("moreContribution").GetDecimal(), result.MoreContribution, "more contribution D63");
        Assert.All(more.Cells, c => { Assert.Null(c.Score); Assert.Null(c.Weight); Assert.Null(c.TargetPercent); });
    }

    [Theory]
    [InlineData(1, 9, 2)] [InlineData(1, 10, 2)] [InlineData(1, 20, 1)] [InlineData(1, 30, 0)]
    [InlineData(2, 9, 3)] [InlineData(2, 20, 2)] [InlineData(2, 30, 1)] [InlineData(2, 40, 0)]
    [InlineData(3, 9, 4)] [InlineData(3, 10, 3)] [InlineData(3, 20, 2)] [InlineData(3, 30, 1)]
    [InlineData(4, 20, 3)] [InlineData(4, 30, 2)] [InlineData(4, 40, 1)] [InlineData(4, 50, 0)]
    [InlineData(5, 20, 4)] [InlineData(5, 30, 3)] [InlineData(5, 40, 2)] [InlineData(5, 50, 1)] [InlineData(5, 60, 0)]
    [InlineData(6, 9, 7)] [InlineData(6, 10, 2)] [InlineData(6, 30, 1)] [InlineData(6, 40, 0)]
    public void Movement_boundaries_are_strict_and_bucket_specific(int bucket, int paid, int ending)
        => Assert.Equal(ending, ActScoreEngine.EndingBucket(bucket, paid, [10, 10, 10, 10, 10, 10]));

    [Fact]
    public void Missing_unapproved_and_inconsistent_inputs_are_unavailable()
    {
        var (rules, input) = Example();
        Assert.Equal("UNAVAILABLE", ActScoreEngine.Calculate(Scope, null, input).Status);
        Assert.Equal("UNAVAILABLE", ActScoreEngine.Calculate(Scope, rules, null).Status);
        Assert.Contains("RULES_UNAPPROVED", ActScoreEngine.Calculate(Scope, rules with { ApprovedBy = Guid.Empty }, input).Issues);
        Assert.Contains("SOURCE_INCOMPLETE", ActScoreEngine.Calculate(Scope, rules, input with { PaymentsComplete = false }).Issues);
        Assert.Contains("SOURCE_INCOMPLETE", ActScoreEngine.Calculate(Scope, rules, input with { OpeningComplete = false }).Issues);
        Assert.Contains("NEGATIVE_STAB_TARGET_REQUIRES_APPROVAL", ActScoreEngine.Calculate(Scope, rules with { AllowNegativeStabilityTarget = false }, input).Issues);
        Assert.Contains("SIGNED_DUES_REQUIRE_APPROVAL", ActScoreEngine.Calculate(Scope, rules with { AllowSignedDues = false }, input).Issues);
        Assert.Contains("MOVEMENT_OVERRIDE_UNAPPROVED", ActScoreEngine.Calculate(Scope, rules with { AllowEndingBucketOverrides = false }, input).Issues);
        Assert.Contains("RULES_INCOMPLETE", ActScoreEngine.Calculate(Scope, rules with { Movements = rules.Movements.Skip(1).ToArray() }, input).Issues);
        Assert.Contains("WEIGHTS_INVALID", ActScoreEngine.Calculate(Scope, rules with { MoreTarget = 0 }, input).Issues);
        Assert.Contains("DUPLICATE_SOURCE", ActScoreEngine.Calculate(Scope, rules, input with { Cases = input.Cases.Append(input.Cases[0]).ToArray() }).Issues);
        Assert.Contains("DUPLICATE_SOURCE", ActScoreEngine.Calculate(Scope, rules, input with { Payments = input.Payments.Append(input.Payments[0]).ToArray() }).Issues);
    }

    [Theory]
    [InlineData("organization")] [InlineData("portfolio")] [InlineData("month")] [InlineData("currency")]
    public void Scope_is_enforced_for_rules_cases_and_payments(string field)
    {
        var (rules, input) = Example();
        var other = field switch { "organization" => Scope with { OrganizationId = Guid.NewGuid() },
            "portfolio" => Scope with { PortfolioId = Guid.NewGuid() }, "month" => Scope with { Period = new(2026, 11, 1) },
            _ => Scope with { Currency = "USD" } };
        Assert.Contains("SCOPE_MISMATCH", ActScoreEngine.Calculate(Scope, rules with { Scope = other }, input).Issues);
        Assert.Contains("SCOPE_MISMATCH", ActScoreEngine.Calculate(Scope, rules, input with { Cases = input.Cases.Select((c,i) => i == 0 ? c with { Scope = other } : c).ToArray() }).Issues);
        Assert.Contains("SCOPE_MISMATCH", ActScoreEngine.Calculate(Scope, rules, input with { Payments = input.Payments.Select((p,i) => i == 0 ? p with { Scope = other } : p).ToArray() }).Issues);
    }

    [Fact]
    public void Approval_reversal_and_month_boundaries_do_not_double_count_collections()
    {
        var (rules, input) = Example(); var p = input.Payments[0];
        var baseline = ActScoreEngine.Calculate(Scope, rules, input);
        var pending = p with { PaymentId = Guid.NewGuid(), ApprovedAt = null };
        var late = pending with { PaymentId = Guid.NewGuid(), ApprovedAt = Cutoff.AddSeconds(1) };
        var reversed = p with { PaymentId = Guid.NewGuid(), ReversedAt = Cutoff };
        var result = ActScoreEngine.Calculate(Scope, rules, input with { Payments = input.Payments.Concat([pending, late, reversed]).ToArray() });
        Assert.Equal(baseline.Paid, result.Paid); Assert.Equal(baseline.ScoreAll, result.ScoreAll);
        var laterReversal = p with { PaymentId = Guid.NewGuid(), ReversedAt = Cutoff.AddSeconds(1) };
        Assert.Equal(baseline.Paid + p.Amount, ActScoreEngine.Calculate(Scope, rules, input with { Payments = input.Payments.Append(laterReversal).ToArray() }).Paid);
        Assert.Contains("PAYMENT_INVALID", ActScoreEngine.Calculate(Scope, rules, input with { Payments = [p with { PaymentDate = new(2026, 11, 1) }] }).Issues);
        Assert.Contains("PAYMENT_INVALID", ActScoreEngine.Calculate(Scope, rules, input with { Payments = [p with { CaseId = Guid.NewGuid() }] }).Issues);
    }

    [Fact]
    public void Uncapped_final_score_and_BKT1_all_forward_exception_are_separate()
    {
        var (rules, input) = Example();
        rules = rules with { Movements = rules.Movements.Select(r => r.Bucket == 2 && r.Movement == "NORM" ? r with { Target = 0.000001m } : r).ToArray() };
        var result = ActScoreEngine.Calculate(Scope, rules, input);
        var norm = result.Buckets[1].Cells.Single(c => c.Category == "NORM");
        Assert.Equal(2m, norm.ActualVsTarget); Assert.True(norm.FinalScore > norm.Score);
        var allForward = input with { Cases = input.Cases.Select(c => c.Bucket == 1 ? c with { Dues = [0, 10000000, 0, 0, 0, 0] } : c).ToArray() };
        var forward = ActScoreEngine.Calculate(Scope, rules, allForward).Buckets[0].Cells.Single(c => c.Category == "FWD");
        Assert.Equal(0m, forward.Score); Assert.True(forward.FinalScore > 0);
    }

    [Fact]
    public void Zero_denominators_remain_unavailable_and_signed_values_are_preserved()
    {
        var (rules, input) = Example();
        var result = ActScoreEngine.Calculate(Scope, rules, input);
        Assert.True(result.Buckets[4].Cells.Single(c => c.Category == "STAB").TargetPercent < 0);
        Assert.True(result.Buckets[0].Cells.Single(c => c.Category == "FWD").ActualBalance < 0);
        var zero = input with { Cases = input.Cases.Select(c => c with { Balance = 0, StartingDues = 0 }).ToArray() };
        result = ActScoreEngine.Calculate(Scope, rules, zero);
        Assert.Equal("PARTIAL", result.Status); Assert.Null(result.ScoreAll); Assert.Null(result.MoreScore);
        Assert.Contains("MORE_DENOMINATOR_ZERO", result.Issues);
    }

    [Fact]
    public void Rules_are_per_scope_and_reference_corrections_are_never_implicit()
    {
        var (rules, input) = Example();
        var reference = ActScoreEngine.Calculate(Scope, rules, input);
        var withoutCorrections = input with { Cases = input.Cases.Select(c => c with { EndingOverride = null }).ToArray() };
        Assert.NotEqual(reference.ScoreAll, ActScoreEngine.Calculate(Scope, rules, withoutCorrections).ScoreAll);
        var custom = rules with { MoreTarget = rules.MoreTarget * 2 };
        var calculated = ActScoreEngine.Calculate(Scope, custom, input);
        Assert.Equal(reference.MoreScore / 2, calculated.MoreScore);
        Assert.NotEqual(reference.ScoreAll, calculated.ScoreAll);
        var other = Scope with { OrganizationId = Guid.NewGuid(), PortfolioId = Guid.NewGuid(), Period = new(2027, 3, 1) };
        var cutoff = new DateTimeOffset(2027, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var rebound = input with { Scope = other, Cutoff = cutoff,
            Cases = input.Cases.Select(c => c with { Scope = other }).ToArray(),
            Payments = input.Payments.Select(p => p with { Scope = other, PaymentDate = new(2027,3,31), ApprovedAt = cutoff.AddDays(-1) }).ToArray() };
        Assert.Equal(reference.ScoreAll, ActScoreEngine.Calculate(other, rules with { Scope = other }, rebound).ScoreAll);
        Assert.Equal(0, ActScoreEngine.EndingBucket(2, 522.5699999999999m, [31.55m, 460.97m, 30.05m, 0, 0, 0], 8));
        Assert.Equal(1, ActScoreEngine.EndingBucket(2, 522.56m, [31.55m, 460.97m, 30.05m, 0, 0, 0], 8));
    }
}
