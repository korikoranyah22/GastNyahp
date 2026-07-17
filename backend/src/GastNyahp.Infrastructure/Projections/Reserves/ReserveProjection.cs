using GastNyahp.Domain.Reserves;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Reserves;

public class ReserveProjection : GastNyahpProjection
{
    const string Prefix = "reserve";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public ReserveProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<ReserveEvents.V1.ReserveRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<ReserveEvents.V1.ReserveDetailsUpdated>(ctx => new ValueTask(HandleDetailsUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ReserveEvents.V1.ReserveMonthAmountSet>(ctx => new ValueTask(HandleMonthAmountSet(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ReserveEvents.V1.ReserveBaseAmountApplied>(ctx => new ValueTask(HandleBaseAmountApplied(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ReserveEvents.V1.ReserveRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(ReserveEvents.V1.ReserveRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Reserves.AnyAsync(r => r.Id == e.ReserveId, ct)) return;

        db.Reserves.Add(new ReserveEntity
        {
            Id = e.ReserveId, FamilyId = e.FamilyId, Label = e.Label, Type = e.Type.ToString(), Icon = e.Icon,
            Recurring = e.Recurring, BaseAmount = e.BaseAmount, UpdatedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleDetailsUpdated(Guid reserveId, ReserveEvents.V1.ReserveDetailsUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Reserves.FirstOrDefaultAsync(r => r.Id == reserveId, ct);
        if (entity is null) return;

        entity.Label = e.Label;
        entity.Type = e.Type.ToString();
        entity.Icon = e.Icon;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleMonthAmountSet(Guid reserveId, ReserveEvents.V1.ReserveMonthAmountSet e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (!await db.Reserves.AnyAsync(r => r.Id == reserveId, ct)) return;

        var existing = await db.ReserveMonthOverrides.FirstOrDefaultAsync(m => m.ReserveId == reserveId && m.Month == e.Month, ct);
        if (existing is null)
        {
            db.ReserveMonthOverrides.Add(new ReserveMonthOverrideEntity
            {
                ReserveId = reserveId, Month = e.Month, Amount = e.Amount, Note = e.Note, UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Amount = e.Amount;
            existing.Note = e.Note;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleBaseAmountApplied(Guid reserveId, ReserveEvents.V1.ReserveBaseAmountApplied e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Reserves.FirstOrDefaultAsync(r => r.Id == reserveId, ct);
        if (entity is null) return;

        entity.BaseAmount = e.BaseAmount;
        entity.Recurring = true;
        entity.UpdatedAt = DateTime.UtcNow;
        // Destructive by design: applying a base clears every per-month override (DOMAIN_MODEL.md §7).
        await db.SaveChangesAsync(ct);
        await db.ReserveMonthOverrides.Where(m => m.ReserveId == reserveId).ExecuteDeleteAsync(ct);
    }

    public async Task HandleRemoved(Guid reserveId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Reserves.Where(r => r.Id == reserveId).ExecuteDeleteAsync(ct);
    }
}
