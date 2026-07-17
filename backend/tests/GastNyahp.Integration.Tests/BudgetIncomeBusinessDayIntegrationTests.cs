using GastNyahp.Domain.Budgets;
using GastNyahp.Domain.BusinessDays;
using GastNyahp.Domain.Income;
using GastNyahp.Infrastructure.Projections.Income;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class BudgetPlanIntegrationTests : IntegrationTest
{
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public async Task Set_creates_the_month_row_and_resetting_updates_it_in_place()
    {
        await Ok(Host.Budgets.Handle(new SetBudgetLimits(FamilyId, "2026-02", 480000m, 316000m, 200000m), default));
        await Ok(Host.Budgets.Handle(new SetBudgetLimits(FamilyId, "2026-02", 500000m, 316000m, 200000m), default));

        await using var db = Host.Db();
        var plan = Assert.Single(await db.BudgetPlans.ToListAsync());
        Assert.Equal("2026-02", plan.Month);
        Assert.Equal(500000m, plan.CreditLimit);
    }

    [Fact]
    public async Task Each_month_gets_its_own_stream_and_row()
    {
        await Ok(Host.Budgets.Handle(new SetBudgetLimits(FamilyId, "2026-02", 480000m, 0, 0), default));
        await Ok(Host.Budgets.Handle(new SetBudgetLimits(FamilyId, "2026-03", 490000m, 0, 0), default));

        await using var db = Host.Db();
        Assert.Equal(2, await db.BudgetPlans.CountAsync());
    }
}

public class IncomeIntegrationTests : IntegrationTest
{
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public async Task First_update_creates_the_singleton_and_a_history_row()
    {
        await Ok(Host.Income.Handle(new UpdateIncome(FamilyId, 500000m, 1050m, 1250m, 70), default));

        await using var db = Host.Db();
        var income = Assert.Single(await db.Income.ToListAsync());
        Assert.Equal(FamilyId, income.FamilyId); // one singleton row PER FAMILY now
        Assert.Equal(500000m, income.NetMonthly);
        Assert.Single(await db.IncomeHistory.ToListAsync());
    }

    [Fact]
    public async Task Partial_update_merges_into_the_singleton_and_appends_merged_history()
    {
        await Ok(Host.Income.Handle(new UpdateIncome(FamilyId, 500000m, 1050m, 1250m, 70), default));
        await Ok(Host.Income.Handle(new UpdateIncome(FamilyId, 600000m, null, null, null), default));

        await using var db = Host.Db();
        var income = await db.Income.SingleAsync();
        Assert.Equal(600000m, income.NetMonthly);
        Assert.Equal(1250m, income.UsdRateCcl); // untouched by the partial update

        var history = await db.IncomeHistory.OrderBy(h => h.SequenceNumber).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(500000m, history[0].NetMonthly);
        Assert.Equal(600000m, history[1].NetMonthly);
        Assert.Equal(1250m, history[1].UsdRateCcl); // history stores the RESULTING merged values
    }
}

public class BusinessDayIntegrationTests : IntegrationTest
{
    [Fact]
    public async Task Open_projects_the_day_row()
    {
        await Ok(Host.BusinessDays.Handle(new OpenBusinessDay("2026-07-09"), default));

        await using var db = Host.Db();
        var day = Assert.Single(await db.BusinessDays.ToListAsync());
        Assert.Equal("2026-07-09", day.Date);
    }

    [Fact]
    public async Task Opening_the_same_date_twice_fails_and_keeps_one_row()
    {
        // The daily IHostedService idempotency guarantee (DOMAIN_MODEL.md §13.1): a container restart that
        // re-runs OpenBusinessDay for today must fail cleanly, not double-open the day.
        await Ok(Host.BusinessDays.Handle(new OpenBusinessDay("2026-07-09"), default));
        await Fails(Host.BusinessDays.Handle(new OpenBusinessDay("2026-07-09"), default));

        await using var db = Host.Db();
        Assert.Single(await db.BusinessDays.ToListAsync());
    }

    [Fact]
    public async Task Different_dates_open_independently()
    {
        await Ok(Host.BusinessDays.Handle(new OpenBusinessDay("2026-07-09"), default));
        await Ok(Host.BusinessDays.Handle(new OpenBusinessDay("2026-07-10"), default));

        await using var db = Host.Db();
        Assert.Equal(2, await db.BusinessDays.CountAsync());
    }
}
