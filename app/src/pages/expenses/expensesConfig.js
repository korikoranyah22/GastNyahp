// ─── Categorías universales (gastos, cuotas, tickets) ────────────────────────
export const APP_CATEGORIES = [
  { value: 'Comida',      icon: '🛒' },
  { value: 'Delivery',    icon: '🛵' },
  { value: 'Vicios',      icon: '🍷' },
  { value: 'Salidas',     icon: '🎭' },
  { value: 'Hogar',       icon: '🏠' },
  { value: 'Limpieza',    icon: '🧹' },
  { value: 'Salud',       icon: '💊' },
  { value: 'Higiene',     icon: '🧴' },
  { value: 'Transporte',  icon: '🚌' },
  { value: 'Servicios',   icon: '⚡' },
  { value: 'Ropa',        icon: '👗' },
  { value: 'Educación',   icon: '🎓' },
  { value: 'Electrónica', icon: '📱' },
  { value: 'Mascotas',    icon: '🐾' },
  { value: 'Perfumes',    icon: '🌸' },
  { value: 'Desconocido', icon: '❓' },
]

// Alias para compatibilidad con imports existentes
export const EXPENSE_CATEGORIES = APP_CATEGORIES

export function getCategoryIcon(category) {
  return APP_CATEGORIES.find((c) => c.value === category)?.icon || '📌'
}

// ─── Semanas del mes ───────────────────────────────────────────────────────────
export const WEEK_RANGES = [
  { key: 1, label: '1 al 7',   from: 1,  to: 7  },
  { key: 2, label: '8 al 14',  from: 8,  to: 14 },
  { key: 3, label: '15 al 22', from: 15, to: 22 },
  { key: 4, label: '23 al 31', from: 23, to: 31 },
]

/** Devuelve 1-4 según el día del mes */
export function getWeek(dateStr) {
  const day = parseInt(dateStr.slice(8, 10), 10)
  if (day <= 7)  return 1
  if (day <= 14) return 2
  if (day <= 22) return 3
  return 4
}

/** Filtra expenses por semana */
export function filterByWeek(expenses, week) {
  return expenses.filter((e) => getWeek(e.date) === week)
}

// ─── Medios de pago ────────────────────────────────────────────────────────────

/** Genera la lista completa de medios disponibles a partir del store */
export function buildPaymentMethods(creditCards, banks) {
  const methods = []

  // Tarjetas de crédito
  if (creditCards.length > 0) {
    methods.push({ group: 'Crédito', items: creditCards.map((c) => ({
      value: c.id,
      label: c.label,
      color: c.color,
      type: 'credit',
    })) })
  }

  // Débito por banco
  if (banks.length > 0) {
    methods.push({ group: 'Débito', items: banks.map((b) => ({
      value: `debit-${b.id}`,
      label: `Débito ${b.name}`,
      color: b.color,
      type: 'debit',
    })) })
  }

  // Efectivo
  methods.push({ group: 'Efectivo', items: [
    { value: 'cash', label: 'Efectivo', color: '#22c55e', type: 'cash' },
  ]})

  // Pagos digitales
  methods.push({ group: 'Digital', items: [
    { value: 'modo',         label: 'MODO',         color: '#6366f1', type: 'digital' },
    { value: 'mercadopago',  label: 'MercadoPago',  color: '#00b1ea', type: 'digital' },
  ]})

  return methods
}

/** Etiqueta corta para mostrar en una fila */
export function getPaymentLabel(paymentMethod, creditCards, banks) {
  if (!paymentMethod) return '—'
  if (paymentMethod === 'cash') return 'Efectivo'
  if (paymentMethod === 'modo') return 'MODO'
  if (paymentMethod === 'mercadopago') return 'MercadoPago'
  if (paymentMethod.startsWith('debit-')) {
    const bankId = paymentMethod.replace('debit-', '')
    const bank = banks.find((b) => b.id === bankId)
    return bank ? `Déb. ${bank.name}` : 'Débito'
  }
  const card = creditCards.find((c) => c.id === paymentMethod)
  return card ? card.label : paymentMethod
}

/** Color del medio de pago */
export function getPaymentColor(paymentMethod, creditCards, banks) {
  if (!paymentMethod) return '#64748b'
  if (paymentMethod === 'cash') return '#22c55e'
  if (paymentMethod === 'modo') return '#6366f1'
  if (paymentMethod === 'mercadopago') return '#00b1ea'
  if (paymentMethod.startsWith('debit-')) {
    const bankId = paymentMethod.replace('debit-', '')
    return banks.find((b) => b.id === bankId)?.color || '#64748b'
  }
  return creditCards.find((c) => c.id === paymentMethod)?.color || '#64748b'
}

/** True si el medio es crédito (cardId de una tarjeta de crédito) */
export function isCredit(paymentMethod, creditCards) {
  return creditCards.some((c) => c.id === paymentMethod)
}

/** True si el medio es débito, efectivo o pago digital (no genera cuota) */
export function isDebitOrCash(paymentMethod) {
  return (
    paymentMethod === 'cash' ||
    paymentMethod === 'modo' ||
    paymentMethod === 'mercadopago' ||
    paymentMethod?.startsWith('debit-')
  )
}

// ─── Billing cycle ─────────────────────────────────────────────────────────────

/**
 * Calcula el mes de cierre al que pertenece un gasto de tarjeta.
 * Si el día del gasto es MAYOR al día de cierre, el gasto va al cierre del MES SIGUIENTE.
 * Devuelve 'YYYY-MM'.
 * Ej: cierre día 15, gasto el 20 de febrero → "2026-03" (cierre de marzo).
 *     cierre día 15, gasto el 10 de febrero → "2026-02" (cierre de febrero).
 */
export function getBillingMonth(dateStr, closingDay) {
  if (!dateStr || !closingDay) return dateStr?.slice(0, 7) || ''
  const day = parseInt(dateStr.slice(8, 10), 10)
  const y   = parseInt(dateStr.slice(0, 4), 10)
  const m   = parseInt(dateStr.slice(5, 7), 10)
  if (day > closingDay) {
    if (m === 12) return `${y + 1}-01`
    return `${y}-${String(m + 1).padStart(2, '0')}`
  }
  return `${y}-${String(m).padStart(2, '0')}`
}

/**
 * Calcula el mes en que se PAGA el resumen de tarjeta.
 * Si dueDay < closingDay → el vencimiento cae el mes siguiente al cierre (caso típico en Argentina).
 * Si dueDay >= closingDay → el vencimiento cae el mismo mes del cierre.
 * Si no hay dueDay → retorna el mes de cierre.
 */
export function getPaymentMonth(dateStr, closingDay, dueDay) {
  const billingYM = getBillingMonth(dateStr, closingDay)
  if (!dueDay || dueDay >= closingDay) return billingYM
  const y = parseInt(billingYM.slice(0, 4), 10)
  const m = parseInt(billingYM.slice(5, 7), 10)
  return m === 12 ? `${y + 1}-01` : `${y}-${String(m + 1).padStart(2, '0')}`
}

// ─── Helpers de fecha ──────────────────────────────────────────────────────────

/** 'YYYY-MM-DD' → 'Lun 24/Feb' */
export function formatDay(dateStr) {
  const d = new Date(dateStr + 'T12:00:00')
  const weekday = d.toLocaleDateString('es-AR', { weekday: 'short' })
  const day = d.getDate()
  const month = d.toLocaleDateString('es-AR', { month: 'short' })
  return `${cap(weekday)} ${day}/${cap(month)}`
}

function cap(str) {
  return str.charAt(0).toUpperCase() + str.slice(1).replace('.', '')
}

/** Hoy en formato YYYY-MM-DD */
export function todayStr() {
  return new Date().toISOString().slice(0, 10)
}

/** Mes actual de una fecha YYYY-MM-DD → 'YYYY-MM' */
export function dateToMonth(dateStr) {
  return dateStr.slice(0, 7)
}

// ─── Ticket helpers ────────────────────────────────────────────────────────────

/**
 * Monto efectivo de un gasto (regular o ticket).
 * Para tickets: suma de ítems menos descuento.
 * Para gastos regulares: expense.amount.
 */
export function getExpenseAmount(expense) {
  if (expense.type !== 'ticket') return expense.amount ?? 0
  const subtotal = (expense.items || []).reduce((s, it) => s + (it.amount || 0), 0)
  return Math.max(0, subtotal - (expense.discount || 0))
}
