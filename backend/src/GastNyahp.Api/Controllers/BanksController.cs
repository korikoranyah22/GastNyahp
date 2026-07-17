using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/banks")]
public sealed class BanksController(BankService banks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await banks.GetAllAsync(HttpContext.GetFamilyId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var bank = await banks.GetByIdAsync(HttpContext.GetFamilyId(), id, ct);
        return bank is null ? NotFound() : Ok(bank);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterBankRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required.");
        var result = await banks.RegisterAsync(HttpContext.GetFamilyId(), body.Name, body.Alias, body.Color ?? "#004B9B", body.Icon ?? "building-2", ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RegisterBankRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required.");
        var result = await banks.UpdateAsync(HttpContext.GetFamilyId(), id, body.Name, body.Alias, body.Color ?? "#004B9B", body.Icon ?? "building-2", ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        (await banks.RemoveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();
}

public record RegisterBankRequest(string Name, string? Alias, string? Color, string? Icon);
