using GastNyahp.Domain.Loans;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class LoanIntegrationTests : IntegrationTest
{
    static readonly Guid LoanId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid BankId = Guid.NewGuid();

    Task RegisterDefault(int total = 12) => Ok(Host.Loans.Handle(
        new RegisterLoan(LoanId, FamilyId, BankId, "Préstamo personal", 2160000m, 180000m, "2025-11", total), default));

    [Fact]
    public async Task Register_projects_loan_with_schedule_and_zero_paid()
    {
        await RegisterDefault(12);

        await using var db = Host.Db();
        var loan = Assert.Single(await db.Loans.ToListAsync());
        Assert.Equal(0, loan.PaidInstallments);
        Assert.Equal(12, await db.LoanMonths.CountAsync());
    }

    [Fact]
    public async Task TogglePaid_keeps_the_denormalized_counter_in_sync()
    {
        await RegisterDefault(3);

        await Ok(Host.Loans.Handle(new ToggleLoanMonthPaid(LoanId, "2025-11"), default));
        await Ok(Host.Loans.Handle(new ToggleLoanMonthPaid(LoanId, "2025-12"), default));

        await using (var db = Host.Db())
            Assert.Equal(2, (await db.Loans.SingleAsync()).PaidInstallments);

        // Toggling one back off must recompute, not blindly decrement.
        await Ok(Host.Loans.Handle(new ToggleLoanMonthPaid(LoanId, "2025-11"), default));

        await using (var db = Host.Db())
            Assert.Equal(1, (await db.Loans.SingleAsync()).PaidInstallments);
    }

    [Fact]
    public async Task OverrideMonthAmount_supports_UVA_variable_installments()
    {
        await RegisterDefault(3);
        await Ok(Host.Loans.Handle(new OverrideLoanMonthAmount(LoanId, "2025-12", 247309m), default));

        await using var db = Host.Db();
        Assert.Equal(247309m, (await db.LoanMonths.SingleAsync(m => m.Month == "2025-12")).Amount);
        Assert.Equal(180000m, (await db.LoanMonths.SingleAsync(m => m.Month == "2025-11")).Amount);
    }

    [Fact]
    public async Task ReviseSchedule_regenerates_preserving_paid_and_recomputes_counter()
    {
        await RegisterDefault(3);
        await Ok(Host.Loans.Handle(new ToggleLoanMonthPaid(LoanId, "2025-11"), default));

        await Ok(Host.Loans.Handle(new ReviseLoanSchedule(LoanId, "2025-11", 6, 200000m), default));

        await using var db = Host.Db();
        var months = await db.LoanMonths.OrderBy(m => m.Month).ToListAsync();
        Assert.Equal(6, months.Count);
        Assert.True(months.Single(m => m.Month == "2025-11").Paid);
        Assert.Equal(180000m, months.Single(m => m.Month == "2025-11").Amount);
        Assert.Equal(200000m, months.Single(m => m.Month == "2025-12").Amount);
        Assert.Equal(1, (await db.Loans.SingleAsync()).PaidInstallments);
    }

    [Fact]
    public async Task Remove_cascades_to_month_rows()
    {
        await RegisterDefault(12);
        await Ok(Host.Loans.Handle(new RemoveLoan(LoanId), default));

        await using var db = Host.Db();
        Assert.Empty(await db.Loans.ToListAsync());
        Assert.Empty(await db.LoanMonths.ToListAsync());
    }
}
