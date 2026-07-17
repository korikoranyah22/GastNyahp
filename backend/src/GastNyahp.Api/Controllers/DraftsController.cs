using GastNyahp.Api.Auth;
using GastNyahp.Domain.Drafts;
using GastNyahp.Infrastructure.Projections.Drafts;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

/// <summary>
/// Borradores conversacionales (DOMAIN_MODEL.md §19): un agente MCP o la UI crean el borrador, lo van
/// moldeando con updates (snapshot completo por versión — el historial queda auditable), y al confirmar el
/// DraftService dispara el comando real. Cualquier miembro Y las claves de agente pueden operar borradores:
/// son exactamente la superficie pensada para agentes.
/// </summary>
[ApiController]
[Route("api/drafts")]
public sealed class DraftsController(DraftService drafts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool all, CancellationToken ct) =>
        Ok((await drafts.GetAsync(HttpContext.GetFamilyId(), onlyOpen: !all, ct)).Select(ToDto));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var draft = await drafts.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return draft is null ? NotFound() : Ok(ToDto(draft));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DraftRequest body, CancellationToken ct)
    {
        if (!Enum.TryParse<DraftKind>(body.Kind, ignoreCase: true, out var kind))
            return BadRequest($"Kind '{body.Kind}' desconocido (Expense, Ticket o Installment).");

        var result = await drafts.CreateAsync(
            HttpContext.GetFamilyId(), kind, body.Payload ?? new DraftPayload(),
            HttpContext.IsAgent() ? "Agent" : "Member", HttpContext.GetMemberId(), ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DraftPayload payload, CancellationToken ct) =>
        (await drafts.UpdateAsync(HttpContext.GetFamilyId(), id, payload, ct)).ToActionResult();

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        (await drafts.ConfirmAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    [HttpPost("{id:guid}/discard")]
    public async Task<IActionResult> Discard(Guid id, [FromBody] DiscardRequest? body, CancellationToken ct) =>
        (await drafts.DiscardAsync(HttpContext.GetFamilyId(), id, body?.Reason, ct)).ToActionResult();

    static object ToDto(DraftEntity d) => new
    {
        id = d.Id,
        kind = d.Kind,
        status = d.Status,
        payload = DraftProjection.Deserialize(d.PayloadJson),
        createdByKind = d.CreatedByKind,
        resultEntityId = d.ResultEntityId,
        createdAt = d.CreatedAt,
        updatedAt = d.UpdatedAt,
    };
}

public record DraftRequest(string Kind, DraftPayload? Payload);
public record DiscardRequest(string? Reason);
