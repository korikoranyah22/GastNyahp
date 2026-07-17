using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/loans")]
public sealed class LoansController(LoanService loans) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await loans.GetAllAsync(HttpContext.GetFamilyId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var loan = await loans.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return loan is null ? NotFound() : Ok(loan);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterLoanRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Description)) return BadRequest("Description is required.");
        if (!ApiConventions.IsValidMonth(body.StartMonth)) return BadRequest("StartMonth must be yyyy-MM.");
        var result = await loans.RegisterAsync(HttpContext.GetFamilyId(), body.BankId, body.Description, body.TotalAmount, body.MonthlyInstallment, body.StartMonth, body.TotalInstallments, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDetails(Guid id, [FromBody] UpdateLoanDetailsRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Description)) return BadRequest("Description is required.");
        return (await loans.UpdateDetailsAsync(HttpContext.GetFamilyId(), id, body.Description, body.TotalAmount, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<IActionResult> Revise(Guid id, [FromBody] ReviseLoanRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(body.StartMonth)) return BadRequest("StartMonth must be yyyy-MM.");
        return (await loans.ReviseScheduleAsync(HttpContext.GetFamilyId(), id, body.StartMonth, body.TotalInstallments, body.MonthlyInstallment, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/months/{month}/toggle-paid")]
    public async Task<IActionResult> ToggleMonthPaid(Guid id, string month, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await loans.ToggleMonthPaidAsync(HttpContext.GetFamilyId(), id, month, ct)).ToActionResult();
    }

    [HttpPut("{id:guid}/months/{month}/amount")]
    public async Task<IActionResult> OverrideMonthAmount(Guid id, string month, [FromBody] AmountRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await loans.OverrideMonthAmountAsync(HttpContext.GetFamilyId(), id, month, body.Amount, ct)).ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await loans.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();
}

public record RegisterLoanRequest(Guid BankId, string Description, decimal? TotalAmount, decimal MonthlyInstallment, string StartMonth, int TotalInstallments);
public record ReviseLoanRequest(string StartMonth, int TotalInstallments, decimal MonthlyInstallment);
public record UpdateLoanDetailsRequest(string Description, decimal? TotalAmount);
