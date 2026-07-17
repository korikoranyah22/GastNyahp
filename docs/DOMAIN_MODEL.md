# GastNyahp — Domain Model (spike)

> Estado: spike inicial — dominio completamente determinado a partir del código real del frontend
> (`app/src/store/useStore.js`, `app/src/store/seedData.js`, `app/src/pages/**`, `app/src/lib/**`) y de
> [`FUNCTIONAL_SPEC.md`](FUNCTIONAL_SPEC.md). Este documento es la fuente de verdad del dominio backend; el frontend actual es
> tratado como **maqueta/especificación funcional**, no como implementación a copiar literalmente — cuando el
> frontend tiene un gap o una inconsistencia real, este documento lo señala explícitamente y decide qué hace el
> dominio nuevo.
>
> Ver también el resumen siempre cargado: [`gastnyahp-domain-model`](../.claude/skills/gastnyahp-domain-model/SKILL.md).
> Actualizar AMBOS documentos en el mismo cambio cuando el dominio evolucione.

## 0. Alcance y objetivo

Reemplazar el store único de `localStorage` (maqueta) por un backend real:

- **.NET + [Eventuous](https://eventuous.dev/)** para la escritura (event sourcing, un aggregate por entidad de
  negocio con historial).
- **PostgreSQL** como event store (vía Eventuous) y como base de los read-models (vía EF Core/Npgsql).
- **Proyecciones EF Core** para la lectura — listas, dashboards, filtros — reconstruibles desde el event store.
- **Continuidad multi-dispositivo/multi-sesión real**: dos pestañas o dispositivos abiertos a la vez ven (o
  pueden ver, con polling/refresh) el mismo estado consistente, porque el backend — no cada cliente — es la
  única fuente de verdad. Esto reemplaza por completo la capa de merge P2P actual del frontend
  (`mergeUtils.js`, `dirtyTracker.js`, `autoSave.js`).
- **Auditabilidad tipo contaduría personal**: cada hecho de negocio (un gasto cargado, una cuota marcada
  pagada, un límite de presupuesto cambiado) queda como un evento inmutable con timestamp, para siempre. No hay
  requisito de cumplir regulación bancaria (KYC, PCI, etc.) — el objetivo es *poder reconstruir el historial
  completo de decisiones financieras personales*, no cumplir un marco regulatorio.
- **Servidor MCP** sobre el mismo dominio (mismos application services), para que un agente de IA pueda cargar
  datos y consultar novedades del día (vencimientos) sin UI.
- **`BusinessDay`**: un `IHostedService` diario abre el "día hábil" de hoy, lo que permite exponer una consulta
  de "novedades del día" (vencimientos de cuotas/préstamos/servicios) que un agente puede pollear vía MCP con
  un cron externo.

## 1. Convenciones

### 1.1 Streams e identidad

- Cada aggregate con identidad propia usa un GUID (`Guid.NewGuid()`) como id de negocio, stream
  `"<tipo>-<id>"` (ver tabla en el skill `gastnyahp-domain-model`).
- Dos aggregates tienen **clave natural** en vez de GUID: `BudgetPlan` (`budget-{yyyy-MM}`) e `Income`
  (`income`, singleton — un único stream para toda la app). `BusinessDay` también usa clave natural
  (`business-day-{yyyy-MM-dd}`).
- Las IDs actuales del frontend (`bank-demo-1`, `card-visa-d1`, ...) son strings arbitrarios generados
  client-side (`generateId(prefix)` en `useStore.js`) — no son GUIDs. La importación de un JSON exportado desde
  la maqueta actual (fuera de alcance de este spike) requeriría mapear esos ids string a GUIDs nuevos.

### 1.2 Primitivos en eventos

- Fechas de calendario → `string` ISO `YYYY-MM-DD`. Timestamps → `string` ISO-8601 completo
  (`DateTime.UtcNow.ToString("O")`).
- Meses → `string` `YYYY-MM` (`YearMonth`, no `DateTime`) — es la clave natural de casi todos los sub-recursos
  (mes de cuota, mes de préstamo, mes de servicio, mes de reserva).
- Montos → `decimal`, siempre en ARS al persistir (la conversión desde USD ocurre en el comando, no se
  persiste el valor USD como monto "real" — ver `Expense`/`Service` más abajo).
- Todos los eventos van con `[EventType("V1.<Nombre>")]`, records inmutables, namespace `V1` desde el día uno
  (ver [[eventuous-event-sourced-aggregate]]).

### 1.3 `OwnerRef` — value object compartido

El patrón `ownerId` se repite en `InstallmentPurchase`, `Service`, `Expense`, y por-ítem en `Ticket`. En el
frontend, los valores posibles son: `null`, la constante hardcodeada `'shared'` (no es una fila de `people`), o
el id de una `Person` real. En el dominio nuevo esto se modela como un value object cerrado, no una FK opcional
con un string mágico:

```csharp
public abstract record OwnerRef
{
    public sealed record Unassigned : OwnerRef;
    public sealed record Shared : OwnerRef;
    public sealed record Owner(Guid PersonId) : OwnerRef;
}
```

En los eventos se serializa como dos campos primitivos (`OwnerKind: "Unassigned"|"Shared"|"Owner"`,
`OwnerPersonId: Guid?`), no como el record polimórfico directo (ver nota de "campos primitivos" en
[[eventuous-event-sourced-aggregate]]).

### 1.4 Categorías (enum cerrado, no libre)

`APP_CATEGORIES` de `app/src/pages/expenses/expensesConfig.js` — compartida por `Expense`, `Ticket` (por ítem),
e `InstallmentPurchase`:

```
Comida, Delivery, Vicios, Salidas, Hogar, Limpieza, Salud, Higiene, Transporte,
Servicios, Ropa, Educación, Electrónica, Mascotas, Perfumes, Desconocido
```

`SERVICE_CATEGORIES` (de `ServiceForm.jsx`, distinta lista, propia de `Service`):
`Electricidad, Gas, Agua, Conectividad, Streaming, Digital, Seguro, Expensas, Telecom, Otros`.

Decisión: representarlas como `string` validado contra una lista cerrada en el guard del comando (no un
`enum` de C# — permite agregar categorías nuevas vía configuración sin recompilar, igual de flexible que hoy
el frontend). El read-model las guarda como `varchar`.

### 1.5 Medio de pago (`PaymentMethod`) — value object compartido por `Expense`/`Ticket`

De `expensesConfig.js` (`buildPaymentMethods`, `isCredit`, `isDebitOrCash`):

```csharp
public abstract record PaymentMethod
{
    public sealed record Card(Guid CardId) : PaymentMethod;        // tarjeta de crédito → genera "mes de pago"
    public sealed record Debit(Guid BankId) : PaymentMethod;       // débito directo de un banco
    public sealed record Cash : PaymentMethod;
    public sealed record Modo : PaymentMethod;
    public sealed record MercadoPago : PaymentMethod;
}
```

---

## 2. `Bank`

Stream: `bank-{id}`.

**Campos del State**: `Name`, `Alias?`, `Color`, `Icon`.

**Comandos → Eventos**:
| Comando | Evento | Guard |
|---|---|---|
| `RegisterBank(Name, Alias?, Color, Icon)` | `BankRegistered` | `Name` no vacío |
| `UpdateBank(Id, Name, Alias?, Color, Icon)` | `BankUpdated` | `Name` no vacío |
| `RemoveBank(Id)` | `BankRemoved` | **Ver invariante de integridad abajo** |

**Invariante de integridad (igual al frontend, `useStore.js:41-48`)**: `RemoveBank` se rechaza si existe algún
`CreditCard` o `Loan` cuyo `BankId` sea este banco. A diferencia de un `DomainException` que aborte el comando
adentro del aggregate (el aggregate `Bank` no sabe nada de `CreditCard`/`Loan`, viola el principio de un
aggregate = una transacción/consistencia), este chequeo cruza aggregates → **vive en el
`BankService`/application-service** (ver [[application-service-layer]]), consultando el read-model de
`CreditCard`/`Loan` ANTES de invocar `commandService.Handle(new RemoveBank(id))`. Mismo patrón para todas las
demás reglas de integridad cross-aggregate de este documento (§3, más abajo).

No hay soft-delete: `BankRemoved` es un evento terminal — el read-model deja de listar el banco, pero el stream
de eventos permanece (auditable).

---

## 3. `CreditCard`

Stream: `card-{id}`.

**Campos del State**: `BankId`, `Label`, `Network` (`Visa`|`Mastercard`), `Type` (`Credit`|`Debit`),
`ClosingDay` (1-31), `DueDay` (1-31), `Color`, `Active` (bool).

**Comandos → Eventos**:
| Comando | Evento | Guard |
|---|---|---|
| `RegisterCard(BankId, Label, Network, Type, ClosingDay, DueDay, Color)` | `CardRegistered` | `BankId` existe (chequeo cross-aggregate en el service), `Label` no vacío, `1<=ClosingDay<=31`, `1<=DueDay<=31` |
| `UpdateCard(Id, Label, Network, Type, ClosingDay, DueDay, Color)` | `CardUpdated` | mismos guards de rango |
| `ActivateCard(Id)` | `CardActivated` | ya estaba inactiva |
| `DeactivateCard(Id)` | `CardDeactivated` | ya estaba activa |
| `RemoveCard(Id)` | `CardRemoved` | ver invariante |

**Decisión deliberada (mejora sobre el frontend actual)**: hoy el frontend bloquea borrar una tarjeta solo si
tiene `installments` asociadas (`useStore.js:57-63`), pero **no** chequea `services.linkedCardId` — dejaría un
servicio con una FK huérfana. El dominio nuevo bloquea `RemoveCard` si hay `InstallmentPurchase.CardId == Id`
**O** `Service.LinkedCardId == Id` (activo o no). Documentado en el skill `gastnyahp-domain-model` como invariante
#2.

**Nota sobre "Active"**: el campo existe hoy en el modelo pero el frontend no tiene un toggle explícito en la
UI (`CardForm.jsx`) — solo se usa como filtro en cálculos de totales, indirectamente vía el filtro
`active !== false` sobre `installments`/`services`, no sobre la tarjeta misma. El dominio nuevo SÍ expone
`ActivateCard`/`DeactivateCard` como comandos de primera clase (más correcto y ya está el campo en el modelo);
la UI puede agregarlos después sin cambios de dominio.

### Cálculo — ciclo de facturación (función pura, no aggregate, vive en `Domain/Billing/BillingCycle.cs`)

Puerto exacto de `getBillingMonth`/`getPaymentMonth` (`app/src/pages/expenses/expensesConfig.js:142-166`):

```csharp
public static class BillingCycle
{
    // Mes de cierre al que pertenece un gasto. Si el día del gasto > closingDay → cierre del mes siguiente.
    public static YearMonth GetBillingMonth(DateOnly expenseDate, int closingDay)
    {
        var ym = new YearMonth(expenseDate.Year, expenseDate.Month);
        return expenseDate.Day > closingDay ? ym.AddMonths(1) : ym;
    }

    // Mes en que se paga el resumen. Si dueDay < closingDay, el vencimiento cae el mes siguiente al cierre
    // (caso típico Argentina). Si dueDay >= closingDay o no hay dueDay, el vencimiento es el mismo mes de cierre.
    public static YearMonth GetPaymentMonth(DateOnly expenseDate, int closingDay, int? dueDay)
    {
        var billing = GetBillingMonth(expenseDate, closingDay);
        if (dueDay is null || dueDay >= closingDay) return billing;
        return billing.AddMonths(1);
    }

    // Mes "efectivo" de un gasto: mes de pago si es tarjeta de crédito, mes calendario si es débito/efectivo/digital.
    public static YearMonth GetEffectiveMonth(DateOnly expenseDate, CreditCard? card) =>
        card is { ClosingDay: > 0 } ? GetPaymentMonth(expenseDate, card.ClosingDay, card.DueDay) : new YearMonth(expenseDate.Year, expenseDate.Month);
}
```

Esta función es usada por la proyección de `Expense`/`Ticket` para calcular a qué "mes de resumen" pertenece
cada gasto con tarjeta — es la regla más citada de todo el dominio (aparece en el Dashboard consolidado, en
`getMonthExpenseTotal`, y en la agrupación del `CardsDashboard`).

---

## 4. `InstallmentPurchase` (Cuota / compra en cuotas)

Stream: `installment-{id}`. El aggregate más complejo del dominio — modela tanto compras con cantidad fija de
cuotas como "mensuales recurrentes" (ej. plan de celular en tarjeta).

**Campos del State**: `CardId`, `Description`, `Category`, `PurchaseDate`, `Frequency` (`Fixed`|`Monthly`),
`MonthlyAmount`, `TotalInstallments?` (null si `Monthly`), `StartMonth`, `Owner` (`OwnerRef`), `Active` (bool),
`Months: IReadOnlyList<InstallmentMonth>` donde `InstallmentMonth = (YearMonth Month, decimal Amount, bool
Paid)`.

**Comandos → Eventos**:

| Comando | Evento | Efecto |
|---|---|---|
| `RegisterInstallmentPurchase(CardId, Description, Category, PurchaseDate, Frequency, MonthlyAmount, TotalInstallments?, StartMonth, Owner)` | `InstallmentPurchaseRegistered` | Genera el calendario inicial (ver regla de generación abajo) |
| `ReviseInstallmentSchedule(Id, StartMonth, TotalInstallments?, Frequency, MonthlyAmount)` | `InstallmentScheduleRevised` | Regenera `Months` — ver regla de regeneración |
| `OverrideInstallmentMonthAmount(Id, Month, Amount)` | `InstallmentMonthAmountOverridden` | Cambia el monto de UN mes puntual, sin tocar el resto |
| `ToggleInstallmentMonthPaid(Id, Month)` | `InstallmentMonthPaidToggled` | Invierte `Paid` de un mes |
| `FinishInstallment(Id)` | `InstallmentFinished` | `Active = false` (soft-close, no borra `Months`) |
| `RemoveInstallmentPurchase(Id)` | `InstallmentRemoved` | Hard remove — sin restricción de integridad, igual que hoy |

**Regla de generación de calendario** (puerto exacto de `addInstallment`, `useStore.js:66-71`):

```
Months = Frequency == Monthly
    ? GenerateMonths(StartMonth, 24).Select(m => (m, MonthlyAmount, Paid: false))   // ventana fija de 24 meses
    : GenerateMonths(StartMonth, TotalInstallments!.Value).Select(m => (m, MonthlyAmount, Paid: false))
```
`GenerateMonths(start, count)` = `count` meses consecutivos desde `start` inclusive.

**Regla de regeneración condicional** (puerto exacto de `updateInstallment`, `useStore.js:73-109`) —
`ReviseInstallmentSchedule` SIEMPRE regenera (a diferencia del frontend, donde `updateInstallment` solo
regenera si `startMonth`/`totalInstallments`/`frequency` cambiaron; acá se modela como dos comandos distintos:
`ReviseInstallmentSchedule` para cambios de calendario, y un futuro `RenameInstallmentPurchase`/similar para
cambios cosméticos que NO deberían regenerar — separar la intención en el nombre del comando evita el bug de
"¿esto regenera o no?" que tiene el frontend por usar un único `updateInstallment` genérico):

```
al regenerar:
  paidMonths = { m.Month : true para cada m en Months actual donde m.Paid }
  preservedAmounts = { m.Month : m.Amount para cada m en Months actual donde m.Paid }  // el monto de un mes YA PAGADO no se pisa
  nuevoCount = Frequency == Monthly ? 24 : TotalInstallments
  Months = GenerateMonths(StartMonth, nuevoCount).Select(m => (
      Month: m,
      Amount: paidMonths.Contains(m) ? preservedAmounts[m] : MonthlyAmount,
      Paid: paidMonths.Contains(m)
  ))
```

**Cálculos derivados (viven en la proyección/read-model, no en el aggregate)**:
- `CardInstallmentsTotal(cardId, month)` = suma de `Amount` del mes `month` de todas las `InstallmentPurchase`
  con `CardId == cardId && Active`.
- Estado visual mensual de una tarjeta (`'Paid'|'Pending'|'None'`, de `CardsPage.jsx`): `Paid` si TODAS las
  cuotas con `Amount>0` de ese mes están `Paid`; `Pending` si alguna no lo está; `None` si no hay cuotas ese
  mes.
- Progreso: `PaidCount / TotalInstallments` (solo aplica a `Frequency == Fixed`).

---

## 5. `Loan` (Préstamo)

Stream: `loan-{id}`.

**Campos del State**: `BankId`, `Description`, `TotalAmount?` (informativo, no afecta cálculos),
`MonthlyInstallment`, `StartMonth`, `TotalInstallments`, `Months: IReadOnlyList<LoanMonth>` donde
`LoanMonth = (YearMonth Month, decimal Amount, bool Paid)`. `PaidInstallments` NO es un campo del State — se
deriva siempre de `Months.Count(m => m.Paid)` (ver decisión abajo).

**Comandos → Eventos**:

| Comando | Evento | Efecto |
|---|---|---|
| `RegisterLoan(BankId, Description, TotalAmount?, MonthlyInstallment, StartMonth, TotalInstallments)` | `LoanRegistered` | Genera calendario: `TotalInstallments` meses desde `StartMonth`, todos `Amount=MonthlyInstallment, Paid=false` |
| `ReviseLoanSchedule(Id, StartMonth, TotalInstallments, MonthlyInstallment)` | `LoanScheduleRevised` | Regenera `Months` — **mismo algoritmo que `ReviseInstallmentSchedule` (§4)**, preservando `Paid`+`Amount` de meses ya pagados |
| `OverrideLoanMonthAmount(Id, Month, Amount)` | `LoanMonthAmountOverridden` | Override puntual (préstamos UVA/ajustables cambian de monto mes a mes) |
| `ToggleLoanMonthPaid(Id, Month)` | `LoanMonthPaidToggled` | Invierte `Paid` de un mes |
| `RemoveLoan(Id)` | `LoanRemoved` | Hard remove, sin restricción de integridad |

**Decisión deliberada (corrige una asimetría real del frontend)**: hoy `updateLoan` (`useStore.js:139-141`)
**nunca** regenera `Months`, ni siquiera si cambia `startDate`/`totalInstallments` — a diferencia de
`updateInstallment`, que sí regenera cuando corresponde. Es una inconsistencia del código actual, no una regla
de negocio intencional (un préstamo y una cuota fija se comportan igual conceptualmente: "recalculá el
calendario si cambian sus parámetros estructurales"). El dominio nuevo unifica: `ReviseLoanSchedule` SIEMPRE
regenera con el mismo algoritmo que `ReviseInstallmentSchedule`. Documentado como invariante #3 en el skill.

**`PaidInstallments` como valor derivado, no field mantenido a mano**: el frontend lo recalcula "contando"
dentro de `toggleLoanPaid` (`useStore.js:142-152`) de una forma no trivial (cuenta meses `Paid` post-toggle).
En el dominio nuevo, `PaidInstallments` NO existe como campo del State — es siempre
`Months.Count(m => m.Paid)`, calculado on-demand donde se necesite (State, proyección). Elimina la clase entera
de bugs de "contador desincronizado".

---

## 6. `Service` (Servicio recurrente)

Stream: `service-{id}`.

**Campos del State**: `Name`, `Category`, `BillingType` (`Monthly`|`Bimonthly`|`Quarterly`) — **metadata
informativa, no cambia cómo se generan `Amounts` (siempre mensual)**, `LinkedCardId?`, `Active`, `Currency`
(`ARS`|`USD`), `Owner` (`OwnerRef`), `Amounts: IReadOnlyList<ServiceMonthAmount>` donde
`ServiceMonthAmount = (YearMonth Month, decimal Amount, bool Paid)`.

**Comandos → Eventos**:

| Comando | Evento | Efecto |
|---|---|---|
| `RegisterService(Name, Category, BillingType, LinkedCardId?, Currency, BaseAmountArs, Owner)` | `ServiceRegistered` | Genera `Amounts` para 12 meses desde el mes de alta, todos con `BaseAmountArs` |
| `UpdateServiceDetails(Id, Name, Category, BillingType, LinkedCardId?, Currency)` | `ServiceDetailsUpdated` | No toca `Amounts` |
| `ActivateService(Id)` / `DeactivateService(Id)` | `ServiceActivated` / `ServiceDeactivated` | — |
| `SetServiceMonthAmount(Id, Month, AmountArs)` | `ServiceMonthAmountSet` | Upsert de UN mes puntual (usado en historial expandido) |
| `ExtendServiceFutureAmounts(Id, FromMonth, AmountArs, MonthsAhead = 12)` | `ServiceFutureAmountsExtended` | Upsert de los próximos `MonthsAhead` meses desde `FromMonth` inclusive — preserva `Paid` de los meses que ya existían |
| `ToggleServiceMonthPaid(Id, Month)` | `ServiceMonthPaidToggled` | Invierte `Paid`; si el mes no existe en `Amounts`, lo crea con `Amount=0, Paid=true` |
| `RemoveService(Id)` | `ServiceRemoved` | Hard remove, sin restricción de integridad (pero ver §3 — SÍ bloquea `RemoveCard` si el servicio sigue vinculado) |

**Conversión USD → ARS** (puerto de `ServiceForm.jsx:71-118`): el comando recibe el monto en la moneda
original; si `Currency == USD`, el application service lo convierte a ARS usando el `UsdRateCcl` vigente
(leído del aggregate `Income`) ANTES de emitir el evento — **el evento siempre persiste el monto ya en ARS**
(`AmountArs = Math.Round(amountUsd * usdRateCcl)`), más dos campos adicionales para poder re-editar en USD la
próxima vez: `OriginalAmount?`, `OriginalCurrency?`. Guard: `RegisterService`/`SetServiceMonthAmount` con
`Currency == USD` fallan si `Income.UsdRateCcl <= 0` (mismo bloqueo que hoy en el form).

**Cálculos derivados (proyección)**:
- `CardServicesTotal(cardId, month)` = suma de `Amount` del mes de servicios con `LinkedCardId == cardId &&
  Active`.
- `IndependentServicesTotal(month)` = suma de servicios sin `LinkedCardId`, `Active`.
- Delta vs. mes anterior: `(current - prev) / prev * 100`, solo se muestra si `prev > 0`.

---

## 7. `Reserve` (ex "Gasto Fijo" — dinero apartado, no gasto consumido)

Stream: `reserve-{id}`. Concepto de negocio: montos que se **apartan** al inicio del mes (reserva para una
persona, fondo de efectivo, deuda pendiente, estimado de variable de tarjeta) — no son transacciones.

**Campos del State**: `Label`, `Type` (`Reserve`|`Cash`|`Debt`|`Other`), `Icon`, `Recurring` (bool),
`BaseAmount` (solo relevante si `Recurring`), `Months: IReadOnlyList<ReserveMonthOverride>` donde
`ReserveMonthOverride = (YearMonth Month, decimal Amount, string? Note)`.

**Comandos → Eventos**:

| Comando | Evento | Efecto |
|---|---|---|
| `RegisterReserve(Label, Type, Icon, Recurring, BaseAmount)` | `ReserveRegistered` | `Months` vacío al crear |
| `UpdateReserveDetails(Id, Label, Type, Icon, Recurring, BaseAmount)` | `ReserveDetailsUpdated` + `ReserveBaseAmountApplied` si cambió `Recurring` o `BaseAmount` | Ver nota destructiva abajo |
| `SetReserveMonthAmount(Id, Month, Amount, Note?)` | `ReserveMonthAmountSet` | Upsert del override de UN mes puntual |
| `ApplyReserveBaseToAllMonths(Id, BaseAmount)` | `ReserveBaseAmountApplied` | Setea `BaseAmount`, `Recurring=true`, **borra TODOS los overrides mensuales existentes** |
| `RemoveReserve(Id)` | `ReserveRemoved` | Hard remove, sin restricción de integridad |

**Regla de "monto efectivo" (la más importante del módulo)** — puerto exacto de `getFixedExpenseTotal`
(`useStore.js:363-369`), vive en la proyección/lectura, NO en el aggregate (es una regla de agregación, no de
transición de estado):

```
EffectiveAmount(reserve, month) =
    reserve.Months.overrideForMonth(month)?.Amount   // override puntual del mes, si existe
    ?? (reserve.Recurring ? reserve.BaseAmount : 0)   // si no hay override: BaseAmount solo si es recurrente
```
Prioridad: **override puntual > monto base recurrente > 0**.

**Operación destructiva preservada intencionalmente** (puerto exacto de `setFixedExpenseBase`,
`useStore.js:373-379`, y del efecto colateral en `FixedExpenseForm.jsx:62-67` al editar `recurring`/
`baseAmount`): `ApplyReserveBaseToAllMonths` borra TODOS los overrides mensuales — es el comportamiento
esperado por el usuario ("aplicar a todos los meses" reemplaza cualquier ajuste puntual previo), no un bug. Se
preserva tal cual, pero queda como un evento explícito y auditable (`ReserveBaseAmountApplied`), a diferencia
del frontend donde es un side-effect silencioso de `updateFixedExpense`.

---

## 8. `Person`

Stream: `person-{id}`.

**Campos del State**: `Name`, `Emoji`, `Color`, `Archived` (bool).

**Comandos → Eventos**:
| Comando | Evento | Guard |
|---|---|---|
| `RegisterPerson(Name, Emoji, Color)` | `PersonRegistered` | `Name` no vacío |
| `UpdatePerson(Id, Name, Emoji, Color)` | `PersonUpdated` | `Name` no vacío |
| `ArchivePerson(Id)` | `PersonArchived` | No estaba ya archivada |

**Decisión deliberada**: NO hay `RemovePerson` (hard delete). El frontend permite borrar una persona sin
chequear si está referenciada como `ownerId` en `installments`/`services`/`expenses`/ítems de ticket, dejando
esas referencias huérfanas silenciosamente (`people.find` no encuentra nada, el badge simplemente no se
renderiza). El dominio nuevo usa `ArchivePerson`: la persona deja de aparecer en selectores de "nueva entidad",
pero el read-model conserva la fila (marcada `Archived=true`) para que cualquier `OwnerRef.Owner(personId)` ya
emitido siga resolviendo nombre/color/emoji correctamente — necesario para la auditabilidad histórica
("¿de quién era este gasto de hace 8 meses?"). Documentado como invariante #4.

La constante `SHARED_OWNER` del frontend (`OwnerBadge.jsx`, `{ id:'shared', name:'Compartido', ... }`) **no es
una `Person`** — es el caso `OwnerRef.Shared` del value object (§1.3), fijo, no editable, no tiene stream
propio.

---

## 9. `Expense` (Gasto simple)

Stream: `expense-{id}`. Cubre lo que el frontend llama "gasto" sin `type: 'ticket'`.

**Campos del State**: `Date`, `Description`, `Category`, `AmountArs`, `PaymentMethod` (§1.5), `Owner`
(`OwnerRef`), `OriginalAmount?`, `OriginalCurrency?` (solo si se cargó en USD).

**Comandos → Eventos**:
| Comando | Evento | Guard |
|---|---|---|
| `RegisterExpense(Date, Description, Category, Amount, Currency, PaymentMethod, Owner)` | `ExpenseRegistered` | `Description` no vacío, `Amount > 0`, `PaymentMethod` válido; si `Currency==USD`, requiere `Income.UsdRateCcl > 0` |
| `UpdateExpense(Id, Date, Description, Category, Amount, Currency, PaymentMethod, Owner)` | `ExpenseUpdated` | mismos guards |
| `RemoveExpense(Id)` | `ExpenseRemoved` | Hard remove, sin restricción |

Conversión USD igual que `Service` (§6): el application service resuelve `AmountArs` antes de emitir el evento.

---

## 10. `Ticket` (Compra con múltiples ítems)

Stream: `ticket-{id}`. Cubre `expenses[]` con `type:'ticket'` del frontend — un ticket de compra con varios
ítems categorizables individualmente (ej. un ticket de supermercado con Comida + Limpieza + Higiene en la misma
compra).

**Campos del State**: `Date`, `Description` (nombre del comercio), `PaymentMethod` (compartido para todo el
ticket), `Discount` (decimal, default 0), `Items: IReadOnlyList<TicketItem>` donde
`TicketItem = (Guid ItemId, string Description, decimal Amount, string Category, OwnerRef Owner)`.

**Comandos → Eventos**:
| Comando | Evento | Guard |
|---|---|---|
| `RegisterTicket(Date, Description, PaymentMethod, Discount, Items[])` | `TicketRegistered` | `Items` no vacío; cada ítem: `Description` no vacío, `Amount > 0`, `Category` válida |
| `UpdateTicket(Id, Date, Description, PaymentMethod, Discount, Items[])` | `TicketUpdated` | mismos guards — reemplaza el set completo de ítems (igual que el form actual, que no soporta edición granular por ítem) |
| `RemoveTicket(Id)` | `TicketRemoved` | Hard remove |

**Fórmula de monto total** (puerto exacto de `getExpenseAmount` para tickets, `expensesConfig.js:200-204`) —
regla más citada del módulo, usada en TODOS los totales (día/semana/mes/categoría/dashboard):

```
TicketTotal = Math.Max(0, Sum(Items.Amount) - Discount)
```

**Nota de asimetría preservada**: los ítems de ticket NO soportan USD individual (a diferencia de `Expense`),
igual que en el frontend actual — no hay caso de uso real de un ticket con ítems en distinta moneda.

### 10.1 `Expense`/`Ticket` — reglas de agregación temporal (viven en la proyección, no en los aggregates)

Puerto exacto de `useStore.js`/`expensesConfig.js`:

- **Semana del mes**: `1-7 → S1`, `8-14 → S2`, `15-22 → S3`, `23-31 → S4` (`GetWeek(date)`).
- `MonthExpenses(month)` — filtra por **mes calendario** de `Date` (para el listado de "Gastos del mes").
- `MonthExpenseTotal(month)` — filtra por **mes de PAGO** (`BillingCycle.GetEffectiveMonth`, §3) — para el
  Dashboard consolidado. **Esta es la distinción crítica**: el mismo gasto puede listarse en un mes calendario
  distinto del mes en que impacta el resumen de tarjeta.
- `MonthCreditTotal(month)` — gastos con `PaymentMethod is Card`, mes CALENDARIO — para las barras de
  presupuesto de la pantalla de gastos.
- `MonthDebitCashTotal(month)` — `PaymentMethod is Cash or Debit or Modo or MercadoPago`, mes calendario.
- `WeekExpenseTotal(month, week)` — mes calendario + rango de día de esa semana.

---

## 11. `BudgetPlan`

Stream: `budget-{yyyy-MM}` (clave natural = mes, no GUID).

**Campos del State**: `CreditLimit`, `DebitCashLimit`, `WeeklyLimit` (los tres `decimal`, default 0 = "sin
meta configurada, no mostrar barra de progreso").

**Comandos → Eventos**:
| Comando | Evento |
|---|---|
| `SetBudgetLimits(Month, CreditLimit, DebitCashLimit, WeeklyLimit)` | `BudgetLimitsSet` |

Semántica de upsert-merge (igual que `setBudget` en `useStore.js:324-326`): el comando recibe los tres valores
completos (el application service los pre-carga con los valores actuales del mes antes de aplicar el patch, si
el caller solo cambió uno) — el aggregate en sí no necesita lógica de "merge parcial", siempre trabaja con el
triple completo.

Fórmula de barra de progreso (vive en el read-model/frontend, no en el dominio): `pct = min(100,
round(spent/limit*100))`, color según umbral (`>=90` rojo, `>=70` ámbar).

---

## 12. `Income`

Stream único `income` (singleton — toda la app tiene un solo ingreso configurado, igual que hoy).

**Campos del State**: `NetMonthly`, `UsdRateOfficial`, `UsdRateCcl`, `SplitPercent` (default 70).

**Comandos → Eventos**:
| Comando | Evento |
|---|---|
| `UpdateIncome(NetMonthly?, UsdRateOfficial?, UsdRateCcl?, SplitPercent?)` | `IncomeUpdated` |

Merge parcial iguales que `setIncome` (`useStore.js:397`) — solo los campos provistos cambian.

**Auditabilidad temporal sin aggregate por mes**: aunque `Income` es un stream único (no versionado por mes,
igual que el frontend hoy), el read-model mantiene además una tabla `income_history` append-only (una fila por
`IncomeUpdated`, con su timestamp de evento) — permite responder "¿cuál era el ingreso neto configurado en
Febrero 2026?" tomando el último `IncomeUpdated` con timestamp `<=` fin de Febrero 2026, sin necesitar
modelarlo como un aggregate por mes. Se documenta como alternativa evaluada y descartada: modelar `Income` como
`income-{yyyy-MM}` (análogo a `BudgetPlan`) daría el mismo resultado con más piezas — se prefiere el stream
único + tabla de histórico por ser más simple y porque el dominio de negocio real (el sueldo de una persona)
es conceptualmente un único valor que cambia en el tiempo, no un valor distinto "por mes" que haya que crear
explícitamente cada vez.

---

## 13. `BusinessDay` — el día hábil diario y las "novedades"

Stream: `business-day-{yyyy-MM-dd}` — un stream nuevo por cada fecha calendario.

**Campos del State**: `Date`, `OpenedAt` (timestamp).

**Comandos → Eventos**:
| Comando | Evento | Guard |
|---|---|---|
| `OpenBusinessDay(Date)` | `BusinessDayOpened` | `ExpectedState.New` — el stream de esa fecha no debe existir todavía (idempotencia por fecha) |

### 13.1 `BusinessDayScheduler` — `IHostedService` (ver [[csharp-conventions-and-patterns]])

Corre una vez al día (hora configurable vía `Options`, ej. `06:00` hora de Argentina), calcula la fecha de
"hoy" en la zona horaria configurada de la app, e invoca el application service para abrir el `BusinessDay` de
esa fecha si todavía no existe. Si el comando falla el guard (porque el día ya fue abierto — restart del
container, doble ejecución), se loguea en nivel `Debug` y se ignora — no es un error.

### 13.2 "Novedades del día" — query derivada, no aggregate ni evento propio

Expuesta como `GET /api/business-day/today/novelties` y como tool MCP (ver [[mcp-tool-server]],
`business_day_novelties`). Se calcula cruzando la fecha del `BusinessDay` abierto con los read-models de
`CreditCard`, `InstallmentPurchase`, `Loan`, `Service`:

```
Novelties(date) =
    installments/loans con un mes == YearMonth(date) y Paid == false, cuyo vencimiento (dueDay de la tarjeta,
        o el propio mes de préstamo) cae hoy o ya venció y sigue sin pagar
  + services con Amounts[mes actual].Paid == false, en su rango de facturación
  + cards cuyo ClosingDay o DueDay == date.Day (para avisar "hoy cierra/vence el resumen de X")
```

Este cálculo NO se persiste — se recalcula en cada consulta desde los read-models existentes, igual que
`PriceHistory` (§14). El único evento que persiste es el propio `BusinessDayOpened`, que sirve como "reloj" de
la app (para que el HostedService sea idempotente y para poder auditar cuándo se abrió cada día).

---

## 14. Read-models derivados sin aggregate propio

- **`PriceHistory`** — puerto de `price-history/PriceHistoryPage.jsx`: agrupa ítems de `Ticket` por
  `(ItemDescription, TicketDescription)` y `Expense` por `Description`, con `count >= 2` ocurrencias. Calcula
  `AvgLast5`, `LastPrice`, delta porcentual entre ocurrencias consecutivas. Se reconstruye 100% desde los
  streams de `Expense`/`Ticket` — no tiene comandos ni eventos propios, es una proyección de lectura
  especializada (ver [[eventuous-projection-readmodel]]).
- **`DualPay`** — calculadora standalone (`DualPayPage.jsx`), no persiste nada hoy y así se mantiene: un
  endpoint de cálculo puro `POST /api/income/dualpay-preview` que recibe `(BrutoNeto, DolarOficial, DolarCcl)`
  y devuelve `(Pesos = round(BN*0.30), Usd = BN*0.70/DolarOficial, Ccl = round(Usd*DolarCcl), Total = Pesos +
  Ccl)`. **Nota de deuda heredada**: el frontend tiene `income.splitPercent` en el modelo pero el cálculo usa
  `0.30`/`0.70` hardcodeado, no ese campo — se preserva tal cual (hardcodeado) salvo que se pida explícitamente
  conectarlo a `SplitPercent`, en cuyo caso es un cambio de UX a decidir con el usuario, no una corrección
  silenciosa del dominio.
- **"Copiar mes anterior"** — no es un aggregate: es una operación del `MonthlyPlanningService`
  (application-service) que lee `Reserve`/`BudgetPlan` de `fromMonth` y emite los mismos comandos
  (`SetReserveMonthAmount`, `SetBudgetLimits`) contra `toMonth` **solo para lo que falte** (puerto exacto de
  `copyMonthData`, `useStore.js:404-421`):
  ```
  para cada Reserve no-recurrente:
      si tiene override en fromMonth Y NO tiene override en toMonth → SetReserveMonthAmount(toMonth, ese valor)
  si existe BudgetPlan(fromMonth) Y NO existe BudgetPlan(toMonth) → SetBudgetLimits(toMonth, esos valores)
  NUNCA copia Expense/Ticket (son transacciones reales, no estimaciones) — igual que hoy.
  ```

---

## 15. Qué reemplaza la capa de merge P2P actual

`mergeUtils.js`, `dirtyTracker.js`, `autoSave.js` (documentados en detalle en el reporte de investigación de
este spike) implementan, en esencia, un CRDT casero last-write-wins porque hoy no hay backend. Con Postgres
como fuente de verdad única:

- **Ya no hace falta mergear** — todo cliente lee/escribe contra el mismo backend. Dos dispositivos abiertos a
  la vez ven datos consistentes en el próximo refresh/poll (y, opcionalmente, en tiempo real vía
  SignalR/WebSocket, ver la sección de Realtime de [[react-feature-module]]).
- **El concepto de timestamp por sub-recurso SÍ se preserva** — no como `_updatedAt` mutable, sino como el
  timestamp intrínseco de cada evento en el event store (ya viene gratis con Eventuous, es más fuerte que el
  `_updatedAt` actual porque es inmutable y ordenado por el propio store).
  `dirtyTracker.js`/`autoSave.js`/export-import manual dejan de tener sentido (no hay "cambios sin guardar":
  cada comando que se ejecuta con éxito ya está guardado).
- **Import/export JSON** puede conservarse como una operación de migración/backup (útil para mover datos de la
  maqueta actual al backend nuevo una única vez), pero deja de ser el mecanismo de persistencia normal de la
  app.

---

## 16. Resumen de invariantes cross-aggregate (viven en application services, no en los aggregates)

| Regla | Aggregates involucrados | Dónde vive el chequeo |
|---|---|---|
| No borrar `Bank` con `CreditCard`/`Loan` asociados | `Bank` ← `CreditCard`, `Loan` | `BankService` |
| No borrar `CreditCard` con `InstallmentPurchase` o `Service.LinkedCardId` asociados | `CreditCard` ← `InstallmentPurchase`, `Service` | `CreditCardService` |
| USD requiere `Income.UsdRateCcl > 0` | `Expense`/`Service` → `Income` | `ExpenseService`/`ServiceService`, leen `Income` antes de convertir |
| "Copiar mes anterior" no pisa datos existentes, nunca copia `Expense`/`Ticket` | `Reserve`, `BudgetPlan` | `MonthlyPlanningService` |
| Novedades del día cruza vencimientos de 4 aggregates distintos | `CreditCard`, `InstallmentPurchase`, `Loan`, `Service` | Query de `BusinessDayService` |

Un aggregate individual (`CommandService` de Eventuous) solo puede garantizar consistencia DENTRO de su propio
stream — cualquier regla que dependa del estado de otro aggregate es, por definición, responsabilidad del
application service que orquesta ambos (ver [[application-service-layer]]), consultando el read-model del otro
aggregate antes de invocar el comando.

## 17. Familias y acceso — sin emails, sin SMS

La maqueta original no necesitaba usuarios: **la posesión del archivo JSON determinaba tu poder**. El backend
traduce esa filosofía a: **la posesión del token de miembro determina tu poder**. Nada de emails ni SMS; el
acceso se propaga físicamente (mostrar un QR, pasar un código), igual que antes se pasaba el archivo.

### 17.1 `AdminInvite` — la puerta de entrada a "crear familia"

Stream `admin-invite-{id}`. Para llegar siquiera a la pantalla de creación de familia hace falta un código
emitido por el administrador de la app (endpoint protegido por `X-Admin-Key`, una API key de configuración).
Así no hay registro abierto: sin código del admin, no hay familia — la app es personal aunque se comparta con
amigos.

| Comando | Evento | Guard |
|---|---|---|
| `IssueAdminInvite(InviteId, CodeHash)` | `AdminInviteIssued` | — |
| `RedeemAdminInvite(InviteId, FamilyId)` | `AdminInviteRedeemed` | **Un solo uso** — ya canjeado ⇒ rechazo |

**Los códigos/tokens nunca se persisten en crudo**: eventos y read-model guardan SHA-256; el valor real se
devuelve UNA vez al emitirlo. Auditable sin filtrar credenciales.

### 17.2 `Family` — miembros e invitaciones QR

Stream `family-{id}`. Los miembros son credenciales de acceso (token opaco hasheado), NO confundir con
`Person` (§8), que sigue siendo la etiqueta de atribución de gastos DENTRO de una familia.

| Comando | Evento(s) | Guard |
|---|---|---|
| `CreateFamily(FamilyId, Name, AdminInviteId, FounderMemberId, FounderName, FounderTokenHash)` | `FamilyCreated` + `FamilyMemberJoined` (rol Admin) | nombre requerido |
| `IssueFamilyInvite(FamilyId, InviteId, CodeHash, IssuedByMemberId, ExpiresAt)` | `FamilyInviteIssued` | emisor es miembro Admin |
| `JoinFamily(FamilyId, InviteId, MemberId, Name, TokenHash, Now)` | `FamilyInviteRedeemed` + `FamilyMemberJoined` | invitación existe, no canjeada/revocada/vencida — **un solo uso** |
| `RevokeFamilyInvite(FamilyId, InviteId, ByMemberId)` | `FamilyInviteRevoked` | revocador es Admin, invitación pendiente |
| `IssueFamilyAgentKey(FamilyId, KeyId, Name, TokenHash, IssuedByMemberId)` | `FamilyAgentKeyIssued` | emisor es miembro Admin |
| `RevokeFamilyAgentKey(FamilyId, KeyId, ByMemberId)` | `FamilyAgentKeyRevoked` | revocador es Admin, clave activa |

**Claves de agente** — la credencial que usan los clientes MCP (el estándar self-hosted: `Authorization:
Bearer <token>` estático; OAuth/PKCE solo si un cliente remoto lo exige). Un Admin las emite con nombre
("cron matutino", "Claude Desktop") desde el panel de la familia, el token se muestra UNA vez, y se revocan
individualmente. En el middleware valen como credencial de **solo datos** (rol `Agent`): como su principal es
un `KeyId` y no un `MemberId`, los guards de Admin (emitir invitaciones QR, emitir/revocar claves) las
rechazan naturalmente.

El QR es responsabilidad del frontend: la API devuelve `{ inviteCode, qrPayload, expiresAt }` donde
`qrPayload = "gastnyahp://join?code=..."` — el backend nunca renderiza imágenes.

### 17.3 Particionado por familia

- Todos los aggregates con GUID llevan `FamilyId` en su evento `*Registered` (hecho de dominio explícito:
  "se registró EN esta familia"), y las proyecciones lo estampan como columna para filtrar TODAS las lecturas.
- `BudgetPlan` pasa a stream `budget-{familyId}-{yyyy-MM}` e `Income` a `income-{familyId}` (un singleton POR
  familia). `BusinessDay` queda global (es el reloj de la app); las novedades se filtran por familia al
  consultarse.
- Autenticación: middleware que resuelve `Authorization: Bearer <token>` → hash → `family_members` → familia
  del request. Los application services reciben `familyId` y verifican pertenencia antes de cada escritura.
- Rutas anónimas: solo `POST /api/families` (con código de admin) y `POST /api/families/join` (con código de
  invitación). Todo lo demás exige credencial.

## 18. Estado y próximos pasos

**Hecho** (todo con tests en verde): aggregates + dominio completo, proyecciones EF, application services,
API REST con auth por familia, servidor MCP embebido (`/mcp`), `BusinessDayScheduler`, modo
`EventStore:Provider=Postgres` (Eventuous.Postgresql + subscription `$all` + checkpoint store, verificado
runtime con `docker compose up` + smoke test de `run-stack.ps1`), stack Docker completo, frontend React
conectado a la API (crear familia/unirse por QR, claves de agente, todos los slices del store), y borradores
conversacionales (§19).

Dos decisiones de runtime que valen documentar: (a) el schema de Eventuous se crea con un data source
DESCARTABLE antes de que el compartido abra su primera conexión (Npgsql cachea el catálogo de tipos al
conectar — ver `EventStoreSchemaInitializer`); (b) read-your-writes en Postgres espera el checkpoint de la
subscription, comiteado por evento (`CheckpointCommitBatchSize=1`, delay 10ms) — ver `SubscriptionReadModelSync`.

**Pendiente**: `PriceHistoryView` como proyección de lectura (§14) — todavía no implementada.

**Importación del JSON legacy** (hecho): `POST /api/import` (solo Admin de la familia; exige familia sin
datos salvo `force=true`) reproduce el export de la maqueta como COMANDOS vía los mismos application
services de la UI — ids string remapeados a GUIDs, pagos como `Toggle*MonthPaid`, montos distintos como
`Override*MonthAmount`, tickets/gastos/presupuestos/ingresos completos. El event store queda auditable como
si la carga hubiera sido manual. Entradas inválidas se saltean con advertencias en el `ImportSummary`, nunca
abortan la importación entera. UI: Ajustes → "Importar desde la maqueta".

El POST valida y devuelve **202**: el job corre en background (`ImportJobTracker`, atado al lifetime de la
app y no al request — un timeout del cliente o un F5 no lo cancelan) y la UI hace polling de
`GET /api/import/status` (`running` + progreso por sección / `completed` + resumen / `failed`), retomando el
spinner al montar si hay un job en curso. Un import por familia a la vez (409 si ya hay uno corriendo).

**Modos sobre una familia con datos**: `force=true` agrega encima (aditivo); `replace=true` **reemplaza
todo** — como el event store es append-only, "borrar" = emitir los comandos terminales de todo lo existente
(Remove de gastos/tickets/cuotas/servicios/tarjetas/préstamos/bancos/reservas en el orden que satisface los
guards cross-aggregate, Archive de personas, presupuestos a cero, ingresos al default) y recién después
importar: el reemplazo entero queda auditado. El resumen reporta `removed`. UI: al detectar datos ofrece
Reemplazar todo / Agregar encima / Cancelar.

## 19. Borradores conversacionales (`Draft`)

**Problema**: cargar un ticket del super requiere tener todos los datos juntos; en la vida real la carga se
dicta de a poco ("estoy en el super" → ítems a medida que caen al changuito → "ya pagué, me descontaron 20%
por una promo"). El agente de Telegram necesita un lugar donde ir moldeando la carga SIN tocar la
contabilidad, y un momento explícito de confirmación.

**Aggregate `Draft`** — stream `draft-{id}`, particionado por `FamilyId`:

| Evento (V1) | Comando | Significado |
|---|---|---|
| `DraftCreated` | `CreateDraft` | Nace abierto, con tipo (`Expense` / `Ticket` / `Installment`), payload parcial y quién lo creó (`Member` o `Agent` + id de la credencial). |
| `DraftUpdated` | `UpdateDraft` | **Snapshot completo** del payload — el stream muestra cómo la conversación moldeó la carga, versión por versión. |
| `DraftConfirmed` | `ConfirmDraft` | Guarda el id de la entidad real creada. Solo desde `Open`. |
| `DraftDiscarded` | `DiscardDraft` | Cierra sin cargar. Solo desde `Open`. |

**Payload** (`DraftPayload`): TODOS los campos opcionales — fecha, descripción, categoría, monto, moneda,
medio de pago, dueño, descuento + ítems (ticket), tarjeta + cuota mensual + total + mes de inicio (cuotas),
y una nota libre de contexto. La validación fuerte NO vive en el aggregate: corre al confirmar.

**Confirmación** (`DraftService.ConfirmAsync`, patrón create+redeem de §17): dispara el comando REAL
(`RegisterExpense` / `RegisterTicket` / `RegisterInstallmentPurchase`) con todos sus guards y, si sale bien,
`ConfirmDraft` con el id resultante. Si el comando real rechaza, el borrador queda `Open` y el error le dice
al agente exactamente qué falta — es parte del diálogo. Defaults indulgentes al confirmar: fecha=hoy,
categoría inválida→`Desconocido`, medio=Efectivo, moneda=Ars; estrictos: montos > 0, ítems no vacíos,
tarjeta de cuotas existente, y toda referencia (tarjeta/banco/persona) debe pertenecer a la familia.

**Por qué solo Expense/Ticket/Installment**: son las cargas "de la fila del super" — conversacionales por
naturaleza. Bancos/tarjetas/servicios/préstamos son setup de baja frecuencia con formularios propios; un
borrador ahí sería ruido (decisión explícita, ver anti-patrones del skill).

**Superficies**: REST `GET/POST /api/drafts`, `PUT /api/drafts/{id}`, `POST .../confirm|discard` (miembros Y
claves de agente — es la superficie pensada para agentes); tools MCP `borrador_crear`, `borrador_actualizar`,
`borrador_item_agregar`, `borrador_item_quitar`, `borradores_listar`, `borrador_confirmar`,
`borrador_descartar` (resuelven nombres → ids, hablan español); UI: panel "Borradores pendientes" en la
página de Gastos (confirmar/descartar); las novedades del día incluyen el conteo de borradores abiertos.
