using Eventuous;

namespace GastNyahp.Domain.Cards;

public enum CardNetwork { Visa, Mastercard }
public enum CardType { Credit, Debit }

public static class CreditCardEvents
{
    public static class V1
    {
        [EventType("V1.CardRegistered")]
        public record CardRegistered(Guid CardId, Guid FamilyId, Guid BankId, string Label, CardNetwork Network, CardType Type, int ClosingDay, int DueDay, string Color);

        [EventType("V1.CardUpdated")]
        public record CardUpdated(string Label, CardNetwork Network, CardType Type, int ClosingDay, int DueDay, string Color);

        [EventType("V1.CardActivated")]
        public record CardActivated;

        [EventType("V1.CardDeactivated")]
        public record CardDeactivated;

        [EventType("V1.CardRemoved")]
        public record CardRemoved(string RemovedAt);
    }
}

public record CreditCardState : State<CreditCardState>
{
    public Guid CardId { get; init; }
    public Guid FamilyId { get; init; }
    public Guid BankId { get; init; }
    public string Label { get; init; } = "";
    public CardNetwork Network { get; init; }
    public CardType Type { get; init; }
    public int ClosingDay { get; init; }
    public int DueDay { get; init; }
    public string Color { get; init; } = "";
    public bool Active { get; init; } = true;
    public bool Removed { get; init; }

    public CreditCardState()
    {
        On<CreditCardEvents.V1.CardRegistered>((s, e) => s with
        {
            CardId = e.CardId, FamilyId = e.FamilyId, BankId = e.BankId, Label = e.Label, Network = e.Network, Type = e.Type,
            ClosingDay = e.ClosingDay, DueDay = e.DueDay, Color = e.Color, Active = true,
        });
        On<CreditCardEvents.V1.CardUpdated>((s, e) => s with
        {
            Label = e.Label, Network = e.Network, Type = e.Type, ClosingDay = e.ClosingDay, DueDay = e.DueDay, Color = e.Color,
        });
        On<CreditCardEvents.V1.CardActivated>((s, _) => s with { Active = true });
        On<CreditCardEvents.V1.CardDeactivated>((s, _) => s with { Active = false });
        On<CreditCardEvents.V1.CardRemoved>((s, _) => s with { Removed = true });
    }
}

public record RegisterCard(Guid CardId, Guid FamilyId, Guid BankId, string Label, CardNetwork Network, CardType Type, int ClosingDay, int DueDay, string Color);
public record UpdateCard(Guid CardId, string Label, CardNetwork Network, CardType Type, int ClosingDay, int DueDay, string Color);
public record ActivateCard(Guid CardId);
public record DeactivateCard(Guid CardId);
public record RemoveCard(Guid CardId);

public sealed class CreditCardCommandService : CommandService<CreditCardState>
{
    public CreditCardCommandService(IEventStore store) : base(store)
    {
        On<RegisterCard>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.CardId)).Act(Register);
        On<UpdateCard>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.CardId)).Act(Update);
        On<ActivateCard>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.CardId)).Act(Activate);
        On<DeactivateCard>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.CardId)).Act(Deactivate);
        On<RemoveCard>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.CardId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterCard cmd)
    {
        ValidateDetails(cmd.Label, cmd.ClosingDay, cmd.DueDay);
        yield return new CreditCardEvents.V1.CardRegistered(
            cmd.CardId, cmd.FamilyId, cmd.BankId, cmd.Label.Trim(), cmd.Network, cmd.Type, cmd.ClosingDay, cmd.DueDay, cmd.Color);
    }

    public static IEnumerable<object> Update(CreditCardState state, object[] _, UpdateCard cmd)
    {
        GuardNotRemoved(state, "UpdateCard");
        ValidateDetails(cmd.Label, cmd.ClosingDay, cmd.DueDay);
        yield return new CreditCardEvents.V1.CardUpdated(cmd.Label.Trim(), cmd.Network, cmd.Type, cmd.ClosingDay, cmd.DueDay, cmd.Color);
    }

    public static IEnumerable<object> Activate(CreditCardState state, object[] _, ActivateCard cmd)
    {
        GuardNotRemoved(state, "ActivateCard");
        if (state.Active) throw new DomainException("ActivateCard: card is already active.");
        yield return new CreditCardEvents.V1.CardActivated();
    }

    public static IEnumerable<object> Deactivate(CreditCardState state, object[] _, DeactivateCard cmd)
    {
        GuardNotRemoved(state, "DeactivateCard");
        if (!state.Active) throw new DomainException("DeactivateCard: card is already inactive.");
        yield return new CreditCardEvents.V1.CardDeactivated();
    }

    // Integrity guard (no InstallmentPurchase or linked Service referencing this card) is a CROSS-aggregate
    // rule enforced by CreditCardService (application layer) — see DOMAIN_MODEL.md §3 and §16. This is a
    // deliberate improvement over the current frontend, which only checks installments, not linked services.
    public static IEnumerable<object> Remove(CreditCardState state, object[] _, RemoveCard cmd)
    {
        GuardNotRemoved(state, "RemoveCard");
        yield return new CreditCardEvents.V1.CardRemoved(Now);
    }

    static void ValidateDetails(string label, int closingDay, int dueDay)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new DomainException("Card: Label required.");
        if (closingDay is < 1 or > 31) throw new DomainException("Card: ClosingDay must be between 1 and 31.");
        if (dueDay is < 1 or > 31) throw new DomainException("Card: DueDay must be between 1 and 31.");
    }

    static void GuardNotRemoved(CreditCardState state, string command)
    {
        if (state.Removed) throw new DomainException($"{command}: card was removed.");
    }

    static StreamName Stream(Guid id) => new($"card-{id}");
    static string Now => DateTime.UtcNow.ToString("O");
}
