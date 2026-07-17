using GastNyahp.Domain.Banks;
using GastNyahp.Domain.Budgets;
using GastNyahp.Domain.BusinessDays;
using GastNyahp.Domain.Cards;
using GastNyahp.Domain.Expenses;
using GastNyahp.Domain.Income;
using GastNyahp.Domain.Installments;
using GastNyahp.Domain.Loans;
using GastNyahp.Domain.People;
using GastNyahp.Domain.Reserves;
using GastNyahp.Domain.Services;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Banks;
using GastNyahp.Infrastructure.Projections.Budgets;
using GastNyahp.Infrastructure.Projections.BusinessDays;
using GastNyahp.Infrastructure.Projections.Cards;
using GastNyahp.Infrastructure.Projections.Expenses;
using GastNyahp.Infrastructure.Projections.Income;
using GastNyahp.Infrastructure.Projections.Installments;
using GastNyahp.Infrastructure.Projections.Loans;
using GastNyahp.Infrastructure.Projections.People;
using GastNyahp.Infrastructure.Projections.Reserves;
using GastNyahp.Infrastructure.Projections.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests.Support;

/// <summary>
/// Full write→read pipeline without Postgres: real Eventuous CommandServices against InMemoryEventStore, real
/// projection handlers against a SQLite in-memory ProjectionsDbContext (same EF model as the Npgsql one), and
/// the same InMemoryProjectionPump the API host uses to feed the store's global log through EVERY projection
/// — exactly what the $all subscription does in production.
/// </summary>
public sealed class GastNyahpTestHost : IAsyncDisposable
{
    readonly SqliteConnection _connection;
    readonly DbContextOptions<ProjectionsDbContext> _dbOptions;
    readonly InMemoryProjectionPump _pump;

    public InMemoryEventStore Store { get; }
    public IDbContextFactory<ProjectionsDbContext> DbFactory { get; }

    public BankCommandService Banks { get; }
    public CreditCardCommandService Cards { get; }
    public InstallmentPurchaseCommandService Installments { get; }
    public LoanCommandService Loans { get; }
    public ServiceCommandService Services { get; }
    public ReserveCommandService Reserves { get; }
    public PersonCommandService People { get; }
    public ExpenseCommandService Expenses { get; }
    public TicketCommandService Tickets { get; }
    public BudgetPlanCommandService Budgets { get; }
    public IncomeCommandService Income { get; }
    public BusinessDayCommandService BusinessDays { get; }

    public GastNyahpTestHost()
    {
        // The in-memory SQLite database lives exactly as long as this open connection.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<ProjectionsDbContext>().UseSqlite(_connection).Options;
        using (var db = new ProjectionsDbContext(_dbOptions)) db.Database.EnsureCreated();
        DbFactory = new FixedOptionsDbContextFactory(_dbOptions);

        Store = new InMemoryEventStore();
        Banks = new BankCommandService(Store);
        Cards = new CreditCardCommandService(Store);
        Installments = new InstallmentPurchaseCommandService(Store);
        Loans = new LoanCommandService(Store);
        Services = new ServiceCommandService(Store);
        Reserves = new ReserveCommandService(Store);
        People = new PersonCommandService(Store);
        Expenses = new ExpenseCommandService(Store);
        Tickets = new TicketCommandService(Store);
        Budgets = new BudgetPlanCommandService(Store);
        Income = new IncomeCommandService(Store);
        BusinessDays = new BusinessDayCommandService(Store);

        _pump = new InMemoryProjectionPump(Store,
        [
            new BankProjection(DbFactory),
            new CreditCardProjection(DbFactory),
            new InstallmentProjection(DbFactory),
            new LoanProjection(DbFactory),
            new ServiceProjection(DbFactory),
            new ReserveProjection(DbFactory),
            new PersonProjection(DbFactory),
            new ExpenseProjection(DbFactory),
            new TicketProjection(DbFactory),
            new BudgetPlanProjection(DbFactory),
            new IncomeProjection(DbFactory),
            new BusinessDayProjection(DbFactory),
        ]);
    }

    /// <summary>Feeds every not-yet-projected event of the global log to ALL projections, in append order —
    /// each handler ignores event types it has no On&lt;T&gt; for, like the real shared subscription.</summary>
    public Task ProjectPending() => _pump.CatchUp();

    /// <summary>Drops and recreates the read model and rewinds the checkpoint — the "rebuild from the event
    /// store" scenario. The next ProjectPending() replays the whole log from position 0.</summary>
    public async Task ResetReadModel()
    {
        await using var db = new ProjectionsDbContext(_dbOptions);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        _pump.Rewind();
    }

    public ProjectionsDbContext Db() => new(_dbOptions);

    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }

    sealed class FixedOptionsDbContextFactory(DbContextOptions<ProjectionsDbContext> options) : IDbContextFactory<ProjectionsDbContext>
    {
        public ProjectionsDbContext CreateDbContext() => new(options);
    }
}
