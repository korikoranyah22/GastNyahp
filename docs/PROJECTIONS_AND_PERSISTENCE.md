# Proyecciones y persistencia

## Dos modelos en PostgreSQL

GastNyahp usa la misma instancia de PostgreSQL para dos responsabilidades separadas:

- schema Eventuous: streams, mensajes append-only y checkpoints;
- schema relacional de EF Core: read-models consultables.

Las tablas EF son derivadas. La historia del dominio vive en el event store.

## ProjectionsDbContext

`ProjectionsDbContext` contiene tablas para:

| Área | Tablas principales |
| --- | --- |
| Acceso | `admin_invites`, `families`, `family_members`, `family_invites`, `family_agent_keys`, `member_sessions`, `password_resets` |
| Catálogo | `banks`, `credit_cards`, `people` |
| Instrumentos | `installment_purchases`, `installment_months`, `loans`, `loan_months`, `services`, `service_month_amounts` |
| Planificación | `reserves`, `reserve_month_overrides`, `budget_plans`, `income`, `income_history`, `business_days` |
| Consumo | `expenses`, `tickets`, `ticket_items` |
| Conversación | `drafts` |

La configuración de claves, índices, relaciones, longitudes y conversiones vive en `OnModelCreating`.

## Entidades de lectura

Las clases `*Entity` son shapes de consulta, no aggregates. Pueden:

- desnormalizar datos;
- mantener colecciones para facilitar `Include`;
- guardar contadores o estados derivados;
- usar claves compuestas adecuadas a una consulta.

No deben contener guards ni aceptar acciones de negocio.

## Handlers de proyección

Cada `*Projection` hereda de `GastNyahpProjection`, que a su vez hereda del `EventHandler` de Eventuous.

La clase base:

- registra todos los `[EventType]` del assembly Domain en `TypeMap`;
- ofrece `SaveIgnoringDuplicate` para inserts idempotentes;
- comparte `StreamIds` para extraer ids desde nombres de stream.

Cada handler registra un `On<TEvent>` y aplica el hecho a una o más tablas EF.

## Subscription `$all`

En Postgres existe una única subscription llamada `gastnyahp-projections`. Consume el stream global y ejecuta
las quince proyecciones registradas:

- bancos;
- tarjetas;
- cuotas;
- préstamos;
- servicios;
- reservas;
- personas;
- gastos;
- tickets;
- presupuestos;
- ingresos;
- día hábil;
- familias;
- invitaciones administrativas;
- drafts.

El checkpoint se confirma por evento y con demora mínima para que la UI pueda releer inmediatamente después de
una mutación.

## Idempotencia

Una proyección puede recibir un evento nuevamente por replay, reinicio o carrera. Los inserts deben tolerarlo.
El patrón combina:

- búsqueda previa por clave;
- restricciones únicas en la base;
- captura específica de PostgreSQL `23505`.

No se ignoran excepciones generales. Una excepción no capturada detiene el avance del checkpoint compartido y
retrasa todas las proyecciones posteriores.

## Read-your-writes

El sistema no actualiza EF directamente desde el servicio. Tras escribir eventos espera el progreso del
proyector:

- Postgres: compara el checkpoint contra el `global_position` máximo observado;
- in-memory: reproduce de forma síncrona el log global pendiente.

Esto conserva el principio de una sola ruta de proyección y permite que un `POST` seguido de un `GET` vea el
cambio en condiciones normales.

## Proveedor de base de proyecciones

`Database:Provider` admite:

- `Postgres`, modo normal de despliegue;
- `Sqlite`, usado por integración y E2E para aislar escenarios.

`IDbContextFactory<ProjectionsDbContext>` evita compartir contextos entre requests, servicios singleton y
handlers de background.

## Migraciones

Las migraciones viven en `Infrastructure/Migrations/Projections`. `ProjectionsDatabaseInitializer` ejecuta
`MigrateAsync` al arrancar. La design-time factory permite usar `dotnet ef` sin bootear toda la API.

Una migración cambia el read-model, no la forma de eventos históricos. Si también cambia un evento publicado,
se necesita versionado de evento y compatibilidad de replay.

## Rebuild y replay

Conceptualmente, un read-model puede reconstruirse desde cero:

1. preparar tablas vacías con las migraciones vigentes;
2. reiniciar el checkpoint de la subscription;
3. reproducir `$all` en orden global;
4. dejar que cada proyección reconstruya sus filas.

El `InMemoryProjectionPump.Rewind` cubre este comportamiento en tests. En producción, cualquier procedimiento de
rebuild debe coordinar tablas y checkpoint para evitar mezclar datos viejos con replay parcial.

## Consistencia y fallos

- El append al event store es la confirmación de la escritura de dominio.
- La proyección es asíncrona, aunque el request normalmente espera su checkpoint.
- Si el catch-up excede diez segundos, la escritura sigue siendo válida y la lectura converge después.
- No se debe “reparar” una demora escribiendo directamente en tablas EF.

## Cómo agregar una proyección

1. Crear o extender la entidad de lectura.
2. Agregar `DbSet` y configuración EF.
3. Implementar handlers idempotentes.
4. Registrar la proyección en `AddGastNyahpProjections`.
5. Agregarla al builder de la subscription Postgres.
6. Crear la migración.
7. Probar escritura seguida de lectura y replay.

## Referencias

- [Arquitectura](ARCHITECTURE.md)
- [Event sourcing](EVENT_SOURCING.md)
- [Comandos y servicios](COMMANDS_AND_SERVICES.md)
