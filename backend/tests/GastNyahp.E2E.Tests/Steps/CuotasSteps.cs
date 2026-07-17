using System.Text.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class CuotasSteps(E2EWorld world)
{
    [Given(@"una compra ""(.*)"" en (\d+) cuotas de \$(\d+) con la tarjeta ""(.*)"" desde ""(.*)""")]
    [When(@"registro la compra ""(.*)"" en (\d+) cuotas de \$(\d+) con la tarjeta ""(.*)"" desde ""(.*)""")]
    public async Task RegistroLaCompraEnCuotas(string descripcion, int cuotas, decimal monto, string tarjeta, string mes)
    {
        var id = await world.PostAndGetId("/api/installments", new
        {
            cardId = world.IdOf("tarjeta", tarjeta),
            description = descripcion,
            category = "Hogar",
            purchaseDate = $"{mes}-05",
            frequency = "Fixed",
            monthlyAmount = monto,
            totalInstallments = cuotas,
            startMonth = mes,
        });
        world.RememberId("compra", descripcion, id);
    }

    [Given(@"marco como pagada la cuota de ""(.*)"" de ""(.*)""")]
    [When(@"marco como pagada la cuota de ""(.*)"" de ""(.*)""")]
    public Task MarcoComoPagadaLaCuota(string mes, string compra) =>
        world.PostOk($"/api/installments/{world.IdOf("compra", compra)}/months/{mes}/toggle-paid", new { });

    [When(@"reviso el plan de ""(.*)"" a (\d+) cuotas de \$(\d+) desde ""(.*)""")]
    public Task RevisoElPlan(string compra, int cuotas, decimal monto, string mes) =>
        world.PostOk($"/api/installments/{world.IdOf("compra", compra)}/revise", new
        {
            startMonth = mes, totalInstallments = cuotas, frequency = "Fixed", monthlyAmount = monto,
        });

    [Then(@"la compra ""(.*)"" tiene (\d+) cuotas")]
    public async Task EntoncesLaCompraTieneCuotas(string compra, int cantidad)
    {
        var installment = await world.GetJson($"/api/installments/{world.IdOf("compra", compra)}");
        Assert.Equal(cantidad, installment.GetProperty("months").GetArrayLength());
    }

    [Then(@"la cuota de ""(.*)"" de ""(.*)"" está pagada")]
    public async Task EntoncesLaCuotaEstaPagada(string mes, string compra) =>
        Assert.True((await Cuota(compra, mes)).GetProperty("paid").GetBoolean());

    [Then(@"la cuota de ""(.*)"" de ""(.*)"" está pendiente")]
    public async Task EntoncesLaCuotaEstaPendiente(string mes, string compra) =>
        Assert.False((await Cuota(compra, mes)).GetProperty("paid").GetBoolean());

    [Then(@"la cuota de ""(.*)"" de ""(.*)"" vale \$(\d+)")]
    public async Task EntoncesLaCuotaVale(string mes, string compra, decimal monto) =>
        Assert.Equal(monto, (await Cuota(compra, mes)).GetProperty("amount").GetDecimal());

    async Task<JsonElement> Cuota(string compra, string mes)
    {
        var installment = await world.GetJson($"/api/installments/{world.IdOf("compra", compra)}");
        return installment.GetProperty("months").EnumerateArray().Single(m => m.GetProperty("month").GetString() == mes);
    }
}
