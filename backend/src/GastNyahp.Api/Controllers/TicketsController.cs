using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(TicketService tickets) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByMonth([FromQuery] string month, CancellationToken ct)
    {
        if (!ApiConventions.IsValidMonth(month)) return BadRequest("Query parameter 'month' must be yyyy-MM.");
        return Ok(await tickets.GetByMonthAsync(HttpContext.GetFamilyId(), month, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] TicketRequest body, CancellationToken ct)
    {
        var (error, paymentMethod, items) = Parse(body);
        if (error is not null) return BadRequest(error);

        var result = await tickets.RegisterAsync(HttpContext.GetFamilyId(), body.Date, body.Description, paymentMethod!, body.Discount, items!, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TicketRequest body, CancellationToken ct)
    {
        var (error, paymentMethod, items) = Parse(body);
        if (error is not null) return BadRequest(error);

        var result = await tickets.UpdateAsync(HttpContext.GetFamilyId(), id, body.Date, body.Description, paymentMethod!, body.Discount, items!, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await tickets.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();

    static (string? Error, PaymentMethod? PaymentMethod, IReadOnlyList<TicketItemInput>? Items) Parse(TicketRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Description)) return ("Description is required.", null, null);
        if (!ApiConventions.IsValidDate(body.Date)) return ("Date must be yyyy-MM-dd.", null, null);
        if (body.Items is not { Count: > 0 }) return ("At least one item is required.", null, null);
        try
        {
            var paymentMethod = PaymentMethod.FromPrimitive(body.PaymentMethodKind, body.PaymentMethodReferenceId);
            var items = body.Items
                .Select(i => new TicketItemInput(Guid.NewGuid(), i.Description, i.Amount, i.Category, i.OwnerKind ?? "Unassigned", i.OwnerPersonId))
                .ToList();
            return (null, paymentMethod, items);
        }
        catch (DomainException ex)
        {
            return (ex.Message, null, null);
        }
    }
}

public record TicketRequest(
    string Date, string Description, string PaymentMethodKind, Guid? PaymentMethodReferenceId,
    decimal Discount, List<TicketItemRequest> Items);

public record TicketItemRequest(string Description, decimal Amount, string Category, string? OwnerKind, Guid? OwnerPersonId);
