using MIS.Application.Common;
using MIS.Application.DTOs.Collections;

namespace MIS.Application.Scoring;

/// <summary>Configuration validation only. Does not change or execute financial formulas.</summary>
public static class BankTargetPolicy
{
    // Exact user-provided template; applying it to a scope is always an explicit choice.
    public static BankTargetValues ReferenceTemplate() => new([
        new(1, "FWD", 45, 10), new(1, "NORM", 10, 0),
        new(2, "FWD", 40, 17), new(2, "BWD", 20, 9), new(2, "NORM", 10, 4),
        new(3, "FWD", 55, 16), new(3, "BWD", 15, 9), new(3, "NORM", 10, 3),
        new(4, "FWD", 80, 6), new(4, "BWD", 10, 7), new(4, "NORM", 5, 3),
        new(5, "FWD", 90, 6), new(5, "BWD", 10, 7), new(5, "NORM", 5, 3)
    ], 4, 100, 90, 10, 20, 40);

    public static void Validate(BankTargetValues? values, bool approval)
    {
        if (values?.Rows is null || values.Rows.Any(x => x is null))
            throw new HrValidationException("TARGETS_VALUES_REQUIRED");
        var expected = ReferenceTemplate().Rows.Select(x => (x.Bucket, x.Movement)).ToHashSet();
        if (values.Rows.Count != expected.Count || !values.Rows.Select(x => (x.Bucket, x.Movement)).ToHashSet().SetEquals(expected))
            throw new HrValidationException("TARGETS_ROWS_INVALID");
        foreach (var number in values.Rows.SelectMany(x => new[] { x.TargetPercent, x.Weight })
            .Concat([values.MoreTargetPercent, values.MoreWeight, values.ActiveClassWeight, values.MoreClassWeight,
                values.WOffBelow90, values.WOffAbove90]))
            if (number < 0 || number > 100 || decimal.Round(number, 2) != number)
                throw new HrValidationException("TARGETS_NUMBER_INVALID");
        if (!approval) return;
        if (values.Rows.Sum(x => x.Weight) != 100) throw new HrValidationException("TARGETS_TOTAL_MUST_BE_100");
        if (values.ActiveClassWeight + values.MoreClassWeight != 100) throw new HrValidationException("TARGETS_CLASSES_MUST_BE_100");
        if (values.Rows.Any(x => x.TargetPercent <= 0) || values.MoreTargetPercent <= 0)
            throw new HrValidationException("TARGETS_PERCENT_MUST_BE_POSITIVE");
        if (values.MoreWeight != 100) throw new HrValidationException("TARGETS_MORE_WEIGHT_MUST_BE_100");
    }

    // Match an independently approved ACT snapshot; never replace that snapshot's rules or facts.
    public static bool Matches(BankTargetValues values, ActScoreRules rules) =>
        rules.Movements is not null && values.MoreWeight == 100 &&
        rules.MoreTarget == values.MoreTargetPercent / 100 &&
        rules.ActiveWeight == values.ActiveClassWeight / 100 && rules.MoreWeight == values.MoreClassWeight / 100 &&
        rules.Movements.Count == values.Rows.Count && values.Rows.All(row =>
            rules.Movements.Count(rule => rule is not null && rule.Bucket == row.Bucket && rule.Movement == row.Movement
                && rule.Target == row.TargetPercent / 100 && rule.Weight == row.Weight) == 1);
}
