using GastNyahp.Domain.Reserves;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Reserves;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class ReserveService(
    ReserveCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<ReserveService> logger)
{
    public async Task<List<ReserveEntity>> GetAllAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Reserves.Include(r => r.Months).Where(r => r.FamilyId == familyId).OrderBy(r => r.Label).ToListAsync(ct);
    }

    public async Task<ReserveEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Reserves.Include(r => r.Months).FirstOrDefaultAsync(r => r.Id == id && r.FamilyId == familyId, ct);
    }

    public Task<OpResult> RegisterAsync(Guid familyId, string label, ReserveType type, string icon, bool recurring, decimal baseAmount, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        return CommandExecutor.Exec(commands.Handle(new RegisterReserve(id, familyId, label, type, icon, recurring, baseAmount), ct), sync, logger, "RegisterReserve", id, ct);
    }

    public async Task<OpResult> UpdateDetailsAsync(Guid familyId, Guid id, string label, ReserveType type, string icon, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La reserva no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdateReserveDetails(id, label, type, icon), ct), sync, logger, "UpdateReserveDetails", id, ct);
    }

    public async Task<OpResult> SetMonthAmountAsync(Guid familyId, Guid id, string month, decimal amount, string? note, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La reserva no existe.");
        return await CommandExecutor.Exec(commands.Handle(new SetReserveMonthAmount(id, month, amount, note), ct), sync, logger, "SetReserveMonthAmount", id, ct);
    }

    public async Task<OpResult> ApplyBaseToAllMonthsAsync(Guid familyId, Guid id, decimal baseAmount, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La reserva no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ApplyReserveBaseToAllMonths(id, baseAmount), ct), sync, logger, "ApplyReserveBaseToAllMonths", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La reserva no existe.");
        return await CommandExecutor.Exec(commands.Handle(new RemoveReserve(id), ct), sync, logger, "RemoveReserve", id, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Reserves.AnyAsync(r => r.Id == id && r.FamilyId == familyId, ct);
    }
}
