using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class DiaHabilSteps(E2EWorld world)
{
    [When(@"abro el día hábil ""(.*)""")]
    public async Task CuandoAbroElDiaHabil(string fecha) =>
        world.LastResponse = await world.Client.PostAsync($"/api/business-days/{fecha}/open", null);

    [Then(@"el día hábil ""(.*)"" figura abierto")]
    public async Task EntoncesElDiaFiguraAbierto(string fecha)
    {
        var status = await world.GetJson($"/api/business-days/{fecha}");
        Assert.True(status.GetProperty("open").GetBoolean());
    }

    [Then(@"las novedades del ""(.*)"" incluyen la cuota pendiente ""(.*)""")]
    public async Task EntoncesLasNovedadesIncluyenLaCuota(string fecha, string descripcion)
    {
        var novelties = await world.GetJson($"/api/business-days/{fecha}/novelties");
        Assert.Contains(novelties.GetProperty("unpaidInstallments").EnumerateArray(),
            i => i.GetProperty("description").GetString() == descripcion);
    }

    [Then(@"las novedades del ""(.*)"" no incluyen la cuota ""(.*)""")]
    public async Task EntoncesLasNovedadesNoIncluyenLaCuota(string fecha, string descripcion)
    {
        var novelties = await world.GetJson($"/api/business-days/{fecha}/novelties");
        Assert.DoesNotContain(novelties.GetProperty("unpaidInstallments").EnumerateArray(),
            i => i.GetProperty("description").GetString() == descripcion);
    }

    [Then(@"las novedades del ""(.*)"" indican que ""(.*)"" vence hoy")]
    public async Task EntoncesLasNovedadesIndicanQueVenceHoy(string fecha, string tarjeta)
    {
        var novelties = await world.GetJson($"/api/business-days/{fecha}/novelties");
        Assert.Contains(novelties.GetProperty("cardsDueToday").EnumerateArray(),
            c => c.GetString() == tarjeta);
    }
}
