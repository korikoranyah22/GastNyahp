using Eventuous;
using GastNyahp.Domain.Budgets;

namespace GastNyahp.Domain.Tests.Budgets;

public class BudgetPlanTests
{
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Set_populates_all_three_limits()
    {
        var state = new BudgetPlanState().When(BudgetPlanCommandService.Set(new SetBudgetLimits(FamilyId, "2026-02", 480000m, 316000m, 200000m)).Single());

        Assert.Equal(480000m, state.CreditLimit);
        Assert.Equal(316000m, state.DebitCashLimit);
        Assert.Equal(200000m, state.WeeklyLimit);
    }

    [Fact]
    public void Set_rejects_negative_limits() =>
        Assert.Throws<DomainException>(() => BudgetPlanCommandService.Set(new SetBudgetLimits(FamilyId, "2026-02", -1, 0, 0)).ToList());

    [Fact]
    public void Set_zero_is_a_valid_meaning_no_limit_configured()
    {
        var state = new BudgetPlanState().When(BudgetPlanCommandService.Set(new SetBudgetLimits(FamilyId, "2026-02", 0, 0, 0)).Single());
        Assert.Equal(0m, state.CreditLimit);
    }
}
