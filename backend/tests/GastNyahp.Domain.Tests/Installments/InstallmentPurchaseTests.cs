using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Installments;

namespace GastNyahp.Domain.Tests.Installments;

public class InstallmentPurchaseTests
{
    static readonly Guid InstallmentId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid CardId = Guid.NewGuid();

    static RegisterInstallmentPurchase FixedPurchase(int totalInstallments = 12, string startMonth = "2025-10", decimal amount = 85000m) =>
        new(InstallmentId, FamilyId, CardId, "Smart TV 55\"", "Hogar", "2025-10-05",
            InstallmentFrequency.Fixed, amount, totalInstallments, startMonth, OwnerRef.None);

    static RegisterInstallmentPurchase MonthlyRecurringPurchase() =>
        new(InstallmentId, FamilyId, CardId, "Plan celular", "Servicios", "2026-01-01",
            InstallmentFrequency.Monthly, 45000m, null, "2026-01", OwnerRef.None);

    [Fact]
    public void Register_fixed_generates_exactly_totalInstallments_months()
    {
        var e = (InstallmentEvents.V1.InstallmentPurchaseRegistered)InstallmentPurchaseCommandService.Register(FixedPurchase(12)).Single();
        var state = new InstallmentPurchaseState().When(e);

        Assert.Equal(12, state.Months.Count);
        Assert.Equal("2025-10", state.Months[0].Month.ToString());
        Assert.Equal("2026-09", state.Months[^1].Month.ToString());
        Assert.All(state.Months, m => Assert.Equal(85000m, m.Amount));
        Assert.All(state.Months, m => Assert.False(m.Paid));
    }

    [Fact]
    public void Register_monthly_recurring_generates_24_month_window()
    {
        var e = (InstallmentEvents.V1.InstallmentPurchaseRegistered)InstallmentPurchaseCommandService.Register(MonthlyRecurringPurchase()).Single();
        var state = new InstallmentPurchaseState().When(e);

        Assert.Equal(24, state.Months.Count);
        Assert.Equal("2026-01", state.Months[0].Month.ToString());
        Assert.Equal("2027-12", state.Months[^1].Month.ToString());
    }

    [Fact]
    public void Register_fixed_without_totalInstallments_throws() =>
        Assert.Throws<DomainException>(() =>
            InstallmentPurchaseCommandService.Register(FixedPurchase() with { TotalInstallments = null }).ToList());

    [Fact]
    public void Register_with_nonpositive_amount_throws() =>
        Assert.Throws<DomainException>(() =>
            InstallmentPurchaseCommandService.Register(FixedPurchase(amount: 0)).ToList());

    [Fact]
    public void Register_with_unknown_category_throws() =>
        Assert.Throws<DomainException>(() =>
            InstallmentPurchaseCommandService.Register(FixedPurchase() with { Category = "NotACategory" }).ToList());

    [Fact]
    public void TogglePaid_flips_the_targeted_month_only()
    {
        var state = new InstallmentPurchaseState().When(InstallmentPurchaseCommandService.Register(FixedPurchase(3)).Single());

        var toggled = InstallmentPurchaseCommandService.TogglePaid(state, [], new ToggleInstallmentMonthPaid(InstallmentId, "2025-10")).Single();
        state = state.When(toggled);

        Assert.True(state.Months.Single(m => m.Month.ToString() == "2025-10").Paid);
        Assert.False(state.Months.Single(m => m.Month.ToString() == "2025-11").Paid);
    }

    [Fact]
    public void ReviseSchedule_preserves_paid_months_when_extending_the_window()
    {
        var state = new InstallmentPurchaseState().When(InstallmentPurchaseCommandService.Register(FixedPurchase(4)).Single());
        state = state.When(InstallmentPurchaseCommandService.TogglePaid(state, [], new ToggleInstallmentMonthPaid(InstallmentId, "2025-10")).Single());
        state = state.When(InstallmentPurchaseCommandService.TogglePaid(state, [], new ToggleInstallmentMonthPaid(InstallmentId, "2025-11")).Single());

        // Revise to 6 installments at a new base amount — mirrors editing totalInstallments in the UI.
        var revised = InstallmentPurchaseCommandService.Revise(state, [],
            new ReviseInstallmentSchedule(InstallmentId, "2025-10", 6, InstallmentFrequency.Fixed, 90000m)).Single();
        state = state.When(revised);

        Assert.Equal(6, state.Months.Count);
        Assert.True(state.Months.Single(m => m.Month.ToString() == "2025-10").Paid);
        Assert.Equal(85000m, state.Months.Single(m => m.Month.ToString() == "2025-10").Amount); // preserved, not overwritten by the new base
        Assert.True(state.Months.Single(m => m.Month.ToString() == "2025-11").Paid);
        Assert.False(state.Months.Single(m => m.Month.ToString() == "2025-12").Paid);
        Assert.Equal(90000m, state.Months.Single(m => m.Month.ToString() == "2025-12").Amount); // new months use the new base amount
    }

    [Fact]
    public void OverrideMonthAmount_changes_a_single_cell_without_touching_others()
    {
        var state = new InstallmentPurchaseState().When(InstallmentPurchaseCommandService.Register(FixedPurchase(3)).Single());
        state = state.When(InstallmentPurchaseCommandService.OverrideAmount(state, [], new OverrideInstallmentMonthAmount(InstallmentId, "2025-11", 999m)).Single());

        Assert.Equal(85000m, state.Months.Single(m => m.Month.ToString() == "2025-10").Amount);
        Assert.Equal(999m, state.Months.Single(m => m.Month.ToString() == "2025-11").Amount);
    }

    [Fact]
    public void Finish_sets_Active_false_but_keeps_Months()
    {
        var state = new InstallmentPurchaseState().When(InstallmentPurchaseCommandService.Register(FixedPurchase(3)).Single());
        state = state.When(InstallmentPurchaseCommandService.Finish(state, [], new FinishInstallment(InstallmentId)).Single());

        Assert.False(state.Active);
        Assert.Equal(3, state.Months.Count);
    }

    [Fact]
    public void Finish_twice_throws()
    {
        var state = new InstallmentPurchaseState().When(InstallmentPurchaseCommandService.Register(FixedPurchase(3)).Single());
        state = state.When(InstallmentPurchaseCommandService.Finish(state, [], new FinishInstallment(InstallmentId)).Single());

        Assert.Throws<DomainException>(() =>
            InstallmentPurchaseCommandService.Finish(state, [], new FinishInstallment(InstallmentId)).ToList());
    }

    [Fact]
    public void Commands_after_removal_all_throw()
    {
        var state = new InstallmentPurchaseState().When(InstallmentPurchaseCommandService.Register(FixedPurchase(3)).Single());
        state = state.When(InstallmentPurchaseCommandService.Remove(state, [], new RemoveInstallmentPurchase(InstallmentId)).Single());

        Assert.Throws<DomainException>(() => InstallmentPurchaseCommandService.TogglePaid(state, [], new ToggleInstallmentMonthPaid(InstallmentId, "2025-10")).ToList());
        Assert.Throws<DomainException>(() => InstallmentPurchaseCommandService.OverrideAmount(state, [], new OverrideInstallmentMonthAmount(InstallmentId, "2025-10", 1)).ToList());
        Assert.Throws<DomainException>(() => InstallmentPurchaseCommandService.Finish(state, [], new FinishInstallment(InstallmentId)).ToList());
    }
}
