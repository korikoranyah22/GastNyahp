using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/people")]
public sealed class PeopleController(PersonService people) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await people.GetAllAsync(HttpContext.GetFamilyId(), includeArchived, ct));

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] PersonRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required.");
        return (await people.RegisterAsync(HttpContext.GetFamilyId(), body.Name, body.Emoji ?? "😀", body.Color ?? "#64748b", ct)).ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PersonRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required.");
        return (await people.UpdateAsync(HttpContext.GetFamilyId(), id, body.Name, body.Emoji ?? "😀", body.Color ?? "#64748b", ct)).ToActionResult();
    }

    // Archive, not delete: historical OwnerRef references must keep resolving (DOMAIN_MODEL.md §8).
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        (await people.ArchiveAsync(HttpContext.GetFamilyId(), id, ct)).ToActionResult();
}

public record PersonRequest(string Name, string? Emoji, string? Color);
