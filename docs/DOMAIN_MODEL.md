# Modelo de dominio vigente de GastNyahp

> Estado: implementado. Este documento describe el código actual de `GastNyahp.Domain` y las invariantes
> cruzadas aplicadas por `GastNyahp.Infrastructure.Services`. Ante una diferencia, mandan el código y las
> pruebas automatizadas.

## 1. Límites y principios

GastNyahp modela las finanzas de una familia. Cada escritura de negocio pasa por un aggregate event-sourced y
produce eventos append-only. Las tablas EF son proyecciones de lectura y no se modifican como atajo.

Principios:

- `FamilyId` separa los datos de cada familia;
- GUID identifica aggregates y principals;
- meses usan `yyyy-MM`, días `yyyy-MM-dd` y timestamps ISO-8601;
- importes se validan en el aggregate;
- referencias a otros aggregates se validan en servicios de aplicación;
- eventos publicados son contratos inmutables y versionados;
- bajas financieras son hechos del stream; personas se archivan para conservar atribución histórica.

La arquitectura técnica está en [ARCHITECTURE.md](ARCHITECTURE.md) y el mecanismo Eventuous en
[EVENT_SOURCING.md](EVENT_SOURCING.md).

## 2. Conceptos compartidos

### 2.1 OwnerRef

`OwnerRef` expresa a quién corresponde un gasto o instrumento:

- `Unassigned`: sin atribución;
- `Shared`: familiar/compartido;
- `Owner`: referencia a una persona concreta.

Cuando se usa `Owner`, el servicio de aplicación comprueba que exista en la misma familia. La proyección
conserva la referencia aunque la persona después sea archivada.

### 2.2 PaymentMethod

`PaymentMethod` distingue:

- efectivo;
- tarjeta de crédito (Card, con CardId);
- débito bancario (Debit, con BankId);
- MODO;
- Mercado Pago.

Las variantes con tarjeta llevan el id del instrumento. El servicio verifica pertenencia y tipo antes de emitir
el comando de gasto o ticket.

### 2.3 Categorías

`AppCategories` mantiene listas cerradas para gastos, tickets, cuotas y servicios. El backend valida categorías;
la IA puede inferirlas, pero no crear valores arbitrarios fuera del catálogo.

### 2.4 Calendarios

`YearMonth` valida y opera meses. `MonthlySchedule` genera y revisa calendarios de cuotas y préstamos preservando
meses ya pagados. `BillingCycle` calcula cierre y mes de pago de tarjetas.

### 2.5 Secretos y contraseñas

`SecretHash` protege tokens de invitación, credenciales de posesión y claves de agente. `PasswordHash` y
`PasswordPolicy` están separados porque una contraseña humana requiere política y derivación específicas.
Nunca se persisten secretos reutilizables en texto plano.

## 3. Acceso y familias

### 3.1 AdminInvite

Stream: `admin-invite-{inviteId}`.

| Comando | Evento | Regla principal |
| --- | --- | --- |
| `IssueAdminInvite` | `AdminInviteIssued` | guarda hash, expiración opcional y si concede propiedad de instancia |
| `RedeemAdminInvite` | `AdminInviteRedeemed` | solo una vez y antes de vencer |

La API key de instancia autoriza emitir estos códigos; no reemplaza la invitación ni se convierte en credencial
familiar.

### 3.2 Family

Stream: `family-{familyId}`.

`Family` reúne miembros, invitaciones, claves de agente, credenciales, sesiones y resets porque esas operaciones
deben ser consistentes dentro de una familia.

| Área | Comandos |
| --- | --- |
| alta | `CreateFamily` |
| miembros | `IssueFamilyInvite`, `JoinFamily`, `RevokeFamilyInvite` |
| agentes | `IssueFamilyAgentKey`, `RevokeFamilyAgentKey` |
| cuenta | `SetMemberCredentials`, `ChangeMemberPassword` |
| sesiones | `IssueMemberSession`, `RevokeMemberSession` |
| recuperación | `IssuePasswordReset`, `RedeemPasswordReset` |

Reglas principales:

- la creación consume una invitación administrativa válida;
- el fundador entra como `Admin`;
- los roles de miembros son `Admin` y `Member`;
- una invitación familiar es de un solo uso, revocable y con vencimiento;
- solo un admin emite o revoca claves de agente;
- emails se normalizan y son únicos dentro de la familia;
- la contraseña cumple `PasswordPolicy` y se guarda derivada;
- cambiar contraseña revoca las sesiones existentes según el flujo de aplicación;
- sesiones y resets son revocables o de un solo uso;
- los tokens sin hash se muestran solamente al emitirlos.

`FamilyService` agrega login, selección de familia cuando un email coincide en varias, throttling y validaciones
de privilegio. El diseño detallado está en [ACCOUNTS_AND_LOGIN.md](ACCOUNTS_AND_LOGIN.md).

## 4. Catálogo financiero

### 4.1 Bank

Stream: `bank-{bankId}`.

Comandos: `RegisterBank`, `UpdateBank`, `RemoveBank`.

Invariantes: nombre requerido; el banco pertenece a una familia; no puede retirarse mientras tenga tarjetas o
préstamos dependientes. Esta última regla se aplica en `BankService` porque cruza aggregates.

### 4.2 CreditCard

Stream: `card-{cardId}`.

Comandos: `RegisterCard`, `UpdateCard`, `ActivateCard`, `DeactivateCard`, `RemoveCard`.

Tipos: `Credit` y `Debit`. Redes: `Visa` y `Mastercard`.

Invariantes:

- banco de la misma familia;
- etiqueta requerida;
- días de cierre y vencimiento válidos;
- no retirar una tarjeta con cuotas o servicios dependientes;
- activar/desactivar es una transición explícita, no una edición de detalle.

### 4.3 Person

Stream: `person-{personId}`.

Comandos: `RegisterPerson`, `UpdatePerson`, `ArchivePerson`.

Una persona es una etiqueta de atribución dentro de la familia, no una identidad de login. Se archiva en lugar de
eliminarse para que `OwnerRef` históricos sigan resolviendo.

## 5. Instrumentos con calendario

### 5.1 InstallmentPurchase

Stream: `installment-{installmentId}`.

Frecuencias:

- `Fixed`: cantidad finita de cuotas;
- `Monthly`: ventana recurrente abierta manejada por el calendario del dominio.

Comandos:

- `RegisterInstallmentPurchase`;
- `ReviseInstallmentSchedule`;
- `UpdateInstallmentDetails`;
- `OverrideInstallmentMonthAmount`;
- `ToggleInstallmentMonthPaid`;
- `FinishInstallment`;
- `RemoveInstallmentPurchase`.

Reglas: tarjeta familiar existente, descripción y categoría válidas, importe positivo, fecha/mes válidos y dueño
resoluble. Revisar el calendario preserva pago e importe de meses ya pagados. Finalizar y retirar son estados
distintos: finalizar conserva el instrumento como completado; retirar lo excluye de uso normal.

### 5.2 Loan

Stream: `loan-{loanId}`.

Comandos: registrar, revisar calendario, editar detalle, sobrescribir importe mensual, alternar pagado y retirar.

Reglas: banco familiar existente, descripción requerida, cuota mensual positiva, cantidad de cuotas positiva,
mes inicial válido y total opcional no negativo. La cantidad pagada se deriva de los meses y no se incrementa
como contador mutable independiente.

### 5.3 Service

Stream: `service-{serviceId}`.

Periodicidades: mensual, bimestral y trimestral. Monedas de entrada: ARS y USD.

Comandos:

- registrar y editar detalle;
- activar/desactivar;
- fijar el importe de un mes;
- extender importes futuros;
- alternar pago mensual;
- retirar.

Los importes USD se convierten a ARS usando el CCL familiar y conservan monto/moneda original cuando aplica. Un
servicio puede vincularse a una tarjeta de la misma familia y atribuirse mediante `OwnerRef`.

## 6. Consumo

### 6.1 Expense

Stream: `expense-{expenseId}`.

Comandos: `RegisterExpense`, `UpdateExpense`, `RemoveExpense`.

Representa un gasto simple con fecha, descripción, categoría, monto, moneda, medio de pago y dueño. El monto debe
ser positivo. Si ingresa en USD, el evento/proyección conserva la conversión canónica a ARS y los datos de origen
necesarios.

### 6.2 Ticket

Stream: `ticket-{ticketId}`.

Comandos: `RegisterTicket`, `UpdateTicket`, `RemoveTicket`.

Representa una compra con múltiples ítems. Cada ítem tiene descripción, monto, categoría y dueño. El descuento se
aplica al total sin producir un total negativo. `UpdateTicket` reemplaza el snapshot completo de ítems; las tools
de corrección construyen ese reemplazo a partir del estado leído.

## 7. Planificación

### 7.1 Reserve

Stream: `reserve-{reserveId}`.

Tipos: reserva, efectivo, deuda u otro.

Comandos: registrar, editar detalle, fijar override mensual, aplicar importe base y retirar.

La resolución mensual usa prioridad: override del mes, luego base si es recurrente, luego cero. Aplicar una nueva
base elimina overrides existentes deliberadamente para restablecer el plan completo.

### 7.2 BudgetPlan

Stream: `budget-{familyId}-{month}`.

`SetBudgetLimits` define límites de crédito, débito/efectivo y semanal para un mes. Los valores no pueden ser
negativos. El servicio de planificación ofrece semántica de actualización completa y copia de estimaciones entre
meses.

### 7.3 Income

Stream: `income-{familyId}`.

`UpdateIncome` mantiene sueldo neto, dólar oficial, dólar CCL y porcentaje de reparto. Los campos son opcionales
para permitir actualización parcial coordinada por el servicio. La proyección conserva estado vigente e
historial de cambios.

### 7.4 Dual Pay

Dual Pay es un cálculo puro del servicio de planificación. No tiene aggregate ni eventos porque no representa un
hecho persistido.

## 8. Día hábil

Stream: `business-day-{date}`.

`OpenBusinessDay` produce `BusinessDayOpened` una sola vez por fecha. `BusinessDayScheduler` abre el día al
arrancar y según horario/zona configurados.

Las novedades del día son una consulta derivada que combina cuotas, préstamos, servicios y tarjetas; no son un
aggregate adicional.

## 9. Borradores conversacionales

Stream: `draft-{draftId}`.

Tipos: `Expense`, `Ticket` e `Installment`. Estados: `Open`, `Confirmed`, `Discarded`.

Comandos:

- `CreateDraft`;
- `UpdateDraft`;
- `ChangeDraftKind`;
- `ConfirmDraft`;
- `DiscardDraft`.

`DraftPayload` admite campos incompletos y una lista de ítems de ticket. Cada actualización guarda un snapshot
completo del payload, por lo que el stream conserva cómo evolucionó la conversación.

Confirmar es una orquestación de `DraftService`:

1. lee y valida el borrador abierto;
2. ejecuta el servicio real de gasto, ticket o cuota;
3. espera su proyección;
4. emite `DraftConfirmed` con el id creado.

Si la carga real falla, el draft permanece abierto. Descartar lo cierra sin crear una entidad financiera.

## 10. Invariantes cruzadas

Estas reglas viven en servicios de aplicación porque requieren más de un stream o una consulta proyectada:

- toda entidad consultada/modificada pertenece a la familia autenticada;
- banco de tarjeta/préstamo y tarjeta de cuota/servicio existen en esa familia;
- referencias de persona y medios de pago resuelven correctamente;
- no se retiran catálogos con dependencias activas;
- conversión USD usa el CCL familiar configurado;
- creación y unión familiar consumen invitaciones válidas;
- operaciones administrativas verifican rol;
- confirmación de draft crea primero la entidad definitiva y cierra después el borrador.

## 11. Consistencia y proyecciones

Tras un comando exitoso, el servicio espera `IReadModelSync.CatchUp`. En Postgres espera el checkpoint de la
subscription `$all`; en tests el pump en memoria reproduce el log pendiente. Esto brinda read-your-writes sin
convertir las tablas EF en una segunda ruta de escritura.

Las proyecciones son idempotentes y reconstruibles. Más detalle en
[PROJECTIONS_AND_PERSISTENCE.md](PROJECTIONS_AND_PERSISTENCE.md).

## 12. Superficies del dominio

- REST expone operaciones humanas y del frontend;
- MCP expone consultas, borradores, ABM, instrumentos y correcciones;
- OAuth permite autorizar clientes MCP;
- las skills descargables explican a los agentes cuándo y cómo usar las tools.

Todas las superficies convergen en los mismos servicios. Consultá [INTERFACES.md](INTERFACES.md).

## 13. Fuera del dominio persistido

No son aggregates:

- estado visual de React/Zustand;
- progreso efímero de importación;
- throttling de login en memoria;
- cálculo Dual Pay;
- novedades del día;
- conexión SignalR;
- paquetes ZIP de skills.

Pueden usar datos del dominio, pero no justifican un stream propio mientras no representen hechos auditables.

## 14. Cómo mantener este documento

Cuando cambie el dominio:

1. actualizar comando, evento, state y tests;
2. actualizar servicio y proyección;
3. actualizar esta referencia en el mismo cambio;
4. actualizar `ACCOUNTS_AND_LOGIN.md` si cambia acceso;
5. actualizar skills MCP si cambia la forma conversacional de operar la capacidad.

El spike original se conserva en [HISTORICAL_DOMAIN_SPIKE.md](HISTORICAL_DOMAIN_SPIKE.md) únicamente como
registro de la migración desde la maqueta local-first.
