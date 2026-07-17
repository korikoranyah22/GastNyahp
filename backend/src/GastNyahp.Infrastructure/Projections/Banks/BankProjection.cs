using GastNyahp.Domain.Banks;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Banks;

public class BankProjection : GastNyahpProjection
{
    const string Prefix = "bank";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public BankProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<BankEvents.V1.BankRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<BankEvents.V1.BankUpdated>(ctx => new ValueTask(HandleUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<BankEvents.V1.BankRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(BankEvents.V1.BankRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Banks.AnyAsync(b => b.Id == e.BankId, ct)) return;

        db.Banks.Add(new BankEntity
        {
            Id = e.BankId, FamilyId = e.FamilyId, Name = e.Name, Alias = e.Alias, Color = e.Color, Icon = e.Icon, UpdatedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleUpdated(Guid bankId, BankEvents.V1.BankUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Banks.FirstOrDefaultAsync(b => b.Id == bankId, ct);
        if (entity is null) return;

        entity.Name = e.Name;
        entity.Alias = e.Alias;
        entity.Color = e.Color;
        entity.Icon = e.Icon;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleRemoved(Guid bankId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Banks.Where(b => b.Id == bankId).ExecuteDeleteAsync(ct);
    }
}
