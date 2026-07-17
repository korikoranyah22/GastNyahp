using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController(ExpenseService expenses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByMonth([FromQuery] string month, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Query parameter 'month' must be yyyy-MM.");
        return Ok(await expenses.GetByMonthAsync(HttpContext.GetFamilyId(), month, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var expense = await expenses.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return expense is null ? NotFound() : Ok(expense);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] ExpenseRequest body, CancellationToken ct)
    {
        var (error, paymentMethod, owner) = Parse(body);
        if (error is not null) return BadRequest(error);

        var result = await expenses.RegisterAsync(HttpContext.GetFamilyId(), body.Date, body.Description, body.Category, body.Amount, body.Currency, paymentMethod!, owner!, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExpenseRequest body, CancellationToken ct)
    {
        var (error, paymentMethod, owner) = Parse(body);
        if (error is not null) return BadRequest(error);

        var result = await expenses.UpdateAsync(HttpContext.GetFamilyId(), id, body.Date, body.Description, body.Category, body.Amount, body.Currency, paymentMethod!, owner!, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await expenses.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    static (string? Error, PaymentMethod? PaymentMethod, OwnerRef? Owner) Parse(ExpenseRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Description)) return ("Description is required.", null, null);
        if (!ApiConventions.IsValidDate(body.Date)) return ("Date must be yyyy-MM-dd.", null, null);
        try
        {
            return (null,
                PaymentMethod.FromPrimitive(body.PaymentMethodKind, body.PaymentMethodReferenceId),
                OwnerRef.FromPrimitive(body.OwnerKind ?? "Unassigned", body.OwnerPersonId));
        }
        catch (DomainException ex)
        {
            return (ex.Message, null, null);
        }
    }
}

public record ExpenseRequest(
    string Date, string Description, string Category, decimal Amount, ExpenseCurrency Currency,
    string PaymentMethodKind, Guid? PaymentMethodReferenceId, string? OwnerKind, Guid? OwnerPersonId);
