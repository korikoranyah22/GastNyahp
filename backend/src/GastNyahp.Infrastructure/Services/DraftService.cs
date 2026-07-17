using System.Text.RegularExpressions;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Drafts;
using GastNyahp.Domain.Expenses;
using GastNyahp.Domain.Installments;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Drafts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

/// <summary>
/// Borradores conversacionales (DOMAIN_MODEL.md §19): un agente (Telegram vía MCP) o la UI los moldea de a
/// poco y, al confirmar, este service dispara el comando REAL (gasto/ticket/cuotas) con todos sus guards —
/// recién ahí la carga entra a la contabilidad. La validación al confirmar es deliberadamente indulgente con
/// lo opcional (fecha=hoy, categoría=Desconocido, medio=Efectivo) y estricta con lo esencial (montos, ítems,
/// tarjeta de cuotas) y con referencias a entidades de la familia.
/// </summary>
public class DraftService(
    DraftCommandService commands,
    ExpenseService expenseService,
    TicketService ticketService,
    InstallmentService installmentService,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<DraftService> logger)
{
    public async Task<List<DraftEntity>> GetAsync(Guid familyId, bool onlyOpen = true, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Drafts.Where(d => d.FamilyId == familyId);
        if (onlyOpen) query = query.Where(d => d.Status == nameof(DraftStatus.Open));
        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
    }

    public async Task<DraftEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Drafts.FirstOrDefaultAsync(d => d.Id == id && d.FamilyId == familyId, ct);
    }

    public async Task<OpResult> CreateAsync(
        Guid familyId, DraftKind kind, DraftPayload payload, string createdByKind, Guid createdById, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        return await CommandExecutor.Exec(
            commands.Handle(new CreateDraft(id, familyId, kind, payload, createdByKind, createdById), ct),
            sync, logger, "CreateDraft", id, ct);
    }

    public async Task<OpResult> UpdateAsync(Guid familyId, Guid id, DraftPayload payload, CancellationToken ct = default)
    {
        if (await GetByIdAsync(familyId, id, ct) is null) return OpResult.Fail("El borrador no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdateDraft(id, payload), ct), sync, logger, "UpdateDraft", id, ct);
    }

    /// <summary>
    /// Convierte un borrador abierto a otro tipo (gasto → cuotas, típicamente: la compra resultó financiada y se
    /// supo a mitad de la conversación). No toca el payload: los campos del tipo nuevo se completan con UpdateAsync,
    /// y ConfirmAsync ya dice qué falta.
    /// </summary>
    public async Task<OpResult> ChangeKindAsync(Guid familyId, Guid id, DraftKind kind, CancellationToken ct = default)
    {
        if (await GetByIdAsync(familyId, id, ct) is null) return OpResult.Fail("El borrador no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ChangeDraftKind(id, kind), ct), sync, logger, "ChangeDraftKind", id, ct);
    }

    public async Task<OpResult> DiscardAsync(Guid familyId, Guid id, string? reason = null, CancellationToken ct = default)
    {
        if (await GetByIdAsync(familyId, id, ct) is null) return OpResult.Fail("El borrador no existe.");
        return await CommandExecutor.Exec(commands.Handle(new DiscardDraft(id, reason), ct), sync, logger, "DiscardDraft", id, ct);
    }

    /// <summary>
    /// Dos streams, como el create+redeem de FamilyService: primero el comando REAL (si sus guards rechazan,
    /// el borrador queda Open y el error vuelve al agente para seguir moldeando), después el ConfirmDraft.
    /// Si el segundo paso fallara (doble confirm concurrente), la carga real ya existe y el borrador queda
    /// Open para descartar a mano — preferible a confirmar un borrador cuya carga nunca se registró.
    /// </summary>
    public async Task<OpResult> ConfirmAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(familyId, id, ct);
        if (entity is null) return OpResult.Fail("El borrador no existe.");
        if (entity.Status != nameof(DraftStatus.Open))
            return OpResult.Fail($"El borrador ya fue {(entity.Status == nameof(DraftStatus.Confirmed) ? "confirmado" : "descartado")}.");

        var payload = DraftProjection.Deserialize(entity.PayloadJson);
        var date = payload.Date ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!Regex.IsMatch(date, @"^\d{4}-\d{2}-\d{2}$"))
            return OpResult.Fail("La fecha del borrador es inválida (esperaba yyyy-MM-dd).");

        var (owner, ownerError) = await ResolveOwnerAsync(familyId, payload.OwnerKind, payload.OwnerPersonId, ct);
        if (ownerError is not null) return OpResult.Fail(ownerError);

        var register = Enum.Parse<DraftKind>(entity.Kind) switch
        {
            DraftKind.Expense => await RegisterExpenseAsync(familyId, payload, date, owner!, ct),
            DraftKind.Ticket => await RegisterTicketAsync(familyId, payload, date, ct),
            _ => await RegisterInstallmentAsync(familyId, payload, date, owner!, ct),
        };
        if (!register.Ok) return register;

        var confirm = await CommandExecutor.Exec(
            commands.Handle(new ConfirmDraft(id, register.Id!.Value), ct), sync, logger, "ConfirmDraft", id, ct);
        if (!confirm.Ok)
            logger.LogError("La carga {EntityId} se registró pero el borrador {DraftId} no pudo confirmarse: {Error}",
                register.Id, id, confirm.Error);

        return OpResult.Success(register.Id);
    }

    async Task<OpResult> RegisterExpenseAsync(Guid familyId, DraftPayload p, string date, OwnerRef owner, CancellationToken ct)
    {
        if (p.Amount is not > 0) return OpResult.Fail("El borrador no tiene monto (> 0) — completalo antes de confirmar.");
        if (string.IsNullOrWhiteSpace(p.Description)) return OpResult.Fail("El borrador no tiene descripción.");
        if (!TryParseCurrency(p.Currency, out var currency)) return OpResult.Fail($"Moneda '{p.Currency}' desconocida (Ars o Usd).");

        var (paymentMethod, pmError) = await ResolvePaymentMethodAsync(familyId, p, ct);
        if (pmError is not null) return OpResult.Fail(pmError);

        return await expenseService.RegisterAsync(
            familyId, date, p.Description, ValidCategory(p.Category), p.Amount.Value, currency, paymentMethod!, owner, ct);
    }

    async Task<OpResult> RegisterTicketAsync(Guid familyId, DraftPayload p, string date, CancellationToken ct)
    {
        var items = (p.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Description) && i.Amount is > 0)
            .ToList();
        if (items.Count == 0) return OpResult.Fail("El borrador no tiene ítems válidos (descripción + monto > 0) — agregalos antes de confirmar.");

        var inputs = new List<TicketItemInput>(items.Count);
        foreach (var i in items)
        {
            var (itemOwner, itemOwnerError) = await ResolveOwnerAsync(familyId, i.OwnerKind, i.OwnerPersonId, ct);
            if (itemOwnerError is not null) return OpResult.Fail($"Ítem '{i.Description}': {itemOwnerError}");
            inputs.Add(new TicketItemInput(Guid.NewGuid(), i.Description!.Trim(), i.Amount!.Value,
                ValidCategory(i.Category), itemOwner!.Kind, itemOwner.PersonId));
        }

        var (paymentMethod, pmError) = await ResolvePaymentMethodAsync(familyId, p, ct);
        if (pmError is not null) return OpResult.Fail(pmError);

        return await ticketService.RegisterAsync(
            familyId, date, string.IsNullOrWhiteSpace(p.Description) ? "Compra" : p.Description, paymentMethod!,
            p.Discount ?? 0, inputs, ct);
    }

    async Task<OpResult> RegisterInstallmentAsync(Guid familyId, DraftPayload p, string date, OwnerRef owner, CancellationToken ct)
    {
        if (p.CardId is null) return OpResult.Fail("El borrador no indica con qué tarjeta es la compra en cuotas.");
        if (p.MonthlyAmount is not > 0) return OpResult.Fail("El borrador no tiene el monto de la cuota mensual (> 0).");
        if (p.TotalInstallments is not > 0) return OpResult.Fail("El borrador no indica en cuántas cuotas es la compra.");
        if (string.IsNullOrWhiteSpace(p.Description)) return OpResult.Fail("El borrador no tiene descripción.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.CreditCards.AnyAsync(c => c.Id == p.CardId && c.FamilyId == familyId, ct))
            return OpResult.Fail("La tarjeta del borrador no existe en la familia.");

        var startMonth = p.StartMonth ?? date[..7];
        return await installmentService.RegisterAsync(
            familyId, p.CardId.Value, p.Description, ValidCategory(p.Category), date,
            InstallmentFrequency.Fixed, p.MonthlyAmount.Value, p.TotalInstallments, startMonth, owner, ct);
    }

    // ── Resolución indulgente-pero-verificada de los campos opcionales ──────────

    static string ValidCategory(string? category) =>
        category is not null && AppCategories.IsValidExpenseCategory(category) ? category : "Desconocido";

    static bool TryParseCurrency(string? raw, out ExpenseCurrency currency)
    {
        currency = ExpenseCurrency.Ars;
        return raw is null || Enum.TryParse(raw, ignoreCase: true, out currency);
    }

    async Task<(PaymentMethod? Method, string? Error)> ResolvePaymentMethodAsync(Guid familyId, DraftPayload p, CancellationToken ct)
    {
        PaymentMethod method;
        try { method = PaymentMethod.FromPrimitive(p.PaymentMethodKind ?? "Cash", p.PaymentMethodReferenceId); }
        catch (Eventuous.DomainException ex) { return (null, ex.Message); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (method is PaymentMethod.Card card && !await db.CreditCards.AnyAsync(c => c.Id == card.CardId && c.FamilyId == familyId, ct))
            return (null, "La tarjeta del borrador no existe en la familia.");
        if (method is PaymentMethod.Debit debit && !await db.Banks.AnyAsync(b => b.Id == debit.BankId && b.FamilyId == familyId, ct))
            return (null, "El banco de débito del borrador no existe en la familia.");
        return (method, null);
    }

    async Task<(OwnerRef? Owner, string? Error)> ResolveOwnerAsync(Guid familyId, string? kind, Guid? personId, CancellationToken ct)
    {
        OwnerRef owner;
        try { owner = OwnerRef.FromPrimitive(kind ?? "Unassigned", personId); }
        catch (Eventuous.DomainException ex) { return (null, ex.Message); }

        if (owner.PersonId is Guid pid)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            if (!await db.People.AnyAsync(p => p.Id == pid && p.FamilyId == familyId, ct))
                return (null, "La persona asignada en el borrador no existe en la familia.");
        }
        return (owner, null);
    }
}
