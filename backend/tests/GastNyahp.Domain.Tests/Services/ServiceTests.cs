using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Services;

namespace GastNyahp.Domain.Tests.Services;

public class ServiceTests
{
    static readonly Guid ServiceId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    static RegisterService ArsService(decimal baseAmount = 35000m) =>
        new(ServiceId, FamilyId, "Electricidad", "Electricidad", BillingType.Monthly, null, ServiceCurrency.Ars, baseAmount, "2026-01", OwnerRef.None, UsdRateCcl: 0);

    [Fact]
    public void Register_generates_12_months_from_the_registration_month()
    {
        var state = new ServiceState().When(ServiceCommandService.Register(ArsService()).Single());

        Assert.Equal(12, state.Amounts.Count);
        Assert.Equal("2026-01", state.Amounts[0].Month.ToString());
        Assert.Equal("2026-12", state.Amounts[^1].Month.ToString());
        Assert.All(state.Amounts, a => Assert.Equal(35000m, a.AmountArs));
    }

    [Fact]
    public void Register_with_USD_converts_to_ARS_using_the_ccl_rate()
    {
        var cmd = new RegisterService(ServiceId, FamilyId, "Seguro", "Seguro", BillingType.Monthly, null, ServiceCurrency.Usd, 50m, "2026-01", OwnerRef.None, UsdRateCcl: 1250m);
        var e = (ServiceEvents.V1.ServiceRegistered)ServiceCommandService.Register(cmd).Single();

        Assert.Equal(62500m, e.BaseAmountArs); // round(50 * 1250)
        Assert.Equal(50m, e.OriginalAmount);
        Assert.Equal(ServiceCurrency.Usd, e.OriginalCurrency);
    }

    [Fact]
    public void Register_with_USD_and_no_ccl_rate_throws()
    {
        var cmd = new RegisterService(ServiceId, FamilyId, "Seguro", "Seguro", BillingType.Monthly, null, ServiceCurrency.Usd, 50m, "2026-01", OwnerRef.None, UsdRateCcl: 0);
        Assert.Throws<DomainException>(() => ServiceCommandService.Register(cmd).ToList());
    }

    [Fact]
    public void ExtendFutureAmounts_upserts_next_N_months_preserving_paid_flag()
    {
        var state = new ServiceState().When(ServiceCommandService.Register(ArsService()).Single());
        state = state.When(ServiceCommandService.TogglePaid(state, [], new ToggleServiceMonthPaid(ServiceId, "2026-02")).Single());

        state = state.When(ServiceCommandService.ExtendFutureAmounts(state, [],
            new ExtendServiceFutureAmounts(ServiceId, "2026-02", 40000m, 12)).Single());

        // The Paid month keeps Paid=true but gets the new amount (upsert semantics — amount always updates).
        var feb = state.Amounts.Single(a => a.Month.ToString() == "2026-02");
        Assert.True(feb.Paid);
        Assert.Equal(40000m, feb.AmountArs);

        // Extends beyond the original 12-month window too.
        Assert.Contains(state.Amounts, a => a.Month.ToString() == "2027-01");
    }

    [Fact]
    public void TogglePaid_on_a_month_with_no_amount_yet_creates_it_with_zero_amount()
    {
        var state = new ServiceState().When(ServiceCommandService.Register(ArsService()).Single());
        state = state.When(ServiceCommandService.TogglePaid(state, [], new ToggleServiceMonthPaid(ServiceId, "2030-06")).Single());

        var created = state.Amounts.Single(a => a.Month.ToString() == "2030-06");
        Assert.True(created.Paid);
        Assert.Equal(0m, created.AmountArs);
    }

    [Fact]
    public void SetMonthAmount_is_a_single_month_upsert_independent_of_others()
    {
        var state = new ServiceState().When(ServiceCommandService.Register(ArsService()).Single());
        state = state.When(ServiceCommandService.SetMonthAmount(state, [],
            new SetServiceMonthAmount(ServiceId, "2026-03", 99999m, ServiceCurrency.Ars, 0)).Single());

        Assert.Equal(99999m, state.Amounts.Single(a => a.Month.ToString() == "2026-03").AmountArs);
        Assert.Equal(35000m, state.Amounts.Single(a => a.Month.ToString() == "2026-04").AmountArs);
    }

    [Fact]
    public void Deactivate_then_activate_roundtrips()
    {
        var state = new ServiceState().When(ServiceCommandService.Register(ArsService()).Single());
        state = state.When(ServiceCommandService.Deactivate(state, [], new DeactivateService(ServiceId)).Single());
        Assert.False(state.Active);

        Assert.Throws<DomainException>(() => ServiceCommandService.Deactivate(state, [], new DeactivateService(ServiceId)).ToList());

        state = state.When(ServiceCommandService.Activate(state, [], new ActivateService(ServiceId)).Single());
        Assert.True(state.Active);
    }

    [Fact]
    public void UpdateDetails_changes_owner_when_it_is_provided()
    {
        var state = new ServiceState().When(ServiceCommandService.Register(ArsService()).Single());
        var personId = Guid.NewGuid();

        var @event = ServiceCommandService.UpdateDetails(state, [],
            new UpdateServiceDetails(ServiceId, "Electricidad", "Electricidad", BillingType.Monthly, null, ServiceCurrency.Ars, OwnerRef.Of(personId))).Single();
        state = state.When(@event);

        Assert.Equal("Owner", state.Owner.Kind);
        Assert.Equal(personId, state.Owner.PersonId);
    }

    [Fact]
    public void Replaying_an_old_details_event_preserves_owner()
    {
        var personId = Guid.NewGuid();
        var state = new ServiceState().When(ServiceCommandService.Register(ArsService() with { Owner = OwnerRef.Of(personId) }).Single());

        state = state.When(new ServiceEvents.V1.ServiceDetailsUpdated(
            "Electricidad", "Electricidad", BillingType.Monthly, null, ServiceCurrency.Ars));

        Assert.Equal(personId, state.Owner.PersonId);
    }
    [Fact]
    public void Register_with_unknown_category_throws() =>
        Assert.Throws<DomainException>(() => ServiceCommandService.Register(ArsService() with { Category = "Bogus" }).ToList());
}
