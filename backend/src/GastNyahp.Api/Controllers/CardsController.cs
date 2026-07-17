using GastNyahp.Domain.Cards;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/cards")]
public sealed class CardsController(CardService cards) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await cards.GetAllAsync(HttpContext.GetFamilyId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var card = await cards.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] CardRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Label)) return BadRequest("Label is required.");
        var result = await cards.RegisterAsync(HttpContext.GetFamilyId(), body.BankId, body.Label, body.Network, body.Type, body.ClosingDay, body.DueDay, body.Color ?? "#3b82f6", ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CardRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Label)) return BadRequest("Label is required.");
        var result = await cards.UpdateAsync(HttpContext.GetFamilyId(), id, body.Label, body.Network, body.Type, body.ClosingDay, body.DueDay, body.Color ?? "#3b82f6", ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        (await cards.ActivateAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        (await cards.DeactivateAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await cards.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();
}

public record CardRequest(Guid BankId, string Label, CardNetwork Network, CardType Type, int ClosingDay, int DueDay, string? Color);
