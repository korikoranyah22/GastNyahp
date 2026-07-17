using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class ImportacionSteps(E2EWorld world)
{
    JsonElement _summary;

    /// <summary>El shape EXACTO del exportData() de la maqueta (ids string, enums legacy en minúsculas).</summary>
    static object LegacyJson() => new
    {
        meta = new { version = "1.4", exported = "2026-02-26T10:00:00Z" },
        people = new[] { new { id = "person-1", name = "Cami", emoji = "😀", color = "#e11d48" } },
        banks = new[] { new { id = "bank-1", name = "BBVA", alias = "Personal", color = "#004B9B", icon = "building-2" } },
        creditCards = new[]
        {
            new { id = "card-1", bankId = "bank-1", label = "VISA BBVA", network = "VISA", type = "credit", closingDay = 15, dueDay = 5, color = "#0057B8", active = true },
        },
        installments = new[]
        {
            new
            {
                id = "inst-1", cardId = "card-1", description = "Smart TV", category = "Hogar",
                purchaseDate = "2025-10-05", frequency = "fixed", monthlyAmount = 85000, totalInstallments = 3, startMonth = "2025-10",
                months = new object[]
                {
                    new { month = "2025-10", amount = 85000, paid = true },
                    new { month = "2025-11", amount = 90000, paid = false },  // override de monto (estilo UVA)
                    new { month = "2025-12", amount = 85000, paid = false },
                },
            },
        },
        loans = new[]
        {
            new
            {
                id = "loan-1", bankId = "bank-1", description = "Préstamo BBVA", totalAmount = 360000,
                monthlyInstallment = 180000, startDate = "2025-11-01", totalInstallments = 2,
                months = new object[]
                {
                    new { month = "2025-11", amount = 180000, paid = true },
                    new { month = "2025-12", amount = 180000, paid = false },
                },
            },
        },
        services = new[]
        {
            new
            {
                id = "svc-1", name = "Edesur", category = "Electricidad", billingType = "monthly",
                linkedCardId = (string?)null, active = true,
                amounts = new object[]
                {
                    new { month = "2026-01", amount = 38000 },
                    new { month = "2026-02", amount = 40000, paid = true },
                },
            },
        },
        fixedExpenses = new[]
        {
            new
            {
                id = "fx-1", label = "Efectivo", type = "cash", icon = "💵", recurring = true, baseAmount = 100000,
                months = new object[] { new { month = "2026-02", amount = 50000, note = "ajuste" } },
            },
        },
        expenses = new object[]
        {
            new { id = "exp-1", date = "2026-02-03", description = "Coto", category = "Comida", amount = 95000, paymentMethod = "card-1", ownerId = "person-1" },
            new
            {
                id = "tkt-1", type = "ticket", date = "2026-02-15", description = "Super", paymentMethod = "cash", discount = 5000,
                items = new object[]
                {
                    new { description = "Carne", amount = 30000, category = "Comida" },
                    new { description = "Lavandina", amount = 8000, category = "Limpieza" },
                },
            },
        },
        budgets = new Dictionary<string, object> { ["2026-02"] = new { creditLimit = 480000, debitCashLimit = 316000, weeklyLimit = 200000 } },
        income = new { netMonthly = 500000, usdRateOfficial = 1050, usdRateCCL = 1250, splitPercent = 70 },
    };

    [When(@"importo el JSON de ejemplo de la maqueta con force")]
    public Task ImportoConForce() => Importar("force=true");

    [When(@"importo el JSON de ejemplo de la maqueta reemplazando todo")]
    public Task ImportoReemplazando() => Importar("replace=true");

    [When(@"importo el JSON de ejemplo de la maqueta")]
    public Task ImportoElJson() => Importar("force=false");

    async Task Importar(string query)
    {
        world.LastResponse = await world.Client.PostAsJsonAsync($"/api/import?{query}", LegacyJson());
        if (!world.LastResponse.IsSuccessStatusCode) return;

        // 202: el job corre en background — los Then necesitan el estado terminal y su resumen.
        for (var i = 0; i < 100; i++)
        {
            var status = await world.GetJson("/api/import/status");
            switch (status.GetProperty("status").GetString())
            {
                case "completed":
                    _summary = status.GetProperty("summary");
                    return;
                case "failed":
                    Assert.Fail($"La importación falló: {status.GetProperty("error")}");
                    return;
            }
            await Task.Delay(100);
        }
        Assert.Fail("La importación no terminó a tiempo.");
    }

    [When(@"la clave de agente ""(.*)"" intenta importar el JSON")]
    public Task LaClaveIntentaImportar(string nombre) =>
        world.SendAs(world.AgentKeys[nombre].Token, HttpMethod.Post, "/api/import?force=false", LegacyJson());

    [Then(@"la operación es prohibida")]
    public void EntoncesEsProhibida() =>
        Assert.Equal(HttpStatusCode.Forbidden, world.LastResponse!.StatusCode);

    [Then(@"el resumen de importación reporta (\d+) banco, (\d+) tarjeta, (\d+) cuota, (\d+) préstamo y (\d+) movimientos")]
    public void ElResumenReporta(int bancos, int tarjetas, int cuotas, int prestamos, int movimientos)
    {
        Assert.Equal(bancos, _summary.GetProperty("banks").GetInt32());
        Assert.Equal(tarjetas, _summary.GetProperty("cards").GetInt32());
        Assert.Equal(cuotas, _summary.GetProperty("installments").GetInt32());
        Assert.Equal(prestamos, _summary.GetProperty("loans").GetInt32());
        Assert.Equal(movimientos, _summary.GetProperty("expenses").GetInt32() + _summary.GetProperty("tickets").GetInt32());
    }

    [Then(@"la cuota importada ""(.*)"" tiene el mes ""(.*)"" pagado")]
    public async Task LaCuotaTieneElMesPagado(string descripcion, string mes) =>
        Assert.True((await CuotaMes(descripcion, mes)).GetProperty("paid").GetBoolean());

    [Then(@"la cuota importada ""(.*)"" tiene el mes ""(.*)"" por \$(\d+)")]
    public async Task LaCuotaTieneElMesPorMonto(string descripcion, string mes, decimal monto) =>
        Assert.Equal(monto, (await CuotaMes(descripcion, mes)).GetProperty("amount").GetDecimal());

    async Task<JsonElement> CuotaMes(string descripcion, string mes)
    {
        var installments = await world.GetJson("/api/installments");
        var installment = installments.EnumerateArray().Single(i => i.GetProperty("description").GetString() == descripcion);
        return installment.GetProperty("months").EnumerateArray().Single(m => m.GetProperty("month").GetString() == mes);
    }

    [Then(@"el ticket importado ""(.*)"" de ""(.*)"" totaliza \$(\d+)")]
    public async Task ElTicketTotaliza(string descripcion, string mes, decimal total)
    {
        var ticketList = await world.GetJson($"/api/tickets?month={mes}");
        var ticket = ticketList.EnumerateArray().Single(t => t.GetProperty("description").GetString() == descripcion);
        Assert.Equal(total, ticket.GetProperty("total").GetDecimal());
    }

    [Then(@"el dólar CCL importado quedó en (\d+)")]
    public async Task ElCclQuedoEn(decimal ccl)
    {
        var income = await world.GetJson("/api/planning/income");
        Assert.Equal(ccl, income.GetProperty("usdRateCcl").GetDecimal());
    }
}
