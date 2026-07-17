using GastNyahp.Domain.Expenses;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Expenses;

public class TicketProjection : GastNyahpProjection
{
    const string Prefix = "ticket";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public TicketProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<TicketEvents.V1.TicketRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<TicketEvents.V1.TicketUpdated>(ctx => new ValueTask(HandleUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<TicketEvents.V1.TicketRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(TicketEvents.V1.TicketRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Tickets.AnyAsync(t => t.Id == e.TicketId, ct)) return;

        db.Tickets.Add(new TicketEntity
        {
            Id = e.TicketId, FamilyId = e.FamilyId, Date = e.Date, Description = e.Description,
            PaymentMethodKind = e.PaymentMethodKind, PaymentMethodReferenceId = e.PaymentMethodReferenceId,
            Discount = e.Discount, Total = ComputeTotal(e.Items, e.Discount), UpdatedAt = DateTime.UtcNow,
            Items = e.Items.Select(i => new TicketItemEntity
            {
                TicketId = e.TicketId, ItemId = i.ItemId, Description = i.Description, Amount = i.Amount,
                Category = i.Category, OwnerKind = i.OwnerKind, OwnerPersonId = i.OwnerPersonId,
            }).ToList(),
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleUpdated(Guid ticketId, TicketEvents.V1.TicketUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Tickets.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (entity is null) return;

        entity.Date = e.Date;
        entity.Description = e.Description;
        entity.PaymentMethodKind = e.PaymentMethodKind;
        entity.PaymentMethodReferenceId = e.PaymentMethodReferenceId;
        entity.Discount = e.Discount;
        entity.Total = ComputeTotal(e.Items, e.Discount);
        entity.UpdatedAt = DateTime.UtcNow;

        // The UpdateTicket command replaces the whole item set (DOMAIN_MODEL.md §10) — mirror that here.
        entity.Items.Clear();
        foreach (var i in e.Items)
        {
            entity.Items.Add(new TicketItemEntity
            {
                TicketId = ticketId, ItemId = i.ItemId, Description = i.Description, Amount = i.Amount,
                Category = i.Category, OwnerKind = i.OwnerKind, OwnerPersonId = i.OwnerPersonId,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleRemoved(Guid ticketId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Tickets.Where(t => t.Id == ticketId).ExecuteDeleteAsync(ct);
    }

    // Ported 1:1 from getExpenseAmount() for tickets — never negative (DOMAIN_MODEL.md §10).
    static decimal ComputeTotal(IReadOnlyList<TicketItemInput> items, decimal discount) =>
        Math.Max(0, items.Sum(i => i.Amount) - discount);
}
