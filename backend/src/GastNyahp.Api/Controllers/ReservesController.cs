using GastNyahp.Domain.Reserves;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/reserves")]
public sealed class ReservesController(ReserveService reserves) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await reserves.GetAllAsync(HttpContext.GetFamilyId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var reserve = await reserves.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return reserve is null ? NotFound() : Ok(reserve);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterReserveRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Label)) return BadRequest("Label is required.");
        return (await reserves.RegisterAsync(HttpContext.GetFamilyId(), body.Label, body.Type, body.Icon ?? "👤", body.Recurring, body.BaseAmount, ct)).ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDetails(Guid id, [FromBody] UpdateReserveRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Label)) return BadRequest("Label is required.");
        return (await reserves.UpdateDetailsAsync(HttpContext.GetFamilyId(), id, body.Label, body.Type, body.Icon ?? "👤", ct)).ToActionResult();
    }

    [HttpPut("{id:guid}/months/{month}")]
    public async Task<IActionResult> SetMonthAmount(Guid id, string month, [FromBody] ReserveMonthRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await reserves.SetMonthAmountAsync(HttpContext.GetFamilyId(), id, month, body.Amount, body.Note, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/apply-base")]
    public async Task<IActionResult> ApplyBase(Guid id, [FromBody] AmountRequest body, CancellationToken ct) =>
        (await reserves.ApplyBaseToAllMonthsAsync(HttpContext.GetFamilyId(), id, body.Amount, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await reserves.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();
}

public record RegisterReserveRequest(string Label, ReserveType Type, string? Icon, bool Recurring, decimal BaseAmount);
public record UpdateReserveRequest(string Label, ReserveType Type, string? Icon);
public record ReserveMonthRequest(decimal Amount, string? Note);
