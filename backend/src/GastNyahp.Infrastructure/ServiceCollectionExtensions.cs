using Eventuous;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using GastNyahp.Domain.Access;
using GastNyahp.Domain.Banks;
using GastNyahp.Domain.Budgets;
using GastNyahp.Domain.BusinessDays;
using GastNyahp.Domain.Cards;
using GastNyahp.Domain.Drafts;
using GastNyahp.Domain.Expenses;
using GastNyahp.Domain.Income;
using GastNyahp.Domain.Installments;
using GastNyahp.Domain.Loans;
using GastNyahp.Domain.People;
using GastNyahp.Domain.Reserves;
using GastNyahp.Domain.Services;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Access;
using GastNyahp.Infrastructure.Projections.Banks;
using GastNyahp.Infrastructure.Projections.Budgets;
using GastNyahp.Infrastructure.Projections.BusinessDays;
using GastNyahp.Infrastructure.Projections.Cards;
using GastNyahp.Infrastructure.Projections.Drafts;
using GastNyahp.Infrastructure.Projections.Expenses;
using GastNyahp.Infrastructure.Projections.Income;
using GastNyahp.Infrastructure.Projections.Installments;
using GastNyahp.Infrastructure.Projections.Loans;
using GastNyahp.Infrastructure.Projections.People;
using GastNyahp.Infrastructure.Projections.Reserves;
using GastNyahp.Infrastructure.Projections.Services;
using GastNyahp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Everything the API host needs: read-model DbContext (Postgres or Sqlite via Database:Provider),
    /// event store (EventStore:Provider — InMemory today, Postgres when the docker stack lands), projections,
    /// command services, application services, and the startup schema initializer.
    /// </summary>
    public static IServiceCollection AddGastNyahpInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Los application services leen algunos settings (p.ej. Admin:ApiKey) vía IConfiguration; en el host real
        // ya está registrada, pero dejamos el grafo auto-suficiente para quien lo arme con un ServiceCollection pelado.
        services.TryAddSingleton(configuration);

        var connectionString = configuration.GetConnectionString("Projections")
            ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Projections'.");
        var databaseProvider = configuration["Database:Provider"] ?? "Postgres";

        services.AddDbContextFactory<ProjectionsDbContext>(opts =>
        {
            if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                opts.UseSqlite(connectionString);
            else
                opts.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null));
        });

        services.AddHostedService<ProjectionsDatabaseInitializer>();

        services.AddGastNyahpEventStore(configuration);

        // The daily BusinessDay heartbeat — E2E/tests disable it via BusinessDay:Enabled=false so scenarios
        // control their own dates. Registered AFTER the event store: it appends on startup, and hosted
        // services start in registration order, so the Eventuous schema bootstrap must run first.
        services.Configure<BusinessDayOptions>(configuration.GetSection("BusinessDay"));
        if (configuration.GetValue("BusinessDay:Enabled", true))
            services.AddHostedService<BusinessDayScheduler>();

        return services
            .AddGastNyahpProjections()
            .AddGastNyahpCommandServices()
            .AddGastNyahpApplicationServices();
    }

    public static IServiceCollection AddGastNyahpEventStore(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["EventStore:Provider"] ?? "InMemory";

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            // Event store + read model share the Postgres instance, in separate schemas: Eventuous owns
            // "eventuous" (append-only streams, created on startup), EF owns "public" (projections).
            var connectionString = configuration.GetConnectionString("Projections")
                ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Projections'.");
            var schema = configuration["EventStore:Schema"] ?? "eventuous";
            const string subscriptionId = "gastnyahp-projections";

            // Schema bootstrap on a throwaway data source, registered BEFORE the subscription so it starts
            // first — see EventStoreSchemaInitializer for why initializeDatabase must stay false here.
            services.AddHostedService(sp => new EventStoreSchemaInitializer(
                connectionString, schema, sp.GetRequiredService<ILogger<Schema>>()));

            services.AddEventuousPostgres(connectionString, schema, initializeDatabase: false);
            // AddEventuousPostgres registers the concrete PostgresStore; our CommandServices depend on the
            // IEventStore abstraction (so tests can swap InMemory) — map it explicitly.
            services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<PostgresStore>());
            services.AddPostgresCheckpointStore();

            // THE $all subscription of the eventuous-projection-readmodel skill: one subscription, every
            // projection handler — which is why every insert handler must stay idempotent (23505 swallow).
            services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
                subscriptionId,
                b => b
                    .Configure(opt =>
                    {
                        // Read-your-writes (SubscriptionReadModelSync) espera el checkpoint: con los defaults
                        // (batch 100 / linger 5000ms) cada write interactivo tardaba ~3-5s en confirmarse.
                        opt.CheckpointCommitBatchSize = 1;
                        opt.CheckpointCommitDelayMs = 10;
                        // Menos agresivo que el default (5ms): en caliente sigue dando lecturas ~100ms tras
                        // un write; en reposo decae hasta 1 poll/segundo.
                        opt.Polling.MinIntervalMs = 100;
                        opt.Polling.MaxIntervalMs = 1000;
                    })
                    .AddEventHandler<BankProjection>()
                    .AddEventHandler<CreditCardProjection>()
                    .AddEventHandler<InstallmentProjection>()
                    .AddEventHandler<LoanProjection>()
                    .AddEventHandler<ServiceProjection>()
                    .AddEventHandler<ReserveProjection>()
                    .AddEventHandler<PersonProjection>()
                    .AddEventHandler<ExpenseProjection>()
                    .AddEventHandler<TicketProjection>()
                    .AddEventHandler<BudgetPlanProjection>()
                    .AddEventHandler<IncomeProjection>()
                    .AddEventHandler<BusinessDayProjection>()
                    .AddEventHandler<FamilyProjection>()
                    .AddEventHandler<AdminInviteProjection>()
                    .AddEventHandler<DraftProjection>());

            services.AddSingleton<IReadModelSync>(_ => new SubscriptionReadModelSync(connectionString, schema, subscriptionId));
            return services;
        }

        if (!provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"EventStore:Provider '{provider}' desconocido — usar 'InMemory' o 'Postgres'.");

        services.AddSingleton<InMemoryEventStore>();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<InMemoryEventStore>());
        services.AddSingleton<InMemoryProjectionPump>();
        services.AddSingleton<IReadModelSync>(sp => sp.GetRequiredService<InMemoryProjectionPump>());
        return services;
    }

    /// <summary>Projection handlers, each registered as itself AND as GastNyahpProjection so the pump (or the
    /// future $all subscription) can enumerate them all.</summary>
    public static IServiceCollection AddGastNyahpProjections(this IServiceCollection services)
    {
        AddProjection<BankProjection>(services);
        AddProjection<CreditCardProjection>(services);
        AddProjection<InstallmentProjection>(services);
        AddProjection<LoanProjection>(services);
        AddProjection<ServiceProjection>(services);
        AddProjection<ReserveProjection>(services);
        AddProjection<PersonProjection>(services);
        AddProjection<ExpenseProjection>(services);
        AddProjection<TicketProjection>(services);
        AddProjection<BudgetPlanProjection>(services);
        AddProjection<IncomeProjection>(services);
        AddProjection<BusinessDayProjection>(services);
        AddProjection<FamilyProjection>(services);
        AddProjection<AdminInviteProjection>(services);
        AddProjection<DraftProjection>(services);
        return services;

        static void AddProjection<T>(IServiceCollection services) where T : GastNyahpProjection
        {
            services.AddSingleton<T>();
            services.AddSingleton<GastNyahpProjection>(sp => sp.GetRequiredService<T>());
        }
    }

    public static IServiceCollection AddGastNyahpCommandServices(this IServiceCollection services)
    {
        services.AddSingleton<BankCommandService>();
        services.AddSingleton<CreditCardCommandService>();
        services.AddSingleton<InstallmentPurchaseCommandService>();
        services.AddSingleton<LoanCommandService>();
        services.AddSingleton<ServiceCommandService>();
        services.AddSingleton<ReserveCommandService>();
        services.AddSingleton<PersonCommandService>();
        services.AddSingleton<ExpenseCommandService>();
        services.AddSingleton<TicketCommandService>();
        services.AddSingleton<BudgetPlanCommandService>();
        services.AddSingleton<IncomeCommandService>();
        services.AddSingleton<BusinessDayCommandService>();
        services.AddSingleton<FamilyCommandService>();
        services.AddSingleton<AdminInviteCommandService>();
        services.AddSingleton<DraftCommandService>();
        return services;
    }

    public static IServiceCollection AddGastNyahpApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<BankService>();
        services.AddSingleton<CardService>();
        services.AddSingleton<InstallmentService>();
        services.AddSingleton<LoanService>();
        services.AddSingleton<ServicesService>();
        services.AddSingleton<ReserveService>();
        services.AddSingleton<PersonService>();
        services.AddSingleton<ExpenseService>();
        services.AddSingleton<TicketService>();
        services.AddSingleton<PlanningService>();
        services.AddSingleton<BusinessDayService>();
        // Singleton a propósito: el backoff de login cuenta intentos ENTRE requests. Scoped se reiniciaría en
        // cada uno y no frenaría nada (docs/DISENO_CUENTAS_LOGIN.md, amenaza #1).
        services.AddSingleton<LoginThrottle>();
        services.AddSingleton<FamilyService>();
        services.AddSingleton<LegacyImportService>();
        services.AddSingleton<ImportJobTracker>();
        services.AddSingleton<DraftService>();
        return services;
    }
}
