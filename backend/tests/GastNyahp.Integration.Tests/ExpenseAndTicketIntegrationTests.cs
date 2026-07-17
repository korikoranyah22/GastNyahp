using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class ExpenseIntegrationTests : IntegrationTest
{
    static readonly Guid ExpenseId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid CardId = Guid.NewGuid();

    [Fact]
    public async Task Register_projects_the_expense_with_its_payment_method()
    {
        await Ok(Host.Expenses.Handle(
            new RegisterExpense(ExpenseId, FamilyId, "2026-02-03", "Supermercado", "Comida", 95000m,
                ExpenseCurrency.Ars, PaymentMethod.ByCard(CardId), OwnerRef.SharedOwner, 0), default));

        await using var db = Host.Db();
        var expense = Assert.Single(await db.Expenses.ToListAsync());
        Assert.Equal("Card", expense.PaymentMethodKind);
        Assert.Equal(CardId, expense.PaymentMethodReferenceId);
        Assert.Equal("Shared", expense.OwnerKind);
        Assert.Null(expense.OwnerPersonId);
    }

    [Fact]
    public async Task Usd_expense_is_converted_and_original_preserved()
    {
        await Ok(Host.Expenses.Handle(
            new RegisterExpense(ExpenseId, FamilyId, "2026-02-03", "Compra online", "Electrónica", 50m,
                ExpenseCurrency.Usd, PaymentMethod.CashPayment, OwnerRef.None, UsdRateCcl: 1250m), default));

        await using var db = Host.Db();
        var expense = await db.Expenses.SingleAsync();
        Assert.Equal(62500m, expense.AmountArs);
        Assert.Equal(50m, expense.OriginalAmount);
        Assert.Equal("Usd", expense.OriginalCurrency);
    }

    [Fact]
    public async Task Update_and_remove_project_correctly()
    {
        await Ok(Host.Expenses.Handle(
            new RegisterExpense(ExpenseId, FamilyId, "2026-02-03", "Supermercado", "Comida", 95000m,
                ExpenseCurrency.Ars, PaymentMethod.CashPayment, OwnerRef.None, 0), default));

        await Ok(Host.Expenses.Handle(
            new UpdateExpense(ExpenseId, "2026-02-04", "Verdulería", "Comida", 8500m,
                ExpenseCurrency.Ars, PaymentMethod.ByDebit(Guid.NewGuid()), OwnerRef.None, 0), default));

        await using (var db = Host.Db())
        {
            var expense = await db.Expenses.SingleAsync();
            Assert.Equal("Verdulería", expense.Description);
            Assert.Equal(8500m, expense.AmountArs);
            Assert.Equal("Debit", expense.PaymentMethodKind);
        }

        await Ok(Host.Expenses.Handle(new RemoveExpense(ExpenseId), default));
        await using (var db = Host.Db())
            Assert.Empty(await db.Expenses.ToListAsync());
    }
}

public class TicketIntegrationTests : IntegrationTest
{
    static readonly Guid TicketId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    static TicketItemInput Item(string description, decimal amount, string category = "Comida") =>
        new(Guid.NewGuid(), description, amount, category, "Unassigned", null);

    [Fact]
    public async Task Register_projects_ticket_items_and_denormalized_total()
    {
        await Ok(Host.Tickets.Handle(
            new RegisterTicket(TicketId, FamilyId, "2026-02-15", "Supermercado", PaymentMethod.CashPayment, Discount: 5000m,
                [Item("Carne", 30000m), Item("Lavandina", 8000m, "Limpieza")]), default));

        await using var db = Host.Db();
        var ticket = Assert.Single(await db.Tickets.ToListAsync());
        Assert.Equal(33000m, ticket.Total); // 30000 + 8000 - 5000
        Assert.Equal(2, await db.TicketItems.CountAsync());
    }

    [Fact]
    public async Task Update_replaces_the_item_set_and_recomputes_total()
    {
        await Ok(Host.Tickets.Handle(
            new RegisterTicket(TicketId, FamilyId, "2026-02-15", "Supermercado", PaymentMethod.CashPayment, 0,
                [Item("Carne", 30000m), Item("Pan", 2000m)]), default));

        await Ok(Host.Tickets.Handle(
            new UpdateTicket(TicketId, "2026-02-15", "Supermercado", PaymentMethod.CashPayment, 1000m,
                [Item("Solo carne", 30000m)]), default));

        await using var db = Host.Db();
        var ticket = await db.Tickets.SingleAsync();
        Assert.Equal(29000m, ticket.Total);
        var item = Assert.Single(await db.TicketItems.ToListAsync());
        Assert.Equal("Solo carne", item.Description);
    }

    [Fact]
    public async Task Remove_cascades_items()
    {
        await Ok(Host.Tickets.Handle(
            new RegisterTicket(TicketId, FamilyId, "2026-02-15", "Supermercado", PaymentMethod.CashPayment, 0,
                [Item("Carne", 30000m)]), default));
        await Ok(Host.Tickets.Handle(new RemoveTicket(TicketId), default));

        await using var db = Host.Db();
        Assert.Empty(await db.Tickets.ToListAsync());
        Assert.Empty(await db.TicketItems.ToListAsync());
    }
}
