using System.Net.Http.Json;
using System.Text.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class BorradoresSteps(E2EWorld world)
{
    Guid _draftId;
    static string ThisMonth => DateTime.UtcNow.ToString("yyyy-MM");

    // ── Setup ────────────────────────────────────────────────────────────────────

    [Given(@"un borrador de ticket ""(.*)""")]
    public async Task DadoUnBorradorDeTicket(string descripcion) =>
        _draftId = await world.PostAndGetId("/api/drafts", new { kind = "Ticket", payload = new { description = descripcion } });

    [Given(@"un borrador de gasto ""(.*)"" por \$(\d+)")]
    public async Task DadoUnBorradorDeGasto(string descripcion, decimal monto) =>
        _draftId = await world.PostAndGetId("/api/drafts", new { kind = "Expense", payload = new { description = descripcion, amount = monto } });

    [Given(@"un borrador de cuotas ""(.*)"" con la tarjeta ""(.*)"" en (\d+) cuotas de \$(\d+)")]
    public async Task DadoUnBorradorDeCuotas(string descripcion, string tarjeta, int cuotas, decimal cuotaMensual) =>
        _draftId = await world.PostAndGetId("/api/drafts", new
        {
            kind = "Installment",
            payload = new { description = descripcion, cardId = world.IdOf("tarjeta", tarjeta), totalInstallments = cuotas, monthlyAmount = cuotaMensual },
        });

    // ── Moldeado (el flujo conversacional: leer payload actual → snapshot nuevo) ──

    [When(@"le agrego al borrador el ítem ""(.*)"" de \$(\d+) en ""(.*)""")]
    public async Task AgregoItem(string descripcion, decimal monto, string categoria)
    {
        var payload = await PayloadActual();
        var items = payload.TryGetProperty("items", out var existing) && existing.ValueKind == JsonValueKind.Array
            ? existing.EnumerateArray().Select(i => (object)i).ToList()
            : [];
        items.Add(new { description = descripcion, amount = monto, category = categoria });
        await ActualizarPayload(payload, items: items);
    }

    [When(@"actualizo el borrador con un descuento de \$(\d+)")]
    public async Task ActualizoConDescuento(decimal descuento)
    {
        var payload = await PayloadActual();
        await ActualizarPayload(payload, discount: descuento);
    }

    [When(@"confirmo el borrador")]
    public async Task ConfirmoElBorrador() =>
        world.LastResponse = await world.Client.PostAsJsonAsync($"/api/drafts/{_draftId}/confirm", new { });

    [When(@"descarto el borrador")]
    public async Task DescartoElBorrador() =>
        world.LastResponse = await world.Client.PostAsJsonAsync($"/api/drafts/{_draftId}/discard", new { reason = "test" });

    // ── Flujo con clave de agente (la superficie MCP usa exactamente estos endpoints) ──

    [When(@"la clave ""(.*)"" crea un borrador de gasto ""(.*)"" por \$(\d+)")]
    public async Task LaClaveCreaBorrador(string nombre, string descripcion, decimal monto)
    {
        var response = await world.SendAs(world.AgentKeys[nombre].Token, HttpMethod.Post, "/api/drafts",
            new { kind = "Expense", payload = new { description = descripcion, amount = monto } });
        response.EnsureSuccessStatusCode();
        _draftId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [When(@"la clave ""(.*)"" confirma ese borrador")]
    public async Task LaClaveConfirma(string nombre)
    {
        var response = await world.SendAs(world.AgentKeys[nombre].Token, HttpMethod.Post, $"/api/drafts/{_draftId}/confirm", new { });
        response.EnsureSuccessStatusCode();
    }

    // ── Asserts ──────────────────────────────────────────────────────────────────

    [Then(@"el borrador sigue abierto")]
    public async Task ElBorradorSigueAbierto()
    {
        var drafts = await world.GetJson("/api/drafts");
        Assert.Contains(drafts.EnumerateArray(), d => d.GetProperty("id").GetGuid() == _draftId && d.GetProperty("status").GetString() == "Open");
    }

    [Then(@"no quedan borradores abiertos")]
    public async Task NoQuedanBorradoresAbiertos()
    {
        var drafts = await world.GetJson("/api/drafts");
        Assert.Empty(drafts.EnumerateArray());
    }

    [Then(@"el ticket ""(.*)"" de este mes totaliza \$(\d+)")]
    public async Task ElTicketDeEsteMesTotaliza(string descripcion, decimal total)
    {
        var tickets = await world.GetJson($"/api/tickets?month={ThisMonth}");
        var ticket = tickets.EnumerateArray().Single(t => t.GetProperty("description").GetString() == descripcion);
        Assert.Equal(total, ticket.GetProperty("total").GetDecimal());
    }

    [Then(@"no hay gastos este mes")]
    public async Task NoHayGastosEsteMes()
    {
        var expenses = await world.GetJson($"/api/expenses?month={ThisMonth}");
        Assert.Empty(expenses.EnumerateArray());
    }

    [Then(@"el gasto ""(.*)"" de este mes figura por \$(\d+)")]
    public async Task ElGastoDeEsteMesFigura(string descripcion, decimal monto)
    {
        var expenses = await world.GetJson($"/api/expenses?month={ThisMonth}");
        var expense = expenses.EnumerateArray().Single(e => e.GetProperty("description").GetString() == descripcion);
        Assert.Equal(monto, expense.GetProperty("amountArs").GetDecimal());
    }

    [Then(@"la compra en cuotas ""(.*)"" tiene (\d+) meses en su calendario")]
    public async Task LaCompraTieneMeses(string descripcion, int meses)
    {
        var installments = await world.GetJson("/api/installments");
        var installment = installments.EnumerateArray().Single(i => i.GetProperty("description").GetString() == descripcion);
        Assert.Equal(meses, installment.GetProperty("months").GetArrayLength());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    async Task<JsonElement> PayloadActual() =>
        (await world.GetJson($"/api/drafts/{_draftId}")).GetProperty("payload");

    /// <summary>PUT con snapshot completo: los campos no tocados se re-mandan tal cual (mismo contrato que usan las tools MCP).</summary>
    async Task ActualizarPayload(JsonElement current, List<object>? items = null, decimal? discount = null)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetRawText())!;
        if (items is not null) body["items"] = items;
        if (discount is not null) body["discount"] = discount;
        world.LastResponse = await world.Client.PutAsJsonAsync($"/api/drafts/{_draftId}", body);
        world.LastResponse.EnsureSuccessStatusCode();
    }
}
