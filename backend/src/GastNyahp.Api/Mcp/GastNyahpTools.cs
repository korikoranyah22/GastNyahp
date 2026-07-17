using System.ComponentModel;
using System.Text;
using GastNyahp.Api.Auth;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Infrastructure.Services;
using ModelContextProtocol.Server;

namespace GastNyahp.Api.Mcp;

/// <summary>
/// MCP tools for agents (mcp-tool-server skill): thin translations from tool invocation to the SAME
/// application services the REST controllers use — zero new business logic. The family comes from the bearer
/// credential (an agent key from the family panel), resolved by FamilyAuthMiddleware before the MCP endpoint
/// runs. Results are readable Spanish text: the consumer is a language model, not a parser.
/// </summary>
[McpServerToolType]
public sealed class GastNyahpTools
{
    [McpServerTool(Name = "novedades_del_dia")]
    [Description("Las novedades financieras del día para la familia: cuotas, préstamos y servicios impagos del mes, y tarjetas que cierran o vencen hoy. Es la consulta que un agente hace cada mañana.")]
    public static async Task<string> NovedadesDelDia(
        BusinessDayService businessDays,
        IHttpContextAccessor http,
        [Description("Fecha yyyy-MM-dd. Si se omite, hoy (UTC).")] string? fecha = null)
    {
        fecha ??= DateTime.UtcNow.ToString("yyyy-MM-dd");
        var novelties = await businessDays.GetNoveltiesAsync(FamilyId(http), fecha);

        var sb = new StringBuilder($"Novedades del {fecha}:\n");
        if (novelties.CardsClosingToday.Count > 0)
            sb.AppendLine($"⚠ Hoy CIERRA el resumen de: {string.Join(", ", novelties.CardsClosingToday)}.");
        if (novelties.CardsDueToday.Count > 0)
            sb.AppendLine($"⚠ Hoy VENCE el pago de: {string.Join(", ", novelties.CardsDueToday)}.");

        AppendPending(sb, "Cuotas impagas del mes", novelties.UnpaidInstallments, i => $"- {i.Description} ({i.CardLabel}): ${i.Amount:N0}");
        AppendPending(sb, "Préstamos impagos del mes", novelties.UnpaidLoanMonths, i => $"- {i.Description}: ${i.Amount:N0}");
        AppendPending(sb, "Servicios impagos del mes", novelties.UnpaidServices, i => $"- {i.Description}: ${i.Amount:N0}");
        if (novelties.OpenDrafts > 0)
            sb.AppendLine($"📝 {novelties.OpenDrafts} borrador(es) sin confirmar — ver borradores_listar.");

        if (novelties.UnpaidInstallments.Count + novelties.UnpaidLoanMonths.Count + novelties.UnpaidServices.Count
            + novelties.CardsClosingToday.Count + novelties.CardsDueToday.Count == 0)
            sb.AppendLine("Sin vencimientos ni pendientes. 🎉");

        return sb.ToString();
    }

    [McpServerTool(Name = "gasto_registrar")]
    [Description("Registra un gasto del día para la familia. Usar tarjetas_listar/bancos_listar primero para conocer los medios de pago disponibles.")]
    public static async Task<string> GastoRegistrar(
        ExpenseService expenses,
        CardService cards,
        BankService banks,
        IHttpContextAccessor http,
        [Description("Fecha yyyy-MM-dd del gasto.")] string fecha,
        [Description("Descripción corta, ej. 'Supermercado'.")] string descripcion,
        [Description("Categoría exacta: Comida, Delivery, Vicios, Salidas, Hogar, Limpieza, Salud, Higiene, Transporte, Servicios, Ropa, Educación, Electrónica, Mascotas, Perfumes o Desconocido.")] string categoria,
        [Description("Monto en pesos argentinos.")] decimal monto,
        [Description("Medio de pago: Efectivo, MODO, MercadoPago, Tarjeta o Débito.")] string medio,
        [Description("Si el medio es Tarjeta: el nombre exacto de la tarjeta. Si es Débito: el nombre exacto del banco.")] string? referencia = null)
    {
        var familyId = FamilyId(http);

        var (paymentMethod, medioError) = await DraftTools.ResolverMedio(cards, banks, familyId, medio, referencia);
        if (medioError is not null) return medioError;

        var result = await expenses.RegisterAsync(familyId, fecha, descripcion, categoria, monto, ExpenseCurrency.Ars, paymentMethod!, OwnerRef.None);
        return result.Ok
            ? $"Gasto registrado: {descripcion} — ${monto:N0} ({categoria}) el {fecha}."
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "gastos_del_mes")]
    [Description("Lista los gastos del mes calendario de la familia, con el total. Incluye el id de cada gasto — es de donde sale el id para corregirlo con gasto_editar.")]
    public static async Task<string> GastosDelMes(
        ExpenseService expenses,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM.")] string mes)
    {
        var list = await expenses.GetByMonthAsync(FamilyId(http), mes);
        if (list.Count == 0) return $"Sin gastos registrados en {mes}.";

        var sb = new StringBuilder($"Gastos de {mes} ({list.Count}):\n");
        // El id va primero: sin él, gasto_editar no tiene forma de identificar cuál corregir.
        foreach (var e in list)
            sb.AppendLine($"- {e.Id} · {e.Date}: {e.Description} ({e.Category}) ${e.AmountArs:N0}");
        sb.AppendLine($"TOTAL: ${list.Sum(e => e.AmountArs):N0}");
        return sb.ToString();
    }

    [McpServerTool(Name = "tarjetas_listar")]
    [Description("Lista las tarjetas de la familia con su día de cierre y vencimiento — necesario antes de registrar un gasto con tarjeta.")]
    public static async Task<string> TarjetasListar(CardService cards, IHttpContextAccessor http)
    {
        var list = await cards.GetAllAsync(FamilyId(http));
        return list.Count == 0
            ? "La familia no tiene tarjetas registradas."
            : string.Join("\n", list.Select(c => $"- {c.Label} ({c.Network} {c.Type}) — cierra el {c.ClosingDay}, vence el {c.DueDay}{(c.Active ? "" : " [INACTIVA]")}"));
    }

    [McpServerTool(Name = "bancos_listar")]
    [Description("Lista los bancos de la familia — necesario antes de registrar un gasto con débito.")]
    public static async Task<string> BancosListar(BankService banks, IHttpContextAccessor http)
    {
        var list = await banks.GetAllAsync(FamilyId(http));
        return list.Count == 0
            ? "La familia no tiene bancos registrados."
            : string.Join("\n", list.Select(b => $"- {b.Name}{(b.Alias is null ? "" : $" ({b.Alias})")}"));
    }

    [McpServerTool(Name = "personas_listar")]
    [Description("Lista las personas de la familia — necesario para decir de QUIÉN es un gasto o un ítem del ticket (el dueño se pasa por nombre exacto). Además del nombre de una persona, el dueño puede ser 'compartido' (de toda la familia) o 'sin asignar'.")]
    public static async Task<string> PersonasListar(PersonService people, IHttpContextAccessor http)
    {
        var list = await people.GetAllAsync(FamilyId(http));
        return list.Count == 0
            ? "La familia no tiene personas registradas — un gasto solo puede ser 'compartido' o 'sin asignar'."
            : string.Join("\n", list.Select(p => $"- {p.Name}{(string.IsNullOrWhiteSpace(p.Emoji) ? "" : $" {p.Emoji}")}"))
              + "\n(También valen 'compartido' y 'sin asignar'.)";
    }

    [McpServerTool(Name = "cuotas_pendientes")]
    [Description("Las cuotas de compras que siguen impagas en un mes dado, agrupadas por tarjeta.")]
    public static async Task<string> CuotasPendientes(
        InstallmentService installments,
        CardService cards,
        IHttpContextAccessor http,
        [Description("Mes yyyy-MM.")] string mes)
    {
        var familyId = FamilyId(http);
        var cardLabels = (await cards.GetAllAsync(familyId)).ToDictionary(c => c.Id, c => c.Label);
        var pending = (await installments.GetAllAsync(familyId))
            .Where(i => i.Active)
            .SelectMany(i => i.Months
                .Where(m => m.Month == mes && !m.Paid)
                .Select(m => (Card: cardLabels.GetValueOrDefault(i.CardId, "?"), i.Description, m.Amount)))
            .ToList();

        if (pending.Count == 0) return $"Sin cuotas pendientes en {mes}.";

        var sb = new StringBuilder($"Cuotas pendientes de {mes}:\n");
        foreach (var group in pending.GroupBy(p => p.Card))
        {
            sb.AppendLine($"{group.Key}:");
            foreach (var (_, description, amount) in group)
                sb.AppendLine($"  - {description}: ${amount:N0}");
        }
        sb.AppendLine($"TOTAL: ${pending.Sum(p => p.Amount):N0}");
        return sb.ToString();
    }

    static void AppendPending(StringBuilder sb, string title, IReadOnlyList<PendingItem> items, Func<PendingItem, string> format)
    {
        if (items.Count == 0) return;
        sb.AppendLine($"{title}:");
        foreach (var item in items) sb.AppendLine(format(item));
    }

    static Guid FamilyId(IHttpContextAccessor http) => http.HttpContext!.GetFamilyId();
}
