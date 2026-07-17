---
name: gastnyahp-domain-model
description: Mapa canónico del dominio de GastNyahp — bounded contexts, aggregates, streams, eventos y comandos reales de la app de finanzas personales (bancos, tarjetas, cuotas, préstamos, servicios, reservas, gastos, BusinessDay). Usar SIEMPRE antes de crear/tocar un aggregate, evento, comando o proyección del backend, para saber a qué entidad real corresponde y qué invariantes ya están decididas.
---

# gastnyahp-domain-model

Este skill es el **QUÉ** del dominio de GastNyahp (qué aggregates existen, qué eventos emiten, qué invariantes
tienen). El **CÓMO** implementarlo en código está en [[eventuous-event-sourced-aggregate]],
[[eventuous-projection-readmodel]], [[application-service-layer]] y [[ef-core-postgres-context]] — no repitas
ese código acá, enlazalo.

El detalle exhaustivo (todas las reglas de cálculo, edge cases, decisiones tomadas vs. el comportamiento actual
del frontend-maqueta) vive en `backend/docs/DOMAIN_MODEL.md`. Este skill es la versión resumida y siempre
cargada — consultalo primero; andá al doc completo solo cuando necesites la regla exacta de un cálculo (ciclo de
facturación de tarjeta, regeneración de calendario de cuotas, fórmula de "sobra/falta", etc.).

## Cuándo usar / cuándo no

- **Usar**: vas a crear un aggregate nuevo, agregar un evento/comando a uno existente, diseñar una proyección, o
  necesitás saber "¿esto ya es un aggregate en GastNyahp, o es un read-model derivado, o no se persiste?".
- **No usar**: para el CÓMO genérico de Eventuous/EF/ASP.NET (eso está en las skills técnicas linkeadas arriba).

## Contexto: de dónde sale este modelo

GastNyahp hoy es un frontend React+Zustand (`app/`) que persiste todo en un único store de `localStorage`
(`app/src/store/useStore.js` + `app/src/store/seedData.js`). No hay backend real. Este modelo de dominio
**reemplaza ese store por un backend event-sourced** (.NET + Eventuous + Postgres) manteniendo el mismo
vocabulario y las mismas reglas de negocio que el frontend ya implementa — el frontend actual es la
especificación funcional de facto (ver también `GASTOS_APP_SPEC.md` en la raíz del repo).

## Tabla canónica de aggregates

| Aggregate | Stream id | Reemplaza (frontend) | Eventos clave (V1) | Comandos clave |
|---|---|---|---|---|
| `Bank` | `bank-{id}` | `banks[]` | `BankRegistered`, `BankUpdated`, `BankRemoved` | `RegisterBank`, `UpdateBank`, `RemoveBank` |
| `CreditCard` | `card-{id}` | `creditCards[]` | `CardRegistered`, `CardUpdated`, `CardActivated`, `CardDeactivated`, `CardRemoved` | `RegisterCard`, `UpdateCard`, `ActivateCard`, `DeactivateCard`, `RemoveCard` |
| `InstallmentPurchase` | `installment-{id}` | `installments[]` | `InstallmentPurchaseRegistered`, `InstallmentScheduleRevised`, `InstallmentMonthAmountOverridden`, `InstallmentMonthPaidToggled`, `InstallmentFinished`, `InstallmentRemoved` | `RegisterInstallmentPurchase`, `ReviseInstallmentSchedule`, `OverrideInstallmentMonthAmount`, `ToggleInstallmentMonthPaid`, `FinishInstallment`, `RemoveInstallmentPurchase` |
| `Loan` | `loan-{id}` | `loans[]` | `LoanRegistered`, `LoanScheduleRevised`, `LoanMonthAmountOverridden`, `LoanMonthPaidToggled`, `LoanRemoved` | `RegisterLoan`, `ReviseLoanSchedule`, `OverrideLoanMonthAmount`, `ToggleLoanMonthPaid`, `RemoveLoan` |
| `Service` | `service-{id}` | `services[]` | `ServiceRegistered`, `ServiceDetailsUpdated`, `ServiceActivated`, `ServiceDeactivated`, `ServiceMonthAmountSet`, `ServiceFutureAmountsExtended`, `ServiceMonthPaidToggled`, `ServiceRemoved` | `RegisterService`, `UpdateServiceDetails`, `ActivateService`, `DeactivateService`, `SetServiceMonthAmount`, `ExtendServiceFutureAmounts`, `ToggleServiceMonthPaid`, `RemoveService` |
| `Reserve` (ex "gasto fijo") | `reserve-{id}` | `fixedExpenses[]` | `ReserveRegistered`, `ReserveDetailsUpdated`, `ReserveMonthAmountSet`, `ReserveBaseAmountApplied`, `ReserveRemoved` | `RegisterReserve`, `UpdateReserveDetails`, `SetReserveMonthAmount`, `ApplyReserveBaseToAllMonths`, `RemoveReserve` |
| `Person` | `person-{id}` | `people[]` | `PersonRegistered`, `PersonUpdated`, `PersonArchived` | `RegisterPerson`, `UpdatePerson`, `ArchivePerson` |
| `Expense` (gasto simple) | `expense-{id}` | `expenses[]` (sin `type`) | `ExpenseRegistered`, `ExpenseUpdated`, `ExpenseRemoved` | `RegisterExpense`, `UpdateExpense`, `RemoveExpense` |
| `Ticket` (compra con ítems) | `ticket-{id}` | `expenses[]` (`type:'ticket'`) | `TicketRegistered`, `TicketUpdated`, `TicketRemoved` | `RegisterTicket`, `UpdateTicket`, `RemoveTicket` |
| `BudgetPlan` | `budget-{yyyy-MM}` | `budgets[month]` | `BudgetLimitsSet` | `SetBudgetLimits` |
| `Income` | `income` (singleton) | `income` | `IncomeUpdated` | `UpdateIncome` |
| `BusinessDay` | `business-day-{yyyy-MM-dd}` | *(no existe hoy)* | `BusinessDayOpened` | `OpenBusinessDay` |
| `AdminInvite` | `admin-invite-{id}` | *(no existe hoy)* | `AdminInviteIssued`, `AdminInviteRedeemed` | `IssueAdminInvite`, `RedeemAdminInvite` |
| `Family` | `family-{id}` | *(no existe hoy — antes "poseer el JSON" era el acceso)* | `FamilyCreated`, `FamilyMemberJoined`, `FamilyInviteIssued`, `FamilyInviteRedeemed`, `FamilyInviteRevoked`, `FamilyAgentKeyIssued`, `FamilyAgentKeyRevoked` | `CreateFamily`, `IssueFamilyInvite`, `JoinFamily`, `RevokeFamilyInvite`, `IssueFamilyAgentKey`, `RevokeFamilyAgentKey` |
| `Draft` | `draft-{id}` | *(no existe hoy — cargas conversacionales vía agente MCP)* | `DraftCreated`, `DraftUpdated`, `DraftConfirmed`, `DraftDiscarded` | `CreateDraft`, `UpdateDraft`, `ConfirmDraft`, `DiscardDraft` |

**No son aggregates** (son read-models/queries derivados, sin comandos propios):
- **DualPay** — cálculo puro (BN, dólar oficial, dólar CCL → pesos/USD/total). Un endpoint de cálculo sin
  estado, no un aggregate. Ver detalle en `DOMAIN_MODEL.md §9`.
- **Price History** — proyección derivada de `Expense`/`Ticket` (agrupa por descripción/producto, calcula
  deltas de precio). Se reconstruye 100% desde esos streams, no tiene eventos propios.
- **Novedades del día (BusinessDay)** — query calculada sobre `CreditCard`/`InstallmentPurchase`/`Loan`/
  `Service` cruzando `dueDay`/`closingDay` con la fecha abierta por `BusinessDayOpened`. Ver `mcp-tool-server`.
- **"Copiar mes anterior"** — no es un aggregate ni un evento nuevo: es una operación del service de aplicación
  que lee `Reserve`/`BudgetPlan` de `fromMonth` y emite los MISMOS comandos (`SetReserveMonthAmount`,
  `SetBudgetLimits`) contra `toMonth`, solo para entradas que falten. Ver [[application-service-layer]].

## Mapeo de los nombres de ejemplo del pedido original

Si en algún momento se habla de "PagoRealizado", "CompraRealizada", "NuevoPréstamo", "NuevaTarjeta" (nombres
ilustrativos usados al plantear la idea del proyecto), el mapeo real al modelo de arriba es:

| Nombre ilustrativo | Evento(s) real(es) |
|---|---|
| NuevaTarjeta | `CardRegistered` |
| NuevoPréstamo | `LoanRegistered` |
| CompraRealizada | `InstallmentPurchaseRegistered` (compra en cuotas) o `ExpenseRegistered`/`TicketRegistered` (compra al contado) |
| PagoRealizado | Ambiguo a propósito — puede ser (a) un gasto nuevo: `ExpenseRegistered`/`TicketRegistered`, o (b) marcar pagado un vencimiento ya proyectado: `InstallmentMonthPaidToggled` / `LoanMonthPaidToggled` / `ServiceMonthPaidToggled`. Elegí el evento según si hay dinero saliendo AHORA (gasto) o un check administrativo sobre algo ya proyectado. |

## Invariantes/decisiones ya tomadas (no las redecidas por aggregate)

1. **`Bank` no se puede remover si tiene `CreditCard` o `Loan` activos referenciándolo** (igual que hoy).
2. **`CreditCard` no se puede remover si tiene `InstallmentPurchase` asociadas** — y a diferencia del frontend
   actual (que tiene este gap), TAMBIÉN se bloquea si tiene `Service` con `linkedCardId` apuntándole. Mejora
   deliberada, ver `DOMAIN_MODEL.md §2`.
3. **`Loan.ReviseLoanSchedule` SÍ regenera el calendario** (a diferencia del frontend actual, que no lo hace al
   editar un préstamo) — se unifica con el comportamiento de `InstallmentPurchase`, que es el correcto. Mejora
   deliberada, ver `DOMAIN_MODEL.md §4`.
4. **`Person` se archiva, no se borra físicamente** (`PersonArchived`, no un `PersonRemoved`) — evita
   referencias `ownerId` huérfanas en `Expense`/`Service`/`InstallmentPurchase`. El valor especial `Shared`
   (antes `'shared'` hardcodeado) es parte del value object `OwnerRef` (`Unassigned | Shared | Owner(personId)`),
   nunca una fila de `Person`.
5. **`Income` es un único stream global** (no versionado por mes, igual que hoy) pero cada `IncomeUpdated`
   queda en el event store con su timestamp — la proyección mantiene además una tabla de histórico apendable
   para poder responder "cuál era el ingreso neto en Febrero 2026" sin necesitar un aggregate por mes.
6. **El merge P2P del frontend (`mergeUtils.js`/`dirtyTracker.js`/`autoSave.js`) queda obsoleto** una vez que el
   backend es la fuente de verdad — se reemplaza por lectura directa del read-model (multi-dispositivo/
   multi-sesión ven la misma base). El concepto de `_updatedAt` por sub-ítem SÍ se preserva como el timestamp
   natural de cada evento.
7. **`BusinessDayOpened` es idempotente por fecha** — el comando `OpenBusinessDay(date)` usa
   `ExpectedState.New` sobre el stream `business-day-{date}`; si el `IHostedService` corre dos veces el mismo
   día (restart del container), el segundo intento falla el guard silenciosamente (se loguea, no se reintenta).
8. **Familias sin emails ni SMS** (ver `DOMAIN_MODEL.md §17`): la posesión del token de miembro reemplaza a la
   posesión del JSON de la maqueta. Crear una familia requiere un código del administrador de la app
   (`AdminInvite`, un solo uso); unirse requiere una invitación QR de la familia (un solo uso, con
   vencimiento). Tokens y códigos se persisten SIEMPRE como SHA-256, nunca en crudo. Todo aggregate de datos
   lleva `FamilyId` en su evento `*Registered`; `BudgetPlan` es `budget-{familyId}-{mes}` e `Income` es
   `income-{familyId}`; `BusinessDay` es global. `FamilyMember` (credencial) NO es `Person` (etiqueta de
   atribución de gastos dentro de la familia).

9. **Borradores (`Draft`) solo para cargas conversacionales** — `Expense`, `Ticket` e
   `InstallmentPurchase` (las cargas "de la fila del super"). El payload es TODO opcional y cada
   `DraftUpdated` es un snapshot completo (el stream audita cómo la conversación moldeó la carga); la
   validación fuerte corre recién al confirmar, cuando `DraftService` dispara el comando REAL y sus guards
   deciden. NO agregar drafts a entidades de setup (bancos, tarjetas, servicios, préstamos) — tienen
   formularios propios y baja frecuencia. Ver `DOMAIN_MODEL.md §19`.

## Procedimiento al modelar un feature nuevo del dominio

1. Buscá la fila correspondiente en la tabla de arriba. Si no existe, decidí: ¿es un aggregate nuevo, o es un
   evento nuevo de uno existente, o es un read-model derivado sin comandos propios?
2. Si es un aggregate nuevo o un evento nuevo: implementalo con [[eventuous-event-sourced-aggregate]].
3. Actualizá la tabla de este skill Y `backend/docs/DOMAIN_MODEL.md` en el mismo cambio — este documento tiene
   que reflejar SIEMPRE el estado real del código, no quedar como una foto del día del spike inicial.
4. Si el feature necesita consulta/listado: proyección ([[eventuous-projection-readmodel]]) + service
   ([[application-service-layer]]) + endpoint ([[aspnet-rest-endpoint]]) y, si aplica, tool MCP
   ([[mcp-tool-server]]).

## Verificación

- La tabla de aggregates de este skill y la de `backend/docs/DOMAIN_MODEL.md` no se contradicen.
- Todo evento nuevo tiene una fila en la tabla (o está documentado como parte de un aggregate existente).
- Ningún concepto nuevo del dominio quedó "flotando" sin decidir si es aggregate, read-model derivado, o
  cálculo sin estado.

## Anti-patrones

- ❌ Crear un aggregate para algo que es 100% derivable de otros streams (ej. Price History, DualPay) — eso es
  una proyección o un cálculo, no un aggregate con comandos propios.
- ❌ Reintroducir `'shared'` como string mágico en vez de modelarlo en el value object `OwnerRef`.
- ❌ Copiar el gap de integridad del frontend (borrar tarjeta con servicios vinculados, o no regenerar el
  calendario de un préstamo editado) asumiendo que "así es como funciona hoy" — este documento ya decidió
  corregir esos dos casos; si aparece un tercer gap real del frontend, decidilo explícitamente acá primero.
- ❌ Modelar `Income`/`BudgetPlan` como aggregates por-id genéricos — sus streams tienen clave natural
  (singleton / mes), no un GUID.
