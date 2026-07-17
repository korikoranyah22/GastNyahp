# Comandos y servicios de aplicación

## Responsabilidades

La arquitectura separa tres tipos de pieza que suelen confundirse:

| Pieza | Decide | Puede consultar EF | Puede emitir eventos |
| --- | --- | --- | --- |
| Command handler | invariantes internas de un aggregate | no | sí |
| Servicio de aplicación | ownership, referencias e invariantes cruzadas | sí | mediante un command service |
| Controller/tool MCP | transporte, autenticación y formato de respuesta | no directamente | no directamente |

## Pipeline compartido

`CommandExecutor.Exec` es el camino normal de escritura:

1. espera `CommandService.Handle`;
2. si Eventuous rechaza el comando, registra el error y devuelve `OpResult.Fail`;
3. si funciona, espera `IReadModelSync.CatchUp`;
4. devuelve `OpResult.Success`, opcionalmente con el id creado.

`OpResult` evita que excepciones de dominio crucen las capas como errores no controlados. REST lo convierte en
`422 Unprocessable Entity`; MCP lo expresa en su respuesta de tool.

## Invariantes internas y cruzadas

Los command handlers validan lo que puede decidirse con un solo stream: estado activo, transición permitida,
campos requeridos, calendario válido o importe no negativo.

Los servicios validan lo que necesita read-models adicionales. Ejemplos:

- una tarjeta debe referenciar un banco de la misma familia;
- no se retira un banco con instrumentos dependientes;
- una compra en cuotas debe referenciar una tarjeta existente;
- un `OwnerRef.Person` debe resolver a una persona válida;
- un servicio vinculado debe apuntar a una tarjeta familiar;
- una operación solo puede modificar una entidad perteneciente a la familia autenticada.

## Servicios registrados

| Servicio | Escritura | Lecturas destacadas |
| --- | --- | --- |
| `BankService` | alta, edición y retiro | bancos por familia e id |
| `CardService` | alta, edición, activar/desactivar y retiro | tarjetas por familia e id |
| `InstallmentService` | alta, detalle, revisión, importe/pago mensual, finalización y retiro | compras, meses y filtro por tarjeta |
| `LoanService` | alta, detalle, revisión, importe/pago mensual y retiro | préstamos y calendarios |
| `ServicesService` | alta, detalle, importes, extensión, pago, activación y retiro | servicios y montos mensuales |
| `ReserveService` | alta, detalle, override mensual, base y retiro | reservas familiares |
| `PersonService` | alta, edición y archivo | personas activas o históricas |
| `ExpenseService` | alta, edición y retiro | gastos por mes |
| `TicketService` | alta, reemplazo y retiro | tickets con ítems por mes |
| `PlanningService` | presupuesto, ingreso y copiar mes | presupuestos, ingreso y Dual Pay |
| `BusinessDayService` | apertura del día | estado y novedades derivadas |
| `FamilyService` | familias, invitaciones, claves, login, sesiones y resets | credenciales, miembros y sesiones |
| `DraftService` | crear, actualizar, confirmar y descartar | borradores abiertos |
| `LegacyImportService` | importación/reemplazo coordinado | detección de datos existentes |

`ImportJobTracker` mantiene el progreso efímero de una importación. `LoginThrottle` mantiene backoff entre
requests y por eso ambos tienen consideraciones de ciclo de vida distintas de un servicio CRUD.

## Familias y autorización

Los métodos de datos reciben `familyId` explícitamente. El ownership se comprueba antes del comando, normalmente
con una consulta `AnyAsync` o `FirstOrDefaultAsync` filtrada por familia e id.

`FamilyService` además maneja:

- hashes de invitaciones y tokens;
- hashing de contraseñas humanas;
- login con posible selección de familia;
- sesiones revocables por dispositivo;
- recuperación de contraseña;
- roles `Admin`, miembro y `Agent`.

Los secretos sin hash solo se devuelven una vez al emitirlos.

## Drafts conversacionales

`DraftService` es el puente entre una conversación y una escritura definitiva:

1. crea un borrador `Expense`, `Ticket` o `Installment`;
2. permite completar campos e ítems progresivamente;
3. valida ownership, categorías, medio de pago y persona;
4. al confirmar, llama al servicio real correspondiente;
5. solo entonces marca el draft como confirmado con el id resultante.

Esto mantiene separados el estado incompleto de conversación y los aggregates financieros definitivos.

## Convenciones de código

- Servicios con primary constructors y dependencias inyectadas.
- Métodos con intención de negocio, no un ejecutor genérico de objetos.
- `CancellationToken` propagado hasta EF y Eventuous.
- `IDbContextFactory` y un contexto corto por operación.
- `OpResult` para rechazos esperables.
- Logging del rechazo en la capa que ejecuta el comando.
- Ningún controller ni tool MCP debe inyectar un `DbContext` o modificar proyecciones directamente.

## Cómo agregar una operación

1. Determinar si modifica un aggregate existente o requiere uno nuevo.
2. Crear un comando con nombre de intención.
3. Crear el evento que representa el hecho resultante.
4. Implementar el guard en el command handler.
5. Exponer un método tipado en el servicio de aplicación.
6. Agregar validaciones cruzadas y ownership en ese servicio.
7. Actualizar la proyección.
8. Exponer REST y/o MCP llamando al mismo método.
9. Agregar tests de dominio, integración y, si cambia el contrato externo, E2E.

## Referencias

- [Event sourcing y Eventuous](EVENT_SOURCING.md)
- [Proyecciones y persistencia](PROJECTIONS_AND_PERSISTENCE.md)
- [Modelo de dominio](DOMAIN_MODEL.md)
