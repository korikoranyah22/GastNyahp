---
name: gastnyahp-consulta
description: Responder preguntas sobre la plata de la familia leyendo GastNyahp — cuánto se gastó, qué vence, qué cuotas quedan. Solo lectura, no mueve nada.
triggers: [cuánto gasté, cuanto gaste, cuánto gastamos, en qué se me fue, qué tengo que pagar, que tengo que pagar, vence, vencimiento, cierra, novedades, cuotas, cuánto debo, cuanto debo, resumen, este mes, el mes pasado, gastos del mes, cómo venimos, como venimos, impago, impagos, debo]
tools: [novedades_del_dia, gastos_del_mes, cuotas_pendientes, tarjetas_listar, bancos_listar, personas_listar]
---

# GastNyahp — consultar (solo lectura)

Responder con **datos reales** de GastNyahp cuando el usuario pregunta por su plata. **Ninguna tool de acá modifica nada**: son todas de lectura, así que no hace falta confirmar antes de usarlas — si te preguntan, mirá y contestá.

> Esto es distinto de **cargar** un gasto (el changuito) y de **dar de alta** una tarjeta (el catálogo).

## Regla #1 — Si te preguntan por plata, mirá; no estimes

Nunca contestes de memoria ni "a ojo". No sabés cuánto gastó el usuario hasta que lo consultás. Si la pregunta es sobre su plata, **llamá la tool en este mismo turno** y contestá con lo que devuelve.

## Qué usar para cada pregunta

| El usuario pregunta… | Tool |
|---|---|
| *"¿qué tengo que pagar?"*, *"¿algo vence hoy?"*, *"¿cómo venimos?"* | `novedades_del_dia` |
| *"¿cuánto gasté este mes?"*, *"¿en qué se me fue la plata?"* | `gastos_del_mes(mes:"yyyy-MM")` |
| *"¿qué cuotas me quedan?"*, *"¿cuánto debo de la Visa?"* | `cuotas_pendientes(mes:"yyyy-MM")` |
| *"¿qué tarjetas tengo?"* / *"¿quiénes están cargados?"* | `tarjetas_listar` / `personas_listar` |

## Escenarios

### A. "¿Cuánto gasté?"
> — *"¿cuánto llevo gastado este mes?"*

1. Resolvé el mes en `yyyy-MM`. **"Este mes" = el mes actual**; "el mes pasado" = el anterior. Si es ambiguo, asumí el actual (no preguntes por algo obvio).
2. `gastos_del_mes(mes:"2026-07")`
3. Contestá con el **total** primero, y el detalle solo si lo pide o si ayuda: *"Este mes llevás 340.000. Lo más pesado fue Comida."*

### B. "¿Qué tengo que pagar?"
> — *"¿me vence algo?"* / *"¿cómo venimos?"*

1. `novedades_del_dia()` (sin fecha = hoy).
2. Resumí lo accionable: qué está impago, qué tarjeta cierra o vence hoy. Si no hay nada: decilo y listo, *"Hoy no vence nada."* — no inventes relleno.

### C. "¿Qué cuotas me quedan?"
1. `cuotas_pendientes(mes:"yyyy-MM")` — el mes actual salvo que pida otro.
2. Vienen agrupadas por tarjeta: leelas así, es como el usuario las piensa.

## Reglas de oro

- **Los meses van en `yyyy-MM`.** Traducí "este mes", "julio", "el mes pasado" vos; no le pidas al usuario que te lo formatee.
- **El total primero, el detalle después.** Si son muchos gastos, no los enumeres todos: total + lo que se destaca.
- **Montos en pesos**, sin centavos si no hacen falta.
- **Si no hay datos, decilo tal cual.** *"No tenés gastos cargados en junio."* Nunca rellenes con números inventados.
- Si el usuario pregunta algo que estas tools no cubren (un presupuesto, un préstamo puntual), decí que eso lo ve en la app — no lo deduzcas de lo que sí tenés.

## Antipatrones

- ❌ Estimar un total "a ojo" en vez de llamar la tool.
- ❌ Pedirle al usuario el mes en `yyyy-MM` — traducilo vos.
- ❌ Enumerar 40 gastos cuando preguntó "¿cuánto gasté?".
- ❌ Confundir consulta con carga: acá no se registra nada.
