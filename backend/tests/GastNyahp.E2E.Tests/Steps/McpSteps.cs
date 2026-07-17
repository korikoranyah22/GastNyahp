using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GastNyahp.E2E.Tests.Support;
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

/// <summary>
/// Talks the real MCP streamable-HTTP protocol (JSON-RPC over POST /mcp) exactly like Claude Desktop or a
/// cron agent would: initialize → notifications/initialized → tools/call, echoing the Mcp-Session-Id header.
/// </summary>
[Binding]
public sealed class McpSteps(E2EWorld world)
{
    readonly Dictionary<string, string> _sessions = []; // agent key name → MCP session id
    string? _lastToolText;

    [When(@"un agente intenta inicializar la sesión MCP sin credencial")]
    public async Task AgenteSinCredencial() =>
        world.LastResponse = await PostRpc(token: null, sessionId: null, InitializePayload());

    [When(@"el agente ""(.*)"" llama a la tool ""(.*)"" con fecha ""(.*)""")]
    public async Task ElAgenteLlamaLaTool(string agente, string tool, string fecha) =>
        _lastToolText = await CallTool(agente, tool, new { fecha });

    [When(@"el agente ""(.*)"" registra por MCP un gasto ""(.*)"" de \$(\d+) en efectivo el ""(.*)""")]
    public async Task ElAgenteRegistraUnGasto(string agente, string descripcion, decimal monto, string fecha) =>
        _lastToolText = await CallTool(agente, "gasto_registrar", new
        {
            fecha, descripcion, categoria = "Comida", monto, medio = "Efectivo",
        });

    [Then(@"la respuesta de la tool menciona ""(.*)""")]
    public void LaRespuestaMenciona(string fragmento) =>
        Assert.Contains(fragmento, _lastToolText);

    // ── Protocolo MCP ──────────────────────────────────────────────────────────

    async Task<string> CallTool(string agentKeyName, string tool, object arguments)
    {
        var token = world.AgentKeys[agentKeyName].Token;
        var sessionId = await EnsureSession(agentKeyName, token);

        var response = await PostRpc(token, sessionId, new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new { name = tool, arguments },
        });
        response.EnsureSuccessStatusCode();

        var rpc = await ReadRpcResult(response);
        var content = rpc.GetProperty("result").GetProperty("content");
        return content[0].GetProperty("text").GetString()!;
    }

    async Task<string> EnsureSession(string agentKeyName, string token)
    {
        if (_sessions.TryGetValue(agentKeyName, out var existing)) return existing;

        var response = await PostRpc(token, sessionId: null, InitializePayload());
        response.EnsureSuccessStatusCode();
        var sessionId = response.Headers.TryGetValues("Mcp-Session-Id", out var values) ? values.First() : "";

        var initialized = await PostRpc(token, sessionId, new { jsonrpc = "2.0", method = "notifications/initialized" });
        Assert.True(initialized.IsSuccessStatusCode, $"initialized notification failed: {(int)initialized.StatusCode}");

        _sessions[agentKeyName] = sessionId;
        return sessionId;
    }

    static object InitializePayload() => new
    {
        jsonrpc = "2.0",
        id = 1,
        method = "initialize",
        @params = new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "gastnyahp-e2e", version = "1.0" },
        },
    };

    async Task<HttpResponseMessage> PostRpc(string? token, string? sessionId, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(sessionId)) request.Headers.Add("Mcp-Session-Id", sessionId);

        world.LastResponse = await world.AnonymousClient.SendAsync(request);
        return world.LastResponse;
    }

    /// <summary>The SDK answers either plain JSON or an SSE stream — extract the JSON-RPC message from both.</summary>
    static async Task<JsonElement> ReadRpcResult(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            var dataLine = body.Split('\n').First(l => l.StartsWith("data: ", StringComparison.Ordinal));
            return JsonSerializer.Deserialize<JsonElement>(dataLine["data: ".Length..]);
        }
        return JsonSerializer.Deserialize<JsonElement>(body);
    }
}
