using GastNyahp.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.People;

public class PersonProjection : GastNyahpProjection
{
    const string Prefix = "person";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public PersonProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<PersonEvents.V1.PersonRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<PersonEvents.V1.PersonUpdated>(ctx => new ValueTask(HandleUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<PersonEvents.V1.PersonArchived>(ctx => new ValueTask(HandleArchived(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(PersonEvents.V1.PersonRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.People.AnyAsync(p => p.Id == e.PersonId, ct)) return;

        db.People.Add(new PersonEntity
        {
            Id = e.PersonId, FamilyId = e.FamilyId, Name = e.Name, Emoji = e.Emoji, Color = e.Color, Archived = false, UpdatedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleUpdated(Guid personId, PersonEvents.V1.PersonUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.People.FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (entity is null) return;

        entity.Name = e.Name;
        entity.Emoji = e.Emoji;
        entity.Color = e.Color;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // Archived people keep their row (Archived=true) so historical OwnerRef references still resolve —
    // DOMAIN_MODEL.md §8. This is why there's no delete handler here at all.
    public async Task HandleArchived(Guid personId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.People.FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (entity is null) return;

        entity.Archived = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
