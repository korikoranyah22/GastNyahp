using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GastNyahp.Api.Auth;
using GastNyahp.E2E.Tests.Support;

namespace GastNyahp.E2E.Tests;

public sealed class OAuthTests : IClassFixture<GastNyahpApiFactory>
{
    readonly HttpClient _client;

    public OAuthTests(GastNyahpApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Discovery_exposes_authorization_token_registration_and_pkce()
    {
        var metadata = await _client.GetFromJsonAsync<JsonElement>("/.well-known/oauth-authorization-server");

        Assert.Equal("http://localhost/oauth/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/oauth/token", metadata.GetProperty("token_endpoint").GetString());
        Assert.Equal("http://localhost/oauth/register", metadata.GetProperty("registration_endpoint").GetString());
        Assert.Contains("S256", metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Protected_mcp_advertises_resource_metadata()
    {
        var response = await _client.PostAsync("/mcp", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Dynamic_registration_accepts_https_redirect_and_rejects_insecure_remote_http()
    {
        var valid = await _client.PostAsJsonAsync("/oauth/register", new
        {
            redirect_uris = new[] { "https://chatgpt.com/connector/oauth/callback" }
        });
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await valid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("client_id").GetString()));

        var invalid = await _client.PostAsJsonAsync("/oauth/register", new
        {
            redirect_uris = new[] { "http://example.com/callback" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public void Authorization_code_is_single_use_and_bound_to_pkce_redirect_and_client()
    {
        var store = new OAuthFlowStore();
        var redirect = "https://chatgpt.com/connector/oauth/callback";
        var client = store.Register(new[] { redirect });
        var verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var code = store.IssueCode(client.ClientId, redirect, challenge, "https://gastnyahp.example/mcp", "member-token");

        Assert.Null(store.RedeemCode(code, client.ClientId, redirect, "verifier-equivocado"));
        Assert.Null(store.RedeemCode(code, client.ClientId, redirect, verifier));

        code = store.IssueCode(client.ClientId, redirect, challenge, null, "member-token");
        Assert.Equal("member-token", store.RedeemCode(code, client.ClientId, redirect, verifier)?.AccessToken);
        Assert.Null(store.RedeemCode(code, client.ClientId, redirect, verifier));
    }
    [Fact]
    public async Task Authorization_requires_s256_pkce()
    {
        var registration = await _client.PostAsJsonAsync("/oauth/register", new
        {
            redirect_uris = new[] { "https://chatgpt.com/connector/oauth/callback" }
        });
        var clientId = (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("client_id").GetString();

        var response = await _client.GetAsync(
            $"/oauth/authorize?client_id={Uri.EscapeDataString(clientId!)}&redirect_uri={Uri.EscapeDataString("https://chatgpt.com/connector/oauth/callback")}&response_type=code&code_challenge=x&code_challenge_method=plain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}