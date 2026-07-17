using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;

namespace GastNyahp.Domain.Tests.Expenses;

public class ExpenseTests
{
    static readonly Guid ExpenseId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid CardId = Guid.NewGuid();

    static RegisterExpense ValidExpense(decimal amount = 130823.02m) =>
        new(ExpenseId, FamilyId, "2025-04-07", "Coto", "Comida", amount, ExpenseCurrency.Ars, PaymentMethod.ByCard(CardId), OwnerRef.None, UsdRateCcl: 0);

    [Fact]
    public void Register_with_zero_or_negative_amount_throws()
    {
        Assert.Throws<DomainException>(() => ExpenseCommandService.Register(ValidExpense(0)).ToList());
        Assert.Throws<DomainException>(() => ExpenseCommandService.Register(ValidExpense(-1)).ToList());
    }

    [Fact]
    public void Register_with_unknown_category_throws() =>
        Assert.Throws<DomainException>(() => ExpenseCommandService.Register(ValidExpense() with { Category = "Bogus" }).ToList());

    [Fact]
    public void Register_preserves_the_payment_method()
    {
        var state = new ExpenseState().When(ExpenseCommandService.Register(ValidExpense()).Single());
        Assert.True(state.PaymentMethod.IsCredit);
        Assert.Equal(CardId, state.PaymentMethod.ReferenceId);
    }

    [Fact]
    public void Register_with_USD_converts_and_keeps_original()
    {
        var cmd = ValidExpense(50m) with { Currency = ExpenseCurrency.Usd, UsdRateCcl = 1250m };
        var e = (ExpenseEvents.V1.ExpenseRegistered)ExpenseCommandService.Register(cmd).Single();

        Assert.Equal(62500m, e.AmountArs);
        Assert.Equal(50m, e.OriginalAmount);
    }

    [Fact]
    public void Register_with_USD_and_no_rate_throws() =>
        Assert.Throws<DomainException>(() =>
            ExpenseCommandService.Register(ValidExpense(50m) with { Currency = ExpenseCurrency.Usd, UsdRateCcl = 0 }).ToList());

    [Fact]
    public void Commands_after_removal_throw()
    {
        var state = new ExpenseState().When(ExpenseCommandService.Register(ValidExpense()).Single());
        state = state.When(ExpenseCommandService.Remove(state, [], new RemoveExpense(ExpenseId)).Single());

        Assert.Throws<DomainException>(() =>
            ExpenseCommandService.Update(state, [], new UpdateExpense(ExpenseId, "2025-04-08", "Coto", "Comida", 1000m, ExpenseCurrency.Ars, PaymentMethod.CashPayment, OwnerRef.None, 0)).ToList());
    }
}
