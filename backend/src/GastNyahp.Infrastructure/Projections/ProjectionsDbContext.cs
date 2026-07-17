using GastNyahp.Infrastructure.Projections.Access;
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
using Microsoft.EntityFrameworkCore;
using BankEntity = GastNyahp.Infrastructure.Projections.Banks.BankEntity;

namespace GastNyahp.Infrastructure.Projections;

/// <summary>
/// Single read-model context for the whole app. DOMAIN_MODEL.md's aggregates are cohesive within one bounded
/// context (GastNyahp itself) and the Dashboard/BusinessDay novelties queries join across almost all of them —
/// splitting into one DbContext per aggregate (the general guidance in ef-core-postgres-context) would make
/// those cross-aggregate reads impossible to express as a single query for no real benefit today. Revisit if a
/// sub-area is ever extracted to its own deployable service.
/// </summary>
public class ProjectionsDbContext(DbContextOptions<ProjectionsDbContext> options) : DbContext(options)
{
    public DbSet<BankEntity> Banks => Set<BankEntity>();
    public DbSet<CreditCardEntity> CreditCards => Set<CreditCardEntity>();
    public DbSet<InstallmentPurchaseEntity> InstallmentPurchases => Set<InstallmentPurchaseEntity>();
    public DbSet<InstallmentMonthEntity> InstallmentMonths => Set<InstallmentMonthEntity>();
    public DbSet<LoanEntity> Loans => Set<LoanEntity>();
    public DbSet<LoanMonthEntity> LoanMonths => Set<LoanMonthEntity>();
    public DbSet<ServiceEntity> Services => Set<ServiceEntity>();
    public DbSet<ServiceMonthAmountEntity> ServiceMonthAmounts => Set<ServiceMonthAmountEntity>();
    public DbSet<ReserveEntity> Reserves => Set<ReserveEntity>();
    public DbSet<ReserveMonthOverrideEntity> ReserveMonthOverrides => Set<ReserveMonthOverrideEntity>();
    public DbSet<PersonEntity> People => Set<PersonEntity>();
    public DbSet<ExpenseEntity> Expenses => Set<ExpenseEntity>();
    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();
    public DbSet<TicketItemEntity> TicketItems => Set<TicketItemEntity>();
    public DbSet<BudgetPlanEntity> BudgetPlans => Set<BudgetPlanEntity>();
    public DbSet<IncomeEntity> Income => Set<IncomeEntity>();
    public DbSet<IncomeHistoryEntity> IncomeHistory => Set<IncomeHistoryEntity>();
    public DbSet<BusinessDayEntity> BusinessDays => Set<BusinessDayEntity>();
    public DbSet<FamilyEntity> Families => Set<FamilyEntity>();
    public DbSet<FamilyMemberEntity> FamilyMembers => Set<FamilyMemberEntity>();
    public DbSet<FamilyInviteEntity> FamilyInvites => Set<FamilyInviteEntity>();
    public DbSet<AdminInviteEntity> AdminInvites => Set<AdminInviteEntity>();
    public DbSet<FamilyAgentKeyEntity> FamilyAgentKeys => Set<FamilyAgentKeyEntity>();
    public DbSet<MemberSessionEntity> MemberSessions => Set<MemberSessionEntity>();
    public DbSet<PasswordResetEntity> PasswordResets => Set<PasswordResetEntity>();
    public DbSet<DraftEntity> Drafts => Set<DraftEntity>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<BankEntity>(e =>
        {
            e.ToTable("banks");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Alias).HasMaxLength(200);
            e.Property(x => x.Color).HasMaxLength(20).IsRequired();
            e.Property(x => x.Icon).HasMaxLength(50).IsRequired();
        });

        m.Entity<CreditCardEntity>(e =>
        {
            e.ToTable("credit_cards");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.BankId);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.Network).HasMaxLength(20).IsRequired();
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Color).HasMaxLength(20).IsRequired();
        });

        m.Entity<InstallmentPurchaseEntity>(e =>
        {
            e.ToTable("installment_purchases");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CardId);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
            e.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
            e.Property(x => x.OwnerKind).HasMaxLength(20).IsRequired();
            e.HasMany(x => x.Months).WithOne(x => x.Installment).HasForeignKey(x => x.InstallmentId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<InstallmentMonthEntity>(e =>
        {
            e.ToTable("installment_months");
            e.HasKey(x => new { x.InstallmentId, x.Month });
            e.Property(x => x.Month).HasMaxLength(7).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        m.Entity<LoanEntity>(e =>
        {
            e.ToTable("loans");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.BankId);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.MonthlyInstallment).HasPrecision(18, 2);
            e.HasMany(x => x.Months).WithOne(x => x.Loan).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<LoanMonthEntity>(e =>
        {
            e.ToTable("loan_months");
            e.HasKey(x => new { x.LoanId, x.Month });
            e.Property(x => x.Month).HasMaxLength(7).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        m.Entity<ServiceEntity>(e =>
        {
            e.ToTable("services");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LinkedCardId);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.BillingType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            e.Property(x => x.OriginalAmount).HasPrecision(18, 2);
            e.Property(x => x.OwnerKind).HasMaxLength(20).IsRequired();
            e.HasMany(x => x.Amounts).WithOne(x => x.Service).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<ServiceMonthAmountEntity>(e =>
        {
            e.ToTable("service_month_amounts");
            e.HasKey(x => new { x.ServiceId, x.Month });
            e.Property(x => x.Month).HasMaxLength(7).IsRequired();
            e.Property(x => x.AmountArs).HasPrecision(18, 2);
        });

        m.Entity<ReserveEntity>(e =>
        {
            e.ToTable("reserves");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.BaseAmount).HasPrecision(18, 2);
            e.HasMany(x => x.Months).WithOne(x => x.Reserve).HasForeignKey(x => x.ReserveId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<ReserveMonthOverrideEntity>(e =>
        {
            e.ToTable("reserve_month_overrides");
            e.HasKey(x => new { x.ReserveId, x.Month });
            e.Property(x => x.Month).HasMaxLength(7).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        m.Entity<PersonEntity>(e =>
        {
            e.ToTable("people");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        m.Entity<ExpenseEntity>(e =>
        {
            e.ToTable("expenses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.AmountArs).HasPrecision(18, 2);
            e.Property(x => x.OriginalAmount).HasPrecision(18, 2);
            e.Property(x => x.PaymentMethodKind).HasMaxLength(20).IsRequired();
            e.Property(x => x.OwnerKind).HasMaxLength(20).IsRequired();
        });

        m.Entity<TicketEntity>(e =>
        {
            e.ToTable("tickets");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.PaymentMethodKind).HasMaxLength(20).IsRequired();
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasMany(x => x.Items).WithOne(x => x.Ticket).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<TicketItemEntity>(e =>
        {
            e.ToTable("ticket_items");
            e.HasKey(x => new { x.TicketId, x.ItemId });
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.OwnerKind).HasMaxLength(20).IsRequired();
        });

        m.Entity<BudgetPlanEntity>(e =>
        {
            e.ToTable("budget_plans");
            e.HasKey(x => new { x.FamilyId, x.Month });
            e.Property(x => x.Month).HasMaxLength(7);
            e.Property(x => x.CreditLimit).HasPrecision(18, 2);
            e.Property(x => x.DebitCashLimit).HasPrecision(18, 2);
            e.Property(x => x.WeeklyLimit).HasPrecision(18, 2);
        });

        m.Entity<IncomeEntity>(e =>
        {
            e.ToTable("income");
            e.HasKey(x => x.FamilyId);
            e.Property(x => x.NetMonthly).HasPrecision(18, 2);
            e.Property(x => x.UsdRateOfficial).HasPrecision(18, 4);
            e.Property(x => x.UsdRateCcl).HasPrecision(18, 4);
        });
        m.Entity<IncomeHistoryEntity>(e =>
        {
            e.ToTable("income_history");
            e.HasKey(x => x.SequenceNumber);
            e.Property(x => x.SequenceNumber).ValueGeneratedOnAdd();
            e.Property(x => x.NetMonthly).HasPrecision(18, 2);
            e.Property(x => x.UsdRateOfficial).HasPrecision(18, 4);
            e.Property(x => x.UsdRateCcl).HasPrecision(18, 4);
        });

        m.Entity<BusinessDayEntity>(e =>
        {
            e.ToTable("business_days");
            e.HasKey(x => x.Date);
            e.Property(x => x.Date).HasMaxLength(10);
        });

        m.Entity<FamilyEntity>(e =>
        {
            e.ToTable("families");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        m.Entity<FamilyMemberEntity>(e =>
        {
            e.ToTable("family_members");
            e.HasKey(x => x.MemberId);
            e.HasIndex(x => x.FamilyId);
            e.HasIndex(x => x.TokenHash).IsUnique(); // the auth lookup: Bearer token hash → member
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            // Cuentas (docs/DISENO_CUENTAS_LOGIN.md): nullable = miembro de la etapa 1, sin cuenta todavía.
            e.Property(x => x.Email).HasMaxLength(320);       // 320 = máximo real de un email (64 local + @ + 255 dominio)
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            // El invariante del email único POR FAMILIA, también en la DB. El aggregate ya lo garantiza; esto es
            // defensa en profundidad: si algún día alguien escribe en la tabla por afuera de la proyección, la DB
            // lo frena igual. Filtrado: los NULL de la etapa 1 no compiten entre sí (varios miembros sin cuenta).
            e.HasIndex(x => new { x.FamilyId, x.Email }).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        });
        m.Entity<MemberSessionEntity>(e =>
        {
            e.ToTable("member_sessions");
            e.HasKey(x => x.SessionId);
            e.HasIndex(x => x.MemberId);
            e.HasIndex(x => x.TokenHash).IsUnique(); // la tercera pata del lookup de auth: token de sesión → miembro
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.DeviceName).HasMaxLength(100).IsRequired();
        });
        m.Entity<PasswordResetEntity>(e =>
        {
            e.ToTable("password_resets");
            e.HasKey(x => x.ResetId);
            e.HasIndex(x => x.CodeHash); // el canje: hash del código presentado → reseteo
            e.HasIndex(x => x.MemberId);
            e.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ExpiresAt).HasMaxLength(40).IsRequired();
        });
        m.Entity<FamilyInviteEntity>(e =>
        {
            e.ToTable("family_invites");
            e.HasKey(x => x.InviteId);
            e.HasIndex(x => x.CodeHash); // the join lookup: presented code hash → invite
            e.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ExpiresAt).HasMaxLength(40).IsRequired();
        });
        m.Entity<AdminInviteEntity>(e =>
        {
            e.ToTable("admin_invites");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CodeHash);
            e.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        });
        m.Entity<FamilyAgentKeyEntity>(e =>
        {
            e.ToTable("family_agent_keys");
            e.HasKey(x => x.KeyId);
            e.HasIndex(x => x.FamilyId);
            e.HasIndex(x => x.TokenHash).IsUnique(); // the other half of the auth lookup
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        });

        m.Entity<DraftEntity>(e =>
        {
            e.ToTable("drafts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyId, x.Status }); // el listado de la UI/MCP: abiertos de mi familia
            e.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.PayloadJson).IsRequired();
            e.Property(x => x.CreatedByKind).HasMaxLength(20).IsRequired();
        });
    }
}
