# Event sourcing y Eventuous

## Modelo de persistencia

El estado de negocio se guarda como una secuencia append-only de eventos. Una fila mutable de EF no es la
fuente de verdad de un banco, tarjeta, gasto o familia; lo es el stream de su aggregate.

En producción, Eventuous usa PostgreSQL y el schema configurado por `EventStore:Schema` — normalmente
`eventuous`. Las proyecciones EF viven en el schema relacional habitual.

## Estructura de un aggregate

La convención, tomada de la skill `eventuous-event-sourced-aggregate`, reúne cuatro bloques por archivo:

1. eventos;
2. state;
3. comandos;
4. command service.

Ejemplos: `Domain/Banks/Bank.cs`, `Domain/Expenses/Ticket.cs` y `Domain/Access/Family.cs`.

## Eventos

Cada evento:

- representa un hecho ya ocurrido;
- es un `record` inmutable;
- lleva `[EventType("V1.Nombre")]`;
- pertenece a un namespace de versión como `V1`;
- usa valores serializables y fechas ISO-8601.

Un evento publicado no debe cambiar de forma incompatible. Si cambia el contrato, se agrega una nueva versión y
se mantiene la capacidad de reproducir eventos anteriores.

## State

Cada aggregate define un `record ...State : State<TState>`. El constructor registra transiciones `On<TEvent>` y
cada transición devuelve un nuevo estado con `with`.

El state:

- se reconstruye reproduciendo el stream;
- no se persiste como snapshot en la implementación actual;
- no ejecuta reglas de negocio;
- solo pliega hechos en memoria.

## Comandos

Los comandos son intenciones, no hechos. Ejemplos:

- `RegisterExpense`;
- `ReviseInstallmentSchedule`;
- `ToggleServiceMonthPaid`;
- `IssueFamilyAgentKey`;
- `ConfirmDraft`.

Un comando puede ser rechazado. Un evento no: si está en el stream, ya forma parte de la historia.

## Command services

Cada aggregate tiene un `CommandService<TState>` que:

- selecciona el stream;
- declara si espera estado nuevo o existente;
- reconstruye el state;
- ejecuta un handler estático;
- agrega cero, uno o varios eventos.

Los handlers usan guards con `DomainException` y emiten eventos con `yield return`. No acceden a EF, HTTP ni
servicios externos.

## Nombres de streams

Los streams suelen seguir `<aggregate>-<id>`, por ejemplo:

- `bank-{guid}`;
- `card-{guid}`;
- `expense-{guid}`;
- `draft-{guid}`;
- `family-{guid}`.

Los singletons por familia incorporan la partición en el nombre, como presupuesto por mes o ingreso familiar.
`BusinessDay` es una excepción global y usa la fecha como identidad.

Las proyecciones recuperan el id desde el stream cuando un evento de transición no lo repite, mediante
`StreamIds.GuidFrom`.

## Catálogo de aggregates

| Aggregate | Intenciones principales |
| --- | --- |
| `AdminInvite` | emitir y canjear el permiso de creación familiar |
| `Family` | crear familia, invitar, unir, emitir/revocar claves, credenciales, sesiones y resets |
| `Bank` | registrar, editar y retirar |
| `CreditCard` | registrar, editar, activar/desactivar y retirar |
| `InstallmentPurchase` | registrar, revisar calendario, editar detalle, ajustar/pagar mes, finalizar y retirar |
| `Loan` | registrar, revisar calendario, editar detalle, ajustar/pagar mes y retirar |
| `Service` | registrar, editar, definir/extender importes, pagar, activar/desactivar y retirar |
| `Reserve` | registrar, editar, sobrescribir mes, aplicar base y retirar |
| `Person` | registrar, editar y archivar |
| `Expense` | registrar, editar y retirar un gasto simple |
| `Ticket` | registrar, reemplazar y retirar una compra con ítems |
| `BudgetPlan` | definir límites de un mes |
| `Income` | actualizar datos de ingreso y cotizaciones |
| `BusinessDay` | abrir un día de negocio de forma idempotente |
| `Draft` | crear, completar, cambiar tipo, confirmar o descartar un borrador conversacional |

El detalle de campos e invariantes está en [DOMAIN_MODEL.md](DOMAIN_MODEL.md).

## Proveedores de event store

### PostgreSQL

`Eventuous.Postgresql` aporta `PostgresStore`, checkpoint store y subscription al `$all`. La aplicación crea el
schema mediante `EventStoreSchemaInitializer` antes de iniciar Eventuous.

### In-memory

`InMemoryEventStore` implementa `IEventStore` para tests. Conserva streams y un log global. No pretende ser una
base productiva ni sobrevivir al proceso.

## Concurrencia y consistencia

Eventuous controla la revisión esperada del stream al agregar eventos. Las invariantes internas del aggregate se
evalúan sobre el state reconstruido. Las invariantes que involucran otros aggregates se verifican antes, en los
servicios de aplicación.

## Read-your-writes

Después de un comando exitoso, `CommandExecutor` llama a `IReadModelSync.CatchUp`:

- en memoria, `InMemoryProjectionPump` entrega todos los eventos pendientes a todas las proyecciones;
- en Postgres, `SubscriptionReadModelSync` espera que el checkpoint de `gastnyahp-projections` alcance el head
  observado al comenzar la espera.

El timeout de Postgres es de diez segundos y degrada a consistencia eventual en vez de convertir una escritura
válida en error.

## Reglas al extender el dominio

1. No actualizar directamente una tabla de proyección para representar una acción de negocio.
2. Agregar comando y evento con intención explícita.
3. Mantener handlers puros y estáticos.
4. Registrar el command service en `AddGastNyahpCommandServices`.
5. Agregar o actualizar la proyección idempotente.
6. Registrar la proyección tanto en DI como en la subscription `$all`.
7. Cubrir guards con tests de dominio y el flujo completo con integración.
