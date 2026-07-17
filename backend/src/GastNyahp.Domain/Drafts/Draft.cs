using Eventuous;

namespace GastNyahp.Domain.Drafts;

/// <summary>Qué tipo de carga real produce el borrador al confirmarse (DOMAIN_MODEL.md §19).</summary>
public enum DraftKind { Expense, Ticket, Installment }

public enum DraftStatus { Open, Confirmed, Discarded }

/// <summary>
/// El contenido del borrador: TODOS los campos opcionales a propósito — un borrador se moldea de a poco en una
/// conversación ("estoy en el super" → ítems → "me descontaron 20%"). La validación fuerte NO vive acá: corre
/// recién al confirmar, cuando el DraftService dispara el comando REAL (RegisterExpense/RegisterTicket/
/// RegisterInstallmentPurchase) y sus guards deciden. Kind determina qué campos importan.
/// </summary>
public record DraftPayload(
    string? Date = null,                    // yyyy-MM-dd; al confirmar, default hoy
    string? Description = null,
    string? Category = null,                // al confirmar, default "Desconocido" si falta o es inválida
    decimal? Amount = null,                 // gasto simple
    string? Currency = null,                // Ars | Usd; default Ars
    string? PaymentMethodKind = null,       // Cash | Card | Debit | Modo | MercadoPago; default Cash
    Guid? PaymentMethodReferenceId = null,
    string? OwnerKind = null,               // Unassigned | Shared | Owner; default Unassigned
    Guid? OwnerPersonId = null,
    decimal? Discount = null,               // ticket
    IReadOnlyList<DraftTicketItem>? Items = null, // ticket
    Guid? CardId = null,                    // cuotas
    decimal? MonthlyAmount = null,          // cuotas
    int? TotalInstallments = null,          // cuotas
    string? StartMonth = null,              // cuotas (yyyy-MM); default el mes de Date
    string? Note = null);                   // contexto libre de la conversación con el agente

public record DraftTicketItem(string? Description, decimal? Amount, string? Category = null, string? OwnerKind = null, Guid? OwnerPersonId = null);

public static class DraftEvents
{
    public static class V1
    {
        [EventType("V1.DraftCreated")]
        public record DraftCreated(
            Guid DraftId, Guid FamilyId, DraftKind Kind, DraftPayload Payload,
            string CreatedByKind, Guid CreatedById, string CreatedAt);

        // Snapshot completo del payload (no un delta): el historial del stream muestra cómo la conversación
        // fue moldeando la carga, versión por versión — auditabilidad del "cómo se llegó a este ticket".
        [EventType("V1.DraftUpdated")]
        public record DraftUpdated(DraftPayload Payload, string UpdatedAt);

        // El borrador cambia de tipo a mitad de la conversación: se empezó a cargar como gasto simple y recién
        // después apareció que era en cuotas ("…ah, y lo pagué en 6 con la Visa"). Sin esto el agente queda preso
        // del tipo que eligió en el primer mensaje y termina cargando la compra en una sola cuota.
        // El Payload NO se toca acá (los campos de cada tipo conviven en el mismo record): lo completa un
        // DraftUpdated, y los guards del confirm dicen qué falta para el tipo nuevo.
        [EventType("V1.DraftKindChanged")]
        public record DraftKindChanged(DraftKind Kind, string ChangedAt);

        [EventType("V1.DraftConfirmed")]
        public record DraftConfirmed(Guid ResultEntityId, string ConfirmedAt);

        [EventType("V1.DraftDiscarded")]
        public record DraftDiscarded(string? Reason, string DiscardedAt);
    }
}

public record DraftState : State<DraftState>
{
    public Guid DraftId { get; init; }
    public Guid FamilyId { get; init; }
    public DraftKind Kind { get; init; }
    public DraftPayload Payload { get; init; } = new();
    public DraftStatus Status { get; init; } = DraftStatus.Open;
    public Guid? ResultEntityId { get; init; }

    public DraftState()
    {
        On<DraftEvents.V1.DraftCreated>((s, e) => s with
        {
            DraftId = e.DraftId, FamilyId = e.FamilyId, Kind = e.Kind, Payload = e.Payload, Status = DraftStatus.Open,
        });
        On<DraftEvents.V1.DraftUpdated>((s, e) => s with { Payload = e.Payload });
        On<DraftEvents.V1.DraftKindChanged>((s, e) => s with { Kind = e.Kind });
        On<DraftEvents.V1.DraftConfirmed>((s, e) => s with { Status = DraftStatus.Confirmed, ResultEntityId = e.ResultEntityId });
        On<DraftEvents.V1.DraftDiscarded>((s, _) => s with { Status = DraftStatus.Discarded });
    }
}

public record CreateDraft(Guid DraftId, Guid FamilyId, DraftKind Kind, DraftPayload Payload, string CreatedByKind, Guid CreatedById);
public record UpdateDraft(Guid DraftId, DraftPayload Payload);
/// <summary>Convierte un borrador abierto a otro tipo (ej. gasto → cuotas al aparecer que la compra era financiada).
/// Comando propio y no un campo de <see cref="UpdateDraft"/>: la intención va en el nombre (§4 del DOMAIN_MODEL).</summary>
public record ChangeDraftKind(Guid DraftId, DraftKind Kind);
public record ConfirmDraft(Guid DraftId, Guid ResultEntityId);
public record DiscardDraft(Guid DraftId, string? Reason = null);

public sealed class DraftCommandService : CommandService<DraftState>
{
    public DraftCommandService(IEventStore store) : base(store)
    {
        On<CreateDraft>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.DraftId)).Act(Create);
        On<UpdateDraft>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.DraftId)).Act(Update);
        On<ChangeDraftKind>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.DraftId)).Act(ChangeKind);
        On<ConfirmDraft>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.DraftId)).Act(Confirm);
        On<DiscardDraft>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.DraftId)).Act(Discard);
    }

    public static IEnumerable<object> Create(CreateDraft cmd)
    {
        if (cmd.DraftId == Guid.Empty) throw new DomainException("CreateDraft: DraftId required.");
        if (cmd.FamilyId == Guid.Empty) throw new DomainException("CreateDraft: FamilyId required.");
        yield return new DraftEvents.V1.DraftCreated(
            cmd.DraftId, cmd.FamilyId, cmd.Kind, cmd.Payload ?? new DraftPayload(), cmd.CreatedByKind, cmd.CreatedById, Now);
    }

    public static IEnumerable<object> Update(DraftState state, object[] _, UpdateDraft cmd)
    {
        GuardOpen(state, "UpdateDraft");
        yield return new DraftEvents.V1.DraftUpdated(cmd.Payload ?? new DraftPayload(), Now);
    }

    public static IEnumerable<object> ChangeKind(DraftState state, object[] _, ChangeDraftKind cmd)
    {
        GuardOpen(state, "ChangeDraftKind");
        // Idempotente: convertir al tipo que ya tiene no ensucia el stream con un evento sin efecto.
        if (state.Kind == cmd.Kind) yield break;
        yield return new DraftEvents.V1.DraftKindChanged(cmd.Kind, Now);
    }

    public static IEnumerable<object> Confirm(DraftState state, object[] _, ConfirmDraft cmd)
    {
        GuardOpen(state, "ConfirmDraft");
        if (cmd.ResultEntityId == Guid.Empty) throw new DomainException("ConfirmDraft: ResultEntityId required.");
        yield return new DraftEvents.V1.DraftConfirmed(cmd.ResultEntityId, Now);
    }

    public static IEnumerable<object> Discard(DraftState state, object[] _, DiscardDraft cmd)
    {
        GuardOpen(state, "DiscardDraft");
        yield return new DraftEvents.V1.DraftDiscarded(cmd.Reason, Now);
    }

    static void GuardOpen(DraftState state, string command)
    {
        if (state.Status != DraftStatus.Open)
            throw new DomainException($"{command}: el borrador ya fue {(state.Status == DraftStatus.Confirmed ? "confirmado" : "descartado")}.");
    }

    static StreamName Stream(Guid id) => new($"draft-{id}");
    static string Now => DateTime.UtcNow.ToString("O");
}
