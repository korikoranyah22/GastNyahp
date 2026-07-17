---
name: gastnyahp-instrumentos
description: Servicios, préstamos, cuotas, reservas y presupuesto en GastNyahp — marcar meses pagados, cargar cuánto vino la factura, y planificar el mes.
triggers: [servicio, servicios, luz, gas, agua, internet, wifi, cable, streaming, netflix, spotify, expensas, seguro, factura, boleta, vence, vencimiento, impago, préstamo, prestamo, uva, cuota, cuotas, financiado, reserva, reservas, apartar, aparto, fondo, deuda, gasto fijo, presupuesto, límite, limite, tope, ingreso, sueldo, dólar, dolar, ccl, cotización, copiar mes]
tools: [servicios_listar, servicio_pagar_mes, servicio_monto_mes, servicio_crear, servicio_extender, servicio_activar, servicio_desactivar, prestamos_listar, prestamo_pagar_mes, prestamo_monto_mes, prestamo_crear, prestamo_revisar, cuota_pagar_mes, cuota_monto_mes, cuota_finalizar, cuota_revisar, cuotas_pendientes, reservas_listar, reserva_monto_mes, reserva_crear, reserva_editar, reserva_aplicar_base, presupuesto_ver, presupuesto_definir, ingreso_ver, ingreso_definir, copiar_mes]
---

# GastNyahp — servicios, préstamos, cuotas, reservas y plan del mes

Esto **no** es la fila del super: es cuando llega la factura o se hace la cuenta del mes. Servicios, préstamos y cuotas comparten la misma forma —un **calendario de meses**, cada uno con su monto y si está pagado— así que el diálogo real es casi siempre el mismo par de gestos:

| El usuario dice | Vos hacés |
|---|---|
| *"pagué la luz"* | `servicio_pagar_mes` |
| *"la luz de julio vino 45 mil"* | `servicio_monto_mes` |
| *"pagué la cuota del préstamo"* | `prestamo_pagar_mes` |
| *"pagué la cuota de la tele"* | `cuota_pagar_mes` |

## Regla #1 — Identificás por NOMBRE, y no adivinás

El usuario dice "la luz", no un id. Los nombres exactos salen de `servicios_listar` / `prestamos_listar` / `cuotas_pendientes` / `reservas_listar`.

Si el nombre es **ambiguo**, la tool te va a decir con cuáles coincide: **preguntá cuál es**, no elijas por tu cuenta. Marcar pagado el préstamo equivocado ensucia la contabilidad y el usuario no se entera.

## Regla #2 — "Pagar" ALTERNA, no "setea"

`*_pagar_mes` **invierte** el estado. Si el mes ya figuraba pagado y volvés a llamarla, lo **despagás**.

- Si el usuario dice *"pagué la luz"* y en el listado ya figura pagada: **no la llames** — decile que ya estaba.
- Solo la llamás cuando el estado tiene que cambiar de verdad.

## Regla #3 — Los meses van en `yyyy-MM`, y los traducís vos

"Este mes", "julio", "el mes pasado" los resolvés vos. Nunca le pidas al usuario que te formatee una fecha. Si no aclara el mes, es el **actual**.

## Escenarios

### A. Llegó la factura
> — *"la luz vino 45 mil"* … *"ya la pagué"*

1. `servicios_listar` → nombre exacto y cómo viene el mes.
2. `servicio_monto_mes(nombre:"Luz", monto:45000)` — cuánto salió.
3. Si además la pagó: `servicio_pagar_mes(nombre:"Luz")`.
4. *"Listo — luz de julio, 45.000, pagada."*

Son dos gestos distintos: **cuánto salió** y **si está paga**. Que diga uno no implica el otro.

### B. Aumentó para adelante
> — *"el internet ahora sale 30 mil por mes"*

`servicio_extender(nombre:"Internet", monto:30000)` — aplica de este mes en adelante y respeta los meses ya pagados. Para un mes suelto es `servicio_monto_mes`.

### C. Cuotas y préstamos
> — *"pagué la cuota de la tele"* → `cuotas_pendientes` para el nombre → `cuota_pagar_mes`
> — *"me refinanciaron el préstamo: ahora 24 de 90 mil"* → `prestamo_revisar`

**`*_revisar` REGENERA el calendario**: preserva los meses ya pagados con su monto y pisa el resto. Para un mes puntual (una cuota UVA que vino distinta) usá `*_monto_mes`, no `revisar`.

> Para dar de **alta** una compra en cuotas no uses estas tools: se carga con el changuito (`borrador_crear tipo:"cuotas"`).

### D. Reservas — plata apartada, no gastada
> — *"este mes aparto 50 mil en vez de 30"* → `reserva_monto_mes`

Una reserva **no es un gasto**: es plata que se separa al inicio del mes. Su monto de un mes sale de: **ajuste del mes > monto base recurrente > 0**.

⚠️ **`reserva_aplicar_base` es destructiva**: pisa el monto de TODOS los meses y **borra los ajustes puntuales**. Usala solo si el usuario pide explícitamente "aplicalo a todos los meses", **decile antes qué ajustes se pierden**, y esperá que confirme.

### E. El plan del mes
> — *"no quiero gastar más de 500 mil con la tarjeta"* → `presupuesto_definir(credito:500000)`
> — *"el CCL está a 1500"* → `ingreso_definir(dolarCcl:1500)`

La **cotización CCL** es de lo que dependen los servicios en USD: si crear uno en dólares falla, es porque falta cargarla.

## Reglas de oro

- **Nada de esto borra.** Un servicio se **desactiva**, una cuota se **finaliza**: el historial siempre queda. Si el usuario insiste en borrar, explicale que el borrado real es desde la app.
- **No inventes montos.** Si no dijo cuánto vino la factura, preguntá.
- **Un gesto por vez.** "Pagué la luz y el gas" son dos llamadas, no una.
- Los errores de GastNyahp son parte del diálogo: traducilos ("no encontré ese servicio, ¿es 'Luz EDESUR'?").

## Antipatrones

- ❌ Volver a llamar `*_pagar_mes` sobre un mes que ya figuraba pagado → lo despagás.
- ❌ Elegir entre dos servicios parecidos sin preguntar.
- ❌ Usar `*_revisar` para arreglar un solo mes → te regenera el calendario entero.
- ❌ Llamar `reserva_aplicar_base` sin avisar que se pierden los ajustes puntuales.
- ❌ Usar estas tools para dar de alta una compra en cuotas → eso es el changuito.
