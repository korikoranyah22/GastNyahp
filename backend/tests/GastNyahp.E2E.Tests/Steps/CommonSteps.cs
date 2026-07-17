using System.Net;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class CommonSteps(E2EWorld world)
{
    [Then(@"la operación es aceptada")]
    public async Task EntoncesLaOperacionEsAceptada()
    {
        var body = world.LastResponse!.IsSuccessStatusCode ? "" : await world.LastResponse.Content.ReadAsStringAsync();
        Assert.True(world.LastResponse.IsSuccessStatusCode, $"Se esperaba éxito pero fue {(int)world.LastResponse.StatusCode}: {body}");
    }

    [Then(@"la operación es rechazada")]
    public void EntoncesLaOperacionEsRechazada() =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity, world.LastResponse!.StatusCode);

    [Then(@"la operación es rechazada con ""(.*)""")]
    public async Task EntoncesLaOperacionEsRechazadaCon(string fragmento)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, world.LastResponse!.StatusCode);
        var body = await world.LastResponse.Content.ReadAsStringAsync();
        Assert.Contains(fragmento, body);
    }
}
