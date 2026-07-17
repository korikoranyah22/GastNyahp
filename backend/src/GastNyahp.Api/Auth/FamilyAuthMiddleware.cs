using GastNyahp.Infrastructure.Services;

namespace GastNyahp.Api.Auth;

/// <summary>
/// Possession-based auth (DOMAIN_MODEL.md Â§17): a valid member token IS the identity â€” no users, no emails,
/// no sessions. Anonymous surface is deliberately tiny: health/openapi, the admin gate (which carries its own
/// X-Admin-Key check), and the two family entry points that carry their own single-use codes.
/// </summary>
public sealed class FamilyAuthMiddleware(RequestDelegate next)
{
    public const string FamilyIdItem = "GastNyahp.FamilyId";
    public const string MemberIdItem = "GastNyahp.MemberId";
    public const string MemberRoleItem = "GastNyahp.MemberRole";

    public async Task InvokeAsync(HttpContext ctx, FamilyService families)
    {
        if (IsAnonymousAllowed(ctx.Request))
        {
            await next(ctx);
            return;
        }

        var header = ctx.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..].Trim() : null;
        // Members and agent keys share the same lookup â€” an MCP agent authenticates exactly like a person,
        // just with a revocable data-only credential (role "Agent").
        var credential = token is null ? null : await families.ResolveCredentialAsync(token, ctx.RequestAborted);
        if (credential is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            if (ctx.Request.Path.StartsWithSegments("/mcp"))
            {
                var issuer = ctx.RequestServices.GetRequiredService<IConfiguration>()["OAuth:Issuer"]?.TrimEnd('/')
                    ?? $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                ctx.Response.Headers.WWWAuthenticate = string.Concat("Bearer resource_metadata=", (char)34, issuer, "/.well-known/oauth-protected-resource/mcp", (char)34);
            }
            await ctx.Response.WriteAsJsonAsync(new { error = "Credencial requerida: unite a una familia o creÃ¡ una con un cÃ³digo de administrador." });
            return;
        }

        ctx.Items[FamilyIdItem] = credential.FamilyId;
        ctx.Items[MemberIdItem] = credential.PrincipalId;
        ctx.Items[MemberRoleItem] = credential.Role;
        await next(ctx);
    }

    static bool IsAnonymousAllowed(HttpRequest request)
    {
        var path = request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/openapi") ||
            path.StartsWithSegments("/.well-known") || path.StartsWithSegments("/oauth")) return true;
        if (path.StartsWithSegments("/api/admin")) return true;
        if (HttpMethods.IsGet(request.Method) && path.StartsWithSegments("/api/skill-packages")) return true; // guarded by X-Admin-Key inside the controller
        if (HttpMethods.IsPost(request.Method) && (path == "/api/families" || path == "/api/families/join")) return true;
        // Login y reseteo son anÃ³nimos por definiciÃ³n: el que llega acÃ¡ NO tiene credencial â€” es justamente lo
        // que viene a conseguir. La autorizaciÃ³n son el email+contraseÃ±a / el cÃ³digo de un uso, verificados
        // adentro (docs/DISENO_CUENTAS_LOGIN.md Â§5).
        if (HttpMethods.IsPost(request.Method) &&
            (path == "/api/families/login" || path == "/api/families/password-resets/redeem")) return true;
        return false;
    }
}

public static class HttpContextFamilyExtensions
{
    public static Guid GetFamilyId(this HttpContext ctx) => (Guid)ctx.Items[FamilyAuthMiddleware.FamilyIdItem]!;
    public static Guid GetMemberId(this HttpContext ctx) => (Guid)ctx.Items[FamilyAuthMiddleware.MemberIdItem]!;
    public static bool IsFamilyAdmin(this HttpContext ctx) => (string?)ctx.Items[FamilyAuthMiddleware.MemberRoleItem] == "Admin";
    public static bool IsAgent(this HttpContext ctx) => (string?)ctx.Items[FamilyAuthMiddleware.MemberRoleItem] == "Agent";
}
