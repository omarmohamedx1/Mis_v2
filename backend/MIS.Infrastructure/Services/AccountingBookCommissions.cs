using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Domain.Constants;

namespace MIS.Infrastructure.Services;

public sealed partial class AccountingBookService
{
    public async Task<AccountingCommissionPreviewDto> PreviewCommissionsAsync(int year, int month, CancellationToken token)
    {
        EnsureAccess(); if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new HrValidationException("Provide a valid payroll year and month.");
        var from = new DateOnly(year, month, 1); var to = from.AddMonths(1).AddDays(-1);
        var payments = await AccountingCommissionQuery.Payments(db, from, to, token);
        var rules = await db.AccountingCommissionRules.AsNoTracking().Where(x => x.EffectiveFrom <= to && (x.IsActive || x.EffectiveTo != null) && (x.EffectiveTo == null || x.EffectiveTo >= to))
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).ToListAsync(token);
        var rows = new List<AccountingCommissionPreviewRowDto>(); var missing = new List<string>();
        var collectors = await db.Users.AsNoTracking().Where(x => payments.Select(p => p.CollectorId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, token);
        var collectorLocked = await db.AccountingCollectorCommissions.AsNoTracking().Include(x => x.CommissionRule).Include(x => x.CollectorUser).Where(x => x.PeriodYear == year && x.PeriodMonth == month && x.Status != "CANCELLED").ToListAsync(token);
        var collectorRule = rules.FirstOrDefault(x => x.Scope == "COLLECTOR" && x.Basis != "TIERED");
        if (collectorRule is null) missing.Add("COLLECTOR");
        else foreach (var group in payments.GroupBy(x => x.CollectorId))
        {
            var old = collectorLocked.FirstOrDefault(x => x.CollectorUserId == group.Key); var locked = old is not null && !AccountingValues.CommissionStatuses.Editable.Contains(old.Status);
            if (locked) continue;
            rows.Add(new("COLLECTOR", group.Key, collectors.GetValueOrDefault(group.Key, "—"), group.Select(x => x.CaseId).Distinct().Count(), group.Sum(x => x.Amount), collectorRule.Calculate(group.Sum(x => x.Amount)), collectorRule.Code, collectorRule.Basis, collectorRule.Percentage, collectorRule.FixedAmount, false));
        }
        foreach (var old in collectorLocked.Where(x => !AccountingValues.CommissionStatuses.Editable.Contains(x.Status)))
            rows.Add(new("COLLECTOR", old.CollectorUserId, old.CollectorUser.FullName, old.AssignedCasesCount, old.CollectedAmount, old.FinalCommission, old.CommissionRule.Code, old.CommissionRule.Basis, old.CommissionRule.Percentage, old.CommissionRule.FixedAmount, true));
        var teams = await db.CollectionTeams.AsNoTracking().Where(x => x.IsActive && x.SupervisorId != null).ToListAsync(token);
        var members = await db.CollectionTeamMembers.AsNoTracking().Where(x => x.IsActive).ToListAsync(token);
        var supervisorIds = teams.Select(x => x.SupervisorId!.Value).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(x => supervisorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, token);
        var supervisorLocked = await db.AccountingSupervisorCommissions.AsNoTracking().Include(x => x.CommissionRule).Include(x => x.SupervisorUser).Where(x => x.PeriodYear == year && x.PeriodMonth == month && x.Status != "CANCELLED").ToListAsync(token);
        var rule = rules.FirstOrDefault(x => x.Scope == "SUPERVISOR" && x.Basis != "TIERED");
        if (rule is null) missing.Add("SUPERVISOR");
        else foreach (var group in teams.GroupBy(x => x.SupervisorId!.Value))
        {
            var ids = group.Select(x => x.Id).ToHashSet(); var collectorsInTeam = members.Where(x => ids.Contains(x.TeamId)).Select(x => x.UserId).ToHashSet();
            var matching = payments.Where(x => collectorsInTeam.Contains(x.CollectorId)).ToList(); var amount = matching.Sum(x => x.Amount);
            var old = supervisorLocked.FirstOrDefault(x => x.SupervisorUserId == group.Key); var locked = old is not null && !AccountingValues.CommissionStatuses.Editable.Contains(old.Status);
            if (locked) continue;
            rows.Add(new("SUPERVISOR", group.Key, names.GetValueOrDefault(group.Key, "—"), matching.Select(x => x.CaseId).Distinct().Count(), amount, rule.Calculate(amount), rule.Code, rule.Basis, rule.Percentage, rule.FixedAmount, false));
        }
        foreach (var old in supervisorLocked.Where(x => !AccountingValues.CommissionStatuses.Editable.Contains(x.Status)))
            rows.Add(new("SUPERVISOR", old.SupervisorUserId, old.SupervisorUser.FullName, old.CollectorCount, old.TeamCollectedAmount, old.FinalCommission, old.CommissionRule.Code, old.CommissionRule.Basis, old.CommissionRule.Percentage, old.CommissionRule.FixedAmount, true));
        return new(year, month, payments.Sum(x => x.Amount), rows, missing);
    }
}
