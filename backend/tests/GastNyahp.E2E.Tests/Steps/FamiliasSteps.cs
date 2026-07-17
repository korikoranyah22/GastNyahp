using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class FamiliasSteps(E2EWorld world)
{
    string? _adminCode;
    string? _inviteCode;
    string? _secondFamilyToken;

    // ── Creación de familia (gate de administrador) ────────────────────────────

    [When(@"intento crear la familia ""(.*)"" con el código ""(.*)""")]
    public async Task CuandoIntentoCrearLaFamiliaConElCodigo(string nombre, string codigo) =>
        world.LastResponse = await world.AnonymousClient.PostAsJsonAsync("/api/families",
            new { adminInviteCode = codigo, familyName = nombre, memberName = "Fundador" });

    [Given(@"un código de administrador emitido")]
    public async Task DadoUnCodigoDeAdministrador() => _adminCode = await world.IssueAdminCodeAsync();

    [When(@"creo la familia ""(.*)"" con ese código")]
    public async Task CuandoCreoLaFamiliaConEseCodigo(string nombre) =>
        world.LastResponse = await world.AnonymousClient.PostAsJsonAsync("/api/families",
            new { adminInviteCode = _adminCode, familyName = nombre, memberName = "Fundador" });

    // ── Invitaciones QR ────────────────────────────────────────────────────────

    [Given(@"una invitación generada para mi familia")]
    [When(@"genero una invitación para mi familia")]
    public async Task GeneroUnaInvitacion()
    {
        world.LastResponse = await world.Client.PostAsJsonAsync("/api/families/invites", new { });
        world.LastResponse.EnsureSuccessStatusCode();
        var payload = await world.LastResponse.Content.ReadFromJsonAsync<JsonElement>();
        _inviteCode = payload.GetProperty("inviteCode").GetString();
        // El QR lo renderiza el frontend a partir de este payload (DOMAIN_MODEL.md §17.2).
        Assert.StartsWith("gastnyahp://join?code=", payload.GetProperty("qrPayload").GetString());
    }

    [Given(@"alguien se une con esa invitación como ""(.*)""")]
    [When(@"alguien se une con esa invitación como ""(.*)""")]
    public async Task AlguienSeUneConEsaInvitacion(string nombre) =>
        world.LastResponse = await world.AnonymousClient.PostAsJsonAsync("/api/families/join",
            new { inviteCode = _inviteCode, memberName = nombre });

    [When(@"otra persona intenta unirse con la misma invitación como ""(.*)""")]
    public Task OtraPersonaIntentaUnirse(string nombre) => AlguienSeUneConEsaInvitacion(nombre);

    [Then(@"mi familia tiene (\d+) miembros")]
    public async Task EntoncesMiFamiliaTieneMiembros(int cantidad)
    {
        var overview = await world.GetJson("/api/families/me");
        Assert.Equal(cantidad, overview.GetProperty("members").GetArrayLength());
    }

    // ── Superficie anónima y aislamiento ───────────────────────────────────────

    [When(@"consulto los bancos sin credencial")]
    public async Task CuandoConsultoSinCredencial() =>
        world.LastResponse = await world.AnonymousClient.GetAsync("/api/banks");

    [Then(@"recibo un error de credencial requerida")]
    public void EntoncesErrorDeCredencial() =>
        Assert.Equal(HttpStatusCode.Unauthorized, world.LastResponse!.StatusCode);

    [Given(@"una segunda familia ""(.*)""")]
    public async Task DadaUnaSegundaFamilia(string nombre)
    {
        var code = await world.IssueAdminCodeAsync();
        var (_, token) = await world.CreateFamilyAsync(code, nombre, "Fundador 2");
        _secondFamilyToken = token;
    }

    [Then(@"la segunda familia no ve ningún banco")]
    public async Task EntoncesLaSegundaFamiliaNoVeBancos()
    {
        var banks = await world.GetJsonAs(_secondFamilyToken!, "/api/banks");
        Assert.Equal(0, banks.GetArrayLength()); // cross-family isolation (DOMAIN_MODEL.md §17.3)
    }

    // ── Claves de agente (credencial estándar para clientes MCP) ───────────────

    [Given(@"una clave de agente llamada ""(.*)""")]
    [When(@"genero una clave de agente llamada ""(.*)""")]
    public async Task GeneroUnaClaveDeAgente(string nombre)
    {
        world.LastResponse = await world.Client.PostAsJsonAsync("/api/families/agent-keys", new { name = nombre });
        world.LastResponse.EnsureSuccessStatusCode();
        var payload = await world.LastResponse.Content.ReadFromJsonAsync<JsonElement>();
        world.AgentKeys[nombre] = (payload.GetProperty("keyId").GetGuid(), payload.GetProperty("token").GetString()!);
    }

    [Then(@"con la clave de agente ""(.*)"" se ve el banco ""(.*)""")]
    public async Task ConLaClaveSeVeElBanco(string clave, string banco)
    {
        var banks = await world.GetJsonAs(world.AgentKeys[clave].Token, "/api/banks");
        Assert.Contains(banks.EnumerateArray(), b => b.GetProperty("name").GetString() == banco);
    }

    [When(@"revoco la clave de agente ""(.*)""")]
    public async Task RevocoLaClaveDeAgente(string nombre) =>
        world.LastResponse = await world.Client.PostAsJsonAsync($"/api/families/agent-keys/{world.AgentKeys[nombre].KeyId}/revoke", new { });

    [Then(@"la clave de agente ""(.*)"" ya no puede acceder a los datos")]
    public async Task LaClaveYaNoAccede(string nombre)
    {
        var response = await world.SendAs(world.AgentKeys[nombre].Token, HttpMethod.Get, "/api/banks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [When(@"la clave de agente ""(.*)"" intenta generar una invitación")]
    public Task LaClaveIntentaGenerarInvitacion(string nombre) =>
        world.SendAs(world.AgentKeys[nombre].Token, HttpMethod.Post, "/api/families/invites", new { });
}
