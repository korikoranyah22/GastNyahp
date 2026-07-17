using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GastNyahp.E2E.Tests.Support;

/// <summary>
/// Per-scenario state: one fresh app + database per Gherkin scenario. The BeforeScenario hook bootstraps a
/// default family (admin code → create → bearer on Client), so business features never mention auth; the
/// Familias feature uses AnonymousClient and explicit credentials to test the access rules themselves.
/// </summary>
public sealed class E2EWorld : IDisposable
{
    readonly GastNyahpApiFactory _factory = new();
    readonly Dictionary<(string Kind, string Name), Guid> _ids = [];

    /// <summary>Authenticated as the default family's founder after BootstrapDefaultFamilyAsync.</summary>
    public HttpClient Client { get; }

    /// <summary>Never carries a credential — for testing the anonymous surface.</summary>
    public HttpClient AnonymousClient { get; }

    public HttpResponseMessage? LastResponse { get; set; }
    public Guid DefaultFamilyId { get; private set; }

    /// <summary>Agent keys minted during the scenario, by name — shared between Familias and MCP steps.</summary>
    public Dictionary<string, (Guid KeyId, string Token)> AgentKeys { get; } = [];

    public E2EWorld()
    {
        Client = _factory.CreateClient();
        AnonymousClient = _factory.CreateClient();
    }

    // ── Family bootstrap ───────────────────────────────────────────────────────

    public async Task BootstrapDefaultFamilyAsync()
    {
        var code = await IssueAdminCodeAsync();
        var (familyId, token) = await CreateFamilyAsync(code, "Familia E2E", "Fundador");
        DefaultFamilyId = familyId;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string> IssueAdminCodeAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invites");
        request.Headers.Add("X-Admin-Key", GastNyahpApiFactory.AdminKey);
        var response = await AnonymousClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("code").GetString()!;
    }

    public async Task<(Guid FamilyId, string Token)> CreateFamilyAsync(string adminCode, string familyName, string memberName)
    {
        LastResponse = await AnonymousClient.PostAsJsonAsync("/api/families",
            new { adminInviteCode = adminCode, familyName, memberName });
        LastResponse.EnsureSuccessStatusCode();
        var payload = await LastResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (payload.GetProperty("familyId").GetGuid(), payload.GetProperty("memberToken").GetString()!);
    }

    /// <summary>GET with an explicit credential (for cross-family isolation asserts).</summary>
    public async Task<JsonElement> GetJsonAs(string token, string url)
    {
        LastResponse = await SendAs(token, HttpMethod.Get, url);
        await EnsureSuccess(url);
        return await LastResponse.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Raw request with an explicit credential — the caller asserts on the response.</summary>
    public async Task<HttpResponseMessage> SendAs(string token, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        LastResponse = await AnonymousClient.SendAsync(request);
        return LastResponse;
    }

    // ── Business-name → id map ─────────────────────────────────────────────────

    public void RememberId(string kind, string name, Guid id) => _ids[(kind, name)] = id;
    public Guid IdOf(string kind, string name) => _ids[(kind, name)];

    // ── HTTP helpers over the authenticated Client ─────────────────────────────

    public async Task PostOk(string url, object body)
    {
        LastResponse = await Client.PostAsJsonAsync(url, body);
        await EnsureSuccess(url);
    }

    public async Task<Guid> PostAndGetId(string url, object body)
    {
        LastResponse = await Client.PostAsJsonAsync(url, body);
        await EnsureSuccess(url);
        var payload = await LastResponse.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetGuid();
    }

    public async Task<JsonElement> GetJson(string url)
    {
        LastResponse = await Client.GetAsync(url);
        await EnsureSuccess(url);
        return await LastResponse.Content.ReadFromJsonAsync<JsonElement>();
    }

    async Task EnsureSuccess(string url)
    {
        if (LastResponse!.IsSuccessStatusCode) return;
        var body = await LastResponse.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"{(int)LastResponse.StatusCode} on {url}: {body}");
    }

    public void Dispose()
    {
        Client.Dispose();
        AnonymousClient.Dispose();
        _factory.Dispose();
    }
}
