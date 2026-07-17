using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class BancosSteps(E2EWorld world)
{
    [Given(@"un banco ""(.*)""")]
    public async Task DadoUnBanco(string nombre)
    {
        var id = await world.PostAndGetId("/api/banks", new { name = nombre, color = "#004B9B", icon = "building-2" });
        world.RememberId("banco", nombre, id);
    }

    [When(@"intento eliminar el banco ""(.*)""")]
    public async Task CuandoIntentoEliminarElBanco(string nombre) =>
        world.LastResponse = await world.Client.DeleteAsync($"/api/banks/{world.IdOf("banco", nombre)}");

    [Then(@"el listado de bancos contiene ""(.*)""")]
    public async Task EntoncesElListadoDeBancosContiene(string nombre)
    {
        var banks = await world.GetJson("/api/banks");
        Assert.Contains(banks.EnumerateArray(), b => b.GetProperty("name").GetString() == nombre);
    }

    [Then(@"el listado de bancos no contiene ""(.*)""")]
    public async Task EntoncesElListadoDeBancosNoContiene(string nombre)
    {
        var banks = await world.GetJson("/api/banks");
        Assert.DoesNotContain(banks.EnumerateArray(), b => b.GetProperty("name").GetString() == nombre);
    }
}

[Binding]
public sealed class PrestamosSteps(E2EWorld world)
{
    [Given(@"un préstamo ""(.*)"" del banco ""(.*)"" de (\d+) cuotas de \$(\d+) desde ""(.*)""")]
    public Task DadoUnPrestamo(string descripcion, string banco, int cuotas, decimal monto, string mes) =>
        world.PostOk("/api/loans", new
        {
            bankId = world.IdOf("banco", banco),
            description = descripcion,
            totalAmount = (decimal?)null,
            monthlyInstallment = monto,
            startMonth = mes,
            totalInstallments = cuotas,
        });
}
