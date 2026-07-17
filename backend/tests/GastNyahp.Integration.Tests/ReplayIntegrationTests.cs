using Eventuous;
using GastNyahp.Domain.Banks;
using GastNyahp.Domain.Budgets;
using GastNyahp.Domain.BusinessDays;
using GastNyahp.Domain.Cards;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Domain.Income;
using GastNyahp.Domain.Installments;
using GastNyahp.Domain.Loans;
using GastNyahp.Domain.People;
using GastNyahp.Domain.Reserves;
using GastNyahp.Domain.Services;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

/// <summary>
/// The property that makes the read model trustworthy without a real Postgres: it is a pure fold of the event
/// log. If we wipe it and replay every event from position 0, we must land on exactly the same rows.
/// </summary>
public class ReplayIntegrationTests : IntegrationTest
{
    readonly Guid _familyId = Guid.NewGuid();
    readonly Guid _bankId = Guid.NewGuid();
    readonly Guid _cardId = Guid.NewGuid();
    readonly Guid _installmentId = Guid.NewGuid();
    readonly Guid _loanId = Guid.NewGuid();
    readonly Guid _serviceId = Guid.NewGuid();
    readonly Guid _reserveId = Guid.NewGuid();
    readonly Guid _personId = Guid.NewGuid();
    readonly Guid _expenseId = Guid.NewGuid();
    readonly Guid _ticketId = Guid.NewGuid();

    /// <summary>A representative slice of real GastNyahp usage touching every aggregate, including edits,
    /// toggles, a schedule revision, and a removal.</summary>
    async Task RunRepresentativeScenario()
    {
        await Ok(Host.People.Handle(new RegisterPerson(_personId, _familyId, "Cami", "😀", "#e11d48"), default));
        await Ok(Host.Banks.Handle(new RegisterBank(_bankId, _familyId, "BBVA", null, "#004B9B", "bbva"), default));
        await Ok(Host.Cards.Handle(new RegisterCard(_cardId, _familyId, _bankId, "VISA BBVA", CardNetwork.Visa, CardType.Credit, 15, 5, "#1e40af"), default));

        await Ok(Host.Installments.Handle(new RegisterInstallmentPurchase(
            _installmentId, _familyId, _cardId, "Smart TV", "Hogar", "2025-10-05", InstallmentFrequency.Fixed, 85000m, 4, "2025-10", OwnerRef.Of(_personId)), default));
        await Ok(Host.Installments.Handle(new ToggleInstallmentMonthPaid(_installmentId, "2025-10"), default));
        await Ok(Host.Installments.Handle(new ReviseInstallmentSchedule(_installmentId, "2025-10", 6, InstallmentFrequency.Fixed, 90000m), default));

        await Ok(Host.Loans.Handle(new RegisterLoan(_loanId, _familyId, _bankId, "Préstamo", 2160000m, 180000m, "2025-11", 12), default));
        await Ok(Host.Loans.Handle(new ToggleLoanMonthPaid(_loanId, "2025-11"), default));
        await Ok(Host.Loans.Handle(new OverrideLoanMonthAmount(_loanId, "2025-12", 247309m), default));

        await Ok(Host.Services.Handle(new RegisterService(
            _serviceId, _familyId, "Electricidad", "Electricidad", BillingType.Monthly, null, ServiceCurrency.Ars, 35000m, "2026-01", OwnerRef.None, 0), default));
        await Ok(Host.Services.Handle(new ToggleServiceMonthPaid(_serviceId, "2026-01"), default));
        await Ok(Host.Services.Handle(new ExtendServiceFutureAmounts(_serviceId, "2026-02", 40000m, 12), default));

        await Ok(Host.Reserves.Handle(new RegisterReserve(_reserveId, _familyId, "Efectivo", ReserveType.Cash, "💵", true, 100000m), default));
        await Ok(Host.Reserves.Handle(new SetReserveMonthAmount(_reserveId, "2026-02", 50000m, "ajuste"), default));

        await Ok(Host.Expenses.Handle(new RegisterExpense(
            _expenseId, _familyId, "2026-02-03", "Supermercado", "Comida", 95000m, ExpenseCurrency.Ars, PaymentMethod.ByCard(_cardId), OwnerRef.None, 0), default));

        await Ok(Host.Tickets.Handle(new RegisterTicket(
            _ticketId, _familyId, "2026-02-15", "Coto", PaymentMethod.CashPayment, 5000m,
            [new TicketItemInput(Guid.NewGuid(), "Carne", 30000m, "Comida", "Owner", _personId),
             new TicketItemInput(Guid.NewGuid(), "Lavandina", 8000m, "Limpieza", "Unassigned", null)]), default));

        await Ok(Host.Budgets.Handle(new SetBudgetLimits(_familyId, "2026-02", 480000m, 316000m, 200000m), default));
        await Ok(Host.Income.Handle(new UpdateIncome(_familyId, 500000m, 1050m, 1250m, 70), default));
        await Ok(Host.Income.Handle(new UpdateIncome(_familyId, 600000m, null, null, null), default));
        await Ok(Host.BusinessDays.Handle(new OpenBusinessDay("2026-07-09"), default));

        // One removal, to prove deletes replay identically too.
        var removedExpense = Guid.NewGuid();
        await Ok(Host.Expenses.Handle(new RegisterExpense(
            removedExpense, _familyId, "2026-02-04", "Error de carga", "Desconocido", 1m, ExpenseCurrency.Ars, PaymentMethod.CashPayment, OwnerRef.None, 0), default));
        await Ok(Host.Expenses.Handle(new RemoveExpense(removedExpense), default));
    }

    record ReadModelSnapshot(
        int Banks, int Cards, int Installments, int InstallmentMonths, int Loans, int LoanMonths,
        int Services, int ServiceAmounts, int Reserves, int ReserveOverrides, int People,
        int Expenses, int Tickets, int TicketItems, int Budgets, int IncomeHistory, int BusinessDays,
        decimal TicketTotal, int LoanPaid, decimal InstallmentNovAmount, bool InstallmentOctPaid, decimal ServiceFebAmount);

    async Task<ReadModelSnapshot> Snapshot()
    {
        await using var db = Host.Db();
        return new ReadModelSnapshot(
            await db.Banks.CountAsync(), await db.CreditCards.CountAsync(),
            await db.InstallmentPurchases.CountAsync(), await db.InstallmentMonths.CountAsync(),
            await db.Loans.CountAsync(), await db.LoanMonths.CountAsync(),
            await db.Services.CountAsync(), await db.ServiceMonthAmounts.CountAsync(),
            await db.Reserves.CountAsync(), await db.ReserveMonthOverrides.CountAsync(),
            await db.People.CountAsync(), await db.Expenses.CountAsync(),
            await db.Tickets.CountAsync(), await db.TicketItems.CountAsync(),
            await db.BudgetPlans.CountAsync(), await db.IncomeHistory.CountAsync(), await db.BusinessDays.CountAsync(),
            (await db.Tickets.SingleAsync()).Total,
            (await db.Loans.SingleAsync()).PaidInstallments,
            (await db.InstallmentMonths.SingleAsync(m => m.Month == "2025-11")).Amount,
            (await db.InstallmentMonths.SingleAsync(m => m.Month == "2025-10")).Paid,
            (await db.ServiceMonthAmounts.SingleAsync(m => m.Month == "2026-02")).AmountArs);
    }

    [Fact]
    public async Task Wiping_the_read_model_and_replaying_the_full_log_rebuilds_it_identically()
    {
        await RunRepresentativeScenario();
        var before = await Snapshot();

        await Host.ResetReadModel();
        await using (var db = Host.Db())
            Assert.Empty(await db.Banks.ToListAsync()); // proves the wipe actually happened

        await Host.ProjectPending(); // full replay from position 0
        var after = await Snapshot();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Read_model_matches_the_domain_state_folded_from_the_same_stream()
    {
        await RunRepresentativeScenario();

        // Fold the installment stream into the aggregate State — the projection must agree cell by cell.
        var state = new InstallmentPurchaseState();
        await foreach (var evt in Host.Store.ReadEvents(new StreamName($"installment-{_installmentId}"), StreamReadPosition.Start, int.MaxValue))
            state = state.When(evt.Payload!);

        await using var db = Host.Db();
        var rows = await db.InstallmentMonths.OrderBy(m => m.Month).ToListAsync();

        Assert.Equal(state.Months.Count, rows.Count);
        foreach (var (domainMonth, row) in state.Months.OrderBy(m => m.Month).Zip(rows))
        {
            Assert.Equal(domainMonth.Month.ToString(), row.Month);
            Assert.Equal(domainMonth.Amount, row.Amount);
            Assert.Equal(domainMonth.Paid, row.Paid);
        }
    }

    [Fact]
    public async Task Loan_counter_matches_domain_derived_value_after_the_scenario()
    {
        await RunRepresentativeScenario();

        var state = new LoanState();
        await foreach (var evt in Host.Store.ReadEvents(new StreamName($"loan-{_loanId}"), StreamReadPosition.Start, int.MaxValue))
            state = state.When(evt.Payload!);

        await using var db = Host.Db();
        var loan = await db.Loans.SingleAsync();
        Assert.Equal(state.PaidInstallments, loan.PaidInstallments);
        Assert.Equal(state.Months.Count, await db.LoanMonths.CountAsync());
    }
}
