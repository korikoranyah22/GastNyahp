using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/planning")]
public sealed class PlanningController(PlanningService planning) : ControllerBase
{
    [HttpGet("budgets")]
    public async Task<IActionResult> GetAllBudgets(CancellationToken ct) =>
        Ok(await planning.GetAllBudgetsAsync(HttpContext.GetFamilyId(), ct));

    [HttpGet("budget/{month}")]
    public async Task<IActionResult> GetBudget(string month, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return Ok(await planning.GetBudgetAsync(HttpContext.GetFamilyId(), month, ct));
    }

    [HttpPut("budget/{month}")]
    public async Task<IActionResult> SetBudget(string month, [FromBody] BudgetRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await planning.SetBudgetAsync(HttpContext.GetFamilyId(), month, body.CreditLimit, body.DebitCashLimit, body.WeeklyLimit, ct)).ToActionResult();
    }

    [HttpGet("income")]
    public async Task<IActionResult> GetIncome(CancellationToken ct) => Ok(await planning.GetIncomeAsync(HttpContext.GetFamilyId(), ct));

    [HttpPut("income")]
    public async Task<IActionResult> UpdateIncome([FromBody] IncomeRequest body, CancellationToken ct) =>
        (await planning.UpdateIncomeAsync(HttpContext.GetFamilyId(), body.NetMonthly, body.UsdRateOfficial, body.UsdRateCcl, body.SplitPercent, ct)).ToActionResult();

    [HttpPost("copy-month")]
    public async Task<IActionResult> CopyMonth([FromBody] CopyMonthRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(body.FromMonth) || !ApiConventions.IsValidMonth(body.ToMonth))
            return BadRequest("FromMonth and ToMonth must be yyyy-MM.");
        return (await planning.CopyMonthAsync(HttpContext.GetFamilyId(), body.FromMonth, body.ToMonth, ct)).ToActionResult();
    }

    // Pure calculation, nothing persisted (DOMAIN_MODEL.md §14) — the DualPay screen calls this.
    [HttpPost("dualpay-preview")]
    public IActionResult DualPayPreview([FromBody] DualPayRequest body) =>
        Ok(PlanningService.CalculateDualPay(body.GrossNet, body.UsdRateOfficial, body.UsdRateCcl));
}

public record BudgetRequest(decimal? CreditLimit, decimal? DebitCashLimit, decimal? WeeklyLimit);
public record IncomeRequest(decimal? NetMonthly, decimal? UsdRateOfficial, decimal? UsdRateCcl, int? SplitPercent);
public record CopyMonthRequest(string FromMonth, string ToMonth);
public record DualPayRequest(decimal GrossNet, decimal UsdRateOfficial, decimal UsdRateCcl);
