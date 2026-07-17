---
name: gastnyahp
description: Cargar compras y gastos en GastNyahp por conversación — el ticket del super ítem por ítem, descuentos, medio de pago y confirmación.
triggers: [super, supermercado, coto, jumbo, carrefour, changuito, chango, compra, compré, comprar, compras, gasto, gastos, gasté, gastar, ticket, factura, pagué, pagar, precio, cuesta, salió, descuento, promo, tarjeta, efectivo, débito, debito, mercadopago, cuotas, anotá, anota, apuntá, registrá, borrador, kiosco, farmacia, verduleria, verdulería, carniceria, carnicería, almacen, almacén, compartido, de quién, de quien, es mío, es mio]
tools: [borrador_crear, borrador_actualizar, borrador_item_agregar, borrador_item_dueno, borrador_item_quitar, borradores_listar, borrador_confirmar, borrador_descartar, tarjetas_listar, bancos_listar, personas_listar]
---

# GastNyahp — cargar una compra por conversación

Tenés herramientas reales para registrar gastos en GastNyahp (la app de finanzas de la familia). El usuario te dicta la compra —muchas veces por audio, en la fila del super— y vos la vas cargando. Esto es un procedimiento, no una sugerencia: si el usuario está registrando plata que gastó, **llamá las tools de verdad en este mismo turno**. No alcanza con decir que lo anotaste.

## El modelo mental: el borrador es el changuito

Todo pasa por un **borrador**. Se llena de a poco, se corrige, y **nada impacta la contabilidad hasta `borrador_confirmar`**. Tres tipos:

| Tipo | Cuándo |
|---|---|
| `ticket` | Compra con varios ítems (supermercado, kiosco). **El caso típico.** |
| `gasto` | Un solo monto, sin desglose ("cargué 5000 de nafta"). |
| `cuotas` | Compra financiada con tarjeta en N cuotas. Cae en el dashboard de cuotas, no en los gastos del día. |

El tipo **no es una decisión definitiva**: si a mitad de la carga aparece que era otra cosa, se convierte con `borrador_actualizar(tipo:…)` — ver D-ter.

## REGLA #1 — No recordás nada entre mensajes

Entre un mensaje y el siguiente se te olvida el `borradorId`, los ítems y el subtotal. **La fuente de verdad es GastNyahp, no tu memoria.**

En **cada** mensaje donde el usuario agregue, corrija, pague o confirme algo:

1. Llamá **`borradores_listar` primero** — salvo que ya hayas creado/visto el ticket en TU respuesta actual. Te devuelve el id, los ítems ya cargados y el subtotal.
2. Recién con ese id, hacé lo nuevo.
3. **Nunca** crees un segundo ticket si ya hay uno abierto. **Nunca** re-agregues un ítem que el listado ya muestra (lo duplicarías).

## Escenarios

### A. Arranca una compra
> — *"Estoy en el Coto, anotame: carne treinta lucas"*

1. `borradores_listar` → ¿hay un ticket abierto? Si no:
2. `borrador_crear(tipo:"ticket", descripcion:"Super Coto")` → te devuelve el id.
3. `borrador_item_agregar(borradorId, descripcion:"Carne", monto:30000)`
4. Respondé corto, con el subtotal que devolvió la tool: *"Anotado — carne 30.000."*

### B. Sigue dictando (el caso que más se repite)
> — *"dos yogures cuatro quinientos"* … *"un pan mil doscientos"*

1. `borradores_listar` → recuperás el id y lo ya cargado.
2. Un `borrador_item_agregar` **por cada ítem nuevo** (podés encadenar varios en el turno).
3. *"Van 3 — subtotal 41.500."*

Si el usuario dicta varias cosas de una, agregá todas. **Solo lo nuevo**, no lo que ya estaba.

### C. Corrige algo
> — *"no, sacá los yogures"* / *"el pollo eran 12 mil, no 21"*

1. `borradores_listar` → mirá la **numeración vigente**.
2. `borrador_item_quitar(borradorId, numero)`. Para corregir un monto: quitá y volvé a agregar.

### D. La caja: descuento y medio de pago
> — *"me hicieron 20% y pagué con la Visa"*

1. `borradores_listar` → id + subtotal.
2. Descuento: calculá el **monto en pesos** sobre el subtotal (20% de 41.500 = 8.300) → `borrador_actualizar(borradorId, descuento:8300)`.
3. Medio de pago: `Efectivo` | `MODO` | `MercadoPago` | `Tarjeta` | `Débito`.
   - Con `Tarjeta` necesitás el **nombre exacto**: si no lo sabés, `tarjetas_listar` y ofrecé opciones (*"¿Visa Galicia o Amex?"*). Igual con `Débito` → `bancos_listar`.
   - `borrador_actualizar(borradorId, medio:"Tarjeta", referencia:"Visa Galicia")`
4. Podés hacer descuento y medio en **una sola** llamada a `borrador_actualizar`.

### D-bis. De quién es cada ítem

En esta familia **cada ítem del ticket tiene dueño**. El dueño puede ser una **persona** (por nombre exacto — `personas_listar`), **`compartido`** (de toda la familia) o **`sin asignar`**.

**Nunca frenes la carga por esto.** El orden es: primero anotás el ítem, después resolvés el dueño.

- Si el usuario **ya lo dijo**, va en la misma llamada:
  > — *"el shampoo, dos mil quinientos, es mío"*
  > `borrador_item_agregar(borradorId, descripcion:"Shampoo", monto:2500, categoria:"Higiene", dueno:"Meli")`
- Si **no lo dijo**, agregá el ítem igual (queda sin dueño) y preguntá. `borradores_listar` te marca cuáles están **`sin dueño`**, así que podés retomarlo cuando quieras.
- Cuando te contesta, **no quites ni re-agregues**: `borrador_item_dueno(borradorId, numero, dueno)` sobre el ítem ya cargado.
  > — *"¿el shampoo es tuyo o compartido?"* — *"mío"*
  > `borradores_listar` → el shampoo es el ítem 3 → `borrador_item_dueno(borradorId, numero:3, dueno:"Meli")`
- **Preguntá de a poco, no interrogues.** Una pregunta por mensaje, natural, mientras sigue dictando. Si está dictando rápido, dejá que termine y barré al final.
- **Antes de cerrar** es el mejor momento para barrer lo que falta: mirá los `sin dueño` y preguntá por los que queden, agrupando si son varios: *"¿la carne y el pan son compartidos?"* → un `borrador_item_dueno` por cada uno.
- Si el usuario dice un nombre que **no está** en `personas_listar`, no lo inventes: preguntá cuál de los que existen es (o si es `compartido`).
- Si la familia **no tiene personas cargadas**, solo valen `compartido` y `sin asignar` — no insistas con nombres.

Para un `gasto` simple (no ticket) el dueño va en `borrador_actualizar(borradorId, dueno:"…")`, no por ítem.

### D-ter. Aparece que era en cuotas

El tipo del borrador **se puede cambiar**: no estás atado al que elegiste en el primer mensaje.

> — *"compré una tele, 600 lucas"* → arrancás un `gasto`
> — *"…ah, la pagué en 6 cuotas con la Visa"*

`borrador_actualizar(borradorId, tipo:"cuotas", tarjeta:"Visa Galicia", totalCuotas:6, cuotaMensual:100000)` — **en una sola llamada**. La conversión **no pierde** lo ya cargado (descripción, fecha, categoría).

- La **cuota mensual** es el monto de CADA cuota, no el total: 600.000 en 6 → `cuotaMensual: 100000`. Si el usuario te da el total, dividilo vos; si no cierra ("600 lucas en 6 de 120"), preguntá cuál es cuál.
- Las cuotas **siempre van con tarjeta** (no existen en efectivo). Si no sabés cuál, `tarjetas_listar`.
- `mesInicio` (yyyy-MM) es opcional: si no lo dice, se usa el mes de la fecha.
- Al confirmar cae en el **dashboard de cuotas**, no en los gastos del día. Eso es lo correcto.

**Nunca** descartes el borrador para rehacerlo como cuotas, y **nunca** lo cargues en una sola cuota porque "ya lo habías empezado como gasto".

### E. Cerrar
> — *"listo"* / *"pagué"* / *"ya está"*

1. `borrador_confirmar(borradorId)`.
2. Si responde que **falta algo** (medio de pago, ítems), **no se cargó**: pedí ese dato puntual, `borrador_actualizar`, y reintentá.
3. Cuando confirma: *"Listo, quedó cargado: Coto, 41.500 con la Visa."*

### F. Abandona
> — *"dejá, no lo cargues"* → `borrador_descartar(borradorId, motivo)`.

### G. Se equivocó, pero YA estaba confirmado
> — *"la carne del Coto fue 3.000, no 30.000"* (y el ticket ya se cargó)

Un borrador confirmado **ya no se toca con las tools de borrador** — pero se corrige igual:

| Qué | Cómo |
|---|---|
| Un producto de una compra | `tickets_del_mes` → `ticket_item_editar(ticketId, numero, monto:3000)` |
| Sacar un producto | `ticket_item_quitar_cargado(ticketId, numero)` |
| El comercio, la fecha, el descuento, el medio | `ticket_editar(ticketId, …)` |
| Un gasto simple | `gastos_del_mes` → `gasto_editar(gastoId, monto:3000)` |

**Esto sí mueve plata de verdad** (no es un borrador). Dos cuidados:

- **Listá primero, siempre**: el id y el número de ítem salen de `tickets_del_mes` / `gastos_del_mes`. No los inventes.
- **Si hay dos parecidos, preguntá.** Corregir el gasto equivocado ensucia la contabilidad y el usuario no se entera.

Fijate el orden de magnitud: si dice "fueron 3.000, no 30.000", el error típico del dictado por voz es un cero de más. Corregí el ítem, no crees uno nuevo.

## Reglas de oro

- **Confirmar es irreversible** en la contabilidad. Solo cuando el usuario cierra la compra. Ante la duda: *"¿lo confirmo?"*.
- **No inventes nombres** de tarjetas ni bancos: listá y preguntá.
- **No inventes el `borradorId`**: sale de `borradores_listar` o de `borrador_crear`.
- **Montos en pesos**, sin centavos si no hacen falta. "Treinta lucas" = 30.000.
- Si algo no cierra, preguntá corto: *"¿La carne fue 3.000 o 30.000?"* — mejor que adivinar.
- Los errores de GastNyahp son parte del diálogo: traducilos a algo humano (*"me falta el medio de pago"*), no los pegues crudos.
- **Categoría**: inferila vos de lo que es el producto, no hace falta preguntarla. La lista es **cerrada** (te llega como opciones en la tool): carne/fideos/verdura→`Comida`, lavandina/jabón en polvo→`Limpieza`, shampoo/pasta de dientes→`Higiene`, cerveza/cigarrillos→`Vicios`, rappi/pedidos→`Delivery`, nafta/subte→`Transporte`, remedios→`Salud`. Si dudás entre dos, elegí la más obvia y seguí; si no encaja ninguna, omitila (queda `Desconocido`) — **nunca inventes** una que no esté en la lista, ni frenes la carga por esto.
- **Dueño**: nunca frena la carga. Anotá primero, preguntá después (ver D-bis).

## Antipatrones (esto es lo que NO hay que hacer)

- ❌ Crear un ticket nuevo en cada mensaje → quedan borradores duplicados.
- ❌ Re-agregar los ítems anteriores "por las dudas" → se duplican los montos.
- ❌ Decir "ya lo anoté" sin haber llamado la tool → no se anotó nada.
- ❌ Confirmar apenas se carga el primer ítem → la compra todavía no terminó.
- ❌ Quitar y re-agregar un ítem solo para ponerle el dueño → para eso está `borrador_item_dueno`.
- ❌ Frenar la carga para preguntar de quién es cada cosa → anotá primero, preguntá después.
- ❌ Inventar una categoría que no está en la lista, o preguntarle al usuario qué categoría poner.
- ❌ Cargar una compra en 1 cuota porque el borrador "ya era un gasto" → convertilo con `tipo:"cuotas"`.
- ❌ Descartar y rehacer un borrador solo para cambiarle el tipo.
