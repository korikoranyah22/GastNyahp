using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Services;

public enum BillingType { Monthly, Bimonthly, Quarterly }
public enum ServiceCurrency { Ars, Usd }

public readonly record struct ServiceMonthAmount(YearMonth Month, decimal AmountArs, bool Paid);

public static class ServiceEvents
{
    public static class V1
    {
        [EventType("V1.ServiceRegistered")]
        public record ServiceRegistered(
            Guid ServiceId, Guid FamilyId, string Name, string Category, BillingType BillingType, Guid? LinkedCardId,
            ServiceCurrency Currency, decimal BaseAmountArs, decimal? OriginalAmount, ServiceCurrency? OriginalCurrency,
            string OwnerKind, Guid? OwnerPersonId, string RegisteredFromMonth);

        [EventType("V1.ServiceDetailsUpdated")]
        public record ServiceDetailsUpdated(string Name, string Category, BillingType BillingType, Guid? LinkedCardId, ServiceCurrency Currency, string? OwnerKind = null, Guid? OwnerPersonId = null);

        [EventType("V1.ServiceActivated")]
        public record ServiceActivated;

        [EventType("V1.ServiceDeactivated")]
        public record ServiceDeactivated;

        [EventType("V1.ServiceMonthAmountSet")]
        public record ServiceMonthAmountSet(string Month, decimal AmountArs, decimal? OriginalAmount, ServiceCurrency? OriginalCurrency);

        [EventType("V1.ServiceFutureAmountsExtended")]
        public record ServiceFutureAmountsExtended(string FromMonth, decimal AmountArs, int MonthsAhead);

        [EventType("V1.ServiceMonthPaidToggled")]
        public record ServiceMonthPaidToggled(string Month);

        [EventType("V1.ServiceRemoved")]
        public record ServiceRemoved;
    }
}

public record ServiceState : State<ServiceState>
{
    public Guid ServiceId { get; init; }
    public Guid FamilyId { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public BillingType BillingType { get; init; }
    public Guid? LinkedCardId { get; init; }
    public bool Active { get; init; } = true;
    public ServiceCurrency Currency { get; init; }
    public decimal? OriginalAmount { get; init; }
    public ServiceCurrency? OriginalCurrency { get; init; }
    public OwnerRef Owner { get; init; } = OwnerRef.None;
    public bool Removed { get; init; }
    public IReadOnlyList<ServiceMonthAmount> Amounts { get; init; } = [];

    public ServiceState()
    {
        On<ServiceEvents.V1.ServiceRegistered>((s, e) => s with
        {
            ServiceId = e.ServiceId, FamilyId = e.FamilyId, Name = e.Name, Category = e.Category, BillingType = e.BillingType,
            LinkedCardId = e.LinkedCardId, Currency = e.Currency, OriginalAmount = e.OriginalAmount,
            OriginalCurrency = e.OriginalCurrency, Owner = OwnerRef.FromPrimitive(e.OwnerKind, e.OwnerPersonId), Active = true,
            Amounts = YearMonth.Parse(e.RegisteredFromMonth).Take(12).Select(m => new ServiceMonthAmount(m, e.BaseAmountArs, false)).ToList(),
        });
        On<ServiceEvents.V1.ServiceDetailsUpdated>((s, e) => s with
        {
            Name = e.Name, Category = e.Category, BillingType = e.BillingType, LinkedCardId = e.LinkedCardId, Currency = e.Currency,
            Owner = e.OwnerKind is null ? s.Owner : OwnerRef.FromPrimitive(e.OwnerKind, e.OwnerPersonId),
        });
        On<ServiceEvents.V1.ServiceActivated>((s, _) => s with { Active = true });
        On<ServiceEvents.V1.ServiceDeactivated>((s, _) => s with { Active = false });
        On<ServiceEvents.V1.ServiceMonthAmountSet>((s, e) => s with
        {
            OriginalAmount = e.OriginalAmount, OriginalCurrency = e.OriginalCurrency,
            Amounts = Upsert(s.Amounts, YearMonth.Parse(e.Month), e.AmountArs),
        });
        On<ServiceEvents.V1.ServiceFutureAmountsExtended>((s, e) =>
        {
            var months = YearMonth.Parse(e.FromMonth).Take(e.MonthsAhead);
            var amounts = s.Amounts;
            foreach (var m in months) amounts = Upsert(amounts, m, e.AmountArs);
            return s with { Amounts = amounts };
        });
        On<ServiceEvents.V1.ServiceMonthPaidToggled>((s, e) =>
        {
            var month = YearMonth.Parse(e.Month);
            var existing = s.Amounts.FirstOrDefault(a => a.Month == month);
            var amounts = s.Amounts.Any(a => a.Month == month)
                ? s.Amounts.Select(a => a.Month == month ? a with { Paid = !a.Paid } : a).ToList()
                : [.. s.Amounts, new ServiceMonthAmount(month, 0, true)];
            return s with { Amounts = amounts };
        });
        On<ServiceEvents.V1.ServiceRemoved>((s, _) => s with { Removed = true });
    }

    static IReadOnlyList<ServiceMonthAmount> Upsert(IReadOnlyList<ServiceMonthAmount> amounts, YearMonth month, decimal amountArs)
    {
        var list = amounts.Any(a => a.Month == month)
            ? amounts.Select(a => a.Month == month ? a with { AmountArs = amountArs } : a).ToList()
            : [.. amounts, new ServiceMonthAmount(month, amountArs, false)];
        return list.OrderBy(a => a.Month).ToList();
    }
}

public record RegisterService(
    Guid ServiceId, Guid FamilyId, string Name, string Category, BillingType BillingType, Guid? LinkedCardId,
    ServiceCurrency Currency, decimal BaseAmount, string RegisteredFromMonth, OwnerRef Owner, decimal UsdRateCcl);

public record UpdateServiceDetails(Guid ServiceId, string Name, string Category, BillingType BillingType, Guid? LinkedCardId, ServiceCurrency Currency, OwnerRef? Owner = null);
public record ActivateService(Guid ServiceId);
public record DeactivateService(Guid ServiceId);
public record SetServiceMonthAmount(Guid ServiceId, string Month, decimal Amount, ServiceCurrency Currency, decimal UsdRateCcl);
public record ExtendServiceFutureAmounts(Guid ServiceId, string FromMonth, decimal AmountArs, int MonthsAhead);
public record ToggleServiceMonthPaid(Guid ServiceId, string Month);
public record RemoveService(Guid ServiceId);

public sealed class ServiceCommandService : CommandService<ServiceState>
{
    public ServiceCommandService(IEventStore store) : base(store)
    {
        On<RegisterService>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(Register);
        On<UpdateServiceDetails>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(UpdateDetails);
        On<ActivateService>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(Activate);
        On<DeactivateService>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(Deactivate);
        On<SetServiceMonthAmount>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(SetMonthAmount);
        On<ExtendServiceFutureAmounts>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(ExtendFutureAmounts);
        On<ToggleServiceMonthPaid>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(TogglePaid);
        On<RemoveService>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.ServiceId)).Act(Remove);
    }

    public static IEnumerable<object> Register(RegisterService cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new DomainException("RegisterService: Name required.");
        if (!AppCategories.IsValidServiceCategory(cmd.Category)) throw new DomainException($"RegisterService: unknown category '{cmd.Category}'.");
        var (amountArs, originalAmount, originalCurrency) = ConvertToArs(cmd.BaseAmount, cmd.Currency, cmd.UsdRateCcl);

        yield return new ServiceEvents.V1.ServiceRegistered(
            cmd.ServiceId, cmd.FamilyId, cmd.Name.Trim(), cmd.Category, cmd.BillingType, cmd.LinkedCardId, cmd.Currency,
            amountArs, originalAmount, originalCurrency, cmd.Owner.Kind, cmd.Owner.PersonId, cmd.RegisteredFromMonth);
    }

    public static IEnumerable<object> UpdateDetails(ServiceState state, object[] _, UpdateServiceDetails cmd)
    {
        GuardNotRemoved(state, "UpdateServiceDetails");
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new DomainException("UpdateServiceDetails: Name required.");
        if (!AppCategories.IsValidServiceCategory(cmd.Category)) throw new DomainException($"UpdateServiceDetails: unknown category '{cmd.Category}'.");
        yield return new ServiceEvents.V1.ServiceDetailsUpdated(cmd.Name.Trim(), cmd.Category, cmd.BillingType, cmd.LinkedCardId, cmd.Currency, cmd.Owner?.Kind, cmd.Owner?.PersonId);
    }

    public static IEnumerable<object> Activate(ServiceState state, object[] _, ActivateService cmd)
    {
        GuardNotRemoved(state, "ActivateService");
        if (state.Active) throw new DomainException("ActivateService: service is already active.");
        yield return new ServiceEvents.V1.ServiceActivated();
    }

    public static IEnumerable<object> Deactivate(ServiceState state, object[] _, DeactivateService cmd)
    {
        GuardNotRemoved(state, "DeactivateService");
        if (!state.Active) throw new DomainException("DeactivateService: service is already inactive.");
        yield return new ServiceEvents.V1.ServiceDeactivated();
    }

    public static IEnumerable<object> SetMonthAmount(ServiceState state, object[] _, SetServiceMonthAmount cmd)
    {
        GuardNotRemoved(state, "SetServiceMonthAmount");
        var (amountArs, originalAmount, originalCurrency) = ConvertToArs(cmd.Amount, cmd.Currency, cmd.UsdRateCcl);
        yield return new ServiceEvents.V1.ServiceMonthAmountSet(cmd.Month, amountArs, originalAmount, originalCurrency);
    }

    public static IEnumerable<object> ExtendFutureAmounts(ServiceState state, object[] _, ExtendServiceFutureAmounts cmd)
    {
        GuardNotRemoved(state, "ExtendServiceFutureAmounts");
        if (cmd.MonthsAhead <= 0) throw new DomainException("ExtendServiceFutureAmounts: MonthsAhead must be > 0.");
        yield return new ServiceEvents.V1.ServiceFutureAmountsExtended(cmd.FromMonth, cmd.AmountArs, cmd.MonthsAhead);
    }

    public static IEnumerable<object> TogglePaid(ServiceState state, object[] _, ToggleServiceMonthPaid cmd)
    {
        GuardNotRemoved(state, "ToggleServiceMonthPaid");
        yield return new ServiceEvents.V1.ServiceMonthPaidToggled(cmd.Month);
    }

    public static IEnumerable<object> Remove(ServiceState state, object[] _, RemoveService cmd)
    {
        GuardNotRemoved(state, "RemoveService");
        yield return new ServiceEvents.V1.ServiceRemoved();
    }

    /// <summary>USD amounts are always converted and persisted in ARS (DOMAIN_MODEL.md §6) — the original
    /// value/currency is kept alongside so the UI can re-edit in USD later.</summary>
    static (decimal AmountArs, decimal? OriginalAmount, ServiceCurrency? OriginalCurrency) ConvertToArs(decimal amount, ServiceCurrency currency, decimal usdRateCcl)
    {
        if (currency == ServiceCurrency.Ars) return (amount, null, null);
        if (usdRateCcl <= 0) throw new DomainException("Service: UsdRateCcl must be configured (> 0) to register a USD amount.");
        return (Math.Round(amount * usdRateCcl), amount, currency);
    }

    static void GuardNotRemoved(ServiceState state, string command)
    {
        if (state.Removed) throw new DomainException($"{command}: service was removed.");
    }

    static StreamName Stream(Guid id) => new($"service-{id}");
}
