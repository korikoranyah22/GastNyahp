using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Installments;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/installments")]
public sealed class InstallmentsController(InstallmentService installments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? cardId, CancellationToken ct) =>
        Ok(await installments.GetAllAsync(HttpContext.GetFamilyId(), cardId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var installment = await installments.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return installment is null ? NotFound() : Ok(installment);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterInstallmentRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Description)) return BadRequest("Description is required.");
        if (!ApiConventions.IsValidDate(body.PurchaseDate)) return BadRequest("PurchaseDate must be yyyy-MM-dd.");
        if (!ApiConventions.IsValidMonth(body.StartMonth)) return BadRequest("StartMonth must be yyyy-MM.");

        OwnerRef owner;
        try { owner = OwnerRef.FromPrimitive(body.OwnerKind ?? "Unassigned", body.OwnerPersonId); }
        catch (DomainException ex) { return BadRequest(ex.Message); }

        var result = await installments.RegisterAsync(
            HttpContext.GetFamilyId(), body.CardId, body.Description, body.Category, body.PurchaseDate, body.Frequency,
            body.MonthlyAmount, body.TotalInstallments, body.StartMonth, owner, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDetails(Guid id, [FromBody] UpdateInstallmentDetailsRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Description)) return BadRequest("Description is required.");
        if (!ApiConventions.IsValidDate(body.PurchaseDate)) return BadRequest("PurchaseDate must be yyyy-MM-dd.");

        OwnerRef owner;
        try { owner = OwnerRef.FromPrimitive(body.OwnerKind ?? "Unassigned", body.OwnerPersonId); }
        catch (DomainException ex) { return BadRequest(ex.Message); }

        return (await installments.UpdateDetailsAsync(HttpContext.GetFamilyId(), id, body.Description, body.Category, body.PurchaseDate, owner, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<IActionResult> Revise(Guid id, [FromBody] ReviseInstallmentRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(body.StartMonth)) return BadRequest("StartMonth must be yyyy-MM.");
        var result = await installments.ReviseScheduleAsync(HttpContext.GetFamilyId(), id, body.StartMonth, body.TotalInstallments, body.Frequency, body.MonthlyAmount, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/months/{month}/toggle-paid")]
    public async Task<IActionResult> ToggleMonthPaid(Guid id, string month, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await installments.ToggleMonthPaidAsync(HttpContext.GetFamilyId(), id, month, ct)).ToActionResult();
    }

    [HttpPut("{id:guid}/months/{month}/amount")]
    public async Task<IActionResult> OverrideMonthAmount(Guid id, string month, [FromBody] AmountRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await installments.OverrideMonthAmountAsync(HttpContext.GetFamilyId(), id, month, body.Amount, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> Finish(Guid id, CancellationToken ct) =>
        (await installments.FinishAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await installments.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();
}

public record RegisterInstallmentRequest(
    Guid CardId, string Description, string Category, string PurchaseDate, InstallmentFrequency Frequency,
    decimal MonthlyAmount, int? TotalInstallments, string StartMonth, string? OwnerKind, Guid? OwnerPersonId);

public record ReviseInstallmentRequest(string StartMonth, int? TotalInstallments, InstallmentFrequency Frequency, decimal MonthlyAmount);
public record UpdateInstallmentDetailsRequest(string Description, string Category, string PurchaseDate, string? OwnerKind, Guid? OwnerPersonId);

public record AmountRequest(decimal Amount);
