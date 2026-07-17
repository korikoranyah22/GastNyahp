using System.Net.Http.Json;
using System.Text.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class GastosSteps(E2EWorld world)
{
    [Given(@"que el dólar CCL configurado es (\d+)")]
    public async Task DadoElDolarCclConfigurado(decimal ccl)
    {
        var response = await world.Client.PutAsJsonAsync("/api/planning/income", new { usdRateCcl = ccl });
        response.EnsureSuccessStatusCode();
    }

    [Given(@"un gasto ""(.*)"" de \$(\d+) en ""(.*)"" pagado con efectivo")]
    public Task DadoUnGastoEnEfectivo(string descripcion, decimal monto, string categoria) =>
        world.PostOk("/api/expenses", new
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"), description = descripcion, category = categoria,
            amount = monto, currency = "Ars", paymentMethodKind = "Cash",
        });

    [When(@"registro un gasto ""(.*)"" de USD (\d+) en efectivo el ""(.*)""")]
    public async Task RegistroUnGastoUsd(string descripcion, decimal monto, string fecha) =>
        world.LastResponse = await world.Client.PostAsJsonAsync("/api/expenses", new
        {
            date = fecha, description = descripcion, category = "Electrónica",
            amount = monto, currency = "Usd", paymentMethodKind = "Cash",
        });

    [Then(@"el gasto ""(.*)"" de ""(.*)"" figura por \$(\d+)")]
    public async Task EntoncesElGastoFiguraPor(string descripcion, string mes, decimal monto) =>
        Assert.Equal(monto, (await Gasto(descripcion, mes)).GetProperty("amountArs").GetDecimal());

    [Then(@"el gasto ""(.*)"" de ""(.*)"" conserva el monto original de USD (\d+)")]
    public async Task EntoncesConservaElMontoOriginal(string descripcion, string mes, decimal monto)
    {
        var gasto = await Gasto(descripcion, mes);
        Assert.Equal(monto, gasto.GetProperty("originalAmount").GetDecimal());
        Assert.Equal("Usd", gasto.GetProperty("originalCurrency").GetString());
    }

    async Task<JsonElement> Gasto(string descripcion, string mes)
    {
        var gastos = await world.GetJson($"/api/expenses?month={mes}");
        return gastos.EnumerateArray().Single(e => e.GetProperty("description").GetString() == descripcion);
    }
}
