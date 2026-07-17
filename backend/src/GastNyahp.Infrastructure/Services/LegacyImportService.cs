using GastNyahp.Domain.Cards;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Domain.Installments;
using GastNyahp.Domain.Reserves;
using GastNyahp.Domain.Services;
using GastNyahp.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

// ── El shape del export de la maqueta (exportData() de useStore.js, version 1.x) ──
// Campos extra del JSON (_updatedAt, meta, etc.) se ignoran al deserializar. Todos opcionales: los exports
// viejos pueden no traer todas las secciones.
public class LegacyData
{
    public List<LegacyBank>? Banks { get; set; }
    public List<LegacyCard>? CreditCards { get; set; }
    public List<LegacyInstallment>? Installments { get; set; }
    public List<LegacyLoan>? Loans { get; set; }
    public List<LegacyService>? Services { get; set; }
    public List<LegacyExpense>? Expenses { get; set; }
    public Dictionary<string, LegacyBudget>? Budgets { get; set; }
    public List<LegacyFixedExpense>? FixedExpenses { get; set; }
    public LegacyIncome? Income { get; set; }
    public List<LegacyPerson>? People { get; set; }
}

public class LegacyBank { public string? Id { get; set; } public string? Name { get; set; } public string? Alias { get; set; } public string? Color { get; set; } public string? Icon { get; set; } }
public class LegacyCard { public string? Id { get; set; } public string? BankId { get; set; } public string? Label { get; set; } public string? Network { get; set; } public string? Type { get; set; } public int? ClosingDay { get; set; } public int? DueDay { get; set; } public string? Color { get; set; } public bool? Active { get; set; } }
public class LegacyMonth { public string? Month { get; set; } public decimal? Amount { get; set; } public bool? Paid { get; set; } public string? Note { get; set; } }
public class LegacyInstallment { public string? Id { get; set; } public string? CardId { get; set; } public string? Description { get; set; } public string? Category { get; set; } public string? PurchaseDate { get; set; } public string? Frequency { get; set; } public decimal? MonthlyAmount { get; set; } public int? TotalInstallments { get; set; } public string? StartMonth { get; set; } public bool? Active { get; set; } public string? OwnerId { get; set; } public List<LegacyMonth>? Months { get; set; } }
public class LegacyLoan { public string? Id { get; set; } public string? BankId { get; set; } public string? Description { get; set; } public decimal? TotalAmount { get; set; } public decimal? MonthlyInstallment { get; set; } public string? StartDate { get; set; } public int? TotalInstallments { get; set; } public List<LegacyMonth>? Months { get; set; } }
public class LegacyService { public string? Id { get; set; } public string? Name { get; set; } public string? Category { get; set; } public string? BillingType { get; set; } public string? LinkedCardId { get; set; } public bool? Active { get; set; } public string? OwnerId { get; set; } public List<LegacyMonth>? Amounts { get; set; } }
public class LegacyTicketItem { public string? Description { get; set; } public decimal? Amount { get; set; } public string? Category { get; set; } public string? OwnerId { get; set; } }
public class LegacyExpense { public string? Id { get; set; } public string? Type { get; set; } public string? Date { get; set; } public string? Description { get; set; } public string? Category { get; set; } public decimal? Amount { get; set; } public string? PaymentMethod { get; set; } public string? OwnerId { get; set; } public decimal? Discount { get; set; } public List<LegacyTicketItem>? Items { get; set; } }
public class LegacyBudget { public decimal? CreditLimit { get; set; } public decimal? DebitCashLimit { get; set; } public decimal? WeeklyLimit { get; set; } }
public class LegacyFixedExpense { public string? Id { get; set; } public string? Label { get; set; } public string? Type { get; set; } public string? Icon { get; set; } public bool? Recurring { get; set; } public decimal? BaseAmount { get; set; } public List<LegacyMonth>? Months { get; set; } }
public class LegacyIncome { public decimal? NetMonthly { get; set; } public decimal? UsdRateOfficial { get; set; } public decimal? UsdRateCCL { get; set; } public int? SplitPercent { get; set; } }
public class LegacyPerson { public string? Id { get; set; } public string? Name { get; set; } public string? Emoji { get; set; } public string? Color { get; set; } }

public record ImportSummary(
    int People, int Banks, int Cards, int Installments, int Loans, int Services,
    int Reserves, int Expenses, int Tickets, int Budgets, bool Income, int Removed, IReadOnlyList<string> Warnings);

/// <summary>
/// Importa el JSON de la maqueta reproduciendo la carga manual como COMANDOS de los mismos application
/// services que usa la UI — todos los guards corren y el event store queda auditable, como si alguien
/// hubiera tipeado todo hasta el punto de guardado del archivo. Los ids string legacy se remapean a los
/// GUIDs nuevos sobre la marcha. Aditivo: se recomienda sobre una familia recién creada (ver guard).
/// </summary>
public class LegacyImportService(
    PersonService peopleService,
    BankService bankService,
    CardService cardService,
    InstallmentService installmentService,
    LoanService loanService,
    ServicesService servicesService,
    ReserveService reserveService,
    ExpenseService expenseService,
    TicketService ticketService,
    PlanningService planningService,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    ILogger<LegacyImportService> logger)
{
    public async Task<(OpResult Result, ImportSummary? Summary)> ImportAsync(
        Guid familyId, LegacyData data, bool force, bool replace = false, Action<ImportProgress>? progress = null, CancellationToken ct = default)
    {
        if (!replace && !force && await FamilyHasDataAsync(familyId, ct))
            return (OpResult.Fail("La familia ya tiene datos. Importá sobre una familia recién creada, o repetí con force=true para agregar igual."), null);

        var warnings = new List<string>();
        var idMap = new Dictionary<string, Guid>(); // id legacy (string) → GUID nuevo
        int people = 0, banks = 0, cards = 0, installments = 0, loans = 0, services = 0, reserves = 0, expenses = 0, tickets = 0, budgets = 0;
        var incomeImported = false;
        int secDone = 0, secTotal = 0; // avance por sección, para el polling de la UI
        void Report(string section) => progress?.Invoke(new ImportProgress(section, secDone++, secTotal));

        // ── 0. Reemplazo: "borrar" = emitir los Remove/Archive de todo lo existente — el event store es
        // append-only, así que el reemplazo entero queda auditado como cualquier otra operación.
        var removed = 0;
        if (replace) removed = await WipeFamilyDataAsync(familyId, progress, warnings, ct);

        OwnerRef MapOwner(string? ownerId)
        {
            if (string.IsNullOrEmpty(ownerId)) return OwnerRef.None;
            if (ownerId == "shared") return OwnerRef.SharedOwner;
            if (idMap.TryGetValue(ownerId, out var personId)) return OwnerRef.Of(personId);
            warnings.Add($"ownerId '{ownerId}' no corresponde a ninguna persona importada — quedó sin asignar.");
            return OwnerRef.None;
        }

        PaymentMethod MapPayment(string? pm, string context)
        {
            if (pm == "cash" || string.IsNullOrEmpty(pm)) return PaymentMethod.CashPayment;
            if (pm == "modo") return PaymentMethod.ModoPayment;
            if (pm == "mercadopago") return PaymentMethod.MercadoPagoPayment;
            if (pm.StartsWith("debit-", StringComparison.Ordinal))
            {
                if (idMap.TryGetValue(pm["debit-".Length..], out var bankId)) return PaymentMethod.ByDebit(bankId);
                warnings.Add($"{context}: banco de débito '{pm}' desconocido — importado como Efectivo.");
                return PaymentMethod.CashPayment;
            }
            if (idMap.TryGetValue(pm, out var cardId)) return PaymentMethod.ByCard(cardId);
            warnings.Add($"{context}: medio de pago '{pm}' desconocido — importado como Efectivo.");
            return PaymentMethod.CashPayment;
        }

        string MapCategory(string? category, string context)
        {
            if (category is not null && AppCategories.IsValidExpenseCategory(category)) return category;
            warnings.Add($"{context}: categoría '{category}' desconocida — importada como Desconocido.");
            return "Desconocido";
        }

        static bool ValidDate(string? d) => d is not null && System.Text.RegularExpressions.Regex.IsMatch(d, @"^\d{4}-\d{2}-\d{2}$");
        static bool ValidMonth(string? m) => m is not null && System.Text.RegularExpressions.Regex.IsMatch(m, @"^\d{4}-\d{2}$");

        // ── 1. Personas (las referencian los ownerId de todo lo demás) ─────────
        (secDone, secTotal) = (0, data.People?.Count ?? 0);
        foreach (var p in data.People ?? [])
        {
            Report("Personas");
            var result = await peopleService.RegisterAsync(familyId, p.Name ?? "", p.Emoji ?? "😀", p.Color ?? "#64748b", ct);
            if (result.Ok && p.Id is not null) { idMap[p.Id] = result.Id!.Value; people++; }
            else if (!result.Ok) warnings.Add($"Persona '{p.Name}': {result.Error}");
        }

        // ── 2. Bancos ───────────────────────────────────────────────────────────
        (secDone, secTotal) = (0, data.Banks?.Count ?? 0);
        foreach (var b in data.Banks ?? [])
        {
            Report("Bancos");
            var result = await bankService.RegisterAsync(familyId, b.Name ?? "", b.Alias, b.Color ?? "#004B9B", b.Icon ?? "building-2", ct);
            if (result.Ok && b.Id is not null) { idMap[b.Id] = result.Id!.Value; banks++; }
            else if (!result.Ok) warnings.Add($"Banco '{b.Name}': {result.Error}");
        }

        // ── 3. Tarjetas ─────────────────────────────────────────────────────────
        (secDone, secTotal) = (0, data.CreditCards?.Count ?? 0);
        foreach (var c in data.CreditCards ?? [])
        {
            Report("Tarjetas");
            if (c.BankId is null || !idMap.TryGetValue(c.BankId, out var bankGuid))
            {
                warnings.Add($"Tarjeta '{c.Label}': banco '{c.BankId}' desconocido — omitida.");
                continue;
            }
            var network = string.Equals(c.Network, "MASTERCARD", StringComparison.OrdinalIgnoreCase) ? CardNetwork.Mastercard : CardNetwork.Visa;
            var type = string.Equals(c.Type, "debit", StringComparison.OrdinalIgnoreCase) ? CardType.Debit : CardType.Credit;
            var result = await cardService.RegisterAsync(familyId, bankGuid, c.Label ?? "", network, type, c.ClosingDay ?? 15, c.DueDay ?? 5, c.Color ?? "#3b82f6", ct);
            if (result.Ok && c.Id is not null)
            {
                idMap[c.Id] = result.Id!.Value;
                cards++;
                if (c.Active == false) await cardService.DeactivateAsync(familyId, result.Id!.Value, ct);
            }
            else if (!result.Ok) warnings.Add($"Tarjeta '{c.Label}': {result.Error}");
        }

        // ── 4. Cuotas: registrar y reproducir pagos/overrides mes a mes ─────────
        (secDone, secTotal) = (0, data.Installments?.Count ?? 0);
        foreach (var i in data.Installments ?? [])
        {
            Report("Cuotas");
            if (i.CardId is null || !idMap.TryGetValue(i.CardId, out var cardGuid))
            {
                warnings.Add($"Cuota '{i.Description}': tarjeta '{i.CardId}' desconocida — omitida.");
                continue;
            }
            var months = i.Months ?? [];
            var startMonth = ValidMonth(i.StartMonth) ? i.StartMonth! : months.FirstOrDefault()?.Month;
            if (!ValidMonth(startMonth)) { warnings.Add($"Cuota '{i.Description}': sin mes de inicio — omitida."); continue; }

            var isMonthly = string.Equals(i.Frequency, "monthly", StringComparison.OrdinalIgnoreCase);
            var total = isMonthly ? (int?)null : (i.TotalInstallments ?? months.Count);
            var monthlyAmount = i.MonthlyAmount ?? months.FirstOrDefault()?.Amount ?? 0;

            var result = await installmentService.RegisterAsync(
                familyId, cardGuid, i.Description ?? "", MapCategory(i.Category, $"Cuota '{i.Description}'"),
                ValidDate(i.PurchaseDate) ? i.PurchaseDate! : $"{startMonth}-01",
                isMonthly ? InstallmentFrequency.Monthly : InstallmentFrequency.Fixed,
                monthlyAmount, total, startMonth!, MapOwner(i.OwnerId), ct);
            if (!result.Ok) { warnings.Add($"Cuota '{i.Description}': {result.Error}"); continue; }

            var newId = result.Id!.Value;
            installments++;
            // Solo meses dentro del calendario generado (el resto sería un no-op silencioso).
            var window = YearMonth.Parse(startMonth!).Take(isMonthly ? InstallmentDefaults.MonthlyRecurringWindow : total!.Value)
                .Select(m => m.ToString()).ToHashSet();
            foreach (var m in months.Where(m => m.Month is not null && window.Contains(m.Month)))
            {
                if (m.Amount is not null && m.Amount != monthlyAmount)
                    await installmentService.OverrideMonthAmountAsync(familyId, newId, m.Month!, m.Amount.Value, ct);
                if (m.Paid == true)
                    await installmentService.ToggleMonthPaidAsync(familyId, newId, m.Month!, ct);
            }
            if (i.Active == false) await installmentService.FinishAsync(familyId, newId, ct);
        }

        // ── 5. Préstamos ────────────────────────────────────────────────────────
        (secDone, secTotal) = (0, data.Loans?.Count ?? 0);
        foreach (var l in data.Loans ?? [])
        {
            Report("Préstamos");
            if (l.BankId is null || !idMap.TryGetValue(l.BankId, out var bankGuid))
            {
                warnings.Add($"Préstamo '{l.Description}': banco '{l.BankId}' desconocido — omitido.");
                continue;
            }
            var months = l.Months ?? [];
            var startMonth = ValidDate(l.StartDate) ? l.StartDate![..7] : months.FirstOrDefault()?.Month;
            if (!ValidMonth(startMonth)) { warnings.Add($"Préstamo '{l.Description}': sin mes de inicio — omitido."); continue; }

            var total = l.TotalInstallments ?? months.Count;
            var monthlyAmount = l.MonthlyInstallment ?? months.FirstOrDefault()?.Amount ?? 0;

            var result = await loanService.RegisterAsync(familyId, bankGuid, l.Description ?? "", l.TotalAmount, monthlyAmount, startMonth!, total, ct);
            if (!result.Ok) { warnings.Add($"Préstamo '{l.Description}': {result.Error}"); continue; }

            var newId = result.Id!.Value;
            loans++;
            var window = YearMonth.Parse(startMonth!).Take(total).Select(m => m.ToString()).ToHashSet();
            foreach (var m in months.Where(m => m.Month is not null && window.Contains(m.Month)))
            {
                if (m.Amount is not null && m.Amount != monthlyAmount)
                    await loanService.OverrideMonthAmountAsync(familyId, newId, m.Month!, m.Amount.Value, ct);
                if (m.Paid == true)
                    await loanService.ToggleMonthPaidAsync(familyId, newId, m.Month!, ct);
            }
        }

        // ── 6. Servicios: registrar y reproducir el histórico mes a mes ─────────
        (secDone, secTotal) = (0, data.Services?.Count ?? 0);
        foreach (var s in data.Services ?? [])
        {
            Report("Servicios");
            Guid? linkedCard = null;
            if (!string.IsNullOrEmpty(s.LinkedCardId))
            {
                if (idMap.TryGetValue(s.LinkedCardId, out var cardGuid)) linkedCard = cardGuid;
                else warnings.Add($"Servicio '{s.Name}': tarjeta vinculada '{s.LinkedCardId}' desconocida — importado sin vínculo.");
            }
            var amounts = (s.Amounts ?? []).Where(a => ValidMonth(a.Month)).ToList();
            var firstMonth = amounts.FirstOrDefault()?.Month ?? DateTime.UtcNow.ToString("yyyy-MM");
            var billingType = string.Equals(s.BillingType, "bimonthly", StringComparison.OrdinalIgnoreCase) ? BillingType.Bimonthly
                : string.Equals(s.BillingType, "quarterly", StringComparison.OrdinalIgnoreCase) ? BillingType.Quarterly : BillingType.Monthly;

            var result = await servicesService.RegisterAsync(
                familyId, s.Name ?? "", AppCategories.IsValidServiceCategory(s.Category ?? "") ? s.Category! : "Otros",
                billingType, linkedCard, ServiceCurrency.Ars, amounts.FirstOrDefault()?.Amount ?? 0, firstMonth,
                MapOwner(s.OwnerId), ct);
            if (!result.Ok) { warnings.Add($"Servicio '{s.Name}': {result.Error}"); continue; }

            var newId = result.Id!.Value;
            services++;
            foreach (var a in amounts)
            {
                if (a.Amount is not null)
                    await servicesService.SetMonthAmountAsync(familyId, newId, a.Month!, a.Amount.Value, ServiceCurrency.Ars, ct);
                if (a.Paid == true)
                    await servicesService.ToggleMonthPaidAsync(familyId, newId, a.Month!, ct);
            }
            if (s.Active == false) await servicesService.DeactivateAsync(familyId, newId, ct);
        }

        // ── 7. Reservas (gastos fijos) ──────────────────────────────────────────
        (secDone, secTotal) = (0, data.FixedExpenses?.Count ?? 0);
        foreach (var f in data.FixedExpenses ?? [])
        {
            Report("Reservas");
            var type = f.Type?.ToLowerInvariant() switch
            {
                "cash" => ReserveType.Cash,
                "debt" => ReserveType.Debt,
                "other" => ReserveType.Other,
                _ => ReserveType.Reserve, // 'reserve' y el legacy 'person'
            };
            var result = await reserveService.RegisterAsync(familyId, f.Label ?? "", type, f.Icon ?? "👤", f.Recurring == true, f.BaseAmount ?? 0, ct);
            if (!result.Ok) { warnings.Add($"Reserva '{f.Label}': {result.Error}"); continue; }

            reserves++;
            foreach (var m in (f.Months ?? []).Where(m => ValidMonth(m.Month) && m.Amount is not null))
                await reserveService.SetMonthAmountAsync(familyId, result.Id!.Value, m.Month!, m.Amount!.Value, m.Note, ct);
        }

        // ── 8. Gastos y tickets ─────────────────────────────────────────────────
        (secDone, secTotal) = (0, data.Expenses?.Count ?? 0);
        foreach (var e in data.Expenses ?? [])
        {
            Report("Movimientos");
            if (!ValidDate(e.Date)) { warnings.Add($"Gasto '{e.Description}': fecha inválida — omitido."); continue; }

            if (e.Type == "ticket")
            {
                var items = (e.Items ?? [])
                    .Where(it => !string.IsNullOrWhiteSpace(it.Description) && it.Amount > 0)
                    .Select(it => new TicketItemInput(Guid.NewGuid(), it.Description!, it.Amount!.Value,
                        MapCategory(it.Category, $"Ticket '{e.Description}'"), MapOwner(it.OwnerId).Kind, MapOwner(it.OwnerId).PersonId))
                    .ToList();
                if (items.Count == 0) { warnings.Add($"Ticket '{e.Description}': sin ítems válidos — omitido."); continue; }

                var result = await ticketService.RegisterAsync(familyId, e.Date!, e.Description ?? "",
                    MapPayment(e.PaymentMethod, $"Ticket '{e.Description}'"), e.Discount ?? 0, items, ct);
                if (result.Ok) tickets++; else warnings.Add($"Ticket '{e.Description}': {result.Error}");
            }
            else
            {
                if ((e.Amount ?? 0) <= 0) { warnings.Add($"Gasto '{e.Description}': monto inválido — omitido."); continue; }
                var result = await expenseService.RegisterAsync(familyId, e.Date!, e.Description ?? "",
                    MapCategory(e.Category, $"Gasto '{e.Description}'"), e.Amount!.Value, ExpenseCurrency.Ars,
                    MapPayment(e.PaymentMethod, $"Gasto '{e.Description}'"), MapOwner(e.OwnerId), ct);
                if (result.Ok) expenses++; else warnings.Add($"Gasto '{e.Description}': {result.Error}");
            }
        }

        // ── 9. Presupuestos e ingresos ──────────────────────────────────────────
        (secDone, secTotal) = (0, data.Budgets?.Count ?? 0);
        foreach (var (month, budget) in data.Budgets ?? [])
        {
            Report("Presupuestos");
            if (!ValidMonth(month)) { warnings.Add($"Presupuesto '{month}': mes inválido — omitido."); continue; }
            var result = await planningService.SetBudgetAsync(familyId, month, budget.CreditLimit, budget.DebitCashLimit, budget.WeeklyLimit, ct);
            if (result.Ok) budgets++; else warnings.Add($"Presupuesto '{month}': {result.Error}");
        }

        if (data.Income is not null)
        {
            (secDone, secTotal) = (0, 1);
            Report("Ingresos");
            var result = await planningService.UpdateIncomeAsync(familyId,
                data.Income.NetMonthly, data.Income.UsdRateOfficial, data.Income.UsdRateCCL, data.Income.SplitPercent, ct);
            incomeImported = result.Ok;
            if (!result.Ok) warnings.Add($"Ingresos: {result.Error}");
        }

        logger.LogInformation("[Import] Familia {FamilyId}: {Banks} bancos, {Cards} tarjetas, {Installments} cuotas, {Expenses} gastos, {Removed} reemplazados, {Warnings} advertencias",
            familyId, banks, cards, installments, expenses, removed, warnings.Count);

        return (OpResult.Success(), new ImportSummary(people, banks, cards, installments, loans, services, reserves, expenses, tickets, budgets, incomeImported, removed, warnings));
    }

    /// <summary>
    /// Emite los comandos terminales de TODO lo que la familia tiene, en el orden que satisface los guards
    /// cross-aggregate (cuotas y servicios liberan tarjetas; tarjetas y préstamos liberan bancos). Personas se
    /// archivan (§5 del modelo: nunca se borran), presupuestos quedan en cero e ingresos vuelven al default.
    /// Cada Remove es un evento más en el stream — el "borrar todo" completo queda auditable.
    /// </summary>
    async Task<int> WipeFamilyDataAsync(Guid familyId, Action<ImportProgress>? progress, List<string> warnings, CancellationToken ct)
    {
        List<Guid> expenseIds, ticketIds, installmentIds, serviceIds, cardIds, loanIds, bankIds, reserveIds, personIds;
        List<string> budgetMonths;
        bool hasIncome;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            expenseIds = await db.Expenses.Where(e => e.FamilyId == familyId).Select(e => e.Id).ToListAsync(ct);
            ticketIds = await db.Tickets.Where(t => t.FamilyId == familyId).Select(t => t.Id).ToListAsync(ct);
            installmentIds = await db.InstallmentPurchases.Where(i => i.FamilyId == familyId).Select(i => i.Id).ToListAsync(ct);
            serviceIds = await db.Services.Where(s => s.FamilyId == familyId).Select(s => s.Id).ToListAsync(ct);
            cardIds = await db.CreditCards.Where(c => c.FamilyId == familyId).Select(c => c.Id).ToListAsync(ct);
            loanIds = await db.Loans.Where(l => l.FamilyId == familyId).Select(l => l.Id).ToListAsync(ct);
            bankIds = await db.Banks.Where(b => b.FamilyId == familyId).Select(b => b.Id).ToListAsync(ct);
            reserveIds = await db.Reserves.Where(r => r.FamilyId == familyId).Select(r => r.Id).ToListAsync(ct);
            personIds = await db.People.Where(p => p.FamilyId == familyId && !p.Archived).Select(p => p.Id).ToListAsync(ct);
            budgetMonths = await db.BudgetPlans.Where(b => b.FamilyId == familyId).Select(b => b.Month).ToListAsync(ct);
            hasIncome = await db.Income.AnyAsync(i => i.FamilyId == familyId, ct);
        }

        var removed = 0;
        var done = 0;
        var total = expenseIds.Count + ticketIds.Count + installmentIds.Count + serviceIds.Count + cardIds.Count
                  + loanIds.Count + bankIds.Count + reserveIds.Count + personIds.Count + budgetMonths.Count + (hasIncome ? 1 : 0);

        async Task Run(string what, Guid id, Func<Task<OpResult>> op)
        {
            progress?.Invoke(new ImportProgress("Limpiando datos anteriores", done++, total));
            var result = await op();
            if (result.Ok) removed++;
            else warnings.Add($"Reemplazo: no se pudo eliminar {what} {id}: {result.Error}");
        }

        foreach (var id in expenseIds) await Run("el gasto", id, () => expenseService.RemoveAsync(familyId, id, ct));
        foreach (var id in ticketIds) await Run("el ticket", id, () => ticketService.RemoveAsync(familyId, id, ct));
        foreach (var id in installmentIds) await Run("la cuota", id, () => installmentService.RemoveAsync(familyId, id, ct));
        foreach (var id in serviceIds) await Run("el servicio", id, () => servicesService.RemoveAsync(familyId, id, ct));
        foreach (var id in cardIds) await Run("la tarjeta", id, () => cardService.RemoveAsync(familyId, id, ct));
        foreach (var id in loanIds) await Run("el préstamo", id, () => loanService.RemoveAsync(familyId, id, ct));
        foreach (var id in bankIds) await Run("el banco", id, () => bankService.RemoveAsync(familyId, id, ct));
        foreach (var id in reserveIds) await Run("la reserva", id, () => reserveService.RemoveAsync(familyId, id, ct));
        foreach (var id in personIds) await Run("la persona", id, () => peopleService.ArchiveAsync(familyId, id, ct));

        // Presupuestos e ingresos no tienen Remove (claves naturales): reemplazar = volver a cero.
        foreach (var month in budgetMonths)
        {
            progress?.Invoke(new ImportProgress("Limpiando datos anteriores", done++, total));
            var result = await planningService.SetBudgetAsync(familyId, month, 0, 0, 0, ct);
            if (!result.Ok) warnings.Add($"Reemplazo: no se pudo resetear el presupuesto de {month}: {result.Error}");
        }
        if (hasIncome)
        {
            progress?.Invoke(new ImportProgress("Limpiando datos anteriores", done++, total));
            var result = await planningService.UpdateIncomeAsync(familyId, 0, 0, 0, 70, ct);
            if (!result.Ok) warnings.Add($"Reemplazo: no se pudieron resetear los ingresos: {result.Error}");
        }

        return removed;
    }

    /// <summary>Público: el controller lo usa como pre-check síncrono para el UX de confirmación con force.</summary>
    public async Task<bool> FamilyHasDataAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Banks.AnyAsync(b => b.FamilyId == familyId, ct)
            || await db.Expenses.AnyAsync(e => e.FamilyId == familyId, ct)
            || await db.Services.AnyAsync(s => s.FamilyId == familyId, ct)
            || await db.Reserves.AnyAsync(r => r.FamilyId == familyId, ct);
    }
}
