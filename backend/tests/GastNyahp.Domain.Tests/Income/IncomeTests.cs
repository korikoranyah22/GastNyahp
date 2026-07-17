using Eventuous;
using GastNyahp.Domain.Income;

namespace GastNyahp.Domain.Tests.Income;

public class IncomeTests
{
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Update_is_a_partial_merge_leaving_unspecified_fields_untouched()
    {
        var state = new IncomeState().When(IncomeCommandService.Update(new UpdateIncome(FamilyId, 500000m, 1050m, 1250m, 70)).Single());

        state = state.When(IncomeCommandService.Update(new UpdateIncome(FamilyId, 600000m, null, null, null)).Single());

        Assert.Equal(600000m, state.NetMonthly);
        Assert.Equal(1050m, state.UsdRateOfficial); // untouched
        Assert.Equal(1250m, state.UsdRateCcl);      // untouched
        Assert.Equal(70, state.SplitPercent);       // untouched
    }

    [Fact]
    public void Default_SplitPercent_is_70_before_any_event()
    {
        Assert.Equal(70, new IncomeState().SplitPercent);
    }

    [Fact]
    public void Update_rejects_negative_amounts() =>
        Assert.Throws<DomainException>(() => IncomeCommandService.Update(new UpdateIncome(FamilyId, -1, null, null, null)).ToList());

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Update_rejects_splitPercent_out_of_0_100_range(int splitPercent) =>
        Assert.Throws<DomainException>(() => IncomeCommandService.Update(new UpdateIncome(FamilyId, null, null, null, splitPercent)).ToList());
}
