using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

/// <summary>
/// The app administrator's gate (DOMAIN_MODEL.md §17.1): issues the single-use codes that allow creating a
/// family. Protected by a static X-Admin-Key from configuration — no admin key configured means the gate is
/// closed entirely (503), so a default deployment can't be farmed for families.
/// </summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminController(FamilyService families, IConfiguration configuration) : ControllerBase
{
    [HttpPost("invites")]
    public async Task<IActionResult> IssueInvite(CancellationToken ct)
    {
        var configuredKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrEmpty(configuredKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Admin:ApiKey no está configurada — la emisión de códigos está deshabilitada.");

        if (Request.Headers["X-Admin-Key"].ToString() != configuredKey)
            return Unauthorized();

        var (result, code) = await families.IssueAdminInviteAsync(ct);
        return result.Ok ? Ok(new AdminInviteResponse(code!)) : UnprocessableEntity(result.Error);
    }

    /// <summary>
    /// LA SALIDA DE EMERGENCIA (docs/DISENO_CUENTAS_LOGIN.md §3.1). Sin esto, el único Admin de una familia que
    /// olvida su contraseña la deja inaccesible PARA SIEMPRE, con todos sus datos adentro — porque el reseteo
    /// normal lo tiene que emitir un Admin, y él es el Admin.
    ///
    /// <para>Quien opera la instancia (la misma llave que habilita crear familias) siempre puede destrabarlo.
    /// Mismo patrón que <see cref="IssueInvite"/>: misma llave, código de un solo uso.</para>
    ///
    /// <para><b>No borrar "porque parece un agujero".</b> Hay un test de dominio que fija esta vía
    /// (<c>The_instance_flow_can_issue_a_reset_without_being_a_member</c>).</para>
    /// </summary>
    [HttpPost("password-resets")]
    public async Task<IActionResult> IssuePasswordReset([FromBody] AdminPasswordResetRequest body, CancellationToken ct)
    {
        var configuredKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrEmpty(configuredKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Admin:ApiKey no está configurada — el reseteo de instancia está deshabilitado.");
        if (Request.Headers["X-Admin-Key"].ToString() != configuredKey)
            return Unauthorized();

        var member = await families.FindMemberByEmailAsync(body.FamilyId, body.Email, ct);
        if (member is null) return NotFound("No hay un miembro con ese email en esa familia.");

        // issuedByMemberId: null = flujo de instancia. El aggregate lo acepta sin exigir Admin de la familia.
        var (result, code) = await families.IssuePasswordResetAsync(member.Value.FamilyId, member.Value.MemberId, null, ct);
        return result.Ok ? Ok(new AdminInviteResponse(code!)) : UnprocessableEntity(result.Error);
    }
}

public record AdminInviteResponse(string Code);
public record AdminPasswordResetRequest(Guid FamilyId, string Email);
