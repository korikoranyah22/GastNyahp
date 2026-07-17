using GastNyahp.Domain.Loans;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class LoanService(
    LoanCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<LoanService> logger)
{
    public async Task<List<LoanEntity>> GetAllAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Loans.Include(l => l.Months).Where(l => l.FamilyId == familyId).OrderBy(l => l.Description).ToListAsync(ct);
    }

    public async Task<LoanEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Loans.Include(l => l.Months).FirstOrDefaultAsync(l => l.Id == id && l.FamilyId == familyId, ct);
    }

    public async Task<OpResult> RegisterAsync(
        Guid familyId, Guid bankId, string description, decimal? totalAmount, decimal monthlyInstallment, string startMonth, int totalInstallments, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (!await db.Banks.AnyAsync(b => b.Id == bankId && b.FamilyId == familyId, ct))
                return OpResult.Fail("El banco no existe.");
        }

        var id = Guid.NewGuid();
        return await CommandExecutor.Exec(
            commands.Handle(new RegisterLoan(id, familyId, bankId, description, totalAmount, monthlyInstallment, startMonth, totalInstallments), ct),
            sync, logger, "RegisterLoan", id, ct);
    }

    public async Task<OpResult> UpdateDetailsAsync(Guid familyId, Guid id, string description, decimal? totalAmount, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El préstamo no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdateLoanDetails(id, description, totalAmount), ct), sync, logger, "UpdateLoanDetails", id, ct);
    }

    public async Task<OpResult> ReviseScheduleAsync(Guid familyId, Guid id, string startMonth, int totalInstallments, decimal monthlyInstallment, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El préstamo no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ReviseLoanSchedule(id, startMonth, totalInstallments, monthlyInstallment), ct), sync, logger, "ReviseLoanSchedule", id, ct);
    }

    public async Task<OpResult> OverrideMonthAmountAsync(Guid familyId, Guid id, string month, decimal amount, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El préstamo no existe.");
        return await CommandExecutor.Exec(commands.Handle(new OverrideLoanMonthAmount(id, month, amount), ct), sync, logger, "OverrideLoanMonthAmount", id, ct);
    }

    public async Task<OpResult> ToggleMonthPaidAsync(Guid familyId, Guid id, string month, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El préstamo no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ToggleLoanMonthPaid(id, month), ct), sync, logger, "ToggleLoanMonthPaid", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El préstamo no existe.");
        return await CommandExecutor.Exec(commands.Handle(new RemoveLoan(id), ct), sync, logger, "RemoveLoan", id, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Loans.AnyAsync(l => l.Id == id && l.FamilyId == familyId, ct);
    }
}
