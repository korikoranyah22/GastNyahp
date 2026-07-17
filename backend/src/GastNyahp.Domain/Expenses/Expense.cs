using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Expenses;

public enum ExpenseCurrency { Ars, Usd }

public static class ExpenseEvents
{
    public static class V1
    {
        [EventType("V1.ExpenseRegistered")]
        public record ExpenseRegistered(
            Guid ExpenseId, Guid FamilyId, string Date, string Description, string Category, decimal AmountArs,
            decimal? OriginalAmount, ExpenseCurrency? OriginalCurrency,
            string PaymentMethodKind, Guid? PaymentMethodReferenceId, string OwnerKind, Guid? OwnerPersonId);

        [EventType("V1.ExpenseUpdated")]
        public record ExpenseUpdated(
            string Date, string Description, string Category, decimal AmountArs,
            decimal? OriginalAmount, ExpenseCurrency? OriginalCurrency,
            string PaymentMethodKind, Guid? PaymentMethodReferenceId, string OwnerKind, Guid? OwnerPersonId);

        [EventType("V1.ExpenseRemoved")]
        public record ExpenseRemoved;
    }
}

public record ExpenseState : State<ExpenseState>
{
    public Guid ExpenseId { get; init; }
    public Guid FamilyId { get; init; }
    public string Date { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal AmountArs { get; init; }
    public decimal? OriginalAmount { get; init; }
    public ExpenseCurrency? OriginalCurrency { get; init; }
    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.CashPayment;
    public OwnerRef Owner { get; init; } = OwnerRef.None;
    public bool Removed { get; init; }

    public ExpenseState()
    {
        On<ExpenseEvents.V1.ExpenseRegistered>((s, e) => s with
        {
            ExpenseId = e.ExpenseId, FamilyId = e.FamilyId, Date = e.Date, Description = e.Description, Category = e.Category, AmountArs = e.AmountArs,
            OriginalAmount = e.OriginalAmount, OriginalCurrency = e.OriginalCurrency,
            PaymentMethod = PaymentMethod.FromPrimitive(e.PaymentMethodKind, e.PaymentMethodReferenceId),
            Owner = OwnerRef.FromPrimitive(e.OwnerKind, e.OwnerPersonId),
        });
        On<ExpenseEvents.V1.ExpenseUpdated>((s, e) => s with
        {
            Date = e.Date, Description = e.Description, Category = e.Category, AmountArs = e.AmountArs,
            OriginalAmount = e.OriginalAmount, OriginalCurrency = e.OriginalCurrency,
            PaymentMethod = PaymentMethod.FromPrimitive(e.PaymentMethodKind, e.PaymentMethodReferenceId),
            Owner = OwnerRef.FromPrimitive(e.OwnerKind, e.OwnerPersonId),
        });
        On<ExpenseEvents.V1.ExpenseRemoved>((s, _) => s with { Removed = true });
    }
}

public record RegisterExpense(Guid ExpenseId, Guid FamilyId, string Date, string Description, string Category, decimal Amount, ExpenseCurrency Currency, PaymentMethod PaymentMethod, OwnerRef Owner, decimal UsdRateCcl);
public record UpdateExpense(Guid ExpenseId, string Date, string Description, string Category, decimal Amount, ExpenseCurrency Currency, PaymentMethod PaymentMethod, OwnerRef Owner, decimal UsdRateCcl);
public record RemoveExpense(Guid ExpenseId);

public sealed class ExpenseCommandService : CommandService<ExpenseState>
{
    public ExpenseCommandService(IEventStore store) : base(store)
    {
        On<RegisterExpense>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.ExpenseId)).Act(Register);
        On<UpdateExpense>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ExpenseId)).Act(Update);
        On<RemoveExpense>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ExpenseId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterExpense cmd)
    {
        Validate(cmd.Description, cmd.Category, cmd.Amount);
        var (amountArs, originalAmount, originalCurrency) = ConvertToArs(cmd.Amount, cmd.Currency, cmd.UsdRateCcl);

        yield return new ExpenseEvents.V1.ExpenseRegistered(
            cmd.ExpenseId, cmd.FamilyId, cmd.Date, cmd.Description.Trim(), cmd.Category, amountArs, originalAmount, originalCurrency,
            cmd.PaymentMethod.Kind, cmd.PaymentMethod.ReferenceId, cmd.Owner.Kind, cmd.Owner.PersonId);
    }

    public static IEnumerable<object> Update(ExpenseState state, object[] _, UpdateExpense cmd)
    {
        GuardNotRemoved(state, "UpdateExpense");
        Validate(cmd.Description, cmd.Category, cmd.Amount);
        var (amountArs, originalAmount, originalCurrency) = ConvertToArs(cmd.Amount, cmd.Currency, cmd.UsdRateCcl);

        yield return new ExpenseEvents.V1.ExpenseUpdated(
            cmd.Date, cmd.Description.Trim(), cmd.Category, amountArs, originalAmount, originalCurrency,
            cmd.PaymentMethod.Kind, cmd.PaymentMethod.ReferenceId, cmd.Owner.Kind, cmd.Owner.PersonId);
    }

    public static IEnumerable<object> Remove(ExpenseState state, object[] _, RemoveExpense cmd)
    {
        GuardNotRemoved(state, "RemoveExpense");
        yield return new ExpenseEvents.V1.ExpenseRemoved();
    }

    static void Validate(string description, string category, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Expense: Description required.");
        if (!AppCategories.IsValidExpenseCategory(category)) throw new DomainException($"Expense: unknown category '{category}'.");
        if (amount <= 0) throw new DomainException("Expense: Amount must be > 0.");
    }

    static (decimal AmountArs, decimal? OriginalAmount, ExpenseCurrency? OriginalCurrency) ConvertToArs(decimal amount, ExpenseCurrency currency, decimal usdRateCcl)
    {
        if (currency == ExpenseCurrency.Ars) return (amount, null, null);
        if (usdRateCcl <= 0) throw new DomainException("Expense: UsdRateCcl must be configured (> 0) to register a USD amount.");
        return (Math.Round(amount * usdRateCcl), amount, currency);
    }

    static void GuardNotRemoved(ExpenseState state, string command)
    {
        if (state.Removed) throw new DomainException($"{command}: expense was removed.");
    }

    static StreamName Stream(Guid id) => new($"expense-{id}");
}
