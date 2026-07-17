using Eventuous;
using GastNyahp.Domain.Loans;

namespace GastNyahp.Domain.Tests.Loans;

public class LoanTests
{
    static readonly Guid LoanId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid BankId = Guid.NewGuid();

    static RegisterLoan ValidLoan(int totalInstallments = 12, decimal monthlyInstallment = 180000m) =>
        new(LoanId, FamilyId, BankId, "Préstamo personal", 2160000m, monthlyInstallment, "2025-11", totalInstallments);

    [Fact]
    public void Register_generates_totalInstallments_months_all_unpaid()
    {
        var state = new LoanState().When(LoanCommandService.Register(ValidLoan(12)).Single());

        Assert.Equal(12, state.Months.Count);
        Assert.Equal(0, state.PaidInstallments);
        Assert.Equal(12 * 180000m, state.RemainingAmount);
    }

    [Fact]
    public void PaidInstallments_is_derived_from_Months_not_a_mutable_counter()
    {
        var state = new LoanState().When(LoanCommandService.Register(ValidLoan(3)).Single());
        state = state.When(LoanCommandService.TogglePaid(state, [], new ToggleLoanMonthPaid(LoanId, "2025-11")).Single());
        state = state.When(LoanCommandService.TogglePaid(state, [], new ToggleLoanMonthPaid(LoanId, "2025-12")).Single());

        Assert.Equal(2, state.PaidInstallments);

        // Toggling one back off must decrement the derived count, never desync like a hand-maintained counter could.
        state = state.When(LoanCommandService.TogglePaid(state, [], new ToggleLoanMonthPaid(LoanId, "2025-11")).Single());
        Assert.Equal(1, state.PaidInstallments);
    }

    [Fact]
    public void ReviseSchedule_DOES_regenerate_unlike_the_legacy_frontend_bug()
    {
        var state = new LoanState().When(LoanCommandService.Register(ValidLoan(3)).Single());
        state = state.When(LoanCommandService.TogglePaid(state, [], new ToggleLoanMonthPaid(LoanId, "2025-11")).Single());

        state = state.When(LoanCommandService.Revise(state, [], new ReviseLoanSchedule(LoanId, "2025-11", 6, 200000m)).Single());

        Assert.Equal(6, state.Months.Count);
        Assert.True(state.Months.Single(m => m.Month.ToString() == "2025-11").Paid);
        Assert.Equal(180000m, state.Months.Single(m => m.Month.ToString() == "2025-11").Amount); // preserved
        Assert.Equal(200000m, state.Months.Single(m => m.Month.ToString() == "2025-12").Amount); // new base amount
    }

    [Theory]
    [InlineData(0)]
    [InlineData(361)]
    public void Register_rejects_out_of_range_totalInstallments(int total) =>
        Assert.Throws<DomainException>(() => LoanCommandService.Register(ValidLoan(total)).ToList());

    [Fact]
    public void Register_rejects_nonpositive_monthlyInstallment() =>
        Assert.Throws<DomainException>(() => LoanCommandService.Register(ValidLoan(monthlyInstallment: 0)).ToList());

    [Fact]
    public void OverrideMonthAmount_supports_UVA_style_variable_installments()
    {
        var state = new LoanState().When(LoanCommandService.Register(ValidLoan(2)).Single());
        state = state.When(LoanCommandService.OverrideAmount(state, [], new OverrideLoanMonthAmount(LoanId, "2025-12", 247309m)).Single());

        Assert.Equal(180000m, state.Months.Single(m => m.Month.ToString() == "2025-11").Amount);
        Assert.Equal(247309m, state.Months.Single(m => m.Month.ToString() == "2025-12").Amount);
    }

    [Fact]
    public void Commands_after_removal_throw()
    {
        var state = new LoanState().When(LoanCommandService.Register(ValidLoan(2)).Single());
        state = state.When(LoanCommandService.Remove(state, [], new RemoveLoan(LoanId)).Single());

        Assert.Throws<DomainException>(() => LoanCommandService.TogglePaid(state, [], new ToggleLoanMonthPaid(LoanId, "2025-11")).ToList());
    }
}
