using Eventuous;

namespace GastNyahp.Domain.Income;

public static class IncomeEvents
{
    public static class V1
    {
        [EventType("V1.IncomeUpdated")]
        public record IncomeUpdated(Guid FamilyId, decimal? NetMonthly, decimal? UsdRateOfficial, decimal? UsdRateCcl, int? SplitPercent);
    }
}

public record IncomeState : State<IncomeState>
{
    public Guid FamilyId { get; init; }
    public decimal NetMonthly { get; init; }
    public decimal UsdRateOfficial { get; init; }
    public decimal UsdRateCcl { get; init; }
    public int SplitPercent { get; init; } = 70;

    public IncomeState()
    {
        On<IncomeEvents.V1.IncomeUpdated>((s, e) => s with
        {
            FamilyId = e.FamilyId,
            NetMonthly = e.NetMonthly ?? s.NetMonthly,
            UsdRateOfficial = e.UsdRateOfficial ?? s.UsdRateOfficial,
            UsdRateCcl = e.UsdRateCcl ?? s.UsdRateCcl,
            SplitPercent = e.SplitPercent ?? s.SplitPercent,
        });
    }
}

/// <summary>Singleton aggregate — a single stream "income" for the whole app (DOMAIN_MODEL.md §12).
/// Partial merge: only the fields the caller provides change.</summary>
public record UpdateIncome(Guid FamilyId, decimal? NetMonthly, decimal? UsdRateOfficial, decimal? UsdRateCcl, int? SplitPercent);

public sealed class IncomeCommandService : CommandService<IncomeState>
{
    public IncomeCommandService(IEventStore store) : base(store)
    {
        On<UpdateIncome>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(UpdateOnAnyState);
    }

    // ExpectedState.Any requires the stateful handler signature; the event carries only the provided fields,
    // so the fold (not the handler) is what merges against prior state.
    static IEnumerable<object> UpdateOnAnyState(IncomeState state, object[] originalEvents, UpdateIncome cmd) => Update(cmd);

    public static IEnumerable<object> Update(UpdateIncome cmd)
    {
        if (cmd.NetMonthly is < 0) throw new DomainException("UpdateIncome: NetMonthly cannot be negative.");
        if (cmd.UsdRateOfficial is < 0) throw new DomainException("UpdateIncome: UsdRateOfficial cannot be negative.");
        if (cmd.UsdRateCcl is < 0) throw new DomainException("UpdateIncome: UsdRateCcl cannot be negative.");
        if (cmd.SplitPercent is < 0 or > 100) throw new DomainException("UpdateIncome: SplitPercent must be between 0 and 100.");
        yield return new IncomeEvents.V1.IncomeUpdated(cmd.FamilyId, cmd.NetMonthly, cmd.UsdRateOfficial, cmd.UsdRateCcl, cmd.SplitPercent);
    }

    static StreamName Stream(Guid familyId) => new($"income-{familyId}");
}
