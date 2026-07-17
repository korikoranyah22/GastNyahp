using GastNyahp.Domain.Expenses;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Expenses;

public class ExpenseProjection : GastNyahpProjection
{
    const string Prefix = "expense";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public ExpenseProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<ExpenseEvents.V1.ExpenseRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<ExpenseEvents.V1.ExpenseUpdated>(ctx => new ValueTask(HandleUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ExpenseEvents.V1.ExpenseRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(ExpenseEvents.V1.ExpenseRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Expenses.AnyAsync(x => x.Id == e.ExpenseId, ct)) return;

        db.Expenses.Add(new ExpenseEntity
        {
            Id = e.ExpenseId, FamilyId = e.FamilyId, Date = e.Date, Description = e.Description, Category = e.Category,
            AmountArs = e.AmountArs, OriginalAmount = e.OriginalAmount, OriginalCurrency = e.OriginalCurrency?.ToString(),
            PaymentMethodKind = e.PaymentMethodKind, PaymentMethodReferenceId = e.PaymentMethodReferenceId,
            OwnerKind = e.OwnerKind, OwnerPersonId = e.OwnerPersonId, UpdatedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleUpdated(Guid expenseId, ExpenseEvents.V1.ExpenseUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Expenses.FirstOrDefaultAsync(x => x.Id == expenseId, ct);
        if (entity is null) return;

        entity.Date = e.Date;
        entity.Description = e.Description;
        entity.Category = e.Category;
        entity.AmountArs = e.AmountArs;
        entity.OriginalAmount = e.OriginalAmount;
        entity.OriginalCurrency = e.OriginalCurrency?.ToString();
        entity.PaymentMethodKind = e.PaymentMethodKind;
        entity.PaymentMethodReferenceId = e.PaymentMethodReferenceId;
        entity.OwnerKind = e.OwnerKind;
        entity.OwnerPersonId = e.OwnerPersonId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleRemoved(Guid expenseId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Expenses.Where(x => x.Id == expenseId).ExecuteDeleteAsync(ct);
    }
}
