using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/business-days")]
public sealed class BusinessDaysController(BusinessDayService businessDays) : ControllerBase
{
    [HttpGet("{date}")]
    public async Task<IActionResult> GetStatus(string date, CancellationToken ct)
    {
        if (!ApiConventions.IsValidDate(date)) return BadRequest("Date must be yyyy-MM-dd.");
        return Ok(new BusinessDayStatus(date, await businessDays.IsOpenAsync(date, ct)));
    }

    // Called by the daily scheduler (future BusinessDayScheduler hosted service) or manually/via MCP.
    // Opening an already-open date returns 422 — the caller logs and moves on (DOMAIN_MODEL.md §13.1).
    [HttpPost("{date}/open")]
    public async Task<IActionResult> Open(string date, CancellationToken ct)
    {
        if (!ApiConventions.IsValidDate(date)) return BadRequest("Date must be yyyy-MM-dd.");
        return (await businessDays.OpenAsync(date, ct)).ToActionResult();
    }

    // The novelties an agent polls every morning via MCP: unpaid installments/loans/services of the month,
    // plus cards closing or due today (DOMAIN_MODEL.md §13.2).
    [HttpGet("{date}/novelties")]
    public async Task<IActionResult> GetNovelties(string date, CancellationToken ct)
    {
        if (!ApiConventions.IsValidDate(date)) return BadRequest("Date must be yyyy-MM-dd.");
        return Ok(await businessDays.GetNoveltiesAsync(HttpContext.GetFamilyId(), date, ct));
    }
}

public record BusinessDayStatus(string Date, bool Open);
