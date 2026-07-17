using System.Text.Json.Serialization;
using GastNyahp.Api.Auth;
using GastNyahp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as their names ("Visa", "Fixed", "Usd") — self-describing payloads for the frontend and MCP.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddGastNyahpInfrastructure(builder.Configuration);
builder.Services.AddSingleton<OAuthFlowStore>();

// MCP lives IN this host (not a separate process as the generic skill suggests) because the InMemory event
// store isn't shareable across processes; with the Postgres event store it can be extracted later. Tools read
// the family from HttpContext, set by FamilyAuthMiddleware — agents authenticate with a family agent key.
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "gastnyahp", Version = "1.0.0" })
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(GastNyahp.Api.Mcp.GastNyahpTools).Assembly);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Liveness probe for the docker-compose healthcheck — deliberately does NOT touch the database
// (see docker-compose-service-network): the process being up is what this endpoint certifies.
app.MapGet("/health/live", () => Results.Json(new { status = "ok" }));

// Possession-based auth: Bearer member-token → family. Everything except health/openapi/admin-gate/family
// entry points requires a credential (DOMAIN_MODEL.md §17).
app.UseMiddleware<FamilyAuthMiddleware>();

app.MapControllers();
app.MapMcp("/mcp"); // requires a bearer credential like any data route — /mcp is not in the anonymous allowlist

app.Run();

// Exposes the implicit Program class to WebApplicationFactory<Program> (E2E tests).
public partial class Program;
