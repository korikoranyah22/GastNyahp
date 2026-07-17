using System.ComponentModel;
using System.Text;
using GastNyahp.Api.Auth;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Drafts;
using GastNyahp.Infrastructure.Projections.Drafts;
using GastNyahp.Infrastructure.Projections.People;
using GastNyahp.Infrastructure.Services;
using ModelContextProtocol.Server;

namespace GastNyahp.Api.Mcp;

/// <summary>
/// La superficie conversacional de los borradores (DOMAIN_MODEL.md §19): el usuario le habla al agente por
/// Telegram mientras hace la fila del super, el agente moldea el borrador tool a tool ("agregá carne 30000",
/// "me descontaron 20%"), y recién con borrador_confirmar la carga entra a la contabilidad con todos los
/// guards del dominio. Los errores de confirmación dicen exactamente qué falta — son parte del diálogo.
/// </summary>
[McpServerToolType]
public sealed class DraftTools
{
    [McpServerTool(Name = "borrador_crear")]
    [Description("Crea un borrador de carga para ir completando por conversación. Tipos: 'ticket' (compra con ítems, ej. supermercado), 'gasto' (gasto simple) o 'cuotas' (compra en cuotas con tarjeta). El borrador NO afecta la contabilidad hasta confirmarlo con borrador_confirmar.")]
    public static async Task<string> BorradorCrear(
        DraftService drafts,
        IHttpContextAccessor http,
        [Description("Tipo: gasto | ticket | cuotas.")] string tipo,
        [Description("Descripción corta, ej. 'Super Coto'.")] string? descripcion = null,
        [Description("Fecha yyyy-MM-dd. Si se omite, al confirmar se usa el día de la confirmación.")] string? fecha = null,
        [Description("Nota libre para retomar el contexto de la conversación.")] string? nota = null)
    {
        var kind = ParseTipo(tipo);
        if (kind is null) return $"Error: tipo '{tipo}' desconocido (gasto, ticket o cuotas).";

        var ctx = http.HttpContext!;
        var result = await drafts.CreateAsync(ctx.GetFamilyId(), kind.Value,
            new DraftPayload(Date: fecha, Description: descripcion, Note: nota),
            ctx.IsAgent() ? "Agent" : "Member", ctx.GetMemberId());

        return result.Ok
            ? $"Borrador de {tipo} creado: {result.Id}. Completalo con borrador_actualizar{(kind == DraftKind.Ticket ? " y borrador_item_agregar" : "")}, y cerralo con borrador_confirmar."
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "borrador_actualizar")]
    [Description("Actualiza campos de un borrador abierto — solo pisa lo que se pasa, el resto queda como estaba. Para agregar ítems a un ticket usar borrador_item_agregar.")]
    public static async Task<string> BorradorActualizar(
        DraftService drafts,
        CardService cards,
        BankService banks,
        PersonService people,
        IHttpContextAccessor http,
        [Description("Id del borrador.")] string borradorId,
        [Description("CONVIERTE el borrador a otro tipo (gasto | ticket | cuotas). Usalo cuando a mitad de la carga aparece que la compra era en cuotas: NO descartes el borrador ni lo cargues en una sola cuota — convertilo y pasá tarjeta/cuotaMensual/totalCuotas en esta misma llamada.")] string? tipo = null,
        [Description("Descripción corta.")] string? descripcion = null,
        [Description("Fecha yyyy-MM-dd.")] string? fecha = null,
        [Description("Categoría exacta (Comida, Hogar, Limpieza, … o Desconocido).")] string? categoria = null,
        [Description("Monto total en pesos (solo borradores de gasto).")] decimal? monto = null,
        [Description("Medio de pago: Efectivo, MODO, MercadoPago, Tarjeta o Débito.")] string? medio = null,
        [Description("Si el medio es Tarjeta: nombre exacto de la tarjeta. Si es Débito: nombre exacto del banco.")] string? referencia = null,
        [Description("Descuento en pesos sobre el total (tickets, ej. promos).")] decimal? descuento = null,
        [Description("De quién es el gasto: 'compartido', 'sin asignar', o el nombre exacto de una persona (usar personas_listar). En un ticket, el dueño de cada ítem se pasa en borrador_item_agregar.")] string? dueno = null,
        [Description("Para cuotas: nombre exacto de la tarjeta de la compra.")] string? tarjeta = null,
        [Description("Para cuotas: monto de cada cuota mensual.")] decimal? cuotaMensual = null,
        [Description("Para cuotas: cantidad total de cuotas.")] int? totalCuotas = null,
        [Description("Para cuotas: mes de la primera cuota (yyyy-MM).")] string? mesInicio = null,
        [Description("Nota libre de contexto.")] string? nota = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (draft, payload, error) = await CargarAbierto(drafts, familyId, borradorId);
        if (error is not null) return error;

        // La conversión va PRIMERO: el resto de la llamada (y el Describir final) tienen que hablar del tipo nuevo.
        var kind = draft!.Kind;
        if (tipo is not null)
        {
            var nuevoKind = ParseTipo(tipo);
            if (nuevoKind is null) return $"Error: tipo '{tipo}' desconocido (gasto, ticket o cuotas).";
            var changed = await drafts.ChangeKindAsync(familyId, draft.Id, nuevoKind.Value);
            if (!changed.Ok) return $"Error: {changed.Error}";
            kind = nuevoKind.Value.ToString();
        }

        if (medio is not null)
        {
            var (method, medioError) = await ResolverMedio(cards, banks, familyId, medio, referencia);
            if (medioError is not null) return medioError;
            payload = payload! with { PaymentMethodKind = method!.Kind, PaymentMethodReferenceId = method.ReferenceId };
        }
        if (tarjeta is not null)
        {
            var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(tarjeta, StringComparison.OrdinalIgnoreCase));
            if (card is null) return $"Error: no existe una tarjeta llamada '{tarjeta}'. Usá tarjetas_listar.";
            payload = payload! with { CardId = card.Id };
        }
        if (dueno is not null)
        {
            var (owner, duenoError) = await ResolverDueno(people, familyId, dueno);
            if (duenoError is not null) return duenoError;
            payload = payload! with { OwnerKind = owner!.Kind, OwnerPersonId = owner.PersonId };
        }

        payload = payload! with
        {
            Description = descripcion ?? payload.Description,
            Date = fecha ?? payload.Date,
            Category = categoria ?? payload.Category,
            Amount = monto ?? payload.Amount,
            Discount = descuento ?? payload.Discount,
            MonthlyAmount = cuotaMensual ?? payload.MonthlyAmount,
            TotalInstallments = totalCuotas ?? payload.TotalInstallments,
            StartMonth = mesInicio ?? payload.StartMonth,
            Note = nota ?? payload.Note,
        };

        var result = await drafts.UpdateAsync(familyId, draft.Id, payload);
        if (!result.Ok) return $"Error: {result.Error}";

        var encabezado = tipo is null ? "Borrador actualizado." : $"Borrador convertido a {TipoDe(kind)}.";
        return $"{encabezado}\n{await Describir(cards, banks, people, familyId, kind, payload)}";
    }

    [McpServerTool(Name = "borrador_item_agregar")]
    [Description("Agrega un ítem a un borrador de ticket — pensado para ir cargando el changuito a medida que el usuario dicta.")]
    public static async Task<string> BorradorItemAgregar(
        DraftService drafts,
        PersonService people,
        IHttpContextAccessor http,
        [Description("Id del borrador.")] string borradorId,
        [Description("Descripción del ítem, ej. 'Carne'.")] string descripcion,
        [Description("Monto del ítem en pesos.")] decimal monto,
        [Description("Categoría exacta del ítem; si se omite queda Desconocido.")] string? categoria = null,
        [Description("De quién es el ítem: 'compartido', 'sin asignar', o el nombre exacto de una persona (usar personas_listar). Si se omite queda sin asignar.")] string? dueno = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (draft, payload, error) = await CargarAbierto(drafts, familyId, borradorId);
        if (error is not null) return error;

        var (owner, ownerError) = await ResolverDueno(people, familyId, dueno);
        if (ownerError is not null) return ownerError;

        var items = (payload!.Items ?? [])
            .Append(new DraftTicketItem(descripcion, monto, categoria, owner?.Kind, owner?.PersonId))
            .ToList();
        var result = await drafts.UpdateAsync(familyId, draft!.Id, payload with { Items = items });
        return result.Ok
            ? $"Ítem agregado ({items.Count} en total): {descripcion} ${monto:N0}. Subtotal: ${items.Sum(i => i.Amount ?? 0):N0}."
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "borrador_item_dueno")]
    [Description("Asigna de quién es un ítem YA cargado del ticket, por su número. Es la que cierra el diálogo 'lo anoto ahora, te pregunto de quién es después' sin tener que quitarlo y volver a agregarlo.")]
    public static async Task<string> BorradorItemDueno(
        DraftService drafts,
        PersonService people,
        IHttpContextAccessor http,
        [Description("Id del borrador.")] string borradorId,
        [Description("Número del ítem según borradores_listar (empieza en 1).")] int numero,
        [Description("'compartido', 'sin asignar', o el nombre exacto de una persona (usar personas_listar).")] string dueno)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (draft, payload, error) = await CargarAbierto(drafts, familyId, borradorId);
        if (error is not null) return error;

        var items = (payload!.Items ?? []).ToList();
        if (numero < 1 || numero > items.Count) return $"Error: el borrador tiene {items.Count} ítems — no existe el número {numero}.";

        var (owner, ownerError) = await ResolverDueno(people, familyId, dueno);
        if (ownerError is not null) return ownerError;

        var target = items[numero - 1];
        items[numero - 1] = target with { OwnerKind = owner!.Kind, OwnerPersonId = owner.PersonId };

        var result = await drafts.UpdateAsync(familyId, draft!.Id, payload with { Items = items });
        if (!result.Ok) return $"Error: {result.Error}";

        var restantes = items.Count(i => i.OwnerKind is null or "Unassigned");
        return $"'{target.Description}' ahora es de {dueno}." +
               (restantes > 0 ? $" Quedan {restantes} ítem(s) sin dueño." : " Ya todos los ítems tienen dueño.");
    }

    [McpServerTool(Name = "borrador_item_quitar")]
    [Description("Quita un ítem de un borrador de ticket, por su número en el listado (1 = el primero).")]
    public static async Task<string> BorradorItemQuitar(
        DraftService drafts,
        IHttpContextAccessor http,
        [Description("Id del borrador.")] string borradorId,
        [Description("Número del ítem según borradores_listar (empieza en 1).")] int numero)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (draft, payload, error) = await CargarAbierto(drafts, familyId, borradorId);
        if (error is not null) return error;

        var items = (payload!.Items ?? []).ToList();
        if (numero < 1 || numero > items.Count) return $"Error: el borrador tiene {items.Count} ítems — no existe el número {numero}.";
        var removed = items[numero - 1];
        items.RemoveAt(numero - 1);

        var result = await drafts.UpdateAsync(familyId, draft!.Id, payload with { Items = items });
        return result.Ok ? $"Ítem quitado: {removed.Description}. Quedan {items.Count}." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "borradores_listar")]
    [Description("Lista los borradores abiertos de la familia, con sus ítems numerados y lo que les falta para poder confirmarse.")]
    public static async Task<string> BorradoresListar(
        DraftService drafts,
        CardService cards,
        BankService banks,
        PersonService people,
        IHttpContextAccessor http)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var list = await drafts.GetAsync(familyId, onlyOpen: true);
        if (list.Count == 0) return "No hay borradores abiertos.";

        var sb = new StringBuilder($"Borradores abiertos ({list.Count}):\n");
        foreach (var d in list)
        {
            sb.AppendLine($"— {d.Id} [{TipoDe(d.Kind)}]");
            sb.AppendLine(await Describir(cards, banks, people, familyId, d.Kind, DraftProjection.Deserialize(d.PayloadJson)));
        }
        return sb.ToString();
    }

    [McpServerTool(Name = "borrador_confirmar")]
    [Description("Confirma un borrador: dispara la carga REAL (gasto, ticket o compra en cuotas) con todas las validaciones del dominio. Si falta algo, el error dice exactamente qué — completalo con borrador_actualizar y reintentá.")]
    public static async Task<string> BorradorConfirmar(
        DraftService drafts,
        IHttpContextAccessor http,
        [Description("Id del borrador.")] string borradorId)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (!Guid.TryParse(borradorId, out var id)) return "Error: el id del borrador no es un GUID válido.";

        var draft = await drafts.GetByIdAsync(familyId, id);
        if (draft is null) return "Error: el borrador no existe.";

        var result = await drafts.ConfirmAsync(familyId, id);
        return result.Ok
            ? $"Confirmado: el borrador se cargó como {TipoDe(draft.Kind)} (id {result.Id})."
            : $"Todavía no se puede confirmar: {result.Error}";
    }

    [McpServerTool(Name = "borrador_descartar")]
    [Description("Descarta un borrador abierto — no afecta la contabilidad, queda en el historial como descartado.")]
    public static async Task<string> BorradorDescartar(
        DraftService drafts,
        IHttpContextAccessor http,
        [Description("Id del borrador.")] string borradorId,
        [Description("Motivo, opcional.")] string? motivo = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (!Guid.TryParse(borradorId, out var id)) return "Error: el id del borrador no es un GUID válido.";

        var result = await drafts.DiscardAsync(familyId, id, motivo);
        return result.Ok ? "Borrador descartado." : $"Error: {result.Error}";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    static DraftKind? ParseTipo(string tipo) => tipo.Trim().ToLowerInvariant() switch
    {
        "gasto" => DraftKind.Expense,
        "ticket" => DraftKind.Ticket,
        "cuotas" => DraftKind.Installment,
        _ => null,
    };

    static string TipoDe(string kind) => kind switch
    {
        nameof(DraftKind.Expense) => "gasto",
        nameof(DraftKind.Ticket) => "ticket",
        _ => "compra en cuotas",
    };

    static async Task<(DraftEntity? Draft, DraftPayload? Payload, string? Error)> CargarAbierto(
        DraftService drafts, Guid familyId, string borradorId)
    {
        if (!Guid.TryParse(borradorId, out var id)) return (null, null, "Error: el id del borrador no es un GUID válido.");
        var draft = await drafts.GetByIdAsync(familyId, id);
        if (draft is null) return (null, null, "Error: el borrador no existe. Usá borradores_listar.");
        if (draft.Status != nameof(DraftStatus.Open)) return (null, null, $"Error: el borrador ya fue {(draft.Status == nameof(DraftStatus.Confirmed) ? "confirmado" : "descartado")}.");
        return (draft, DraftProjection.Deserialize(draft.PayloadJson), null);
    }

    /// <summary>
    /// "compartido" | "sin asignar" | nombre de persona → <see cref="OwnerRef"/>. Mismo criterio que
    /// <see cref="ResolverMedio"/>: el agente habla en nombres, no en GUIDs, y el error dice qué hacer.
    /// null (sin especificar) ⇒ null owner: el caller deja el campo como estaba / sin asignar.
    /// </summary>
    internal static async Task<(OwnerRef? Owner, string? Error)> ResolverDueno(
        PersonService people, Guid familyId, string? dueno)
    {
        if (dueno is null) return (null, null);

        switch (dueno.Trim().ToLowerInvariant())
        {
            case "compartido" or "compartida" or "shared":
                return (OwnerRef.SharedOwner, null);
            case "sin asignar" or "nadie" or "unassigned" or "":
                return (OwnerRef.None, null);
            default:
                var person = (await people.GetAllAsync(familyId))
                    .FirstOrDefault(p => p.Name.Equals(dueno.Trim(), StringComparison.OrdinalIgnoreCase));
                return person is null
                    ? (null, $"Error: no existe una persona llamada '{dueno}'. Usá personas_listar (o 'compartido' / 'sin asignar').")
                    : (OwnerRef.Of(person.Id), null);
        }
    }

    internal static async Task<(PaymentMethod? Method, string? Error)> ResolverMedio(
        CardService cards, BankService banks, Guid familyId, string medio, string? referencia)
    {
        switch (medio.Trim().ToLowerInvariant())
        {
            case "efectivo": return (PaymentMethod.CashPayment, null);
            case "modo": return (PaymentMethod.ModoPayment, null);
            case "mercadopago": return (PaymentMethod.MercadoPagoPayment, null);
            case "tarjeta":
                var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(referencia, StringComparison.OrdinalIgnoreCase));
                return card is null
                    ? (null, $"Error: no existe una tarjeta llamada '{referencia}'. Usá tarjetas_listar.")
                    : (PaymentMethod.ByCard(card.Id), null);
            case "débito" or "debito":
                var bank = (await banks.GetAllAsync(familyId)).FirstOrDefault(b => b.Name.Equals(referencia, StringComparison.OrdinalIgnoreCase));
                return bank is null
                    ? (null, $"Error: no existe un banco llamado '{referencia}'. Usá bancos_listar.")
                    : (PaymentMethod.ByDebit(bank.Id), null);
            default:
                return (null, $"Error: medio de pago '{medio}' desconocido (Efectivo, MODO, MercadoPago, Tarjeta o Débito).");
        }
    }

    /// <summary>"Compartido" | "Sin asignar" | el nombre de la persona — para que el agente lea el dueño, no un GUID.</summary>
    static string DuenoLegible(IReadOnlyList<PersonEntity> people, string? kind, Guid? personId) => kind switch
    {
        "Shared" => "compartido",
        "Owner" => people.FirstOrDefault(p => p.Id == personId)?.Name ?? "?",
        _ => "sin asignar",
    };

    static async Task<string> Describir(CardService cards, BankService banks, PersonService people, Guid familyId, string kind, DraftPayload p)
    {
        var personas = await people.GetAllAsync(familyId);
        var sb = new StringBuilder();
        sb.Append($"  {p.Description ?? "(sin descripción)"}");
        if (p.Date is not null) sb.Append($" — {p.Date}");
        if (p.Category is not null) sb.Append($" ({p.Category})");
        sb.AppendLine();

        if (p.PaymentMethodKind is not null)
            sb.AppendLine($"  Medio: {await MedioLegible(cards, banks, familyId, p)}");
        if (p.OwnerKind is not null and not "Unassigned")
            sb.AppendLine($"  Dueño: {DuenoLegible(personas, p.OwnerKind, p.OwnerPersonId)}");

        switch (kind)
        {
            case nameof(DraftKind.Expense):
                sb.AppendLine(p.Amount is > 0 ? $"  Monto: ${p.Amount:N0}" : "  FALTA: monto.");
                break;
            case nameof(DraftKind.Ticket):
                var items = p.Items ?? [];
                if (items.Count == 0) sb.AppendLine("  FALTA: ítems (usar borrador_item_agregar).");
                else
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        var it = items[i];
                        // El dueño se muestra por ítem: "sin dueño" es lo que el agente todavía tiene que preguntar.
                        var dueno = it.OwnerKind is null or "Unassigned"
                            ? "sin dueño"
                            : DuenoLegible(personas, it.OwnerKind, it.OwnerPersonId);
                        sb.AppendLine($"  {i + 1}. {it.Description} ${it.Amount ?? 0:N0}" +
                                      $"{(it.Category is null ? "" : $" ({it.Category})")} — {dueno}");
                    }
                    var sinDueno = items.Count(i => i.OwnerKind is null or "Unassigned");
                    if (sinDueno > 0)
                        sb.AppendLine($"  FALTA (opcional): {sinDueno} ítem(s) sin dueño — preguntá de quién son y cargalos con borrador_item_agregar/dueno.");
                    var total = Math.Max(0, items.Sum(i => i.Amount ?? 0) - (p.Discount ?? 0));
                    sb.AppendLine($"  Total: ${total:N0}{(p.Discount is > 0 ? $" (descuento ${p.Discount:N0})" : "")}");
                }
                break;
            default:
                var faltan = new List<string>();
                if (p.CardId is null) faltan.Add("tarjeta");
                if (p.MonthlyAmount is not > 0) faltan.Add("cuota mensual");
                if (p.TotalInstallments is not > 0) faltan.Add("total de cuotas");
                sb.AppendLine(faltan.Count > 0
                    ? $"  FALTA: {string.Join(", ", faltan)}."
                    : $"  {p.TotalInstallments} cuotas de ${p.MonthlyAmount:N0} desde {p.StartMonth ?? "(mes de la fecha)"}");
                break;
        }
        if (p.Note is not null) sb.AppendLine($"  Nota: {p.Note}");
        return sb.ToString().TrimEnd();
    }

    static async Task<string> MedioLegible(CardService cards, BankService banks, Guid familyId, DraftPayload p)
    {
        switch (p.PaymentMethodKind)
        {
            case "Card":
                var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Id == p.PaymentMethodReferenceId);
                return $"Tarjeta {card?.Label ?? "?"}";
            case "Debit":
                var bank = (await banks.GetAllAsync(familyId)).FirstOrDefault(b => b.Id == p.PaymentMethodReferenceId);
                return $"Débito {bank?.Name ?? "?"}";
            default:
                return p.PaymentMethodKind == "Cash" ? "Efectivo" : p.PaymentMethodKind!;
        }
    }
}
