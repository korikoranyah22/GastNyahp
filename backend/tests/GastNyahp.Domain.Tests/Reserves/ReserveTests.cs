using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Reserves;

namespace GastNyahp.Domain.Tests.Reserves;

public class ReserveTests
{
    static readonly Guid ReserveId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void NonRecurring_reserve_with_no_override_has_zero_effective_amount()
    {
        var state = new ReserveState().When(ReserveCommandService.Register(
            new RegisterReserve(ReserveId, FamilyId, "Reserva A", ReserveType.Reserve, "👤", Recurring: false, BaseAmount: 0)).Single());

        Assert.Equal(0m, state.EffectiveAmount(YearMonth.Parse("2026-02")));
    }

    [Fact]
    public void Recurring_reserve_falls_back_to_BaseAmount_when_no_override()
    {
        var state = new ReserveState().When(ReserveCommandService.Register(
            new RegisterReserve(ReserveId, FamilyId, "Efectivo", ReserveType.Cash, "💵", Recurring: true, BaseAmount: 100000m)).Single());

        Assert.Equal(100000m, state.EffectiveAmount(YearMonth.Parse("2026-02")));
        Assert.Equal(100000m, state.EffectiveAmount(YearMonth.Parse("2099-12"))); // recurring applies to any month
    }

    [Fact]
    public void Month_override_takes_priority_over_recurring_BaseAmount()
    {
        var state = new ReserveState().When(ReserveCommandService.Register(
            new RegisterReserve(ReserveId, FamilyId, "Efectivo", ReserveType.Cash, "💵", Recurring: true, BaseAmount: 100000m)).Single());
        state = state.When(ReserveCommandService.SetMonthAmount(state, [], new SetReserveMonthAmount(ReserveId, "2026-02", 50000m, "ajuste puntual")).Single());

        Assert.Equal(50000m, state.EffectiveAmount(YearMonth.Parse("2026-02")));
        Assert.Equal(100000m, state.EffectiveAmount(YearMonth.Parse("2026-03"))); // other months unaffected
    }

    [Fact]
    public void ApplyBaseToAllMonths_clears_every_existing_override()
    {
        var state = new ReserveState().When(ReserveCommandService.Register(
            new RegisterReserve(ReserveId, FamilyId, "Reserva A", ReserveType.Reserve, "👤", Recurring: false, BaseAmount: 0)).Single());
        state = state.When(ReserveCommandService.SetMonthAmount(state, [], new SetReserveMonthAmount(ReserveId, "2026-01", 1000m, null)).Single());
        state = state.When(ReserveCommandService.SetMonthAmount(state, [], new SetReserveMonthAmount(ReserveId, "2026-02", 2000m, null)).Single());
        Assert.Equal(2, state.Months.Count);

        state = state.When(ReserveCommandService.ApplyBase(state, [], new ApplyReserveBaseToAllMonths(ReserveId, 5000m)).Single());

        Assert.Empty(state.Months);
        Assert.True(state.Recurring);
        Assert.Equal(5000m, state.EffectiveAmount(YearMonth.Parse("2026-01")));
        Assert.Equal(5000m, state.EffectiveAmount(YearMonth.Parse("2099-01")));
    }

    [Fact]
    public void Register_recurring_without_positive_baseAmount_throws() =>
        Assert.Throws<DomainException>(() =>
            ReserveCommandService.Register(new RegisterReserve(ReserveId, FamilyId, "Efectivo", ReserveType.Cash, "💵", Recurring: true, BaseAmount: 0)).ToList());

    [Fact]
    public void SetMonthAmount_upserts_note_alongside_amount()
    {
        var state = new ReserveState().When(ReserveCommandService.Register(
            new RegisterReserve(ReserveId, FamilyId, "Cami", ReserveType.Reserve, "👤", Recurring: false, BaseAmount: 0)).Single());
        state = state.When(ReserveCommandService.SetMonthAmount(state, [], new SetReserveMonthAmount(ReserveId, "2026-02", 30000m, "Facu + Médica")).Single());

        var entry = state.Months.Single(m => m.Month.ToString() == "2026-02");
        Assert.Equal("Facu + Médica", entry.Note);

        // Re-setting the same month upserts (updates), doesn't add a duplicate entry.
        state = state.When(ReserveCommandService.SetMonthAmount(state, [], new SetReserveMonthAmount(ReserveId, "2026-02", 40000m, "actualizado")).Single());
        Assert.Single(state.Months);
        Assert.Equal(40000m, state.Months[0].Amount);
    }
}
