using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Expenses;

public readonly record struct TicketItem(Guid ItemId, string Description, decimal Amount, string Category, OwnerRef Owner);

/// <summary>Primitive shape of a TicketItem for event payloads — records can't carry OwnerRef directly (see
/// the "primitive fields only" rule in eventuous-event-sourced-aggregate).</summary>
public readonly record struct TicketItemInput(Guid ItemId, string Description, decimal Amount, string Category, string OwnerKind, Guid? OwnerPersonId);

public static class TicketEvents
{
    public static class V1
    {
        [EventType("V1.TicketRegistered")]
        public record TicketRegistered(Guid TicketId, Guid FamilyId, string Date, string Description, string PaymentMethodKind, Guid? PaymentMethodReferenceId, decimal Discount, IReadOnlyList<TicketItemInput> Items);

        [EventType("V1.TicketUpdated")]
        public record TicketUpdated(string Date, string Description, string PaymentMethodKind, Guid? PaymentMethodReferenceId, decimal Discount, IReadOnlyList<TicketItemInput> Items);

        [EventType("V1.TicketRemoved")]
        public record TicketRemoved;
    }
}

public record TicketState : State<TicketState>
{
    public Guid TicketId { get; init; }
    public Guid FamilyId { get; init; }
    public string Date { get; init; } = "";
    public string Description { get; init; } = "";
    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.CashPayment;
    public decimal Discount { get; init; }
    public IReadOnlyList<TicketItem> Items { get; init; } = [];
    public bool Removed { get; init; }

    /// <summary>Ported 1:1 from getExpenseAmount() for tickets — DOMAIN_MODEL.md §10.</summary>
    public decimal Total => Math.Max(0, Items.Sum(i => i.Amount) - Discount);

    public TicketState()
    {
        On<TicketEvents.V1.TicketRegistered>((s, e) => s with
        {
            TicketId = e.TicketId, FamilyId = e.FamilyId, Date = e.Date, Description = e.Description,
            PaymentMethod = PaymentMethod.FromPrimitive(e.PaymentMethodKind, e.PaymentMethodReferenceId),
            Discount = e.Discount, Items = ToItems(e.Items),
        });
        On<TicketEvents.V1.TicketUpdated>((s, e) => s with
        {
            Date = e.Date, Description = e.Description,
            PaymentMethod = PaymentMethod.FromPrimitive(e.PaymentMethodKind, e.PaymentMethodReferenceId),
            Discount = e.Discount, Items = ToItems(e.Items),
        });
        On<TicketEvents.V1.TicketRemoved>((s, _) => s with { Removed = true });
    }

    static IReadOnlyList<TicketItem> ToItems(IReadOnlyList<TicketItemInput> items) =>
        items.Select(i => new TicketItem(i.ItemId, i.Description, i.Amount, i.Category, OwnerRef.FromPrimitive(i.OwnerKind, i.OwnerPersonId))).ToList();
}

public record RegisterTicket(Guid TicketId, Guid FamilyId, string Date, string Description, PaymentMethod PaymentMethod, decimal Discount, IReadOnlyList<TicketItemInput> Items);
public record UpdateTicket(Guid TicketId, string Date, string Description, PaymentMethod PaymentMethod, decimal Discount, IReadOnlyList<TicketItemInput> Items);
public record RemoveTicket(Guid TicketId);

public sealed class TicketCommandService : CommandService<TicketState>
{
    public TicketCommandService(IEventStore store) : base(store)
    {
        On<RegisterTicket>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.TicketId)).Act(Register);
        On<UpdateTicket>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.TicketId)).Act(Update);
        On<RemoveTicket>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.TicketId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterTicket cmd)
    {
        ValidateTicket(cmd.Description, cmd.Items);
        yield return new TicketEvents.V1.TicketRegistered(cmd.TicketId, cmd.FamilyId, cmd.Date, cmd.Description.Trim(), cmd.PaymentMethod.Kind, cmd.PaymentMethod.ReferenceId, cmd.Discount, cmd.Items);
    }

    public static IEnumerable<object> Update(TicketState state, object[] _, UpdateTicket cmd)
    {
        GuardNotRemoved(state, "UpdateTicket");
        ValidateTicket(cmd.Description, cmd.Items);
        yield return new TicketEvents.V1.TicketUpdated(cmd.Date, cmd.Description.Trim(), cmd.PaymentMethod.Kind, cmd.PaymentMethod.ReferenceId, cmd.Discount, cmd.Items);
    }

    public static IEnumerable<object> Remove(TicketState state, object[] _, RemoveTicket cmd)
    {
        GuardNotRemoved(state, "RemoveTicket");
        yield return new TicketEvents.V1.TicketRemoved();
    }

    static void ValidateTicket(string description, IReadOnlyList<TicketItemInput> items)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Ticket: Description required.");
        if (items.Count == 0) throw new DomainException("Ticket: at least one item is required.");
        foreach (var (item, index) in items.Select((i, idx) => (i, idx)))
        {
            if (string.IsNullOrWhiteSpace(item.Description)) throw new DomainException($"Ticket: item {index + 1} requires a Description.");
            if (item.Amount <= 0) throw new DomainException($"Ticket: item {index + 1} requires an Amount > 0.");
            if (!AppCategories.IsValidExpenseCategory(item.Category)) throw new DomainException($"Ticket: item {index + 1} has unknown category '{item.Category}'.");
        }
    }

    static void GuardNotRemoved(TicketState state, string command)
    {
        if (state.Removed) throw new DomainException($"{command}: ticket was removed.");
    }

    static StreamName Stream(Guid id) => new($"ticket-{id}");
}
