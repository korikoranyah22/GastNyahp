using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Reserves;

public enum ReserveType { Reserve, Cash, Debt, Other }

public readonly record struct ReserveMonthOverride(YearMonth Month, decimal Amount, string? Note);

public static class ReserveEvents
{
    public static class V1
    {
        [EventType("V1.ReserveRegistered")]
        public record ReserveRegistered(Guid ReserveId, Guid FamilyId, string Label, ReserveType Type, string Icon, bool Recurring, decimal BaseAmount);

        [EventType("V1.ReserveDetailsUpdated")]
        public record ReserveDetailsUpdated(string Label, ReserveType Type, string Icon);

        [EventType("V1.ReserveMonthAmountSet")]
        public record ReserveMonthAmountSet(string Month, decimal Amount, string? Note);

        [EventType("V1.ReserveBaseAmountApplied")]
        public record ReserveBaseAmountApplied(decimal BaseAmount);

        [EventType("V1.ReserveRemoved")]
        public record ReserveRemoved;
    }
}

public record ReserveState : State<ReserveState>
{
    public Guid ReserveId { get; init; }
    public Guid FamilyId { get; init; }
    public string Label { get; init; } = "";
    public ReserveType Type { get; init; }
    public string Icon { get; init; } = "";
    public bool Recurring { get; init; }
    public decimal BaseAmount { get; init; }
    public bool Removed { get; init; }
    public IReadOnlyList<ReserveMonthOverride> Months { get; init; } = [];

    public ReserveState()
    {
        On<ReserveEvents.V1.ReserveRegistered>((s, e) => s with
        {
            ReserveId = e.ReserveId, FamilyId = e.FamilyId, Label = e.Label, Type = e.Type, Icon = e.Icon, Recurring = e.Recurring, BaseAmount = e.BaseAmount, Months = [],
        });
        On<ReserveEvents.V1.ReserveDetailsUpdated>((s, e) => s with { Label = e.Label, Type = e.Type, Icon = e.Icon });
        On<ReserveEvents.V1.ReserveMonthAmountSet>((s, e) =>
        {
            var month = YearMonth.Parse(e.Month);
            var months = s.Months.Any(m => m.Month == month)
                ? s.Months.Select(m => m.Month == month ? new ReserveMonthOverride(month, e.Amount, e.Note) : m).ToList()
                : [.. s.Months, new ReserveMonthOverride(month, e.Amount, e.Note)];
            return s with { Months = months };
        });
        // Destructive by design (DOMAIN_MODEL.md §7): applying a new base amount clears ALL per-month overrides.
        On<ReserveEvents.V1.ReserveBaseAmountApplied>((s, e) => s with { BaseAmount = e.BaseAmount, Recurring = true, Months = [] });
        On<ReserveEvents.V1.ReserveRemoved>((s, _) => s with { Removed = true });
    }

    /// <summary>Priority: per-month override &gt; BaseAmount (only if Recurring) &gt; 0. DOMAIN_MODEL.md §7.</summary>
    public decimal EffectiveAmount(YearMonth month)
    {
        var overrideAmount = Months.Where(m => m.Month == month).Select(m => (decimal?)m.Amount).FirstOrDefault();
        if (overrideAmount is not null) return overrideAmount.Value;
        return Recurring ? BaseAmount : 0;
    }
}

public record RegisterReserve(Guid ReserveId, Guid FamilyId, string Label, ReserveType Type, string Icon, bool Recurring, decimal BaseAmount);
public record UpdateReserveDetails(Guid ReserveId, string Label, ReserveType Type, string Icon);
public record SetReserveMonthAmount(Guid ReserveId, string Month, decimal Amount, string? Note);
public record ApplyReserveBaseToAllMonths(Guid ReserveId, decimal BaseAmount);
public record RemoveReserve(Guid ReserveId);

public sealed class ReserveCommandService : CommandService<ReserveState>
{
    public ReserveCommandService(IEventStore store) : base(store)
    {
        On<RegisterReserve>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.ReserveId)).Act(Register);
        On<UpdateReserveDetails>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ReserveId)).Act(UpdateDetails);
        On<SetReserveMonthAmount>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ReserveId)).Act(SetMonthAmount);
        On<ApplyReserveBaseToAllMonths>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ReserveId)).Act(ApplyBase);
        On<RemoveReserve>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ReserveId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterReserve cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Label)) throw new DomainException("RegisterReserve: Label required.");
        if (cmd.Recurring && cmd.BaseAmount <= 0) throw new DomainException("RegisterReserve: BaseAmount required (> 0) when Recurring.");
        yield return new ReserveEvents.V1.ReserveRegistered(cmd.ReserveId, cmd.FamilyId, cmd.Label.Trim(), cmd.Type, cmd.Icon, cmd.Recurring, cmd.Recurring ? cmd.BaseAmount : 0);
    }

    public static IEnumerable<object> UpdateDetails(ReserveState state, object[] _, UpdateReserveDetails cmd)
    {
        GuardNotRemoved(state, "UpdateReserveDetails");
        if (string.IsNullOrWhiteSpace(cmd.Label)) throw new DomainException("UpdateReserveDetails: Label required.");
        yield return new ReserveEvents.V1.ReserveDetailsUpdated(cmd.Label.Trim(), cmd.Type, cmd.Icon);
    }

    public static IEnumerable<object> SetMonthAmount(ReserveState state, object[] _, SetReserveMonthAmount cmd)
    {
        GuardNotRemoved(state, "SetReserveMonthAmount");
        yield return new ReserveEvents.V1.ReserveMonthAmountSet(cmd.Month, cmd.Amount, cmd.Note);
    }

    public static IEnumerable<object> ApplyBase(ReserveState state, object[] _, ApplyReserveBaseToAllMonths cmd)
    {
        GuardNotRemoved(state, "ApplyReserveBaseToAllMonths");
        if (cmd.BaseAmount < 0) throw new DomainException("ApplyReserveBaseToAllMonths: BaseAmount cannot be negative.");
        yield return new ReserveEvents.V1.ReserveBaseAmountApplied(cmd.BaseAmount);
    }

    public static IEnumerable<object> Remove(ReserveState state, object[] _, RemoveReserve cmd)
    {
        GuardNotRemoved(state, "RemoveReserve");
        yield return new ReserveEvents.V1.ReserveRemoved();
    }

    static void GuardNotRemoved(ReserveState state, string command)
    {
        if (state.Removed) throw new DomainException($"{command}: reserve was removed.");
    }

    static StreamName Stream(Guid id) => new($"reserve-{id}");
}
