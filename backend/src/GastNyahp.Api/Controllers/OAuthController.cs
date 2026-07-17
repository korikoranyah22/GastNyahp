using System.Net;
using System.Text;
using System.Text.Json;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
public sealed class OAuthController(
    OAuthFlowStore flows,
    FamilyService families,
    IConfiguration configuration) : ControllerBase
{
    string Issuer
    {
        get
        {
            var configured = configuration["OAuth:Issuer"]?.TrimEnd('/');
            return !string.IsNullOrWhiteSpace(configured)
                ? configured
                : $"{Request.Scheme}://{Request.Host}";
        }
    }

    [HttpGet("/.well-known/oauth-protected-resource")]
    [HttpGet("/.well-known/oauth-protected-resource/mcp")]
    public IActionResult ProtectedResource() => Ok(new
    {
        resource = $"{Issuer}/mcp",
        authorization_servers = new[] { Issuer },
        bearer_methods_supported = new[] { "header" },
        scopes_supported = new[] { "mcp" }
    });

    [HttpGet("/.well-known/oauth-authorization-server")]
    public IActionResult AuthorizationServer() => Ok(new
    {
        issuer = Issuer,
        authorization_endpoint = $"{Issuer}/oauth/authorize",
        token_endpoint = $"{Issuer}/oauth/token",
        registration_endpoint = $"{Issuer}/oauth/register",
        response_types_supported = new[] { "code" },
        grant_types_supported = new[] { "authorization_code" },
        code_challenge_methods_supported = new[] { "S256" },
        token_endpoint_auth_methods_supported = new[] { "none" },
        scopes_supported = new[] { "mcp" }
    });

    [HttpPost("/oauth/register")]
    public async Task<IActionResult> RegisterClient()
    {
        using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: HttpContext.RequestAborted);
        if (!document.RootElement.TryGetProperty("redirect_uris", out var redirects) ||
            redirects.ValueKind != JsonValueKind.Array)
            return BadRequest(new { error = "invalid_client_metadata", error_description = "redirect_uris es requerido." });

        try
        {
            var client = flows.Register(redirects.EnumerateArray().Select(x => x.GetString() ?? ""));
            return StatusCode(StatusCodes.Status201Created, new
            {
                client_id = client.ClientId,
                client_id_issued_at = client.CreatedAt.ToUnixTimeSeconds(),
                redirect_uris = client.RedirectUris,
                token_endpoint_auth_method = "none",
                grant_types = new[] { "authorization_code" },
                response_types = new[] { "code" }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_redirect_uri", error_description = ex.Message });
        }
    }

    [HttpGet("/oauth/authorize")]
    public IActionResult Authorize(
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "code_challenge")] string codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string codeChallengeMethod,
        [FromQuery] string? state,
        [FromQuery] string? resource,
        [FromQuery] string? scope)
    {
        var error = ValidateAuthorize(clientId, redirectUri, responseType, codeChallenge, codeChallengeMethod);
        if (error is not null) return BadRequest(error);
        return Content(RenderLogin(clientId, redirectUri, codeChallenge, state, resource, scope), "text/html", Encoding.UTF8);
    }

    [HttpPost("/oauth/authorize")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> AuthorizePost(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var clientId = form["client_id"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var responseType = form["response_type"].ToString();
        var challenge = form["code_challenge"].ToString();
        var challengeMethod = form["code_challenge_method"].ToString();
        var state = form["state"].ToString();
        var resource = form["resource"].ToString();
        var scope = form["scope"].ToString();
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        Guid? familyId = Guid.TryParse(form["family_id"], out var parsedFamily) ? parsedFamily : null;

        var validationError = ValidateAuthorize(clientId, redirectUri, responseType, challenge, challengeMethod);
        if (validationError is not null) return BadRequest(validationError);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var login = await families.LoginAsync(email, password, familyId, "ChatGPT OAuth", ip, ct);
        if (login.RetryAfterSeconds is not null) Response.Headers.RetryAfter = login.RetryAfterSeconds.Value.ToString();

        if (login.Credential is null)
        {
            var message = login.Choices is { Count: > 0 }
                ? "Tu cuenta pertenece a más de una familia. Elegí una y volvé a escribir la contraseña."
                : login.Error ?? "No se pudo iniciar sesión.";
            return Content(RenderLogin(clientId, redirectUri, challenge, state, resource, scope, message, email, login.Choices),
                "text/html", Encoding.UTF8);
        }

        var code = flows.IssueCode(clientId, redirectUri, challenge, resource, login.Credential.MemberToken);
        return Redirect(AppendQuery(redirectUri, ("code", code), ("state", state)));
    }

    [HttpPost("/oauth/token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (form["grant_type"] != "authorization_code")
            return BadRequest(new { error = "unsupported_grant_type" });

        var redeemed = flows.RedeemCode(
            form["code"].ToString(),
            form["client_id"].ToString(),
            form["redirect_uri"].ToString(),
            form["code_verifier"].ToString());

        if (redeemed is null)
            return BadRequest(new { error = "invalid_grant", error_description = "El código es inválido, venció o PKCE no coincide." });

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return Ok(new
        {
            access_token = redeemed.AccessToken,
            token_type = "Bearer",
            scope = "mcp"
        });
    }

    object? ValidateAuthorize(string clientId, string redirectUri, string responseType, string challenge, string method)
    {
        if (responseType != "code")
            return new { error = "unsupported_response_type" };
        if (method != "S256" || string.IsNullOrWhiteSpace(challenge))
            return new { error = "invalid_request", error_description = "PKCE S256 es obligatorio." };
        if (!flows.IsRedirectAllowed(clientId, redirectUri))
            return new { error = "invalid_request", error_description = "client_id o redirect_uri no registrado." };
        return null;
    }

    string RenderLogin(
        string clientId, string redirectUri, string challenge, string? state, string? resource, string? scope,
        string? error = null, string? email = null, IReadOnlyList<FamilyChoice>? choices = null)
    {
        static string H(string? value) => WebUtility.HtmlEncode(value ?? "");
        var familySelect = choices is { Count: > 0 }
            ? $"""<label>Familia<select name="family_id" required><option value="">Elegí una familia</option>{string.Join("", choices.Select(c => $"<option value=\"{c.FamilyId}\">{H(c.Name)}</option>"))}</select></label>"""
            : "";

        return $$"""
<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Autorizar ChatGPT · GastNyahp</title>
<style>
*{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;background:#0d0f14;color:#e2e8f0;font:14px system-ui;padding:20px}
main{width:min(420px,100%);background:#151820;border:1px solid #2e3350;border-radius:16px;padding:24px}
h1{font-size:20px;margin:0 0 8px}.muted{color:#94a3b8;font-size:12px;line-height:1.5}.error{color:#fca5a5;background:#7f1d1d33;border:1px solid #ef444455;padding:10px;border-radius:8px}
label{display:grid;gap:6px;margin-top:14px;color:#94a3b8;font-size:12px}input,select{width:100%;padding:10px 12px;border-radius:9px;border:1px solid #2e3350;background:#1c2030;color:white}
button{width:100%;margin-top:18px;padding:11px;border:0;border-radius:9px;background:#2563eb;color:white;font-weight:650;cursor:pointer}
</style></head><body><main>
<h1>Autorizar ChatGPT</h1>
<p class="muted">Iniciá sesión para permitir que ChatGPT use las herramientas de tu familia en GastNyahp. Se creará una sesión revocable llamada “ChatGPT OAuth”.</p>
{{(error is null ? "" : $"<p class=\"error\">{H(error)}</p>")}}
<form method="post" action="/oauth/authorize">
<input type="hidden" name="client_id" value="{{H(clientId)}}"><input type="hidden" name="redirect_uri" value="{{H(redirectUri)}}">
<input type="hidden" name="response_type" value="code"><input type="hidden" name="code_challenge" value="{{H(challenge)}}">
<input type="hidden" name="code_challenge_method" value="S256"><input type="hidden" name="state" value="{{H(state)}}">
<input type="hidden" name="resource" value="{{H(resource)}}"><input type="hidden" name="scope" value="{{H(scope)}}">
<label>Email<input type="email" name="email" autocomplete="username" value="{{H(email)}}" required></label>
{{familySelect}}
<label>Contraseña<input type="password" name="password" autocomplete="current-password" required></label>
<button type="submit">Autorizar ChatGPT</button>
</form></main></body></html>
""";
    }

    static string AppendQuery(string url, params (string Key, string? Value)[] values)
    {
        var separator = url.Contains('?') ? '&' : '?';
        var query = string.Join("&", values
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));
        return url + separator + query;
    }
}