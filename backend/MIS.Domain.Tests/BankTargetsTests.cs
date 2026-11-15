using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Application.Scoring;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class BankTargetsTests
{
    [Fact]
    public void Exact_reference_totals_100_and_is_preserved_without_normalization()
    {
        var template = BankTargetPolicy.ReferenceTemplate();
        Assert.Equal(14, template.Rows.Count);
        Assert.Equal(100, template.Rows.Sum(x => x.Weight));
        Assert.Equal(0, template.Rows.Single(x => x.Bucket == 1 && x.Movement == "NORM").Weight);
        Assert.Equal(4, template.MoreTargetPercent); Assert.Equal(100, template.MoreWeight);
        Assert.Equal(90, template.ActiveClassWeight); Assert.Equal(10, template.MoreClassWeight);
        Assert.Equal(20, template.WOffBelow90); Assert.Equal(40, template.WOffAbove90);
        BankTargetPolicy.Validate(template, false);
        BankTargetPolicy.Validate(template, true);
        Assert.Equal("TARGETS_TOTAL_MUST_BE_100", Assert.Throws<HrValidationException>(() => BankTargetPolicy.Validate(template with { Rows = template.Rows.Select((r, i) => i == 0 ? r with { Weight = 11 } : r).ToArray() }, true)).Message);
    }

    [Fact]
    public void Validation_rejects_duplicates_missing_rows_precision_ranges_and_invalid_class_totals()
    {
        var valid = ValidValues(); BankTargetPolicy.Validate(valid, true);
        Assert.Throws<HrValidationException>(() => BankTargetPolicy.Validate(valid with { Rows = valid.Rows.Skip(1).ToArray() }, false));
        Assert.Throws<HrValidationException>(() => BankTargetPolicy.Validate(valid with { Rows = valid.Rows.Select(_ => valid.Rows[0]).ToArray() }, false));
        foreach (var value in new[] { -1m, 101m, 0.001m })
            Assert.Throws<HrValidationException>(() => BankTargetPolicy.Validate(valid with { MoreTargetPercent = value }, false));
        Assert.Throws<HrValidationException>(() => BankTargetPolicy.Validate(valid with { ActiveClassWeight = 89 }, true));
        Assert.Throws<HrValidationException>(() => BankTargetPolicy.Validate(valid with { MoreWeight = 99 }, true));
        Assert.Throws<HrValidationException>(() => BankTargetPolicy.Validate(valid with { MoreTargetPercent = 0 }, true));
    }

    [Fact]
    public async Task Reading_missing_targets_does_not_apply_a_template_or_write_records()
    {
        await using var f = await Fixture.Create();
        var result = await f.Get();
        Assert.Null(result.Configuration); Assert.Null(result.Scores.Active); Assert.Null(result.Scores.More);
        Assert.Contains("TARGETS_MISSING", result.Scores.Issues);
        Assert.Empty(await f.Db.BankTargetConfigurations.ToListAsync()); Assert.Empty(await f.Db.BankTargetAudits.ToListAsync());
    }

    [Fact]
    public async Task Draft_review_approval_is_versioned_audited_and_approved_history_is_immutable()
    {
        await using var f = await Fixture.Create();
        var draft = (await f.Save()).Configuration!;
        Assert.Equal("DRAFT", draft.Status);
        await Assert.ThrowsAsync<HrConflictException>(() => f.Review(draft.Version, approve: true));
        var reviewed = (await f.Review(draft.Version)).Configuration!;
        Assert.Equal("REVIEWED", reviewed.Status); Assert.NotEqual(draft.Version, reviewed.Version);
        await Assert.ThrowsAsync<HrConflictException>(() => f.Save(draft.Version));
        var edited = (await f.Save(reviewed.Version)).Configuration!;
        Assert.Equal("DRAFT", edited.Status); Assert.Null(edited.ReviewedBy);
        reviewed = (await f.Review(edited.Version)).Configuration!;
        var approved = (await f.Review(reviewed.Version, approve: true)).Configuration!;
        Assert.Equal("APPROVED", approved.Status); Assert.NotNull(approved.ApprovedAt);
        await Assert.ThrowsAsync<HrConflictException>(() => f.Save(approved.Version));
        await Assert.ThrowsAsync<HrConflictException>(() => f.Service.CopyTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default));
        Assert.Equal(5, (await f.Get()).History.Count);
        Assert.Empty(await f.Db.CollectionScoreValues.ToListAsync()); Assert.Empty(await f.Db.ActScorePeriods.ToListAsync());
    }

    [Fact]
    public async Task Copy_requires_previous_calendar_month_approved_same_scope_and_creates_only_a_draft()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<HrNotFoundException>(() => f.Copy("2026-11"));
        var draft = (await f.Save()).Configuration!;
        await Assert.ThrowsAsync<HrNotFoundException>(() => f.Copy("2026-11"));
        var reviewed = (await f.Review(draft.Version)).Configuration!;
        var approved = (await f.Review(reviewed.Version, true)).Configuration!;
        var copy = (await f.Copy("2026-11")).Configuration!;
        Assert.Equal("DRAFT", copy.Status); Assert.Equal(approved.Id, copy.CopiedFromId);
        Assert.Null(copy.ApprovedBy); Assert.Null(copy.ReviewedBy); Assert.NotEqual(approved.Version, copy.Version);
        Assert.Equal(JsonSerializer.Serialize(approved.Values), JsonSerializer.Serialize(copy.Values));
        await Assert.ThrowsAsync<HrConflictException>(() => f.Copy("2026-11"));
        await Assert.ThrowsAsync<HrNotFoundException>(() => f.Copy("2027-01"));
        Assert.Equal("APPROVED", (await f.Get()).Configuration!.Status);
    }

    [Fact]
    public async Task Scope_is_independent_for_organizations_portfolios_types_and_months()
    {
        await using var f = await Fixture.Create();
        await f.Save();
        var otherOrg = new ClientOrganization("OTHER", "Other", "Other", "CONSUMER_FINANCE", DateTimeOffset.UtcNow);
        var other = new CollectionPortfolio(otherOrg.Id, "ACT", "Other", "Other", "EGP", DateTimeOffset.UtcNow, "ACT", "LOAN");
        var wo = new CollectionPortfolio(f.Org.Id, "WO", "WO", "WO", "EGP", DateTimeOffset.UtcNow, "WO", "LOAN");
        var second = new CollectionPortfolio(f.Org.Id, "SECOND", "Second", "Second", "EGP", DateTimeOffset.UtcNow, "ACT", "LOAN");
        f.Db.AddRange(otherOrg, other, wo, second); await f.Db.SaveChangesAsync();
        foreach (var (org, type, portfolio) in new[] { (otherOrg.Id, "ACT", other.Id), (f.Org.Id, "WO", wo.Id), (f.Org.Id, "ACT", second.Id) })
        {
            Assert.Null((await f.Service.GetTargetsAsync(org, type, portfolio, "2026-10", default)).Configuration);
            await f.Service.SaveTargetsAsync(org, type, portfolio, "2026-10", new(null, ValidValues()), default);
        }
        Assert.Null((await f.Service.GetTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-11", default)).Configuration);
        await Assert.ThrowsAsync<HrNotFoundException>(() => f.Service.GetTargetsAsync(otherOrg.Id, "ACT", f.Portfolio.Id, "2026-10", default));
        await Assert.ThrowsAsync<HrNotFoundException>(() => f.Service.GetTargetsAsync(f.Org.Id, "WO", f.Portfolio.Id, "2026-10", default));
        Assert.Equal(4, await f.Db.BankTargetConfigurations.CountAsync());
    }

    [Fact]
    public async Task Scope_authorization_and_review_permission_are_enforced_in_the_service()
    {
        await using var f = await Fixture.Create();
        var userId = Guid.NewGuid();
        var denied = new CollectionScoreService(f.Db, new Principal(userId, [], []));
        await Assert.ThrowsAsync<HrNotFoundException>(() => denied.GetTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default));
        f.Db.Add(new CollectionUserAccess(userId, f.Org.Id, f.Portfolio.Id, DateTimeOffset.UtcNow)); await f.Db.SaveChangesAsync();
        var read = await denied.GetTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", default);
        Assert.False(read.CanEdit); Assert.False(read.CanApprove);
        await Assert.ThrowsAsync<HrForbiddenException>(() => denied.SaveTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", new(null, ValidValues()), default));
        var editor = new CollectionScoreService(f.Db, new Principal(userId, [], [SystemPermissionCodes.CollectionsConfigurationManage]));
        var saved = await editor.SaveTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", new(null, ValidValues()), default);
        await Assert.ThrowsAsync<HrForbiddenException>(() => editor.ReviewTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", new(saved.Configuration!.Version), false, default));
        var reviewer = new CollectionScoreService(f.Db, new Principal(Guid.NewGuid(), [SystemRoleNames.CollectionsReviewer], []));
        Assert.Equal("REVIEWED", (await reviewer.ReviewTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", new(saved.Configuration!.Version), false, default)).Configuration!.Status);
    }

    [Fact]
    public async Task Stale_save_does_not_overwrite_new_values_and_duplicate_creation_is_rejected()
    {
        await using var f = await Fixture.Create();
        var first = (await f.Save()).Configuration!;
        await f.Service.SaveTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", new(first.Version, ValidValues() with { MoreTargetPercent = 6 }), default);
        await Assert.ThrowsAsync<HrConflictException>(() => f.Save(first.Version));
        await Assert.ThrowsAsync<HrConflictException>(() => f.Save());
        Assert.Equal(6, (await f.Get()).Configuration!.Values.MoreTargetPercent);
        Assert.Equal(2, await f.Db.BankTargetAudits.CountAsync());
    }

    [Fact]
    public async Task Scores_require_approval_and_exact_matching_ACT_rules_and_do_not_change_the_source()
    {
        await using var f = await Fixture.Create(enabled: true);
        var (rules, input) = ActScoreEngineTests.Example();
        var scope = rules.Scope with { OrganizationId = f.Org.Id, PortfolioId = f.Portfolio.Id };
        rules = rules with { Scope = scope };
        input = input with { Scope = scope, Cases = input.Cases.Select(c => c with { Scope = scope }).ToArray(), Payments = input.Payments.Select(p => p with { Scope = scope }).ToArray() };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var source = new ActScorePeriod(f.Org.Id, f.Portfolio.Id, scope.Period, "EGP", JsonSerializer.Serialize(rules, options), JsonSerializer.Serialize(input, options), f.Actor, DateTimeOffset.UtcNow);
        f.Db.Add(source); await f.Db.SaveChangesAsync();
        var values = new BankTargetValues(rules.Movements.Select(r => new BankTargetRow(r.Bucket, r.Movement, r.Target * 100, r.Weight)).ToArray(), rules.MoreTarget * 100, 100, rules.ActiveWeight * 100, rules.MoreWeight * 100, 20, 40);
        var draft = await f.Service.SaveTargetsAsync(f.Org.Id, "ACT", f.Portfolio.Id, "2026-10", new(null, values), default);
        Assert.Null(draft.Scores.Active); Assert.Contains("TARGETS_NOT_APPROVED", draft.Scores.Issues);
        var reviewed = await f.Review(draft.Configuration!.Version);
        var approved = await f.Review(reviewed.Configuration!.Version, true);
        var actual = ActScoreEngine.Calculate(scope, rules, input);
        Assert.NotNull(approved.Scores.Active); Assert.Equal(actual.BalanceScore, approved.Scores.Active); Assert.Equal(actual.MoreScore, approved.Scores.More);
        Assert.True(BankTargetPolicy.Matches(values, rules));
        Assert.False(BankTargetPolicy.Matches(values with { MoreTargetPercent = values.MoreTargetPercent + 1 }, rules));
        Assert.Equal(source.RulesJson, (await f.Db.ActScorePeriods.SingleAsync()).RulesJson);
        Assert.Empty(await f.Db.CollectionScoreValues.ToListAsync());
    }

    [Fact]
    public void Missing_numeric_JSON_fields_are_rejected_instead_of_becoming_zero()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BankTargetValues>("{}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BankTargetRow>("{\"bucket\":1,\"movement\":\"FWD\",\"weight\":10}", options));
    }

    [Fact]
    public async Task Concurrent_context_cannot_save_a_stale_draft_or_append_a_success_audit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"targets-race-{Guid.NewGuid()}").Options;
        await using var first = new ApplicationDbContext(options);
        await using var second = new ApplicationDbContext(options);
        var now = DateTimeOffset.UtcNow; var actor = Guid.NewGuid();
        var org = new ClientOrganization("BANK", "Bank", "Bank", "BANK", now);
        var portfolio = new CollectionPortfolio(org.Id, "ACT", "Portfolio", "Portfolio", "EGP", now, "ACT", "LOAN");
        first.AddRange(org, portfolio); await first.SaveChangesAsync();
        var user = new Principal(actor, [SystemRoleNames.Admin], ["*"]);
        var service1 = new CollectionScoreService(first, user); var service2 = new CollectionScoreService(second, user);
        var created = await service1.SaveTargetsAsync(org.Id, "ACT", portfolio.Id, "2026-10", new(null, ValidValues()), default);
        // Track the pre-edit row in the second request before the first request saves.
        await second.BankTargetConfigurations.SingleAsync();
        await service1.SaveTargetsAsync(org.Id, "ACT", portfolio.Id, "2026-10", new(created.Configuration!.Version, ValidValues() with { MoreTargetPercent = 7 }), default);
        await Assert.ThrowsAsync<HrConflictException>(() => service2.SaveTargetsAsync(org.Id, "ACT", portfolio.Id, "2026-10", new(created.Configuration.Version, ValidValues()), default));
        Assert.Equal(7, (await service1.GetTargetsAsync(org.Id, "ACT", portfolio.Id, "2026-10", default)).Configuration!.Values.MoreTargetPercent);
    }

    [Fact]
    public void Relational_mapping_enforces_unique_scope_and_concurrent_updates()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=ModelCheck;Username=unused;Password=unused").Options);
        var entity = db.Model.FindEntityType(typeof(BankTargetConfiguration))!;
        Assert.True(entity.FindProperty(nameof(BankTargetConfiguration.Version))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "OrganizationId", "PortfolioId", "PortfolioType", "ReportingPeriod" }));
    }

    private static BankTargetValues ValidValues() => BankTargetPolicy.ReferenceTemplate();
    private sealed record Principal(Guid UserId, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions) : ICurrentUserContext
    { public string Username => "targets-test"; }
    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"targets-{Guid.NewGuid()}").Options);
        public Guid Actor { get; } = Guid.NewGuid();
        public ClientOrganization Org { get; private set; } = null!;
        public CollectionPortfolio Portfolio { get; private set; } = null!;
        public CollectionScoreService Service { get; private set; } = null!;
        public static async Task<Fixture> Create(bool enabled = false)
        {
            var f = new Fixture(); var now = DateTimeOffset.UtcNow;
            f.Org = new("BANK", "Bank", "Bank", "BANK", now);
            f.Portfolio = new(f.Org.Id, "ACT", "Portfolio", "Portfolio", "EGP", now, "ACT", "LOAN");
            f.Db.AddRange(f.Org, f.Portfolio); await f.Db.SaveChangesAsync();
            f.Service = new(f.Db, new Principal(f.Actor, [SystemRoleNames.Admin], ["*"]), options: Options.Create(new ActScoringOptions { Enabled = enabled }));
            return f;
        }
        public Task<BankTargetsDto> Get() => Service.GetTargetsAsync(Org.Id, "ACT", Portfolio.Id, "2026-10", default);
        public Task<BankTargetsDto> Save(Guid? version = null) => Service.SaveTargetsAsync(Org.Id, "ACT", Portfolio.Id, "2026-10", new(version, ValidValues()), default);
        public Task<BankTargetsDto> Review(Guid version, bool approve = false) => Service.ReviewTargetsAsync(Org.Id, "ACT", Portfolio.Id, "2026-10", new(version), approve, default);
        public Task<BankTargetsDto> Copy(string month) => Service.CopyTargetsAsync(Org.Id, "ACT", Portfolio.Id, month, default);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
