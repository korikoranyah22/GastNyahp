using System.Net.Http.Json;
using System.Text.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class TarjetasSteps(E2EWorld world)
{
    [Given(@"una tarjeta ""(.*)"" del banco ""(.*)""")]
    public async Task DadaUnaTarjeta(string label, string banco)
    {
        await Registrar(label, banco, closingDay: 15, dueDay: 5);
        world.LastResponse!.EnsureSuccessStatusCode(); // setup steps must not fail silently
    }

    [Given(@"una tarjeta ""(.*)"" del banco ""(.*)"" con cierre el día (\d+) y vencimiento el día (\d+)")]
    [When(@"registro la tarjeta ""(.*)"" del banco ""(.*)"" con cierre el día (\d+) y vencimiento el día (\d+)")]
    public Task RegistroLaTarjetaConDias(string label, string banco, int cierre, int vencimiento) =>
        Registrar(label, banco, cierre, vencimiento);

    [When(@"registro la tarjeta ""(.*)"" de un banco inexistente")]
    public async Task RegistroTarjetaDeBancoInexistente(string label) =>
        world.LastResponse = await world.Client.PostAsJsonAsync("/api/cards", new
        {
            bankId = Guid.NewGuid(), label, network = "Visa", type = "Credit", closingDay = 15, dueDay = 5, color = "#000",
        });

    [When(@"intento eliminar la tarjeta ""(.*)""")]
    public async Task CuandoIntentoEliminarLaTarjeta(string label) =>
        world.LastResponse = await world.Client.DeleteAsync($"/api/cards/{world.IdOf("tarjeta", label)}");

    [Then(@"el listado de tarjetas contiene ""(.*)""")]
    public async Task EntoncesElListadoDeTarjetasContiene(string label)
    {
        var cards = await world.GetJson("/api/cards");
        Assert.Contains(cards.EnumerateArray(), c => c.GetProperty("label").GetString() == label);
    }

    async Task Registrar(string label, string banco, int closingDay, int dueDay)
    {
        world.LastResponse = await world.Client.PostAsJsonAsync("/api/cards", new
        {
            bankId = world.IdOf("banco", banco), label, network = "Visa", type = "Credit",
            closingDay, dueDay, color = "#1e40af",
        });
        if (world.LastResponse.IsSuccessStatusCode)
        {
            var payload = await world.LastResponse.Content.ReadFromJsonAsync<JsonElement>();
            world.RememberId("tarjeta", label, payload.GetProperty("id").GetGuid());
        }
    }
}

[Binding]
public sealed class ServiciosSteps(E2EWorld world)
{
    [Given(@"un servicio ""(.*)"" de \$(\d+) mensuales vinculado a la tarjeta ""(.*)""")]
    public Task DadoUnServicioVinculado(string nombre, decimal monto, string tarjeta) =>
        world.PostOk("/api/services", new
        {
            name = nombre, category = "Seguro", billingType = "Monthly",
            linkedCardId = world.IdOf("tarjeta", tarjeta), currency = "Ars",
            baseAmount = monto, registeredFromMonth = "2026-01",
        });
}
