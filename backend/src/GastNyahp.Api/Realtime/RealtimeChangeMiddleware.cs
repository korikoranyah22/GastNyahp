using GastNyahp.Api.Auth;
using Microsoft.AspNetCore.SignalR;

namespace GastNyahp.Api.Realtime;

/// <summary>Publica una invalidación luego de una escritura exitosa, incluida una escritura MCP.</summary>
public sealed class RealtimeChangeMiddleware(RequestDelegate next)
{
    static readonly HashSet<string> ReadMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get, HttpMethods.Head, HttpMethods.Options
    };

    public async Task InvokeAsync(HttpContext context, IHubContext<FamilyUpdatesHub> hub)
    {
        await next(context);

        if (ReadMethods.Contains(context.Request.Method) ||
            context.Request.Path.StartsWithSegments("/hubs/updates") ||
            context.Response.StatusCode is < 200 or >= 400 ||
            context.Items[FamilyAuthMiddleware.FamilyIdItem] is not Guid familyId)
            return;

        await hub.Clients.Group(FamilyUpdatesHub.Group(familyId)).SendAsync(
            "dataChanged",
            new { path = context.Request.Path.Value, occurredAt = DateTimeOffset.UtcNow },
            context.RequestAborted);
    }
}
