using MIS.Application.DTOs.Collections;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionScoreCalculatorTests
{
    private static CollectionScoreCellDto Setting(string category, decimal? target, decimal? weight) =>
        new(category, 999, 999, target, 999, 999, 999, weight, 999, 999);

    private static CollectionScoreCalculator.CaseInput[] Cases(int bucket,
        params (string Movement, int Count, decimal Balance)[] rows) => rows.SelectMany(row =>
            Enumerable.Range(0, row.Count).Select(index => new CollectionScoreCalculator.CaseInput(
                bucket, index == 0 ? row.Balance : 0, row.Movement, 100, 0))).ToArray();

    [Fact]
    public void Bkt1_matches_reference_and_does_not_score_norm_or_stab()
    {
        var inputs = Cases(1, ("STAB", 52, 2132520), ("NORM", 21, 863317), ("FWD", 74, 3912994));
        var result = CollectionScoreCalculator.Calculate("BKT1", "v", [Setting("STAB", 99, 99), Setting("NORM", 10, null), Setting("FWD", 45, 10)], inputs, true);
        Assert.Empty(result.Issues!);
        Assert.Equal(147, result.CaseCount);
        Assert.Equal(6908831m, result.ActualBalance);
        Assert.Equal(10m, result.Weight);
        Assert.Equal(7.95m, decimal.Round(result.Score!.Value, 2));
        var fwd = result.Categories.Single(item => item.Category == "FWD");
        Assert.Equal(74, fwd.CaseCount);
        Assert.Equal(-3912994m, fwd.ActualBalance);
        Assert.Equal(-3108973.95m, fwd.TargetBalance);
        Assert.Equal(-804020.05m, fwd.Missing);
        Assert.Equal(79.45m, decimal.Round(fwd.ActualVsTarget!.Value, 2));
        Assert.Null(result.Categories.Single(item => item.Category == "NORM").Score);
        Assert.Equal(45m, result.Categories.Single(item => item.Category == "STAB").TargetPercent);
    }

    [Theory]
    [InlineData(2, 57, 1061469, 48, 846147, 61, 413428, 177, 4241569, 20, 10, 40, 9, 4, 17, 18.84)]
    [InlineData(3, 42, 483721, 29, 213574, 23, 380048, 202, 3533089, 15, 10, 55, 9, 3, 16, 16.74)]
    [InlineData(4, 15, 184542, 40, 635893, 10, 71456, 306, 5217270, 10, 5, 80, 7, 3, 6, 13.61)]
    [InlineData(5, 3, 43181, 9, 127881, 18, 29961, 243, 2154490, 10, 5, 90, 7, 3, 6, 10.47)]
    public void Remaining_reference_buckets_match_counts_scores_and_signed_variances(int bucket,
        int stabCount, decimal stab, int bwdCount, decimal bwd, int normCount, decimal norm, int fwdCount, decimal fwd,
        decimal bwdTarget, decimal normTarget, decimal fwdTarget, decimal bwdWeight, decimal normWeight, decimal fwdWeight, decimal expected)
    {
        var result = CollectionScoreCalculator.Calculate($"BKT{bucket}", "v",
            [Setting("STAB", null, null), Setting("BWD", bwdTarget, bwdWeight), Setting("NORM", normTarget, normWeight), Setting("FWD", fwdTarget, fwdWeight)],
            Cases(bucket, ("STAB", stabCount, stab), ("BWD", bwdCount, bwd), ("NORM", normCount, norm), ("FWD", fwdCount, fwd)), true);
        Assert.Empty(result.Issues!);
        Assert.Equal(stabCount + bwdCount + normCount + fwdCount, result.CaseCount);
        Assert.Equal(expected, decimal.Round(result.Score!.Value, 2));
        var stable = result.Categories.Single(item => item.Category == "STAB");
        Assert.Equal(100 - bwdTarget - normTarget - fwdTarget, stable.TargetPercent);
        if (bucket == 5) { Assert.Equal(-5, stable.TargetPercent); Assert.True(stable.TargetBalance < 0); Assert.True(stable.Missing > 0); }
    }

    [Theory]
    [InlineData(1, 9, 2)] [InlineData(1, 10, 1)] [InlineData(1, 11, 0)]
    [InlineData(2, 9, 2)] [InlineData(2, 15, 1)] [InlineData(2, 16, 0)]
    [InlineData(3, 9, 3)] [InlineData(3, 10, 2)] [InlineData(3, 15, 1)]
    [InlineData(4, 19, 2)] [InlineData(5, 24, 2)]
    [InlineData(6, 9, 2)] [InlineData(6, 15, 1)] [InlineData(6, 16, 0)]
    public void Movement_thresholds_preserve_workbook_variants_and_equality(int bucket, decimal payment, int expected) =>
        Assert.Equal(expected, CollectionScoreCalculator.EndingBucket(bucket, payment, [1, 5, 5, 5, 5, 5]));

    [Fact]
    public void All_forward_bkt1_has_zero_score_and_missing_data_never_becomes_zero()
    {
        CollectionScoreCellDto[] settings = [Setting("STAB", null, null), Setting("NORM", 10, null), Setting("FWD", 45, 10)];
        var forward = CollectionScoreCalculator.Calculate("BKT1", "v", settings, Cases(1, ("FWD", 3, 100)), true);
        Assert.Equal(0m, forward.Score);
        Assert.Equal(45m, forward.Categories.Single(item => item.Category == "FWD").ActualVsTarget);
        var missing = CollectionScoreCalculator.Calculate("BKT1", "v", settings, [], false);
        Assert.Null(missing.CaseCount); Assert.Null(missing.Score);
        Assert.All(missing.Categories, item => { Assert.Null(item.ActualBalance); Assert.Null(item.CaseCount); Assert.Null(item.Score); });
        Assert.Contains("MISSING_PERIOD_SOURCE", missing.Issues!);
    }

    [Fact]
    public void More_uses_starting_dues_and_uncapped_payment_ratio()
    {
        var result = CollectionScoreCalculator.Calculate("BKT6", "v", [Setting("MORE", 4, null)],
            [new(6, 999, null, 7703950.2564m, 67257)], true);
        Assert.Equal(21.825491m, decimal.Round(result.Score!.Value, 6));
        var high = CollectionScoreCalculator.Calculate("BKT6", "v", [Setting("MORE", 4, null)], [new(6, 1000, null, 100, 50)], true);
        Assert.Equal(1250m, high.Score);
    }

    [Fact]
    public void Unknown_opening_bucket_does_not_produce_partial_totals()
    {
        var result = CollectionScoreCalculator.Calculate("BKT2", "v",
            [Setting("STAB", null, null), Setting("BWD", 20, 9), Setting("NORM", 10, 4), Setting("FWD", 40, 17)],
            [new(null, 100, null, 10, 0), new(2, 100, "STAB", 10, 10)], true);
        Assert.Null(result.CaseCount); Assert.Null(result.ActualBalance); Assert.Null(result.Score);
        Assert.Contains("MISSING_OPENING_BUCKET", result.Issues!);
    }

    [Fact]
    public void Ratios_are_capped_before_scoring_and_zero_denominators_are_explicit()
    {
        CollectionScoreCellDto[] settings = [Setting("STAB", null, null), Setting("BWD", 1, 9), Setting("NORM", 1, 4), Setting("FWD", 40, 17)];
        var high = CollectionScoreCalculator.Calculate("BKT2", "v", settings,
            Cases(2, ("BWD", 1, 50), ("NORM", 1, 49), ("FWD", 1, 1)), true);
        Assert.Equal(200m, high.Categories.Single(item => item.Category == "BWD").ActualVsTarget);
        Assert.Equal(18m, high.Categories.Single(item => item.Category == "BWD").Score);
        Assert.Equal(200m, high.Categories.Single(item => item.Category == "FWD").ActualVsTarget);
        Assert.Equal(34m, high.Categories.Single(item => item.Category == "FWD").Score);
        var zero = CollectionScoreCalculator.Calculate("BKT2", "v", settings, Cases(2, ("STAB", 1, 100)), true);
        Assert.Null(zero.Categories.Single(item => item.Category == "FWD").Score);
        Assert.Contains("ZERO_RATIO_BASE", zero.Issues!);
    }
}
