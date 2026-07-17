using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Loans;

public static class LoanEvents
{
    public static class V1
    {
        [EventType("V1.LoanRegistered")]
        public record LoanRegistered(Guid LoanId, Guid FamilyId, Guid BankId, string Description, decimal? TotalAmount, decimal MonthlyInstallment, string StartMonth, int TotalInstallments);

        [EventType("V1.LoanScheduleRevised")]
        public record LoanScheduleRevised(string StartMonth, int TotalInstallments, decimal MonthlyInstallment);

        [EventType("V1.LoanDetailsUpdated")]
        public record LoanDetailsUpdated(string Description, decimal? TotalAmount);

        [EventType("V1.LoanMonthAmountOverridden")]
        public record LoanMonthAmountOverridden(string Month, decimal Amount);

        [EventType("V1.LoanMonthPaidToggled")]
        public record LoanMonthPaidToggled(string Month);

        [EventType("V1.LoanRemoved")]
        public record LoanRemoved;
    }
}

public record LoanState : State<LoanState>
{
    public Guid LoanId { get; init; }
    public Guid FamilyId { get; init; }
    public Guid BankId { get; init; }
    public string Description { get; init; } = "";
    public decimal? TotalAmount { get; init; }
    public decimal MonthlyInstallment { get; init; }
    public YearMonth StartMonth { get; init; }
    public int TotalInstallments { get; init; }
    public bool Removed { get; init; }
    public IReadOnlyList<ScheduleMonth> Months { get; init; } = [];

    /// <summary>Always derived from Months — never a mutated counter (see DOMAIN_MODEL.md §5 decision).</summary>
    public int PaidInstallments => Months.Count(m => m.Paid);
    public decimal RemainingAmount => Months.Where(m => !m.Paid).Sum(m => m.Amount);

    public LoanState()
    {
        On<LoanEvents.V1.LoanRegistered>((s, e) =>
        {
            var start = YearMonth.Parse(e.StartMonth);
            return s with
            {
                LoanId = e.LoanId, FamilyId = e.FamilyId, BankId = e.BankId, Description = e.Description, TotalAmount = e.TotalAmount,
                MonthlyInstallment = e.MonthlyInstallment, StartMonth = start, TotalInstallments = e.TotalInstallments,
                Months = MonthlySchedule.Generate(start, e.TotalInstallments, e.MonthlyInstallment),
            };
        });
        On<LoanEvents.V1.LoanScheduleRevised>((s, e) =>
        {
            var start = YearMonth.Parse(e.StartMonth);
            return s with
            {
                StartMonth = start, TotalInstallments = e.TotalInstallments, MonthlyInstallment = e.MonthlyInstallment,
                Months = MonthlySchedule.Revise(s.Months, start, e.TotalInstallments, e.MonthlyInstallment),
            };
        });
        On<LoanEvents.V1.LoanDetailsUpdated>((s, e) => s with { Description = e.Description, TotalAmount = e.TotalAmount });
        On<LoanEvents.V1.LoanMonthAmountOverridden>((s, e) =>
            s with { Months = MonthlySchedule.OverrideAmount(s.Months, YearMonth.Parse(e.Month), e.Amount) });
        On<LoanEvents.V1.LoanMonthPaidToggled>((s, e) =>
            s with { Months = MonthlySchedule.TogglePaid(s.Months, YearMonth.Parse(e.Month)) });
        On<LoanEvents.V1.LoanRemoved>((s, _) => s with { Removed = true });
    }
}

public record RegisterLoan(Guid LoanId, Guid FamilyId, Guid BankId, string Description, decimal? TotalAmount, decimal MonthlyInstallment, string StartMonth, int TotalInstallments);
public record ReviseLoanSchedule(Guid LoanId, string StartMonth, int TotalInstallments, decimal MonthlyInstallment);
public record UpdateLoanDetails(Guid LoanId, string Description, decimal? TotalAmount);
public record OverrideLoanMonthAmount(Guid LoanId, string Month, decimal Amount);
public record ToggleLoanMonthPaid(Guid LoanId, string Month);
public record RemoveLoan(Guid LoanId);

public sealed class LoanCommandService : CommandService<LoanState>
{
    public LoanCommandService(IEventStore store) : base(store)
    {
        On<RegisterLoan>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.LoanId)).Act(Register);
        On<ReviseLoanSchedule>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.LoanId)).Act(Revise);
        On<UpdateLoanDetails>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.LoanId)).Act(UpdateDetails);
        On<OverrideLoanMonthAmount>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.LoanId)).Act(OverrideAmount);
        On<ToggleLoanMonthPaid>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.LoanId)).Act(TogglePaid);
        On<RemoveLoan>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.LoanId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterLoan cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Description)) throw new DomainException("RegisterLoan: Description required.");
        if (cmd.MonthlyInstallment <= 0) throw new DomainException("RegisterLoan: MonthlyInstallment must be > 0.");
        if (cmd.TotalInstallments is <= 0 or > 360) throw new DomainException("RegisterLoan: TotalInstallments must be between 1 and 360.");

        yield return new LoanEvents.V1.LoanRegistered(cmd.LoanId, cmd.FamilyId, cmd.BankId, cmd.Description.Trim(), cmd.TotalAmount, cmd.MonthlyInstallment, cmd.StartMonth, cmd.TotalInstallments);
    }

    // Deliberate improvement over the current frontend, which never regenerates a loan's calendar on edit —
    // see DOMAIN_MODEL.md §5 decision #3. Uses the same MonthlySchedule.Revise algorithm as InstallmentPurchase.
    public static IEnumerable<object> Revise(LoanState state, object[] _, ReviseLoanSchedule cmd)
    {
        GuardNotRemoved(state, "ReviseLoanSchedule");
        if (cmd.MonthlyInstallment <= 0) throw new DomainException("ReviseLoanSchedule: MonthlyInstallment must be > 0.");
        if (cmd.TotalInstallments is <= 0 or > 360) throw new DomainException("ReviseLoanSchedule: TotalInstallments must be between 1 and 360.");
        yield return new LoanEvents.V1.LoanScheduleRevised(cmd.StartMonth, cmd.TotalInstallments, cmd.MonthlyInstallment);
    }

    public static IEnumerable<object> UpdateDetails(LoanState state, object[] _, UpdateLoanDetails cmd)
    {
        GuardNotRemoved(state, "UpdateLoanDetails");
        if (string.IsNullOrWhiteSpace(cmd.Description)) throw new DomainException("UpdateLoanDetails: Description required.");
        yield return new LoanEvents.V1.LoanDetailsUpdated(cmd.Description.Trim(), cmd.TotalAmount);
    }

    public static IEnumerable<object> OverrideAmount(LoanState state, object[] _, OverrideLoanMonthAmount cmd)
    {
        GuardNotRemoved(state, "OverrideLoanMonthAmount");
        if (cmd.Amount < 0) throw new DomainException("OverrideLoanMonthAmount: Amount cannot be negative.");
        yield return new LoanEvents.V1.LoanMonthAmountOverridden(cmd.Month, cmd.Amount);
    }

    public static IEnumerable<object> TogglePaid(LoanState state, object[] _, ToggleLoanMonthPaid cmd)
    {
        GuardNotRemoved(state, "ToggleLoanMonthPaid");
        yield return new LoanEvents.V1.LoanMonthPaidToggled(cmd.Month);
    }

    public static IEnumerable<object> Remove(LoanState state, object[] _, RemoveLoan cmd)
    {
        GuardNotRemoved(state, "RemoveLoan");
        yield return new LoanEvents.V1.LoanRemoved();
    }

    static void GuardNotRemoved(LoanState state, string command)
    {
        if (state.Removed) throw new DomainException($"{command}: loan was removed.");
    }

    static StreamName Stream(Guid id) => new($"loan-{id}");
}
