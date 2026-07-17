using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class TicketService(
    TicketCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<TicketService> logger)
{
    public async Task<List<TicketEntity>> GetByMonthAsync(Guid familyId, string month, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Tickets.Include(t => t.Items).Where(t => t.FamilyId == familyId && t.Date.StartsWith(month)).OrderBy(t => t.Date).ToListAsync(ct);
    }

    public async Task<TicketEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Tickets.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id && t.FamilyId == familyId, ct);
    }

    public Task<OpResult> RegisterAsync(
        Guid familyId, string date, string description, PaymentMethod paymentMethod, decimal discount,
        IReadOnlyList<TicketItemInput> items, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        return CommandExecutor.Exec(
            commands.Handle(new RegisterTicket(id, familyId, date, description, paymentMethod, discount, items), ct),
            sync, logger, "RegisterTicket", id, ct);
    }

    public async Task<OpResult> UpdateAsync(
        Guid familyId, Guid id, string date, string description, PaymentMethod paymentMethod, decimal discount,
        IReadOnlyList<TicketItemInput> items, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El ticket no existe.");
        return await CommandExecutor.Exec(
            commands.Handle(new UpdateTicket(id, date, description, paymentMethod, discount, items), ct),
            sync, logger, "UpdateTicket", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El ticket no existe.");
        return await CommandExecutor.Exec(commands.Handle(new RemoveTicket(id), ct), sync, logger, "RemoveTicket", id, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Tickets.AnyAsync(t => t.Id == id && t.FamilyId == familyId, ct);
    }
}
