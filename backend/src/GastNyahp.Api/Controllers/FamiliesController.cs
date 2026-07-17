using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/families")]
public sealed class FamiliesController(FamilyService families) : ControllerBase
{
    // Anonymous — the admin invite code IS the authorization (single-use, DOMAIN_MODEL.md §17.1).
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFamilyRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.AdminInviteCode)) return BadRequest("AdminInviteCode is required.");
        if (string.IsNullOrWhiteSpace(body.FamilyName)) return BadRequest("FamilyName is required.");
        if (string.IsNullOrWhiteSpace(body.MemberName)) return BadRequest("MemberName is required.");

        var (result, credential) = await families.CreateFamilyAsync(body.AdminInviteCode, body.FamilyName, body.MemberName, ct);
        return result.Ok ? Ok(credential) : UnprocessableEntity(result.Error);
    }

    // Anonymous — the QR invite code IS the authorization (single-use, expiring).
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinFamilyRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.InviteCode)) return BadRequest("InviteCode is required.");
        if (string.IsNullOrWhiteSpace(body.MemberName)) return BadRequest("MemberName is required.");

        var (result, credential) = await families.JoinFamilyAsync(body.InviteCode, body.MemberName, ct);
        return result.Ok ? Ok(credential) : UnprocessableEntity(result.Error);
    }

    // Authenticated: only a family Admin can mint invites; the QR itself is rendered by the frontend.
    [HttpPost("invites")]
    public async Task<IActionResult> IssueInvite(CancellationToken ct)
    {
        var (result, invite) = await families.IssueInviteAsync(HttpContext.GetFamilyId(), HttpContext.GetMemberId(), ct);
        return result.Ok ? Ok(invite) : UnprocessableEntity(result.Error);
    }

    // Autenticado: solo un Admin de una familia DEL DUEÑO puede emitir un código para crear una familia NUEVA
    // (el guard vive en el service). Devuelve el código crudo una sola vez; el frontend arma el enlace copiable.
    [HttpPost("family-creation-invites")]
    public async Task<IActionResult> IssueFamilyCreationInvite(CancellationToken ct)
    {
        var (result, invite) = await families.IssueFamilyCreationInviteAsync(HttpContext.GetFamilyId(), HttpContext.GetMemberId(), ct);
        return result.Ok ? Ok(invite) : UnprocessableEntity(result.Error);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var overview = await families.GetOverviewAsync(HttpContext.GetFamilyId(), HttpContext.GetMemberId(), ct);
        return overview is null ? NotFound() : Ok(overview);
    }

    // ── Cuentas y login (docs/DISENO_CUENTAS_LOGIN.md) ────────────────────────────

    /// <summary>
    /// Anónimo — el email+contraseña SON la autorización. Devuelve un token de SESIÓN nuevo (el token del miembro
    /// no se puede devolver: solo guardamos su hash).
    /// Con unicidad por familia el email puede estar en varias: si hay más de un match, 300 con la lista para que
    /// el cliente reintente con familyId (§3.2).
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        var result = await families.LoginAsync(body.Email, body.Password, body.FamilyId, body.DeviceName, ip, ct);

        if (result.Credential is not null) return Ok(result.Credential);
        if (result.Choices is not null) return StatusCode(StatusCodes.Status300MultipleChoices, new { families = result.Choices });

        if (result.RetryAfterSeconds is not null)
        {
            Response.Headers.RetryAfter = result.RetryAfterSeconds.Value.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = result.Error });
        }
        // 401 SIEMPRE genérico: no decimos si el email existe (amenaza #2).
        return Unauthorized(new { error = result.Error });
    }

    /// <summary>
    /// Crea la cuenta del miembro autenticado. En la etapa 1 se llama con el token de posesión de siempre: así
    /// los que ya estaban crean su cuenta sin quedar afuera (§3.3).
    /// </summary>
    [HttpPost("me/credentials")]
    public async Task<IActionResult> SetMyCredentials([FromBody] CredentialsRequest body, CancellationToken ct) =>
        (await families.SetCredentialsAsync(HttpContext.GetFamilyId(), HttpContext.GetMemberId(), body.Email, body.Password, ct))
            .ToActionResult();

    /// <summary>Cambia la contraseña. Cierra TODAS las sesiones (incluida la actual): hay que volver a entrar.</summary>
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest body, CancellationToken ct) =>
        (await families.ChangePasswordAsync(HttpContext.GetFamilyId(), HttpContext.GetMemberId(), body.CurrentPassword, body.NewPassword, ct))
            .ToActionResult();

    [HttpGet("me/sessions")]
    public async Task<IActionResult> MySessions(CancellationToken ct)
    {
        var raw = HttpContext.Request.Headers.Authorization.ToString();
        var token = raw.StartsWith("Bearer ", StringComparison.Ordinal) ? raw["Bearer ".Length..].Trim() : null;
        return Ok(await families.ListSessionsAsync(HttpContext.GetMemberId(), token, ct));
    }

    [HttpPost("me/sessions/{sessionId:guid}/revoke")]
    public async Task<IActionResult> RevokeMySession(Guid sessionId, CancellationToken ct) =>
        (await families.RevokeSessionAsync(HttpContext.GetFamilyId(), sessionId, HttpContext.GetMemberId(), ct)).ToActionResult();

    /// <summary>Un Admin genera el código para que un miembro resetee su contraseña. No hay mail: se lo pasa a mano.</summary>
    [HttpPost("password-resets")]
    public async Task<IActionResult> IssuePasswordReset([FromBody] PasswordResetRequest body, CancellationToken ct)
    {
        var (result, code) = await families.IssuePasswordResetAsync(
            HttpContext.GetFamilyId(), body.MemberId, HttpContext.GetMemberId(), ct);
        return result.Ok ? Ok(new PasswordResetIssued(code!)) : UnprocessableEntity(result.Error);
    }

    /// <summary>Anónimo — el código de un uso ES la autorización, igual que las invitaciones.</summary>
    [HttpPost("password-resets/redeem")]
    public async Task<IActionResult> RedeemPasswordReset([FromBody] RedeemResetRequest body, CancellationToken ct) =>
        (await families.RedeemPasswordResetAsync(body.Code, body.NewPassword, ct)).ToActionResult();

    // ── Claves de agente — el "panel" de credenciales para MCP (DOMAIN_MODEL.md §17) ──
    // El estándar para servidores MCP self-hosted es Authorization: Bearer <token> estático; la clave se
    // muestra UNA vez y se pega en la config del cliente MCP (Claude Desktop, cron del agente, etc.).

    [HttpPost("agent-keys")]
    public async Task<IActionResult> IssueAgentKey([FromBody] IssueAgentKeyRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required.");
        var (result, token) = await families.IssueAgentKeyAsync(HttpContext.GetFamilyId(), HttpContext.GetMemberId(), body.Name, ct);
        return result.Ok ? Ok(new AgentKeyIssued(result.Id!.Value, body.Name, token!)) : UnprocessableEntity(result.Error);
    }

    [HttpGet("agent-keys")]
    public async Task<IActionResult> ListAgentKeys(CancellationToken ct) =>
        Ok(await families.ListAgentKeysAsync(HttpContext.GetFamilyId(), ct));

    [HttpPost("agent-keys/{keyId:guid}/revoke")]
    public async Task<IActionResult> RevokeAgentKey(Guid keyId, CancellationToken ct) =>
        (await families.RevokeAgentKeyAsync(HttpContext.GetFamilyId(), keyId, HttpContext.GetMemberId(), ct)).ToActionResult();
}

public record CreateFamilyRequest(string AdminInviteCode, string FamilyName, string MemberName);
public record JoinFamilyRequest(string InviteCode, string MemberName);
public record IssueAgentKeyRequest(string Name);
public record AgentKeyIssued(Guid KeyId, string Name, string Token);

/// <param name="FamilyId">Solo hace falta si el email está en más de una familia (el 300 de §3.2).</param>
public record LoginRequest(string Email, string Password, Guid? FamilyId = null, string? DeviceName = null);
public record CredentialsRequest(string Email, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record PasswordResetRequest(Guid MemberId);
public record RedeemResetRequest(string Code, string NewPassword);
public record PasswordResetIssued(string Code);
