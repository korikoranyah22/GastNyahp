# Plan: Gastos Diarios

## Decisiones de diseño

| Campo | Decisión |
|-------|----------|
| Granularidad | Transacciones individuales (una fila = un gasto) |
| Categorías | Comida, Vicios, Hogar, Salud, Higiene, Transporte, Servicios, Salidas, Limpieza, Ropa, Desconocido |
| Medio de pago | Tarjetas de crédito del store + Débito por banco + Efectivo |
| Vista principal | Lista por día, agrupada en semanas (acordeón) |
| Presupuesto | 3 metas por mes: Crédito, Débito/Efectivo, Semanal |

---

## Data Model

```js
// Expense
{
  id: 'exp-...',
  date: '2026-02-24',        // YYYY-MM-DD
  description: 'Coto',
  category: 'Comida',        // enum de categorías
  amount: 103051,            // Precio real del ítem
  paymentMethod: string,     // 'card-visa-galicia' | 'debit-bank-bbva' | 'cash'
}

// Budget (objeto keyed por mes, no array)
budgets: {
  '2026-02': {
    creditLimit: 480000,     // Meta Crédito
    debitCashLimit: 316000,  // Meta Débito/Efectivo
    weeklyLimit: 200000,     // Meta semanal
  }
}
```

### Medios de pago (helpers)
- **Crédito**: `paymentMethod` es un `cardId` de las tarjetas del store (type: 'credit')
- **Débito**: `paymentMethod` es `'debit-{bankId}'` (ej: `'debit-bank-bbva'`)
- **Efectivo**: `paymentMethod === 'cash'`

---

## Categorías y emojis

| Categoría | Emoji |
|-----------|-------|
| Comida | 🛒 |
| Vicios | 🍷 |
| Hogar | 🏠 |
| Salud | 💊 |
| Higiene | 🧴 |
| Transporte | 🚌 |
| Servicios | ⚡ |
| Salidas | 🎭 |
| Limpieza | 🧹 |
| Ropa | 👗 |
| Desconocido | ❓ |

---

## Semanas del mes

| Semana | Días |
|--------|------|
| Semana 1 | 1 al 7 |
| Semana 2 | 8 al 14 |
| Semana 3 | 15 al 22 |
| Semana 4 | 23 al 31 |

Función helper: `getWeek(date: string): 1 | 2 | 3 | 4`

---

## Layout: ExpensesPage

```
┌──────────────────────────────────────────────────────────┐
│ Sub-header                                    [+ Gasto]  │
│ Crédito   ██████████░░░░  $480k / $1.0M      [Presupuesto]
│ Déb/Efvo  ████░░░░░░░░░░  $120k / $316k                  │
│ Semanal   ████████░░░░░░  $160k / $200k (sem actual)     │
├──────────────────────────────────────────────────────────┤
│ ▼ Semana 1 · 1 al 7         $38.000  ↑12%               │
│   ┌─────────────────────────────────────────────────┐   │
│   │ ▼ Lun 3/Feb                         $38.000     │   │
│   │   Coto           Comida  VISA GAL   $30.000  🗑  │   │
│   │   Verdulería     Comida  Déb BBVA    $8.000  🗑  │   │
│   └─────────────────────────────────────────────────┘   │
│                                                          │
│ ▼ Semana 2 · 8 al 14        $54.000  ↓5%                │
│   ▶ Mar 9/Feb                         $20.000            │
│   ▼ Vie 12/Feb                        $34.000            │
│     Farmacia       Salud   Déb BBVA   $34.000  🗑        │
│                                                          │
│ [resumen por categoría — expandible]                     │
│   Comida $340k · Vicios $45k · Transporte $28k · ...    │
└──────────────────────────────────────────────────────────┘
```

---

## Archivos a crear/modificar

### 1. `src/store/useStore.js` ← MODIFICAR
Agregar slices:
```js
expenses: seedData.expenses,          // array
budgets: seedData.budgets,            // objeto { [month]: {creditLimit, debitCashLimit, weeklyLimit} }

addExpense(exp)                        // genera id, pushes
updateExpense(id, data)
deleteExpense(id)
setBudget(month, budgetData)           // upsert

// Helpers
getMonthExpenses(month)               // expenses filtradas por date.startsWith(month)
getMonthExpenseTotal(month)           // suma de amounts
getMonthCreditTotal(month)            // suma donde paymentMethod es cardId (crédito)
getMonthDebitCashTotal(month)         // suma donde paymentMethod es 'debit-*' o 'cash'
getWeekExpenseTotal(month, week)      // suma de semana 1-4 (usando getWeek helper)
```

### 2. `src/store/seedData.js` ← MODIFICAR
Agregar:
- ~25 gastos de ejemplo para Feb/2026 (variados: comida, transporte, salud)
- Budget de ejemplo para Feb/2026: `{ creditLimit: 480000, debitCashLimit: 316000, weeklyLimit: 200000 }`

### 3. `src/pages/expenses/expensesConfig.js` ← CREAR
```js
export const EXPENSE_CATEGORIES = [
  { value: 'Comida', icon: '🛒' },
  { value: 'Vicios', icon: '🍷' },
  // ...10 categorías total
]

export const WEEK_RANGES = [
  { key: 1, label: '1 al 7',   days: [1, 7]  },
  { key: 2, label: '8 al 14',  days: [8, 14] },
  { key: 3, label: '15 al 22', days: [15, 22]},
  { key: 4, label: '23 al 31', days: [23, 31]},
]

export function getWeek(dateStr)   // '2026-02-15' → 3
export function getPaymentLabel(paymentMethod, cards, banks)  // 'Visa Galicia'
export function isCredit(paymentMethod, creditCards)           // true si es cardId
```

### 4. `src/pages/expenses/ExpenseForm.jsx` ← CREAR
SlideOver con:
- **Fecha**: date input (default hoy)
- **Descripción**: text input con autocompletar de descripciones previas del mes
- **Categoría**: grilla 4×3 de chips con emoji (selección única)
- **Precio**: AmountInput
- **Medio de pago**: lista de opciones agrupadas:
  - Tarjetas de crédito (del store)
  - Débito BBVA / Débito Galicia (uno por banco)
  - Efectivo

### 5. `src/pages/expenses/BudgetModal.jsx` ← CREAR
SlideOver simple con 3 campos numéricos:
- Meta Crédito del mes
- Meta Débito/Efectivo del mes
- Meta semanal

### 6. `src/pages/expenses/ExpensesPage.jsx` ← CREAR (archivo principal)
Componentes internos:
- `BudgetBar` — barra de progreso con label, gasto/meta, % color (verde→amarillo→rojo)
- `WeekGroup` — acordeón de semana con subtotal, delta vs semana anterior
- `DayGroup` — acordeón de día dentro de la semana
- `ExpenseRow` — fila de transacción: descripción, categoría badge, medio badge, monto, botón edit/delete
- `CategorySummary` — panel expansible al final: chips por categoría con monto del mes

### 7. `src/pages/Home.jsx` ← MODIFICAR
- Agregar bloque "Gastos del mes" en el summary grid (pasa a ser 5 bloques, o reemplaza el gran total)
- Mostrar total de gastos diarios + link a `/expenses`

### 8. `src/App.jsx` ← MODIFICAR
```jsx
<Route path="/expenses" element={<ExpensesPage />} />
```

### 9. `src/components/layout/Sidebar.jsx` ← MODIFICAR
- Agregar nav item: `{ to: '/expenses', label: 'Gastos', icon: ShoppingCart }`

### 10. `src/components/layout/Shell.jsx` ← MODIFICAR
- Agregar `'/expenses': 'Gastos'` al mapa de títulos

---

## Orden de implementación

1. `expensesConfig.js` — constants y helpers
2. `useStore.js` — slices + actions + helpers
3. `seedData.js` — datos de prueba
4. `ExpenseForm.jsx` — formulario de alta/edición
5. `BudgetModal.jsx` — formulario de presupuestos
6. `ExpensesPage.jsx` — página principal
7. `Home.jsx` — bloque en el resumen
8. `App.jsx` + `Sidebar.jsx` + `Shell.jsx` — rutas y nav

---

## Notas técnicas
- `expenses` se almacena como array plano (no agrupado) → el agrupamiento se hace en el componente con `useMemo`
- `budgets` es un objeto `{ [month]: {...} }` para lookup O(1)
- El helper `getWeek(dateStr)` devuelve 1-4 basado en el día del mes
- El medio de pago se muestra como chip de color: crédito = azul/rojo (color de la tarjeta), débito = verde, efectivo = amarillo
- Las semanas vacías (sin gastos) NO se muestran
- Click en una fila de gasto → abre `ExpenseForm` en modo edición
