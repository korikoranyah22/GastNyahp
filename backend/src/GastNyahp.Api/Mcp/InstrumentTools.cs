using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using GastNyahp.Api.Auth;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Installments;
using GastNyahp.Domain.Services;
using GastNyahp.Infrastructure.Services;
using ModelContextProtocol.Server;

namespace GastNyahp.Api.Mcp;

/// <summary>
/// Los instrumentos con CALENDARIO MENSUAL: servicios recurrentes, préstamos y compras en cuotas. Los tres
/// comparten la misma forma — una lista de meses con (monto, pagado) — y por eso el diálogo real sobre ellos es
/// casi siempre el mismo par de gestos: <b>"pagué la luz de julio"</b> y <b>"la luz de julio salió 45 mil"</b>.
///
/// <para>Identificación POR NOMBRE, no por id: el usuario dice "la luz", no un GUID. Si el nombre es ambiguo la
/// tool lo dice y pide desambiguar en vez de elegir por su cuenta — pisar el préstamo equivocado no se deshace.</para>
///
/// <para><b>Nada acá borra.</b> Un servicio se desactiva, una cuota se finaliza (soft-close): el historial
/// siempre queda. El DELETE real vive solo en la UI.</para>
///
/// <para>El alta de una compra en cuotas NO está acá: sale del flujo de borradores
/// (<see cref="DraftTools"/>, <c>tipo:"cuotas"</c>), que es como se carga una compra por conversación.</para>
/// </summary>
[McpServerToolType]
public sealed class InstrumentTools
{
    // ── Servicios ────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "servicios_listar")]
    [Description("Lista los servicios recurrentes de la familia (luz, gas, internet, streaming…) con el monto y el estado de pago del mes pedido. Es de donde salen los nombres exactos para las demás tools de servicio.")]
    public static async Task<string> ServiciosListar(
        ServicesService services,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM del que mostrar monto y estado. Si se omite, el mes actual.")] string? mes = null)
    {
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;

        var list = await services.GetAllAsync(FamilyId(http));
        if (list.Count == 0) return "La familia no tiene servicios cargados.";

        var sb = new StringBuilder($"Servicios ({month}):\n");
        foreach (var s in list.OrderBy(s => s.Name))
        {
            var m = s.Amounts.FirstOrDefault(a => a.Month == month);
            var estado = m is null ? "sin cargar" : m.Paid ? $"${m.AmountArs:N0} PAGADO" : $"${m.AmountArs:N0} impago";
            sb.AppendLine($"- {s.Name} ({s.Category}) — {estado}{(s.Active ? "" : " [INACTIVO]")}");
        }
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "servicio_pagar_mes")]
    [Description("Marca (o desmarca) como PAGADO un mes de un servicio. Es el gesto más común: 'ya pagué la luz de julio'. Alterna el estado: si el mes ya estaba pagado, lo vuelve a impago.")]
    public static async Task<string> ServicioPagarMes(
        ServicesService services,
        IHttpContextAccessor http,
        [Description("Nombre exacto del servicio (usar servicios_listar).")] string nombre,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null)
    {
        var familyId = FamilyId(http);
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;

        var (svc, error) = Resolver(await services.GetAllAsync(familyId), nombre, s => s.Name, "servicio", "servicios_listar");
        if (error is not null) return error;

        var estabaPago = svc!.Amounts.FirstOrDefault(a => a.Month == month)?.Paid ?? false;
        var result = await services.ToggleMonthPaidAsync(familyId, svc.Id, month);
        return result.Ok
            ? estabaPago
                ? $"{svc.Name} {month}: lo marqué de nuevo como IMPAGO."
                : $"{svc.Name} {month}: pagado ✔"
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "servicio_monto_mes")]
    [Description("Setea cuánto salió un servicio en un mes puntual ('la luz de julio vino 45 mil'). No toca los demás meses ni el estado de pago.")]
    public static async Task<string> ServicioMontoMes(
        ServicesService services,
        IHttpContextAccessor http,
        [Description("Nombre exacto del servicio (usar servicios_listar).")] string nombre,
        [Description("Monto del mes. En la moneda del servicio (si el servicio es en USD, el monto va en USD).")] decimal monto,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null)
    {
        var familyId = FamilyId(http);
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;
        if (monto < 0) return "Error: el monto no puede ser negativo.";

        var (svc, error) = Resolver(await services.GetAllAsync(familyId), nombre, s => s.Name, "servicio", "servicios_listar");
        if (error is not null) return error;

        var currency = Enum.TryParse<ServiceCurrency>(svc!.Currency, out var c) ? c : ServiceCurrency.Ars;
        var result = await services.SetMonthAmountAsync(familyId, svc.Id, month, monto, currency);
        return result.Ok ? $"{svc.Name} {month}: ${monto:N0}." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "servicio_crear")]
    [Description("Da de alta un servicio recurrente (luz, gas, internet, streaming…). Genera 12 meses desde el mes de alta con el monto base.")]
    public static async Task<string> ServicioCrear(
        ServicesService services,
        CardService cards,
        IHttpContextAccessor http,
        [Description("Nombre del servicio, ej. 'Luz'.")] string nombre,
        [Description("Categoría exacta: Electricidad, Gas, Agua, Conectividad, Streaming, Digital, Seguro, Expensas, Telecom u Otros.")] string categoria,
        [Description("Monto base mensual (en la moneda indicada).")] decimal montoBase,
        [Description("Facturación: 'Mensual', 'Bimestral' o 'Trimestral'. Default Mensual. Es informativo: los montos siempre se generan mensuales.")] string? facturacion = null,
        [Description("Moneda: 'Ars' o 'Usd'. Default Ars. Si es Usd, la familia necesita tener cargada la cotización CCL.")] string? moneda = null,
        [Description("Nombre exacto de la tarjeta a la que está débitado, si aplica (usar tarjetas_listar).")] string? tarjeta = null)
    {
        var familyId = FamilyId(http);
        if (string.IsNullOrWhiteSpace(nombre)) return "Error: el nombre del servicio es obligatorio.";
        if (!AppCategories.IsValidServiceCategory(categoria))
            return $"Error: categoría '{categoria}' inválida. Válidas: {string.Join(", ", AppCategories.ServiceCategories)}.";
        if (montoBase < 0) return "Error: el monto base no puede ser negativo.";

        var existente = (await services.GetAllAsync(familyId)).FirstOrDefault(s => s.Name.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existente is not null) return $"El servicio '{existente.Name}' ya existe — no hace falta crearlo de nuevo.";

        var billing = BillingType.Monthly;
        if (facturacion is not null)
        {
            var parsed = ParseFacturacion(facturacion);
            if (parsed is null) return $"Error: facturación '{facturacion}' desconocida (Mensual, Bimestral o Trimestral).";
            billing = parsed.Value;
        }
        var currency = ServiceCurrency.Ars;
        if (moneda is not null)
        {
            if (!Enum.TryParse(moneda.Trim(), ignoreCase: true, out currency))
                return $"Error: moneda '{moneda}' desconocida (Ars o Usd).";
        }

        Guid? cardId = null;
        if (tarjeta is not null)
        {
            var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(tarjeta.Trim(), StringComparison.OrdinalIgnoreCase));
            if (card is null) return $"Error: no existe una tarjeta llamada '{tarjeta}'. Usá tarjetas_listar.";
            cardId = card.Id;
        }

        // Los 12 meses del servicio se generan desde acá: sin mes explícito, el alta arranca en el mes actual.
        var desde = DateTime.UtcNow.ToString("yyyy-MM");
        var result = await services.RegisterAsync(familyId, nombre.Trim(), categoria, billing, cardId, currency, montoBase, desde, OwnerRef.None);
        return result.Ok
            ? $"Servicio '{nombre.Trim()}' creado ({categoria}, ${montoBase:N0}/mes desde {desde})."
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "servicio_extender")]
    [Description("Aplica un monto a los próximos meses de un servicio desde un mes dado ('la luz ahora sale 50 mil de acá en adelante'). Preserva el estado de pago de los meses que ya existían.")]
    public static async Task<string> ServicioExtender(
        ServicesService services,
        IHttpContextAccessor http,
        [Description("Nombre exacto del servicio (usar servicios_listar).")] string nombre,
        [Description("Monto a aplicar, en pesos.")] decimal monto,
        [Description("Mes desde el que aplicar, yyyy-MM (inclusive). Si se omite, el mes actual.")] string? desdeMes = null,
        [Description("Cuántos meses hacia adelante. Default 12.")] int? meses = null)
    {
        var familyId = FamilyId(http);
        var month = desdeMes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;
        if (monto < 0) return "Error: el monto no puede ser negativo.";

        var (svc, error) = Resolver(await services.GetAllAsync(familyId), nombre, s => s.Name, "servicio", "servicios_listar");
        if (error is not null) return error;

        var ahead = meses is > 0 ? meses.Value : 12;
        var result = await services.ExtendFutureAmountsAsync(familyId, svc!.Id, month, monto, ahead);
        return result.Ok ? $"{svc.Name}: ${monto:N0} aplicado a {ahead} meses desde {month}." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "servicio_desactivar")]
    [Description("Desactiva un servicio: deja de contar en los totales, pero NO se borra y su historial queda intacto. Es la forma reversible de 'dar de baja' un servicio.")]
    public static async Task<string> ServicioDesactivar(
        ServicesService services,
        IHttpContextAccessor http,
        [Description("Nombre exacto del servicio (usar servicios_listar).")] string nombre)
    {
        var familyId = FamilyId(http);
        var (svc, error) = Resolver(await services.GetAllAsync(familyId), nombre, s => s.Name, "servicio", "servicios_listar");
        if (error is not null) return error;
        if (!svc!.Active) return $"El servicio '{svc.Name}' ya está inactivo.";

        var result = await services.DeactivateAsync(familyId, svc.Id);
        return result.Ok ? $"Servicio '{svc.Name}' desactivado — se puede reactivar cuando quieras." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "servicio_activar")]
    [Description("Reactiva un servicio que estaba desactivado, para que vuelva a contar en los totales.")]
    public static async Task<string> ServicioActivar(
        ServicesService services,
        IHttpContextAccessor http,
        [Description("Nombre exacto del servicio (usar servicios_listar).")] string nombre)
    {
        var familyId = FamilyId(http);
        var (svc, error) = Resolver(await services.GetAllAsync(familyId), nombre, s => s.Name, "servicio", "servicios_listar");
        if (error is not null) return error;
        if (svc!.Active) return $"El servicio '{svc.Name}' ya está activo.";

        var result = await services.ActivateAsync(familyId, svc.Id);
        return result.Ok ? $"Servicio '{svc.Name}' reactivado." : $"Error: {result.Error}";
    }

    // ── Préstamos ────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "prestamos_listar")]
    [Description("Lista los préstamos de la familia con su progreso (cuotas pagas de total) y el estado del mes pedido. Es de donde salen los nombres exactos para las demás tools de préstamo.")]
    public static async Task<string> PrestamosListar(
        LoanService loans,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM del que mostrar el estado. Si se omite, el mes actual.")] string? mes = null)
    {
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;

        var list = await loans.GetAllAsync(FamilyId(http));
        if (list.Count == 0) return "La familia no tiene préstamos cargados.";

        var sb = new StringBuilder($"Préstamos ({month}):\n");
        foreach (var l in list.OrderBy(l => l.Description))
        {
            var m = l.Months.FirstOrDefault(x => x.Month == month);
            var estado = m is null ? "sin cuota este mes" : m.Paid ? $"${m.Amount:N0} PAGADA" : $"${m.Amount:N0} impaga";
            sb.AppendLine($"- {l.Description} — {estado} · {l.PaidInstallments}/{l.TotalInstallments} cuotas pagas");
        }
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "prestamo_pagar_mes")]
    [Description("Marca (o desmarca) como PAGADA la cuota de un mes de un préstamo ('pagué la cuota del préstamo del auto'). Alterna el estado.")]
    public static async Task<string> PrestamoPagarMes(
        LoanService loans,
        IHttpContextAccessor http,
        [Description("Nombre/descripción exacta del préstamo (usar prestamos_listar).")] string nombre,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null)
    {
        var familyId = FamilyId(http);
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;

        var (loan, error) = Resolver(await loans.GetAllAsync(familyId), nombre, l => l.Description, "préstamo", "prestamos_listar");
        if (error is not null) return error;

        var estabaPago = loan!.Months.FirstOrDefault(m => m.Month == month)?.Paid ?? false;
        var result = await loans.ToggleMonthPaidAsync(familyId, loan.Id, month);
        return result.Ok
            ? estabaPago
                ? $"{loan.Description} {month}: la marqué de nuevo como IMPAGA."
                : $"{loan.Description} {month}: cuota pagada ✔"
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "prestamo_monto_mes")]
    [Description("Cambia el monto de la cuota de UN mes puntual de un préstamo, sin tocar el resto (típico de préstamos UVA/ajustables).")]
    public static async Task<string> PrestamoMontoMes(
        LoanService loans,
        IHttpContextAccessor http,
        [Description("Nombre/descripción exacta del préstamo (usar prestamos_listar).")] string nombre,
        [Description("Monto de la cuota de ese mes.")] decimal monto,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null)
    {
        var familyId = FamilyId(http);
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;
        if (monto < 0) return "Error: el monto no puede ser negativo.";

        var (loan, error) = Resolver(await loans.GetAllAsync(familyId), nombre, l => l.Description, "préstamo", "prestamos_listar");
        if (error is not null) return error;

        var result = await loans.OverrideMonthAmountAsync(familyId, loan!.Id, month, monto);
        return result.Ok ? $"{loan.Description} {month}: la cuota queda en ${monto:N0}." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "prestamo_crear")]
    [Description("Da de alta un préstamo de un banco. Genera el calendario completo de cuotas desde el mes de inicio.")]
    public static async Task<string> PrestamoCrear(
        LoanService loans,
        BankService banks,
        IHttpContextAccessor http,
        [Description("Descripción del préstamo, ej. 'Préstamo auto'. Es el nombre que se usa después.")] string nombre,
        [Description("Nombre exacto del banco que lo otorgó (usar bancos_listar).")] string banco,
        [Description("Monto de cada cuota mensual.")] decimal cuotaMensual,
        [Description("Cantidad total de cuotas.")] int totalCuotas,
        [Description("Mes de la primera cuota, yyyy-MM. Si se omite, el mes actual.")] string? mesInicio = null,
        [Description("Monto total del préstamo. Informativo, no afecta cálculos.")] decimal? montoTotal = null)
    {
        var familyId = FamilyId(http);
        if (string.IsNullOrWhiteSpace(nombre)) return "Error: la descripción del préstamo es obligatoria.";
        if (cuotaMensual <= 0) return "Error: la cuota mensual tiene que ser mayor a 0.";
        if (totalCuotas <= 0) return "Error: el total de cuotas tiene que ser mayor a 0.";

        var month = mesInicio ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;

        var bank = (await banks.GetAllAsync(familyId)).FirstOrDefault(b => b.Name.Equals(banco.Trim(), StringComparison.OrdinalIgnoreCase));
        if (bank is null) return $"Error: no existe un banco llamado '{banco}'. Usá bancos_listar, o crealo con banco_crear.";

        var result = await loans.RegisterAsync(familyId, bank.Id, nombre.Trim(), montoTotal, cuotaMensual, month, totalCuotas);
        return result.Ok
            ? $"Préstamo '{nombre.Trim()}' creado en {bank.Name}: {totalCuotas} cuotas de ${cuotaMensual:N0} desde {month}."
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "prestamo_revisar")]
    [Description("Recalcula el calendario de un préstamo (cambió el mes de inicio, la cantidad de cuotas o el monto). REGENERA los meses: preserva los que ya estaban pagados con su monto, y pisa el resto.")]
    public static async Task<string> PrestamoRevisar(
        LoanService loans,
        IHttpContextAccessor http,
        [Description("Nombre/descripción exacta del préstamo (usar prestamos_listar).")] string nombre,
        [Description("Cuota mensual nueva. Si se omite, la actual.")] decimal? cuotaMensual = null,
        [Description("Total de cuotas nuevo. Si se omite, el actual.")] int? totalCuotas = null,
        [Description("Mes de inicio nuevo, yyyy-MM. Si se omite, el actual.")] string? mesInicio = null)
    {
        var familyId = FamilyId(http);
        var (loan, error) = Resolver(await loans.GetAllAsync(familyId), nombre, l => l.Description, "préstamo", "prestamos_listar");
        if (error is not null) return error;

        var month = mesInicio ?? loan!.StartMonth;
        if (MesInvalido(month, out var mesError)) return mesError;

        var result = await loans.ReviseScheduleAsync(familyId, loan!.Id, month,
            totalCuotas ?? loan.TotalInstallments, cuotaMensual ?? loan.MonthlyInstallment);
        return result.Ok
            ? $"{loan.Description}: calendario recalculado — {totalCuotas ?? loan.TotalInstallments} cuotas de ${cuotaMensual ?? loan.MonthlyInstallment:N0} desde {month} (los meses ya pagados se respetaron)."
            : $"Error: {result.Error}";
    }

    // ── Cuotas (compras en cuotas) ───────────────────────────────────────────────
    // El ALTA no está acá: una compra en cuotas se carga por el flujo de borradores (borrador_crear tipo:"cuotas").

    [McpServerTool(Name = "cuota_pagar_mes")]
    [Description("Marca (o desmarca) como PAGADA la cuota de un mes de una compra en cuotas ('pagué la cuota de la tele'). Alterna el estado.")]
    public static async Task<string> CuotaPagarMes(
        InstallmentService installments,
        IHttpContextAccessor http,
        [Description("Descripción exacta de la compra en cuotas (usar cuotas_pendientes).")] string nombre,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null)
    {
        var familyId = FamilyId(http);
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;

        var (ip, error) = Resolver(await installments.GetAllAsync(familyId), nombre, i => i.Description, "compra en cuotas", "cuotas_pendientes");
        if (error is not null) return error;

        var estabaPaga = ip!.Months.FirstOrDefault(m => m.Month == month)?.Paid ?? false;
        var result = await installments.ToggleMonthPaidAsync(familyId, ip.Id, month);
        return result.Ok
            ? estabaPaga
                ? $"{ip.Description} {month}: la marqué de nuevo como IMPAGA."
                : $"{ip.Description} {month}: cuota pagada ✔"
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "cuota_monto_mes")]
    [Description("Cambia el monto de UN mes puntual de una compra en cuotas, sin tocar el resto.")]
    public static async Task<string> CuotaMontoMes(
        InstallmentService installments,
        IHttpContextAccessor http,
        [Description("Descripción exacta de la compra en cuotas (usar cuotas_pendientes).")] string nombre,
        [Description("Monto de la cuota de ese mes.")] decimal monto,
        [Description("Mes yyyy-MM. Si se omite, el mes actual.")] string? mes = null)
    {
        var familyId = FamilyId(http);
        var month = mes ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (MesInvalido(month, out var mesError)) return mesError;
        if (monto < 0) return "Error: el monto no puede ser negativo.";

        var (ip, error) = Resolver(await installments.GetAllAsync(familyId), nombre, i => i.Description, "compra en cuotas", "cuotas_pendientes");
        if (error is not null) return error;

        var result = await installments.OverrideMonthAmountAsync(familyId, ip!.Id, month, monto);
        return result.Ok ? $"{ip.Description} {month}: la cuota queda en ${monto:N0}." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "cuota_finalizar")]
    [Description("Cierra una compra en cuotas: deja de contar en los totales futuros, pero NO se borra y su historial queda intacto. Para cuando terminó de pagarse o se canceló.")]
    public static async Task<string> CuotaFinalizar(
        InstallmentService installments,
        IHttpContextAccessor http,
        [Description("Descripción exacta de la compra en cuotas (usar cuotas_pendientes).")] string nombre)
    {
        var familyId = FamilyId(http);
        var (ip, error) = Resolver(await installments.GetAllAsync(familyId), nombre, i => i.Description, "compra en cuotas", "cuotas_pendientes");
        if (error is not null) return error;

        var result = await installments.FinishAsync(familyId, ip!.Id);
        return result.Ok ? $"'{ip.Description}' finalizada — su historial queda intacto." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "cuota_revisar")]
    [Description("Recalcula el calendario de una compra en cuotas (cambió el mes de inicio, la cantidad de cuotas o el monto). REGENERA los meses: preserva los ya pagados con su monto, y pisa el resto.")]
    public static async Task<string> CuotaRevisar(
        InstallmentService installments,
        IHttpContextAccessor http,
        [Description("Descripción exacta de la compra en cuotas (usar cuotas_pendientes).")] string nombre,
        [Description("Monto de cada cuota nuevo. Si se omite, el actual.")] decimal? cuotaMensual = null,
        [Description("Total de cuotas nuevo. Si se omite, el actual.")] int? totalCuotas = null,
        [Description("Mes de inicio nuevo, yyyy-MM. Si se omite, el actual.")] string? mesInicio = null)
    {
        var familyId = FamilyId(http);
        var (ip, error) = Resolver(await installments.GetAllAsync(familyId), nombre, i => i.Description, "compra en cuotas", "cuotas_pendientes");
        if (error is not null) return error;

        var month = mesInicio ?? ip!.StartMonth;
        if (MesInvalido(month, out var mesError)) return mesError;

        var frequency = Enum.TryParse<InstallmentFrequency>(ip!.Frequency, out var f) ? f : InstallmentFrequency.Fixed;
        var result = await installments.ReviseScheduleAsync(familyId, ip.Id, month,
            totalCuotas ?? ip.TotalInstallments, frequency, cuotaMensual ?? ip.MonthlyAmount);
        return result.Ok
            ? $"{ip.Description}: calendario recalculado desde {month} (los meses ya pagados se respetaron)."
            : $"Error: {result.Error}";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    static Guid FamilyId(IHttpContextAccessor http) => http.HttpContext!.GetFamilyId();

    /// <summary>
    /// Resuelve una entidad por nombre. Exacto primero; si no, "contiene" (el usuario dice "la luz", el servicio
    /// se llama "Luz EDESUR"). Si hay VARIOS candidatos NO elige: devuelve el error pidiendo desambiguar —
    /// tocar el préstamo equivocado no se deshace.
    /// </summary>
    internal static (T? Item, string? Error) Resolver<T>(
        IReadOnlyList<T> items, string nombre, Func<T, string> name, string tipo, string listarTool)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return (default, $"Error: falta el nombre del {tipo}.");
        var q = nombre.Trim();

        var exacto = items.FirstOrDefault(i => name(i).Equals(q, StringComparison.OrdinalIgnoreCase));
        if (exacto is not null) return (exacto, null);

        var parciales = items.Where(i => name(i).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        return parciales.Count switch
        {
            1 => (parciales[0], null),
            0 => (default, $"Error: no encontré un {tipo} que coincida con '{nombre}'. Usá {listarTool}."),
            _ => (default, $"Error: '{nombre}' coincide con varios: {string.Join(", ", parciales.Select(name))}. Preguntale al usuario cuál es."),
        };
    }

    internal static bool MesInvalido(string mes, out string error)
    {
        if (Regex.IsMatch(mes, @"^\d{4}-(0[1-9]|1[0-2])$")) { error = ""; return false; }
        error = $"Error: el mes '{mes}' es inválido (esperaba yyyy-MM, ej. '2026-07').";
        return true;
    }

    static BillingType? ParseFacturacion(string f) => f.Trim().ToLowerInvariant() switch
    {
        "mensual" or "monthly" => BillingType.Monthly,
        "bimestral" or "bimonthly" => BillingType.Bimonthly,
        "trimestral" or "quarterly" => BillingType.Quarterly,
        _ => null,
    };
}
