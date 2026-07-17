using Eventuous;

namespace GastNyahp.Domain.Banks;

public static class BankEvents
{
    public static class V1
    {
        [EventType("V1.BankRegistered")]
        public record BankRegistered(Guid BankId, Guid FamilyId, string Name, string? Alias, string Color, string Icon);

        [EventType("V1.BankUpdated")]
        public record BankUpdated(string Name, string? Alias, string Color, string Icon);

        [EventType("V1.BankRemoved")]
        public record BankRemoved(string RemovedAt);
    }
}

public record BankState : State<BankState>
{
    public Guid BankId { get; init; }
    public Guid FamilyId { get; init; }
    public string Name { get; init; } = "";
    public string? Alias { get; init; }
    public string Color { get; init; } = "";
    public string Icon { get; init; } = "";
    public bool Removed { get; init; }

    public BankState()
    {
        On<BankEvents.V1.BankRegistered>((s, e) => s with
        {
            BankId = e.BankId, FamilyId = e.FamilyId, Name = e.Name, Alias = e.Alias, Color = e.Color, Icon = e.Icon
        });
        On<BankEvents.V1.BankUpdated>((s, e) => s with
        {
            Name = e.Name, Alias = e.Alias, Color = e.Color, Icon = e.Icon
        });
        On<BankEvents.V1.BankRemoved>((s, _) => s with { Removed = true });
    }
}

public record RegisterBank(Guid BankId, Guid FamilyId, string Name, string? Alias, string Color, string Icon);
public record UpdateBank(Guid BankId, string Name, string? Alias, string Color, string Icon);
public record RemoveBank(Guid BankId);

public sealed class BankCommandService : CommandService<BankState>
{
    public BankCommandService(IEventStore store) : base(store)
    {
        On<RegisterBank>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.BankId)).Act(Register);
        On<UpdateBank>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.BankId)).Act(Update);
        On<RemoveBank>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.BankId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterBank cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new DomainException("RegisterBank: Name required.");
        yield return new BankEvents.V1.BankRegistered(cmd.BankId, cmd.FamilyId, cmd.Name.Trim(), cmd.Alias, cmd.Color, cmd.Icon);
    }

    public static IEnumerable<object> Update(BankState state, object[] _, UpdateBank cmd)
    {
        if (state.Removed) throw new DomainException("UpdateBank: bank was removed.");
        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new DomainException("UpdateBank: Name required.");
        yield return new BankEvents.V1.BankUpdated(cmd.Name.Trim(), cmd.Alias, cmd.Color, cmd.Icon);
    }

    // Integrity guard (no CreditCard/Loan referencing this bank) is a CROSS-aggregate rule, enforced by
    // BankService (application layer) BEFORE this command is even sent — see DOMAIN_MODEL.md §2 and §16.
    // This aggregate only guards against double-removal.
    public static IEnumerable<object> Remove(BankState state, object[] _, RemoveBank cmd)
    {
        if (state.Removed) throw new DomainException("RemoveBank: bank already removed.");
        yield return new BankEvents.V1.BankRemoved(Now);
    }

    static StreamName Stream(Guid id) => new($"bank-{id}");
    static string Now => DateTime.UtcNow.ToString("O");
}
