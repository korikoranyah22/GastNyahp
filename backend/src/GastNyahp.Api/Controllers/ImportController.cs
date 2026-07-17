using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

/// <summary>
/// Importa el export JSON de la maqueta (version 1.x de GastNyahp) reproduciéndolo como comandos — la carga
/// queda en el event store como si se hubiera tipeado a mano. Solo un Admin de la familia puede importar,
/// y por defecto solo sobre una familia sin datos (force=true para agregar igual).
///
/// El POST valida y devuelve 202: el job corre en background atado al lifetime de la app, NO al request —
/// un timeout del cliente o un F5 no lo cancelan. La UI sigue el avance con GET /api/import/status.
/// </summary>
[ApiController]
[Route("api/import")]
public sealed class ImportController(
    LegacyImportService import,
    ImportJobTracker tracker,
    IHostApplicationLifetime lifetime,
    ILogger<ImportController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Import([FromBody] LegacyData body, [FromQuery] bool force, [FromQuery] bool replace, CancellationToken ct)
    {
        if (!HttpContext.IsFamilyAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, "Solo un administrador de la familia puede importar datos.");

        var familyId = HttpContext.GetFamilyId();

        // Pre-checks síncronos: el 422 de familia-con-datos alimenta la elección Reemplazar/Agregar de la UI.
        if (tracker.Get(familyId)?.Status == ImportJobStatus.Running)
            return Conflict(new { error = "Ya hay una importación en curso para esta familia." });
        if (!replace && !force && await import.FamilyHasDataAsync(familyId, ct))
            return UnprocessableEntity("La familia ya tiene datos. Importá sobre una familia recién creada, o repetí con force=true para agregar igual.");

        if (!tracker.TryStart(familyId))
            return Conflict(new { error = "Ya hay una importación en curso para esta familia." });

        _ = Task.Run(async () =>
        {
            try
            {
                var (result, summary) = await import.ImportAsync(
                    familyId, body, force, replace, p => tracker.Report(familyId, p), lifetime.ApplicationStopping);
                if (result.Ok) tracker.Complete(familyId, summary!);
                else tracker.Fail(familyId, result.Error!);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Import] job de la familia {FamilyId} abortado", familyId);
                tracker.Fail(familyId, $"La importación falló a mitad de camino: {ex.Message}");
            }
        });

        return Accepted("/api/import/status", new { status = "running" });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var state = tracker.Get(HttpContext.GetFamilyId());
        if (state is null) return Ok(new { status = "idle" });

        return Ok(new
        {
            status = state.Status switch
            {
                ImportJobStatus.Running => "running",
                ImportJobStatus.Completed => "completed",
                _ => "failed",
            },
            startedAt = state.StartedAtUtc,
            finishedAt = state.FinishedAtUtc,
            progress = state.Progress,
            summary = state.Summary,
            error = state.Error,
        });
    }
}
