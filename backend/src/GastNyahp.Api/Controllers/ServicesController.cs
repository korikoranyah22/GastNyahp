using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Services;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/services")]
public sealed class ServicesController(ServicesService services) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await services.GetAllAsync(HttpContext.GetFamilyId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var service = await services.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return service is null ? NotFound() : Ok(service);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterServiceRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required.");
        if (!ApiConventions.IsValidMonth(body.RegisteredFromMonth)) return BadRequest("RegisteredFromMonth must be yyyy-MM.");

        OwnerRef owner;
        try { owner = OwnerRef.FromPrimitive(body.OwnerKind ?? "Unassigned", body.OwnerPersonId); }
        catch (DomainException ex) { return BadRequest(ex.Message); }

        var result = await services.RegisterAsync(
            HttpContext.GetFamilyId(), body.Name, body.Category, body.BillingType, body.LinkedCardId, body.Currency, body.BaseAmount, body.RegisteredFromMonth, owner, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDetails(Guid id, [FromBody] UpdateServiceRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required.");
        OwnerRef? owner = null;
        try
        {
            if (body.OwnerKind is not null) owner = OwnerRef.FromPrimitive(body.OwnerKind, body.OwnerPersonId);
        }
        catch (DomainException ex) { return BadRequest(ex.Message); }

        return (await services.UpdateDetailsAsync(
            HttpContext.GetFamilyId(), id, body.Name, body.Category, body.BillingType, body.LinkedCardId, body.Currency, owner, ct)).ToActionResult();
    }

    [HttpPut("{id:guid}/months/{month}/amount")]
    public async Task<IActionResult> SetMonthAmount(Guid id, string month, [FromBody] ServiceAmountRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await services.SetMonthAmountAsync(HttpContext.GetFamilyId(), id, month, body.Amount, body.Currency, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/extend-future")]
    public async Task<IActionResult> ExtendFuture(Guid id, [FromBody] ExtendFutureRequest body, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(body.FromMonth)) return BadRequest("FromMonth must be yyyy-MM.");
        return (await services.ExtendFutureAmountsAsync(HttpContext.GetFamilyId(), id, body.FromMonth, body.AmountArs, body.MonthsAhead ?? 12, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/months/{month}/toggle-paid")]
    public async Task<IActionResult> ToggleMonthPaid(Guid id, string month, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Month must be yyyy-MM.");
        return (await services.ToggleMonthPaidAsync(HttpContext.GetFamilyId(), id, month, ct)).ToActionResult();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        (await services.ActivateAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        (await services.DeactivateAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await services.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();
}

public record RegisterServiceRequest(
    string Name, string Category, BillingType BillingType, Guid? LinkedCardId, ServiceCurrency Currency,
    decimal BaseAmount, string RegisteredFromMonth, string? OwnerKind, Guid? OwnerPersonId);

public record UpdateServiceRequest(string Name, string Category, BillingType BillingType, Guid? LinkedCardId, ServiceCurrency Currency, string? OwnerKind, Guid? OwnerPersonId);
public record ServiceAmountRequest(decimal Amount, ServiceCurrency Currency);
public record ExtendFutureRequest(string FromMonth, decimal AmountArs, int? MonthsAhead);
