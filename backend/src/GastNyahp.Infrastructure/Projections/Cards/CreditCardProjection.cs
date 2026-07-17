using GastNyahp.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Cards;

public class CreditCardProjection : GastNyahpProjection
{
    const string Prefix = "card";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public CreditCardProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<CreditCardEvents.V1.CardRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<CreditCardEvents.V1.CardUpdated>(ctx => new ValueTask(HandleUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<CreditCardEvents.V1.CardActivated>(ctx => new ValueTask(HandleActiveChanged(StreamIds.GuidFrom(ctx.Stream, Prefix), active: true, ctx.CancellationToken)));
        On<CreditCardEvents.V1.CardDeactivated>(ctx => new ValueTask(HandleActiveChanged(StreamIds.GuidFrom(ctx.Stream, Prefix), active: false, ctx.CancellationToken)));
        On<CreditCardEvents.V1.CardRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(CreditCardEvents.V1.CardRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.CreditCards.AnyAsync(c => c.Id == e.CardId, ct)) return;

        db.CreditCards.Add(new CreditCardEntity
        {
            Id = e.CardId, FamilyId = e.FamilyId, BankId = e.BankId, Label = e.Label, Network = e.Network.ToString(), Type = e.Type.ToString(),
            ClosingDay = e.ClosingDay, DueDay = e.DueDay, Color = e.Color, Active = true, UpdatedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleUpdated(Guid cardId, CreditCardEvents.V1.CardUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (entity is null) return;

        entity.Label = e.Label;
        entity.Network = e.Network.ToString();
        entity.Type = e.Type.ToString();
        entity.ClosingDay = e.ClosingDay;
        entity.DueDay = e.DueDay;
        entity.Color = e.Color;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleActiveChanged(Guid cardId, bool active, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (entity is null) return;

        entity.Active = active;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleRemoved(Guid cardId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.CreditCards.Where(c => c.Id == cardId).ExecuteDeleteAsync(ct);
    }
}
