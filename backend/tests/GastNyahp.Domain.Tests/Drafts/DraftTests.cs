using Eventuous;
using GastNyahp.Domain.Drafts;

namespace GastNyahp.Domain.Tests.Drafts;

public class DraftTests
{
    static readonly Guid DraftId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid MemberId = Guid.NewGuid();

    static CreateDraft ValidDraft(DraftKind kind = DraftKind.Ticket) =>
        new(DraftId, FamilyId, kind, new DraftPayload(Description: "Super"), "Agent", MemberId);

    static DraftState OpenDraft() => new DraftState().When(DraftCommandService.Create(ValidDraft()).Single());

    [Fact]
    public void Create_without_family_throws() =>
        Assert.Throws<DomainException>(() => DraftCommandService.Create(ValidDraft() with { FamilyId = Guid.Empty }).ToList());

    [Fact]
    public void Create_starts_open_with_the_payload_and_kind()
    {
        var state = OpenDraft();
        Assert.Equal(DraftStatus.Open, state.Status);
        Assert.Equal(DraftKind.Ticket, state.Kind);
        Assert.Equal("Super", state.Payload.Description);
        Assert.Equal(FamilyId, state.FamilyId);
    }

    [Fact]
    public void Update_replaces_the_whole_payload_snapshot()
    {
        // "me descontaron 20%" — el agente re-manda el payload moldeado; el stream guarda cada versión.
        var state = OpenDraft();
        var reshaped = new DraftPayload(
            Description: "Super",
            Discount: 6400,
            Items: [new DraftTicketItem("Carne", 30000, "Comida"), new DraftTicketItem("Lavandina", 2000, "Limpieza")]);
        state = state.When(DraftCommandService.Update(state, [], new UpdateDraft(DraftId, reshaped)).Single());

        Assert.Equal(6400, state.Payload.Discount);
        Assert.Equal(2, state.Payload.Items!.Count);
        Assert.Equal(DraftStatus.Open, state.Status);
    }

    [Fact]
    public void Confirm_records_the_resulting_entity()
    {
        var ticketId = Guid.NewGuid();
        var state = OpenDraft();
        state = state.When(DraftCommandService.Confirm(state, [], new ConfirmDraft(DraftId, ticketId)).Single());

        Assert.Equal(DraftStatus.Confirmed, state.Status);
        Assert.Equal(ticketId, state.ResultEntityId);
    }

    [Fact]
    public void Confirm_requires_the_resulting_entity_id() =>
        Assert.Throws<DomainException>(() => DraftCommandService.Confirm(OpenDraft(), [], new ConfirmDraft(DraftId, Guid.Empty)).ToList());

    [Fact]
    public void Discard_closes_the_draft()
    {
        var state = OpenDraft();
        state = state.When(DraftCommandService.Discard(state, [], new DiscardDraft(DraftId, "me arrepentí")).Single());
        Assert.Equal(DraftStatus.Discarded, state.Status);
    }

    [Fact]
    public void Commands_after_confirm_or_discard_throw()
    {
        var confirmed = OpenDraft();
        confirmed = confirmed.When(DraftCommandService.Confirm(confirmed, [], new ConfirmDraft(DraftId, Guid.NewGuid())).Single());
        Assert.Throws<DomainException>(() => DraftCommandService.Update(confirmed, [], new UpdateDraft(DraftId, new DraftPayload())).ToList());
        Assert.Throws<DomainException>(() => DraftCommandService.Confirm(confirmed, [], new ConfirmDraft(DraftId, Guid.NewGuid())).ToList());

        var discarded = OpenDraft();
        discarded = discarded.When(DraftCommandService.Discard(discarded, [], new DiscardDraft(DraftId)).Single());
        Assert.Throws<DomainException>(() => DraftCommandService.Confirm(discarded, [], new ConfirmDraft(DraftId, Guid.NewGuid())).ToList());
    }

    // ── ChangeKind ──────────────────────────────────────────────────────────────
    // "compré una tele, 600 lucas… ah, en 6 cuotas con la Visa": el tipo se sabe DESPUÉS de arrancar la carga.
    // Sin esto el agente queda preso del tipo que eligió en el primer mensaje y termina cargando la compra en
    // una sola cuota (que es exactamente el bug que motivó el comando).

    [Fact]
    public void ChangeKind_converts_an_open_draft()
    {
        var state = new DraftState().When(DraftCommandService.Create(ValidDraft(DraftKind.Expense)).Single());
        state = state.When(DraftCommandService.ChangeKind(state, [], new ChangeDraftKind(DraftId, DraftKind.Installment)).Single());

        Assert.Equal(DraftKind.Installment, state.Kind);
    }

    [Fact]
    public void ChangeKind_keeps_the_payload_untouched()
    {
        // La conversión no pierde lo ya cargado: los campos de cada tipo conviven en el mismo payload.
        var state = new DraftState().When(DraftCommandService.Create(ValidDraft(DraftKind.Expense)).Single());
        state = state.When(DraftCommandService.ChangeKind(state, [], new ChangeDraftKind(DraftId, DraftKind.Installment)).Single());

        Assert.Equal("Super", state.Payload.Description);
    }

    [Fact]
    public void ChangeKind_to_the_same_kind_emits_nothing()
    {
        // Idempotente: convertir al tipo que ya tiene no ensucia el stream con un evento sin efecto.
        var state = OpenDraft();   // Ticket
        Assert.Empty(DraftCommandService.ChangeKind(state, [], new ChangeDraftKind(DraftId, DraftKind.Ticket)));
    }

    [Fact]
    public void ChangeKind_after_confirm_or_discard_throws()
    {
        var confirmed = OpenDraft();
        confirmed = confirmed.When(DraftCommandService.Confirm(confirmed, [], new ConfirmDraft(DraftId, Guid.NewGuid())).Single());
        Assert.Throws<DomainException>(() =>
            DraftCommandService.ChangeKind(confirmed, [], new ChangeDraftKind(DraftId, DraftKind.Expense)).ToList());

        var discarded = OpenDraft();
        discarded = discarded.When(DraftCommandService.Discard(discarded, [], new DiscardDraft(DraftId)).Single());
        Assert.Throws<DomainException>(() =>
            DraftCommandService.ChangeKind(discarded, [], new ChangeDraftKind(DraftId, DraftKind.Expense)).ToList());
    }
}
