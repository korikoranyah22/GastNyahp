---
name: gastnyahp-abm
description: Dar de alta y editar el catálogo de GastNyahp por conversación — bancos, tarjetas y personas. Es setup, no carga de gastos.
triggers: [banco, bancos, tarjeta, tarjetas, visa, mastercard, amex, persona, personas, dar de alta, alta, dar de baja, baja, desactivar, activar, archivar, renombrar, no existe, no tengo cargado, agregá el, agregá la, crear banco, crear tarjeta, nueva tarjeta, nuevo banco]
tools: [banco_crear, banco_editar, tarjeta_crear, tarjeta_editar, tarjeta_activar, tarjeta_desactivar, persona_crear, persona_editar, persona_archivar, bancos_listar, tarjetas_listar, personas_listar]
---

# GastNyahp — el catálogo (bancos, tarjetas, personas)

Esto es **setup**, no carga de gastos: dar de alta el banco, la tarjeta o la persona que después el resto de las tools referencia **por nombre**. Se usa poco y suele aparecer cuando algo falla: *"no existe una tarjeta llamada 'Visa'"*.

> Para cargar una compra, ese es el flujo del changuito — no es esto.

## Regla #1 — El catálogo no se borra por conversación

**Ninguna de estas tools borra nada.** Para sacar algo de circulación:

| Querés… | Usás | Qué pasa |
|---|---|---|
| "dar de baja" una tarjeta | `tarjeta_desactivar` | Deja de ofrecerse para pagar. Historial intacto. Reversible con `tarjeta_activar`. |
| "sacar" a una persona | `persona_archivar` | Deja de ofrecerse para asignar gastos. Historial intacto. |
| borrar de verdad | — | **No se puede desde acá.** Explicá que se desactiva/archiva, y que el borrado real es desde la app. |

Esto es a propósito: un "borrá la Visa" mal transcripto de un audio no se deshace y arrastra datos asociados.

## Regla #2 — Nunca inventes datos del catálogo

Un banco o una tarjeta mal creados ensucian la contabilidad para siempre. Si te falta un dato **obligatorio**, preguntalo. No lo completes "razonablemente".

## Escenarios

### A. Falta el banco
> — *"agregá el Galicia"* / o `borrador_actualizar` falló con *"no existe un banco llamado 'Galicia'"*

1. `bancos_listar` → ¿ya está? (la tool igual te avisa si existe y no duplica).
2. `banco_crear(nombre:"Galicia")`. Alias/color/icono son opcionales: no los preguntes, tienen default.

### B. Falta la tarjeta
> — *"agregá la Visa del Galicia"* / o algo falló con *"no existe una tarjeta llamada 'Visa'"*

La tarjeta **necesita un banco que ya exista** y **días de cierre y vencimiento**:

1. `bancos_listar` → si el banco no está, `banco_crear` primero (escenario A).
2. Faltan datos obligatorios que **no podés adivinar**: la red (`Visa`/`Mastercard`), el tipo (`Credito`/`Debito`), y **qué día cierra y qué día vence**. Preguntá lo que falte, corto:
   > *"¿Qué día cierra y qué día vence la Visa?"*
3. `tarjeta_crear(nombre:"Visa Galicia", banco:"Galicia", red:"Visa", tipo:"Credito", diaCierre:15, diaVencimiento:25)`
4. El **nombre** es el que se va a usar después para pagar: que sea el que el usuario dice naturalmente (*"la Visa del Galicia"* → `Visa Galicia`).

### C. Falta la persona
> — *"el shampoo es de Meli"* y Meli no está en `personas_listar`

1. No inventes ni asumas: *"No tengo a Meli cargada, ¿la agrego?"*.
2. `persona_crear(nombre:"Meli")` → y seguí con lo que estabas haciendo (asignar el ítem).

### D. Corregir
> — *"la Visa cierra el 15, no el 20"* / *"se escribe Meli, no Melu"*

`tarjeta_editar` / `banco_editar` / `persona_editar`, identificando por el nombre **actual**. Solo pasás lo que cambia.

### E. Dar de baja
> — *"la Amex ya no la uso"* → `tarjeta_desactivar(nombre:"Amex")`
> — *"sacá a Juan"* → `persona_archivar(nombre:"Juan")`

## Reglas de oro

- **Identificás por nombre, no por id**: los nombres exactos salen de `bancos_listar` / `tarjetas_listar` / `personas_listar`.
- **Si ya existe, no lo crees de nuevo** — la tool te avisa; no insistas ni crees una variante con otro nombre.
- **No frenes una compra por esto.** Si estabas cargando un ticket y falta la tarjeta: creala y seguí donde estabas (el borrador sigue abierto, `borradores_listar` te lo devuelve).
- **Confirmá lo que hiciste en una línea**: *"Listo, agregué la Visa Galicia — cierra el 15."*

## Antipatrones

- ❌ Inventar el día de cierre/vencimiento de una tarjeta porque el usuario no lo dijo.
- ❌ Crear una tarjeta con un banco inventado en vez de crear el banco primero.
- ❌ Prometer que borraste algo: no borrás, desactivás o archivás.
- ❌ Crear una persona/banco duplicado con el nombre apenas distinto.
