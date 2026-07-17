using GastNyahp.Domain.Common;
using GastNyahp.Domain.Installments;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class InstallmentIntegrationTests : IntegrationTest
{
    static readonly Guid InstallmentId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid CardId = Guid.NewGuid();

    Task RegisterFixed(int total = 12, decimal amount = 85000m) => Ok(Host.Installments.Handle(
        new RegisterInstallmentPurchase(InstallmentId, FamilyId, CardId, "Smart TV 55\"", "Hogar", "2025-10-05",
            InstallmentFrequency.Fixed, amount, total, "2025-10", OwnerRef.None), default));

    [Fact]
    public async Task Register_fixed_projects_parent_row_plus_calendar()
    {
        await RegisterFixed(12);

        await using var db = Host.Db();
        var parent = Assert.Single(await db.InstallmentPurchases.ToListAsync());
        Assert.Equal("Smart TV 55\"", parent.Description);
        Assert.True(parent.Active);

        var months = await db.InstallmentMonths.OrderBy(m => m.Month).ToListAsync();
        Assert.Equal(12, months.Count);
        Assert.Equal("2025-10", months[0].Month);
        Assert.Equal("2026-09", months[^1].Month);
        Assert.All(months, m => Assert.Equal(85000m, m.Amount));
        Assert.All(months, m => Assert.False(m.Paid));
    }

    [Fact]
    public async Task Register_monthly_recurring_projects_24_month_window()
    {
        await Ok(Host.Installments.Handle(
            new RegisterInstallmentPurchase(InstallmentId, FamilyId, CardId, "Plan celular", "Servicios", "2026-01-01",
                InstallmentFrequency.Monthly, 45000m, null, "2026-01", OwnerRef.None), default));

        await using var db = Host.Db();
        Assert.Equal(24, await db.InstallmentMonths.CountAsync());
    }

    [Fact]
    public async Task TogglePaid_flips_a_single_month_and_toggling_back_restores_it()
    {
        await RegisterFixed(3);
        await Ok(Host.Installments.Handle(new ToggleInstallmentMonthPaid(InstallmentId, "2025-10"), default));

        await using (var db = Host.Db())
        {
            Assert.True((await db.InstallmentMonths.SingleAsync(m => m.Month == "2025-10")).Paid);
            Assert.False((await db.InstallmentMonths.SingleAsync(m => m.Month == "2025-11")).Paid);
        }

        await Ok(Host.Installments.Handle(new ToggleInstallmentMonthPaid(InstallmentId, "2025-10"), default));

        await using (var db = Host.Db())
            Assert.False((await db.InstallmentMonths.SingleAsync(m => m.Month == "2025-10")).Paid);
    }

    [Fact]
    public async Task OverrideMonthAmount_changes_one_cell_only()
    {
        await RegisterFixed(3);
        await Ok(Host.Installments.Handle(new OverrideInstallmentMonthAmount(InstallmentId, "2025-11", 99000m), default));

        await using var db = Host.Db();
        Assert.Equal(99000m, (await db.InstallmentMonths.SingleAsync(m => m.Month == "2025-11")).Amount);
        Assert.Equal(85000m, (await db.InstallmentMonths.SingleAsync(m => m.Month == "2025-10")).Amount);
    }

    [Fact]
    public async Task ReviseSchedule_replaces_calendar_preserving_paid_months_and_their_amounts()
    {
        await RegisterFixed(4);
        await Ok(Host.Installments.Handle(new ToggleInstallmentMonthPaid(InstallmentId, "2025-10"), default));
        await Ok(Host.Installments.Handle(new OverrideInstallmentMonthAmount(InstallmentId, "2025-11", 90000m), default));
        await Ok(Host.Installments.Handle(new ToggleInstallmentMonthPaid(InstallmentId, "2025-11"), default));

        await Ok(Host.Installments.Handle(
            new ReviseInstallmentSchedule(InstallmentId, "2025-10", 6, InstallmentFrequency.Fixed, 100000m), default));

        await using var db = Host.Db();
        var months = await db.InstallmentMonths.OrderBy(m => m.Month).ToListAsync();
        Assert.Equal(6, months.Count);

        var oct = months.Single(m => m.Month == "2025-10");
        var nov = months.Single(m => m.Month == "2025-11");
        var dec = months.Single(m => m.Month == "2025-12");
        Assert.True(oct.Paid); Assert.Equal(85000m, oct.Amount);   // historical amount preserved
        Assert.True(nov.Paid); Assert.Equal(90000m, nov.Amount);   // per-month override preserved too
        Assert.False(dec.Paid); Assert.Equal(100000m, dec.Amount); // unpaid months take the new base

        var parent = await db.InstallmentPurchases.SingleAsync();
        Assert.Equal(6, parent.TotalInstallments);
        Assert.Equal(100000m, parent.MonthlyAmount);
    }

    [Fact]
    public async Task Finish_marks_inactive_but_keeps_the_calendar()
    {
        await RegisterFixed(3);
        await Ok(Host.Installments.Handle(new FinishInstallment(InstallmentId), default));

        await using var db = Host.Db();
        Assert.False((await db.InstallmentPurchases.SingleAsync()).Active);
        Assert.Equal(3, await db.InstallmentMonths.CountAsync());
    }

    [Fact]
    public async Task Remove_cascades_to_the_month_rows()
    {
        await RegisterFixed(12);
        await Ok(Host.Installments.Handle(new RemoveInstallmentPurchase(InstallmentId), default));

        await using var db = Host.Db();
        Assert.Empty(await db.InstallmentPurchases.ToListAsync());
        Assert.Empty(await db.InstallmentMonths.ToListAsync());
    }
}
