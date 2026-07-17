using System.Net.Http.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class PlanificacionSteps(E2EWorld world)
{
    [Given(@"un presupuesto para ""(.*)"" con meta de crédito \$(\d+)")]
    public async Task DadoUnPresupuesto(string mes, decimal credito)
    {
        var response = await world.Client.PutAsJsonAsync($"/api/planning/budget/{mes}", new { creditLimit = credito });
        response.EnsureSuccessStatusCode();
    }

    [Given(@"una reserva no recurrente ""(.*)"" con \$(\d+) para ""(.*)""")]
    public async Task DadaUnaReservaNoRecurrente(string label, decimal monto, string mes)
    {
        var id = await world.PostAndGetId("/api/reserves", new { label, type = "Reserve", recurring = false, baseAmount = 0 });
        world.RememberId("reserva", label, id);

        var response = await world.Client.PutAsJsonAsync($"/api/reserves/{id}/months/{mes}", new { amount = monto, note = (string?)null });
        response.EnsureSuccessStatusCode();
    }

    [Given(@"una reserva recurrente ""(.*)"" con base \$(\d+)")]
    public async Task DadaUnaReservaRecurrente(string label, decimal baseAmount)
    {
        var id = await world.PostAndGetId("/api/reserves", new { label, type = "Cash", recurring = true, baseAmount });
        world.RememberId("reserva", label, id);
    }

    [When(@"copio el mes ""(.*)"" al mes ""(.*)""")]
    public async Task CuandoCopioElMes(string desde, string hasta) =>
        world.LastResponse = await world.Client.PostAsJsonAsync("/api/planning/copy-month", new { fromMonth = desde, toMonth = hasta });

    [Then(@"el presupuesto de ""(.*)"" tiene meta de crédito \$(\d+)")]
    public async Task EntoncesElPresupuestoTieneMeta(string mes, decimal credito)
    {
        var budget = await world.GetJson($"/api/planning/budget/{mes}");
        Assert.Equal(credito, budget.GetProperty("creditLimit").GetDecimal());
    }

    [Then(@"la reserva ""(.*)"" tiene \$(\d+) para ""(.*)""")]
    public async Task EntoncesLaReservaTiene(string label, decimal monto, string mes)
    {
        var reserve = await world.GetJson($"/api/reserves/{world.IdOf("reserva", label)}");
        var entry = reserve.GetProperty("months").EnumerateArray().Single(m => m.GetProperty("month").GetString() == mes);
        Assert.Equal(monto, entry.GetProperty("amount").GetDecimal());
    }

    [Then(@"la reserva ""(.*)"" no tiene entradas puntuales")]
    public async Task EntoncesLaReservaNoTieneEntradas(string label)
    {
        var reserve = await world.GetJson($"/api/reserves/{world.IdOf("reserva", label)}");
        Assert.Equal(0, reserve.GetProperty("months").GetArrayLength());
    }
}
