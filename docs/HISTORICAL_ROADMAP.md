# GastNyahp — Plan de próximas acciones

> **Archivado.** Esta fotografía corresponde al 27 de febrero de 2026 y contiene tareas ya implementadas.
> No usar como backlog: consultá [ROADMAP.md](ROADMAP.md).

> Generado: 2026-02-27
> Basado en: [`FUNCTIONAL_SPEC.md`](FUNCTIONAL_SPEC.md) vs estado actual de la app

---

## Estado actual (lo que está implementado)

| Módulo | Ruta | Estado |
|--------|------|--------|
| Bancos | `/banks` | ✅ Completo |
| Tarjetas | `/cards` | ✅ Completo |
| Cuotas (MonthGrid) | `/cards/:id/installments` | ✅ Completo |
| Préstamos | `/loans` | ✅ Completo |
| Servicios | `/services` | ✅ Completo |
| Dashboard Tarjetas+Servicios | `/dashboard` | ✅ Completo |
| Gastos diarios | `/expenses` | ✅ Recién implementado |
| Home (resumen parcial) | `/` | 🟡 Parcial — falta ingresos, sobra, gastos fijos |
| Gastos fijos (Cami/Miyu/Cash) | `/fixed-expenses` | ❌ Falta |
| Dashboard Mensual Consolidado | `/` o `/consolidado` | ❌ Falta |
| Ingresos / DualPay | `/settings` | ❌ Falta |
| "Copiar del mes anterior" | global | ❌ Falta |
| Atajos de teclado | global | ❌ Falta |
| Ajustes (Settings real) | `/settings` | ❌ Placeholder vacío |

---

## Acciones pendientes — priorizadas

---

### 🔴 PRIORIDAD 1 — Gastos Fijos Personales (módulo 3.6)

**Qué es:** Cami, Miyu, Cash, Saldo Impago, Gastos Crédito.
**Por qué ahora:** Es input necesario para el Dashboard Consolidado (prioridad 2).

#### Data model (nuevo slice en store)
```js
fixedExpenses: [
  {
    id: 'fx-miyu',
    label: 'Miyu',
    type: 'person',   // 'person' | 'cash' | 'debt' | 'other'
    icon: '👤',
    months: [
      { month: '2026-02', amount: 0, note: '' },
    ]
  },
  { id: 'fx-cami',  label: 'Cami',  type: 'person', ... },
  { id: 'fx-cash',  label: 'Cash',  type: 'cash',   ... },
]
```
- `setBudgetFixedExpense(id, month, amount, note)` — upsert por mes
- `getFixedExpenseTotal(month)` — suma del mes

#### Archivos a crear/modificar
| Archivo | Cambio |
|---------|--------|
| `useStore.js` | Agregar slice `fixedExpenses[]` + actions |
| `seedData.js` | Seed: Miyu, Cami, Cash, Saldo Impago, Gastos Crédito |
| `pages/fixed/FixedExpensesPage.jsx` | Lista editable inline + historial expandible |
| `pages/fixed/FixedExpenseForm.jsx` | SlideOver: label, tipo, ícono |
| `App.jsx` | Ruta `/fixed-expenses` |
| `Sidebar.jsx` | Nav item "Gastos Fijos" (📌) |
| `Shell.jsx` | Título de página |

#### UX
- Lista simple con inline-edit del monto del mes actual (click → input → Enter)
- Historial expandible por ítem (igual que LoansPage)
- Nota opcional por mes (ej: "Facu + médica")
- Total al pie

---

### 🔴 PRIORIDAD 2 — Dashboard Mensual Consolidado (módulo 3.9)

**Qué es:** La pantalla de "Estimaciones" del Excel. Resumen financiero completo con ingresos y cálculo de "sobra".
**Por qué ahora:** Es el módulo central de la app; lo que el usuario ve primero.

#### Decisión de ubicación
Actualmente `/` es un "Home" parcial. Opciones:
- **Opción A:** Reemplazar `/` por el Dashboard Consolidado completo.
- **Opción B:** El Home actual se convierte en una vista rápida y se agrega `/consolidado` como pantalla separada.

→ **Recomendación: Opción A** — reemplazar el Home con el Dashboard Consolidado completo.

#### Layout
```
┌─────────────────────────────────────────────────────┐
│ PRÉSTAMOS                          $XXX.XXX   ▼     │
│   Préstamo BBVA        $207.077    ✓ pagado          │
│   Préstamo Galicia     $350.232    pendiente         │
├─────────────────────────────────────────────────────┤
│ CUOTAS + SERVICIOS                $X.XXX.XXX   →    │  (link a /dashboard)
│   Tarjetas             $1.435.172                   │
│   Servicios indep.       $384.600                   │
├─────────────────────────────────────────────────────┤
│ GASTOS FIJOS                        $XXX.XXX   ▼    │
│   Miyu                      $0                      │
│   Cami                      $0                      │
│   Cash                 $600.000                     │
│   Gastos Crédito       $558.000 (est. variable)     │
├─────────────────────────────────────────────────────┤
│ GASTOS DIARIOS                      $XXX.XXX   →    │  (link a /expenses)
├─────────────────────────────────────────────────────┤
│ ══════════════════════════════════════════════════   │
│ TOTAL EGRESOS                     $X.XXX.XXX        │
├─────────────────────────────────────────────────────┤
│ INGRESOS (neto)                   $4.557.000  [✏]   │
│ SOBRA / FALTA                     $1.405.444  🟢    │
│ Equivalente USD CCL                   $702          │
│ Tipo de cambio CCL       $2.000   [✏]               │
└─────────────────────────────────────────────────────┘
```

#### Nuevo slice `income` en store
```js
income: {
  netMonthly: 0,           // sueldo neto editable
  usdRateOfficial: 0,
  usdRateCCL: 0,
  splitPercent: 70,        // DualPay: % del ingreso que va a gastos
}
// actions: setIncome(data)
```

#### Archivos a crear/modificar
| Archivo | Cambio |
|---------|--------|
| `useStore.js` | Agregar slice `income` + `setIncome` |
| `seedData.js` | Seed: income con valores de ejemplo |
| `pages/Home.jsx` | **Reescribir completo** como Dashboard Consolidado |

---

### 🟡 PRIORIDAD 3 — "Copiar del mes anterior"

**Qué es:** Al cambiar a un mes sin datos (→ mes futuro), ofrecer copiar estructura del mes anterior.
**Por qué:** Flujo principal de uso (UC-03 del spec). Evita re-ingresar todo.

#### Lógica
Cuando `currentMonth` cambia a un mes que no tiene ningún dato de `fixedExpenses` ni `budgets`:
- Mostrar un banner/prompt: **"¿Copiar estimaciones de Febrero 2026 para Marzo 2026?"**
- Si acepta: copiar `fixedExpenses.months` + `budgets` + `services.amounts` (el último valor conocido)
- **No** copia gastos diarios (son datos reales, no estimaciones)

#### Archivos a crear/modificar
| Archivo | Cambio |
|---------|--------|
| `useStore.js` | Action `copyMonthData(fromMonth, toMonth)` |
| `components/ui/CopyMonthBanner.jsx` | Banner/prompt colapsable en el Dashboard |
| `pages/Home.jsx` | Detectar si el mes está "vacío" y mostrar el banner |

---

### 🟡 PRIORIDAD 4 — Ajustes / Settings real

**Qué es:** Reemplazar el placeholder actual de `/settings` con configuración real.

#### Secciones
1. **Ingresos** — neto mensual, USD oficial/CCL, splitPercent (puede ir inline en el Dashboard, ver prioridad 2)
2. **Preferencias** — (futuro) tema, moneda

#### Archivos a modificar
| Archivo | Cambio |
|---------|--------|
| `App.jsx` (Settings component) | Reemplazar placeholder por `SettingsPage` real |
| `pages/settings/SettingsPage.jsx` | Crear con sección de ingresos y tipo de cambio |

---

### 🟢 PRIORIDAD 5 — Atajos de teclado globales

**Qué es:** Accesos rápidos del spec — `N`, `←`/`→`, `Ctrl+S`, `Escape`.

#### Implementación
Hook global `useKeyboardShortcuts()` montado en `Shell.jsx`:

| Tecla | Acción | Contexto |
|-------|--------|---------|
| `N` | Abrir "Nueva cuota / Nuevo gasto" | Según ruta activa |
| `←` / `→` | Cambiar mes anterior / siguiente | Global |
| `Ctrl+S` | Exportar JSON (guardar) | Global |
| `Escape` | Cerrar SlideOver activo | Global |

#### Archivos a crear
| Archivo | Cambio |
|---------|--------|
| `hooks/useKeyboardShortcuts.js` | Hook con listeners de `keydown` |
| `components/layout/Shell.jsx` | Montar el hook |
| `components/ui/MonthSelector.jsx` | Exponer handlers para ← → |

---

### 🟢 PRIORIDAD 6 — UX improvements en Gastos diarios

**Pendientes del spec (módulo 3.7):**

| Feature | Descripción |
|---------|-------------|
| **Barra de carga rápida siempre visible** | Input inline en el sub-header (fecha, descripción, categoría, monto, medio) sin abrir SlideOver |
| **Filtros rápidos** | Por categoría, por medio de pago, por semana |
| **Buscador inline** | Filtrar por texto en tiempo real |
| **Panel de resumen sticky** | Total Crédito, Total Débito, Total por banco, Total por categoría principal (como el panel lateral del Excel) |

#### Archivos a modificar
| Archivo | Cambio |
|---------|--------|
| `pages/expenses/ExpensesPage.jsx` | Agregar QuickExpenseBar inline + filtros + buscador |

---

### 🔵 PRIORIDAD 7 — Indicadores visuales en Tarjetas

**Del spec (3.2):** Indicator dot verde/rojo en cada tarjeta mostrando si hay cuotas pendientes este mes.

#### Archivos a modificar
| Archivo | Cambio |
|---------|--------|
| `pages/cards/CardsPage.jsx` | Agregar dot de estado en cada card |

---

## Resumen de orden de implementación

```
1. FixedExpensesPage    (Prioridad 1)  — nuevo módulo + store
2. Home.jsx rewrite     (Prioridad 2)  — Dashboard Consolidado con ingresos
3. income slice         (Prioridad 2)  — store + seed
4. CopyMonthBanner      (Prioridad 3)  — UX nuevo mes
5. SettingsPage         (Prioridad 4)  — reemplazar placeholder
6. useKeyboardShortcuts (Prioridad 5)  — hook global
7. Gastos UX           (Prioridad 6)  — QuickBar + filtros
8. Cards indicators     (Prioridad 7)  — dot verde/rojo
```

---

## Archivos nuevos totales a crear

```
src/
├── hooks/
│   └── useKeyboardShortcuts.js
├── pages/
│   ├── fixed/
│   │   ├── FixedExpensesPage.jsx
│   │   └── FixedExpenseForm.jsx
│   └── settings/
│       └── SettingsPage.jsx
└── components/ui/
    └── CopyMonthBanner.jsx
```

## Archivos a modificar

```
src/store/useStore.js         — slices fixedExpenses + income
src/store/seedData.js         — seed de fixedExpenses + income
src/pages/Home.jsx            — reescritura completa (Dashboard Consolidado)
src/pages/expenses/ExpensesPage.jsx  — QuickBar + filtros
src/pages/cards/CardsPage.jsx        — dot de estado
src/components/layout/Shell.jsx      — montar keyboard shortcuts
src/components/ui/MonthSelector.jsx  — exponer ← → para shortcuts
src/App.jsx                   — ruta /fixed-expenses, /settings real
src/components/layout/Sidebar.jsx    — nav item Gastos Fijos
src/components/layout/Shell.jsx      — título Gastos Fijos
```
