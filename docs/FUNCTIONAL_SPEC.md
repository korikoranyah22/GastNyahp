# GastNyahp — Especificación Funcional y UX

> App frontend (React + Vite, target: Electron) para gestión de finanzas personales.
> Persistencia local en un único archivo JSON (estilo draw.io: importable/exportable).

---

## Índice

1. [Visión general](#1-visión-general)
2. [Modelo de datos JSON](#2-modelo-de-datos-json)
3. [Módulos y pantallas](#3-módulos-y-pantallas)
   - 3.1 [Bancos](#31-bancos)
   - 3.2 [Tarjetas de crédito](#32-tarjetas-de-crédito)
   - 3.3 [Cuotas de tarjetas](#33-cuotas-de-tarjetas)
   - 3.4 [Préstamos](#34-préstamos)
   - 3.5 [Servicios](#35-servicios)
   - 3.6 [Gastos fijos personales](#36-gastos-fijos-personales)
   - 3.7 [Gastos del día (registro)](#37-gastos-del-día-registro)
   - 3.8 [Dashboard Tarjetas + Servicios](#38-dashboard-tarjetas--servicios)
   - 3.9 [Dashboard Mensual Consolidado](#39-dashboard-mensual-consolidado)
4. [Navegación y shell de la app](#4-navegación-y-shell-de-la-app)
5. [Flujo principal de uso (happy path)](#5-flujo-principal-de-uso-happy-path)
6. [Casos de uso detallados](#6-casos-de-uso-detallados)
7. [UX / UI Guidelines](#7-ux--ui-guidelines)
8. [Persistencia JSON (draw.io style)](#8-persistencia-json-drawio-style)
9. [Roadmap hacia Electron](#9-roadmap-hacia-electron)

---

## 1. Visión general

GastNyahp replica y supera la lógica del archivo **"Hoja de Gastos.xlsx"**, que tiene:

| Hoja Excel              | Equivalente en la app           |
|-------------------------|---------------------------------|
| `Cuotas`                | Módulo de Cuotas de Tarjetas    |
| `Gastos-{Mes}{Año}`     | Registro diario de gastos       |
| `Estimaciones-{Mes}{Año}` | Dashboard Mensual Consolidado |
| `DualPay`               | Configuración de ingresos       |

**Tarjetas conocidas del archivo real:**
- VISA BBVA (débito + crédito)
- MASTER BBVA
- VISA GALICIA
- MASTER GALICIA

**Servicios recurrentes del archivo real:**
Edesur, AGIP, Metrogas, AYSA, Expensas, Baulera, Spotify, YouTube Premium, ChatGPT (Cami y Miyu), Movistar, Rappi Pro, Netflix, Tuenti.

**Préstamos del archivo real:** Préstamo BBVA, Préstamo Galicia.

**Categorías de gastos:** Comida, Ropa, Hogar, Salud, Higiene, Transporte, Salidas, Vicios, Educación, Servicios, Limpieza, Perfumes, Desconocido.

---

## 2. Modelo de datos JSON

```jsonc
{
  "meta": {
    "version": "1.0",
    "lastSaved": "2026-02-26T10:00:00Z",
    "currency": "ARS"
  },
  "banks": [
    {
      "id": "bank_1",
      "name": "BBVA",
      "color": "#004B9B",
      "icon": "bbva"
    }
  ],
  "creditCards": [
    {
      "id": "card_1",
      "bankId": "bank_1",
      "label": "VISA BBVA",
      "network": "VISA",         // "VISA" | "MASTERCARD"
      "type": "credit",          // "credit" | "debit"
      "closingDay": 15,          // día de cierre
      "dueDay": 5,               // día de vencimiento
      "color": "#0057B8"
    }
  ],
  "installments": [
    {
      "id": "inst_1",
      "cardId": "card_1",
      "description": "Lavarropas",
      "category": "Hogar",
      "purchaseDate": "2024-12-14",
      "frequency": "fixed",      // "fixed" | "monthly" (recurrente)
      "monthlyAmount": 105833.25,
      "months": [
        { "month": "2024-12", "amount": 105833.25, "paid": true },
        { "month": "2025-01", "amount": 105833.25, "paid": true },
        { "month": "2025-02", "amount": 105833.25, "paid": false }
        // ...
      ]
    }
  ],
  "loans": [
    {
      "id": "loan_1",
      "bankId": "bank_1",
      "description": "Préstamo BBVA",
      "totalAmount": 900000,
      "monthlyInstallment": 310000,
      "startDate": "2025-01-01",
      "totalInstallments": 12,
      "paidInstallments": 2,
      "months": [
        { "month": "2026-01", "amount": 310000, "paid": true },
        { "month": "2026-02", "amount": 247309, "paid": false }
      ]
    }
  ],
  "services": [
    {
      "id": "svc_1",
      "name": "Edesur",
      "category": "Servicios",
      "billingType": "monthly",  // "monthly" | "bimonthly" | "quarterly"
      "linkedCardId": null,      // null = débito/efectivo
      "amounts": [
        { "month": "2026-01", "amount": 38000 },
        { "month": "2026-02", "amount": 38000 }
      ]
    },
    {
      "id": "svc_2",
      "name": "Movistar",
      "category": "Servicios",
      "billingType": "monthly",
      "linkedCardId": "card_1",
      "amounts": [
        { "month": "2026-01", "amount": 77000 },
        { "month": "2026-02", "amount": 77000 }
      ]
    }
  ],
  "fixedExpenses": [
    {
      "id": "fx_1",
      "label": "Miyu",
      "type": "person",          // "person" | "cash" | "other"
      "months": [
        { "month": "2026-02", "amount": 0, "note": "" }
      ]
    },
    {
      "id": "fx_2",
      "label": "Cami",
      "type": "person",
      "months": [
        { "month": "2026-02", "amount": 0, "note": "Facu + Médica" }
      ]
    },
    {
      "id": "fx_3",
      "label": "Cash",
      "type": "cash",
      "months": [
        { "month": "2026-02", "amount": 600000, "note": "" }
      ]
    }
  ],
  "dailyExpenses": [
    {
      "id": "exp_1",
      "date": "2025-04-07",
      "description": "Coto",
      "category": "Comida",
      "amount": 130823.02,
      "ticket": 130823.02,
      "paymentMethod": "Visa-Galicia-Credito",
      "week": "1-7",
      "tags": []
    }
  ],
  "income": {
    "netMonthly": 4557000,
    "usdRateOfficial": 1426,
    "usdRateCCL": 2000,
    "splitPercent": 70         // % del sueldo que va a gastos
  }
}
```

---

## 3. Módulos y pantallas

### 3.1 Bancos

**Ruta:** `/settings/banks`

**Propósito:** Administrar las entidades bancarias base.

**Campos del formulario:**
| Campo     | Tipo      | Validación              |
|-----------|-----------|-------------------------|
| Nombre    | texto     | requerido, único        |
| Color     | color picker | default por banco    |
| Ícono/logo| selector  | BBVA, Galicia, Santander, Macro, etc. |
| Alias     | texto     | ej. "BBVA Personal"     |

**UX hints:**
- Lista compacta de bancos como tarjetas con color de la entidad.
- Botón **+ Agregar banco** abre un slide-over (panel lateral), no un modal fullscreen.
- Cada banco muestra un badge con la cantidad de tarjetas y préstamos asociados.
- No se puede eliminar un banco si tiene tarjetas o préstamos asociados → mostrar mensaje bloqueante.
- Orden drag-and-drop para preferencia de visualización.

---

### 3.2 Tarjetas de crédito

**Ruta:** `/settings/cards`

**Propósito:** Configurar las tarjetas vinculadas a cada banco.

**Campos del formulario:**
| Campo           | Tipo         | Notas                                      |
|-----------------|--------------|--------------------------------------------|
| Banco           | select       | muestra bancos configurados                |
| Etiqueta        | texto        | ej. "VISA BBVA", "MASTER GALICIA"          |
| Red             | toggle       | VISA / MASTERCARD                          |
| Tipo            | toggle       | Crédito / Débito                           |
| Día de cierre   | número (1-31)|                                            |
| Día de vencimiento | número (1-31) |                                        |
| Color           | color picker | hereda el color del banco por defecto      |
| Activa          | boolean      | si está activa o fue dada de baja           |

**UX hints:**
- Las tarjetas se muestran como tarjetas físicas (card UI) con el logo de VISA/MC y el color del banco.
- Click en una tarjeta navega a su lista de cuotas (módulo 3.3).
- Indicator dot verde/rojo de si hay cuotas pendientes este mes.
- El formulario agrupa crédito y débito en la misma pantalla; las tarjetas de débito no tienen cuotas, solo se usan como medio de pago.

---

### 3.3 Cuotas de tarjetas

**Ruta:** `/cards/:cardId/installments`

**Propósito:** Registrar y seguir las cuotas pendientes de cada compra en cuotas, equivalente a la hoja `Cuotas` del Excel.

**Sub-formulario "Nueva cuota":**
| Campo            | Tipo            | Notas                                          |
|------------------|-----------------|------------------------------------------------|
| Descripción      | texto           | ej. "Lavarropas", "Secarropas"                |
| Categoría        | select          | Hogar, Ropa, Educación, Transporte, etc.       |
| Fecha de compra  | date picker     |                                                |
| Frecuencia       | toggle          | Cuotas fijas / Mensual recurrente              |
| Monto por cuota  | número          | en ARS                                         |
| Cantidad de cuotas | número        | solo si es cuotas fijas                        |
| Mes de inicio    | month picker    | mes en que aparece la primera cuota            |

**Vista de cuotas (tabla/grilla):**
- Filas: cada compra en cuotas.
- Columnas: los próximos 12-18 meses (scrollable horizontalmente).
- Celdas con monto si hay cuota ese mes, vacía si no.
- Celdas en verde claro = cuota ya pagada (mes pasado), azul = mes actual, gris = futuro.
- Click en celda del mes actual → toggle pagada/pendiente.
- Fila de **TOTAL** fija al fondo de la tabla, suma todos los montos por mes.
- Filtros: mostrar solo activas / todas (incluye finalizadas).
- Botón "Editar" en cada fila abre slide-over con el formulario pre-completado.
- Botón "Finalizar" marca todas las cuotas restantes como canceladas/terminadas.

**Indicadores:**
- Badge en la tab del mes actual: suma total de cuotas de esta tarjeta.
- Alert si el mes actual tiene cuota pendiente de pago.

---

### 3.4 Préstamos

**Ruta:** `/loans`

**Propósito:** Registrar préstamos bancarios y seguir las cuotas mensuales.

**Formulario "Nuevo préstamo":**
| Campo                | Tipo        | Notas                               |
|----------------------|-------------|-------------------------------------|
| Banco                | select      |                                     |
| Descripción          | texto       | ej. "Préstamo BBVA personal"        |
| Monto total          | número      |                                     |
| Cuota mensual        | número      | puede variar por mes (UVA, inflación)|
| Fecha de inicio      | month picker|                                     |
| Cantidad de cuotas   | número      |                                     |

**Vista de préstamos:**
- Lista de préstamos activos agrupados por banco.
- Cada préstamo muestra: cuotas pagadas / total, barra de progreso, próxima cuota y monto.
- Click expande el detalle mes a mes (similar al de cuotas).
- El monto de cada cuota es **editable por mes** porque en Argentina los préstamos UVA o con ajuste pueden variar → cada mes se puede editar el importe real.
- Las cuotas pasadas se marcan como pagadas automáticamente al avanzar el mes.

**UX hints:**
- Color del préstamo hereda el color del banco.
- Proyección: gráfico de línea mostrando la deuda restante a lo largo del tiempo.
- Alerta si hay cuota de préstamo pendiente en el mes actual.

---

### 3.5 Servicios

**Ruta:** `/services`

**Propósito:** Registrar servicios recurrentes (utilities, suscripciones, expensas).

**Formulario "Nuevo servicio":**
| Campo              | Tipo         | Notas                                       |
|--------------------|--------------|---------------------------------------------|
| Nombre             | texto        | ej. "Edesur", "Movistar", "Netflix"         |
| Categoría          | select       | Electricidad, Gas, Agua, Telecom, Streaming, Seguro, Expensas, Otros |
| Frecuencia de cobro| select       | Mensual / Bimestral / Trimestral / Anual    |
| Medio de pago      | select       | Débito automático, Tarjeta específica, Efectivo |
| Tarjeta vinculada  | select       | si el medio es tarjeta                      |
| Monto base         | número       | estimado (se puede editar por mes)           |
| Activo             | boolean      |                                             |

**Vista de servicios:**
- Grid de cards, una por servicio, con ícono de categoría.
- Cada card muestra el monto del mes actual y la variación vs. mes anterior (↑↓).
- Click en la card despliega el historial de montos mes a mes editable.
- Botón "Cargar monto del mes" por servicio: ingresa el importe real del mes en curso.
- Vista alternativa: tabla anual estilo Excel (filas = servicios, columnas = meses).

**Sub-grupos visuales:**
1. **Servicios del hogar** (Edesur, AGIP/Gas, Metrogas, AYSA, Expensas, Baulera)
2. **Conectividad** (Movistar, Tuenti)
3. **Streaming / Digital** (Netflix, Spotify, YouTube Premium, ChatGPT x persona)
4. **Seguros** (Triunfo, BBVA Seguros)
5. **Otros** (Rappi Pro, Megathlon, etc.)

**Servicios en tarjeta de crédito:**
- Si el servicio está vinculado a una tarjeta, aparece también en la vista de cuotas de esa tarjeta con etiqueta `[Servicio]`.
- El total de servicios-en-tarjeta se suma al total de cuotas del dashboard.

---

### 3.6 Gastos fijos personales

**Ruta:** `/fixed-expenses`

**Propósito:** Registrar gastos fijos mensuales asociados a personas o categorías especiales que no son ni tarjeta ni servicio.

**Ítems del archivo real (ejemplos):**
- **Miyu** — gastos de la persona Miyu (YouTube Premium propio, ChatGPT propio, etc.)
- **Cami** — gastos de Cami (Facu Cami, médica, ChatGPT Cami, Tuenti Cami, etc.)
- **Cash** — efectivo disponible mensual presupuestado
- **Saldo Impago** — deuda de tarjeta no pagada que genera interés
- **Gastos Crédito** — estimación de gastos variables de tarjeta

**Formulario:**
| Campo         | Tipo       | Notas                                    |
|---------------|------------|------------------------------------------|
| Etiqueta      | texto      | ej. "Miyu", "Cash", "Saldo Impago"       |
| Tipo          | select     | Persona / Efectivo / Deuda / Otro        |
| Monto mensual | número     | editable por mes                         |
| Notas         | texto libre| ej. "incluye Facu + Médica"              |

**Vista:**
- Lista simple editable. Cada ítem tiene su monto del mes actual (editable inline).
- Historial expandible: ver montos de meses anteriores.
- Total al pie de la lista.
- Estos gastos se **inyectan directamente en el Dashboard Mensual Consolidado**.

---

### 3.7 Gastos del día (registro)

**Ruta:** `/expenses/:year/:month`

**Propósito:** Registro diario de gastos (equivalente a las hojas `Gastos-{Mes}{Año}`).

**Formulario de carga rápida (barra superior fija):**
| Campo           | Tipo          | Notas                                          |
|-----------------|---------------|------------------------------------------------|
| Fecha           | date          | default: hoy                                   |
| Descripción     | texto         | con autocompletado (últimas 30 usadas)         |
| Categoría       | select + badge| Comida, Ropa, Hogar, Salud, Higiene, Transporte, Salidas, Vicios, Educación, Limpieza, Perfumes, Desconocido |
| Importe         | número        |                                                |
| Ticket          | número        | total del ticket (el campo Precio puede ser una parte) |
| Medio de pago   | select        | VISA BBVA Crédito, VISA BBVA Débito/MODO, MASTER BBVA, VISA GALICIA, MASTER GALICIA, Efectivo, Transferencia |
| Semana          | auto-calc     | "1 al 7", "8 al 14", "15 al 22", "23 al 31"  |

**Vista de la lista de gastos:**
- Agrupados por semana del mes con subtotales.
- Colores por categoría (Comida = verde, Vicios = naranja, Hogar = azul, etc.).
- Filtros rápidos: por categoría, por medio de pago, por semana.
- Cada gasto tiene opciones de editar / eliminar.
- Buscador inline.

**Panel lateral de resumen (sticky):**
- **Total Crédito** / Meta Crédito
- **Total Débito/Efectivo** / Meta Débito/Efectivo
- **Total BBVA** / **Total Galicia**
- **Total Comida** / **Total Vicios** / **Total Salidas** / **Total Transporte**
- Desglose por semana: columnas "1 al 7", "8 al 14", "15 al 22", "23 al 31"

**UX hints:**
- La entrada rápida es la acción más frecuente → el formulario de carga rápida está visible sin scroll.
- Soporte para cargar varios gastos del mismo ticket (ej: una compra con detalle de ítems).
- El campo "Ticket" permite registrar el total del comprobante aunque solo se detalle parte.
- Exportación del mes a CSV.
- Navegación mes a mes con flechas `←` `→`.

---

### 3.8 Dashboard Tarjetas + Servicios

**Ruta:** `/dashboard/cards-services`

**Propósito:** Visión mensual de lo que vence en tarjetas (cuotas + servicios en tarjeta).

**Layout:**

```
┌─────────────────────────────────────────────────┐
│  ← Febrero 2026  →          [Selector de mes]   │
├──────────────┬──────────────┬────────────────────┤
│  VISA BBVA   │ MASTER BBVA  │   VISA GALICIA      │
│  $1.323.339  │   $28.440    │   $388.447          │
├──────────────┴──────────────┴────────────────────┤
│  TOTAL TARJETAS                    $1.740.226    │
├─────────────────────────────────────────────────┤
│  SERVICIOS (independientes de tarjeta)           │
│  Edesur        $38.000                          │
│  Metrogas      $28.200                          │
│  AYSA          $19.000                          │
│  ...                                            │
│  TOTAL SERVICIOS                   $409.600     │
├─────────────────────────────────────────────────┤
│  GRAN TOTAL CUOTAS + SERVICIOS    $2.149.826    │
└─────────────────────────────────────────────────┘
```

**Desglose por tarjeta (expandible):**
- Click en cada tarjeta despliega el listado de cuotas que componen ese total:
  - Descripción, categoría, cuota N/total, monto.
  - Separado: cuotas de compras / servicios en tarjeta.

**UX hints:**
- Los totales de tarjeta deben coincidir con la fila `TOTALS` de la hoja `Cuotas` del Excel.
- Indicador visual si el total supera un umbral configurable (ej: "límite de cuotas").
- Comparación vs. mes anterior con flechita y porcentaje.

---

### 3.9 Dashboard Mensual Consolidado

**Ruta:** `/dashboard/monthly` (pantalla principal / home)

**Propósito:** Resumen financiero completo del mes. Equivalente a las hojas `Estimaciones-{Mes}{Año}`.

**Layout de bloques:**

```
┌────────────────────────────────────────────────────────┐
│  ESTIMACIONES — Febrero 2026         ← →               │
├────────────────────────────────────────────────────────┤
│  PRÉSTAMOS                                             │
│  Préstamo BBVA              $310.000                   │
│  Préstamo Galicia           $247.309                   │
│  Saldo Impago               $0                         │
│  Saldo Impago (interés)     $0                         │
│                    TOTAL PRÉSTAMOS   $557.309          │
├────────────────────────────────────────────────────────┤
│  CUOTAS + SERVICIOS                                     │
│  [→ link al Dashboard 3.8]           $1.436.247        │
├────────────────────────────────────────────────────────┤
│  GASTOS FIJOS / PERSONALES                             │
│  Miyu                       $0                         │
│  Cami                       $0                         │
│  Cash                       $600.000                   │
│  Gastos Crédito (variable)  $558.000                   │
│  Facu Cami                  $0                         │
│                    TOTAL FIJOS       $1.158.000        │
├────────────────────────────────────────────────────────┤
│  ─────────────────────────────────────                 │
│  TOTAL EGRESOS                      $3.151.556         │
├────────────────────────────────────────────────────────┤
│  INGRESOS (Neto mensual)            $4.557.000         │
│  SOBRA / FALTA                      $1.405.444         │
│  Equivalente en USD (CCL)           $702                │
└────────────────────────────────────────────────────────┘
```

**Edición inline:**
- Cada ítem de esta pantalla es editable directamente (click → campo de texto).
- Los valores de tarjetas y servicios vienen calculados automáticamente desde sus módulos.
- Los préstamos vienen de su módulo pero se pueden ajustar manualmente por mes.
- Los gastos fijos se editan inline aquí o desde el módulo 3.6.

**Panel de ingresos (configurable):**
- Neto mensual editable.
- Tipo de cambio oficial y CCL editables.
- Cálculo de equivalente en USD automático.
- Opción de ver el "Sobra" particionado por porcentaje (ej: 70% / 30% como en `DualPay`).

**UX hints:**
- Esta pantalla es el "home" de la app, lo primero que se ve.
- Al abrir la app, si el mes actual no tiene estimaciones guardadas, ofrece **"Copiar del mes anterior"** con un único botón.
- Cada bloque es colapsable para reducir ruido visual.
- Los números negativos (cuando los egresos > ingresos) se muestran en rojo.

---

## 4. Navegación y shell de la app

```
┌──────────────────────────────────────────────────────────────┐
│ [≡ GastNyahp]  [← Febrero 2026 →]              [💾] [📤] [⚙]  │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  Sidebar izquierdo (colapsable):                             │
│  ──────────────────────────────                             │
│  🏠 Inicio (Dashboard Consolidado)                           │
│  💳 Tarjetas + Cuotas                                        │
│     ↳ VISA BBVA                                             │
│     ↳ MASTER BBVA                                           │
│     ↳ VISA GALICIA                                          │
│     ↳ MASTER GALICIA                                        │
│  📊 Dashboard Tarjetas/Servicios                            │
│  📋 Gastos del mes                                          │
│  🔁 Préstamos                                               │
│  🔌 Servicios                                               │
│  📌 Gastos Fijos                                            │
│  ─────────────────────────────                              │
│  ⚙  Configuración                                           │
│     ↳ Bancos                                                │
│     ↳ Tarjetas                                              │
│     ↳ Ingresos / DualPay                                    │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

**Selector de mes global:**
- `← Febrero 2026 →` en el header afecta todas las pantallas.
- El mes activo se persiste en el estado de la app (no en la URL necesariamente).
- Botón **"Hoy"** para volver rápido al mes actual.

**Acciones del header:**
- 💾 **Guardar** — escribe el JSON al disco (en Electron: `fs.writeFileSync`; en browser: descarga).
- 📤 **Exportar / Importar** — abre modal para importar un JSON existente o exportar el actual.
- ⚙ **Ajustes** — acceso rápido a configuración.

---

## 5. Flujo principal de uso (happy path)

```
Inicio del mes
    │
    ▼
Abrir GastNyahp → Dashboard Consolidado
    │
    ├─ Si es mes nuevo → ¿Copiar estimaciones del mes anterior? [Sí / No]
    │
    ▼
Revisar préstamos del mes
    └─ Ajustar cuota si cambió (UVA, etc.)
    │
    ▼
Revisar cuotas de tarjetas automáticas (ya calculadas)
    └─ Ver si hay cuotas nuevas que registrar
    │
    ▼
Cargar servicios del mes
    └─ Ingresar montos reales (factura de Edesur, Metrogas, etc.)
    │
    ▼
Ajustar gastos fijos (Cami, Miyu, Cash)
    │
    ▼
Revisar Dashboard Consolidado → validar que "Sobra" sea razonable
    │
    ├─ Durante el mes →  Registrar gastos diarios en "Gastos del mes"
    │                    Monitorear totales por categoría y medio de pago
    │
    ▼
Fin de mes → Exportar / guardar el JSON
    └─ Revisar si quedan cuotas de tarjeta sin marcar como pagadas
```

---

## 6. Casos de uso detallados

### UC-01: Registrar una nueva compra en cuotas

**Actor:** Usuario
**Precondición:** La tarjeta existe en el sistema.

1. Usuario navega a **Tarjetas > VISA BBVA**.
2. Hace click en **"+ Nueva cuota"**.
3. Ingresa: descripción "Secarropas", categoría "Hogar", fecha "14/12/2024", tipo "Cuotas fijas", monto cuota $160.833, cantidad 12 cuotas.
4. El sistema calcula automáticamente los meses: Dic 2024 → Nov 2025.
5. Usuario confirma. La cuota aparece en la grilla.
6. El Dashboard Tarjetas se actualiza sumando $160.833 al total de VISA BBVA para los meses correspondientes.

---

### UC-02: Cargar el servicio de Edesur del mes

**Actor:** Usuario
**Precondición:** Edesur está registrado como servicio.

1. Usuario navega a **Servicios**.
2. Localiza "Edesur" y hace click en **"Cargar monto del mes"** (o click directo en la card).
3. Ingresa $38.000 para Febrero 2026.
4. El sistema guarda y actualiza el total de servicios en el Dashboard.

---

### UC-03: Cerrar el mes y preparar el siguiente

**Actor:** Usuario
**Precondición:** Mes actual tiene datos cargados.

1. Usuario cambia el selector de mes a **Marzo 2026**.
2. La app detecta que no hay datos para Marzo → muestra el prompt **"¿Copiar estimaciones de Febrero 2026?"**.
3. Usuario confirma.
4. El sistema copia: préstamos (con cuota siguiente), cuotas de tarjetas (avanzando un mes), gastos fijos, pero **no** copia montos de servicios (deberán cargarse cuando lleguen las facturas).
5. El usuario ajusta lo necesario.

---

### UC-04: Ver cuánto sobra este mes

**Actor:** Usuario
**Precondición:** Todos los datos del mes están cargados.

1. Usuario navega a **Inicio (Dashboard Consolidado)**.
2. Ve el resumen:
   - Préstamos: $557.309
   - Cuotas + Servicios: $1.436.247
   - Gastos fijos: $1.158.000
   - **Total egresos: $3.151.556**
   - **Neto: $4.557.000**
   - **Sobra: $1.405.444 (~$702 USD al CCL)**
3. Puede ajustar el ingreso neto si cobró diferente.

---

### UC-05: Registrar un gasto diario

**Actor:** Usuario
**Precondición:** Mes activo seleccionado.

1. Usuario navega a **Gastos del mes**.
2. En la barra de carga rápida ingresa: hoy, "Coto", "Comida", $130.823, ticket $130.823, "VISA Galicia Crédito".
3. El sistema asigna automáticamente la semana ("1 al 7" si es día 1-7).
4. El gasto aparece en la lista agrupado por semana.
5. Los totales del panel lateral se actualizan: Total Crédito, Total Galicia, Total Comida.

---

### UC-06: Importar/exportar el JSON

**Actor:** Usuario
**Precondición:** ninguna.

1. Click en 📤 del header.
2. Modal con dos opciones:
   - **Exportar**: descarga `gastos_YYYY-MM.json` (snapshot del mes) o `gastos_full.json` (todo).
   - **Importar**: carga un JSON, la app valida la versión del schema y mezcla o reemplaza los datos.
3. Si hay conflicto (datos locales vs. importados), se muestra diff simplificado y el usuario elige.

---

### UC-07: Ver proyección de cuotas a 12 meses

**Actor:** Usuario
**Precondición:** Hay cuotas registradas.

1. Usuario navega a **Tarjetas > VISA BBVA > cuotas**.
2. La grilla muestra columnas de los próximos 12+ meses.
3. Se puede hacer scroll horizontal para ver meses futuros.
4. La fila de TOTAL al pie muestra cómo evoluciona la deuda en cuotas mes a mes.
5. El usuario puede ver cuándo se "libera" el presupuesto al terminar cuotas grandes.

---

## 7. UX / UI Guidelines

### Principios generales

- **Densidad de información**: la app maneja muchos números; las tablas deben ser compactas pero legibles. Tipografía monospaced para importes.
- **Edición inline**: minimizar modales. Preferir slide-overs y edición directa en tabla.
- **Sin pasos innecesarios**: cargar un gasto diario no puede tardar más de 3 clicks/teclas.
- **Persistencia visual del mes**: el mes seleccionado siempre visible en el header.
- **Feedback inmediato**: al ingresar un monto, los totales se actualizan en tiempo real (sin necesidad de guardar).

### Paleta de colores sugerida

| Elemento              | Color            |
|-----------------------|------------------|
| BBVA                  | `#004B9B` (azul) |
| Galicia               | `#E30613` (rojo) |
| Positivo / Sobra      | `#16A34A` (verde)|
| Negativo / Falta      | `#DC2626` (rojo) |
| Cuota pagada          | `#BBF7D0` (verde claro) |
| Cuota pendiente mes actual | `#FDE68A` (amarillo) |
| Cuota futura          | `#F3F4F6` (gris claro) |
| Servicio              | `#7C3AED` (violeta) |
| Préstamo              | `#EA580C` (naranja) |

### Componentes clave

- **AmountInput**: input numérico con formato `$1.234.567` automático al tipear.
- **MonthGrid**: la grilla de cuotas scrollable horizontalmente, con sticky de columna de descripción.
- **BlockCard**: cada bloque del Dashboard (Préstamos, Cuotas, Fijos) es una card colapsable con total en el header.
- **QuickExpenseBar**: barra de carga rápida siempre visible en la pantalla de gastos del mes.
- **DeltaBadge**: badge con flechita ↑↓ y porcentaje de variación vs. mes anterior.

### Atajos de teclado

- `N` → nueva cuota / nuevo gasto (dependiendo de la pantalla activa)
- `←` / `→` → cambiar mes
- `Ctrl+S` → guardar JSON
- `Escape` → cerrar slide-over / cancelar edición

### Mobile / responsive

- El target primario es desktop (Electron).
- El target secundario es tablet/desktop en browser.
- En mobile: la sidebar colapsa a bottom navigation, la MonthGrid es scrollable en x con swipe.

---

## 8. Persistencia JSON (draw.io style)

### Cómo funciona

- **Un único archivo JSON** contiene todo el estado de la app.
- En **browser**: el archivo vive en `localStorage` (key: `gastos-app-data`) y se puede exportar/importar manualmente.
- En **Electron** (fase 2): el archivo se lee/escribe directamente en el sistema de archivos. Path configurable por el usuario (ej: `~/Documents/gastos.json`).
- El guardado es **explícito** (botón 💾) con un indicador de "cambios sin guardar" (dot naranja en el ícono).
- Opcionalmente: auto-guardado cada 5 minutos con debounce.

### Versionado del schema

```json
{
  "meta": {
    "version": "1.0",
    "appVersion": "0.1.0",
    "lastSaved": "2026-02-26T10:00:00Z"
  }
}
```

- Si la versión del JSON importado es menor, se corre una migración.
- Si es mayor (archivo de versión futura), se muestra advertencia.

### Backup automático

- Antes de cada guardado, se crea una copia en `gastos.backup.json`.
- En Electron: también se puede configurar un directorio de backups con N copias rotativas.

---

## 9. Roadmap hacia Electron

### Fase 1 — Browser (MVP)

- [x] Modelo de datos JSON completo
- [ ] Módulo Bancos y Tarjetas
- [ ] Módulo Cuotas (MonthGrid)
- [ ] Módulo Servicios
- [ ] Módulo Préstamos
- [ ] Módulo Gastos Fijos
- [ ] Dashboard Tarjetas + Servicios
- [ ] Dashboard Mensual Consolidado
- [ ] Módulo Gastos del Día
- [ ] Import / Export JSON
- [ ] Persistencia en localStorage

### Fase 2 — Electron

- [ ] Wrapper Electron + main process
- [ ] `ipcMain` para `fs.readFileSync` / `fs.writeFileSync`
- [ ] Auto-guardado configurable
- [ ] Backups rotativos
- [ ] Tray icon con acceso rápido
- [ ] Atajos de teclado globales
- [ ] Auto-update

### Stack sugerido

| Capa          | Tecnología                |
|---------------|---------------------------|
| Framework     | React 19 + Vite           |
| Estado        | Zustand (store global)    |
| UI            | Tailwind CSS + shadcn/ui  |
| Tablas        | TanStack Table            |
| Gráficos      | Recharts                  |
| Fechas        | date-fns                  |
| Electron      | electron-vite (fase 2)    |
| Persistencia  | localStorage → fs (fase 2)|

---

*Generado el 2026-02-26 a partir del análisis de `Hoja de Gastos.xlsx`.*
