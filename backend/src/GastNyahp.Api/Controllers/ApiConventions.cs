using System.Text.RegularExpressions;
using GastNyahp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

/// <summary>Shared HTTP mapping: OpResult.Ok → 200 (with { id } when a resource was created), domain
/// rejection → 422 with the domain message as plain text (see aspnet-rest-endpoint).</summary>
public static partial class ApiConventions
{
    public static IActionResult ToActionResult(this OpResult result) =>
        result.Ok
            ? result.Id is { } id ? new OkObjectResult(new IdResponse(id)) : new OkResult()
            : new UnprocessableEntityObjectResult(result.Error);

    public static bool IsValidMonth(string? month) => month is not null && MonthRegex().IsMatch(month);
    public static bool IsValidDate(string? date) => date is not null && DateRegex().IsMatch(date);

    [GeneratedRegex(@"^\d{4}-\d{2}$")]
    private static partial Regex MonthRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateRegex();
}

public record IdResponse(Guid Id);
