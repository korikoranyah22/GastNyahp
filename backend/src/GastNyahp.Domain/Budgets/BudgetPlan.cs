using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Budgets;

public static class BudgetPlanEvents
{
    public static class V1
    {
        [EventType("V1.BudgetLimitsSet")]
        public record BudgetLimitsSet(Guid FamilyId, string Month, decimal CreditLimit, decimal DebitCashLimit, decimal WeeklyLimit);
    }
}

public record BudgetPlanState : State<BudgetPlanState>
{
    public Guid FamilyId { get; init; }
    public YearMonth Month { get; init; }
    public decimal CreditLimit { get; init; }
    public decimal DebitCashLimit { get; init; }
    public decimal WeeklyLimit { get; init; }

    public BudgetPlanState()
    {
        On<BudgetPlanEvents.V1.BudgetLimitsSet>((s, e) => s with
        {
            FamilyId = e.FamilyId, Month = YearMonth.Parse(e.Month), CreditLimit = e.CreditLimit, DebitCashLimit = e.DebitCashLimit, WeeklyLimit = e.WeeklyLimit,
        });
    }
}

/// <summary>Always the full triple — the application service pre-loads current values for any field the
/// caller didn't intend to change (DOMAIN_MODEL.md §11); the aggregate itself has no notion of "partial".</summary>
public record SetBudgetLimits(Guid FamilyId, string Month, decimal CreditLimit, decimal DebitCashLimit, decimal WeeklyLimit);

public sealed class BudgetPlanCommandService : CommandService<BudgetPlanState>
{
    public BudgetPlanCommandService(IEventStore store) : base(store)
    {
        On<SetBudgetLimits>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => Stream(cmd.FamilyId, cmd.Month)).Act(SetOnAnyState);
    }

    // ExpectedState.Any requires the stateful handler signature even though the decision ignores prior state:
    // BudgetLimitsSet always carries the full triple, so folding it is a plain overwrite.
    static IEnumerable<object> SetOnAnyState(BudgetPlanState state, object[] originalEvents, SetBudgetLimits cmd) => Set(cmd);

    public static IEnumerable<object> Set(SetBudgetLimits cmd)
    {
        if (cmd.CreditLimit < 0 || cmd.DebitCashLimit < 0 || cmd.WeeklyLimit < 0)
            throw new DomainException("SetBudgetLimits: limits cannot be negative.");
        yield return new BudgetPlanEvents.V1.BudgetLimitsSet(cmd.FamilyId, cmd.Month, cmd.CreditLimit, cmd.DebitCashLimit, cmd.WeeklyLimit);
    }

    static StreamName Stream(Guid familyId, string month) => new($"budget-{familyId}-{month}");
}
