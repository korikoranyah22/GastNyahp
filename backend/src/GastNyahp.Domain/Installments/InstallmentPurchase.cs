using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Installments;

public enum InstallmentFrequency { Fixed, Monthly }

/// <summary>Fixed window used for "Monthly" (open-ended recurring) installments — see DOMAIN_MODEL.md §4.</summary>
public static class InstallmentDefaults
{
    public const int MonthlyRecurringWindow = 24;
}

public static class InstallmentEvents
{
    public static class V1
    {
        [EventType("V1.InstallmentPurchaseRegistered")]
        public record InstallmentPurchaseRegistered(
            Guid InstallmentId, Guid FamilyId, Guid CardId, string Description, string Category, string PurchaseDate,
            InstallmentFrequency Frequency, decimal MonthlyAmount, int? TotalInstallments, string StartMonth,
            string OwnerKind, Guid? OwnerPersonId);

        [EventType("V1.InstallmentScheduleRevised")]
        public record InstallmentScheduleRevised(string StartMonth, int? TotalInstallments, InstallmentFrequency Frequency, decimal MonthlyAmount);

        // Cosmetic edits (description/category/owner) are a SEPARATE event from schedule revisions on
        // purpose — the frontend's single generic update caused the "does this regenerate?" ambiguity (§4).
        [EventType("V1.InstallmentDetailsUpdated")]
        public record InstallmentDetailsUpdated(string Description, string Category, string PurchaseDate, string OwnerKind, Guid? OwnerPersonId);

        [EventType("V1.InstallmentMonthAmountOverridden")]
        public record InstallmentMonthAmountOverridden(string Month, decimal Amount);

        [EventType("V1.InstallmentMonthPaidToggled")]
        public record InstallmentMonthPaidToggled(string Month);

        [EventType("V1.InstallmentFinished")]
        public record InstallmentFinished;

        [EventType("V1.InstallmentRemoved")]
        public record InstallmentRemoved;
    }
}

public record InstallmentPurchaseState : State<InstallmentPurchaseState>
{
    public Guid InstallmentId { get; init; }
    public Guid FamilyId { get; init; }
    public Guid CardId { get; init; }
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string PurchaseDate { get; init; } = "";
    public InstallmentFrequency Frequency { get; init; }
    public decimal MonthlyAmount { get; init; }
    public int? TotalInstallments { get; init; }
    public YearMonth StartMonth { get; init; }
    public OwnerRef Owner { get; init; } = OwnerRef.None;
    public bool Active { get; init; } = true;
    public bool Removed { get; init; }
    public IReadOnlyList<ScheduleMonth> Months { get; init; } = [];

    public InstallmentPurchaseState()
    {
        On<InstallmentEvents.V1.InstallmentPurchaseRegistered>((s, e) =>
        {
            var start = YearMonth.Parse(e.StartMonth);
            var count = e.Frequency == InstallmentFrequency.Monthly ? InstallmentDefaults.MonthlyRecurringWindow : e.TotalInstallments!.Value;
            return s with
            {
                InstallmentId = e.InstallmentId, FamilyId = e.FamilyId, CardId = e.CardId, Description = e.Description, Category = e.Category,
                PurchaseDate = e.PurchaseDate, Frequency = e.Frequency, MonthlyAmount = e.MonthlyAmount,
                TotalInstallments = e.TotalInstallments, StartMonth = start, Owner = OwnerRef.FromPrimitive(e.OwnerKind, e.OwnerPersonId),
                Active = true, Months = MonthlySchedule.Generate(start, count, e.MonthlyAmount),
            };
        });
        On<InstallmentEvents.V1.InstallmentScheduleRevised>((s, e) =>
        {
            var start = YearMonth.Parse(e.StartMonth);
            var count = e.Frequency == InstallmentFrequency.Monthly ? InstallmentDefaults.MonthlyRecurringWindow : e.TotalInstallments!.Value;
            return s with
            {
                StartMonth = start, TotalInstallments = e.TotalInstallments, Frequency = e.Frequency, MonthlyAmount = e.MonthlyAmount,
                Months = MonthlySchedule.Revise(s.Months, start, count, e.MonthlyAmount),
            };
        });
        On<InstallmentEvents.V1.InstallmentDetailsUpdated>((s, e) => s with
        {
            Description = e.Description, Category = e.Category, PurchaseDate = e.PurchaseDate,
            Owner = OwnerRef.FromPrimitive(e.OwnerKind, e.OwnerPersonId),
        });
        On<InstallmentEvents.V1.InstallmentMonthAmountOverridden>((s, e) =>
            s with { Months = MonthlySchedule.OverrideAmount(s.Months, YearMonth.Parse(e.Month), e.Amount) });
        On<InstallmentEvents.V1.InstallmentMonthPaidToggled>((s, e) =>
            s with { Months = MonthlySchedule.TogglePaid(s.Months, YearMonth.Parse(e.Month)) });
        On<InstallmentEvents.V1.InstallmentFinished>((s, _) => s with { Active = false });
        On<InstallmentEvents.V1.InstallmentRemoved>((s, _) => s with { Removed = true });
    }
}

public record RegisterInstallmentPurchase(
    Guid InstallmentId, Guid FamilyId, Guid CardId, string Description, string Category, string PurchaseDate,
    InstallmentFrequency Frequency, decimal MonthlyAmount, int? TotalInstallments, string StartMonth, OwnerRef Owner);

public record ReviseInstallmentSchedule(Guid InstallmentId, string StartMonth, int? TotalInstallments, InstallmentFrequency Frequency, decimal MonthlyAmount);
public record UpdateInstallmentDetails(Guid InstallmentId, string Description, string Category, string PurchaseDate, OwnerRef Owner);
public record OverrideInstallmentMonthAmount(Guid InstallmentId, string Month, decimal Amount);
public record ToggleInstallmentMonthPaid(Guid InstallmentId, string Month);
public record FinishInstallment(Guid InstallmentId);
public record RemoveInstallmentPurchase(Guid InstallmentId);

public sealed class InstallmentPurchaseCommandService : CommandService<InstallmentPurchaseState>
{
    public InstallmentPurchaseCommandService(IEventStore store) : base(store)
    {
        On<RegisterInstallmentPurchase>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.InstallmentId)).Act(Register);
        On<ReviseInstallmentSchedule>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.InstallmentId)).Act(Revise);
        On<UpdateInstallmentDetails>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.InstallmentId)).Act(UpdateDetails);
        On<OverrideInstallmentMonthAmount>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.InstallmentId)).Act(OverrideAmount);
        On<ToggleInstallmentMonthPaid>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.InstallmentId)).Act(TogglePaid);
        On<FinishInstallment>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.InstallmentId)).Act(Finish);
        On<RemoveInstallmentPurchase>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.InstallmentId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterInstallmentPurchase cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Description)) throw new DomainException("RegisterInstallmentPurchase: Description required.");
        if (!AppCategories.IsValidExpenseCategory(cmd.Category)) throw new DomainException($"RegisterInstallmentPurchase: unknown category '{cmd.Category}'.");
        if (cmd.MonthlyAmount <= 0) throw new DomainException("RegisterInstallmentPurchase: MonthlyAmount must be > 0.");
        ValidateFrequency(cmd.Frequency, cmd.TotalInstallments);

        yield return new InstallmentEvents.V1.InstallmentPurchaseRegistered(
            cmd.InstallmentId, cmd.FamilyId, cmd.CardId, cmd.Description.Trim(), cmd.Category, cmd.PurchaseDate,
            cmd.Frequency, cmd.MonthlyAmount, cmd.TotalInstallments, cmd.StartMonth, cmd.Owner.Kind, cmd.Owner.PersonId);
    }

    public static IEnumerable<object> Revise(InstallmentPurchaseState state, object[] _, ReviseInstallmentSchedule cmd)
    {
        GuardNotRemoved(state, "ReviseInstallmentSchedule");
        if (cmd.MonthlyAmount <= 0) throw new DomainException("ReviseInstallmentSchedule: MonthlyAmount must be > 0.");
        ValidateFrequency(cmd.Frequency, cmd.TotalInstallments);
        yield return new InstallmentEvents.V1.InstallmentScheduleRevised(cmd.StartMonth, cmd.TotalInstallments, cmd.Frequency, cmd.MonthlyAmount);
    }

    public static IEnumerable<object> UpdateDetails(InstallmentPurchaseState state, object[] _, UpdateInstallmentDetails cmd)
    {
        GuardNotRemoved(state, "UpdateInstallmentDetails");
        if (string.IsNullOrWhiteSpace(cmd.Description)) throw new DomainException("UpdateInstallmentDetails: Description required.");
        if (!AppCategories.IsValidExpenseCategory(cmd.Category)) throw new DomainException($"UpdateInstallmentDetails: unknown category '{cmd.Category}'.");
        yield return new InstallmentEvents.V1.InstallmentDetailsUpdated(cmd.Description.Trim(), cmd.Category, cmd.PurchaseDate, cmd.Owner.Kind, cmd.Owner.PersonId);
    }

    public static IEnumerable<object> OverrideAmount(InstallmentPurchaseState state, object[] _, OverrideInstallmentMonthAmount cmd)
    {
        GuardNotRemoved(state, "OverrideInstallmentMonthAmount");
        if (cmd.Amount < 0) throw new DomainException("OverrideInstallmentMonthAmount: Amount cannot be negative.");
        yield return new InstallmentEvents.V1.InstallmentMonthAmountOverridden(cmd.Month, cmd.Amount);
    }

    public static IEnumerable<object> TogglePaid(InstallmentPurchaseState state, object[] _, ToggleInstallmentMonthPaid cmd)
    {
        GuardNotRemoved(state, "ToggleInstallmentMonthPaid");
        yield return new InstallmentEvents.V1.InstallmentMonthPaidToggled(cmd.Month);
    }

    public static IEnumerable<object> Finish(InstallmentPurchaseState state, object[] _, FinishInstallment cmd)
    {
        GuardNotRemoved(state, "FinishInstallment");
        if (!state.Active) throw new DomainException("FinishInstallment: installment is already finished.");
        yield return new InstallmentEvents.V1.InstallmentFinished();
    }

    public static IEnumerable<object> Remove(InstallmentPurchaseState state, object[] _, RemoveInstallmentPurchase cmd)
    {
        GuardNotRemoved(state, "RemoveInstallmentPurchase");
        yield return new InstallmentEvents.V1.InstallmentRemoved();
    }

    static void ValidateFrequency(InstallmentFrequency frequency, int? totalInstallments)
    {
        if (frequency == InstallmentFrequency.Fixed && totalInstallments is not (> 0 and <= 120))
            throw new DomainException("Installment: TotalInstallments required (1-120) when Frequency is Fixed.");
    }

    static void GuardNotRemoved(InstallmentPurchaseState state, string command)
    {
        if (state.Removed) throw new DomainException($"{command}: installment was removed.");
    }

    static StreamName Stream(Guid id) => new($"installment-{id}");
}
