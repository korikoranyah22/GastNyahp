using System.ComponentModel;
using System.Text;
using GastNyahp.Api.Auth;
using GastNyahp.Api.Controllers;   // ApiConventions: validación de fecha/mes compartida con los endpoints REST
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Domain.Installments;
using GastNyahp.Domain.Services;
using GastNyahp.Infrastructure.Services;
using ModelContextProtocol.Server;

namespace GastNyahp.Api.Mcp;

/// <summary>
/// Corregir lo que YA se cargó. Todo el resto de la superficie conversacional edita <b>borradores</b> (antes de
/// confirmar); una vez confirmado, el gasto o el ticket quedaban intocables por conversación — y equivocarse
/// dictando por audio en la fila del super es justamente lo más probable ("la carne fue 3.000, no 30.000").
///
/// <para><b>Esto SÍ mueve plata</b>, a diferencia de tocar un borrador: el gasto ya está en la contabilidad.
/// Por eso identifica por ID (que sale de los listados), nunca por nombre aproximado.</para>
///
/// <para>El ticket se edita GRANULAR a propósito: <c>TicketService.Update</c> reemplaza el set COMPLETO de ítems
/// (DOMAIN_MODEL §10), así que pedirle al modelo que reenvíe todos es una receta para que se coma uno. Estas
/// tools leen el ticket, cambian lo que se pidió y reenvían el resto intacto.</para>
/// </summary>
[McpServerToolType]
public sealed class CorrectionTools
{
    // ── Gastos ya cargados ───────────────────────────────────────────────────────

    [McpServerTool(Name = "gasto_editar")]
    [Description("Corrige un gasto YA cargado (monto, descripción, categoría, fecha, medio de pago o dueño). Para el id usar gastos_del_mes. Solo cambia lo que se pasa; el resto queda igual. OJO: esto modifica la contabilidad real, no un borrador.")]
    public static async Task<string> GastoEditar(
        ExpenseService expenses,
        CardService cards,
        BankService banks,
        PersonService people,
        IHttpContextAccessor http,
        [Description("Id del gasto (sale de gastos_del_mes).")] string gastoId,
        [Description("Descripción nueva. Si se omite, no cambia.")] string? descripcion = null,
        [Description("Monto nuevo en pesos. Si se omite, no cambia.")] decimal? monto = null,
        [Description("Categoría exacta nueva. Si se omite, no cambia.")] string? categoria = null,
        [Description("Fecha nueva yyyy-MM-dd. Si se omite, no cambia.")] string? fecha = null,
        [Description("Medio de pago nuevo: Efectivo, MODO, MercadoPago, Tarjeta o Débito. Si se omite, no cambia.")] string? medio = null,
        [Description("Si medio es Tarjeta: nombre exacto de la tarjeta. Si es Débito: nombre exacto del banco.")] string? referencia = null,
        [Description("Dueño nuevo: 'compartido', 'sin asignar', o el nombre exacto de una persona. Si se omite, no cambia.")] string? dueno = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (!Guid.TryParse(gastoId, out var id)) return "Error: el id del gasto no es un GUID válido. Usá gastos_del_mes.";

        var gasto = await expenses.GetByIdAsync(familyId, id);
        if (gasto is null) return "Error: el gasto no existe. Usá gastos_del_mes.";

        if (categoria is not null && !AppCategories.IsValidExpenseCategory(categoria))
            return $"Error: categoría '{categoria}' inválida. Válidas: {string.Join(", ", AppCategories.ExpenseCategories)}.";
        if (monto is <= 0) return "Error: el monto tiene que ser mayor a 0.";
        if (fecha is not null && !ApiConventions.IsValidDate(fecha)) return "Error: la fecha tiene que ser yyyy-MM-dd.";

        // Medio y dueño: si no se pasan, se reconstruyen los que ya tenía (Update pide el objeto entero).
        var method = PaymentMethod.FromPrimitive(gasto.PaymentMethodKind, gasto.PaymentMethodReferenceId);
        if (medio is not null)
        {
            var (m, medioError) = await DraftTools.ResolverMedio(cards, banks, familyId, medio, referencia);
            if (medioError is not null) return medioError;
            method = m!;
        }
        var owner = OwnerRef.FromPrimitive(gasto.OwnerKind, gasto.OwnerPersonId);
        if (dueno is not null)
        {
            var (o, duenoError) = await DraftTools.ResolverDueno(people, familyId, dueno);
            if (duenoError is not null) return duenoError;
            owner = o!;
        }

        var currency = Enum.TryParse<ExpenseCurrency>(gasto.OriginalCurrency ?? "Ars", true, out var c) ? c : ExpenseCurrency.Ars;
        var result = await expenses.UpdateAsync(familyId, id,
            fecha ?? gasto.Date,
            descripcion?.Trim() ?? gasto.Description,
            categoria ?? gasto.Category,
            monto ?? gasto.AmountArs,
            currency, method, owner);

        return result.Ok
            ? $"Gasto corregido: {descripcion?.Trim() ?? gasto.Description} ${monto ?? gasto.AmountArs:N0}."
            : $"Error: {result.Error}";
    }

    // ── Tickets ya cargados ──────────────────────────────────────────────────────

    [McpServerTool(Name = "tickets_del_mes")]
    [Description("Lista los tickets (compras con varios ítems) YA cargados de un mes, con su id y sus ítems numerados. Es de donde salen el id y el número de ítem para corregirlos.")]
    public static async Task<string> TicketsDelMes(
        TicketService tickets,
        PersonService people,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM.")] string mes)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (!ApiConventions.IsValidMonth(mes)) return "Error: el mes tiene que ser yyyy-MM.";

        var list = await tickets.GetByMonthAsync(familyId, mes);
        if (list.Count == 0) return $"Sin tickets cargados en {mes}.";

        var personas = await people.GetAllAsync(familyId);
        var sb = new StringBuilder($"Tickets de {mes} ({list.Count}):\n");
        foreach (var t in list)
        {
            var total = Math.Max(0, t.Items.Sum(i => i.Amount) - t.Discount);
            sb.AppendLine($"— {t.Id} · {t.Date}: {t.Description} — total ${total:N0}{(t.Discount > 0 ? $" (descuento ${t.Discount:N0})" : "")}");
            var n = 1;
            foreach (var i in Ordenados(t))
            {
                var dueno = i.OwnerKind is "Owner" ? personas.FirstOrDefault(p => p.Id == i.OwnerPersonId)?.Name ?? "?"
                          : i.OwnerKind is "Shared" ? "compartido" : "sin dueño";
                sb.AppendLine($"   {n++}. {i.Description} ${i.Amount:N0} ({i.Category}) — {dueno}");
            }
        }
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "ticket_item_editar")]
    [Description("Corrige UN ítem de un ticket ya cargado ('la carne fue 3.000, no 30.000'), por su número. Los demás ítems quedan intactos. Para el id y el número usar tickets_del_mes.")]
    public static async Task<string> TicketItemEditar(
        TicketService tickets,
        PersonService people,
        IHttpContextAccessor http,
        [Description("Id del ticket (sale de tickets_del_mes).")] string ticketId,
        [Description("Número del ítem según tickets_del_mes (empieza en 1).")] int numero,
        [Description("Descripción nueva del ítem. Si se omite, no cambia.")] string? descripcion = null,
        [Description("Monto nuevo del ítem. Si se omite, no cambia.")] decimal? monto = null,
        [Description("Categoría exacta nueva del ítem. Si se omite, no cambia.")] string? categoria = null,
        [Description("Dueño nuevo: 'compartido', 'sin asignar', o el nombre exacto de una persona. Si se omite, no cambia.")] string? dueno = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (ticket, error) = await CargarTicket(tickets, familyId, ticketId);
        if (error is not null) return error;

        // MISMO orden estable que usó tickets_del_mes para numerar: si no, el número apunta a otro producto.
        var loadedTicket = ticket!;
        var items = Ordenados(loadedTicket).ToList();
        if (numero < 1 || numero > items.Count) return $"Error: el ticket tiene {items.Count} ítems — no existe el número {numero}.";
        if (categoria is not null && !AppCategories.IsValidExpenseCategory(categoria))
            return $"Error: categoría '{categoria}' inválida. Válidas: {string.Join(", ", AppCategories.ExpenseCategories)}.";
        if (monto is <= 0) return "Error: el monto tiene que ser mayor a 0.";

        var target = items[numero - 1];
        var ownerKind = target.OwnerKind;
        Guid? ownerPersonId = target.OwnerPersonId;
        if (dueno is not null)
        {
            var (o, duenoError) = await DraftTools.ResolverDueno(people, familyId, dueno);
            if (duenoError is not null) return duenoError;
            ownerKind = o!.Kind;
            ownerPersonId = o.PersonId;
        }

        // Update reemplaza el set COMPLETO: reenviamos todos, con el ItemId original de cada uno para no perder identidad.
        var nuevos = items.Select((i, idx) => idx == numero - 1
            ? new TicketItemInput(i.ItemId, descripcion?.Trim() ?? i.Description, monto ?? i.Amount, categoria ?? i.Category, ownerKind, ownerPersonId)
            : new TicketItemInput(i.ItemId, i.Description, i.Amount, i.Category, i.OwnerKind, i.OwnerPersonId)).ToList();

        var result = await tickets.UpdateAsync(familyId, loadedTicket.Id, loadedTicket.Date, loadedTicket.Description,
            PaymentMethod.FromPrimitive(loadedTicket.PaymentMethodKind, loadedTicket.PaymentMethodReferenceId), loadedTicket.Discount, nuevos);

        return result.Ok
            ? $"Ítem {numero} corregido: {descripcion?.Trim() ?? target.Description} ${monto ?? target.Amount:N0}."
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "ticket_item_quitar_cargado")]
    [Description("Quita UN ítem de un ticket YA cargado, por su número. Los demás quedan intactos. No confundir con borrador_item_quitar (eso es antes de confirmar).")]
    public static async Task<string> TicketItemQuitarCargado(
        TicketService tickets,
        IHttpContextAccessor http,
        [Description("Id del ticket (sale de tickets_del_mes).")] string ticketId,
        [Description("Número del ítem según tickets_del_mes (empieza en 1).")] int numero)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (ticket, error) = await CargarTicket(tickets, familyId, ticketId);
        if (error is not null) return error;

        var loadedTicket = ticket!;
        var items = Ordenados(loadedTicket).ToList();   // mismo orden estable que numeró tickets_del_mes
        if (numero < 1 || numero > items.Count) return $"Error: el ticket tiene {items.Count} ítems — no existe el número {numero}.";
        if (items.Count == 1) return "Error: un ticket no puede quedar sin ítems. Si la compra entera no va, borrala desde la app.";

        var removed = items[numero - 1];
        items.RemoveAt(numero - 1);

        var result = await tickets.UpdateAsync(familyId, loadedTicket.Id, loadedTicket.Date, loadedTicket.Description,
            PaymentMethod.FromPrimitive(loadedTicket.PaymentMethodKind, loadedTicket.PaymentMethodReferenceId), loadedTicket.Discount,
            items.Select(i => new TicketItemInput(i.ItemId, i.Description, i.Amount, i.Category, i.OwnerKind, i.OwnerPersonId)).ToList());

        return result.Ok ? $"Ítem quitado: {removed.Description}. Quedan {items.Count}." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "ticket_editar")]
    [Description("Corrige la CABECERA de un ticket ya cargado (fecha, comercio, descuento o medio de pago). Los ítems quedan intactos — para un ítem usar ticket_item_editar.")]
    public static async Task<string> TicketEditar(
        TicketService tickets,
        CardService cards,
        BankService banks,
        IHttpContextAccessor http,
        [Description("Id del ticket (sale de tickets_del_mes).")] string ticketId,
        [Description("Nombre del comercio nuevo. Si se omite, no cambia.")] string? descripcion = null,
        [Description("Fecha nueva yyyy-MM-dd. Si se omite, no cambia.")] string? fecha = null,
        [Description("Descuento nuevo en pesos. Si se omite, no cambia.")] decimal? descuento = null,
        [Description("Medio de pago nuevo: Efectivo, MODO, MercadoPago, Tarjeta o Débito. Si se omite, no cambia.")] string? medio = null,
        [Description("Si medio es Tarjeta: nombre exacto de la tarjeta. Si es Débito: nombre exacto del banco.")] string? referencia = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (ticket, error) = await CargarTicket(tickets, familyId, ticketId);
        if (error is not null) return error;
        if (fecha is not null && !ApiConventions.IsValidDate(fecha)) return "Error: la fecha tiene que ser yyyy-MM-dd.";
        if (descuento is < 0) return "Error: el descuento no puede ser negativo.";

        var method = PaymentMethod.FromPrimitive(ticket!.PaymentMethodKind, ticket.PaymentMethodReferenceId);
        if (medio is not null)
        {
            var (m, medioError) = await DraftTools.ResolverMedio(cards, banks, familyId, medio, referencia);
            if (medioError is not null) return medioError;
            method = m!;
        }

        var result = await tickets.UpdateAsync(familyId, ticket.Id,
            fecha ?? ticket.Date, descripcion?.Trim() ?? ticket.Description, method, descuento ?? ticket.Discount,
            ticket.Items.Select(i => new TicketItemInput(i.ItemId, i.Description, i.Amount, i.Category, i.OwnerKind, i.OwnerPersonId)).ToList());

        return result.Ok ? $"Ticket corregido: {descripcion?.Trim() ?? ticket.Description}." : $"Error: {result.Error}";
    }

    // ── Renombrar instrumentos ───────────────────────────────────────────────────

    [McpServerTool(Name = "servicio_editar")]
    [Description("Corrige los datos de un servicio (nombre, categoría, facturación, moneda o tarjeta vinculada). NO toca los montos ni el estado de pago.")]
    public static async Task<string> ServicioEditar(
        ServicesService services,
        CardService cards,
        IHttpContextAccessor http,
        [Description("Nombre ACTUAL del servicio (usar servicios_listar).")] string nombre,
        [Description("Nombre nuevo. Si se omite, no cambia.")] string? nombreNuevo = null,
        [Description("Categoría exacta nueva. Si se omite, no cambia.")] string? categoria = null,
        [Description("Facturación nueva: 'Mensual', 'Bimestral' o 'Trimestral'. Si se omite, no cambia.")] string? facturacion = null,
        [Description("Nombre exacto de la tarjeta a vincular. Si se omite, no cambia.")] string? tarjeta = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (svc, error) = InstrumentTools.Resolver(await services.GetAllAsync(familyId), nombre, s => s.Name, "servicio", "servicios_listar");
        if (error is not null) return error;

        if (categoria is not null && !AppCategories.IsValidServiceCategory(categoria))
            return $"Error: categoría '{categoria}' inválida. Válidas: {string.Join(", ", AppCategories.ServiceCategories)}.";

        var billing = Enum.TryParse<BillingType>(svc!.BillingType, out var b) ? b : BillingType.Monthly;
        if (facturacion is not null)
        {
            var parsed = facturacion.Trim().ToLowerInvariant() switch
            {
                "mensual" or "monthly" => (BillingType?)BillingType.Monthly,
                "bimestral" or "bimonthly" => BillingType.Bimonthly,
                "trimestral" or "quarterly" => BillingType.Quarterly,
                _ => null,
            };
            if (parsed is null) return $"Error: facturación '{facturacion}' desconocida (Mensual, Bimestral o Trimestral).";
            billing = parsed.Value;
        }

        var cardId = svc.LinkedCardId;
        if (tarjeta is not null)
        {
            var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(tarjeta.Trim(), StringComparison.OrdinalIgnoreCase));
            if (card is null) return $"Error: no existe una tarjeta llamada '{tarjeta}'. Usá tarjetas_listar.";
            cardId = card.Id;
        }

        var currency = Enum.TryParse<ServiceCurrency>(svc.Currency, out var cu) ? cu : ServiceCurrency.Ars;
        var result = await services.UpdateDetailsAsync(familyId, svc.Id,
            nombreNuevo?.Trim() ?? svc.Name, categoria ?? svc.Category, billing, cardId, currency);
        return result.Ok ? $"Servicio '{nombreNuevo?.Trim() ?? svc.Name}' actualizado." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "prestamo_editar")]
    [Description("Corrige la descripción o el monto total informativo de un préstamo. NO toca el calendario de cuotas — para eso está prestamo_revisar.")]
    public static async Task<string> PrestamoEditar(
        LoanService loans,
        IHttpContextAccessor http,
        [Description("Nombre/descripción ACTUAL del préstamo (usar prestamos_listar).")] string nombre,
        [Description("Descripción nueva. Si se omite, no cambia.")] string? nombreNuevo = null,
        [Description("Monto total del préstamo (informativo). Si se omite, no cambia.")] decimal? montoTotal = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (loan, error) = InstrumentTools.Resolver(await loans.GetAllAsync(familyId), nombre, l => l.Description, "préstamo", "prestamos_listar");
        if (error is not null) return error;

        var result = await loans.UpdateDetailsAsync(familyId, loan!.Id, nombreNuevo?.Trim() ?? loan.Description, montoTotal ?? loan.TotalAmount);
        return result.Ok ? $"Préstamo '{nombreNuevo?.Trim() ?? loan.Description}' actualizado." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "cuota_editar")]
    [Description("Corrige la descripción, la categoría, la fecha de compra o el dueño de una compra en cuotas. NO toca el calendario — para eso está cuota_revisar.")]
    public static async Task<string> CuotaEditar(
        InstallmentService installments,
        PersonService people,
        IHttpContextAccessor http,
        [Description("Descripción ACTUAL de la compra en cuotas (usar cuotas_pendientes).")] string nombre,
        [Description("Descripción nueva. Si se omite, no cambia.")] string? nombreNuevo = null,
        [Description("Categoría exacta nueva. Si se omite, no cambia.")] string? categoria = null,
        [Description("Fecha de compra nueva yyyy-MM-dd. Si se omite, no cambia.")] string? fecha = null,
        [Description("Dueño nuevo: 'compartido', 'sin asignar', o el nombre exacto de una persona. Si se omite, no cambia.")] string? dueno = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (ip, error) = InstrumentTools.Resolver(await installments.GetAllAsync(familyId), nombre, i => i.Description, "compra en cuotas", "cuotas_pendientes");
        if (error is not null) return error;

        if (categoria is not null && !AppCategories.IsValidExpenseCategory(categoria))
            return $"Error: categoría '{categoria}' inválida. Válidas: {string.Join(", ", AppCategories.ExpenseCategories)}.";
        if (fecha is not null && !ApiConventions.IsValidDate(fecha)) return "Error: la fecha tiene que ser yyyy-MM-dd.";

        var owner = OwnerRef.FromPrimitive(ip!.OwnerKind, ip.OwnerPersonId);
        if (dueno is not null)
        {
            var (o, duenoError) = await DraftTools.ResolverDueno(people, familyId, dueno);
            if (duenoError is not null) return duenoError;
            owner = o!;
        }

        var result = await installments.UpdateDetailsAsync(familyId, ip.Id,
            nombreNuevo?.Trim() ?? ip.Description, categoria ?? ip.Category, fecha ?? ip.PurchaseDate, owner);
        return result.Ok ? $"'{nombreNuevo?.Trim() ?? ip.Description}' actualizada." : $"Error: {result.Error}";
    }

    // ── Día hábil ────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "dia_abrir")]
    [Description("Abre el día hábil. Es lo que habilita las novedades del día. Abrir un día ya abierto no rompe nada: avisa y sigue.")]
    public static async Task<string> DiaAbrir(
        BusinessDayService businessDays,
        IHttpContextAccessor http,
        [Description("Fecha yyyy-MM-dd. Si se omite, hoy.")] string? fecha = null)
    {
        var date = fecha ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!ApiConventions.IsValidDate(date)) return "Error: la fecha tiene que ser yyyy-MM-dd.";

        if (await businessDays.IsOpenAsync(date)) return $"El día {date} ya estaba abierto.";

        var result = await businessDays.OpenAsync(date);
        return result.Ok ? $"Día {date} abierto." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "dia_estado")]
    [Description("Dice si un día hábil está abierto o no. Solo lee; para abrirlo usar dia_abrir.")]
    public static async Task<string> DiaEstado(
        BusinessDayService businessDays,
        [Description("Fecha yyyy-MM-dd. Si se omite, hoy.")] string? fecha = null)
    {
        var date = fecha ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!ApiConventions.IsValidDate(date)) return "Error: la fecha tiene que ser yyyy-MM-dd.";
        return await businessDays.IsOpenAsync(date) ? $"El día {date} está abierto." : $"El día {date} todavía no se abrió.";
    }

    [McpServerTool(Name = "familia_ver")]
    [Description("Muestra la familia y sus MIEMBROS (quiénes tienen acceso a la app). Ojo: los miembros NO son lo mismo que las personas a las que se les asignan gastos — para eso está personas_listar.")]
    public static async Task<string> FamiliaVer(FamilyService families, IHttpContextAccessor http)
    {
        var overview = await families.GetOverviewAsync(http.HttpContext!.GetFamilyId(), http.HttpContext!.GetMemberId());
        if (overview is null) return "Error: no encontré la familia.";

        var sb = new StringBuilder($"Familia: {overview.Name}\nMiembros ({overview.Members.Count}):\n");
        foreach (var m in overview.Members)
            sb.AppendLine($"- {m.Name} ({m.Role})");
        sb.Append("(Los miembros son quienes usan la app. Las personas a las que se les imputan gastos salen de personas_listar.)");
        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    static async Task<(GastNyahp.Infrastructure.Projections.Expenses.TicketEntity? Ticket, string? Error)> CargarTicket(
        TicketService tickets, Guid familyId, string ticketId)
    {
        if (!Guid.TryParse(ticketId, out var id)) return (null, "Error: el id del ticket no es un GUID válido. Usá tickets_del_mes.");
        var ticket = await tickets.GetByIdAsync(familyId, id);
        return ticket is null ? (null, "Error: el ticket no existe. Usá tickets_del_mes.") : (ticket, null);
    }

    /// <summary>
    /// Ítems en un orden ESTABLE (por ItemId). La proyección los devuelve sin ORDER BY, así que EF los trae en el
    /// orden que se le antoje a la DB — y cambia entre llamadas, incluso sin tocar el ticket. Sin esto, el "número"
    /// que ve el agente en tickets_del_mes puede apuntar a OTRO producto cuando llega el ticket_item_editar:
    /// corrompe datos en silencio. Todas las tools de ticket numeran contra ESTE mismo orden.
    /// </summary>
    static IReadOnlyList<GastNyahp.Infrastructure.Projections.Expenses.TicketItemEntity> Ordenados(
        GastNyahp.Infrastructure.Projections.Expenses.TicketEntity t) =>
        t.Items.OrderBy(i => i.ItemId).ToList();
}
