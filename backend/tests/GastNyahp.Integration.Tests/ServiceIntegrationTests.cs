using GastNyahp.Domain.Common;
using GastNyahp.Domain.Services;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class ServiceIntegrationTests : IntegrationTest
{
    static readonly Guid ServiceId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    Task RegisterArs(decimal baseAmount = 35000m) => Ok(Host.Services.Handle(
        new RegisterService(ServiceId, FamilyId, "Electricidad", "Electricidad", BillingType.Monthly, null,
            ServiceCurrency.Ars, baseAmount, "2026-01", OwnerRef.None, UsdRateCcl: 0), default));

    [Fact]
    public async Task Register_projects_service_with_12_month_amounts()
    {
        await RegisterArs();

        await using var db = Host.Db();
        var service = Assert.Single(await db.Services.ToListAsync());
        Assert.True(service.Active);

        var amounts = await db.ServiceMonthAmounts.OrderBy(a => a.Month).ToListAsync();
        Assert.Equal(12, amounts.Count);
        Assert.Equal("2026-01", amounts[0].Month);
        Assert.Equal("2026-12", amounts[^1].Month);
        Assert.All(amounts, a => Assert.Equal(35000m, a.AmountArs));
    }

    [Fact]
    public async Task Usd_service_is_stored_converted_to_ars_with_original_kept()
    {
        await Ok(Host.Services.Handle(
            new RegisterService(ServiceId, FamilyId, "Seguro auto", "Seguro", BillingType.Monthly, null,
                ServiceCurrency.Usd, 50m, "2026-01", OwnerRef.None, UsdRateCcl: 1250m), default));

        await using var db = Host.Db();
        var service = await db.Services.SingleAsync();
        Assert.Equal(50m, service.OriginalAmount);
        Assert.Equal("Usd", service.OriginalCurrency);
        Assert.All(await db.ServiceMonthAmounts.ToListAsync(), a => Assert.Equal(62500m, a.AmountArs));
    }

    [Fact]
    public async Task ExtendFutureAmounts_upserts_preserving_paid_and_extends_the_window()
    {
        await RegisterArs();
        await Ok(Host.Services.Handle(new ToggleServiceMonthPaid(ServiceId, "2026-02"), default));

        await Ok(Host.Services.Handle(new ExtendServiceFutureAmounts(ServiceId, "2026-02", 40000m, 12), default));

        await using var db = Host.Db();
        var feb = await db.ServiceMonthAmounts.SingleAsync(a => a.Month == "2026-02");
        Assert.True(feb.Paid);                 // paid flag survived the amount upsert
        Assert.Equal(40000m, feb.AmountArs);   // but the amount did update

        Assert.NotNull(await db.ServiceMonthAmounts.SingleOrDefaultAsync(a => a.Month == "2027-01")); // extended past the original 12
        Assert.Equal(35000m, (await db.ServiceMonthAmounts.SingleAsync(a => a.Month == "2026-01")).AmountArs); // before FromMonth: untouched
    }

    [Fact]
    public async Task TogglePaid_on_an_unloaded_month_creates_it_with_zero_amount()
    {
        await RegisterArs();
        await Ok(Host.Services.Handle(new ToggleServiceMonthPaid(ServiceId, "2030-06"), default));

        await using var db = Host.Db();
        var created = await db.ServiceMonthAmounts.SingleAsync(a => a.Month == "2030-06");
        Assert.True(created.Paid);
        Assert.Equal(0m, created.AmountArs);
    }

    [Fact]
    public async Task SetMonthAmount_touches_one_month_only()
    {
        await RegisterArs();
        await Ok(Host.Services.Handle(new SetServiceMonthAmount(ServiceId, "2026-03", 99999m, ServiceCurrency.Ars, 0), default));

        await using var db = Host.Db();
        Assert.Equal(99999m, (await db.ServiceMonthAmounts.SingleAsync(a => a.Month == "2026-03")).AmountArs);
        Assert.Equal(35000m, (await db.ServiceMonthAmounts.SingleAsync(a => a.Month == "2026-04")).AmountArs);
    }

    [Fact]
    public async Task UpdateDetails_and_deactivation_project_correctly()
    {
        await RegisterArs();
        await Ok(Host.Services.Handle(new UpdateServiceDetails(ServiceId, "Edesur", "Electricidad", BillingType.Bimonthly, null, ServiceCurrency.Ars), default));
        await Ok(Host.Services.Handle(new DeactivateService(ServiceId), default));

        await using var db = Host.Db();
        var service = await db.Services.SingleAsync();
        Assert.Equal("Edesur", service.Name);
        Assert.Equal("Bimonthly", service.BillingType);
        Assert.False(service.Active);
    }

    [Fact]
    public async Task Remove_cascades_to_month_amounts()
    {
        await RegisterArs();
        await Ok(Host.Services.Handle(new RemoveService(ServiceId), default));

        await using var db = Host.Db();
        Assert.Empty(await db.Services.ToListAsync());
        Assert.Empty(await db.ServiceMonthAmounts.ToListAsync());
    }
}
