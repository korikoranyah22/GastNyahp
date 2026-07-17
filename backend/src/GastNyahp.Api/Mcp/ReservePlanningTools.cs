using System.ComponentModel;
using System.Text;
using GastNyahp.Api.Auth;
using GastNyahp.Domain.Reserves;
using GastNyahp.Infrastructure.Services;
using ModelContextProtocol.Server;

namespace GastNyahp.Api.Mcp;

/// <summary>
/// Reservas (plata que se APARTA, no que se gastó) y planificación (presupuesto del mes + ingreso).
///
/// <para>Una reserva no es una transacción: es un monto que se separa al inicio del mes (fondo de efectivo, plata
/// de alguien, una deuda pendiente, el estimado de la variable de la tarjeta). Su monto de un mes sale de
/// <b>override puntual &gt; monto base recurrente &gt; 0</b> (DOMAIN_MODEL §7).</para>
///
/// <para>El <c>ingreso</c> incluye la <b>cotización CCL</b>, que es de lo que dependen los servicios en USD: sin
/// ella cargada, dar de alta un servicio en dólares falla.</para>
/// </summary>
[McpServerToolType]
public sealed class ReservePlanningTools
{
    // ── Reservas ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "reservas_listar")]
    [Description("Lista las reservas de la familia (plata apartada: fondos, efectivo, deudas) con el monto efectivo del mes pedido. Es de donde salen los nombres exactos para las demás tools de reserva.")]
    public static async Task<string> ReservasListar(
        ReserveService reserves,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM del que mostrar el monto efectivo. Si se omite, el mes actual.")] string? mes = null)
    {
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (InstrumentTools.MesInvalido(month, out var mesError)) return mesError;

        var list = await reserves.GetAllAsync(http.HttpContext!.GetFamilyId());
        if (list.Count == 0) return "La familia no tiene reservas cargadas.";

        var sb = new StringBuilder($"Reservas ({month}):\n");
        foreach (var r in list.OrderBy(r => r.Label))
        {
            // Monto efectivo (DOMAIN_MODEL §7): override del mes > base recurrente > 0.
            var ovr = r.Months.FirstOrDefault(m => m.Month == month);
            var efectivo = ovr?.Amount ?? (r.Recurring ? r.BaseAmount : 0);
            var origen = ovr is not null ? "ajuste del mes" : r.Recurring ? "base recurrente" : "sin monto";
            sb.AppendLine($"- {r.Label} ({r.Type}) — ${efectivo:N0} [{origen}]{(string.IsNullOrWhiteSpace(ovr?.Note) ? "" : $" — {ovr!.Note}")}");
        }
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "reserva_monto_mes")]
    [Description("Ajusta cuánto se aparta en una reserva en UN mes puntual, sin tocar los demás meses ni el monto base ('este mes aparto 50 mil en vez de 30').")]
    public static async Task<string> ReservaMontoMes(
        ReserveService reserves,
        IHttpContextAccessor http,
        [Description("Nombre exacto de la reserva (usar reservas_listar).")] string nombre,
        [Description("Monto a apartar ese mes.")] decimal monto,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null,
        [Description("Nota del ajuste, ej. 'este mes viaje'. Opcional.")] string? nota = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (InstrumentTools.MesInvalido(month, out var mesError)) return mesError;
        if (monto < 0) return "Error: el monto no puede ser negativo.";

        var (res, error) = InstrumentTools.Resolver(await reserves.GetAllAsync(familyId), nombre, r => r.Label, "reserva", "reservas_listar");
        if (error is not null) return error;

        var result = await reserves.SetMonthAmountAsync(familyId, res!.Id, month, monto, nota);
        return result.Ok ? $"{res.Label} {month}: ${monto:N0} apartados." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "reserva_crear")]
    [Description("Da de alta una reserva (plata que se aparta al inicio del mes, no un gasto consumido). Si es recurrente, el monto base aplica a todos los meses sin ajuste puntual.")]
    public static async Task<string> ReservaCrear(
        ReserveService reserves,
        IHttpContextAccessor http,
        [Description("Nombre de la reserva, ej. 'Fondo efectivo'.")] string nombre,
        [Description("Tipo: 'Reserva', 'Efectivo', 'Deuda' u 'Otro'. Default Reserva.")] string? tipo = null,
        [Description("Monto base mensual (solo aplica si es recurrente). Default 0.")] decimal? montoBase = null,
        [Description("Si aparta el mismo monto todos los meses. Default true.")] bool? recurrente = null,
        [Description("Icono (lucide). Opcional.")] string? icono = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (string.IsNullOrWhiteSpace(nombre)) return "Error: el nombre de la reserva es obligatorio.";

        var existente = (await reserves.GetAllAsync(familyId)).FirstOrDefault(r => r.Label.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existente is not null) return $"La reserva '{existente.Label}' ya existe — no hace falta crearla de nuevo.";

        var type = ReserveType.Reserve;
        if (tipo is not null)
        {
            var parsed = ParseTipo(tipo);
            if (parsed is null) return $"Error: tipo '{tipo}' desconocido (Reserva, Efectivo, Deuda u Otro).";
            type = parsed.Value;
        }

        var result = await reserves.RegisterAsync(familyId, nombre.Trim(), type, icono ?? "piggy-bank",
            recurrente ?? true, montoBase ?? 0);
        return result.Ok ? $"Reserva '{nombre.Trim()}' creada." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "reserva_editar")]
    [Description("Edita el nombre, tipo o icono de una reserva. NO toca los montos: para eso están reserva_monto_mes (un mes) y reserva_aplicar_base (todos).")]
    public static async Task<string> ReservaEditar(
        ReserveService reserves,
        IHttpContextAccessor http,
        [Description("Nombre ACTUAL de la reserva (usar reservas_listar).")] string nombre,
        [Description("Nombre nuevo; si se omite, no cambia.")] string? nombreNuevo = null,
        [Description("Tipo nuevo: 'Reserva', 'Efectivo', 'Deuda' u 'Otro'; si se omite, no cambia.")] string? tipo = null,
        [Description("Icono nuevo; si se omite, no cambia.")] string? icono = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var (res, error) = InstrumentTools.Resolver(await reserves.GetAllAsync(familyId), nombre, r => r.Label, "reserva", "reservas_listar");
        if (error is not null) return error;

        var type = Enum.TryParse<ReserveType>(res!.Type, out var t) ? t : ReserveType.Reserve;
        if (tipo is not null)
        {
            var parsed = ParseTipo(tipo);
            if (parsed is null) return $"Error: tipo '{tipo}' desconocido (Reserva, Efectivo, Deuda u Otro).";
            type = parsed.Value;
        }

        var result = await reserves.UpdateDetailsAsync(familyId, res.Id, nombreNuevo?.Trim() ?? res.Label, type, icono ?? res.Icon);
        return result.Ok ? $"Reserva '{nombreNuevo?.Trim() ?? res.Label}' actualizada." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "reserva_aplicar_base")]
    [Description("DESTRUCTIVO: setea el monto base de una reserva y lo aplica a TODOS los meses, BORRANDO todos los ajustes puntuales que tenga. Usar SOLO si el usuario pide explícitamente 'aplicalo a todos los meses' y confirmó que se pierden los ajustes. Para un mes puntual usar reserva_monto_mes.")]
    public static async Task<string> ReservaAplicarBase(
        ReserveService reserves,
        IHttpContextAccessor http,
        [Description("Nombre exacto de la reserva (usar reservas_listar).")] string nombre,
        [Description("Monto base a aplicar a todos los meses.")] decimal montoBase)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (montoBase < 0) return "Error: el monto no puede ser negativo.";

        var (res, error) = InstrumentTools.Resolver(await reserves.GetAllAsync(familyId), nombre, r => r.Label, "reserva", "reservas_listar");
        if (error is not null) return error;

        var ajustes = res!.Months.Count;
        var result = await reserves.ApplyBaseToAllMonthsAsync(familyId, res.Id, montoBase);
        return result.Ok
            ? $"{res.Label}: ${montoBase:N0} aplicado a todos los meses." +
              (ajustes > 0 ? $" Se borraron {ajustes} ajuste(s) puntual(es) que tenía." : "")
            : $"Error: {result.Error}";
    }

    // ── Planificación ────────────────────────────────────────────────────────────

    [McpServerTool(Name = "presupuesto_ver")]
    [Description("Muestra los límites de presupuesto de un mes: tope de crédito, tope de débito/efectivo y tope semanal.")]
    public static async Task<string> PresupuestoVer(
        PlanningService planning,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null)
    {
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (InstrumentTools.MesInvalido(month, out var mesError)) return mesError;

        var b = await planning.GetBudgetAsync(http.HttpContext!.GetFamilyId(), month);
        if (b.CreditLimit == 0 && b.DebitCashLimit == 0 && b.WeeklyLimit == 0)
            return $"No hay presupuesto definido para {month}.";

        return $"Presupuesto {month}:\n- Crédito: ${b.CreditLimit:N0}\n- Débito/efectivo: ${b.DebitCashLimit:N0}\n- Semanal: ${b.WeeklyLimit:N0}";
    }

    [McpServerTool(Name = "presupuesto_definir")]
    [Description("Define los límites de presupuesto de un mes. Solo pisa los que se pasan; los omitidos quedan como estaban.")]
    public static async Task<string> PresupuestoDefinir(
        PlanningService planning,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null,
        [Description("Tope de gastos con tarjeta de crédito.")] decimal? credito = null,
        [Description("Tope de gastos con débito/efectivo.")] decimal? debitoEfectivo = null,
        [Description("Tope de gasto semanal.")] decimal? semanal = null)
    {
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (InstrumentTools.MesInvalido(month, out var mesError)) return mesError;
        if (credito is null && debitoEfectivo is null && semanal is null)
            return "Error: no me dijiste ningún límite para definir (crédito, débito/efectivo o semanal).";

        var result = await planning.SetBudgetAsync(http.HttpContext!.GetFamilyId(), month, credito, debitoEfectivo, semanal);
        return result.Ok ? $"Presupuesto de {month} actualizado." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "presupuestos_listar")]
    [Description("Lista TODOS los meses que tienen presupuesto definido, con sus límites. Para ver un mes puntual usar presupuesto_ver.")]
    public static async Task<string> PresupuestosListar(PlanningService planning, IHttpContextAccessor http)
    {
        var list = await planning.GetAllBudgetsAsync(http.HttpContext!.GetFamilyId());
        if (list.Count == 0) return "No hay ningún presupuesto definido todavía.";

        var sb = new StringBuilder($"Presupuestos definidos ({list.Count}):\n");
        foreach (var b in list)
            sb.AppendLine($"- {b.Month}: crédito ${b.CreditLimit:N0} · débito/efectivo ${b.DebitCashLimit:N0} · semanal ${b.WeeklyLimit:N0}");
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "dualpay_calcular")]
    [Description("Calculadora: dado un sueldo bruto y las cotizaciones, estima el reparto 30/70 entre pesos y dólares. NO lee ni modifica nada de la familia — es solo una cuenta.")]
    public static string DualPayCalcular(
        [Description("Sueldo bruto/neto a repartir.")] decimal montoBruto,
        [Description("Cotización del dólar oficial.")] decimal dolarOficial,
        [Description("Cotización del dólar CCL.")] decimal dolarCcl)
    {
        if (montoBruto <= 0) return "Error: el monto tiene que ser mayor a 0.";
        if (dolarOficial <= 0 || dolarCcl <= 0) return "Error: las cotizaciones tienen que ser mayores a 0.";

        var r = PlanningService.CalculateDualPay(montoBruto, dolarOficial, dolarCcl);
        return $"Reparto de ${montoBruto:N0}:\n- En pesos: ${r.Pesos:N0}\n- En dólares: US${r.Usd:N0} (CCL ${r.Ccl:N0})\n- Total equivalente: ${r.Total:N0}";
    }

    [McpServerTool(Name = "ingreso_ver")]
    [Description("Muestra el ingreso neto mensual de la familia y las cotizaciones del dólar cargadas (oficial y CCL). El CCL es el que se usa para convertir los servicios en USD.")]
    public static async Task<string> IngresoVer(PlanningService planning, IHttpContextAccessor http)
    {
        var i = await planning.GetIncomeAsync(http.HttpContext!.GetFamilyId());
        return $"Ingreso neto mensual: ${i.NetMonthly:N0}\n" +
               $"Dólar oficial: ${i.UsdRateOfficial:N0} · CCL: ${i.UsdRateCcl:N0}\n" +
               $"Split: {i.SplitPercent}%";
    }

    [McpServerTool(Name = "ingreso_definir")]
    [Description("Define el ingreso neto mensual y/o las cotizaciones del dólar. Solo pisa lo que se pasa. La cotización CCL es necesaria para poder cargar servicios en USD.")]
    public static async Task<string> IngresoDefinir(
        PlanningService planning,
        IHttpContextAccessor http,
        [Description("Ingreso neto mensual de la familia.")] decimal? neto = null,
        [Description("Cotización del dólar oficial.")] decimal? dolarOficial = null,
        [Description("Cotización del dólar CCL (la que convierte los servicios en USD).")] decimal? dolarCcl = null,
        [Description("Porcentaje de split (1-100).")] int? splitPorcentaje = null)
    {
        if (neto is null && dolarOficial is null && dolarCcl is null && splitPorcentaje is null)
            return "Error: no me dijiste qué definir (ingreso neto, dólar oficial, dólar CCL o split).";
        if (splitPorcentaje is < 1 or > 100) return "Error: el split tiene que estar entre 1 y 100.";

        var result = await planning.UpdateIncomeAsync(http.HttpContext!.GetFamilyId(), neto, dolarOficial, dolarCcl, splitPorcentaje);
        return result.Ok ? "Ingreso actualizado." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "copiar_mes")]
    [Description("Copia los datos de ESTIMACIÓN de un mes a otro (presupuesto y ajustes de reservas). NUNCA copia gastos ni tickets, y NUNCA pisa el mes destino si ya tiene datos.")]
    public static async Task<string> CopiarMes(
        PlanningService planning,
        IHttpContextAccessor http,
        [Description("Mes de origen yyyy-MM.")] string desdeMes,
        [Description("Mes de destino yyyy-MM.")] string hastaMes)
    {
        if (InstrumentTools.MesInvalido(desdeMes, out var e1)) return e1;
        if (InstrumentTools.MesInvalido(hastaMes, out var e2)) return e2;
        if (desdeMes == hastaMes) return "Error: el mes de origen y el de destino son el mismo.";

        var result = await planning.CopyMonthAsync(http.HttpContext!.GetFamilyId(), desdeMes, hastaMes);
        return result.Ok ? $"Datos de estimación copiados de {desdeMes} a {hastaMes}." : $"Error: {result.Error}";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    static ReserveType? ParseTipo(string tipo) => tipo.Trim().ToLowerInvariant() switch
    {
        "reserva" or "reserve" => ReserveType.Reserve,
        "efectivo" or "cash" => ReserveType.Cash,
        "deuda" or "debt" => ReserveType.Debt,
        "otro" or "other" => ReserveType.Other,
        _ => null,
    };
}
