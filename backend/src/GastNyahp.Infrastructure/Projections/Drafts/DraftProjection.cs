using System.Text.Json;
using GastNyahp.Domain.Drafts;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Drafts;

public class DraftProjection : GastNyahpProjection
{
    const string Prefix = "draft";
    static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public DraftProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<DraftEvents.V1.DraftCreated>(ctx => new ValueTask(HandleCreated(ctx.Message, ctx.CancellationToken)));
        On<DraftEvents.V1.DraftUpdated>(ctx => new ValueTask(HandleUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<DraftEvents.V1.DraftKindChanged>(ctx => new ValueTask(HandleKindChanged(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<DraftEvents.V1.DraftConfirmed>(ctx => new ValueTask(HandleConfirmed(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<DraftEvents.V1.DraftDiscarded>(ctx => new ValueTask(HandleDiscarded(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public static string Serialize(DraftPayload payload) => JsonSerializer.Serialize(payload, PayloadJson);
    public static DraftPayload Deserialize(string json) => JsonSerializer.Deserialize<DraftPayload>(json, PayloadJson) ?? new DraftPayload();

    public async Task HandleCreated(DraftEvents.V1.DraftCreated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Drafts.AnyAsync(d => d.Id == e.DraftId, ct)) return;

        db.Drafts.Add(new DraftEntity
        {
            Id = e.DraftId, FamilyId = e.FamilyId, Kind = e.Kind.ToString(), Status = DraftStatus.Open.ToString(),
            PayloadJson = Serialize(e.Payload), CreatedByKind = e.CreatedByKind, CreatedById = e.CreatedById,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleUpdated(Guid draftId, DraftEvents.V1.DraftUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (entity is null) return;

        entity.PayloadJson = Serialize(e.Payload);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleKindChanged(Guid draftId, DraftEvents.V1.DraftKindChanged e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (entity is null) return;

        // El Kind del read-model es lo que lee ConfirmAsync para enrutar el registro (gasto/ticket/cuotas).
        entity.Kind = e.Kind.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleConfirmed(Guid draftId, DraftEvents.V1.DraftConfirmed e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (entity is null) return;

        entity.Status = DraftStatus.Confirmed.ToString();
        entity.ResultEntityId = e.ResultEntityId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleDiscarded(Guid draftId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (entity is null) return;

        entity.Status = DraftStatus.Discarded.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
